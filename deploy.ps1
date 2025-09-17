<#
.SYNOPSIS
Deploys the FoodBot solution by pulling the latest sources and publishing to IIS.

.DESCRIPTION
Stops the configured IIS web site and/or application pool, pulls the latest changes
from git, runs `dotnet publish` for the specified solution, copies the build output
into the target directory and finally restarts IIS. The script is intended to run on
Windows PowerShell with administrative privileges on the web server.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string]$RepositoryPath = (Resolve-Path -LiteralPath $PSScriptRoot).Path,

    [Parameter()]
    [string]$SolutionPath = "FoodBot/FoodBot.sln",

    [Parameter()]
    [string]$Configuration = "Release",

    [Parameter()]
    [string]$PublishDirectory = "C:\\inetpub\\wwwroot\\FoodBot",

    [Parameter()]
    [string]$Framework,

    [Parameter()]
    [string]$Runtime,

    [Parameter()]
    [string]$WebsiteName,

    [Parameter()]
    [string]$AppPoolName,

    [Parameter()]
    [switch]$SkipGitPull
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryFullPath = (Resolve-Path -LiteralPath $RepositoryPath).Path

if (-not [System.IO.Path]::IsPathRooted($PublishDirectory)) {
    $PublishDirectory = Join-Path -Path $repositoryFullPath -ChildPath $PublishDirectory
}
$PublishDirectory = [System.IO.Path]::GetFullPath($PublishDirectory)

if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
    throw "PublishDirectory must be specified."
}

$publishRoot = [System.IO.Path]::GetPathRoot($PublishDirectory)
if ($publishRoot -and ($PublishDirectory.TrimEnd('\\') -eq $publishRoot.TrimEnd('\\'))) {
    throw "PublishDirectory '$PublishDirectory' resolves to the root of drive '$publishRoot'. Aborting to avoid removing the entire drive contents."
}

if ([System.IO.Path]::IsPathRooted($SolutionPath)) {
    $solutionFullPath = (Resolve-Path -LiteralPath $SolutionPath).Path
} else {
    $solutionFullPath = (Resolve-Path -LiteralPath (Join-Path -Path $repositoryFullPath -ChildPath $SolutionPath)).Path
}

$shouldStartWebsite = $false
$shouldStartAppPool = $false

if ($WebsiteName -or $AppPoolName) {
    Write-Host "Importing WebAdministration module..."
    Import-Module WebAdministration -ErrorAction Stop
}

if ($WebsiteName) {
    $website = Get-Website -Name $WebsiteName -ErrorAction Stop
    if ($website.State -eq "Started") {
        Write-Host "Stopping IIS website '$WebsiteName'..."
        Stop-Website -Name $WebsiteName -ErrorAction Stop
        $shouldStartWebsite = $true
    } else {
        Write-Host "Website '$WebsiteName' is already stopped (state: $($website.State))."
    }
}

if ($AppPoolName) {
    $appPoolState = (Get-WebAppPoolState -Name $AppPoolName -ErrorAction Stop).Value
    if ($appPoolState -eq "Started") {
        Write-Host "Stopping IIS application pool '$AppPoolName'..."
        Stop-WebAppPool -Name $AppPoolName -ErrorAction Stop
        $shouldStartAppPool = $true
    } else {
        Write-Host "Application pool '$AppPoolName' is already stopped (state: $appPoolState)."
    }
}

$publishTemp = $null

try {
    Push-Location -LiteralPath $repositoryFullPath
    try {
        if (-not $SkipGitPull.IsPresent) {
            Write-Host "Pulling latest changes in '$repositoryFullPath'..."
            git pull --ff-only
            if ($LASTEXITCODE -ne 0) {
                throw "git pull failed with exit code $LASTEXITCODE."
            }
        } else {
            Write-Host "SkipGitPull specified - skipping git pull."
        }

        $publishTemp = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ("publish_" + [Guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $publishTemp -Force | Out-Null

        $publishArgs = @("publish", $solutionFullPath, "--configuration", $Configuration, "--output", $publishTemp)
        if ($Framework) { $publishArgs += @("--framework", $Framework) }
        if ($Runtime) { $publishArgs += @("--runtime", $Runtime) }

        Write-Host "Running: dotnet $($publishArgs -join ' ')"
        dotnet @publishArgs
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed with exit code $LASTEXITCODE."
        }

        if (-not (Test-Path -LiteralPath $PublishDirectory)) {
            Write-Host "Creating publish directory '$PublishDirectory'..."
            New-Item -ItemType Directory -Path $PublishDirectory -Force | Out-Null
        } else {
            Write-Host "Cleaning existing contents of '$PublishDirectory'..."
            Get-ChildItem -LiteralPath $PublishDirectory -Force -ErrorAction SilentlyContinue |
                Remove-Item -Recurse -Force -ErrorAction Stop
        }

        Write-Host "Copying published output to '$PublishDirectory'..."
        Copy-Item -Path (Join-Path -Path $publishTemp -ChildPath '*') -Destination $PublishDirectory -Recurse -Force
    }
    finally {
        Pop-Location
    }
}
finally {
    if ($publishTemp -and (Test-Path -LiteralPath $publishTemp)) {
        Remove-Item -LiteralPath $publishTemp -Recurse -Force
    }

    if ($AppPoolName -and $shouldStartAppPool) {
        try {
            Write-Host "Starting IIS application pool '$AppPoolName'..."
            Start-WebAppPool -Name $AppPoolName -ErrorAction Stop
        } catch {
            Write-Warning "Failed to start application pool '$AppPoolName': $_"
        }
    }

    if ($WebsiteName -and $shouldStartWebsite) {
        try {
            Write-Host "Starting IIS website '$WebsiteName'..."
            Start-Website -Name $WebsiteName -ErrorAction Stop
        } catch {
            Write-Warning "Failed to start website '$WebsiteName': $_"
        }
    }
}

Write-Host "Deployment completed successfully."
