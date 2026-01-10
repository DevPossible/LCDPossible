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
lcdpossible show --layout "CPU:{cpu.temp}|GPU:{gpu.temp}"

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

## Technology Stack

- **HidSharp** - Cross-platform USB HID communication
- **SixLabors.ImageSharp** - Image processing
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
