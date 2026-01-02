param(
    [switch]$Release,         # сборка release (по умолчанию debug)
    [switch]$OpenLogOnFail    # открыть build.log при ошибке
)

# --- Базовые настройки ---
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

Push-Location $PSScriptRoot
$logFile = Join-Path $PSScriptRoot 'build.log'
"===== Build started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') =====" | Out-File -FilePath $logFile -Encoding UTF8

# --- Утилита: найти .cmd-шим вместо npm.ps1/npx.ps1 ---
function Find-CmdShim([string]$base) {
    $found = $null
    try {
        $found = & where.exe "$base.cmd" 2>$null | Select-Object -First 1
    } catch {}
    if (-not $found) {
        $candidate = Join-Path "${env:ProgramFiles}\nodejs" "$base.cmd"
        if (Test-Path $candidate) { $found = $candidate }
    }
    if ($found) { return $found } else { return $base }  # пусть PowerShell сам найдёт, если что
}

# --- Утилита: запуск внешнего инструмента с зеркалированием stdout+stderr в лог и консоль ---
function Invoke-Tool {
    param(
        [Parameter(Mandatory)][string]$Title,
        [Parameter(Mandatory)][string]$Exe,      # полный путь к .cmd/.bat/исполняемому файлу
        [string[]]$Args = @()
    )
    Write-Host $Title
    $prevEap = $ErrorActionPreference
    try {
        # Не превращать stderr из native-команды в terminating error внутри конвейера
        $ErrorActionPreference = 'Continue'
        & $Exe @Args 2>&1 | Tee-Object -FilePath $logFile -Append
        if ($LASTEXITCODE -ne 0) {
            throw "Step failed: $Title (exitcode $LASTEXITCODE)"
        }
    }
    finally {
        $ErrorActionPreference = $prevEap
    }
}

try {
    # --- Инструменты ---
    $npm = Find-CmdShim "npm"
    $npx = Find-CmdShim "npx"

    # --- Шаги сборки ---
    Invoke-Tool -Title "Installing dependencies..." `
        -Exe $npm -Args @("ci","--no-audit","--fund=false","--progress=false")

    Invoke-Tool -Title "Building Angular project..." `
        -Exe $npm -Args @("run","build")

    Invoke-Tool -Title "Syncing Capacitor Android platform..." `
        -Exe $npx -Args @("cap","sync","android")

    $gradleDir = Join-Path $PSScriptRoot "android"
    $gradlew = if (Test-Path (Join-Path $gradleDir "gradlew.bat")) {
        Join-Path $gradleDir "gradlew.bat"
    } else {
        Join-Path $gradleDir "gradlew"
    }

    Push-Location $gradleDir
    if ($Release) {
        Invoke-Tool -Title "Building release APK..." `
            -Exe $gradlew -Args @("assembleRelease","--no-daemon","--stacktrace","--info")
    } else {
        Invoke-Tool -Title "Building debug APK..." `
            -Exe $gradlew -Args @("assembleDebug","--no-daemon","--stacktrace","--info")
    }
    Pop-Location

    # --- Вывод найденных APK ---
    Write-Host "APK files:"
    Get-ChildItem "$gradleDir/app/build/outputs/apk" -Recurse -Filter *.apk -ErrorAction SilentlyContinue |
        ForEach-Object { $_.FullName } |
        Tee-Object -FilePath $logFile -Append |
        ForEach-Object { Write-Host " - $_" }

    "===== Build finished OK: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') =====" | Add-Content -Path $logFile -Encoding UTF8
}
catch {
    "===== Build FAILED: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') =====" | Add-Content -Path $logFile -Encoding UTF8
    "ERROR: $_" | Add-Content -Path $logFile -Encoding UTF8
    Write-Host "`nBuild failed. See log: $logFile"
    if ($OpenLogOnFail) { Start-Process "$logFile" }
    throw
}
finally {
    Pop-Location
}
