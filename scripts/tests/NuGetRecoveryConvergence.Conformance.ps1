Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts/NuGetRecoveryConvergence.ps1')

function Assert-Eq($Expected, $Actual, [string] $Message) {
    if ($Expected -cne $Actual) {
        throw "$Message Expected '$Expected', actual '$Actual'."
    }
}

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}

function Invoke-Case([string] $Id, [string] $Name, [scriptblock] $Body) {
    try {
        & $Body
        [pscustomobject]@{ Id=$Id; Name=$Name; Outcome='PASS' }
    }
    catch {
        [pscustomobject]@{ Id=$Id; Name=$Name; Outcome='FAIL'; Error=$_.Exception.Message }
    }
}

function New-Observation([string] $State) {
    [pscustomobject]@{ State=$State }
}

function Assert-Transition([string] $TriggerKind, [string] $ObservationState, [string] $RecoveryState, [bool] $Terminal) {
    $trigger = New-NuGetRecoveryTrigger -Kind $TriggerKind
    $result = Resolve-NuGetRecoveryObservation -Trigger $trigger -Observation (New-Observation $ObservationState)

    Assert-Eq $TriggerKind $result.Trigger 'trigger'
    Assert-Eq $ObservationState $result.Observation 'observation'
    Assert-Eq $RecoveryState $result.RecoveryState 'recovery state'
    Assert-Eq $Terminal $result.Terminal 'terminal'
}

$results = @()

$results += Invoke-Case C01 'Conflict409 trigger is admitted' {
    $r = New-NuGetRecoveryTrigger -Kind Conflict409
    Assert-Eq Conflict409 $r.Kind 'kind'
}
$results += Invoke-Case C02 'IndeterminateMutation trigger is admitted' {
    $r = New-NuGetRecoveryTrigger -Kind IndeterminateMutation
    Assert-Eq IndeterminateMutation $r.Kind 'kind'
}
$results += Invoke-Case C03 'StaleAttempting trigger is admitted' {
    $r = New-NuGetRecoveryTrigger -Kind StaleAttempting
    Assert-Eq StaleAttempting $r.Kind 'kind'
}

$caseNumber = 4
foreach ($triggerKind in @('Conflict409', 'IndeterminateMutation', 'StaleAttempting')) {
    foreach ($transition in @(
        [pscustomobject]@{ Observation='Equivalent';    Recovery='Converged'; Terminal=$true  },
        [pscustomobject]@{ Observation='Different';     Recovery='Conflict';  Terminal=$true  },
        [pscustomobject]@{ Observation='NotObservable'; Recovery='Uncertain'; Terminal=$false },
        [pscustomobject]@{ Observation='Indeterminate'; Recovery='Uncertain'; Terminal=$false }
    )) {
        $id = 'C{0:d2}' -f $caseNumber
        $name = "$triggerKind + $($transition.Observation) => $($transition.Recovery)"
        $results += Invoke-Case $id $name {
            Assert-Transition -TriggerKind $triggerKind -ObservationState $transition.Observation -RecoveryState $transition.Recovery -Terminal $transition.Terminal
        }
        $caseNumber++
    }
}

$results += Invoke-Case C16 'invalid trigger is rejected' {
    $thrown = $false
    try { New-NuGetRecoveryTrigger -Kind 'UnknownTrigger' | Out-Null } catch { $thrown = $true }
    Assert-True $thrown 'invalid trigger was admitted'
}
$results += Invoke-Case C17 'invalid observation is rejected' {
    $trigger = New-NuGetRecoveryTrigger -Kind Conflict409
    $thrown = $false
    try { Resolve-NuGetRecoveryObservation -Trigger $trigger -Observation (New-Observation 'Absent') | Out-Null } catch { $thrown = $true }
    Assert-True $thrown 'unsupported observation was admitted'
}
$results += Invoke-Case C18 'transition is deterministic' {
    $trigger = New-NuGetRecoveryTrigger -Kind IndeterminateMutation
    $observation = New-Observation Indeterminate
    $a = Resolve-NuGetRecoveryObservation -Trigger $trigger -Observation $observation | ConvertTo-Json -Compress
    $b = Resolve-NuGetRecoveryObservation -Trigger $trigger -Observation $observation | ConvertTo-Json -Compress
    Assert-Eq $a $b 'deterministic result'
}
$results += Invoke-Case C19 'inputs are not rewritten' {
    $trigger = New-NuGetRecoveryTrigger -Kind StaleAttempting
    $observation = [pscustomobject]@{ State='Equivalent'; RemoteSha256='abc'; Diagnostic=$null }
    $triggerBefore = $trigger | ConvertTo-Json -Compress
    $observationBefore = $observation | ConvertTo-Json -Compress
    Resolve-NuGetRecoveryObservation -Trigger $trigger -Observation $observation | Out-Null
    Assert-Eq $triggerBefore ($trigger | ConvertTo-Json -Compress) 'trigger changed'
    Assert-Eq $observationBefore ($observation | ConvertTo-Json -Compress) 'observation changed'
}
$results += Invoke-Case C20 'RP-3C.1 exposes no publication instruction' {
    $trigger = New-NuGetRecoveryTrigger -Kind Conflict409
    foreach ($state in @('Equivalent', 'Different', 'NotObservable', 'Indeterminate')) {
        $r = Resolve-NuGetRecoveryObservation -Trigger $trigger -Observation (New-Observation $state)
        $propertyNames = @($r.PSObject.Properties.Name)
        Assert-True (@($propertyNames | Where-Object { $_ -match '(?i)publish|publication|mutat|retry|credential|apikey' }).Count -eq 0) "publication-capable output property exposed for $state"
    }

    $parameterNames = @((Get-Command Resolve-NuGetRecoveryObservation).Parameters.Keys)
    Assert-True (@($parameterNames | Where-Object { $_ -match '(?i)publish|publication|mutat|retry|credential|apikey|endpoint|request' }).Count -eq 0) 'publication-capable parameter seam exposed'
}
$results += Invoke-Case C21 'uncertainty does not synthesize Unresolved' {
    $trigger = New-NuGetRecoveryTrigger -Kind Conflict409
    foreach ($state in @('NotObservable', 'Indeterminate')) {
        $r = Resolve-NuGetRecoveryObservation -Trigger $trigger -Observation (New-Observation $state)
        Assert-Eq Uncertain $r.RecoveryState 'uncertain state'
        Assert-Eq $false $r.Terminal 'uncertainty terminal flag'
    }
}
$results += Invoke-Case C22 'algebra has no polling clock or deadline seam' {
    $parameterNames = @((Get-Command Resolve-NuGetRecoveryObservation).Parameters.Keys)
    foreach ($name in @('Clock', 'Deadline', 'PollInterval', 'Backoff', 'MaxPollCount')) {
        Assert-True (-not ($parameterNames -contains $name)) "$name seam exists"
    }
}

$results | Format-Table Id,Name,Outcome -AutoSize
$failed = @($results | Where-Object { $_.Outcome -ne 'PASS' })
if ($failed.Count -gt 0) {
    $failed | Format-List *
    throw "RP-3C.1 CONFORMANCE FAILED: $($failed.Count) case(s)."
}

"`nRP-3C.1 CONFORMANCE: $($results.Count) / $($results.Count) PASS"
