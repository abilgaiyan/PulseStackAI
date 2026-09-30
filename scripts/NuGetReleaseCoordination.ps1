Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-NuGetReleaseCoordinationResult {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('NoActionRequired', 'Progressed', 'Blocked', 'Indeterminate')]
        [string]$Disposition,

        [Parameter(Mandatory)]
        [ValidateSet('None', 'InitialPublication', 'Recovery', 'Continuation')]
        [string]$Action,

        [Parameter(Mandatory)]
        [object]$ReleaseIdentity,

        [AllowNull()]
        [object]$EffectiveRelease,

        [AllowNull()]
        [object]$OperationResult
    )

    [pscustomobject][ordered]@{
        Disposition     = $Disposition
        ReleaseIdentity = $ReleaseIdentity
        EffectiveRelease = $EffectiveRelease
        Action          = $Action
        OperationResult = $OperationResult
    }
}

function Invoke-NuGetReleaseCoordination {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object]$AdmittedRelease,

        [Parameter(Mandatory)]
        [scriptblock]$GetReleaseIdentity,

        [Parameter(Mandatory)]
        [scriptblock]$GetEffectiveRelease,

        [Parameter(Mandatory)]
        [scriptblock]$GetInitialPreflight,

        [Parameter(Mandatory)]
        [scriptblock]$InvokeInitialPublication
    )

    # RP-7 is intentionally ephemeral. It coordinates existing authorities and
    # creates no coordinator ledger, operation identity, or durable state.
    $releaseIdentity = & $GetReleaseIdentity $AdmittedRelease
    if ($null -eq $releaseIdentity) {
        throw 'Release identity authority returned null.'
    }

    $effectiveRelease = & $GetEffectiveRelease $AdmittedRelease $releaseIdentity
    if ($null -eq $effectiveRelease) {
        throw 'Effective release authority returned null.'
    }

    $state = [string]$effectiveRelease.State

    switch ($state) {
        'Complete' {
            return New-NuGetReleaseCoordinationResult `
                -Disposition 'NoActionRequired' `
                -Action 'None' `
                -ReleaseIdentity $releaseIdentity `
                -EffectiveRelease $effectiveRelease `
                -OperationResult $null
        }

        'Blocked' {
            return New-NuGetReleaseCoordinationResult `
                -Disposition 'Blocked' `
                -Action 'None' `
                -ReleaseIdentity $releaseIdentity `
                -EffectiveRelease $effectiveRelease `
                -OperationResult $null
        }

        'Indeterminate' {
            return New-NuGetReleaseCoordinationResult `
                -Disposition 'Indeterminate' `
                -Action 'None' `
                -ReleaseIdentity $releaseIdentity `
                -EffectiveRelease $effectiveRelease `
                -OperationResult $null
        }

        'NotStarted' {
            $preflight = & $GetInitialPreflight $AdmittedRelease $releaseIdentity
            if ($null -eq $preflight) {
                throw 'Initial preflight authority returned null.'
            }

            if ([string]$preflight.Outcome -ne 'AllAbsent') {
                $disposition = if ([string]$preflight.Outcome -eq 'Indeterminate') {
                    'Indeterminate'
                }
                else {
                    'Blocked'
                }

                return New-NuGetReleaseCoordinationResult `
                    -Disposition $disposition `
                    -Action 'None' `
                    -ReleaseIdentity $releaseIdentity `
                    -EffectiveRelease $effectiveRelease `
                    -OperationResult $preflight
            }

            # One coordinator invocation may invoke at most one mutation
            # operation. RP-3 owns the package-level mutation scope of this
            # initial publication operation; RP-7 does not enlarge it and does
            # not launch continuation after it returns.
            $publicationResult = & $InvokeInitialPublication $AdmittedRelease $releaseIdentity $preflight
            if ($null -eq $publicationResult) {
                throw 'Initial publication authority returned null.'
            }

            return New-NuGetReleaseCoordinationResult `
                -Disposition 'Progressed' `
                -Action 'InitialPublication' `
                -ReleaseIdentity $releaseIdentity `
                -EffectiveRelease $effectiveRelease `
                -OperationResult $publicationResult
        }

        default {
            throw "RP-7C foundation does not yet route effective release state '$state'."
        }
    }
}
