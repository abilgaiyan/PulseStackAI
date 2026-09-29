Set-StrictMode -Version Latest

function Assert-Rp5DProperty {
    param([Parameter(Mandatory)][object]$Object,[Parameter(Mandatory)][string]$Name,[Parameter(Mandatory)][string]$Context)
    $property=$Object.PSObject.Properties[$Name]
    if($null-eq$property){throw [System.InvalidOperationException]::new("$Context must contain $Name.")}
    return $property.Value
}

function Get-NuGetWholeReleaseEffectiveState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Release,
        [Parameter(Mandatory)][object[]]$PositionStates
    )

    $profile=[string](Assert-Rp5DProperty $Release 'Profile' 'Release')
    $releaseShaProperty=$Release.PSObject.Properties['Sha256']
    if($null-eq$releaseShaProperty){$releaseShaProperty=$Release.PSObject.Properties['ReleaseIdentitySha256']}
    if($profile-ceq''-or$null-eq$releaseShaProperty-or[string]$releaseShaProperty.Value-cnotmatch'^[0-9a-f]{64}$'){
        throw [System.InvalidOperationException]::new('Release must contain a canonical release identity.')
    }
    $releaseSha=[string]$releaseShaProperty.Value
    $sourceCommit=[string](Assert-Rp5DProperty $Release 'SourceCommit' 'Release')
    $releaseAuthorityTag=[string](Assert-Rp5DProperty $Release 'ReleaseAuthorityTag' 'Release')
    $packageCount=[int](Assert-Rp5DProperty $Release 'PackageCount' 'Release')
    $packages=@(Assert-Rp5DProperty $Release 'Packages' 'Release')
    $states=@($PositionStates)
    if($packageCount-le0-or$packages.Count-ne$packageCount-or$states.Count-ne$packageCount){
        throw [System.InvalidOperationException]::new('Whole-release projection requires exactly one effective position state for every canonical package position.')
    }

    for($i=0;$i-lt$packageCount;$i++){
        $state=$states[$i];$package=$packages[$i]
        foreach($name in @('EffectiveState','ReleaseIdentityProfile','ReleaseIdentitySha256','PackageIndex','PackageId','PackageVersion','AdmittedSha256')){
            $null=Assert-Rp5DProperty $state $name "Effective position[$i]"
        }
        if([string]$state.ReleaseIdentityProfile-cne$profile-or[string]$state.ReleaseIdentitySha256-cne$releaseSha-or[int]$state.PackageIndex-ne$i){
            throw [System.InvalidOperationException]::new("Effective position[$i] does not belong to the exact release identity and canonical position.")
        }
        if([string]$state.PackageId-cne[string]$package.Id-or[string]$state.PackageVersion-cne[string]$package.Version-or[string]$state.AdmittedSha256-cne[string]$package.Sha256){
            throw [System.InvalidOperationException]::new("Effective position[$i] does not match the canonical package identity.")
        }
        if([string]$state.EffectiveState-cnotin@('Satisfied','Unsatisfied','Blocked','Indeterminate')){
            throw [System.InvalidOperationException]::new("Effective position[$i] contains unsupported state '$($state.EffectiveState)'.")
        }
    }

    $blocked=@($states|Where-Object{[string]$_.EffectiveState-ceq'Blocked'})
    if($blocked.Count-gt0){
        return [pscustomobject]@{ReleaseState='Blocked';Reason='BlockedPosition';ReleaseIdentityProfile=$profile;ReleaseIdentitySha256=$releaseSha;PackageIndex=$null;PackageId=$null;PackageVersion=$null;AdmittedSha256=$null;SourceCommit=$sourceCommit;ReleaseAuthorityTag=$releaseAuthorityTag;ProjectionEvidence=@($states)}
    }

    if(@($states|Where-Object{[string]$_.EffectiveState-cne'Satisfied'}).Count-eq0){
        return [pscustomobject]@{ReleaseState='ReleaseComplete';Reason=$null;ReleaseIdentityProfile=$profile;ReleaseIdentitySha256=$releaseSha;PackageIndex=$null;PackageId=$null;PackageVersion=$null;AdmittedSha256=$null;SourceCommit=$sourceCommit;ReleaseAuthorityTag=$releaseAuthorityTag;ProjectionEvidence=@($states)}
    }

    $first=-1
    for($i=0;$i-lt$states.Count;$i++){if([string]$states[$i].EffectiveState-cne'Satisfied'){$first=$i;break}}
    if([string]$states[$first].EffectiveState-ceq'Indeterminate'){
        return [pscustomobject]@{ReleaseState='Indeterminate';Reason='IndeterminatePosition';ReleaseIdentityProfile=$profile;ReleaseIdentitySha256=$releaseSha;PackageIndex=$first;PackageId=[string]$states[$first].PackageId;PackageVersion=[string]$states[$first].PackageVersion;AdmittedSha256=[string]$states[$first].AdmittedSha256;SourceCommit=$sourceCommit;ReleaseAuthorityTag=$releaseAuthorityTag;ProjectionEvidence=@($states)}
    }

    $suffix=if($first+1-lt$states.Count){@($states[($first+1)..($states.Count-1)])}else{@()}
    if(@($suffix|Where-Object{[string]$_.EffectiveState-ceq'Satisfied'}).Count-gt0){
        return [pscustomobject]@{ReleaseState='Blocked';Reason='OutOfSequenceSatisfaction';ReleaseIdentityProfile=$profile;ReleaseIdentitySha256=$releaseSha;PackageIndex=$first;PackageId=[string]$states[$first].PackageId;PackageVersion=[string]$states[$first].PackageVersion;AdmittedSha256=[string]$states[$first].AdmittedSha256;SourceCommit=$sourceCommit;ReleaseAuthorityTag=$releaseAuthorityTag;ProjectionEvidence=@($states)}
    }
    if(@($suffix|Where-Object{[string]$_.EffectiveState-ceq'Indeterminate'}).Count-gt0){
        return [pscustomobject]@{ReleaseState='Indeterminate';Reason='EvidenceBeyondContinuationBoundary';ReleaseIdentityProfile=$profile;ReleaseIdentitySha256=$releaseSha;PackageIndex=$first;PackageId=[string]$states[$first].PackageId;PackageVersion=[string]$states[$first].PackageVersion;AdmittedSha256=[string]$states[$first].AdmittedSha256;SourceCommit=$sourceCommit;ReleaseAuthorityTag=$releaseAuthorityTag;ProjectionEvidence=@($states)}
    }

    $selected=$states[$first]
    return [pscustomobject]@{ReleaseState='ContinuationEligible';Reason=$null;ReleaseIdentityProfile=$profile;ReleaseIdentitySha256=$releaseSha;PackageIndex=$first;PackageId=[string]$selected.PackageId;PackageVersion=[string]$selected.PackageVersion;AdmittedSha256=[string]$selected.AdmittedSha256;SourceCommit=$sourceCommit;ReleaseAuthorityTag=$releaseAuthorityTag;ProjectionEvidence=@($states)}
}
