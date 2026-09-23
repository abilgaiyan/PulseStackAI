Set-StrictMode -Version Latest

$script:NuGetV3ServiceIndex = "https://api.nuget.org/v3/index.json"

function New-NuGetPreflightDiagnostic {
    param(
        [Parameter(Mandatory)] [string] $Code,
        [Parameter(Mandatory)] [string] $Message,
        [AllowNull()] [Nullable[int]] $StatusCode = $null
    )

    return [pscustomobject]@{
        Code       = $Code
        Message    = $Message
        StatusCode = $StatusCode
    }
}

function New-NuGetPreflightRequest {
    param(
        [Parameter(Mandatory)] [ValidateSet("GET", "HEAD")] [string] $Method,
        [Parameter(Mandatory)] [string] $Uri
    )

    return [pscustomobject]@{
        Method = $Method
        Uri    = $Uri
    }
}

function Invoke-DefaultNuGetPreflightRequest {
    param(
        [Parameter(Mandatory)] [object] $Request
    )

    # Method policy is owned by RP-2. This transport receives only requests
    # created by New-NuGetPreflightRequest (GET service index / HEAD package).
    try {
        $response = Invoke-WebRequest -Method $Request.Method -Uri $Request.Uri -UseBasicParsing -ErrorAction Stop
        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Content    = if ($Request.Method -eq "GET") { [string]$response.Content } else { $null }
            Headers    = $response.Headers
        }
    }
    catch {
        $statusCode = $null
        if ($null -ne $_.Exception.Response -and $null -ne $_.Exception.Response.StatusCode) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            return [pscustomobject]@{
                StatusCode = $statusCode
                Content    = $null
                Headers    = $null
            }
        }

        throw
    }
}

function Get-PackageBaseAddressFromServiceIndex {
    param(
        [Parameter(Mandatory)] [object] $Response
    )

    if ([int]$Response.StatusCode -ne 200) {
        return [pscustomobject]@{
            Success    = $false
            BaseAddress = $null
            Diagnostic = New-NuGetPreflightDiagnostic -Code "ServiceIndexUnavailable" -Message "NuGet V3 service index returned HTTP $($Response.StatusCode)." -StatusCode ([int]$Response.StatusCode)
        }
    }

    try {
        $index = [string]$Response.Content | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        return [pscustomobject]@{
            Success    = $false
            BaseAddress = $null
            Diagnostic = New-NuGetPreflightDiagnostic -Code "ServiceIndexInvalid" -Message "NuGet V3 service index is not valid JSON."
        }
    }

    $resourcesProperty = $index.PSObject.Properties["resources"]
    if ($null -eq $resourcesProperty -or $null -eq $resourcesProperty.Value) {
        return [pscustomobject]@{
            Success    = $false
            BaseAddress = $null
            Diagnostic = New-NuGetPreflightDiagnostic -Code "ServiceIndexInvalid" -Message "NuGet V3 service index does not contain resources."
        }
    }

    $matches = @(
        $resourcesProperty.Value | Where-Object {
            $typeProperty = $_.PSObject.Properties["@type"]
            $idProperty = $_.PSObject.Properties["@id"]
            $null -ne $typeProperty -and
            $null -ne $idProperty -and
            [string]$typeProperty.Value -match "^PackageBaseAddress/" -and
            -not [string]::IsNullOrWhiteSpace([string]$idProperty.Value)
        }
    )

    if ($matches.Count -eq 0) {
        return [pscustomobject]@{
            Success    = $false
            BaseAddress = $null
            Diagnostic = New-NuGetPreflightDiagnostic -Code "PackageBaseAddressUnavailable" -Message "NuGet V3 service index does not expose PackageBaseAddress."
        }
    }

    $baseAddress = [string]$matches[0].PSObject.Properties["@id"].Value
    $uri = $null
    if (-not [System.Uri]::TryCreate($baseAddress, [System.UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -notin @("http", "https")) {
        return [pscustomobject]@{
            Success    = $false
            BaseAddress = $null
            Diagnostic = New-NuGetPreflightDiagnostic -Code "PackageBaseAddressUnavailable" -Message "PackageBaseAddress is not an absolute HTTP(S) URI."
        }
    }

    if (-not $baseAddress.EndsWith("/", [System.StringComparison]::Ordinal)) {
        $baseAddress += "/"
    }

    return [pscustomobject]@{
        Success     = $true
        BaseAddress = $baseAddress
        Diagnostic  = $null
    }
}

function Get-NuGetPackageContentUri {
    param(
        [Parameter(Mandatory)] [string] $BaseAddress,
        [Parameter(Mandatory)] [string] $Id,
        [Parameter(Mandatory)] [string] $Version
    )

    $lowerId = $Id.ToLowerInvariant()
    $lowerVersion = $Version.ToLowerInvariant()
    return "$BaseAddress$lowerId/$lowerVersion/$lowerId.$lowerVersion.nupkg"
}

function Get-NuGetPackageObservation {
    param(
        [Parameter(Mandatory)] [string] $Id,
        [Parameter(Mandatory)] [string] $Version,
        [Parameter(Mandatory)] [string] $Uri,
        [Parameter(Mandatory)] [scriptblock] $Request
    )

    $transportRequest = New-NuGetPreflightRequest -Method "HEAD" -Uri $Uri
    try {
        $response = & $Request $transportRequest
    }
    catch {
        return [pscustomobject]@{
            Id         = $Id
            Version    = $Version
            State      = "Indeterminate"
            StatusCode = $null
            Diagnostic = New-NuGetPreflightDiagnostic -Code "TransportFailure" -Message $_.Exception.Message
        }
    }

    $statusCode = [int]$response.StatusCode
    switch ($statusCode) {
        200 {
            return [pscustomobject]@{ Id=$Id; Version=$Version; State="Present"; StatusCode=200; Diagnostic=$null }
        }
        404 {
            return [pscustomobject]@{ Id=$Id; Version=$Version; State="Absent"; StatusCode=404; Diagnostic=$null }
        }
        429 {
            return [pscustomobject]@{ Id=$Id; Version=$Version; State="Indeterminate"; StatusCode=429; Diagnostic=(New-NuGetPreflightDiagnostic -Code "RateLimited" -Message "NuGet package observation was rate limited." -StatusCode 429) }
        }
        default {
            if ($statusCode -ge 500 -and $statusCode -le 599) {
                return [pscustomobject]@{ Id=$Id; Version=$Version; State="Indeterminate"; StatusCode=$statusCode; Diagnostic=(New-NuGetPreflightDiagnostic -Code "ServerFailure" -Message "NuGet package observation returned HTTP $statusCode." -StatusCode $statusCode) }
            }

            return [pscustomobject]@{ Id=$Id; Version=$Version; State="Indeterminate"; StatusCode=$statusCode; Diagnostic=(New-NuGetPreflightDiagnostic -Code "UnexpectedStatus" -Message "NuGet package observation returned unexpected HTTP $statusCode." -StatusCode $statusCode) }
        }
    }
}

function Invoke-NuGetRegistryPreflight {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object] $AdmittedPackageSet,
        [scriptblock] $Request = ${function:Invoke-DefaultNuGetPreflightRequest},
        [string] $ServiceIndexUri = $script:NuGetV3ServiceIndex
    )

    if ($null -eq $AdmittedPackageSet) {
        throw [System.ArgumentNullException]::new("AdmittedPackageSet")
    }

    $packagesProperty = $AdmittedPackageSet.PSObject.Properties["Packages"]
    if ($null -eq $packagesProperty -or $null -eq $packagesProperty.Value -or @($packagesProperty.Value).Count -eq 0) {
        throw [System.ArgumentException]::new("AdmittedPackageSet must contain admitted Packages.")
    }

    # The public operation owns method policy. The injected seam receives only
    # a GET for discovery and HEAD for existence; callers cannot select methods.
    $discoveryRequest = New-NuGetPreflightRequest -Method "GET" -Uri $ServiceIndexUri
    try {
        $serviceIndexResponse = & $Request $discoveryRequest
    }
    catch {
        return [pscustomobject]@{
            Registry           = $ServiceIndexUri
            PackageBaseAddress = $null
            State              = "Indeterminate"
            Packages           = @()
            Diagnostic         = New-NuGetPreflightDiagnostic -Code "ServiceIndexUnavailable" -Message $_.Exception.Message
        }
    }

    $discovery = Get-PackageBaseAddressFromServiceIndex -Response $serviceIndexResponse
    if (-not $discovery.Success) {
        return [pscustomobject]@{
            Registry           = $ServiceIndexUri
            PackageBaseAddress = $null
            State              = "Indeterminate"
            Packages           = @()
            Diagnostic         = $discovery.Diagnostic
        }
    }

    $observations = [System.Collections.Generic.List[object]]::new()
    foreach ($package in @($packagesProperty.Value)) {
        $idProperty = $package.PSObject.Properties["Id"]
        $versionProperty = $package.PSObject.Properties["Version"]
        if ($null -eq $idProperty -or [string]::IsNullOrWhiteSpace([string]$idProperty.Value) -or
            $null -eq $versionProperty -or [string]::IsNullOrWhiteSpace([string]$versionProperty.Value)) {
            throw [System.ArgumentException]::new("Every admitted package must contain Id and Version.")
        }

        $id = [string]$idProperty.Value
        $version = [string]$versionProperty.Value
        $uri = Get-NuGetPackageContentUri -BaseAddress $discovery.BaseAddress -Id $id -Version $version
        $observations.Add((Get-NuGetPackageObservation -Id $id -Version $version -Uri $uri -Request $Request))
    }

    $states = @($observations | ForEach-Object { $_.State })
    if (@($states | Where-Object { $_ -eq "Indeterminate" }).Count -gt 0) {
        $aggregate = "Indeterminate"
    }
    elseif (@($states | Where-Object { $_ -eq "Absent" }).Count -eq $states.Count) {
        $aggregate = "AllAbsent"
    }
    elseif (@($states | Where-Object { $_ -eq "Present" }).Count -eq $states.Count) {
        $aggregate = "AllPresent"
    }
    else {
        $aggregate = "Mixed"
    }

    return [pscustomobject]@{
        Registry           = $ServiceIndexUri
        PackageBaseAddress = $discovery.BaseAddress
        State              = $aggregate
        Packages           = @($observations)
        Diagnostic         = $null
    }
}
