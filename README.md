# LCDPossible

Cross-platform .NET 10 LCD controller service for HID-based LCD screens.

[![CI](https://github.com/DevPossible/lcd-possible/actions/workflows/ci.yml/badge.svg)](https://github.com/DevPossible/lcd-possible/actions/workflows/ci.yml)
[![Release](https://github.com/DevPossible/lcd-possible/actions/workflows/release.yml/badge.svg)](https://github.com/DevPossible/lcd-possible/releases)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## Overview

LCDPossible is an open-source alternative to vendor-specific Windows-only software for controlling HID-based LCD displays found in AIO coolers and other PC components.

**Features:**
- Real-time system monitoring (CPU, GPU, RAM temperatures and usage)
- Media playback (animated GIFs, videos, YouTube, web pages)
- 16+ built-in screensavers
- Slideshows with smooth transitions
- Plugin architecture for custom panels
- Cross-platform (Windows, Linux, macOS)

**[Full Documentation](docs/README.md)** - Detailed guides, panel reference, configuration options

## Supported Devices

| Device | VID | PID | Resolution | Status |
|--------|-----|-----|------------|--------|
| Thermalright Trofeo Vision 360 ARGB | 0x0416 | 0x5302 | 1280x480 | Fully Supported |
| Thermalright PA120 Digital | 0x0416 | 0x8001 | Segment | Driver Ready |

Additional HID-based LCD devices can be supported via the plugin system.

## Quick Start

```bash
# List connected LCD devices
lcdpossible list

# Display system info
lcdpossible show basic-info

# Run a slideshow
lcdpossible show cpu-usage-graphic,gpu-usage-graphic,ram-usage-graphic

# Run a screensaver
lcdpossible show starfield

# Start the service
lcdpossible serve
```

See [CLI Reference](docs/cli/README.md) for all commands.

## Installation

### One-Line Install (Recommended)

**Windows** (PowerShell as Administrator):
```powershell
irm https://raw.githubusercontent.com/DevPossible/lcd-possible/main/scripts/install-windows.ps1 | iex
```

**Ubuntu/Debian**:
```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/lcd-possible/main/scripts/install-ubuntu.sh | bash
```

**Fedora/RHEL**:
```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/lcd-possible/main/scripts/install-fedora.sh | bash
```

**Arch Linux**:
```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/lcd-possible/main/scripts/install-arch.sh | bash
```

**macOS**:
```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/lcd-possible/main/scripts/install-macos.sh | bash
```

**Proxmox VE** (run as root):
```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/lcd-possible/main/scripts/install-proxmox.sh | bash
```

These scripts install dependencies, set up USB permissions (Linux), and configure the service.

### Manual Installation

<details>
<summary>Windows (Manual)</summary>

1. Download the latest `lcdpossible-x.x.x-win-x64.zip` from [Releases](https://github.com/DevPossible/lcd-possible/releases)
2. Extract to a folder (e.g., `C:\Program Files\LCDPossible`)
3. Run `LCDPossible.exe` from the command line or add to PATH

**Install as Windows Service:**
```powershell
lcdpossible service install
lcdpossible service start
```
</details>

<details>
<summary>Linux (Manual)</summary>

```bash
# Install dependencies (Ubuntu/Debian)
sudo apt install vlc libvlc-dev fonts-dejavu-core

# Download and extract
wget https://github.com/DevPossible/lcd-possible/releases/latest/download/lcdpossible-x.x.x-linux-x64.tar.gz
sudo mkdir -p /opt/lcdpossible
sudo tar -xzf lcdpossible-*.tar.gz -C /opt/lcdpossible
sudo ln -sf /opt/lcdpossible/lcdpossible /usr/local/bin/

# Install udev rules for USB access
sudo tee /etc/udev/rules.d/99-lcdpossible.rules << 'EOF'
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="5302", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0416", MODE="0666", TAG+="uaccess"
EOF
sudo udevadm control --reload-rules && sudo udevadm trigger
```
</details>

<details>
<summary>macOS (Manual)</summary>

```bash
# Install dependencies
brew install vlc

# Download and extract
curl -LO https://github.com/DevPossible/lcd-possible/releases/latest/download/lcdpossible-x.x.x-osx-x64.tar.gz
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
git clone https://github.com/DevPossible/lcd-possible.git
cd LCDPossible
./build.ps1
./test-full.ps1
```

## Uninstall

**Windows** (PowerShell as Administrator):
```powershell
irm https://raw.githubusercontent.com/DevPossible/lcd-possible/main/scripts/uninstall-windows.ps1 | iex
```

**Linux** (Ubuntu/Debian/Proxmox):
```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/lcd-possible/main/scripts/uninstall-ubuntu.sh | sudo bash
```

Add `--remove-config` (Linux/macOS) or `-RemoveConfig` (Windows) to also remove configuration files.

## Documentation

| Topic | Description |
|-------|-------------|
| [Getting Started](docs/getting-started.md) | First steps after installation |
| [Panels](docs/panels/README.md) | Available display panels with screenshots |
| [Effects](docs/effects/README.md) | Page effects and animations |
| [Themes](docs/themes/README.md) | Color themes |
| [Configuration](docs/configuration/README.md) | Profiles, settings, service setup |
| [CLI Reference](docs/cli/README.md) | Command-line interface |
| [Plugin Development](docs/plugins/README.md) | Creating custom panels |
| [Troubleshooting](docs/troubleshooting.md) | Common issues and solutions |

## Technology Stack

| Package | Purpose |
|---------|---------|
| [HidSharp](https://github.com/IntergatedCircuits/HidSharp) | Cross-platform USB HID |
| [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) | Image processing |
| [LibVLCSharp](https://github.com/videolan/libvlcsharp) | Video playback |
| [PuppeteerSharp](https://github.com/hardkoded/puppeteer-sharp) | Headless browser |
| [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | Hardware monitoring (Windows) |

## Contributing

Contributions are welcome! Please read the [Implementation Plan](docs/reference/Implementation-Plan.md) for architecture details.

We use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add new panel type           # Minor version bump
fix: correct USB timeout handling  # Patch version bump
feat!: redesign device driver API  # Major version bump (breaking)
docs: update installation guide    # No version bump
```

See [Plugin Development](docs/plugins/README.md) for creating custom panels and device drivers.

## License

MIT License - see [LICENSE](LICENSE) file.

## Acknowledgments

- [thermalright-lcd-control](https://github.com/rejeb/thermalright-lcd-control) - Python GUI for Thermalright LCDs
- [trlcd_libusb](https://github.com/NoNameOnFile/trlcd_libusb) - C implementation with libusb
- [digital_thermal_right_lcd](https://github.com/MathieuxHugo/digital_thermal_right_lcd) - Python reference for PA120
