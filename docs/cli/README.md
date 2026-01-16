# CLI Reference

LCDPossible provides a command-line interface for all operations.

## Usage

```bash
lcdpossible <command> [options]
```

## Commands

### Display Commands

| Command | Description |
|---------|-------------|
| `show [panels]` | Display panels on LCD (uses profile if no panels given) |
| `render [panels]` | Render panels to JPEG files for testing |
| `set-image -p <file>` | Display a static image on the LCD |
| `test-pattern` | Display a test pattern on the LCD |

### Device Commands

| Command | Description |
|---------|-------------|
| `list` | List connected LCD devices |
| `list-drivers` | List available device drivers |
| `status` | Show device and service status |

### Panel Commands

| Command | Description |
|---------|-------------|
| `list-panels` | List all available panel types |
| `help-panel <type>` | Show detailed help for a panel type |

### Profile Commands

| Command | Description |
|---------|-------------|
| `profile show` | Show current profile panels |
| `profile add <panel>` | Add a panel to the profile |
| `profile remove <index>` | Remove a panel from the profile |

### Config Commands

| Command | Description |
|---------|-------------|
| `config show` | Show current configuration |
| `config set-theme <name>` | Set the default theme |
| `config set-effect <name>` | Set the default page effect |
| `config list-themes` | List available themes |
| `config list-effects` | List available page effects |

### Service Commands

| Command | Description |
|---------|-------------|
| `serve` | Start the display service (foreground) |
| `service install` | Install as system service |
| `service start` | Start the service |
| `service stop` | Stop the service |
| `service status` | Show service status |

### Runtime Commands

These commands work when the service is running:

| Command | Description |
|---------|-------------|
| `next` | Advance to next slide |
| `previous` | Go to previous slide |
| `goto <index>` | Jump to specific slide |
| `stop` | Stop the service gracefully |

## Panel Modifiers

Apply modifiers using pipe syntax (requires quotes):

```bash
# Apply effect
lcdpossible show "cpu-info|@effect=matrix-rain"

# Apply theme
lcdpossible show "cpu-info|@theme=rgb-gaming"

# Set duration (seconds)
lcdpossible show "cpu-info|@duration=30"

# Combine modifiers
lcdpossible show "cpu-info|@effect=hologram|@theme=cyberpunk|@duration=20"
```

## Examples

```bash
# Display single panel
lcdpossible show cpu-info

# Display multiple panels as slideshow
lcdpossible show cpu-info,gpu-info,ram-info

# Display with wildcard
lcdpossible show cpu-*

# Render panel to file
lcdpossible render cpu-info -o ./output --debug

# List available panels
lcdpossible list-panels

# Get help for specific panel
lcdpossible help-panel proxmox-summary
```

---

*[Back to Documentation](../README.md)*
