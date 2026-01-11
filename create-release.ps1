#Requires -Version 7.0
<#
.SYNOPSIS
    Merges develop to main to trigger a release.

.DESCRIPTION
    This script automates the release process:
    1. Ensures you're on the develop branch with no uncommitted changes
    2. Runs tests (optional)
    3. Merges develop to main
    4. Pushes main to trigger the release workflow

    The CI workflow handles version calculation and tagging automatically.

.PARAMETER SkipTests
    Skip running tests before release.

.PARAMETER DryRun
    Show what would be done without making changes.

.EXAMPLE
    ./create-release.ps1
    # Merges develop to main and triggers release

.EXAMPLE
    ./create-release.ps1 -SkipTests
    # Skip tests and proceed with release

.EXAMPLE
    ./create-release.ps1 -DryRun
    # Shows what would happen without making changes
#>

param(
    [switch]$SkipTests,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# ============================================================================
# Helper Functions
# ============================================================================

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

function Write-Skipped {
    param([string]$Message)
    Write-Host "  [SKIP] $Message" -ForegroundColor DarkGray
}

function Write-Warning {
    param([string]$Message)
    Write-Host "  [WARN] $Message" -ForegroundColor Yellow
}

function Invoke-Git {
    param(
        [Parameter(Mandatory)]
        [string]$Command,
        [switch]$AllowFailure,
        [switch]$Quiet
    )

    if ($DryRun) {
        Write-Host "  [DRY-RUN] git $Command" -ForegroundColor Magenta
        return ""
    }

    $result = Invoke-Expression "git $Command 2>&1"
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "Git command failed (exit $exitCode): git $Command`n$result"
    }

    if (-not $Quiet -and $result) {
        $result | ForEach-Object { Write-Info $_ }
    }

    return $result
}

function Get-CurrentBranch {
    return (git branch --show-current 2>$null)?.Trim()
}

function Test-WorkingTreeClean {
    $status = git status --porcelain 2>$null
    return -not $status
}

# ============================================================================
# Pre-flight Checks
# ============================================================================

Write-Step "Pre-flight Checks"

# Check we're in a git repository
if (-not (Test-Path ".git")) {
    throw "Not in a git repository root. Run from the project root directory."
}
Write-Success "In git repository"

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

# Check we're on develop branch
$currentBranch = Get-CurrentBranch
if ($currentBranch -ne "develop") {
    throw "Must be on develop branch to create a release. Currently on: $currentBranch"
}
Write-Success "On develop branch"

# Check working tree is clean
if (-not (Test-WorkingTreeClean)) {
    Write-Host "`nUncommitted changes detected:" -ForegroundColor Red
    git status --porcelain | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    throw "Please commit or stash changes before creating a release."
}
Write-Success "Working tree clean"

# ============================================================================
# Run Tests
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
    Write-Skipped "Tests (--SkipTests)"
}

# ============================================================================
# Show What Will Be Released
# ============================================================================

Write-Step "Release Preview"

# Get last tag and commit info
$lastTag = git describe --tags --abbrev=0 2>$null
if ($lastTag) {
    Write-Info "Last release: $lastTag"
    $commitCount = git rev-list "$lastTag..HEAD" --count 2>$null
    Write-Info "Commits since last release: $commitCount"

    Write-Host "`n  Recent commits:" -ForegroundColor White
    git log "$lastTag..HEAD" --pretty=format:"    - %s" --reverse 2>$null | Select-Object -First 10 | ForEach-Object { Write-Host $_ -ForegroundColor Gray }
} else {
    Write-Info "No previous releases"
}

# Confirm
if (-not $DryRun) {
    Write-Host "`nProceed with release? (Y/n): " -NoNewline -ForegroundColor Yellow
    $response = Read-Host
    if ($response -and $response -notmatch '^[Yy]$') {
        Write-Host "Release cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# ============================================================================
# Merge to Main
# ============================================================================

Write-Step "Merging to Main"

# Fetch latest
Invoke-Git "fetch origin" -Quiet
Write-Info "Fetched latest from origin"

# Checkout main
Invoke-Git "checkout main" -Quiet
Write-Info "Switched to main branch"

# Pull latest main to avoid conflicts
$pullResult = Invoke-Git "pull origin main --ff-only" -AllowFailure -Quiet
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Could not fast-forward main. Attempting merge anyway..."
}

# Merge develop
Invoke-Git "merge develop --no-edit" -Quiet
Write-Success "Merged develop into main"

# ============================================================================
# Push to Remote
# ============================================================================

Write-Step "Pushing to Remote"

Invoke-Git "push origin main" -Quiet
Write-Success "Pushed main branch"

# Switch back to develop
Invoke-Git "checkout develop" -Quiet
Write-Info "Switched back to develop branch"

# ============================================================================
# Summary
# ============================================================================

Write-Host "`n" + ("=" * 60) -ForegroundColor Green
Write-Host "  RELEASE TRIGGERED!" -ForegroundColor Green
Write-Host ("=" * 60) -ForegroundColor Green

Write-Host "`nThe CI workflow will now:"
Write-Host "  1. Calculate the version from conventional commits" -ForegroundColor White
Write-Host "  2. Build artifacts for all platforms" -ForegroundColor White
Write-Host "  3. Create a GitHub release with the tag" -ForegroundColor White

Write-Host "`nMonitor the release workflow:" -ForegroundColor White
Write-Host "  https://github.com/DevPossible/LCDPossible/actions" -ForegroundColor Blue
Write-Host ""
