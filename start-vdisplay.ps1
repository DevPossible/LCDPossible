#Requires -Version 7.0
<#
.SYNOPSIS
    Launches a VirtualLCD simulator instance for testing without hardware.
.DESCRIPTION
    Starts the VirtualLCD application from the build output folder.
    By default, simulates a Thermalright Trofeo Vision 360 ARGB display.
.PARAMETER Driver
    LCD driver to simulate. Use --list-drivers to see available options.
.PARAMETER Port
    UDP port to listen on. If not specified, auto-assigns from 5302.
.PARAMETER InstanceName
    Custom instance name for discovery.
.PARAMETER NoBuild
    Skip building the project before running.
.PARAMETER Stats
    Show statistics overlay on the display.
.PARAMETER AlwaysOnTop
    Keep the window above other windows.
.PARAMETER Borderless
    Hide window decorations.
.PARAMETER Scale
    Window scale factor (default: 1.0).
.PARAMETER ListDrivers
    List available drivers and exit.
.EXAMPLE
    ./start-vdisplay.ps1
    Launches default Trofeo Vision simulator with auto-assigned port.
.EXAMPLE
    ./start-vdisplay.ps1 -Port 5303 -InstanceName "Secondary"
    Launches with specific port and instance name.
.EXAMPLE
    ./start-vdisplay.ps1 -ListDrivers
    Shows available LCD drivers.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string]$Driver,

    [Parameter()]
    [int]$Port,

    [Parameter()]
    [string]$InstanceName,

    [Parameter()]
    [switch]$NoBuild,

    [Parameter()]
    [switch]$Stats,

    [Parameter()]
    [switch]$AlwaysOnTop,

    [Parameter()]
    [switch]$Borderless,

    [Parameter()]
    [double]$Scale,

    [Parameter()]
    [switch]$ListDrivers
)

$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$projectPath = Join-Path $projectRoot 'src/LCDPossible.VirtualLcd/LCDPossible.VirtualLcd.csproj'
$buildOutput = Join-Path $projectRoot '.build/LCDPossible.VirtualLcd/bin/Debug/net10.0'
$exePath = Join-Path $buildOutput 'VirtualLCD.exe'

# Build if needed
if (-not $NoBuild) {
    Write-Host "Building VirtualLCD..." -ForegroundColor Cyan
    & dotnet build $projectPath --configuration Debug --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}

# Verify exe exists
if (-not (Test-Path $exePath)) {
    Write-Error "Executable not found at: $exePath`nRun without -NoBuild to build first."
    exit 1
}

# Build arguments
$arguments = @()

if ($ListDrivers) {
    $arguments += '--list-drivers'
}
else {
    if ($Driver) {
        $arguments += @('-d', $Driver)
    }

    if ($Port -gt 0) {
        $arguments += @('-p', $Port)
    }

    if ($InstanceName) {
        $arguments += @('-n', $InstanceName)
    }

    if ($Stats) {
        $arguments += '--stats'
    }

    if ($AlwaysOnTop) {
        $arguments += '--always-on-top'
    }

    if ($Borderless) {
        $arguments += '--borderless'
    }

    if ($Scale -gt 0) {
        $arguments += @('--scale', $Scale)
    }
}

# Launch
Write-Host "Starting VirtualLCD..." -ForegroundColor Cyan
& $exePath @arguments
exit $LASTEXITCODE
