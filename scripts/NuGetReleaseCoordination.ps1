Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-NuGetReleaseCoordinationResult {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('NoActionRequired','Progressed','Blocked','Indeterminate')][string]$Disposition,
        [Parameter(Mandatory)][ValidateSet('None','InitialPublication','Recovery','Continuation')][string]$Action,
        [Parameter(Mandatory)][object]$ReleaseIdentity,
        [AllowNull()][object]$EffectiveRelease,
        [AllowNull()][object]$OperationResult
    )

    [pscustomobject][ordered]@{
        Disposition      = $Disposition
        ReleaseIdentity  = $ReleaseIdentity
        EffectiveRelease = $EffectiveRelease
        Action           = $Action
        OperationResult  = $OperationResult
    }
}

function Get-NuGetCoordinationReleaseState {
    param([Parameter(Mandatory)][object]$EffectiveRelease)
    $property = $EffectiveRelease.PSObject.Properties['ReleaseState']
    if ($null -eq $property) { throw 'Effective release authority result must contain ReleaseState.' }
    return [string]$property.Value
}

function Invoke-NuGetReleaseCoordination {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$AdmittedRelease,
        [Parameter(Mandatory)][scriptblock]$GetReleaseIdentity,
        [Parameter(Mandatory)][scriptblock]$GetEffectiveRelease,
        [Parameter(Mandatory)][scriptblock]$GetInitialPreflight,
        [Parameter(Mandatory)][scriptblock]$InvokeInitialPublication,
        [scriptblock]$RecoverRelease,
        [scriptblock]$ReprojectEffectiveRelease,
        [scriptblock]$SelectContinuation,
        [scriptblock]$AdmitContinuation,
        [scriptblock]$InvokeContinuation
    )

    # RP-7 is ephemeral coordination only. Existing RP authorities own all
    # durable evidence, release truth, admission, and mutation authority.
    $releaseIdentity = & $GetReleaseIdentity $AdmittedRelease
    if ($null -eq $releaseIdentity) { throw 'Release identity authority returned null.' }

    $effectiveRelease = & $GetEffectiveRelease $AdmittedRelease $releaseIdentity
    if ($null -eq $effectiveRelease) { throw 'Effective release authority returned null.' }
    $state = Get-NuGetCoordinationReleaseState -EffectiveRelease $effectiveRelease

    $recoveryResult = $null
    $recoveryPerformed = $false

    if ($state -eq 'Indeterminate' -and $null -ne $RecoverRelease) {
        if ($null -eq $ReprojectEffectiveRelease) {
            throw 'Recovery coordination requires an effective-release reprojection authority.'
        }

        # Recovery may establish new evidence but must not replay publication.
        $recoveryResult = & $RecoverRelease $AdmittedRelease $releaseIdentity $effectiveRelease
        if ($null -eq $recoveryResult) { throw 'Recovery authority returned null.' }
        $recoveryPerformed = $true

        # RP-5 remains release-state authority after recovery. The coordinator
        # does not translate recovery state directly into release truth.
        $effectiveRelease = & $ReprojectEffectiveRelease $AdmittedRelease $releaseIdentity $recoveryResult
        if ($null -eq $effectiveRelease) { throw 'Effective release reprojection authority returned null.' }
        $state = Get-NuGetCoordinationReleaseState -EffectiveRelease $effectiveRelease
    }

    switch ($state) {
        'ReleaseComplete' {
            return New-NuGetReleaseCoordinationResult -Disposition 'NoActionRequired' -Action $(if ($recoveryPerformed) {'Recovery'} else {'None'}) -ReleaseIdentity $releaseIdentity -EffectiveRelease $effectiveRelease -OperationResult $recoveryResult
        }
        'Blocked' {
            return New-NuGetReleaseCoordinationResult -Disposition 'Blocked' -Action $(if ($recoveryPerformed) {'Recovery'} else {'None'}) -ReleaseIdentity $releaseIdentity -EffectiveRelease $effectiveRelease -OperationResult $recoveryResult
        }
        'Indeterminate' {
            return New-NuGetReleaseCoordinationResult -Disposition 'Indeterminate' -Action $(if ($recoveryPerformed) {'Recovery'} else {'None'}) -ReleaseIdentity $releaseIdentity -EffectiveRelease $effectiveRelease -OperationResult $recoveryResult
        }
        'NotStarted' {
            if ($recoveryPerformed) {
                throw 'Recovery reprojection cannot authorize initial publication.'
            }

            $preflight = & $GetInitialPreflight $AdmittedRelease $releaseIdentity
            if ($null -eq $preflight) { throw 'Initial preflight authority returned null.' }
            $preflightStateProperty = $preflight.PSObject.Properties['State']
            if ($null -eq $preflightStateProperty) { throw 'Initial preflight authority result must contain State.' }
            $preflightState = [string]$preflightStateProperty.Value

            if ($preflightState -ne 'AllAbsent') {
                $disposition = if ($preflightState -eq 'Indeterminate') { 'Indeterminate' } else { 'Blocked' }
                return New-NuGetReleaseCoordinationResult -Disposition $disposition -Action 'None' -ReleaseIdentity $releaseIdentity -EffectiveRelease $effectiveRelease -OperationResult $preflight
            }

            $publicationResult = & $InvokeInitialPublication $AdmittedRelease $releaseIdentity $preflight
            if ($null -eq $publicationResult) { throw 'Initial publication authority returned null.' }
            return New-NuGetReleaseCoordinationResult -Disposition 'Progressed' -Action 'InitialPublication' -ReleaseIdentity $releaseIdentity -EffectiveRelease $effectiveRelease -OperationResult $publicationResult
        }
        'ContinuationEligible' {
            if ($null -eq $SelectContinuation -or $null -eq $AdmitContinuation -or $null -eq $InvokeContinuation) {
                throw 'Continuation coordination authorities are required for a continuation-eligible release.'
            }

            $selection = & $SelectContinuation $AdmittedRelease $releaseIdentity $effectiveRelease
            if ($null -eq $selection) { throw 'Continuation selection authority returned null.' }

            $admission = & $AdmitContinuation $AdmittedRelease $releaseIdentity $effectiveRelease $selection
            if ($null -eq $admission) { throw 'Continuation admission authority returned null.' }
            $admissionStateProperty = $admission.PSObject.Properties['State']
            if ($null -eq $admissionStateProperty) { throw 'Continuation admission authority result must contain State.' }
            $admissionState = [string]$admissionStateProperty.Value

            if ($admissionState -ne 'Admissible') {
                $disposition = if ($admissionState -eq 'Indeterminate') { 'Indeterminate' } else { 'Blocked' }
                return New-NuGetReleaseCoordinationResult -Disposition $disposition -Action $(if ($recoveryPerformed) {'Recovery'} else {'None'}) -ReleaseIdentity $releaseIdentity -EffectiveRelease $effectiveRelease -OperationResult $(if ($recoveryPerformed) {[pscustomobject]@{Recovery=$recoveryResult;Admission=$admission}} else {$admission})
            }

            # Recovery is evidence convergence, not a package mutation. After
            # reprojection it may precede exactly one freshly admitted RP-6
            # continuation operation. No second successor is selected here.
            $continuationResult = & $InvokeContinuation $AdmittedRelease $releaseIdentity $effectiveRelease $selection $admission
            if ($null -eq $continuationResult) { throw 'Continuation authority returned null.' }

            $operationResult = if ($recoveryPerformed) {
                [pscustomobject][ordered]@{ Recovery=$recoveryResult; Continuation=$continuationResult }
            } else { $continuationResult }

            return New-NuGetReleaseCoordinationResult -Disposition 'Progressed' -Action 'Continuation' -ReleaseIdentity $releaseIdentity -EffectiveRelease $effectiveRelease -OperationResult $operationResult
        }
        default {
            throw "Unsupported effective release state '$state'."
        }
    }
}
