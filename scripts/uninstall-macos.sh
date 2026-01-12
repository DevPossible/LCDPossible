#!/bin/bash
# LCDPossible Uninstaller for macOS
# Usage: curl -fsSL https://raw.githubusercontent.com/USER/LCDPossible/main/scripts/uninstall-macos.sh | bash
#
# This script:
# - Stops and unloads the launchd agent
# - Removes the launchd plist file
# - Removes the symlink from /usr/local/bin or ~/.local/bin
# - Removes installed files from ~/.local/share/lcdpossible
# - Optionally removes configuration from ~/.config/lcdpossible

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
INSTALL_DIR="$HOME/.local/share/lcdpossible"
AGENT_NAME="com.lcdpossible.agent"
AGENT_PLIST="$HOME/Library/LaunchAgents/${AGENT_NAME}.plist"
SYMLINK_PATHS=("/usr/local/bin/lcdpossible" "$HOME/.local/bin/lcdpossible")
CONFIG_DIR="$HOME/.config/lcdpossible"

# Parse arguments
REMOVE_CONFIG=false
QUIET=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --remove-config)
            REMOVE_CONFIG=true
            shift
            ;;
        --quiet|-q)
            QUIET=true
            shift
            ;;
        --help|-h)
            echo "LCDPossible Uninstaller for macOS"
            echo ""
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --remove-config    Also remove configuration files from ~/.config/lcdpossible"
            echo "  --quiet, -q        Suppress non-essential output"
            echo "  --help, -h         Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

log() {
    if [ "$QUIET" = false ]; then
        echo -e "${GREEN}[+]${NC} $1"
    fi
}

warn() {
    echo -e "${YELLOW}[!]${NC} $1"
}

error() {
    echo -e "${RED}[✗]${NC} $1"
}

info() {
    if [ "$QUIET" = false ]; then
        echo -e "${BLUE}[i]${NC} $1"
    fi
}

echo ""
echo -e "${BLUE}╔══════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║${NC}               LCDPossible Uninstaller - macOS                ${BLUE}║${NC}"
echo -e "${BLUE}╚══════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Check if LCDPossible is installed
FOUND_INSTALL=false
if [ -d "$INSTALL_DIR" ]; then
    FOUND_INSTALL=true
fi
if [ -f "$AGENT_PLIST" ]; then
    FOUND_INSTALL=true
fi
for SYMLINK_PATH in "${SYMLINK_PATHS[@]}"; do
    if [ -L "$SYMLINK_PATH" ]; then
        FOUND_INSTALL=true
        break
    fi
done

if [ "$FOUND_INSTALL" = false ]; then
    warn "LCDPossible does not appear to be installed"
    exit 0
fi

# Step 1: Stop and unload the launchd agent
log "Stopping and unloading launchd agent..."
if [ -f "$AGENT_PLIST" ]; then
    # Check if agent is loaded
    if launchctl list | grep -q "$AGENT_NAME" 2>/dev/null; then
        launchctl unload "$AGENT_PLIST" 2>/dev/null || warn "Failed to unload agent"
        log "Agent stopped and unloaded"
    else
        info "Agent was not loaded"
    fi
else
    info "Agent plist not found"
fi

# Step 2: Remove the launchd plist
log "Removing launchd agent configuration..."
if [ -f "$AGENT_PLIST" ]; then
    rm -f "$AGENT_PLIST"
    log "Agent plist removed: $AGENT_PLIST"
else
    info "Agent plist not found"
fi

# Step 3: Remove symlinks
log "Removing symlinks..."
SYMLINK_REMOVED=false
for SYMLINK_PATH in "${SYMLINK_PATHS[@]}"; do
    if [ -L "$SYMLINK_PATH" ]; then
        # Check if we need sudo for /usr/local/bin
        if [[ "$SYMLINK_PATH" == /usr/local/bin/* ]]; then
            if [ -w "$(dirname "$SYMLINK_PATH")" ]; then
                rm -f "$SYMLINK_PATH"
            else
                sudo rm -f "$SYMLINK_PATH"
            fi
        else
            rm -f "$SYMLINK_PATH"
        fi
        log "Symlink removed from $SYMLINK_PATH"
        SYMLINK_REMOVED=true
    fi
done

if [ "$SYMLINK_REMOVED" = false ]; then
    info "No symlinks found"
fi

# Step 4: Remove installed files
log "Removing installed files..."
if [ -d "$INSTALL_DIR" ]; then
    rm -rf "$INSTALL_DIR"
    log "Installation directory removed: $INSTALL_DIR"
else
    info "Installation directory not found"
fi

# Step 5: Optionally remove configuration
if [ "$REMOVE_CONFIG" = true ]; then
    log "Removing configuration files..."
    if [ -d "$CONFIG_DIR" ]; then
        rm -rf "$CONFIG_DIR"
        log "Configuration directory removed: $CONFIG_DIR"
    else
        info "Configuration directory not found"
    fi
else
    if [ -d "$CONFIG_DIR" ]; then
        info "Configuration preserved at $CONFIG_DIR (use --remove-config to delete)"
    fi
fi

echo ""
echo -e "${GREEN}╔══════════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║${NC}              LCDPossible uninstalled successfully!            ${GREEN}║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════════════════════════╝${NC}"
echo ""

if [ "$REMOVE_CONFIG" = false ] && [ -d "$CONFIG_DIR" ]; then
    info "To also remove configuration files, run:"
    echo "    $0 --remove-config"
    echo ""
fi

info "Dependencies (vlc) were not removed."
info "Remove them manually if no longer needed:"
echo "    brew uninstall vlc"
echo ""
