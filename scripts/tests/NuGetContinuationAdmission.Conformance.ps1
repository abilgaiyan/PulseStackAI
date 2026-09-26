Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'

$repositoryRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts/NuGetContinuationAdmission.ps1')

function Assert-Eq($Expected,$Actual,[string]$Message){if($Expected-cne$Actual){throw "$Message Expected '$Expected', actual '$Actual'."}}
function Assert-True([bool]$Condition,[string]$Message){if(-not$Condition){throw $Message}}
function Invoke-Case([string]$Id,[string]$Name,[scriptblock]$Body){try{&$Body;[pscustomobject]@{Id=$Id;Name=$Name;Outcome='PASS'}}catch{[pscustomobject]@{Id=$Id;Name=$Name;Outcome='FAIL';Error=$_.Exception.Message}}}
function Assert-Throws([scriptblock]$Body,[string]$Contains){try{&$Body;throw 'Expected exception was not thrown.'}catch{if($_.Exception.Message -eq 'Expected exception was not thrown.'){throw};if($_.Exception.Message -notlike "*$Contains*"){throw "Unexpected exception: $($_.Exception.Message)"}}}

$registry='https://api.nuget.org/v3/index.json'
$base='https://api.nuget.org/v3-flatcontainer/'
$serviceJson='{"resources":[{"@id":"https://api.nuget.org/v3-flatcontainer/","@type":"PackageBaseAddress/3.0.0"}]}'
$historicalId='00000000-0000-0000-0000-000000000401'
$continuationId='00000000-0000-0000-0000-000000000402'
$version='1.0.4-test.1'

function Get-Hash([byte[]]$Bytes){Get-Sha256Hex -Bytes $Bytes}
function New-Decision([string]$Id='Pkg.D',[string]$Version=$version,[string]$Hash=('d'*64),[bool]$MayContinue=$true,[string]$Disposition='ContinuationEligible'){
    [pscustomobject]@{OperationId=$historicalId;RecoveryPackageIndex=2;RecoveryState='Converged';MayContinue=$MayContinue;HasNextPackage=$true;NextPackageIndex=3;NextPackage=if($MayContinue){[pscustomobject]@{Id=$Id;Version=$Version;AdmittedSha256=$Hash;MutationState='NotAttempted'}}else{$null};WholeOperationDisposition=$Disposition}
}
function New-Package([string]$Id='Pkg.D',[string]$Version=$version,[string]$Hash=('d'*64),[string]$FilePath='unused.nupkg'){[pscustomobject]@{Id=$Id;Version=$Version;Sha256=$Hash;FilePath=$FilePath}}
function New-RecoveryEvidence([string]$State='Converged',[string]$OperationId=$historicalId){[pscustomobject]@{HistoricalPublication=[pscustomobject]@{Operation=[pscustomobject]@{OperationId=$OperationId}};Recovery=[pscustomobject]@{RecoveryState=$State}}}
function New-Request([int]$ContentStatus,[AllowNull()][byte[]]$Bytes=$null,[bool]$ThrowContent=$false){
    $script:calls=0
    return {
        param($request)
        $script:calls++
        if($script:calls-eq 1){return [pscustomobject]@{StatusCode=200;Content=$serviceJson;Bytes=$null}}
        if($ThrowContent){throw 'transport failed'}
        [pscustomobject]@{StatusCode=$ContentStatus;Content=$null;Bytes=$Bytes}
    }.GetNewClosure()
}
function Observe([object]$Package,[object]$Decision,[scriptblock]$Request){Invoke-NuGetExactSuccessorRemoteAdmission -Package $Package -ContinuationDecision $Decision -ServiceIndexUri $registry -Request $Request -Clock { [datetime]'2026-09-26T10:00:00Z' }}

$results=@()
$results+=Invoke-Case 'A01' '404 is authoritative absence and Admissible' {
    $p=New-Package;$r=Observe $p (New-Decision) (New-Request 404)
    Assert-Eq 'Admissible' $r.State 'state';Assert-Eq 'AuthoritativeAbsent' $r.Reason 'reason';Assert-Eq '404' ([string]$r.StatusCode) 'status';Assert-Eq '2' ([string]$script:calls) 'request count'
}
$results+=Invoke-Case 'A02' 'exact observation uses GET package-content URI' {
    $seen=@();$req={param($r)$seen+=$r;if($seen.Count-eq 1){[pscustomobject]@{StatusCode=200;Content=$serviceJson;Bytes=$null}}else{[pscustomobject]@{StatusCode=404;Content=$null;Bytes=$null}}}.GetNewClosure()
    $null=Observe (New-Package) (New-Decision) $req
    Assert-Eq 'GET' $seen[1].Method 'method';Assert-Eq ($base+'pkg.d/1.0.4-test.1/pkg.d.1.0.4-test.1.nupkg') $seen[1].Uri 'content URI'
}
$results+=Invoke-Case 'A03' '200 equal bytes are BlockedEquivalent' {
    $bytes=[Text.Encoding]::UTF8.GetBytes('same');$hash=Get-Hash $bytes;$r=Observe (New-Package -Hash $hash) (New-Decision -Hash $hash) (New-Request 200 $bytes)
    Assert-Eq 'Blocked' $r.State 'state';Assert-Eq 'EquivalentPackagePresent' $r.Reason 'reason';Assert-Eq $hash $r.RemoteSha256 'remote hash'
}
$results+=Invoke-Case 'A04' '200 different bytes are BlockedDifferent' {
    $bytes=[Text.Encoding]::UTF8.GetBytes('remote');$r=Observe (New-Package) (New-Decision) (New-Request 200 $bytes)
    Assert-Eq 'Blocked' $r.State 'state';Assert-Eq 'DifferentPackagePresent' $r.Reason 'reason'
}
$results+=Invoke-Case 'A05' '200 without complete bytes is Indeterminate' {
    $r=Observe (New-Package) (New-Decision) (New-Request 200 $null);Assert-Eq 'Indeterminate' $r.State 'state';Assert-Eq 'ContentUnreadable' $r.Reason 'reason'
}
$results+=Invoke-Case 'A06' '429 is Indeterminate' {$r=Observe (New-Package) (New-Decision) (New-Request 429);Assert-Eq 'Indeterminate' $r.State 'state';Assert-Eq 'RateLimited' $r.Reason 'reason'}
$results+=Invoke-Case 'A07' '5xx is Indeterminate' {$r=Observe (New-Package) (New-Decision) (New-Request 503);Assert-Eq 'Indeterminate' $r.State 'state';Assert-Eq 'ServerFailure' $r.Reason 'reason'}
$results+=Invoke-Case 'A08' 'transport failure is Indeterminate' {$r=Observe (New-Package) (New-Decision) (New-Request 0 $null $true);Assert-Eq 'Indeterminate' $r.State 'state';Assert-Eq 'TransportFailure' $r.Reason 'reason'}
$results+=Invoke-Case 'A09' 'non-eligible RP-3C decision cannot be observed' {Assert-Throws {Observe (New-Package) (New-Decision -MayContinue $false -Disposition 'RecoveredEnd') (New-Request 404)} 'does not authorize'}
$results+=Invoke-Case 'A10' 'package must exactly match RP-3C successor' {Assert-Throws {Observe (New-Package -Id 'Pkg.E') (New-Decision) (New-Request 404)} 'does not match'}

$temp=Join-Path ([IO.Path]::GetTempPath()) ('pulsestack-rp4a-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $temp -Force|Out-Null
try {
    $artifact=Join-Path $temp 'Pkg.D.nupkg';$bytes=[Text.Encoding]::UTF8.GetBytes('admitted-package');[IO.File]::WriteAllBytes($artifact,$bytes);$hash=Get-Hash $bytes
    $pkg=New-Package -Hash $hash -FilePath $artifact;$decision=New-Decision -Hash $hash;$remote=Observe $pkg $decision (New-Request 404)
    $set=[pscustomobject]@{SourceCommit=('a'*40);VersionPrefix='1.0.4';PackageVersion=$version;ReleaseAuthorityTag='release/v1.0.4';Packages=@($pkg)}
    $recovery=New-RecoveryEvidence

    $results+=Invoke-Case 'G01' 'Admissible exact successor produces one package-bound grant' {
        $g=New-NuGetExactPackageContinuationGrant -AdmittedPackageSet $set -ContinuationDecision $decision -RemoteAdmission $remote -RecoveryEvidence $recovery -ContinuationOperationId $continuationId
        Assert-Eq $historicalId $g.HistoricalPublicationOperationId 'historical operation';Assert-Eq $continuationId $g.ContinuationOperationId 'continuation operation';Assert-Eq '3' ([string]$g.PackageIndex) 'index';Assert-Eq 'Pkg.D' $g.PackageId 'id';Assert-Eq $hash $g.AdmittedSha256 'hash';Assert-Eq $artifact $g.ArtifactPath 'artifact';Assert-Eq 'Admissible' $g.RemoteAdmissionState 'remote state'
    }
    $results+=Invoke-Case 'G02' 'Blocked remote state cannot produce grant' {
        $blocked=[pscustomobject]@{}+$remote;$blocked.State='Blocked';$blocked.Reason='EquivalentPackagePresent';$blocked.StatusCode=200
        Assert-Throws {New-NuGetExactPackageContinuationGrant $set $decision $blocked $recovery $continuationId} 'not Admissible'
    }
    $results+=Invoke-Case 'G03' 'Indeterminate remote state cannot produce grant' {
        $ind=[pscustomobject]@{}+$remote;$ind.State='Indeterminate';$ind.Reason='TransportFailure';$ind.StatusCode=$null
        Assert-Throws {New-NuGetExactPackageContinuationGrant $set $decision $ind $recovery $continuationId} 'not Admissible'
    }
    $results+=Invoke-Case 'G04' 'grant rejects historical operation mismatch' {Assert-Throws {New-NuGetExactPackageContinuationGrant $set $decision $remote (New-RecoveryEvidence -OperationId ('f'*40)) $continuationId} 'does not bind'}
    $results+=Invoke-Case 'G05' 'grant rejects non-converged recovery evidence' {Assert-Throws {New-NuGetExactPackageContinuationGrant $set $decision $remote (New-RecoveryEvidence -State 'Conflict') $continuationId} 'does not bind'}
    $results+=Invoke-Case 'G06' 'grant rejects artifact changed after admission' {
        [IO.File]::WriteAllText($artifact,'changed',[Text.UTF8Encoding]::new($false))
        Assert-Throws {New-NuGetExactPackageContinuationGrant $set $decision $remote $recovery $continuationId} 'changed after admission'
        [IO.File]::WriteAllBytes($artifact,$bytes)
    }
    $results+=Invoke-Case 'G07' 'grant requires canonical new operation identity' {Assert-Throws {New-NuGetExactPackageContinuationGrant $set $decision $remote $recovery 'NOT-A-GUID'} 'canonical lowercase GUID'}
}
finally {Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue}

$results|Format-Table -AutoSize
$failed=@($results|Where-Object Outcome -eq 'FAIL')
if($failed.Count-gt 0){$failed|ForEach-Object{Write-Host "$($_.Id) $($_.Name): $($_.Error)" -ForegroundColor Red};throw "RP-4A continuation admission conformance failed: $($failed.Count) case(s)."}
Write-Host "RP-4A continuation admission conformance passed: $($results.Count)/$($results.Count)."
