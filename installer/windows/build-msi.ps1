#Requires -Version 7.0
<#
.SYNOPSIS
    Builds the LCDPossible MSI installer using WiX Toolset.

.DESCRIPTION
    This script:
    1. Publishes the application if not already done
    2. Harvests all published files into a WiX fragment
    3. Compiles the MSI using WiX Toolset v4+

.PARAMETER Version
    The version number for the installer (e.g., "1.0.0")

.PARAMETER Runtime
    The target runtime (default: win-x64)

.PARAMETER OutputDir
    Where to place the final MSI (default: .dist/installers)

.PARAMETER SkipPublish
    Skip the dotnet publish step if files already exist

.EXAMPLE
    ./build-msi.ps1 -Version "1.0.0"

.EXAMPLE
    ./build-msi.ps1 -Version "1.0.0" -SkipPublish
#>

param(
    [Parameter(Mandatory)]
    [string]$Version,

    [ValidateSet("win-x64", "win-x86", "win-arm64")]
    [string]$Runtime = "win-x64",

    [string]$OutputDir = $null,

    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$InstallerDir = $PSScriptRoot
$PublishDir = Join-Path $ProjectRoot ".build/publish/LCDPossible/$Runtime"
$OutputDir = if ($OutputDir) { $OutputDir } else { Join-Path $ProjectRoot '.dist/installers' }
$WixObjDir = Join-Path $ProjectRoot '.build/wix'

Write-Host ""
Write-Host "=== Building LCDPossible MSI Installer ===" -ForegroundColor Cyan
Write-Host "Version:    $Version" -ForegroundColor Yellow
Write-Host "Runtime:    $Runtime" -ForegroundColor Yellow
Write-Host "Output:     $OutputDir" -ForegroundColor Yellow
Write-Host ""

# Ensure WiX is installed
function Ensure-WixToolset {
    # Check for dotnet tool
    $wixTool = dotnet tool list -g | Select-String "wix"
    if (-not $wixTool) {
        Write-Host "[1/5] Installing WiX Toolset..." -ForegroundColor Cyan
        dotnet tool install -g wix
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install WiX Toolset. Install manually: dotnet tool install -g wix"
        }
        # Add WiX UI extension
        wix extension add WixToolset.UI.wixext
        Write-Host "    [OK] WiX Toolset installed" -ForegroundColor Green
    }
    else {
        Write-Host "[1/5] WiX Toolset already installed" -ForegroundColor Cyan
        Write-Host "    [OK] Found WiX" -ForegroundColor Green
    }

    # Ensure UI extension is available
    $extensions = wix extension list 2>$null
    if ($extensions -notmatch "WixToolset.UI.wixext") {
        Write-Host "    Installing WixUI extension..." -ForegroundColor Yellow
        wix extension add WixToolset.UI.wixext
    }
}

# Publish the application
function Publish-Application {
    if ($SkipPublish -and (Test-Path $PublishDir)) {
        Write-Host "[2/5] Skipping publish (using existing files)" -ForegroundColor Cyan
        Write-Host "    [OK] Found $PublishDir" -ForegroundColor Green
        return
    }

    Write-Host "[2/5] Publishing application..." -ForegroundColor Cyan

    Push-Location $ProjectRoot
    try {
        dotnet publish src/LCDPossible/LCDPossible.csproj `
            --configuration Release `
            --runtime $Runtime `
            --self-contained true `
            -p:PublishSingleFile=false `
            -p:Version=$Version `
            --output $PublishDir

        if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
        Write-Host "    [OK] Published to $PublishDir" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

# Generate WiX source with all files
function Generate-WixSource {
    Write-Host "[3/5] Generating WiX source..." -ForegroundColor Cyan

    # Get all files from publish directory
    $files = Get-ChildItem -Path $PublishDir -Recurse -File

    # Generate unique GUIDs for components (deterministic based on path)
    function Get-DeterministicGuid {
        param([string]$Input)
        $md5 = [System.Security.Cryptography.MD5]::Create()
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Input)
        $hash = $md5.ComputeHash($bytes)
        return [Guid]::new($hash).ToString().ToUpper()
    }

    # Build file components XML
    $fileComponents = @()
    $componentRefs = @()
    $fileId = 0

    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring($PublishDir.Length + 1)
        $fileId++
        $componentId = "Component_$fileId"
        $fileGuid = Get-DeterministicGuid -Input "LCDPossible_$relativePath"

        # Handle subdirectories
        $dirParts = $relativePath.Split('\')
        if ($dirParts.Length -gt 1) {
            # File is in a subdirectory - we'll handle these with Directory elements
            continue
        }

        $fileComponents += @"

      <Component Id="$componentId" Guid="$fileGuid" Directory="INSTALLFOLDER">
        <File Id="File_$fileId" Source="$($file.FullName)" KeyPath="yes" />
      </Component>
"@
        $componentRefs += "      <ComponentRef Id=`"$componentId`" />"
    }

    # Handle plugins directory
    $pluginsDir = Join-Path $PublishDir "plugins"
    if (Test-Path $pluginsDir) {
        $pluginFiles = Get-ChildItem -Path $pluginsDir -Recurse -File
        foreach ($file in $pluginFiles) {
            $relativePath = $file.FullName.Substring($pluginsDir.Length + 1)
            $fileId++
            $componentId = "PluginComponent_$fileId"
            $fileGuid = Get-DeterministicGuid -Input "LCDPossible_plugin_$relativePath"

            $fileComponents += @"

      <Component Id="$componentId" Guid="$fileGuid" Directory="PluginsFolder">
        <File Id="PluginFile_$fileId" Source="$($file.FullName)" KeyPath="yes" />
      </Component>
"@
            $componentRefs += "      <ComponentRef Id=`"$componentId`" />"
        }
    }

    # Generate the complete WXS file
    $wxsContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui">

  <!--
    LCDPossible MSI Installer
    Generated by build-msi.ps1
    Version: $Version

    This installer:
    - Installs to Program Files\LCDPossible
    - Adds install directory to system PATH
    - Installs and starts the Windows Service
    - Supports upgrades (same UpgradeCode)
  -->

  <Package Name="LCDPossible"
           Manufacturer="DevPossible"
           Version="$Version"
           UpgradeCode="7E8F4A2B-1C3D-4E5F-9A0B-2C3D4E5F6A7B"
           Scope="perMachine"
           Compressed="yes">

    <SummaryInformation Description="LCDPossible LCD Controller Service - Controls HID-based LCD screens"
                        Manufacturer="DevPossible" />

    <!-- Upgrade handling - removes previous versions -->
    <MajorUpgrade DowngradeErrorMessage="A newer version of [ProductName] is already installed."
                  Schedule="afterInstallInitialize" />

    <!-- Embed cabinet in MSI for single-file distribution -->
    <MediaTemplate EmbedCab="yes" CompressionLevel="high" />

    <!-- Installation directory -->
    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER" Name="LCDPossible">
        <Directory Id="PluginsFolder" Name="plugins" />
      </Directory>
    </StandardDirectory>

    <!-- Add to PATH -->
    <Component Id="PathEnvironment" Guid="D4E5F6A7-B8C9-0123-DEF0-234567890123" Directory="INSTALLFOLDER">
      <Environment Id="PATH" Name="PATH" Value="[INSTALLFOLDER]" Permanent="no" Part="last" Action="set" System="yes" />
    </Component>

    <!-- Windows Service -->
    <Component Id="ServiceComponent" Guid="E5F6A7B8-C9D0-1234-EF01-345678901234" Directory="INSTALLFOLDER">
      <ServiceInstall Id="LCDPossibleService"
                      Type="ownProcess"
                      Name="LCDPossible"
                      DisplayName="LCDPossible LCD Controller"
                      Description="Controls HID-based LCD screens for system monitoring and custom displays."
                      Start="auto"
                      Account="LocalSystem"
                      ErrorControl="normal"
                      Arguments="serve --service" />
      <ServiceControl Id="StartService"
                      Start="install"
                      Stop="both"
                      Remove="uninstall"
                      Name="LCDPossible"
                      Wait="yes" />
    </Component>

    <!-- File Components -->
$($fileComponents -join "`n")

    <!-- Main Feature -->
    <Feature Id="MainFeature" Title="LCDPossible" Level="1" AllowAbsent="no">
$($componentRefs -join "`n")
      <ComponentRef Id="PathEnvironment" />
      <ComponentRef Id="ServiceComponent" />
    </Feature>

    <!-- Standard UI with install directory selection -->
    <Property Id="WIXUI_INSTALLDIR" Value="INSTALLFOLDER" />
    <ui:WixUI Id="WixUI_InstallDir" />

    <!-- License file -->
    <WixVariable Id="WixUILicenseRtf" Value="$InstallerDir\License.rtf" />

    <!-- Icon for Add/Remove Programs -->
    <Icon Id="ProductIcon" SourceFile="$PublishDir\LCDPossible.exe" />
    <Property Id="ARPPRODUCTICON" Value="ProductIcon" />
    <Property Id="ARPURLINFOABOUT" Value="https://github.com/DevPossible/lcd-possible" />

  </Package>
</Wix>
"@

    $wxsPath = Join-Path $WixObjDir "Product.wxs"
    New-Item -ItemType Directory -Path $WixObjDir -Force | Out-Null
    Set-Content -Path $wxsPath -Value $wxsContent -Encoding UTF8
    Write-Host "    [OK] Generated $wxsPath" -ForegroundColor Green

    return $wxsPath
}

# Create license file if it doesn't exist
function Ensure-LicenseFile {
    $licenseRtf = Join-Path $InstallerDir "License.rtf"
    if (-not (Test-Path $licenseRtf)) {
        Write-Host "    Creating placeholder license file..." -ForegroundColor Yellow

        # Check for LICENSE in project root
        $licenseTxt = Join-Path $ProjectRoot "LICENSE"
        if (Test-Path $licenseTxt) {
            $licenseText = Get-Content $licenseTxt -Raw
        }
        else {
            $licenseText = @"
MIT License

Copyright (c) 2024 DevPossible

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
"@
        }

        # Convert to RTF format
        $rtfContent = "{\rtf1\ansi\deff0 {\fonttbl {\f0 Consolas;}}`n\f0\fs20 "
        $rtfContent += $licenseText -replace "`r`n", "\par`n" -replace "`n", "\par`n"
        $rtfContent += "`n}"

        Set-Content -Path $licenseRtf -Value $rtfContent -Encoding ASCII
        Write-Host "    [OK] Created $licenseRtf" -ForegroundColor Green
    }
}

# Build the MSI
function Build-Msi {
    param([string]$WxsPath)

    Write-Host "[4/5] Building MSI..." -ForegroundColor Cyan

    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

    $msiName = "LCDPossible-$Version-$Runtime.msi"
    $msiPath = Join-Path $OutputDir $msiName

    # Build with WiX
    Push-Location $WixObjDir
    try {
        wix build $WxsPath -o $msiPath -ext WixToolset.UI.wixext

        if ($LASTEXITCODE -ne 0) {
            throw "WiX build failed"
        }

        Write-Host "    [OK] Built $msiPath" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }

    return $msiPath
}

# Clean up
function Cleanup {
    Write-Host "[5/5] Cleaning up..." -ForegroundColor Cyan
    # Keep the WixObjDir for debugging, just note it
    Write-Host "    [OK] Build artifacts in $WixObjDir" -ForegroundColor Green
}

# Main
try {
    Ensure-WixToolset
    Publish-Application
    Ensure-LicenseFile
    $wxsPath = Generate-WixSource
    $msiPath = Build-Msi -WxsPath $wxsPath
    Cleanup

    Write-Host ""
    Write-Host "=== MSI Build Complete ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "Output: $msiPath" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "The MSI installer will:" -ForegroundColor White
    Write-Host "  - Install to C:\Program Files\LCDPossible" -ForegroundColor Gray
    Write-Host "  - Add the install directory to system PATH" -ForegroundColor Gray
    Write-Host "  - Install and start the LCDPossible Windows Service" -ForegroundColor Gray
    Write-Host "  - Support upgrades (run new MSI to upgrade)" -ForegroundColor Gray
    Write-Host ""

    return $msiPath
}
catch {
    Write-Host ""
    Write-Host "ERROR: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}
