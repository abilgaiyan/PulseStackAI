Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'NuGetRecoveryConvergence.ps1')

function Invoke-NuGetRecoveryPolling {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $Trigger,
        [Parameter(Mandatory)] [scriptblock] $Observe,
        [Parameter(Mandatory)] [TimeSpan] $RecoveryWindow,
        [Parameter(Mandatory)] [TimeSpan] $PollInterval,
        [scriptblock] $Clock = { [DateTimeOffset]::UtcNow },
        [scriptblock] $IsCancellationRequested = { $false },
        [scriptblock] $Wait = { param([TimeSpan] $Duration, [scriptblock] $CancellationProbe) Start-Sleep -Milliseconds $Duration.TotalMilliseconds }
    )

    if ($RecoveryWindow -le [TimeSpan]::Zero) {
        throw [System.ArgumentOutOfRangeException]::new('RecoveryWindow', 'RecoveryWindow must be greater than zero.')
    }
    if ($PollInterval -le [TimeSpan]::Zero) {
        throw [System.ArgumentOutOfRangeException]::new('PollInterval', 'PollInterval must be greater than zero.')
    }

    $startedAt = ([DateTimeOffset](& $Clock)).ToUniversalTime()
    $deadlineUtc = $startedAt + $RecoveryWindow
    $observationCount = 0
    $lastObservation = $null

    while ($true) {
        if (& $IsCancellationRequested) {
            throw [System.OperationCanceledException]::new('Recovery operation was canceled before observation admission.')
        }

        $admissionTime = ([DateTimeOffset](& $Clock)).ToUniversalTime()
        if ($admissionTime -ge $deadlineUtc) {
            return [pscustomobject]@{
                RecoveryState    = 'Unresolved'
                Terminal         = $true
                StartedAtUtc     = $startedAt
                DeadlineUtc      = $deadlineUtc
                ObservationCount = $observationCount
                LastObservation  = $lastObservation
            }
        }

        $lastObservation = & $Observe
        $observationCount++
        $resolution = Resolve-NuGetRecoveryObservation -Trigger $Trigger -Observation $lastObservation

        if ($resolution.RecoveryState -eq 'Converged' -or $resolution.RecoveryState -eq 'Conflict') {
            return [pscustomobject]@{
                RecoveryState    = $resolution.RecoveryState
                Terminal         = $true
                StartedAtUtc     = $startedAt
                DeadlineUtc      = $deadlineUtc
                ObservationCount = $observationCount
                LastObservation  = $lastObservation
            }
        }

        if (& $IsCancellationRequested) {
            throw [System.OperationCanceledException]::new('Recovery operation was canceled after an uncertain observation.')
        }

        $continuationTime = ([DateTimeOffset](& $Clock)).ToUniversalTime()
        if ($continuationTime -ge $deadlineUtc) {
            return [pscustomobject]@{
                RecoveryState    = 'Unresolved'
                Terminal         = $true
                StartedAtUtc     = $startedAt
                DeadlineUtc      = $deadlineUtc
                ObservationCount = $observationCount
                LastObservation  = $lastObservation
            }
        }

        $remaining = $deadlineUtc - $continuationTime
        $waitDuration = if ($PollInterval -lt $remaining) { $PollInterval } else { $remaining }

        if (& $IsCancellationRequested) {
            throw [System.OperationCanceledException]::new('Recovery operation was canceled before waiting.')
        }

        & $Wait $waitDuration $IsCancellationRequested
    }
}
