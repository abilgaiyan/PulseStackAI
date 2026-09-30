Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '..\NuGetReleaseCoordination.ps1')

$script:Passed=0;$script:Failed=0
function Invoke-Case { param([string]$Id,[string]$Name,[scriptblock]$Body) try{&$Body;$script:Passed++;Write-Host "$Id $Name PASS"}catch{$script:Failed++;Write-Host "$Id $Name FAIL: $($_.Exception.Message)"} }
function Assert-Equal { param([object]$Expected,[object]$Actual,[string]$Message) if($Expected-ne$Actual){throw "$Message Expected='$Expected' Actual='$Actual'."} }
function Assert-True { param([bool]$Condition,[string]$Message) if(-not$Condition){throw $Message} }

$admitted=[pscustomobject]@{Id='admitted-release'};$identity=[pscustomobject]@{CanonicalId='release-identity'}
function New-Counts { [pscustomobject]@{Preflight=0;Publication=0;Recovery=0;Reprojection=0;Selection=0;Admission=0;Continuation=0} }

function Invoke-WithCounts {
    param([string]$ReleaseState,[string]$PreflightState='AllAbsent',[string]$AdmissionState='Admissible',[AllowNull()][string]$RecoveredReleaseState=$null,[object]$Counts)
    $parameters=@{
        AdmittedRelease=$admitted
        GetReleaseIdentity={param($release)$identity}
        GetEffectiveRelease={param($release,$releaseIdentity)[pscustomobject]@{ReleaseState=$ReleaseState;Reason=$null}}
        GetInitialPreflight={param($release,$releaseIdentity)$Counts.Preflight++;[pscustomobject]@{Registry='https://api.nuget.org/v3/index.json';State=$PreflightState;Packages=@();Diagnostic=$null}}
        InvokeInitialPublication={param($release,$releaseIdentity,$preflight)$Counts.Publication++;[pscustomobject]@{LedgerPath='publication.json';Result=[pscustomobject]@{operationConclusion='Complete'}}}
        SelectContinuation={param($release,$releaseIdentity,$effectiveRelease)$Counts.Selection++;[pscustomobject]@{PackageId='PulseStack.Core';PackageIndex=4;SelectionSource='RP5EffectiveRelease'}}
        AdmitContinuation={param($release,$releaseIdentity,$effectiveRelease,$selection)$Counts.Admission++;[pscustomobject]@{State=$AdmissionState;Reason=if($AdmissionState-eq'Admissible'){'AuthoritativeAbsent'}else{'NotAdmissible'}}}
        InvokeContinuation={param($release,$releaseIdentity,$effectiveRelease,$selection,$admission)$Counts.Continuation++;[pscustomobject]@{LedgerPath='continuation.json';Result=[pscustomobject]@{operationConclusion='Accepted'}}}
    }
    if($PSBoundParameters.ContainsKey('RecoveredReleaseState') -and $null-ne$RecoveredReleaseState){
        $parameters.RecoverRelease={param($release,$releaseIdentity,$effectiveRelease)$Counts.Recovery++;[pscustomobject]@{RecoveryState='Converged';EvidenceSource='ContinuationRecovery'}}
        $parameters.ReprojectEffectiveRelease={param($release,$releaseIdentity,$recoveryResult)$Counts.Reprojection++;[pscustomobject]@{ReleaseState=$RecoveredReleaseState;Reason=$null}}
    }
    Invoke-NuGetReleaseCoordination @parameters
}

Invoke-Case 'C01' 'canonical ReleaseComplete performs no mutation' {$c=New-Counts;$r=Invoke-WithCounts -ReleaseState ReleaseComplete -Counts $c;Assert-Equal NoActionRequired $r.Disposition 'Disposition.';Assert-Equal 0 $c.Publication 'Publication.';Assert-Equal 0 $c.Continuation 'Continuation.'}
Invoke-Case 'C02' 'canonical Blocked performs no mutation' {$c=New-Counts;$r=Invoke-WithCounts -ReleaseState Blocked -Counts $c;Assert-Equal Blocked $r.Disposition 'Disposition.';Assert-Equal 0 $c.Continuation 'Continuation.'}
Invoke-Case 'C03' 'canonical Indeterminate without recovery authority performs no mutation' {$c=New-Counts;$r=Invoke-WithCounts -ReleaseState Indeterminate -Counts $c;Assert-Equal Indeterminate $r.Disposition 'Disposition.';Assert-Equal 0 $c.Recovery 'Recovery.';Assert-Equal 0 $c.Continuation 'Continuation.'}
Invoke-Case 'C04' 'canonical RP2 AllAbsent invokes one initial publication operation' {$c=New-Counts;$r=Invoke-WithCounts -ReleaseState NotStarted -Counts $c;Assert-Equal Progressed $r.Disposition 'Disposition.';Assert-Equal InitialPublication $r.Action 'Action.';Assert-Equal 1 $c.Publication 'Publication.';Assert-Equal 0 $c.Continuation 'Continuation.'}
Invoke-Case 'C05' 'canonical RP2 Mixed blocks publication' {$c=New-Counts;$r=Invoke-WithCounts -ReleaseState NotStarted -PreflightState Mixed -Counts $c;Assert-Equal Blocked $r.Disposition 'Disposition.';Assert-Equal 0 $c.Publication 'Publication.'}
Invoke-Case 'C06' 'canonical RP2 Indeterminate does not mutate' {$c=New-Counts;$r=Invoke-WithCounts -ReleaseState NotStarted -PreflightState Indeterminate -Counts $c;Assert-Equal Indeterminate $r.Disposition 'Disposition.';Assert-Equal 0 $c.Publication 'Publication.'}
Invoke-Case 'C07' 'canonical ContinuationEligible plus Admissible invokes one continuation' {$c=New-Counts;$r=Invoke-WithCounts -ReleaseState ContinuationEligible -Counts $c;Assert-Equal Progressed $r.Disposition 'Disposition.';Assert-Equal 1 $c.Continuation 'Continuation.';Assert-Equal 0 $c.Publication 'Publication.'}
Invoke-Case 'C08' 'canonical continuation Blocked admission prevents mutation' {$c=New-Counts;$r=Invoke-WithCounts -ReleaseState ContinuationEligible -AdmissionState Blocked -Counts $c;Assert-Equal Blocked $r.Disposition 'Disposition.';Assert-Equal 0 $c.Continuation 'Continuation.'}
Invoke-Case 'C09' 'canonical continuation Indeterminate admission prevents mutation' {$c=New-Counts;$r=Invoke-WithCounts -ReleaseState ContinuationEligible -AdmissionState Indeterminate -Counts $c;Assert-Equal Indeterminate $r.Disposition 'Disposition.';Assert-Equal 0 $c.Continuation 'Continuation.'}
Invoke-Case 'C10' 'missing ReleaseState fails closed before mutation' {$c=New-Counts;$threw=$false;try{Invoke-NuGetReleaseCoordination -AdmittedRelease $admitted -GetReleaseIdentity{$identity} -GetEffectiveRelease{[pscustomobject]@{State='ReleaseComplete'}} -GetInitialPreflight{$c.Preflight++} -InvokeInitialPublication{$c.Publication++}|Out-Null}catch{$threw=$true};Assert-True $threw 'Must fail.';Assert-Equal 0 $c.Publication 'Publication.'}
Invoke-Case 'C11' 'missing RP2 State fails closed before publication' {$c=New-Counts;$threw=$false;try{Invoke-NuGetReleaseCoordination -AdmittedRelease $admitted -GetReleaseIdentity{$identity} -GetEffectiveRelease{[pscustomobject]@{ReleaseState='NotStarted'}} -GetInitialPreflight{[pscustomobject]@{Outcome='AllAbsent'}} -InvokeInitialPublication{$c.Publication++}|Out-Null}catch{$threw=$true};Assert-True $threw 'Must fail.';Assert-Equal 0 $c.Publication 'Publication.'}
Invoke-Case 'C12' 'one invocation never combines initial publication and continuation' {$c=New-Counts;$null=Invoke-WithCounts -ReleaseState NotStarted -Counts $c;Assert-Equal 1 ($c.Publication+$c.Continuation) 'Mutation count.';$c=New-Counts;$null=Invoke-WithCounts -ReleaseState ContinuationEligible -Counts $c;Assert-Equal 1 ($c.Publication+$c.Continuation) 'Mutation count.'}
Invoke-Case 'C13' 'coordinator result contains no coordinator operation identity' {$c=New-Counts;$r=Invoke-WithCounts -ReleaseState ReleaseComplete -Counts $c;Assert-True ($null-eq$r.PSObject.Properties['OperationId']) 'No OperationId.'}

Invoke-Case 'C14' 'recovery reprojects to ReleaseComplete without mutation' {$c=New-Counts;$r=Invoke-WithCounts -ReleaseState Indeterminate -RecoveredReleaseState ReleaseComplete -Counts $c;Assert-Equal NoActionRequired $r.Disposition 'Disposition.';Assert-Equal Recovery $r.Action 'Action.';Assert-Equal 1 $c.Recovery 'Recovery.';Assert-Equal 1 $c.Reprojection 'Reprojection.';Assert-Equal 0 ($c.Publication+$c.Continuation) 'Mutation count.'}
Invoke-Case 'C15' 'recovery reprojects to Blocked without mutation' {$c=New-Counts;$r=Invoke-WithCounts -ReleaseState Indeterminate -RecoveredReleaseState Blocked -Counts $c;Assert-Equal Blocked $r.Disposition 'Disposition.';Assert-Equal Recovery $r.Action 'Action.';Assert-Equal 0 $c.Continuation 'Continuation.'}
Invoke-Case 'C16' 'recovery remains Indeterminate without mutation' {$c=New-Counts;$r=Invoke-WithCounts -ReleaseState Indeterminate -RecoveredReleaseState Indeterminate -Counts $c;Assert-Equal Indeterminate $r.Disposition 'Disposition.';Assert-Equal Recovery $r.Action 'Action.';Assert-Equal 0 $c.Continuation 'Continuation.'}
Invoke-Case 'C17' 'recovery reprojects ContinuationEligible then performs one fresh continuation' {$c=New-Counts;$r=Invoke-WithCounts -ReleaseState Indeterminate -RecoveredReleaseState ContinuationEligible -Counts $c;Assert-Equal Progressed $r.Disposition 'Disposition.';Assert-Equal Continuation $r.Action 'Action.';Assert-Equal 1 $c.Recovery 'Recovery.';Assert-Equal 1 $c.Reprojection 'Reprojection.';Assert-Equal 1 $c.Selection 'Selection.';Assert-Equal 1 $c.Admission 'Admission.';Assert-Equal 1 $c.Continuation 'Continuation.';Assert-Equal 0 $c.Publication 'Publication.';Assert-Equal Converged $r.OperationResult.Recovery.RecoveryState 'Recovery result.';Assert-Equal Accepted $r.OperationResult.Continuation.Result.operationConclusion 'Continuation result.'}
Invoke-Case 'C18' 'recovered continuation blocked by fresh admission performs no mutation' {$c=New-Counts;$r=Invoke-WithCounts -ReleaseState Indeterminate -RecoveredReleaseState ContinuationEligible -AdmissionState Blocked -Counts $c;Assert-Equal Blocked $r.Disposition 'Disposition.';Assert-Equal Recovery $r.Action 'Action.';Assert-Equal 1 $c.Recovery 'Recovery.';Assert-Equal 1 $c.Admission 'Admission.';Assert-Equal 0 $c.Continuation 'Continuation.'}
Invoke-Case 'C19' 'recovery requires reprojection authority and fails before mutation' {$c=New-Counts;$threw=$false;try{Invoke-NuGetReleaseCoordination -AdmittedRelease $admitted -GetReleaseIdentity{$identity} -GetEffectiveRelease{[pscustomobject]@{ReleaseState='Indeterminate'}} -GetInitialPreflight{throw 'must not run'} -InvokeInitialPublication{$c.Publication++} -RecoverRelease{$c.Recovery++;[pscustomobject]@{RecoveryState='Converged'}}|Out-Null}catch{$threw=$true};Assert-True $threw 'Must fail.';Assert-Equal 0 $c.Recovery 'Recovery must not run without reprojection authority.';Assert-Equal 0 ($c.Publication+$c.Continuation) 'Mutation count.'}
Invoke-Case 'C20' 'recovery reprojection cannot authorize initial publication' {$c=New-Counts;$threw=$false;try{$null=Invoke-WithCounts -ReleaseState Indeterminate -RecoveredReleaseState NotStarted -Counts $c}catch{$threw=$true};Assert-True $threw 'Recovered NotStarted must fail.';Assert-Equal 0 $c.Preflight 'Preflight.';Assert-Equal 0 $c.Publication 'Publication.'}
Invoke-Case 'C21' 'recovery path never combines publication with continuation' {$c=New-Counts;$null=Invoke-WithCounts -ReleaseState Indeterminate -RecoveredReleaseState ContinuationEligible -Counts $c;Assert-Equal 1 ($c.Publication+$c.Continuation) 'Mutation count.';Assert-Equal 1 $c.Recovery 'Recovery count.'}

Write-Host ''
Write-Host "RP-7C recovery conformance: passed=$script:Passed failed=$script:Failed"
if($script:Failed-ne0){exit 1}
