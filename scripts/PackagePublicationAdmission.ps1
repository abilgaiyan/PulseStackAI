Set-StrictMode -Version Latest

$script:PulseStackReleasePackageIds = @(
    "PulseStack.Abstractions",
    "PulseStack.Core",
    "PulseStack.Agents",
    "PulseStack.Tools",
    "PulseStack.Providers.OpenAI",
    "PulseStack.Providers.AzureOpenAI",
    "PulseStack.Providers.Ollama",
    "PulseStack.Providers.Gemini",
    "PulseStack.Providers.Groq",
    "PulseStack.Providers.OpenRouter"
)

$script:PulseStackRepositoryUrl = "https://github.com/abilgaiyan/PulseStackAI"

function New-ReleasePublicationAdmissionFailure {
    param(
        [Parameter(Mandatory)]
        [ValidateSet(
            "ManifestInvalid",
            "ReleaseIdentityInvalid",
            "PackageSetInvalid",
            "PackageFileInvalid",
            "PackageHashMismatch",
            "PackageMetadataInvalid"
        )]
        [string] $Category,

        [Parameter(Mandatory)]
        [string] $Message
    )

    $exception = [System.InvalidOperationException]::new("[$Category] $Message")
    $exception.Data["ReleasePublicationAdmissionCategory"] = $Category
    return $exception
}

function Throw-ReleasePublicationAdmissionFailure {
    param(
        [Parameter(Mandatory)]
        [string] $Category,

        [Parameter(Mandatory)]
        [string] $Message
    )

    throw (New-ReleasePublicationAdmissionFailure -Category $Category -Message $Message)
}

function Get-RequiredManifestString {
    param(
        [Parameter(Mandatory)]
        [object] $Object,

        [Parameter(Mandatory)]
        [string] $PropertyName,

        [Parameter(Mandatory)]
        [string] $Category,

        [Parameter(Mandatory)]
        [string] $Context
    )

    $property = $Object.PSObject.Properties[$PropertyName]
    if ($null -eq $property -or $property.Value -isnot [string] -or [string]::IsNullOrWhiteSpace($property.Value)) {
        Throw-ReleasePublicationAdmissionFailure -Category $Category -Message "$Context requires non-empty string '$PropertyName'."
    }

    return [string]$property.Value
}

function Read-PackageNuspecMetadata {
    param(
        [Parameter(Mandatory)]
        [string] $PackagePath
    )

    try {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    }
    catch {
        Throw-ReleasePublicationAdmissionFailure -Category "PackageFileInvalid" -Message "Package '$PackagePath' is not a readable NuGet archive."
    }

    try {
        $nuspecEntries = @(
            $archive.Entries |
                Where-Object {
                    $_.FullName -notmatch "/" -and
                    $_.FullName.EndsWith(".nuspec", [System.StringComparison]::OrdinalIgnoreCase)
                }
        )

        if ($nuspecEntries.Count -ne 1) {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageFileInvalid" -Message "Package '$PackagePath' must contain exactly one root .nuspec; found $($nuspecEntries.Count)."
        }

        try {
            $reader = [System.IO.StreamReader]::new($nuspecEntries[0].Open())
            try {
                [xml]$nuspec = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }
        }
        catch {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageFileInvalid" -Message "Package '$PackagePath' contains an invalid .nuspec."
        }

        $metadata = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
        if ($null -eq $metadata) {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageMetadataInvalid" -Message "Package '$PackagePath' has no nuspec metadata element."
        }

        $idNode = $metadata.SelectSingleNode("*[local-name()='id']")
        $versionNode = $metadata.SelectSingleNode("*[local-name()='version']")
        $repositoryNode = $metadata.SelectSingleNode("*[local-name()='repository']")

        if ($null -eq $idNode -or [string]::IsNullOrWhiteSpace($idNode.InnerText)) {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageMetadataInvalid" -Message "Package '$PackagePath' has no nuspec package id."
        }

        if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageMetadataInvalid" -Message "Package '$PackagePath' has no nuspec package version."
        }

        if ($null -eq $repositoryNode) {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageMetadataInvalid" -Message "Package '$PackagePath' has no nuspec repository metadata."
        }

        return [pscustomobject]@{
            Id               = [string]$idNode.InnerText
            Version          = [string]$versionNode.InnerText
            RepositoryUrl    = [string]$repositoryNode.GetAttribute("url")
            RepositoryType   = [string]$repositoryNode.GetAttribute("type")
            RepositoryCommit = [string]$repositoryNode.GetAttribute("commit")
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Invoke-ReleasePublicationAdmission {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $ManifestPath
    )

    $resolvedManifestPath = $null
    try {
        $resolvedManifestPath = (Resolve-Path -LiteralPath $ManifestPath -ErrorAction Stop).Path
    }
    catch {
        Throw-ReleasePublicationAdmissionFailure -Category "ManifestInvalid" -Message "Manifest '$ManifestPath' does not exist."
    }

    try {
        $manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        Throw-ReleasePublicationAdmissionFailure -Category "ManifestInvalid" -Message "Manifest '$resolvedManifestPath' is not valid JSON."
    }

    $productionKind = Get-RequiredManifestString -Object $manifest -PropertyName "productionKind" -Category "ManifestInvalid" -Context "Release manifest"
    if ($productionKind -cne "release") {
        Throw-ReleasePublicationAdmissionFailure -Category "ManifestInvalid" -Message "Release publication requires productionKind 'release'; found '$productionKind'."
    }

    $configuration = Get-RequiredManifestString -Object $manifest -PropertyName "configuration" -Category "ManifestInvalid" -Context "Release manifest"
    if ($configuration -cne "Release") {
        Throw-ReleasePublicationAdmissionFailure -Category "ManifestInvalid" -Message "Release publication requires configuration 'Release'; found '$configuration'."
    }

    $sourceCommit = Get-RequiredManifestString -Object $manifest -PropertyName "sourceCommit" -Category "ManifestInvalid" -Context "Release manifest"
    if ($sourceCommit -cnotmatch "^[0-9a-f]{40}$") {
        Throw-ReleasePublicationAdmissionFailure -Category "ManifestInvalid" -Message "sourceCommit must be a canonical lowercase 40-character Git SHA."
    }

    $versionPrefix = Get-RequiredManifestString -Object $manifest -PropertyName "versionPrefix" -Category "ManifestInvalid" -Context "Release manifest"
    $packageVersion = Get-RequiredManifestString -Object $manifest -PropertyName "packageVersion" -Category "ManifestInvalid" -Context "Release manifest"

    $releaseAuthorityProperty = $manifest.PSObject.Properties["releaseAuthority"]
    if ($null -eq $releaseAuthorityProperty -or $null -eq $releaseAuthorityProperty.Value) {
        Throw-ReleasePublicationAdmissionFailure -Category "ReleaseIdentityInvalid" -Message "Release manifest requires releaseAuthority."
    }

    $releaseAuthorityTag = Get-RequiredManifestString -Object $releaseAuthorityProperty.Value -PropertyName "tagName" -Category "ReleaseIdentityInvalid" -Context "releaseAuthority"
    $expectedTag = "v$packageVersion"
    if ($releaseAuthorityTag -cne $expectedTag) {
        Throw-ReleasePublicationAdmissionFailure -Category "ReleaseIdentityInvalid" -Message "releaseAuthority.tagName '$releaseAuthorityTag' must equal '$expectedTag'."
    }

    $packagesProperty = $manifest.PSObject.Properties["packages"]
    if ($null -eq $packagesProperty -or $null -eq $packagesProperty.Value) {
        Throw-ReleasePublicationAdmissionFailure -Category "PackageSetInvalid" -Message "Release manifest requires packages."
    }

    $packageEntries = @($packagesProperty.Value)
    if ($packageEntries.Count -ne $script:PulseStackReleasePackageIds.Count) {
        Throw-ReleasePublicationAdmissionFailure -Category "PackageSetInvalid" -Message "Release manifest must contain exactly $($script:PulseStackReleasePackageIds.Count) packages; found $($packageEntries.Count)."
    }

    $manifestDirectory = Split-Path -Parent $resolvedManifestPath
    $candidatePackages = [System.Collections.Generic.List[object]]::new()

    for ($index = 0; $index -lt $script:PulseStackReleasePackageIds.Count; $index++) {
        $entry = $packageEntries[$index]
        $expectedId = $script:PulseStackReleasePackageIds[$index]

        $id = Get-RequiredManifestString -Object $entry -PropertyName "id" -Category "PackageSetInvalid" -Context "Package entry $index"
        if ($id -cne $expectedId) {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageSetInvalid" -Message "Package entry $index must be '$expectedId'; found '$id'."
        }

        $version = Get-RequiredManifestString -Object $entry -PropertyName "version" -Category "PackageSetInvalid" -Context "Package '$id'"
        if ($version -cne $packageVersion) {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageSetInvalid" -Message "Package '$id' version '$version' must equal manifest packageVersion '$packageVersion'."
        }

        $fileName = Get-RequiredManifestString -Object $entry -PropertyName "fileName" -Category "PackageSetInvalid" -Context "Package '$id'"
        $expectedFileName = "$id.$packageVersion.nupkg"
        if ($fileName -cne $expectedFileName) {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageSetInvalid" -Message "Package '$id' filename '$fileName' must equal '$expectedFileName'."
        }

        $sha256 = Get-RequiredManifestString -Object $entry -PropertyName "sha256" -Category "PackageSetInvalid" -Context "Package '$id'"
        if ($sha256 -cnotmatch "^[0-9a-f]{64}$") {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageSetInvalid" -Message "Package '$id' sha256 must be canonical lowercase hexadecimal."
        }

        $packagePath = Join-Path $manifestDirectory $fileName
        if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageFileInvalid" -Message "Package '$id' is missing physical file '$fileName'."
        }

        $resolvedPackagePath = (Resolve-Path -LiteralPath $packagePath).Path
        $actualHash = (Get-FileHash -LiteralPath $resolvedPackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -cne $sha256) {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageHashMismatch" -Message "Package '$id' physical SHA-256 does not match the production manifest."
        }

        $nuspec = Read-PackageNuspecMetadata -PackagePath $resolvedPackagePath

        if ($nuspec.Id -cne $id) {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageMetadataInvalid" -Message "Package '$id' nuspec id '$($nuspec.Id)' does not match the manifest."
        }

        if ($nuspec.Version -cne $packageVersion) {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageMetadataInvalid" -Message "Package '$id' nuspec version '$($nuspec.Version)' does not match '$packageVersion'."
        }

        if ($nuspec.RepositoryUrl -cne $script:PulseStackRepositoryUrl) {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageMetadataInvalid" -Message "Package '$id' repository URL '$($nuspec.RepositoryUrl)' is not '$script:PulseStackRepositoryUrl'."
        }

        if ($nuspec.RepositoryType -cne "git") {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageMetadataInvalid" -Message "Package '$id' repository type '$($nuspec.RepositoryType)' is not 'git'."
        }

        if ($nuspec.RepositoryCommit -cne $sourceCommit) {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageMetadataInvalid" -Message "Package '$id' repository commit '$($nuspec.RepositoryCommit)' does not match sourceCommit '$sourceCommit'."
        }

        $candidatePackages.Add([pscustomobject]@{
            Id       = $id
            Version  = $version
            FilePath = $resolvedPackagePath
            Sha256   = $sha256
        })
    }

    $physicalPackages = @(
        Get-ChildItem -LiteralPath $manifestDirectory -File |
            Where-Object { $_.Extension -ieq ".nupkg" }
    )

    if ($physicalPackages.Count -ne $script:PulseStackReleasePackageIds.Count) {
        Throw-ReleasePublicationAdmissionFailure -Category "PackageSetInvalid" -Message "Artifact directory must contain exactly $($script:PulseStackReleasePackageIds.Count) .nupkg files; found $($physicalPackages.Count)."
    }

    $expectedFileNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($candidate in $candidatePackages) {
        [void]$expectedFileNames.Add([System.IO.Path]::GetFileName($candidate.FilePath))
    }

    foreach ($physicalPackage in $physicalPackages) {
        if (-not $expectedFileNames.Contains($physicalPackage.Name)) {
            Throw-ReleasePublicationAdmissionFailure -Category "PackageSetInvalid" -Message "Unexpected physical package '$($physicalPackage.Name)' is present."
        }
    }

    # Whole-set admission is atomic at the API boundary: no package state is returned
    # until every manifest, file, hash, archive, and provenance check has succeeded.
    return [pscustomobject]@{
        SourceCommit        = $sourceCommit
        VersionPrefix       = $versionPrefix
        PackageVersion      = $packageVersion
        ReleaseAuthorityTag = $releaseAuthorityTag
        Packages            = @($candidatePackages)
    }
}
