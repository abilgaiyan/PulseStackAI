Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repositoryRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts/NuGetAdmittedReleaseIdentity.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetReleaseIdentityEvidence.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetReleaseEvidenceClaims.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetReleasePositionEffectiveState.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetWholeReleaseEffectiveState.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetContinuationSelection.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetContinuationAdmission.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetContinuationOperation.ps1')

function Assert-Eq($Expected,$Actual,[string]$Message){if([string]$Expected-cne[string]$Actual){throw "$Message Expected '$Expected', actual '$Actual'."}}
function Assert-True([bool]$Value,[string]$Message){if(-not$Value){throw $Message}}
function Assert-Null($Actual,[string]$Message){if($null-ne$Actual){throw "$Message Expected null, actual '$Actual'."}}
function Invoke-Case([string]$Id,[string]$Name,[scriptblock]$Body){try{&$Body;[pscustomobject]@{Id=$Id;Name=$Name;Outcome='PASS'}}catch{[pscustomobject]@{Id=$Id;Name=$Name;Outcome='FAIL';Error=$_.Exception.Message}}}

function Get-PositionStates([object]$Release,[object[]]$Claims){
    $states=[System.Collections.Generic.List[object]]::new()
    for($i=0;$i-lt$Release.PackageCount;$i++){
        $at=@($Claims|Where-Object{[int]$_.PackageIndex-eq$i})
        $states.Add((Get-NuGetReleasePositionEffectiveState -ReleaseIdentityProfile $Release.Profile -ReleaseIdentitySha256 $Release.Sha256 -PackageIndex $i -Claims $at))
    }
    return @($states)
}

$temp=Join-Path ([IO.Path]::GetTempPath()) ('pulsestack-rp6b4-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force|Out-Null
$results=@()
try{
    $sourceCommit='0123456789abcdef0123456789abcdef01234567'
    $version='1.0.4'
    $tag='v1.0.4'
    $ids=@('A','B','C','D','E')
    $packages=[System.Collections.Generic.List[object]]::new()
    foreach($id in $ids){
        $path=Join-Path $temp ("Pkg.$id.nupkg")
        [IO.File]::WriteAllText($path,("package-$id"),[Text.UTF8Encoding]::new($false))
        $sha=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        $packages.Add([pscustomobject]@{Id=$id;Version=$version;Sha256=$sha;FilePath=$path})
    }
    $admitted=[pscustomobject]@{SourceCommit=$sourceCommit;PackageVersion=$version;ReleaseAuthorityTag=$tag;Packages=@($packages)}
    $identity=Get-NuGetReleaseIdentityEvidence -AdmittedPackageSet $admitted
    $release=[pscustomobject]@{
        Profile=$identity.ReleaseIdentityProfile
        Sha256=$identity.ReleaseIdentitySha256
        SourceCommit=$sourceCommit
        PackageVersion=$version
        ReleaseAuthorityTag=$tag
        PackageCount=$packages.Count
        Packages=@($packages|ForEach-Object{[pscustomobject]@{Id=$_.Id;Version=$_.Version;Sha256=$_.Sha256}})
    }

    $originalLedger=[pscustomobject]@{
        operationId='00000000-0000-0000-0000-000000000301'
        sourceCommit=$sourceCommit
        packageVersion=$version
        releaseAuthorityTag=$tag
        packages=@(
            [pscustomobject]@{id='A';version=$version;admittedSha256=$packages[0].Sha256;mutationState='Accepted';statusCode=201;diagnostic=$null},
            [pscustomobject]@{id='B';version=$version;admittedSha256=$packages[1].Sha256;mutationState='Accepted';statusCode=201;diagnostic=$null},
            [pscustomobject]@{id='C';version=$version;admittedSha256=$packages[2].Sha256;mutationState='Accepted';statusCode=201;diagnostic=$null},
            [pscustomobject]@{id='D';version=$version;admittedSha256=$packages[3].Sha256;mutationState='NotAttempted';statusCode=$null;diagnostic=$null},
            [pscustomobject]@{id='E';version=$version;admittedSha256=$packages[4].Sha256;mutationState='NotAttempted';statusCode=$null;diagnostic=$null}
        )
    }
    $claims=@(ConvertFrom-NuGetOriginalPublicationLedgerClaims -Ledger $originalLedger -Release $release)
    $states1=Get-PositionStates -Release $release -Claims $claims
    $projectionD=Get-NuGetWholeReleaseEffectiveState -Release $release -PositionStates $states1
    $selectionD=ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult $projectionD

    $registry='https://api.nuget.org/v3/index.json'
    $baseAddress='https://api.nuget.org/v3-flatcontainer/'
    $serviceJson='{"resources":[{"@id":"https://api.nuget.org/v3-flatcontainer/","@type":"PackageBaseAddress/3.0.0"}]}'
    $observationState=[pscustomobject]@{ServiceIndexGets=0;PackageGets=[System.Collections.Generic.List[string]]::new()}
    $request={
        param($r)
        $uri=[string]$r.Uri
        if($uri-ceq$registry){$observationState.ServiceIndexGets++;return [pscustomobject]@{StatusCode=200;Content=$serviceJson}}
        $observationState.PackageGets.Add($uri)
        return [pscustomobject]@{StatusCode=404;Content=$null;Bytes=$null}
    }.GetNewClosure()

    $admissionD=Invoke-NuGetExactSuccessorRemoteAdmission -ContinuationSelection $selectionD -ServiceIndexUri $registry -Request $request -Clock {[DateTime]'2026-09-29T12:00:00Z'}
    $grantD=New-NuGetExactPackageContinuationGrant -AdmittedPackageSet $admitted -ContinuationSelection $selectionD -RemoteAdmission $admissionD -ContinuationOperationId '00000000-0000-0000-0000-000000000401'

    $publishState=[pscustomobject]@{Count=0;Paths=[System.Collections.Generic.List[string]]::new()}
    $publish={param($endpoint,$path,$key);$publishState.Count++;$publishState.Paths.Add([string]$path);[pscustomobject]@{StatusCode=201}}.GetNewClosure()
    $discovery={param($uri);[pscustomobject]@{StatusCode=200;Content='{"resources":[{"@id":"https://www.nuget.org/api/v2/package","@type":"PackagePublish/2.0.0"}]}'}}
    $write={param($ledger,$path)}
    $operationD=Invoke-NuGetContinuationOperation -Grant $grantD -EvidenceRoot $temp -CredentialAvailable {$true} -AcquireCredential {'secret'} -DiscoveryRequest $discovery -PublishRequest $publish -WriteLedger $write -Clock {[DateTimeOffset]'2026-09-29T12:01:00Z'} -ServiceIndexUri $registry

    $publishCountAfterD=$publishState.Count
    $packageObservationCountAfterD=$observationState.PackageGets.Count
    $claimD=ConvertFrom-NuGetContinuationPublicationLedgerClaim -Ledger $operationD.Result -Release $release
    $claimsAfterD=@($claims)+@($claimD)
    $states2=Get-PositionStates -Release $release -Claims $claimsAfterD
    $projectionE=Get-NuGetWholeReleaseEffectiveState -Release $release -PositionStates $states2
    $selectionE=ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult $projectionE
    $admissionE=Invoke-NuGetExactSuccessorRemoteAdmission -ContinuationSelection $selectionE -ServiceIndexUri $registry -Request $request -Clock {[DateTime]'2026-09-29T12:02:00Z'}
    $grantE=New-NuGetExactPackageContinuationGrant -AdmittedPackageSet $admitted -ContinuationSelection $selectionE -RemoteAdmission $admissionE -ContinuationOperationId '00000000-0000-0000-0000-000000000402'
    $operationE=Invoke-NuGetContinuationOperation -Grant $grantE -EvidenceRoot $temp -CredentialAvailable {$true} -AcquireCredential {'secret'} -DiscoveryRequest $discovery -PublishRequest $publish -WriteLedger $write -Clock {[DateTimeOffset]'2026-09-29T12:03:00Z'} -ServiceIndexUri $registry

    $results+=Invoke-Case 'Y01' 'initial effective release selects D only' {Assert-Eq 'ContinuationEligible' $projectionD.ReleaseState 'release state';Assert-Eq '3' $projectionD.PackageIndex 'index';Assert-Eq 'D' $projectionD.PackageId 'package'}
    $results+=Invoke-Case 'Y02' 'D selection is RP-5 sourced with no legacy provenance' {Assert-Eq 'RP5EffectiveRelease' $selectionD.SelectionSource 'source';Assert-Null $selectionD.LegacyHistoricalPublicationOperationId 'historical provenance'}
    $results+=Invoke-Case 'Y03' 'D receives a fresh authoritative absence observation' {Assert-Eq 'Admissible' $admissionD.State 'state';Assert-Eq 'AuthoritativeAbsent' $admissionD.Reason 'reason';Assert-Eq '3' $admissionD.PackageIndex 'index'}
    $results+=Invoke-Case 'Y04' 'D operation mutates exactly one package and does not execute E' {Assert-Eq '1' $publishCountAfterD 'PUT count after D';Assert-Eq $packages[3].FilePath $publishState.Paths[0] 'D artifact';Assert-Eq '1' $operationD.Result.packages.Count 'D ledger package count';Assert-Eq '3' $operationD.Result.packageIndex 'D index';Assert-Eq 'RP5EffectiveRelease' $operationD.Result.selectionSource 'source';Assert-Null $operationD.Result.historicalPublicationOperationId 'D historical provenance'}
    $results+=Invoke-Case 'Y05' 'C.2 accepts RP-5 D ledger and preserves accepted evidence' {Assert-Eq 'ContinuationPublication' $claimD.EvidenceSource 'source';Assert-Eq 'Accepted' $claimD.RawMutationState 'mutation';Assert-Eq '3' $claimD.PackageIndex 'index';Assert-Null $claimD.HistoricalPublicationOperationId 'historical provenance'}
    $results+=Invoke-Case 'Y06' 'accepted D evidence makes D satisfied under unchanged D2' {Assert-Eq 'Satisfied' $states2[3].EffectiveState 'D state';Assert-Eq 'Unsatisfied' $states2[4].EffectiveState 'E state'}
    $results+=Invoke-Case 'Y07' 'reprojection selects E only after D becomes satisfied' {Assert-Eq 'ContinuationEligible' $projectionE.ReleaseState 'release state';Assert-Eq '4' $projectionE.PackageIndex 'index';Assert-Eq 'E' $projectionE.PackageId 'package'}
    $results+=Invoke-Case 'Y08' 'E selection is newly projected and has no historical pointer' {Assert-Eq 'RP5EffectiveRelease' $selectionE.SelectionSource 'source';Assert-Eq '4' $selectionE.PackageIndex 'index';Assert-Null $selectionE.LegacyHistoricalPublicationOperationId 'historical provenance'}
    $results+=Invoke-Case 'Y09' 'E requires a distinct fresh remote observation' {Assert-Eq '2' $observationState.PackageGets.Count 'package observations';Assert-Eq '2' $observationState.ServiceIndexGets 'service-index observations';Assert-Eq '1' $packageObservationCountAfterD 'observations before E';Assert-True ($observationState.PackageGets[0]-cne$observationState.PackageGets[1]) 'D and E content URIs must differ';Assert-True ($observationState.PackageGets[1]-like'*e*1.0.4*') 'second observation was not for E'}
    $results+=Invoke-Case 'Y10' 'D and E grants use distinct operation identities' {Assert-Eq '00000000-0000-0000-0000-000000000401' $grantD.ContinuationOperationId 'D operation';Assert-Eq '00000000-0000-0000-0000-000000000402' $grantE.ContinuationOperationId 'E operation';Assert-True ($grantD.ContinuationOperationId-cne$grantE.ContinuationOperationId) 'operation identities were reused'}
    $results+=Invoke-Case 'Y11' 'second invocation mutates E only' {Assert-Eq '2' $publishState.Count 'total PUT count';Assert-Eq $packages[4].FilePath $publishState.Paths[1] 'E artifact';Assert-Eq '1' $operationE.Result.packages.Count 'E ledger package count';Assert-Eq '4' $operationE.Result.packageIndex 'E index'}
    $results+=Invoke-Case 'Y12' 'cycle uses no suffix grant or automatic mutation loop' {Assert-Eq '1' $publishCountAfterD 'D invocation PUT count';Assert-Eq '2' $publishState.Count 'total explicit invocation PUT count';Assert-Eq '3' $grantD.PackageIndex 'D grant index';Assert-Eq '4' $grantE.PackageIndex 'E grant index'}
}
finally{Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue}

$results|Format-Table -AutoSize
$failed=@($results|Where-Object Outcome -eq 'FAIL')
if($failed.Count){$failed|ForEach-Object{Write-Host "$($_.Id) $($_.Name): $($_.Error)" -ForegroundColor Red};throw "RP-6B.4 continuation cycle conformance failed: $($failed.Count) case(s)."}
Write-Host "RP-6B.4 continuation cycle conformance passed: $($results.Count)/$($results.Count)."
