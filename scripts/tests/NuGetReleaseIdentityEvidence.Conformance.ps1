Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts/NuGetReleaseIdentityEvidence.ps1')

function Assert-Eq($Expected,$Actual,[string]$Message){if($Expected-cne$Actual){throw "$Message Expected '$Expected', actual '$Actual'."}}
function Assert-True([bool]$Value,[string]$Message){if(-not$Value){throw $Message}}
function Assert-Throws([scriptblock]$Body,[string]$Contains){try{&$Body;throw 'Expected exception was not thrown.'}catch{if($_.Exception.Message-eq'Expected exception was not thrown.'){throw};if($_.Exception.Message-notlike"*$Contains*"){throw "Unexpected exception: $($_.Exception.Message)"}}}
function Invoke-Case([string]$Id,[string]$Name,[scriptblock]$Body){try{&$Body;[pscustomobject]@{Id=$Id;Name=$Name;Outcome='PASS'}}catch{[pscustomobject]@{Id=$Id;Name=$Name;Outcome='FAIL';Error=$_.Exception.Message}}}

function New-AdmittedSet {
    [pscustomobject]@{
        SourceCommit='0123456789abcdef0123456789abcdef01234567'
        VersionPrefix='1.0.4'
        PackageVersion='1.0.4'
        ReleaseAuthorityTag='v1.0.4'
        Packages=@(
            [pscustomobject]@{Id='PulseStack.Core';Version='1.0.4';FilePath='C:\fixture\PulseStack.Core.1.0.4.nupkg';Sha256=('a'*64)},
            [pscustomobject]@{Id='PulseStack.Agents';Version='1.0.4';FilePath='C:\fixture\PulseStack.Agents.1.0.4.nupkg';Sha256=('b'*64)}
        )
    }
}

$results=@()
$results+=Invoke-Case 'B01' 'semantic adapter produces profile 1' {
    $e=Get-NuGetReleaseIdentityEvidence (New-AdmittedSet)
    Assert-Eq '1' $e.ReleaseIdentityProfile 'profile'
}
$results+=Invoke-Case 'B02' 'evidence digest equals independent RP-5A derivation' {
    $set=New-AdmittedSet
    $e=Get-NuGetReleaseIdentityEvidence $set
    $semantic=[pscustomobject]@{ProductionKind='Release';SourceCommit=$set.SourceCommit;PackageVersion=$set.PackageVersion;ReleaseAuthorityTag=$set.ReleaseAuthorityTag;Packages=$set.Packages}
    $expected=Get-NuGetAdmittedReleaseIdentity $semantic
    Assert-Eq $expected.Sha256 $e.ReleaseIdentitySha256 'digest'
}
$results+=Invoke-Case 'B03' 'artifact path is not identity authority' {
    $a=New-AdmittedSet;$b=New-AdmittedSet;$b.Packages[0].FilePath='D:\elsewhere\renamed.nupkg'
    Assert-Eq (Get-NuGetReleaseIdentityEvidence $a).ReleaseIdentitySha256 (Get-NuGetReleaseIdentityEvidence $b).ReleaseIdentitySha256 'artifact path independence'
}
$results+=Invoke-Case 'B04' 'canonical package order remains identity-sensitive' {
    $a=New-AdmittedSet;$b=New-AdmittedSet;$b.Packages=@($b.Packages[1],$b.Packages[0])
    $left=(Get-NuGetReleaseIdentityEvidence $a).ReleaseIdentitySha256;$right=(Get-NuGetReleaseIdentityEvidence $b).ReleaseIdentitySha256
    Assert-True ($left -cne $right) 'package order did not affect release identity'
}
$results+=Invoke-Case 'B05' 'admitted SHA remains identity-sensitive' {
    $a=New-AdmittedSet;$b=New-AdmittedSet;$b.Packages[1].Sha256=('c'*64)
    $left=(Get-NuGetReleaseIdentityEvidence $a).ReleaseIdentitySha256;$right=(Get-NuGetReleaseIdentityEvidence $b).ReleaseIdentitySha256
    Assert-True ($left -cne $right) 'package SHA did not affect release identity'
}
$results+=Invoke-Case 'B06' 'evidence plumbing adds only identity fields' {
    $set=New-AdmittedSet;$e=[pscustomobject]@{schemaVersion='1.0';sourceCommit=$set.SourceCommit}
    $before=@($e.PSObject.Properties.Name)
    Add-NuGetReleaseIdentityEvidence -Evidence $e -AdmittedPackageSet $set | Out-Null
    $after=@($e.PSObject.Properties.Name)
    Assert-Eq '1' ([string]$e.releaseIdentityProfile) 'profile'
    Assert-True ([string]$e.releaseIdentitySha256 -match '^[0-9a-f]{64}$') 'digest is not canonical SHA-256'
    Assert-Eq '1.0' ([string]$e.schemaVersion) 'existing evidence changed'
    Assert-Eq $set.SourceCommit ([string]$e.sourceCommit) 'existing provenance changed'
    Assert-Eq ([string]($before.Count+2)) ([string]$after.Count) 'unexpected evidence mutation'
}
$results+=Invoke-Case 'B07' 'identity evidence cannot be overwritten' {
    $set=New-AdmittedSet;$e=[pscustomobject]@{releaseIdentityProfile='1';releaseIdentitySha256=('d'*64)}
    Assert-Throws {Add-NuGetReleaseIdentityEvidence -Evidence $e -AdmittedPackageSet $set} 'cannot be overwritten or reopened'
}
$results+=Invoke-Case 'B08' 'pre-RP-5 evidence may remain without identity fields' {
    $old=[pscustomobject]@{schemaVersion='1.0';operationId='00000000-0000-0000-0000-000000000001';sourceCommit='0123456789abcdef0123456789abcdef01234567'}
    Assert-True ($null -eq $old.PSObject.Properties['releaseIdentityProfile']) 'old evidence unexpectedly requires profile'
    Assert-True ($null -eq $old.PSObject.Properties['releaseIdentitySha256']) 'old evidence unexpectedly requires digest'
}
$results+=Invoke-Case 'B09' 'invalid admitted semantics fail during identity derivation' {
    $set=New-AdmittedSet;$set.Packages[0].Sha256=('A'*64)
    Assert-Throws {Get-NuGetReleaseIdentityEvidence $set} 'canonical lowercase 64-hex'
}

$results|Format-Table -AutoSize
$failed=@($results|Where-Object Outcome -eq 'FAIL')
if($failed.Count){$failed|ForEach-Object{Write-Host "$($_.Id) $($_.Name): $($_.Error)" -ForegroundColor Red};throw "RP-5B release identity evidence conformance failed: $($failed.Count) case(s)."}
Write-Host "RP-5B release identity evidence conformance passed: $($results.Count)/$($results.Count)."
