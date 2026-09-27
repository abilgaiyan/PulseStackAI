Set-StrictMode -Version Latest

function Assert-Rp5Property {
    param([Parameter(Mandatory)][object]$Object,[Parameter(Mandatory)][string]$Name,[Parameter(Mandatory)][string]$Context)
    $property=$Object.PSObject.Properties[$Name]
    if($null -eq $property){throw [System.ArgumentException]::new("$Context must contain $Name.")}
    return $property.Value
}

function Get-Rp5CanonicalRelease {
    param([Parameter(Mandatory)][object]$Release)
    if($null -eq $Release){throw [System.ArgumentNullException]::new('Release')}
    $profile=Assert-Rp5Property $Release 'Profile' 'Release'
    $sourceCommit=Assert-Rp5Property $Release 'SourceCommit' 'Release'
    $packageVersion=Assert-Rp5Property $Release 'PackageVersion' 'Release'
    $releaseAuthorityTag=Assert-Rp5Property $Release 'ReleaseAuthorityTag' 'Release'
    $packageCount=[int](Assert-Rp5Property $Release 'PackageCount' 'Release')
    $packages=@(Assert-Rp5Property $Release 'Packages' 'Release')
    if($packageCount -le 0 -or $packages.Count -ne $packageCount){throw [System.ArgumentException]::new('Release PackageCount must exactly match its non-empty Packages sequence.')}
    $identityProperty=$Release.PSObject.Properties['Sha256']
    if($null -eq $identityProperty){$identityProperty=$Release.PSObject.Properties['ReleaseIdentitySha256']}
    if($null -eq $identityProperty -or [string]$identityProperty.Value -cnotmatch '^[0-9a-f]{64}$'){throw [System.ArgumentException]::new('Release must contain canonical release identity SHA-256.')}
    [pscustomobject]@{Profile=[string]$profile;Sha256=[string]$identityProperty.Value;SourceCommit=[string]$sourceCommit;PackageVersion=[string]$packageVersion;ReleaseAuthorityTag=[string]$releaseAuthorityTag;PackageCount=$packageCount;Packages=$packages}
}

function Assert-Rp5LedgerReleaseIdentity {
    param([Parameter(Mandatory)][object]$Ledger,[Parameter(Mandatory)][object]$Release)
    if([string](Assert-Rp5Property $Ledger 'sourceCommit' 'Publication ledger') -cne $Release.SourceCommit -or
       [string](Assert-Rp5Property $Ledger 'packageVersion' 'Publication ledger') -cne $Release.PackageVersion -or
       [string](Assert-Rp5Property $Ledger 'releaseAuthorityTag' 'Publication ledger') -cne $Release.ReleaseAuthorityTag){
        throw [System.InvalidOperationException]::new('Publication ledger provenance does not belong to the supplied release identity.')
    }
    $profile=$Ledger.PSObject.Properties['releaseIdentityProfile'];$sha=$Ledger.PSObject.Properties['releaseIdentitySha256']
    if(($null -eq $profile) -xor ($null -eq $sha)){throw [System.InvalidOperationException]::new('Publication ledger contains incomplete release identity evidence.')}
    if($null -ne $profile -and ([string]$profile.Value -cne $Release.Profile -or [string]$sha.Value -cne $Release.Sha256)){
        throw [System.InvalidOperationException]::new('Publication ledger release identity does not match the supplied release identity.')
    }
}

function ConvertFrom-NuGetOriginalPublicationLedgerClaims {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object]$Ledger,[Parameter(Mandatory)][object]$Release)
    $canonical=Get-Rp5CanonicalRelease $Release
    Assert-Rp5LedgerReleaseIdentity $Ledger $canonical
    $operationId=[string](Assert-Rp5Property $Ledger 'operationId' 'Publication ledger')
    $ledgerPackages=@(Assert-Rp5Property $Ledger 'packages' 'Publication ledger')
    if($ledgerPackages.Count -ne $canonical.PackageCount){throw [System.InvalidOperationException]::new('Publication ledger package count does not match the supplied release identity.')}
    $claims=[System.Collections.Generic.List[object]]::new()
    for($i=0;$i -lt $canonical.PackageCount;$i++){
        $actual=$ledgerPackages[$i];$expected=$canonical.Packages[$i]
        $id=[string](Assert-Rp5Property $actual 'id' "Publication package[$i]")
        $version=[string](Assert-Rp5Property $actual 'version' "Publication package[$i]")
        $sha=[string](Assert-Rp5Property $actual 'admittedSha256' "Publication package[$i]")
        if($id -cne [string]$expected.Id -or $version -cne [string]$expected.Version -or $sha -cne [string]$expected.Sha256){throw [System.InvalidOperationException]::new("Publication ledger package at canonical index $i does not belong to the supplied release identity.")}
        $mutation=[string](Assert-Rp5Property $actual 'mutationState' "Publication package[$i]")
        if($mutation -notin @('Accepted','NotAttempted','Attempting','Indeterminate','Rejected')){throw [System.InvalidOperationException]::new("Publication package at canonical index $i has unsupported mutation state '$mutation'.")}
        $status=$actual.PSObject.Properties['statusCode'];$diagnostic=$actual.PSObject.Properties['diagnostic']
        $claims.Add([pscustomobject]@{
            ReleaseIdentityProfile=$canonical.Profile;ReleaseIdentitySha256=$canonical.Sha256;PackageIndex=$i;PackageId=$id;PackageVersion=$version;AdmittedSha256=$sha
            EvidenceSource='OriginalPublication';OperationId=$operationId;RawDisposition='PublicationMutation';RawMutationState=$mutation;RecoveryState=$null
            StatusCode=if($null -eq $status){$null}else{$status.Value};Diagnostic=if($null -eq $diagnostic){$null}else{$diagnostic.Value};Evidence=$actual
        })
    }
    return @($claims)
}
