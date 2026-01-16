# Potential LCD Drivers for LCDPossible

This document catalogs USB LCD displays that could potentially be supported by LCDPossible. Each entry includes protocol documentation, VID/PID information, and implementation references where available.

## Priority Legend

| Priority | Meaning |
|----------|---------|
| P1 | Well-documented protocol, high demand, relatively easy to implement |
| P2 | Documented protocol, moderate demand or complexity |
| P3 | Partially documented, requires reverse engineering work |
| P4 | Undocumented, significant RE effort required |

---

## Currently Supported

### Thermalright Trofeo Vision 360 ARGB
- **Status**: Implemented
- **VID**: 0x0416
- **PID**: 0x5302
- **Resolution**: 1280x480
- **Protocol**: HID, 513-byte reports (1 byte report ID + 512 bytes data)
- **Image Format**: JPEG (preferred) or RGB565
- **Driver**: `TrofeoVisionDriver`

### Thermalright PA120 Digital
- **Status**: Stub implemented
- **VID**: 0x0416
- **PID**: 0x8001
- **Resolution**: Segment display
- **Protocol**: HID, 65-byte reports
- **Driver**: `PA120DigitalDriver`

---

## P1 - High Priority Targets

### Turing Smart Screen Family

Low-cost USB-C displays popular for PC monitoring. Protocol fully reverse-engineered.

| Model | Size | Resolution | Protocol Class |
|-------|------|------------|----------------|
| Turing 3.5" (Rev A) | 3.5" | 480x320 | LcdCommRevA |
| XuanFang 3.5" (Rev B) | 3.5" | 480x320 | LcdCommRevB |
| XuanFang Flagship | 3.5" | 480x320 | LcdCommRevB + RGB LEDs |
| Turing 2.1" Round | 2.1" | 480x480 | LcdCommRevC |
| Turing 5" | 5" | 800x480 | LcdCommRevC |
| Turing 8.8" | 8.8" | 480x1920 | LcdCommRevC |
| Turing 8.8" V1.1 | 8.8" | 480x1920 | New protocol (partial) |
| Kipye Qiye 3.5" | 3.5" | 480x320 | LcdCommRevD |

**USB Identification**:
| Model | VID | PID | Notes |
|-------|-----|-----|-------|
| Turing 8.8" V1.0 | 0x1CBE | 0x0088 | Product: TURZX1.0 |
| Turing 8.8" V1.1 | 0x1CBE | 0xF000 | Product: USB-Daemon (new) |
| Others | Serial | Serial | USB CDC serial devices |

**Protocol Documentation**:
- Main project: https://github.com/mathoudebine/turing-smart-screen-python
- Hardware revisions wiki: https://github.com/mathoudebine/turing-smart-screen-python/wiki/Hardware-revisions
- MIT-licensed reimplementation: https://github.com/gerph/turing-smart-screen-python-mit
- Vendor apps analysis: https://github.com/mathoudebine/turing-smart-screen-python/wiki/Vendor-apps

**Implementation Notes**:
- Uses USB serial communication (not HID class)
- Different protocol classes for different hardware revisions
- Python reference implementations available
- XuanFang "Flagship" supports backplate RGB LED control
- Vendor apps built in C# with Visual Studio, some obfuscated
- Linux may need `dev.detach_kernel_driver(0)` or udev rules

**Pricing**: ~$20-35 USD on AliExpress/Banggood

---

### Elgato Stream Deck

Officially documented HID protocol for LCD key displays.

| Model | Keys | Per-Key Resolution | VID | PID |
|-------|------|-------------------|-----|-----|
| Stream Deck Mini | 6 | 80x80 | 0x0FD9 | 0x0063 |
| Stream Deck Original | 15 | 72x72 | 0x0FD9 | 0x0060 |
| Stream Deck MK.2 | 15 | 72x72 | 0x0FD9 | 0x0080 |
| Stream Deck XL | 32 | 96x96 | 0x0FD9 | 0x006C |
| Stream Deck Pedal | 3 | N/A (no LCD) | 0x0FD9 | 0x0086 |
| Stream Deck + | 8 + dials | 200x100 strip | 0x0FD9 | 0x0084 |
| Stream Deck Module 6 | 6 | 72x72 | 0x0FD9 | varies |
| Stream Deck Module 15 | 15 | 72x72 | 0x0FD9 | varies |
| Stream Deck Module 32 | 32 | 72x72 | 0x0FD9 | varies |

**Protocol Details**:
- V1 Protocol: 8191 bytes transfer size, BMP images
- V2 Protocol: 1024 bytes transfer size, JPEG images
- Feature reports: 32 bytes max, zero-padded
- Input reports: Button state array (polled via HID READ)
- V2 devices accept both BMP and JPEG icons

**Protocol Documentation**:
- Official HID docs: https://docs.elgato.com/streamdeck/hid/
- Module 6 docs: https://docs.elgato.com/streamdeck/hid/module-6/
- Module 15/32 docs: https://docs.elgato.com/streamdeck/hid/module-15_32/
- Community protocol notes: https://gist.github.com/cliffrowley/d18a9c4569537b195f2b1eb6c68469e0
- Reverse engineering blog: https://den.dev/blog/reverse-engineering-stream-deck/
- Deno implementation: https://dbushell.com/2022/10/14/deno-usb-hid-stream-deck/

**Implementation Notes**:
- V1 vs V2 protocol differences (transfer size, image format)
- Images split into chunks in output reports
- Official SDK available for reference
- 54-byte BMP header in V1 protocol
- hid4j Java library implementation available (MIT license)

---

## P2 - Medium Priority Targets

### NZXT Kraken Z Series (AIO Cooler LCD)

LCD screens on AIO liquid coolers. Supported by liquidctl project.

| Model | LCD Resolution | VID | PID |
|-------|---------------|-----|-----|
| Kraken Z53 | 240x240 | 0x1E71 | 0x3008 |
| Kraken Z63 | 240x240 | 0x1E71 | 0x3008 |
| Kraken Z73 | 240x240 | 0x1E71 | 0x3008 |
| Kraken Z53 Elite | 640x640 | 0x1E71 | varies |
| Kraken Z63 Elite | 640x640 | 0x1E71 | varies |
| Kraken Z73 Elite | 640x640 | 0x1E71 | varies |
| Kraken 2023 | 240x240 | 0x1E71 | varies |
| Kraken 2023 Elite | 640x640 | 0x1E71 | varies |
| Kraken 2024 RGB | 640x640 | 0x1E71 | varies |

**Protocol Details**:
- Read/Write buffer: 64 bytes
- LCD channel: "lcd"
- Brightness: 0-100
- Orientation: 0-3 (up, right, down, left)
- Animation speed: 0, 1, 2
- Manufacturer: "NZXT Inc."
- Product Name: "NZXT KrakenZ Device"

**Protocol Documentation**:
- liquidctl project: https://github.com/liquidctl/liquidctl
- Kraken guide: https://github.com/liquidctl/liquidctl/blob/main/docs/kraken-x3-z3-guide.md
- LCD screen issue: https://github.com/liquidctl/liquidctl/issues/444
- HWiNFO integration: https://www.hwinfo.com/forum/threads/nzxt-kraken-hwinfo-nzxt-cam-finally-defeated.8009/
- SignalRGB troubleshooting: https://docs.signalrgb.com/troubleshooting/nzxt

**Implementation Notes**:
- NZXT CAM opens device in exclusive mode if started first
- If other software opens first, CAM falls back to shared mode
- liquidctl has experimental LCD support since v1.11.0
- Elite models have light ring on pump housing (not yet supported in liquidctl)
- Kraken 2023 AIOs removed integrated LED controller
- Uses Asetek Gen 7 pump

---

### ASUS ROG Ryujin II/III (AIO Cooler LCD)

Premium AIO coolers with large LCD screens.

| Model | LCD Size | VID | Notes |
|-------|----------|-----|-------|
| ROG Ryujin II 240 | 3.5" | 0x0B05 | |
| ROG Ryujin II 360 | 3.5" | 0x0B05 | |
| ROG Ryujin III 360 | 3.5" | 0x0B05 | Largest AIO LCD |
| ROG Ryujin III 360 ARGB | 3.5" | 0x0B05 | With ARGB |

**Protocol Documentation**:
- Linux HWMON driver announcement: https://www.phoronix.com/news/ASUS-ROG-RYUJIN-II-360-Linux
- ASUS support article: https://www.asus.com/support/faq/1048625/
- ROG Forum discussion: https://rog-forum.asus.com/t5/all-in-one-cooling/asus-rog-ryyjin-ii-360-fan-and-pump-info-in-lcd-screen/td-p/907761

**Implementation Notes**:
- Linux HWMON driver by Aleksa Savic (same developer as NZXT driver)
- Protocol reverse-engineered by Florian Freudiger
- HWMON driver reports: pump speed, fan speeds, coolant temps
- LCD control NOT in Linux HWMON driver yet
- AIDA64 Extreme integration available on Windows
- USB 2.0 controller cable for fan/pump control
- Fans NOT connected to motherboard headers
- Largest AIO LCD screen (3.5")
- Uses Asetek Gen 7 pump (same as NZXT)

---

### Corsair iCUE Link LCD

| Model | LCD Size | VID | PID |
|-------|----------|-----|-----|
| iCUE Link H150i LCD | varies | 0x1B1C | TBD |
| iCUE Link H170i LCD | varies | 0x1B1C | TBD |
| iCUE Link LCD Screen Module | varies | 0x1B1C | TBD |

**Protocol Documentation**:
- liquidctl issue tracking: https://github.com/liquidctl/liquidctl/issues/633
- Corsair manual: https://www.corsair.com/us/en/explorer/diy-builder/cpu-coolers/icue-link-aio-lcd-screen-module/
- Custom LCD guide: https://www.corsair.com/us/en/explorer/diy-builder/cpu-coolers/create-a-custom-lcd-screen-for-your-aio-in-icue/
- Troubleshooting: https://www.corsair.com/us/en/explorer/diy-builder/cpu-coolers/icue-link-lcd-aio-screen-troubleshooting/

**Implementation Notes**:
- Similar protocol to previous Capellix LCD products (per SignalRGB developers)
- Different device IDs from Capellix
- Can control fan/pump speeds, LED lighting, accessories
- liquidctl looking for testers on both Linux and Windows

---

### Corsair Capellix LCD (Previous Generation)

| Model | LCD Size | VID | Notes |
|-------|----------|-----|-------|
| H100i Elite LCD | varies | 0x1B1C | Previous gen |
| H150i Elite LCD | varies | 0x1B1C | Previous gen |
| H170i Elite LCD | varies | 0x1B1C | Previous gen |

**Implementation Notes**:
- Predecessor to iCUE Link LCD
- Protocol documented in liquidctl
- Being replaced by iCUE Link products

---

## P3 - Lower Priority / More RE Needed

### HYTE Y60 LCD Mod Kit

Popular case mod LCD panel.

**Product Info**:
- Designed for HYTE Y60 case
- DIY mod kit officially sold by HYTE
- Community-developed mod made official

**References**:
- Product announcement: https://www.digitaltrends.com/computing/hyte-y60-lcd-diy-mod-kit/
- HYTE case mod guide: https://hyte.com/blog/pc-case-mod-ideas

**Implementation Notes**:
- Protocol undocumented
- May use standard USB display protocols
- Popular in enthusiast community

---

### Matrix Orbital USB LCDs

Professional-grade USB LCD displays for industrial/commercial use.

| Model | Resolution | Interface Options |
|-------|------------|-------------------|
| GLK Series | 240x128 | USB, Serial RS232, TTL, I2C, RS422 |
| GTT Series | 320x240 | USB, Serial |
| Various TFT | Various | Multiple |

**Protocol Documentation**:
- Product page: https://www.matrixorbital.com/usb

**Implementation Notes**:
- Multiple protocol options (Serial, I2C, USB)
- Professional/industrial grade
- More expensive than consumer displays
- Well-documented commercial protocol
- Resistive or capacitive touchscreen options
- HMI (Human-Machine Interface) focused

---

### DoubleSight USB LCD Monitor

Portable USB monitor requiring no video card.

| Model | Size | Resolution |
|-------|------|------------|
| DS-10U | 10" | 1024x600 |

**Product Info**:
- Amazon: https://www.amazon.com/DoubleSight-Monitor-Screen-Portable-Required/dp/B00IQOBPCO

**Implementation Notes**:
- Uses USB for both power and video
- No additional video port or cable needed
- Likely uses DisplayLink or similar USB display protocol
- Not HID-based

---

### Mnpctech Touch Screen TFT

Touch screen LCD for AIDA64/Rainmeter integration.

**References**:
- Install guide: https://www.mnpctech.com/blogs/news/install-touch-screen-lcd-pc-case-mod

**Implementation Notes**:
- Designed for AIDA64 and Rainmeter
- Touch screen capability
- PC case modding focused

---

### RoboPeak Mini USB Display

Open-source USB display project with Linux kernel drivers.

**Protocol Documentation**:
- GitHub: https://github.com/robopeak/rpusbdisp

**Implementation Notes**:
- Linux kernel driver available
- Configured via kernel menuconfig: Device Drivers -> Graphic supports -> Support for frame buffer display
- Frame buffer based

---

### Seeed Studio USB Display

USB monitor with Linux kernel driver.

**Protocol Documentation**:
- GitHub: https://github.com/Seeed-Studio/seeed-linux-usbdisp

**Implementation Notes**:
- Linux kernel driver
- From Seeed Studio (maker community focused)

---

## P4 - Research Phase / Limited Support

### Waveshare USB Monitors

| Model | Size | Resolution |
|-------|------|------------|
| 2.1" USB Monitor | 2.1" | 480x480 |
| 2.8" USB Monitor | 2.8" | 640x480 |
| 5" USB Monitor | 5" | 800x480 |
| 7" USB Monitor | 7" | 1024x600 |

**Status**: **Cannot be supported** - requires proprietary Windows firmware/software.

**Implementation Notes**:
- Managed by proprietary Windows software only
- Cannot be supported by open-source projects
- Need specific firmware from Waveshare

---

### Generic AliExpress "AIDA64" Displays

Various cheap displays sold as "AIDA64 compatible" or "sensor panels".

| Common Names | Size | Resolution |
|--------------|------|------------|
| Nvarcher IPS | 3.5" | 480x320 |
| Hitoxi USB Mini Screen | 3.5" | 480x320 |
| "PC Sensor Panel" | 3.5"-5" | Various |
| "USB Sub-Screen" | 3.5" | 480x320 |

**Product Examples**:
- AliExpress Nvarcher: https://www.aliexpress.com/item/1005001694838836.html
- Amazon Hitoxi: https://www.amazon.com/Hitoxi-Display-Monitor-Subscreen-Raspberry/dp/B0B4BGGZ74
- AliExpress search: https://www.aliexpress.com/w/wholesale-pc-sensor-panel.html

**References**:
- CNX Software article: https://www.cnx-software.com/2022/04/29/turing-smart-screen-a-low-cost-3-5-inch-usb-type-c-information-display/
- SIKAI Case setup guide: https://sikaicase.com/blogs/news/3-5inch-ips-usb-mini-screensoftware-setting

**Implementation Notes**:
- Many are rebranded Turing Smart Screens (check with turing-smart-screen-python first)
- Some use unique/proprietary protocols
- Quality and protocol varies significantly by manufacturer
- Usually $15-30 USD
- Some have RGB breathing lights
- Often labeled "UsbPCMonitor" software

---

### Appotech AX206 Photo Frame Displays

Hacked digital photo frames repurposed as USB displays.

**Implementation Notes**:
- Uses hacked firmware
- Supported by AIDA64 and lcd4linux
- Based on Appotech AX206 chipset
- Multiple manufacturers use same firmware
- Legacy/older technology

**References**:
- lcd4linux project has support

---

### ModBros MPI3508 Display

Raspberry Pi-based hardware monitor display.

| Model | Size | Resolution | Interface |
|-------|------|------------|-----------|
| MPI3508 | 3.5" | 480x320 | HDMI + GPIO |

**References**:
- ModBros guide: https://www.mod-bros.com/en/blog/b/how-to-create-a-hardware-monitor-inside-your-pc-case~37

**Implementation Notes**:
- Designed for Raspberry Pi
- Uses HDMI for display, GPIO for touch
- Requires separate Raspberry Pi
- ModBros provides custom software
- Not direct USB to PC

---

## DIY / Hobbyist Options

### Arduino HID Auxiliary Display

Custom Arduino-based HID displays using USB HID Usage Page 0x14 (Alphanumeric Display).

**Reference Implementation**:
- VID: 0x2341 (Arduino)
- PID: 0x8036
- Hackster project: https://www.hackster.io/abratchik/hid-compliant-auxiliary-lcd-display-for-pc-c0f5cd
- Hackaday article: https://hackaday.com/2012/10/23/driving-an-lcd-character-display-using-custom-hid-codes/

**Protocol Details**:
- Uses HID Usage Page 0x14 (Alphanumeric Display standard)
- Rarely seen implemented in practice
- Commands 0x10-0x23: clear display, write strings, control backlight
- Refresh rate and command delay configurable

**Implementation Notes**:
- Requires Arduino with USB capabilities
- Standard HID protocol (plug and play)
- Can connect any LCD to Arduino
- PIC microcontroller version also exists (Microchip USB stack)

---

### LCD2USB (Text LCD)

Cheap AVR-based HD44780 character LCD interface.

**Protocol Documentation**:
- GitHub: https://github.com/harbaum/LCD2USB

**Supported By**:
- lcd4linux (built-in)
- LCD Smartie (requires separate driver)
- LCDProc (built-in)

**Implementation Notes**:
- Character LCDs only (not graphical)
- Atmel AVR Mega8 CPU (8KB flash)
- Cheap and easy to obtain components
- DIY-focused project

---

### Raspberry Pi Pico GUD Display

USB display using Generic USB Display (GUD) Linux kernel protocol.

**Protocol Documentation**:
- GitHub: https://github.com/notro/gud-pico

**Implementation Notes**:
- Linux kernel GUD driver support (v5.15+)
- DIY-focused
- Default VID:PID not supported before Linux v5.15
- Uses Raspberry Pi Pico microcontroller

---

### Transparent LCD Side Panel (DIY)

DIY project using recycled monitors for transparent case side panels.

**References**:
- Instructables guide: https://www.instructables.com/DIY-Transparent-Side-Panel-From-a-Recycled-Monitor/
- Hackaday article: https://hackaday.com/2022/06/30/lcd-screen-windows-are-this-summers-hottest-case-mod/
- PC Gamer guide: https://www.pcgamesn.com/how-to-fit-screen-pc-case

**Implementation Notes**:
- Inspired by "Snowblind" PC case
- Uses DVI to HDMI through case to GPU
- Configured as dual screen in display settings
- Not USB-based (uses standard display output)
- Bezel hiding with paper, 3D printing, or acrylic

---

### Virtual Display Driver (Windows)

Software virtual display for development/testing.

**References**:
- GitHub: https://github.com/LinJiabang/virtual-display

**Implementation Notes**:
- USB/Ethernet Display driver sample for Windows
- Useful for development without hardware

---

## USB HID Reverse Engineering Resources

### General Guides
- Linux HID RE guide: https://popovicu.com/posts/how-to-reverse-engineer-usb-hid-on-linux/
- USB HID RE guide: https://santeri.pikarinen.com/pages/usb_hid_reverse_engineering/
- USB basics for RE: https://felhr85.net/2016/07/23/reverse-engineering-usb-devices-101-usb-basics/
- Wireshark USB capture: https://crescentro.se/posts/wireshark-usb/
- Protocol RE blog: https://blog.ironsm4sh.nl/post/2023-06-01-Protocol-reverse-engneering/article/

### USB Transfer Types
| Type | Use Case | Notes |
|------|----------|-------|
| Control | Device identification, configuration | Setup packets |
| Bulk | Non-time-critical data | Large transfers |
| Isochronous | Audio/video streaming | No error correction |
| Interrupt | HID devices | Guaranteed latency, error correction |

### Tools
- **Wireshark** with USBPcap (Windows) or usbmon (Linux)
- **`cat /dev/hidrawX`** for raw HID data on Linux
- **USBlyzer**, **USB Monitor Pro** (Windows commercial)
- **liquidctl** for reference implementations
- **JetBrains DotPeek** for decompiling C# vendor apps (some obfuscated)

### Linux-Specific Tips
- HID devices expose raw reports via `/dev/hidraw*`
- First byte is report ID, remaining bytes defined by descriptor
- May need to detach kernel driver: `dev.detach_kernel_driver(0)`
- Create udev rules for permissions

### Protocol Analysis Tips
1. Change one setting per capture for comparison
2. Use recognizable values (0, 100, 0xFF, RGB #AABBCC)
3. Look for magic bytes at packet start
4. Identify report ID patterns
5. Map out command bytes and their effects
6. Compare with known protocols (many devices share similar structures)

---

## Common VID/PID Reference

| Vendor | VID | Products |
|--------|-----|----------|
| Thermalright | 0x0416 | Trofeo Vision, PA120 |
| Elgato | 0x0FD9 | Stream Deck family |
| NZXT | 0x1E71 | Kraken AIOs |
| ASUS | 0x0B05 | ROG Ryujin |
| Corsair | 0x1B1C | iCUE devices |
| Turing/TURZX | 0x1CBE | Turing Smart Screen |
| Arduino | 0x2341 | Arduino boards |
| D-WAV Scientific | 0x0EEF | Touch panels (PID 0x0005) |

---

## Implementation Checklist for New Drivers

When implementing a new driver:

1. [ ] Document VID/PID
2. [ ] Determine protocol type (HID vs Serial vs other)
3. [ ] Capture sample packets with Wireshark
4. [ ] Identify frame header format
5. [ ] Determine image encoding (JPEG, BMP, RGB565, etc.)
6. [ ] Implement `ILcdDevice` interface
7. [ ] Add to `DriverRegistry`
8. [ ] Create VirtualLCD protocol for testing
9. [ ] Write unit tests
10. [ ] Test with actual hardware
11. [ ] Document in `docs/devices/{VID-PID}/`

---

## Contributing

If you have access to hardware not listed here, contributions are welcome:

1. Capture USB traffic using Wireshark
2. Document the protocol format
3. Submit a PR with driver implementation
4. Or open an issue with packet captures for others to analyze

## References

### Projects
- liquidctl project: https://github.com/liquidctl/liquidctl
- turing-smart-screen-python: https://github.com/mathoudebine/turing-smart-screen-python
- LCD2USB: https://github.com/harbaum/LCD2USB
- RoboPeak USB Display: https://github.com/robopeak/rpusbdisp
- Seeed USB Display: https://github.com/Seeed-Studio/seeed-linux-usbdisp
- GUD Pico: https://github.com/notro/gud-pico

### Documentation
- Elgato Stream Deck HID docs: https://docs.elgato.com/streamdeck/hid/
- Hackaday USB HID tag: https://hackaday.com/tag/usb-hid/
- Hackaday case mod tag: https://hackaday.com/tag/case-mod/

### Community
- Linus Tech Tips case mods: https://linustechtips.com/topic/1491200-best-guides-for-lcd-side-panel-case-mod/
- Tom's Hardware forums: https://forums.tomshardware.com/
- SlashGear best CPU coolers with LCD: https://www.slashgear.com/1734047/best-cpu-cooler-with-screen/
