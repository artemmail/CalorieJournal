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

function Get-AppPoolWorkerProcesses {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    try {
        return Get-CimInstance -Namespace "root\\WebAdministration" -ClassName "WorkerProcess" -Filter "AppPoolName='$Name'" -ErrorAction Stop
    } catch {
        Write-Verbose "Unable to query worker processes for app pool '$Name': $_"
        return @()
    }
}

function Stop-AppPoolAndWait {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter()]
        [int]$TimeoutSeconds = 30
    )

    $shouldRestart = $false

    $appPoolState = (Get-WebAppPoolState -Name $Name -ErrorAction Stop).Value
    if ($appPoolState -eq "Started") {
        Write-Host "Stopping IIS application pool '$Name'..."
        Stop-WebAppPool -Name $Name -ErrorAction Stop
        $shouldRestart = $true

        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $workerProcesses = @()
        do {
            Start-Sleep -Milliseconds 500
            $workerProcesses = Get-AppPoolWorkerProcesses -Name $Name
            if (-not $workerProcesses) { break }
        } while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds)

        if ($workerProcesses) {
            Write-Warning "Application pool '$Name' still has running worker process(es) after waiting $TimeoutSeconds seconds. Forcing termination of the remaining process(es)."
            foreach ($worker in $workerProcesses) {
                try {
                    Stop-Process -Id $worker.ProcessId -Force -ErrorAction Stop
                } catch {
                    Write-Warning "Failed to terminate worker process $($worker.ProcessId) for app pool '$Name': $_"
                }
            }

            Start-Sleep -Seconds 1
        }
    } else {
        Write-Host "Application pool '$Name' is already stopped (state: $appPoolState)."
    }

    return $shouldRestart
}

function Clear-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue | ForEach-Object {
        $item = $_
        try {
            try {
                [System.IO.File]::SetAttributes($item.FullName, [System.IO.FileAttributes]::Normal)
            } catch {
                Write-Verbose "Unable to reset attributes on '$($item.FullName)': $_"
            }

            Remove-Item -LiteralPath $item.FullName -Recurse -Force -ErrorAction Stop
        } catch {
            throw "Failed to remove '$($item.FullName)' while cleaning '$Path': $_"
        }
    }
}

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
$resolvedAppPoolName = $AppPoolName

if ($WebsiteName -or $AppPoolName) {
    Write-Host "Importing WebAdministration module..."
    Import-Module WebAdministration -ErrorAction Stop
}

if ($WebsiteName) {
    $website = Get-Website -Name $WebsiteName -ErrorAction Stop
    if (-not $resolvedAppPoolName -and $website.applicationPool) {
        $resolvedAppPoolName = $website.applicationPool
        Write-Host "Resolved application pool '$resolvedAppPoolName' for website '$WebsiteName'."
    }

    if ($website.State -eq "Started") {
        Write-Host "Stopping IIS website '$WebsiteName'..."
        Stop-Website -Name $WebsiteName -ErrorAction Stop
        $shouldStartWebsite = $true
    } else {
        Write-Host "Website '$WebsiteName' is already stopped (state: $($website.State))."
    }
}

if ($resolvedAppPoolName) {
    $shouldStartAppPool = Stop-AppPoolAndWait -Name $resolvedAppPoolName
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
            Clear-Directory -Path $PublishDirectory
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

    if ($resolvedAppPoolName -and $shouldStartAppPool) {
        try {
            Write-Host "Starting IIS application pool '$resolvedAppPoolName'..."
            Start-WebAppPool -Name $resolvedAppPoolName -ErrorAction Stop
        } catch {
            Write-Warning "Failed to start application pool '$resolvedAppPoolName': $_"
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
