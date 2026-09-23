Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts/NuGetRemoteEquivalence.ps1')

$serviceIndex='https://unit.test/v3/index.json'
$base='https://content.unit.test/flat/'
$bytes=[Text.Encoding]::UTF8.GetBytes('canonical-package-bytes')
$hash=Get-Sha256Hex -Bytes $bytes
$package=[pscustomobject]@{ Id='PulseStack.Core'; Version='1.0.4-RC.1'; FilePath='C:\fixture\PulseStack.Core.nupkg'; Sha256=$hash }

function New-Index([string]$Address=$base,[switch]$Missing) {
    $resources=if($Missing){@([ordered]@{'@id'='https://unit.test/search';'@type'='SearchQueryService/3.5.0'})}else{@([ordered]@{'@id'=$Address;'@type'='PackageBaseAddress/3.0.0'})}
    ([ordered]@{version='3.0.0';resources=$resources}|ConvertTo-Json -Depth 4 -Compress)
}
function New-Transport {
    param([int]$Status=200,[byte[]]$Body=$bytes,[switch]$ThrowContent,[switch]$ThrowIndex,[string]$Index=(New-Index),[int]$IndexStatus=200,[Collections.Generic.List[object]]$Requests)
    $state=[pscustomobject]@{Count=0;Status=$Status;Body=$Body;ThrowContent=$ThrowContent;ThrowIndex=$ThrowIndex;Index=$Index;IndexStatus=$IndexStatus;Requests=$Requests}
    return { param($request)
        if($null-ne $state.Requests){$state.Requests.Add($request)}
        $state.Count++
        if($state.Count-eq 1){if($state.ThrowIndex){throw 'index failure'};return [pscustomobject]@{StatusCode=$state.IndexStatus;Content=$state.Index;Bytes=$null}}
        if($state.ThrowContent){throw 'content failure'}
        [pscustomobject]@{StatusCode=$state.Status;Content=$null;Bytes=$state.Body}
    }.GetNewClosure()
}
function Assert-Eq($e,$a,[string]$m){if($e-cne$a){throw "$m Expected '$e', actual '$a'."}}
function Assert-True([bool]$c,[string]$m){if(-not$c){throw $m}}
function Invoke-Case([string]$Id,[string]$Name,[scriptblock]$Body){try{&$Body;[pscustomobject]@{Id=$Id;Name=$Name;Outcome='PASS'}}catch{[pscustomobject]@{Id=$Id;Name=$Name;Outcome='FAIL';Error=$_.Exception.Message}}}
function Invoke-Observed([scriptblock]$Transport,[object]$P=$package,[object]$Context=$null){Invoke-NuGetRemoteEquivalence -Package $P -ServiceIndexUri $serviceIndex -RecoveryContext $Context -Request $Transport -Clock { [datetime]'2026-09-24T00:00:00Z' }}

$results=@()
$results+=Invoke-Case E01 '200 equal SHA is Equivalent' { $r=Invoke-Observed (New-Transport);Assert-Eq Equivalent $r.State 'state';Assert-Eq $hash $r.RemoteSha256 'remote hash' }
$results+=Invoke-Case E02 '200 different SHA is Different' { $r=Invoke-Observed (New-Transport -Body ([byte[]](1,2,3)));Assert-Eq Different $r.State 'state' }
$results+=Invoke-Case E03 '404 is NotObservable' { $r=Invoke-Observed (New-Transport -Status 404 -Body $null);Assert-Eq NotObservable $r.State 'state' }
$results+=Invoke-Case E04 'content transport failure is Indeterminate' { $r=Invoke-Observed (New-Transport -ThrowContent);Assert-Eq Indeterminate $r.State 'state' }
$results+=Invoke-Case E05 '429 is Indeterminate' { $r=Invoke-Observed (New-Transport -Status 429 -Body $null);Assert-Eq Indeterminate $r.State 'state';Assert-Eq RateLimited $r.Diagnostic.Code 'diagnostic' }
$results+=Invoke-Case E06 '5xx is Indeterminate' { $r=Invoke-Observed (New-Transport -Status 503 -Body $null);Assert-Eq Indeterminate $r.State 'state' }
$results+=Invoke-Case E07 'unexpected status is Indeterminate' { $r=Invoke-Observed (New-Transport -Status 401 -Body $null);Assert-Eq Indeterminate $r.State 'state' }
$results+=Invoke-Case E08 'service index transport failure stops content GET' { $q=[Collections.Generic.List[object]]::new();$r=Invoke-Observed (New-Transport -ThrowIndex -Requests $q);Assert-Eq Indeterminate $r.State 'state';Assert-Eq 1 $q.Count 'requests' }
$results+=Invoke-Case E09 'malformed service index stops content GET' { $q=[Collections.Generic.List[object]]::new();$r=Invoke-Observed (New-Transport -Index '{bad' -Requests $q);Assert-Eq Indeterminate $r.State 'state';Assert-Eq 1 $q.Count 'requests' }
$results+=Invoke-Case E10 'missing PackageBaseAddress stops content GET' { $q=[Collections.Generic.List[object]]::new();$r=Invoke-Observed (New-Transport -Index (New-Index -Missing) -Requests $q);Assert-Eq Indeterminate $r.State 'state';Assert-Eq 1 $q.Count 'requests' }
$results+=Invoke-Case E11 'discovered PackageBaseAddress drives content request' { $q=[Collections.Generic.List[object]]::new();$r=Invoke-Observed (New-Transport -Requests $q);Assert-True $q[1].Uri.StartsWith($base) 'discovered base not used' }
$results+=Invoke-Case E12 'URI applies NuGet normalization then lowercase' { $u=Get-NuGetRemotePackageContentUri -BaseAddress $base -Id 'PulseStack.Core' -Version '01.02.003.0-RC.01';Assert-Eq ($base+'pulsestack.core/1.2.3-rc.1/pulsestack.core.1.2.3-rc.1.nupkg') $u 'normalized URI';$u2=Get-NuGetRemotePackageContentUri -BaseAddress $base -Id 'Mixed.ID' -Version '1.0.4-preview.1';Assert-True ($u2.Contains('mixed.id/1.0.4-preview.1/')) 'lowercase identity' }
$results+=Invoke-Case E13 'admitted SHA is preserved exactly' { $r=Invoke-Observed (New-Transport);Assert-Eq $hash $r.AdmittedSha256 'admitted hash' }
$results+=Invoke-Case E14 'RemoteSha256 exists only after successful hash' { $a=Invoke-Observed (New-Transport);$b=Invoke-Observed (New-Transport -Status 404 -Body $null);Assert-True (-not [string]::IsNullOrWhiteSpace($a.RemoteSha256)) 'missing hash';Assert-True ($null-eq$b.RemoteSha256) 'unexpected hash' }
$results+=Invoke-Case E15 '200 without complete bytes is Indeterminate' { $r=Invoke-Observed (New-Transport -Body $null);Assert-Eq Indeterminate $r.State 'state';Assert-Eq ContentUnreadable $r.Diagnostic.Code 'diagnostic' }
$results+=Invoke-Case E16 'operation exposes no credential seam' { $names=@((Get-Command Invoke-NuGetRemoteEquivalence).Parameters.Keys);Assert-True (-not($names -contains 'ApiKey')) 'ApiKey seam exists';Assert-True (-not($names -contains 'CredentialSource')) 'credential seam exists' }
$results+=Invoke-Case E17 'registry mismatch is rejected before observation' { $q=[Collections.Generic.List[object]]::new();$ctx=[pscustomobject]@{Registry='https://other.test/v3/index.json'};$thrown=$false;try{Invoke-Observed (New-Transport -Requests $q) $package $ctx|Out-Null}catch{$thrown=$true};Assert-True $thrown 'mismatch not rejected';Assert-Eq 0 $q.Count 'remote requests' }
$results+=Invoke-Case E18 'Equivalent does not rewrite prior 409 evidence' { $prior=[pscustomobject]@{Registry=$serviceIndex;MutationState='Rejected';StatusCode=409;Diagnostic='ExistingIdentityConflict'};$before=$prior|ConvertTo-Json -Compress;$r=Invoke-Observed (New-Transport) $package $prior;Assert-Eq Equivalent $r.State 'state';Assert-Eq $before ($prior|ConvertTo-Json -Compress) 'prior evidence changed' }
$results+=Invoke-Case E19 'Equivalent does not rewrite prior Attempting evidence' { $prior=[pscustomobject]@{Registry=$serviceIndex;MutationState='Attempting';StatusCode=$null};$before=$prior|ConvertTo-Json -Compress;$r=Invoke-Observed (New-Transport) $package $prior;Assert-Eq Equivalent $r.State 'state';Assert-Eq $before ($prior|ConvertTo-Json -Compress) 'prior evidence changed' }
$results+=Invoke-Case E20 'Different performs no mutation or remediation' { $q=[Collections.Generic.List[object]]::new();$r=Invoke-Observed (New-Transport -Body ([byte[]](9,8,7)) -Requests $q);Assert-Eq Different $r.State 'state';Assert-Eq 2 $q.Count 'requests';Assert-True (@($q|Where-Object{$_.Method-ne'GET'}).Count-eq 0) 'mutation request emitted' }
$results+=Invoke-Case E21 'only GET requests are emitted' { $q=[Collections.Generic.List[object]]::new();Invoke-Observed (New-Transport -Requests $q)|Out-Null;Assert-Eq 2 $q.Count 'requests';foreach($x in $q){Assert-Eq GET $x.Method 'method'} }
$results+=Invoke-Case E22 'later observation may change NotObservable to Equivalent' { $prior=[pscustomobject]@{Registry=$serviceIndex;Observation='NotObservable'};$before=$prior|ConvertTo-Json -Compress;$a=Invoke-Observed (New-Transport -Status 404 -Body $null) $package $prior;$b=Invoke-Observed (New-Transport) $package $prior;Assert-Eq NotObservable $a.State 'first';Assert-Eq Equivalent $b.State 'second';Assert-Eq $before ($prior|ConvertTo-Json -Compress) 'prior observation changed' }

$results|Format-Table Id,Name,Outcome -AutoSize
$failed=@($results|Where-Object{$_.Outcome-ne'PASS'})
if($failed.Count-gt 0){$failed|Format-List *;throw "RP-3B CONFORMANCE FAILED: $($failed.Count) case(s)."}
"`nRP-3B CONFORMANCE: $($results.Count) / $($results.Count) PASS"
