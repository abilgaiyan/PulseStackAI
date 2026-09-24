Set-StrictMode -Version Latest

$script:NuGetRecoveryTriggerKinds = @(
    'Conflict409',
    'IndeterminateMutation',
    'StaleAttempting'
)

$script:NuGetRecoveryObservationStates = @(
    'Equivalent',
    'Different',
    'NotObservable',
    'Indeterminate'
)

function New-NuGetRecoveryTrigger {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Conflict409', 'IndeterminateMutation', 'StaleAttempting')]
        [string] $Kind
    )

    [pscustomobject]@{
        Kind = $Kind
    }
}

function Resolve-NuGetRecoveryObservation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $Trigger,
        [Parameter(Mandatory)] [object] $Observation
    )

    $triggerKindProperty = $Trigger.PSObject.Properties['Kind']
    if ($null -eq $triggerKindProperty -or
        [string]::IsNullOrWhiteSpace([string]$triggerKindProperty.Value) -or
        [string]$triggerKindProperty.Value -cnotin $script:NuGetRecoveryTriggerKinds) {
        throw [System.ArgumentException]::new('Recovery trigger must be Conflict409, IndeterminateMutation, or StaleAttempting.')
    }

    $observationStateProperty = $Observation.PSObject.Properties['State']
    if ($null -eq $observationStateProperty -or
        [string]::IsNullOrWhiteSpace([string]$observationStateProperty.Value) -or
        [string]$observationStateProperty.Value -cnotin $script:NuGetRecoveryObservationStates) {
        throw [System.ArgumentException]::new('Recovery observation must be Equivalent, Different, NotObservable, or Indeterminate.')
    }

    $triggerKind = [string]$triggerKindProperty.Value
    $observationState = [string]$observationStateProperty.Value

    $recoveryState = switch ($observationState) {
        'Equivalent'    { 'Converged' }
        'Different'     { 'Conflict' }
        'NotObservable' { 'Uncertain' }
        'Indeterminate' { 'Uncertain' }
        default { throw [System.InvalidOperationException]::new("Unsupported recovery observation '$observationState'.") }
    }

    [pscustomobject]@{
        Trigger       = $triggerKind
        Observation   = $observationState
        RecoveryState = $recoveryState
        Terminal      = $recoveryState -in @('Converged', 'Conflict')
    }
}
