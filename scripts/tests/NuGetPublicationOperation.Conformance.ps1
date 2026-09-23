Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot "scripts/NuGetPublicationOperation.ps1")

$packageIds = @(
    "PulseStack.Abstractions",
    "PulseStack.Core",
    "PulseStack.Agents",
    "PulseStack.Tools",
    "PulseStack.Providers.OpenAI",
    "PulseStack.Providers.AzureOpenAI",
    "PulseStack.Providers.Ollama",
    "PulseStack.Providers.Gemini",
    "PulseStack.Providers.Groq",
    "PulseStack.Providers.OpenRouter"
)
$version = "1.0.4-conformance.1"
$serviceIndex = "https://unit.test/v3/index.json"
$publishEndpoint = "https://publish.unit.test/api/v2/package"
$secret = "super-secret-conformance-key"

function Assert-Equal($Expected, $Actual, [string] $Message) {
    if ($Expected -cne $Actual) { throw "$Message Expected '$Expected', actual '$Actual'." }
}
function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}
function Invoke-Case {
    param([string]$Id,[string]$Name,[scriptblock]$Body)
    try { & $Body; [pscustomobject]@{Id=$Id;Name=$Name;Outcome="PASS"} }
    catch { throw "$Id $Name failed: $($_.Exception.Message)" }
}
function New-AdmittedSet {
    param([string] $PathRoot = "C:\fixture")
    [pscustomobject]@{
        SourceCommit = "0123456789abcdef0123456789abcdef01234567"
        VersionPrefix = "1.0.4"
        PackageVersion = $version
        ReleaseAuthorityTag = "v$version"
        Packages = @($packageIds | ForEach-Object {
            [pscustomobject]@{
                Id = $_
                Version = $version
                FilePath = Join-Path $PathRoot "$($_).$version.nupkg"
                Sha256 = ("a" * 64)
            }
        })
    }
}
function New-Preflight {
    param([string] $State="AllAbsent", [object] $Admitted=(New-AdmittedSet))
    [pscustomobject]@{
        State = $State
        Packages = @($Admitted.Packages | ForEach-Object {
            [pscustomobject]@{ Id=$_.Id; Version=$_.Version; State="Absent"; StatusCode=404; Diagnostic=$null }
        })
    }
}
function New-ServiceIndexResponse {
    param([switch] $MissingPublish)
    $resources = if ($MissingPublish) {
        @([ordered]@{ "@id"="https://unit.test/flat/"; "@type"="PackageBaseAddress/3.0.0" })
    } else {
        @([ordered]@{ "@id"=$publishEndpoint; "@type"="PackagePublish/2.0.0" })
    }
    [pscustomobject]@{
        StatusCode=200
        Content=([ordered]@{version="3.0.0";resources=$resources}|ConvertTo-Json -Depth 5 -Compress)
    }
}
function New-Discovery {
    param([System.Collections.Generic.List[string]] $Calls, [switch]$MissingPublish)
    return {
        param($uri)
        if ($null -ne $Calls) { $Calls.Add("GET $uri") }
        New-ServiceIndexResponse -MissingPublish:$MissingPublish
    }.GetNewClosure()
}
function New-CredentialAvailability {
    param([bool]$Available=$true,[System.Collections.Generic.List[string]]$Calls)
    return {
        if ($null -ne $Calls) { $Calls.Add("availability") }
        return $Available
    }.GetNewClosure()
}
function New-CredentialAcquire {
    param([System.Collections.Generic.List[string]]$Calls,[switch]$Throw)
    return {
        if ($null -ne $Calls) { $Calls.Add("acquire") }
        if ($Throw) { throw "credential acquisition failed" }
        return $secret
    }.GetNewClosure()
}
function New-PublishTransport {
    param(
        [int[]]$Statuses=@(201,201,201,201,201,201,201,201,201,201),
        [int[]]$ThrowAt=@(),
        [System.Collections.Generic.List[object]]$Calls
    )
    $state=[pscustomobject]@{Index=0;Statuses=$Statuses;ThrowAt=$ThrowAt;Calls=$Calls}
    return {
        param($endpoint,$filePath,$apiKey)
        $i=$state.Index; $state.Index++
        if($null -ne $state.Calls){$state.Calls.Add([pscustomobject]@{Index=$i;Endpoint=$endpoint;FilePath=$filePath;ApiKey=$apiKey;Method="PUT"})}
        if($state.ThrowAt -contains $i){throw "simulated transport uncertainty"}
        [pscustomobject]@{StatusCode=[int]$state.Statuses[$i]}
    }.GetNewClosure()
}
function New-Clock {
    $state=[pscustomobject]@{Tick=0}
    return {
        $state.Tick++
        [DateTimeOffset]::Parse("2026-09-23T12:00:00Z").AddSeconds($state.Tick)
    }.GetNewClosure()
}
function New-OperationIdFactory([string]$Id="00000000-0000-0000-0000-000000000001") { return { $Id }.GetNewClosure() }
function New-TempEvidenceRoot {
    $path=Join-Path ([System.IO.Path]::GetTempPath()) ("pulsestack-rp3a-"+[guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    return $path
}
function Invoke-Operation {
    param(
        [object]$Admitted=(New-AdmittedSet),
        [object]$Preflight,
        [string]$EvidenceRoot,
        [scriptblock]$DiscoveryRequest,
        [scriptblock]$CredentialAvailable,
        [scriptblock]$AcquireCredential,
        [scriptblock]$PublishRequest,
        [scriptblock]$WriteLedger=${function:Write-AtomicPublicationLedger},
        [scriptblock]$AfterAttemptingPersisted=$null,
        [string]$OperationId="00000000-0000-0000-0000-000000000001"
    )
    if($null -eq $Preflight){$Preflight=New-Preflight -Admitted $Admitted}
    if($null -eq $EvidenceRoot){$EvidenceRoot=New-TempEvidenceRoot}
    if($null -eq $DiscoveryRequest){$DiscoveryRequest=New-Discovery}
    if($null -eq $CredentialAvailable){$CredentialAvailable=New-CredentialAvailability}
    if($null -eq $AcquireCredential){$AcquireCredential=New-CredentialAcquire}
    if($null -eq $PublishRequest){$PublishRequest=New-PublishTransport}
    Invoke-NuGetPublicationOperation -AdmittedPackageSet $Admitted -PreflightResult $Preflight -EvidenceRoot $EvidenceRoot -CredentialAvailable $CredentialAvailable -AcquireCredential $AcquireCredential -DiscoveryRequest $DiscoveryRequest -PublishRequest $PublishRequest -WriteLedger $WriteLedger -OperationIdFactory (New-OperationIdFactory $OperationId) -Clock (New-Clock) -ServiceIndexUri $serviceIndex -AfterAttemptingPersisted $AfterAttemptingPersisted
}

$results=@()

$results += Invoke-Case "M01" "ten accepted outcomes complete" {
    $r=Invoke-Operation
    Assert-Equal "Complete" $r.Result.operationConclusion "Conclusion."
    Assert-Equal 10 $r.Result.knownAcceptedCount "Accepted count."
    Assert-Equal "Terminal" $r.Result.ledgerState "Ledger state."
}
$results += Invoke-Case "M02" "first rejection stops" {
    $calls=[System.Collections.Generic.List[object]]::new()
    $r=Invoke-Operation -PublishRequest (New-PublishTransport -Statuses @(400) -Calls $calls)
    Assert-Equal "StoppedRejected" $r.Result.operationConclusion "Conclusion."
    Assert-Equal 1 $r.Result.knownRejectedCount "Rejected count."
    Assert-Equal 9 $r.Result.notAttemptedCount "NotAttempted count."
    Assert-Equal 1 $calls.Count "Transport count."
}
$results += Invoke-Case "M03" "three accepted then rejected" {
    $calls=[System.Collections.Generic.List[object]]::new()
    $r=Invoke-Operation -PublishRequest (New-PublishTransport -Statuses @(201,201,202,400) -Calls $calls)
    Assert-Equal 3 $r.Result.knownAcceptedCount "Accepted count."
    Assert-Equal "StoppedRejected" $r.Result.operationConclusion "Conclusion."
    Assert-Equal 4 $calls.Count "Transport count."
}
$results += Invoke-Case "M04" "first uncertainty stops" {
    $calls=[System.Collections.Generic.List[object]]::new()
    $r=Invoke-Operation -PublishRequest (New-PublishTransport -ThrowAt @(0) -Calls $calls)
    Assert-Equal "StoppedIndeterminate" $r.Result.operationConclusion "Conclusion."
    Assert-Equal 1 $r.Result.indeterminateCount "Indeterminate count."
}
$results += Invoke-Case "M05" "three accepted then indeterminate" {
    $calls=[System.Collections.Generic.List[object]]::new()
    $r=Invoke-Operation -PublishRequest (New-PublishTransport -Statuses @(201,201,201,201) -ThrowAt @(3) -Calls $calls)
    Assert-Equal 3 $r.Result.knownAcceptedCount "Accepted count."
    Assert-Equal "StoppedIndeterminate" $r.Result.operationConclusion "Conclusion."
    Assert-Equal 4 $calls.Count "Transport count."
}
$results += Invoke-Case "M06" "409 is rejected conflict" {
    $r=Invoke-Operation -PublishRequest (New-PublishTransport -Statuses @(409))
    Assert-Equal "Rejected" $r.Result.packages[0].mutationState "Mutation state."
    Assert-Equal "ExistingIdentityConflict" $r.Result.packages[0].diagnostic.Code "Diagnostic."
}
$results += Invoke-Case "M07" "correspondence mismatch has zero mutation-side activity" {
    $admitted=New-AdmittedSet; $preflight=New-Preflight -Admitted $admitted; $preflight.Packages[2].Version="wrong"
    $events=[System.Collections.Generic.List[string]]::new(); $puts=[System.Collections.Generic.List[object]]::new(); $failed=$false
    try{Invoke-Operation -Admitted $admitted -Preflight $preflight -DiscoveryRequest (New-Discovery -Calls $events) -CredentialAvailable (New-CredentialAvailability -Calls $events) -AcquireCredential (New-CredentialAcquire -Calls $events) -PublishRequest (New-PublishTransport -Calls $puts)|Out-Null}catch{$failed=$true}
    Assert-True $failed "Expected failure."; Assert-Equal 0 $events.Count "Mutation-side events."; Assert-Equal 0 $puts.Count "PUTs."
}
$results += Invoke-Case "M08" "non-AllAbsent preflight has zero mutation activity" {
    $admitted=New-AdmittedSet; $preflight=New-Preflight -State "Mixed" -Admitted $admitted; $puts=[System.Collections.Generic.List[object]]::new(); $failed=$false
    try{Invoke-Operation -Admitted $admitted -Preflight $preflight -PublishRequest (New-PublishTransport -Calls $puts)|Out-Null}catch{$failed=$true}
    Assert-True $failed "Expected failure."; Assert-Equal 0 $puts.Count "PUTs."
}
$results += Invoke-Case "M09" "PackagePublish discovery failure creates no ledger or PUT" {
    $root=New-TempEvidenceRoot; $puts=[System.Collections.Generic.List[object]]::new(); $failed=$false
    try{Invoke-Operation -EvidenceRoot $root -DiscoveryRequest (New-Discovery -MissingPublish) -PublishRequest (New-PublishTransport -Calls $puts)|Out-Null}catch{$failed=$true}
    Assert-True $failed "Expected failure."; Assert-Equal 0 $puts.Count "PUTs."; Assert-True (-not (Get-ChildItem $root -Recurse -Filter publication-result.json -ErrorAction SilentlyContinue)) "Ledger must not exist."
}
$results += Invoke-Case "M10" "credential unavailable creates no ledger or PUT" {
    $root=New-TempEvidenceRoot; $puts=[System.Collections.Generic.List[object]]::new(); $failed=$false
    try{Invoke-Operation -EvidenceRoot $root -CredentialAvailable (New-CredentialAvailability -Available:$false) -PublishRequest (New-PublishTransport -Calls $puts)|Out-Null}catch{$failed=$true}
    Assert-True $failed "Expected failure."; Assert-Equal 0 $puts.Count "PUTs."; Assert-True (-not (Get-ChildItem $root -Recurse -Filter publication-result.json -ErrorAction SilentlyContinue)) "Ledger must not exist."
}
$results += Invoke-Case "M11" "exact admitted package path reaches transport" {
    $admitted=New-AdmittedSet -PathRoot "C:\exact"; $calls=[System.Collections.Generic.List[object]]::new()
    $null=Invoke-Operation -Admitted $admitted -Preflight (New-Preflight -Admitted $admitted) -PublishRequest (New-PublishTransport -Calls $calls)
    Assert-Equal $admitted.Packages[0].FilePath $calls[0].FilePath "Package path."
}
$results += Invoke-Case "M12" "mutation order preserves admitted order" {
    $admitted=New-AdmittedSet; $calls=[System.Collections.Generic.List[object]]::new(); $null=Invoke-Operation -Admitted $admitted -Preflight (New-Preflight -Admitted $admitted) -PublishRequest (New-PublishTransport -Calls $calls)
    for($i=0;$i -lt 10;$i++){Assert-Equal $admitted.Packages[$i].FilePath $calls[$i].FilePath "Order $i."}
}
$results += Invoke-Case "M13" "secret absent from durable evidence" {
    $r=Invoke-Operation; $json=Get-Content -LiteralPath $r.LedgerPath -Raw
    Assert-True (-not $json.Contains($secret,[System.StringComparison]::Ordinal)) "Secret leaked to ledger."
}
$results += Invoke-Case "M14" "discovery GET and publication PUT policy only" {
    $events=[System.Collections.Generic.List[string]]::new(); $puts=[System.Collections.Generic.List[object]]::new(); $null=Invoke-Operation -DiscoveryRequest (New-Discovery -Calls $events) -PublishRequest (New-PublishTransport -Calls $puts)
    Assert-Equal "GET $serviceIndex" $events[0] "Discovery request."; Assert-True (@($puts|Where-Object Method -ne "PUT").Count -eq 0) "Non-PUT mutation method."
}
$results += Invoke-Case "M15" "Attempting is persisted before transport" {
    $sequence=[System.Collections.Generic.List[string]]::new()
    $writer={param($ledger,$path) $state=[string]$ledger.packages[0].mutationState; $sequence.Add("write:$state"); Write-AtomicPublicationLedger $ledger $path}.GetNewClosure()
    $transport={param($endpoint,$filePath,$apiKey) $sequence.Add("transport"); [pscustomobject]@{StatusCode=400}}.GetNewClosure()
    $null=Invoke-Operation -WriteLedger $writer -PublishRequest $transport
    $attempt=$sequence.IndexOf("write:Attempting"); $tx=$sequence.IndexOf("transport"); Assert-True ($attempt -ge 0 -and $tx -gt $attempt) "Attempting must persist before transport."
}
$results += Invoke-Case "M16" "interruption after durable Attempting preserves Attempting" {
    $root=New-TempEvidenceRoot; $puts=[System.Collections.Generic.List[object]]::new(); $interruption={param($ledger,$path,$index) throw "simulated hard interruption"}; $failed=$false
    try{Invoke-Operation -EvidenceRoot $root -AfterAttemptingPersisted $interruption -PublishRequest (New-PublishTransport -Calls $puts)|Out-Null}catch{$failed=$true}
    Assert-True $failed "Expected interruption."; Assert-Equal 0 $puts.Count "Transport must not run."; $file=Get-ChildItem $root -Recurse -Filter publication-result.json | Select-Object -First 1; $ledger=Get-Content $file.FullName -Raw|ConvertFrom-Json; Assert-Equal "Attempting" $ledger.packages[0].mutationState "Durable state."; Assert-Equal "InProgress" $ledger.ledgerState "Ledger lifecycle."
}
$results += Invoke-Case "M17" "transport exception becomes Indeterminate" {
    $r=Invoke-Operation -PublishRequest (New-PublishTransport -ThrowAt @(0)); Assert-Equal "Indeterminate" $r.Result.packages[0].mutationState "Mutation state."; Assert-Equal "TransportUncertainty" $r.Result.packages[0].diagnostic.Code "Diagnostic."
}
$results += Invoke-Case "M18" "Attempting persistence failure prevents transport" {
    $writes=[pscustomobject]@{Count=0}; $puts=[System.Collections.Generic.List[object]]::new()
    $writer={param($ledger,$path) $writes.Count++; if($writes.Count -eq 2){throw "simulated Attempting persistence failure"}; Write-AtomicPublicationLedger $ledger $path}.GetNewClosure(); $failed=$false
    try{Invoke-Operation -WriteLedger $writer -PublishRequest (New-PublishTransport -Calls $puts)|Out-Null}catch{$failed=$true}; Assert-True $failed "Expected write failure."; Assert-Equal 0 $puts.Count "Transport must not run."
}
$results += Invoke-Case "M19" "outcome persistence failure leaves Attempting authoritative" {
    $root=New-TempEvidenceRoot; $writes=[pscustomobject]@{Count=0}; $writer={param($ledger,$path) $writes.Count++; if($writes.Count -eq 3){throw "simulated outcome persistence failure"}; Write-AtomicPublicationLedger $ledger $path}.GetNewClosure(); $failed=$false
    try{Invoke-Operation -EvidenceRoot $root -WriteLedger $writer -PublishRequest (New-PublishTransport -Statuses @(201))|Out-Null}catch{$failed=$true}; Assert-True $failed "Expected write failure."; $file=Get-ChildItem $root -Recurse -Filter publication-result.json|Select-Object -First 1; $ledger=Get-Content $file.FullName -Raw|ConvertFrom-Json; Assert-Equal "Attempting" $ledger.packages[0].mutationState "Authoritative durable state."
}
$results += Invoke-Case "M20" "terminal operation id cannot be reopened" {
    $root=New-TempEvidenceRoot; $id="00000000-0000-0000-0000-000000000020"; $null=Invoke-Operation -EvidenceRoot $root -OperationId $id; $failed=$false
    try{Invoke-Operation -EvidenceRoot $root -OperationId $id|Out-Null}catch{$failed=$true}; Assert-True $failed "Existing terminal operation must not reopen."
}

$results | Format-Table Id,Name,Outcome -AutoSize
if($results.Count -ne 20 -or @($results|Where-Object Outcome -ne "PASS").Count -ne 0){throw "RP-3A conformance failed."}
Write-Host ""
Write-Host "RP-3A CONFORMANCE: 20 / 20 PASS"
