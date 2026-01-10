#!/bin/bash
# LCDPossible Linux Uninstallation Script
set -e

INSTALL_DIR="/opt/lcdpossible"
BIN_LINK="/usr/local/bin/lcdpossible"
UDEV_RULES="/etc/udev/rules.d/99-lcdpossible.rules"
SYSTEMD_SERVICE="/etc/systemd/system/lcdpossible.service"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}[ERROR]${NC} This script must be run as root (use sudo)"
    exit 1
fi

print_status "Uninstalling LCDPossible..."

# Stop and disable service
if systemctl is-active --quiet lcdpossible 2>/dev/null; then
    print_status "Stopping lcdpossible service..."
    systemctl stop lcdpossible
fi

if systemctl is-enabled --quiet lcdpossible 2>/dev/null; then
    print_status "Disabling lcdpossible service..."
    systemctl disable lcdpossible
fi

# Remove systemd service
if [ -f "$SYSTEMD_SERVICE" ]; then
    print_status "Removing systemd service..."
    rm -f "$SYSTEMD_SERVICE"
    systemctl daemon-reload
fi

# Remove udev rules
if [ -f "$UDEV_RULES" ]; then
    print_status "Removing udev rules..."
    rm -f "$UDEV_RULES"
    udevadm control --reload-rules
fi

# Remove symlink
if [ -L "$BIN_LINK" ]; then
    print_status "Removing command symlink..."
    rm -f "$BIN_LINK"
fi

# Remove installation directory
if [ -d "$INSTALL_DIR" ]; then
    print_status "Removing installation directory..."
    rm -rf "$INSTALL_DIR"
fi

print_status "LCDPossible has been uninstalled."
