Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'NuGetRecoveryEvidence.ps1')

function Get-NuGetRecoveryDiagnosticSignature {
    [CmdletBinding()]
    param([AllowNull()][object] $Diagnostic)

    if ($null -eq $Diagnostic) { return '<null>' }

    $code = if ($null -ne $Diagnostic.PSObject.Properties['Code']) { [string]$Diagnostic.Code } else { '' }
    $message = if ($null -ne $Diagnostic.PSObject.Properties['Message']) { [string]$Diagnostic.Message } else { '' }
    $statusCode = if ($null -ne $Diagnostic.PSObject.Properties['StatusCode']) { [string]$Diagnostic.StatusCode } else { '' }
    return "$code|$message|$statusCode"
}

function Assert-NuGetRecoveryBindingMatchesCandidate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $Candidate,
        [Parameter(Mandatory)] [object] $RecoveryEvidence
    )

    if ($null -eq $RecoveryEvidence.PSObject.Properties['HistoricalPublication'] -or
        $null -eq $RecoveryEvidence.PSObject.Properties['Recovery']) {
        throw [System.InvalidOperationException]::new('Recovery evidence is missing historical publication or recovery data.')
    }

    $historicalOperation = $RecoveryEvidence.HistoricalPublication.Operation
    $historicalPackage = $RecoveryEvidence.HistoricalPublication.Package
    $recovery = $RecoveryEvidence.Recovery

    if ([string]$historicalOperation.OperationId -cne [string]$Candidate.Operation.OperationId -or
        [string]$historicalPackage.Id -cne [string]$Candidate.Package.Id -or
        [string]$historicalPackage.Version -cne [string]$Candidate.Package.Version -or
        [string]$historicalPackage.AdmittedSha256 -cne [string]$Candidate.Package.AdmittedSha256) {
        throw [System.InvalidOperationException]::new('Recovery evidence does not bind to the exact historical recovery candidate.')
    }

    if ([string]$historicalOperation.LedgerState -cne [string]$Candidate.Operation.LedgerState -or
        [string]$historicalOperation.OperationConclusion -cne [string]$Candidate.Operation.OperationConclusion -or
        [string]$historicalOperation.StartedAtUtc -cne [string]$Candidate.Operation.StartedAtUtc -or
        [string]$historicalOperation.CompletedAtUtc -cne [string]$Candidate.Operation.CompletedAtUtc -or
        [string]$historicalOperation.Registry -cne [string]$Candidate.Operation.Registry) {
        throw [System.InvalidOperationException]::new('Recovery evidence does not preserve historical publication lifecycle evidence.')
    }

    if ([string]$historicalPackage.MutationState -cne [string]$Candidate.Package.MutationState -or
        [string]$historicalPackage.StatusCode -cne [string]$Candidate.Package.StatusCode -or
        (Get-NuGetRecoveryDiagnosticSignature $historicalPackage.Diagnostic) -cne (Get-NuGetRecoveryDiagnosticSignature $Candidate.Package.Diagnostic)) {
        throw [System.InvalidOperationException]::new('Recovery evidence does not preserve the historical mutation classification.')
    }

    if ([string]$recovery.Trigger -cne [string]$Candidate.Trigger) {
        throw [System.InvalidOperationException]::new('Recovery evidence trigger does not match the historical recovery candidate.')
    }

    $state = [string]$recovery.RecoveryState
    if ($state -cnotin @('Converged','Conflict','Unresolved')) {
        throw [System.InvalidOperationException]::new('Recovery evidence must contain terminal Converged, Conflict, or Unresolved state.')
    }

    return $state
}

function Assert-NuGetPersistedCandidateMatchesLedger {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $Ledger,
        [Parameter(Mandatory)] [object] $Candidate,
        [Parameter(Mandatory)] [object] $PersistedPackage
    )

    if ([string]$Ledger.operationId -cne [string]$Candidate.Operation.OperationId -or
        [string]$Ledger.ledgerState -cne [string]$Candidate.Operation.LedgerState -or
        [string]$Ledger.operationConclusion -cne [string]$Candidate.Operation.OperationConclusion -or
        [string]$Ledger.startedAtUtc -cne [string]$Candidate.Operation.StartedAtUtc -or
        [string]$Ledger.completedAtUtc -cne [string]$Candidate.Operation.CompletedAtUtc -or
        [string]$Ledger.registry -cne [string]$Candidate.Operation.Registry) {
        throw [System.InvalidOperationException]::new('Recovery candidate operation evidence does not match the persisted publication ledger.')
    }

    if ([string]$PersistedPackage.id -cne [string]$Candidate.Package.Id -or
        [string]$PersistedPackage.version -cne [string]$Candidate.Package.Version -or
        [string]$PersistedPackage.admittedSha256 -cne [string]$Candidate.Package.AdmittedSha256 -or
        [string]$PersistedPackage.mutationState -cne [string]$Candidate.Package.MutationState -or
        [string]$PersistedPackage.statusCode -cne [string]$Candidate.Package.StatusCode -or
        (Get-NuGetRecoveryDiagnosticSignature $PersistedPackage.diagnostic) -cne (Get-NuGetRecoveryDiagnosticSignature $Candidate.Package.Diagnostic)) {
        throw [System.InvalidOperationException]::new('Recovery candidate package evidence does not match the persisted publication ledger.')
    }
}

function Get-NuGetWholeOperationContinuationDecision {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $LedgerPath,
        [Parameter(Mandatory)] [object] $Candidate,
        [Parameter(Mandatory)] [object] $RecoveryEvidence
    )

    if ([string]::IsNullOrWhiteSpace($LedgerPath)) {
        throw [System.ArgumentException]::new('LedgerPath is required.')
    }

    $recoveryState = Assert-NuGetRecoveryBindingMatchesCandidate -Candidate $Candidate -RecoveryEvidence $RecoveryEvidence
    $ledger = Read-NuGetPublicationLedger -LedgerPath $LedgerPath

    $packages = @($ledger.packages)
    $targetMatches = @()
    for ($i = 0; $i -lt $packages.Count; $i++) {
        $package = $packages[$i]
        if ([string]$package.id -ceq [string]$Candidate.Package.Id -and
            [string]$package.version -ceq [string]$Candidate.Package.Version -and
            [string]$package.admittedSha256 -ceq [string]$Candidate.Package.AdmittedSha256) {
            $targetMatches += $i
        }
    }

    if ($targetMatches.Count -ne 1) {
        throw [System.InvalidOperationException]::new('Recovery boundary was not uniquely identified in persisted package order.')
    }

    $targetIndex = [int]$targetMatches[0]
    $targetPackage = $packages[$targetIndex]
    Assert-NuGetPersistedCandidateMatchesLedger -Ledger $ledger -Candidate $Candidate -PersistedPackage $targetPackage

    $persistedTrigger = Get-NuGetRecoveryTriggerForHistoricalAttempt -PackageAttempt $targetPackage
    if ([string]$persistedTrigger -cne [string]$Candidate.Trigger) {
        throw [System.InvalidOperationException]::new('Persisted recovery boundary trigger does not match the recovery candidate.')
    }

    for ($i = 0; $i -lt $targetIndex; $i++) {
        if ([string]$packages[$i].mutationState -cne 'Accepted') {
            throw [System.InvalidOperationException]::new("Package at index $i precedes the recovery boundary but is not Accepted.")
        }
    }

    for ($i = $targetIndex + 1; $i -lt $packages.Count; $i++) {
        if ([string]$packages[$i].mutationState -cne 'NotAttempted') {
            throw [System.InvalidOperationException]::new("Package at index $i follows the recovery boundary but is not NotAttempted.")
        }
    }

    $hasNextPackage = $targetIndex -lt ($packages.Count - 1)
    $mayContinue = $recoveryState -ceq 'Converged' -and $hasNextPackage
    $nextPackageIndex = if ($mayContinue) { $targetIndex + 1 } else { $null }
    $nextPackage = if ($mayContinue) {
        $persistedNext = $packages[$nextPackageIndex]
        [pscustomobject]@{
            Id             = [string]$persistedNext.id
            Version        = [string]$persistedNext.version
            AdmittedSha256 = [string]$persistedNext.admittedSha256
            MutationState  = [string]$persistedNext.mutationState
        }
    } else { $null }

    $disposition = switch ($recoveryState) {
        'Converged' {
            if ($hasNextPackage) { 'ContinuationEligible' } else { 'RecoveredEnd' }
            break
        }
        'Conflict' { 'StoppedConflict'; break }
        'Unresolved' { 'StoppedUnresolved'; break }
        default { throw [System.InvalidOperationException]::new('Unsupported recovery state.') }
    }

    [pscustomobject]@{
        OperationId               = [string]$ledger.operationId
        RecoveryPackageIndex      = $targetIndex
        RecoveryPackage           = [pscustomobject]@{
            Id             = [string]$targetPackage.id
            Version        = [string]$targetPackage.version
            AdmittedSha256 = [string]$targetPackage.admittedSha256
            MutationState  = [string]$targetPackage.mutationState
            StatusCode     = $targetPackage.statusCode
            Diagnostic     = $targetPackage.diagnostic
        }
        RecoveryState             = $recoveryState
        MayContinue               = $mayContinue
        HasNextPackage            = $hasNextPackage
        NextPackageIndex          = $nextPackageIndex
        NextPackage               = $nextPackage
        WholeOperationDisposition = $disposition
    }
}
