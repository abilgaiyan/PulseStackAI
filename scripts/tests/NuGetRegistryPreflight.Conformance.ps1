Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot "scripts/NuGetRegistryPreflight.ps1")

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
$discoveredBase = "https://discovered.unit.test/flat/"

function New-AdmittedSet {
    return [pscustomobject]@{
        SourceCommit        = "0123456789abcdef0123456789abcdef01234567"
        VersionPrefix       = "1.0.4"
        PackageVersion      = $version
        ReleaseAuthorityTag = "v$version"
        Packages            = @($packageIds | ForEach-Object {
            [pscustomobject]@{ Id=$_; Version=$version; FilePath="C:\fixture\$($_).$version.nupkg"; Sha256=("a"*64) }
        })
    }
}

function New-ServiceIndexContent {
    param([string] $BaseAddress = $discoveredBase, [switch] $WithoutPackageBaseAddress)
    $resources = if ($WithoutPackageBaseAddress) {
        @([ordered]@{ "@id"="https://unit.test/search"; "@type"="SearchQueryService/3.5.0" })
    } else {
        @([ordered]@{ "@id"=$BaseAddress; "@type"="PackageBaseAddress/3.0.0" })
    }
    return ([ordered]@{ version="3.0.0"; resources=$resources } | ConvertTo-Json -Depth 5 -Compress)
}

function New-FakeTransport {
    param(
        [int[]] $Statuses = @(404,404,404,404,404,404,404,404,404,404),
        [int[]] $ThrowAt = @(),
        [string] $ServiceContent = (New-ServiceIndexContent),
        [int] $ServiceStatus = 200,
        [System.Collections.Generic.List[object]] $Requests
    )

    $state = [pscustomobject]@{ Index=0; Statuses=$Statuses; ThrowAt=$ThrowAt; Requests=$Requests; ServiceContent=$ServiceContent; ServiceStatus=$ServiceStatus }
    return {
        param($request)
        if ($null -ne $state.Requests) { $state.Requests.Add($request) }
        if ($request.Method -eq "GET") {
            return [pscustomobject]@{ StatusCode=$state.ServiceStatus; Content=$state.ServiceContent; Headers=$null }
        }
        $i = $state.Index
        $state.Index++
        if ($state.ThrowAt -contains $i) { throw "simulated timeout $i" }
        return [pscustomobject]@{ StatusCode=[int]$state.Statuses[$i]; Content=$null; Headers=$null }
    }.GetNewClosure()
}

function Assert-Equal($Expected, $Actual, [string] $Message) {
    if ($Expected -cne $Actual) { throw "$Message Expected '$Expected', actual '$Actual'." }
}
function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}

function Invoke-Case {
    param([string]$Id,[string]$Name,[scriptblock]$Body)
    try {
        & $Body
        [pscustomobject]@{ Id=$Id; Name=$Name; Outcome="PASS" }
    }
    catch {
        throw "$Id $Name failed: $($_.Exception.Message)"
    }
}

$results = @()

$results += Invoke-Case "P01" "all 404 produces AllAbsent" {
    $r=Invoke-NuGetRegistryPreflight -AdmittedPackageSet (New-AdmittedSet) -Request (New-FakeTransport) -ServiceIndexUri $serviceIndex
    Assert-Equal "AllAbsent" $r.State "Aggregate state."
    Assert-True (@($r.Packages | Where-Object State -eq "Absent").Count -eq 10) "Expected ten Absent observations."
}

$results += Invoke-Case "P02" "all 200 produces AllPresent" {
    $r=Invoke-NuGetRegistryPreflight -AdmittedPackageSet (New-AdmittedSet) -Request (New-FakeTransport -Statuses @(200,200,200,200,200,200,200,200,200,200)) -ServiceIndexUri $serviceIndex
    Assert-Equal "AllPresent" $r.State "Aggregate state."
}

$results += Invoke-Case "P03" "200 and 404 produces Mixed" {
    $r=Invoke-NuGetRegistryPreflight -AdmittedPackageSet (New-AdmittedSet) -Request (New-FakeTransport -Statuses @(200,404,200,404,200,404,200,404,200,404)) -ServiceIndexUri $serviceIndex
    Assert-Equal "Mixed" $r.State "Aggregate state."
}

$results += Invoke-Case "P04" "timeout is Indeterminate and observation continues" {
    $requests=[System.Collections.Generic.List[object]]::new()
    $r=Invoke-NuGetRegistryPreflight -AdmittedPackageSet (New-AdmittedSet) -Request (New-FakeTransport -ThrowAt @(2) -Requests $requests) -ServiceIndexUri $serviceIndex
    Assert-Equal "Indeterminate" $r.State "Aggregate state."
    Assert-Equal "TransportFailure" $r.Packages[2].Diagnostic.Code "Diagnostic."
    Assert-True ($r.Packages.Count -eq 10 -and $requests.Count -eq 11) "All ten packages must still be observed."
}

$results += Invoke-Case "P05" "429 is Indeterminate RateLimited" {
    $s=@(404,404,429,404,404,404,404,404,404,404)
    $r=Invoke-NuGetRegistryPreflight -AdmittedPackageSet (New-AdmittedSet) -Request (New-FakeTransport -Statuses $s) -ServiceIndexUri $serviceIndex
    Assert-Equal "Indeterminate" $r.State "Aggregate state."
    Assert-Equal "RateLimited" $r.Packages[2].Diagnostic.Code "Diagnostic."
}

$results += Invoke-Case "P06" "5xx is Indeterminate ServerFailure" {
    $s=@(404,503,404,404,404,404,404,404,404,404)
    $r=Invoke-NuGetRegistryPreflight -AdmittedPackageSet (New-AdmittedSet) -Request (New-FakeTransport -Statuses $s) -ServiceIndexUri $serviceIndex
    Assert-Equal "Indeterminate" $r.State "Aggregate state."
    Assert-Equal "ServerFailure" $r.Packages[1].Diagnostic.Code "Diagnostic."
}

$results += Invoke-Case "P07" "malformed service index stops before packages" {
    $requests=[System.Collections.Generic.List[object]]::new()
    $r=Invoke-NuGetRegistryPreflight -AdmittedPackageSet (New-AdmittedSet) -Request (New-FakeTransport -ServiceContent "not-json" -Requests $requests) -ServiceIndexUri $serviceIndex
    Assert-Equal "Indeterminate" $r.State "Aggregate state."
    Assert-Equal "ServiceIndexInvalid" $r.Diagnostic.Code "Diagnostic."
    Assert-True ($r.Packages.Count -eq 0 -and $requests.Count -eq 1) "Discovery failure must issue zero package requests."
}

$results += Invoke-Case "P08" "missing PackageBaseAddress stops before packages" {
    $requests=[System.Collections.Generic.List[object]]::new()
    $content=New-ServiceIndexContent -WithoutPackageBaseAddress
    $r=Invoke-NuGetRegistryPreflight -AdmittedPackageSet (New-AdmittedSet) -Request (New-FakeTransport -ServiceContent $content -Requests $requests) -ServiceIndexUri $serviceIndex
    Assert-Equal "Indeterminate" $r.State "Aggregate state."
    Assert-Equal "PackageBaseAddressUnavailable" $r.Diagnostic.Code "Diagnostic."
    Assert-True ($r.Packages.Count -eq 0 -and $requests.Count -eq 1) "Discovery failure must issue zero package requests."
}

$results += Invoke-Case "P09" "unexpected status is Indeterminate" {
    $s=@(404,418,404,404,404,404,404,404,404,404)
    $r=Invoke-NuGetRegistryPreflight -AdmittedPackageSet (New-AdmittedSet) -Request (New-FakeTransport -Statuses $s) -ServiceIndexUri $serviceIndex
    Assert-Equal "Indeterminate" $r.State "Aggregate state."
    Assert-Equal "UnexpectedStatus" $r.Packages[1].Diagnostic.Code "Diagnostic."
}

$results += Invoke-Case "P10" "result preserves admitted package order" {
    $r=Invoke-NuGetRegistryPreflight -AdmittedPackageSet (New-AdmittedSet) -Request (New-FakeTransport -Statuses @(200,404,200,404,200,404,200,404,200,404)) -ServiceIndexUri $serviceIndex
    for($i=0;$i -lt $packageIds.Count;$i++){ Assert-Equal $packageIds[$i] $r.Packages[$i].Id "Package order at index $i." }
}

$results += Invoke-Case "P11" "Indeterminate dominates otherwise Absent" {
    $s=@(404,404,429,404,404,404,404,404,404,404)
    $r=Invoke-NuGetRegistryPreflight -AdmittedPackageSet (New-AdmittedSet) -Request (New-FakeTransport -Statuses $s) -ServiceIndexUri $serviceIndex
    Assert-Equal "Indeterminate" $r.State "Aggregate state."
}

$results += Invoke-Case "P12" "Indeterminate dominates Present and Absent mix" {
    $s=@(200,404,503,200,404,200,404,200,404,200)
    $r=Invoke-NuGetRegistryPreflight -AdmittedPackageSet (New-AdmittedSet) -Request (New-FakeTransport -Statuses $s) -ServiceIndexUri $serviceIndex
    Assert-Equal "Indeterminate" $r.State "Aggregate state."
}

$results += Invoke-Case "P13" "discovered PackageBaseAddress drives requests" {
    $requests=[System.Collections.Generic.List[object]]::new()
    $customBase="https://authority.example.test/custom-flat/"
    $content=New-ServiceIndexContent -BaseAddress $customBase
    $r=Invoke-NuGetRegistryPreflight -AdmittedPackageSet (New-AdmittedSet) -Request (New-FakeTransport -ServiceContent $content -Requests $requests) -ServiceIndexUri $serviceIndex
    Assert-Equal $customBase $r.PackageBaseAddress "Discovered base address."
    foreach($request in @($requests | Select-Object -Skip 1)){ Assert-True ($request.Uri.StartsWith($customBase,[System.StringComparison]::Ordinal)) "Package request did not use discovered PackageBaseAddress." }
}

$results += Invoke-Case "P14" "operation owns read-only HTTP method policy" {
    $requests=[System.Collections.Generic.List[object]]::new()
    $null=Invoke-NuGetRegistryPreflight -AdmittedPackageSet (New-AdmittedSet) -Request (New-FakeTransport -Requests $requests) -ServiceIndexUri $serviceIndex
    Assert-True ($requests.Count -eq 11) "Expected one discovery and ten package requests."
    Assert-Equal "GET" $requests[0].Method "Discovery method."
    foreach($request in @($requests | Select-Object -Skip 1)){ Assert-Equal "HEAD" $request.Method "Package observation method." }
    Assert-True (@($requests | Where-Object { $_.Method -notin @("GET","HEAD") }).Count -eq 0) "Mutation-capable method was issued."
}

$results | Format-Table Id,Name,Outcome -AutoSize
if ($results.Count -ne 14 -or @($results | Where-Object Outcome -ne "PASS").Count -ne 0) { throw "RP-2 conformance failed." }
Write-Host ""
Write-Host "RP-2 CONFORMANCE: 14 / 14 PASS"
