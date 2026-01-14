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
| Scriban | Template engine for HTML panels |
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
| `test <panels>` | Render panels to JPEG files (no LCD required) |
| `test-pattern` | Display a test pattern on the LCD |
| `set-image -p <file>` | Send an image to the LCD |
| `profile` | Show current display profile |
| `generate-profile` | Generate sample YAML profile |
| `--help` | Show all available commands |

## Testing Panels with the `test` Verb

The `test` command renders panels to JPEG files without requiring an LCD device. Use this to verify panels render correctly with valid data before deploying.

**Key flags:**
- `--debug` - Show detailed output including full file paths and sizes
- `-r WxH` or `--resolution WxH` - Set target resolution (default: 1280x480)
- `-o <path>` or `--output <path>` - Output directory (default: user home)
- `-w <seconds>` or `--wait <seconds>` - Wait N seconds before capture (useful for HTML/widget panels that need browser init time, or animated panels)

**Examples:**

```bash
# Render a single panel
./start-app.ps1 test cpu-info --debug

# Render with debug output (shows full path and file size)
./start-app.ps1 test cpu-widget --debug
# Output: [DEBUG] Written: C:\Users\richa\cpu-widget.jpg (10211 bytes)

# Render at a different resolution
./start-app.ps1 test cpu-info -r 800x480

# Render all CPU panels using wildcard
./start-app.ps1 test cpu-* --debug

# Render ALL panels
./start-app.ps1 test * --debug

# Wait for HTML/widget panel to fully initialize (browser + components)
./start-app.ps1 test cpu-widget -w 2

# Render animated panel after letting it run for 3 seconds
./start-app.ps1 test animated-gif:demo.gif -w 3

# Save output to specific directory
./start-app.ps1 test cpu-info -o ./test-output
```

**When to use the test verb:**
1. After implementing a new panel - verify it renders without errors
2. After modifying panel layouts - check visual appearance
3. Before deploying to LCD - confirm panels show correct data
4. Debugging rendering issues - use `--debug` to see detailed output

## Available Panel Types

For complete panel documentation with screenshots, see `docs/panels/README.md`.

### System Monitoring Panels

| Panel Type | Description |
|------------|-------------|
| `basic-info` | Hostname, OS, uptime summary |
| `basic-usage-text` | Basic system usage as text |
| `cpu-info` | CPU model and specifications |
| `cpu-status` | CPU dashboard with usage bar, temperature, sparkline |
| `cpu-usage-text` | CPU usage as text |
| `cpu-usage-graphic` | CPU usage with visual bars |
| `cpu-thermal-graphic` | CPU temperature gauge |
| `gpu-info` | GPU model and specifications |
| `gpu-usage-text` | GPU usage as text |
| `gpu-usage-graphic` | GPU usage with visual bars |
| `gpu-thermal-graphic` | GPU temperature gauge |
| `ram-info` | RAM specifications |
| `ram-usage-text` | RAM usage as text |
| `ram-usage-graphic` | RAM usage with visual bars |
| `system-thermal-graphic` | Combined CPU/GPU temperature display |
| `network-info` | Network interfaces (smart layout, 1-4 widgets) |

### Proxmox Panels

| Panel Type | Description |
|------------|-------------|
| `proxmox-summary` | Proxmox cluster overview |
| `proxmox-vms` | Proxmox VM/Container list |

### Media Panels

| Panel Type | Description |
|------------|-------------|
| `animated-gif:<path\|url>` | Animated GIF from file or URL |
| `image-sequence:<folder>` | Folder of numbered images as animation |
| `video:<path\|url>` | Video file, URL, or YouTube link |
| `html:<path>` | Local HTML file rendered as web page |
| `web:<url>` | Live website rendered from URL |

### Screensaver Panels

| Panel Type | Description |
|------------|-------------|
| `screensaver` | Random screensaver or cycle through all |
| `clock` | Analog clock with smooth second hand |
| `plasma` | Classic demoscene plasma effect |
| `matrix-rain` | Digital rain effect (The Matrix) |
| `starfield` | Classic starfield warp effect |
| `warp-tunnel` | Flying through colorful warp tunnel |
| `fire` | Classic demoscene fire effect |
| `pipes` | 3D pipes growing in random directions |
| `mystify` | Bouncing connected polygons with trails |
| `bubbles` | Floating translucent bubbles |
| `rain` | Falling raindrops with splash effects |
| `noise` | TV static / white noise effect |
| `spiral` | Hypnotic rotating spiral pattern |
| `game-of-life` | Conway's cellular automaton |
| `asteroids` | Asteroids game simulation |
| `falling-blocks` | Tetris-style falling blocks |
| `missile-command` | Defend cities from missiles |
| `bouncing-logo` | DVD screensaver style bouncing text |

## Available Themes

For complete theme documentation, see `docs/themes/README.md`.

| Theme | Category | Description |
|-------|----------|-------------|
| `cyberpunk` | Gamer | Neon cyan/magenta, glow effects (default) |
| `rgb-gaming` | Gamer | Vibrant rainbow, bold colors |
| `executive` | Corporate | Dark blue/gold, professional |
| `clean` | Corporate | Light mode, minimal |

### Theme Usage

```bash
# Set default theme
lcdpossible config set-theme cyberpunk

# List available themes
lcdpossible config list-themes

# Per-panel theme override
lcdpossible show cpu-info|@theme=executive
```

## Available Page Effects

For complete effects documentation, see `docs/effects/README.md`.

Page effects are animated overlays applied to panels. Apply with `|@effect=<name>`.

### Background Effects

| Effect | Description |
|--------|-------------|
| `scanlines` | CRT/retro scanline overlay |
| `matrix-rain` | Digital rain falling behind widgets |
| `particle-field` | Floating particles in the background |
| `grid-pulse` | Grid lines pulse outward from center |
| `fireworks` | Colorful fireworks exploding |
| `aurora` | Northern lights with flowing color ribbons |
| `snow` | Gentle snowflakes drifting down |
| `rain` | Rain drops falling with splash effects |
| `bubbles` | Translucent bubbles floating upward |
| `fireflies` | Glowing particles drifting randomly |
| `stars-twinkle` | Stationary twinkling starfield |
| `lava-lamp` | Blobby colored blobs floating |
| `bokeh` | Out-of-focus light circles drifting |
| `smoke` | Wispy smoke tendrils rising |
| `waves` | Ocean waves flowing at bottom |
| `confetti` | Colorful confetti falling |
| `lightning` | Occasional lightning flashes |
| `clouds` | Slow-moving clouds drifting |
| `embers` | Glowing embers floating upward |
| `breathing-glow` | Pulsing ambient glow around edges |

### Overlay Effects

| Effect | Description |
|--------|-------------|
| `vhs-static` | VHS tape noise/tracking lines |
| `film-grain` | Old film grain texture overlay |
| `lens-flare` | Moving lens flare effect |
| `neon-border` | Glowing pulse around widget edges |
| `chromatic-aberration` | RGB split/shift effect |
| `crt-warp` | CRT screen edge warping |

### Effect Usage

```bash
# Apply effect to panel
lcdpossible show cpu-status|@effect=matrix-rain

# Combine with theme
lcdpossible show cpu-status|@effect=scanlines|@theme=cyberpunk

# Random effect
lcdpossible show cpu-status|@effect=random
```

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

### Panel Base Class Hierarchy

```
IDisplayPanel (interface)
    │
    └── BasePanel (abstract - color scheme, lifecycle)
            │
            ├── HtmlPanel (abstract - Scriban templates + PuppeteerSharp)
            │       │
            │       └── WidgetPanel (abstract - 12-column grid + web components)
            │               │
            │               └── CpuWidgetPanel, NetworkWidgetPanel, etc.
            │
            └── CanvasPanel (abstract - ImageSharp direct drawing)
                    │
                    └── PlasmaPanel, MatrixRainPanel, etc. (screensavers)
```

### Panel Base Class Decision

| Content Type | Base Class | Examples |
|--------------|------------|----------|
| System info with widgets | `WidgetPanel` | cpu-widget, ram-widget, network-widget |
| Custom HTML layout | `HtmlPanel` | custom dashboards, web-based panels |
| Screensaver/effects | `CanvasPanel` | plasma, matrix, starfield, warp-tunnel |
| Media/animation | Plugin via `IPanelPlugin` | animated-gif, video, web |

### WidgetPanel (Recommended for Info Panels)

Use `WidgetPanel` for panels displaying system information. It provides:
- **12-column CSS grid** for flexible layouts
- **Responsive web components** that scale across display resolutions
- **Automatic refresh** with configurable interval
- **Color scheme injection** as CSS variables

```csharp
public sealed class CpuWidgetPanel : WidgetPanel
{
    public override string PanelId => "cpu-widget";
    public override string DisplayName => "CPU Info (Widget)";

    // Provide data for the panel
    protected override async Task<object> GetPanelDataAsync(CancellationToken ct)
    {
        var metrics = await _provider.GetMetricsAsync(ct);
        return new {
            name = metrics?.Cpu?.Name ?? "Unknown",
            usage = metrics?.Cpu?.UsagePercent ?? 0,
            temperature = metrics?.Cpu?.TemperatureCelsius
        };
    }

    // Define widgets using the data
    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        dynamic data = panelData;

        yield return new WidgetDefinition("lcd-stat-card", 6, 1, new {
            title = "CPU",
            value = data.name,
            size = "small"
        });

        yield return new WidgetDefinition("lcd-usage-bar", 6, 1, new {
            value = data.usage,
            label = "Usage",
            showPercent = true
        });

        if (data.temperature != null)
        {
            yield return new WidgetDefinition("lcd-temp-gauge", 4, 2, new {
                value = data.temperature,
                label = "Temp"
            });
        }
    }
}
```

**Available Web Components:**

| Component | Purpose | Key Props |
|-----------|---------|-----------|
| `lcd-stat-card` | Value with title/unit | title, value, unit, status, size |
| `lcd-usage-bar` | Progress bar | value, max, label, orientation, showPercent |
| `lcd-temp-gauge` | Temperature donut | value, max, label |
| `lcd-donut` | Circular percentage | value, max, label, color |
| `lcd-info-list` | Label/value pairs | items: [{label, value, color}] |
| `lcd-sparkline` | Mini line chart | values, label, color |
| `lcd-status-dot` | Status indicator | status, label |

**Grid Layout:**
- 12 columns, 4 rows by default
- `ColSpan`: 1-12 (widget width)
- `RowSpan`: 1-4 (widget height)
- Helper methods: `WidgetDefinition.FullWidth()`, `.HalfWidth()`, `.ThirdWidth()`, `.QuarterWidth()`

### CanvasPanel (Screensavers/Custom Drawing)

Use `CanvasPanel` for panels requiring direct pixel manipulation (screensavers, visualizers):

```csharp
public sealed class PlasmaPanel : CanvasPanel
{
    public override string PanelId => "plasma";
    public override string DisplayName => "Plasma Effect";
    public override bool IsAnimated => true;

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken ct)
    {
        UpdateTiming(); // Updates ElapsedSeconds, DeltaSeconds
        var image = CreateBaseImage(width, height);

        image.Mutate(ctx => {
            // Direct pixel drawing using ImageSharp
            // Use ElapsedSeconds for animation timing
        });

        return Task.FromResult(image);
    }
}
```

**Available utilities in CanvasPanel:**
- `CreateBaseImage()` - Create image with background color
- `DrawText()`, `DrawCenteredText()` - Text rendering
- `DrawProgressBar()`, `DrawVerticalBar()` - Bar charts
- `GetUsageColor()`, `GetTemperatureColor()` - Status colors
- `ElapsedSeconds`, `DeltaSeconds` - Animation timing

### HtmlPanel (Custom HTML Templates)

Use `HtmlPanel` for custom HTML layouts with Scriban templating:

```csharp
public sealed class CustomDashboard : HtmlPanel
{
    protected override string TemplatePath => "templates/dashboard.html";
    // Or use TemplateContent for inline templates
    // Or use TemplateUrl for remote pages

    protected override async Task<object> GetDataModelAsync(CancellationToken ct)
    {
        return new { title = "Dashboard", items = await GetItemsAsync(ct) };
    }
}
```

Template variables available:
- `{{ data }}` - Your data model
- `{{ assets_path }}` - Path to html_assets folder
- `{{ colors }}` - Color scheme object
- `{{ colors_css }}` - CSS variables for color scheme

### Panel Registration

Register new panels in the plugin's `PanelTypes` dictionary and `CreatePanel` method:

```csharp
// In CorePlugin.cs PanelTypes
["cpu-widget"] = new PanelTypeInfo {
    TypeId = "cpu-widget",
    DisplayName = "CPU Info (Widget)",
    Category = "System",
    IsLive = true
}

// In CorePlugin.cs CreatePanel
"cpu-widget" => new CpuWidgetPanel(provider),
```

### Legacy Base Classes (Deprecated)

| Class | Status | Replacement |
|-------|--------|-------------|
| `BaseLivePanel` | Deprecated | Use `CanvasPanel` for drawing, `WidgetPanel` for info |
| `SmartLayoutPanel<T>` | Deprecated | Use `WidgetPanel` with `GetItemsAsync()` + `DefineItemWidget()` |

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
