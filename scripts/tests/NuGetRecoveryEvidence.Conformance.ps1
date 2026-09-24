Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts/NuGetRecoveryEvidence.ps1')

function Assert-Eq($Expected,$Actual,[string]$Message){if($Expected-cne$Actual){throw "$Message Expected '$Expected', actual '$Actual'."}}
function Assert-True([bool]$Condition,[string]$Message){if(-not$Condition){throw $Message}}
function Invoke-Case([string]$Id,[string]$Name,[scriptblock]$Body){try{&$Body;[pscustomobject]@{Id=$Id;Name=$Name;Outcome='PASS'}}catch{[pscustomobject]@{Id=$Id;Name=$Name;Outcome='FAIL';Error=$_.Exception.Message}}}

$operationId='00000000-0000-0000-0000-000000000301'
$packageId='PulseStack.Core'
$version='1.0.4-test.1'
$sha=('a'*64)

function New-Ledger {
    param(
        [string]$MutationState='Rejected',
        [AllowNull()][object]$StatusCode=409,
        [AllowNull()][object]$Diagnostic=([pscustomobject]@{Code='ExistingIdentityConflict';Message='exists';StatusCode=409}),
        [string]$LedgerState='Terminal',
        [AllowNull()][string]$Conclusion='StoppedRejected',
        [object[]]$Packages=$null
    )
    if($null-eq$Packages){
        $Packages=@([pscustomobject]@{id=$packageId;version=$version;admittedSha256=$sha;mutationState=$MutationState;statusCode=$StatusCode;diagnostic=$Diagnostic})
    }
    [pscustomobject]@{
        schemaVersion='1.0';operationId=$operationId;ledgerState=$LedgerState;registry='NuGet.org';serviceIndex='https://api.nuget.org/v3/index.json';
        packagePublishEndpoint='https://www.nuget.org/api/v2/package';sourceCommit=('b'*40);versionPrefix='1.0.4';packageVersion=$version;releaseAuthorityTag="v$version";
        startedAtUtc='2026-09-24T00:00:00.0000000+00:00';completedAtUtc=if($LedgerState-ceq'Terminal'){'2026-09-24T00:00:05.0000000+00:00'}else{$null};
        operationConclusion=$Conclusion;knownAcceptedCount=0;knownRejectedCount=if($MutationState-ceq'Rejected'){1}else{0};
        indeterminateCount=if($MutationState-in@('Indeterminate','Attempting')){1}else{0};notAttemptedCount=0;packages=@($Packages)
    }
}
function New-TempLedger {
    param([object]$Ledger=(New-Ledger))
    $dir=Join-Path ([IO.Path]::GetTempPath()) ('pulsestack-rp3c3-'+[guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $dir -Force|Out-Null
    $path=Join-Path $dir 'publication-result.json'
    [IO.File]::WriteAllText($path,($Ledger|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    $path
}
function Get-Candidate {
    param([string]$Path)
    Get-NuGetRecoveryCandidate -LedgerPath $Path -OperationId $operationId -PackageId $packageId -PackageVersion $version -AdmittedSha256 $sha
}
function New-RecoveryResult {
    param([string]$State='Converged')
    [pscustomobject]@{RecoveryState=$State;Terminal=$true;StartedAtUtc=[DateTimeOffset]'2026-09-24T00:10:00Z';DeadlineUtc=[DateTimeOffset]'2026-09-24T00:10:30Z';ObservationCount=2;LastObservation=[pscustomobject]@{State=if($State-ceq'Conflict'){'Different'}elseif($State-ceq'Converged'){'Equivalent'}else{'Indeterminate'}}}
}

$results=@()
$results+=Invoke-Case H01 'Rejected 409 classifies Conflict409' {
    $p=New-TempLedger;$c=Get-Candidate $p;Assert-Eq Conflict409 $c.Trigger 'trigger';Assert-Eq Rejected $c.Package.MutationState 'state';Assert-Eq 409 $c.Package.StatusCode 'status'
}
$results+=Invoke-Case H02 'Indeterminate classifies IndeterminateMutation' {
    $d=[pscustomobject]@{Code='TransportUncertainty';Message='uncertain';StatusCode=$null};$p=New-TempLedger (New-Ledger -MutationState Indeterminate -StatusCode $null -Diagnostic $d -Conclusion StoppedIndeterminate);$c=Get-Candidate $p;Assert-Eq IndeterminateMutation $c.Trigger 'trigger';Assert-Eq Indeterminate $c.Package.MutationState 'state'
}
$results+=Invoke-Case H03 'persisted Attempting classifies StaleAttempting without age' {
    $p=New-TempLedger (New-Ledger -MutationState Attempting -StatusCode $null -Diagnostic $null -LedgerState InProgress -Conclusion $null);$c=Get-Candidate $p;Assert-Eq StaleAttempting $c.Trigger 'trigger';Assert-Eq Attempting $c.Package.MutationState 'state';Assert-Eq InProgress $c.Operation.LedgerState 'ledger state'
}
$results+=Invoke-Case H04 'Accepted is not recovery eligible' {
    $p=New-TempLedger (New-Ledger -MutationState Accepted -StatusCode 201 -Diagnostic $null -Conclusion Complete);$thrown=$false;try{Get-Candidate $p|Out-Null}catch{$thrown=$true};Assert-True $thrown 'Accepted admitted to recovery'
}
$results+=Invoke-Case H05 'Rejected 400 is not Conflict409' {
    $d=[pscustomobject]@{Code='InvalidPackage';Message='bad';StatusCode=400};$p=New-TempLedger (New-Ledger -MutationState Rejected -StatusCode 400 -Diagnostic $d);$thrown=$false;try{Get-Candidate $p|Out-Null}catch{$thrown=$true};Assert-True $thrown 'Rejected 400 admitted'
}
$results+=Invoke-Case H06 'operation identity must match exactly' {
    $p=New-TempLedger;$thrown=$false;try{Get-NuGetRecoveryCandidate -LedgerPath $p -OperationId '00000000-0000-0000-0000-000000000999' -PackageId $packageId -PackageVersion $version -AdmittedSha256 $sha|Out-Null}catch{$thrown=$true};Assert-True $thrown 'operation mismatch admitted'
}
$results+=Invoke-Case H07 'package identity must match exactly' {
    $p=New-TempLedger;$thrown=$false;try{Get-NuGetRecoveryCandidate -LedgerPath $p -OperationId $operationId -PackageId 'PulseStack.Agents' -PackageVersion $version -AdmittedSha256 $sha|Out-Null}catch{$thrown=$true};Assert-True $thrown 'package mismatch admitted'
}
$results+=Invoke-Case H08 'duplicate exact package attempts are rejected' {
    $x=[pscustomobject]@{id=$packageId;version=$version;admittedSha256=$sha;mutationState='Attempting';statusCode=$null;diagnostic=$null};$p=New-TempLedger (New-Ledger -Packages @($x,$x) -LedgerState InProgress -Conclusion $null);$thrown=$false;try{Get-Candidate $p|Out-Null}catch{$thrown=$true};Assert-True $thrown 'duplicate exact attempts admitted'
}
$results+=Invoke-Case H09 'invalid JSON is rejected' {
    $p=New-TempLedger;[IO.File]::WriteAllText($p,'{bad',[Text.UTF8Encoding]::new($false));$thrown=$false;try{Read-NuGetPublicationLedger $p|Out-Null}catch{$thrown=$true};Assert-True $thrown 'invalid JSON admitted'
}
$results+=Invoke-Case H10 'Rejected409 plus Converged preserves historical mutation truth' {
    $p=New-TempLedger;$c=Get-Candidate $p;$before=$c.Package|ConvertTo-Json -Depth 6 -Compress;$b=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-RecoveryResult Converged);Assert-Eq $before ($c.Package|ConvertTo-Json -Depth 6 -Compress) 'candidate changed';Assert-Eq Rejected $b.HistoricalPublication.Package.MutationState 'historical state';Assert-Eq 409 $b.HistoricalPublication.Package.StatusCode 'historical status';Assert-Eq Converged $b.Recovery.RecoveryState 'recovery state'
}
$results+=Invoke-Case H11 'Indeterminate plus Conflict preserves Indeterminate' {
    $d=[pscustomobject]@{Code='TransportUncertainty';Message='uncertain';StatusCode=$null};$p=New-TempLedger (New-Ledger -MutationState Indeterminate -StatusCode $null -Diagnostic $d -Conclusion StoppedIndeterminate);$c=Get-Candidate $p;$b=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-RecoveryResult Conflict);Assert-Eq Indeterminate $b.HistoricalPublication.Package.MutationState 'historical state';Assert-Eq Conflict $b.Recovery.RecoveryState 'recovery state'
}
$results+=Invoke-Case H12 'Attempting plus Converged preserves Attempting' {
    $p=New-TempLedger (New-Ledger -MutationState Attempting -StatusCode $null -Diagnostic $null -LedgerState InProgress -Conclusion $null);$c=Get-Candidate $p;$b=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-RecoveryResult Converged);Assert-Eq Attempting $b.HistoricalPublication.Package.MutationState 'historical state';Assert-Eq Converged $b.Recovery.RecoveryState 'recovery state'
}
$results+=Invoke-Case H13 'recovery binding preserves publication lifecycle fields' {
    $p=New-TempLedger;$c=Get-Candidate $p;$b=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-RecoveryResult Converged);Assert-Eq Terminal $b.HistoricalPublication.Operation.LedgerState 'ledger state';Assert-Eq StoppedRejected $b.HistoricalPublication.Operation.OperationConclusion 'conclusion';Assert-Eq '2026-09-24T00:00:00.0000000+00:00' $b.HistoricalPublication.Operation.StartedAtUtc 'started';Assert-Eq '2026-09-24T00:00:05.0000000+00:00' $b.HistoricalPublication.Operation.CompletedAtUtc 'completed'
}
$results+=Invoke-Case H14 'Unresolved is valid supplemental recovery terminal state' {
    $p=New-TempLedger;$c=Get-Candidate $p;$b=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-RecoveryResult Unresolved);Assert-Eq Unresolved $b.Recovery.RecoveryState 'recovery state';Assert-Eq Rejected $b.HistoricalPublication.Package.MutationState 'historical state'
}
$results+=Invoke-Case H15 'observation history is supplemental evidence' {
    $p=New-TempLedger;$c=Get-Candidate $p;$obs=@([pscustomobject]@{State='NotObservable'},[pscustomobject]@{State='Equivalent'});$b=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-RecoveryResult Converged) -Observations $obs;Assert-Eq 2 $b.Recovery.Observations.Count 'observation count';Assert-Eq NotObservable $b.Recovery.Observations[0].State 'first';Assert-Eq Equivalent $b.Recovery.Observations[1].State 'second'
}
$results+=Invoke-Case H16 'nonterminal recovery algebra state cannot be bound' {
    $p=New-TempLedger;$c=Get-Candidate $p;$r=[pscustomobject]@{RecoveryState='Uncertain';Terminal=$false};$thrown=$false;try{New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult $r|Out-Null}catch{$thrown=$true};Assert-True $thrown 'Uncertain bound as terminal evidence'
}
$results+=Invoke-Case H17 'classification has no age or timeout authority' {
    $names=@((Get-Command Get-NuGetRecoveryCandidate).Parameters.Keys)
    $forbidden=@('Age','AgeThreshold','StaleAfter','StaleAfterUtc','Timeout','TimeoutSeconds','Deadline','DeadlineUtc','PollInterval','PollCount','Duration','RecoveryWindow')
    Assert-True (@($names|Where-Object{$forbidden -contains $_}).Count-eq 0) 'age/timing classification seam exposed'
}
$results+=Invoke-Case H18 'RP-3C.3 exposes no publication or credential capability' {
    foreach($command in @('Get-NuGetRecoveryCandidate','New-NuGetRecoveryEvidenceBinding')){$names=@((Get-Command $command).Parameters.Keys);Assert-True (@($names|Where-Object{$_ -match '(?i)publish|put|push|apikey|credential|endpoint|retry|remutat'}).Count-eq 0) "$command exposes mutation capability"}
}
$results+=Invoke-Case H19 'load classify and bind never rewrite persisted ledger bytes' {
    $p=New-TempLedger;$before=[IO.File]::ReadAllBytes($p);$c=Get-Candidate $p;$null=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-RecoveryResult Converged);$after=[IO.File]::ReadAllBytes($p);Assert-Eq ([Convert]::ToBase64String($before)) ([Convert]::ToBase64String($after)) 'persisted ledger changed'
}
$results+=Invoke-Case H20 'binding does not mutate candidate evidence' {
    $p=New-TempLedger;$c=Get-Candidate $p;$before=$c|ConvertTo-Json -Depth 8 -Compress;$null=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-RecoveryResult Conflict);Assert-Eq $before ($c|ConvertTo-Json -Depth 8 -Compress) 'candidate changed'
}

$results|Format-Table Id,Name,Outcome -AutoSize
$failed=@($results|Where-Object{$_.Outcome-ne'PASS'})
if($failed.Count-gt 0){$failed|Format-List *;throw "RP-3C.3 CONFORMANCE FAILED: $($failed.Count) case(s)."}
"`nRP-3C.3 CONFORMANCE: $($results.Count) / $($results.Count) PASS"
