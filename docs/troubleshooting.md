# Troubleshooting

Common issues and solutions for LCDPossible.

## Device Issues

### Device Not Found

**Symptom:** `lcdpossible list` shows no devices

**Solutions:**

1. **Check USB connection** - Ensure the LCD is properly connected
2. **Check device power** - Some LCDs need separate power
3. **Linux: Check udev rules** - Add rules for your device:
   ```bash
   # /etc/udev/rules.d/99-lcdpossible.rules
   SUBSYSTEM=="usb", ATTR{idVendor}=="0416", ATTR{idProduct}=="5302", MODE="0666"
   SUBSYSTEM=="hidraw", ATTRS{idVendor}=="0416", MODE="0666"
   ```
   Then reload: `sudo udevadm control --reload-rules && sudo udevadm trigger`

4. **Windows: Check drivers** - Device should appear in Device Manager under HID devices

### Permission Denied

**Symptom:** Error accessing device

**Linux Solution:**
```bash
# Add user to plugdev group
sudo usermod -a -G plugdev $USER
# Log out and back in
```

**Windows Solution:**
- Run as Administrator for initial setup
- Ensure no other software is using the device

## Panel Issues

### Panel Not Rendering

**Symptom:** Black screen or error message

**Solutions:**

1. **Test the panel:**
   ```bash
   lcdpossible render cpu-info --debug
   ```

2. **Check for browser issues (HTML panels):**
   - HTML/Widget panels require Chromium
   - Delete browser cache: `~/.local/share/lcdpossible/browser/` or `%LOCALAPPDATA%\LCDPossible\browser\`

3. **Check panel dependencies:**
   - Video panels require VLC/LibVLC
   - Some panels require sensor data (LibreHardwareMonitor on Windows)

### Sensor Data Missing

**Symptom:** Panels show 0% or "N/A" for CPU/GPU metrics

**Windows:**
- Run LCDPossible as Administrator (required for hardware monitoring)

**Linux:**
- Install `lm-sensors`: `sudo apt install lm-sensors && sudo sensors-detect`

## Service Issues

### Service Won't Start

**Check logs:**

```bash
# Linux (systemd)
journalctl -u lcdpossible -f

# Windows
# Check Event Viewer > Application logs
```

**Common causes:**
- Device not connected
- Port conflict (IPC port 5123)
- Missing dependencies

### Service Crashes

**Enable debug logging:**

```bash
# Edit appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

## Performance Issues

### High CPU Usage

**Solutions:**
1. Reduce panel refresh rate in profile
2. Avoid multiple animated panels
3. Use simpler effects (scanlines vs particle effects)

### Memory Growth

**Solutions:**
1. Update to latest version
2. Check for panel-specific memory leaks
3. Restart service periodically (workaround)

## Getting Help

1. **Check logs** - Enable debug logging for detailed output
2. **Search issues** - [GitHub Issues](https://github.com/DevPossible/lcd-possible/issues)
3. **Report a bug** - Include:
   - OS and version
   - LCDPossible version (`lcdpossible --version`)
   - Device info (`lcdpossible list`)
   - Relevant logs

---

*[Back to Documentation](README.md)*
