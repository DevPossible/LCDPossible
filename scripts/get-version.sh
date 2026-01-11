#!/bin/bash
# Calculates the next semantic version based on conventional commits.
#
# Usage: ./get-version.sh [-v|--verbose]
#
# Analyzes git commits since the last tag and determines the next version:
# - BREAKING CHANGE or !: → major bump
# - feat: → minor bump
# - fix:, docs:, etc. → patch bump

set -e

VERBOSE=false
if [[ "$1" == "-v" || "$1" == "--verbose" ]]; then
    VERBOSE=true
fi

log() {
    if $VERBOSE; then
        echo "$1" >&2
    fi
}

# Get the last tag
get_last_tag() {
    git describe --tags --abbrev=0 2>/dev/null || echo ""
}

# Parse version from tag (e.g., "v1.2.3" -> "1 2 3")
parse_version() {
    local tag="$1"
    if [[ -z "$tag" ]]; then
        echo "0 0 0"
        return
    fi

    # Remove 'v' prefix and parse
    local version="${tag#v}"
    if [[ "$version" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+) ]]; then
        echo "${BASH_REMATCH[1]} ${BASH_REMATCH[2]} ${BASH_REMATCH[3]}"
    else
        echo "0 0 0"
    fi
}

# Get commits since tag
get_commits_since_tag() {
    local tag="$1"
    if [[ -n "$tag" ]]; then
        git log "$tag..HEAD" --pretty=format:"%s" 2>/dev/null
    else
        git log --pretty=format:"%s" 2>/dev/null
    fi
}

# Main logic
log "Analyzing commits for version calculation..."

LAST_TAG=$(get_last_tag)
log "Last tag: ${LAST_TAG:-(none)}"

read -r MAJOR MINOR PATCH <<< "$(parse_version "$LAST_TAG")"
log "Current version: $MAJOR.$MINOR.$PATCH"

# Get commits
COMMITS=$(get_commits_since_tag "$LAST_TAG")
COMMIT_COUNT=$(echo "$COMMITS" | grep -c . || echo 0)
log "Commits since last tag: $COMMIT_COUNT"

if [[ $COMMIT_COUNT -eq 0 || -z "$COMMITS" ]]; then
    log "No new commits, keeping version: $MAJOR.$MINOR.$PATCH"
    echo "$MAJOR.$MINOR.$PATCH"
    exit 0
fi

# Analyze commits
HAS_BREAKING=false
HAS_FEAT=false
HAS_FIX=false
BREAKING_COUNT=0
FEAT_COUNT=0
FIX_COUNT=0
OTHER_COUNT=0

while IFS= read -r commit; do
    [[ -z "$commit" ]] && continue

    # Check for breaking changes (type!: or BREAKING CHANGE:)
    if [[ "$commit" =~ ^[a-z]+(\([^)]+\))?\!: ]] || [[ "$commit" =~ BREAKING\ CHANGE: ]]; then
        HAS_BREAKING=true
        ((BREAKING_COUNT++))
        log "BREAKING: $commit"
    # Check for features
    elif [[ "$commit" =~ ^feat(\([^)]+\))?: ]]; then
        HAS_FEAT=true
        ((FEAT_COUNT++))
        log "FEAT: $commit"
    # Check for fixes
    elif [[ "$commit" =~ ^fix(\([^)]+\))?: ]]; then
        HAS_FIX=true
        ((FIX_COUNT++))
        log "FIX: $commit"
    # Other conventional commits
    elif [[ "$commit" =~ ^(docs|style|refactor|perf|test|build|ci|chore)(\([^)]+\))?: ]]; then
        ((OTHER_COUNT++))
        log "OTHER: $commit"
    else
        log "SKIP: $commit"
    fi
done <<< "$COMMITS"

# Calculate next version
if $HAS_BREAKING; then
    ((MAJOR++))
    MINOR=0
    PATCH=0
    log "Bump: MAJOR (breaking change)"
elif $HAS_FEAT; then
    ((MINOR++))
    PATCH=0
    log "Bump: MINOR (new feature)"
elif $HAS_FIX || [[ $OTHER_COUNT -gt 0 ]]; then
    ((PATCH++))
    log "Bump: PATCH (fix or maintenance)"
else
    ((PATCH++))
    log "Bump: PATCH (default)"
fi

log "Summary:"
log "  Breaking changes: $BREAKING_COUNT"
log "  Features: $FEAT_COUNT"
log "  Fixes: $FIX_COUNT"
log "  Other: $OTHER_COUNT"
log "Next version: $MAJOR.$MINOR.$PATCH"

echo "$MAJOR.$MINOR.$PATCH"
