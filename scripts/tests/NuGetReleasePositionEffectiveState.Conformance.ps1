Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repositoryRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts/NuGetReleasePositionEffectiveState.ps1')
function Assert-Eq($Expected,$Actual,[string]$Message){if($Expected-cne$Actual){throw "$Message Expected '$Expected', actual '$Actual'."}}
function Assert-Throws([scriptblock]$Body,[string]$Contains){try{&$Body;throw 'Expected exception was not thrown.'}catch{if($_.Exception.Message-eq'Expected exception was not thrown.'){throw};if($_.Exception.Message-notlike"*$Contains*"){throw "Unexpected exception: $($_.Exception.Message)"}}}
function Invoke-Case([string]$Id,[string]$Name,[scriptblock]$Body){try{&$Body;[pscustomobject]@{Id=$Id;Name=$Name;Outcome='PASS'}}catch{[pscustomobject]@{Id=$Id;Name=$Name;Outcome='FAIL';Error=$_.Exception.Message}}}
$R=('f'*64)
function New-Claim([string]$Mutation,[string]$Disposition='PublicationMutation',[string]$Recovery=$null,[int]$Index=1,[string]$ReleaseSha=$R,[string]$Id='B',[int]$Status=0,[string]$Code=$null){
 $diag=if($null-eq$Code){$null}else{[pscustomobject]@{Code=$Code}}
 [pscustomobject]@{ReleaseIdentityProfile='1';ReleaseIdentitySha256=$ReleaseSha;PackageIndex=$Index;PackageId=$Id;PackageVersion='1.0.4';AdmittedSha256=('b'*64);EvidenceSource='Fixture';OperationId=[guid]::NewGuid().ToString();HistoricalPublicationOperationId=$null;RawDisposition=$Disposition;RawMutationState=$Mutation;RecoveryState=$Recovery;StatusCode=if($Status-eq0){$null}else{$Status};Diagnostic=$diag;Evidence=$null}
}
function Reduce([object[]]$Claims){Get-NuGetReleasePositionEffectiveState -ReleaseIdentityProfile '1' -ReleaseIdentitySha256 $R -PackageIndex 1 -Claims $Claims}
$results=@()
$results+=Invoke-Case 'C401' 'direct Accepted satisfies position' {Assert-Eq 'Satisfied' (Reduce @((New-Claim 'Accepted'))).EffectiveState 'state'}
$results+=Invoke-Case 'C402' 'recovered Converged satisfies position' {Assert-Eq 'Satisfied' (Reduce @((New-Claim 'Indeterminate'),(New-Claim 'Indeterminate' 'Recovery' 'Converged'))).EffectiveState 'state'}
$results+=Invoke-Case 'C403' 'NotAttempted alone is Unsatisfied' {Assert-Eq 'Unsatisfied' (Reduce @((New-Claim 'NotAttempted'))).EffectiveState 'state'}
$results+=Invoke-Case 'C404' 'Attempting is Indeterminate' {Assert-Eq 'Indeterminate' (Reduce @((New-Claim 'Attempting'))).EffectiveState 'state'}
$results+=Invoke-Case 'C405' 'Indeterminate mutation is Indeterminate' {Assert-Eq 'Indeterminate' (Reduce @((New-Claim 'Indeterminate'))).EffectiveState 'state'}
$results+=Invoke-Case 'C406' 'recoverable 409 remains Indeterminate before reconciliation' {Assert-Eq 'Indeterminate' (Reduce @((New-Claim 'Rejected' 'PublicationMutation' $null 1 $R 'B' 409 'ExistingIdentityConflict'))).EffectiveState 'state'}
$results+=Invoke-Case 'C407' 'recovery Unresolved is Indeterminate' {$p=New-Claim 'Attempting';$r=New-Claim 'Attempting' 'Recovery' 'Unresolved';Assert-Eq 'Indeterminate' (Reduce @($p,$r)).EffectiveState 'state'}
$results+=Invoke-Case 'C408' 'nonrecoverable Rejected is Blocked' {Assert-Eq 'Blocked' (Reduce @((New-Claim 'Rejected' 'PublicationMutation' $null 1 $R 'B' 400))).EffectiveState 'state'}
$results+=Invoke-Case 'C409' 'recovery Conflict is Blocked' {$p=New-Claim 'Indeterminate';$r=New-Claim 'Indeterminate' 'Recovery' 'Conflict';Assert-Eq 'Blocked' (Reduce @($p,$r)).EffectiveState 'state'}
$results+=Invoke-Case 'C410' 'satisfaction is monotonic over unresolved publication evidence' {$claims=@((New-Claim 'Accepted'),(New-Claim 'Attempting'),(New-Claim 'Indeterminate'),(New-Claim 'Rejected' 'PublicationMutation' $null 1 $R 'B' 409 'ExistingIdentityConflict'),(New-Claim 'Attempting' 'Recovery' 'Unresolved'));Assert-Eq 'Satisfied' (Reduce $claims).EffectiveState 'state'}
$results+=Invoke-Case 'C411' 'terminal conflict dominates otherwise valid satisfaction' {$claims=@((New-Claim 'Accepted'),(New-Claim 'Indeterminate' 'Recovery' 'Conflict'));Assert-Eq 'Blocked' (Reduce $claims).EffectiveState 'state'}
$results+=Invoke-Case 'C412' 'nonrecoverable rejection dominates otherwise valid satisfaction' {$claims=@((New-Claim 'Accepted'),(New-Claim 'Rejected' 'PublicationMutation' $null 1 $R 'B' 400));Assert-Eq 'Blocked' (Reduce $claims).EffectiveState 'state'}
$results+=Invoke-Case 'C413' 'equivalent satisfying proofs coexist' {$claims=@((New-Claim 'Accepted'),(New-Claim 'Attempting' 'Recovery' 'Converged'));Assert-Eq 'Satisfied' (Reduce $claims).EffectiveState 'state'}
$results+=Invoke-Case 'C414' 'input ordering has no precedence authority' {$a=@((New-Claim 'Accepted'),(New-Claim 'Attempting'),(New-Claim 'Attempting' 'Recovery' 'Unresolved'));$b=@($a[2],$a[0],$a[1]);Assert-Eq (Reduce $a).EffectiveState (Reduce $b).EffectiveState 'order independence'}
$results+=Invoke-Case 'C415' 'claim from another release is rejected' {$claims=@((New-Claim 'Accepted'),(New-Claim 'Accepted' 'PublicationMutation' $null 1 ('e'*64)));Assert-Throws {Reduce $claims} 'exactly the requested release identity and package position'}
$results+=Invoke-Case 'C416' 'claim from another position is rejected' {Assert-Throws {Reduce @((New-Claim 'Accepted' 'PublicationMutation' $null 2))} 'exactly the requested release identity and package position'}
$results+=Invoke-Case 'C417' 'package identity disagreement is rejected' {$claims=@((New-Claim 'Accepted'),(New-Claim 'Accepted' 'PublicationMutation' $null 1 $R 'C'));Assert-Throws {Reduce $claims} 'disagree on exact package identity'}
$results+=Invoke-Case 'C418' 'nonterminal recovery disposition is rejected' {$c=New-Claim 'Attempting' 'Recovery' 'Pending';Assert-Throws {Reduce @($c)} 'terminal recovery state'}
$results+=Invoke-Case 'C419' 'recovery cannot attach to nonrecoverable rejection' {$c=New-Claim 'Rejected' 'Recovery' 'Converged' 1 $R 'B' 400;Assert-Throws {Reduce @($c)} 'non-recoverable rejection'}
$results+=Invoke-Case 'C420' 'raw claims are not mutated by reduction' {$c=New-Claim 'Accepted';$before=@($c.PSObject.Properties.Name);$null=Reduce @($c);$after=@($c.PSObject.Properties.Name);Assert-Eq ([string]$before.Count) ([string]$after.Count) 'property count';Assert-Eq 'Accepted' $c.RawMutationState 'raw state'}
$results+=Invoke-Case 'C421' 'reducer output remains position-local and has no mutation authority' {$o=Reduce @((New-Claim 'Accepted'));Assert-Eq '1' ([string]$o.PackageIndex) 'position';if($null-ne$o.PSObject.Properties['NextPackage']-or$null-ne$o.PSObject.Properties['ContinuationGrant']-or$null-ne$o.PSObject.Properties['MutationAuthority']){throw 'Reducer leaked D3 or mutation authority.'}}
$results|Format-Table -AutoSize
$failed=@($results|Where-Object Outcome -eq 'FAIL');if($failed.Count){$failed|ForEach-Object{Write-Host "$($_.Id) $($_.Name): $($_.Error)" -ForegroundColor Red};throw "RP-5C.4 position effective-state conformance failed: $($failed.Count) case(s)."};Write-Host "RP-5C.4 position effective-state conformance passed: $($results.Count)/$($results.Count)."
