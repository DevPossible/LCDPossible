# Service Setup

LCDPossible can run as a background service that starts automatically with your system.

## Quick Start

### Windows

```powershell
# Install as Windows Service
lcdpossible service install

# Start the service
lcdpossible service start

# Check status
lcdpossible service status
```

### Linux

```bash
# Install as systemd service
sudo lcdpossible service install

# Start the service
sudo systemctl start lcdpossible

# Enable on boot
sudo systemctl enable lcdpossible
```

### macOS

```bash
# Install as Launch Agent
lcdpossible service install

# Load the agent
launchctl load ~/Library/LaunchAgents/com.devpossible.lcdpossible.plist
```

## Service Commands

| Command | Description |
|---------|-------------|
| `service install` | Install as system service |
| `service remove` | Remove the service |
| `service start` | Start the service |
| `service stop` | Stop the service |
| `service restart` | Restart the service |
| `service status` | Show service status |
| `service help` | Show service commands |

## Windows Service

### Installation

```powershell
# Install with default settings
lcdpossible service install

# The service is installed as:
# - Name: LCDPossible
# - Display Name: LCDPossible Display Service
# - Startup Type: Automatic
```

### Management

```powershell
# Start
lcdpossible service start
# or: sc start LCDPossible
# or: Start-Service LCDPossible

# Stop
lcdpossible service stop
# or: sc stop LCDPossible
# or: Stop-Service LCDPossible

# Restart
lcdpossible service restart
# or: Restart-Service LCDPossible

# Status
lcdpossible service status
# or: sc query LCDPossible
# or: Get-Service LCDPossible
```

### Removal

```powershell
# Stop and remove
lcdpossible service stop
lcdpossible service remove
```

### Logs

View Windows Event Log:
```powershell
Get-EventLog -LogName Application -Source LCDPossible -Newest 50
```

Or use Event Viewer: Applications and Services Logs > LCDPossible

## Linux (systemd)

### Prerequisites

```bash
# USB device access (required for HID communication)
# Create udev rules for your devices

# Thermalright devices
sudo tee /etc/udev/rules.d/99-lcdpossible.rules << 'EOF'
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="5302", MODE="0666"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0416", MODE="0666"
EOF

# Reload udev rules
sudo udevadm control --reload-rules
sudo udevadm trigger
```

### Installation

```bash
# Install as systemd service (requires root)
sudo lcdpossible service install

# The service file is installed to:
# /etc/systemd/system/lcdpossible.service
```

### Service File

The generated service file:

```ini
[Unit]
Description=LCDPossible Display Service
After=network.target

[Service]
Type=simple
ExecStart=/usr/local/bin/lcdpossible serve
Restart=always
RestartSec=5
User=root

[Install]
WantedBy=multi-user.target
```

### Management

```bash
# Start
sudo systemctl start lcdpossible

# Stop
sudo systemctl stop lcdpossible

# Restart
sudo systemctl restart lcdpossible

# Enable on boot
sudo systemctl enable lcdpossible

# Disable on boot
sudo systemctl disable lcdpossible

# Status
sudo systemctl status lcdpossible

# View logs
sudo journalctl -u lcdpossible -f
```

### Removal

```bash
sudo systemctl stop lcdpossible
sudo systemctl disable lcdpossible
sudo lcdpossible service remove
```

## macOS (launchd)

### Installation

```bash
# Install as Launch Agent (user service)
lcdpossible service install

# The plist is installed to:
# ~/Library/LaunchAgents/com.devpossible.lcdpossible.plist
```

### Launch Agent Plist

The generated plist file:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.devpossible.lcdpossible</string>
    <key>ProgramArguments</key>
    <array>
        <string>/usr/local/bin/lcdpossible</string>
        <string>serve</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
</dict>
</plist>
```

### Management

```bash
# Load (start)
launchctl load ~/Library/LaunchAgents/com.devpossible.lcdpossible.plist

# Unload (stop)
launchctl unload ~/Library/LaunchAgents/com.devpossible.lcdpossible.plist

# Status (macOS 10.10+)
launchctl list | grep lcdpossible

# View logs
tail -f ~/Library/Logs/lcdpossible.log
```

### Removal

```bash
launchctl unload ~/Library/LaunchAgents/com.devpossible.lcdpossible.plist
lcdpossible service remove
```

## Running Without Service

For testing or temporary use, run in the foreground:

```bash
# Start display service in foreground
lcdpossible serve

# With debug output
lcdpossible serve --debug

# With specific profile
lcdpossible serve --profile my-profile

# Press Ctrl+C to stop
```

## Runtime Control

While the service is running, use these commands:

```bash
# Navigate slides
lcdpossible next
lcdpossible previous
lcdpossible goto 3

# Reload profile
lcdpossible profile reload

# Stop service gracefully
lcdpossible stop
```

## Troubleshooting

### Windows

**Service won't start:**
1. Check Event Viewer for errors
2. Verify USB device is connected
3. Run `lcdpossible list` to check device detection
4. Try running in foreground: `lcdpossible serve --debug`

**Permission denied:**
- Run PowerShell as Administrator for service installation

### Linux

**Service fails with permission error:**
1. Check udev rules are installed
2. Verify user permissions: `ls -la /dev/hidraw*`
3. Try running as root: `sudo lcdpossible serve`

**USB device not found:**
```bash
# Check HID devices
ls -la /dev/hidraw*

# Check USB devices
lsusb | grep -i 0416

# Reload udev rules
sudo udevadm control --reload-rules
sudo udevadm trigger
```

### macOS

**Permission denied on USB:**
- macOS may require additional USB permissions
- Check System Preferences > Security & Privacy

**Launch Agent not starting:**
```bash
# Check for errors
launchctl error system/com.devpossible.lcdpossible

# View system log
log show --predicate 'process == "lcdpossible"' --last 1h
```

## Multiple Devices

When using multiple LCD devices:

1. Each device is identified by VID:PID
2. Configure per-device settings in `appsettings.json`
3. Service manages all detected devices automatically

```json
{
  "LCDPossible": {
    "Devices": {
      "0416:5302": {
        "Mode": "slideshow",
        "Brightness": 100
      },
      "0416:5304": {
        "Mode": "panel",
        "Panel": "clock"
      }
    }
  }
}
```

---

*See also: [Settings](settings.md) | [Profiles](profiles.md)*

*[Back to Configuration](README.md)*
