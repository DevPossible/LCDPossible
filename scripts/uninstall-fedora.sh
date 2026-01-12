#!/bin/bash
# LCDPossible Uninstaller for Fedora/RHEL
# Usage: curl -fsSL https://raw.githubusercontent.com/USER/LCDPossible/main/scripts/uninstall-fedora.sh | sudo bash
#
# This script:
# - Stops and disables the systemd service
# - Removes the service file
# - Removes the symlink from /usr/local/bin
# - Removes installed files from /opt/lcdpossible
# - Optionally removes configuration from /etc/lcdpossible
# - Removes udev rules

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
INSTALL_DIR="/opt/lcdpossible"
SERVICE_NAME="lcdpossible"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
SYMLINK_PATH="/usr/local/bin/lcdpossible"
CONFIG_DIR="/etc/lcdpossible"
UDEV_RULES_FILE="/etc/udev/rules.d/99-lcdpossible.rules"

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
            echo "LCDPossible Uninstaller for Fedora/RHEL"
            echo ""
            echo "Usage: sudo $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --remove-config    Also remove configuration files from /etc/lcdpossible"
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

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    error "This script must be run as root (use sudo)"
    exit 1
fi

echo ""
echo -e "${BLUE}╔══════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║${NC}             LCDPossible Uninstaller - Fedora/RHEL            ${BLUE}║${NC}"
echo -e "${BLUE}╚══════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Check if LCDPossible is installed
if [ ! -d "$INSTALL_DIR" ] && [ ! -f "$SERVICE_FILE" ] && [ ! -L "$SYMLINK_PATH" ]; then
    warn "LCDPossible does not appear to be installed"
    exit 0
fi

# Step 1: Stop the service if running
log "Stopping LCDPossible service..."
if systemctl is-active --quiet "$SERVICE_NAME" 2>/dev/null; then
    systemctl stop "$SERVICE_NAME" || warn "Failed to stop service"
    log "Service stopped"
else
    info "Service was not running"
fi

# Step 2: Disable and remove the service
log "Disabling and removing systemd service..."
if [ -f "$SERVICE_FILE" ]; then
    systemctl disable "$SERVICE_NAME" 2>/dev/null || true
    rm -f "$SERVICE_FILE"
    systemctl daemon-reload
    log "Service removed"
else
    info "Service file not found"
fi

# Step 3: Remove symlink
log "Removing symlink..."
if [ -L "$SYMLINK_PATH" ]; then
    rm -f "$SYMLINK_PATH"
    log "Symlink removed from $SYMLINK_PATH"
else
    info "Symlink not found at $SYMLINK_PATH"
fi

# Step 4: Remove udev rules
log "Removing udev rules..."
if [ -f "$UDEV_RULES_FILE" ]; then
    rm -f "$UDEV_RULES_FILE"
    udevadm control --reload-rules 2>/dev/null || true
    udevadm trigger 2>/dev/null || true
    log "Udev rules removed"
else
    info "Udev rules file not found"
fi

# Step 5: Remove installed files
log "Removing installed files..."
if [ -d "$INSTALL_DIR" ]; then
    rm -rf "$INSTALL_DIR"
    log "Installation directory removed: $INSTALL_DIR"
else
    info "Installation directory not found"
fi

# Step 6: Optionally remove configuration
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
    echo "    sudo $0 --remove-config"
    echo ""
fi

info "Dependencies (vlc, dejavu-sans-fonts, jq) were not removed."
info "Remove them manually if no longer needed:"
echo "    sudo dnf remove vlc dejavu-sans-fonts jq"
echo ""
