#Requires -Version 7.0
$ErrorActionPreference = 'Stop'

$BuildDir = Join-Path $PSScriptRoot '.build'

#region Tool Installation Functions
function Ensure-WingetPackage {
    param([string]$PackageId, [string]$Command)
    if (-not (Get-Command $Command -ErrorAction SilentlyContinue)) {
        Write-Host "Installing $PackageId via winget..." -ForegroundColor Yellow
        winget install --id $PackageId --silent --accept-package-agreements --accept-source-agreements
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
                    [System.Environment]::GetEnvironmentVariable("Path", "User")
    }
}

function Ensure-DotnetTool {
    param([string]$Package, [string]$Command = $Package)
    if (-not (Get-Command $Command -ErrorAction SilentlyContinue)) {
        Write-Host "Installing $Package via dotnet tool..." -ForegroundColor Yellow
        dotnet tool install -g $Package
    }
}

function Ensure-NpmTool {
    param([string]$Package, [string]$Command = $Package)
    if (-not (Get-Command $Command -ErrorAction SilentlyContinue)) {
        Write-Host "Installing $Package via npm..." -ForegroundColor Yellow
        npm install -g $Package
    }
}
#endregion

Push-Location $PSScriptRoot
try {
    # Ensure required tools are installed
    Ensure-WingetPackage -PackageId 'Microsoft.DotNet.SDK.10' -Command 'dotnet'

    # Clean previous build
    if (Test-Path $BuildDir) {
        Remove-Item $BuildDir -Recurse -Force
    }

    # Restore and build
    dotnet restore src/LCDPossible.sln
    dotnet build src/LCDPossible.sln --configuration Release --no-restore

    Write-Host "Build outputs: $BuildDir" -ForegroundColor Green
}
finally {
    Pop-Location
}
