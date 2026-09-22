Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$admissionScript = Join-Path $repositoryRoot "scripts/PackagePublicationAdmission.ps1"
. $admissionScript

$packageIds = @(
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

$sourceCommit = "0123456789abcdef0123456789abcdef01234567"
$versionPrefix = "1.0.4"
$packageVersion = "1.0.4-conformance.1"
$repositoryUrl = "https://github.com/abilgaiyan/PulseStackAI"

function Write-TestPackage {
    param(
        [Parameter(Mandatory)] [string] $Directory,
        [Parameter(Mandatory)] [string] $Id,
        [string] $Version = $packageVersion,
        [string] $NuspecId = $Id,
        [string] $NuspecVersion = $Version,
        [string] $RepositoryUrl = $script:repositoryUrl,
        [string] $RepositoryType = "git",
        [string] $RepositoryCommit = $script:sourceCommit,
        [ValidateSet("Valid", "NoNuspec", "TwoNuspecs", "InvalidXml", "NoMetadata")]
        [string] $ArchiveShape = "Valid"
    )

    $fileName = "$Id.$Version.nupkg"
    $packagePath = Join-Path $Directory $fileName
    $working = Join-Path $Directory ("zip-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $working | Out-Null

    try {
        if ($ArchiveShape -ne "NoNuspec") {
            $nuspecPath = Join-Path $working "$Id.nuspec"
            if ($ArchiveShape -eq "InvalidXml") {
                Set-Content -LiteralPath $nuspecPath -Value "<package><metadata>" -NoNewline
            }
            elseif ($ArchiveShape -eq "NoMetadata") {
                Set-Content -LiteralPath $nuspecPath -Value "<?xml version=`"1.0`"?><package></package>" -NoNewline
            }
            else {
                $xml = @"
<?xml version="1.0"?>
<package>
  <metadata>
    <id>$NuspecId</id>
    <version>$NuspecVersion</version>
    <authors>PulseStackAI Contributors</authors>
    <description>RP-1 conformance fixture.</description>
    <repository type="$RepositoryType" url="$RepositoryUrl" commit="$RepositoryCommit" />
  </metadata>
</package>
"@
                Set-Content -LiteralPath $nuspecPath -Value $xml -NoNewline
            }

            if ($ArchiveShape -eq "TwoNuspecs") {
                Copy-Item -LiteralPath $nuspecPath -Destination (Join-Path $working "duplicate.nuspec")
            }
        }

        [System.IO.Compression.ZipFile]::CreateFromDirectory($working, $packagePath)
    }
    finally {
        Remove-Item -LiteralPath $working -Recurse -Force
    }

    return $packagePath
}

function Write-Manifest {
    param(
        [Parameter(Mandatory)] [string] $Directory,
        [Parameter(Mandatory)] [object[]] $Packages,
        [string] $ProductionKind = "release",
        [string] $Configuration = "Release",
        [string] $SourceCommit = $script:sourceCommit,
        [string] $VersionPrefix = $script:versionPrefix,
        [string] $PackageVersion = $script:packageVersion,
        [AllowNull()] [object] $ReleaseAuthority = ([ordered]@{ tagName = "v$packageVersion" }),
        [switch] $OmitProductionKind,
        [switch] $OmitReleaseAuthority
    )

    $manifest = [ordered]@{}
    if (-not $OmitProductionKind) {
        $manifest.productionKind = $ProductionKind
    }

    $manifest.sourceCommit = $SourceCommit
    $manifest.versionPrefix = $VersionPrefix
    $manifest.packageVersion = $PackageVersion
    $manifest.configuration = $Configuration

    if (-not $OmitReleaseAuthority) {
        $manifest.releaseAuthority = $ReleaseAuthority
    }

    $manifest.packages = $Packages
    $path = Join-Path $Directory "package-production.json"
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path
    return $path
}

function New-ValidFixture {
    $directory = Join-Path ([System.IO.Path]::GetTempPath()) ("PulseStack-RP1-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $directory | Out-Null

    $entries = foreach ($id in $packageIds) {
        $path = Write-TestPackage -Directory $directory -Id $id
        [ordered]@{
            id       = $id
            version  = $packageVersion
            fileName = [System.IO.Path]::GetFileName($path)
            sha256   = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }

    $manifestPath = Write-Manifest -Directory $directory -Packages $entries

    return [pscustomobject]@{
        Directory    = $directory
        ManifestPath = $manifestPath
    }
}

function Read-FixtureManifest {
    param([Parameter(Mandatory)] [object] $Fixture)
    return Get-Content -LiteralPath $Fixture.ManifestPath -Raw | ConvertFrom-Json
}

function Save-FixtureManifest {
    param(
        [Parameter(Mandatory)] [object] $Fixture,
        [Parameter(Mandatory)] [object] $Manifest
    )
    $Manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Fixture.ManifestPath
}

function Replace-Package {
    param(
        [Parameter(Mandatory)] [object] $Fixture,
        [Parameter(Mandatory)] [int] $Index,
        [hashtable] $PackageArguments = @{}
    )

    $manifest = Read-FixtureManifest -Fixture $Fixture
    $entry = $manifest.packages[$Index]
    $oldPath = Join-Path $Fixture.Directory $entry.fileName
    Remove-Item -LiteralPath $oldPath -Force

    $arguments = @{
        Directory = $Fixture.Directory
        Id        = [string]$entry.id
        Version   = [string]$entry.version
    }
    foreach ($key in $PackageArguments.Keys) {
        $arguments[$key] = $PackageArguments[$key]
    }

    $newPath = Write-TestPackage @arguments
    $entry.fileName = [System.IO.Path]::GetFileName($newPath)
    $entry.sha256 = (Get-FileHash -LiteralPath $newPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Save-FixtureManifest -Fixture $Fixture -Manifest $manifest
}

function Invoke-AdmissionCase {
    param(
        [Parameter(Mandatory)] [string] $Id,
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [bool] $ShouldPass,
        [Parameter(Mandatory)] [scriptblock] $Arrange
    )

    $fixture = New-ValidFixture
    try {
        & $Arrange $fixture

        $failure = $null
        $result = $null
        try {
            $result = Invoke-ReleasePublicationAdmission -ManifestPath $fixture.ManifestPath
        }
        catch {
            $failure = $_
        }

        if ($ShouldPass) {
            if ($null -ne $failure) {
                throw "$Id $Name expected PASS but failed: $($failure.Exception.Message)"
            }

            if ($null -eq $result -or @($result.Packages).Count -ne 10) {
                throw "$Id $Name did not return the complete admitted ten-package set."
            }
        }
        else {
            if ($null -eq $failure) {
                throw "$Id $Name expected rejection but admission succeeded."
            }

            if ($null -ne $result) {
                throw "$Id $Name returned usable package state on failure."
            }

            $category = $failure.Exception.Data["ReleasePublicationAdmissionCategory"]
            if ([string]::IsNullOrWhiteSpace([string]$category)) {
                throw "$Id $Name failed without a deterministic admission category."
            }
        }

        [pscustomobject]@{ Id = $Id; Name = $Name; Outcome = "PASS" }
    }
    finally {
        Remove-Item -LiteralPath $fixture.Directory -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$cases = @(
    @{ Id="A01"; Name="valid release manifest and exact files"; Pass=$true; Arrange={ param($f) } },
    @{ Id="A02"; Name="development manifest"; Pass=$false; Arrange={ param($f) $m=Read-FixtureManifest $f; $m.productionKind="development"; Save-FixtureManifest $f $m } },
    @{ Id="A03"; Name="missing productionKind"; Pass=$false; Arrange={ param($f) $m=Read-FixtureManifest $f; $m.PSObject.Properties.Remove("productionKind"); Save-FixtureManifest $f $m } },
    @{ Id="A04"; Name="missing releaseAuthority"; Pass=$false; Arrange={ param($f) $m=Read-FixtureManifest $f; $m.PSObject.Properties.Remove("releaseAuthority"); Save-FixtureManifest $f $m } },
    @{ Id="A05"; Name="tagName does not match packageVersion"; Pass=$false; Arrange={ param($f) $m=Read-FixtureManifest $f; $m.releaseAuthority.tagName="v9.9.9"; Save-FixtureManifest $f $m } },
    @{ Id="A06"; Name="non-Release configuration"; Pass=$false; Arrange={ param($f) $m=Read-FixtureManifest $f; $m.configuration="Debug"; Save-FixtureManifest $f $m } },
    @{ Id="A07"; Name="malformed sourceCommit"; Pass=$false; Arrange={ param($f) $m=Read-FixtureManifest $f; $m.sourceCommit="ABC123"; Save-FixtureManifest $f $m } },
    @{ Id="A08"; Name="package count is not ten"; Pass=$false; Arrange={ param($f) $m=Read-FixtureManifest $f; $m.packages=@($m.packages | Select-Object -First 9); Save-FixtureManifest $f $m } },
    @{ Id="A09"; Name="wrong package order"; Pass=$false; Arrange={ param($f) $m=Read-FixtureManifest $f; $x=$m.packages[0]; $m.packages[0]=$m.packages[1]; $m.packages[1]=$x; Save-FixtureManifest $f $m } },
    @{ Id="A10"; Name="unexpected package ID"; Pass=$false; Arrange={ param($f) $m=Read-FixtureManifest $f; $m.packages[0].id="PulseStack.Unexpected"; Save-FixtureManifest $f $m } },
    @{ Id="A11"; Name="package version differs from manifest"; Pass=$false; Arrange={ param($f) $m=Read-FixtureManifest $f; $m.packages[0].version="9.9.9"; Save-FixtureManifest $f $m } },
    @{ Id="A12"; Name="wrong package filename"; Pass=$false; Arrange={ param($f) $m=Read-FixtureManifest $f; $m.packages[0].fileName="wrong.nupkg"; Save-FixtureManifest $f $m } },
    @{ Id="A13"; Name="missing physical package"; Pass=$false; Arrange={ param($f) $m=Read-FixtureManifest $f; Remove-Item (Join-Path $f.Directory $m.packages[0].fileName) -Force } },
    @{ Id="A14"; Name="unexpected extra nupkg"; Pass=$false; Arrange={ param($f) Copy-Item (Join-Path $f.Directory "PulseStack.Core.$packageVersion.nupkg") (Join-Path $f.Directory "extra.nupkg") } },
    @{ Id="A15"; Name="malformed SHA-256"; Pass=$false; Arrange={ param($f) $m=Read-FixtureManifest $f; $m.packages[0].sha256="not-a-sha"; Save-FixtureManifest $f $m } },
    @{ Id="A16"; Name="physical SHA-256 mismatch"; Pass=$false; Arrange={ param($f) $m=Read-FixtureManifest $f; $m.packages[0].sha256=("0"*64); Save-FixtureManifest $f $m } },
    @{ Id="A17"; Name="nuspec ID mismatch"; Pass=$false; Arrange={ param($f) Replace-Package $f 0 @{ NuspecId="PulseStack.Wrong" } } },
    @{ Id="A18"; Name="nuspec version mismatch"; Pass=$false; Arrange={ param($f) Replace-Package $f 0 @{ NuspecVersion="9.9.9" } } },
    @{ Id="A19"; Name="repository URL mismatch"; Pass=$false; Arrange={ param($f) Replace-Package $f 0 @{ RepositoryUrl="https://example.invalid/wrong" } } },
    @{ Id="A20"; Name="repository type mismatch"; Pass=$false; Arrange={ param($f) Replace-Package $f 0 @{ RepositoryType="svn" } } },
    @{ Id="A21"; Name="RepositoryCommit mismatch"; Pass=$false; Arrange={ param($f) Replace-Package $f 0 @{ RepositoryCommit=("f"*40) } } },
    @{ Id="A22"; Name="malformed or ambiguous physical package"; Pass=$false; Arrange={ param($f) Replace-Package $f 0 @{ ArchiveShape="TwoNuspecs" } } }
)

$results = foreach ($case in $cases) {
    Invoke-AdmissionCase -Id $case.Id -Name $case.Name -ShouldPass $case.Pass -Arrange $case.Arrange
}

$results | Format-Table Id, Name, Outcome -AutoSize

$failed = @($results | Where-Object { $_.Outcome -ne "PASS" })
if ($results.Count -ne 22 -or $failed.Count -ne 0) {
    throw "RP-1 conformance failed."
}

Write-Host ""
Write-Host "RP-1 CONFORMANCE: 22 / 22 PASS"
