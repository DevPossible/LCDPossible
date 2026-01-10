#!/bin/bash
# LCDPossible Linux Installation Script
set -e

INSTALL_DIR="/opt/lcdpossible"
BIN_LINK="/usr/local/bin/lcdpossible"
UDEV_RULES="/etc/udev/rules.d/99-lcdpossible.rules"
SYSTEMD_SERVICE="/etc/systemd/system/lcdpossible.service"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    print_error "This script must be run as root (use sudo)"
    exit 1
fi

# Determine script directory (where the files are)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

print_status "Installing LCDPossible..."

# Create installation directory
print_status "Creating installation directory: $INSTALL_DIR"
mkdir -p "$INSTALL_DIR"

# Copy files
if [ -f "$SCRIPT_DIR/LCDPossible" ]; then
    print_status "Copying application files..."
    cp -r "$SCRIPT_DIR"/* "$INSTALL_DIR/"
    chmod +x "$INSTALL_DIR/LCDPossible"
elif [ -f "$SCRIPT_DIR/../publish/LCDPossible" ]; then
    print_status "Copying application files from publish directory..."
    cp -r "$SCRIPT_DIR/../publish"/* "$INSTALL_DIR/"
    chmod +x "$INSTALL_DIR/LCDPossible"
else
    print_error "LCDPossible executable not found. Please run from the installation package directory."
    exit 1
fi

# Create symlink in PATH
print_status "Creating command symlink: $BIN_LINK"
ln -sf "$INSTALL_DIR/LCDPossible" "$BIN_LINK"

# Install udev rules for USB HID access
print_status "Installing udev rules for USB access..."
cat > "$UDEV_RULES" << 'EOF'
# LCDPossible - USB HID LCD device rules
# Thermalright devices
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="5302", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="8001", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="usb", ATTR{idVendor}=="0418", ATTR{idProduct}=="5303", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="usb", ATTR{idVendor}=="0418", ATTR{idProduct}=="5304", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0416", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0418", MODE="0666", TAG+="uaccess"
EOF

# Reload udev rules
print_status "Reloading udev rules..."
udevadm control --reload-rules
udevadm trigger

# Install systemd service
print_status "Installing systemd service..."
cat > "$SYSTEMD_SERVICE" << 'EOF'
[Unit]
Description=LCDPossible LCD Controller Service
After=network.target

[Service]
Type=simple
ExecStart=/opt/lcdpossible/LCDPossible serve
Restart=on-failure
RestartSec=5
User=root
WorkingDirectory=/opt/lcdpossible

[Install]
WantedBy=multi-user.target
EOF

# Reload systemd
systemctl daemon-reload

print_status "Installation complete!"
echo ""
echo "Usage:"
echo "  lcdpossible list          - List connected LCD devices"
echo "  lcdpossible test          - Display test pattern"
echo "  lcdpossible serve         - Start the service (foreground)"
echo ""
echo "To enable as a service:"
echo "  sudo systemctl enable lcdpossible"
echo "  sudo systemctl start lcdpossible"
echo ""
print_warning "You may need to log out and back in for udev rules to take effect."
print_warning "Or run: sudo udevadm trigger"
