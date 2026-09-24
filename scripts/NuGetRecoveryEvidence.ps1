Set-StrictMode -Version Latest

function Read-NuGetPublicationLedger {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $LedgerPath
    )

    if ([string]::IsNullOrWhiteSpace($LedgerPath)) {
        throw [System.ArgumentException]::new('LedgerPath is required.')
    }
    if (-not (Test-Path -LiteralPath $LedgerPath -PathType Leaf)) {
        throw [System.IO.FileNotFoundException]::new('Publication ledger was not found.', $LedgerPath)
    }

    try {
        $raw = [System.IO.File]::ReadAllText($LedgerPath, [System.Text.Encoding]::UTF8)
        $ledger = $raw | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        throw [System.InvalidOperationException]::new('Publication ledger is not valid JSON evidence.', $_.Exception)
    }

    foreach ($name in @('schemaVersion','operationId','ledgerState','registry','packages')) {
        if ($null -eq $ledger.PSObject.Properties[$name]) {
            throw [System.InvalidOperationException]::new("Publication ledger is missing required field '$name'.")
        }
    }

    if (@($ledger.packages).Count -eq 0) {
        throw [System.InvalidOperationException]::new('Publication ledger contains no package attempts.')
    }

    $ledger
}

function Get-NuGetRecoveryTriggerForHistoricalAttempt {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $PackageAttempt
    )

    $state = [string]$PackageAttempt.mutationState
    switch ($state) {
        'Rejected' {
            $diagnosticCode = if ($null -ne $PackageAttempt.diagnostic) { [string]$PackageAttempt.diagnostic.Code } else { $null }
            if ([int]$PackageAttempt.statusCode -eq 409 -and $diagnosticCode -ceq 'ExistingIdentityConflict') {
                return 'Conflict409'
            }
        }
        'Indeterminate' {
            return 'IndeterminateMutation'
        }
        'Attempting' {
            return 'StaleAttempting'
        }
    }

    throw [System.InvalidOperationException]::new("Publication package attempt in state '$state' is not eligible for RP-3C recovery.")
}

function Get-NuGetRecoveryCandidate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $LedgerPath,
        [Parameter(Mandatory)] [string] $OperationId,
        [Parameter(Mandatory)] [string] $PackageId,
        [Parameter(Mandatory)] [string] $PackageVersion,
        [Parameter(Mandatory)] [string] $AdmittedSha256
    )

    if ([string]::IsNullOrWhiteSpace($OperationId) -or
        [string]::IsNullOrWhiteSpace($PackageId) -or
        [string]::IsNullOrWhiteSpace($PackageVersion) -or
        [string]::IsNullOrWhiteSpace($AdmittedSha256)) {
        throw [System.ArgumentException]::new('OperationId, PackageId, PackageVersion, and AdmittedSha256 are required.')
    }

    $ledger = Read-NuGetPublicationLedger -LedgerPath $LedgerPath
    if ([string]$ledger.operationId -cne $OperationId) {
        throw [System.InvalidOperationException]::new('Requested operation does not match persisted publication evidence.')
    }

    $matches = @($ledger.packages | Where-Object {
        [string]$_.id -ceq $PackageId -and
        [string]$_.version -ceq $PackageVersion -and
        [string]$_.admittedSha256 -ceq $AdmittedSha256
    })

    if ($matches.Count -ne 1) {
        throw [System.InvalidOperationException]::new('Exact persisted package attempt was not uniquely identified.')
    }

    $package = $matches[0]
    $trigger = Get-NuGetRecoveryTriggerForHistoricalAttempt -PackageAttempt $package
    $diagnostic = if ($null -ne $package.diagnostic) {
        [pscustomobject]@{
            Code       = [string]$package.diagnostic.Code
            Message    = [string]$package.diagnostic.Message
            StatusCode = $package.diagnostic.StatusCode
        }
    } else { $null }

    [pscustomobject]@{
        LedgerPath = $LedgerPath
        Operation = [pscustomobject]@{
            OperationId         = [string]$ledger.operationId
            LedgerState         = [string]$ledger.ledgerState
            OperationConclusion = $ledger.operationConclusion
            StartedAtUtc        = $ledger.startedAtUtc
            CompletedAtUtc      = $ledger.completedAtUtc
            Registry            = [string]$ledger.registry
        }
        Package = [pscustomobject]@{
            Id             = [string]$package.id
            Version        = [string]$package.version
            AdmittedSha256 = [string]$package.admittedSha256
            MutationState  = [string]$package.mutationState
            StatusCode     = $package.statusCode
            Diagnostic     = $diagnostic
        }
        Trigger = $trigger
    }
}

function New-NuGetRecoveryEvidenceBinding {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $Candidate,
        [Parameter(Mandatory)] [object] $RecoveryResult,
        [object[]] $Observations = @()
    )

    $trigger = [string]$Candidate.Trigger
    if ($trigger -cnotin @('Conflict409','IndeterminateMutation','StaleAttempting')) {
        throw [System.InvalidOperationException]::new('Candidate trigger is not an RP-3C historical recovery trigger.')
    }

    $recoveryState = [string]$RecoveryResult.RecoveryState
    if ($recoveryState -cnotin @('Converged','Conflict','Unresolved')) {
        throw [System.InvalidOperationException]::new('RecoveryResult must be terminal Converged, Conflict, or Unresolved.')
    }

    if ($RecoveryResult.PSObject.Properties['Terminal'] -and -not [bool]$RecoveryResult.Terminal) {
        throw [System.InvalidOperationException]::new('RecoveryResult must be terminal.')
    }

    [pscustomobject]@{
        HistoricalPublication = [pscustomobject]@{
            Operation = [pscustomobject]@{
                OperationId         = [string]$Candidate.Operation.OperationId
                LedgerState         = [string]$Candidate.Operation.LedgerState
                OperationConclusion = $Candidate.Operation.OperationConclusion
                StartedAtUtc        = $Candidate.Operation.StartedAtUtc
                CompletedAtUtc      = $Candidate.Operation.CompletedAtUtc
                Registry            = [string]$Candidate.Operation.Registry
            }
            Package = [pscustomobject]@{
                Id             = [string]$Candidate.Package.Id
                Version        = [string]$Candidate.Package.Version
                AdmittedSha256 = [string]$Candidate.Package.AdmittedSha256
                MutationState  = [string]$Candidate.Package.MutationState
                StatusCode     = $Candidate.Package.StatusCode
                Diagnostic     = $Candidate.Package.Diagnostic
            }
        }
        Recovery = [pscustomobject]@{
            Trigger          = $trigger
            RecoveryState    = $recoveryState
            StartedAtUtc     = $RecoveryResult.StartedAtUtc
            DeadlineUtc      = $RecoveryResult.DeadlineUtc
            ObservationCount = $RecoveryResult.ObservationCount
            LastObservation  = $RecoveryResult.LastObservation
            Observations     = @($Observations)
        }
    }
}
