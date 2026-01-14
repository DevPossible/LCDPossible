---
name: create-panel
description: |
  Creates new display panels for LCDPossible LCD controller.
  Use when: creating a new panel, adding display panel, implementing panel type,
  new LCD screen content, new monitoring panel, new info panel.
  Guides through plugin vs core decision, consistent styling, and registration.
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# Creating Display Panels for LCDPossible

This skill guides the creation of new display panels for the LCDPossible LCD controller.

## Panel Type Decision

| Content Type | Base Class | Location |
|--------------|------------|----------|
| System info widgets | `WidgetPanel` | Core plugin |
| Variable item count (0-N) | `WidgetPanel` with `GetItemsAsync` | Core plugin |
| External service data | `WidgetPanel` | Separate plugin |
| Custom canvas drawing | `CanvasPanel` | Screensavers plugin |
| Animated effects | `CanvasPanel` | Screensavers plugin |

**Prefer WidgetPanel** for new info panels - it uses HTML/CSS rendering with responsive web components.

## WidgetPanel Structure

```csharp
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

public sealed class MyNewPanel : WidgetPanel
{
    public override string PanelId => "my-panel";       // URL-safe slug
    public override string DisplayName => "My Panel";   // Human-readable

    // Panel-level data (timestamp, totals, etc.)
    protected override Task<object> GetPanelDataAsync(CancellationToken ct)
    {
        return Task.FromResult<object>(new
        {
            timestamp = DateTime.Now.ToString("HH:mm:ss"),
            total_count = 42
        });
    }

    // Static widgets from panel data
    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        // Cast or use dynamic
        dynamic data = panelData;

        yield return new WidgetDefinition("lcd-stat-card", 6, 2, new
        {
            title = "Total",
            value = data.total_count.ToString(),
            status = "success"
        });
    }
}
```

## Variable Item Panels

For panels displaying 0-N items (network interfaces, VMs, sensors):

```csharp
public sealed class MyItemsPanel : WidgetPanel
{
    public override string PanelId => "my-items";
    public override string DisplayName => "My Items";

    protected override Task<object> GetPanelDataAsync(CancellationToken ct)
        => Task.FromResult<object>(new { timestamp = DateTime.Now.ToString("HH:mm:ss") });

    // Return items to display
    protected override Task<IReadOnlyList<object>> GetItemsAsync(CancellationToken ct)
    {
        var items = GetMyItems(); // Your data source
        return Task.FromResult<IReadOnlyList<object>>(items.Cast<object>().ToList());
    }

    // Customize empty state message
    protected override string GetEmptyStateMessage() => "No items found";

    // Render each item as a widget
    protected override WidgetDefinition? DefineItemWidget(object item, int index, int totalItems)
    {
        var myItem = (MyItemType)item;

        // Auto-layout based on item count (1-4 items)
        var (colSpan, rowSpan) = WidgetLayouts.GetAutoLayout(index, totalItems);

        return new WidgetDefinition("lcd-info-list", colSpan, rowSpan, new
        {
            title = myItem.Name,
            items = new[]
            {
                new { label = "Status", value = myItem.Status },
                new { label = "Value", value = myItem.Value }
            }
        });
    }
}
```

## Available Web Components

| Component | Props | Use For |
|-----------|-------|---------|
| `lcd-stat-card` | title, value, unit, subtitle, status, size | Single value display |
| `lcd-usage-bar` | value, max, label, color, orientation | Progress bars |
| `lcd-info-list` | title, subtitle, items, status | Key-value pairs |
| `lcd-temp-gauge` | value, max, label | Temperature gauges |
| `lcd-sparkline` | values, color, label | Time-series charts |
| `lcd-status-dot` | status, label | Status indicators |
| `lcd-donut` | value, max, label, color | Circular percentage |

### Component Status Values

`success` (green), `warning` (yellow), `critical` (red), `info` (blue), `neutral` (gray)

## Widget Layout

The grid is 12 columns wide. Common patterns:

| ColSpan | Meaning |
|---------|---------|
| 12 | Full width |
| 6 | Half width (2 per row) |
| 4 | Third width (3 per row) |
| 3 | Quarter width (4 per row) |

### Auto-Layout for Variable Items

Use `WidgetLayouts.GetAutoLayout(index, totalItems)` for responsive layouts:

| Items | Layout |
|-------|--------|
| 1 | Full width (12, 4) |
| 2 | Half each (6, 4) |
| 3 | First half (6, 4), second + third quarter (6, 2) |
| 4 | 2x2 grid (6, 2) |

## Registration Checklist

### 1. Add PanelTypeInfo to Plugin

In the plugin class (e.g., `CorePlugin.cs`):

```csharp
public IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes { get; } = new Dictionary<string, PanelTypeInfo>
{
    // ... existing panels ...
    ["my-panel"] = new PanelTypeInfo
    {
        TypeId = "my-panel",
        DisplayName = "My Panel",
        Description = "Description for panel list",
        Category = "System",  // System, Network, Thermal, etc.
        IsLive = true
    }
};
```

### 2. Add CreatePanel Case

In the plugin's `CreatePanel` method:

```csharp
IDisplayPanel? panel = panelTypeId.ToLowerInvariant() switch
{
    // ... existing cases ...
    "my-panel" => new MyNewPanel(),
    _ => null
};
```

### 3. Update plugin.json

Add detailed metadata for CLI help:

```json
{
  "typeId": "my-panel",
  "displayName": "My Panel",
  "description": "Brief description",
  "category": "System",
  "isLive": true,
  "dependencies": ["PuppeteerSharp"],
  "helpText": "Detailed description for help command",
  "examples": [
    {
      "command": "lcdpossible show my-panel",
      "description": "Display my panel"
    }
  ]
}
```

## Testing

```bash
# Render to file
./start-app.ps1 test my-panel --debug

# Check output
# [DEBUG] Written: C:\Users\...\my-panel.jpg (XXXX bytes)
```

## Template Syntax

WidgetPanel uses Scriban templates internally. If you need custom templates, see the `scriban-templates` skill for escaping rules.

Key rule: Web components are generated automatically - you don't write Scriban templates for standard WidgetPanels.

## Files to Create/Modify

| File | Action |
|------|--------|
| `Panels/MyNewPanel.cs` | Create - panel implementation |
| `CorePlugin.cs` or plugin class | Modify - PanelTypes + CreatePanel |
| `plugin.json` | Modify - add panel metadata |

## Visual Appeal Standards

**IMPORTANT:** All panels must comply with the visual appeal rules in `.claude/rules/panel-visual-appeal.md`.

Key requirements:
- **Container Filling:** Widgets should fill their space (no tiny text in large boxes)
- **Typography Hierarchy:** Values larger than labels (text-3xl+ for values, text-lg for labels)
- **Centering:** Content centered unless there's a reason not to
- **Color Contrast:** High contrast for LCD viewing distance (3-6 feet)
- **Theme Compliance:** Use DaisyUI classes, not hardcoded colors
- **Empty States:** Handle missing data gracefully

### Widget Sizing Guide

| Widget Content | Recommended ColSpan | Value Size |
|----------------|---------------------|------------|
| Single large value | 3-4 | text-4xl to text-5xl |
| Value with label | 4-6 | text-2xl to text-3xl |
| Info list (2-4 items) | 4-5 | text-xl values |
| Donut/gauge | 3-4 | text-3xl to text-4xl |
| Sparkline | 6-8 | N/A |

After creating a panel, validate with: `/ensure-panelvisualappeal <panel-id>`

## Common Mistakes

1. **Forgetting registration** - Panel exists but isn't in PanelTypes dictionary
2. **Wrong PanelId format** - Use lowercase with hyphens: `my-panel` not `MyPanel`
3. **Missing CreatePanel case** - Panel registered but not instantiated
4. **Not casting panelData** - Use `dynamic data = panelData;` or explicit cast
5. **Text too small** - Use `size = "large"` for stat cards in 4+ column spans
6. **Hardcoded colors** - Use `text-primary`, `text-success`, not `#00d4ff`
