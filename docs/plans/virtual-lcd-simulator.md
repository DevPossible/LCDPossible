# Virtual LCD Simulator - Implementation Plan

## Overview

A standalone Avalonia application that simulates LCD hardware by receiving raw HID packets over UDP, parsing device-specific protocols, and displaying the decoded frames. This enables development and testing without physical hardware.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              LCDPossible (Main App)                          │
│                                                                              │
│  Panel → JPEG → Driver → Protocol Header + Chunks → IHidDevice.WriteAsync() │
│                                                              ↓               │
│                                              ┌───────────────┴────────────┐  │
│                                              ↓                            ↓  │
│                                    HidSharpDevice              VirtualHidDevice
│                                    (real USB)                  (UDP sender)  │
└──────────────────────────────────────┼────────────────────────────┼─────────┘
                                       ↓                            ↓
                                  Physical LCD              UDP datagrams
                                                                    ↓
                              ┌─────────────────────────────────────┴──────────┐
                              │           VirtualLCD (Avalonia App)            │
                              │                                                │
                              │  UdpHidReceiver → ILcdProtocol → FrameDecoder  │
                              │                                      ↓         │
                              │                              LcdDisplayView    │
                              │                              (1280×480 window) │
                              └────────────────────────────────────────────────┘
```

## Project Structure

```
src/
├── LCDPossible.VirtualLcd/                    # NEW: Standalone Avalonia app
│   ├── LCDPossible.VirtualLcd.csproj
│   ├── Program.cs                             # Entry point + CLI parsing
│   ├── App.axaml                              # Avalonia app definition
│   ├── App.axaml.cs
│   │
│   ├── ViewModels/
│   │   └── MainViewModel.cs                   # Main window state
│   │
│   ├── Views/
│   │   ├── MainWindow.axaml                   # Main window layout
│   │   ├── MainWindow.axaml.cs
│   │   └── LcdDisplayControl.cs               # Custom control for frame rendering
│   │
│   ├── Network/
│   │   └── UdpHidReceiver.cs                  # UDP listener, receives HID packets
│   │
│   └── Protocols/
│       ├── ILcdProtocol.cs                    # Protocol abstraction interface
│       ├── LcdProtocolBase.cs                 # Shared protocol logic
│       ├── TrofeoVisionProtocol.cs            # 1280×480, DA DB DC DD header
│       ├── Pa120DigitalProtocol.cs            # Segment display (stub)
│       └── ProtocolRegistry.cs                # Driver discovery/registration
│
├── LCDPossible.Core/
│   └── Usb/
│       ├── IHidDevice.cs                      # EXISTS - no changes needed
│       ├── IDeviceEnumerator.cs               # EXISTS - no changes needed
│       ├── VirtualHidDevice.cs                # NEW: UDP sender implementing IHidDevice
│       └── VirtualDeviceEnumerator.cs         # NEW: Returns virtual devices
│
└── LCDPossible/
    └── Configuration/
        └── VirtualDeviceSettings.cs           # NEW: Config for virtual devices
```

## Interfaces

### ILcdProtocol (VirtualLCD side)

```csharp
namespace LCDPossible.VirtualLcd.Protocols;

/// <summary>
/// Defines how to parse HID packets for a specific LCD device.
/// </summary>
public interface ILcdProtocol
{
    /// <summary>
    /// Protocol identifier (e.g., "trofeo-vision", "pa120-digital").
    /// </summary>
    string ProtocolId { get; }

    /// <summary>
    /// Human-readable name for display.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Display width in pixels.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Display height in pixels.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Expected HID report size (e.g., 513 for Trofeo, 65 for PA120).
    /// </summary>
    int HidReportSize { get; }

    /// <summary>
    /// Process an incoming HID report. Returns a complete frame when available.
    /// </summary>
    /// <param name="hidReport">Raw HID report data.</param>
    /// <returns>Frame result indicating completion status and decoded data.</returns>
    FrameResult ProcessHidReport(ReadOnlySpan<byte> hidReport);

    /// <summary>
    /// Reset protocol state (e.g., on connection or error).
    /// </summary>
    void Reset();
}

/// <summary>
/// Result of processing an HID report.
/// </summary>
public readonly record struct FrameResult
{
    /// <summary>
    /// True when a complete frame has been assembled.
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// Decoded image data (JPEG or raw pixels). Only valid when IsComplete is true.
    /// </summary>
    public ReadOnlyMemory<byte>? ImageData { get; init; }

    /// <summary>
    /// Image format of the decoded data.
    /// </summary>
    public ImageFormat Format { get; init; }

    /// <summary>
    /// Error message if parsing failed.
    /// </summary>
    public string? Error { get; init; }

    public static FrameResult Incomplete() => new() { IsComplete = false };
    public static FrameResult Complete(byte[] data, ImageFormat format) =>
        new() { IsComplete = true, ImageData = data, Format = format };
    public static FrameResult Failed(string error) =>
        new() { IsComplete = false, Error = error };
}

public enum ImageFormat
{
    Jpeg,
    Rgb565,
    Rgb888
}
```

### VirtualHidDevice (Main app side)

```csharp
namespace LCDPossible.Core.Usb;

/// <summary>
/// IHidDevice implementation that sends HID reports over UDP to a VirtualLCD instance.
/// </summary>
public sealed class VirtualHidDevice : IHidDevice
{
    private readonly IPEndPoint _endpoint;
    private UdpClient? _client;
    private bool _isOpen;

    public VirtualHidDevice(
        string host,
        int port,
        ushort vendorId,
        ushort productId,
        int maxOutputReportLength = 513)
    {
        _endpoint = new IPEndPoint(IPAddress.Parse(host), port);
        VendorId = vendorId;
        ProductId = productId;
        MaxOutputReportLength = maxOutputReportLength;
        DevicePath = $"virtual://{host}:{port}";
    }

    public string DevicePath { get; }
    public ushort VendorId { get; }
    public ushort ProductId { get; }
    public string? Manufacturer => "Virtual";
    public string? ProductName => "Virtual LCD";
    public bool IsOpen => _isOpen;
    public int MaxOutputReportLength { get; }
    public int MaxInputReportLength => 0; // No input from virtual device

    public void Open()
    {
        if (_isOpen) throw new InvalidOperationException("Already open");
        _client = new UdpClient();
        _client.Connect(_endpoint);
        _isOpen = true;
    }

    public void Close()
    {
        _client?.Close();
        _client?.Dispose();
        _client = null;
        _isOpen = false;
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (!_isOpen || _client == null)
            throw new InvalidOperationException("Device not open");
        _client.Send(data);
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (!_isOpen || _client == null)
            throw new InvalidOperationException("Device not open");
        await _client.SendAsync(data, ct);
    }

    public int Read(Span<byte> buffer, int timeout = 1000) => 0; // No reads
    public Task<int> ReadAsync(Memory<byte> buffer, int timeout = 1000, CancellationToken ct = default)
        => Task.FromResult(0);

    public void Dispose() => Close();
}
```

## CLI Interface

### VirtualLCD

```
VirtualLCD - LCD Hardware Simulator
Simulates LCD hardware by receiving HID packets over UDP.

USAGE:
    VirtualLCD [OPTIONS]
    VirtualLCD --list-protocols

OPTIONS:
    -p, --protocol <name>     LCD protocol to simulate [default: trofeo-vision]
    -l, --listen <port>       UDP port to listen on [default: 5302]
    -b, --bind <address>      IP address to bind to [default: 0.0.0.0]

    --width <pixels>          Override display width (generic protocol only)
    --height <pixels>         Override display height (generic protocol only)

    --stats                   Show statistics overlay (FPS, bytes/sec)
    --always-on-top           Keep window above other windows
    --borderless              Hide window decorations
    --scale <factor>          Window scale factor [default: 1.0]

    --record <file>           Record HID traffic to file
    --replay <file>           Replay recorded HID traffic (ignores UDP)

    --list-protocols          List available protocols and exit
    -h, --help                Show this help
    -v, --version             Show version

PROTOCOLS:
    trofeo-vision    Thermalright Trofeo Vision 360 ARGB (1280×480)
    pa120-digital    Thermalright PA120 Digital (segment display)
    generic          Generic JPEG-over-HID (use --width/--height)

EXAMPLES:
    VirtualLCD                                    # Default: Trofeo Vision on port 5302
    VirtualLCD -p trofeo-vision -l 5302           # Explicit protocol and port
    VirtualLCD -p generic --width 800 --height 480 -l 5000
    VirtualLCD --stats --always-on-top            # With overlay, stays visible
    VirtualLCD --record session.hid              # Record for later replay
```

### LCDPossible (Main App) - New Options

```
# In appsettings.json or via CLI
{
  "VirtualDevices": [
    {
      "Enabled": true,
      "Protocol": "trofeo-vision",
      "Endpoint": "udp://127.0.0.1:5302",
      "Name": "Virtual Trofeo Vision"
    }
  ]
}

# CLI alternative
lcdpossible serve --virtual udp://127.0.0.1:5302
lcdpossible list --include-virtual
```

## Implementation Phases

### Phase 1: Core Protocol Infrastructure (Day 1)

**Goal:** Establish protocol abstraction and implement Trofeo Vision parsing.

**Files to create:**

1. `src/LCDPossible.VirtualLcd/LCDPossible.VirtualLcd.csproj`
   - Avalonia app, net10.0
   - References: Avalonia, ImageSharp, System.CommandLine

2. `src/LCDPossible.VirtualLcd/Protocols/ILcdProtocol.cs`
   - Interface definition (as shown above)
   - FrameResult record

3. `src/LCDPossible.VirtualLcd/Protocols/LcdProtocolBase.cs`
   - Base class with common buffer management
   - Packet accumulation logic

4. `src/LCDPossible.VirtualLcd/Protocols/TrofeoVisionProtocol.cs`
   - Parse DA DB DC DD header
   - Extract width, height, compression, length
   - Accumulate packets until complete
   - Return JPEG data

5. `src/LCDPossible.VirtualLcd/Protocols/ProtocolRegistry.cs`
   - Static registry of available protocols
   - Factory method: `GetProtocol(string name)`

**Validation:** Unit tests for protocol parsing using captured HID data.

### Phase 2: UDP Receiver & Display (Day 2)

**Goal:** Receive packets and display frames in Avalonia window.

**Files to create:**

1. `src/LCDPossible.VirtualLcd/Network/UdpHidReceiver.cs`
   - Async UDP listener
   - Configurable bind address and port
   - Events: `PacketReceived`, `Error`
   - Statistics tracking

2. `src/LCDPossible.VirtualLcd/Views/LcdDisplayControl.cs`
   - Custom Avalonia control
   - WriteableBitmap for efficient updates
   - JPEG decode via ImageSharp
   - Maintains aspect ratio

3. `src/LCDPossible.VirtualLcd/Views/MainWindow.axaml`
   - Window sized to protocol dimensions
   - LcdDisplayControl as main content
   - Optional stats overlay

4. `src/LCDPossible.VirtualLcd/ViewModels/MainViewModel.cs`
   - Coordinates receiver → protocol → display
   - Exposes stats for binding
   - Start/stop control

5. `src/LCDPossible.VirtualLcd/Program.cs`
   - CLI parsing with System.CommandLine
   - Protocol selection
   - Window configuration

**Validation:** Manual test with netcat sending raw bytes.

### Phase 3: Virtual Device in Main App (Day 3)

**Goal:** Enable LCDPossible to send to VirtualLCD.

**Files to create:**

1. `src/LCDPossible.Core/Usb/VirtualHidDevice.cs`
   - IHidDevice implementation
   - UDP client sending to endpoint
   - Connection state management

2. `src/LCDPossible.Core/Usb/VirtualDeviceEnumerator.cs`
   - IDeviceEnumerator for virtual devices
   - Reads from configuration
   - Creates VirtualHidDevice instances

3. `src/LCDPossible/Configuration/VirtualDeviceSettings.cs`
   - POCO for virtual device config
   - Endpoint, protocol, enabled flag

**Files to modify:**

4. `src/LCDPossible/Program.cs` / DI registration
   - Register VirtualDeviceEnumerator when configured
   - Composite enumerator pattern (real + virtual)

5. `src/LCDPossible/appsettings.json`
   - Add VirtualDevices section

**Validation:** End-to-end test: LCDPossible → VirtualLCD displaying panel.

### Phase 4: Polish & Features (Day 4)

**Goal:** Add quality-of-life features.

**Features:**

1. **Stats overlay**
   - FPS counter
   - Bytes/sec
   - Packets received/dropped
   - Last frame time

2. **Recording/playback**
   - Record HID packets to file with timestamps
   - Replay for debugging/testing

3. **Window options**
   - Scale factor for HiDPI
   - Borderless mode
   - Always-on-top

4. **Error handling**
   - Protocol errors displayed in window
   - Reconnection on receiver errors
   - Graceful shutdown

5. **CLI for main app**
   - `--virtual` flag for quick setup
   - `list --include-virtual` shows virtual devices

### Phase 5: Documentation & Testing (Day 5)

**Goal:** Comprehensive docs and test coverage.

**Deliverables:**

1. `docs/virtual-lcd.md` - User guide
2. Unit tests for protocols
3. Integration test script
4. Update CLAUDE.md with virtual LCD info
5. README in VirtualLcd project

## Package Dependencies

### LCDPossible.VirtualLcd.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.*" />
    <PackageReference Include="Avalonia.Desktop" Version="11.*" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.*" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="11.*" />
    <PackageReference Include="SixLabors.ImageSharp" Version="3.*" />
    <PackageReference Include="System.CommandLine" Version="2.*" />
  </ItemGroup>
</Project>
```

## Testing Strategy

### Unit Tests

- `TrofeoVisionProtocolTests.cs`
  - Parse valid header
  - Accumulate multi-packet frame
  - Handle malformed packets
  - Reset state correctly

### Integration Tests

- Loopback test: VirtualHidDevice → UdpHidReceiver → Protocol → verify frame
- Performance test: Sustain 60fps for 60 seconds

### Manual Testing

1. Start VirtualLCD: `VirtualLCD --stats`
2. Start LCDPossible: `lcdpossible serve --virtual udp://localhost:5302`
3. Verify panels display correctly
4. Check stats show ~60fps

## Success Criteria

1. VirtualLCD displays frames from LCDPossible at 60fps
2. No physical hardware required for development
3. Protocol errors clearly reported
4. Works on Windows and Linux
5. < 5ms latency from send to display
6. Memory stable (no leaks over extended run)
