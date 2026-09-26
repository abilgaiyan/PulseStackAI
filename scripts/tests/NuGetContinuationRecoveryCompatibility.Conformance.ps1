Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'

$repositoryRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts/NuGetRecoveryEvidence.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetRecoveryConvergence.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetRecoveryPolling.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetRecoveryContinuation.ps1')

function Assert-Eq($Expected,$Actual,[string]$Message){if($Expected-cne$Actual){throw "$Message Expected '$Expected', actual '$Actual'."}}
function Assert-True([bool]$Value,[string]$Message){if(-not$Value){throw $Message}}
function Invoke-Case([string]$Id,[string]$Name,[scriptblock]$Body){try{&$Body;[pscustomobject]@{Id=$Id;Name=$Name;Outcome='PASS'}}catch{[pscustomobject]@{Id=$Id;Name=$Name;Outcome='FAIL';Error=$_.Exception.Message}}}

$temp=Join-Path ([IO.Path]::GetTempPath()) ('pulsestack-rp4c-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force|Out-Null
$operationId='00000000-0000-0000-0000-000000000402'
$historicalId='00000000-0000-0000-0000-000000000401'
$packageId='Pkg.D';$version='1.0.4-test.1';$sha=('a'*64)

function Write-Ledger([string]$State,[AllowNull()][Nullable[int]]$Status,[AllowNull()][object]$Diagnostic,[string]$LedgerState='Terminal',[AllowNull()][string]$Conclusion=$null){
 $ledger=[ordered]@{schemaVersion='1.0';operationId=$operationId;operationKind='Continuation';historicalPublicationOperationId=$historicalId;packageIndex=3;ledgerState=$LedgerState;registry='NuGet.org';serviceIndex='https://api.nuget.org/v3/index.json';packagePublishEndpoint='https://www.nuget.org/api/v2/package';sourceCommit=('b'*40);versionPrefix=$null;packageVersion=$version;releaseAuthorityTag='release/v1.0.4';startedAtUtc='2026-09-26T12:00:00.0000000+00:00';completedAtUtc=if($LedgerState-eq'Terminal'){'2026-09-26T12:01:00.0000000+00:00'}else{$null};operationConclusion=$Conclusion;knownAcceptedCount=0;knownRejectedCount=if($State-eq'Rejected'){1}else{0};indeterminateCount=if($State-eq'Indeterminate'){1}else{0};notAttemptedCount=0;packages=@([ordered]@{id=$packageId;version=$version;admittedSha256=$sha;mutationState=$State;statusCode=$Status;diagnostic=$Diagnostic})}
 $path=Join-Path $temp ("$State.json")
 [IO.File]::WriteAllText($path,($ledger|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false));$path
}
function New-Candidate([string]$Path){Get-NuGetRecoveryCandidate -LedgerPath $Path -OperationId $operationId -PackageId $packageId -PackageVersion $version -AdmittedSha256 $sha}
function New-TerminalResult([string]$State,[object]$Observation){[pscustomobject]@{RecoveryState=$State;Terminal=$true;StartedAtUtc=[DateTimeOffset]'2026-09-26T12:02:00Z';DeadlineUtc=[DateTimeOffset]'2026-09-26T12:07:00Z';ObservationCount=1;LastObservation=$Observation}}

$results=@()
try {
 $rejected=Write-Ledger 'Rejected' 409 ([pscustomobject]@{Code='ExistingIdentityConflict';Message='Registry reports that the exact package identity already exists.';StatusCode=409}) 'Terminal' 'Rejected'
 $indeterminate=Write-Ledger 'Indeterminate' $null ([pscustomobject]@{Code='TransportUncertainty';Message='Package PUT failed without an authoritative registry outcome.';StatusCode=$null}) 'Terminal' 'Indeterminate'
 $attempting=Write-Ledger 'Attempting' $null $null 'InProgress' $null

 $results+=Invoke-Case 'C01' 'continuation 409 maps to Conflict409' {$c=New-Candidate $rejected;Assert-Eq 'Conflict409' $c.Trigger 'trigger'}
 $results+=Invoke-Case 'C02' 'continuation Indeterminate maps to IndeterminateMutation' {$c=New-Candidate $indeterminate;Assert-Eq 'IndeterminateMutation' $c.Trigger 'trigger'}
 $results+=Invoke-Case 'C03' 'continuation Attempting maps to StaleAttempting' {$c=New-Candidate $attempting;Assert-Eq 'StaleAttempting' $c.Trigger 'trigger'}
 $results+=Invoke-Case 'C04' 'candidate authority is continuation operation identity' {$c=New-Candidate $rejected;Assert-Eq $operationId $c.Operation.OperationId 'candidate operation';Assert-True ($c.Operation.OperationId-cne$historicalId) 'historical operation became recovery authority'}
 $results+=Invoke-Case 'C05' 'candidate preserves exact continuation package identity' {$c=New-Candidate $indeterminate;Assert-Eq $packageId $c.Package.Id 'id';Assert-Eq $version $c.Package.Version 'version';Assert-Eq $sha $c.Package.AdmittedSha256 'sha'}
 $results+=Invoke-Case 'C06' 'Equivalent recovery observation converges' {$c=New-Candidate $rejected;$t=New-NuGetRecoveryTrigger -Kind $c.Trigger;$r=Resolve-NuGetRecoveryObservation -Trigger $t -Observation ([pscustomobject]@{State='Equivalent'});Assert-Eq 'Converged' $r.RecoveryState 'state';Assert-Eq 'True' ([string]$r.Terminal) 'terminal'}
 $results+=Invoke-Case 'C07' 'Different recovery observation conflicts' {$c=New-Candidate $indeterminate;$t=New-NuGetRecoveryTrigger -Kind $c.Trigger;$r=Resolve-NuGetRecoveryObservation -Trigger $t -Observation ([pscustomobject]@{State='Different'});Assert-Eq 'Conflict' $r.RecoveryState 'state';Assert-Eq 'True' ([string]$r.Terminal) 'terminal'}
 $results+=Invoke-Case 'C08' 'uncertain observations can terminate Unresolved' {$c=New-Candidate $attempting;$t=New-NuGetRecoveryTrigger -Kind $c.Trigger;$times=[System.Collections.Generic.Queue[DateTimeOffset]]::new();$times.Enqueue([DateTimeOffset]'2026-09-26T12:00:00Z');$times.Enqueue([DateTimeOffset]'2026-09-26T12:00:00Z');$times.Enqueue([DateTimeOffset]'2026-09-26T12:00:01Z');$clock={if($times.Count){$times.Dequeue()}else{[DateTimeOffset]'2026-09-26T12:00:02Z'}}.GetNewClosure();$r=Invoke-NuGetRecoveryPolling -Trigger $t -Observe {[pscustomobject]@{State='NotObservable'}} -RecoveryWindow ([TimeSpan]::FromSeconds(1)) -PollInterval ([TimeSpan]::FromSeconds(1)) -Clock $clock -Wait {param($d,$p)};Assert-Eq 'Unresolved' $r.RecoveryState 'state'}
 $results+=Invoke-Case 'C09' 'recovery evidence binds continuation operation and package' {$c=New-Candidate $rejected;$o=[pscustomobject]@{State='Equivalent'};$e=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-TerminalResult 'Converged' $o) -Observations @($o);Assert-Eq $operationId $e.HistoricalPublication.Operation.OperationId 'operation';Assert-Eq $packageId $e.HistoricalPublication.Package.Id 'package';Assert-Eq 'Conflict409' $e.Recovery.Trigger 'trigger'}
 $results+=Invoke-Case 'C10' 'recovery candidate does not rewrite historical provenance' {$before=[IO.File]::ReadAllText($rejected);$null=New-Candidate $rejected;$after=[IO.File]::ReadAllText($rejected);Assert-Eq $before $after 'ledger bytes'}
 $results+=Invoke-Case 'C11' 'recovery path contains no mutation authority' {$c=New-Candidate $attempting;$t=New-NuGetRecoveryTrigger -Kind $c.Trigger;$r=Resolve-NuGetRecoveryObservation -Trigger $t -Observation ([pscustomobject]@{State='Equivalent'});Assert-Eq 'Converged' $r.RecoveryState 'state';Assert-True ($null-eq$r.PSObject.Properties['Credential']) 'credential surfaced';Assert-True ($null-eq$r.PSObject.Properties['Publish']) 'publish surfaced'}
 $results+=Invoke-Case 'C12' 'one-package recovery does not authorize a suffix' {$c=New-Candidate $rejected;$o=[pscustomobject]@{State='Equivalent'};$e=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-TerminalResult 'Converged' $o) -Observations @($o);$d=Get-NuGetWholeOperationContinuationDecision -LedgerPath $rejected -Candidate $c -RecoveryEvidence $e;Assert-Eq 'False' ([string]$d.MayContinue) 'may continue';Assert-Eq 'False' ([string]$d.HasNextPackage) 'has next';Assert-True ($null-eq$d.NextPackage) 'next package exists'}
 $results+=Invoke-Case 'C13' 'RP-3C reports RecoveredEnd for recovered one-package operation' {$c=New-Candidate $rejected;$o=[pscustomobject]@{State='Equivalent'};$e=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-TerminalResult 'Converged' $o) -Observations @($o);$d=Get-NuGetWholeOperationContinuationDecision -LedgerPath $rejected -Candidate $c -RecoveryEvidence $e;Assert-Eq 'RecoveredEnd' $d.WholeOperationDisposition 'disposition';Assert-True ($d.WholeOperationDisposition-cne'ContinuationEligible') 'suffix authority manufactured'}
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue }

$results|Format-Table -AutoSize
$failed=@($results|Where-Object Outcome -eq 'FAIL')
if($failed.Count){$failed|ForEach-Object{Write-Host "$($_.Id) $($_.Name): $($_.Error)" -ForegroundColor Red};throw "RP-4C continuation recovery compatibility conformance failed: $($failed.Count) case(s)."}
Write-Host "RP-4C continuation recovery compatibility conformance passed: $($results.Count)/$($results.Count)."
