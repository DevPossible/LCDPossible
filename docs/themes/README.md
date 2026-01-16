# LCDPossible Themes

LCDPossible includes several built-in color themes designed for LCD signage viewing at 3-6 feet distance. All themes use the DaisyUI component library with OKLCH color space for accurate color reproduction.

## Quick Reference

| Theme | Category | Style | Best For |
|-------|----------|-------|----------|
| `cyberpunk` | Gamer | Neon cyan/magenta, glow effects | Gaming PCs, enthusiast builds |
| `rgb-gaming` | Gamer | Vibrant rainbow, bold colors | RGB setups, streaming rigs |
| `executive` | Corporate | Dark blue/gold, professional | Workstations, server rooms |
| `clean` | Corporate | Light mode, minimal | Bright environments, offices |

## Using Themes

### List Available Themes

```bash
# List themes with current default marked
lcdpossible config list-themes
```

### Set Default Theme

```bash
# Set theme for all panels
lcdpossible config set-theme cyberpunk
```

### Per-Panel Theme Override

```bash
# Override theme for specific panel (note: quotes required for pipe syntax)
lcdpossible show "cpu-info|@theme=executive"

# Combine with effect
lcdpossible show "cpu-info|@theme=executive|@effect=hologram"

# In profile YAML
panels:
  - type: cpu-info
    theme: executive
  - type: gpu-info
    theme: cyberpunk
```

## Theme Details

### Cyberpunk (Default)

**ID:** `cyberpunk`
**Category:** Gamer
**Color Scheme:** Dark

Neon HUD aesthetic with cyan primary, magenta secondary, and deep space black backgrounds. Features subtle scanline effects and glow animations.

| Element | Color |
|---------|-------|
| Primary | Cyan (#00ffff) |
| Secondary | Magenta (#ff00aa) |
| Accent | Electric Green (#00ff88) |
| Background | Deep Black (#050508) |
| Text | White (#ffffff) |

**Effects:**
- Glow effects enabled
- Scanline overlay (subtle)
- Gradient backgrounds

**Widget Styles:**
- Gauges: ECharts arc style with glow
- Donuts: ECharts default
- Sparklines: ECharts area fill
- Progress: ECharts animated

---

### RGB Gaming

**ID:** `rgb-gaming`
**Category:** Gamer
**Color Scheme:** Dark

Vibrant rainbow colors inspired by RGB gaming peripherals. Bold, high-contrast design optimized for gaming setups.

| Element | Color |
|---------|-------|
| Primary | Hot Pink (#ff0055) |
| Secondary | Electric Green (#00ff99) |
| Accent | Amber (#ffaa00) |
| Background | Pure Black (#0a0a0a) |
| Text | White (#ffffff) |

**Effects:**
- Glow effects enabled
- No scanlines
- Gradient accents

**Widget Styles:**
- Gauges: ECharts ring (speedometer style)
- Donuts: DaisyUI large
- Sparklines: ECharts line
- Progress: DaisyUI large

---

### Executive

**ID:** `executive`
**Category:** Corporate
**Color Scheme:** Dark

Professional dark theme with navy backgrounds and gold accents. Clean, sophisticated appearance suitable for business environments.

| Element | Color |
|---------|-------|
| Primary | Gold (#c9a227) |
| Secondary | Corporate Blue (#4a90d9) |
| Accent | Teal (#68c4af) |
| Background | Deep Navy (#0d1421) |
| Text | Light Gray (#f0f4f8) |

**Effects:**
- No glow effects
- No scanlines
- Subtle gradients

**Widget Styles:**
- Gauges: DaisyUI large
- Donuts: DaisyUI large
- Sparklines: ECharts line
- Progress: DaisyUI large

---

### Clean

**ID:** `clean`
**Category:** Corporate
**Color Scheme:** Light

Modern minimal light theme for bright environments. High contrast and readability optimized for well-lit spaces.

| Element | Color |
|---------|-------|
| Primary | Corporate Blue (#0066cc) |
| Secondary | Green (#00994d) |
| Accent | Orange (#cc6600) |
| Background | White (#ffffff) |
| Text | Dark Gray (#1a1a1a) |

**Effects:**
- No glow effects
- No scanlines
- No gradients (flat design)

**Widget Styles:**
- Gauges: DaisyUI medium
- Donuts: DaisyUI medium
- Sparklines: ECharts line
- Progress: DaisyUI medium

---

## Theme CSS Variables

All themes expose CSS variables for advanced customization:

```css
/* Color tokens */
--p: 90.5% 0.171 194.77;        /* Primary (OKLCH) */
--s: 70.2% 0.273 328.36;        /* Secondary */
--a: 87.1% 0.201 142.5;         /* Accent */
--b1: 11% 0.020 270;            /* Background base */
--bc: 92% 0.015 240;            /* Base content */

/* Status colors */
--su: /* Success */
--wa: /* Warning */
--er: /* Error */
--in: /* Info */

/* Typography scale */
--text-sm: clamp(1.125rem, 3vmin, 1.5rem);
--text-base: clamp(1.25rem, 4vmin, 1.75rem);
--text-lg: clamp(1.5rem, 5vmin, 2rem);
/* ... etc */
```

## Theme JavaScript Hooks

Themes can include JavaScript for dynamic effects. The theme JS file provides lifecycle hooks:

```javascript
window.LCDTheme = {
    // Called after DOM is ready
    onDomReady: function() { },

    // Called after transition animation
    onTransitionEnd: function() { },

    // Called before each frame render
    onBeforeRender: function() { }
};
```

The cyberpunk theme uses these hooks to add glow pulse effects and scanline overlays.

## Custom Themes

Custom themes can be created by extending the Theme class and registering with ThemeManager:

```csharp
// In plugin code
var customTheme = new Theme
{
    Id = "my-theme",
    Name = "My Custom Theme",
    Background = "#1a1a2e",
    Accent = "#e94560",
    // ... other properties
};
ThemeManager.RegisterPreset(customTheme);
```

---

*Generated by [LCDPossible](https://github.com/DevPossible/LCDPossible)*
