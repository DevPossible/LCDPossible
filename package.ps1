#Requires -Version 7.0
param(
    [string]$Version = $null,
    [switch]$SkipTests,
    [switch]$SkipChangelog,
    [switch]$NonInteractive,
    [string[]]$Runtimes = @('win-x64', 'linux-x64', 'linux-arm64', 'osx-x64')
)

$ErrorActionPreference = 'Stop'

$DistDir = Join-Path $PSScriptRoot '.dist'
$BuildDir = Join-Path $PSScriptRoot '.build'

#region Tool Installation Functions
function Ensure-DotnetTool {
    param([string]$Package, [string]$Command = $Package)
    if (-not (Get-Command $Command -ErrorAction SilentlyContinue)) {
        Write-Host "Installing $Package via dotnet tool..." -ForegroundColor Yellow
        dotnet tool install -g $Package
    }
}
#endregion

#region Platform-Specific Runtime Filtering
function Remove-NonMatchingRuntimes {
    <#
    .SYNOPSIS
        Removes platform-specific runtime directories that don't match the target runtime.
    .DESCRIPTION
        Scans all plugin directories for 'runtimes' folders and removes subdirectories
        that don't match the target platform. This significantly reduces package size.
    .PARAMETER PluginsDir
        Path to the plugins directory to scan.
    .PARAMETER TargetRuntime
        Target runtime identifier (e.g., 'linux-x64', 'win-x64', 'osx-arm64').
    .RETURNS
        Number of directories removed.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$PluginsDir,

        [Parameter(Mandatory)]
        [string]$TargetRuntime
    )

    $removedCount = 0

    # Parse target runtime: "linux-x64" -> os="linux", arch="x64"
    $parts = $TargetRuntime.Split('-')
    $targetOs = $parts[0].ToLowerInvariant()
    $targetArch = if ($parts.Length -gt 1) { $parts[1].ToLowerInvariant() } else { $null }

    # Define which runtime directories to keep for each target OS
    # Be precise: exact match + base/generic versions only
    $keepExact = switch ($targetOs) {
        'linux' {
            @(
                "linux-$targetArch",       # Exact match (e.g., linux-x64)
                'linux',                    # Base linux (no arch)
                'unix'                      # Unix generic
            )
        }
        'win' {
            @(
                "win-$targetArch",          # Exact match (e.g., win-x64)
                'win',                      # Base windows (no arch)
                'windows'                   # Windows generic
            )
        }
        'osx' {
            @(
                "osx-$targetArch",          # Exact match (e.g., osx-arm64)
                'osx',                      # Base macOS (no arch)
                'unix'                      # Unix generic (macOS is unix-like)
            )
        }
        default {
            @($TargetRuntime)               # Just keep exact match
        }
    }

    # Find all 'runtimes' directories in all plugins
    $runtimesDirs = Get-ChildItem -Path $PluginsDir -Directory -Recurse -Filter 'runtimes' -ErrorAction SilentlyContinue

    foreach ($runtimesDir in $runtimesDirs) {
        # Get all platform subdirectories
        $platformDirs = Get-ChildItem -Path $runtimesDir.FullName -Directory -ErrorAction SilentlyContinue

        foreach ($platformDir in $platformDirs) {
            $platformName = $platformDir.Name.ToLowerInvariant()

            # Check if this platform should be kept (exact match only, no wildcards)
            $shouldKeep = $keepExact -contains $platformName

            if (-not $shouldKeep) {
                Remove-Item -Path $platformDir.FullName -Recurse -Force -ErrorAction SilentlyContinue
                $removedCount++
            }
        }

        # If runtimes directory is now empty, remove it too
        $remaining = Get-ChildItem -Path $runtimesDir.FullName -ErrorAction SilentlyContinue
        if ($remaining.Count -eq 0) {
            Remove-Item -Path $runtimesDir.FullName -Force -ErrorAction SilentlyContinue
        }
    }

    return $removedCount
}
#endregion

#region Version Management
function Get-NextVersion {
    # Use conventional commits to calculate version
    $versionScript = Join-Path $PSScriptRoot 'scripts' 'get-version.ps1'
    if (Test-Path $versionScript) {
        $version = & $versionScript
        if ($version -and $version -match '^\d+\.\d+\.\d+$') {
            return $version
        }
    }

    # Fallback: try to get version from git tags
    $lastTag = git describe --tags --abbrev=0 2>$null
    if ($lastTag) {
        $current = [Version]($lastTag -replace '^v', '')
        return "$($current.Major).$($current.Minor).$($current.Build + 1)"
    }

    return "0.1.0"
}

function Update-VersionInFiles {
    param([string]$NewVersion)

    # Update Directory.Build.props
    $propsFile = Join-Path $PSScriptRoot 'Directory.Build.props'
    if (Test-Path $propsFile) {
        $content = Get-Content $propsFile -Raw
        if ($content -match '<Version>') {
            $content = $content -replace '<Version>[^<]+</Version>', "<Version>$NewVersion</Version>"
            Set-Content $propsFile $content -NoNewline
            Write-Host "Updated version in Directory.Build.props" -ForegroundColor Green
        }
    }

    # Update release-please manifest
    $manifestFile = Join-Path $PSScriptRoot '.github' 'release-please-manifest.json'
    if (Test-Path $manifestFile) {
        $manifest = Get-Content $manifestFile -Raw | ConvertFrom-Json
        $manifest.'.' = $NewVersion
        $manifest | ConvertTo-Json | Set-Content $manifestFile
        Write-Host "Updated version in release-please-manifest.json" -ForegroundColor Green
    }
}
#endregion

#region Changelog Management
function Update-Changelog {
    param([string]$NewVersion)

    $changelogPath = Join-Path $PSScriptRoot 'CHANGELOG.md'
    $today = Get-Date -Format 'yyyy-MM-dd'

    if (Test-Path $changelogPath) {
        $content = Get-Content $changelogPath -Raw
        # Replace [Unreleased] section
        $content = $content -replace '\[Unreleased\]', "[$NewVersion] - $today`n`n## [Unreleased]"
        Set-Content $changelogPath $content -NoNewline
        Write-Host "Updated CHANGELOG.md with version $NewVersion" -ForegroundColor Green
    }
}
#endregion

Push-Location $PSScriptRoot
try {
    Write-Host "=== LCDPossible Packaging ===" -ForegroundColor Cyan

    # Determine version
    if (-not $Version) {
        $Version = Get-NextVersion
        Write-Host "Auto-detected version: $Version" -ForegroundColor Yellow

        if (-not $NonInteractive) {
            $confirm = Read-Host "Use this version? (y/n or enter custom version)"
            if ($confirm -ne 'y' -and $confirm -ne 'Y' -and $confirm -ne '') {
                $Version = $confirm
            }
        }
    }
    Write-Host "Packaging version: $Version" -ForegroundColor Green

    # Clean dist folder
    if (Test-Path $DistDir) {
        Remove-Item $DistDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

    # Update version in files (skip in CI - Release Please handles this)
    if (-not $NonInteractive) {
        Update-VersionInFiles -NewVersion $Version
    }

    # Update changelog (skip in CI - Release Please handles this)
    if (-not $SkipChangelog -and -not $NonInteractive) {
        Update-Changelog -NewVersion $Version
    }

    # Run build
    Write-Host "`n=== Building ===" -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'build.ps1')
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }

    # Run tests
    if (-not $SkipTests) {
        Write-Host "`n=== Testing ===" -ForegroundColor Cyan
        & (Join-Path $PSScriptRoot 'test-full.ps1')
        if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
    }

    # Publish for each target runtime
    Write-Host "`n=== Publishing ===" -ForegroundColor Cyan

    foreach ($runtime in $Runtimes) {
        Write-Host "Publishing for $runtime..." -ForegroundColor Yellow
        $outputDir = Join-Path $DistDir 'LCDPossible' $runtime

        dotnet publish src/LCDPossible/LCDPossible.csproj `
            --configuration Release `
            --runtime $runtime `
            --self-contained true `
            --output $outputDir `
            -p:Version=$Version `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true

        if ($LASTEXITCODE -ne 0) { throw "Publish failed for $runtime" }

        # Copy plugins from build output (they're not included in single-file publish)
        $buildPluginsDir = Join-Path $BuildDir 'LCDPossible' 'bin' 'Release' 'net10.0' $runtime 'plugins'
        $publishPluginsDir = Join-Path $outputDir 'plugins'
        if (Test-Path $buildPluginsDir) {
            Copy-Item -Path $buildPluginsDir -Destination $publishPluginsDir -Recurse -Force
            Write-Host "  Copied plugins to publish output" -ForegroundColor Gray

            # Remove platform-specific runtime files that don't match the target
            $removedCount = Remove-NonMatchingRuntimes -PluginsDir $publishPluginsDir -TargetRuntime $runtime
            if ($removedCount -gt 0) {
                Write-Host "  Removed $removedCount non-matching runtime directories" -ForegroundColor Gray
            }
        } else {
            Write-Host "  [WARN] Plugins directory not found in build output: $buildPluginsDir" -ForegroundColor Yellow
        }

        # Copy SDK and Core assemblies to output directory (plugins need them on disk, not just embedded in single-file)
        $buildOutputDir = Join-Path $BuildDir 'LCDPossible' 'bin' 'Release' 'net10.0' $runtime
        foreach ($sharedDll in @('LCDPossible.Sdk.dll', 'LCDPossible.Core.dll')) {
            $dllPath = Join-Path $buildOutputDir $sharedDll
            if (Test-Path $dllPath) {
                Copy-Item -Path $dllPath -Destination $outputDir -Force
                Write-Host "  Copied $sharedDll for plugin compatibility" -ForegroundColor Gray
            } else {
                Write-Host "  [WARN] Assembly not found: $dllPath" -ForegroundColor Yellow
            }
        }

        # Rename executable to lowercase for Linux/macOS (case-sensitive filesystems)
        if ($runtime -notlike 'win-*') {
            $exePath = Join-Path $outputDir 'LCDPossible'
            $lowerExePath = Join-Path $outputDir 'lcdpossible'
            if (Test-Path $exePath) {
                Move-Item $exePath $lowerExePath -Force
                Write-Host "  Renamed executable to lowercase for $runtime" -ForegroundColor Gray
            }
        }

        # Create archive
        $archiveName = "lcdpossible-$Version-$runtime"
        if ($runtime -like 'win-*') {
            $archivePath = Join-Path $DistDir "$archiveName.zip"
            Compress-Archive -Path "$outputDir\*" -DestinationPath $archivePath -Force
            Write-Host "  Created: $archivePath" -ForegroundColor Gray
        } else {
            # For Linux/macOS, we'd need tar (available in Git Bash or WSL)
            $tarPath = Join-Path $DistDir "$archiveName.tar.gz"
            if (Get-Command tar -ErrorAction SilentlyContinue) {
                Push-Location $outputDir
                # Use relative path for tar (avoids Windows C: being interpreted as remote host)
                $relativeTarPath = "../../$archiveName.tar.gz"
                # Exclude obj folder (intermediate files that shouldn't be published)
                tar -czvf $relativeTarPath --exclude='obj' --exclude='obj/*' *
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "  Created: $tarPath" -ForegroundColor Gray
                } else {
                    Write-Host "  [WARN] Failed to create tarball" -ForegroundColor Yellow
                }
                Pop-Location
            }
        }
    }

    # Package NuGet packages (if any library projects)
    Write-Host "`n=== NuGet Packages ===" -ForegroundColor Cyan
    $nugetDir = Join-Path $DistDir 'nuget'
    New-Item -ItemType Directory -Path $nugetDir -Force | Out-Null

    $libraryProjects = Get-ChildItem -Path 'src' -Recurse -Filter '*.csproj' |
        Where-Object {
            $content = Get-Content $_.FullName -Raw
            $content -match '<IsPackable>true</IsPackable>' -or
            (-not ($content -match '<OutputType>Exe</OutputType>') -and
             -not ($content -match 'Microsoft\.NET\.Sdk\.Web') -and
             -not ($content -match 'Microsoft\.NET\.Sdk\.Worker'))
        }

    foreach ($project in $libraryProjects) {
        Write-Host "Packing $($project.BaseName)..." -ForegroundColor Yellow
        dotnet pack $project.FullName `
            --configuration Release `
            --output $nugetDir `
            -p:Version=$Version `
            --no-build
    }

    # Copy documentation
    Write-Host "`n=== Documentation ===" -ForegroundColor Cyan
    $docsDistDir = Join-Path $DistDir 'docs'
    if (Test-Path 'docs') {
        Copy-Item -Path 'docs' -Destination $docsDistDir -Recurse
    }
    Copy-Item -Path 'README.md' -Destination $DistDir -ErrorAction SilentlyContinue
    Copy-Item -Path 'LICENSE' -Destination $DistDir -ErrorAction SilentlyContinue
    Copy-Item -Path 'CHANGELOG.md' -Destination $DistDir -ErrorAction SilentlyContinue

    # Copy installer scripts
    Write-Host "`n=== Installer Scripts ===" -ForegroundColor Cyan
    $installerDistDir = Join-Path $DistDir 'installer'
    if (Test-Path 'installer') {
        Copy-Item -Path 'installer' -Destination $installerDistDir -Recurse
    }

    # Create version file
    @{
        Version = $Version
        BuildDate = (Get-Date -Format 'o')
        GitCommit = (git rev-parse HEAD 2>$null)
        GitBranch = (git rev-parse --abbrev-ref HEAD 2>$null)
    } | ConvertTo-Json | Set-Content (Join-Path $DistDir 'version.json')

    Write-Host "`n=== Packaging Complete ===" -ForegroundColor Green
    Write-Host "Distribution folder: $DistDir" -ForegroundColor Green
    Write-Host "Version: $Version" -ForegroundColor Green

    # List contents
    Write-Host "`nContents:" -ForegroundColor Cyan
    Get-ChildItem $DistDir -Recurse -File | ForEach-Object {
        $relativePath = $_.FullName.Replace($DistDir, '.dist')
        $sizeKB = [math]::Round($_.Length / 1KB, 1)
        Write-Host "  $relativePath ($sizeKB KB)" -ForegroundColor Gray
    }
}
finally {
    Pop-Location
}
