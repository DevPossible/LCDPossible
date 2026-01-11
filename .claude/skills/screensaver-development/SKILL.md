---
name: screensaver-development
description: |
  Guide for implementing LCDPossible screensavers. Use when:
  - Creating a new screensaver panel
  - Adding animated visual effects
  - Implementing game simulations (Pac-Man, Asteroids, etc.)
  - Working with delta-time animation
  - Creating demoscene effects (plasma, fire, tunnel)
  - Drawing sprites, shapes, or per-pixel effects
---

# LCDPossible Screensaver Development Guide

This skill provides patterns for implementing animated screensaver panels.

## Quick Start

All screensavers go in `src/Plugins/LCDPossible.Plugins.Screensavers/Panels/`.

```csharp
using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Screensavers.Panels;

public sealed class MyScreensaverPanel : BaseLivePanel
{
    private DateTime _lastUpdate = DateTime.UtcNow;

    public override string PanelId => "my-screensaver";
    public override string DisplayName => "My Screensaver";
    public override bool IsAnimated => true;  // IMPORTANT: Must be true

    public override Task InitializeAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(
        int width, int height, CancellationToken ct = default)
    {
        // Calculate delta time for smooth animation
        var now = DateTime.UtcNow;
        var deltaTime = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        var image = new Image<Rgba32>(width, height, new Rgba32(0, 0, 0));

        image.Mutate(ctx =>
        {
            // Your drawing code here
        });

        return Task.FromResult(image);
    }
}
```

## Registration (3 places)

### 1. ScreensaversPlugin.cs - PanelTypes Dictionary

```csharp
["my-screensaver"] = new PanelTypeInfo
{
    TypeId = "my-screensaver",
    DisplayName = "My Screensaver",
    Description = "Description here",
    Category = "Screensaver",
    IsLive = true
}
```

### 2. ScreensaversPlugin.cs - ScreensaverTypes Array

```csharp
private static readonly string[] ScreensaverTypes =
[
    "starfield", "matrix-rain", /* ... */, "my-screensaver"
];
```

### 3. ScreensaversPlugin.cs - CreateScreensaverPanel Switch

```csharp
"my-screensaver" or "myscreensaver" => new MyScreensaverPanel(),
```

### 4. plugin.json (Full Metadata Required)

```json
{
  "typeId": "my-screensaver",
  "displayName": "My Screensaver",
  "description": "Brief one-line description for list-panels output",
  "category": "Screensaver",
  "isLive": true,
  "isAnimated": true,
  "helpText": "Detailed description of the screensaver effect.\n\nFeatures:\n- Feature 1\n- Feature 2\n- Feature 3\n\nInspired by [reference if applicable].",
  "examples": [
    {
      "command": "lcdpossible show my-screensaver",
      "description": "Display the screensaver effect"
    }
  ]
}
```

### Required Metadata Fields for Screensavers
| Field | Value | Notes |
|-------|-------|-------|
| `typeId` | lowercase-with-hyphens | Unique panel identifier |
| `displayName` | Title Case | Shown in help |
| `description` | Brief text | Shown in `list-panels` output |
| `category` | `"Screensaver"` | Always use this for screensavers |
| `isLive` | `true` | Screensavers update continuously |
| `isAnimated` | `true` | Screensavers manage their own timing |
| `helpText` | Multi-line | Detailed help for `help-panel` command |
| `examples` | Array | At least one usage example |

### For Parameterized Screensavers
If your screensaver accepts arguments (like `falling-blocks:2` for 2 players):

```json
{
  "typeId": "my-screensaver",
  "prefixPattern": "my-screensaver:",
  "parameters": [
    {
      "name": "mode",
      "description": "Screensaver mode or configuration",
      "required": false,
      "defaultValue": "default",
      "exampleValues": ["option1", "option2"]
    }
  ],
  "examples": [
    {
      "command": "lcdpossible show my-screensaver:option1",
      "description": "Run with option1 mode"
    }
  ]
}
```

## Animation Patterns

### Delta-Time Movement

For objects that move at consistent speed regardless of frame rate:

```csharp
private float _x, _y;
private float _velocityX = 100f;  // pixels per second
private float _velocityY = 80f;
private DateTime _lastUpdate = DateTime.UtcNow;

public override Task<Image<Rgba32>> RenderFrameAsync(...)
{
    var now = DateTime.UtcNow;
    var deltaTime = (float)(now - _lastUpdate).TotalSeconds;
    _lastUpdate = now;

    // Movement is frame-rate independent
    _x += _velocityX * deltaTime;
    _y += _velocityY * deltaTime;
}
```

### Time-Based Effects

For cyclical animations (pulsing, rotating, oscillating):

```csharp
private DateTime _startTime = DateTime.UtcNow;

public override Task<Image<Rgba32>> RenderFrameAsync(...)
{
    var time = (float)(DateTime.UtcNow - _startTime).TotalSeconds;

    // Oscillation (0 to 1 to 0)
    var pulse = (MathF.Sin(time * 2f) + 1f) / 2f;

    // Rotation (radians)
    var rotation = time * MathF.PI;  // Half rotation per second

    // Color cycling
    var hue = (time * 0.1f) % 1f;
}
```

### Cyclic Animation (Looping)

```csharp
var cycleTime = time % 20f;  // 20-second loop

if (cycleTime < 5f)
{
    // Phase 1: 0-5 seconds
}
else if (cycleTime < 10f)
{
    // Phase 2: 5-10 seconds
}
// etc.
```

## Drawing Techniques

### Essential Imports

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;           // EllipsePolygon, PathBuilder
using SixLabors.ImageSharp.Drawing.Processing; // Fill, DrawLine
using SixLabors.ImageSharp.PixelFormats;      // Rgba32
using SixLabors.ImageSharp.Processing;        // Mutate, ProcessPixelRows
```

### Basic Shapes

```csharp
// Circle/Ellipse
ctx.Fill(color, new EllipsePolygon(centerX, centerY, radius));
ctx.Fill(color, new EllipsePolygon(centerX, centerY, radiusX, radiusY));

// Rectangle
ctx.Fill(color, new RectangleF(x, y, width, height));

// Line
ctx.DrawLine(color, thickness, new PointF(x1, y1), new PointF(x2, y2));

// Multiple connected lines
ctx.DrawLine(color, thickness,
    new PointF(x1, y1),
    new PointF(x2, y2),
    new PointF(x3, y3));
```

### Complex Shapes with PathBuilder

**CRITICAL:** Always start with `MoveTo()`, not `LineTo()`!

```csharp
var path = new PathBuilder();
path.MoveTo(new PointF(x1, y1));   // First point - MUST use MoveTo
path.LineTo(new PointF(x2, y2));   // Subsequent points
path.LineTo(new PointF(x3, y3));
path.CloseFigure();                // Connect back to start
ctx.Fill(color, path.Build());
```

### Composite Sprites (Ghost Example)

For complex sprites, combine simple shapes:

```csharp
// Ghost body = dome + rectangle + wavy bottom
ctx.Fill(bodyColor, new EllipsePolygon(x, y - size * 0.3f, size, size * 0.7f));  // Dome
ctx.Fill(bodyColor, new RectangleF(x - size, y - size * 0.3f, size * 2, size * 0.8f));  // Body

// Wavy bottom bumps
for (var i = 0; i < 4; i++)
{
    var bx = x - size + (i + 0.5f) * size * 0.5f;
    ctx.Fill(bodyColor, new EllipsePolygon(bx, y + size * 0.5f, size * 0.25f));
}

// Eyes on top
ctx.Fill(white, new EllipsePolygon(x - 4, y - size * 0.2f, 4, 5));
ctx.Fill(white, new EllipsePolygon(x + 4, y - size * 0.2f, 4, 5));
```

### Pac-Man Mouth (Animated Wedge)

```csharp
var mouthAngle = (MathF.Sin(time * 15f) + 1f) / 2f * 45f;  // 0-45 degrees
var mouthRad = mouthAngle * MathF.PI / 180f;
var baseAngle = facingLeft ? MathF.PI : 0f;

var path = new PathBuilder();
path.MoveTo(new PointF(x, y));  // Center point

var startAngle = baseAngle + mouthRad;
var endAngle = baseAngle - mouthRad + MathF.PI * 2f;

for (var a = startAngle; a < endAngle; a += 0.1f)
{
    path.LineTo(new PointF(
        x + MathF.Cos(a) * radius,
        y + MathF.Sin(a) * radius));
}
path.CloseFigure();
ctx.Fill(yellow, path.Build());
```

## Per-Pixel Effects

For plasma, fire, noise, and other demoscene effects:

```csharp
image.ProcessPixelRows(accessor =>
{
    for (var py = 0; py < height; py++)
    {
        var row = accessor.GetRowSpan(py);
        for (var px = 0; px < width; px++)
        {
            // Calculate color for this pixel
            var r = (byte)(/* calculation */);
            var g = (byte)(/* calculation */);
            var b = (byte)(/* calculation */);

            row[px] = new Rgba32(r, g, b);
        }
    }
});
```

### Performance Optimization: Render at Lower Resolution

```csharp
private const int ScaleFactor = 4;  // Render at 1/4 resolution

public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, ...)
{
    var scaledWidth = width / ScaleFactor;
    var scaledHeight = height / ScaleFactor;

    // Calculate at scaled resolution
    var buffer = new float[scaledWidth * scaledHeight];
    for (var sy = 0; sy < scaledHeight; sy++)
    {
        for (var sx = 0; sx < scaledWidth; sx++)
        {
            buffer[sy * scaledWidth + sx] = /* calculate */;
        }
    }

    // Upscale when rendering
    image.ProcessPixelRows(accessor =>
    {
        for (var py = 0; py < height; py++)
        {
            var sy = Math.Min(py / ScaleFactor, scaledHeight - 1);
            var row = accessor.GetRowSpan(py);

            for (var px = 0; px < width; px++)
            {
                var sx = Math.Min(px / ScaleFactor, scaledWidth - 1);
                var value = buffer[sy * scaledWidth + sx];
                row[px] = ValueToColor(value);
            }
        }
    });
}
```

## Common Effect Patterns

### Plasma Effect

```csharp
var value = 0f;
value += MathF.Sin((x * 8f + time * 0.5f) * MathF.PI * 2f);
value += MathF.Sin((y * 7f + time * 0.4f) * MathF.PI * 2f);
value += MathF.Sin(((x + y) * 6f + time * 0.7f) * MathF.PI * 2f);

// Circular waves from moving center
var dx = x - (0.5f + MathF.Sin(time * 0.7f) * 0.3f);
var dy = y - (0.5f + MathF.Cos(time * 0.5f) * 0.3f);
var dist = MathF.Sqrt(dx * dx + dy * dy);
value += MathF.Sin((dist * 15f - time) * MathF.PI * 2f);

value = (value / 4f + 1f) / 2f;  // Normalize to 0-1
```

### Fire Effect

```csharp
// Use a buffer smaller than output, propagate heat upward
private byte[] _fireBuffer;

// Each frame: Add heat at bottom, propagate up with cooling
for (var x = 0; x < bufferWidth; x++)
{
    _fireBuffer[(bufferHeight - 1) * bufferWidth + x] =
        (byte)_random.Next(200, 256);
}

for (var y = 0; y < bufferHeight - 1; y++)
{
    for (var x = 0; x < bufferWidth; x++)
    {
        var sum = _fireBuffer[(y + 1) * bufferWidth + Math.Max(0, x - 1)]
                + _fireBuffer[(y + 1) * bufferWidth + x]
                + _fireBuffer[(y + 1) * bufferWidth + Math.Min(bufferWidth - 1, x + 1)]
                + _fireBuffer[Math.Min(bufferHeight - 1, y + 2) * bufferWidth + x];
        _fireBuffer[y * bufferWidth + x] = (byte)Math.Max(0, sum / 4 - 2);
    }
}
```

### Starfield

```csharp
private struct Star { public float X, Y, Z; }
private Star[] _stars;

// Update: Move stars toward viewer (decrease Z)
for (var i = 0; i < _stars.Length; i++)
{
    _stars[i].Z -= speed * deltaTime;
    if (_stars[i].Z <= 0)
    {
        _stars[i] = new Star
        {
            X = _random.NextSingle() * 2 - 1,
            Y = _random.NextSingle() * 2 - 1,
            Z = 1f
        };
    }
}

// Draw: Project 3D to 2D
foreach (var star in _stars)
{
    var screenX = centerX + star.X / star.Z * centerX;
    var screenY = centerY + star.Y / star.Z * centerY;
    var brightness = (byte)(255 * (1f - star.Z));
    var size = 3f * (1f - star.Z);

    ctx.Fill(new Rgba32(brightness, brightness, brightness),
        new EllipsePolygon(screenX, screenY, size));
}
```

## Game Simulation Patterns

### Entity Management

```csharp
private readonly List<Enemy> _enemies = new();
private readonly List<Bullet> _bullets = new();

private void Update(float deltaTime)
{
    // Update backwards for safe removal
    for (var i = _enemies.Count - 1; i >= 0; i--)
    {
        var enemy = _enemies[i];
        enemy.X += enemy.Vx * deltaTime;

        if (enemy.X < 0 || enemy.X > _width)
        {
            _enemies.RemoveAt(i);
            continue;
        }

        _enemies[i] = enemy;
    }
}
```

### Collision Detection

```csharp
// Circle collision
var dx = obj1.X - obj2.X;
var dy = obj1.Y - obj2.Y;
var distance = MathF.Sqrt(dx * dx + dy * dy);
var colliding = distance < (obj1.Radius + obj2.Radius);

// Rectangle collision
var colliding = rect1.X < rect2.X + rect2.Width &&
                rect1.X + rect1.Width > rect2.X &&
                rect1.Y < rect2.Y + rect2.Height &&
                rect1.Y + rect1.Height > rect2.Y;
```

### Screen Wrapping

```csharp
_x = (_x + _width) % _width;
_y = (_y + _height) % _height;
```

### Bouncing

```csharp
if (_x < radius || _x > _width - radius) _vx = -_vx;
if (_y < radius || _y > _height - radius) _vy = -_vy;

_x = Math.Clamp(_x, radius, _width - radius);
_y = Math.Clamp(_y, radius, _height - radius);
```

## Color Techniques

### HSV to RGB

```csharp
private static Rgba32 HsvToRgb(float h, float s, float v)
{
    var hi = (int)(h * 6) % 6;
    var f = h * 6 - (int)(h * 6);
    var p = v * (1 - s);
    var q = v * (1 - f * s);
    var t = v * (1 - (1 - f) * s);

    return hi switch
    {
        0 => new Rgba32((byte)(v * 255), (byte)(t * 255), (byte)(p * 255)),
        1 => new Rgba32((byte)(q * 255), (byte)(v * 255), (byte)(p * 255)),
        2 => new Rgba32((byte)(p * 255), (byte)(v * 255), (byte)(t * 255)),
        3 => new Rgba32((byte)(p * 255), (byte)(q * 255), (byte)(v * 255)),
        4 => new Rgba32((byte)(t * 255), (byte)(p * 255), (byte)(v * 255)),
        _ => new Rgba32((byte)(v * 255), (byte)(p * 255), (byte)(q * 255))
    };
}
```

### Fire Palette

```csharp
private static Rgba32 GetFireColor(byte heat)
{
    return heat switch
    {
        < 64 => new Rgba32(heat * 4, 0, 0),           // Black to red
        < 128 => new Rgba32(255, (heat - 64) * 4, 0), // Red to yellow
        _ => new Rgba32(255, 255, (heat - 128) * 2)   // Yellow to white
    };
}
```

### Color Cycling

```csharp
var phase = value * MathF.PI * 2f + time * 0.5f;
var r = (MathF.Sin(phase) + 1f) / 2f;
var g = (MathF.Sin(phase + MathF.PI * 2f / 3f) + 1f) / 2f;
var b = (MathF.Sin(phase + MathF.PI * 4f / 3f) + 1f) / 2f;
```

## Existing Screensavers Reference

| Panel | Type | Key Techniques |
|-------|------|----------------|
| `starfield` | 3D effect | Z-depth projection, star structs |
| `matrix-rain` | Character animation | Column drops, character grid |
| `bouncing-logo` | Simple physics | Velocity, wall bouncing |
| `mystify` | Polygon animation | Trail history, connected vertices |
| `plasma` | Per-pixel | Multiple sine waves, moving centers |
| `fire` | Cellular automata | Heat buffer, upward propagation |
| `game-of-life` | Cellular automata | Neighbor counting, state buffer |
| `bubbles` | Particle system | List of bubbles, collision |
| `rain` | Particle system | Raindrops + splashes |
| `spiral` | Per-pixel | Polar coordinates, rotation |
| `clock` | Vector graphics | Angle calculation, line drawing |
| `noise` | Per-pixel | Random values, scan lines |
| `warp-tunnel` | Per-pixel | Distance from center, rings |
| `pipes` | 3D simulation | Segment list, direction changes |
| `asteroids` | Game simulation | Ship AI, bullets, collision |
| `pacman` | Game simulation | Maze, ghost AI, pathfinding |
| `mspacman-cutscene` | Animation | Phased timeline, sprite drawing |
| `missile-command` | Game simulation | Trajectories, explosions, AI defense |
