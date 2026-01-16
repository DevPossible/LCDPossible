# Configuration

LCDPossible can be configured through profiles, settings files, and command-line options.

## Configuration Files

| File | Location | Purpose |
|------|----------|---------|
| `display-profile.yaml` | `~/.config/lcdpossible/` (Linux/macOS) or `%APPDATA%\LCDPossible\` (Windows) | Display profile with panels, effects, themes |
| `appsettings.json` | `/etc/lcdpossible/` or `%ProgramData%\LCDPossible\` | Service settings, integrations |

## Topics

- [Profiles](profiles.md) - Configure which panels to display
- [Settings](settings.md) - Service and application settings
- [Service Setup](service-setup.md) - Running as a system service

## Quick Configuration

### Set Default Theme

```bash
lcdpossible config set-theme cyberpunk
```

### Set Default Effect

```bash
lcdpossible config set-effect hologram
```

### View Current Configuration

```bash
lcdpossible config show
```

## Example Profile

```yaml
# display-profile.yaml
version: 1
settings:
  theme: cyberpunk
  effect: none
  slideshow:
    interval: 10
    transition: fade

panels:
  - type: cpu-info
  - type: gpu-info
  - type: ram-usage-graphic
    effect: matrix-rain
  - type: plasma
    duration: 30
```

See [Profiles](profiles.md) for complete documentation.

---

*[Back to Documentation](../README.md)*
