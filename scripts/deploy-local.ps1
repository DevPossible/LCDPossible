#Requires -Version 7.0
<#
.SYNOPSIS
    Deploy LCDPossible to a remote Linux host via SSH without using GitHub.

.DESCRIPTION
    Builds the solution for the target runtime, creates a tarball, copies it to
    the remote host via SCP, and runs the installation script.

    Run this script from PowerShell where your SSH keys are configured.
    If 'ssh root@yourhost' works from your terminal, this script will work.

.PARAMETER TargetHost
    The target hostname or IP address. Required.
    Uses your SSH config, so hostnames like 'myserver' work if configured.

.PARAMETER User
    SSH username. Defaults to 'root'.

.PARAMETER Runtime
    Target runtime identifier. Defaults to 'linux-x64'.
    Valid values: linux-x64, linux-arm64

.PARAMETER Distro
    Target distribution for install script. Defaults to 'ubuntu'.
    Valid values: ubuntu, proxmox, fedora, arch, debian

.PARAMETER SkipBuild
    Skip building and use existing tarball from .dist folder.

.PARAMETER SkipTests
    Skip running tests before packaging.

.PARAMETER SshKey
    Path to SSH private key file. Uses default SSH key if not specified.

.PARAMETER Port
    SSH port. Defaults to 22.

.PARAMETER Version
    Version to package. Auto-detected if not specified.

.EXAMPLE
    .\deploy-local.ps1 -TargetHost myserver.local
    Builds and deploys to myserver.local as root

.EXAMPLE
    .\deploy-local.ps1 -TargetHost 192.168.1.100 -User admin -Distro proxmox
    Deploys to a Proxmox server

.EXAMPLE
    .\deploy-local.ps1 -TargetHost mypi -Runtime linux-arm64
    Deploys to a Raspberry Pi or ARM64 device

.EXAMPLE
    .\deploy-local.ps1 -TargetHost myserver -SkipBuild
    Deploys using existing build in .dist folder

.EXAMPLE
    .\deploy-local.ps1 -TargetHost myserver -StayRemote
    Deploys and opens an interactive SSH session after completion
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [Alias('Host')]
    [string]$TargetHost,

    [string]$User = 'root',

    [ValidateSet('linux-x64', 'linux-arm64')]
    [string]$Runtime = 'linux-x64',

    [ValidateSet('ubuntu', 'proxmox', 'fedora', 'arch', 'debian')]
    [string]$Distro = 'ubuntu',

    [switch]$SkipBuild,
    [switch]$SkipTests,
    [switch]$StayRemote,
    [switch]$AutoCreateApiKey,
    [string]$SshKey,
    [int]$Port = 22,
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'  # Disable progress bars (messes up console output)

$ScriptRoot = $PSScriptRoot
$ProjectRoot = Split-Path $ScriptRoot -Parent
$DistDir = Join-Path $ProjectRoot '.dist'

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
    Write-Host "`n[$Step] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Gray
}

function Test-SshConnection {
    Write-Step "1/5" "Verifying SSH target: $SshTarget"
    Write-Success "Will connect to $SshTarget"
    Write-Info "(SSH connection verified on first remote command)"
    return $true
}

function Get-OrBuildPackage {
    Write-Step "2/5" "Preparing package..."

    # Look for existing tarball
    $tarballPattern = "lcdpossible-*-$Runtime.tar.gz"
    $existingTarball = Get-ChildItem -Path $DistDir -Filter $tarballPattern -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($SkipBuild -and $existingTarball) {
        Write-Info "Using existing package: $($existingTarball.Name)"
        return $existingTarball.FullName
    }

    if ($SkipBuild -and -not $existingTarball) {
        throw "No existing package found in $DistDir. Remove -SkipBuild to build first."
    }

    # Build and package
    Write-Info "Building for $Runtime..."

    # Build parameter hashtable for proper splatting
    $packageParams = @{
        Runtimes = @($Runtime)
        NonInteractive = $true
    }

    if ($SkipTests) {
        $packageParams.SkipTests = $true
    }

    if ($Version) {
        $packageParams.Version = $Version
    }

    Push-Location $ProjectRoot
    try {
        # Pipe to Out-Host so output displays but doesn't get captured as return value
        & (Join-Path $ProjectRoot 'package.ps1') @packageParams | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Package build failed"
        }
    }
    finally {
        Pop-Location
    }

    # Find the created tarball
    $tarball = Get-ChildItem -Path $DistDir -Filter $tarballPattern -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $tarball) {
        throw "Tarball not found after build. Check package.ps1 output."
    }

    Write-Success "Package ready: $($tarball.Name)"
    return $tarball.FullName
}

function Copy-PackageToHost {
    param([string]$TarballPath)

    Write-Step "3/5" "Copying package to $TargetHost..."

    $remoteTempDir = '/tmp/lcdpossible-deploy'
    $tarballName = Split-Path $TarballPath -Leaf
    $remoteTarball = "$remoteTempDir/$tarballName"

    # Create temp directory on remote
    ssh @SshOptions $SshTarget "mkdir -p $remoteTempDir" | Out-Null

    # Copy tarball
    $tarballSize = [math]::Round((Get-Item $TarballPath).Length / 1MB, 1)
    Write-Info "Uploading $tarballName ($tarballSize MB)..."

    scp @ScpOptions $TarballPath "${SshTarget}:${remoteTarball}"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to copy tarball to remote host"
    }

    Write-Success "Package uploaded"
    return $remoteTarball
}

function Copy-InstallScript {
    Write-Step "4/5" "Copying install script..."

    $scriptName = switch ($Distro) {
        'proxmox' { 'install-proxmox.sh' }
        'fedora'  { 'install-fedora.sh' }
        'arch'    { 'install-arch.sh' }
        'debian'  { 'install-ubuntu.sh' }  # Debian uses same script
        default   { 'install-ubuntu.sh' }
    }

    $localScript = Join-Path $ScriptRoot $scriptName
    if (-not (Test-Path $localScript)) {
        throw "Install script not found: $localScript"
    }

    $remoteTempDir = '/tmp/lcdpossible-deploy'
    $remoteScript = "$remoteTempDir/install.sh"

    scp @ScpOptions $localScript "${SshTarget}:${remoteScript}"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to copy install script"
    }

    # Convert Windows line endings to Unix and make executable
    ssh @SshOptions $SshTarget "sed -i 's/\r$//' $remoteScript && chmod +x $remoteScript" | Out-Null

    Write-Success "Install script ready ($scriptName)"
    return $remoteScript
}

function Invoke-RemoteInstall {
    param(
        [string]$RemoteTarball,
        [string]$RemoteScript
    )

    Write-Step "5/5" "Running installation on $TargetHost..."
    Write-Host ""

    # Build environment variables for install script
    $envVars = "export LOCAL_TARBALL='$RemoteTarball'"
    if ($AutoCreateApiKey) {
        $envVars += " && export AUTO_CREATE_API_KEY=true"
        Write-Info "Auto-create API key: enabled"
    }

    $installCmd = "$envVars && bash '$RemoteScript'"

    Write-Info "Remote tarball: $RemoteTarball"
    Write-Info "Running: $installCmd"
    Write-Host ""

    # Stream output in real-time with TTY allocation
    ssh @SshOptions -t $SshTarget $installCmd

    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "  [WARN] Installation may have encountered issues" -ForegroundColor Yellow
        Write-Host "  Check the output above and verify on the remote host" -ForegroundColor Yellow
    }
    else {
        Write-Host ""
        Write-Success "Installation completed successfully!"
    }

    # Cleanup temp files
    Write-Info "Cleaning up temporary files..."
    ssh @SshOptions $SshTarget "rm -rf /tmp/lcdpossible-deploy" 2>$null
}

# Main execution
Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  LCDPossible Local Deployment" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Target:   $SshTarget" -ForegroundColor White
Write-Host "Runtime:  $Runtime" -ForegroundColor White
Write-Host "Distro:   $Distro" -ForegroundColor White
Write-Host ""

# Step 1: Test SSH
if (-not (Test-SshConnection)) {
    exit 1
}

# Step 2: Build/get package
$tarball = Get-OrBuildPackage

# Step 3: Copy package
$remoteTarball = Copy-PackageToHost -TarballPath $tarball

# Step 4: Copy install script
$remoteScript = Copy-InstallScript

# Step 5: Run installation
Invoke-RemoteInstall -RemoteTarball $remoteTarball -RemoteScript $remoteScript

Write-Host ""
Write-Host "=============================================" -ForegroundColor Green
Write-Host "  Deployment Complete!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host ""

if ($StayRemote) {
    Write-Host "Connecting to remote host..." -ForegroundColor Yellow
    Write-Host ""
    # Start interactive SSH session
    ssh @SshOptions $SshTarget
} else {
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  ssh $SshTarget"
    Write-Host "  lcdpossible list"
    Write-Host "  lcdpossible status"
    Write-Host "  sudo journalctl -u lcdpossible -f"
    Write-Host ""
}
