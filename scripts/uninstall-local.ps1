#Requires -Version 7.0
<#
.SYNOPSIS
    Uninstall LCDPossible from a remote Linux host via SSH.

.DESCRIPTION
    Copies the appropriate uninstall script to the remote host and executes it
    to remove LCDPossible, its service, and optionally configuration files.

    Run this script from PowerShell where your SSH keys are configured.
    If 'ssh root@yourhost' works from your terminal, this script will work.

.PARAMETER TargetHost
    The target hostname or IP address. Required.
    Uses your SSH config, so hostnames like 'myserver' work if configured.

.PARAMETER User
    SSH username. Defaults to 'root'.

.PARAMETER Distro
    Target distribution for uninstall script. Defaults to 'ubuntu'.
    Valid values: ubuntu, proxmox, fedora, arch, debian, macos

.PARAMETER RemoveConfig
    Also remove configuration files from /etc/lcdpossible (or platform equivalent).

.PARAMETER SshKey
    Path to SSH private key file. Uses default SSH key if not specified.

.PARAMETER Port
    SSH port. Defaults to 22.

.PARAMETER Quiet
    Suppress non-essential output.

.EXAMPLE
    .\uninstall-local.ps1 -TargetHost myserver.local
    Uninstalls from myserver.local as root

.EXAMPLE
    .\uninstall-local.ps1 -TargetHost 192.168.1.100 -Distro proxmox -RemoveConfig
    Uninstalls from a Proxmox server and removes configuration

.EXAMPLE
    .\uninstall-local.ps1 -TargetHost mymac -User admin -Distro macos
    Uninstalls from a macOS machine
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [Alias('Host')]
    [string]$TargetHost,

    [string]$User = 'root',

    [ValidateSet('ubuntu', 'proxmox', 'fedora', 'arch', 'debian', 'macos')]
    [string]$Distro = 'ubuntu',

    [switch]$RemoveConfig,
    [switch]$Quiet,
    [string]$SshKey,
    [int]$Port = 22
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$ScriptRoot = $PSScriptRoot
$ProjectRoot = Split-Path $ScriptRoot -Parent

# Build SSH options array
$SshOptions = @('-o', 'StrictHostKeyChecking=accept-new')
if ($SshKey) {
    $SshOptions += @('-i', $SshKey)
}
if ($Port -ne 22) {
    $SshOptions += @('-p', $Port)
}

# SCP options (uses -P for port instead of -p)
$ScpOptions = @('-o', 'StrictHostKeyChecking=accept-new')
if ($SshKey) {
    $ScpOptions += @('-i', $SshKey)
}
if ($Port -ne 22) {
    $ScpOptions += @('-P', $Port)
}

$SshTarget = "$User@$TargetHost"

function Write-Step {
    param([string]$Step, [string]$Message)
    if (-not $Quiet) {
        Write-Host "`n[$Step] $Message" -ForegroundColor Cyan
    }
}

function Write-Success {
    param([string]$Message)
    if (-not $Quiet) {
        Write-Host "  [OK] $Message" -ForegroundColor Green
    }
}

function Write-Info {
    param([string]$Message)
    if (-not $Quiet) {
        Write-Host "  $Message" -ForegroundColor Gray
    }
}

function Copy-UninstallScript {
    Write-Step "1/2" "Copying uninstall script..."

    $scriptName = switch ($Distro) {
        'proxmox' { 'uninstall-ubuntu.sh' }  # Proxmox uses ubuntu script
        'fedora'  { 'uninstall-fedora.sh' }
        'arch'    { 'uninstall-arch.sh' }
        'debian'  { 'uninstall-ubuntu.sh' }  # Debian uses same script
        'macos'   { 'uninstall-macos.sh' }
        default   { 'uninstall-ubuntu.sh' }
    }

    $localScript = Join-Path $ScriptRoot $scriptName
    if (-not (Test-Path $localScript)) {
        throw "Uninstall script not found: $localScript"
    }

    $remoteTempDir = '/tmp/lcdpossible-uninstall'
    $remoteScript = "$remoteTempDir/uninstall.sh"

    # Create temp directory on remote
    ssh @SshOptions $SshTarget "mkdir -p $remoteTempDir" | Out-Null

    # Copy script
    scp @ScpOptions $localScript "${SshTarget}:${remoteScript}"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to copy uninstall script"
    }

    # Convert Windows line endings to Unix and make executable
    ssh @SshOptions $SshTarget "sed -i 's/\r$//' $remoteScript && chmod +x $remoteScript" | Out-Null

    Write-Success "Uninstall script ready ($scriptName)"
    return $remoteScript
}

function Invoke-RemoteUninstall {
    param([string]$RemoteScript)

    Write-Step "2/2" "Running uninstall on $TargetHost..."
    Write-Host ""

    # Build command with options
    $uninstallArgs = @()
    if ($RemoveConfig) {
        $uninstallArgs += '--remove-config'
    }
    if ($Quiet) {
        $uninstallArgs += '--quiet'
    }

    $argsString = $uninstallArgs -join ' '
    $uninstallCmd = "bash '$RemoteScript' $argsString"

    Write-Info "Running: $uninstallCmd"
    Write-Host ""

    # Stream output in real-time with TTY allocation
    # macOS doesn't need sudo for user-level uninstall
    if ($Distro -eq 'macos') {
        ssh @SshOptions -t $SshTarget $uninstallCmd
    }
    else {
        # Linux distros need sudo (or root)
        if ($User -eq 'root') {
            ssh @SshOptions -t $SshTarget $uninstallCmd
        }
        else {
            ssh @SshOptions -t $SshTarget "sudo $uninstallCmd"
        }
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "  [WARN] Uninstall may have encountered issues" -ForegroundColor Yellow
        Write-Host "  Check the output above and verify on the remote host" -ForegroundColor Yellow
    }
    else {
        Write-Host ""
        Write-Success "Uninstall completed successfully!"
    }

    # Cleanup temp files
    Write-Info "Cleaning up temporary files..."
    ssh @SshOptions $SshTarget "rm -rf /tmp/lcdpossible-uninstall" 2>$null
}

# Main execution
Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  LCDPossible Remote Uninstall" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Target:        $SshTarget" -ForegroundColor White
Write-Host "Distro:        $Distro" -ForegroundColor White
Write-Host "Remove config: $RemoveConfig" -ForegroundColor White
Write-Host ""

# Step 1: Copy uninstall script
$remoteScript = Copy-UninstallScript

# Step 2: Run uninstall
Invoke-RemoteUninstall -RemoteScript $remoteScript

Write-Host ""
Write-Host "=============================================" -ForegroundColor Green
Write-Host "  Uninstall Complete!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host ""

if (-not $RemoveConfig) {
    Write-Host "Configuration files were preserved." -ForegroundColor Yellow
    Write-Host "To also remove configuration, run with -RemoveConfig" -ForegroundColor Yellow
    Write-Host ""
}
