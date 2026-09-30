Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '..\NuGetReleaseCoordination.ps1')

$script:Passed = 0
$script:Failed = 0

function Invoke-Case {
    param(
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Body
    )

    try {
        & $Body
        $script:Passed++
        Write-Host "$Id $Name PASS"
    }
    catch {
        $script:Failed++
        Write-Host "$Id $Name FAIL: $($_.Exception.Message)"
    }
}

function Assert-Equal {
    param([object]$Expected, [object]$Actual, [string]$Message)
    if ($Expected -ne $Actual) {
        throw "$Message Expected='$Expected' Actual='$Actual'."
    }
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$admitted = [pscustomobject]@{ Id = 'admitted-release' }
$identity = [pscustomobject]@{ CanonicalId = 'release-identity' }

function Invoke-CoordinatorCase {
    param(
        [Parameter(Mandatory)][string]$State,
        [string]$PreflightOutcome = 'AllAbsent',
        [string]$AdmissionOutcome = 'Admitted',
        [ref]$PreflightCalls,
        [ref]$PublicationCalls,
        [ref]$SelectionCalls,
        [ref]$AdmissionCalls,
        [ref]$ContinuationCalls
    )

    Invoke-NuGetReleaseCoordination `
        -AdmittedRelease $admitted `
        -GetReleaseIdentity { param($release) $identity } `
        -GetEffectiveRelease { param($release, $releaseIdentity) [pscustomobject]@{ State = $State } } `
        -GetInitialPreflight {
            param($release, $releaseIdentity)
            $PreflightCalls.Value++
            [pscustomobject]@{ Outcome = $PreflightOutcome }
        } `
        -InvokeInitialPublication {
            param($release, $releaseIdentity, $preflight)
            $PublicationCalls.Value++
            [pscustomobject]@{ Outcome = 'Complete'; OperationId = 'publication-1' }
        } `
        -SelectContinuation {
            param($release, $releaseIdentity, $effectiveRelease)
            $SelectionCalls.Value++
            [pscustomobject]@{ PackageId = 'PulseStack.Core'; Position = 4 }
        } `
        -AdmitContinuation {
            param($release, $releaseIdentity, $effectiveRelease, $selection)
            $AdmissionCalls.Value++
            [pscustomobject]@{ Outcome = $AdmissionOutcome; GrantId = if ($AdmissionOutcome -eq 'Admitted') { 'grant-1' } else { $null } }
        } `
        -InvokeContinuation {
            param($release, $releaseIdentity, $effectiveRelease, $selection, $admission)
            $ContinuationCalls.Value++
            [pscustomobject]@{ Outcome = 'Accepted'; OperationId = 'continuation-1'; PackageId = $selection.PackageId }
        }
}

function New-Counts {
    [pscustomobject]@{
        Preflight = 0
        Publication = 0
        Selection = 0
        Admission = 0
        Continuation = 0
    }
}

function Invoke-WithCounts {
    param(
        [Parameter(Mandatory)][string]$State,
        [string]$PreflightOutcome = 'AllAbsent',
        [string]$AdmissionOutcome = 'Admitted',
        [Parameter(Mandatory)][object]$Counts
    )

    Invoke-CoordinatorCase `
        -State $State `
        -PreflightOutcome $PreflightOutcome `
        -AdmissionOutcome $AdmissionOutcome `
        -PreflightCalls ([ref]$Counts.Preflight) `
        -PublicationCalls ([ref]$Counts.Publication) `
        -SelectionCalls ([ref]$Counts.Selection) `
        -AdmissionCalls ([ref]$Counts.Admission) `
        -ContinuationCalls ([ref]$Counts.Continuation)
}

Invoke-Case 'C01' 'complete release performs no mutation' {
    $c = New-Counts
    $result = Invoke-WithCounts -State 'Complete' -Counts $c
    Assert-Equal 'NoActionRequired' $result.Disposition 'Disposition mismatch.'
    Assert-Equal 'None' $result.Action 'Action mismatch.'
    Assert-Equal 0 $c.Preflight 'Preflight must not run.'
    Assert-Equal 0 $c.Publication 'Publication must not run.'
    Assert-Equal 0 $c.Continuation 'Continuation must not run.'
}

Invoke-Case 'C02' 'blocked release performs no mutation' {
    $c = New-Counts
    $result = Invoke-WithCounts -State 'Blocked' -Counts $c
    Assert-Equal 'Blocked' $result.Disposition 'Disposition mismatch.'
    Assert-Equal 0 $c.Publication 'Publication must not run.'
    Assert-Equal 0 $c.Continuation 'Continuation must not run.'
}

Invoke-Case 'C03' 'indeterminate release performs no mutation' {
    $c = New-Counts
    $result = Invoke-WithCounts -State 'Indeterminate' -Counts $c
    Assert-Equal 'Indeterminate' $result.Disposition 'Disposition mismatch.'
    Assert-Equal 0 $c.Publication 'Publication must not run.'
    Assert-Equal 0 $c.Continuation 'Continuation must not run.'
}

Invoke-Case 'C04' 'fresh all-absent release invokes one initial publication operation' {
    $c = New-Counts
    $result = Invoke-WithCounts -State 'NotStarted' -PreflightOutcome 'AllAbsent' -Counts $c
    Assert-Equal 'Progressed' $result.Disposition 'Disposition mismatch.'
    Assert-Equal 'InitialPublication' $result.Action 'Action mismatch.'
    Assert-Equal 1 $c.Preflight 'Preflight call count mismatch.'
    Assert-Equal 1 $c.Publication 'Publication call count mismatch.'
    Assert-Equal 0 $c.Continuation 'Continuation must not run after initial publication.'
    Assert-Equal 'Complete' $result.OperationResult.Outcome 'Underlying result must be preserved.'
}

Invoke-Case 'C05' 'fresh non-absent preflight blocks publication' {
    $c = New-Counts
    $result = Invoke-WithCounts -State 'NotStarted' -PreflightOutcome 'Conflict' -Counts $c
    Assert-Equal 'Blocked' $result.Disposition 'Disposition mismatch.'
    Assert-Equal 'None' $result.Action 'Action mismatch.'
    Assert-Equal 1 $c.Preflight 'Preflight call count mismatch.'
    Assert-Equal 0 $c.Publication 'Publication must not run.'
    Assert-Equal 0 $c.Continuation 'Continuation must not run.'
}

Invoke-Case 'C06' 'fresh indeterminate preflight does not mutate' {
    $c = New-Counts
    $result = Invoke-WithCounts -State 'NotStarted' -PreflightOutcome 'Indeterminate' -Counts $c
    Assert-Equal 'Indeterminate' $result.Disposition 'Disposition mismatch.'
    Assert-Equal 0 $c.Publication 'Publication must not run.'
    Assert-Equal 0 $c.Continuation 'Continuation must not run.'
}

Invoke-Case 'C07' 'continuation eligible release selects admits and invokes one continuation' {
    $c = New-Counts
    $result = Invoke-WithCounts -State 'ContinuationEligible' -AdmissionOutcome 'Admitted' -Counts $c
    Assert-Equal 'Progressed' $result.Disposition 'Disposition mismatch.'
    Assert-Equal 'Continuation' $result.Action 'Action mismatch.'
    Assert-Equal 0 $c.Preflight 'Initial preflight must not run.'
    Assert-Equal 0 $c.Publication 'Initial publication must not run.'
    Assert-Equal 1 $c.Selection 'Selection call count mismatch.'
    Assert-Equal 1 $c.Admission 'Admission call count mismatch.'
    Assert-Equal 1 $c.Continuation 'Continuation call count mismatch.'
    Assert-Equal 'Accepted' $result.OperationResult.Outcome 'Underlying continuation result must be preserved.'
}

Invoke-Case 'C08' 'continuation admission conflict blocks mutation' {
    $c = New-Counts
    $result = Invoke-WithCounts -State 'ContinuationEligible' -AdmissionOutcome 'Conflict' -Counts $c
    Assert-Equal 'Blocked' $result.Disposition 'Disposition mismatch.'
    Assert-Equal 'None' $result.Action 'Action mismatch.'
    Assert-Equal 1 $c.Selection 'Selection call count mismatch.'
    Assert-Equal 1 $c.Admission 'Admission call count mismatch.'
    Assert-Equal 0 $c.Continuation 'Continuation must not run.'
}

Invoke-Case 'C09' 'continuation admission indeterminate does not mutate' {
    $c = New-Counts
    $result = Invoke-WithCounts -State 'ContinuationEligible' -AdmissionOutcome 'Indeterminate' -Counts $c
    Assert-Equal 'Indeterminate' $result.Disposition 'Disposition mismatch.'
    Assert-Equal 'None' $result.Action 'Action mismatch.'
    Assert-Equal 1 $c.Selection 'Selection call count mismatch.'
    Assert-Equal 1 $c.Admission 'Admission call count mismatch.'
    Assert-Equal 0 $c.Continuation 'Continuation must not run.'
}

Invoke-Case 'C10' 'continuation routing requires all continuation authorities' {
    $threw = $false
    try {
        $null = Invoke-NuGetReleaseCoordination `
            -AdmittedRelease $admitted `
            -GetReleaseIdentity { param($release) $identity } `
            -GetEffectiveRelease { param($release, $releaseIdentity) [pscustomobject]@{ State = 'ContinuationEligible' } } `
            -GetInitialPreflight { throw 'must not run' } `
            -InvokeInitialPublication { throw 'must not run' }
    }
    catch {
        $threw = $true
    }
    Assert-True $threw 'Missing continuation authorities must fail closed.'
}

Invoke-Case 'C11' 'one coordinator invocation never combines initial publication and continuation' {
    $c = New-Counts
    $null = Invoke-WithCounts -State 'NotStarted' -Counts $c
    Assert-Equal 1 ($c.Publication + $c.Continuation) 'Exactly one mutation operation is allowed.'

    $c = New-Counts
    $null = Invoke-WithCounts -State 'ContinuationEligible' -Counts $c
    Assert-Equal 1 ($c.Publication + $c.Continuation) 'Exactly one mutation operation is allowed.'
}

Invoke-Case 'C12' 'coordinator result contains no coordinator operation identity' {
    $c = New-Counts
    $result = Invoke-WithCounts -State 'Complete' -Counts $c
    Assert-True ($null -eq $result.PSObject.Properties['OperationId']) 'Coordinator must not own an operation id.'
    Assert-True ($null -eq $result.PSObject.Properties['CoordinatorOperationId']) 'Coordinator must not expose a coordinator operation id.'
}

Write-Host ""
Write-Host "RP-7C continuation conformance: passed=$script:Passed failed=$script:Failed"
if ($script:Failed -ne 0) { exit 1 }
