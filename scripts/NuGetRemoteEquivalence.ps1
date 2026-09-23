Set-StrictMode -Version Latest

$script:NuGetV3ServiceIndex = "https://api.nuget.org/v3/index.json"

function New-NuGetRemoteEquivalenceDiagnostic {
    param(
        [Parameter(Mandatory)] [string] $Code,
        [Parameter(Mandatory)] [string] $Message,
        [AllowNull()] [Nullable[int]] $StatusCode = $null
    )

    [pscustomobject]@{ Code=$Code; Message=$Message; StatusCode=$StatusCode }
}

function New-NuGetRemoteEquivalenceRequest {
    param([Parameter(Mandatory)] [string] $Uri)
    [pscustomobject]@{ Method="GET"; Uri=$Uri }
}

function Invoke-DefaultNuGetRemoteEquivalenceRequest {
    param([Parameter(Mandatory)] [object] $Request)

    try {
        $response = Invoke-WebRequest -Method GET -Uri $Request.Uri -UseBasicParsing -ErrorAction Stop
        $bytes = $null
        if ($null -ne $response.RawContentStream) {
            $stream = $response.RawContentStream
            if ($stream.CanSeek) { $stream.Position = 0 }
            $memory = [System.IO.MemoryStream]::new()
            try { $stream.CopyTo($memory); $bytes = $memory.ToArray() } finally { $memory.Dispose() }
        }
        return [pscustomobject]@{ StatusCode=[int]$response.StatusCode; Content=[string]$response.Content; Bytes=$bytes }
    }
    catch {
        if ($null -ne $_.Exception.Response -and $null -ne $_.Exception.Response.StatusCode) {
            return [pscustomobject]@{ StatusCode=[int]$_.Exception.Response.StatusCode; Content=$null; Bytes=$null }
        }
        throw
    }
}

function Get-NuGetNormalizedVersion {
    param([Parameter(Mandatory)] [string] $Version)

    # PackageBaseAddress requires NuGet-normalized versions. Normalize numeric
    # components explicitly rather than treating lowercase text as normalization.
    $match = [regex]::Match($Version, '^(?<core>[0-9]+(?:\.[0-9]+){0,3})(?:-(?<pre>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+(?<meta>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$')
    if (-not $match.Success) { throw [System.ArgumentException]::new("Package version '$Version' is not a supported NuGet version.") }

    $parts = @($match.Groups['core'].Value.Split('.') | ForEach-Object { [uint64]::Parse($_, [System.Globalization.CultureInfo]::InvariantCulture) })
    if ($parts.Count -gt 4) { throw [System.ArgumentException]::new("Package version '$Version' has too many numeric components.") }
    while ($parts.Count -lt 3) { $parts += [uint64]0 }
    if ($parts.Count -eq 4 -and $parts[3] -eq 0) { $parts = @($parts[0],$parts[1],$parts[2]) }

    $normalized = ($parts | ForEach-Object { $_.ToString([System.Globalization.CultureInfo]::InvariantCulture) }) -join '.'
    if ($match.Groups['pre'].Success) {
        $pre = @($match.Groups['pre'].Value.Split('.') | ForEach-Object {
            if ($_ -match '^[0-9]+$') { ([uint64]::Parse($_, [System.Globalization.CultureInfo]::InvariantCulture)).ToString([System.Globalization.CultureInfo]::InvariantCulture) } else { $_ }
        }) -join '.'
        $normalized += "-$pre"
    }
    # Build metadata is not part of NuGet package identity/path normalization.
    return $normalized
}

function Get-NuGetRemotePackageContentUri {
    param(
        [Parameter(Mandatory)] [string] $BaseAddress,
        [Parameter(Mandatory)] [string] $Id,
        [Parameter(Mandatory)] [string] $Version
    )

    $lowerId = $Id.ToLowerInvariant()
    $lowerVersion = (Get-NuGetNormalizedVersion -Version $Version).ToLowerInvariant()
    if (-not $BaseAddress.EndsWith('/', [System.StringComparison]::Ordinal)) { $BaseAddress += '/' }
    "$BaseAddress$lowerId/$lowerVersion/$lowerId.$lowerVersion.nupkg"
}

function Get-PackageBaseAddressForEquivalence {
    param([Parameter(Mandatory)] [object] $Response)

    if ([int]$Response.StatusCode -ne 200) {
        return [pscustomobject]@{ Success=$false; BaseAddress=$null; Diagnostic=(New-NuGetRemoteEquivalenceDiagnostic -Code 'ServiceIndexUnavailable' -Message "NuGet V3 service index returned HTTP $($Response.StatusCode)." -StatusCode ([int]$Response.StatusCode)) }
    }
    try { $index = [string]$Response.Content | ConvertFrom-Json -ErrorAction Stop }
    catch { return [pscustomobject]@{ Success=$false; BaseAddress=$null; Diagnostic=(New-NuGetRemoteEquivalenceDiagnostic -Code 'ServiceIndexInvalid' -Message 'NuGet V3 service index is not valid JSON.') } }

    $resources = $index.PSObject.Properties['resources']
    if ($null -eq $resources -or $null -eq $resources.Value) {
        return [pscustomobject]@{ Success=$false; BaseAddress=$null; Diagnostic=(New-NuGetRemoteEquivalenceDiagnostic -Code 'ServiceIndexInvalid' -Message 'NuGet V3 service index does not contain resources.') }
    }
    $matches = @($resources.Value | Where-Object {
        $t=$_.PSObject.Properties['@type']; $i=$_.PSObject.Properties['@id']
        $null -ne $t -and $null -ne $i -and [string]$t.Value -match '^PackageBaseAddress/' -and -not [string]::IsNullOrWhiteSpace([string]$i.Value)
    })
    if ($matches.Count -eq 0) {
        return [pscustomobject]@{ Success=$false; BaseAddress=$null; Diagnostic=(New-NuGetRemoteEquivalenceDiagnostic -Code 'PackageBaseAddressUnavailable' -Message 'NuGet V3 service index does not expose PackageBaseAddress.') }
    }
    $base = [string]$matches[0].PSObject.Properties['@id'].Value
    $uri=$null
    if (-not [Uri]::TryCreate($base,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -notin @('http','https')) {
        return [pscustomobject]@{ Success=$false; BaseAddress=$null; Diagnostic=(New-NuGetRemoteEquivalenceDiagnostic -Code 'PackageBaseAddressUnavailable' -Message 'PackageBaseAddress is not an absolute HTTP(S) URI.') }
    }
    if (-not $base.EndsWith('/')) { $base += '/' }
    [pscustomobject]@{ Success=$true; BaseAddress=$base; Diagnostic=$null }
}

function Get-Sha256Hex {
    param([Parameter(Mandatory)] [byte[]] $Bytes)
    $sha=[System.Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
}

function Normalize-RegistryUri {
    param([Parameter(Mandatory)] [string] $Uri)
    $parsed=$null
    if (-not [Uri]::TryCreate($Uri,[UriKind]::Absolute,[ref]$parsed) -or $parsed.Scheme -notin @('http','https')) { throw [ArgumentException]::new('Registry must be an absolute HTTP(S) service-index URI.') }
    $builder=[UriBuilder]::new($parsed)
    $builder.Scheme=$builder.Scheme.ToLowerInvariant(); $builder.Host=$builder.Host.ToLowerInvariant()
    if (($builder.Scheme -eq 'https' -and $builder.Port -eq 443) -or ($builder.Scheme -eq 'http' -and $builder.Port -eq 80)) { $builder.Port=-1 }
    $builder.Uri.AbsoluteUri.TrimEnd('/')
}

function New-IndeterminateEquivalenceResult {
    param([string]$Registry,[string]$Base,[AllowNull()][string]$ContentUri,[object]$Package,[object]$Diagnostic,[AllowNull()][Nullable[int]]$StatusCode,[datetime]$ObservedAtUtc)
    [pscustomobject]@{ Registry=$Registry; PackageBaseAddress=$Base; PackageContentUri=$ContentUri; Id=[string]$Package.Id; Version=[string]$Package.Version; AdmittedSha256=[string]$Package.Sha256; RemoteSha256=$null; State='Indeterminate'; StatusCode=$StatusCode; ObservedAtUtc=$ObservedAtUtc; Diagnostic=$Diagnostic }
}

function Invoke-NuGetRemoteEquivalence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $Package,
        [string] $ServiceIndexUri = $script:NuGetV3ServiceIndex,
        [AllowNull()] [object] $RecoveryContext = $null,
        [scriptblock] $Request = ${function:Invoke-DefaultNuGetRemoteEquivalenceRequest},
        [scriptblock] $Clock = { [DateTime]::UtcNow }
    )

    foreach ($name in @('Id','Version','Sha256')) {
        $p=$Package.PSObject.Properties[$name]
        if ($null -eq $p -or [string]::IsNullOrWhiteSpace([string]$p.Value)) { throw [ArgumentException]::new("Package must contain $name.") }
    }
    $admittedHash=[string]$Package.Sha256
    if ($admittedHash -cnotmatch '^[0-9a-f]{64}$') { throw [ArgumentException]::new('Package Sha256 must be canonical lowercase SHA-256.') }
    $registry=Normalize-RegistryUri -Uri $ServiceIndexUri

    if ($null -ne $RecoveryContext) {
        $rp=$RecoveryContext.PSObject.Properties['Registry']
        if ($null -eq $rp -or [string]::IsNullOrWhiteSpace([string]$rp.Value) -or (Normalize-RegistryUri -Uri ([string]$rp.Value)) -cne $registry) {
            throw [ArgumentException]::new('Recovery context registry does not match the target registry.')
        }
    }

    $observed=& $Clock
    $discoveryRequest=New-NuGetRemoteEquivalenceRequest -Uri $ServiceIndexUri
    try { $service=& $Request $discoveryRequest }
    catch { return New-IndeterminateEquivalenceResult -Registry $registry -Base $null -ContentUri $null -Package $Package -Diagnostic (New-NuGetRemoteEquivalenceDiagnostic -Code 'ServiceIndexUnavailable' -Message $_.Exception.Message) -StatusCode $null -ObservedAtUtc $observed }

    $discovery=Get-PackageBaseAddressForEquivalence -Response $service
    if (-not $discovery.Success) { return New-IndeterminateEquivalenceResult -Registry $registry -Base $null -ContentUri $null -Package $Package -Diagnostic $discovery.Diagnostic -StatusCode $discovery.Diagnostic.StatusCode -ObservedAtUtc $observed }

    try { $contentUri=Get-NuGetRemotePackageContentUri -BaseAddress $discovery.BaseAddress -Id ([string]$Package.Id) -Version ([string]$Package.Version) }
    catch { return New-IndeterminateEquivalenceResult -Registry $registry -Base $discovery.BaseAddress -ContentUri $null -Package $Package -Diagnostic (New-NuGetRemoteEquivalenceDiagnostic -Code 'PackageIdentityInvalid' -Message $_.Exception.Message) -StatusCode $null -ObservedAtUtc $observed }

    try { $response=& $Request (New-NuGetRemoteEquivalenceRequest -Uri $contentUri) }
    catch { return New-IndeterminateEquivalenceResult -Registry $registry -Base $discovery.BaseAddress -ContentUri $contentUri -Package $Package -Diagnostic (New-NuGetRemoteEquivalenceDiagnostic -Code 'TransportFailure' -Message $_.Exception.Message) -StatusCode $null -ObservedAtUtc $observed }

    $status=[int]$response.StatusCode
    if ($status -eq 404) {
        return [pscustomobject]@{ Registry=$registry; PackageBaseAddress=$discovery.BaseAddress; PackageContentUri=$contentUri; Id=[string]$Package.Id; Version=[string]$Package.Version; AdmittedSha256=$admittedHash; RemoteSha256=$null; State='NotObservable'; StatusCode=404; ObservedAtUtc=$observed; Diagnostic=$null }
    }
    if ($status -ne 200) {
        $code=if($status -eq 429){'RateLimited'}elseif($status -ge 500 -and $status -le 599){'ServerFailure'}else{'UnexpectedStatus'}
        return New-IndeterminateEquivalenceResult -Registry $registry -Base $discovery.BaseAddress -ContentUri $contentUri -Package $Package -Diagnostic (New-NuGetRemoteEquivalenceDiagnostic -Code $code -Message "NuGet package-content observation returned HTTP $status." -StatusCode $status) -StatusCode $status -ObservedAtUtc $observed
    }

    $bytesProperty=$response.PSObject.Properties['Bytes']
    if ($null -eq $bytesProperty -or $null -eq $bytesProperty.Value) {
        return New-IndeterminateEquivalenceResult -Registry $registry -Base $discovery.BaseAddress -ContentUri $contentUri -Package $Package -Diagnostic (New-NuGetRemoteEquivalenceDiagnostic -Code 'ContentUnreadable' -Message 'NuGet package-content response did not provide complete package bytes.' -StatusCode 200) -StatusCode 200 -ObservedAtUtc $observed
    }
    try { $remoteHash=Get-Sha256Hex -Bytes ([byte[]]$bytesProperty.Value) }
    catch { return New-IndeterminateEquivalenceResult -Registry $registry -Base $discovery.BaseAddress -ContentUri $contentUri -Package $Package -Diagnostic (New-NuGetRemoteEquivalenceDiagnostic -Code 'ContentHashFailure' -Message $_.Exception.Message -StatusCode 200) -StatusCode 200 -ObservedAtUtc $observed }

    $state=if($remoteHash -ceq $admittedHash){'Equivalent'}else{'Different'}
    [pscustomobject]@{ Registry=$registry; PackageBaseAddress=$discovery.BaseAddress; PackageContentUri=$contentUri; Id=[string]$Package.Id; Version=[string]$Package.Version; AdmittedSha256=$admittedHash; RemoteSha256=$remoteHash; State=$state; StatusCode=200; ObservedAtUtc=$observed; Diagnostic=$null }
}
