#Requires -Version 7.0
param(
    [string]$Version = $null,
    [switch]$SkipTests,
    [switch]$SkipChangelog
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
    # Try to get version from git tags, Directory.Build.props, or prompt
    $lastTag = git describe --tags --abbrev=0 2>$null
    if ($lastTag) {
        $current = [Version]($lastTag -replace '^v', '')
        return "$($current.Major).$($current.Minor).$($current.Build + 1)"
    }
    return "1.0.0"
}

function Update-VersionInFiles {
    param([string]$NewVersion)

    # Update Directory.Build.props if it has version
    $propsFile = Join-Path $PSScriptRoot 'Directory.Build.props'
    if (Test-Path $propsFile) {
        $content = Get-Content $propsFile -Raw
        if ($content -match '<Version>') {
            $content = $content -replace '<Version>[^<]+</Version>', "<Version>$NewVersion</Version>"
            Set-Content $propsFile $content -NoNewline
            Write-Host "Updated version in Directory.Build.props" -ForegroundColor Green
        }
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
        # Replace [Unreleased] with new version
        $content = $content -replace '\[Unreleased\]', "[$NewVersion] - $today`n`n## [Unreleased]"
        Set-Content $changelogPath $content -NoNewline
        Write-Host "Updated CHANGELOG.md with version $NewVersion" -ForegroundColor Green
    }
}
#endregion

Push-Location $PSScriptRoot
try {
    Write-Host "=== Packaging ===" -ForegroundColor Cyan

    # Determine version
    if (-not $Version) {
        $Version = Get-NextVersion
        Write-Host "Auto-detected version: $Version" -ForegroundColor Yellow
        $confirm = Read-Host "Use this version? (y/n or enter custom version)"
        if ($confirm -ne 'y' -and $confirm -ne 'Y' -and $confirm -ne '') {
            $Version = $confirm
        }
    }
    Write-Host "Packaging version: $Version" -ForegroundColor Green

    # Clean dist folder
    if (Test-Path $DistDir) {
        Remove-Item $DistDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

    # Update version in files
    Update-VersionInFiles -NewVersion $Version

    # Update changelog
    if (-not $SkipChangelog) {
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
    $runtimes = @('win-x64', 'linux-x64')
    $publishProjects = Get-ChildItem -Path 'src' -Recurse -Filter '*.csproj' |
        Where-Object {
            $content = Get-Content $_.FullName -Raw
            $content -match '<OutputType>Exe</OutputType>' -or
            $content -match 'Microsoft\.NET\.Sdk\.Web'
        }

    foreach ($project in $publishProjects) {
        $projectName = $project.BaseName
        Write-Host "Publishing $projectName..." -ForegroundColor Yellow

        foreach ($runtime in $runtimes) {
            $outputDir = Join-Path $DistDir $projectName $runtime

            dotnet publish $project.FullName `
                --configuration Release `
                --runtime $runtime `
                --self-contained true `
                --output $outputDir `
                -p:Version=$Version `
                -p:PublishSingleFile=true `
                -p:IncludeNativeLibrariesForSelfExtract=true

            if ($LASTEXITCODE -ne 0) { throw "Publish failed for $projectName ($runtime)" }
        }
    }

    # Package NuGet packages
    Write-Host "`n=== NuGet Packages ===" -ForegroundColor Cyan
    $nugetDir = Join-Path $DistDir 'nuget'
    New-Item -ItemType Directory -Path $nugetDir -Force | Out-Null

    $libraryProjects = Get-ChildItem -Path 'src' -Recurse -Filter '*.csproj' |
        Where-Object {
            $content = Get-Content $_.FullName -Raw
            $content -match '<IsPackable>true</IsPackable>' -or
            (-not ($content -match '<OutputType>Exe</OutputType>') -and
             -not ($content -match 'Microsoft\.NET\.Sdk\.Web'))
        }

    foreach ($project in $libraryProjects) {
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
    Get-ChildItem $DistDir -Recurse -Directory | ForEach-Object {
        Write-Host "  $($_.FullName.Replace($DistDir, '.dist'))" -ForegroundColor Gray
    }
}
finally {
    Pop-Location
}
