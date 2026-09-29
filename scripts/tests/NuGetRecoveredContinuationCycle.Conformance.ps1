Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repositoryRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts/NuGetAdmittedReleaseIdentity.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetReleaseIdentityEvidence.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetReleaseEvidenceClaims.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetRecoveryReleaseEvidenceClaims.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetReleasePositionEffectiveState.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetWholeReleaseEffectiveState.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetContinuationSelection.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetContinuationAdmission.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetContinuationOperation.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetRecoveryEvidence.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetRecoveryConvergence.ps1')

function Assert-Eq($Expected,$Actual,[string]$Message){if([string]$Expected-cne[string]$Actual){throw "$Message Expected '$Expected', actual '$Actual'."}}
function Assert-True([bool]$Value,[string]$Message){if(-not$Value){throw $Message}}
function Assert-Null($Actual,[string]$Message){if($null-ne$Actual){throw "$Message Expected null, actual '$Actual'."}}
function Invoke-Case([string]$Id,[string]$Name,[scriptblock]$Body){try{&$Body;[pscustomobject]@{Id=$Id;Name=$Name;Outcome='PASS'}}catch{[pscustomobject]@{Id=$Id;Name=$Name;Outcome='FAIL';Error=$_.Exception.Message}}}
function Get-PositionStates([object]$Release,[object[]]$Claims){$states=[System.Collections.Generic.List[object]]::new();for($i=0;$i-lt$Release.PackageCount;$i++){$at=@($Claims|Where-Object{[int]$_.PackageIndex-eq$i});$states.Add((Get-NuGetReleasePositionEffectiveState -ReleaseIdentityProfile $Release.Profile -ReleaseIdentitySha256 $Release.Sha256 -PackageIndex $i -Claims $at))};@($states)}

$temp=Join-Path ([IO.Path]::GetTempPath()) ('pulsestack-rp6c-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force|Out-Null
$results=@()
try {
    $sourceCommit='0123456789abcdef0123456789abcdef01234567';$version='1.0.4';$tag='v1.0.4';$ids=@('A','B','C','D','E')
    $packages=[System.Collections.Generic.List[object]]::new()
    foreach($id in $ids){$path=Join-Path $temp ("Pkg.$id.nupkg");[IO.File]::WriteAllText($path,("package-$id"),[Text.UTF8Encoding]::new($false));$sha=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant();$packages.Add([pscustomobject]@{Id=$id;Version=$version;Sha256=$sha;FilePath=$path})}
    $admitted=[pscustomobject]@{SourceCommit=$sourceCommit;PackageVersion=$version;ReleaseAuthorityTag=$tag;Packages=@($packages)}
    $identity=Get-NuGetReleaseIdentityEvidence -AdmittedPackageSet $admitted
    $release=[pscustomobject]@{Profile=$identity.ReleaseIdentityProfile;Sha256=$identity.ReleaseIdentitySha256;SourceCommit=$sourceCommit;PackageVersion=$version;ReleaseAuthorityTag=$tag;PackageCount=$packages.Count;Packages=@($packages|ForEach-Object{[pscustomobject]@{Id=$_.Id;Version=$_.Version;Sha256=$_.Sha256}})}
    $originalLedger=[pscustomobject]@{operationId='00000000-0000-0000-0000-000000000301';sourceCommit=$sourceCommit;packageVersion=$version;releaseAuthorityTag=$tag;packages=@(
        [pscustomobject]@{id='A';version=$version;admittedSha256=$packages[0].Sha256;mutationState='Accepted';statusCode=201;diagnostic=$null},
        [pscustomobject]@{id='B';version=$version;admittedSha256=$packages[1].Sha256;mutationState='Accepted';statusCode=201;diagnostic=$null},
        [pscustomobject]@{id='C';version=$version;admittedSha256=$packages[2].Sha256;mutationState='Accepted';statusCode=201;diagnostic=$null},
        [pscustomobject]@{id='D';version=$version;admittedSha256=$packages[3].Sha256;mutationState='NotAttempted';statusCode=$null;diagnostic=$null},
        [pscustomobject]@{id='E';version=$version;admittedSha256=$packages[4].Sha256;mutationState='NotAttempted';statusCode=$null;diagnostic=$null})}
    $claims=@(ConvertFrom-NuGetOriginalPublicationLedgerClaims -Ledger $originalLedger -Release $release)
    $states1=Get-PositionStates $release $claims;$projectionD=Get-NuGetWholeReleaseEffectiveState -Release $release -PositionStates $states1;$selectionD=ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult $projectionD

    $registry='https://api.nuget.org/v3/index.json';$serviceJson='{"resources":[{"@id":"https://api.nuget.org/v3-flatcontainer/","@type":"PackageBaseAddress/3.0.0"}]}'
    $request={param($r);if([string]$r.Uri-ceq$registry){return [pscustomobject]@{StatusCode=200;Content=$serviceJson}};[pscustomobject]@{StatusCode=404;Content=$null;Bytes=$null}}
    $admissionD=Invoke-NuGetExactSuccessorRemoteAdmission -ContinuationSelection $selectionD -ServiceIndexUri $registry -Request $request -Clock {[DateTime]'2026-09-29T12:00:00Z'}
    $grantD=New-NuGetExactPackageContinuationGrant -AdmittedPackageSet $admitted -ContinuationSelection $selectionD -RemoteAdmission $admissionD -ContinuationOperationId '00000000-0000-0000-0000-000000000401'
    $publish={param($endpoint,$path,$key);[pscustomobject]@{StatusCode=409}}
    $discovery={param($uri);[pscustomobject]@{StatusCode=200;Content='{"resources":[{"@id":"https://www.nuget.org/api/v2/package","@type":"PackagePublish/2.0.0"}]}'}}
    $write={param($ledger,$path)}
    $operationD=Invoke-NuGetContinuationOperation -Grant $grantD -EvidenceRoot $temp -CredentialAvailable {$true} -AcquireCredential {'secret'} -DiscoveryRequest $discovery -PublishRequest $publish -WriteLedger $write -Clock {[DateTimeOffset]'2026-09-29T12:01:00Z'} -ServiceIndexUri $registry
    $ledgerD=$operationD.Result
    $ledgerPath=Join-Path $temp 'continuation-d.json';[IO.File]::WriteAllText($ledgerPath,($ledgerD|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    $candidate=Get-NuGetRecoveryCandidate -LedgerPath $ledgerPath -OperationId $ledgerD.operationId -PackageId 'D' -PackageVersion $version -AdmittedSha256 $packages[3].Sha256
    $trigger=New-NuGetRecoveryTrigger -Kind $candidate.Trigger;$resolved=Resolve-NuGetRecoveryObservation -Trigger $trigger -Observation ([pscustomobject]@{State='Equivalent'})
    $recoveryResult=[pscustomobject]@{RecoveryState=$resolved.RecoveryState;Terminal=$resolved.Terminal;StartedAtUtc=[DateTimeOffset]'2026-09-29T12:02:00Z';DeadlineUtc=[DateTimeOffset]'2026-09-29T12:07:00Z';ObservationCount=1;LastObservation=[pscustomobject]@{State='Equivalent'}}
    $recoveryEvidence=New-NuGetRecoveryEvidenceBinding -Candidate $candidate -RecoveryResult $recoveryResult -Observations @([pscustomobject]@{State='Equivalent'})
    $recoveryClaim=ConvertFrom-NuGetRecoveryEvidenceClaim -RecoveryEvidence $recoveryEvidence -SourceLedger $ledgerD -Release $release
    $claimsAfterRecovery=@($claims)+@($recoveryClaim);$states2=Get-PositionStates $release $claimsAfterRecovery;$projectionE=Get-NuGetWholeReleaseEffectiveState -Release $release -PositionStates $states2;$selectionE=ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult $projectionE

    $results+=Invoke-Case 'R01' 'RP-5 effective release selects D before recovery cycle' {Assert-Eq 'ContinuationEligible' $projectionD.ReleaseState 'release';Assert-Eq '3' $selectionD.PackageIndex 'D index';Assert-Eq 'RP5EffectiveRelease' $selectionD.SelectionSource 'source'}
    $results+=Invoke-Case 'R02' 'D receives fresh admission before mutation' {Assert-Eq 'Admissible' $admissionD.State 'admission';Assert-Eq 'AuthoritativeAbsent' $admissionD.Reason 'reason'}
    $results+=Invoke-Case 'R03' 'RP-4B produces recoverable RP-5 continuation ledger' {Assert-Eq 'Rejected' $ledgerD.packages[0].mutationState 'mutation';Assert-Eq '409' $ledgerD.packages[0].statusCode 'status';Assert-Eq 'RP5EffectiveRelease' $ledgerD.selectionSource 'source';Assert-Null $ledgerD.historicalPublicationOperationId 'historical pointer'}
    $results+=Invoke-Case 'R04' 'RP-3 recovery authority is D continuation operation' {Assert-Eq $ledgerD.operationId $candidate.Operation.OperationId 'operation';Assert-Eq 'D' $candidate.Package.Id 'package';Assert-Eq 'Conflict409' $candidate.Trigger 'trigger'}
    $results+=Invoke-Case 'R05' 'equivalent observation converges D recovery' {Assert-Eq 'Converged' $resolved.RecoveryState 'state';Assert-Eq 'True' $resolved.Terminal 'terminal'}
    $results+=Invoke-Case 'R06' 'C.3 joins recovered D to canonical release position' {Assert-Eq 'ContinuationRecovery' $recoveryClaim.EvidenceSource 'source';Assert-Eq $release.Sha256 $recoveryClaim.ReleaseIdentitySha256 'R';Assert-Eq '3' $recoveryClaim.PackageIndex 'index';Assert-Eq 'D' $recoveryClaim.PackageId 'package'}
    $results+=Invoke-Case 'R07' 'C.3 preserves null RP-5 historical provenance' {Assert-Null $recoveryClaim.HistoricalPublicationOperationId 'historical pointer';Assert-Eq $ledgerD.operationId $recoveryClaim.OperationId 'operation'}
    $results+=Invoke-Case 'R08' 'recovered Converged D satisfies unchanged D2' {Assert-Eq 'Satisfied' $states2[3].EffectiveState 'D state';Assert-Eq 'Unsatisfied' $states2[4].EffectiveState 'E state'}
    $results+=Invoke-Case 'R09' 'unchanged D3 selects E after recovered D satisfaction' {Assert-Eq 'ContinuationEligible' $projectionE.ReleaseState 'release';Assert-Eq '4' $projectionE.PackageIndex 'index';Assert-Eq 'E' $projectionE.PackageId 'package'}
    $results+=Invoke-Case 'R10' 'E is a new RP-5 selection with no legacy provenance' {Assert-Eq 'RP5EffectiveRelease' $selectionE.SelectionSource 'source';Assert-Eq '4' $selectionE.PackageIndex 'index';Assert-Null $selectionE.LegacyHistoricalPublicationOperationId 'historical pointer'}
    $results+=Invoke-Case 'R11' 'recovery path creates no suffix mutation authority' {foreach($n in @('Credential','Publish','Put','ContinuationGrant')){Assert-True ($null-eq$recoveryClaim.PSObject.Properties[$n]) "$n surfaced"}}
    $results+=Invoke-Case 'R12' 'D recovery does not itself admit or mutate E' {Assert-True ($null-eq$selectionE.PSObject.Properties['RemoteAdmission']) 'remote admission surfaced';Assert-True ($null-eq$selectionE.PSObject.Properties['ContinuationGrant']) 'grant surfaced'}
}
finally {Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue}
$results|Format-Table -AutoSize
$failed=@($results|Where-Object Outcome -eq 'FAIL')
if($failed.Count){$failed|ForEach-Object{Write-Host "$($_.Id) $($_.Name): $($_.Error)" -ForegroundColor Red};throw "RP-6C recovered continuation cycle conformance failed: $($failed.Count) case(s)."}
Write-Host "RP-6C recovered continuation cycle conformance passed: $($results.Count)/$($results.Count)."
