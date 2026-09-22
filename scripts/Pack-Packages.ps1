[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet("Development", "Release")]
    [string] $ProductionKind = "Development"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ExpectedPackageIds = @(
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

function Invoke-Checked {
    param(
        [Parameter(Mandatory)]
        [string] $FilePath,

        [Parameter()]
        [string[]] $Arguments = @()
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "'$FilePath $($Arguments -join ' ')' failed with exit code $LASTEXITCODE."
    }
}

function Get-GitText {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    $output = & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "'git $($Arguments -join ' ')' failed with exit code $LASTEXITCODE."
    }

    return (($output | Out-String).Trim())
}

function Get-EvaluatedVersionPrefix {
    param(
        [Parameter(Mandatory)]
        [string] $ProjectPath
    )

    $output = & dotnet msbuild $ProjectPath "-getProperty:VersionPrefix"
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to evaluate VersionPrefix through MSBuild."
    }

    $versionPrefix = (($output | Out-String).Trim())
    if ([string]::IsNullOrWhiteSpace($versionPrefix)) {
        throw "MSBuild evaluated an empty VersionPrefix."
    }

    return $versionPrefix
}

function Get-ReleaseIdentity {
    param(
        [Parameter(Mandatory)]
        [string] $VersionPrefix,

        [Parameter(Mandatory)]
        [string] $SourceCommit
    )

    $tagsText = Get-GitText @("tag", "--points-at", $SourceCommit)
    $tags = @($tagsText -split "\r?\n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    $escapedVersionPrefix = [regex]::Escape($VersionPrefix)
    $prereleaseIdentifierPattern = "(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)"
    $releaseTagPattern = "^v$escapedVersionPrefix(?:-(?<prerelease>$prereleaseIdentifierPattern(?:\.$prereleaseIdentifierPattern)*))?$"
    $qualifyingTags = @($tags | Where-Object { $_ -cmatch $releaseTagPattern })

    if ($qualifyingTags.Count -eq 0) {
        throw "Release production requires exactly one qualifying release-version tag at HEAD matching 'v$VersionPrefix' or 'v$VersionPrefix-<prerelease>'."
    }

    if ($qualifyingTags.Count -ne 1) {
        throw "Release production found multiple qualifying release-version tags at HEAD: $($qualifyingTags -join ', ')."
    }

    $tagName = $qualifyingTags[0]
    $tagCommit = Get-GitText @("rev-list", "-n", "1", $tagName)
    if ($tagCommit -notmatch "^[0-9a-fA-F]{40}$") {
        throw "Release tag '$tagName' did not resolve to a full 40-character Git commit SHA."
    }

    if ($tagCommit.ToLowerInvariant() -cne $SourceCommit) {
        throw "Release tag '$tagName' does not resolve exactly to HEAD '$SourceCommit'."
    }

    return [pscustomobject]@{
        TagName        = $tagName
        PackageVersion = $tagName.Substring(1)
    }
}

function Get-NuspecMetadata {
    param(
        [Parameter(Mandatory)]
        [string] $PackagePath
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $nuspecEntries = @($archive.Entries | Where-Object {
            $_.FullName -notmatch "/" -and $_.FullName.EndsWith(".nuspec", [System.StringComparison]::OrdinalIgnoreCase)
        })

        if ($nuspecEntries.Count -ne 1) {
            throw "Package '$PackagePath' must contain exactly one root .nuspec file."
        }

        $reader = [System.IO.StreamReader]::new($nuspecEntries[0].Open())
        try {
            [xml]$nuspec = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }

    $metadata = $nuspec.package.metadata
    if ($null -eq $metadata) {
        throw "Package '$PackagePath' does not contain nuspec package metadata."
    }

    $repository = $metadata.repository

    return [pscustomobject]@{
        Id               = [string]$metadata.id
        Version          = [string]$metadata.version
        RepositoryUrl    = [string]$repository.url
        RepositoryType   = [string]$repository.type
        RepositoryCommit = [string]$repository.commit
    }
}

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
Push-Location $repoRoot

try {
    $actualGitRoot = Get-GitText @("rev-parse", "--show-toplevel")
    $resolvedGitRoot = (Resolve-Path $actualGitRoot).Path
    if (-not [string]::Equals($resolvedGitRoot, $repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Script must run from the PulseStackAI repository that owns this script."
    }

    $workingTreeStatus = Get-GitText @("status", "--porcelain")
    if (-not [string]::IsNullOrEmpty($workingTreeStatus)) {
        throw "Package production requires a clean committed working tree."
    }

    $sourceCommit = Get-GitText @("rev-parse", "HEAD")
    if ($sourceCommit -notmatch "^[0-9a-fA-F]{40}$") {
        throw "HEAD did not resolve to a full 40-character Git commit SHA."
    }
    $sourceCommit = $sourceCommit.ToLowerInvariant()

    $versionProject = Join-Path $repoRoot "src/PulseStack.Abstractions/PulseStack.Abstractions.csproj"
    $versionPrefix = Get-EvaluatedVersionPrefix $versionProject

    $releaseIdentity = $null
    if ($ProductionKind -ceq "Release") {
        $releaseIdentity = Get-ReleaseIdentity -VersionPrefix $versionPrefix -SourceCommit $sourceCommit
        $packageVersion = $releaseIdentity.PackageVersion
    }
    else {
        $packageVersion = "$versionPrefix-dev.$sourceCommit"
    }

    $productionKindValue = $ProductionKind.ToLowerInvariant()

    $stagingPath = Join-Path $repoRoot "artifacts/packages/staging/$packageVersion"
    if (Test-Path -LiteralPath $stagingPath) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null

    Write-Host "PulseStackAI deterministic package production"
    Write-Host "ProductionKind $productionKindValue"
    Write-Host "SourceCommit   $sourceCommit"
    Write-Host "VersionPrefix  $versionPrefix"
    Write-Host "PackageVersion $packageVersion"
    if ($null -ne $releaseIdentity) {
        Write-Host "ReleaseTag     $($releaseIdentity.TagName)"
    }
    Write-Host ""

    Invoke-Checked dotnet @(
        "build",
        "PulseStackAI.sln",
        "--configuration", "Release"
    )

    Invoke-Checked dotnet @(
        "test",
        "tests/PulseStack.Tests/PulseStack.Tests.csproj",
        "--configuration", "Release",
        "--no-build"
    )

    Invoke-Checked dotnet @(
        "pack",
        "PulseStackAI.sln",
        "--configuration", "Release",
        "--no-build",
        "--output", $stagingPath,
        "-p:PackageVersion=$packageVersion",
        "-p:RepositoryCommit=$sourceCommit"
    )

    $actualPackages = @(Get-ChildItem -LiteralPath $stagingPath -File -Filter "*.nupkg")
    if ($actualPackages.Count -ne $ExpectedPackageIds.Count) {
        throw "Expected exactly $($ExpectedPackageIds.Count) .nupkg files but found $($actualPackages.Count)."
    }

    $verifiedPackages = [System.Collections.Generic.List[object]]::new()

    foreach ($packageId in $ExpectedPackageIds) {
        $expectedFileName = "$packageId.$packageVersion.nupkg"
        $matchingPackages = @($actualPackages | Where-Object { $_.Name -ceq $expectedFileName })

        if ($matchingPackages.Count -ne 1) {
            throw "Expected exactly one package named '$expectedFileName' but found $($matchingPackages.Count)."
        }

        $package = $matchingPackages[0]
        $metadata = Get-NuspecMetadata $package.FullName

        if ($metadata.Id -cne $packageId) {
            throw "Package '$($package.Name)' has id '$($metadata.Id)' instead of '$packageId'."
        }
        if ($metadata.Version -cne $packageVersion) {
            throw "Package '$($package.Name)' has version '$($metadata.Version)' instead of '$packageVersion'."
        }
        if ($metadata.RepositoryUrl -cne "https://github.com/abilgaiyan/PulseStackAI") {
            throw "Package '$($package.Name)' has unexpected repository URL '$($metadata.RepositoryUrl)'."
        }
        if ($metadata.RepositoryType -cne "git") {
            throw "Package '$($package.Name)' has unexpected repository type '$($metadata.RepositoryType)'."
        }
        if ($metadata.RepositoryCommit -cne $sourceCommit) {
            throw "Package '$($package.Name)' has repository commit '$($metadata.RepositoryCommit)' instead of '$sourceCommit'."
        }

        $sha256 = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($sha256 -notmatch "^[0-9a-f]{64}$") {
            throw "Package '$($package.Name)' did not produce a canonical SHA-256 digest."
        }

        $verifiedPackages.Add([ordered]@{
            id       = $packageId
            version  = $packageVersion
            fileName = $package.Name
            sha256   = $sha256
        })
    }

    $expectedFileNames = @($ExpectedPackageIds | ForEach-Object { "$_.$packageVersion.nupkg" })
    $unexpectedPackages = @($actualPackages | Where-Object { $_.Name -cnotin $expectedFileNames })
    if ($unexpectedPackages.Count -ne 0) {
        throw "Unexpected packages were produced: $($unexpectedPackages.Name -join ', ')."
    }

    $manifest = [ordered]@{
        productionKind = $productionKindValue
        sourceCommit = $sourceCommit
        versionPrefix = $versionPrefix
        packageVersion = $packageVersion
        configuration = "Release"
    }

    if ($null -ne $releaseIdentity) {
        $manifest["releaseAuthority"] = [ordered]@{
            tagName = $releaseIdentity.TagName
        }
    }

    $manifest["packages"] = @($verifiedPackages)

    $manifestPath = Join-Path $stagingPath "package-production.json"
    $manifestJson = $manifest | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText(
        $manifestPath,
        $manifestJson + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false)
    )

    Write-Host ""
    Write-Host "PulseStackAI Package Production"
    Write-Host "ProductionKind $productionKindValue"
    Write-Host "SourceCommit   $sourceCommit"
    Write-Host "VersionPrefix  $versionPrefix"
    Write-Host "PackageVersion $packageVersion"
    if ($null -ne $releaseIdentity) {
        Write-Host "ReleaseTag     $($releaseIdentity.TagName)"
    }
    Write-Host "Configuration  Release"
    Write-Host "PackageCount   $($verifiedPackages.Count)"
    Write-Host "StagingPath    $stagingPath"
    Write-Host "Manifest       $manifestPath"
    Write-Host "Verification   PASS"
}
finally {
    Pop-Location
}
