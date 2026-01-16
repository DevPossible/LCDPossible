# LCDPossible - .NET 10 Implementation Plan

> **Project Name:** `LCDPossible`
> **Target Framework:** .NET 10
> **Platforms:** Windows, Linux, macOS
> **Architecture:** Cross-platform daemon/service with plugin-based device support

---

> **Note:** This document was the original implementation plan. The project has evolved significantly and now includes:
> - **Plugin-based panel architecture** with 6 plugin types (Core, Images, Proxmox, Screensavers, Video, Web)
> - **40+ display panels** including system monitoring, screensavers, and media panels
> - **Page effects system** with 30+ animated effects
> - **Themes system** with multiple color schemes
> - **YAML-based display profiles**
> - **Modular CLI** with commands for panels, profiles, config, and service management
>
> For the current architecture and usage, see [CLAUDE.md](../CLAUDE.md) in the project root.

---

## Table of Contents

1. [Project Goals](#project-goals)
2. [Architecture Overview](#architecture-overview)
3. [Project Structure](#project-structure)
4. [Phase 1: Core Infrastructure](#phase-1-core-infrastructure)
5. [Phase 2: Device Support](#phase-2-device-support)
6. [Phase 3: Display Engine](#phase-3-display-engine)
7. [Phase 4: System Monitoring](#phase-4-system-monitoring)
8. [Phase 5: Configuration & UI](#phase-5-configuration--ui)
9. [Phase 6: Platform Integration](#phase-6-platform-integration)
10. [Technology Stack](#technology-stack)
11. [Testing Strategy](#testing-strategy)
12. [Deployment](#deployment)
13. [Future Enhancements](#future-enhancements)

---

## Project Goals

### Primary Goals

1. **Cross-Platform:** Run as a background service on Windows, Linux, and macOS
2. **Multi-Device Support:** Plugin architecture supporting multiple HID-based LCD controllers from various manufacturers
3. **Open Source:** Community-driven development and contributions
4. **Extensible:** Easy to add drivers for new devices via the plugin architecture

### Supported Devices (Initial)

| Device | VID | PID | Display | Priority |
|--------|-----|-----|---------|----------|
| Thermalright Trofeo Vision 360 | 0x0416 | 0x5302 | 1280×480 LCD | P0 (Primary) |
| Thermalright PA120 Digital | 0x0416 | 0x8001 | Segment display | P1 |
| Other Thermalright | 0x0418 | 0x5303/5304 | Various | P2 |

> **Note:** The architecture supports any HID-based LCD device. Additional manufacturers (NZXT, Deepcool, etc.) can be added by implementing the `ILcdDevice` interface.

### Non-Goals (v1.0)

- LED/ARGB control (separate project or future phase)
- Fan/pump speed control (use existing tools like liquidctl)
- Theme marketplace/cloud features

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         LCDPossible                             │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │
│  │   Web UI    │  │  CLI Tool   │  │  Tray App   │  Interfaces  │
│  │  (Blazor)   │  │             │  │  (Optional) │              │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘              │
│         └────────────────┼────────────────┘                     │
│                          ▼                                      │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                    Core Service                            │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │  │
│  │  │  Display    │  │   System    │  │   Config    │        │  │
│  │  │  Engine     │  │   Monitor   │  │   Manager   │        │  │
│  │  └──────┬──────┘  └──────┬──────┘  └─────────────┘        │  │
│  │         │                │                                 │  │
│  │         ▼                ▼                                 │  │
│  │  ┌───────────────────────────────────────────────────────┐│  │
│  │  │              Render Pipeline                          ││  │
│  │  │  [Theme/Image] → [Overlay] → [Transform] → [Encode]   ││  │
│  │  └───────────────────────────────────────────────────────┘│  │
│  └───────────────────────────────────────────────────────────┘  │
│                          │                                      │
│                          ▼                                      │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                  Device Abstraction Layer                  │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │  │
│  │  │ TrofeoVision │  │  PA120Digital│  │  GenericLCD  │     │  │
│  │  │   Driver     │  │    Driver    │  │    Driver    │     │  │
│  │  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘     │  │
│  └─────────┼─────────────────┼─────────────────┼─────────────┘  │
│            └─────────────────┼─────────────────┘                │
│                              ▼                                  │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                    USB HID Layer                           │  │
│  │                    (HidSharp)                              │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
LCDPossible/
├── .github/
│   └── workflows/
│       ├── build.yml
│       └── release.yml
│
├── docs/
│   ├── LCD-Technical-Reference.md
│   └── Implementation-Plan.md
│
├── samples/
│   ├── themes/                          # Sample themes/images
│   └── scripts/                         # Installation scripts
│
├── scripts/                             # Build/deploy scripts
│
├── src/
│   ├── LCDPossible.sln                  # Solution file
│   │
│   ├── LCDPossible.Core/                # Core library (net10.0)
│   │   ├── Devices/
│   │   │   ├── ILcdDevice.cs            # Device interface
│   │   │   ├── DeviceInfo.cs            # Device metadata
│   │   │   ├── DeviceManager.cs         # Discovery & lifecycle
│   │   │   └── Drivers/
│   │   │       └── Thermalright/
│   │   │           ├── TrofeoVisionDriver.cs
│   │   │           └── PA120DigitalDriver.cs
│   │   ├── Rendering/
│   │   │   ├── IRenderSource.cs         # Content source interface
│   │   │   ├── RenderPipeline.cs        # Frame processing
│   │   │   ├── ImageEncoder.cs          # JPEG/RGB565 encoding
│   │   │   └── Sources/
│   │   │       ├── StaticImageSource.cs
│   │   │       ├── GifAnimationSource.cs
│   │   │       ├── SystemInfoSource.cs
│   │   │       └── ScreenCaptureSource.cs
│   │   ├── Monitoring/
│   │   │   ├── ISystemMonitor.cs
│   │   │   ├── SystemMetrics.cs
│   │   │   └── Providers/
│   │   │       ├── WindowsMonitor.cs
│   │   │       ├── LinuxMonitor.cs
│   │   │       └── MacOSMonitor.cs
│   │   ├── Configuration/
│   │   │   ├── AppConfig.cs
│   │   │   ├── DeviceProfile.cs
│   │   │   └── ThemeConfig.cs
│   │   └── Usb/
│   │       ├── HidDeviceWrapper.cs
│   │       └── UsbProtocol.cs
│   │
│   ├── LCDPossible.Service/             # Background service (net10.0)
│   │   ├── Program.cs
│   │   ├── Worker.cs                    # Main service loop
│   │   ├── appsettings.json
│   │   └── Properties/
│   │       └── launchSettings.json
│   │
│   ├── LCDPossible.Cli/                 # Command-line tool (net10.0)
│   │   ├── Program.cs
│   │   └── Commands/
│   │       ├── ListDevicesCommand.cs
│   │       ├── SetImageCommand.cs
│   │       ├── SetModeCommand.cs
│   │       └── StatusCommand.cs
│   │
│   └── LCDPossible.Web/                 # Web UI (Blazor, optional)
│       ├── Program.cs
│       ├── Components/
│       └── wwwroot/
│
├── tests/
│   ├── LCDPossible.Core.Tests/
│   └── LCDPossible.Integration.Tests/
│
├── build.ps1                            # Build script
├── start.ps1                            # Run service
├── test-smoke.ps1                       # Run unit tests
├── test-full.ps1                        # Run all tests
├── Directory.Build.props                # Centralized build output config
├── README.md
└── LICENSE
```

---

## Phase 1: Core Infrastructure

### 1.1 Project Setup

- [ ] Create solution structure
- [ ] Configure multi-targeting (net10.0, netstandard2.1 for Core)
- [ ] Set up GitHub repository
- [ ] Configure CI/CD (GitHub Actions)
- [ ] Add NuGet package references

### 1.2 USB HID Layer

**Goal:** Abstract USB HID communication across platforms

```csharp
public interface IHidDevice : IDisposable
{
    string DevicePath { get; }
    ushort VendorId { get; }
    ushort ProductId { get; }
    string Manufacturer { get; }
    string ProductName { get; }

    bool IsOpen { get; }
    void Open();
    void Close();

    void Write(ReadOnlySpan<byte> data);
    int Read(Span<byte> buffer, int timeout = 1000);

    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    Task<int> ReadAsync(Memory<byte> buffer, int timeout = 1000, CancellationToken ct = default);
}

public interface IDeviceEnumerator
{
    IEnumerable<HidDeviceInfo> EnumerateDevices();
    IEnumerable<HidDeviceInfo> EnumerateDevices(ushort vendorId);
    IEnumerable<HidDeviceInfo> EnumerateDevices(ushort vendorId, ushort productId);

    event EventHandler<DeviceEventArgs>? DeviceArrived;
    event EventHandler<DeviceEventArgs>? DeviceRemoved;
}
```

**Dependencies:**
- `HidSharp` (MIT license, cross-platform)

### 1.3 Device Abstraction Layer

**Goal:** Plugin-style driver architecture

```csharp
public interface ILcdDevice : IDisposable
{
    DeviceInfo Info { get; }
    LcdCapabilities Capabilities { get; }

    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);

    Task SendFrameAsync(ReadOnlyMemory<byte> frameData, CancellationToken ct = default);
    Task SetBrightnessAsync(byte brightness, CancellationToken ct = default);
    Task SetOrientationAsync(Orientation orientation, CancellationToken ct = default);
}

public record DeviceInfo(
    ushort VendorId,
    ushort ProductId,
    string Name,
    string Manufacturer,
    string DriverName
);

public record LcdCapabilities(
    int Width,
    int Height,
    ColorFormat[] SupportedFormats,
    int MaxPacketSize,
    int MaxFrameRate
);

public enum ColorFormat
{
    Rgb565,
    Rgb888,
    Jpeg
}

public enum Orientation
{
    Landscape0 = 0,
    Portrait90 = 90,
    Landscape180 = 180,
    Portrait270 = 270
}
```

### 1.4 Configuration System

**Goal:** JSON-based configuration with hot reload

```csharp
public class AppConfig
{
    public GeneralSettings General { get; set; } = new();
    public Dictionary<string, DeviceProfile> DeviceProfiles { get; set; } = new();
}

public class GeneralSettings
{
    public int TargetFrameRate { get; set; } = 30;
    public bool AutoStart { get; set; } = true;
    public string ThemesDirectory { get; set; } = "themes";
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
}

public class DeviceProfile
{
    public string? DeviceId { get; set; }
    public string Mode { get; set; } = "static";
    public string? ImagePath { get; set; }
    public byte Brightness { get; set; } = 100;
    public Orientation Orientation { get; set; } = Orientation.Landscape0;
    public SystemInfoSettings? SystemInfo { get; set; }
}
```

---

## Phase 2: Device Support

### 2.1 Trofeo Vision Driver (Priority 0)

**Target:** VID 0x0416, PID 0x5302

```csharp
public class TrofeoVisionDriver : ILcdDevice
{
    private const ushort VendorId = 0x0416;
    private const ushort ProductId = 0x5302;
    private const int PacketSize = 512;

    private static readonly byte[] Header = { 0xDA, 0xDB, 0xDC, 0xDD };

    public LcdCapabilities Capabilities => new(
        Width: 1280,
        Height: 480,
        SupportedFormats: [ColorFormat.Jpeg, ColorFormat.Rgb565],
        MaxPacketSize: 512,
        MaxFrameRate: 60
    );

    public async Task SendFrameAsync(ReadOnlyMemory<byte> jpegData, CancellationToken ct)
    {
        var packet = BuildImagePacket(jpegData);
        await SendPacketsAsync(packet, ct);
    }

    private byte[] BuildImagePacket(ReadOnlyMemory<byte> imageData)
    {
        // Implementation per technical reference
        var header = new byte[20];
        Header.CopyTo(header, 0);
        header[4] = 0x02;  // Image command
        // ... width, height, length encoding

        return [..header, ..imageData.Span];
    }
}
```

### 2.2 PA120 Digital Driver (Priority 1)

**Target:** VID 0x0416, PID 0x8001

Reference existing project: [digital_thermal_right_lcd](https://github.com/MathieuxHugo/digital_thermal_right_lcd)

```csharp
public class PA120DigitalDriver : ILcdDevice
{
    private const ushort VendorId = 0x0416;
    private const ushort ProductId = 0x8001;
    private const int PacketSize = 64;

    public LcdCapabilities Capabilities => new(
        Width: 0,   // Segment display, not pixel-based
        Height: 0,
        SupportedFormats: [],  // Uses custom data format
        MaxPacketSize: 64,
        MaxFrameRate: 10
    );

    // Simpler protocol for digit displays
    public async Task SendTemperatureAsync(int cpuTemp, int gpuTemp, CancellationToken ct)
    {
        // Implementation based on digital_thermal_right_lcd
    }
}
```

### 2.3 Device Discovery & Registration

```csharp
public class DeviceManager : IDisposable
{
    private readonly IDeviceEnumerator _enumerator;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, ILcdDevice> _devices = new();

    // Driver registry - add new drivers here
    private static readonly Dictionary<(ushort Vid, ushort Pid), Type> DriverRegistry = new()
    {
        [(0x0416, 0x5302)] = typeof(TrofeoVisionDriver),
        [(0x0416, 0x8001)] = typeof(PA120DigitalDriver),
        [(0x0418, 0x5303)] = typeof(GenericLcdDriver),
        [(0x0418, 0x5304)] = typeof(GenericLcdDriver),
    };

    public IEnumerable<ILcdDevice> DiscoverDevices()
    {
        foreach (var hidDevice in _enumerator.EnumerateDevices(0x0416))
        {
            if (DriverRegistry.TryGetValue((hidDevice.VendorId, hidDevice.ProductId), out var driverType))
            {
                var driver = (ILcdDevice)ActivatorUtilities.CreateInstance(_serviceProvider, driverType, hidDevice);
                yield return driver;
            }
        }
    }
}
```

---

## Phase 3: Display Engine

### 3.1 Render Pipeline

```csharp
public class RenderPipeline
{
    private readonly IImageEncoder _encoder;
    private readonly ILcdDevice _device;

    public async Task RenderFrameAsync(IRenderSource source, CancellationToken ct)
    {
        // 1. Get source image
        using var image = await source.GetFrameAsync(ct);

        // 2. Apply transformations (rotation, scaling)
        using var transformed = Transform(image, _device.Capabilities);

        // 3. Apply overlays (system info, time, etc.)
        using var composited = ApplyOverlays(transformed);

        // 4. Encode for device
        var encoded = _encoder.Encode(composited, _device.Capabilities);

        // 5. Send to device
        await _device.SendFrameAsync(encoded, ct);
    }
}
```

### 3.2 Render Sources

```csharp
public interface IRenderSource
{
    bool IsAnimated { get; }
    TimeSpan FrameDuration { get; }

    Task<Image<Rgba32>> GetFrameAsync(CancellationToken ct = default);
}

// Implementations:
public class StaticImageSource : IRenderSource { }
public class GifAnimationSource : IRenderSource { }
public class SystemInfoSource : IRenderSource { }
public class ScreenCaptureSource : IRenderSource { }
public class ClockSource : IRenderSource { }
```

### 3.3 Image Encoding

```csharp
public interface IImageEncoder
{
    ReadOnlyMemory<byte> Encode(Image<Rgba32> image, LcdCapabilities caps);
}

public class JpegEncoder : IImageEncoder
{
    public int Quality { get; set; } = 95;

    public ReadOnlyMemory<byte> Encode(Image<Rgba32> image, LcdCapabilities caps)
    {
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms, new JpegEncoder { Quality = Quality });
        return ms.ToArray();
    }
}

public class Rgb565Encoder : IImageEncoder
{
    public ReadOnlyMemory<byte> Encode(Image<Rgba32> image, LcdCapabilities caps)
    {
        var buffer = new byte[caps.Width * caps.Height * 2];
        image.ProcessPixelRows(accessor =>
        {
            int offset = 0;
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                foreach (var pixel in row)
                {
                    // RGB565 encoding (little-endian)
                    ushort rgb565 = (ushort)(
                        ((pixel.R & 0xF8) << 8) |
                        ((pixel.G & 0xFC) << 3) |
                        (pixel.B >> 3)
                    );
                    buffer[offset++] = (byte)(rgb565 & 0xFF);
                    buffer[offset++] = (byte)(rgb565 >> 8);
                }
            }
        });
        return buffer;
    }
}
```

---

## Phase 4: System Monitoring

### 4.1 Platform-Agnostic Interface

```csharp
public interface ISystemMonitor
{
    SystemMetrics GetCurrentMetrics();
    Task<SystemMetrics> GetCurrentMetricsAsync(CancellationToken ct = default);
}

public record SystemMetrics
{
    public CpuMetrics Cpu { get; init; } = new();
    public GpuMetrics? Gpu { get; init; }
    public MemoryMetrics Memory { get; init; } = new();
    public DiskMetrics[] Disks { get; init; } = [];
    public NetworkMetrics Network { get; init; } = new();
}

public record CpuMetrics
{
    public float Temperature { get; init; }
    public float UsagePercent { get; init; }
    public float FrequencyMHz { get; init; }
    public float PowerWatts { get; init; }
}

public record GpuMetrics
{
    public string Name { get; init; } = "";
    public float Temperature { get; init; }
    public float UsagePercent { get; init; }
    public float MemoryUsedMB { get; init; }
    public float MemoryTotalMB { get; init; }
    public float PowerWatts { get; init; }
}
```

### 4.2 Platform Implementations

```csharp
// Windows: Use LibreHardwareMonitor or WMI
public class WindowsSystemMonitor : ISystemMonitor
{
    // LibreHardwareMonitorLib NuGet package
    private readonly Computer _computer;
}

// Linux: Use /sys/class/hwmon, lm-sensors, nvidia-smi
public class LinuxSystemMonitor : ISystemMonitor
{
    // Parse /sys/class/hwmon/*/temp*_input
    // Parse /proc/stat for CPU usage
    // Use nvidia-smi for NVIDIA GPUs
}

// macOS: Use IOKit, powermetrics
public class MacOSSystemMonitor : ISystemMonitor
{
    // Use osx-cpu-temp or similar
    // Parse system_profiler output
}
```

---

## Phase 5: Configuration & UI

### 5.1 CLI Tool

```bash
# List connected devices
lcdpossible list

# Set static image
lcdpossible set-image --device 0 --path /path/to/image.png

# Set system info mode
lcdpossible set-mode --device 0 --mode sysinfo

# Set brightness
lcdpossible brightness --device 0 --value 80

# Show current status
lcdpossible status
```

### 5.2 Web UI (Optional, Blazor)

Simple web interface accessible at `http://localhost:5123`:

- Device list with status
- Mode selection (static, animation, sysinfo, clock)
- Image/theme upload
- Brightness/rotation controls
- System info configuration
- Live preview

### 5.3 Configuration File

`appsettings.json`:
```json
{
  "LCDPossible": {
    "General": {
      "TargetFrameRate": 30,
      "AutoStart": true,
      "ThemesDirectory": "themes",
      "WebUI": {
        "Enabled": true,
        "Port": 5123
      }
    },
    "Devices": {
      "default": {
        "Mode": "sysinfo",
        "Brightness": 100,
        "Orientation": 0,
        "SystemInfo": {
          "ShowCpuTemp": true,
          "ShowCpuUsage": true,
          "ShowGpuTemp": true,
          "ShowGpuUsage": true,
          "ShowRamUsage": true,
          "Theme": "default"
        }
      }
    }
  }
}
```

---

## Phase 6: Platform Integration

### 6.1 Linux

**systemd service:** `/etc/systemd/system/lcdpossible.service`
```ini
[Unit]
Description=LCDPossible LCD Controller Service
After=network.target

[Service]
Type=notify
ExecStart=/opt/lcdpossible/lcdpossible-service
Restart=always
RestartSec=5
User=lcdpossible

[Install]
WantedBy=multi-user.target
```

**udev rules:** `/etc/udev/rules.d/99-lcdpossible.rules`
```
# HID LCD devices (Thermalright)
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="5302", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="8001", MODE="0666", TAG+="uaccess"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0416", MODE="0666", TAG+="uaccess"
# Add additional vendor rules here as devices are supported
```

### 6.2 Windows

**Windows Service** via `Microsoft.Extensions.Hosting.WindowsServices`

```csharp
Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "LCDPossible";
    })
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    });
```

**Optional:** System tray application with NotifyIcon

### 6.3 macOS

**launchd plist:** `~/Library/LaunchAgents/com.lcdpossible.service.plist`
```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.lcdpossible.service</string>
    <key>ProgramArguments</key>
    <array>
        <string>/usr/local/bin/lcdpossible-service</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
</dict>
</plist>
```

---

## Technology Stack

### Core Dependencies

| Package | Purpose | License |
|---------|---------|---------|
| `HidSharp` | USB HID communication | MIT |
| `SixLabors.ImageSharp` | Image processing | Apache 2.0 |
| `Microsoft.Extensions.Hosting` | Service hosting | MIT |
| `Microsoft.Extensions.Configuration` | Configuration | MIT |
| `Serilog` | Logging | Apache 2.0 |
| `System.CommandLine` | CLI parsing | MIT |

### Optional Dependencies

| Package | Purpose | License |
|---------|---------|---------|
| `LibreHardwareMonitorLib` | Windows hardware monitoring | MPL 2.0 |
| `Blazor` | Web UI | MIT |

### Target Frameworks

```xml
<PropertyGroup>
  <!-- Core library - maximum compatibility -->
  <TargetFramework>netstandard2.1</TargetFramework>
</PropertyGroup>

<PropertyGroup>
  <!-- Service & CLI - latest features -->
  <TargetFramework>net10.0</TargetFramework>
</PropertyGroup>
```

---

## Testing Strategy

### Unit Tests

- Device protocol encoding/decoding
- Image encoding (RGB565, JPEG)
- Configuration parsing
- Render pipeline

### Integration Tests

- USB HID mock device communication
- Platform-specific system monitoring

### Manual Testing

- Real device testing on each platform
- Performance benchmarking (CPU usage, frame rate)

---

## Deployment

### Build Artifacts

```bash
# Self-contained single-file executables
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true

# Optional: Native AOT for smaller binaries
dotnet publish -c Release -r linux-x64 -p:PublishAot=true
```

### Distribution

- GitHub Releases with platform-specific binaries
- AUR package (Arch Linux)
- Homebrew formula (macOS)
- Windows installer (optional)

---

## Future Enhancements

### Version 1.1
- Additional device drivers (NZXT Kraken, Deepcool, etc.)
- LED/ARGB control support
- Custom theme editor
- Multiple simultaneous devices

### Version 1.2
- Plugin system for third-party render sources
- MQTT/Home Assistant integration
- REST API

### Version 2.0
- External plugin loading (dynamically load driver DLLs)
- Mobile app for configuration
- Cloud theme sharing

---

## Development Milestones

| Phase | Milestone | Est. Effort |
|-------|-----------|-------------|
| 1 | Core infrastructure, USB layer | Foundation |
| 2 | Trofeo Vision driver working | First device |
| 3 | Static image display | MVP |
| 4 | System info overlay | Key feature |
| 5 | CLI + basic config | Usable |
| 6 | Linux systemd integration | Platform 1 |
| 6 | Windows service | Platform 2 |
| 6 | macOS launchd | Platform 3 |
| 7 | Web UI | Nice-to-have |

---

## References

- [LCD Technical Reference](./LCD-Technical-Reference.md)
- [digital_thermal_right_lcd (Python reference)](https://github.com/MathieuxHugo/digital_thermal_right_lcd)
- [HidSharp documentation](https://github.com/IntergatedCircuits/HidSharp)
- [ImageSharp documentation](https://docs.sixlabors.com/articles/imagesharp/)
