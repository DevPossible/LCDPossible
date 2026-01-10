# Thermalright Trofeo Vision 360 ARGB - LCD Technical Reference

> **Document Version:** 1.0
> **Date:** January 2026
> **Source:** Reverse-engineered from TRCC.exe v2.1.0.0 (decompiled) + web research

---

## Table of Contents

1. [Product Overview](#product-overview)
2. [USB Device Identification](#usb-device-identification)
3. [USB HID Protocol](#usb-hid-protocol)
4. [Image Transfer Protocol](#image-transfer-protocol)
5. [Color Format & Encoding](#color-format--encoding)
6. [Display Modes](#display-modes)
7. [LED Control Protocol](#led-control-protocol)
8. [Theme System](#theme-system)
9. [Existing Linux Projects](#existing-linux-projects)
10. [Implementation Notes](#implementation-notes)

---

## Product Overview

### Hardware Specifications

| Specification | Value |
|---------------|-------|
| **Product** | Thermalright Trofeo Vision 360 ARGB |
| **Display Type** | IPS LCD |
| **Display Size** | 6.86 inches (diagonal) |
| **Resolution** | 1280 × 480 pixels |
| **Aspect Ratio** | 8:3 (ultra-wide) |
| **Color Depth** | 16-bit (RGB565) or JPEG compressed |
| **Connection** | USB 2.0 (9-pin internal header) + USB Type-C data |
| **Pump Speed** | Up to 6400 RPM |
| **ARGB** | 3-PIN 5V connector |

### Display Characteristics

- **Persistence:** Display does NOT retain images - requires continuous streaming
- **Refresh Rate:** ~64 Hz (15.625ms per frame based on code analysis)
- **Orientation:** Supports 0°, 90°, 180°, 270° rotation
- **Brightness:** Software controllable

### Official Resources

- [Thermalright Product Page](https://www.thermalright.com/product/trofeo-vision-360-argb-black/)
- [Thermalright Downloads](https://www.thermalright.com/support/download/)
- [Amazon Product Page](https://www.amazon.com/Thermalright-Trofeo-Vision-Black-Cooler/dp/B0FJM4WBHF)

---

## USB Device Identification

### Thermalright USB HID Devices

The TRCC software supports multiple Thermalright products with different USB identifiers:

| Device ID | VID (Hex) | VID (Dec) | PID (Hex) | PID (Dec) | Packet Size | Description |
|-----------|-----------|-----------|-----------|-----------|-------------|-------------|
| Device 1 | 0x0416 | 1046 | 0x8001 | 32769 | 64 bytes | Small digital display (PA120 Digital, etc.) |
| **Device 2** | **0x0416** | **1046** | **0x5302** | **21250** | **512 bytes** | **Trofeo Vision LCD (primary)** |
| Device 3 | 0x0418 | 1048 | 0x5303 | 21251 | 64 bytes | Secondary controller |
| Device 4 | 0x0418 | 1048 | 0x5304 | 21252 | 512 bytes | Extended LCD support |

**Source:** `UCDevice.cs:166-201`

### Vendor Information

- **VID 0x0416** is registered to **Winbond Electronics Corp.** in the USB-IF database
- [Device Hunt - VID 0416](https://devicehunt.com/view/type/usb/vendor/0416)

### Linux Device Detection

```bash
# Check if device is connected
lsusb | grep "0416:5302"

# Detailed device info
lsusb -d 0416:5302 -v

# Check HID interface
ls -la /dev/hidraw*
```

---

## USB HID Protocol

### Packet Structure Overview

All communication uses USB HID reports with the following general structure:

```
┌─────────────────────────────────────────────────────────────┐
│ Offset │ Size │ Description                                 │
├────────┼──────┼─────────────────────────────────────────────┤
│ 0-3    │ 4    │ Header bytes (magic signature)              │
│ 4      │ 1    │ Command type                                │
│ 5-7    │ 3    │ Reserved / command-specific                 │
│ 8-11   │ 4    │ Parameter 1 (e.g., width/height)            │
│ 12-15  │ 4    │ Parameter 2 (e.g., data length)             │
│ 16-19  │ 4    │ Parameter 3 (command-specific)              │
│ 20+    │ var  │ Payload data                                │
└─────────────────────────────────────────────────────────────┘
```

### Header Signatures

Two header patterns are used depending on device/command:

| Header Type | Bytes (Hex) | Bytes (Dec) | Usage |
|-------------|-------------|-------------|-------|
| Primary | `DA DB DC DD` | 218 219 220 221 | LCD image data |
| Alternative | `12 34 56 78` | 18 52 86 120 | Some control commands |
| Legacy | `DC DD` | 220 221 | Older devices (64-byte) |

**Source:** `FormCZTV.cs:172-173`, `FormCZTV.cs:2790-2791`

### Command Types

| Command ID | Hex | Name | Description |
|------------|-----|------|-------------|
| 0x00 | 0 | ONOFF | Power on/off control |
| 0x01 | 1 | STATE | Set device state |
| 0x02 | 2 | GET_STATE | Query device state |
| 0x04 | 4 | AUDIO | Audio control features |
| 0x05 | 5 | MOTOR | Pump/motor control |
| 0x06 | 6 | FAN | Fan speed control |
| 0x10 | 16 | LED | LED lighting control |
| 0x30 | 48 | LCD | LCD display data |
| 0x68 | 104 | LEDMS | LED millisecond timing |

**Source:** `UCDevice.cs`, `FormKVMALED6.cs:23-32`

---

## Image Transfer Protocol

### 1280×480 LCD Image Packet Structure

For the Trofeo Vision display, image data is sent with this 20-byte header:

```
Offset  Size  Value           Description
──────────────────────────────────────────────────────────
0       1     0xDA            Header byte 0
1       1     0xDB            Header byte 1
2       1     0xDC            Header byte 2 (220)
3       1     0xDD            Header byte 3 (221)
4       1     0x02            Command: Image data
5       1     0x00            Reserved
6       1     0x00            Reserved
7       1     0x00            Reserved
8       1     0x00            Width low byte (1280 & 0xFF = 0x00)
9       1     0x05            Width high byte (1280 >> 8 = 0x05)
10      1     0xE0            Height low byte (480 & 0xFF = 0xE0)
11      1     0x01            Height high byte (480 >> 8 = 0x01)
12      1     0x02            Compression type (0x02 = JPEG)
13      1     0x00            Reserved
14      1     0x00            Reserved
15      1     0x00            Reserved
16      4     [length]        Image data length (little-endian uint32)
20+     var   [jpeg_data]     JPEG image bytes
```

**Source:** `FormCZTV.cs:3558-3581`

### Supported Resolutions

The TRCC software supports these LCD resolutions:

| Resolution | Width×Height | Aspect | Notes |
|------------|--------------|--------|-------|
| 240×240 | Square | 1:1 | Small square displays |
| 320×320 | Square | 1:1 | |
| 360×360 | Square | 1:1 | |
| 480×480 | Square | 1:1 | |
| 320×240 | Landscape | 4:3 | |
| 640×480 | Landscape | 4:3 | VGA |
| 800×480 | Landscape | 5:3 | WVGA |
| 854×480 | Landscape | 16:9 | FWVGA |
| 960×540 | Landscape | 16:9 | qHD |
| 960×320 | Ultra-wide | 3:1 | |
| **1280×480** | **Ultra-wide** | **8:3** | **Trofeo Vision** |
| 1600×720 | Ultra-wide | 20:9 | |
| 1920×462 | Bar | ~4:1 | |

**Source:** `FormCZTV.cs:3558-3679` (multiple resolution handlers)

### Transfer Modes

| Mode | Compression | Packet Content | Use Case |
|------|-------------|----------------|----------|
| Mode 1 | RGB565 raw | Uncompressed pixels | Direct framebuffer |
| Mode 2 | JPEG | Compressed image | Network/efficient transfer |

**Source:** `FormCZTV.cs:1957-1960`

### JPEG Compression Settings

```csharp
// Default JPEG quality
public int myDeviceJpgYSL = 95;  // 95% quality

// Compression function signature
private static byte[] CompressionImage(Image image, long quality)
```

**Source:** `FormCZTV.cs:34`, `FormCZTV.cs:2471-2480`

---

## Color Format & Encoding

### RGB565 Format (16-bit color)

When sending raw pixel data (Mode 1), images are converted to RGB565:

```
Bit layout (16 bits per pixel):
┌─────────────────────────────────────┐
│ 15 14 13 12 11 │ 10 9 8 7 6 5 │ 4 3 2 1 0 │
│     R (5 bits) │   G (6 bits) │ B (5 bits)│
└─────────────────────────────────────┘

Memory layout (little-endian):
Byte 0: GGGBBBBB  (G lower 3 bits + B 5 bits)
Byte 1: RRRRRGGG  (R 5 bits + G upper 3 bits)
```

### Conversion Algorithm

```csharp
// From 32-bit ARGB to RGB565 (little-endian)
byte R = source[offset + 2];  // Red component
byte G = source[offset + 1];  // Green component
byte B = source[offset + 0];  // Blue component

// Standard RGB565 encoding:
rgb565[0] = (byte)((G << 3) & 0xE0) | (byte)(B >> 3);
rgb565[1] = (byte)(R & 0xF8) | (byte)(G >> 5);

// Some devices use big-endian (bytes swapped):
rgb565[0] = (byte)(R & 0xF8) | (byte)(G >> 5);
rgb565[1] = (byte)((G << 3) & 0xE0) | (byte)(B >> 3);
```

**Source:** `FormCZTV.cs:3791-3816`

### Raw Frame Size Calculation

```
1280 × 480 × 2 bytes/pixel = 1,228,800 bytes (~1.17 MB per frame)
```

---

## Display Modes

### Theme Display Modes

| Mode ID | Name (Chinese) | Name (English) | Description |
|---------|----------------|----------------|-------------|
| 0 | 背景显示 | Background Display | Static image or GIF animation |
| 1 | 投屏显示 | Screen Projection | Mirror PC screen region to LCD |
| 2 | 视频播放器 | Video Player | Play video files |
| 3 | 系统信息 | System Info | Real-time hardware monitoring |
| 4 | 蒙版显示 | Mask/Overlay | Transparency effects |

**Source:** `FormCZTV.cs` mode handling

### System Information Display

Available metrics for on-screen display:

| Category | Metrics |
|----------|---------|
| CPU | Temperature, Usage %, Frequency, Power |
| GPU | Temperature, Usage %, Memory, Frequency |
| Memory | Usage %, Used/Total |
| Storage | Disk activity, Usage |
| Network | Upload/Download speed |
| Custom | User-defined text overlays |

---

## LED Control Protocol

### LED Packet Structure

```
Offset  Size  Description
─────────────────────────────
0       1     Header (0xDC = 220)
1       1     Command (0x10 = LED)
2       1     Mode
3       1     Brightness (0-100)
4       1     Speed (animation speed)
5-7     3     RGB color 1
8-10    3     RGB color 2 (for dual-color modes)
11+     var   Per-channel settings
```

### LED Modes

| Mode | Name | Description |
|------|------|-------------|
| 1 | Static | Solid color |
| 2 | Breathing | Fade in/out |
| 3 | Colorful | Cycle through colors |
| 4 | Rainbow | Rainbow wave effect |

### LED Channel Control

Up to 10 LED channels supported:
- Per-channel on/off
- Per-channel mode selection
- Master brightness control
- Animation speed control

**Source:** `FormKVMALED6.cs:38-80`

---

## Theme System

### Theme File Structure

Themes are stored in: `Data\USBLCD\Theme\{resolution}\{theme_name}\`

| File | Purpose |
|------|---------|
| `Theme.zt` | Theme configuration (binary) |
| `Theme.png` | Preview thumbnail |
| `config1.dc` | Device configuration |
| `Color.dc` | Color settings |
| `*.gif` | Animation frames |

### Web Theme Download

Themes can be downloaded from Thermalright's CDN:

```
Base URL: http://www.czhorde.com/tr/
Pattern:  bj{resolution}/{theme_id}/

Example:  http://www.czhorde.com/tr/bj1280480/theme001/
```

**Source:** `FormCZTV.cs:446`, `UCThemeWeb.cs`

### GIF Animation Support

- Maximum frames: 20,000
- Frame timing: Stored in `gifDelays[]` array
- Loop support: Continuous or single-play

---

## Existing Linux Projects

### Reference Implementations

| Project | URL | Target Device | Status |
|---------|-----|---------------|--------|
| digital_thermal_right_lcd | [GitHub](https://github.com/MathieuxHugo/digital_thermal_right_lcd) | PA120 Digital (0x8001) | Active |
| Peerless_assassin_and_CLI_UI | [GitHub](https://github.com/raffa0001/Peerless_assassin_and_CLI_UI) | Peerless Assassin 120 D | Active |
| liquidctl | [GitHub](https://github.com/liquidctl/liquidctl) | Various AIOs | No Thermalright support |

### Key Differences from Trofeo Vision

| Feature | PA120 Digital | Trofeo Vision |
|---------|---------------|---------------|
| PID | 0x8001 | 0x5302 |
| Packet Size | 64 bytes | 512 bytes |
| Display | Segment/digit | Full LCD |
| Data Format | Simple values | JPEG/RGB565 images |

---

## Implementation Notes

### USB HID Access on Linux

```bash
# Install hidapi
sudo apt install libhidapi-hidraw0 libhidapi-libusb0

# Create udev rule for unprivileged access
# /etc/udev/rules.d/99-thermalright-lcd.rules
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="5302", MODE="0666"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0416", ATTRS{idProduct}=="5302", MODE="0666"

# Reload udev rules
sudo udevadm control --reload-rules
sudo udevadm trigger
```

### Recommended Libraries

| Language | Library | Notes |
|----------|---------|-------|
| .NET | HidSharp | Cross-platform, well-maintained |
| .NET | HidApi.Net | Wrapper around hidapi |
| Python | hidapi | Used by existing projects |
| Rust | hidapi | Rust bindings for hidapi |
| C/C++ | libhidapi | Native library |

### Performance Considerations

- **Frame Rate:** Target 30-60 FPS for smooth animation
- **JPEG Encoding:** Use hardware acceleration if available
- **USB Latency:** HID reports have ~1ms latency minimum
- **Buffer Size:** 512-byte HID reports, may need multiple reports per frame

### Packet Fragmentation

For large images (JPEG > 512 bytes), data must be split across multiple HID reports:

```
Report 1: [Header 20 bytes] + [JPEG bytes 0-491]
Report 2: [JPEG bytes 492-1003]
Report 3: [JPEG bytes 1004-1515]
...
Report N: [Remaining JPEG bytes] + [Padding]
```

---

## Appendix: Code References

### Key Source Files (from TRCC decompilation)

| File | Lines | Purpose |
|------|-------|---------|
| `FormCZTV.cs` | ~7,000 | Main LCD control, theme management |
| `UCDevice.cs` | ~1,400 | USB HID communication layer |
| `FormKVMALED6.cs` | ~1,900 | LED controller |
| `UCScreenImage.cs` | ~800 | Display rendering |
| `UCSystemInfo.cs` | ~600 | Hardware monitoring |

### Protocol Constants

```csharp
// Headers
const byte USB_PACKED_Head = 220;      // 0xDC
const byte USB_PACKED_Head_NEW = 221;  // 0xDD

// Commands
const byte USB_PACKED_ONOFF = 0;
const byte USB_PACKED_STATE = 1;
const byte USB_PACKED_GET_STATE = 2;
const byte USB_PACKED_AUDIO = 4;
const byte USB_PACKED_MOTOR = 5;
const byte USB_PACKED_FAN = 6;
const byte USB_PACKED_LED = 16;        // 0x10
const byte USB_PACKED_LCD = 48;        // 0x30
const byte USB_PACKED_LEDMS = 104;     // 0x68
```

---

## Document History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | January 2026 | Initial reverse-engineering documentation |
