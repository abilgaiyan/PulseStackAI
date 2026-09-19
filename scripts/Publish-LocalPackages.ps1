[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ManifestPath,

    [Parameter(Mandatory)]
    [string] $FeedPath
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

function Get-CanonicalSha256 {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($sha256 -notmatch "^[0-9a-f]{64}$") {
        throw "File '$Path' did not produce a canonical SHA-256 digest."
    }

    return $sha256
}

function Get-RequiredString {
    param(
        [Parameter(Mandatory)]
        [object] $Object,

        [Parameter(Mandatory)]
        [string] $PropertyName,

        [Parameter(Mandatory)]
        [string] $Context
    )

    $property = $Object.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        throw "$Context is missing required property '$PropertyName'."
    }

    $value = [string]$property.Value
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "$Context has an empty required property '$PropertyName'."
    }

    return $value
}

$resolvedManifestPath = (Resolve-Path -LiteralPath $ManifestPath -ErrorAction Stop).Path
if (-not [System.IO.File]::Exists($resolvedManifestPath)) {
    throw "ManifestPath '$ManifestPath' must identify a file."
}

$manifestDirectory = Split-Path -Parent $resolvedManifestPath

try {
    $manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
}
catch {
    throw "Manifest '$resolvedManifestPath' is not valid JSON: $($_.Exception.Message)"
}

if ($null -eq $manifest) {
    throw "Manifest '$resolvedManifestPath' is empty."
}

$sourceCommit = Get-RequiredString $manifest "sourceCommit" "Manifest"
$versionPrefix = Get-RequiredString $manifest "versionPrefix" "Manifest"
$packageVersion = Get-RequiredString $manifest "packageVersion" "Manifest"
$configuration = Get-RequiredString $manifest "configuration" "Manifest"

if ($sourceCommit -notmatch "^[0-9a-f]{40}$") {
    throw "Manifest sourceCommit must be a lowercase full 40-character Git SHA."
}
if ($configuration -cne "Release") {
    throw "Manifest configuration must be 'Release'."
}

$packagesProperty = $manifest.PSObject.Properties["packages"]
if ($null -eq $packagesProperty -or $null -eq $packagesProperty.Value) {
    throw "Manifest is missing required property 'packages'."
}

$packages = @($packagesProperty.Value)
if ($packages.Count -ne $ExpectedPackageIds.Count) {
    throw "Manifest must contain exactly $($ExpectedPackageIds.Count) package entries but contains $($packages.Count)."
}

$sourcePackages = @(Get-ChildItem -LiteralPath $manifestDirectory -File -Filter "*.nupkg")
if ($sourcePackages.Count -ne $ExpectedPackageIds.Count) {
    throw "Manifest directory must contain exactly $($ExpectedPackageIds.Count) .nupkg files but contains $($sourcePackages.Count)."
}

$admittedPackages = [System.Collections.Generic.List[object]]::new()

for ($index = 0; $index -lt $ExpectedPackageIds.Count; $index++) {
    $expectedPackageId = $ExpectedPackageIds[$index]
    $entry = $packages[$index]
    $context = "Manifest package entry $index"

    $packageId = Get-RequiredString $entry "id" $context
    $entryVersion = Get-RequiredString $entry "version" $context
    $fileName = Get-RequiredString $entry "fileName" $context
    $expectedSha256 = Get-RequiredString $entry "sha256" $context

    if ($packageId -cne $expectedPackageId) {
        throw "$context has id '$packageId'; expected '$expectedPackageId' in the frozen package-set order."
    }
    if ($entryVersion -cne $packageVersion) {
        throw "$context has version '$entryVersion'; expected manifest packageVersion '$packageVersion'."
    }

    $expectedFileName = "$packageId.$packageVersion.nupkg"
    if ($fileName -cne $expectedFileName) {
        throw "$context has fileName '$fileName'; expected '$expectedFileName'."
    }
    if ([System.IO.Path]::GetFileName($fileName) -cne $fileName) {
        throw "$context fileName must be a simple file name."
    }
    if ($expectedSha256 -notmatch "^[0-9a-f]{64}$") {
        throw "$context sha256 must be lowercase 64-character hexadecimal."
    }

    $sourcePath = Join-Path $manifestDirectory $fileName
    if (-not [System.IO.File]::Exists($sourcePath)) {
        throw "Manifest package '$fileName' does not exist beside the manifest."
    }

    $matchingSourcePackages = @($sourcePackages | Where-Object { $_.Name -ceq $fileName })
    if ($matchingSourcePackages.Count -ne 1) {
        throw "Expected exactly one source package named '$fileName' but found $($matchingSourcePackages.Count)."
    }

    $actualSha256 = Get-CanonicalSha256 $sourcePath
    if ($actualSha256 -cne $expectedSha256) {
        throw "Source package '$fileName' SHA-256 '$actualSha256' does not match manifest '$expectedSha256'."
    }

    $admittedPackages.Add([pscustomobject]@{
        Id       = $packageId
        Version  = $entryVersion
        FileName = $fileName
        Sha256   = $expectedSha256
        SourcePath = $sourcePath
    })
}

$expectedSourceFileNames = @($admittedPackages | ForEach-Object { $_.FileName })
$unexpectedSourcePackages = @($sourcePackages | Where-Object { $_.Name -cnotin $expectedSourceFileNames })
if ($unexpectedSourcePackages.Count -ne 0) {
    throw "Unexpected source packages are present beside the manifest: $($unexpectedSourcePackages.Name -join ', ')."
}

$resolvedFeedPath = [System.IO.Path]::GetFullPath($FeedPath)
if (-not (Test-Path -LiteralPath $resolvedFeedPath)) {
    New-Item -ItemType Directory -Path $resolvedFeedPath -Force | Out-Null
}
elseif (-not [System.IO.Directory]::Exists($resolvedFeedPath)) {
    throw "FeedPath '$FeedPath' must identify a directory."
}

foreach ($package in $admittedPackages) {
    $destinationPath = Join-Path $resolvedFeedPath $package.FileName
    if (Test-Path -LiteralPath $destinationPath) {
        throw "Publication refused because destination package '$($package.FileName)' already exists. Overwrite is forbidden."
    }
}

Write-Host "PulseStackAI local package publication"
Write-Host "Manifest       $resolvedManifestPath"
Write-Host "SourceCommit   $sourceCommit"
Write-Host "VersionPrefix  $versionPrefix"
Write-Host "PackageVersion $packageVersion"
Write-Host "PackageCount   $($admittedPackages.Count)"
Write-Host "FeedPath       $resolvedFeedPath"
Write-Host ""

foreach ($package in $admittedPackages) {
    $destinationPath = Join-Path $resolvedFeedPath $package.FileName
    Copy-Item -LiteralPath $package.SourcePath -Destination $destinationPath
}

foreach ($package in $admittedPackages) {
    $destinationPath = Join-Path $resolvedFeedPath $package.FileName
    if (-not [System.IO.File]::Exists($destinationPath)) {
        throw "Published package '$($package.FileName)' is missing from the feed."
    }

    $publishedSha256 = Get-CanonicalSha256 $destinationPath
    if ($publishedSha256 -cne $package.Sha256) {
        throw "Published package '$($package.FileName)' SHA-256 '$publishedSha256' does not match manifest '$($package.Sha256)'."
    }
}

Write-Host "PulseStackAI Local NuGet Distribution"
Write-Host "PackageVersion $packageVersion"
Write-Host "PackageCount   $($admittedPackages.Count)"
Write-Host "FeedPath       $resolvedFeedPath"
Write-Host "Verification   PASS"
