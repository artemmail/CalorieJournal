param(
    [switch]$Release,
    [switch]$PublishSigned,
    [string]$KeystorePath,
    [string]$KeystorePassword,
    [string]$KeyAlias,
    [string]$KeyPassword
)

$ErrorActionPreference = "Stop"

Push-Location $PSScriptRoot
try {
    if ($PublishSigned -and -not $Release) {
        throw "-PublishSigned can be used only with -Release"
    }

    $requiredPackages = @(
        "node_modules/@angular/build/package.json",
        "node_modules/@capacitor/cli/package.json"
    )
    $missingPackages = $requiredPackages | Where-Object {
        -not (Test-Path (Join-Path $PSScriptRoot $_))
    }
    if ($missingPackages.Count -gt 0) {
        Write-Host "Installing dependencies..."
        npm ci | Out-Null
    } else {
        Write-Host "Dependencies already installed. Skipping npm ci."
    }

    Write-Host "Building Angular project..."
    npm run build | Out-Null

    Write-Host "Syncing Capacitor Android platform..."
    npx cap sync android | Out-Null

    $gradleDir = Join-Path $PSScriptRoot "android"

    Push-Location $gradleDir
    $gradlew = if (Test-Path ".\gradlew.bat") {
        ".\gradlew.bat"
    } elseif (Test-Path ".\gradlew") {
        ".\gradlew"
    } else {
        throw "Gradle wrapper not found in $gradleDir"
    }

    if ($Release) {
        $gradleArgs = @("assembleRelease", "--no-daemon")

        if ($PublishSigned) {
            if ([string]::IsNullOrWhiteSpace($KeystorePath)) {
                throw "For -PublishSigned provide -KeystorePath"
            }
            if ([string]::IsNullOrWhiteSpace($KeystorePassword)) {
                throw "For -PublishSigned provide -KeystorePassword"
            }
            if ([string]::IsNullOrWhiteSpace($KeyAlias)) {
                throw "For -PublishSigned provide -KeyAlias"
            }
            if ([string]::IsNullOrWhiteSpace($KeyPassword)) {
                throw "For -PublishSigned provide -KeyPassword"
            }

            $keystoreCandidate = if ([System.IO.Path]::IsPathRooted($KeystorePath)) {
                $KeystorePath
            } else {
                Join-Path $PSScriptRoot $KeystorePath
            }
            if (-not (Test-Path -LiteralPath $keystoreCandidate)) {
                throw "Keystore not found: $keystoreCandidate"
            }
            $resolvedKeystorePath = (Resolve-Path -LiteralPath $keystoreCandidate).Path

            $gradleArgs += "-Pandroid.injected.signing.store.file=$resolvedKeystorePath"
            $gradleArgs += "-Pandroid.injected.signing.store.password=$KeystorePassword"
            $gradleArgs += "-Pandroid.injected.signing.key.alias=$KeyAlias"
            $gradleArgs += "-Pandroid.injected.signing.key.password=$KeyPassword"

            Write-Host "Building release APK with publish signing..."
        } else {
            Write-Host "Building release APK..."
        }

        & $gradlew @gradleArgs
    } else {
        Write-Host "Building debug APK..."
        & $gradlew assembleDebug --no-daemon
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
