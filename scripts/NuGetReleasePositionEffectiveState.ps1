Set-StrictMode -Version Latest

function Get-Rp5ClaimDiagnosticCode {
    param([Parameter(Mandatory)][object]$Claim)
    if($null-eq$Claim.PSObject.Properties['Diagnostic']-or$null-eq$Claim.Diagnostic){return $null}
    if($null-ne$Claim.Diagnostic.PSObject.Properties['Code']){return [string]$Claim.Diagnostic.Code}
    return $null
}

function Test-Rp5RecoverableRejectedClaim {
    param([Parameter(Mandatory)][object]$Claim)
    return ([string]$Claim.RawMutationState-ceq'Rejected'-and[int]$Claim.StatusCode-eq409-and(Get-Rp5ClaimDiagnosticCode $Claim)-ceq'ExistingIdentityConflict')
}

function Get-NuGetReleasePositionEffectiveState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ReleaseIdentityProfile,
        [Parameter(Mandatory)][string]$ReleaseIdentitySha256,
        [Parameter(Mandatory)][int]$PackageIndex,
        [Parameter(Mandatory)][object[]]$Claims
    )

    if($ReleaseIdentityProfile-ceq''-or$ReleaseIdentitySha256-cnotmatch'^[0-9a-f]{64}$'){throw [System.ArgumentException]::new('A canonical release identity is required.')}
    if($PackageIndex-lt0){throw [System.ArgumentOutOfRangeException]::new('PackageIndex')}
    $items=@($Claims)
    if($items.Count-eq0){throw [System.InvalidOperationException]::new('At least one normalized evidence claim is required for position reduction.')}

    $packageId=$null;$packageVersion=$null;$admittedSha=$null
    foreach($claim in $items){
        foreach($name in @('ReleaseIdentityProfile','ReleaseIdentitySha256','PackageIndex','PackageId','PackageVersion','AdmittedSha256','RawDisposition','RawMutationState')){
            if($null-eq$claim.PSObject.Properties[$name]){throw [System.InvalidOperationException]::new("Normalized evidence claim must contain $name.")}
        }
        if([string]$claim.ReleaseIdentityProfile-cne$ReleaseIdentityProfile-or[string]$claim.ReleaseIdentitySha256-cne$ReleaseIdentitySha256-or[int]$claim.PackageIndex-ne$PackageIndex){
            throw [System.InvalidOperationException]::new('All normalized evidence claims must belong to exactly the requested release identity and package position.')
        }
        if([string]$claim.AdmittedSha256-cnotmatch'^[0-9a-f]{64}$'){throw [System.InvalidOperationException]::new('Normalized evidence claim contains non-canonical admitted SHA-256.')}
        if($null-eq$packageId){$packageId=[string]$claim.PackageId;$packageVersion=[string]$claim.PackageVersion;$admittedSha=[string]$claim.AdmittedSha256}
        elseif([string]$claim.PackageId-cne$packageId-or[string]$claim.PackageVersion-cne$packageVersion-or[string]$claim.AdmittedSha256-cne$admittedSha){
            throw [System.InvalidOperationException]::new('Normalized evidence claims disagree on exact package identity for the requested position.')
        }
        if([string]$claim.RawDisposition-cnotin@('PublicationMutation','Recovery')){throw [System.InvalidOperationException]::new("Unsupported normalized evidence disposition '$($claim.RawDisposition)'.")}
        if([string]$claim.RawMutationState-cnotin@('Accepted','NotAttempted','Attempting','Indeterminate','Rejected')){throw [System.InvalidOperationException]::new("Unsupported normalized mutation state '$($claim.RawMutationState)'.")}
        if([string]$claim.RawDisposition-ceq'Recovery'){
            if($null-eq$claim.PSObject.Properties['RecoveryState']-or[string]$claim.RecoveryState-cnotin@('Converged','Conflict','Unresolved')){throw [System.InvalidOperationException]::new('Recovery claim must contain a terminal recovery state.')}
            if([string]$claim.RawMutationState-cnotin@('Rejected','Attempting','Indeterminate')){throw [System.InvalidOperationException]::new('Recovery claim must remain bound to a recovery-eligible historical mutation state.')}
            if([string]$claim.RawMutationState-ceq'Rejected'-and-not(Test-Rp5RecoverableRejectedClaim $claim)){throw [System.InvalidOperationException]::new('Recovery claim cannot be based on a non-recoverable rejection.')}
        }
    }

    $blocked=@($items|Where-Object{
        ([string]$_.RawDisposition-ceq'Recovery'-and[string]$_.RecoveryState-ceq'Conflict') -or
        ([string]$_.RawDisposition-ceq'PublicationMutation'-and[string]$_.RawMutationState-ceq'Rejected'-and-not(Test-Rp5RecoverableRejectedClaim $_))
    })
    if($blocked.Count-gt0){return [pscustomobject]@{EffectiveState='Blocked';ReleaseIdentityProfile=$ReleaseIdentityProfile;ReleaseIdentitySha256=$ReleaseIdentitySha256;PackageIndex=$PackageIndex;PackageId=$packageId;PackageVersion=$packageVersion;AdmittedSha256=$admittedSha}}

    $satisfied=@($items|Where-Object{
        ([string]$_.RawDisposition-ceq'PublicationMutation'-and[string]$_.RawMutationState-ceq'Accepted') -or
        ([string]$_.RawDisposition-ceq'Recovery'-and[string]$_.RecoveryState-ceq'Converged')
    })
    if($satisfied.Count-gt0){return [pscustomobject]@{EffectiveState='Satisfied';ReleaseIdentityProfile=$ReleaseIdentityProfile;ReleaseIdentitySha256=$ReleaseIdentitySha256;PackageIndex=$PackageIndex;PackageId=$packageId;PackageVersion=$packageVersion;AdmittedSha256=$admittedSha}}

    $indeterminate=@($items|Where-Object{
        ([string]$_.RawDisposition-ceq'Recovery'-and[string]$_.RecoveryState-ceq'Unresolved') -or
        ([string]$_.RawDisposition-ceq'PublicationMutation'-and[string]$_.RawMutationState-cin@('Attempting','Indeterminate')) -or
        ([string]$_.RawDisposition-ceq'PublicationMutation'-and(Test-Rp5RecoverableRejectedClaim $_))
    })
    if($indeterminate.Count-gt0){return [pscustomobject]@{EffectiveState='Indeterminate';ReleaseIdentityProfile=$ReleaseIdentityProfile;ReleaseIdentitySha256=$ReleaseIdentitySha256;PackageIndex=$PackageIndex;PackageId=$packageId;PackageVersion=$packageVersion;AdmittedSha256=$admittedSha}}

    if(@($items|Where-Object{[string]$_.RawDisposition-cne'PublicationMutation'-or[string]$_.RawMutationState-cne'NotAttempted'}).Count-eq0){
        return [pscustomobject]@{EffectiveState='Unsatisfied';ReleaseIdentityProfile=$ReleaseIdentityProfile;ReleaseIdentitySha256=$ReleaseIdentitySha256;PackageIndex=$PackageIndex;PackageId=$packageId;PackageVersion=$packageVersion;AdmittedSha256=$admittedSha}
    }
    throw [System.InvalidOperationException]::new('Normalized evidence claims do not reduce under the frozen RP-5 D2 precedence rules.')
}
