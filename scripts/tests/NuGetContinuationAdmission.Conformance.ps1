Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'

$repositoryRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts/NuGetContinuationAdmission.ps1')

function Assert-Eq($Expected,$Actual,[string]$Message){if([string]$Expected-cne[string]$Actual){throw "$Message Expected '$Expected', actual '$Actual'."}}
function Assert-Null($Actual,[string]$Message){if($null-ne$Actual){throw "$Message Expected null, actual '$Actual'."}}
function Invoke-Case([string]$Id,[string]$Name,[scriptblock]$Body){try{&$Body;[pscustomobject]@{Id=$Id;Name=$Name;Outcome='PASS'}}catch{[pscustomobject]@{Id=$Id;Name=$Name;Outcome='FAIL';Error=$_.Exception.Message}}}
function Assert-Throws([scriptblock]$Body,[string]$Contains){try{&$Body;throw 'Expected exception was not thrown.'}catch{if($_.Exception.Message -eq 'Expected exception was not thrown.'){throw};if($_.Exception.Message -notlike "*$Contains*"){throw "Unexpected exception: $($_.Exception.Message)"}}}

$registry='https://api.nuget.org/v3/index.json'
$base='https://api.nuget.org/v3-flatcontainer/'
$serviceJson='{"resources":[{"@id":"https://api.nuget.org/v3-flatcontainer/","@type":"PackageBaseAddress/3.0.0"}]}'
$historicalId='00000000-0000-0000-0000-000000000401'
$continuationId='00000000-0000-0000-0000-000000000402'
$version='1.0.4-test.1'

function Get-Hash([byte[]]$Bytes){Get-Sha256Hex -Bytes $Bytes}
function New-Selection(
    [string]$Id='Pkg.D',
    [string]$Version=$version,
    [string]$Hash=('d'*64),
    [int]$Index=3,
    [string]$ReleaseSha=('f'*64),
    [string]$Source='RP3RecoveryContinuation',
    [AllowNull()][string]$LegacyId=$historicalId,
    [string]$SourceCommit=('a'*40),
    [string]$ReleaseAuthorityTag='release/v1.0.4') {
    [pscustomobject]@{
        ReleaseIdentityProfile='1';ReleaseIdentitySha256=$ReleaseSha;PackageIndex=$Index;PackageId=$Id;PackageVersion=$Version;AdmittedSha256=$Hash
        SourceCommit=$SourceCommit;ReleaseAuthorityTag=$ReleaseAuthorityTag;SelectionSource=$Source;SelectionEvidence=[pscustomobject]@{Kind='fixture'}
        LegacyHistoricalPublicationOperationId=$LegacyId
    }
}
function New-Request([int]$ContentStatus,[AllowNull()][byte[]]$Bytes=$null,[bool]$ThrowContent=$false){
    $serviceIndexContent=$script:serviceJson
    $state=[pscustomobject]@{Calls=0;Requests=[System.Collections.Generic.List[object]]::new()}
    $request={param($request);$state.Calls++;$state.Requests.Add($request);if(($state.Calls%2)-eq 1){return [pscustomobject]@{StatusCode=200;Content=$serviceIndexContent;Bytes=$null}};if($ThrowContent){throw 'transport failed'};[pscustomobject]@{StatusCode=$ContentStatus;Content=$null;Bytes=$Bytes}}.GetNewClosure()
    [pscustomobject]@{Request=$request;State=$state}
}
function Observe([object]$Selection,[scriptblock]$Request){Invoke-NuGetExactSuccessorRemoteAdmission -ContinuationSelection $Selection -ServiceIndexUri $registry -Request $Request -Clock { [datetime]'2026-09-26T10:00:00Z' }}
function Copy-Remote([object]$Remote,[string]$State,[string]$Reason,[AllowNull()][Nullable[int]]$StatusCode){
    [pscustomobject]@{
        Registry=$Remote.Registry;PackageBaseAddress=$Remote.PackageBaseAddress;PackageContentUri=$Remote.PackageContentUri
        ReleaseIdentityProfile=$Remote.ReleaseIdentityProfile;ReleaseIdentitySha256=$Remote.ReleaseIdentitySha256;PackageIndex=$Remote.PackageIndex
        Id=$Remote.Id;Version=$Remote.Version;AdmittedSha256=$Remote.AdmittedSha256;SelectionSource=$Remote.SelectionSource
        RemoteSha256=$Remote.RemoteSha256;State=$State;Reason=$Reason;StatusCode=$StatusCode;ObservedAtUtc=$Remote.ObservedAtUtc;Diagnostic=$Remote.Diagnostic
    }
}

$results=@()
$results+=Invoke-Case 'A01' '404 is authoritative absence and Admissible' {$fixture=New-Request 404;$r=Observe (New-Selection) $fixture.Request;Assert-Eq 'Admissible' $r.State 'state';Assert-Eq 'AuthoritativeAbsent' $r.Reason 'reason';Assert-Eq '404' $r.StatusCode 'status';Assert-Eq '2' $fixture.State.Calls 'request count'}
$results+=Invoke-Case 'A02' 'exact observation uses GET package-content URI' {$fixture=New-Request 404;$null=Observe (New-Selection) $fixture.Request;Assert-Eq '2' $fixture.State.Requests.Count 'request count';Assert-Eq 'GET' $fixture.State.Requests[1].Method 'method';Assert-Eq ($base+'pkg.d/1.0.4-test.1/pkg.d.1.0.4-test.1.nupkg') $fixture.State.Requests[1].Uri 'content URI'}
$results+=Invoke-Case 'A03' '200 equal bytes are BlockedEquivalent' {$bytes=[Text.Encoding]::UTF8.GetBytes('same');$hash=Get-Hash $bytes;$fixture=New-Request 200 $bytes;$r=Observe (New-Selection -Hash $hash) $fixture.Request;Assert-Eq 'Blocked' $r.State 'state';Assert-Eq 'EquivalentPackagePresent' $r.Reason 'reason';Assert-Eq $hash $r.RemoteSha256 'remote hash'}
$results+=Invoke-Case 'A04' '200 different bytes are BlockedDifferent' {$bytes=[Text.Encoding]::UTF8.GetBytes('remote');$fixture=New-Request 200 $bytes;$r=Observe (New-Selection) $fixture.Request;Assert-Eq 'Blocked' $r.State 'state';Assert-Eq 'DifferentPackagePresent' $r.Reason 'reason'}
$results+=Invoke-Case 'A05' '200 without complete bytes is Indeterminate' {$fixture=New-Request 200 $null;$r=Observe (New-Selection) $fixture.Request;Assert-Eq 'Indeterminate' $r.State 'state';Assert-Eq 'ContentUnreadable' $r.Reason 'reason'}
$results+=Invoke-Case 'A06' '429 is Indeterminate' {$fixture=New-Request 429;$r=Observe (New-Selection) $fixture.Request;Assert-Eq 'Indeterminate' $r.State 'state';Assert-Eq 'RateLimited' $r.Reason 'reason'}
$results+=Invoke-Case 'A07' '5xx is Indeterminate' {$fixture=New-Request 503;$r=Observe (New-Selection) $fixture.Request;Assert-Eq 'Indeterminate' $r.State 'state';Assert-Eq 'ServerFailure' $r.Reason 'reason'}
$results+=Invoke-Case 'A08' 'transport failure is Indeterminate' {$fixture=New-Request 0 $null $true;$r=Observe (New-Selection) $fixture.Request;Assert-Eq 'Indeterminate' $r.State 'state';Assert-Eq 'TransportFailure' $r.Reason 'reason'}
$results+=Invoke-Case 'A09' 'RP-5 selection is admissible without recovery state' {$fixture=New-Request 404;$s=New-Selection -Source 'RP5EffectiveRelease' -LegacyId $null;$r=Observe $s $fixture.Request;Assert-Eq 'Admissible' $r.State 'state';Assert-Eq 'RP5EffectiveRelease' $r.SelectionSource 'selection source'}
$results+=Invoke-Case 'A10' 'invalid selection fails before remote observation' {$fixture=New-Request 404;$s=New-Selection -Source 'RP5EffectiveRelease' -LegacyId $historicalId;Assert-Throws {Observe $s $fixture.Request} 'must not synthesize';Assert-Eq '0' $fixture.State.Calls 'request count'}
$results+=Invoke-Case 'A11' 'each admission invocation performs a fresh exact observation' {$fixture=New-Request 404;$s=New-Selection -Source 'RP5EffectiveRelease' -LegacyId $null;$r1=Observe $s $fixture.Request;$r2=Observe $s $fixture.Request;Assert-Eq 'Admissible' $r1.State 'first state';Assert-Eq 'Admissible' $r2.State 'second state';Assert-Eq '4' $fixture.State.Calls 'two independent service-index/content observations'}

$temp=Join-Path ([IO.Path]::GetTempPath()) ('pulsestack-rp6b2-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $temp -Force|Out-Null
try {
    $packages=[System.Collections.Generic.List[object]]::new()
    foreach($entry in @(@('Pkg.A','a'),@('Pkg.B','b'),@('Pkg.C','c'),@('Pkg.D','admitted-package'))){
        $path=Join-Path $temp ($entry[0]+'.nupkg');$bytes=[Text.Encoding]::UTF8.GetBytes($entry[1]);[IO.File]::WriteAllBytes($path,$bytes)
        $packages.Add([pscustomobject]@{Id=$entry[0];Version=$version;Sha256=(Get-Hash $bytes);FilePath=$path})
    }
    $set=[pscustomobject]@{SourceCommit=('a'*40);VersionPrefix='1.0.4';PackageVersion=$version;ReleaseAuthorityTag='release/v1.0.4';Packages=@($packages)}
    $identity=Get-NuGetReleaseIdentityEvidence -AdmittedPackageSet $set
    $target=$packages[3]
    $rp3=New-Selection -Id $target.Id -Version $target.Version -Hash $target.Sha256 -ReleaseSha $identity.ReleaseIdentitySha256 -Source 'RP3RecoveryContinuation' -LegacyId $historicalId
    $rp5=New-Selection -Id $target.Id -Version $target.Version -Hash $target.Sha256 -ReleaseSha $identity.ReleaseIdentitySha256 -Source 'RP5EffectiveRelease' -LegacyId $null
    $remote3=Observe $rp3 (New-Request 404).Request
    $remote5=Observe $rp5 (New-Request 404).Request

    $results+=Invoke-Case 'G01' 'RP-3 selection produces one package-bound grant' {$g=New-NuGetExactPackageContinuationGrant -AdmittedPackageSet $set -ContinuationSelection $rp3 -RemoteAdmission $remote3 -ContinuationOperationId $continuationId;Assert-Eq $historicalId $g.HistoricalPublicationOperationId 'historical operation';Assert-Eq $continuationId $g.ContinuationOperationId 'continuation operation';Assert-Eq 'RP3RecoveryContinuation' $g.SelectionSource 'selection source';Assert-Eq $identity.ReleaseIdentitySha256 $g.ReleaseIdentitySha256 'release identity';Assert-Eq '3' $g.PackageIndex 'index';Assert-Eq 'Pkg.D' $g.PackageId 'id';Assert-Eq $target.Sha256 $g.AdmittedSha256 'hash';Assert-Eq $target.FilePath $g.ArtifactPath 'artifact'}
    $results+=Invoke-Case 'G02' 'RP-5 selection produces grant without recovery evidence' {$g=New-NuGetExactPackageContinuationGrant -AdmittedPackageSet $set -ContinuationSelection $rp5 -RemoteAdmission $remote5 -ContinuationOperationId $continuationId;Assert-Null $g.HistoricalPublicationOperationId 'historical operation';Assert-Eq 'RP5EffectiveRelease' $g.SelectionSource 'selection source';Assert-Eq $identity.ReleaseIdentityProfile $g.ReleaseIdentityProfile 'release profile';Assert-Eq $identity.ReleaseIdentitySha256 $g.ReleaseIdentitySha256 'release identity'}
    $results+=Invoke-Case 'G03' 'Blocked remote state cannot produce grant' {$blocked=Copy-Remote $remote5 'Blocked' 'EquivalentPackagePresent' 200;Assert-Throws {New-NuGetExactPackageContinuationGrant $set $rp5 $blocked $continuationId} 'not Admissible'}
    $results+=Invoke-Case 'G04' 'Indeterminate remote state cannot produce grant' {$ind=Copy-Remote $remote5 'Indeterminate' 'TransportFailure' $null;Assert-Throws {New-NuGetExactPackageContinuationGrant $set $rp5 $ind $continuationId} 'not Admissible'}
    $results+=Invoke-Case 'G05' 'grant rejects remote admission from another selection' {$wrong=Copy-Remote $remote5 'Admissible' 'AuthoritativeAbsent' 404;$wrong.ReleaseIdentitySha256='e'*64;Assert-Throws {New-NuGetExactPackageContinuationGrant $set $rp5 $wrong $continuationId} 'does not bind'}
    $results+=Invoke-Case 'G06' 'grant rejects selection release identity mismatch' {$wrong=New-Selection -Id $target.Id -Version $target.Version -Hash $target.Sha256 -ReleaseSha ('e'*64) -Source 'RP5EffectiveRelease' -LegacyId $null;Assert-Throws {New-NuGetExactPackageContinuationGrant $set $wrong $remote5 $continuationId} 'does not belong'}
    $results+=Invoke-Case 'G07' 'grant rejects package index disagreement with admitted release' {$wrong=New-Selection -Id $target.Id -Version $target.Version -Hash $target.Sha256 -Index 2 -ReleaseSha $identity.ReleaseIdentitySha256 -Source 'RP5EffectiveRelease' -LegacyId $null;Assert-Throws {New-NuGetExactPackageContinuationGrant $set $wrong $remote5 $continuationId} 'does not match admitted release position'}
    $results+=Invoke-Case 'G08' 'grant rejects artifact changed after admission' {[IO.File]::WriteAllText($target.FilePath,'changed',[Text.UTF8Encoding]::new($false));Assert-Throws {New-NuGetExactPackageContinuationGrant $set $rp5 $remote5 $continuationId} 'changed after admission';[IO.File]::WriteAllBytes($target.FilePath,[Text.Encoding]::UTF8.GetBytes('admitted-package'))}
    $results+=Invoke-Case 'G09' 'grant requires canonical new operation identity' {Assert-Throws {New-NuGetExactPackageContinuationGrant $set $rp5 $remote5 'NOT-A-GUID'} 'canonical lowercase GUID'}
    $results+=Invoke-Case 'G10' 'grant propagates canonical selection identity unchanged' {$g=New-NuGetExactPackageContinuationGrant $set $rp5 $remote5 $continuationId;foreach($name in @('ReleaseIdentityProfile','ReleaseIdentitySha256','SelectionSource','PackageIndex','PackageId','PackageVersion','AdmittedSha256','SourceCommit','ReleaseAuthorityTag')){Assert-Eq $rp5.$name $g.$name "grant field $name"}}
}
finally {Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue}

$results|Format-Table -AutoSize
$failed=@($results|Where-Object Outcome -eq 'FAIL')
if($failed.Count-gt 0){$failed|ForEach-Object{Write-Host "$($_.Id) $($_.Name): $($_.Error)" -ForegroundColor Red};throw "RP-6B.2 generalized continuation admission conformance failed: $($failed.Count) case(s)."}
Write-Host "RP-6B.2 generalized continuation admission conformance passed: $($results.Count)/$($results.Count)."
