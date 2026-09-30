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
        [ref]$PreflightCalls,
        [ref]$PublicationCalls
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
        }
}

Invoke-Case 'C01' 'complete release performs no mutation' {
    $preflight = 0; $publication = 0
    $result = Invoke-CoordinatorCase -State 'Complete' -PreflightCalls ([ref]$preflight) -PublicationCalls ([ref]$publication)
    Assert-Equal 'NoActionRequired' $result.Disposition 'Disposition mismatch.'
    Assert-Equal 'None' $result.Action 'Action mismatch.'
    Assert-Equal 0 $preflight 'Preflight must not run.'
    Assert-Equal 0 $publication 'Publication must not run.'
}

Invoke-Case 'C02' 'blocked release performs no mutation' {
    $preflight = 0; $publication = 0
    $result = Invoke-CoordinatorCase -State 'Blocked' -PreflightCalls ([ref]$preflight) -PublicationCalls ([ref]$publication)
    Assert-Equal 'Blocked' $result.Disposition 'Disposition mismatch.'
    Assert-Equal 0 $publication 'Publication must not run.'
}

Invoke-Case 'C03' 'indeterminate release performs no mutation' {
    $preflight = 0; $publication = 0
    $result = Invoke-CoordinatorCase -State 'Indeterminate' -PreflightCalls ([ref]$preflight) -PublicationCalls ([ref]$publication)
    Assert-Equal 'Indeterminate' $result.Disposition 'Disposition mismatch.'
    Assert-Equal 0 $publication 'Publication must not run.'
}

Invoke-Case 'C04' 'fresh all-absent release invokes one initial publication operation' {
    $preflight = 0; $publication = 0
    $result = Invoke-CoordinatorCase -State 'NotStarted' -PreflightOutcome 'AllAbsent' -PreflightCalls ([ref]$preflight) -PublicationCalls ([ref]$publication)
    Assert-Equal 'Progressed' $result.Disposition 'Disposition mismatch.'
    Assert-Equal 'InitialPublication' $result.Action 'Action mismatch.'
    Assert-Equal 1 $preflight 'Preflight call count mismatch.'
    Assert-Equal 1 $publication 'Publication call count mismatch.'
    Assert-Equal 'Complete' $result.OperationResult.Outcome 'Underlying result must be preserved.'
}

Invoke-Case 'C05' 'fresh non-absent preflight blocks publication' {
    $preflight = 0; $publication = 0
    $result = Invoke-CoordinatorCase -State 'NotStarted' -PreflightOutcome 'Conflict' -PreflightCalls ([ref]$preflight) -PublicationCalls ([ref]$publication)
    Assert-Equal 'Blocked' $result.Disposition 'Disposition mismatch.'
    Assert-Equal 'None' $result.Action 'Action mismatch.'
    Assert-Equal 1 $preflight 'Preflight call count mismatch.'
    Assert-Equal 0 $publication 'Publication must not run.'
}

Invoke-Case 'C06' 'fresh indeterminate preflight does not mutate' {
    $preflight = 0; $publication = 0
    $result = Invoke-CoordinatorCase -State 'NotStarted' -PreflightOutcome 'Indeterminate' -PreflightCalls ([ref]$preflight) -PublicationCalls ([ref]$publication)
    Assert-Equal 'Indeterminate' $result.Disposition 'Disposition mismatch.'
    Assert-Equal 0 $publication 'Publication must not run.'
}

Invoke-Case 'C07' 'foundation rejects unsupported continuation state without mutation' {
    $preflight = 0; $publication = 0
    $threw = $false
    try {
        $null = Invoke-CoordinatorCase -State 'ContinuationEligible' -PreflightCalls ([ref]$preflight) -PublicationCalls ([ref]$publication)
    }
    catch {
        $threw = $true
    }
    Assert-True $threw 'Unsupported state must fail closed.'
    Assert-Equal 0 $publication 'Publication must not run.'
}

Invoke-Case 'C08' 'coordinator result contains no coordinator operation identity' {
    $preflight = 0; $publication = 0
    $result = Invoke-CoordinatorCase -State 'Complete' -PreflightCalls ([ref]$preflight) -PublicationCalls ([ref]$publication)
    Assert-True ($null -eq $result.PSObject.Properties['OperationId']) 'Coordinator must not own an operation id.'
    Assert-True ($null -eq $result.PSObject.Properties['CoordinatorOperationId']) 'Coordinator must not expose a coordinator operation id.'
}

Write-Host ""
Write-Host "RP-7C foundation conformance: passed=$script:Passed failed=$script:Failed"
if ($script:Failed -ne 0) { exit 1 }
