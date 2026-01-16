# Display Panels

LCDPossible includes a variety of display panels organized by category.

## Panel Categories

### [System Monitoring](system/)

Hardware monitoring panels for CPU, GPU, RAM, and network.

| Panel | Description |
|-------|-------------|
| [cpu-info](system/cpu-info.md) | CPU model and specifications |
| [cpu-status](system/cpu-status.md) | CPU dashboard with usage, temp, sparkline |
| [cpu-usage-text](system/cpu-usage-text.md) | CPU usage as text |
| [cpu-usage-graphic](system/cpu-usage-graphic.md) | CPU usage with visual bars |
| [cpu-thermal-graphic](system/cpu-thermal-graphic.md) | CPU temperature gauge |
| [gpu-info](system/gpu-info.md) | GPU model and specifications |
| [gpu-usage-text](system/gpu-usage-text.md) | GPU usage as text |
| [gpu-usage-graphic](system/gpu-usage-graphic.md) | GPU usage with visual bars |
| [gpu-thermal-graphic](system/gpu-thermal-graphic.md) | GPU temperature gauge |
| [ram-info](system/ram-info.md) | RAM specifications |
| [ram-usage-text](system/ram-usage-text.md) | RAM usage as text |
| [ram-usage-graphic](system/ram-usage-graphic.md) | RAM usage with visual bars |
| [network-info](system/network-info.md) | Network interface information |
| [system-thermal-graphic](system/system-thermal-graphic.md) | Combined CPU/GPU temperature |
| [basic-info](system/basic-info.md) | System hostname, OS, uptime |
| [basic-usage-text](system/basic-usage-text.md) | Basic system usage summary |

### [Media Panels](media/)

Video, images, and web content.

| Panel | Description |
|-------|-------------|
| [animated-gif](media/animated-gif.md) | Animated GIF from file or URL |
| [image-sequence](media/image-sequence.md) | Folder of images as animation |
| [video](media/video.md) | Video file, URL, or YouTube |
| [html](media/html.md) | Local HTML file |
| [web](media/web.md) | Live website rendering |

### [Screensavers](screensavers/)

Animated visual effects and games.

| Panel | Description |
|-------|-------------|
| [screensaver](screensavers/screensaver.md) | Random screensaver or cycle all |
| [plasma](screensavers/plasma.md) | Classic demoscene plasma |
| [matrix-rain](screensavers/matrix-rain.md) | Digital rain effect |
| [starfield](screensavers/starfield.md) | Classic starfield warp |
| [fire](screensavers/fire.md) | Demoscene fire effect |
| [warp-tunnel](screensavers/warp-tunnel.md) | Colorful warp tunnel |
| [clock](screensavers/clock.md) | Analog clock |
| [pipes](screensavers/pipes.md) | 3D pipes screensaver |
| [mystify](screensavers/mystify.md) | Bouncing polygons |
| [bubbles](screensavers/bubbles.md) | Floating bubbles |
| [rain](screensavers/rain.md) | Raindrops with splashes |
| [noise](screensavers/noise.md) | TV static effect |
| [spiral](screensavers/spiral.md) | Hypnotic spiral |
| [game-of-life](screensavers/game-of-life.md) | Conway's Game of Life |
| [asteroids](screensavers/asteroids.md) | Asteroids game simulation |
| [falling-blocks](screensavers/falling-blocks.md) | Tetris-style blocks |
| [missile-command](screensavers/missile-command.md) | Missile defense game |
| [bouncing-logo](screensavers/bouncing-logo.md) | DVD-style bouncing text |

### [Integrations](integrations/)

External service integrations.

| Panel | Description |
|-------|-------------|
| [proxmox-summary](integrations/proxmox-summary.md) | Proxmox cluster overview |
| [proxmox-vms](integrations/proxmox-vms.md) | Proxmox VM/container list |

## Using Panels

### Display a Panel

```bash
lcdpossible show cpu-info
```

### Display Multiple Panels (Slideshow)

```bash
lcdpossible show cpu-info,gpu-info,ram-info
```

### Use Wildcards

```bash
lcdpossible show cpu-*        # All CPU panels
lcdpossible show *-graphic    # All graphic panels
```

### Apply Modifiers

```bash
# With effect
lcdpossible show "cpu-info|@effect=matrix-rain"

# With theme
lcdpossible show "cpu-info|@theme=rgb-gaming"

# With duration (seconds)
lcdpossible show "cpu-info|@duration=30"
```

## Panel Help

Get detailed help for any panel:

```bash
lcdpossible help-panel proxmox-summary
```

---

*[Back to Documentation](../README.md)*
