Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts/NuGetRecoveryPolling.ps1')

function Assert-Eq($Expected, $Actual, [string] $Message) {
    if ($Expected -cne $Actual) { throw "$Message Expected '$Expected', actual '$Actual'." }
}
function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}
function Invoke-Case([string] $Id, [string] $Name, [scriptblock] $Body) {
    try { & $Body; [pscustomobject]@{ Id=$Id; Name=$Name; Outcome='PASS' } }
    catch { [pscustomobject]@{ Id=$Id; Name=$Name; Outcome='FAIL'; Error=$_.Exception.Message } }
}
function New-Trigger([string] $Kind='Conflict409') {
    New-NuGetRecoveryTrigger -Kind $Kind
}
function New-Observation([string] $State) {
    [pscustomobject]@{ State=$State }
}

$epoch = [DateTimeOffset]::Parse('2026-09-24T00:00:00Z')
$results = @()

$results += Invoke-Case P01 'Equivalent before deadline converges immediately' {
    $state=[pscustomobject]@{Now=$epoch;Observations=0;Waits=0}
    $clock={ $state.Now }.GetNewClosure()
    $observe={ $state.Observations++; New-Observation Equivalent }.GetNewClosure()
    $wait={ param($d,$c) $state.Waits++; $state.Now=$state.Now+$d }.GetNewClosure()
    $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(30)) -PollInterval ([TimeSpan]::FromSeconds(5)) -Clock $clock -Wait $wait
    Assert-Eq Converged $r.RecoveryState 'state';Assert-Eq 1 $r.ObservationCount 'observations';Assert-Eq 0 $state.Waits 'waits'
}
$results += Invoke-Case P02 'Different before deadline conflicts immediately' {
    $state=[pscustomobject]@{Now=$epoch;Observations=0;Waits=0}
    $clock={ $state.Now }.GetNewClosure()
    $observe={ $state.Observations++; New-Observation Different }.GetNewClosure()
    $wait={ param($d,$c) $state.Waits++; $state.Now=$state.Now+$d }.GetNewClosure()
    $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(30)) -PollInterval ([TimeSpan]::FromSeconds(5)) -Clock $clock -Wait $wait
    Assert-Eq Conflict $r.RecoveryState 'state';Assert-Eq 1 $r.ObservationCount 'observations';Assert-Eq 0 $state.Waits 'waits'
}
$results += Invoke-Case P03 'NotObservable then Equivalent converges' {
    $state=[pscustomobject]@{Now=$epoch;Index=0;Waits=0}
    $sequence=@('NotObservable','Equivalent')
    $clock={ $state.Now }.GetNewClosure()
    $observe={ $value=$sequence[$state.Index];$state.Index++;New-Observation $value }.GetNewClosure()
    $wait={ param($d,$c) $state.Waits++;$state.Now=$state.Now+$d }.GetNewClosure()
    $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(30)) -PollInterval ([TimeSpan]::FromSeconds(5)) -Clock $clock -Wait $wait
    Assert-Eq Converged $r.RecoveryState 'state';Assert-Eq 2 $r.ObservationCount 'observations';Assert-Eq 1 $state.Waits 'waits'
}
$results += Invoke-Case P04 'Indeterminate then Different conflicts' {
    $state=[pscustomobject]@{Now=$epoch;Index=0;Waits=0}
    $sequence=@('Indeterminate','Different')
    $clock={ $state.Now }.GetNewClosure()
    $observe={ $value=$sequence[$state.Index];$state.Index++;New-Observation $value }.GetNewClosure()
    $wait={ param($d,$c) $state.Waits++;$state.Now=$state.Now+$d }.GetNewClosure()
    $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger -Kind IndeterminateMutation) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(30)) -PollInterval ([TimeSpan]::FromSeconds(5)) -Clock $clock -Wait $wait
    Assert-Eq Conflict $r.RecoveryState 'state';Assert-Eq 2 $r.ObservationCount 'observations';Assert-Eq 1 $state.Waits 'waits'
}
$results += Invoke-Case P05 'continued uncertainty reaches Unresolved' {
    $state=[pscustomobject]@{Now=$epoch;Observations=0;Waits=0}
    $clock={ $state.Now }.GetNewClosure()
    $observe={ $state.Observations++;New-Observation NotObservable }.GetNewClosure()
    $wait={ param($d,$c) $state.Waits++;$state.Now=$state.Now+$d }.GetNewClosure()
    $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(10)) -PollInterval ([TimeSpan]::FromSeconds(4)) -Clock $clock -Wait $wait
    Assert-Eq Unresolved $r.RecoveryState 'state';Assert-Eq 3 $r.ObservationCount 'observations';Assert-Eq 3 $state.Waits 'waits';Assert-Eq ($epoch.AddSeconds(10)) $r.DeadlineUtc 'deadline'
}
$results += Invoke-Case P06 'deadline blocks next observation admission' {
    $state=[pscustomobject]@{Now=$epoch;Observations=0}
    $clock={ $state.Now }.GetNewClosure()
    $observe={ $state.Observations++;New-Observation Equivalent }.GetNewClosure()
    $wait={ param($d,$c) $state.Now=$state.Now+$d }.GetNewClosure()
    $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromTicks(1)) -PollInterval ([TimeSpan]::FromSeconds(1)) -Clock $clock -Wait $wait
    Assert-Eq Unresolved $r.RecoveryState 'state';Assert-Eq 0 $state.Observations 'observations'
}
$results += Invoke-Case P07 'admitted Equivalent remains decisive after deadline crossing' {
    $state=[pscustomobject]@{Now=$epoch}
    $clock={ $state.Now }.GetNewClosure()
    $observe={ $state.Now=$state.Now.AddSeconds(20);New-Observation Equivalent }.GetNewClosure()
    $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(10)) -PollInterval ([TimeSpan]::FromSeconds(2)) -Clock $clock
    Assert-Eq Converged $r.RecoveryState 'state';Assert-Eq 1 $r.ObservationCount 'observations'
}
$results += Invoke-Case P08 'admitted Different remains decisive after deadline crossing' {
    $state=[pscustomobject]@{Now=$epoch}
    $clock={ $state.Now }.GetNewClosure()
    $observe={ $state.Now=$state.Now.AddSeconds(20);New-Observation Different }.GetNewClosure()
    $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(10)) -PollInterval ([TimeSpan]::FromSeconds(2)) -Clock $clock
    Assert-Eq Conflict $r.RecoveryState 'state';Assert-Eq 1 $r.ObservationCount 'observations'
}
$results += Invoke-Case P09 'uncertain observation crossing deadline becomes Unresolved' {
    $state=[pscustomobject]@{Now=$epoch;Waits=0}
    $clock={ $state.Now }.GetNewClosure()
    $observe={ $state.Now=$state.Now.AddSeconds(20);New-Observation Indeterminate }.GetNewClosure()
    $wait={ param($d,$c) $state.Waits++ }.GetNewClosure()
    $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(10)) -PollInterval ([TimeSpan]::FromSeconds(2)) -Clock $clock -Wait $wait
    Assert-Eq Unresolved $r.RecoveryState 'state';Assert-Eq 1 $r.ObservationCount 'observations';Assert-Eq 0 $state.Waits 'waits'
}
$results += Invoke-Case P10 'cancellation before admission prevents observation' {
    $state=[pscustomobject]@{Now=$epoch;Observations=0}
    $clock={ $state.Now }.GetNewClosure()
    $cancel={ $true }
    $observe={ $state.Observations++;New-Observation Equivalent }.GetNewClosure()
    $thrown=$false
    try { Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(10)) -PollInterval ([TimeSpan]::FromSeconds(1)) -Clock $clock -IsCancellationRequested $cancel | Out-Null } catch [System.OperationCanceledException] { $thrown=$true }
    Assert-True $thrown 'cancellation not surfaced';Assert-Eq 0 $state.Observations 'observations'
}
$results += Invoke-Case P11 'cancellation after uncertainty wins before wait' {
    $state=[pscustomobject]@{Now=$epoch;Canceled=$false;Waits=0}
    $clock={ $state.Now }.GetNewClosure()
    $cancel={ $state.Canceled }.GetNewClosure()
    $observe={ $state.Canceled=$true;New-Observation NotObservable }.GetNewClosure()
    $wait={ param($d,$c) $state.Waits++ }.GetNewClosure()
    $thrown=$false
    try { Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(10)) -PollInterval ([TimeSpan]::FromSeconds(1)) -Clock $clock -IsCancellationRequested $cancel -Wait $wait | Out-Null } catch [System.OperationCanceledException] { $thrown=$true }
    Assert-True $thrown 'cancellation not surfaced';Assert-Eq 0 $state.Waits 'waits'
}
$results += Invoke-Case P12 'cancellation during decisive observation does not erase Converged' {
    $state=[pscustomobject]@{Now=$epoch;Canceled=$false}
    $clock={ $state.Now }.GetNewClosure()
    $cancel={ $state.Canceled }.GetNewClosure()
    $observe={ $state.Canceled=$true;New-Observation Equivalent }.GetNewClosure()
    $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(10)) -PollInterval ([TimeSpan]::FromSeconds(1)) -Clock $clock -IsCancellationRequested $cancel
    Assert-Eq Converged $r.RecoveryState 'state'
}
$results += Invoke-Case P13 'cancellation during decisive observation does not erase Conflict' {
    $state=[pscustomobject]@{Now=$epoch;Canceled=$false}
    $clock={ $state.Now }.GetNewClosure()
    $cancel={ $state.Canceled }.GetNewClosure()
    $observe={ $state.Canceled=$true;New-Observation Different }.GetNewClosure()
    $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(10)) -PollInterval ([TimeSpan]::FromSeconds(1)) -Clock $clock -IsCancellationRequested $cancel
    Assert-Eq Conflict $r.RecoveryState 'state'
}
$results += Invoke-Case P14 'wait is bounded by remaining deadline budget' {
    $state=[pscustomobject]@{Now=$epoch;Durations=[Collections.Generic.List[TimeSpan]]::new()}
    $clock={ $state.Now }.GetNewClosure()
    $observe={ New-Observation NotObservable }
    $wait={ param($d,$c) $state.Durations.Add($d);$state.Now=$state.Now+$d }.GetNewClosure()
    $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(5)) -PollInterval ([TimeSpan]::FromSeconds(20)) -Clock $clock -Wait $wait
    Assert-Eq Unresolved $r.RecoveryState 'state';Assert-Eq 1 $state.Durations.Count 'wait count';Assert-Eq ([TimeSpan]::FromSeconds(5)) $state.Durations[0] 'wait duration'
}
$results += Invoke-Case P15 'deadline is established once and never reset' {
    $state=[pscustomobject]@{Now=$epoch;Deadlines=[Collections.Generic.List[DateTimeOffset]]::new();Index=0}
    $sequence=@('NotObservable','NotObservable','Equivalent')
    $clock={ $state.Now }.GetNewClosure()
    $observe={ $value=$sequence[$state.Index];$state.Index++;New-Observation $value }.GetNewClosure()
    $wait={ param($d,$c) $state.Now=$state.Now+$d }.GetNewClosure()
    $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(10)) -PollInterval ([TimeSpan]::FromSeconds(3)) -Clock $clock -Wait $wait
    Assert-Eq Converged $r.RecoveryState 'state';Assert-Eq ($epoch.AddSeconds(10)) $r.DeadlineUtc 'deadline';Assert-Eq 3 $r.ObservationCount 'observations'
}
$results += Invoke-Case P16 'wait receives cancellation probe seam' {
    $state=[pscustomobject]@{Now=$epoch;ProbeSeen=$false;Calls=0}
    $clock={ $state.Now }.GetNewClosure()
    $cancel={ $false }
    $observe={ New-Observation NotObservable }
    $wait={ param($d,$probe) $state.Calls++;$state.ProbeSeen=($null-ne$probe);$state.Now=$state.Now+$d }.GetNewClosure()
    $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(1)) -PollInterval ([TimeSpan]::FromSeconds(1)) -Clock $clock -IsCancellationRequested $cancel -Wait $wait
    Assert-Eq Unresolved $r.RecoveryState 'state';Assert-True $state.ProbeSeen 'probe not supplied';Assert-Eq 1 $state.Calls 'wait calls'
}
$results += Invoke-Case P17 'invalid recovery window is rejected' {
    $thrown=$false
    try { Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe { New-Observation Equivalent } -RecoveryWindow ([TimeSpan]::Zero) -PollInterval ([TimeSpan]::FromSeconds(1)) | Out-Null } catch [System.ArgumentOutOfRangeException] { $thrown=$true }
    Assert-True $thrown 'zero recovery window admitted'
}
$results += Invoke-Case P18 'invalid poll interval is rejected' {
    $thrown=$false
    try { Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe { New-Observation Equivalent } -RecoveryWindow ([TimeSpan]::FromSeconds(1)) -PollInterval ([TimeSpan]::Zero) | Out-Null } catch [System.ArgumentOutOfRangeException] { $thrown=$true }
    Assert-True $thrown 'zero poll interval admitted'
}
$results += Invoke-Case P19 'orchestration exposes no publication capability' {
    $names=@((Get-Command Invoke-NuGetRecoveryPolling).Parameters.Keys)
    Assert-True (@($names|Where-Object{$_ -match '(?i)publish|publication|apikey|credential|endpoint|put|push|mutation'}).Count-eq 0) 'publication-capable parameter seam exposed'
    $state=[pscustomobject]@{Now=$epoch}
    $clock={ $state.Now }.GetNewClosure()
    $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe { New-Observation Equivalent } -RecoveryWindow ([TimeSpan]::FromSeconds(1)) -PollInterval ([TimeSpan]::FromSeconds(1)) -Clock $clock
    Assert-True (@($r.PSObject.Properties.Name|Where-Object{$_ -match '(?i)publish|publication|retry|mutation|credential'}).Count-eq 0) 'publication-capable result exposed'
}
$results += Invoke-Case P20 'all recovery terminal outputs are contract states' {
    foreach($observation in @('Equivalent','Different')) {
        $state=[pscustomobject]@{Now=$epoch}
        $clock={ $state.Now }.GetNewClosure()
        $obs=$observation
        $observe={ New-Observation $obs }.GetNewClosure()
        $r=Invoke-NuGetRecoveryPolling -Trigger (New-Trigger) -Observe $observe -RecoveryWindow ([TimeSpan]::FromSeconds(1)) -PollInterval ([TimeSpan]::FromSeconds(1)) -Clock $clock
        Assert-True ($r.RecoveryState -in @('Converged','Conflict','Unresolved')) "unexpected terminal state $($r.RecoveryState)"
    }
}

$results | Format-Table Id,Name,Outcome -AutoSize
$failed=@($results|Where-Object{$_.Outcome-ne'PASS'})
if($failed.Count-gt 0){$failed|Format-List *;throw "RP-3C.2 CONFORMANCE FAILED: $($failed.Count) case(s)."}
"`nRP-3C.2 CONFORMANCE: $($results.Count) / $($results.Count) PASS"
