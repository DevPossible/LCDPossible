#Requires -Version 5.1
<#
.SYNOPSIS
    LCDPossible - Windows Full Installer

.DESCRIPTION
    Downloads and installs LCDPossible with all dependencies.
    This script is idempotent - safe to run multiple times.

.EXAMPLE
    # One-liner install:
    irm https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/install-windows.ps1 | iex

.NOTES
    Requires Administrator privileges for Windows Service installation.
#>

$ErrorActionPreference = 'Stop'

$Repo = "DevPossible/LCDPossible"
$InstallDir = "$env:ProgramFiles\LCDPossible"
$ConfigDir = "$env:ProgramData\LCDPossible"
$ServiceName = "LCDPossible"

function Write-Step {
    param([string]$Step, [string]$Message)
    Write-Host "[$Step] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor Gray
}

function Test-Admin {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-Architecture {
    if ([Environment]::Is64BitOperatingSystem) {
        return "win-x64"
    }
    return "win-x86"
}

# Banner
Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "  LCDPossible Installer (Windows)" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

# Check admin rights
if (-not (Test-Admin)) {
    Write-Host "This script requires Administrator privileges." -ForegroundColor Yellow
    Write-Host "Please run PowerShell as Administrator and try again." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Or run:" -ForegroundColor Gray
    Write-Host "  Start-Process powershell -Verb RunAs -ArgumentList '-Command irm https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/install-windows.ps1 | iex'" -ForegroundColor White
    exit 1
}

$Arch = Get-Architecture
Write-Info "Architecture: $Arch"

# Step 1: Fetch latest release
Write-Step "1/5" "Fetching latest release..."

try {
    $ReleaseInfo = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest" -UseBasicParsing
    $Version = $ReleaseInfo.tag_name
    $Asset = $ReleaseInfo.assets | Where-Object { $_.name -like "*$Arch*" } | Select-Object -First 1
    $DownloadUrl = $Asset.browser_download_url
}
catch {
    Write-Host "ERROR: Could not fetch release information." -ForegroundColor Red
    Write-Host "Please check https://github.com/$Repo/releases" -ForegroundColor Red
    exit 1
}

if (-not $Version -or -not $DownloadUrl) {
    Write-Host "ERROR: No release found for architecture: $Arch" -ForegroundColor Red
    exit 1
}

Write-Info "Latest version: $Version"
Write-Info "Download URL: $DownloadUrl"

# Check if already installed with same version
$VersionFile = Join-Path $InstallDir "version.json"
$SkipDownload = $false
if (Test-Path $VersionFile) {
    $InstalledVersion = (Get-Content $VersionFile | ConvertFrom-Json).Version
    if ($InstalledVersion -eq $Version.TrimStart('v')) {
        Write-Host ""
        Write-Host "  Version $Version is already installed." -ForegroundColor Yellow
        $response = Read-Host "  Reinstall? [y/N]"
        if ($response -notmatch '^[Yy]$') {
            Write-Info "Skipping download."
            $SkipDownload = $true
        }
    }
}

# Step 2: Download and extract
Write-Step "2/5" "Downloading and extracting..."

if (-not $SkipDownload) {
    $TempDir = Join-Path $env:TEMP "lcdpossible-install"
    $ZipFile = Join-Path $TempDir "lcdpossible.zip"

    # Clean up temp dir
    if (Test-Path $TempDir) {
        Remove-Item $TempDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

    Write-Info "Downloading..."
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $ZipFile -UseBasicParsing

    # Stop service if running
    Write-Info "Stopping service if running..."
    Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    # Extract
    Write-Info "Extracting to $InstallDir..."
    if (Test-Path $InstallDir) {
        Remove-Item $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Expand-Archive -Path $ZipFile -DestinationPath $InstallDir -Force

    # Clean up
    Remove-Item $TempDir -Recurse -Force
}

# Step 3: Setup configuration
Write-Step "3/5" "Setting up configuration..."

if (-not (Test-Path $ConfigDir)) {
    New-Item -ItemType Directory -Path $ConfigDir -Force | Out-Null
    $SourceConfig = Join-Path $InstallDir "appsettings.json"
    if (Test-Path $SourceConfig) {
        Copy-Item $SourceConfig -Destination (Join-Path $ConfigDir "appsettings.json")
        Write-Success "Created $ConfigDir\appsettings.json"
    }
}
else {
    Write-Info "Configuration already exists at $ConfigDir"
}

# Step 4: Add to PATH
Write-Step "4/5" "Updating PATH..."

$CurrentPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
if ($CurrentPath -notlike "*$InstallDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$CurrentPath;$InstallDir", "Machine")
    Write-Success "Added $InstallDir to PATH"
}
else {
    Write-Info "PATH already configured"
}

# Step 5: Install Windows Service
Write-Step "5/5" "Installing Windows Service..."

$ExePath = Join-Path $InstallDir "LCDPossible.exe"

# Remove existing service if present
$ExistingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($ExistingService) {
    Write-Info "Removing existing service..."
    Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Create new service
Write-Info "Creating service..."
$ServiceParams = @{
    Name = $ServiceName
    BinaryPathName = "`"$ExePath`" serve --service"
    DisplayName = "LCDPossible LCD Controller"
    Description = "Controls HID-based LCD screens"
    StartupType = "Automatic"
}
New-Service @ServiceParams | Out-Null

# Set service recovery options (restart on failure)
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

Write-Success "Service installed"

# Done!
Write-Host ""
Write-Host "==============================================" -ForegroundColor Green
Write-Host "  Installation Complete!" -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Installed:" -ForegroundColor White
Write-Host "  [✓] LCDPossible $Version" -ForegroundColor Green
Write-Host "  [✓] Windows Service (auto-start)" -ForegroundColor Green
Write-Host ""
Write-Host "Locations:" -ForegroundColor White
Write-Host "  Binary:  $InstallDir\LCDPossible.exe" -ForegroundColor Gray
Write-Host "  Config:  $ConfigDir\appsettings.json" -ForegroundColor Gray
Write-Host ""
Write-Host "Commands:" -ForegroundColor White
Write-Host "  Start service:   Start-Service $ServiceName" -ForegroundColor Gray
Write-Host "  Stop service:    Stop-Service $ServiceName" -ForegroundColor Gray
Write-Host "  View status:     Get-Service $ServiceName" -ForegroundColor Gray
Write-Host "  List devices:    LCDPossible list" -ForegroundColor Gray
Write-Host "  Run manually:    LCDPossible serve" -ForegroundColor Gray
Write-Host ""
Write-Host "Edit $ConfigDir\appsettings.json to configure your display." -ForegroundColor Yellow
Write-Host ""

# Ask to start service
$startNow = Read-Host "Start the service now? [Y/n]"
if ($startNow -notmatch '^[Nn]$') {
    Start-Service -Name $ServiceName
    Write-Host ""
    Write-Host "Service started!" -ForegroundColor Green
}
