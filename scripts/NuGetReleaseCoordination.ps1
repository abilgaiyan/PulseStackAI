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
        Disposition      = $Disposition
        ReleaseIdentity  = $ReleaseIdentity
        EffectiveRelease = $EffectiveRelease
        Action           = $Action
        OperationResult  = $OperationResult
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
        [scriptblock]$InvokeInitialPublication,

        [scriptblock]$SelectContinuation,

        [scriptblock]$AdmitContinuation,

        [scriptblock]$InvokeContinuation
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

        'ContinuationEligible' {
            if ($null -eq $SelectContinuation -or $null -eq $AdmitContinuation -or $null -eq $InvokeContinuation) {
                throw 'Continuation coordination authorities are required for a continuation-eligible release.'
            }

            # Selection is read-only and carries no mutation authority.
            $selection = & $SelectContinuation $AdmittedRelease $releaseIdentity $effectiveRelease
            if ($null -eq $selection) {
                throw 'Continuation selection authority returned null.'
            }

            # Admission must be fresh for the selected successor. The admission
            # authority decides whether an exact one-position grant exists.
            $admission = & $AdmitContinuation $AdmittedRelease $releaseIdentity $effectiveRelease $selection
            if ($null -eq $admission) {
                throw 'Continuation admission authority returned null.'
            }

            $admissionOutcome = [string]$admission.Outcome
            if ($admissionOutcome -ne 'Admitted') {
                $disposition = if ($admissionOutcome -eq 'Indeterminate') {
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
                    -OperationResult $admission
            }

            # RP-6 owns the exact-successor mutation scope. RP-7 invokes the
            # continuation authority once and returns immediately; it never
            # selects or publishes a second successor in the same invocation.
            $continuationResult = & $InvokeContinuation $AdmittedRelease $releaseIdentity $effectiveRelease $selection $admission
            if ($null -eq $continuationResult) {
                throw 'Continuation authority returned null.'
            }

            return New-NuGetReleaseCoordinationResult `
                -Disposition 'Progressed' `
                -Action 'Continuation' `
                -ReleaseIdentity $releaseIdentity `
                -EffectiveRelease $effectiveRelease `
                -OperationResult $continuationResult
        }

        default {
            throw "Unsupported effective release state '$state'."
        }
    }
}
