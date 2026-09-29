Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'NuGetReleaseEvidenceClaims.ps1')

function Assert-NuGetContinuationSelectionProperty {
    param(
        [Parameter(Mandatory)][object]$Object,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Context
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        throw [System.InvalidOperationException]::new("$Context must contain $Name.")
    }

    return $property.Value
}

function Assert-NuGetContinuationSelectedPackage {
    param(
        [Parameter(Mandatory)][object]$CanonicalRelease,
        [Parameter(Mandatory)][int]$PackageIndex,
        [Parameter(Mandatory)][string]$PackageId,
        [Parameter(Mandatory)][string]$PackageVersion,
        [Parameter(Mandatory)][string]$AdmittedSha256
    )

    if ($PackageIndex -lt 0 -or $PackageIndex -ge $CanonicalRelease.PackageCount) {
        throw [System.InvalidOperationException]::new("Continuation selection package index '$PackageIndex' is outside the canonical release.")
    }

    $expected = $CanonicalRelease.Packages[$PackageIndex]
    if ($PackageId -cne [string]$expected.Id -or
        $PackageVersion -cne [string]$expected.Version -or
        $AdmittedSha256 -cne [string]$expected.Sha256) {
        throw [System.InvalidOperationException]::new("Continuation selection package does not match canonical release position $PackageIndex.")
    }
}

function New-NuGetContinuationSelection {
    param(
        [Parameter(Mandatory)][object]$CanonicalRelease,
        [Parameter(Mandatory)][int]$PackageIndex,
        [Parameter(Mandatory)][string]$PackageId,
        [Parameter(Mandatory)][string]$PackageVersion,
        [Parameter(Mandatory)][string]$AdmittedSha256,
        [Parameter(Mandatory)][ValidateSet('RP3RecoveryContinuation','RP5EffectiveRelease')][string]$SelectionSource,
        [Parameter(Mandatory)][object]$SelectionEvidence,
        [AllowNull()][string]$LegacyHistoricalPublicationOperationId
    )

    Assert-NuGetContinuationSelectedPackage -CanonicalRelease $CanonicalRelease -PackageIndex $PackageIndex -PackageId $PackageId -PackageVersion $PackageVersion -AdmittedSha256 $AdmittedSha256

    if ($SelectionSource -ceq 'RP3RecoveryContinuation') {
        if ([string]::IsNullOrWhiteSpace($LegacyHistoricalPublicationOperationId)) {
            throw [System.InvalidOperationException]::new('RP-3 recovery continuation selection requires genuine historical publication operation provenance.')
        }
    }
    elseif (-not [string]::IsNullOrEmpty($LegacyHistoricalPublicationOperationId)) {
        throw [System.InvalidOperationException]::new('RP-5 effective-release continuation selection must not synthesize historical publication operation provenance.')
    }

    [pscustomobject]@{
        ReleaseIdentityProfile = [string]$CanonicalRelease.Profile
        ReleaseIdentitySha256 = [string]$CanonicalRelease.Sha256
        PackageIndex = $PackageIndex
        PackageId = $PackageId
        PackageVersion = $PackageVersion
        AdmittedSha256 = $AdmittedSha256
        SourceCommit = [string]$CanonicalRelease.SourceCommit
        ReleaseAuthorityTag = [string]$CanonicalRelease.ReleaseAuthorityTag
        SelectionSource = $SelectionSource
        SelectionEvidence = $SelectionEvidence
        LegacyHistoricalPublicationOperationId = if ([string]::IsNullOrEmpty($LegacyHistoricalPublicationOperationId)) { $null } else { $LegacyHistoricalPublicationOperationId }
    }
}

function ConvertFrom-NuGetRecoveryContinuationSelection {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Release,
        [Parameter(Mandatory)][object]$ContinuationDecision
    )

    $canonical = Get-Rp5CanonicalRelease $Release
    $recoveryState = [string](Assert-NuGetContinuationSelectionProperty $ContinuationDecision 'RecoveryState' 'RP-3C continuation decision')
    $mayContinue = [bool](Assert-NuGetContinuationSelectionProperty $ContinuationDecision 'MayContinue' 'RP-3C continuation decision')
    $hasNextPackage = [bool](Assert-NuGetContinuationSelectionProperty $ContinuationDecision 'HasNextPackage' 'RP-3C continuation decision')
    $disposition = [string](Assert-NuGetContinuationSelectionProperty $ContinuationDecision 'WholeOperationDisposition' 'RP-3C continuation decision')

    if ($recoveryState -cne 'Converged') {
        throw [System.InvalidOperationException]::new('RP-3C continuation selection requires Converged recovery.')
    }
    if (-not $mayContinue -or -not $hasNextPackage) {
        throw [System.InvalidOperationException]::new('RP-3C continuation decision is not eligible to continue.')
    }
    if ($disposition -cne 'ContinuationEligible') {
        throw [System.InvalidOperationException]::new("RP-3C continuation decision disposition must be exactly 'ContinuationEligible'.")
    }

    $operationId = [string](Assert-NuGetContinuationSelectionProperty $ContinuationDecision 'OperationId' 'RP-3C continuation decision')
    if ([string]::IsNullOrWhiteSpace($operationId)) {
        throw [System.InvalidOperationException]::new('RP-3C continuation decision must preserve its historical publication operation ID.')
    }

    $recoveryPackageIndex = [int](Assert-NuGetContinuationSelectionProperty $ContinuationDecision 'RecoveryPackageIndex' 'RP-3C continuation decision')
    $packageIndex = [int](Assert-NuGetContinuationSelectionProperty $ContinuationDecision 'NextPackageIndex' 'RP-3C continuation decision')
    if ($packageIndex -ne ($recoveryPackageIndex + 1)) {
        throw [System.InvalidOperationException]::new('RP-3C continuation selection must identify the exact successor immediately after the recovery boundary.')
    }

    $package = Assert-NuGetContinuationSelectionProperty $ContinuationDecision 'NextPackage' 'RP-3C continuation decision'
    if ($null -eq $package) {
        throw [System.InvalidOperationException]::new('RP-3C continuation decision must contain its exact next package.')
    }

    $packageId = [string](Assert-NuGetContinuationSelectionProperty $package 'Id' 'RP-3C next package')
    $packageVersion = [string](Assert-NuGetContinuationSelectionProperty $package 'Version' 'RP-3C next package')
    $sha = [string](Assert-NuGetContinuationSelectionProperty $package 'AdmittedSha256' 'RP-3C next package')
    $mutationState = [string](Assert-NuGetContinuationSelectionProperty $package 'MutationState' 'RP-3C next package')
    if ($mutationState -cne 'NotAttempted') {
        throw [System.InvalidOperationException]::new("RP-3C continuation successor must be exactly NotAttempted, actual '$mutationState'.")
    }

    New-NuGetContinuationSelection -CanonicalRelease $canonical -PackageIndex $packageIndex -PackageId $packageId -PackageVersion $packageVersion -AdmittedSha256 $sha -SelectionSource 'RP3RecoveryContinuation' -SelectionEvidence $ContinuationDecision -LegacyHistoricalPublicationOperationId $operationId
}

function ConvertFrom-NuGetEffectiveReleaseContinuationSelection {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Release,
        [Parameter(Mandatory)][object]$EffectiveReleaseResult
    )

    $canonical = Get-Rp5CanonicalRelease $Release
    $releaseState = [string](Assert-NuGetContinuationSelectionProperty $EffectiveReleaseResult 'ReleaseState' 'RP-5D whole-release result')
    if ($releaseState -cne 'ContinuationEligible') {
        throw [System.InvalidOperationException]::new("RP-5D whole-release result must be exactly 'ContinuationEligible'.")
    }

    $profile = [string](Assert-NuGetContinuationSelectionProperty $EffectiveReleaseResult 'ReleaseIdentityProfile' 'RP-5D whole-release result')
    $releaseSha = [string](Assert-NuGetContinuationSelectionProperty $EffectiveReleaseResult 'ReleaseIdentitySha256' 'RP-5D whole-release result')
    $sourceCommit = [string](Assert-NuGetContinuationSelectionProperty $EffectiveReleaseResult 'SourceCommit' 'RP-5D whole-release result')
    $releaseAuthorityTag = [string](Assert-NuGetContinuationSelectionProperty $EffectiveReleaseResult 'ReleaseAuthorityTag' 'RP-5D whole-release result')
    if ($profile -cne $canonical.Profile -or $releaseSha -cne $canonical.Sha256 -or $sourceCommit -cne $canonical.SourceCommit -or $releaseAuthorityTag -cne $canonical.ReleaseAuthorityTag) {
        throw [System.InvalidOperationException]::new('RP-5D whole-release result does not belong to the supplied canonical release identity.')
    }

    $packageIndex = [int](Assert-NuGetContinuationSelectionProperty $EffectiveReleaseResult 'PackageIndex' 'RP-5D whole-release result')
    $packageId = [string](Assert-NuGetContinuationSelectionProperty $EffectiveReleaseResult 'PackageId' 'RP-5D whole-release result')
    $packageVersion = [string](Assert-NuGetContinuationSelectionProperty $EffectiveReleaseResult 'PackageVersion' 'RP-5D whole-release result')
    $sha = [string](Assert-NuGetContinuationSelectionProperty $EffectiveReleaseResult 'AdmittedSha256' 'RP-5D whole-release result')

    New-NuGetContinuationSelection -CanonicalRelease $canonical -PackageIndex $packageIndex -PackageId $packageId -PackageVersion $packageVersion -AdmittedSha256 $sha -SelectionSource 'RP5EffectiveRelease' -SelectionEvidence $EffectiveReleaseResult -LegacyHistoricalPublicationOperationId $null
}
