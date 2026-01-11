#!/bin/bash
# LCDPossible - Fedora/RHEL Full Installer
# Usage: curl -sSL https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/install-fedora.sh | bash
#
# This script is idempotent - safe to run multiple times.
# Re-running will verify all components and upgrade if a new version is available.

set -e

REPO="DevPossible/LCDPossible"
INSTALL_DIR="/opt/lcdpossible"
SERVICE_NAME="lcdpossible"
CONFIG_DIR="/etc/lcdpossible"

echo "=============================================="
echo "  LCDPossible Installer (Fedora/RHEL)"
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
    rpm -q "$1" &>/dev/null
}

# Detect package manager (dnf vs yum)
if command -v dnf &>/dev/null; then
    PKG_MGR="dnf"
else
    PKG_MGR="yum"
fi

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

echo "[1/7] Checking/installing dependencies..."
echo ""

echo "  Checking LibVLC..."
if is_installed "vlc-devel"; then
    echo "    [OK] LibVLC already installed."
else
    echo "    Installing LibVLC..."
    # Enable RPM Fusion for VLC (if not already enabled)
    if ! is_installed "rpmfusion-free-release"; then
        echo "    Enabling RPM Fusion repository..."
        $SUDO $PKG_MGR install -y \
            "https://download1.rpmfusion.org/free/fedora/rpmfusion-free-release-$(rpm -E %fedora).noarch.rpm" \
            2>/dev/null || true
    fi
    $SUDO $PKG_MGR install -y -q vlc vlc-devel
    echo "    [OK] LibVLC installed."
fi

echo "  Checking fonts..."
if is_installed "dejavu-sans-fonts"; then
    echo "    [OK] Fonts already installed."
else
    echo "    Installing fonts..."
    $SUDO $PKG_MGR install -y -q dejavu-sans-fonts
    echo "    [OK] Fonts installed."
fi

echo "  Checking jq (for JSON parsing)..."
if command -v jq &>/dev/null; then
    echo "    [OK] jq already installed."
else
    echo "    Installing jq..."
    $SUDO $PKG_MGR install -y -q jq
    echo "    [OK] jq installed."
fi

echo ""
echo "[2/7] Fetching latest release..."
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

# Check installed version
SKIP_DOWNLOAD=false
INSTALLED_VERSION=""
IS_UPGRADE=false
if [ -f "$INSTALL_DIR/version.json" ]; then
    INSTALLED_VERSION=$(jq -r '.Version' "$INSTALL_DIR/version.json" 2>/dev/null || echo "")
    if [ -n "$INSTALLED_VERSION" ]; then
        echo "  Installed version: v$INSTALLED_VERSION"
        if [ "$INSTALLED_VERSION" = "${VERSION#v}" ]; then
            echo ""
            echo "  Version $VERSION is already installed."
            read -p "  Reinstall anyway? [y/N] " -n 1 -r
            echo
            if [[ ! $REPLY =~ ^[Yy]$ ]]; then
                SKIP_DOWNLOAD=true
                echo "  Skipping download, will verify configuration..."
            fi
        else
            IS_UPGRADE=true
            echo ""
            echo "  ** Upgrading from v$INSTALLED_VERSION to $VERSION **"
        fi
    fi
fi

echo ""
echo "[3/7] Downloading and extracting..."
if [ "$SKIP_DOWNLOAD" != "true" ]; then
    TEMP_DIR=$(mktemp -d)
    trap "rm -rf $TEMP_DIR" EXIT

    echo "  Downloading from: $DOWNLOAD_URL"
    curl -sSL "$DOWNLOAD_URL" -o "$TEMP_DIR/lcdpossible.tar.gz"

    echo "  Stopping service if running..."
    $SUDO systemctl stop $SERVICE_NAME 2>/dev/null || true

    echo "  Extracting to $INSTALL_DIR..."
    $SUDO mkdir -p "$INSTALL_DIR"
    $SUDO tar -xzf "$TEMP_DIR/lcdpossible.tar.gz" -C "$INSTALL_DIR" --strip-components=1

    echo "  Setting executable permissions..."
    $SUDO chmod +x "$INSTALL_DIR/LCDPossible"
    echo "  [OK] Extracted and configured."
else
    echo "  [SKIP] Using existing installation."
fi

echo ""
echo "[4/7] Verifying configuration..."
if [ ! -d "$CONFIG_DIR" ]; then
    $SUDO mkdir -p "$CONFIG_DIR"
    if [ -f "$INSTALL_DIR/appsettings.json" ]; then
        $SUDO cp "$INSTALL_DIR/appsettings.json" "$CONFIG_DIR/appsettings.json"
        echo "  [OK] Created $CONFIG_DIR/appsettings.json"
    fi
else
    echo "  [OK] Configuration exists at $CONFIG_DIR"
    if [ ! -f "$CONFIG_DIR/appsettings.json" ] && [ -f "$INSTALL_DIR/appsettings.json" ]; then
        $SUDO cp "$INSTALL_DIR/appsettings.json" "$CONFIG_DIR/appsettings.json"
        echo "  [OK] Restored missing appsettings.json"
    fi
fi

echo ""
echo "[5/7] Updating udev rules..."
UDEV_RULES="/etc/udev/rules.d/99-lcdpossible.rules"
RULES_CONTENT='# LCDPossible - USB HID LCD device permissions
# Thermalright devices
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="5302", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="8001", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="usb", ATTR{idVendor}=="0418", ATTR{idProduct}=="5303", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="usb", ATTR{idVendor}=="0418", ATTR{idProduct}=="5304", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0416", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0418", MODE="0666", TAG+="uaccess"'

# Always write udev rules to ensure they're up to date
echo "$RULES_CONTENT" | $SUDO tee "$UDEV_RULES" > /dev/null
$SUDO udevadm control --reload-rules
$SUDO udevadm trigger
echo "  [OK] udev rules updated and reloaded."

echo ""
echo "[6/7] Updating systemd service..."
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
echo "  [OK] Service configured and enabled."

echo ""
echo "[7/7] Starting service..."
$SUDO systemctl start $SERVICE_NAME
if $SUDO systemctl is-active --quiet $SERVICE_NAME; then
    echo "  [OK] Service is running."
else
    echo "  [WARN] Service may have failed to start. Check: journalctl -u $SERVICE_NAME"
fi

echo ""
echo "=============================================="
if [ "$IS_UPGRADE" = "true" ]; then
    echo "  Upgrade Complete! (v$INSTALLED_VERSION -> $VERSION)"
elif [ "$SKIP_DOWNLOAD" = "true" ]; then
    echo "  Verification Complete! (v$INSTALLED_VERSION)"
else
    echo "  Installation Complete! ($VERSION)"
fi
echo "=============================================="
echo ""
echo "Verified:"
echo "  [+] LCDPossible $VERSION"
echo "  [+] LibVLC (video playback)"
echo "  [+] DejaVu fonts (text rendering)"
echo "  [+] udev rules (USB device access)"
echo "  [+] systemd service"
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
