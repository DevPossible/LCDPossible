# LCDPossible

Cross-platform .NET 10 LCD controller service for HID-based LCD screens.

[![CI](https://github.com/DevPossible/LCDPossible/actions/workflows/ci.yml/badge.svg)](https://github.com/DevPossible/LCDPossible/actions/workflows/ci.yml)
[![Release](https://github.com/DevPossible/LCDPossible/actions/workflows/release.yml/badge.svg)](https://github.com/DevPossible/LCDPossible/releases)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## Overview

LCDPossible is an open-source alternative to vendor-specific Windows-only software for controlling HID-based LCD displays found in AIO coolers and other PC components. It provides:

- **Real-time system monitoring** - CPU, GPU, RAM usage and temperatures
- **Media playback** - Animated GIFs, videos, YouTube, and web pages
- **Screensavers** - 14 built-in animated screensavers
- **Slideshows** - Automatic panel rotation with smooth transitions
- **Plugin architecture** - Extensible panel system
- **Cross-platform** - Windows, Linux, and macOS support

## Supported Devices

| Device | VID | PID | Resolution | Status |
|--------|-----|-----|------------|--------|
| Thermalright Trofeo Vision 360 ARGB | 0x0416 | 0x5302 | 1280x480 | Fully Supported |
| Thermalright PA120 Digital | 0x0416 | 0x8001 | Segment | Driver Ready |

Additional HID-based LCD devices can be supported by implementing the `ILcdDevice` interface.

## Quick Start

```bash
# List connected LCD devices
lcdpossible list

# Display system info
lcdpossible show basic-info

# Display CPU and GPU monitors
lcdpossible show cpu-usage-graphic,gpu-usage-graphic

# Run a screensaver
lcdpossible show starfield

# Start the service with default slideshow
lcdpossible serve
```

## Installation

### One-Line Install (Recommended)

Install LCDPossible with all dependencies using a single command:

**Windows** (PowerShell as Administrator):
```powershell
irm https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/install-windows.ps1 | iex
```

**Ubuntu/Debian**:
```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/install-ubuntu.sh | bash
```

**Fedora/RHEL**:
```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/install-fedora.sh | bash
```

**Arch Linux**:
```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/install-arch.sh | bash
```

**macOS**:
```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/install-macos.sh | bash
```

**Proxmox VE** (run as root):
```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/install-proxmox.sh | bash
```

These scripts will:
- Install all required dependencies (LibVLC, fonts)
- Download the latest release
- Set up USB device permissions (Linux)
- Install and enable the service (systemd/launchd/Windows Service)

### Manual Installation

<details>
<summary>Windows (Manual)</summary>

1. Download the latest `lcdpossible-x.x.x-win-x64.zip` from [Releases](https://github.com/DevPossible/LCDPossible/releases)
2. Extract to a folder (e.g., `C:\Program Files\LCDPossible`)
3. Run `LCDPossible.exe` from the command line or add to PATH

**Install as Windows Service:**
```powershell
# Run as Administrator
sc.exe create LCDPossible binPath= "C:\Program Files\LCDPossible\LCDPossible.exe serve --service" start= auto
sc.exe start LCDPossible
```
</details>

<details>
<summary>Linux (Manual)</summary>

```bash
# Install dependencies (Ubuntu/Debian)
sudo apt install vlc libvlc-dev fonts-dejavu-core

# Download and extract
wget https://github.com/DevPossible/LCDPossible/releases/latest/download/lcdpossible-x.x.x-linux-x64.tar.gz
sudo mkdir -p /opt/lcdpossible
sudo tar -xzf lcdpossible-*.tar.gz -C /opt/lcdpossible

# Install udev rules for USB access
sudo tee /etc/udev/rules.d/99-lcdpossible.rules << 'EOF'
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="5302", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0416", MODE="0666", TAG+="uaccess"
EOF
sudo udevadm control --reload-rules && sudo udevadm trigger

# Add to PATH
echo 'export PATH="/opt/lcdpossible:$PATH"' >> ~/.bashrc
source ~/.bashrc
```
</details>

<details>
<summary>macOS (Manual)</summary>

```bash
# Install dependencies
brew install vlc

# Download and extract
curl -LO https://github.com/DevPossible/LCDPossible/releases/latest/download/lcdpossible-x.x.x-osx-x64.tar.gz
mkdir -p ~/.local/share/lcdpossible
tar -xzf lcdpossible-*.tar.gz -C ~/.local/share/lcdpossible

# Add to PATH
echo 'export PATH="$HOME/.local/share/lcdpossible:$PATH"' >> ~/.zshrc
source ~/.zshrc
```
</details>

### Build from Source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/DevPossible/LCDPossible.git
cd LCDPossible

# Build
./build.ps1
# or: dotnet build src/LCDPossible.sln

# Run tests
./test-full.ps1

# Package for distribution
./package.ps1 -Version "1.0.0"
```

## Uninstall

### One-Line Uninstall

Remove LCDPossible using a single command:

**Windows** (PowerShell as Administrator):
```powershell
irm https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/uninstall-windows.ps1 | iex
```

**Ubuntu/Debian/Proxmox**:
```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/uninstall-ubuntu.sh | sudo bash
```

**Fedora/RHEL**:
```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/uninstall-fedora.sh | sudo bash
```

**Arch Linux**:
```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/uninstall-arch.sh | sudo bash
```

**macOS**:
```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/uninstall-macos.sh | bash
```

These scripts will:
- Stop and remove the service (systemd/launchd/Windows Service)
- Remove the symlink or PATH entry
- Remove installed files from the installation directory
- Preserve configuration files by default

### Remove Configuration Files

To also remove configuration files, use the `--remove-config` flag:

```bash
# Linux/macOS
curl -sSL https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/uninstall-ubuntu.sh | sudo bash -s -- --remove-config

# Windows
irm https://raw.githubusercontent.com/DevPossible/LCDPossible/main/scripts/uninstall-windows.ps1 -OutFile uninstall.ps1; .\uninstall.ps1 -RemoveConfig
```

### Local Uninstall Script

If you have the repository cloned, you can use the local scripts:

```bash
# Windows
.\scripts\uninstall-windows.ps1
.\scripts\uninstall-windows.ps1 -RemoveConfig  # Also remove config

# Linux/macOS
sudo ./scripts/uninstall-ubuntu.sh
sudo ./scripts/uninstall-ubuntu.sh --remove-config
```

### Remote Uninstall via SSH

Use `uninstall-local.ps1` to uninstall from a remote Linux/macOS host:

```powershell
# Basic uninstall
.\scripts\uninstall-local.ps1 -TargetHost myserver.local

# Specify distro and remove config
.\scripts\uninstall-local.ps1 -TargetHost 192.168.1.100 -Distro proxmox -RemoveConfig

# macOS target
.\scripts\uninstall-local.ps1 -TargetHost mymac -User admin -Distro macos
```

Available distros: `ubuntu`, `debian`, `proxmox`, `fedora`, `arch`, `macos`

## CLI Commands

### Device Management

```bash
lcdpossible list                    # List connected LCD devices
lcdpossible test-pattern            # Display test pattern on all devices
lcdpossible test-pattern -d 0       # Display test pattern on device 0
lcdpossible set-brightness 80       # Set brightness to 80%
lcdpossible set-image -p image.jpg  # Display a static image
```

### Panel Display

```bash
# Show single panel
lcdpossible show cpu-info

# Show multiple panels (slideshow)
lcdpossible show cpu-info,gpu-info,ram-info

# Custom duration per panel (seconds)
lcdpossible show cpu-info|@duration=30

# Custom update interval (seconds)
lcdpossible show cpu-usage-graphic|@interval=2

# Use wildcards
lcdpossible show cpu-*              # All CPU panels
lcdpossible show *-graphic          # All graphic panels
lcdpossible show *                  # ALL panels
```

### Profile Management

```bash
lcdpossible profile new myprofile                    # Create profile
lcdpossible profile list                             # List all profiles
lcdpossible profile show myprofile                   # Show profile details
lcdpossible profile append-panel cpu-usage-graphic   # Add panel to default profile
lcdpossible profile append-panel gpu-info -p myprofile -d 15  # Add to specific profile
lcdpossible profile remove-panel 0                   # Remove panel at index 0
lcdpossible profile move-panel 0 2                   # Move panel from index 0 to 2
lcdpossible profile set-defaults --interval 5        # Set default update interval
lcdpossible profile delete myprofile                 # Delete profile
```

### Service Control

```bash
lcdpossible serve                   # Start service (foreground)
lcdpossible serve --service         # Run as Windows Service
lcdpossible stop                    # Stop the service
lcdpossible status                  # Show service status
```

### Testing & Debugging

```bash
lcdpossible test                    # Render default panels to files
lcdpossible test cpu-*              # Render matching panels to files
lcdpossible test "*"                # Render ALL panels to files
lcdpossible list-panels             # List all available panel types
lcdpossible help-panel cpu-info     # Show help for specific panel
lcdpossible sensors                 # List hardware sensors (Windows)
lcdpossible debug                   # Run diagnostics
```

## Available Panels

### System Monitoring

| Panel | Description |
|-------|-------------|
| `basic-info` | Hostname, OS, uptime |
| `basic-usage-text` | CPU/RAM/GPU usage as text |
| `cpu-info` | CPU model and specifications |
| `cpu-usage-text` | CPU usage as text |
| `cpu-usage-graphic` | CPU usage with visual bar |
| `cpu-thermal-graphic` | CPU temperature gauge |
| `gpu-info` | GPU model and specifications |
| `gpu-usage-text` | GPU usage as text |
| `gpu-usage-graphic` | GPU usage with visual bar |
| `gpu-thermal-graphic` | GPU temperature gauge |
| `ram-info` | RAM specifications |
| `ram-usage-text` | RAM usage as text |
| `ram-usage-graphic` | RAM usage with visual bar |
| `network-info` | Network interface information |
| `system-thermal-graphic` | Combined thermal overview |

### Screensavers

| Panel | Description |
|-------|-------------|
| `starfield` | Classic 3D starfield |
| `matrix-rain` | Matrix-style falling characters |
| `bouncing-logo` | DVD-style bouncing logo |
| `mystify` | Windows Mystify lines |
| `plasma` | Colorful plasma effect |
| `fire` | Animated fire simulation |
| `game-of-life` | Conway's Game of Life |
| `bubbles` | Floating bubbles |
| `rain` | Rainfall animation |
| `spiral` | Hypnotic spiral |
| `clock` | Digital clock display |
| `noise` | TV static noise |
| `warp-tunnel` | Space warp tunnel |
| `pipes` | 3D pipes screensaver |
| `asteroids` | Asteroids game demo |
| `missile-command` | Missile Command demo |
| `falling-blocks` | Tetris-style blocks |
| `screensaver` | Random screensaver |

### Media Panels

| Panel | Description |
|-------|-------------|
| `animated-gif:<path\|url>` | Animated GIF playback |
| `image-sequence:<folder>` | Image folder as animation |
| `video:<path\|url>` | Video file, URL, or YouTube |
| `html:<path>` | Render local HTML file |
| `web:<url>` | Render live website |

### Proxmox Integration

| Panel | Description |
|-------|-------------|
| `proxmox-summary` | Cluster overview with node status |
| `proxmox-vms` | VM and container list |

## Media Panel Examples

```bash
# Animated GIF
lcdpossible show animated-gif:https://upload.wikimedia.org/wikipedia/commons/2/2c/Rotating_earth_%28large%29.gif

# Video from URL (CC-BY Big Buck Bunny)
lcdpossible show video:https://archive.org/download/BigBuckBunny_124/Content/big_buck_bunny_720p_surround.mp4

# YouTube video
lcdpossible show video:https://www.youtube.com/watch?v=aqz-KE-bpKQ

# Live weather display
lcdpossible show web:https://wttr.in/London

# Local HTML dashboard
lcdpossible show html:/path/to/dashboard.html
```

## Configuration

### YAML Display Profiles

Profiles are stored in platform-specific locations:
- **Windows:** `%APPDATA%\LCDPossible\`
- **Linux:** `~/.config/LCDPossible/`
- **macOS:** `~/Library/Application Support/LCDPossible/`

Example profile (`profile.yaml`):

```yaml
name: "My Display Profile"
description: "System monitoring slideshow"

# Defaults for all slides
default_duration: 15
default_update_interval: 5
default_transition: crossfade
default_transition_duration: 800

# Color scheme
colors:
  background: "#0F0F19"
  text_primary: "#FFFFFF"
  accent: "#0096FF"
  usage_low: "#32C864"
  usage_medium: "#0096FF"
  usage_high: "#FFB400"
  usage_critical: "#FF3232"

# Slideshow panels
slides:
  - panel: basic-info
    duration: 10

  - panel: cpu-usage-graphic
    duration: 15
    update_interval: 2

  - panel: gpu-usage-graphic
    duration: 15
    transition: slide-left

  - panel: ram-usage-graphic
    duration: 10

  - panel: starfield
    duration: 30
```

### Available Transitions

| Transition | Description |
|------------|-------------|
| `none` | Instant switch |
| `fade` | Fade from black |
| `crossfade` | Dissolve between panels |
| `slide-left/right/up/down` | Directional slide |
| `wipe-left/right/up/down` | Wipe effect |
| `zoom-in/out` | Scale transition |
| `push-left/right` | Push old frame out |
| `random` | Random selection (default) |

### Environment Variables

| Variable | Description |
|----------|-------------|
| `LCDPOSSIBLE_DATA_DIR` | Override user data directory |

## Proxmox VE Integration

LCDPossible can display real-time metrics from your Proxmox VE cluster.

### Creating an API Token

1. Log into Proxmox web interface
2. Navigate to **Datacenter** > **Permissions** > **API Tokens**
3. Click **Add** and create a token (e.g., `monitor@pve!lcdpossible`)
4. Copy the token secret (shown only once)

### Required Permissions

```bash
pveum aclmod / -user monitor@pve -role PVEAuditor
```

### Configuration

Add to `appsettings.json`:

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
    }
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `Enabled` | `false` | Enable Proxmox integration |
| `ApiUrl` | `""` | Proxmox API URL (e.g., `https://proxmox.local:8006`) |
| `TokenId` | `""` | API token ID (format: `user@realm!tokenid`) |
| `TokenSecret` | `""` | API token secret |
| `IgnoreSslErrors` | `false` | Skip SSL verification (for self-signed certs) |
| `PollingIntervalSeconds` | `5` | How often to fetch metrics |
| `ShowVms` | `true` | Show VM status in panels |
| `ShowContainers` | `true` | Show container status in panels |
| `ShowAlerts` | `true` | Show cluster alerts |
| `MaxDisplayItems` | `10` | Max items per category |

## Linux USB Permissions

Create `/etc/udev/rules.d/99-lcdpossible.rules`:

```
# Thermalright LCD devices
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="5302", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0416", MODE="0666", TAG+="uaccess"
```

Then reload:

```bash
sudo udevadm control --reload-rules
sudo udevadm trigger
```

## Project Structure

```
LCDPossible/
├── .github/                           # CI/CD workflows
├── docs/                              # Documentation
│   ├── LCD-Technical-Reference.md     # USB HID protocol details
│   ├── Implementation-Plan.md         # Architecture documentation
│   └── devices/                       # Per-device specifications
├── scripts/                           # Build and deployment scripts
├── src/
│   ├── LCDPossible.sln                # Solution file
│   ├── LCDPossible/                   # Main executable (CLI + service)
│   ├── LCDPossible.Core/              # Core library
│   ├── LCDPossible.Sdk/               # Plugin SDK
│   └── LCDPossible.Plugins.*/         # Built-in plugins
├── tests/
│   ├── LCDPossible.Core.Tests/        # Unit tests
│   └── LCDPossible.FunctionalTests/   # Functional tests
├── build.ps1                          # Build script
├── package.ps1                        # Package for distribution
├── start-app.ps1                      # Run service
├── test-smoke.ps1                     # Quick tests
└── test-full.ps1                      # Full test suite
```

## Technology Stack

| Package | Purpose |
|---------|---------|
| [HidSharp](https://github.com/IntergatedCircuits/HidSharp) | Cross-platform USB HID |
| [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) | Image processing |
| [LibVLCSharp](https://github.com/videolan/libvlcsharp) | Video playback |
| [PuppeteerSharp](https://github.com/hardkoded/puppeteer-sharp) | Headless browser |
| [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) | YouTube stream extraction |
| [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | Hardware monitoring (Windows) |
| [Microsoft.Extensions.Hosting](https://docs.microsoft.com/en-us/dotnet/core/extensions/generic-host) | Service hosting |

## Contributing

Contributions are welcome! Please read the [Implementation Plan](docs/Implementation-Plan.md) for architecture details.

### Commit Message Format

We use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add new panel type           # Minor version bump
fix: correct USB timeout handling  # Patch version bump
feat!: redesign device driver API  # Major version bump (breaking)
docs: update installation guide    # No version bump
```

### Adding a New Device Driver

1. Create a driver class implementing `ILcdDevice` in `src/LCDPossible.Core/Devices/Drivers/{Manufacturer}/`
2. Register the driver in the driver registry
3. Add device documentation to `docs/devices/{VID-PID}/`
4. Add tests

### Adding a New Panel Plugin

1. Create a new project implementing `IPanelPlugin`
2. Reference `LCDPossible.Sdk`
3. Implement panel types with `IDisplayPanel`
4. Build to the `plugins/` directory

## License

MIT License - see [LICENSE](LICENSE) file.

## Acknowledgments

- [thermalright-lcd-control](https://github.com/rejeb/thermalright-lcd-control) - Python GUI for Thermalright LCDs
- [trlcd_libusb](https://github.com/NoNameOnFile/trlcd_libusb) - C implementation with libusb
- [digital_thermal_right_lcd](https://github.com/MathieuxHugo/digital_thermal_right_lcd) - Python reference for PA120
- [HidSharp](https://github.com/IntergatedCircuits/HidSharp) - Cross-platform HID library
