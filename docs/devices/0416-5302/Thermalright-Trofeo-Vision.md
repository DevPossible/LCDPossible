# Thermalright Trofeo Vision LCD - Device Documentation

> **Device ID:** VID 0x0416 / PID 0x5302
> **Document Version:** 1.2
> **Last Updated:** January 2026
> **Status:** Verified Working

---

## Table of Contents

1. [Hardware Overview](#hardware-overview)
2. [Controller Hardware Research](#controller-hardware-research)
3. [USB Protocol](#usb-protocol)
4. [HID Commands Reference](#hid-commands-reference)
5. [Image Transfer Protocol](#image-transfer-protocol)
6. [Firmware Update Possibilities](#firmware-update-possibilities)
7. [Known Limitations](#known-limitations)
8. [Reverse Engineering Sources](#reverse-engineering-sources)

---

## Hardware Overview

### Specifications

| Property | Value |
|----------|-------|
| **Product Name** | Thermalright Trofeo Vision 360 ARGB |
| **Vendor ID (VID)** | `0x0416` (1046 decimal) - Winbond Electronics Corp. |
| **Product ID (PID)** | `0x5302` (21250 decimal) |
| **Display Type** | IPS LCD |
| **Display Size** | 6.86 inches (diagonal) |
| **Resolution** | 1280 x 480 pixels |
| **Aspect Ratio** | 8:3 (ultra-wide) |
| **Color Depth** | 16-bit RGB565 (raw) or JPEG compressed |
| **Interface** | USB 2.0 HID Class |
| **HID Packet Size** | 512 bytes + 1 byte Report ID |
| **Connection** | USB Type-C (data) + 9-pin internal header |
| **Persistence** | None - requires continuous streaming |

### Physical Dimensions

- **Screen:** L187.2mm x W72.1mm x H21mm
- **Viewing Angle:** IPS wide viewing angles

### Related Device IDs

| VID | PID | Packet Size | Description |
|-----|-----|-------------|-------------|
| 0x0416 | 0x8001 | 64 bytes | PA120 Digital segment display |
| **0x0416** | **0x5302** | **512 bytes** | **Trofeo Vision LCD (this device)** |
| 0x0418 | 0x5303 | 64 bytes | Secondary controller |
| 0x0418 | 0x5304 | 512 bytes | Extended LCD support |
| 0x87AD | 0x70DB | 512 bytes | ChiZhu Tech USBDISPLAY (480x480) |

---

## Controller Hardware Research

### USB Vendor Analysis

**VID 0x0416** is registered to **Winbond Electronics Corp.** (Taiwan), which is also associated with **Nuvoton Technology Corp.** (Nuvoton is a spin-off of Winbond's logic IC business).

Related Winbond/Nuvoton USB devices in the same vendor space:

| PID | Description | Notes |
|-----|-------------|-------|
| 0x5210 | Nuvoton Nu-Link2 MSC FW UPGRADE | Mass Storage firmware upgrade tool |
| 0x5211 | Nuvoton Nu-Link2 HID FW UPGRADE | HID-based firmware upgrade tool |
| 0x3813 | Panel Display | Display device |
| **0x5302** | **Thermalright LCD** | **This device** |

The PID 5302 being in the 52xx range (similar to firmware upgrade tools) suggests the device may use a Nuvoton microcontroller with ISP capabilities.

### PCB Analysis (Limited)

From teardown reports of similar Thermalright devices:

| Component | Marking | Status |
|-----------|---------|--------|
| Main Controller IC | XJE9XR000RDA | **Unknown** - no public datasheet |
| PCB | 50.LMA01001A | **Unknown** - no manufacturer identified |

The controller chip uses custom/OEM markings that don't match any public datasheets, making deep hardware analysis difficult.

### Similar USB LCD Controllers

Other USB LCD displays use known controllers:

| Controller | Manufacturer | Used In | Flash | Data Flash |
|------------|--------------|---------|-------|------------|
| CH552T | WCH (Nanjing Qinheng) | Turing Smart Screen | 16KB | 128 bytes |
| AX206 | Appotech | Photo frame LCDs | Varies | Unknown |
| RTD2556T | Realtek | Larger USB monitors | External | External |

The **CH552T** architecture is notable:
- 8-bit E8051 core MCU
- 16KB code flash (14KB app + 2KB bootloader)
- **128 bytes persistent DataFlash** (address C000h-C0FFh)
- USB 2.0 Full Speed device
- Supports ISP (In-System Programming) via USB

If the Thermalright LCD uses a similar architecture, it could theoretically support:
- 128 bytes of persistent user data
- Firmware updates via USB ISP bootloader

### Official Firmware References

The **TR-VISION HOME** software documentation states:
> "Please confirm the LCD firmware version is updated to **118**"

This confirms:
1. The LCD has updateable firmware
2. Firmware versioning exists (v118 as of 2026)
3. Official update mechanism exists (method unknown)

---

## USB Protocol

### Communication Method

- **Transport:** USB HID Class (Human Interface Device)
- **Transfer Type:** Interrupt/Bulk transfers
- **Report ID:** 0x00 (must be prepended to all packets)
- **Max Packet Size:** 513 bytes (1 byte Report ID + 512 bytes data)

### USB Control Transfer Parameters

Used by some implementations for initial setup:

| Parameter | Value | Description |
|-----------|-------|-------------|
| bmRequestType | 0x21 | Host-to-Device, Class, Interface |
| bRequest | 0x09 | SET_REPORT |
| wValue | 0x0200 | Output report, ID 0 |
| wIndex | 0x0000 | Interface 0 |

### Linux udev Rules

```bash
# /etc/udev/rules.d/99-thermalright-lcd.rules
SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="5302", MODE="0666"
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0416", ATTRS{idProduct}=="5302", MODE="0666"
```

---

## HID Commands Reference

### Packet Header Format

All packets start with a 4-byte magic signature:

```
Offset  Byte   Description
------  ----   -----------
0       0xDA   Magic byte 0 (218)
1       0xDB   Magic byte 1 (219)
2       0xDC   Magic byte 2 (220)
3       0xDD   Magic byte 3 (221)
```

### Command Types

| Command ID | Hex | Name | Description | Status |
|------------|-----|------|-------------|--------|
| 0x00 | 0 | ONOFF | Power on/off control | Documented |
| 0x01 | 1 | STATE | Set device state | Documented |
| 0x02 | 2 | IMAGE | Send image data | **Verified Working** |
| 0x04 | 4 | AUDIO | Audio control | Unknown parameters |
| 0x05 | 5 | MOTOR | Pump motor control | Unknown parameters |
| 0x06 | 6 | FAN | Fan speed control | Unknown parameters |
| 0x10 | 16 | LED | ARGB LED control | Documented |
| 0x30 | 48 | LCD | LCD control | Unknown parameters |
| 0x68 | 104 | LEDMS | LED timing (ms) | Unknown parameters |

### ONOFF Command (0x00)

Turn display on or off:

```
Offset  Size  Value   Description
------  ----  -----   -----------
0-3     4     Magic   DA DB DC DD
4       1     0x00    Command: ONOFF
5       1     state   0=Off, 1=On
6-511   506   0x00    Padding
```

### STATE Command (0x01)

Query or set device state:

```
Offset  Size  Value   Description
------  ----  -----   -----------
0-3     4     Magic   DA DB DC DD
4       1     0x01    Command: STATE
5       1     mode    Mode selector
6-511   506   0x00    Padding
```

### LED Command (0x10)

Control ARGB lighting:

```
Offset  Size  Value      Description
------  ----  -----      -----------
0       1     0xDC       Header (legacy format)
1       1     0x10       Command: LED
2       1     mode       0=Off, 1=Static, 2=Breathing, 3=Colorful, 4=Rainbow
3       1     brightness 0-100
4       1     speed      Animation speed
5-7     3     RGB        Primary color (R, G, B)
8-10    3     RGB        Secondary color (for dual-color modes)
11+     var   channels   Per-channel settings
```

---

## Image Transfer Protocol

### Image Packet Structure

For JPEG-compressed images (recommended):

```
Offset  Size  Value           Description
------  ----  -----           -----------
0       1     0xDA            Header byte 0
1       1     0xDB            Header byte 1
2       1     0xDC            Header byte 2
3       1     0xDD            Header byte 3
4       1     0x02            Command: Image data
5-7     3     0x00 0x00 0x00  Reserved
8       2     0x00 0x05       Width: 1280 (little-endian)
10      2     0xE0 0x01       Height: 480 (little-endian)
12      1     0x02            Compression: JPEG (0x02)
13-15   3     0x00 0x00 0x00  Reserved
16-19   4     [length]        JPEG data length (little-endian uint32)
20+     var   [jpeg_data]     JPEG image bytes
```

### Compression Types

| Value | Type | Description |
|-------|------|-------------|
| 0x00 | RAW | Uncompressed RGB565 (1.2MB per frame) |
| 0x02 | JPEG | JPEG compressed (10-50KB typical) |

### Multi-Packet Transfer

Large images are split across multiple HID reports:

```
Packet 1: [Report ID 0x00] [Header 20 bytes] [JPEG bytes 0-491]
Packet 2: [Report ID 0x00] [JPEG bytes 492-1003]
Packet 3: [Report ID 0x00] [JPEG bytes 1004-1515]
...
Packet N: [Report ID 0x00] [Remaining bytes] [Zero padding]
```

### JPEG Encoding Recommendations

| Setting | Recommended Value |
|---------|-------------------|
| Quality | 90-95% |
| Color Space | YCbCr |
| Subsampling | 4:2:2 or 4:2:0 |
| Progressive | No (baseline) |

### RGB565 Format (Raw Mode)

```
Bit layout (16 bits per pixel):
[15:11] = Red (5 bits)
[10:5]  = Green (6 bits)
[4:0]   = Blue (5 bits)

Memory layout (little-endian):
Byte 0: GGGBBBBB  (G[2:0] + B[4:0])
Byte 1: RRRRRGGG  (R[4:0] + G[5:3])

Frame size: 1280 x 480 x 2 = 1,228,800 bytes
```

---

## Firmware Update Possibilities

### Current Status: Unknown Protocol

Based on research, firmware updates **may be possible** but the protocol is not publicly documented.

### Evidence of Firmware Updates

1. **Version references:** TR-VISION HOME requires "LCD firmware version 118"
2. **Nuvoton association:** VID 0x0416 includes firmware upgrade tools (PID 5210, 5211)
3. **ISP bootloader pattern:** Similar devices use In-System Programming via USB

### Potential Approaches

#### 1. Official TRCC/TR-VISION Software (Safest)
- Check if TRCC v2.1.2 or TR-VISION HOME v2.0.5 includes firmware update functionality
- May require specific menu options or key combinations to access

#### 2. USB Packet Capture Analysis
- Capture USB traffic during TRCC startup/initialization
- Look for bootloader enumeration or firmware version queries
- Tools: Wireshark with USBPcap, USBlyzer

#### 3. Bootloader Mode Discovery
- Some devices enter bootloader with specific key combinations during power-on
- May enumerate with different PID (e.g., 5210 or 5211) in bootloader mode
- Monitor USB device manager while power-cycling

#### 4. Nuvoton ISP Tools
If the device uses a Nuvoton MCU:
- **NuMicro ISP Programming Tool** - Official Nuvoton tool
- **Command format:** 64-byte HID packets with encrypted firmware data
- **LDROM/APROM architecture:** Bootloader in LDROM (2KB), app in APROM (14KB)

### Theoretical Persistent Storage

If the controller has DataFlash similar to CH552:

| Feature | Capacity | Use Case |
|---------|----------|----------|
| DataFlash | 128 bytes | Could store: brightness, default image reference, LED settings |
| External SPI Flash | Unknown | Could store: actual default image (if present) |

**Note:** No external SPI flash chip has been identified in teardowns, suggesting any persistent storage would be limited to MCU internal flash.

### Required Research

To enable persistent default image:

1. **Identify controller chip** - Physical teardown and IC marking research
2. **Find bootloader mode** - USB enumeration testing during various power-on states
3. **Reverse engineer update protocol** - Packet capture during official firmware update
4. **Determine DataFlash access** - New HID commands for reading/writing persistent settings

### Related ISP Tools (for Reference)

| Tool | Purpose | URL |
|------|---------|-----|
| wchisp | WCH CH55x ISP programmer (Rust) | [ch32-rs/wchisp](https://github.com/ch32-rs/wchisp) |
| ch552tool | CH55x flash tool (Python) | [MarsTechHAN/ch552tool](https://github.com/MarsTechHAN/ch552tool) |
| NuMicro ISP | Nuvoton official tool | [Nuvoton Downloads](https://www.nuvoton.com/tool-and-software/) |
| isp55e0 | WCH ISP for Linux | [frank-zago/isp55e0](https://github.com/frank-zago/isp55e0) |

---

## Known Limitations

### No Persistent Storage (Current State)

**CRITICAL:** The LCD does NOT have persistent storage for images or settings.

| Feature | Persistence |
|---------|-------------|
| Display image | **None** - requires continuous streaming |
| Default image | **Not supported** - no protocol command exists |
| Brightness | **None** - controlled by host software |
| LED settings | **None** - must be set on each power-on |

The device displays a blank or default splash screen when powered on without host control.

### Undocumented Features

The following features are mentioned in TRCC software but protocol details are unknown:

| Feature | Status | Notes |
|---------|--------|-------|
| Set persistent default image | **Not possible** | No firmware storage command found |
| Brightness control | **Unknown** | May be software-only (gamma adjustment) |
| Display rotation | **Unknown** | May require image pre-rotation |
| Power state query | **Unknown** | GET_STATE command exists but parameters unknown |
| Audio passthrough | **Unknown** | AUDIO command exists |
| Motor/pump speed | **Unknown** | MOTOR command exists |

### Frame Rate Considerations

| Mode | Max FPS | Notes |
|------|---------|-------|
| JPEG streaming | ~60 FPS | Limited by USB bandwidth and encoding speed |
| RGB565 raw | ~10 FPS | Large transfer size limits throughput |

---

## Reverse Engineering Sources

### GitHub Projects

| Project | Language | Target Device | URL |
|---------|----------|---------------|-----|
| thermalright-lcd-control | Python | Multiple | [rejeb/thermalright-lcd-control](https://github.com/rejeb/thermalright-lcd-control) |
| trlcd_libusb | C | 0416:5302 | [NoNameOnFile/trlcd_libusb](https://github.com/NoNameOnFile/trlcd_libusb) |
| digital_thermal_right_lcd | Python | 0416:8001 | [MathieuxHugo/digital_thermal_right_lcd](https://github.com/MathieuxHugo/digital_thermal_right_lcd) |

### Official Software

| Software | Version | Purpose |
|----------|---------|---------|
| TRCC | v2.1.2 | Windows control software |
| TR-VISION HOME | v2.0.5 | Older control software |

Download: [Thermalright Downloads](https://www.thermalright.com/support/download/)

### Protocol Research Notes

1. **Header discovery:** The magic bytes `DA DB DC DD` were found in TRCC decompilation (FormCZTV.cs)
2. **Image format:** JPEG with compression type `0x02` verified working through USB packet capture
3. **No persistent commands:** Extensive search of TRCC code and reverse engineering projects found no commands for storing images on device
4. **Brightness:** TRCC implements brightness as software gamma adjustment, not a device command

---

## Implementation Checklist

For developers implementing support:

- [x] Device detection (VID 0x0416, PID 0x5302)
- [x] HID packet framing (512 bytes + Report ID)
- [x] Magic header (`DA DB DC DD`)
- [x] Image command (0x02)
- [x] JPEG compression
- [x] Multi-packet transfer
- [ ] LED control (0x10) - partially documented
- [ ] Power on/off (0x00) - untested
- [ ] Device state (0x01) - untested

---

## Document History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | January 2026 | Initial documentation |
| 1.1 | January 2026 | Added limitations section, persistence notes, research sources |
| 1.2 | January 2026 | Added controller hardware research, firmware update possibilities, ISP tools reference |
