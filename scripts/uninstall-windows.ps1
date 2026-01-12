#Requires -Version 5.1
<#
.SYNOPSIS
    LCDPossible - Windows Uninstaller

.DESCRIPTION
    Removes LCDPossible installation including:
    - Windows Service
    - Program Files installation
    - PATH entry
    - Optionally, configuration files

.PARAMETER RemoveConfig
    Also remove configuration files from ProgramData.

.PARAMETER Quiet
    Suppress non-essential output.

.EXAMPLE
    # Uninstall, keeping configuration
    .\uninstall-windows.ps1

.EXAMPLE
    # Uninstall and remove all configuration
    .\uninstall-windows.ps1 -RemoveConfig

.NOTES
    Requires Administrator privileges.
#>

param(
    [switch]$RemoveConfig,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

$InstallDir = "$env:ProgramFiles\LCDPossible"
$ConfigDir = "$env:ProgramData\LCDPossible"
$ServiceName = "LCDPossible"

function Write-Step {
    param([string]$Step, [string]$Message)
    if (-not $Quiet) {
        Write-Host "[$Step] $Message" -ForegroundColor Cyan
    }
}

function Write-Success {
    param([string]$Message)
    if (-not $Quiet) {
        Write-Host "    [OK] $Message" -ForegroundColor Green
    }
}

function Write-Info {
    param([string]$Message)
    if (-not $Quiet) {
        Write-Host "    $Message" -ForegroundColor Gray
    }
}

function Write-Warn {
    param([string]$Message)
    Write-Host "    [!] $Message" -ForegroundColor Yellow
}

function Test-Admin {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Banner
Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "  LCDPossible Uninstaller (Windows)" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

# Check admin rights
if (-not (Test-Admin)) {
    Write-Host "This script requires Administrator privileges." -ForegroundColor Yellow
    Write-Host "Please run PowerShell as Administrator and try again." -ForegroundColor Yellow
    exit 1
}

# Check if installed
$ServiceExists = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
$InstallExists = Test-Path $InstallDir
$PathContains = ([Environment]::GetEnvironmentVariable("Path", "Machine") -like "*$InstallDir*")

if (-not $ServiceExists -and -not $InstallExists -and -not $PathContains) {
    Write-Warn "LCDPossible does not appear to be installed"
    exit 0
}

# Step 1: Stop and remove service
Write-Step "1/4" "Stopping and removing Windows Service..."

if ($ServiceExists) {
    # Stop service if running
    if ($ServiceExists.Status -eq 'Running') {
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Info "Service stopped"
    }
    else {
        Write-Info "Service was not running"
    }

    # Delete service
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
    Write-Success "Service removed"
}
else {
    Write-Info "Service not found"
}

# Step 2: Remove from PATH
Write-Step "2/4" "Removing from PATH..."

$CurrentPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
if ($CurrentPath -like "*$InstallDir*") {
    # Remove the install dir from PATH
    $NewPath = ($CurrentPath -split ';' | Where-Object { $_ -ne $InstallDir -and $_ -ne "" }) -join ';'
    [Environment]::SetEnvironmentVariable("Path", $NewPath, "Machine")
    Write-Success "Removed from PATH"
}
else {
    Write-Info "PATH entry not found"
}

# Step 3: Remove installation files
Write-Step "3/4" "Removing installed files..."

if (Test-Path $InstallDir) {
    # Retry removal a few times in case files are still locked
    $retries = 3
    $removed = $false
    for ($i = 0; $i -lt $retries; $i++) {
        try {
            Remove-Item $InstallDir -Recurse -Force -ErrorAction Stop
            $removed = $true
            break
        }
        catch {
            if ($i -lt ($retries - 1)) {
                Start-Sleep -Seconds 2
            }
        }
    }

    if ($removed) {
        Write-Success "Installation directory removed: $InstallDir"
    }
    else {
        Write-Warn "Could not remove $InstallDir - files may be in use"
        Write-Info "Please delete manually after restart"
    }
}
else {
    Write-Info "Installation directory not found"
}

# Step 4: Optionally remove configuration
Write-Step "4/4" "Checking configuration..."

if ($RemoveConfig) {
    if (Test-Path $ConfigDir) {
        Remove-Item $ConfigDir -Recurse -Force
        Write-Success "Configuration directory removed: $ConfigDir"
    }
    else {
        Write-Info "Configuration directory not found"
    }
}
else {
    if (Test-Path $ConfigDir) {
        Write-Info "Configuration preserved at $ConfigDir"
        Write-Info "Use -RemoveConfig to delete"
    }
}

# Done!
Write-Host ""
Write-Host "==============================================" -ForegroundColor Green
Write-Host "  LCDPossible uninstalled successfully!" -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Green
Write-Host ""

if (-not $RemoveConfig -and (Test-Path $ConfigDir)) {
    Write-Host "To also remove configuration files, run:" -ForegroundColor Gray
    Write-Host "    .\uninstall-windows.ps1 -RemoveConfig" -ForegroundColor White
    Write-Host ""
}

Write-Host "Note: Open a new terminal for PATH changes to take effect." -ForegroundColor Yellow
Write-Host ""
