#Requires -Version 7.0
<#
.SYNOPSIS
    Creates and pushes a release from the develop branch.

.DESCRIPTION
    This script automates the release process:
    1. Ensures you're on the develop branch with no uncommitted changes
    2. Prompts for version number (or auto-detects from commits)
    3. Updates version in Directory.Build.props
    4. Updates CHANGELOG.md
    5. Commits and tags the release
    6. Merges to main and pushes
    7. Triggers the release workflow

.PARAMETER Version
    The version number for the release (e.g., "0.2.0"). If not specified, will prompt.

.PARAMETER SkipTests
    Skip running tests before release.

.PARAMETER DryRun
    Show what would be done without making changes.

.EXAMPLE
    ./create-release.ps1
    # Interactive mode - prompts for version

.EXAMPLE
    ./create-release.ps1 -Version "0.3.0"
    # Creates release v0.3.0

.EXAMPLE
    ./create-release.ps1 -Version "0.3.0" -DryRun
    # Shows what would happen without making changes
#>

param(
    [string]$Version,
    [switch]$SkipTests,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

function Write-Info {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Gray
}

function Write-Success {
    param([string]$Message)
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "  [WARN] $Message" -ForegroundColor Yellow
}

function Invoke-GitCommand {
    param([string]$Command, [switch]$AllowFailure)

    if ($DryRun) {
        Write-Host "  [DRY-RUN] git $Command" -ForegroundColor Magenta
        return ""
    }

    $result = Invoke-Expression "git $Command 2>&1"
    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
        throw "Git command failed: git $Command`n$result"
    }
    return $result
}

# ============================================================================
# Pre-flight checks
# ============================================================================

Write-Step "Pre-flight Checks"

# Check we're in a git repository
if (-not (Test-Path ".git")) {
    throw "Not in a git repository root. Run from the project root directory."
}

# Check current branch
$currentBranch = (git branch --show-current).Trim()
if ($currentBranch -ne "develop") {
    throw "Must be on 'develop' branch. Currently on '$currentBranch'.`nRun: git checkout develop"
}
Write-Success "On develop branch"

# Check for uncommitted changes
$status = git status --porcelain
if ($status) {
    Write-Host "`nUncommitted changes detected:" -ForegroundColor Red
    Write-Host $status -ForegroundColor Yellow
    throw "Please commit or stash changes before creating a release."
}
Write-Success "Working tree clean"

# Check gh CLI is available
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) is required. Install from: https://cli.github.com/"
}
Write-Success "GitHub CLI available"

# Check gh is authenticated
$ghAuth = gh auth status 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "GitHub CLI not authenticated. Run: gh auth login"
}
Write-Success "GitHub CLI authenticated"

# ============================================================================
# Determine version
# ============================================================================

Write-Step "Version Determination"

# Get current version from Directory.Build.props
$propsFile = Join-Path $PSScriptRoot "Directory.Build.props"
$propsContent = Get-Content $propsFile -Raw
if ($propsContent -match '<Version>([^<]+)</Version>') {
    $currentVersion = $Matches[1]
    Write-Info "Current version: $currentVersion"
} else {
    $currentVersion = "0.0.0"
    Write-Warning "Could not detect current version"
}

# Get last tag
$lastTag = git describe --tags --abbrev=0 2>$null
if ($lastTag) {
    Write-Info "Last tag: $lastTag"
}

# Count commits since last tag
$commitCount = (git rev-list "$lastTag..HEAD" --count 2>$null) ?? "unknown"
Write-Info "Commits since last release: $commitCount"

# Prompt for version if not provided
if (-not $Version) {
    # Suggest next version based on current
    $versionParts = $currentVersion.Split('.')
    $suggestedVersion = "$($versionParts[0]).$([int]$versionParts[1] + 1).0"

    Write-Host "`nEnter version number (suggested: $suggestedVersion): " -NoNewline -ForegroundColor Yellow
    $Version = Read-Host
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = $suggestedVersion
    }
}

# Validate version format
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Invalid version format '$Version'. Expected format: X.Y.Z (e.g., 0.2.0)"
}

Write-Success "Release version: $Version"

# Check tag doesn't already exist
$existingTag = git tag -l "v$Version"
if ($existingTag) {
    throw "Tag v$Version already exists. Choose a different version."
}

# ============================================================================
# Run tests (unless skipped)
# ============================================================================

if (-not $SkipTests) {
    Write-Step "Running Tests"

    if ($DryRun) {
        Write-Host "  [DRY-RUN] ./test-smoke.ps1" -ForegroundColor Magenta
    } else {
        & "$PSScriptRoot/test-smoke.ps1"
        if ($LASTEXITCODE -ne 0) {
            throw "Tests failed. Fix tests before releasing."
        }
        Write-Success "All tests passed"
    }
} else {
    Write-Warning "Skipping tests (--SkipTests)"
}

# ============================================================================
# Update version files
# ============================================================================

Write-Step "Updating Version Files"

# Update Directory.Build.props
Write-Info "Updating Directory.Build.props"
if (-not $DryRun) {
    $propsContent = $propsContent -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
    Set-Content $propsFile $propsContent -NoNewline
}
Write-Success "Version updated to $Version"

# Update CHANGELOG.md
$changelogFile = Join-Path $PSScriptRoot "CHANGELOG.md"
if (Test-Path $changelogFile) {
    Write-Info "Updating CHANGELOG.md"

    if (-not $DryRun) {
        $changelog = Get-Content $changelogFile -Raw
        $today = Get-Date -Format "yyyy-MM-dd"

        # Replace [Unreleased] section header with version
        $changelog = $changelog -replace '## \[Unreleased\]', "## [Unreleased]`n`n## [$Version] - $today"

        Set-Content $changelogFile $changelog -NoNewline
    }
    Write-Success "CHANGELOG.md updated"
}

# ============================================================================
# Commit and tag
# ============================================================================

Write-Step "Creating Release Commit"

$commitMessage = @"
chore(release): bump version to $Version

Release v$Version
"@

Invoke-GitCommand "add Directory.Build.props CHANGELOG.md"
Invoke-GitCommand "commit -m `"$commitMessage`""
Write-Success "Release commit created"

Write-Step "Creating Tag"

$tagMessage = "Release v$Version"
Invoke-GitCommand "tag -a v$Version -m `"$tagMessage`""
Write-Success "Tag v$Version created"

# ============================================================================
# Merge to main and push
# ============================================================================

Write-Step "Merging to Main"

# Fetch latest
Invoke-GitCommand "fetch origin"

# Checkout main
Invoke-GitCommand "checkout main"
Write-Info "Switched to main branch"

# Merge develop
Invoke-GitCommand "merge develop --no-edit"
Write-Success "Merged develop into main"

Write-Step "Pushing to Remote"

# Push main
Invoke-GitCommand "push origin main"
Write-Success "Pushed main branch"

# Push tag
Invoke-GitCommand "push origin v$Version"
Write-Success "Pushed tag v$Version"

# Switch back to develop
Invoke-GitCommand "checkout develop"
Write-Info "Switched back to develop branch"

# ============================================================================
# Verify workflow triggered
# ============================================================================

Write-Step "Verifying Release Workflow"

if (-not $DryRun) {
    Start-Sleep -Seconds 5  # Give GitHub a moment to start the workflow

    $runs = gh run list --workflow=release.yml --limit=1 --json status,conclusion,createdAt | ConvertFrom-Json
    if ($runs -and $runs[0].status -eq "in_progress") {
        Write-Success "Release workflow is running"
        Write-Host "`n  View progress: " -NoNewline
        Write-Host "https://github.com/DevPossible/LCDPossible/actions" -ForegroundColor Blue
    } else {
        Write-Warning "Could not verify workflow status. Check GitHub Actions manually."
    }
}

# ============================================================================
# Summary
# ============================================================================

Write-Host "`n" + ("=" * 60) -ForegroundColor Green
Write-Host "  RELEASE v$Version CREATED SUCCESSFULLY!" -ForegroundColor Green
Write-Host ("=" * 60) -ForegroundColor Green

Write-Host "`nNext steps:"
Write-Host "  1. Monitor the release workflow:" -ForegroundColor White
Write-Host "     https://github.com/DevPossible/LCDPossible/actions" -ForegroundColor Blue
Write-Host "  2. Once complete, verify release artifacts:" -ForegroundColor White
Write-Host "     https://github.com/DevPossible/LCDPossible/releases/tag/v$Version" -ForegroundColor Blue
Write-Host ""
