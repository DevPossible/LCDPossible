#Requires -Version 5.1
<#
.SYNOPSIS
    Calculates the next semantic version based on conventional commits.

.DESCRIPTION
    Analyzes git commits since the last tag and determines the next version
    based on conventional commit messages:
    - BREAKING CHANGE or !: → major bump
    - feat: → minor bump
    - fix:, docs:, style:, refactor:, perf:, test:, build:, ci:, chore: → patch bump

.PARAMETER BaseBranch
    The base branch to compare against (default: main)

.EXAMPLE
    ./get-version.ps1
    Returns: 1.2.3

.EXAMPLE
    ./get-version.ps1 -Verbose
    Shows detailed commit analysis

.OUTPUTS
    Version string (e.g., "1.2.3")
#>

[CmdletBinding()]
param(
    [string]$BaseBranch = "main"
)

$ErrorActionPreference = 'Stop'

function Get-LastTag {
    $tag = git describe --tags --abbrev=0 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $tag) {
        return $null
    }
    return $tag.Trim()
}

function Parse-Version {
    param([string]$Tag)

    if (-not $Tag) {
        return @{ Major = 0; Minor = 0; Patch = 0 }
    }

    # Remove 'v' prefix if present
    $versionString = $Tag -replace '^v', ''

    if ($versionString -match '^(\d+)\.(\d+)\.(\d+)') {
        return @{
            Major = [int]$Matches[1]
            Minor = [int]$Matches[2]
            Patch = [int]$Matches[3]
        }
    }

    return @{ Major = 0; Minor = 0; Patch = 0 }
}

function Get-CommitsSinceTag {
    param([string]$Tag)

    if ($Tag) {
        $commits = git log "$Tag..HEAD" --pretty=format:"%s" 2>$null
    } else {
        $commits = git log --pretty=format:"%s" 2>$null
    }

    if ($LASTEXITCODE -ne 0) {
        return @()
    }

    return $commits -split "`n" | Where-Object { $_ -ne "" }
}

function Analyze-Commits {
    param([string[]]$Commits)

    $analysis = @{
        HasBreaking = $false
        HasFeat = $false
        HasFix = $false
        BreakingCommits = @()
        FeatCommits = @()
        FixCommits = @()
        OtherCommits = @()
    }

    foreach ($commit in $Commits) {
        # Check for breaking changes
        if ($commit -match '^[a-z]+(\([^)]+\))?!:' -or $commit -match 'BREAKING CHANGE:') {
            $analysis.HasBreaking = $true
            $analysis.BreakingCommits += $commit
            Write-Verbose "BREAKING: $commit"
        }
        # Check for features
        elseif ($commit -match '^feat(\([^)]+\))?:') {
            $analysis.HasFeat = $true
            $analysis.FeatCommits += $commit
            Write-Verbose "FEAT: $commit"
        }
        # Check for fixes
        elseif ($commit -match '^fix(\([^)]+\))?:') {
            $analysis.HasFix = $true
            $analysis.FixCommits += $commit
            Write-Verbose "FIX: $commit"
        }
        # Other conventional commits (patch)
        elseif ($commit -match '^(docs|style|refactor|perf|test|build|ci|chore)(\([^)]+\))?:') {
            $analysis.OtherCommits += $commit
            Write-Verbose "OTHER: $commit"
        }
        else {
            Write-Verbose "SKIP: $commit"
        }
    }

    return $analysis
}

function Calculate-NextVersion {
    param(
        [hashtable]$CurrentVersion,
        [hashtable]$Analysis
    )

    $major = $CurrentVersion.Major
    $minor = $CurrentVersion.Minor
    $patch = $CurrentVersion.Patch

    if ($Analysis.HasBreaking) {
        $major++
        $minor = 0
        $patch = 0
        Write-Verbose "Bump: MAJOR (breaking change)"
    }
    elseif ($Analysis.HasFeat) {
        $minor++
        $patch = 0
        Write-Verbose "Bump: MINOR (new feature)"
    }
    elseif ($Analysis.HasFix -or $Analysis.OtherCommits.Count -gt 0) {
        $patch++
        Write-Verbose "Bump: PATCH (fix or maintenance)"
    }
    else {
        # No conventional commits found, still bump patch if there are any commits
        $patch++
        Write-Verbose "Bump: PATCH (default)"
    }

    return "$major.$minor.$patch"
}

# Main logic
Write-Verbose "Analyzing commits for version calculation..."

$lastTag = Get-LastTag
Write-Verbose "Last tag: $($lastTag ?? '(none)')"

$currentVersion = Parse-Version -Tag $lastTag
Write-Verbose "Current version: $($currentVersion.Major).$($currentVersion.Minor).$($currentVersion.Patch)"

$commits = Get-CommitsSinceTag -Tag $lastTag
Write-Verbose "Commits since last tag: $($commits.Count)"

if ($commits.Count -eq 0) {
    # No new commits, return current version
    $version = "$($currentVersion.Major).$($currentVersion.Minor).$($currentVersion.Patch)"
    Write-Verbose "No new commits, keeping version: $version"
    Write-Output $version
    exit 0
}

$analysis = Analyze-Commits -Commits $commits
$nextVersion = Calculate-NextVersion -CurrentVersion $currentVersion -Analysis $analysis

Write-Verbose "Summary:"
Write-Verbose "  Breaking changes: $($analysis.BreakingCommits.Count)"
Write-Verbose "  Features: $($analysis.FeatCommits.Count)"
Write-Verbose "  Fixes: $($analysis.FixCommits.Count)"
Write-Verbose "  Other: $($analysis.OtherCommits.Count)"
Write-Verbose "Next version: $nextVersion"

Write-Output $nextVersion
