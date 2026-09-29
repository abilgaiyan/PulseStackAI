Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts/NuGetContinuationSelection.ps1')

function Assert-Eq($Expected,$Actual,[string]$Message) {
    if ([string]$Expected -cne [string]$Actual) { throw "$Message Expected '$Expected', actual '$Actual'." }
}
function Assert-Null($Actual,[string]$Message) {
    if ($null -ne $Actual) { throw "$Message Expected null, actual '$Actual'." }
}
function Assert-Throws([scriptblock]$Body,[string]$Contains) {
    try { & $Body; throw 'Expected exception was not thrown.' }
    catch {
        if ($_.Exception.Message -eq 'Expected exception was not thrown.') { throw }
        if ($_.Exception.Message -notlike "*$Contains*") { throw "Unexpected exception: $($_.Exception.Message)" }
    }
}
function Invoke-Case([string]$Id,[string]$Name,[scriptblock]$Body) {
    try { & $Body; [pscustomobject]@{Id=$Id;Name=$Name;Outcome='PASS'} }
    catch { [pscustomobject]@{Id=$Id;Name=$Name;Outcome='FAIL';Error=$_.Exception.Message} }
}

$releaseSha = 'f' * 64
$release = [pscustomobject]@{
    Profile = '1'
    Sha256 = $releaseSha
    SourceCommit = '0123456789abcdef0123456789abcdef01234567'
    PackageVersion = '1.0.4'
    ReleaseAuthorityTag = 'v1.0.4'
    PackageCount = 3
    Packages = @(
        [pscustomobject]@{Id='A';Version='1.0.4';Sha256=('a'*64)},
        [pscustomobject]@{Id='B';Version='1.0.4';Sha256=('b'*64)},
        [pscustomobject]@{Id='C';Version='1.0.4';Sha256=('c'*64)}
    )
}

function New-Rp3Decision {
    param([int]$Index=1,[string]$RecoveryState='Converged',[bool]$MayContinue=$true,[bool]$HasNextPackage=$true,[string]$Disposition='ContinuationEligible',[string]$MutationState='NotAttempted')
    $p = $release.Packages[$Index]
    [pscustomobject]@{
        OperationId = 'publication-operation-001'
        RecoveryPackageIndex = 0
        RecoveryPackage = $release.Packages[0]
        RecoveryState = $RecoveryState
        MayContinue = $MayContinue
        HasNextPackage = $HasNextPackage
        NextPackageIndex = $Index
        NextPackage = [pscustomobject]@{Id=$p.Id;Version=$p.Version;AdmittedSha256=$p.Sha256;MutationState=$MutationState}
        WholeOperationDisposition = $Disposition
    }
}

function New-Rp5Result {
    param([int]$Index=1,[string]$State='ContinuationEligible')
    $p = $release.Packages[$Index]
    [pscustomobject]@{
        ReleaseState = $State
        Reason = $null
        ReleaseIdentityProfile = $release.Profile
        ReleaseIdentitySha256 = $release.Sha256
        PackageIndex = $Index
        PackageId = $p.Id
        PackageVersion = $p.Version
        AdmittedSha256 = $p.Sha256
        SourceCommit = $release.SourceCommit
        ReleaseAuthorityTag = $release.ReleaseAuthorityTag
        ProjectionEvidence = @()
    }
}

function Assert-CanonicalSelection($Selection,[int]$Index) {
    $p = $release.Packages[$Index]
    Assert-Eq $release.Profile $Selection.ReleaseIdentityProfile 'profile'
    Assert-Eq $release.Sha256 $Selection.ReleaseIdentitySha256 'release SHA'
    Assert-Eq $Index $Selection.PackageIndex 'package index'
    Assert-Eq $p.Id $Selection.PackageId 'package ID'
    Assert-Eq $p.Version $Selection.PackageVersion 'package version'
    Assert-Eq $p.Sha256 $Selection.AdmittedSha256 'package SHA'
    Assert-Eq $release.SourceCommit $Selection.SourceCommit 'source commit'
    Assert-Eq $release.ReleaseAuthorityTag $Selection.ReleaseAuthorityTag 'release authority tag'
}

$results = @()
$results += Invoke-Case 'S01' 'RP-3C eligible decision projects canonical selection' {
    $s = ConvertFrom-NuGetRecoveryContinuationSelection -Release $release -ContinuationDecision (New-Rp3Decision)
    Assert-CanonicalSelection $s 1
}
$results += Invoke-Case 'S02' 'RP-3C selection preserves exact R N package identity' {
    $s = ConvertFrom-NuGetRecoveryContinuationSelection -Release $release -ContinuationDecision (New-Rp3Decision -Index 2)
    Assert-CanonicalSelection $s 2
}
$results += Invoke-Case 'S03' 'RP-3C selection records recovery continuation source' {
    $s = ConvertFrom-NuGetRecoveryContinuationSelection -Release $release -ContinuationDecision (New-Rp3Decision)
    Assert-Eq 'RP3RecoveryContinuation' $s.SelectionSource 'selection source'
}
$results += Invoke-Case 'S04' 'RP-3C selection preserves genuine historical operation provenance' {
    $d = New-Rp3Decision
    $s = ConvertFrom-NuGetRecoveryContinuationSelection -Release $release -ContinuationDecision $d
    Assert-Eq $d.OperationId $s.LegacyHistoricalPublicationOperationId 'historical operation provenance'
    if (-not [object]::ReferenceEquals($d,$s.SelectionEvidence)) { throw 'RP-3C selection did not preserve the producer result as SelectionEvidence.' }
}
$results += Invoke-Case 'S05' 'RP-3C non-Converged decision is rejected' {
    Assert-Throws { ConvertFrom-NuGetRecoveryContinuationSelection -Release $release -ContinuationDecision (New-Rp3Decision -RecoveryState 'Unresolved') } 'requires Converged recovery'
}
$results += Invoke-Case 'S06' 'RP-3C MayContinue false is rejected' {
    Assert-Throws { ConvertFrom-NuGetRecoveryContinuationSelection -Release $release -ContinuationDecision (New-Rp3Decision -MayContinue $false) } 'not eligible to continue'
}
$results += Invoke-Case 'S07' 'RP-3C non-ContinuationEligible disposition is rejected' {
    Assert-Throws { ConvertFrom-NuGetRecoveryContinuationSelection -Release $release -ContinuationDecision (New-Rp3Decision -Disposition 'RecoveredEnd') } 'ContinuationEligible'
}
$results += Invoke-Case 'S08' 'RP-3C next package disagreement with R is rejected' {
    $d = New-Rp3Decision
    $d.NextPackage.Id = 'Wrong'
    Assert-Throws { ConvertFrom-NuGetRecoveryContinuationSelection -Release $release -ContinuationDecision $d } 'does not match canonical release position'
}
$results += Invoke-Case 'S09' 'RP-3C successor not NotAttempted is rejected' {
    Assert-Throws { ConvertFrom-NuGetRecoveryContinuationSelection -Release $release -ContinuationDecision (New-Rp3Decision -MutationState 'Accepted') } 'must be exactly NotAttempted'
}
$results += Invoke-Case 'S10' 'RP-5D ContinuationEligible projects canonical selection' {
    $s = ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult (New-Rp5Result)
    Assert-CanonicalSelection $s 1
}
$results += Invoke-Case 'S11' 'RP-5D selection preserves exact R N package identity' {
    $s = ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult (New-Rp5Result -Index 2)
    Assert-CanonicalSelection $s 2
}
$results += Invoke-Case 'S12' 'RP-5D selection records effective-release source' {
    $s = ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult (New-Rp5Result)
    Assert-Eq 'RP5EffectiveRelease' $s.SelectionSource 'selection source'
}
$results += Invoke-Case 'S13' 'RP-5D selection has no historical publication operation ID' {
    $r = New-Rp5Result
    $s = ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult $r
    Assert-Null $s.LegacyHistoricalPublicationOperationId 'legacy provenance'
    if (-not [object]::ReferenceEquals($r,$s.SelectionEvidence)) { throw 'RP-5D selection did not preserve the producer result as SelectionEvidence.' }
}
$results += Invoke-Case 'S14' 'RP-5D non-ContinuationEligible result is rejected' {
    Assert-Throws { ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult (New-Rp5Result -State 'ReleaseComplete') } 'ContinuationEligible'
}
$results += Invoke-Case 'S15' 'RP-5D release identity mismatch is rejected' {
    $r = New-Rp5Result
    $r.ReleaseIdentitySha256 = 'e' * 64
    Assert-Throws { ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult $r } 'does not belong to the supplied canonical release identity'
}
$results += Invoke-Case 'S16' 'RP-5D package disagreement with R is rejected' {
    $r = New-Rp5Result
    $r.PackageId = 'Wrong'
    Assert-Throws { ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult $r } 'does not match canonical release position'
}
$results += Invoke-Case 'S17' 'both producer routes normalize to the same canonical identity shape' {
    $a = ConvertFrom-NuGetRecoveryContinuationSelection -Release $release -ContinuationDecision (New-Rp3Decision)
    $b = ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult (New-Rp5Result)
    $aNames = @($a.PSObject.Properties.Name)
    $bNames = @($b.PSObject.Properties.Name)
    Assert-Eq ($aNames -join '|') ($bNames -join '|') 'canonical property shape'
    foreach ($name in @('ReleaseIdentityProfile','ReleaseIdentitySha256','PackageIndex','PackageId','PackageVersion','AdmittedSha256','SourceCommit','ReleaseAuthorityTag')) {
        Assert-Eq $a.$name $b.$name "canonical field $name"
    }
}
$results += Invoke-Case 'S18' 'selection exposes no mutation authority' {
    foreach ($s in @(
        (ConvertFrom-NuGetRecoveryContinuationSelection -Release $release -ContinuationDecision (New-Rp3Decision)),
        (ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult (New-Rp5Result)))) {
        foreach ($name in @('RemoteAdmission','ContinuationGrant','ContinuationOperationId','Registry','Credential','ArtifactPath','PUTAuthority','MutationAuthority','NextPackage','SuffixAuthority')) {
            if ($null -ne $s.PSObject.Properties[$name]) { throw "Selection leaked forbidden authority '$name'." }
        }
    }
}
$results += Invoke-Case 'S19' 'RP-5D projection evidence operation IDs do not create legacy provenance' {
    $r = New-Rp5Result
    $r.ProjectionEvidence = @([pscustomobject]@{OperationId='old-operation';HistoricalPublicationOperationId='older-operation'})
    $s = ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult $r
    Assert-Null $s.LegacyHistoricalPublicationOperationId 'legacy provenance mined from projection evidence'
}
$results += Invoke-Case 'S20' 'descriptive timestamp evidence does not affect canonical selection' {
    $a = New-Rp5Result
    $b = New-Rp5Result
    $a | Add-Member TimestampUtc '2000-01-01T00:00:00.0000000Z'
    $b | Add-Member TimestampUtc '2099-01-01T00:00:00.0000000Z'
    $sa = ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult $a
    $sb = ConvertFrom-NuGetEffectiveReleaseContinuationSelection -Release $release -EffectiveReleaseResult $b
    foreach ($name in @('ReleaseIdentityProfile','ReleaseIdentitySha256','PackageIndex','PackageId','PackageVersion','AdmittedSha256','SourceCommit','ReleaseAuthorityTag','SelectionSource','LegacyHistoricalPublicationOperationId')) {
        Assert-Eq $sa.$name $sb.$name "timestamp-independent field $name"
    }
}

$results | Format-Table -AutoSize
$failed = @($results | Where-Object Outcome -eq 'FAIL')
if ($failed.Count) {
    $failed | ForEach-Object { Write-Host "$($_.Id) $($_.Name): $($_.Error)" -ForegroundColor Red }
    throw "RP-6B.1 continuation selection conformance failed: $($failed.Count) case(s)."
}
Write-Host "RP-6B.1 continuation selection conformance passed: $($results.Count)/$($results.Count)."
