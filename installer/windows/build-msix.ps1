#Requires -Version 7.0
param(
    [string]$Version = "0.1.0",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = $null
)

$ErrorActionPreference = 'Stop'

$ProjectRoot = Join-Path $PSScriptRoot '../..'
$PublishDir = Join-Path $ProjectRoot '.build/publish/LCDPossible' $Runtime
$OutputDir = if ($OutputDir) { $OutputDir } else { Join-Path $ProjectRoot '.dist/installers' }

Write-Host "=== Building MSIX Package ===" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Runtime: $Runtime" -ForegroundColor Yellow

Push-Location $ProjectRoot
try {
    # Ensure output directory exists
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

    # Publish the application if not already done
    if (-not (Test-Path $PublishDir)) {
        Write-Host "Publishing application..." -ForegroundColor Yellow
        dotnet publish src/LCDPossible/LCDPossible.csproj `
            --configuration Release `
            --runtime $Runtime `
            --self-contained true `
            -p:PublishSingleFile=true `
            -p:Version=$Version `
            --output $PublishDir

        if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
    }

    # Check if makeappx is available
    $makeappx = Get-Command makeappx.exe -ErrorAction SilentlyContinue
    if (-not $makeappx) {
        # Try to find in Windows SDK
        $sdkPaths = @(
            "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe",
            "${env:ProgramFiles}\Windows Kits\10\bin\*\x64\makeappx.exe"
        )
        foreach ($path in $sdkPaths) {
            $found = Get-ChildItem -Path $path -ErrorAction SilentlyContinue | Sort-Object -Descending | Select-Object -First 1
            if ($found) {
                $makeappx = $found.FullName
                break
            }
        }
    }

    if (-not $makeappx) {
        Write-Host "MSIX packaging requires Windows SDK with makeappx.exe" -ForegroundColor Yellow
        Write-Host "Creating ZIP package instead..." -ForegroundColor Yellow

        $zipPath = Join-Path $OutputDir "lcdpossible-$Version-$Runtime.zip"
        Compress-Archive -Path "$PublishDir\*" -DestinationPath $zipPath -Force

        Write-Host "Created: $zipPath" -ForegroundColor Green
        return $zipPath
    }

    # Create MSIX package layout
    $layoutDir = Join-Path $ProjectRoot '.build/msix-layout'
    if (Test-Path $layoutDir) {
        Remove-Item $layoutDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $layoutDir -Force | Out-Null

    # Copy published files
    Copy-Item -Path "$PublishDir\*" -Destination $layoutDir -Recurse

    # Generate AppxManifest.xml
    $manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities">
  <Identity Name="DevPossible.LCDPossible"
            Version="$Version.0"
            Publisher="CN=DevPossible"
            ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>LCDPossible</DisplayName>
    <PublisherDisplayName>DevPossible</PublisherDisplayName>
    <Description>Cross-platform LCD controller service for HID-based displays</Description>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Resources>
    <Resource Language="en-us" />
  </Resources>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
  <Applications>
    <Application Id="LCDPossible" Executable="LCDPossible.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="LCDPossible"
                          Description="LCD Controller Service"
                          BackgroundColor="transparent"
                          Square150x150Logo="Assets\Square150x150Logo.png"
                          Square44x44Logo="Assets\Square44x44Logo.png">
      </uap:VisualElements>
    </Application>
  </Applications>
</Package>
"@
    Set-Content -Path (Join-Path $layoutDir 'AppxManifest.xml') -Value $manifest

    # Create placeholder assets directory
    $assetsDir = Join-Path $layoutDir 'Assets'
    New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null

    # Create placeholder images (1x1 transparent PNG for now)
    # In a real scenario, you'd have proper logos
    $placeholderPng = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==')
    [System.IO.File]::WriteAllBytes((Join-Path $assetsDir 'StoreLogo.png'), $placeholderPng)
    [System.IO.File]::WriteAllBytes((Join-Path $assetsDir 'Square150x150Logo.png'), $placeholderPng)
    [System.IO.File]::WriteAllBytes((Join-Path $assetsDir 'Square44x44Logo.png'), $placeholderPng)

    # Create MSIX package
    $msixPath = Join-Path $OutputDir "lcdpossible-$Version-$Runtime.msix"
    & $makeappx pack /d $layoutDir /p $msixPath /o

    if ($LASTEXITCODE -ne 0) { throw "MSIX packaging failed" }

    Write-Host "Created: $msixPath" -ForegroundColor Green
    return $msixPath
}
finally {
    Pop-Location
}
