# Display Profiles

Profiles define what content is shown on your LCD and how it's presented. A profile is a YAML file containing a list of slides (panels or images) with timing and transition settings.

## Profile Location

Profiles are stored in the LCDPossible data directory:

| Platform | Location |
|----------|----------|
| Windows | `%LOCALAPPDATA%\LCDPossible\profiles\` |
| Linux | `~/.local/share/LCDPossible/profiles/` |
| macOS | `~/Library/Application Support/LCDPossible/profiles/` |

The default profile is `default.yaml`.

## Quick Start

### View Current Profile

```bash
lcdpossible profile show
```

### Add a Panel

```bash
# Add a panel to the current profile
lcdpossible profile append-panel cpu-info

# Add with modifiers (quotes required for pipe syntax)
lcdpossible profile append-panel "cpu-info|@effect=hologram|@theme=cyberpunk"
```

### Remove a Panel

```bash
# Remove by index (0-based)
lcdpossible profile remove-panel 2
```

### Create New Profile

```bash
# Create a new profile
lcdpossible profile new my-gaming-profile

# Switch to it
lcdpossible serve --profile my-gaming-profile
```

## Profile Structure

A profile YAML file has this structure:

```yaml
name: My Profile
description: Custom display profile

# Default settings (optional, these are the defaults)
default_update_interval: 5    # Seconds between data refreshes
default_duration: 15          # Seconds each slide is shown
default_transition: random    # Transition between slides
default_transition_duration: 1500  # Transition duration in ms
default_page_effect: none     # Page effect for all panels

# Color scheme (optional)
colors:
  background: "#0F0F19"
  accent: "#0096FF"
  # ... see Color Scheme section

# Slides to display
slides:
  - panel: basic-info

  - panel: cpu-usage-graphic
    duration: 20              # Override default duration
    update_interval: 2        # Override refresh interval

  - panel: gpu-usage-graphic
    page_effect: hologram     # Per-slide effect

  - type: image
    source: /path/to/image.png
    duration: 5
```

## Slide Options

Each slide in the `slides` list can have:

| Property | Description | Default |
|----------|-------------|---------|
| `panel` | Panel type ID (e.g., `cpu-info`, `gpu-usage-graphic`) | Required for panel slides |
| `type` | Slide type: `panel` or `image` | `panel` |
| `source` | Image path (for image slides) | - |
| `duration` | Display duration in seconds | Profile default |
| `update_interval` | Data refresh interval in seconds | Profile default |
| `background` | Background image path | - |
| `transition` | Transition effect for this slide | Profile default |
| `transition_duration` | Transition duration in ms (50-2000) | Profile default |
| `page_effect` | Page effect for this slide | Profile default |

## Transitions

Available transition effects between slides:

| Transition | Description |
|------------|-------------|
| `none` | Instant switch, no animation |
| `fade` | Fade out old, fade in new |
| `crossfade` | Smooth crossfade blend |
| `slide-left` | New slide enters from right |
| `slide-right` | New slide enters from left |
| `slide-up` | New slide enters from bottom |
| `slide-down` | New slide enters from top |
| `wipe-left` | Wipe reveal from right to left |
| `wipe-right` | Wipe reveal from left to right |
| `wipe-up` | Wipe reveal from bottom to top |
| `wipe-down` | Wipe reveal from top to bottom |
| `zoom-in` | New slide zooms in from center |
| `zoom-out` | Old slide zooms out |
| `push-left` | Push old slide off to the left |
| `push-right` | Push old slide off to the right |
| `random` | Random transition each time (default) |

## Color Scheme

Customize colors in your profile:

```yaml
colors:
  # Base colors
  background: "#0F0F19"
  background_secondary: "#282832"

  # Text colors
  text_primary: "#FFFFFF"
  text_secondary: "#B4B4C8"
  text_muted: "#6E6E82"

  # Accent colors
  accent: "#0096FF"
  accent_secondary: "#00D4AA"

  # Status colors
  success: "#32C864"
  warning: "#FFB400"
  critical: "#FF3232"
  info: "#00AAFF"

  # Usage gradient
  usage_low: "#32C864"
  usage_medium: "#0096FF"
  usage_high: "#FFB400"
  usage_critical: "#FF3232"

  # Temperature gradient
  temp_cool: "#32C864"
  temp_warm: "#FFB400"
  temp_hot: "#FF3232"

  # Chart colors (array)
  chart_colors:
    - "#0096FF"
    - "#00D4AA"
    - "#FFB400"
    - "#FF6B9D"
    - "#A855F7"
    - "#32C864"
```

## CLI Commands

### Profile Management

```bash
# List all profiles
lcdpossible profile list

# Show current profile content
lcdpossible profile show

# Show specific profile
lcdpossible profile show --name my-profile

# Create new profile
lcdpossible profile new my-profile

# Delete profile
lcdpossible profile delete my-profile

# Export profile to JSON
lcdpossible profile show --json
```

### Panel Management

```bash
# List panels in profile
lcdpossible profile list-panels

# Add panel at end
lcdpossible profile append-panel cpu-info

# Remove panel by index
lcdpossible profile remove-panel 2

# Move panel to new position
lcdpossible profile move-panel 3 1

# Set panel parameter
lcdpossible profile set-param 0 duration 30

# Get panel parameter
lcdpossible profile get-param 0 duration

# Clear panel parameters (reset to defaults)
lcdpossible profile clear-params 0
```

### Profile Defaults

```bash
# Set default duration for all panels
lcdpossible profile set-defaults --duration 20

# Set default transition
lcdpossible profile set-defaults --transition crossfade

# Set default page effect
lcdpossible profile set-defaults --effect hologram
```

## Example Profiles

### System Monitoring

```yaml
name: System Monitor
description: Real-time system monitoring

default_duration: 10
default_update_interval: 2
default_transition: fade

slides:
  - panel: cpu-status
  - panel: gpu-status
  - panel: ram-usage-graphic
  - panel: network-info
```

### Gaming Setup

```yaml
name: Gaming
description: Gaming-focused display with effects

default_duration: 15
default_page_effect: hologram
default_transition: crossfade

slides:
  - panel: cpu-thermal-graphic
    page_effect: scanlines
  - panel: gpu-thermal-graphic
    page_effect: scanlines
  - panel: system-thermal-graphic
```

### Ambient Display

```yaml
name: Ambient
description: Screensavers and ambient effects

default_duration: 60
default_transition: fade

slides:
  - panel: clock
  - panel: plasma
  - panel: starfield
  - panel: matrix-rain
```

## Using Profiles

### Start Service with Profile

```bash
# Use default profile
lcdpossible serve

# Use specific profile
lcdpossible serve --profile my-gaming-profile
```

### Runtime Commands

While the service is running:

```bash
# Reload current profile
lcdpossible profile reload

# Navigate slides
lcdpossible next
lcdpossible previous
lcdpossible goto 3
```

---

*See also: [Effects](../effects/README.md) | [Themes](../themes/README.md) | [Panels](../panels/README.md)*

*[Back to Configuration](README.md)*
