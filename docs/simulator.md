# Virtual LCD Simulator

LCDPossible includes a virtual LCD simulator for testing and development without physical hardware. The simulator is a standalone Avalonia GUI application that receives display data over UDP and renders it in a window.

## Use Cases

- **Testing without hardware** - Develop and test panels without an LCD device
- **Development** - Debug rendering issues with visual feedback
- **Demonstrations** - Show LCDPossible capabilities without hardware
- **CI/CD** - Automated testing of panel rendering

## Quick Start

### 1. Start the Simulator

```bash
# From the repository root
dotnet run --project src/LCDPossible.VirtualLcd
```

The simulator opens a window showing a virtual 1280x480 LCD display.

### 2. Run LCDPossible with Virtual Device

In another terminal:

```bash
# The service auto-detects the virtual device
lcdpossible show cpu-info
```

The panel appears in the simulator window.

## Simulator Options

```bash
dotnet run --project src/LCDPossible.VirtualLcd -- [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `-d, --driver` | Protocol to simulate | `trofeo-vision` |
| `-p, --port` | UDP port to listen on | Auto (5302-5399) |
| `-b, --bind` | IP address to bind to | `0.0.0.0` |
| `--stats` | Show statistics overlay | Off |
| `--always-on-top` | Keep window above others | Off |
| `--borderless` | Hide window decorations | Off |
| `--scale` | Window scale factor (0.1-10.0) | 1.0 |
| `--list-drivers` | List available protocols | - |

### Examples

```bash
# Show all available protocols
dotnet run --project src/LCDPossible.VirtualLcd -- --list-drivers

# Start with specific port
dotnet run --project src/LCDPossible.VirtualLcd -- -p 5302

# Scaled-down window, always on top
dotnet run --project src/LCDPossible.VirtualLcd -- --scale 0.5 --always-on-top

# Borderless window for streaming/recording
dotnet run --project src/LCDPossible.VirtualLcd -- --borderless

# Show frame statistics
dotnet run --project src/LCDPossible.VirtualLcd -- --stats
```

## Supported Protocols

The simulator supports the same device protocols as physical devices:

| Protocol | Description | Resolution |
|----------|-------------|------------|
| `trofeo-vision` | Thermalright Trofeo Vision LCD | 1280x480 |

Protocols are provided by device plugins. When you add a new device driver plugin, it can also provide a simulator handler.

## How It Works

```
┌─────────────────┐      UDP Packets      ┌─────────────────┐
│   LCDPossible   │ ──────────────────▶  │  VirtualLcd     │
│   (Service)     │                       │  (Simulator)    │
└─────────────────┘                       └─────────────────┘
        │                                         │
        │ Encodes frames as                       │ Decodes packets
        │ HID packets                             │ using protocol
        │                                         │ handler
        ▼                                         ▼
   JPEG-encoded                              Rendered in
   frame data                                Avalonia window
```

1. LCDPossible encodes panel frames as JPEG data in HID packet format
2. Instead of sending to USB, packets go to the simulator via UDP
3. The simulator's protocol handler decodes the packets
4. Decoded frames are displayed in the Avalonia window

## Using with Profiles

Test your display profile with the simulator:

```bash
# Terminal 1: Start simulator
dotnet run --project src/LCDPossible.VirtualLcd

# Terminal 2: Run with your profile
lcdpossible serve --profile my-gaming-profile
```

## Development Tips

### Testing Panel Rendering

```bash
# Render panel to file (no simulator needed)
lcdpossible render cpu-info --debug

# Test with simulator for live preview
# Terminal 1:
dotnet run --project src/LCDPossible.VirtualLcd

# Terminal 2:
lcdpossible show my-new-panel
```

### Debugging Display Issues

Enable statistics overlay to see:
- Frame rate
- Decode time
- Packet count

```bash
dotnet run --project src/LCDPossible.VirtualLcd -- --stats
```

### Recording/Screenshots

Use borderless mode for clean captures:

```bash
dotnet run --project src/LCDPossible.VirtualLcd -- --borderless --scale 1.0
```

## Building the Simulator

The simulator is built as part of the main solution:

```bash
# Build all projects including simulator
./build.ps1

# Or build just the simulator
dotnet build src/LCDPossible.VirtualLcd
```

### Requirements

- .NET 10.0 SDK
- Avalonia UI (included via NuGet)

## Troubleshooting

### Simulator Not Receiving Data

1. **Check port availability:**
   ```bash
   # Linux/macOS
   netstat -an | grep 5302

   # Windows
   netstat -an | findstr 5302
   ```

2. **Check firewall rules** - UDP port must be open

3. **Verify service is targeting simulator:**
   ```bash
   lcdpossible list
   # Should show virtual device
   ```

### Window Not Appearing

- Check if Avalonia dependencies are installed
- Try without `--borderless` flag
- Check display scaling settings

### Performance Issues

- Reduce window scale: `--scale 0.5`
- Disable stats overlay if enabled
- Check CPU usage of both simulator and service

---

*See also: [Getting Started](getting-started.md) | [Panel Development](plugins/creating-panels.md)*

*[Back to Documentation](README.md)*
