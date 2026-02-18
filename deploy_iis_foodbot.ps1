param(
    [string]$SourceRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path,
    [string]$ProjectRelativePath = "FoodBot\FoodBot.csproj",
    [string]$SiteName = "footbot",
    [string]$AppPool = "",
    [string]$TargetPath = "C:\fb",
    [string]$Configuration = "Release",
    [string]$PublishOutput = "C:\fb_publish",
    [switch]$SkipBuild,
    [int]$StopTimeoutSec = 60,
    [string]$HealthUrl = "",
    [int]$HealthTimeoutSec = 60,
    [int]$HealthRetryDelaySec = 2
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "[deploy] $Message"
}

function Resolve-AppCmdPath {
    $path = Join-Path $env:windir "System32\inetsrv\appcmd.exe"
    if (Test-Path $path) {
        return $path
    }

    throw "IIS appcmd not found: $path. Install IIS Management Scripts and Tools."
}

function Invoke-AppCmd([string]$AppCmdPath, [string[]]$Arguments) {
    $output = & $AppCmdPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "appcmd failed: $($Arguments -join ' ')`n$output"
    }

    return ($output | Out-String).Trim()
}

function Try-Invoke-AppCmd([string]$AppCmdPath, [string[]]$Arguments) {
    $output = & $AppCmdPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        return @{
            Success = $false
            Output = ($output | Out-String).Trim()
        }
    }

    return @{
        Success = $true
        Output = ($output | Out-String).Trim()
    }
}

function Get-AppPoolState([string]$AppCmdPath, [string]$PoolName) {
    return (Invoke-AppCmd $AppCmdPath @("list", "apppool", $PoolName, "/text:state")).Trim()
}

function Resolve-AppPoolName([string]$AppCmdPath, [string]$SiteName, [string]$AppPoolParam) {
    if (-not [string]::IsNullOrWhiteSpace($AppPoolParam)) {
        return $AppPoolParam
    }

    $siteCheck = Try-Invoke-AppCmd $AppCmdPath @("list", "site", $SiteName, "/text:name")
    if (-not $siteCheck.Success -or [string]::IsNullOrWhiteSpace($siteCheck.Output)) {
        throw "IIS site '$SiteName' not found. Pass an existing site name via -SiteName."
    }

    $resolved = Try-Invoke-AppCmd $AppCmdPath @("list", "app", "$SiteName/", "/text:applicationPool")
    if ($resolved.Success -and -not [string]::IsNullOrWhiteSpace($resolved.Output)) {
        return $resolved.Output
    }

    $sitePool = Try-Invoke-AppCmd $AppCmdPath @("list", "site", $SiteName, "/text:applicationPool")
    if ($sitePool.Success -and -not [string]::IsNullOrWhiteSpace($sitePool.Output)) {
        return $sitePool.Output
    }

    try {
        Import-Module WebAdministration -ErrorAction Stop
        $ws = Get-Website -Name $SiteName -ErrorAction Stop
        if (-not [string]::IsNullOrWhiteSpace($ws.applicationPool)) {
            return $ws.applicationPool
        }
    }
    catch {
        # ignore and throw deterministic guidance below
    }

    throw "Cannot resolve app pool for IIS site '$SiteName'. Set it explicitly via -AppPool."
}

function Wait-AppPoolStopped([string]$AppCmdPath, [string]$PoolName, [int]$TimeoutSec) {
    $started = Get-Date
    while ((Get-AppPoolState $AppCmdPath $PoolName) -ne "Stopped") {
        Start-Sleep -Seconds 1
        if (((Get-Date) - $started).TotalSeconds -gt $TimeoutSec) {
            throw "App pool '$PoolName' did not stop within $TimeoutSec seconds."
        }
    }
}

function Wait-Health([string]$Url, [int]$TimeoutSec, [int]$RetryDelaySec) {
    $started = Get-Date
    $lastError = $null

    while (((Get-Date) - $started).TotalSeconds -lt $TimeoutSec) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 10
            return $response.StatusCode
        }
        catch {
            $lastError = $_.Exception.Message
            Start-Sleep -Seconds $RetryDelaySec
        }
    }

    throw "Health check failed after $TimeoutSec sec for '$Url'. Last error: $lastError"
}

$appCmdPath = Resolve-AppCmdPath
Write-Step "Using appcmd: $appCmdPath"

$projectPath = Join-Path $SourceRoot $ProjectRelativePath
if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

$resolvedPool = Resolve-AppPoolName -AppCmdPath $appCmdPath -SiteName $SiteName -AppPoolParam $AppPool

if (-not (Test-Path $TargetPath)) {
    Write-Step "Creating target directory: $TargetPath"
    New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
}

if (Test-Path $PublishOutput) {
    Write-Step "Cleaning publish directory: $PublishOutput"
    Remove-Item -LiteralPath $PublishOutput -Recurse -Force
}
New-Item -ItemType Directory -Path $PublishOutput -Force | Out-Null

$dotnetArgs = @("publish", $projectPath, "-c", $Configuration, "-o", $PublishOutput, "--nologo")
if ($SkipBuild) {
    $dotnetArgs += "--no-build"
}

Write-Step "dotnet $($dotnetArgs -join ' ')"
& dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$appOfflinePath = Join-Path $TargetPath "app_offline.htm"

try {
    Write-Step "Creating app_offline.htm"
    Set-Content -LiteralPath $appOfflinePath -Value "<html><body>Updating site...</body></html>" -Encoding UTF8

    Write-Step "Stopping site: $SiteName"
    Invoke-AppCmd $appCmdPath @("stop", "site", "/site.name:$SiteName") | Out-Null

    Write-Step "Stopping app pool: $resolvedPool"
    Invoke-AppCmd $appCmdPath @("stop", "apppool", "/apppool.name:$resolvedPool") | Out-Null

    Wait-AppPoolStopped -AppCmdPath $appCmdPath -PoolName $resolvedPool -TimeoutSec $StopTimeoutSec

    Write-Step "Copying publish output to: $TargetPath"
    $roboArgs = @(
        $PublishOutput,
        $TargetPath,
        "/MIR",
        "/R:2",
        "/W:2",
        "/NFL",
        "/NDL",
        "/NP"
    )

    & robocopy @roboArgs
    $robocopyCode = $LASTEXITCODE
    if ($robocopyCode -ge 8) {
        throw "robocopy failed with exit code $robocopyCode."
    }

    Write-Step "Removing app_offline.htm"
    Remove-Item -LiteralPath $appOfflinePath -Force -ErrorAction SilentlyContinue

    Write-Step "Starting app pool: $resolvedPool"
    Invoke-AppCmd $appCmdPath @("start", "apppool", "/apppool.name:$resolvedPool") | Out-Null

    Write-Step "Starting site: $SiteName"
    Invoke-AppCmd $appCmdPath @("start", "site", "/site.name:$SiteName") | Out-Null

    if (-not [string]::IsNullOrWhiteSpace($HealthUrl)) {
        Write-Step "Health check: $HealthUrl"
        $statusCode = Wait-Health -Url $HealthUrl -TimeoutSec $HealthTimeoutSec -RetryDelaySec $HealthRetryDelaySec
        Write-Step "Health check status: $statusCode"
    }

    Write-Step "Deploy completed."
}
catch {
    Write-Error $_
    throw
}
finally {
    if (Test-Path $appOfflinePath) {
        Remove-Item -LiteralPath $appOfflinePath -Force -ErrorAction SilentlyContinue
    }
}
