Set-StrictMode -Version Latest

$script:NuGetV3ServiceIndex = "https://api.nuget.org/v3/index.json"
$script:PublicationSchemaVersion = "1.0"

function New-PublicationDiagnostic {
    param(
        [Parameter(Mandatory)] [string] $Code,
        [Parameter(Mandatory)] [string] $Message,
        [AllowNull()] [Nullable[int]] $StatusCode = $null
    )

    [pscustomobject]@{
        Code       = $Code
        Message    = $Message
        StatusCode = $StatusCode
    }
}

function Assert-PublicationCorrespondence {
    param(
        [Parameter(Mandatory)] [object] $AdmittedPackageSet,
        [Parameter(Mandatory)] [object] $PreflightResult
    )

    if ($null -eq $AdmittedPackageSet -or $null -eq $PreflightResult) {
        throw [System.ArgumentNullException]::new("AdmittedPackageSet/PreflightResult")
    }

    if ([string]$PreflightResult.State -cne "AllAbsent") {
        throw [System.InvalidOperationException]::new("Fresh publication requires RP-2 preflight state 'AllAbsent'.")
    }

    $admitted = @($AdmittedPackageSet.Packages)
    $observed = @($PreflightResult.Packages)
    if ($admitted.Count -eq 0 -or $admitted.Count -ne $observed.Count) {
        throw [System.InvalidOperationException]::new("RP-1/RP-2 package-set correspondence failed.")
    }

    for ($i = 0; $i -lt $admitted.Count; $i++) {
        if ([string]$admitted[$i].Id -cne [string]$observed[$i].Id -or
            [string]$admitted[$i].Version -cne [string]$observed[$i].Version -or
            [string]$observed[$i].State -cne "Absent") {
            throw [System.InvalidOperationException]::new("RP-1/RP-2 correspondence failed at package index $i.")
        }
    }
}

function Get-PackagePublishEndpointFromServiceIndex {
    param([Parameter(Mandatory)] [object] $Response)

    if ([int]$Response.StatusCode -ne 200) {
        return [pscustomobject]@{
            Success = $false
            Endpoint = $null
            Diagnostic = New-PublicationDiagnostic -Code "ServiceIndexUnavailable" -Message "NuGet V3 service index returned HTTP $($Response.StatusCode)." -StatusCode ([int]$Response.StatusCode)
        }
    }

    try {
        $index = [string]$Response.Content | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        return [pscustomobject]@{
            Success = $false
            Endpoint = $null
            Diagnostic = New-PublicationDiagnostic -Code "ServiceIndexInvalid" -Message "NuGet V3 service index is not valid JSON."
        }
    }

    $resources = $index.PSObject.Properties["resources"]
    if ($null -eq $resources -or $null -eq $resources.Value) {
        return [pscustomobject]@{
            Success = $false
            Endpoint = $null
            Diagnostic = New-PublicationDiagnostic -Code "ServiceIndexInvalid" -Message "NuGet V3 service index does not contain resources."
        }
    }

    $matches = @($resources.Value | Where-Object {
        $type = $_.PSObject.Properties["@type"]
        $id = $_.PSObject.Properties["@id"]
        $null -ne $type -and $null -ne $id -and
        [string]$type.Value -match "^PackagePublish/2\.0\.0$" -and
        -not [string]::IsNullOrWhiteSpace([string]$id.Value)
    })

    if ($matches.Count -eq 0) {
        return [pscustomobject]@{
            Success = $false
            Endpoint = $null
            Diagnostic = New-PublicationDiagnostic -Code "PackagePublishUnavailable" -Message "NuGet V3 service index does not expose PackagePublish/2.0.0."
        }
    }

    $endpoint = [string]$matches[0].PSObject.Properties["@id"].Value
    $uri = $null
    if (-not [System.Uri]::TryCreate($endpoint, [System.UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -notin @("http", "https")) {
        return [pscustomobject]@{
            Success = $false
            Endpoint = $null
            Diagnostic = New-PublicationDiagnostic -Code "PackagePublishUnavailable" -Message "PackagePublish is not an absolute HTTP(S) URI."
        }
    }

    [pscustomobject]@{ Success=$true; Endpoint=$endpoint; Diagnostic=$null }
}

function Invoke-DefaultPublicationDiscoveryRequest {
    param([Parameter(Mandatory)] [string] $Uri)

    try {
        $response = Invoke-WebRequest -Method GET -Uri $Uri -UseBasicParsing -ErrorAction Stop
        [pscustomobject]@{ StatusCode=[int]$response.StatusCode; Content=[string]$response.Content }
    }
    catch {
        if ($null -ne $_.Exception.Response -and $null -ne $_.Exception.Response.StatusCode) {
            return [pscustomobject]@{ StatusCode=[int]$_.Exception.Response.StatusCode; Content=$null }
        }
        throw
    }
}

function Invoke-DefaultNuGetPublishRequest {
    param(
        [Parameter(Mandatory)] [string] $Endpoint,
        [Parameter(Mandatory)] [string] $PackageFilePath,
        [Parameter(Mandatory)] [string] $ApiKey
    )

    $client = [System.Net.Http.HttpClient]::new()
    try {
        $client.DefaultRequestHeaders.Add("X-NuGet-ApiKey", $ApiKey)
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        try {
            $stream = [System.IO.File]::OpenRead($PackageFilePath)
            try {
                $fileContent = [System.Net.Http.StreamContent]::new($stream)
                $content.Add($fileContent, "package", [System.IO.Path]::GetFileName($PackageFilePath))
                $response = $client.PutAsync($Endpoint, $content).GetAwaiter().GetResult()
                return [pscustomobject]@{ StatusCode=[int]$response.StatusCode }
            }
            finally {
                $stream.Dispose()
            }
        }
        finally {
            $content.Dispose()
        }
    }
    finally {
        $client.Dispose()
    }
}

function Write-AtomicPublicationLedger {
    param(
        [Parameter(Mandatory)] [object] $Ledger,
        [Parameter(Mandatory)] [string] $LedgerPath
    )

    $directory = Split-Path -Parent $LedgerPath
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $tempPath = Join-Path $directory ("publication-result." + [guid]::NewGuid().ToString("N") + ".tmp")
    try {
        $json = $Ledger | ConvertTo-Json -Depth 8
        [System.IO.File]::WriteAllText($tempPath, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))

        if (Test-Path -LiteralPath $LedgerPath) {
            $backupPath = Join-Path $directory ("publication-result." + [guid]::NewGuid().ToString("N") + ".bak")
            try {
                [System.IO.File]::Replace($tempPath, $LedgerPath, $backupPath, $true)
            }
            finally {
                if (Test-Path -LiteralPath $backupPath) { Remove-Item -LiteralPath $backupPath -Force }
            }
        }
        else {
            [System.IO.File]::Move($tempPath, $LedgerPath)
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempPath) { Remove-Item -LiteralPath $tempPath -Force }
    }
}

function Update-PublicationCounts {
    param([Parameter(Mandatory)] [object] $Ledger)

    $states = @($Ledger.packages | ForEach-Object { $_.mutationState })
    $Ledger.knownAcceptedCount = @($states | Where-Object { $_ -eq "Accepted" }).Count
    $Ledger.knownRejectedCount = @($states | Where-Object { $_ -eq "Rejected" }).Count
    $Ledger.indeterminateCount = @($states | Where-Object { $_ -eq "Indeterminate" -or $_ -eq "Attempting" }).Count
    $Ledger.notAttemptedCount = @($states | Where-Object { $_ -eq "NotAttempted" }).Count
}

function Invoke-NuGetPublicationOperation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $AdmittedPackageSet,
        [Parameter(Mandatory)] [object] $PreflightResult,
        [Parameter(Mandatory)] [string] $EvidenceRoot,
        [Parameter(Mandatory)] [scriptblock] $CredentialAvailable,
        [Parameter(Mandatory)] [scriptblock] $AcquireCredential,
        [scriptblock] $DiscoveryRequest = ${function:Invoke-DefaultPublicationDiscoveryRequest},
        [scriptblock] $PublishRequest = ${function:Invoke-DefaultNuGetPublishRequest},
        [scriptblock] $WriteLedger = ${function:Write-AtomicPublicationLedger},
        [scriptblock] $OperationIdFactory = { [guid]::NewGuid().ToString("D") },
        [scriptblock] $Clock = { [DateTimeOffset]::UtcNow },
        [string] $ServiceIndexUri = $script:NuGetV3ServiceIndex,
        [scriptblock] $AfterAttemptingPersisted = $null
    )

    Assert-PublicationCorrespondence -AdmittedPackageSet $AdmittedPackageSet -PreflightResult $PreflightResult

    $discoveryResponse = $null
    try {
        $discoveryResponse = & $DiscoveryRequest $ServiceIndexUri
    }
    catch {
        throw [System.InvalidOperationException]::new("PackagePublish discovery failed before publication attempt ledger creation.", $_.Exception)
    }

    $discovery = Get-PackagePublishEndpointFromServiceIndex -Response $discoveryResponse
    if (-not $discovery.Success) {
        throw [System.InvalidOperationException]::new("[$($discovery.Diagnostic.Code)] $($discovery.Diagnostic.Message)")
    }

    if (-not (& $CredentialAvailable)) {
        throw [System.InvalidOperationException]::new("Publication credential is unavailable.")
    }

    $operationId = [string](& $OperationIdFactory)
    if ([string]::IsNullOrWhiteSpace($operationId)) {
        throw [System.InvalidOperationException]::new("OperationIdFactory returned an empty operation id.")
    }

    $operationDirectory = Join-Path (Join-Path (Join-Path $EvidenceRoot ([string]$AdmittedPackageSet.PackageVersion)) "attempts") $operationId
    $ledgerPath = Join-Path $operationDirectory "publication-result.json"
    if (Test-Path -LiteralPath $ledgerPath) {
        throw [System.InvalidOperationException]::new("Publication operation '$operationId' already has durable evidence and cannot be reopened.")
    }

    $startedAt = & $Clock
    $ledger = [pscustomobject]@{
        schemaVersion          = $script:PublicationSchemaVersion
        operationId            = $operationId
        ledgerState            = "InProgress"
        registry               = "NuGet.org"
        serviceIndex           = $ServiceIndexUri
        packagePublishEndpoint = $discovery.Endpoint
        sourceCommit           = [string]$AdmittedPackageSet.SourceCommit
        versionPrefix          = [string]$AdmittedPackageSet.VersionPrefix
        packageVersion         = [string]$AdmittedPackageSet.PackageVersion
        releaseAuthorityTag    = [string]$AdmittedPackageSet.ReleaseAuthorityTag
        startedAtUtc           = ([DateTimeOffset]$startedAt).ToUniversalTime().ToString("O")
        completedAtUtc         = $null
        operationConclusion    = $null
        knownAcceptedCount     = 0
        knownRejectedCount     = 0
        indeterminateCount     = 0
        notAttemptedCount      = @($AdmittedPackageSet.Packages).Count
        packages               = @($AdmittedPackageSet.Packages | ForEach-Object {
            [pscustomobject]@{
                id             = [string]$_.Id
                version        = [string]$_.Version
                admittedSha256 = [string]$_.Sha256
                mutationState  = "NotAttempted"
                statusCode     = $null
                diagnostic     = $null
            }
        })
    }

    & $WriteLedger $ledger $ledgerPath

    $terminalConclusion = $null

    for ($i = 0; $i -lt $ledger.packages.Count; $i++) {
        $ledger.packages[$i].mutationState = "Attempting"
        $ledger.packages[$i].statusCode = $null
        $ledger.packages[$i].diagnostic = $null
        Update-PublicationCounts -Ledger $ledger
        & $WriteLedger $ledger $ledgerPath

        if ($null -ne $AfterAttemptingPersisted) {
            & $AfterAttemptingPersisted $ledger $ledgerPath $i
        }

        $apiKey = $null
        try {
            try {
                $apiKey = & $AcquireCredential
                if ([string]::IsNullOrWhiteSpace([string]$apiKey)) {
                    throw [System.InvalidOperationException]::new("Credential provider returned no credential material.")
                }
            }
            catch {
                $ledger.packages[$i].mutationState = "Indeterminate"
                $ledger.packages[$i].diagnostic = New-PublicationDiagnostic -Code "CredentialAcquisitionFailure" -Message "Credential acquisition failed after Attempting became durable."
                Update-PublicationCounts -Ledger $ledger
                & $WriteLedger $ledger $ledgerPath
                $terminalConclusion = "StoppedIndeterminate"
                break
            }

            try {
                $response = & $PublishRequest $discovery.Endpoint ([string]$AdmittedPackageSet.Packages[$i].FilePath) ([string]$apiKey)
                $status = [int]$response.StatusCode
                switch ($status) {
                    201 {
                        $ledger.packages[$i].mutationState = "Accepted"
                        $ledger.packages[$i].statusCode = 201
                    }
                    202 {
                        $ledger.packages[$i].mutationState = "Accepted"
                        $ledger.packages[$i].statusCode = 202
                    }
                    400 {
                        $ledger.packages[$i].mutationState = "Rejected"
                        $ledger.packages[$i].statusCode = 400
                        $ledger.packages[$i].diagnostic = New-PublicationDiagnostic -Code "InvalidPackage" -Message "NuGet.org rejected the package as invalid." -StatusCode 400
                    }
                    409 {
                        $ledger.packages[$i].mutationState = "Rejected"
                        $ledger.packages[$i].statusCode = 409
                        $ledger.packages[$i].diagnostic = New-PublicationDiagnostic -Code "ExistingIdentityConflict" -Message "NuGet.org reports the package ID/version already exists." -StatusCode 409
                    }
                    default {
                        $ledger.packages[$i].mutationState = "Indeterminate"
                        $ledger.packages[$i].statusCode = $status
                        $ledger.packages[$i].diagnostic = New-PublicationDiagnostic -Code "UnexpectedStatus" -Message "NuGet.org returned an unclassified publication status." -StatusCode $status
                    }
                }
            }
            catch {
                $ledger.packages[$i].mutationState = "Indeterminate"
                $ledger.packages[$i].statusCode = $null
                $ledger.packages[$i].diagnostic = New-PublicationDiagnostic -Code "TransportUncertainty" -Message "Publication transport did not yield a definitive response."
            }
        }
        finally {
            $apiKey = $null
        }

        Update-PublicationCounts -Ledger $ledger
        & $WriteLedger $ledger $ledgerPath

        if ($ledger.packages[$i].mutationState -eq "Rejected") {
            $terminalConclusion = "StoppedRejected"
            break
        }
        if ($ledger.packages[$i].mutationState -eq "Indeterminate") {
            $terminalConclusion = "StoppedIndeterminate"
            break
        }
    }

    if ($null -eq $terminalConclusion) {
        $terminalConclusion = "Complete"
    }

    $ledger.ledgerState = "Terminal"
    $ledger.operationConclusion = $terminalConclusion
    $ledger.completedAtUtc = ([DateTimeOffset](& $Clock)).ToUniversalTime().ToString("O")
    Update-PublicationCounts -Ledger $ledger
    & $WriteLedger $ledger $ledgerPath

    [pscustomobject]@{
        LedgerPath = $ledgerPath
        Result     = $ledger
    }
}
