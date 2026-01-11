#!/bin/bash
# LCDPossible - macOS Full Installer
# Usage: curl -sSL https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/install-macos.sh | bash
#
# This script is idempotent - safe to run multiple times.
# It will install dependencies, download the latest release, and set up a launch agent.

set -e

REPO="DevPossible/LCDPossible"
INSTALL_DIR="$HOME/.local/share/lcdpossible"
CONFIG_DIR="$HOME/.config/lcdpossible"
LAUNCH_AGENT_DIR="$HOME/Library/LaunchAgents"
LAUNCH_AGENT="com.lcdpossible.service.plist"

echo "=============================================="
echo "  LCDPossible Installer (macOS)"
echo "=============================================="
echo ""

# Check for Homebrew
if ! command -v brew &>/dev/null; then
    echo "ERROR: Homebrew is required but not installed."
    echo "Install it from: https://brew.sh"
    exit 1
fi

# Detect architecture
detect_arch() {
    local arch=$(uname -m)
    case $arch in
        x86_64)  echo "osx-x64" ;;
        arm64)   echo "osx-arm64" ;;
        *)       echo ""; return 1 ;;
    esac
}

ARCH=$(detect_arch)
if [ -z "$ARCH" ]; then
    echo "ERROR: Unsupported architecture: $(uname -m)"
    exit 1
fi

echo "[1/5] Installing dependencies..."
echo ""

echo "  Checking LibVLC..."
if brew list vlc &>/dev/null; then
    echo "    LibVLC already installed."
else
    echo "    Installing LibVLC (this may take a while)..."
    brew install --cask vlc
fi

echo "  Checking jq (for JSON parsing)..."
if ! command -v jq &>/dev/null; then
    echo "    Installing jq..."
    brew install jq
fi

echo ""
echo "[2/5] Fetching latest release..."
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
echo "[3/5] Downloading and extracting..."
if [ "$SKIP_DOWNLOAD" != "true" ]; then
    TEMP_DIR=$(mktemp -d)
    trap "rm -rf $TEMP_DIR" EXIT

    echo "  Downloading..."
    curl -sSL "$DOWNLOAD_URL" -o "$TEMP_DIR/lcdpossible.tar.gz"

    echo "  Stopping service if running..."
    launchctl unload "$LAUNCH_AGENT_DIR/$LAUNCH_AGENT" 2>/dev/null || true

    echo "  Extracting to $INSTALL_DIR..."
    mkdir -p "$INSTALL_DIR"
    tar -xzf "$TEMP_DIR/lcdpossible.tar.gz" -C "$INSTALL_DIR" --strip-components=1

    echo "  Setting permissions..."
    chmod +x "$INSTALL_DIR/LCDPossible"
fi

echo ""
echo "[4/5] Setting up configuration..."
if [ ! -d "$CONFIG_DIR" ]; then
    mkdir -p "$CONFIG_DIR"
    if [ -f "$INSTALL_DIR/appsettings.json" ]; then
        cp "$INSTALL_DIR/appsettings.json" "$CONFIG_DIR/appsettings.json"
        echo "  Created $CONFIG_DIR/appsettings.json"
    fi
else
    echo "  Configuration already exists at $CONFIG_DIR"
fi

echo ""
echo "[5/5] Setting up launch agent..."
mkdir -p "$LAUNCH_AGENT_DIR"
PLIST_CONTENT="<?xml version=\"1.0\" encoding=\"UTF-8\"?>
<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">
<plist version=\"1.0\">
<dict>
    <key>Label</key>
    <string>com.lcdpossible.service</string>
    <key>ProgramArguments</key>
    <array>
        <string>$INSTALL_DIR/LCDPossible</string>
        <string>serve</string>
    </array>
    <key>WorkingDirectory</key>
    <string>$INSTALL_DIR</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>DOTNET_ENVIRONMENT</key>
        <string>Production</string>
        <key>LCDPOSSIBLE_CONFIG</key>
        <string>$CONFIG_DIR/appsettings.json</string>
    </dict>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>StandardOutPath</key>
    <string>$HOME/Library/Logs/lcdpossible.log</string>
    <key>StandardErrorPath</key>
    <string>$HOME/Library/Logs/lcdpossible.error.log</string>
</dict>
</plist>"

echo "$PLIST_CONTENT" > "$LAUNCH_AGENT_DIR/$LAUNCH_AGENT"

echo ""
echo "=============================================="
echo "  Installation Complete!"
echo "=============================================="
echo ""
echo "Installed:"
echo "  [✓] LCDPossible $VERSION"
echo "  [✓] LibVLC (video playback)"
echo "  [✓] Launch agent (auto-start)"
echo ""
echo "Locations:"
echo "  Binary:  $INSTALL_DIR/LCDPossible"
echo "  Config:  $CONFIG_DIR/appsettings.json"
echo "  Logs:    ~/Library/Logs/lcdpossible.log"
echo "  Service: $LAUNCH_AGENT_DIR/$LAUNCH_AGENT"
echo ""
echo "Commands:"
echo "  Start service:   launchctl load $LAUNCH_AGENT_DIR/$LAUNCH_AGENT"
echo "  Stop service:    launchctl unload $LAUNCH_AGENT_DIR/$LAUNCH_AGENT"
echo "  View logs:       tail -f ~/Library/Logs/lcdpossible.log"
echo "  List devices:    $INSTALL_DIR/LCDPossible list"
echo "  Run manually:    $INSTALL_DIR/LCDPossible serve"
echo ""
echo "Edit $CONFIG_DIR/appsettings.json to configure your display."
echo ""
