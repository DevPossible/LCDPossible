# Settings

LCDPossible can be configured through command-line arguments, environment variables, and configuration files.

## Configuration File

The main configuration file is `appsettings.json`, located in the application directory.

### Configuration Locations

| Platform | Location |
|----------|----------|
| Windows | `%PROGRAMFILES%\LCDPossible\appsettings.json` |
| Linux | `/etc/lcdpossible/appsettings.json` or `~/.config/lcdpossible/appsettings.json` |
| macOS | `~/Library/Application Support/LCDPossible/appsettings.json` |

### View Current Configuration

```bash
lcdpossible config show
```

### Configuration Path

```bash
lcdpossible config path
```

## General Settings

```json
{
  "LCDPossible": {
    "General": {
      "TargetFrameRate": 30,
      "AutoStart": true,
      "ThemesDirectory": "themes",
      "DefaultTheme": "cyberpunk",
      "JpegQuality": 95,
      "DefaultPanelUpdateIntervalSeconds": 5,
      "DefaultPanelDurationSeconds": 15
    }
  }
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `TargetFrameRate` | Frame rate for animated content | 30 |
| `AutoStart` | Start display on service startup | true |
| `ThemesDirectory` | Directory containing theme files | "themes" |
| `DefaultTheme` | Default theme for panels | "cyberpunk" |
| `JpegQuality` | JPEG encoding quality (1-100) | 95 |
| `DefaultPanelUpdateIntervalSeconds` | Data refresh interval | 5 |
| `DefaultPanelDurationSeconds` | Default slide duration | 15 |

## Theme Settings

### CLI Commands

```bash
# List available themes
lcdpossible config list-themes

# Set default theme
lcdpossible config set-theme cyberpunk

# Get current theme
lcdpossible config show | grep -i theme
```

### Available Themes

| Theme | Description |
|-------|-------------|
| `cyberpunk` | Neon cyan/magenta, dark background (default) |
| `rgb-gaming` | Vibrant rainbow colors, bold |
| `executive` | Dark blue/gold, professional |
| `clean` | Light mode, minimal |

See [Themes](../themes/README.md) for detailed theme documentation.

## Page Effect Settings

### CLI Commands

```bash
# List available effects
lcdpossible config list-effects

# Set default page effect
lcdpossible config set-effect hologram

# Disable effects
lcdpossible config set-effect none
```

See [Effects](../effects/README.md) for available effects.

## Per-Device Settings

Configure individual devices in `appsettings.json`:

```json
{
  "LCDPossible": {
    "Devices": {
      "0416:5302": {
        "Mode": "slideshow",
        "Brightness": 100,
        "Orientation": 0,
        "Theme": "cyberpunk"
      }
    }
  }
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `Mode` | Display mode | "slideshow" |
| `Brightness` | Screen brightness (0-100) | 100 |
| `Orientation` | Rotation in degrees (0, 90, 180, 270) | 0 |
| `Theme` | Theme override for this device | - |
| `Panel` | Single panel (for "panel" mode) | - |
| `Slideshow` | Slideshow config string | - |
| `ImagePath` | Static image (for "static" mode) | - |
| `AnimationPath` | Animation file (for "animation" mode) | - |

### Display Modes

| Mode | Description |
|------|-------------|
| `slideshow` | Cycle through profile panels (default) |
| `panel` | Single live panel |
| `static` | Static image |
| `animation` | Animated GIF or video |
| `clock` | Clock display |
| `off` | Display off |

## Proxmox Integration

Configure Proxmox VE connection for `proxmox-summary` and `proxmox-vms` panels:

```json
{
  "LCDPossible": {
    "Proxmox": {
      "Enabled": true,
      "ApiUrl": "https://proxmox.local:8006",
      "TokenId": "user@pam!lcdpossible",
      "TokenSecret": "your-token-secret",
      "IgnoreSslErrors": false,
      "PollingIntervalSeconds": 5,
      "ShowVms": true,
      "ShowContainers": true,
      "ShowAlerts": true,
      "MaxDisplayItems": 10
    }
  }
}
```

### CLI Commands

```bash
# Configure Proxmox
lcdpossible config set-proxmox \
  --api-url https://proxmox.local:8006 \
  --token-id "user@pam!lcdpossible" \
  --token-secret "your-token-secret"

# Validate connection
lcdpossible config validate-proxmox

# Disable Proxmox
lcdpossible config set-proxmox --enabled false
```

### Proxmox Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `Enabled` | Enable Proxmox integration | false |
| `ApiUrl` | Proxmox API URL | - |
| `TokenId` | API token ID (user@realm!tokenid) | - |
| `TokenSecret` | API token secret | - |
| `IgnoreSslErrors` | Skip SSL verification | false |
| `PollingIntervalSeconds` | Metrics fetch interval | 5 |
| `ShowVms` | Show VM status | true |
| `ShowContainers` | Show container status | true |
| `ShowAlerts` | Show cluster alerts | true |
| `MaxDisplayItems` | Max items per category | 10 |

## Environment Variables

Override configuration with environment variables:

```bash
# General settings
export LCDPOSSIBLE__GENERAL__TARGETFRAMERATE=60
export LCDPOSSIBLE__GENERAL__DEFAULTTHEME=executive

# Proxmox settings
export LCDPOSSIBLE__PROXMOX__ENABLED=true
export LCDPOSSIBLE__PROXMOX__APIURL=https://proxmox.local:8006
export LCDPOSSIBLE__PROXMOX__TOKENID=user@pam!token
export LCDPOSSIBLE__PROXMOX__TOKENSECRET=secret
```

Note: Use double underscore (`__`) as separator for nested settings.

## Command-Line Options

### Global Options

| Option | Description |
|--------|-------------|
| `--debug` | Enable debug logging |
| `--verbose` | Enable verbose output |
| `--config <path>` | Use alternative config file |
| `--profile <name>` | Use specific profile |

### Display Options

| Option | Description |
|--------|-------------|
| `--brightness <0-100>` | Set brightness |
| `--theme <name>` | Override theme |
| `--effect <name>` | Override page effect |

## Configuration Precedence

Settings are applied in this order (later overrides earlier):

1. Default values
2. `appsettings.json`
3. Environment variables
4. Command-line arguments
5. Profile settings
6. Per-panel modifiers (e.g., `|@theme=cyberpunk`)

---

*See also: [Profiles](profiles.md) | [Service Setup](service-setup.md)*

*[Back to Configuration](README.md)*
