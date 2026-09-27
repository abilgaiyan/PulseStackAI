Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repositoryRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts/NuGetAdmittedReleaseIdentity.ps1')
. (Join-Path $repositoryRoot 'scripts/NuGetReleaseEvidenceClaims.ps1')
function Assert-Eq($Expected,$Actual,[string]$Message){if($Expected-cne$Actual){throw "$Message Expected '$Expected', actual '$Actual'."}}
function Assert-True([bool]$Value,[string]$Message){if(-not$Value){throw $Message}}
function Assert-Throws([scriptblock]$Body,[string]$Contains){try{&$Body;throw 'Expected exception was not thrown.'}catch{if($_.Exception.Message-eq'Expected exception was not thrown.'){throw};if($_.Exception.Message-notlike"*$Contains*"){throw "Unexpected exception: $($_.Exception.Message)"}}}
function Invoke-Case([string]$Id,[string]$Name,[scriptblock]$Body){try{&$Body;[pscustomobject]@{Id=$Id;Name=$Name;Outcome='PASS'}}catch{[pscustomobject]@{Id=$Id;Name=$Name;Outcome='FAIL';Error=$_.Exception.Message}}}
function New-Release {
 $set=[pscustomobject]@{ProductionKind='Release';SourceCommit='0123456789abcdef0123456789abcdef01234567';PackageVersion='1.0.4';ReleaseAuthorityTag='v1.0.4';Packages=@([pscustomobject]@{Id='A';Version='1.0.4';Sha256=('a'*64)},[pscustomobject]@{Id='B';Version='1.0.4';Sha256=('b'*64)},[pscustomobject]@{Id='C';Version='1.0.4';Sha256=('c'*64)})}
 $identity=Get-NuGetAdmittedReleaseIdentity $set
 $p=$identity.Projection;$p|Add-Member -NotePropertyName Sha256 -NotePropertyValue $identity.Sha256;return $p
}
function New-Ledger {
 param([object]$Release)
 [pscustomobject]@{operationId='00000000-0000-0000-0000-000000000001';sourceCommit=$Release.SourceCommit;packageVersion=$Release.PackageVersion;releaseAuthorityTag=$Release.ReleaseAuthorityTag;packages=@([pscustomobject]@{id='A';version='1.0.4';admittedSha256=('a'*64);mutationState='Accepted';statusCode=201;diagnostic=$null},[pscustomobject]@{id='B';version='1.0.4';admittedSha256=('b'*64);mutationState='Attempting';statusCode=$null;diagnostic=$null},[pscustomobject]@{id='C';version='1.0.4';admittedSha256=('c'*64);mutationState='NotAttempted';statusCode=$null;diagnostic=$null})}
}
$results=@()
$results+=Invoke-Case 'C101' 'original ledger produces one claim per canonical position' {$r=New-Release;$c=@(ConvertFrom-NuGetOriginalPublicationLedgerClaims (New-Ledger $r) $r);Assert-Eq '3' ([string]$c.Count) 'claim count';Assert-Eq '0' ([string]$c[0].PackageIndex) 'first index';Assert-Eq '2' ([string]$c[2].PackageIndex) 'last index'}
$results+=Invoke-Case 'C102' 'claim preserves exact release and package identity' {$r=New-Release;$c=@(ConvertFrom-NuGetOriginalPublicationLedgerClaims (New-Ledger $r) $r);Assert-Eq $r.Sha256 $c[1].ReleaseIdentitySha256 'release identity';Assert-Eq 'B' $c[1].PackageId 'id';Assert-Eq ('b'*64) $c[1].AdmittedSha256 'sha'}
$results+=Invoke-Case 'C103' 'claim preserves raw mutation state without D2 reduction' {$r=New-Release;$c=@(ConvertFrom-NuGetOriginalPublicationLedgerClaims (New-Ledger $r) $r);Assert-Eq 'Accepted' $c[0].RawMutationState 'accepted';Assert-Eq 'Attempting' $c[1].RawMutationState 'attempting';Assert-Eq 'NotAttempted' $c[2].RawMutationState 'not attempted';Assert-True ($null -eq $c[0].PSObject.Properties['EffectiveState']) 'normalizer performed D2 reduction'}
$results+=Invoke-Case 'C104' 'canonical position comes from persisted ledger order' {$r=New-Release;$l=New-Ledger $r;$tmp=$l.packages[0];$l.packages[0]=$l.packages[1];$l.packages[1]=$tmp;Assert-Throws {ConvertFrom-NuGetOriginalPublicationLedgerClaims $l $r} 'canonical index 0'}
$results+=Invoke-Case 'C105' 'package count mismatch rejects evidence for release' {$r=New-Release;$l=New-Ledger $r;$l.packages=@($l.packages[0],$l.packages[1]);Assert-Throws {ConvertFrom-NuGetOriginalPublicationLedgerClaims $l $r} 'package count'}
$results+=Invoke-Case 'C106' 'provenance mismatch rejects evidence for release' {$r=New-Release;$l=New-Ledger $r;$l.releaseAuthorityTag='v9.9.9';Assert-Throws {ConvertFrom-NuGetOriginalPublicationLedgerClaims $l $r} 'provenance does not belong'}
$results+=Invoke-Case 'C107' 'pre-RP-5 ledger without identity remains structurally validatable' {$r=New-Release;$l=New-Ledger $r;$c=@(ConvertFrom-NuGetOriginalPublicationLedgerClaims $l $r);Assert-Eq 'OriginalPublication' $c[0].EvidenceSource 'source'}
$results+=Invoke-Case 'C108' 'future identity-bearing ledger must match R' {$r=New-Release;$l=New-Ledger $r;$l|Add-Member releaseIdentityProfile $r.Profile;$l|Add-Member releaseIdentitySha256 $r.Sha256;$c=@(ConvertFrom-NuGetOriginalPublicationLedgerClaims $l $r);Assert-Eq $r.Sha256 $c[0].ReleaseIdentitySha256 'identity'}
$results+=Invoke-Case 'C109' 'mismatched future identity is rejected' {$r=New-Release;$l=New-Ledger $r;$l|Add-Member releaseIdentityProfile $r.Profile;$l|Add-Member releaseIdentitySha256 ('d'*64);Assert-Throws {ConvertFrom-NuGetOriginalPublicationLedgerClaims $l $r} 'release identity does not match'}
$results+=Invoke-Case 'C110' 'partial future identity is rejected' {$r=New-Release;$l=New-Ledger $r;$l|Add-Member releaseIdentityProfile $r.Profile;Assert-Throws {ConvertFrom-NuGetOriginalPublicationLedgerClaims $l $r} 'incomplete release identity'}
$results|Format-Table -AutoSize
$failed=@($results|Where-Object Outcome -eq 'FAIL');if($failed.Count){$failed|ForEach-Object{Write-Host "$($_.Id) $($_.Name): $($_.Error)" -ForegroundColor Red};throw "RP-5C.1 release evidence claim conformance failed: $($failed.Count) case(s)."};Write-Host "RP-5C.1 release evidence claim conformance passed: $($results.Count)/$($results.Count)."
