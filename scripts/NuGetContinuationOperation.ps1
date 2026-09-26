Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'NuGetPublicationOperation.ps1')
. (Join-Path $PSScriptRoot 'NuGetContinuationAdmission.ps1')

function Assert-NuGetContinuationGrantForOperation {
    param(
        [Parameter(Mandatory)] [object] $Grant,
        [Parameter(Mandatory)] [string] $ServiceIndexUri
    )

    foreach ($name in @('HistoricalPublicationOperationId','ContinuationOperationId','TargetRegistryIdentity','PackageIndex','PackageId','PackageVersion','AdmittedSha256','ArtifactPath','SourceCommit','ReleaseAuthorityTag','RecoveryState','RemoteAdmissionState','RemoteAdmissionReason')) {
        $property=$Grant.PSObject.Properties[$name]
        if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
            throw [System.ArgumentException]::new("Continuation grant must contain $name.")
        }
    }

    $parsed=[guid]::Empty
    if (-not [guid]::TryParseExact([string]$Grant.ContinuationOperationId,'D',[ref]$parsed) -or [string]$Grant.ContinuationOperationId -cne $parsed.ToString('D')) {
        throw [System.ArgumentException]::new("ContinuationOperationId must be a canonical lowercase GUID in 'D' format.")
    }
    if ([string]$Grant.AdmittedSha256 -cnotmatch '^[0-9a-f]{64}$') { throw [System.ArgumentException]::new('AdmittedSha256 must be canonical lowercase SHA-256.') }
    if ([int]$Grant.PackageIndex -lt 0) { throw [System.ArgumentException]::new('PackageIndex must be non-negative.') }
    if ([string]$Grant.RecoveryState -cne 'Converged' -or [string]$Grant.RemoteAdmissionState -cne 'Admissible' -or [string]$Grant.RemoteAdmissionReason -cne 'AuthoritativeAbsent') {
        throw [System.InvalidOperationException]::new('Continuation grant is not mutation-authoritative.')
    }

    $grantRegistry=Get-NormalizedPublicationUriIdentity -Value ([string]$Grant.TargetRegistryIdentity) -Context 'Continuation grant registry'
    $targetRegistry=Get-NormalizedPublicationUriIdentity -Value $ServiceIndexUri -Context 'Continuation operation service index'
    if ($grantRegistry -cne $targetRegistry) { throw [System.InvalidOperationException]::new('Continuation grant registry does not match the continuation operation service index.') }

    if (-not (Test-Path -LiteralPath ([string]$Grant.ArtifactPath) -PathType Leaf)) { throw [System.InvalidOperationException]::new('Continuation grant artifact is unavailable.') }
    $actual=(Get-FileHash -LiteralPath ([string]$Grant.ArtifactPath) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -cne [string]$Grant.AdmittedSha256) { throw [System.InvalidOperationException]::new('Continuation grant artifact SHA-256 changed before mutation.') }
}

function Invoke-NuGetContinuationOperation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $Grant,
        [Parameter(Mandatory)] [string] $EvidenceRoot,
        [Parameter(Mandatory)] [scriptblock] $CredentialAvailable,
        [Parameter(Mandatory)] [scriptblock] $AcquireCredential,
        [scriptblock] $DiscoveryRequest = ${function:Invoke-DefaultPublicationDiscoveryRequest},
        [scriptblock] $PublishRequest = ${function:Invoke-DefaultNuGetPublishRequest},
        [scriptblock] $WriteLedger = ${function:Write-AtomicPublicationLedger},
        [scriptblock] $Clock = { [DateTimeOffset]::UtcNow },
        [string] $ServiceIndexUri = $script:NuGetV3ServiceIndex,
        [scriptblock] $AfterAttemptingPersisted = $null
    )

    Assert-NuGetContinuationGrantForOperation -Grant $Grant -ServiceIndexUri $ServiceIndexUri

    try { $discoveryResponse=& $DiscoveryRequest $ServiceIndexUri }
    catch { throw [System.InvalidOperationException]::new('PackagePublish discovery failed before continuation operation ledger creation.', $_.Exception) }
    $discovery=Get-PackagePublishEndpointFromServiceIndex -Response $discoveryResponse
    if (-not $discovery.Success) { throw [System.InvalidOperationException]::new("[$($discovery.Diagnostic.Code)] $($discovery.Diagnostic.Message)") }

    if (-not (& $CredentialAvailable)) { throw [System.InvalidOperationException]::new('Publication credential is unavailable.') }

    $operationId=[string]$Grant.ContinuationOperationId
    $operationDirectory=Join-Path (Join-Path (Join-Path $EvidenceRoot ([string]$Grant.PackageVersion)) 'attempts') $operationId
    $ledgerPath=Join-Path $operationDirectory 'publication-result.json'
    if (Test-Path -LiteralPath $ledgerPath) { throw [System.InvalidOperationException]::new("Continuation operation '$operationId' already has durable evidence and cannot be reopened.") }

    $startedAt=& $Clock
    $ledger=[pscustomobject]@{
        schemaVersion=$script:PublicationSchemaVersion
        operationId=$operationId
        operationKind='Continuation'
        historicalPublicationOperationId=[string]$Grant.HistoricalPublicationOperationId
        packageIndex=[int]$Grant.PackageIndex
        ledgerState='InProgress'
        registry='NuGet.org'
        serviceIndex=$ServiceIndexUri
        packagePublishEndpoint=$discovery.Endpoint
        sourceCommit=[string]$Grant.SourceCommit
        versionPrefix=$null
        packageVersion=[string]$Grant.PackageVersion
        releaseAuthorityTag=[string]$Grant.ReleaseAuthorityTag
        startedAtUtc=([DateTimeOffset]$startedAt).ToUniversalTime().ToString('O')
        completedAtUtc=$null
        operationConclusion=$null
        knownAcceptedCount=0
        knownRejectedCount=0
        indeterminateCount=0
        notAttemptedCount=1
        packages=@([pscustomobject]@{id=[string]$Grant.PackageId;version=[string]$Grant.PackageVersion;admittedSha256=[string]$Grant.AdmittedSha256;mutationState='NotAttempted';statusCode=$null;diagnostic=$null})
    }

    & $WriteLedger $ledger $ledgerPath

    $package=$ledger.packages[0]
    $package.mutationState='Attempting';$package.statusCode=$null;$package.diagnostic=$null
    Update-PublicationCounts -Ledger $ledger
    try { & $WriteLedger $ledger $ledgerPath }
    catch {
        $attemptingPersistenceFailure=$_.Exception
        $package.mutationState='NotAttempted';$package.statusCode=$null;$package.diagnostic=$null
        $ledger.ledgerState='Terminal';$ledger.operationConclusion='NotStarted';$ledger.completedAtUtc=([DateTimeOffset](& $Clock)).ToUniversalTime().ToString('O')
        Update-PublicationCounts -Ledger $ledger
        try { & $WriteLedger $ledger $ledgerPath; return [pscustomobject]@{LedgerPath=$ledgerPath;Result=$ledger} }
        catch { throw $attemptingPersistenceFailure }
    }

    if ($null -ne $AfterAttemptingPersisted) { & $AfterAttemptingPersisted $ledger $ledgerPath 0 }

    try {
        try {
            $apiKey=& $AcquireCredential
            if ([string]::IsNullOrWhiteSpace([string]$apiKey)) { throw [System.InvalidOperationException]::new('Credential provider returned no credential material.') }
        }
        catch {
            $package.mutationState='Indeterminate';$package.diagnostic=New-PublicationDiagnostic -Code 'CredentialAcquisitionFailure' -Message 'Credential acquisition failed after Attempting became durable.'
            Update-PublicationCounts -Ledger $ledger
            & $WriteLedger $ledger $ledgerPath
            $ledger.ledgerState='Terminal';$ledger.operationConclusion='Indeterminate';$ledger.completedAtUtc=([DateTimeOffset](& $Clock)).ToUniversalTime().ToString('O')
            Update-PublicationCounts -Ledger $ledger;& $WriteLedger $ledger $ledgerPath
            return [pscustomobject]@{LedgerPath=$ledgerPath;Result=$ledger}
        }

        try { $response=& $PublishRequest $discovery.Endpoint ([string]$Grant.ArtifactPath) ([string]$apiKey) }
        catch {
            $package.mutationState='Indeterminate';$package.diagnostic=New-PublicationDiagnostic -Code 'TransportUncertainty' -Message 'Package PUT failed without an authoritative registry outcome.'
            Update-PublicationCounts -Ledger $ledger;& $WriteLedger $ledger $ledgerPath
            $ledger.ledgerState='Terminal';$ledger.operationConclusion='Indeterminate';$ledger.completedAtUtc=([DateTimeOffset](& $Clock)).ToUniversalTime().ToString('O')
            Update-PublicationCounts -Ledger $ledger;& $WriteLedger $ledger $ledgerPath
            return [pscustomobject]@{LedgerPath=$ledgerPath;Result=$ledger}
        }

        $status=[int]$response.StatusCode;$package.statusCode=$status
        if ($status -in @(200,201,202)) { $package.mutationState='Accepted';$package.diagnostic=$null;$conclusion='Accepted' }
        elseif ($status -eq 409) { $package.mutationState='Rejected';$package.diagnostic=New-PublicationDiagnostic -Code 'ExistingIdentityConflict' -Message 'Registry reports that the exact package identity already exists.' -StatusCode $status;$conclusion='Rejected' }
        elseif ($status -eq 400) { $package.mutationState='Rejected';$package.diagnostic=New-PublicationDiagnostic -Code 'InvalidPackage' -Message 'Registry rejected the package as invalid.' -StatusCode $status;$conclusion='Rejected' }
        else { $package.mutationState='Indeterminate';$package.diagnostic=New-PublicationDiagnostic -Code 'UnexpectedPublishStatus' -Message "Package PUT returned HTTP $status without a safe continuation interpretation." -StatusCode $status;$conclusion='Indeterminate' }
        Update-PublicationCounts -Ledger $ledger;& $WriteLedger $ledger $ledgerPath
        $ledger.ledgerState='Terminal';$ledger.operationConclusion=$conclusion;$ledger.completedAtUtc=([DateTimeOffset](& $Clock)).ToUniversalTime().ToString('O')
        Update-PublicationCounts -Ledger $ledger;& $WriteLedger $ledger $ledgerPath
        [pscustomobject]@{LedgerPath=$ledgerPath;Result=$ledger}
    }
    finally { $apiKey=$null }
}
