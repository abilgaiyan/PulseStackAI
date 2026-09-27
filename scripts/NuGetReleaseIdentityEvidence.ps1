Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'NuGetAdmittedReleaseIdentity.ps1')

function Get-NuGetReleaseIdentityEvidence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $AdmittedPackageSet
    )

    if ($null -eq $AdmittedPackageSet) {
        throw [System.ArgumentNullException]::new('AdmittedPackageSet')
    }

    $semanticSet = [pscustomobject]@{
        ProductionKind      = 'Release'
        SourceCommit        = [string]$AdmittedPackageSet.SourceCommit
        PackageVersion      = [string]$AdmittedPackageSet.PackageVersion
        ReleaseAuthorityTag = [string]$AdmittedPackageSet.ReleaseAuthorityTag
        Packages            = @($AdmittedPackageSet.Packages | ForEach-Object {
            [pscustomobject]@{
                Id      = [string]$_.Id
                Version = [string]$_.Version
                Sha256  = [string]$_.Sha256
            }
        })
    }

    $identity = Get-NuGetAdmittedReleaseIdentity -AdmittedPackageSet $semanticSet
    return [pscustomobject]@{
        ReleaseIdentityProfile = [string]$identity.Profile
        ReleaseIdentitySha256  = [string]$identity.Sha256
    }
}

function Add-NuGetReleaseIdentityEvidence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $Evidence,
        [Parameter(Mandatory)] [object] $AdmittedPackageSet
    )

    $identity = Get-NuGetReleaseIdentityEvidence -AdmittedPackageSet $AdmittedPackageSet

    $profile = $Evidence.PSObject.Properties['releaseIdentityProfile']
    $sha256 = $Evidence.PSObject.Properties['releaseIdentitySha256']
    if ($null -ne $profile -or $null -ne $sha256) {
        throw [System.InvalidOperationException]::new('Release identity evidence cannot be overwritten or reopened.')
    }

    $Evidence | Add-Member -NotePropertyName 'releaseIdentityProfile' -NotePropertyValue $identity.ReleaseIdentityProfile
    $Evidence | Add-Member -NotePropertyName 'releaseIdentitySha256' -NotePropertyValue $identity.ReleaseIdentitySha256
    return $Evidence
}
