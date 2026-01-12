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

> **Full Documentation:** See [docs/panels/README.md](docs/panels/README.md) for detailed panel documentation with screenshots.

### CPU

| Panel | Description |
|-------|-------------|
| [`cpu-info`](docs/core/panels/cpu-info/cpu-info.md) | Detailed CPU information including model, usage, temperature, frequency, and power |
| [`cpu-usage-graphic`](docs/core/panels/cpu-usage-graphic/cpu-usage-graphic.md) | CPU usage with graphical bars including per-core breakdown |
| [`cpu-usage-text`](docs/core/panels/cpu-usage-text/cpu-usage-text.md) | CPU usage displayed as large text percentage |

### GPU

| Panel | Description |
|-------|-------------|
| [`gpu-info`](docs/core/panels/gpu-info/gpu-info.md) | GPU information including model, usage, temperature, and VRAM |
| [`gpu-usage-graphic`](docs/core/panels/gpu-usage-graphic/gpu-usage-graphic.md) | GPU usage with graphical bars |
| [`gpu-usage-text`](docs/core/panels/gpu-usage-text/gpu-usage-text.md) | GPU usage displayed as large text |

### Media

| Panel | Description |
|-------|-------------|
| [`animated-gif:`](docs/images/panels/animated-gif/animated-gif.md) | Plays animated GIF files or URLs with full animation support |
| [`image-sequence:`](docs/images/panels/image-sequence/image-sequence.md) | Plays a sequence of numbered images from a folder at 30fps |
| [`video:`](docs/video/panels/video/video.md) | Plays video files, streaming URLs, or YouTube links |

### Memory

| Panel | Description |
|-------|-------------|
| [`ram-info`](docs/core/panels/ram-info/ram-info.md) | Memory information including total, used, and available |
| [`ram-usage-graphic`](docs/core/panels/ram-usage-graphic/ram-usage-graphic.md) | RAM usage with graphical bar |
| [`ram-usage-text`](docs/core/panels/ram-usage-text/ram-usage-text.md) | RAM usage displayed as large text |

### Network

| Panel | Description |
|-------|-------------|
| [`network-info`](docs/core/panels/network-info/network-info.md) | Network configuration including hostname, IP addresses, gateway, and DNS |

### Proxmox

| Panel | Description |
|-------|-------------|
| [`proxmox-summary`](docs/proxmox/panels/proxmox-summary/proxmox-summary.md) | Proxmox cluster overview with node status and resource usage |
| [`proxmox-vms`](docs/proxmox/panels/proxmox-vms/proxmox-vms.md) | List of VMs and containers with status and resource usage |

### Screensaver

| Panel | Description |
|-------|-------------|
| [`clock`](docs/screensavers/panels/clock/clock.md) | Analog clock with smooth second hand |
| [`asteroids`](docs/screensavers/panels/asteroids/asteroids.md) | Asteroids game simulation with vector graphics |
| [`bouncing-logo:`](docs/screensavers/panels/bouncing-logo/bouncing-logo.md) | Customizable text bouncing off screen edges (DVD screensaver style) with color, size, 3D, and rotation options |
| [`bubbles`](docs/screensavers/panels/bubbles/bubbles.md) | Floating, bouncing translucent bubbles |
| [`falling-blocks:`](docs/screensavers/panels/falling-blocks/falling-blocks.md) | Tetris-style falling blocks simulator with AI gameplay |
| [`fire`](docs/screensavers/panels/fire/fire.md) | Classic demoscene fire effect with palette animation |
| [`game-of-life`](docs/screensavers/panels/game-of-life/game-of-life.md) | Conway's cellular automaton with colorful patterns |
| [`matrix-rain`](docs/screensavers/panels/matrix-rain/matrix-rain.md) | Digital rain effect inspired by The Matrix |
| [`missile-command`](docs/screensavers/panels/missile-command/missile-command.md) | Defend cities from incoming missiles |
| [`mystify`](docs/screensavers/panels/mystify/mystify.md) | Bouncing connected polygons with color trails |
| [`pipes`](docs/screensavers/panels/pipes/pipes.md) | 3D pipes growing in random directions (classic Windows) |
| [`plasma`](docs/screensavers/panels/plasma/plasma.md) | Classic demoscene plasma effect |
| [`rain`](docs/screensavers/panels/rain/rain.md) | Falling raindrops with splash effects |
| [`screensaver:`](docs/screensavers/panels/screensaver/screensaver.md) | Plays a random screensaver effect or cycles through all |
| [`spiral`](docs/screensavers/panels/spiral/spiral.md) | Hypnotic rotating spiral pattern |
| [`starfield`](docs/screensavers/panels/starfield/starfield.md) | Classic starfield warp effect with stars streaming from center |
| [`noise`](docs/screensavers/panels/noise/noise.md) | TV static / white noise effect |
| [`warp-tunnel`](docs/screensavers/panels/warp-tunnel/warp-tunnel.md) | Flying through a colorful warp tunnel |

### System

| Panel | Description |
|-------|-------------|
| [`basic-info`](docs/core/panels/basic-info/basic-info.md) | Basic system information including hostname, OS, and uptime |
| [`basic-usage-text`](docs/core/panels/basic-usage-text/basic-usage-text.md) | Simple CPU/RAM/GPU usage summary |

### Thermal

| Panel | Description |
|-------|-------------|
| [`cpu-thermal-graphic`](docs/core/panels/cpu-thermal-graphic/cpu-thermal-graphic.md) | CPU temperature with graphical gauge display |
| [`gpu-thermal-graphic`](docs/core/panels/gpu-thermal-graphic/gpu-thermal-graphic.md) | GPU temperature with graphical gauge display |
| [`system-thermal-graphic`](docs/core/panels/system-thermal-graphic/system-thermal-graphic.md) | Combined CPU and GPU temperature display with vertical thermometers |

### Web

| Panel | Description |
|-------|-------------|
| [`html:`](docs/web/panels/html/html.md) | Renders a local HTML file using headless browser |
| [`web:`](docs/web/panels/web/web.md) | Renders a live website from URL |

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
