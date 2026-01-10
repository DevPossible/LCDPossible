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

#region Version Management
function Get-NextVersion {
    # Try to get version from git tags
    $lastTag = git describe --tags --abbrev=0 2>$null
    if ($lastTag) {
        $current = [Version]($lastTag -replace '^v', '')
        return "$($current.Major).$($current.Minor).$($current.Build + 1)"
    }

    # Try to get from Directory.Build.props
    $propsFile = Join-Path $PSScriptRoot 'Directory.Build.props'
    if (Test-Path $propsFile) {
        $content = Get-Content $propsFile -Raw
        if ($content -match '<Version>([^<]+)</Version>') {
            return $Matches[1]
        }
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
                tar -czvf $tarPath *
                Pop-Location
                Write-Host "  Created: $tarPath" -ForegroundColor Gray
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
