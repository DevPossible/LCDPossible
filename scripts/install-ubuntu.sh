#!/bin/bash
# LCDPossible - Ubuntu/Debian Full Installer
# Usage: curl -sSL https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/install-ubuntu.sh | bash
#
# This script is idempotent - safe to run multiple times.
# It will install dependencies, download the latest release, and set up the service.

set -e

REPO="DevPossible/LCDPossible"
INSTALL_DIR="/opt/lcdpossible"
SERVICE_NAME="lcdpossible"
CONFIG_DIR="/etc/lcdpossible"

echo "=============================================="
echo "  LCDPossible Installer (Ubuntu/Debian)"
echo "=============================================="
echo ""

# Check for root/sudo
if [ "$EUID" -ne 0 ]; then
    SUDO="sudo"
else
    SUDO=""
fi

# Helper function to check if a package is installed
is_installed() {
    dpkg -l "$1" 2>/dev/null | grep -q "^ii"
}

# Detect architecture
detect_arch() {
    local arch=$(uname -m)
    case $arch in
        x86_64)  echo "linux-x64" ;;
        aarch64) echo "linux-arm64" ;;
        armv7l)  echo "linux-arm" ;;
        *)       echo ""; return 1 ;;
    esac
}

ARCH=$(detect_arch)
if [ -z "$ARCH" ]; then
    echo "ERROR: Unsupported architecture: $(uname -m)"
    exit 1
fi

echo "[1/6] Installing dependencies..."
echo ""

echo "  Checking LibVLC..."
if is_installed "libvlc-dev"; then
    echo "    LibVLC already installed."
else
    echo "    Installing LibVLC..."
    $SUDO apt-get update -qq
    $SUDO apt-get install -y -qq vlc libvlc-dev
fi

echo "  Checking fonts..."
if is_installed "fonts-dejavu-core"; then
    echo "    Fonts already installed."
else
    echo "    Installing fonts..."
    $SUDO apt-get install -y -qq fonts-dejavu-core
fi

echo "  Checking jq (for JSON parsing)..."
if ! command -v jq &>/dev/null; then
    echo "    Installing jq..."
    $SUDO apt-get install -y -qq jq
fi

echo ""
echo "[2/6] Fetching latest release..."
RELEASE_INFO=$(curl -sSL "https://api.github.com/repos/$REPO/releases/latest")
VERSION=$(echo "$RELEASE_INFO" | jq -r '.tag_name')
DOWNLOAD_URL=$(echo "$RELEASE_INFO" | jq -r ".assets[] | select(.name | contains(\"$ARCH\")) | .browser_download_url" | head -1)

if [ -z "$VERSION" ] || [ "$VERSION" = "null" ]; then
    echo "ERROR: Could not determine latest version."
    echo "Please check https://github.com/$REPO/releases"
    exit 1
fi

if [ -z "$DOWNLOAD_URL" ] || [ "$DOWNLOAD_URL" = "null" ]; then
    echo "ERROR: No release found for architecture: $ARCH"
    echo "Available releases:"
    echo "$RELEASE_INFO" | jq -r '.assets[].name'
    exit 1
fi

echo "  Latest version: $VERSION"
echo "  Architecture: $ARCH"
echo "  Download URL: $DOWNLOAD_URL"

# Check if already installed with same version
if [ -f "$INSTALL_DIR/version.json" ]; then
    INSTALLED_VERSION=$(jq -r '.Version' "$INSTALL_DIR/version.json" 2>/dev/null || echo "")
    if [ "$INSTALLED_VERSION" = "${VERSION#v}" ]; then
        echo ""
        echo "  Version $VERSION is already installed."
        read -p "  Reinstall? [y/N] " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            echo "  Skipping download."
            SKIP_DOWNLOAD=true
        fi
    fi
fi

echo ""
echo "[3/6] Downloading and extracting..."
if [ "$SKIP_DOWNLOAD" != "true" ]; then
    TEMP_DIR=$(mktemp -d)
    trap "rm -rf $TEMP_DIR" EXIT

    echo "  Downloading..."
    curl -sSL "$DOWNLOAD_URL" -o "$TEMP_DIR/lcdpossible.tar.gz"

    echo "  Stopping service if running..."
    $SUDO systemctl stop $SERVICE_NAME 2>/dev/null || true

    echo "  Extracting to $INSTALL_DIR..."
    $SUDO mkdir -p "$INSTALL_DIR"
    $SUDO tar -xzf "$TEMP_DIR/lcdpossible.tar.gz" -C "$INSTALL_DIR" --strip-components=1

    echo "  Setting permissions..."
    $SUDO chmod +x "$INSTALL_DIR/LCDPossible"
fi

echo ""
echo "[4/6] Setting up configuration..."
if [ ! -d "$CONFIG_DIR" ]; then
    $SUDO mkdir -p "$CONFIG_DIR"
    if [ -f "$INSTALL_DIR/appsettings.json" ]; then
        $SUDO cp "$INSTALL_DIR/appsettings.json" "$CONFIG_DIR/appsettings.json"
        echo "  Created $CONFIG_DIR/appsettings.json"
    fi
else
    echo "  Configuration already exists at $CONFIG_DIR"
fi

echo ""
echo "[5/6] Setting up udev rules..."
UDEV_RULES="/etc/udev/rules.d/99-lcdpossible.rules"
RULES_CONTENT='# LCDPossible - USB HID LCD device permissions
# Thermalright devices
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="5302", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="8001", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="usb", ATTR{idVendor}=="0418", ATTR{idProduct}=="5303", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="usb", ATTR{idVendor}=="0418", ATTR{idProduct}=="5304", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0416", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0418", MODE="0666", TAG+="uaccess"'

if [ -f "$UDEV_RULES" ] && grep -q "LCDPossible" "$UDEV_RULES" 2>/dev/null; then
    echo "  udev rules already configured."
else
    echo "  Installing udev rules..."
    echo "$RULES_CONTENT" | $SUDO tee "$UDEV_RULES" > /dev/null
    $SUDO udevadm control --reload-rules
    $SUDO udevadm trigger
fi

echo ""
echo "[6/6] Setting up systemd service..."
SERVICE_FILE="/etc/systemd/system/$SERVICE_NAME.service"
SERVICE_CONTENT="[Unit]
Description=LCDPossible LCD Controller Service
After=network.target

[Service]
Type=simple
ExecStart=$INSTALL_DIR/LCDPossible serve
WorkingDirectory=$INSTALL_DIR
Environment=DOTNET_ENVIRONMENT=Production
Environment=LCDPOSSIBLE_CONFIG=$CONFIG_DIR/appsettings.json
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target"

echo "$SERVICE_CONTENT" | $SUDO tee "$SERVICE_FILE" > /dev/null
$SUDO systemctl daemon-reload
$SUDO systemctl enable $SERVICE_NAME

echo ""
echo "=============================================="
echo "  Installation Complete!"
echo "=============================================="
echo ""
echo "Installed:"
echo "  [✓] LCDPossible $VERSION"
echo "  [✓] LibVLC (video playback)"
echo "  [✓] DejaVu fonts (text rendering)"
echo "  [✓] udev rules (USB device access)"
echo "  [✓] systemd service"
echo ""
echo "Locations:"
echo "  Binary:  $INSTALL_DIR/LCDPossible"
echo "  Config:  $CONFIG_DIR/appsettings.json"
echo "  Service: $SERVICE_FILE"
echo ""
echo "Commands:"
echo "  Start service:   sudo systemctl start $SERVICE_NAME"
echo "  Stop service:    sudo systemctl stop $SERVICE_NAME"
echo "  View logs:       sudo journalctl -u $SERVICE_NAME -f"
echo "  List devices:    $INSTALL_DIR/LCDPossible list"
echo "  Run manually:    $INSTALL_DIR/LCDPossible serve"
echo ""
echo "Edit $CONFIG_DIR/appsettings.json to configure your display."
echo ""
