Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'NuGetRemoteEquivalence.ps1')

function New-NuGetContinuationAdmissionDiagnostic {
    param(
        [Parameter(Mandatory)] [string] $Code,
        [Parameter(Mandatory)] [string] $Message,
        [AllowNull()] [Nullable[int]] $StatusCode = $null
    )
    [pscustomobject]@{ Code=$Code; Message=$Message; StatusCode=$StatusCode }
}

function New-IndeterminateContinuationAdmission {
    param([string]$Registry,[AllowNull()][string]$Base,[AllowNull()][string]$ContentUri,[object]$Package,[object]$Diagnostic,[AllowNull()][Nullable[int]]$StatusCode,[datetime]$ObservedAtUtc)
    [pscustomobject]@{
        Registry=$Registry; PackageBaseAddress=$Base; PackageContentUri=$ContentUri
        Id=[string]$Package.Id; Version=[string]$Package.Version; AdmittedSha256=[string]$Package.Sha256
        RemoteSha256=$null; State='Indeterminate'; Reason=[string]$Diagnostic.Code
        StatusCode=$StatusCode; ObservedAtUtc=$ObservedAtUtc; Diagnostic=$Diagnostic
    }
}

function Invoke-NuGetExactSuccessorRemoteAdmission {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $Package,
        [Parameter(Mandatory)] [object] $ContinuationDecision,
        [string] $ServiceIndexUri = $script:NuGetV3ServiceIndex,
        [scriptblock] $Request = ${function:Invoke-DefaultNuGetRemoteEquivalenceRequest},
        [scriptblock] $Clock = { [DateTime]::UtcNow }
    )

    foreach ($name in @('Id','Version','Sha256')) {
        $p=$Package.PSObject.Properties[$name]
        if ($null -eq $p -or [string]::IsNullOrWhiteSpace([string]$p.Value)) { throw [ArgumentException]::new("Package must contain $name.") }
    }
    if ([string]$Package.Sha256 -cnotmatch '^[0-9a-f]{64}$') { throw [ArgumentException]::new('Package Sha256 must be canonical lowercase SHA-256.') }

    if ($null -eq $ContinuationDecision.PSObject.Properties['MayContinue'] -or -not [bool]$ContinuationDecision.MayContinue -or
        [string]$ContinuationDecision.WholeOperationDisposition -cne 'ContinuationEligible' -or $null -eq $ContinuationDecision.NextPackage) {
        throw [InvalidOperationException]::new('Continuation decision does not authorize exact-successor admission.')
    }
    $next=$ContinuationDecision.NextPackage
    if ([string]$next.MutationState -cne 'NotAttempted' -or
        [string]$next.Id -cne [string]$Package.Id -or [string]$next.Version -cne [string]$Package.Version -or
        [string]$next.AdmittedSha256 -cne [string]$Package.Sha256) {
        throw [InvalidOperationException]::new('Package does not match the exact RP-3C-selected NotAttempted successor.')
    }

    $registry=Normalize-RegistryUri -Uri $ServiceIndexUri
    $observed=& $Clock
    try { $service=& $Request (New-NuGetRemoteEquivalenceRequest -Uri $ServiceIndexUri) }
    catch { return New-IndeterminateContinuationAdmission -Registry $registry -Base $null -ContentUri $null -Package $Package -Diagnostic (New-NuGetContinuationAdmissionDiagnostic -Code 'ServiceIndexUnavailable' -Message $_.Exception.Message) -StatusCode $null -ObservedAtUtc $observed }

    $discovery=Get-PackageBaseAddressForEquivalence -Response $service
    if (-not $discovery.Success) {
        return New-IndeterminateContinuationAdmission -Registry $registry -Base $null -ContentUri $null -Package $Package -Diagnostic (New-NuGetContinuationAdmissionDiagnostic -Code ([string]$discovery.Diagnostic.Code) -Message ([string]$discovery.Diagnostic.Message) -StatusCode $discovery.Diagnostic.StatusCode) -StatusCode $discovery.Diagnostic.StatusCode -ObservedAtUtc $observed
    }
    try { $contentUri=Get-NuGetRemotePackageContentUri -BaseAddress $discovery.BaseAddress -Id ([string]$Package.Id) -Version ([string]$Package.Version) }
    catch { return New-IndeterminateContinuationAdmission -Registry $registry -Base $discovery.BaseAddress -ContentUri $null -Package $Package -Diagnostic (New-NuGetContinuationAdmissionDiagnostic -Code 'PackageIdentityInvalid' -Message $_.Exception.Message) -StatusCode $null -ObservedAtUtc $observed }

    try { $response=& $Request (New-NuGetRemoteEquivalenceRequest -Uri $contentUri) }
    catch { return New-IndeterminateContinuationAdmission -Registry $registry -Base $discovery.BaseAddress -ContentUri $contentUri -Package $Package -Diagnostic (New-NuGetContinuationAdmissionDiagnostic -Code 'TransportFailure' -Message $_.Exception.Message) -StatusCode $null -ObservedAtUtc $observed }

    $status=[int]$response.StatusCode
    if ($status -eq 404) {
        return [pscustomobject]@{ Registry=$registry; PackageBaseAddress=$discovery.BaseAddress; PackageContentUri=$contentUri; Id=[string]$Package.Id; Version=[string]$Package.Version; AdmittedSha256=[string]$Package.Sha256; RemoteSha256=$null; State='Admissible'; Reason='AuthoritativeAbsent'; StatusCode=404; ObservedAtUtc=$observed; Diagnostic=$null }
    }
    if ($status -ne 200) {
        $code=if($status -eq 429){'RateLimited'}elseif($status -ge 500 -and $status -le 599){'ServerFailure'}else{'UnexpectedStatus'}
        return New-IndeterminateContinuationAdmission -Registry $registry -Base $discovery.BaseAddress -ContentUri $contentUri -Package $Package -Diagnostic (New-NuGetContinuationAdmissionDiagnostic -Code $code -Message "NuGet exact-successor observation returned HTTP $status." -StatusCode $status) -StatusCode $status -ObservedAtUtc $observed
    }

    $bytesProperty=$response.PSObject.Properties['Bytes']
    if ($null -eq $bytesProperty -or $null -eq $bytesProperty.Value) {
        return New-IndeterminateContinuationAdmission -Registry $registry -Base $discovery.BaseAddress -ContentUri $contentUri -Package $Package -Diagnostic (New-NuGetContinuationAdmissionDiagnostic -Code 'ContentUnreadable' -Message 'NuGet exact-successor response did not provide complete package bytes.' -StatusCode 200) -StatusCode 200 -ObservedAtUtc $observed
    }
    try { $remoteHash=Get-Sha256Hex -Bytes ([byte[]]$bytesProperty.Value) }
    catch { return New-IndeterminateContinuationAdmission -Registry $registry -Base $discovery.BaseAddress -ContentUri $contentUri -Package $Package -Diagnostic (New-NuGetContinuationAdmissionDiagnostic -Code 'ContentHashFailure' -Message $_.Exception.Message -StatusCode 200) -StatusCode 200 -ObservedAtUtc $observed }

    $reason=if($remoteHash -ceq [string]$Package.Sha256){'EquivalentPackagePresent'}else{'DifferentPackagePresent'}
    [pscustomobject]@{ Registry=$registry; PackageBaseAddress=$discovery.BaseAddress; PackageContentUri=$contentUri; Id=[string]$Package.Id; Version=[string]$Package.Version; AdmittedSha256=[string]$Package.Sha256; RemoteSha256=$remoteHash; State='Blocked'; Reason=$reason; StatusCode=200; ObservedAtUtc=$observed; Diagnostic=$null }
}

function New-NuGetExactPackageContinuationGrant {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $AdmittedPackageSet,
        [Parameter(Mandatory)] [object] $ContinuationDecision,
        [Parameter(Mandatory)] [object] $RemoteAdmission,
        [Parameter(Mandatory)] [object] $RecoveryEvidence,
        [Parameter(Mandatory)] [string] $ContinuationOperationId
    )

    $parsed=[guid]::Empty
    if (-not [guid]::TryParseExact($ContinuationOperationId,'D',[ref]$parsed) -or $ContinuationOperationId -cne $parsed.ToString('D')) { throw [ArgumentException]::new("ContinuationOperationId must be a canonical lowercase GUID in 'D' format.") }
    if (-not [bool]$ContinuationDecision.MayContinue -or [string]$ContinuationDecision.WholeOperationDisposition -cne 'ContinuationEligible') { throw [InvalidOperationException]::new('Continuation decision is not eligible.') }
    if ([string]$RemoteAdmission.State -cne 'Admissible' -or [string]$RemoteAdmission.Reason -cne 'AuthoritativeAbsent' -or [int]$RemoteAdmission.StatusCode -ne 404) { throw [InvalidOperationException]::new('Remote admission is not Admissible by authoritative exact absence.') }

    $next=$ContinuationDecision.NextPackage
    $matches=@($AdmittedPackageSet.Packages | Where-Object { [string]$_.Id -ceq [string]$next.Id -and [string]$_.Version -ceq [string]$next.Version -and [string]$_.Sha256 -ceq [string]$next.AdmittedSha256 })
    if ($matches.Count -ne 1) { throw [InvalidOperationException]::new('Exact successor does not uniquely match the admitted package set.') }
    $package=$matches[0]
    if ([string]$RemoteAdmission.Id -cne [string]$package.Id -or [string]$RemoteAdmission.Version -cne [string]$package.Version -or [string]$RemoteAdmission.AdmittedSha256 -cne [string]$package.Sha256) { throw [InvalidOperationException]::new('Remote admission does not bind to the exact successor artifact.') }
    if (-not (Test-Path -LiteralPath ([string]$package.FilePath) -PathType Leaf)) { throw [InvalidOperationException]::new('Exact successor artifact is unavailable.') }
    $actual=(Get-FileHash -LiteralPath ([string]$package.FilePath) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -cne [string]$package.Sha256) { throw [InvalidOperationException]::new('Exact successor artifact SHA-256 changed after admission.') }

    $historical=$RecoveryEvidence.HistoricalPublication
    if ($null -eq $historical -or [string]$historical.Operation.OperationId -cne [string]$ContinuationDecision.OperationId -or [string]$RecoveryEvidence.Recovery.RecoveryState -cne 'Converged') { throw [InvalidOperationException]::new('Recovery evidence does not bind a converged recovery to the historical publication operation.') }

    [pscustomobject]@{
        HistoricalPublicationOperationId=[string]$ContinuationDecision.OperationId
        ContinuationOperationId=$parsed.ToString('D')
        TargetRegistryIdentity=[string]$RemoteAdmission.Registry
        PackageIndex=[int]$ContinuationDecision.NextPackageIndex
        PackageId=[string]$package.Id
        PackageVersion=[string]$package.Version
        AdmittedSha256=[string]$package.Sha256
        ArtifactPath=[string]$package.FilePath
        SourceCommit=[string]$AdmittedPackageSet.SourceCommit
        ReleaseAuthorityTag=[string]$AdmittedPackageSet.ReleaseAuthorityTag
        RecoveryState=[string]$RecoveryEvidence.Recovery.RecoveryState
        RemoteAdmissionState=[string]$RemoteAdmission.State
        RemoteAdmissionReason=[string]$RemoteAdmission.Reason
        RemoteObservedAtUtc=$RemoteAdmission.ObservedAtUtc
    }
}
