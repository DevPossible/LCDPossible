---
name: plugin-development
description: |
  Guide for implementing LCDPossible plugins. Use when:
  - Creating a new plugin for LCDPossible
  - Adding panel types to an existing plugin
  - Implementing IDisplayPanel or IPanelPlugin
  - Working with plugin.json manifests
  - Creating screensavers, visualizers, or info panels
---

# LCDPossible Plugin Development Guide

This skill provides patterns and requirements for implementing LCDPossible plugins.

## Plugin Architecture Overview

LCDPossible uses a plugin-based architecture where each plugin:
1. Lives in `src/Plugins/LCDPossible.Plugins.{Name}/`
2. Implements `IPanelPlugin` interface
3. Provides one or more `IDisplayPanel` implementations
4. Declares panels in `plugin.json` manifest

## Directory Structure

```
src/Plugins/LCDPossible.Plugins.{Name}/
├── {Name}Plugin.cs              # Main plugin class (IPanelPlugin)
├── plugin.json                  # Plugin manifest
├── LCDPossible.Plugins.{Name}.csproj
└── Panels/                      # Panel implementations
    ├── {PanelName}Panel.cs
    └── ...
```

## Required Files

### 1. Project File (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\LCDPossible.Sdk\LCDPossible.Sdk.csproj" />
  </ItemGroup>

  <!-- Copy plugin.json to output -->
  <ItemGroup>
    <None Update="plugin.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

### 2. Plugin Manifest (plugin.json)

```json
{
  "id": "lcdpossible.{name}",
  "name": "Display Name",
  "version": "1.0.0",
  "author": "Author Name",
  "description": "What this plugin provides",
  "minimumSdkVersion": "1.0.0",
  "assemblyName": "LCDPossible.Plugins.{Name}.dll",
  "panelTypes": [
    {
      "typeId": "panel-id",
      "displayName": "Panel Display Name",
      "description": "What the panel shows",
      "category": "Category Name",
      "isLive": true
    }
  ]
}
```

**Panel Type Fields:**
- `typeId`: Unique identifier (lowercase, hyphens)
- `displayName`: Human-readable name
- `description`: Brief description
- `category`: Grouping category (e.g., "System", "Screensaver", "Media")
- `isLive`: `true` if panel shows real-time data
- `prefixPattern` (optional): For panels that accept parameters (e.g., `"image:"`)

### 3. Main Plugin Class

```csharp
using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;
using LCDPossible.Plugins.{Name}.Panels;

namespace LCDPossible.Plugins.{Name};

public sealed class {Name}Plugin : IPanelPlugin
{
    public string PluginId => "lcdpossible.{name}";
    public string DisplayName => "Plugin Display Name";
    public Version Version => new(1, 0, 0);
    public string Author => "Author Name";
    public Version MinimumSdkVersion => new(1, 0, 0);

    public IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes { get; } =
        new Dictionary<string, PanelTypeInfo>
    {
        ["panel-id"] = new PanelTypeInfo
        {
            TypeId = "panel-id",
            DisplayName = "Panel Name",
            Description = "Description",
            Category = "Category",
            IsLive = true
        }
    };

    public Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public IDisplayPanel? CreatePanel(string panelTypeId, PanelCreationContext context)
    {
        var typeId = panelTypeId.ToLowerInvariant();

        IDisplayPanel? panel = typeId switch
        {
            "panel-id" => new PanelNamePanel(),
            _ => null
        };

        // Apply color scheme if panel supports it
        if (panel is LCDPossible.Sdk.BaseLivePanel livePanel && context.ColorScheme != null)
        {
            livePanel.SetColorScheme(context.ColorScheme);
        }

        return panel;
    }

    public void Dispose() { }
}
```

## Panel Implementation

### Base Class: BaseLivePanel

Use `BaseLivePanel` from `LCDPossible.Sdk` for panels with:
- Color scheme support
- Common drawing utilities
- Font loading helpers

```csharp
using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.{Name}.Panels;

public sealed class {PanelName}Panel : BaseLivePanel
{
    public override string PanelId => "panel-id";
    public override string DisplayName => "Panel Name";
    public override bool IsLive => true;      // Shows real-time data
    public override bool IsAnimated => false; // Self-managed frame timing

    public override Task InitializeAsync(CancellationToken ct = default)
    {
        // One-time setup
        return Task.CompletedTask;
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(
        int width, int height, CancellationToken ct = default)
    {
        var image = new Image<Rgba32>(width, height, BackgroundColor);

        image.Mutate(ctx =>
        {
            // Drawing code here
        });

        return Task.FromResult(image);
    }
}
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `PanelId` | string | Unique identifier matching plugin registration |
| `DisplayName` | string | Human-readable name |
| `IsLive` | bool | `true` if panel shows real-time/changing data |
| `IsAnimated` | bool | `true` if panel manages its own animation timing |

### Drawing Utilities (from BaseLivePanel)

```csharp
// Colors from scheme
BackgroundColor, PrimaryTextColor, SecondaryTextColor, AccentColor

// Fonts (after InitializeAsync)
TitleFont, ValueFont, LabelFont, SmallFont

// Drawing helpers
DrawText(ctx, text, x, y, font, color, maxWidth);
DrawCenteredText(ctx, text, centerX, y, font, color);
DrawRightText(ctx, text, rightX, y, font, color);
DrawProgressBar(ctx, percentage, x, y, width, height, fillColor);
DrawVerticalBar(ctx, percentage, x, y, width, height, fillColor);

// Color helpers
GetUsageColor(percentage);      // Green → Yellow → Red
GetTemperatureColor(celsius);   // Blue → Green → Yellow → Red
```

## Animation Patterns

### Delta-Time Animation

For smooth, frame-rate independent animation:

```csharp
private DateTime _lastUpdate = DateTime.UtcNow;

public override Task<Image<Rgba32>> RenderFrameAsync(...)
{
    var now = DateTime.UtcNow;
    var deltaTime = (float)(now - _lastUpdate).TotalSeconds;
    _lastUpdate = now;

    // Use deltaTime for movement
    _position += _velocity * deltaTime;
}
```

### Time-Based Animation

For cyclical effects:

```csharp
private DateTime _startTime = DateTime.UtcNow;

public override Task<Image<Rgba32>> RenderFrameAsync(...)
{
    var time = (float)(DateTime.UtcNow - _startTime).TotalSeconds;

    // Use time for oscillation
    var pulse = MathF.Sin(time * 2f) * 0.5f + 0.5f;
}
```

## Common Drawing Operations

### Shapes (SixLabors.ImageSharp.Drawing)

```csharp
// Ellipse/Circle
ctx.Fill(color, new EllipsePolygon(centerX, centerY, radiusX, radiusY));
ctx.Fill(color, new EllipsePolygon(centerX, centerY, radius)); // Circle

// Rectangle
ctx.Fill(color, new RectangleF(x, y, width, height));

// Line
ctx.DrawLine(color, thickness, new PointF(x1, y1), new PointF(x2, y2));

// Polygon path
var path = new PathBuilder();
path.MoveTo(new PointF(x1, y1));  // MUST start with MoveTo
path.LineTo(new PointF(x2, y2));
path.LineTo(new PointF(x3, y3));
path.CloseFigure();
ctx.Fill(color, path.Build());
```

### Per-Pixel Operations

For effects like plasma, fire, noise:

```csharp
image.ProcessPixelRows(accessor =>
{
    for (var y = 0; y < height; y++)
    {
        var row = accessor.GetRowSpan(y);
        for (var x = 0; x < width; x++)
        {
            row[x] = new Rgba32(r, g, b);
        }
    }
});
```

## Panel Type Categories

| Category | Use For |
|----------|---------|
| `System` | CPU, RAM, GPU, network info |
| `Screensaver` | Animated visual effects |
| `Media` | Images, videos, GIFs |
| `Web` | HTML/website rendering |
| `Integration` | External services (Proxmox, etc.) |

## Registration Checklist

When adding a new panel:

1. [ ] Create `{PanelName}Panel.cs` in `Panels/` folder
2. [ ] Add entry to `PanelTypes` dictionary in plugin class
3. [ ] Add case to `CreatePanel` switch statement
4. [ ] Add entry to `plugin.json` panelTypes array
5. [ ] Build and verify plugin loads

## Common Issues

### PathBuilder Lines from Origin
**Problem:** Lines drawn from (0,0) to first point
**Solution:** Always call `MoveTo()` before `LineTo()`

```csharp
// WRONG
path.LineTo(new PointF(x, y));

// CORRECT
path.MoveTo(new PointF(x, y));
path.LineTo(new PointF(x2, y2));
```

### Missing EllipsePolygon
**Problem:** `EllipsePolygon` type not found
**Solution:** Add using directive:
```csharp
using SixLabors.ImageSharp.Drawing;
```

### Nullable Struct Comparison
**Problem:** Can't compare struct to null
**Solution:** Use `.HasValue` and `.Value` for nullable structs:
```csharp
if (nullableStruct.HasValue)
{
    var value = nullableStruct.Value;
}
```

## Example: Simple Screensaver

```csharp
public sealed class BouncingBallPanel : BaseLivePanel
{
    private float _x, _y, _vx = 100, _vy = 80;
    private DateTime _lastUpdate = DateTime.UtcNow;

    public override string PanelId => "bouncing-ball";
    public override string DisplayName => "Bouncing Ball";
    public override bool IsAnimated => true;

    public override Task<Image<Rgba32>> RenderFrameAsync(
        int width, int height, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var dt = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        // Update position
        _x += _vx * dt;
        _y += _vy * dt;

        // Bounce off walls
        if (_x < 20 || _x > width - 20) _vx = -_vx;
        if (_y < 20 || _y > height - 20) _vy = -_vy;

        // Clamp
        _x = Math.Clamp(_x, 20, width - 20);
        _y = Math.Clamp(_y, 20, height - 20);

        var image = new Image<Rgba32>(width, height, new Rgba32(0, 0, 32));
        image.Mutate(ctx =>
        {
            ctx.Fill(new Rgba32(255, 100, 100),
                new EllipsePolygon(_x, _y, 20));
        });

        return Task.FromResult(image);
    }
}
```
