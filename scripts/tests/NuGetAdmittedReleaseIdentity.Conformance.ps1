Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts/NuGetAdmittedReleaseIdentity.ps1')

function Assert-Eq($Expected,$Actual,[string]$Message){if($Expected-cne$Actual){throw "$Message Expected '$Expected', actual '$Actual'."}}
function Assert-Ne($Left,$Right,[string]$Message){if($Left-ceq$Right){throw "$Message Values unexpectedly matched '$Left'."}}
function Assert-True([bool]$Value,[string]$Message){if(-not$Value){throw $Message}}
function Invoke-Case([string]$Id,[string]$Name,[scriptblock]$Body){try{&$Body;[pscustomobject]@{Id=$Id;Name=$Name;Outcome='PASS'}}catch{[pscustomobject]@{Id=$Id;Name=$Name;Outcome='FAIL';Error=$_.Exception.Message}}}
function Assert-Throws([scriptblock]$Body,[string]$Contains){try{&$Body;throw 'Expected exception was not thrown.'}catch{if($_.Exception.Message-eq'Expected exception was not thrown.'){throw};if($_.Exception.Message-notlike"*$Contains*"){throw "Unexpected exception: $($_.Exception.Message)"}}}

function New-ReferenceSet([string]$Tag='v1.0.4') {
    [pscustomobject]@{
        ProductionKind='Release'
        SourceCommit='0123456789abcdef0123456789abcdef01234567'
        PackageVersion='1.0.4'
        ReleaseAuthorityTag=$Tag
        Packages=@(
            [pscustomobject]@{Id='PulseStack.Core';Version='1.0.4';Sha256=('a'*64)},
            [pscustomobject]@{Id='PulseStack.Agents';Version='1.0.4';Sha256=('b'*64)}
        )
    }
}

# Keep this test source ASCII-only so Windows PowerShell 5.1 does not reinterpret
# a UTF-8-without-BOM script using the active ANSI code page. The identity
# implementation itself remains strict UTF-8; the multibyte scalar is created
# explicitly below from its Unicode code point.
$eAcute = [string][char]0x00E9
$multibyteTag = 'v1.0.4-' + $eAcute

$referenceText = (@"
pulsestack.nuget.admitted-release.v1
productionKind:7:Release
sourceCommit:40:0123456789abcdef0123456789abcdef01234567
packageVersion:5:1.0.4
releaseAuthorityTag:6:v1.0.4
packageCount:2
package:0:15:PulseStack.Core:5:1.0.4:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
package:1:17:PulseStack.Agents:5:1.0.4:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
"@ -replace "`r`n", "`n") + "`n"

$results=@()
$results+=Invoke-Case 'I01' 'ASCII reference vector is exact' {
    $identity=Get-NuGetAdmittedReleaseIdentity (New-ReferenceSet)
    Assert-Eq '1' $identity.Profile 'profile'
    Assert-Eq '392' ([string]$identity.ByteCount) 'byte count'
    Assert-Eq '573602b6938b1cf5cfe37dc9ab3695a447b652ea9732dea09e3f6b1913131916' $identity.Sha256 'SHA-256'
    [byte[]]$bytes=Get-NuGetAdmittedReleaseIdentityCanonicalBytes $identity.Projection
    $text=[Text.UTF8Encoding]::new($false,$true).GetString($bytes)
    Assert-Eq $referenceText $text 'canonical text'
}
$results+=Invoke-Case 'I02' 'multibyte vector uses UTF-8 byte length' {
    $identity=Get-NuGetAdmittedReleaseIdentity (New-ReferenceSet $multibyteTag)
    Assert-Eq '395' ([string]$identity.ByteCount) 'byte count'
    Assert-Eq 'ed7af455ea586dd7b10fe3470260185acffe2c9f88eb832facc05ae8926e0c96' $identity.Sha256 'SHA-256'
    [byte[]]$bytes=Get-NuGetAdmittedReleaseIdentityCanonicalBytes $identity.Projection
    $text=[Text.UTF8Encoding]::new($false,$true).GetString($bytes)
    Assert-True $text.Contains("releaseAuthorityTag:9:${multibyteTag}`n") 'multibyte tag did not use UTF-8 byte length 9'
}
$results+=Invoke-Case 'I03' 'package order is identity-sensitive' {
    $a=New-ReferenceSet;$b=New-ReferenceSet;$b.Packages=@($b.Packages[1],$b.Packages[0])
    Assert-Ne (Get-NuGetAdmittedReleaseIdentity $a).Sha256 (Get-NuGetAdmittedReleaseIdentity $b).Sha256 'order sensitivity'
}
$results+=Invoke-Case 'I04' 'package SHA is identity-sensitive' {
    $a=New-ReferenceSet;$b=New-ReferenceSet;$b.Packages[1].Sha256=('c'*64)
    Assert-Ne (Get-NuGetAdmittedReleaseIdentity $a).Sha256 (Get-NuGetAdmittedReleaseIdentity $b).Sha256 'SHA sensitivity'
}
$results+=Invoke-Case 'I05' 'field boundaries are length-prefixed' {
    $a=New-ReferenceSet 'a:b';$identity=Get-NuGetAdmittedReleaseIdentity $a
    [byte[]]$bytes=Get-NuGetAdmittedReleaseIdentityCanonicalBytes $identity.Projection
    $text=[Text.UTF8Encoding]::new($false,$true).GetString($bytes)
    Assert-True $text.Contains("releaseAuthorityTag:3:a:b`n") 'delimiter-bearing value was not length-prefixed'
    Assert-Ne (Get-NuGetAdmittedReleaseIdentity (New-ReferenceSet 'a:b')).Sha256 (Get-NuGetAdmittedReleaseIdentity (New-ReferenceSet 'a')).Sha256 'field boundary sensitivity'
}
$results+=Invoke-Case 'I06' 'source commit casing is canonical' {
    $set=New-ReferenceSet;$set.SourceCommit=$set.SourceCommit.ToUpperInvariant()
    Assert-Throws {Get-NuGetAdmittedReleaseIdentity $set} 'canonical lowercase 40-hex'
}
$results+=Invoke-Case 'I07' 'package SHA casing is canonical' {
    $set=New-ReferenceSet;$set.Packages[0].Sha256=$set.Packages[0].Sha256.ToUpperInvariant()
    Assert-Throws {Get-NuGetAdmittedReleaseIdentity $set} 'canonical lowercase 64-hex'
}
$results+=Invoke-Case 'I08' 'profile version is separated' {
    Assert-Throws {Get-NuGetAdmittedReleaseIdentity (New-ReferenceSet) -Profile '2'} "Unsupported admitted release identity profile '2'"
}
$results+=Invoke-Case 'I09' 'identity requires Release production kind' {
    $set=New-ReferenceSet;$set.ProductionKind='Development'
    Assert-Throws {Get-NuGetAdmittedReleaseIdentity $set} "ProductionKind exactly 'Release'"
}
$results+=Invoke-Case 'I10' 'package version must match release version' {
    $set=New-ReferenceSet;$set.Packages[1].Version='1.0.5'
    Assert-Throws {Get-NuGetAdmittedReleaseIdentity $set} 'version does not match PackageVersion'
}
$results+=Invoke-Case 'I11' 'line-breaking text is inadmissible' {
    $set=New-ReferenceSet;$set.ReleaseAuthorityTag="v1.0.4`nforged"
    Assert-Throws {Get-NuGetAdmittedReleaseIdentity $set} 'must not contain CR or LF'
}
$results+=Invoke-Case 'I12' 'package count and indices are explicit' {
    $identity=Get-NuGetAdmittedReleaseIdentity (New-ReferenceSet)
    Assert-Eq '2' ([string]$identity.Projection.PackageCount) 'package count'
    Assert-Eq '0' ([string]$identity.Projection.Packages[0].Index) 'first index'
    Assert-Eq '1' ([string]$identity.Projection.Packages[1].Index) 'second index'
}

$results|Format-Table -AutoSize
$failed=@($results|Where-Object Outcome -eq 'FAIL')
if($failed.Count){$failed|ForEach-Object{Write-Host "$($_.Id) $($_.Name): $($_.Error)" -ForegroundColor Red};throw "RP-5A admitted release identity conformance failed: $($failed.Count) case(s)."}
Write-Host "RP-5A admitted release identity conformance passed: $($results.Count)/$($results.Count)."
