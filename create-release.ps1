#Requires -Version 7.0
<#
.SYNOPSIS
    Creates and pushes a release from the develop branch.

.DESCRIPTION
    This script automates the release process with resilience and restartability:
    1. Detects current state and skips already-completed steps
    2. Ensures you're on the develop branch with no uncommitted changes
    3. Prompts for version number (or auto-detects from commits)
    4. Updates version in Directory.Build.props
    5. Updates CHANGELOG.md
    6. Commits and tags the release
    7. Merges to main and pushes
    8. Triggers the release workflow

    If interrupted, simply run again with the same version to resume.

.PARAMETER Version
    The version number for the release (e.g., "0.2.0"). If not specified, will prompt.

.PARAMETER SkipTests
    Skip running tests before release.

.PARAMETER DryRun
    Show what would be done without making changes.

.PARAMETER Force
    Force re-run of steps even if they appear complete.

.PARAMETER Cleanup
    Clean up a failed release attempt (removes local tag, resets changes).

.EXAMPLE
    ./create-release.ps1
    # Interactive mode - prompts for version

.EXAMPLE
    ./create-release.ps1 -Version "0.3.0"
    # Creates release v0.3.0

.EXAMPLE
    ./create-release.ps1 -Version "0.3.0" -DryRun
    # Shows what would happen without making changes

.EXAMPLE
    ./create-release.ps1 -Version "0.3.0" -Cleanup
    # Cleans up a failed v0.3.0 release attempt
#>

param(
    [string]$Version,
    [switch]$SkipTests,
    [switch]$DryRun,
    [switch]$Force,
    [switch]$Cleanup
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

function Write-Error {
    param([string]$Message)
    Write-Host "  [ERROR] $Message" -ForegroundColor Red
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

function Test-TagExists {
    param([string]$Tag)
    $existing = git tag -l $Tag 2>$null
    return [bool]$existing
}

function Test-TagPushed {
    param([string]$Tag)
    $remote = git ls-remote --tags origin $Tag 2>$null
    return [bool]$remote
}

function Test-BranchContainsCommit {
    param([string]$Branch, [string]$Commit)
    $result = git branch --contains $Commit --list $Branch 2>$null
    return [bool]$result
}

function Get-CurrentBranch {
    return (git branch --show-current 2>$null)?.Trim()
}

function Get-VersionFromProps {
    $propsFile = Join-Path $PSScriptRoot "Directory.Build.props"
    if (Test-Path $propsFile) {
        $content = Get-Content $propsFile -Raw
        if ($content -match '<Version>([^<]+)</Version>') {
            return $Matches[1]
        }
    }
    return $null
}

function Test-WorkingTreeClean {
    $status = git status --porcelain 2>$null
    return -not $status
}

function Get-ReleaseState {
    param([string]$Ver)

    $state = @{
        Version = $Ver
        TagName = "v$Ver"
        PropsVersion = Get-VersionFromProps
        TagExists = Test-TagExists "v$Ver"
        TagPushed = Test-TagPushed "v$Ver"
        MainBranch = $null
        MainContainsTag = $false
        MainPushed = $false
        CurrentBranch = Get-CurrentBranch
        WorkingTreeClean = Test-WorkingTreeClean
    }

    # Check if main contains the tag commit
    if ($state.TagExists) {
        $tagCommit = git rev-list -n 1 "v$Ver" 2>$null
        if ($tagCommit) {
            $state.MainContainsTag = Test-BranchContainsCommit "main" $tagCommit
        }
    }

    # Check if main is pushed
    $localMain = git rev-parse main 2>$null
    $remoteMain = git rev-parse origin/main 2>$null
    $state.MainPushed = ($localMain -eq $remoteMain)

    return $state
}

# ============================================================================
# Cleanup Mode
# ============================================================================

if ($Cleanup) {
    Write-Step "Cleanup Mode"

    if (-not $Version) {
        Write-Host "Enter version to clean up: " -NoNewline -ForegroundColor Yellow
        $Version = Read-Host
    }

    if ($Version -notmatch '^\d+\.\d+\.\d+$') {
        throw "Invalid version format '$Version'. Expected format: X.Y.Z"
    }

    $tagName = "v$Version"

    Write-Info "Cleaning up release $tagName..."

    # Delete local tag if exists
    if (Test-TagExists $tagName) {
        Write-Info "Deleting local tag $tagName"
        if (-not $DryRun) {
            git tag -d $tagName 2>$null
        }
        Write-Success "Local tag deleted"
    } else {
        Write-Skipped "Local tag doesn't exist"
    }

    # Warn about remote tag (don't auto-delete - dangerous)
    if (Test-TagPushed $tagName) {
        Write-Warning "Remote tag $tagName exists. To delete it manually:"
        Write-Host "    git push origin --delete $tagName" -ForegroundColor Yellow
    }

    # Reset any uncommitted changes
    $status = git status --porcelain 2>$null
    if ($status) {
        Write-Info "Resetting uncommitted changes..."
        if (-not $DryRun) {
            git checkout -- . 2>$null
        }
        Write-Success "Changes reset"
    }

    # Return to develop if not there
    $currentBranch = Get-CurrentBranch
    if ($currentBranch -ne "develop") {
        Write-Info "Switching to develop branch..."
        if (-not $DryRun) {
            git checkout develop 2>$null
        }
        Write-Success "Switched to develop"
    }

    Write-Host "`nCleanup complete. You can now retry the release." -ForegroundColor Green
    exit 0
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

# ============================================================================
# Determine Version
# ============================================================================

Write-Step "Version Determination"

# Get current version from Directory.Build.props
$currentVersion = Get-VersionFromProps
if ($currentVersion) {
    Write-Info "Current version in props: $currentVersion"
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
if ($lastTag) {
    $commitCount = git rev-list "$lastTag..HEAD" --count 2>$null
    Write-Info "Commits since last release: $commitCount"
}

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

Write-Success "Target version: $Version"

# ============================================================================
# Analyze Current State
# ============================================================================

Write-Step "Analyzing Release State"

$state = Get-ReleaseState $Version

Write-Info "Props version: $($state.PropsVersion)"
Write-Info "Tag v$Version exists locally: $($state.TagExists)"
Write-Info "Tag v$Version pushed to remote: $($state.TagPushed)"
Write-Info "Main contains release: $($state.MainContainsTag)"
Write-Info "Main pushed to remote: $($state.MainPushed)"
Write-Info "Current branch: $($state.CurrentBranch)"
Write-Info "Working tree clean: $($state.WorkingTreeClean)"

# Determine what needs to be done
$needsVersionUpdate = ($state.PropsVersion -ne $Version)
$needsCommit = $needsVersionUpdate -or -not $state.TagExists
$needsTag = -not $state.TagExists
$needsMerge = -not $state.MainContainsTag
$needsPushMain = $state.MainContainsTag -and -not $state.MainPushed
$needsPushTag = $state.TagExists -and -not $state.TagPushed

# Check if release is already complete
if ($state.TagPushed -and $state.MainPushed -and $state.MainContainsTag) {
    Write-Host "`n" -NoNewline
    Write-Success "Release v$Version is already complete!"
    Write-Host "`n  View release: https://github.com/DevPossible/LCDPossible/releases/tag/v$Version" -ForegroundColor Blue
    exit 0
}

# Show what will be done
Write-Step "Release Plan"
if ($needsVersionUpdate) { Write-Info "Will update version files to $Version" }
else { Write-Skipped "Version files already at $Version" }

if ($needsCommit) { Write-Info "Will create release commit" }
else { Write-Skipped "Release commit exists" }

if ($needsTag) { Write-Info "Will create tag v$Version" }
else { Write-Skipped "Tag v$Version already exists" }

if ($needsMerge) { Write-Info "Will merge to main" }
else { Write-Skipped "Main already contains release" }

if ($needsPushMain -or $needsMerge) { Write-Info "Will push main branch" }
if ($needsPushTag -or $needsTag) { Write-Info "Will push tag v$Version" }

# ============================================================================
# Ensure Clean State for Modifications
# ============================================================================

if ($needsVersionUpdate -or $needsCommit) {
    # Need to be on develop with clean working tree
    if ($state.CurrentBranch -ne "develop") {
        Write-Step "Switching to Develop Branch"
        Invoke-Git "checkout develop"
        Write-Success "Switched to develop"
    }

    if (-not $state.WorkingTreeClean) {
        Write-Host "`nUncommitted changes detected:" -ForegroundColor Red
        git status --porcelain | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
        throw "Please commit or stash changes before creating a release.`nOr run: ./create-release.ps1 -Cleanup -Version $Version"
    }
}

# ============================================================================
# Run Tests (unless skipped or already tagged)
# ============================================================================

if (-not $SkipTests -and $needsCommit) {
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
} elseif ($SkipTests) {
    Write-Skipped "Tests (--SkipTests)"
} else {
    Write-Skipped "Tests (release already committed)"
}

# ============================================================================
# Update Version Files
# ============================================================================

if ($needsVersionUpdate -and -not $Force) {
    Write-Step "Updating Version Files"

    # Update Directory.Build.props
    $propsFile = Join-Path $PSScriptRoot "Directory.Build.props"
    Write-Info "Updating Directory.Build.props"
    if (-not $DryRun) {
        $propsContent = Get-Content $propsFile -Raw
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

            # Only update if [Unreleased] section exists and version not already present
            if ($changelog -match '## \[Unreleased\]' -and $changelog -notmatch "## \[$Version\]") {
                $changelog = $changelog -replace '## \[Unreleased\]', "## [Unreleased]`n`n## [$Version] - $today"
                Set-Content $changelogFile $changelog -NoNewline
                Write-Success "CHANGELOG.md updated"
            } else {
                Write-Skipped "CHANGELOG.md already contains $Version or no [Unreleased] section"
            }
        }
    }
} else {
    Write-Skipped "Version files (already at $Version)"
}

# ============================================================================
# Commit and Tag
# ============================================================================

if ($needsCommit) {
    Write-Step "Creating Release Commit"

    # Check if there are changes to commit
    $hasChanges = -not (Test-WorkingTreeClean)

    if ($hasChanges) {
        $commitMessage = "chore(release): bump version to $Version"

        Invoke-Git "add Directory.Build.props CHANGELOG.md" -Quiet
        Invoke-Git "commit -m `"$commitMessage`"" -Quiet
        Write-Success "Release commit created"
    } else {
        Write-Skipped "No changes to commit"
    }
} else {
    Write-Skipped "Release commit (already exists)"
}

if ($needsTag) {
    Write-Step "Creating Tag"

    $tagMessage = "Release v$Version"
    Invoke-Git "tag -a v$Version -m `"$tagMessage`"" -Quiet
    Write-Success "Tag v$Version created"
} else {
    Write-Skipped "Tag v$Version (already exists)"
}

# ============================================================================
# Merge to Main
# ============================================================================

if ($needsMerge) {
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

    # Merge develop (or the tag)
    Invoke-Git "merge v$Version --no-edit" -Quiet
    Write-Success "Merged v$Version into main"
} else {
    Write-Skipped "Merge to main (already contains release)"
}

# ============================================================================
# Push to Remote
# ============================================================================

Write-Step "Pushing to Remote"

# Ensure we're on main for pushing
$currentBranch = Get-CurrentBranch
if ($currentBranch -ne "main" -and ($needsMerge -or $needsPushMain)) {
    Invoke-Git "checkout main" -Quiet
}

# Push main if needed
if ($needsMerge -or $needsPushMain) {
    Invoke-Git "push origin main" -Quiet
    Write-Success "Pushed main branch"
} else {
    Write-Skipped "Push main (already up to date)"
}

# Push tag if needed
if ($needsTag -or $needsPushTag) {
    Invoke-Git "push origin v$Version" -Quiet
    Write-Success "Pushed tag v$Version"
} else {
    Write-Skipped "Push tag (already pushed)"
}

# Switch back to develop
Invoke-Git "checkout develop" -Quiet
Write-Info "Switched back to develop branch"

# ============================================================================
# Verify Workflow
# ============================================================================

Write-Step "Verifying Release Workflow"

if (-not $DryRun) {
    Write-Info "Waiting for workflow to start..."
    Start-Sleep -Seconds 5

    $runs = gh run list --workflow=release.yml --limit=1 --json status,conclusion,createdAt,headBranch 2>$null | ConvertFrom-Json

    if ($runs -and $runs.Count -gt 0) {
        $latestRun = $runs[0]
        if ($latestRun.status -eq "in_progress" -or $latestRun.status -eq "queued") {
            Write-Success "Release workflow is running"
        } elseif ($latestRun.status -eq "completed" -and $latestRun.conclusion -eq "success") {
            Write-Success "Release workflow completed successfully"
        } else {
            Write-Warning "Workflow status: $($latestRun.status) / $($latestRun.conclusion)"
        }
    } else {
        Write-Warning "Could not detect workflow status. Check GitHub Actions manually."
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
