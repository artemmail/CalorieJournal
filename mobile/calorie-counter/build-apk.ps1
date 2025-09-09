param(
    [switch]$Release
)

$ErrorActionPreference = "Stop"

Push-Location $PSScriptRoot
try {
    Write-Host "Installing dependencies..."
    npm ci | Out-Null

    Write-Host "Building Angular project..."
    npm run build | Out-Null

    Write-Host "Syncing Capacitor Android platform..."
    npx cap sync android | Out-Null

    $gradleDir = Join-Path $PSScriptRoot "android"
    $gradlew = Join-Path $gradleDir "gradlew"
    if ($IsWindows) { $gradlew += ".bat" }

    Push-Location $gradleDir
    if ($Release) {
        Write-Host "Building release APK..."
        & $gradlew assembleRelease
    } else {
        Write-Host "Building debug APK..."
        & $gradlew assembleDebug
    }
    Pop-Location

    Write-Host "APK files:"
    Get-ChildItem "$gradleDir/app/build/outputs/apk" -Recurse -Filter *.apk | ForEach-Object {
        Write-Host (" - " + $_.FullName)
    }
}
finally {
    Pop-Location
}
