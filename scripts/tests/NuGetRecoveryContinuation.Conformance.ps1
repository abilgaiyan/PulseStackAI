Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts/NuGetRecoveryEvidence.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetRecoveryContinuation.ps1')

function Assert-Eq($Expected,$Actual,[string]$Message){if($Expected-cne$Actual){throw "$Message Expected '$Expected', actual '$Actual'."}}
function Assert-True([bool]$Condition,[string]$Message){if(-not$Condition){throw $Message}}
function Invoke-Case([string]$Id,[string]$Name,[scriptblock]$Body){try{&$Body;[pscustomobject]@{Id=$Id;Name=$Name;Outcome='PASS'}}catch{[pscustomobject]@{Id=$Id;Name=$Name;Outcome='FAIL';Error=$_.Exception.Message}}}

$operationId='00000000-0000-0000-0000-000000000401'
$version='1.0.4-test.1'
$ids=@('Pkg.A','Pkg.B','Pkg.C','Pkg.D','Pkg.E')

function New-Package {
    param([int]$Index,[string]$State,[AllowNull()][object]$StatusCode=$null,[AllowNull()][object]$Diagnostic=$null)
    $hex=($Index+1).ToString('x')
    [pscustomobject]@{id=$ids[$Index];version=$version;admittedSha256=($hex*64);mutationState=$State;statusCode=$StatusCode;diagnostic=$Diagnostic}
}
function New-Rejected409([int]$Index){New-Package -Index $Index -State Rejected -StatusCode 409 -Diagnostic ([pscustomobject]@{Code='ExistingIdentityConflict';Message='exists';StatusCode=409})}
function New-Indeterminate([int]$Index){New-Package -Index $Index -State Indeterminate -Diagnostic ([pscustomobject]@{Code='TransportUncertainty';Message='uncertain';StatusCode=$null})}
function New-Attempting([int]$Index){New-Package -Index $Index -State Attempting}

function New-Ledger {
    param([int]$Boundary=2,[string]$BoundaryKind='Rejected409',[object[]]$OverridePackages=$null)
    if($null-ne$OverridePackages){$packages=@($OverridePackages)}else{
        $packages=@()
        for($i=0;$i-lt$ids.Count;$i++){
            if($i-lt$Boundary){$packages+=New-Package -Index $i -State Accepted -StatusCode 201}
            elseif($i-eq$Boundary){$packages+=switch($BoundaryKind){'Rejected409'{New-Rejected409 $i}'Indeterminate'{New-Indeterminate $i}'Attempting'{New-Attempting $i}default{throw 'bad boundary kind'}}}
            else{$packages+=New-Package -Index $i -State NotAttempted}
        }
    }
    $ledgerState=if($BoundaryKind-ceq'Attempting'){'InProgress'}else{'Terminal'}
    $conclusion=if($BoundaryKind-ceq'Indeterminate'){'StoppedIndeterminate'}elseif($BoundaryKind-ceq'Attempting'){$null}else{'StoppedRejected'}
    $completed=if($BoundaryKind-ceq'Attempting'){$null}else{'2026-09-25T00:00:05Z'}
    [pscustomobject]@{schemaVersion='1.0';operationId=$operationId;ledgerState=$ledgerState;registry='NuGet.org';startedAtUtc='2026-09-25T00:00:00Z';completedAtUtc=$completed;operationConclusion=$conclusion;packages=@($packages)}
}
function New-TempLedger([object]$Ledger=(New-Ledger)){
    $dir=Join-Path ([IO.Path]::GetTempPath()) ('pulsestack-rp3c4-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $dir -Force|Out-Null
    $path=Join-Path $dir 'publication-result.json';[IO.File]::WriteAllText($path,($Ledger|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false));$path
}
function Get-Candidate([string]$Path,[int]$Boundary=2){$hex=($Boundary+1).ToString('x');Get-NuGetRecoveryCandidate -LedgerPath $Path -OperationId $operationId -PackageId $ids[$Boundary] -PackageVersion $version -AdmittedSha256 ($hex*64)}
function New-RecoveryResult([string]$State='Converged'){[pscustomobject]@{RecoveryState=$State;Terminal=$true;StartedAtUtc=[DateTimeOffset]'2026-09-25T00:10:00Z';DeadlineUtc=[DateTimeOffset]'2026-09-25T00:10:30Z';ObservationCount=1;LastObservation=[pscustomobject]@{State=if($State-ceq'Converged'){'Equivalent'}elseif($State-ceq'Conflict'){'Different'}else{'Indeterminate'}}}}
function Get-Decision([string]$Path,[int]$Boundary=2,[string]$State='Converged'){$c=Get-Candidate $Path $Boundary;$b=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-RecoveryResult $State);Get-NuGetWholeOperationContinuationDecision -LedgerPath $Path -Candidate $c -RecoveryEvidence $b}
function Invoke-InvalidShape([object[]]$Packages,[int]$Boundary){$p=New-TempLedger (New-Ledger -OverridePackages $Packages);$c=Get-Candidate $p $Boundary;$b=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-RecoveryResult Converged);$thrown=$false;try{Get-NuGetWholeOperationContinuationDecision -LedgerPath $p -Candidate $c -RecoveryEvidence $b|Out-Null}catch{$thrown=$true};$thrown}

$results=@()
$results+=Invoke-Case W01 'Converged permits immediate canonical successor' {$p=New-TempLedger;$d=Get-Decision $p;Assert-Eq $true $d.MayContinue 'continue';Assert-Eq 2 $d.RecoveryPackageIndex 'boundary';Assert-Eq 3 $d.NextPackageIndex 'next';Assert-Eq 'Pkg.D' $d.NextPackage.Id 'next id';Assert-Eq ContinuationEligible $d.WholeOperationDisposition 'disposition'}
$results+=Invoke-Case W02 'Conflict stops and exposes no continuation package' {$p=New-TempLedger;$d=Get-Decision $p 2 Conflict;Assert-Eq $false $d.MayContinue 'continue';Assert-Eq $true $d.HasNextPackage 'structural successor';Assert-Eq $null $d.NextPackageIndex 'next index';Assert-Eq $null $d.NextPackage 'next package';Assert-Eq StoppedConflict $d.WholeOperationDisposition 'disposition'}
$results+=Invoke-Case W03 'Unresolved stops and exposes no continuation package' {$p=New-TempLedger;$d=Get-Decision $p 2 Unresolved;Assert-Eq $false $d.MayContinue 'continue';Assert-Eq $null $d.NextPackage 'next package';Assert-Eq StoppedUnresolved $d.WholeOperationDisposition 'disposition'}
$results+=Invoke-Case W04 'final-package Converged yields RecoveredEnd' {$p=New-TempLedger (New-Ledger -Boundary 4);$d=Get-Decision $p 4 Converged;Assert-Eq $false $d.MayContinue 'continue';Assert-Eq $false $d.HasNextPackage 'has next';Assert-Eq $null $d.NextPackage 'next';Assert-Eq RecoveredEnd $d.WholeOperationDisposition 'disposition'}
$results+=Invoke-Case W05 'first package may be recovery boundary' {$p=New-TempLedger (New-Ledger -Boundary 0);$d=Get-Decision $p 0 Converged;Assert-Eq 0 $d.RecoveryPackageIndex 'boundary';Assert-Eq 1 $d.NextPackageIndex 'next';Assert-Eq 'Pkg.B' $d.NextPackage.Id 'next id'}
$results+=Invoke-Case W06 'Indeterminate boundary participates' {$p=New-TempLedger (New-Ledger -Boundary 1 -BoundaryKind Indeterminate);$d=Get-Decision $p 1 Converged;Assert-Eq Indeterminate $d.RecoveryPackage.MutationState 'state';Assert-Eq 2 $d.NextPackageIndex 'next'}
$results+=Invoke-Case W07 'Attempting boundary participates without age authority' {$p=New-TempLedger (New-Ledger -Boundary 1 -BoundaryKind Attempting);$d=Get-Decision $p 1 Converged;Assert-Eq Attempting $d.RecoveryPackage.MutationState 'state';Assert-Eq ContinuationEligible $d.WholeOperationDisposition 'disposition'}
$results+=Invoke-Case W08 'non-Accepted prefix is rejected' {$x=@();$x+=New-Package -Index 0 -State NotAttempted;$x+=New-Rejected409 1;$x+=New-Package -Index 2 -State NotAttempted;$x+=New-Package -Index 3 -State NotAttempted;$x+=New-Package -Index 4 -State NotAttempted;Assert-True (Invoke-InvalidShape $x 1) 'invalid prefix admitted'}
$results+=Invoke-Case W09 'non-NotAttempted suffix is rejected' {$x=@();$x+=New-Package -Index 0 -State Accepted -StatusCode 201;$x+=New-Rejected409 1;$x+=New-Package -Index 2 -State Accepted -StatusCode 201;$x+=New-Package -Index 3 -State NotAttempted;$x+=New-Package -Index 4 -State NotAttempted;Assert-True (Invoke-InvalidShape $x 1) 'invalid suffix admitted'}
$results+=Invoke-Case W10 'skipped gap before boundary is rejected' {$x=@();$x+=New-Package -Index 0 -State Accepted -StatusCode 201;$x+=New-Package -Index 1 -State NotAttempted;$x+=New-Rejected409 2;$x+=New-Package -Index 3 -State NotAttempted;$x+=New-Package -Index 4 -State NotAttempted;Assert-True (Invoke-InvalidShape $x 2) 'gap admitted'}
$results+=Invoke-Case W11 'candidate operation identity must match ledger' {$p=New-TempLedger;$c=Get-Candidate $p;$b=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-RecoveryResult);$c.Operation.OperationId='00000000-0000-0000-0000-000000000999';$thrown=$false;try{Get-NuGetWholeOperationContinuationDecision $p $c $b|Out-Null}catch{$thrown=$true};Assert-True $thrown 'operation mismatch admitted'}
$results+=Invoke-Case W12 'recovery binding must match exact package' {$p=New-TempLedger;$c=Get-Candidate $p;$b=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-RecoveryResult);$b.HistoricalPublication.Package.Id='Pkg.X';$thrown=$false;try{Get-NuGetWholeOperationContinuationDecision $p $c $b|Out-Null}catch{$thrown=$true};Assert-True $thrown 'binding mismatch admitted'}
$results+=Invoke-Case W13 'recovery binding trigger must match candidate' {$p=New-TempLedger;$c=Get-Candidate $p;$b=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-RecoveryResult);$b.Recovery.Trigger='IndeterminateMutation';$thrown=$false;try{Get-NuGetWholeOperationContinuationDecision $p $c $b|Out-Null}catch{$thrown=$true};Assert-True $thrown 'trigger mismatch admitted'}
$results+=Invoke-Case W14 'nonterminal recovery state is rejected' {$p=New-TempLedger;$c=Get-Candidate $p;$b=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-RecoveryResult);$b.Recovery.RecoveryState='Uncertain';$thrown=$false;try{Get-NuGetWholeOperationContinuationDecision $p $c $b|Out-Null}catch{$thrown=$true};Assert-True $thrown 'nonterminal state admitted'}
$results+=Invoke-Case W15 'next package remains NotAttempted' {$p=New-TempLedger;$d=Get-Decision $p;Assert-Eq 'Pkg.D' $d.NextPackage.Id 'next id';Assert-Eq NotAttempted $d.NextPackage.MutationState 'next state'}
$results+=Invoke-Case W16 'ordered-set logic is not fixed to ten packages' {$p=New-TempLedger;$d=Get-Decision $p;Assert-Eq 5 ((Read-NuGetPublicationLedger $p).packages.Count) 'fixture size';Assert-Eq ContinuationEligible $d.WholeOperationDisposition 'disposition'}
$results+=Invoke-Case W17 'analysis never rewrites persisted ledger bytes' {$p=New-TempLedger;$before=[IO.File]::ReadAllBytes($p);$null=Get-Decision $p;$after=[IO.File]::ReadAllBytes($p);Assert-Eq ([Convert]::ToBase64String($before)) ([Convert]::ToBase64String($after)) 'ledger changed'}
$results+=Invoke-Case W18 'decision exposes no publication or credential capability' {$names=@((Get-Command Get-NuGetWholeOperationContinuationDecision).Parameters.Keys);Assert-True (@($names|Where-Object{$_ -match '(?i)publish|put|push|apikey|credential|endpoint|retry|remutat|filepath'}).Count-eq 0) 'mutation capability exposed'}
$results+=Invoke-Case W19 'decision has no fixed package-count parameter' {$names=@((Get-Command Get-NuGetWholeOperationContinuationDecision).Parameters.Keys);Assert-True (@($names|Where-Object{$_ -match '(?i)packagecount|maxpackages|ten'}).Count-eq 0) 'fixed package count exposed'}
$results+=Invoke-Case W20 'decision does not mutate candidate or recovery evidence' {$p=New-TempLedger;$c=Get-Candidate $p;$b=New-NuGetRecoveryEvidenceBinding -Candidate $c -RecoveryResult (New-RecoveryResult);$cb=$c|ConvertTo-Json -Depth 8 -Compress;$bb=$b|ConvertTo-Json -Depth 8 -Compress;$null=Get-NuGetWholeOperationContinuationDecision $p $c $b;Assert-Eq $cb ($c|ConvertTo-Json -Depth 8 -Compress) 'candidate changed';Assert-Eq $bb ($b|ConvertTo-Json -Depth 8 -Compress) 'binding changed'}

$results|Format-Table Id,Name,Outcome -AutoSize
$failed=@($results|Where-Object{$_.Outcome-ne'PASS'})
if($failed.Count-gt 0){$failed|Format-List *;throw "RP-3C.4 CONFORMANCE FAILED: $($failed.Count) case(s)."}
"`nRP-3C.4 CONFORMANCE: $($results.Count) / $($results.Count) PASS"
