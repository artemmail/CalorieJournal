param(
    [string]$Project = "FoodBot/FoodBot.csproj",
    [string]$Profile = "FoodBot",
    [string]$Url = "",
    [int]$TimeoutSeconds = 90,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

function Stop-FoodBotProcesses {
    $targets = Get-CimInstance Win32_Process |
        Where-Object {
            ($_.Name -ieq "FoodBot.exe" -or $_.Name -ieq "dotnet.exe") -and
            ($_.CommandLine -match "FoodBot")
        }

    foreach ($proc in $targets) {
        try {
            Stop-Process -Id $proc.ProcessId -Force -ErrorAction Stop
            Write-Host "Stopped PID $($proc.ProcessId) [$($proc.Name)]"
        }
        catch {
            Write-Warning "Failed to stop PID $($proc.ProcessId): $($_.Exception.Message)"
        }
    }
}

function Resolve-HealthUrl([string]$projectPath, [string]$profileName, [string]$urlValue) {
    if (-not [string]::IsNullOrWhiteSpace($urlValue)) {
        return $urlValue
    }

    try {
        $projectDir = Split-Path -Parent $projectPath
        $launchPath = Join-Path $projectDir "Properties/launchSettings.json"
        if (Test-Path -LiteralPath $launchPath) {
            $json = Get-Content -LiteralPath $launchPath -Raw | ConvertFrom-Json
            $appUrls = $json.profiles.$profileName.applicationUrl
            if (-not [string]::IsNullOrWhiteSpace($appUrls)) {
                $httpUrl = ($appUrls -split ";") | Where-Object { $_ -like "http://*" } | Select-Object -First 1
                if (-not [string]::IsNullOrWhiteSpace($httpUrl)) {
                    return $httpUrl.TrimEnd("/") + "/"
                }
            }
        }
    } catch {
        Write-Warning "Failed to parse launchSettings.json: $($_.Exception.Message)"
    }

    return "http://localhost:52803/"
}

if (-not (Test-Path -LiteralPath $Project)) {
    throw "Project file not found: $Project"
}

$Url = Resolve-HealthUrl -projectPath $Project -profileName $Profile -urlValue $Url

Write-Host "Preparing local run for $Project (profile: $Profile)"
Stop-FoodBotProcesses

if (-not $SkipBuild) {
    Write-Host "Building project..."
    & dotnet build $Project -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with code $LASTEXITCODE"
    }
}

$args = @(
    "run",
    "--project", $Project,
    "--no-build",
    "--launch-profile", $Profile
)

Write-Host "Starting app..."
$runner = Start-Process -FilePath "dotnet" -ArgumentList $args -PassThru
Write-Host "dotnet run PID: $($runner.Id)"

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$isReady = $false
$lastError = $null

while ((Get-Date) -lt $deadline) {
    if ($runner.HasExited) {
        throw "dotnet run exited early with code $($runner.ExitCode)"
    }

    try {
        $resp = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
        if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 500) {
            $isReady = $true
            break
        }
    }
    catch {
        $lastError = $_.Exception.Message
    }

    Start-Sleep -Milliseconds 600
}

if (-not $isReady) {
    throw "App did not become ready at $Url in $TimeoutSeconds sec. Last error: $lastError"
}

Write-Host "App is up: $Url"
Start-Process $Url | Out-Null
Write-Host "Browser open requested."
