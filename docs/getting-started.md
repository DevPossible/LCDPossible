# Getting Started

This guide will help you install LCDPossible and display your first panel.

## Prerequisites

- A supported LCD device (see [Supported Devices](#supported-devices))
- .NET 10.0 Runtime (installed automatically by installer scripts)

## Quick Install

### Windows

```powershell
irm https://raw.githubusercontent.com/DevPossible/lcd-possible/main/scripts/install-windows.ps1 | iex
```

### Linux (Ubuntu/Debian)

```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/lcd-possible/main/scripts/install-ubuntu.sh | bash
```

### macOS

```bash
curl -sSL https://raw.githubusercontent.com/DevPossible/lcd-possible/main/scripts/install-macos.sh | bash
```

For more installation options, see the main [README](../README.md#installation).

## First Steps

### 1. Verify Installation

```bash
lcdpossible --version
```

### 2. List Connected Devices

```bash
lcdpossible list
```

You should see your LCD device listed with its VID:PID.

### 3. Display a Test Panel

```bash
lcdpossible show cpu-info
```

This displays the CPU information panel on your LCD.

### 4. Try Different Panels

```bash
# System monitoring
lcdpossible show cpu-usage-graphic

# Screensaver
lcdpossible show plasma

# Multiple panels (slideshow)
lcdpossible show cpu-info,gpu-info,ram-info
```

### 5. Apply an Effect

```bash
lcdpossible show "cpu-info|@effect=matrix-rain"
```

### 6. Change Theme

```bash
lcdpossible config set-theme rgb-gaming
lcdpossible show cpu-info
```

## Running as a Service

To have LCDPossible start automatically:

```bash
# Install and start service
lcdpossible service install
lcdpossible service start
```

See [Service Setup](configuration/service-setup.md) for detailed instructions.

## Supported Devices

| Device | VID:PID | Status |
|--------|---------|--------|
| Thermalright Trofeo Vision 360 ARGB | 0416:5302 | Fully Supported |

For device protocol details, see [Reference/Devices](reference/devices/).

## Next Steps

- [Browse available panels](panels/README.md)
- [Explore effects](effects/README.md)
- [Customize themes](themes/README.md)
- [Configure profiles](configuration/profiles.md)

---

*[Back to Documentation](README.md)*
