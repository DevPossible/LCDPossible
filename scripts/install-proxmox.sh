#!/bin/bash
# LCDPossible - Proxmox VE Full Installer
# Usage: curl -sSL https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/install-proxmox.sh | bash
#
# Proxmox VE runs as root, so no sudo is required.
# This script is idempotent - safe to run multiple times.
# Re-running will verify all components and upgrade if a new version is available.

set -e

REPO="DevPossible/LCDPossible"
INSTALL_DIR="/opt/lcdpossible"
SERVICE_NAME="lcdpossible"
CONFIG_DIR="/etc/lcdpossible"

echo "=============================================="
echo "  LCDPossible Installer (Proxmox VE)"
echo "=============================================="
echo ""

# Verify running as root (Proxmox default)
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: This script must be run as root on Proxmox VE."
    echo "Run: curl -sSL https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/install-proxmox.sh | bash"
    exit 1
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
        *)       echo ""; return 1 ;;
    esac
}

ARCH=$(detect_arch)
if [ -z "$ARCH" ]; then
    echo "ERROR: Unsupported architecture: $(uname -m)"
    exit 1
fi

echo "[1/8] Checking/installing dependencies..."
echo ""

echo "  Checking LibVLC..."
if is_installed "libvlc-dev"; then
    echo "    [OK] LibVLC already installed."
else
    echo "    Installing LibVLC..."
    apt-get update -qq
    apt-get install -y -qq vlc libvlc-dev
    echo "    [OK] LibVLC installed."
fi

echo "  Checking fonts..."
if is_installed "fonts-dejavu-core"; then
    echo "    [OK] Fonts already installed."
else
    echo "    Installing fonts..."
    apt-get install -y -qq fonts-dejavu-core
    echo "    [OK] Fonts installed."
fi

echo "  Checking jq (for JSON parsing)..."
if command -v jq &>/dev/null; then
    echo "    [OK] jq already installed."
else
    echo "    Installing jq..."
    apt-get install -y -qq jq
    echo "    [OK] jq installed."
fi

echo ""
echo "[2/8] Fetching latest release..."
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
echo "[3/8] Downloading and extracting..."
if [ "$SKIP_DOWNLOAD" != "true" ]; then
    TEMP_DIR=$(mktemp -d)
    trap "rm -rf $TEMP_DIR" EXIT

    echo "  Downloading from: $DOWNLOAD_URL"
    curl -sSL "$DOWNLOAD_URL" -o "$TEMP_DIR/lcdpossible.tar.gz"

    echo "  Stopping service if running..."
    systemctl stop $SERVICE_NAME 2>/dev/null || true

    echo "  Extracting to $INSTALL_DIR..."
    mkdir -p "$INSTALL_DIR"
    tar -xzf "$TEMP_DIR/lcdpossible.tar.gz" -C "$INSTALL_DIR" --strip-components=1

    echo "  Setting executable permissions..."
    chmod +x "$INSTALL_DIR/LCDPossible"
    echo "  [OK] Extracted and configured."
else
    echo "  [SKIP] Using existing installation."
fi

echo ""
echo "[4/8] Verifying configuration..."
if [ ! -d "$CONFIG_DIR" ]; then
    mkdir -p "$CONFIG_DIR"
    if [ -f "$INSTALL_DIR/appsettings.json" ]; then
        cp "$INSTALL_DIR/appsettings.json" "$CONFIG_DIR/appsettings.json"
        echo "  [OK] Created $CONFIG_DIR/appsettings.json"
    fi
else
    echo "  [OK] Configuration exists at $CONFIG_DIR"
    # Check if config file exists
    if [ ! -f "$CONFIG_DIR/appsettings.json" ] && [ -f "$INSTALL_DIR/appsettings.json" ]; then
        cp "$INSTALL_DIR/appsettings.json" "$CONFIG_DIR/appsettings.json"
        echo "  [OK] Restored missing appsettings.json"
    fi
fi

echo ""
echo "[5/8] Updating udev rules..."
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
echo "$RULES_CONTENT" | tee "$UDEV_RULES" > /dev/null
udevadm control --reload-rules
udevadm trigger
echo "  [OK] udev rules updated and reloaded."

echo ""
echo "[6/8] Configuring Proxmox API access..."
PROXMOX_API_CONFIGURED=false
CREATE_NEW_TOKEN=false

# Check if Proxmox config already exists
if [ -f "$CONFIG_DIR/appsettings.json" ]; then
    PROXMOX_ENABLED=$(jq -r '.LCDPossible.Proxmox.Enabled // false' "$CONFIG_DIR/appsettings.json" 2>/dev/null)
    EXISTING_TOKEN_ID=$(jq -r '.LCDPossible.Proxmox.TokenId // ""' "$CONFIG_DIR/appsettings.json" 2>/dev/null)
    EXISTING_API_URL=$(jq -r '.LCDPossible.Proxmox.ApiUrl // ""' "$CONFIG_DIR/appsettings.json" 2>/dev/null)

    if [ "$PROXMOX_ENABLED" = "true" ] && [ -n "$EXISTING_TOKEN_ID" ] && [ "$EXISTING_TOKEN_ID" != "null" ]; then
        # Existing configuration found
        echo "  Existing Proxmox API configuration found:"
        echo "    URL:      $EXISTING_API_URL"
        echo "    Token ID: $EXISTING_TOKEN_ID"
        echo ""
        echo "  Options:"
        echo "    [K] Keep existing configuration"
        echo "    [N] Create new API token (regenerate)"
        echo "    [D] Disable Proxmox integration"
        echo ""
        # Read from /dev/tty to work with curl | bash
        read -p "  Choice [K/n/d]: " -n 1 -r REPLY </dev/tty
        echo

        if [[ $REPLY =~ ^[Nn]$ ]]; then
            # User wants to create new token
            echo "  Creating new Proxmox API token..."
            CREATE_NEW_TOKEN=true
        elif [[ $REPLY =~ ^[Dd]$ ]]; then
            # User wants to disable
            echo "  Disabling Proxmox API integration..."
            TMP_CONFIG=$(mktemp)
            jq '.LCDPossible.Proxmox.Enabled = false' "$CONFIG_DIR/appsettings.json" > "$TMP_CONFIG"
            mv "$TMP_CONFIG" "$CONFIG_DIR/appsettings.json"
            echo "  [OK] Proxmox API disabled."
        else
            # Keep existing (default)
            echo "  [OK] Keeping existing Proxmox API configuration."
            PROXMOX_API_CONFIGURED=true
        fi
    else
        # No existing config, ask if they want to set it up
        echo ""
        # Read from /dev/tty to work with curl | bash
        read -p "  Configure Proxmox API integration? [Y/n] " -n 1 -r REPLY </dev/tty
        echo
        if [[ ! $REPLY =~ ^[Nn]$ ]]; then
            CREATE_NEW_TOKEN=true
        else
            echo "  [SKIP] Proxmox API not configured."
        fi
    fi
else
    # Config file doesn't exist yet - this shouldn't happen but handle it
    echo "  [WARN] Config file not found, skipping Proxmox API setup."
    echo "  Run the installer again after the config file is created."
fi

# Create new API token if requested
if [ "$CREATE_NEW_TOKEN" = "true" ]; then
    PVE_USER="lcdpossible@pve"
    PVE_TOKEN="lcdpossible"

    echo "  Creating Proxmox API user and token..."

    # Create user if it doesn't exist
    if ! pveum user list | grep -q "lcdpossible@pve"; then
        pveum user add "$PVE_USER" --comment "LCDPossible LCD Controller" 2>/dev/null || true
        echo "    [OK] Created user: $PVE_USER"
    else
        echo "    [OK] User already exists: $PVE_USER"
    fi

    # Create or regenerate token
    TOKEN_OUTPUT=$(pveum user token add "$PVE_USER" "$PVE_TOKEN" --privsep=0 2>&1 || true)
    if echo "$TOKEN_OUTPUT" | grep -q "already exists"; then
        # Token exists, delete and recreate
        pveum user token remove "$PVE_USER" "$PVE_TOKEN" 2>/dev/null || true
        TOKEN_OUTPUT=$(pveum user token add "$PVE_USER" "$PVE_TOKEN" --privsep=0 2>&1)
    fi

    # Extract token value
    TOKEN_SECRET=$(echo "$TOKEN_OUTPUT" | grep "value" | awk '{print $NF}' | tr -d '\n')

    if [ -n "$TOKEN_SECRET" ]; then
        echo "    [OK] Created API token: $PVE_USER!$PVE_TOKEN"

        # Grant read-only access
        pveum aclmod / -user "$PVE_USER" -role PVEAuditor 2>/dev/null || true
        echo "    [OK] Granted PVEAuditor role (read-only)"

        # Get hostname for API URL
        HOSTNAME=$(hostname -f 2>/dev/null || hostname)
        API_URL="https://${HOSTNAME}:8006"

        # Update config file with Proxmox settings
        TMP_CONFIG=$(mktemp)
        jq --arg url "$API_URL" \
           --arg tokenId "$PVE_USER!$PVE_TOKEN" \
           --arg tokenSecret "$TOKEN_SECRET" \
           '.LCDPossible.Proxmox = {
               "Enabled": true,
               "ApiUrl": $url,
               "TokenId": $tokenId,
               "TokenSecret": $tokenSecret,
               "IgnoreSslErrors": true,
               "PollingIntervalSeconds": 5,
               "ShowVms": true,
               "ShowContainers": true,
               "ShowAlerts": true,
               "MaxDisplayItems": 10
           }' "$CONFIG_DIR/appsettings.json" > "$TMP_CONFIG"
        mv "$TMP_CONFIG" "$CONFIG_DIR/appsettings.json"
        echo "    [OK] Updated appsettings.json with Proxmox API config"

        echo ""
        echo "  Proxmox API configured:"
        echo "    URL:      $API_URL"
        echo "    Token ID: $PVE_USER!$PVE_TOKEN"
        echo "    Secret:   (saved to config)"
        PROXMOX_API_CONFIGURED=true
    else
        echo "    [WARN] Could not create API token. Configure manually."
    fi
fi

echo ""
echo "[7/9] Creating command symlink..."
SYMLINK_PATH="/usr/local/bin/lcdpossible"
# Check if symlink already points to correct target
if [ -L "$SYMLINK_PATH" ] && [ "$(readlink "$SYMLINK_PATH")" = "$INSTALL_DIR/LCDPossible" ]; then
    echo "  [OK] Symlink already exists and is correct."
else
    # Remove any existing file/symlink and create fresh
    if [ -L "$SYMLINK_PATH" ] || [ -e "$SYMLINK_PATH" ]; then
        rm -f "$SYMLINK_PATH"
    fi
    ln -s "$INSTALL_DIR/LCDPossible" "$SYMLINK_PATH"
    echo "  [OK] Created symlink: $SYMLINK_PATH -> $INSTALL_DIR/LCDPossible"
fi

echo ""
echo "[8/9] Updating systemd service..."
SERVICE_FILE="/etc/systemd/system/$SERVICE_NAME.service"

# Stop service if running (to apply new service definition)
if systemctl is-active --quiet $SERVICE_NAME 2>/dev/null; then
    echo "  Stopping existing service..."
    systemctl stop $SERVICE_NAME
fi

# Remove old service file to ensure clean update
if [ -f "$SERVICE_FILE" ]; then
    rm -f "$SERVICE_FILE"
fi

SERVICE_CONTENT="[Unit]
Description=LCDPossible LCD Controller Service
After=network.target pve-cluster.service

[Service]
Type=simple
ExecStart=$INSTALL_DIR/LCDPossible serve
WorkingDirectory=$INSTALL_DIR
Environment=DOTNET_ENVIRONMENT=Production
Environment=LCDPOSSIBLE_DATA_DIR=$CONFIG_DIR
Environment=LCDPOSSIBLE_CONFIG=$CONFIG_DIR/appsettings.json
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target"

echo "$SERVICE_CONTENT" | tee "$SERVICE_FILE" > /dev/null
systemctl daemon-reload
systemctl enable $SERVICE_NAME
echo "  [OK] Service configured and enabled."

echo ""
echo "[9/9] Starting service..."
systemctl start $SERVICE_NAME
if systemctl is-active --quiet $SERVICE_NAME; then
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
echo "  [+] CLI command (lcdpossible)"
if [ "$PROXMOX_API_CONFIGURED" = "true" ]; then
    echo "  [+] Proxmox API integration"
fi
echo ""
echo "Locations:"
echo "  Binary:  $INSTALL_DIR/LCDPossible"
echo "  Command: $SYMLINK_PATH"
echo "  Config:  $CONFIG_DIR/appsettings.json"
echo "  Service: $SERVICE_FILE"
echo ""
echo "Commands:"
echo "  Start service:   systemctl start $SERVICE_NAME"
echo "  Stop service:    systemctl stop $SERVICE_NAME"
echo "  View logs:       journalctl -u $SERVICE_NAME -f"
echo "  List devices:    lcdpossible list"
echo "  Show status:     lcdpossible status"
echo "  Run manually:    lcdpossible serve"
echo ""
echo "Proxmox-specific panels:"
echo "  proxmox-summary  - Show cluster/node overview"
echo "  proxmox-vms      - Show VM/container status"
echo ""
echo "Edit $CONFIG_DIR/appsettings.json to configure your display."
echo ""
