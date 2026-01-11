# LCDPossible

Cross-platform .NET 10 LCD controller service for HID-based LCD screens.

[![CI](https://github.com/DevPossible/LCDPossible/actions/workflows/ci.yml/badge.svg)](https://github.com/DevPossible/LCDPossible/actions/workflows/ci.yml)
[![Release](https://github.com/DevPossible/LCDPossible/actions/workflows/release.yml/badge.svg)](https://github.com/DevPossible/LCDPossible/releases)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## Overview

LCDPossible is an open-source alternative to vendor-specific Windows-only software for controlling HID-based LCD displays found in AIO coolers and other PC components.

### Supported Devices

| Device | VID | PID | Resolution | Status |
|--------|-----|-----|------------|--------|
| Thermalright Trofeo Vision 360 ARGB | 0x0416 | 0x5302 | 1280x480 | Supported |
| Thermalright PA120 Digital | 0x0416 | 0x8001 | Segment | Partial |

## Installation

### Windows

**Option 1: Download Release (Recommended)**

1. Download the latest `lcdpossible-x.x.x-win-x64.zip` from [Releases](https://github.com/DevPossible/LCDPossible/releases)
2. Extract to a folder (e.g., `C:\Program Files\LCDPossible`)
3. Run `LCDPossible.exe` from the command line or add to PATH

**Option 2: Install as Windows Service**

```powershell
# After extracting, install as a service (run as Administrator)
sc.exe create LCDPossible binPath= "C:\Program Files\LCDPossible\LCDPossible.exe serve --service" start= auto
sc.exe start LCDPossible
```

### Linux (Debian/Ubuntu)

**Option 1: DEB Package (Recommended)**

```bash
# Download the .deb package from Releases
wget https://github.com/DevPossible/LCDPossible/releases/latest/download/lcdpossible_x.x.x_amd64.deb

# Install
sudo dpkg -i lcdpossible_*.deb

# Enable and start service
sudo systemctl enable lcdpossible
sudo systemctl start lcdpossible
```

**Option 2: Manual Installation**

```bash
# Download the tar.gz from Releases
wget https://github.com/DevPossible/LCDPossible/releases/latest/download/lcdpossible-x.x.x-linux-x64.tar.gz

# Extract
sudo mkdir -p /opt/lcdpossible
sudo tar -xzf lcdpossible-*.tar.gz -C /opt/lcdpossible

# Run the install script
sudo /opt/lcdpossible/installer/linux/install.sh
```

### Linux (Fedora/RHEL)

```bash
# Download the .rpm package from Releases
wget https://github.com/DevPossible/LCDPossible/releases/latest/download/lcdpossible-x.x.x.x86_64.rpm

# Install
sudo rpm -i lcdpossible-*.rpm

# Enable and start service
sudo systemctl enable lcdpossible
sudo systemctl start lcdpossible
```

### macOS

```bash
# Download the tar.gz from Releases
curl -LO https://github.com/DevPossible/LCDPossible/releases/latest/download/lcdpossible-x.x.x-osx-x64.tar.gz

# Extract
sudo mkdir -p /usr/local/lcdpossible
sudo tar -xzf lcdpossible-*.tar.gz -C /usr/local/lcdpossible

# Add to PATH
echo 'export PATH="/usr/local/lcdpossible:$PATH"' >> ~/.zshrc
source ~/.zshrc
```

### Build from Source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/DevPossible/LCDPossible.git
cd LCDPossible

# Build
./build.ps1      # PowerShell (Windows/Linux/macOS)
# or
dotnet build src/LCDPossible.sln

# Run tests
./test-full.ps1

# Package for distribution
./package.ps1 -Version "1.0.0"
```

## Usage

### CLI Commands

```bash
# List connected LCD devices
lcdpossible list

# Display a test pattern
lcdpossible test

# Send an image to the LCD
lcdpossible set-image -p wallpaper.jpg

# Quick inline panel display (system info)
lcdpossible show basic-info
lcdpossible show cpu-usage-graphic,ram-usage-graphic

# Display animated content
lcdpossible show animated-gif:animation.gif
lcdpossible show video:https://www.youtube.com/watch?v=aqz-KE-bpKQ
lcdpossible show web:https://wttr.in/London

# Start service (foreground)
lcdpossible serve

# Show all commands
lcdpossible --help
```

### Run as Service

**Windows:**
```powershell
# As Windows Service
lcdpossible serve --service
```

**Linux:**
```bash
# As systemd service
sudo systemctl start lcdpossible
sudo systemctl status lcdpossible

# View logs
journalctl -u lcdpossible -f
```

## Configuration

### Display Profiles (YAML)

Create `~/.config/lcdpossible/profile.yaml`:

```yaml
name: "System Monitor"
panels:
  - type: cpu
    duration: 5s
  - type: gpu
    duration: 5s
  - type: ram
    duration: 5s
colorScheme:
  primary: "#00FF00"
  secondary: "#FFFFFF"
  background: "#000000"
```

### appsettings.json

```json
{
  "LCDPossible": {
    "General": {
      "TargetFrameRate": 30,
      "AutoStart": true
    },
    "Devices": {
      "default": {
        "Brightness": 100,
        "Orientation": 0
      }
    }
  }
}
```

## Proxmox VE Integration

LCDPossible can display real-time metrics from your Proxmox VE cluster, including node status, VM/container information, and cluster alerts.

### Creating a Proxmox API Token

1. Log into the Proxmox web interface
2. Navigate to **Datacenter** > **Permissions** > **API Tokens**
3. Click **Add** to create a new token:
   - **User**: Select or create a user (e.g., `monitor@pve`)
   - **Token ID**: Enter a name (e.g., `lcdpossible`)
   - **Privilege Separation**: Uncheck for full user permissions, or leave checked for token-specific permissions
4. Click **Add** and **copy the token secret** (it won't be shown again)

The resulting Token ID format will be: `user@realm!tokenid` (e.g., `monitor@pve!lcdpossible`)

### Required Permissions

For the API token to access cluster metrics, grant these permissions:

```bash
# Minimal permissions for monitoring
pveum aclmod / -user monitor@pve -role PVEAuditor
```

Or create a custom role with specific permissions:
- `Sys.Audit` - View system information
- `VM.Audit` - View VM configuration and status
- `Datastore.Audit` - View storage information

### Proxmox Configuration

Add the Proxmox settings to your `appsettings.json`:

```json
{
  "LCDPossible": {
    "Proxmox": {
      "Enabled": true,
      "ApiUrl": "https://proxmox.local:8006",
      "TokenId": "monitor@pve!lcdpossible",
      "TokenSecret": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
      "IgnoreSslErrors": true,
      "PollingIntervalSeconds": 5,
      "ShowVms": true,
      "ShowContainers": true,
      "ShowAlerts": true,
      "MaxDisplayItems": 10
    },
    "Devices": {
      "default": {
        "Mode": "slideshow",
        "Slideshow": "proxmox-summary|15,proxmox-vms|15,cpu-usage-graphic|10"
      }
    }
  }
}
```

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `Enabled` | `false` | Enable/disable Proxmox integration |
| `ApiUrl` | - | Proxmox API URL (e.g., `https://192.168.1.100:8006`) |
| `TokenId` | - | API token ID in format `user@realm!tokenid` |
| `TokenSecret` | - | API token secret (UUID format) |
| `IgnoreSslErrors` | `false` | Skip SSL certificate verification (for self-signed certs) |
| `PollingIntervalSeconds` | `5` | How often to fetch metrics from Proxmox |
| `ShowVms` | `true` | Display individual VM status |
| `ShowContainers` | `true` | Display individual LXC container status |
| `ShowAlerts` | `true` | Display cluster alerts and warnings |
| `MaxDisplayItems` | `10` | Maximum VMs/containers to display |

### Proxmox Display Panels

Use these panel types in your slideshow or single-panel configuration:

| Panel | Description |
|-------|-------------|
| `proxmox-summary` | Cluster overview with node status, total resources, and alerts |
| `proxmox-vms` | List of VMs and containers with status indicators |

**Example slideshow configuration:**

```json
{
  "Devices": {
    "default": {
      "Mode": "slideshow",
      "Slideshow": "proxmox-summary|20,proxmox-vms|15,cpu-usage-graphic|10,ram-usage-graphic|10"
    }
  }
}
```

### Troubleshooting Proxmox Connection

**Connection refused or timeout:**
- Verify the Proxmox host is reachable: `curl -k https://proxmox.local:8006/api2/json/version`
- Check firewall rules allow port 8006

**401 Unauthorized:**
- Verify TokenId format is correct (`user@realm!tokenid`)
- Ensure the token secret is correct and not expired
- Check the user/token has the required permissions

**SSL certificate errors:**
- Set `"IgnoreSslErrors": true` for self-signed certificates
- Or install a valid SSL certificate on Proxmox

**No VMs/containers shown:**
- Verify `ShowVms` and `ShowContainers` are set to `true`
- Check the token has `VM.Audit` permission
- Ensure VMs/containers exist and are visible to the user

## Linux USB Permissions

If you get "access denied" errors, install the udev rules:

```bash
# Copy rules file
sudo cp /opt/lcdpossible/99-lcdpossible.rules /etc/udev/rules.d/

# Reload rules
sudo udevadm control --reload-rules
sudo udevadm trigger

# Log out and back in (or reboot)
```

Or manually create `/etc/udev/rules.d/99-lcdpossible.rules`:

```
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="5302", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0416", MODE="0666", TAG+="uaccess"
```

## Project Structure

```
LCDPossible/
├── .github/                       # CI/CD workflows
├── docs/                          # Documentation
├── installer/                     # Platform installers
│   ├── windows/                   # Windows MSIX/ZIP
│   └── linux/                     # Linux DEB/RPM/tar.gz
├── src/
│   ├── LCDPossible.sln            # Solution file
│   ├── LCDPossible/               # Main executable (service + CLI)
│   └── LCDPossible.Core/          # Core library
├── tests/
│   └── LCDPossible.Core.Tests/    # Unit tests
├── build.ps1                      # Build script
├── package.ps1                    # Package for distribution
└── start-app.ps1                  # Run service
```

## Media Panels

LCDPossible supports various media panels for displaying dynamic content beyond system monitoring.

### Animated GIF Panel

Display animated GIF files from local paths or URLs.

```bash
# Local file
lcdpossible show animated-gif:C:\gifs\animation.gif

# Remote URL (CC-BY-SA licensed example)
lcdpossible show animated-gif:https://upload.wikimedia.org/wikipedia/commons/2/2c/Rotating_earth_%28large%29.gif
```

### Image Sequence Panel

Display a folder of numbered images as an animation (e.g., frame001.png, frame002.png).

```bash
lcdpossible show image-sequence:C:\frames\
```

Settings:
- `fps` - Frame rate (1-120, default: 30)
- `loop` - Loop playback (true/false, default: true)

### Video Panel

Play video files, direct URLs, or YouTube videos using LibVLC.

```bash
# Local video file
lcdpossible show video:C:\videos\demo.mp4

# Direct video URL (CC-BY licensed Big Buck Bunny)
lcdpossible show video:https://archive.org/download/BigBuckBunny_124/Content/big_buck_bunny_720p_surround.mp4

# YouTube URL (CC-BY licensed)
lcdpossible show video:https://www.youtube.com/watch?v=aqz-KE-bpKQ
```

Settings:
- `loop` - Loop playback (true/false, default: true)
- `volume` - Audio volume 0-100 (default: 0, muted)

### HTML Panel

Render a local HTML file as a web page using a headless browser.

```bash
lcdpossible show html:C:\dashboard\status.html
```

Settings:
- `refresh` - Refresh interval in seconds (default: 5)

### Web Panel

Display a live website from a URL using a headless browser.

```bash
# Weather display (wttr.in is designed for programmatic access)
lcdpossible show web:https://wttr.in/London
```

Settings:
- `refresh` - Refresh interval in seconds (default: 30)
- `autorefresh` - Auto-refresh the page (true/false, default: true)

### Sample Media URLs (CC-Licensed)

| Type | URL | License |
|------|-----|---------|
| Animated GIF | `https://upload.wikimedia.org/wikipedia/commons/2/2c/Rotating_earth_%28large%29.gif` | CC-BY-SA |
| Video | `https://archive.org/download/BigBuckBunny_124/Content/big_buck_bunny_720p_surround.mp4` | CC-BY |
| YouTube | `https://www.youtube.com/watch?v=aqz-KE-bpKQ` | CC-BY |

## Technology Stack

- **HidSharp** - Cross-platform USB HID communication
- **SixLabors.ImageSharp** - Image processing
- **LibVLCSharp** - Video playback (with VideoLAN.LibVLC natives)
- **PuppeteerSharp** - Headless browser for HTML/Web panels
- **YoutubeExplode** - YouTube stream URL extraction
- **Microsoft.Extensions.Hosting** - Service hosting
- **Serilog** - Structured logging
- **LibreHardwareMonitorLib** - Hardware monitoring (Windows)

## Contributing

Contributions are welcome! Please read the [Implementation Plan](docs/Implementation-Plan.md) for architecture details.

### Commit Message Format

We use [Conventional Commits](https://www.conventionalcommits.org/) for automatic versioning:

```
feat: add GPU temperature panel     # Minor version bump
fix: correct USB timeout handling   # Patch version bump
feat!: redesign device driver API   # Major version bump
docs: update installation guide     # No version bump
```

### Adding a New Device Driver

1. Create a new driver class implementing `ILcdDevice` in `src/LCDPossible.Core/Devices/Drivers/{Manufacturer}/`
2. Register the driver in `DeviceRegistry.cs`
3. Add tests

## License

MIT License - see [LICENSE](LICENSE) file.

## Acknowledgments

- [digital_thermal_right_lcd](https://github.com/MathieuxHugo/digital_thermal_right_lcd) - Python reference implementation
- [HidSharp](https://github.com/IntergatedCircuits/HidSharp) - Cross-platform HID library
