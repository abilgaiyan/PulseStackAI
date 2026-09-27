Set-StrictMode -Version Latest

$script:AdmittedReleaseIdentityProfile = '1'
$script:AdmittedReleaseIdentityHeader = 'pulsestack.nuget.admitted-release.v1'

function Assert-AdmittedReleaseIdentityText {
    param(
        [Parameter(Mandatory)] [string] $Value,
        [Parameter(Mandatory)] [string] $Name
    )

    if ([string]::IsNullOrEmpty($Value)) {
        throw [System.ArgumentException]::new("$Name is required.")
    }
    if ($Value.Contains("`r") -or $Value.Contains("`n")) {
        throw [System.ArgumentException]::new("$Name must not contain CR or LF.")
    }
}

function Get-AdmittedReleaseUtf8Bytes {
    param(
        [Parameter(Mandatory)] [string] $Value,
        [Parameter(Mandatory)] [string] $Name
    )

    Assert-AdmittedReleaseIdentityText -Value $Value -Name $Name
    try {
        $encoding = [System.Text.UTF8Encoding]::new($false, $true)
        $bytes = [byte[]]$encoding.GetBytes($Value)
        Write-Output -NoEnumerate $bytes
    }
    catch {
        throw [System.ArgumentException]::new("$Name must be valid Unicode encodable as UTF-8.", $_.Exception)
    }
}

function Get-AdmittedReleaseByteLength {
    param(
        [Parameter(Mandatory)] [string] $Value,
        [Parameter(Mandatory)] [string] $Name
    )

    [byte[]]$bytes = Get-AdmittedReleaseUtf8Bytes -Value $Value -Name $Name
    return $bytes.Count
}

function Get-NuGetAdmittedReleaseIdentityProjection {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $AdmittedPackageSet,
        [string] $Profile = $script:AdmittedReleaseIdentityProfile
    )

    if ($Profile -cne $script:AdmittedReleaseIdentityProfile) {
        throw [System.ArgumentException]::new("Unsupported admitted release identity profile '$Profile'.")
    }
    if ($null -eq $AdmittedPackageSet) {
        throw [System.ArgumentNullException]::new('AdmittedPackageSet')
    }

    foreach ($name in @('ProductionKind','SourceCommit','PackageVersion','ReleaseAuthorityTag','Packages')) {
        if ($null -eq $AdmittedPackageSet.PSObject.Properties[$name]) {
            throw [System.ArgumentException]::new("AdmittedPackageSet must contain $name.")
        }
    }

    $productionKind = [string]$AdmittedPackageSet.ProductionKind
    if ($productionKind -cne 'Release') {
        throw [System.ArgumentException]::new("Admitted release identity requires ProductionKind exactly 'Release'.")
    }

    $sourceCommit = [string]$AdmittedPackageSet.SourceCommit
    if ($sourceCommit -cnotmatch '^[0-9a-f]{40}$') {
        throw [System.ArgumentException]::new('SourceCommit must be canonical lowercase 40-hex.')
    }

    $packageVersion = [string]$AdmittedPackageSet.PackageVersion
    Assert-AdmittedReleaseIdentityText -Value $packageVersion -Name 'PackageVersion'
    $releaseAuthorityTag = [string]$AdmittedPackageSet.ReleaseAuthorityTag
    Assert-AdmittedReleaseIdentityText -Value $releaseAuthorityTag -Name 'ReleaseAuthorityTag'

    $packages = @($AdmittedPackageSet.Packages)
    if ($packages.Count -eq 0) {
        throw [System.ArgumentException]::new('AdmittedPackageSet must contain at least one package.')
    }

    $projectedPackages = [System.Collections.Generic.List[object]]::new()
    for ($i = 0; $i -lt $packages.Count; $i++) {
        $package = $packages[$i]
        foreach ($name in @('Id','Version','Sha256')) {
            if ($null -eq $package.PSObject.Properties[$name]) {
                throw [System.ArgumentException]::new("Package at index $i must contain $name.")
            }
        }

        $id = [string]$package.Id
        $version = [string]$package.Version
        $sha256 = [string]$package.Sha256
        Assert-AdmittedReleaseIdentityText -Value $id -Name "Packages[$i].Id"
        Assert-AdmittedReleaseIdentityText -Value $version -Name "Packages[$i].Version"
        if ($version -cne $packageVersion) {
            throw [System.ArgumentException]::new("Package at index $i version does not match PackageVersion.")
        }
        if ($sha256 -cnotmatch '^[0-9a-f]{64}$') {
            throw [System.ArgumentException]::new("Package at index $i Sha256 must be canonical lowercase 64-hex.")
        }

        $projectedPackages.Add([pscustomobject]@{
            Index   = $i
            Id      = $id
            Version = $version
            Sha256  = $sha256
        })
    }

    return [pscustomobject]@{
        Profile             = $script:AdmittedReleaseIdentityProfile
        ProductionKind      = $productionKind
        SourceCommit        = $sourceCommit
        PackageVersion      = $packageVersion
        ReleaseAuthorityTag = $releaseAuthorityTag
        PackageCount        = $projectedPackages.Count
        Packages            = @($projectedPackages)
    }
}

function Get-NuGetAdmittedReleaseIdentityCanonicalBytes {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $Projection
    )

    if ([string]$Projection.Profile -cne $script:AdmittedReleaseIdentityProfile) {
        throw [System.ArgumentException]::new('Projection profile is not the supported admitted release identity profile.')
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add($script:AdmittedReleaseIdentityHeader)

    $productionKindLength = Get-AdmittedReleaseByteLength -Value ([string]$Projection.ProductionKind) -Name 'ProductionKind'
    $sourceCommitLength = Get-AdmittedReleaseByteLength -Value ([string]$Projection.SourceCommit) -Name 'SourceCommit'
    $packageVersionLength = Get-AdmittedReleaseByteLength -Value ([string]$Projection.PackageVersion) -Name 'PackageVersion'
    $releaseAuthorityTagLength = Get-AdmittedReleaseByteLength -Value ([string]$Projection.ReleaseAuthorityTag) -Name 'ReleaseAuthorityTag'
    $lines.Add("productionKind:${productionKindLength}:$([string]$Projection.ProductionKind)")
    $lines.Add("sourceCommit:${sourceCommitLength}:$([string]$Projection.SourceCommit)")
    $lines.Add("packageVersion:${packageVersionLength}:$([string]$Projection.PackageVersion)")
    $lines.Add("releaseAuthorityTag:${releaseAuthorityTagLength}:$([string]$Projection.ReleaseAuthorityTag)")

    $packages = @($Projection.Packages)
    if ([int]$Projection.PackageCount -ne $packages.Count -or $packages.Count -eq 0) {
        throw [System.ArgumentException]::new('Projection PackageCount must exactly match its non-empty Packages sequence.')
    }
    $lines.Add("packageCount:$($packages.Count)")

    for ($i = 0; $i -lt $packages.Count; $i++) {
        $package = $packages[$i]
        if ([int]$package.Index -ne $i) {
            throw [System.ArgumentException]::new("Projection package index $i is not canonical.")
        }
        if ([string]$package.Version -cne [string]$Projection.PackageVersion) {
            throw [System.ArgumentException]::new("Projection package at index $i version does not match PackageVersion.")
        }
        if ([string]$package.Sha256 -cnotmatch '^[0-9a-f]{64}$') {
            throw [System.ArgumentException]::new("Projection package at index $i Sha256 must be canonical lowercase 64-hex.")
        }

        $idLength = Get-AdmittedReleaseByteLength -Value ([string]$package.Id) -Name "Packages[$i].Id"
        $versionLength = Get-AdmittedReleaseByteLength -Value ([string]$package.Version) -Name "Packages[$i].Version"
        $lines.Add("package:${i}:${idLength}:$([string]$package.Id):${versionLength}:$([string]$package.Version):$([string]$package.Sha256)")
    }

    $canonicalText = ($lines -join "`n") + "`n"
    $encoding = [System.Text.UTF8Encoding]::new($false, $true)
    $bytes = [byte[]]$encoding.GetBytes($canonicalText)
    Write-Output -NoEnumerate $bytes
}

function Get-NuGetAdmittedReleaseIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $AdmittedPackageSet,
        [string] $Profile = $script:AdmittedReleaseIdentityProfile
    )

    $projection = Get-NuGetAdmittedReleaseIdentityProjection -AdmittedPackageSet $AdmittedPackageSet -Profile $Profile
    [byte[]]$bytes = Get-NuGetAdmittedReleaseIdentityCanonicalBytes -Projection $projection
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $digest = $sha.ComputeHash($bytes)
    }
    finally {
        $sha.Dispose()
    }

    $hex = -join ($digest | ForEach-Object { $_.ToString('x2') })
    return [pscustomobject]@{
        Profile    = $script:AdmittedReleaseIdentityProfile
        Sha256     = $hex
        ByteCount  = $bytes.Count
        Projection = $projection
    }
}
