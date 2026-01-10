# LCDPossible

Cross-platform .NET 10 LCD controller service for HID-based LCD screens.

[![Build](https://github.com/yourusername/LCDPossible/actions/workflows/build.yml/badge.svg)](https://github.com/yourusername/LCDPossible/actions)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## Overview

LCDPossible is an open-source alternative to vendor-specific Windows-only software for controlling HID-based LCD displays found in AIO coolers and other PC components.

### Supported Devices

| Device | VID | PID | Resolution | Status |
|--------|-----|-----|------------|--------|
| Thermalright Trofeo Vision 360 ARGB | 0x0416 | 0x5302 | 1280x480 | Supported |
| Thermalright PA120 Digital | 0x0416 | 0x8001 | Segment | Partial |

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows, Linux, or macOS

### Build

```bash
git clone https://github.com/yourusername/LCDPossible.git
cd LCDPossible

# Using build script (recommended)
./build.ps1

# Or with dotnet directly
dotnet build src/LCDPossible.sln
```

### Run Tests

```bash
# Run all tests
./test-full.ps1

# Run unit/smoke tests only
./test-smoke.ps1

# Or with dotnet directly
dotnet test src/LCDPossible.sln
```

### Run CLI Tool

```bash
# List connected devices
dotnet run --project src/LCDPossible.Cli/LCDPossible.Cli.csproj -- list

# Display test pattern
dotnet run --project src/LCDPossible.Cli/LCDPossible.Cli.csproj -- test

# Set an image
dotnet run --project src/LCDPossible.Cli/LCDPossible.Cli.csproj -- set-image --path wallpaper.jpg
```

### Run Service (Foreground/Debug Mode)

```bash
# Using start script
./start-app.ps1

# Or with dotnet directly
dotnet run --project src/LCDPossible.Service/LCDPossible.Service.csproj
```

Press `Ctrl+C` to stop.

## CLI Commands

```
lcdpossible <command> [options]

Commands:
  list                    List connected LCD devices
  status                  Show status of connected devices
  test [--device <n>]     Display a test pattern on the LCD
  set-image --path <file> [--device <n>]  Send an image to the LCD display

Options:
  --device, -d <n>        Device index (default: 0)
  --path, -p <file>       Path to image file
  --help, -h              Show help
  --version, -v           Show version
```

## Project Structure

```
LCDPossible/
├── .build/                        # Build outputs (gitignored, hidden)
├── .github/workflows/             # GitHub Actions CI/CD
├── docs/                          # Documentation
│   ├── LCD-Technical-Reference.md # Protocol documentation
│   └── Implementation-Plan.md     # Architecture & roadmap
├── scripts/                       # Deployment & setup scripts
│   └── publish.ps1                # Cross-platform publish helper
├── src/
│   ├── LCDPossible.sln            # Solution file
│   ├── LCDPossible.Core/          # Core library
│   │   ├── Devices/               # Device abstraction & drivers
│   │   ├── Rendering/             # Image encoding
│   │   └── Usb/                   # USB HID layer
│   ├── LCDPossible.Service/       # Background service
│   └── LCDPossible.Cli/           # CLI tool
├── tests/
│   └── LCDPossible.Core.Tests/    # Unit tests
├── build.ps1                      # Build script (auto-installs tools)
├── package.ps1                    # Package for distribution
├── start-app.ps1                  # Run service
├── test-smoke.ps1                 # Run unit tests
├── test-full.ps1                  # Run all tests
└── Directory.Build.props          # Centralized build output to /.build/
```

## Linux Setup

On Linux, create udev rules for unprivileged USB access:

```bash
# /etc/udev/rules.d/99-lcdpossible.rules
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="5302", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0416", MODE="0666", TAG+="uaccess"
```

Reload rules:
```bash
sudo udevadm control --reload-rules
sudo udevadm trigger
```

## Configuration

Edit `appsettings.json`:

```json
{
  "LCDPossible": {
    "General": {
      "TargetFrameRate": 30,
      "AutoStart": true,
      "ThemesDirectory": "themes"
    },
    "Devices": {
      "default": {
        "Mode": "static",
        "Brightness": 100,
        "Orientation": 0
      }
    }
  }
}
```

## Building Single-File Executables

```bash
# Using the publish script (recommended)
./scripts/publish.ps1 -Runtime win-x64 -Project Service
./scripts/publish.ps1 -Runtime linux-x64 -Project Service
./scripts/publish.ps1 -Runtime osx-x64 -Project Service

# Or with dotnet directly
dotnet publish src/LCDPossible.Service/LCDPossible.Service.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Build outputs are placed in the `.build/` directory (hidden, gitignored).

## Technology Stack

- **HidSharp** - Cross-platform USB HID communication
- **SixLabors.ImageSharp** - Image processing
- **Microsoft.Extensions.Hosting** - Service hosting
- **Serilog** - Structured logging

## Contributing

Contributions are welcome! Please read the [Implementation Plan](docs/Implementation-Plan.md) for architecture details and roadmap.

### Adding a New Device Driver

1. Create a new driver class implementing `ILcdDevice` in `src/LCDPossible.Core/Devices/Drivers/{Manufacturer}/`
2. Register the driver in `DriverRegistry.cs`
3. Add tests

## License

MIT License - see [LICENSE](LICENSE) file.

## Acknowledgments

- [digital_thermal_right_lcd](https://github.com/MathieuxHugo/digital_thermal_right_lcd) - Python reference implementation
- [HidSharp](https://github.com/IntergatedCircuits/HidSharp) - Cross-platform HID library
