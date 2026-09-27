Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'NuGetReleaseEvidenceClaims.ps1')

function Get-Rp5RecoveryTriggerForClaim {
    param([Parameter(Mandatory)][object]$Claim)
    switch ([string]$Claim.RawMutationState) {
        'Rejected' {
            $code=if($null-ne$Claim.Diagnostic-and$null-ne$Claim.Diagnostic.PSObject.Properties['Code']){[string]$Claim.Diagnostic.Code}else{$null}
            if([int]$Claim.StatusCode-eq409-and$code-ceq'ExistingIdentityConflict'){return 'Conflict409'}
        }
        'Indeterminate' { return 'IndeterminateMutation' }
        'Attempting' { return 'StaleAttempting' }
    }
    throw [System.InvalidOperationException]::new("Source publication claim in state '$($Claim.RawMutationState)' is not recovery eligible.")
}

function ConvertFrom-NuGetRecoveryEvidenceClaim {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$RecoveryEvidence,
        [Parameter(Mandatory)][object]$SourceLedger,
        [Parameter(Mandatory)][object]$Release
    )

    if($null-eq$RecoveryEvidence.PSObject.Properties['HistoricalPublication']-or$null-eq$RecoveryEvidence.PSObject.Properties['Recovery']){
        throw [System.InvalidOperationException]::new('Recovery evidence is missing historical publication or recovery data.')
    }

    $operationKindProperty=$SourceLedger.PSObject.Properties['operationKind']
    if($null-ne$operationKindProperty-and[string]$operationKindProperty.Value-ceq'Continuation'){
        $sourceClaims=@(ConvertFrom-NuGetContinuationPublicationLedgerClaim -Ledger $SourceLedger -Release $Release)
    } else {
        $sourceClaims=@(ConvertFrom-NuGetOriginalPublicationLedgerClaims -Ledger $SourceLedger -Release $Release)
    }

    $historicalOperation=$RecoveryEvidence.HistoricalPublication.Operation
    $historicalPackage=$RecoveryEvidence.HistoricalPublication.Package
    $operationId=[string](Assert-Rp5Property $historicalOperation 'OperationId' 'Recovery historical operation')
    $packageId=[string](Assert-Rp5Property $historicalPackage 'Id' 'Recovery historical package')
    $packageVersion=[string](Assert-Rp5Property $historicalPackage 'Version' 'Recovery historical package')
    $sha=[string](Assert-Rp5Property $historicalPackage 'AdmittedSha256' 'Recovery historical package')

    $matches=@($sourceClaims|Where-Object{
        [string]$_.OperationId-ceq$operationId-and[string]$_.PackageId-ceq$packageId-and[string]$_.PackageVersion-ceq$packageVersion-and[string]$_.AdmittedSha256-ceq$sha
    })
    if($matches.Count-ne1){throw [System.InvalidOperationException]::new('Recovery evidence does not uniquely join to its immutable source publication claim.')}
    $source=$matches[0]

    if([string](Assert-Rp5Property $historicalPackage 'MutationState' 'Recovery historical package')-cne[string]$source.RawMutationState-or
       [string](Assert-Rp5Property $historicalPackage 'StatusCode' 'Recovery historical package')-cne[string]$source.StatusCode){
        throw [System.InvalidOperationException]::new('Recovery evidence does not preserve source publication mutation lifecycle evidence.')
    }

    foreach($name in @('LedgerState','OperationConclusion','StartedAtUtc','CompletedAtUtc','Registry')){
        $ledgerName=switch($name){'LedgerState'{'ledgerState'};'OperationConclusion'{'operationConclusion'};'StartedAtUtc'{'startedAtUtc'};'CompletedAtUtc'{'completedAtUtc'};'Registry'{'registry'}}
        $ledgerProperty=$SourceLedger.PSObject.Properties[$ledgerName]
        if($null-eq$ledgerProperty-or[string](Assert-Rp5Property $historicalOperation $name 'Recovery historical operation')-cne[string]$ledgerProperty.Value){
            throw [System.InvalidOperationException]::new('Recovery evidence does not preserve source publication operation lifecycle evidence.')
        }
    }

    $recovery=$RecoveryEvidence.Recovery
    $trigger=[string](Assert-Rp5Property $recovery 'Trigger' 'Recovery evidence')
    $expectedTrigger=Get-Rp5RecoveryTriggerForClaim $source
    if($trigger-cne$expectedTrigger){throw [System.InvalidOperationException]::new('Recovery trigger does not match the source publication claim.')}
    $recoveryState=[string](Assert-Rp5Property $recovery 'RecoveryState' 'Recovery evidence')
    if($recoveryState-cnotin@('Converged','Conflict','Unresolved')){throw [System.InvalidOperationException]::new('Recovery evidence must contain terminal Converged, Conflict, or Unresolved state.')}

    [pscustomobject]@{
        ReleaseIdentityProfile=$source.ReleaseIdentityProfile;ReleaseIdentitySha256=$source.ReleaseIdentitySha256
        PackageIndex=$source.PackageIndex;PackageId=$source.PackageId;PackageVersion=$source.PackageVersion;AdmittedSha256=$source.AdmittedSha256
        EvidenceSource=if($source.EvidenceSource-ceq'ContinuationPublication'){'ContinuationRecovery'}else{'OriginalRecovery'}
        OperationId=$source.OperationId;HistoricalPublicationOperationId=$source.HistoricalPublicationOperationId
        RawDisposition='Recovery';RawMutationState=$source.RawMutationState;RecoveryState=$recoveryState;RecoveryTrigger=$trigger
        StatusCode=$source.StatusCode;Diagnostic=$source.Diagnostic;Evidence=$RecoveryEvidence
    }
}
