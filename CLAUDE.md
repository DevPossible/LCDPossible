# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**LCDPossible** is a cross-platform .NET 10 LCD controller service for HID-based LCD screens such as the Thermalright Trofeo Vision 360 ARGB (1280x480 LCD). The project uses a plugin-based driver architecture to support multiple devices and aims to be an open-source alternative to vendor-specific Windows-only software.

**Current State:** Phase 1-2 complete. Core infrastructure and Trofeo Vision LCD driver implemented and verified working.

## Key Documentation

- `docs/LCD-Technical-Reference.md` - USB HID protocol details, packet structures, reverse-engineered from TRCC.exe
- `docs/Implementation-Plan.md` - Complete architecture and phase-by-phase implementation plan

## Project Structure

```
LCDPossible/
├── .build/                        # Build outputs (gitignored, hidden)
├── .github/workflows/             # GitHub Actions CI/CD
├── docs/                          # Documentation
├── scripts/                       # Deployment & setup scripts
│   └── publish.ps1                # Cross-platform publish helper
├── src/
│   ├── LCDPossible.sln            # Solution file
│   ├── LCDPossible.Core/          # Core library (net10.0)
│   │   ├── Devices/               # Device abstraction & drivers
│   │   ├── Rendering/             # Image encoding (JPEG, RGB565)
│   │   └── Usb/                   # USB HID layer (HidSharp)
│   └── LCDPossible/               # Main executable - service + CLI (net10.0)
│       ├── Cli/                   # CLI commands (debug, etc.)
│       ├── Monitoring/            # Hardware monitoring providers
│       ├── Panels/                # Display panel implementations
│       └── Rendering/             # System info rendering
├── tests/
│   └── LCDPossible.Core.Tests/    # Unit tests (20 tests passing)
├── build.ps1                      # Build script (auto-installs tools)
├── package.ps1                    # Package for distribution
├── start-app.ps1                  # Run service
├── test-smoke.ps1                 # Run unit tests
├── test-full.ps1                  # Run all tests
└── Directory.Build.props          # Centralized build output to /.build/
```

## USB Device Specifications (Initial Devices)

| Device | VID | PID | Packet Size | Notes |
|--------|-----|-----|-------------|-------|
| Thermalright Trofeo Vision | 0x0416 | 0x5302 | 512 bytes | 1280x480 LCD |
| Thermalright PA120 Digital | 0x0416 | 0x8001 | 64 bytes | Segment display |
| Thermalright Secondary | 0x0418 | 0x5303 | 64 bytes | |
| Thermalright Extended LCD | 0x0418 | 0x5304 | 512 bytes | |

> Additional HID-based LCD devices can be supported by implementing `ILcdDevice`. Drivers are organized by manufacturer under `Devices/Drivers/{Manufacturer}/`.

## Protocol Quick Reference

**HID Report structure (513 bytes per packet):**
```
[Report ID 0x00] [Data: up to 512 bytes]
```

**First packet data - Protocol header (20 bytes) + JPEG data:**
```
DA DB DC DD 02 00 00 00 [width LE 2B] [height LE 2B] 02 00 00 00 [length LE 4B] [JPEG data...]
```

- **IMPORTANT:** Each HID packet MUST include Report ID 0x00 as first byte
- Header magic: `0xDA 0xDB 0xDC 0xDD`
- Command 0x02 = image data
- Compression 0x02 = JPEG (preferred)
- Resolution: 1280x480 (width=0x0500, height=0x01E0)

## Technology Stack

| Package | Purpose |
|---------|---------|
| HidSharp | USB HID communication |
| SixLabors.ImageSharp | Image processing |
| Microsoft.Extensions.Hosting | Service hosting |
| LibreHardwareMonitorLib | Windows hardware monitoring |

## Build & Run Commands

```bash
# Build all projects (using root script)
./build.ps1

# Or with dotnet directly
dotnet build src/LCDPossible.sln

# Run tests
./test-full.ps1                    # All tests
./test-smoke.ps1                   # Unit tests only
dotnet test src/LCDPossible.sln    # Direct

# Run service (foreground)
./start-app.ps1
dotnet run --project src/LCDPossible/LCDPossible.csproj -- serve

# Package for distribution
./package.ps1 -Version "1.0.0" -SkipTests  # Skip tests for quick packaging

# CLI Commands
dotnet run --project src/LCDPossible/LCDPossible.csproj -- list           # List devices
dotnet run --project src/LCDPossible/LCDPossible.csproj -- test           # Display test pattern
dotnet run --project src/LCDPossible/LCDPossible.csproj -- set-image -p image.jpg  # Display image
dotnet run --project src/LCDPossible/LCDPossible.csproj -- serve          # Start service
dotnet run --project src/LCDPossible/LCDPossible.csproj -- --help         # Show all commands

# Publish using helper script (outputs to .build/publish/)
./scripts/publish.ps1 -Runtime linux-x64
./scripts/publish.ps1 -Runtime win-x64
```

## Executable Commands

The `LCDPossible` executable handles both service and CLI modes:

| Command | Description |
|---------|-------------|
| `serve` or `run` | Start the LCD service (foreground) |
| `serve --service` | Run as Windows Service |
| `list` | List connected LCD devices |
| `test` | Display a test pattern |
| `set-image -p <file>` | Send an image to the LCD |
| `profile` | Show current display profile |
| `generate-profile` | Generate sample YAML profile |
| `--help` | Show all available commands |

## Implementation Phases

1. ✅ **Core Infrastructure** - USB HID layer with HidSharp, device abstraction interfaces
2. ✅ **Device Support** - TrofeoVisionDriver (0x0416:0x5302) working, PA120DigitalDriver stub
3. ⏳ **Display Engine** - Basic JPEG encoding done, frame sources pending
4. ⏳ **System Monitoring** - Platform-specific metrics (CPU/GPU temp, usage)
5. ⏳ **Configuration & UI** - CLI tool working, JSON config pending
6. ⏳ **Platform Integration** - systemd (Linux), Windows Service, launchd (macOS)

## Key Interfaces to Implement

```csharp
// Core device abstraction
public interface ILcdDevice : IDisposable
{
    DeviceInfo Info { get; }
    LcdCapabilities Capabilities { get; }
    Task SendFrameAsync(ReadOnlyMemory<byte> frameData, CancellationToken ct);
}

// Render source abstraction
public interface IRenderSource
{
    bool IsAnimated { get; }
    TimeSpan FrameDuration { get; }
    Task<Image<Rgba32>> GetFrameAsync(CancellationToken ct);
}
```

## Platform-Specific Notes

**Linux:** Requires udev rules for unprivileged USB access (add rules per supported vendor):
```
# Thermalright devices
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="5302", MODE="0666"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0416", MODE="0666"
```

**Windows:** Use `Microsoft.Extensions.Hosting.WindowsServices` for service registration.

## Reference Projects

- [digital_thermal_right_lcd](https://github.com/MathieuxHugo/digital_thermal_right_lcd) - Python, PA120 Digital
- [Peerless_assassin_and_CLI_UI](https://github.com/raffa0001/Peerless_assassin_and_CLI_UI) - Python
