# Creating Panels

This guide covers how to create custom display panels for LCDPossible.

## Panel Base Classes

Choose the appropriate base class based on your panel's content:

| Base Class | Use Case | Examples |
|------------|----------|----------|
| `WidgetPanel` | Grid-based layouts with web components | System info panels, dashboards |
| `HtmlPanel` | Custom HTML templates | Custom layouts, complex designs |
| `CanvasPanel` | Direct pixel drawing | Screensavers, effects, visualizers |

## Quick Start: WidgetPanel

The easiest way to create a system information panel:

```csharp
using LCDPossible.Sdk;

public sealed class MyStatusPanel : WidgetPanel
{
    public override string PanelId => "my-status";
    public override string DisplayName => "My Status Panel";
    public override bool IsLive => true;  // Shows real-time data

    // Provide data for the panel
    protected override async Task<object> GetPanelDataAsync(CancellationToken ct)
    {
        return new
        {
            title = "My App",
            status = "Running",
            uptime = TimeSpan.FromHours(24).ToString(@"d\.hh\:mm"),
            cpuUsage = 45.2,
            memoryUsed = 8.5,
            memoryTotal = 16.0
        };
    }

    // Define widgets using the data
    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        dynamic data = panelData;

        // Title spanning full width
        yield return new WidgetDefinition("lcd-stat-card", 12, 1, new
        {
            title = data.title,
            value = data.status,
            status = "success"
        });

        // Two half-width cards
        yield return new WidgetDefinition("lcd-stat-card", 6, 1, new
        {
            title = "Uptime",
            value = data.uptime
        });

        yield return new WidgetDefinition("lcd-usage-bar", 6, 1, new
        {
            label = "CPU",
            value = data.cpuUsage,
            showPercent = true
        });

        // Memory gauge
        yield return new WidgetDefinition("lcd-donut", 4, 2, new
        {
            value = data.memoryUsed,
            max = data.memoryTotal,
            label = "Memory",
            unit = "GB"
        });
    }
}
```

## WidgetPanel Components

### Available Web Components

| Component | Purpose | Key Props |
|-----------|---------|-----------|
| `lcd-stat-card` | Display a value with title | `title`, `value`, `unit`, `status`, `size` |
| `lcd-usage-bar` | Progress bar | `value`, `max`, `label`, `showPercent` |
| `lcd-donut` | Circular percentage | `value`, `max`, `label`, `color` |
| `lcd-temp-gauge` | Temperature donut | `value`, `max`, `label` |
| `lcd-info-list` | Label/value pairs | `items: [{label, value, color}]` |
| `lcd-sparkline` | Mini line chart | `values`, `label`, `color` |
| `lcd-status-dot` | Status indicator | `status`, `label` |

### Widget Layout

WidgetPanel uses a 12-column, 4-row grid:

```csharp
// WidgetDefinition(component, colSpan, rowSpan, props)
new WidgetDefinition("lcd-stat-card", 12, 1, props)  // Full width
new WidgetDefinition("lcd-stat-card", 6, 1, props)   // Half width
new WidgetDefinition("lcd-stat-card", 4, 2, props)   // Third width, tall
new WidgetDefinition("lcd-donut", 3, 2, props)       // Quarter width
```

Helper methods:
```csharp
WidgetDefinition.FullWidth("lcd-stat-card", 1, props)    // 12 cols
WidgetDefinition.HalfWidth("lcd-stat-card", 1, props)    // 6 cols
WidgetDefinition.ThirdWidth("lcd-stat-card", 1, props)   // 4 cols
```

### Size Variants

The `lcd-stat-card` component supports size variants:

```csharp
new { title = "CPU", value = "47%", size = "small" }   // text-xl
new { title = "CPU", value = "47%", size = "medium" }  // text-2xl (default)
new { title = "CPU", value = "47%", size = "large" }   // text-4xl
```

### Status Colors

Use semantic status values for automatic coloring:

```csharp
new { status = "success" }   // Green
new { status = "warning" }   // Yellow/Orange
new { status = "error" }     // Red
new { status = "info" }      // Blue
```

## CanvasPanel for Screensavers

For animations and direct pixel drawing:

```csharp
using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

public sealed class MyScreensaver : CanvasPanel
{
    public override string PanelId => "my-screensaver";
    public override string DisplayName => "My Screensaver";
    public override bool IsAnimated => true;

    public override Task<Image<Rgba32>> RenderFrameAsync(
        int width, int height, CancellationToken ct)
    {
        // Update timing (provides ElapsedSeconds, DeltaSeconds)
        UpdateTiming();

        // Create base image with background color
        var image = CreateBaseImage(width, height);

        image.Mutate(ctx =>
        {
            // Draw animated content using ImageSharp
            // Use ElapsedSeconds for animation timing
            var x = (int)(Math.Sin(ElapsedSeconds) * 100 + width / 2);
            var y = (int)(Math.Cos(ElapsedSeconds) * 100 + height / 2);

            // Draw shapes, text, etc.
            DrawCenteredText(ctx, "Hello!", width / 2, height / 2);
        });

        return Task.FromResult(image);
    }
}
```

### CanvasPanel Utilities

| Property/Method | Description |
|-----------------|-------------|
| `ElapsedSeconds` | Total time since panel started |
| `DeltaSeconds` | Time since last frame |
| `BackgroundColor` | Panel background color |
| `PrimaryTextColor` | Primary text color |
| `AccentColor` | Accent/highlight color |
| `CreateBaseImage()` | Create image with background |
| `DrawText()` | Draw text at position |
| `DrawCenteredText()` | Draw centered text |
| `DrawProgressBar()` | Draw horizontal progress bar |
| `DrawVerticalBar()` | Draw vertical bar |
| `GetUsageColor()` | Color for usage percentages |
| `GetTemperatureColor()` | Color for temperatures |

## HtmlPanel for Custom Layouts

For complete control over HTML rendering:

```csharp
using LCDPossible.Sdk;

public sealed class MyHtmlPanel : HtmlPanel
{
    public override string PanelId => "my-html";
    public override string DisplayName => "My HTML Panel";

    // Option 1: Inline template
    protected override string TemplateContent => @"
        <div class=""flex flex-col items-center justify-center h-full"">
            <h1 class=""text-5xl text-primary"">{{ data.title }}</h1>
            <p class=""text-2xl text-secondary"">{{ data.subtitle }}</p>
        </div>
    ";

    // Option 2: External template file
    // protected override string TemplatePath => "templates/my-panel.html";

    protected override async Task<object> GetDataModelAsync(CancellationToken ct)
    {
        return new
        {
            title = "Welcome",
            subtitle = "LCDPossible"
        };
    }
}
```

### Template Variables

Available in Scriban templates:

| Variable | Description |
|----------|-------------|
| `{{ data }}` | Your data model from `GetDataModelAsync()` |
| `{{ assets_path }}` | Path to html_assets folder |
| `{{ colors }}` | Color scheme object |
| `{{ colors_css }}` | CSS variables for colors |

### Base HTML Structure

Templates are wrapped with base HTML that includes:
- Tailwind CSS
- DaisyUI components
- ECharts for charts
- LCD web components
- Theme CSS
- Color scheme variables

## Panel Registration

### In a Plugin

```csharp
public class MyPlugin : IPanelPlugin
{
    public string PluginId => "my-plugin";
    public string DisplayName => "My Panels";

    public IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes =>
        new Dictionary<string, PanelTypeInfo>
        {
            ["my-status"] = new PanelTypeInfo
            {
                TypeId = "my-status",
                DisplayName = "My Status",
                Description = "Shows custom status information",
                Category = "Custom",
                IsLive = true,
                IsAnimated = false
            },
            ["my-screensaver"] = new PanelTypeInfo
            {
                TypeId = "my-screensaver",
                DisplayName = "My Screensaver",
                Category = "Screensaver",
                IsLive = false,
                IsAnimated = true
            }
        };

    public IDisplayPanel? CreatePanel(string typeId, PanelCreationContext context)
    {
        return typeId switch
        {
            "my-status" => new MyStatusPanel(),
            "my-screensaver" => new MyScreensaver(),
            _ => null
        };
    }
}
```

### Plugin Manifest (plugin.json)

```json
{
  "id": "my-plugin",
  "type": "panels",
  "name": "My Custom Panels",
  "version": "1.0.0",
  "description": "Custom panels for LCDPossible",
  "assemblyName": "MyPlugin.dll",
  "panelTypes": [
    {
      "typeId": "my-status",
      "displayName": "My Status",
      "description": "Shows custom status information",
      "category": "Custom",
      "isLive": true,
      "isAnimated": false
    },
    {
      "typeId": "my-screensaver",
      "displayName": "My Screensaver",
      "category": "Screensaver",
      "isLive": false,
      "isAnimated": true
    }
  ]
}
```

## Panel Naming Conventions

Panel IDs should describe content, not implementation:

| Good | Bad |
|------|-----|
| `cpu-info` | `cpu-widget` |
| `network-status` | `network-panel` |
| `system-thermal` | `thermal-widget` |

Pattern: `{subject}-{content-type}[-{variant}]`

- **subject**: What it shows (cpu, gpu, network)
- **content-type**: Type of info (info, status, usage)
- **variant**: Presentation style (text, graphic)

## Testing Panels

### Render to File

```bash
# Render your panel to a JPEG file
lcdpossible render my-status --debug

# With wait time for initialization
lcdpossible render my-status -w 2
```

### Debug Output

```bash
# See detailed panel output
lcdpossible show my-status --debug
```

## Best Practices

1. **Use appropriate base class**
   - `WidgetPanel` for dashboards and info displays
   - `CanvasPanel` for animations and effects
   - `HtmlPanel` only when needed for custom layouts

2. **Handle missing data gracefully**
   - Return placeholder values (---, N/A) instead of null
   - Show empty state messages for no data

3. **Respect color scheme**
   - Use theme colors, not hardcoded values
   - Use semantic status colors

4. **Optimize for LCD viewing**
   - Large text (readable from 3-6 feet)
   - High contrast
   - Fill available space

5. **Test with render command**
   - Verify panels render without errors
   - Check visual appearance before deployment

---

*See also: [Plugin Development](README.md) | [SDK Reference](sdk-reference.md)*

*[Back to Plugins](README.md)*
