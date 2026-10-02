<#
.SYNOPSIS
    Starts the MiniERP Development API and Angular application as one local
    runtime, with a generated proxy targeting the exact selected API URL.

.DESCRIPTION
    This is a Development-only launcher. It never stops a process unless
    -Restart is supplied and the listener is proven to be a MiniERP process
    launched from this repository. It does not write the Development password
    to a file, the generated proxy, process state, or the logs.

    The API target can be selected with -ApiPort, -ApiUrl,
    MESP_DEV_API_PORT, or MESP_DEV_API_URL. With no override, port 5000 is
    preferred. If that generic port is occupied, an available local fallback
    beginning at 5300 is selected without touching the existing listener.
#>

[CmdletBinding()]
param(
    [int]$ApiPort = 0,
    [string]$ApiUrl = '',
    [ValidateRange(1024, 65535)]
    [int]$FrontendPort = 4300,
    [string]$AdminLogin = 'admin@mesp.com',
    [switch]$DevAuthBypass,
    [switch]$Restart,
    [switch]$ValidateOnly,
    [ValidateRange(10, 600)]
    [int]$StartupTimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
$devAuthBypassEnabled = $DevAuthBypass.IsPresent

if ($null -ne $env:MESP_DEV_ADMIN_LOGIN) {
    $AdminLogin = $env:MESP_DEV_ADMIN_LOGIN
}
if ([string]::IsNullOrWhiteSpace($AdminLogin)) {
    throw 'MESP_DEV_ADMIN_LOGIN must be a non-empty email address.'
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$backendProject = Join-Path $repositoryRoot 'backend\src\MiniErp.Api\MiniErp.Api.csproj'
$backendExecutable = Join-Path $repositoryRoot 'backend\src\MiniErp.Api\bin\Release\net10.0\MiniErp.Api.exe'
$frontendRoot = Join-Path $repositoryRoot 'frontend'
$runtimeDirectory = Join-Path $repositoryRoot '.runtime'
$proxyPath = Join-Path $runtimeDirectory 'proxy.conf.json'
$statePath = Join-Path $runtimeDirectory 'processes.json'
$backendLogPath = Join-Path $runtimeDirectory 'backend.log'
$frontendLogPath = Join-Path $runtimeDirectory 'frontend.log'

function Get-ExecutablePath {
    param(
        [Parameter(Mandatory = $true)][string]$Name
    )

    $command = Get-Command -Name $Name -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $command) {
        throw "Required executable '$Name' was not found on PATH."
    }

    return $command.Source
}

function Get-ProcessDescriptor {
    param(
        [Parameter(Mandatory = $true)][int]$ProcessId
    )

    $process = Get-CimInstance -ClassName Win32_Process -Filter "ProcessId=$ProcessId" -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        return $null
    }

    return [pscustomobject]@{
        ProcessId      = [int]$process.ProcessId
        Name           = [string]$process.Name
        ExecutablePath = [string]$process.ExecutablePath
        CommandLine    = [string]$process.CommandLine
    }
}

function Get-ListeningProcess {
    param(
        [Parameter(Mandatory = $true)][int]$Port
    )

    $connections = @(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue)
    $processIds = @($connections | Select-Object -ExpandProperty OwningProcess -Unique)
    foreach ($processId in $processIds) {
        $descriptor = Get-ProcessDescriptor -ProcessId ([int]$processId)
        if ($null -eq $descriptor) {
            continue
        }

        $descriptor
    }
}

function Get-MiniErpProcessKind {
    param(
        [Parameter(Mandatory = $true)]$Process,
        [Parameter(Mandatory = $true)][string]$Root
    )

    $normalizedRoot = $Root.TrimEnd('\').ToLowerInvariant()
    $command = ("{0} {1}" -f $Process.ExecutablePath, $Process.CommandLine).ToLowerInvariant()

    if ($command.Contains($normalizedRoot) -and $command.Contains('minierp.api')) {
        return 'backend'
    }

    if (($command.Contains($normalizedRoot)) -and ($command.Contains('ng.js')) -and ($command.Contains(' serve'))) {
        return 'frontend'
    }

    return 'other'
}

function Get-ListeningProcessInfo {
    param(
        [Parameter(Mandatory = $true)][int]$Port
    )

    foreach ($process in @(Get-ListeningProcess -Port $Port)) {
        [pscustomobject]@{
            ProcessId      = $process.ProcessId
            Name           = $process.Name
            ExecutablePath = $process.ExecutablePath
            CommandLine    = $process.CommandLine
            Kind           = Get-MiniErpProcessKind -Process $process -Root $repositoryRoot
        }
    }
}

function Get-ListenerSummary {
    param(
        [Parameter(Mandatory = $true)]$Listeners
    )

    $items = @($Listeners | ForEach-Object { "$($_.Kind) PID $($_.ProcessId) ($($_.Name))" })
    if ($items.Count -eq 0) {
        return 'unknown process'
    }

    return ($items -join ', ')
}

function Wait-ForPortToBeFree {
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (@(Get-ListeningProcessInfo -Port $Port).Count -eq 0) {
            return
        }

        Start-Sleep -Milliseconds 250
    }

    throw "Port $Port is still occupied after the requested MiniERP process restart."
}

function Stop-MiniErpListener {
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [Parameter(Mandatory = $true)][string]$ExpectedKind,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $listeners = @(Get-ListeningProcessInfo -Port $Port)
    if ($listeners.Count -eq 0) {
        return
    }

    foreach ($listener in $listeners) {
        if ($listener.Kind -ne $ExpectedKind) {
            throw "Refusing to stop port $Port. Its owner is $($listener.Kind) PID $($listener.ProcessId) ($($listener.Name)), not the repository-owned MiniERP $ExpectedKind process."
        }

        Write-Verbose "Stopping existing MiniERP $ExpectedKind process PID $($listener.ProcessId) on port $Port."
        Stop-Process -Id $listener.ProcessId -Force -ErrorAction Stop
    }

    Wait-ForPortToBeFree -Port $Port -TimeoutSeconds $TimeoutSeconds
}

function New-LocalApiUrl {
    param(
        [Parameter(Mandatory = $true)][int]$Port
    )

    return "http://localhost:$Port"
}

function Resolve-ConfiguredApiSelection {
    $rawUrl = $null
    $source = 'default'
    $explicit = $false

    if ($ApiPort -gt 0 -and -not [string]::IsNullOrWhiteSpace($ApiUrl)) {
        throw 'Use either -ApiPort or -ApiUrl, not both.'
    }

    if ($ApiPort -gt 0) {
        if ($ApiPort -lt 1024 -or $ApiPort -gt 65535) {
            throw '-ApiPort must be between 1024 and 65535.'
        }

        $rawUrl = New-LocalApiUrl -Port $ApiPort
        $source = '-ApiPort'
        $explicit = $true
    }
    elseif (-not [string]::IsNullOrWhiteSpace($ApiUrl)) {
        $rawUrl = $ApiUrl
        $source = '-ApiUrl'
        $explicit = $true
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:MESP_DEV_API_URL)) {
        $rawUrl = $env:MESP_DEV_API_URL
        $source = 'MESP_DEV_API_URL'
        $explicit = $true
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:MESP_DEV_API_PORT)) {
        $configuredPort = 0
        if ((-not [int]::TryParse($env:MESP_DEV_API_PORT, [ref]$configuredPort)) -or ($configuredPort -lt 1024) -or ($configuredPort -gt 65535)) {
            throw 'MESP_DEV_API_PORT must be a number between 1024 and 65535.'
        }

        $rawUrl = New-LocalApiUrl -Port $configuredPort
        $source = 'MESP_DEV_API_PORT'
        $explicit = $true
    }
    else {
        $rawUrl = New-LocalApiUrl -Port 5000
    }

    $uri = $null
    if (-not [Uri]::TryCreate($rawUrl.Trim(), [UriKind]::Absolute, [ref]$uri)) {
        throw "The Development API URL '$rawUrl' is not a valid absolute URL."
    }

    $allowedHosts = @('localhost', '127.0.0.1', '::1')
    if ($uri.Scheme -ne 'http' -or $allowedHosts -notcontains $uri.Host.ToLowerInvariant()) {
        throw "The Development API URL must be an HTTP loopback URL, but '$rawUrl' was supplied."
    }

    if (($uri.Port -lt 1024) -or ($uri.Port -gt 65535) -or ($uri.AbsolutePath -ne '/') -or (-not [string]::IsNullOrWhiteSpace($uri.Query)) -or (-not [string]::IsNullOrWhiteSpace($uri.Fragment))) {
        throw "The Development API URL must contain only an HTTP loopback authority and port, but '$rawUrl' was supplied."
    }

    $hostPart = $uri.Host
    if ($hostPart.Contains(':')) {
        $hostPart = "[$hostPart]"
    }

    return [pscustomobject]@{
        ApiUrl   = "http://${hostPart}:$($uri.Port)"
        Port     = [int]$uri.Port
        Source   = $source
        Explicit = $explicit
    }
}

function Resolve-BackendTarget {
    param(
        [Parameter(Mandatory = $true)]$ConfiguredSelection
    )

    if ($ValidateOnly -and $ConfiguredSelection.Explicit) {
        return $ConfiguredSelection
    }

    if ($ConfiguredSelection.Explicit) {
        $candidatePorts = @($ConfiguredSelection.Port)
    }
    else {
        $candidatePorts = @(5000) + @(5300..5310)
    }

    foreach ($candidatePort in $candidatePorts) {
        $listeners = @(Get-ListeningProcessInfo -Port $candidatePort)
        if ($listeners.Count -eq 0) {
            if ($candidatePort -eq $ConfiguredSelection.Port) {
                return $ConfiguredSelection
            }

            return [pscustomobject]@{
                ApiUrl   = New-LocalApiUrl -Port $candidatePort
                Port     = $candidatePort
                Source   = "fallback from $($ConfiguredSelection.ApiUrl)"
                Explicit = $false
            }
        }

        if ($ConfiguredSelection.Explicit) {
            if ($Restart -and @($listeners | Where-Object { $_.Kind -eq 'backend' }).Count -eq $listeners.Count) {
                Stop-MiniErpListener -Port $candidatePort -ExpectedKind 'backend' -TimeoutSeconds $StartupTimeoutSeconds
                return $ConfiguredSelection
            }

            throw "Requested MiniERP API port $candidatePort is occupied by $(Get-ListenerSummary -Listeners $listeners). Use another -ApiPort or -Restart only for an existing repository-owned MiniERP API."
        }

        if ($Restart -and @($listeners | Where-Object { $_.Kind -eq 'backend' }).Count -eq $listeners.Count) {
            Stop-MiniErpListener -Port $candidatePort -ExpectedKind 'backend' -TimeoutSeconds $StartupTimeoutSeconds
            return [pscustomobject]@{
                ApiUrl   = if ($candidatePort -eq $ConfiguredSelection.Port) { $ConfiguredSelection.ApiUrl } else { New-LocalApiUrl -Port $candidatePort }
                Port     = $candidatePort
                Source   = if ($candidatePort -eq $ConfiguredSelection.Port) { $ConfiguredSelection.Source } else { "fallback from $($ConfiguredSelection.ApiUrl)" }
                Explicit = $false
            }
        }

        Write-Verbose "Leaving port $candidatePort untouched; it is occupied by $(Get-ListenerSummary -Listeners $listeners)."
    }

    throw 'No safe local MiniERP API port was available. Supply an explicit free -ApiPort value.'
}

function Write-GeneratedProxy {
    param(
        [Parameter(Mandatory = $true)][string]$TargetUrl
    )

    New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null

    $proxyDocument = [ordered]@{
        '/api' = [ordered]@{
            target       = $TargetUrl
            secure       = $false
            # Preserve the browser Host so the API can resolve the configured
            # common, Tenant, or platform entry mode. The target remains a
            # loopback transport detail, not the Tenant routing authority.
            changeOrigin = $false
            logLevel     = 'debug'
        }
    }

    $proxyJson = $proxyDocument | ConvertTo-Json -Depth 5
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($proxyPath, $proxyJson, $utf8WithoutBom)

    $written = Get-Content -LiteralPath $proxyPath -Raw | ConvertFrom-Json
    $writtenApi = $written.PSObject.Properties['/api'].Value
    if ($null -eq $writtenApi -or $writtenApi.target -ne $TargetUrl) {
        throw "Generated Angular proxy '$proxyPath' does not target '$TargetUrl'."
    }

    return $proxyPath
}

function Get-DevelopmentPassword {
    if ($null -ne $env:MESP_DEV_ADMIN_PASSWORD) {
        if ([string]::IsNullOrWhiteSpace($env:MESP_DEV_ADMIN_PASSWORD)) {
            throw 'MESP_DEV_ADMIN_PASSWORD was set but is empty.'
        }

        return [pscustomobject]@{
            Value  = $env:MESP_DEV_ADMIN_PASSWORD
            Source = 'MESP_DEV_ADMIN_PASSWORD'
        }
    }

    if ($ValidateOnly) {
        return $null
    }

    return [pscustomobject]@{
        Value  = '123'
        Source = 'Development default'
    }
}

function Quote-ProcessArgument {
    param(
        [Parameter(Mandatory = $true)][string]$Value
    )

    return '"' + $Value.Replace('"', '\"') + '"'
}

function Start-ManagedProcess {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][hashtable]$Environment,
        [Parameter(Mandatory = $true)][string]$StandardOutputPath,
        [Parameter(Mandatory = $true)][string]$StandardErrorPath
    )

    New-Item -ItemType Directory -Path (Split-Path -Parent $StandardOutputPath) -Force | Out-Null

    # Windows PowerShell 5.1 has no Start-Process -Environment parameter. Set
    # the launcher's process environment only for the synchronous
    # Start-Process call, then restore it immediately. The password is never
    # placed in command-line arguments or persisted by this script.
    $previousEnvironment = @{}
    foreach ($key in $Environment.Keys) {
        $previousEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        Set-Item -Path "Env:$key" -Value ([string]$Environment[$key])
    }

    try {
        return Start-Process `
            -FilePath $FilePath `
            -ArgumentList $Arguments `
            -WorkingDirectory $WorkingDirectory `
            -WindowStyle Hidden `
            -PassThru `
            -RedirectStandardOutput $StandardOutputPath `
            -RedirectStandardError $StandardErrorPath
    }
    finally {
        foreach ($key in $Environment.Keys) {
            $previousValue = $previousEnvironment[$key]
            if ($null -eq $previousValue) {
                Remove-Item -Path "Env:$key" -ErrorAction SilentlyContinue
            }
            else {
                Set-Item -Path "Env:$key" -Value $previousValue
            }
        }
    }
}

function Wait-ForHttpStatus {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [Parameter(Mandatory = $true)][int]$ExpectedStatus,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds,
        [Parameter(Mandatory = $true)]$Process,
        [Parameter(Mandatory = $true)][string]$ProcessDescription,
        [Parameter(Mandatory = $true)][string]$LogPath
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) {
            throw "$ProcessDescription exited before '$Url' became ready. See $LogPath."
        }

        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -Method Get -TimeoutSec 2
            if ([int]$response.StatusCode -eq $ExpectedStatus) {
                return
            }
        }
        catch {
            # The API/frontend can take a few seconds to bind after the child
            # process starts. The bounded retry is intentionally quiet.
        }

        Start-Sleep -Milliseconds 350
    }

    throw "Timed out waiting for $ProcessDescription at '$Url'. See $LogPath."
}

function Assert-FrontendPortAvailable {
    $listeners = @(Get-ListeningProcessInfo -Port $FrontendPort)
    if ($listeners.Count -eq 0) {
        return
    }

    if ($Restart -and @($listeners | Where-Object { $_.Kind -eq 'frontend' }).Count -eq $listeners.Count) {
        Stop-MiniErpListener -Port $FrontendPort -ExpectedKind 'frontend' -TimeoutSeconds $StartupTimeoutSeconds
        return
    }

    throw "Requested Angular port $FrontendPort is occupied by $(Get-ListenerSummary -Listeners $listeners). Use another -FrontendPort or -Restart only for an existing repository-owned MiniERP frontend."
}

function Write-ProcessState {
    param(
        [Parameter(Mandatory = $true)]$BackendProcess,
        [Parameter(Mandatory = $true)]$FrontendProcess,
        [Parameter(Mandatory = $true)]$BackendListener,
        [Parameter(Mandatory = $true)][string]$TargetUrl,
        [Parameter(Mandatory = $true)][string]$GeneratedProxyPath
    )

    $state = [ordered]@{
        startedAtUtc       = [DateTime]::UtcNow.ToString('o')
        backendLauncherPid = $BackendProcess.Id
        backendListenerPid = $BackendListener.ProcessId
        backendUrl         = $TargetUrl
        frontendPid        = $FrontendProcess.Id
        frontendUrl        = "http://localhost:$FrontendPort"
        generatedProxy     = $GeneratedProxyPath
    }

    $state | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $statePath -Encoding UTF8
}

$configuredSelection = Resolve-ConfiguredApiSelection
$target = Resolve-BackendTarget -ConfiguredSelection $configuredSelection
$generatedProxyPath = Write-GeneratedProxy -TargetUrl $target.ApiUrl

Write-Output "MiniERP Development API target: $($target.ApiUrl) [$($target.Source)]"
Write-Output "Angular proxy: $generatedProxyPath -> $($target.ApiUrl)"
Write-Output "Development bypass: $(if ($devAuthBypassEnabled) { 'enabled by -DevAuthBypass' } else { 'off' })"

if ($ValidateOnly) {
    Write-Output 'Runtime configuration validation complete; no processes were started or stopped.'
    return
}

if (-not (Test-Path -LiteralPath $backendProject)) {
    throw "Backend project was not found at '$backendProject'."
}

if (-not (Test-Path -LiteralPath $backendExecutable)) {
    throw "The Release API executable was not found at '$backendExecutable'. Run 'dotnet build backend/MiniErp.sln --configuration Release' before starting the Development runtime."
}

if (-not (Test-Path -LiteralPath (Join-Path $frontendRoot 'package.json'))) {
    throw "Angular package manifest was not found under '$frontendRoot'."
}

if (-not (Test-Path -LiteralPath (Join-Path $frontendRoot 'node_modules\@angular\cli\bin\ng.js'))) {
    throw "Angular dependencies are not installed. Run 'npm install' in '$frontendRoot' and retry."
}

Assert-FrontendPortAvailable
$password = $null
if (-not $devAuthBypassEnabled) {
    $password = Get-DevelopmentPassword
    if ($null -eq $password) {
        throw 'A Development password is required unless -ValidateOnly is used.'
    }
}

$tenantDisplayName = if ([string]::IsNullOrWhiteSpace($env:MESP_DEV_TENANT_DISPLAY_NAME)) {
    'Wafra'
} else {
    $env:MESP_DEV_TENANT_DISPLAY_NAME.Trim()
}

$nodePath = Get-ExecutablePath -Name 'node.exe'
$ngScript = Join-Path $frontendRoot 'node_modules\@angular\cli\bin\ng.js'

$backendArguments = '--urls ' + (Quote-ProcessArgument -Value $target.ApiUrl)
$frontendArguments = (Quote-ProcessArgument -Value $ngScript) +
    ' serve --configuration development --host localhost --port ' + $FrontendPort +
    ' --proxy-config ' + (Quote-ProcessArgument -Value $generatedProxyPath)

$backendEnvironment = @{
    ASPNETCORE_ENVIRONMENT    = 'Development'
    Scalar__Enabled            = 'true'
    MESP_DEV_BOOTSTRAP_ENABLED = 'true'
    MESP_DEV_AUTH_BYPASS       = if ($devAuthBypassEnabled) { 'true' } else { 'false' }
    MESP_DEV_TENANT_DISPLAY_NAME = $tenantDisplayName
    MESP_DEV_ADMIN_LOGIN      = $AdminLogin.Trim()
    MESP_DEV_ADMIN_PASSWORD   = if ($null -eq $password) { $null } else { $password.Value }
    MESP_DEV_API_URL           = $target.ApiUrl
}

try {
    Write-Output "Starting MiniERP backend executable on $($target.ApiUrl)."
    $backendProcess = Start-ManagedProcess `
        -FilePath $backendExecutable `
        -Arguments $backendArguments `
        -WorkingDirectory (Split-Path -Parent $backendExecutable) `
        -Environment $backendEnvironment `
        -StandardOutputPath $backendLogPath `
        -StandardErrorPath (Join-Path $runtimeDirectory 'backend.error.log')
    Write-Output "MiniERP backend process started with PID $($backendProcess.Id)."
}
finally {
    # Do not retain the password in this launcher scope after the child has
    # inherited its environment. It is never written to the runtime files.
    $password = $null
    $backendEnvironment['MESP_DEV_ADMIN_PASSWORD'] = $null
}

Wait-ForHttpStatus `
    -Url "$($target.ApiUrl)/health" `
    -ExpectedStatus 200 `
    -TimeoutSeconds $StartupTimeoutSeconds `
    -Process $backendProcess `
    -ProcessDescription 'MiniERP backend' `
    -LogPath $backendLogPath
Write-Output 'MiniERP backend health check passed.'

$backendListener = @(Get-ListeningProcessInfo -Port $target.Port | Where-Object { $_.Kind -eq 'backend' }) |
    Select-Object -First 1
if ($null -eq $backendListener) {
    throw "Health responded at $($target.ApiUrl), but the listener could not be proven to be the repository-owned MiniERP API."
}

try {
    Write-Output "Starting Angular development server on http://localhost:$FrontendPort."
    $frontendProcess = Start-ManagedProcess `
        -FilePath $nodePath `
        -Arguments $frontendArguments `
        -WorkingDirectory $frontendRoot `
        -Environment @{} `
        -StandardOutputPath $frontendLogPath `
        -StandardErrorPath (Join-Path $runtimeDirectory 'frontend.error.log')
    Write-Output "Angular process started with PID $($frontendProcess.Id)."
}
catch {
    Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
    throw
}

Wait-ForHttpStatus `
    -Url "http://localhost:$FrontendPort/" `
    -ExpectedStatus 200 `
    -TimeoutSeconds $StartupTimeoutSeconds `
    -Process $frontendProcess `
    -ProcessDescription 'Angular development server' `
    -LogPath $frontendLogPath
Write-Output 'Angular health check passed.'

$frontendListener = @(Get-ListeningProcessInfo -Port $FrontendPort | Where-Object { $_.Kind -eq 'frontend' }) |
    Select-Object -First 1
if ($null -eq $frontendListener) {
    throw "Angular responded on http://localhost:$FrontendPort, but the listener could not be proven to be the repository-owned Angular process."
}

Write-ProcessState `
    -BackendProcess $backendProcess `
    -FrontendProcess $frontendProcess `
    -BackendListener $backendListener `
    -TargetUrl $target.ApiUrl `
    -GeneratedProxyPath $generatedProxyPath

Write-Output ''
Write-Output 'MiniERP Development runtime is ready.'
Write-Output "Backend:  $($target.ApiUrl)"
Write-Output "Frontend: http://localhost:$FrontendPort"
Write-Output "Login:   $($AdminLogin.Trim())"
if ($devAuthBypassEnabled) {
    Write-Output 'Auth:    automatic loopback Development bypass is enabled; no password prompt was used.'
} else {
    $passwordSource = if ($null -ne $env:MESP_DEV_ADMIN_PASSWORD) { 'MESP_DEV_ADMIN_PASSWORD' } else { 'Development default' }
    Write-Output "Password: supplied by $passwordSource; not printed or persisted."
}
Write-Output "Tenant:  $tenantDisplayName (server-configured display name)"
Write-Output "Process state: $statePath"
Write-Output 'Use -Restart only to stop and replace these repository-owned MiniERP listeners.'
