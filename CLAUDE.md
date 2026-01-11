# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**LCDPossible** is a cross-platform .NET 10 LCD controller service for HID-based LCD screens such as the Thermalright Trofeo Vision 360 ARGB (1280x480 LCD). The project uses a plugin-based driver architecture to support multiple devices and aims to be an open-source alternative to vendor-specific Windows-only software.

**Current State:** Phase 1-2 complete. Core infrastructure and Trofeo Vision LCD driver implemented and verified working.

## Key Documentation

- `docs/LCD-Technical-Reference.md` - USB HID protocol details, packet structures, reverse-engineered from TRCC.exe
- `docs/Implementation-Plan.md` - Complete architecture and phase-by-phase implementation plan
- `docs/devices/{VID-PID}/{DeviceName}.md` - Per-device technical specifications and protocol details

### Device-Specific Documentation

When implementing code for a specific device, check the corresponding device documentation:

| Device | Documentation |
|--------|---------------|
| Trofeo Vision LCD | `docs/devices/0416-5302/Thermalright-Trofeo-Vision.md` |

Device docs contain: verified commands, packet formats, known limitations, and protocol research sources.

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
| SixLabors.ImageSharp | Image processing & GIF frame extraction |
| LibVLCSharp | Video playback (local, URL, YouTube) |
| VideoLAN.LibVLC.Windows | LibVLC native binaries (Windows only, see Platform Notes) |
| YoutubeExplode | YouTube stream URL extraction |
| PuppeteerSharp | Headless browser for HTML/Web panels |
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

## Available Panel Types

| Panel Type | Description |
|------------|-------------|
| `cpu-info` | CPU model and specifications |
| `cpu-usage-text` | CPU usage as text |
| `cpu-usage-graphic` | CPU usage with visual bars |
| `ram-info` | RAM specifications |
| `ram-usage-text` | RAM usage as text |
| `ram-usage-graphic` | RAM usage with visual bars |
| `gpu-info` | GPU model and specifications |
| `gpu-usage-text` | GPU usage as text |
| `gpu-usage-graphic` | GPU usage with visual bars |
| `basic-info` | Hostname, OS, uptime summary |
| `basic-usage-text` | Basic system usage as text |
| `network-info` | Network interfaces (smart layout, 1-4 widgets) |
| `proxmox-summary` | Proxmox cluster overview |
| `proxmox-vms` | Proxmox VM/Container list |
| `animated-gif:<path\|url>` | Animated GIF from file or URL |
| `image-sequence:<folder>` | Folder of numbered images as animation |
| `video:<path\|url>` | Video file, URL, or YouTube link |
| `html:<path>` | Local HTML file rendered as web page |
| `web:<url>` | Live website rendered from URL |

### Media Panel Examples

```bash
# Animated GIF (CC-BY-SA)
dotnet run -- show animated-gif:https://upload.wikimedia.org/wikipedia/commons/2/2c/Rotating_earth_%28large%29.gif

# Video from Archive.org (CC-BY)
dotnet run -- show video:https://archive.org/download/BigBuckBunny_124/Content/big_buck_bunny_720p_surround.mp4

# YouTube video (CC-BY)
dotnet run -- show video:https://www.youtube.com/watch?v=aqz-KE-bpKQ

# Live website
dotnet run -- show web:https://wttr.in/London
```

## Implementation Phases

1. ✅ **Core Infrastructure** - USB HID layer with HidSharp, device abstraction interfaces
2. ✅ **Device Support** - TrofeoVisionDriver (0x0416:0x5302) working, PA120DigitalDriver stub
3. ✅ **Display Engine** - JPEG encoding, animated GIF, video, web panels complete
4. ✅ **System Monitoring** - CPU/GPU/RAM panels with LibreHardwareMonitor, Proxmox integration
5. ✅ **Configuration & UI** - CLI tool complete, YAML profile support
6. ⏳ **Platform Integration** - Windows Service done, systemd (Linux), launchd (macOS) pending

## Key Interfaces

```csharp
// Core device abstraction
public interface ILcdDevice : IDisposable
{
    DeviceInfo Info { get; }
    LcdCapabilities Capabilities { get; }
    Task SendFrameAsync(ReadOnlyMemory<byte> frameData, CancellationToken ct);
}

// Display panel abstraction
public interface IDisplayPanel : IDisposable
{
    string PanelId { get; }
    string DisplayName { get; }
    bool IsLive { get; }      // True if panel shows real-time data
    bool IsAnimated { get; }  // True if panel has its own frame timing
    Task InitializeAsync(CancellationToken ct);
    Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken ct);
}
```

## Creating New Panels

When implementing new display panels, choose the appropriate base class based on the panel's content type.

### Panel Base Class Decision

| Content Type | Base Class | Examples |
|--------------|------------|----------|
| Single fixed layout | `BaseLivePanel` | cpu-info, ram-usage-text, gpu-usage-graphic |
| Variable item count (0-N items) | `SmartLayoutPanel<T>` | network-info, storage-info, sensors-info |
| Media/animation | Plugin via `IPanelPlugin` | animated-gif, video, web |
| Screensaver/effects | Plugin via `IPanelPlugin` | plasma, matrix, starfield |

### SmartLayoutPanel (Variable Items)

Use `SmartLayoutPanel<T>` when the panel displays **0 or more items** that should adapt to available space. The layout system automatically:
- Determines optimal widget count (1-4) based on item count
- Scales fonts proportionally based on widget size
- Handles empty state (0 items) with customizable message
- Shows overflow indicator when items exceed 4

**Key features:**
- **Resolution-agnostic**: All calculations use percentages, not hardcoded pixels
- **Font scaling**: Based on widget height relative to 480px reference
- **Empty state**: Override `GetEmptyStateMessage()` to customize

```csharp
public sealed class NetworkInfoPanel : SmartLayoutPanel<NetworkInterfaceInfo>
{
    public override string PanelId => "network-info";
    public override string DisplayName => "Network Interfaces";

    // Return items to display (0 or more)
    protected override Task<IReadOnlyList<NetworkInterfaceInfo>> GetItemsAsync(CancellationToken ct)
        => _provider.GetNetworkInterfacesAsync(ct);

    // Render single item into widget area (fonts/bounds are pre-scaled)
    protected override void RenderWidget(
        IImageProcessingContext ctx,
        WidgetRenderContext widget,
        NetworkInterfaceInfo item)
    {
        DrawText(ctx, item.Name, widget.ContentX, widget.ContentY,
                 widget.Fonts.Title, widget.Colors.Accent);
        // ... render item details using widget.Fonts and widget.Bounds
    }

    // Optional: customize empty state message
    protected override string GetEmptyStateMessage() => "No network interfaces detected";
}
```

**Widget layout patterns:**

| Items | Layout | Font Scale |
|-------|--------|------------|
| 0 | Empty state message | N/A |
| 1 | Full panel (100%) | 1.0× |
| 2 | Side-by-side (50% each) | 0.85× |
| 3 | Left large + right stack | 0.85× / 0.7× |
| 4 | 2×2 grid (25% each) | 0.7× |
| 5+ | 3 widgets + "+N more" | 0.7× |

See `docs/Smart-Widget-Layout-Plan.md` for detailed implementation specifications.

### BaseLivePanel (Fixed Layout)

Use `BaseLivePanel` for panels with a **single fixed layout** that always displays the same structure regardless of content.

```csharp
public sealed class CpuInfoPanel : BaseLivePanel
{
    public override string PanelId => "cpu-info";
    public override string DisplayName => "CPU Info";

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken ct)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetMetricsAsync(ct);

        image.Mutate(ctx =>
        {
            // Fixed layout using TitleFont, ValueFont, LabelFont, SmallFont
            DrawText(ctx, "CPU", 20, 20, TitleFont!, AccentColor, width - 40);
            // ...
        });

        return image;
    }
}
```

### Panel Registration

Register new panels in `PanelFactory.cs`:
```csharp
{ "network-info", new PanelMetadata("Network Interfaces", "System", true, false) },
```

## Platform-Specific Notes

**Linux:** Requires udev rules for unprivileged USB access (add rules per supported vendor):
```bash
# Thermalright devices
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="5302", MODE="0666"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0416", MODE="0666"
```

**Linux/macOS - Video Panel Requirements:** LibVLC must be installed via system package manager:
```bash
# Linux (Debian/Ubuntu)
sudo apt install vlc libvlc-dev

# Linux (Fedora/RHEL)
sudo dnf install vlc vlc-devel

# Linux (Arch)
sudo pacman -S vlc

# macOS
brew install vlc
```

**Linux - Font Requirements:** Panels that render text require TrueType fonts:
```bash
# Linux (Debian/Ubuntu) - minimal font package
sudo apt install fonts-dejavu-core

# Linux (Fedora/RHEL)
sudo dnf install dejavu-sans-fonts

# Linux (Arch)
sudo pacman -S ttf-dejavu

# Docker/minimal environments - add to Dockerfile
RUN apt-get update && apt-get install -y fonts-dejavu-core
```

**Windows:**
- Use `Microsoft.Extensions.Hosting.WindowsServices` for service registration
- LibVLC native binaries are included automatically via NuGet

## Reference Projects

- [thermalright-lcd-control](https://github.com/rejeb/thermalright-lcd-control) - Python, GUI for multiple Thermalright LCD devices
- [trlcd_libusb](https://github.com/NoNameOnFile/trlcd_libusb) - C, libusb-based with APNG animation support
- [digital_thermal_right_lcd](https://github.com/MathieuxHugo/digital_thermal_right_lcd) - Python, PA120 Digital
- [Peerless_assassin_and_CLI_UI](https://github.com/raffa0001/Peerless_assassin_and_CLI_UI) - Python
