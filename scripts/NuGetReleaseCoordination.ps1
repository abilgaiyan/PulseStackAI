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

    $releaseIdentity = & $GetReleaseIdentity $AdmittedRelease
    if ($null -eq $releaseIdentity) { throw 'Release identity authority returned null.' }

    $effectiveRelease = & $GetEffectiveRelease $AdmittedRelease $releaseIdentity
    if ($null -eq $effectiveRelease) { throw 'Effective release authority returned null.' }

    $releaseStateProperty = $effectiveRelease.PSObject.Properties['ReleaseState']
    if ($null -eq $releaseStateProperty) {
        throw 'Effective release authority result must contain ReleaseState.'
    }
    $state = [string]$releaseStateProperty.Value

    switch ($state) {
        'ReleaseComplete' {
            return New-NuGetReleaseCoordinationResult -Disposition 'NoActionRequired' -Action 'None' -ReleaseIdentity $releaseIdentity -EffectiveRelease $effectiveRelease -OperationResult $null
        }
        'Blocked' {
            return New-NuGetReleaseCoordinationResult -Disposition 'Blocked' -Action 'None' -ReleaseIdentity $releaseIdentity -EffectiveRelease $effectiveRelease -OperationResult $null
        }
        'Indeterminate' {
            return New-NuGetReleaseCoordinationResult -Disposition 'Indeterminate' -Action 'None' -ReleaseIdentity $releaseIdentity -EffectiveRelease $effectiveRelease -OperationResult $null
        }
        'NotStarted' {
            $preflight = & $GetInitialPreflight $AdmittedRelease $releaseIdentity
            if ($null -eq $preflight) { throw 'Initial preflight authority returned null.' }

            $preflightStateProperty = $preflight.PSObject.Properties['State']
            if ($null -eq $preflightStateProperty) {
                throw 'Initial preflight authority result must contain State.'
            }
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
            if ($null -eq $admissionStateProperty) {
                throw 'Continuation admission authority result must contain State.'
            }
            $admissionState = [string]$admissionStateProperty.Value

            if ($admissionState -ne 'Admissible') {
                $disposition = if ($admissionState -eq 'Indeterminate') { 'Indeterminate' } else { 'Blocked' }
                return New-NuGetReleaseCoordinationResult -Disposition $disposition -Action 'None' -ReleaseIdentity $releaseIdentity -EffectiveRelease $effectiveRelease -OperationResult $admission
            }

            $continuationResult = & $InvokeContinuation $AdmittedRelease $releaseIdentity $effectiveRelease $selection $admission
            if ($null -eq $continuationResult) { throw 'Continuation authority returned null.' }

            return New-NuGetReleaseCoordinationResult -Disposition 'Progressed' -Action 'Continuation' -ReleaseIdentity $releaseIdentity -EffectiveRelease $effectiveRelease -OperationResult $continuationResult
        }
        default {
            throw "Unsupported effective release state '$state'."
        }
    }
}
