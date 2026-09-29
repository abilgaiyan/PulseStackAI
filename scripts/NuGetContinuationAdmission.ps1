Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'NuGetRemoteEquivalence.ps1')
. (Join-Path $PSScriptRoot 'NuGetReleaseIdentityEvidence.ps1')

function New-NuGetContinuationAdmissionDiagnostic {
    param(
        [Parameter(Mandatory)] [string] $Code,
        [Parameter(Mandatory)] [string] $Message,
        [AllowNull()] [Nullable[int]] $StatusCode = $null
    )
    [pscustomobject]@{ Code=$Code; Message=$Message; StatusCode=$StatusCode }
}

function Assert-NuGetContinuationAdmissionSelection {
    param(
        [Parameter(Mandatory)][object]$Selection,
        [AllowNull()][object]$AdmittedPackageSet = $null
    )

    foreach ($name in @('ReleaseIdentityProfile','ReleaseIdentitySha256','PackageIndex','PackageId','PackageVersion','AdmittedSha256','SourceCommit','ReleaseAuthorityTag','SelectionSource','LegacyHistoricalPublicationOperationId')) {
        if ($null -eq $Selection.PSObject.Properties[$name]) {
            throw [System.InvalidOperationException]::new("Continuation selection must contain $name.")
        }
    }

    if ([string]$Selection.ReleaseIdentityProfile -cne '1') {
        throw [System.InvalidOperationException]::new('Continuation selection release identity profile is unsupported.')
    }
    if ([string]$Selection.ReleaseIdentitySha256 -cnotmatch '^[0-9a-f]{64}$' -or [string]$Selection.AdmittedSha256 -cnotmatch '^[0-9a-f]{64}$') {
        throw [System.InvalidOperationException]::new('Continuation selection must contain canonical lowercase SHA-256 identities.')
    }
    if ([string]::IsNullOrWhiteSpace([string]$Selection.PackageId) -or [string]::IsNullOrWhiteSpace([string]$Selection.PackageVersion) -or
        [string]::IsNullOrWhiteSpace([string]$Selection.SourceCommit) -or [string]::IsNullOrWhiteSpace([string]$Selection.ReleaseAuthorityTag)) {
        throw [System.InvalidOperationException]::new('Continuation selection contains incomplete canonical release/package provenance.')
    }

    $source=[string]$Selection.SelectionSource
    if ($source -ceq 'RP3RecoveryContinuation') {
        if ([string]::IsNullOrWhiteSpace([string]$Selection.LegacyHistoricalPublicationOperationId)) {
            throw [System.InvalidOperationException]::new('RP-3 continuation selection requires genuine historical publication operation provenance.')
        }
    }
    elseif ($source -ceq 'RP5EffectiveRelease') {
        if (-not [string]::IsNullOrEmpty([string]$Selection.LegacyHistoricalPublicationOperationId)) {
            throw [System.InvalidOperationException]::new('RP-5 continuation selection must not synthesize historical publication operation provenance.')
        }
    }
    else {
        throw [System.InvalidOperationException]::new("Continuation selection source '$source' is unsupported.")
    }

    if ($null -ne $AdmittedPackageSet) {
        $identity=Get-NuGetReleaseIdentityEvidence -AdmittedPackageSet $AdmittedPackageSet
        if ([string]$Selection.ReleaseIdentityProfile -cne [string]$identity.ReleaseIdentityProfile -or
            [string]$Selection.ReleaseIdentitySha256 -cne [string]$identity.ReleaseIdentitySha256 -or
            [string]$Selection.SourceCommit -cne [string]$AdmittedPackageSet.SourceCommit -or
            [string]$Selection.ReleaseAuthorityTag -cne [string]$AdmittedPackageSet.ReleaseAuthorityTag) {
            throw [System.InvalidOperationException]::new('Continuation selection does not belong to the supplied admitted release identity.')
        }
        $packages=@($AdmittedPackageSet.Packages)
        $index=[int]$Selection.PackageIndex
        if ($index -lt 0 -or $index -ge $packages.Count) {
            throw [System.InvalidOperationException]::new("Continuation selection package index '$index' is outside the admitted release.")
        }
        $expected=$packages[$index]
        if ([string]$Selection.PackageId -cne [string]$expected.Id -or
            [string]$Selection.PackageVersion -cne [string]$expected.Version -or
            [string]$Selection.AdmittedSha256 -cne [string]$expected.Sha256) {
            throw [System.InvalidOperationException]::new("Continuation selection package does not match admitted release position $index.")
        }
    }

    return $Selection
}

function New-IndeterminateContinuationAdmission {
    param([string]$Registry,[AllowNull()][string]$Base,[AllowNull()][string]$ContentUri,[object]$Selection,[object]$Diagnostic,[AllowNull()][Nullable[int]]$StatusCode,[datetime]$ObservedAtUtc)
    [pscustomobject]@{
        Registry=$Registry; PackageBaseAddress=$Base; PackageContentUri=$ContentUri
        ReleaseIdentityProfile=[string]$Selection.ReleaseIdentityProfile; ReleaseIdentitySha256=[string]$Selection.ReleaseIdentitySha256
        PackageIndex=[int]$Selection.PackageIndex; Id=[string]$Selection.PackageId; Version=[string]$Selection.PackageVersion; AdmittedSha256=[string]$Selection.AdmittedSha256
        SelectionSource=[string]$Selection.SelectionSource
        RemoteSha256=$null; State='Indeterminate'; Reason=[string]$Diagnostic.Code
        StatusCode=$StatusCode; ObservedAtUtc=$ObservedAtUtc; Diagnostic=$Diagnostic
    }
}

function Invoke-NuGetExactSuccessorRemoteAdmission {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $ContinuationSelection,
        [string] $ServiceIndexUri = $script:NuGetV3ServiceIndex,
        [scriptblock] $Request = ${function:Invoke-DefaultNuGetRemoteEquivalenceRequest},
        [scriptblock] $Clock = { [DateTime]::UtcNow }
    )

    $selection=Assert-NuGetContinuationAdmissionSelection -Selection $ContinuationSelection
    $registry=Normalize-RegistryUri -Uri $ServiceIndexUri
    $observed=& $Clock
    try { $service=& $Request (New-NuGetRemoteEquivalenceRequest -Uri $ServiceIndexUri) }
    catch { return New-IndeterminateContinuationAdmission -Registry $registry -Base $null -ContentUri $null -Selection $selection -Diagnostic (New-NuGetContinuationAdmissionDiagnostic -Code 'ServiceIndexUnavailable' -Message $_.Exception.Message) -StatusCode $null -ObservedAtUtc $observed }

    $discovery=Get-PackageBaseAddressForEquivalence -Response $service
    if (-not $discovery.Success) {
        return New-IndeterminateContinuationAdmission -Registry $registry -Base $null -ContentUri $null -Selection $selection -Diagnostic (New-NuGetContinuationAdmissionDiagnostic -Code ([string]$discovery.Diagnostic.Code) -Message ([string]$discovery.Diagnostic.Message) -StatusCode $discovery.Diagnostic.StatusCode) -StatusCode $discovery.Diagnostic.StatusCode -ObservedAtUtc $observed
    }
    try { $contentUri=Get-NuGetRemotePackageContentUri -BaseAddress $discovery.BaseAddress -Id ([string]$selection.PackageId) -Version ([string]$selection.PackageVersion) }
    catch { return New-IndeterminateContinuationAdmission -Registry $registry -Base $discovery.BaseAddress -ContentUri $null -Selection $selection -Diagnostic (New-NuGetContinuationAdmissionDiagnostic -Code 'PackageIdentityInvalid' -Message $_.Exception.Message) -StatusCode $null -ObservedAtUtc $observed }

    try { $response=& $Request (New-NuGetRemoteEquivalenceRequest -Uri $contentUri) }
    catch { return New-IndeterminateContinuationAdmission -Registry $registry -Base $discovery.BaseAddress -ContentUri $contentUri -Selection $selection -Diagnostic (New-NuGetContinuationAdmissionDiagnostic -Code 'TransportFailure' -Message $_.Exception.Message) -StatusCode $null -ObservedAtUtc $observed }

    $status=[int]$response.StatusCode
    if ($status -eq 404) {
        return [pscustomobject]@{
            Registry=$registry; PackageBaseAddress=$discovery.BaseAddress; PackageContentUri=$contentUri
            ReleaseIdentityProfile=[string]$selection.ReleaseIdentityProfile; ReleaseIdentitySha256=[string]$selection.ReleaseIdentitySha256
            PackageIndex=[int]$selection.PackageIndex; Id=[string]$selection.PackageId; Version=[string]$selection.PackageVersion; AdmittedSha256=[string]$selection.AdmittedSha256
            SelectionSource=[string]$selection.SelectionSource
            RemoteSha256=$null; State='Admissible'; Reason='AuthoritativeAbsent'; StatusCode=404; ObservedAtUtc=$observed; Diagnostic=$null
        }
    }
    if ($status -ne 200) {
        $code=if($status -eq 429){'RateLimited'}elseif($status -ge 500 -and $status -le 599){'ServerFailure'}else{'UnexpectedStatus'}
        return New-IndeterminateContinuationAdmission -Registry $registry -Base $discovery.BaseAddress -ContentUri $contentUri -Selection $selection -Diagnostic (New-NuGetContinuationAdmissionDiagnostic -Code $code -Message "NuGet exact-successor observation returned HTTP $status." -StatusCode $status) -StatusCode $status -ObservedAtUtc $observed
    }

    $bytesProperty=$response.PSObject.Properties['Bytes']
    if ($null -eq $bytesProperty -or $null -eq $bytesProperty.Value) {
        return New-IndeterminateContinuationAdmission -Registry $registry -Base $discovery.BaseAddress -ContentUri $contentUri -Selection $selection -Diagnostic (New-NuGetContinuationAdmissionDiagnostic -Code 'ContentUnreadable' -Message 'NuGet exact-successor response did not provide complete package bytes.' -StatusCode 200) -StatusCode 200 -ObservedAtUtc $observed
    }
    try { $remoteHash=Get-Sha256Hex -Bytes ([byte[]]$bytesProperty.Value) }
    catch { return New-IndeterminateContinuationAdmission -Registry $registry -Base $discovery.BaseAddress -ContentUri $contentUri -Selection $selection -Diagnostic (New-NuGetContinuationAdmissionDiagnostic -Code 'ContentHashFailure' -Message $_.Exception.Message -StatusCode 200) -StatusCode 200 -ObservedAtUtc $observed }

    $reason=if($remoteHash -ceq [string]$selection.AdmittedSha256){'EquivalentPackagePresent'}else{'DifferentPackagePresent'}
    [pscustomobject]@{
        Registry=$registry; PackageBaseAddress=$discovery.BaseAddress; PackageContentUri=$contentUri
        ReleaseIdentityProfile=[string]$selection.ReleaseIdentityProfile; ReleaseIdentitySha256=[string]$selection.ReleaseIdentitySha256
        PackageIndex=[int]$selection.PackageIndex; Id=[string]$selection.PackageId; Version=[string]$selection.PackageVersion; AdmittedSha256=[string]$selection.AdmittedSha256
        SelectionSource=[string]$selection.SelectionSource
        RemoteSha256=$remoteHash; State='Blocked'; Reason=$reason; StatusCode=200; ObservedAtUtc=$observed; Diagnostic=$null
    }
}

function New-NuGetExactPackageContinuationGrant {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $AdmittedPackageSet,
        [Parameter(Mandatory)] [object] $ContinuationSelection,
        [Parameter(Mandatory)] [object] $RemoteAdmission,
        [Parameter(Mandatory)] [string] $ContinuationOperationId
    )

    $parsed=[guid]::Empty
    if (-not [guid]::TryParseExact($ContinuationOperationId,'D',[ref]$parsed) -or $ContinuationOperationId -cne $parsed.ToString('D')) { throw [ArgumentException]::new("ContinuationOperationId must be a canonical lowercase GUID in 'D' format.") }
    $selection=Assert-NuGetContinuationAdmissionSelection -Selection $ContinuationSelection -AdmittedPackageSet $AdmittedPackageSet
    if ([string]$RemoteAdmission.State -cne 'Admissible' -or [string]$RemoteAdmission.Reason -cne 'AuthoritativeAbsent' -or [int]$RemoteAdmission.StatusCode -ne 404) { throw [InvalidOperationException]::new('Remote admission is not Admissible by authoritative exact absence.') }

    foreach ($name in @('ReleaseIdentityProfile','ReleaseIdentitySha256','PackageIndex','Id','Version','AdmittedSha256','SelectionSource')) {
        if ($null -eq $RemoteAdmission.PSObject.Properties[$name]) { throw [InvalidOperationException]::new("Remote admission does not contain $name binding.") }
    }
    if ([string]$RemoteAdmission.ReleaseIdentityProfile -cne [string]$selection.ReleaseIdentityProfile -or
        [string]$RemoteAdmission.ReleaseIdentitySha256 -cne [string]$selection.ReleaseIdentitySha256 -or
        [int]$RemoteAdmission.PackageIndex -ne [int]$selection.PackageIndex -or
        [string]$RemoteAdmission.Id -cne [string]$selection.PackageId -or
        [string]$RemoteAdmission.Version -cne [string]$selection.PackageVersion -or
        [string]$RemoteAdmission.AdmittedSha256 -cne [string]$selection.AdmittedSha256 -or
        [string]$RemoteAdmission.SelectionSource -cne [string]$selection.SelectionSource) {
        throw [InvalidOperationException]::new('Remote admission does not bind to the exact canonical continuation selection.')
    }

    $packages=@($AdmittedPackageSet.Packages)
    $package=$packages[[int]$selection.PackageIndex]
    if (-not (Test-Path -LiteralPath ([string]$package.FilePath) -PathType Leaf)) { throw [InvalidOperationException]::new('Exact successor artifact is unavailable.') }
    $actual=(Get-FileHash -LiteralPath ([string]$package.FilePath) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -cne [string]$package.Sha256) { throw [InvalidOperationException]::new('Exact successor artifact SHA-256 changed after admission.') }

    [pscustomobject]@{
        HistoricalPublicationOperationId=if ([string]$selection.SelectionSource -ceq 'RP3RecoveryContinuation') { [string]$selection.LegacyHistoricalPublicationOperationId } else { $null }
        ContinuationOperationId=$parsed.ToString('D')
        TargetRegistryIdentity=[string]$RemoteAdmission.Registry
        ReleaseIdentityProfile=[string]$selection.ReleaseIdentityProfile
        ReleaseIdentitySha256=[string]$selection.ReleaseIdentitySha256
        SelectionSource=[string]$selection.SelectionSource
        PackageIndex=[int]$selection.PackageIndex
        PackageId=[string]$package.Id
        PackageVersion=[string]$package.Version
        AdmittedSha256=[string]$package.Sha256
        ArtifactPath=[string]$package.FilePath
        SourceCommit=[string]$AdmittedPackageSet.SourceCommit
        ReleaseAuthorityTag=[string]$AdmittedPackageSet.ReleaseAuthorityTag
        RemoteAdmissionState=[string]$RemoteAdmission.State
        RemoteAdmissionReason=[string]$RemoteAdmission.Reason
        RemoteObservedAtUtc=$RemoteAdmission.ObservedAtUtc
    }
}
