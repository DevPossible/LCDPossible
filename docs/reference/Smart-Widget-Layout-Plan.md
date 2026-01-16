# Smart Widget Layout System - Implementation Plan

## Overview

A smart layout system for panels that display variable numbers of items (network interfaces, storage devices, sensors). The system automatically determines the optimal widget layout (1-4 widgets) based on item count and scales fonts appropriately for each widget size.

## Design Principles

1. **Prefer fewer, larger widgets** - Better readability from a distance
2. **Consistent content structure** - Same information per widget, just scaled
3. **Graceful overflow** - 5+ items shows 3 widgets + overflow indicator
4. **No horizontal scrolling** - Fixed widget positions with gaps
5. **Font scaling** - Proportional font sizes for each widget tier

## Resolution-Agnostic Design

The layout system is **resolution-agnostic** - all calculations are based on the actual width/height passed to the panel, not hardcoded pixel values. This ensures the system works correctly on any LCD device.

### Scaling Philosophy

- **Widget dimensions**: Calculated as fractions of total screen size
- **Font sizes**: Scaled proportionally based on widget height
- **Gaps/padding**: Calculated as percentage of screen dimensions
- **Base reference**: Height is the primary scaling factor (readable text is height-dependent)

### Layout Patterns (Resolution Independent)

```
1 Widget (Full Panel):
┌────────────────────────────────────────────┐
│                                            │
│           FULL: 100% × 100%                │
│         (1 item = maximum detail)          │
│                                            │
└────────────────────────────────────────────┘

2 Widgets (Side by Side):
┌───────────────────┬────────────────────────┐
│                   │                        │
│   ~50% × 100%     │      ~50% × 100%       │
│                   │                        │
└───────────────────┴────────────────────────┘

3 Widgets (Left Large + Right Stack):
┌───────────────────┬────────────────────────┐
│                   │      ~50% × ~50%       │
│   ~50% × 100%     ├────────────────────────┤
│                   │      ~50% × ~50%       │
└───────────────────┴────────────────────────┘

4 Widgets (2×2 Grid):
┌───────────────────┬────────────────────────┐
│   ~50% × ~50%     │      ~50% × ~50%       │
├───────────────────┼────────────────────────┤
│   ~50% × ~50%     │      ~50% × ~50%       │
└───────────────────┴────────────────────────┘

5+ Items (Overflow Mode):
┌───────────────────┬────────────────────────┐
│   ITEM 1          │      ITEM 2            │
├───────────────────┼────────────────────────┤
│   ITEM 3          │   "+N more" indicator  │
└───────────────────┴────────────────────────┘
```

### Gap Calculation

```csharp
// Gap is 1% of the smaller dimension, clamped to reasonable bounds
int gap = Math.Clamp((int)(Math.Min(width, height) * 0.01f), 4, 20);
```

## Font Scaling Strategy

Fonts scale based on **widget height** relative to a reference height. This ensures text remains readable regardless of screen resolution.

### Base Font Sizes (Reference: 480px height)

| Font Type | Base Size | Purpose |
|-----------|-----------|---------|
| Value     | 72pt      | Large numbers (percentages) |
| Title     | 36pt      | Section headers |
| Label     | 24pt      | Field labels |
| Small     | 18pt      | Details, secondary info |

### Scaling Formula

```csharp
// Scale factor based on widget height relative to reference
float heightScale = widgetHeight / 480f;

// Widget size multiplier (Full=1.0, Half=0.85, Quarter=0.7)
float sizeMultiplier = GetSizeMultiplier(widgetSize);

// Final scale
float scale = heightScale * sizeMultiplier;

// Clamp to reasonable bounds (prevent tiny or huge fonts)
scale = Math.Clamp(scale, 0.3f, 2.0f);
```

### Size Multipliers

| Widget Size | Multiplier | Rationale |
|-------------|------------|-----------|
| Full        | 1.00       | Maximum readability |
| Half        | 0.85       | Slightly smaller for two items |
| Quarter     | 0.70       | Compact but readable |

### Example Calculations

| Screen | Widget | Height | Scale | Value Font |
|--------|--------|--------|-------|------------|
| 1280×480 | Full | 480 | 1.0 × 1.0 = 1.0 | 72pt |
| 1280×480 | Half | 480 | 1.0 × 0.85 = 0.85 | 61pt |
| 1280×480 | Quarter | 235 | 0.49 × 0.70 = 0.34 | 24pt |
| 800×480 | Full | 480 | 1.0 × 1.0 = 1.0 | 72pt |
| 800×480 | Quarter | 235 | 0.49 × 0.70 = 0.34 | 24pt |
| 320×240 | Full | 240 | 0.5 × 1.0 = 0.5 | 36pt |
| 320×240 | Quarter | 115 | 0.24 × 0.70 = 0.17 | 12pt |

## Implementation Components

### Phase 1: Core Layout Infrastructure

#### 1.1 WidgetSize Enum
```csharp
// src/LCDPossible/Panels/Layout/WidgetSize.cs
public enum WidgetSize
{
    Full,       // 1280×480 - single item, maximum detail
    Half,       // 635×480  - two items side by side
    Quarter     // 635×235  - four items in grid
}
```

#### 1.2 WidgetBounds Struct
```csharp
// src/LCDPossible/Panels/Layout/WidgetBounds.cs
public readonly struct WidgetBounds
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public WidgetSize Size { get; init; }

    public Rectangle ToRectangle() => new(X, Y, Width, Height);
}
```

#### 1.3 WidgetFontSet Class
```csharp
// src/LCDPossible/Panels/Layout/WidgetFontSet.cs
public sealed class WidgetFontSet
{
    public Font Value { get; }   // Large numbers (percentages)
    public Font Title { get; }   // Section headers
    public Font Label { get; }   // Field labels
    public Font Small { get; }   // Details, secondary info
    public float Scale { get; }  // For reference

    // Base font sizes at reference height (480px)
    private const float BaseValueSize = 72f;
    private const float BaseTitleSize = 36f;
    private const float BaseLabelSize = 24f;
    private const float BaseSmallSize = 18f;
    private const float ReferenceHeight = 480f;

    /// <summary>
    /// Creates a font set scaled for the given widget dimensions.
    /// Fonts scale proportionally based on widget height.
    /// </summary>
    public static WidgetFontSet Create(FontFamily family, WidgetBounds bounds)
    {
        // Calculate scale based on widget height relative to reference
        float heightScale = bounds.Height / ReferenceHeight;

        // Apply size multiplier based on widget size
        float sizeMultiplier = bounds.Size switch
        {
            WidgetSize.Full => 1.0f,
            WidgetSize.Half => 0.85f,
            WidgetSize.Quarter => 0.70f,
            _ => 1.0f
        };

        // Final scale with reasonable bounds
        float scale = Math.Clamp(heightScale * sizeMultiplier, 0.3f, 2.0f);

        // Minimum font sizes for readability
        float valueSize = Math.Max(BaseValueSize * scale, 14f);
        float titleSize = Math.Max(BaseTitleSize * scale, 10f);
        float labelSize = Math.Max(BaseLabelSize * scale, 8f);
        float smallSize = Math.Max(BaseSmallSize * scale, 6f);

        return new WidgetFontSet
        {
            Value = family.CreateFont(valueSize, FontStyle.Bold),
            Title = family.CreateFont(titleSize, FontStyle.Bold),
            Label = family.CreateFont(labelSize, FontStyle.Regular),
            Small = family.CreateFont(smallSize, FontStyle.Regular),
            Scale = scale
        };
    }
}
```

#### 1.4 WidgetLayout Class
```csharp
// src/LCDPossible/Panels/Layout/WidgetLayout.cs
public sealed class WidgetLayout
{
    public int TotalWidth { get; }
    public int TotalHeight { get; }
    public IReadOnlyList<WidgetBounds> Widgets { get; }
    public int DisplayedCount { get; }  // Actual items shown
    public int OverflowCount { get; }   // Items not shown (0 if all fit)

    /// <summary>
    /// Calculates widget layout based on screen dimensions and item count.
    /// All calculations are proportional - no hardcoded pixel values.
    /// </summary>
    public static WidgetLayout Calculate(int width, int height, int itemCount)
    {
        // Gap is 1% of smaller dimension, clamped to reasonable bounds
        int gap = Math.Clamp((int)(Math.Min(width, height) * 0.01f), 4, 20);

        var widgets = new List<WidgetBounds>();
        int displayedCount = Math.Min(itemCount, 4);
        int overflowCount = Math.Max(0, itemCount - 3); // Show 3 + overflow if 4+

        switch (itemCount)
        {
            case 0:
                break;

            case 1:
                // Full panel
                widgets.Add(new WidgetBounds
                {
                    X = 0, Y = 0,
                    Width = width, Height = height,
                    Size = WidgetSize.Full
                });
                break;

            case 2:
                // Side by side
                int halfWidth = (width - gap) / 2;
                widgets.Add(new WidgetBounds
                {
                    X = 0, Y = 0,
                    Width = halfWidth, Height = height,
                    Size = WidgetSize.Half
                });
                widgets.Add(new WidgetBounds
                {
                    X = halfWidth + gap, Y = 0,
                    Width = width - halfWidth - gap, Height = height,
                    Size = WidgetSize.Half
                });
                break;

            case 3:
                // Left large + right stack
                int leftWidth = (width - gap) / 2;
                int rightWidth = width - leftWidth - gap;
                int topHeight = (height - gap) / 2;
                int bottomHeight = height - topHeight - gap;

                widgets.Add(new WidgetBounds
                {
                    X = 0, Y = 0,
                    Width = leftWidth, Height = height,
                    Size = WidgetSize.Half
                });
                widgets.Add(new WidgetBounds
                {
                    X = leftWidth + gap, Y = 0,
                    Width = rightWidth, Height = topHeight,
                    Size = WidgetSize.Quarter
                });
                widgets.Add(new WidgetBounds
                {
                    X = leftWidth + gap, Y = topHeight + gap,
                    Width = rightWidth, Height = bottomHeight,
                    Size = WidgetSize.Quarter
                });
                break;

            default: // 4 or more
                // 2x2 grid (3 items + overflow indicator for 5+)
                int colWidth = (width - gap) / 2;
                int rowHeight = (height - gap) / 2;

                widgets.Add(new WidgetBounds { X = 0, Y = 0, Width = colWidth, Height = rowHeight, Size = WidgetSize.Quarter });
                widgets.Add(new WidgetBounds { X = colWidth + gap, Y = 0, Width = width - colWidth - gap, Height = rowHeight, Size = WidgetSize.Quarter });
                widgets.Add(new WidgetBounds { X = 0, Y = rowHeight + gap, Width = colWidth, Height = height - rowHeight - gap, Size = WidgetSize.Quarter });
                widgets.Add(new WidgetBounds { X = colWidth + gap, Y = rowHeight + gap, Width = width - colWidth - gap, Height = height - rowHeight - gap, Size = WidgetSize.Quarter });

                displayedCount = itemCount > 4 ? 3 : 4; // 4th slot is overflow if 5+
                overflowCount = itemCount > 4 ? itemCount - 3 : 0;
                break;
        }

        return new WidgetLayout(width, height, widgets, displayedCount, overflowCount);
    }
}
```

#### 1.5 WidgetRenderContext Class
```csharp
// src/LCDPossible/Panels/Layout/WidgetRenderContext.cs
public sealed class WidgetRenderContext
{
    public WidgetBounds Bounds { get; }
    public WidgetFontSet Fonts { get; }
    public ResolvedColorScheme Colors { get; }
    public int Index { get; }           // 0-based widget index
    public bool IsOverflowWidget { get; }
    public int OverflowCount { get; }   // Items not shown

    // Helper methods for positioning within widget
    public PointF Center => new(Bounds.X + Bounds.Width / 2f, Bounds.Y + Bounds.Height / 2f);
    public int ContentX => Bounds.X + Padding;
    public int ContentY => Bounds.Y + Padding;
    public int ContentWidth => Bounds.Width - Padding * 2;
    public int ContentHeight => Bounds.Height - Padding * 2;

    private const int Padding = 15;
}
```

### Phase 2: Base Panel Class

#### 2.1 SmartLayoutPanel Abstract Class
```csharp
// src/LCDPossible/Panels/SmartLayoutPanel.cs
public abstract class SmartLayoutPanel<TItem> : BaseLivePanel
{
    // Abstract: Get items to display
    protected abstract Task<IReadOnlyList<TItem>> GetItemsAsync(CancellationToken ct);

    // Abstract: Render single item into widget area
    protected abstract void RenderWidget(
        IImageProcessingContext ctx,
        WidgetRenderContext widget,
        TItem item);

    // Virtual: Render when no items available (default shows "No items" message)
    protected virtual void RenderEmptyState(
        IImageProcessingContext ctx,
        int width, int height)
    {
        if (!FontsLoaded || TitleFont == null) return;

        var message = GetEmptyStateMessage();
        DrawCenteredText(ctx, message, width / 2f, height / 2f - 20,
                        TitleFont, Colors.TextMuted);
    }

    // Virtual: Get message for empty state (override to customize)
    protected virtual string GetEmptyStateMessage() => "No items available";

    // Virtual: Render overflow indicator (default implementation provided)
    protected virtual void RenderOverflowWidget(
        IImageProcessingContext ctx,
        WidgetRenderContext widget)
    {
        // Draw centered "+N more" message
        var message = $"+{widget.OverflowCount} more";
        DrawCenteredText(ctx, message, widget.Center.X, widget.Center.Y,
                        widget.Fonts.Title, Colors.TextSecondary);
    }

    // Template method - handles layout calculation and delegation
    public sealed override async Task<Image<Rgba32>> RenderFrameAsync(
        int width, int height, CancellationToken ct)
    {
        var items = await GetItemsAsync(ct);
        var image = CreateBaseImage(width, height);

        image.Mutate(ctx =>
        {
            // Handle empty state
            if (items.Count == 0)
            {
                RenderEmptyState(ctx, width, height);
                DrawTimestamp(ctx, width, height);
                return;
            }

            // Calculate layout for items
            var layout = WidgetLayout.Calculate(width, height, items.Count);

            // Render item widgets
            for (int i = 0; i < layout.DisplayedCount; i++)
            {
                var widgetCtx = CreateWidgetContext(layout.Widgets[i], i, layout);
                RenderWidget(ctx, widgetCtx, items[i]);
            }

            // Render overflow widget if needed
            if (layout.OverflowCount > 0)
            {
                var overflowCtx = CreateOverflowContext(layout);
                RenderOverflowWidget(ctx, overflowCtx);
            }

            DrawTimestamp(ctx, width, height);
        });

        return image;
    }
}
```

### Phase 3: Concrete Panel Implementations

#### 3.1 NetworkInfoPanel
```csharp
// src/LCDPossible/Panels/NetworkPanels.cs
public sealed class NetworkInfoPanel : SmartLayoutPanel<NetworkInterfaceInfo>
{
    public override string PanelId => "network-info";
    public override string DisplayName => "Network Interfaces";

    protected override async Task<IReadOnlyList<NetworkInterfaceInfo>> GetItemsAsync(CancellationToken ct)
    {
        // Get active network interfaces from system
        return await _provider.GetNetworkInterfacesAsync(ct);
    }

    protected override void RenderWidget(
        IImageProcessingContext ctx,
        WidgetRenderContext widget,
        NetworkInterfaceInfo iface)
    {
        // Interface name (title)
        DrawText(ctx, iface.Name, widget.ContentX, widget.ContentY,
                 widget.Fonts.Title, widget.Colors.Accent);

        // IP Address (large value)
        DrawCenteredText(ctx, iface.IpAddress, widget.Center.X,
                         widget.Bounds.Y + 80, widget.Fonts.Value,
                         widget.Colors.TextPrimary);

        // Speed/Status
        DrawText(ctx, $"{iface.Speed} Mbps", widget.ContentX,
                 widget.ContentY + 120, widget.Fonts.Label,
                 widget.Colors.TextSecondary);

        // Traffic (RX/TX)
        DrawText(ctx, $"↓ {iface.RxRate}  ↑ {iface.TxRate}",
                 widget.ContentX, widget.ContentY + 150,
                 widget.Fonts.Small, widget.Colors.TextMuted);
    }
}
```

#### 3.2 StorageInfoPanel
```csharp
// src/LCDPossible/Panels/StoragePanels.cs
public sealed class StorageInfoPanel : SmartLayoutPanel<StorageDriveInfo>
{
    public override string PanelId => "storage-info";
    public override string DisplayName => "Storage Drives";

    protected override void RenderWidget(
        IImageProcessingContext ctx,
        WidgetRenderContext widget,
        StorageDriveInfo drive)
    {
        // Drive label (C:, D:, /dev/sda1)
        DrawText(ctx, drive.Label, widget.ContentX, widget.ContentY,
                 widget.Fonts.Title, widget.Colors.Accent);

        // Usage percentage (large)
        DrawCenteredText(ctx, $"{drive.UsagePercent:F0}%", widget.Center.X,
                         widget.Center.Y - 20, widget.Fonts.Value,
                         widget.Colors.GetUsageColor(drive.UsagePercent));

        // Used/Total
        DrawText(ctx, $"{drive.UsedGb:F0}/{drive.TotalGb:F0} GB",
                 widget.ContentX, widget.ContentY + 140,
                 widget.Fonts.Label, widget.Colors.TextSecondary);

        // Progress bar
        var barY = widget.Bounds.Y + widget.Bounds.Height - 50;
        DrawProgressBar(ctx, drive.UsagePercent, widget.ContentX, barY,
                       widget.ContentWidth, 30);
    }
}
```

#### 3.3 SensorsInfoPanel
```csharp
// src/LCDPossible/Panels/SensorsPanels.cs
public sealed class SensorsInfoPanel : SmartLayoutPanel<SensorInfo>
{
    public override string PanelId => "sensors-info";
    public override string DisplayName => "Temperature Sensors";

    protected override void RenderWidget(
        IImageProcessingContext ctx,
        WidgetRenderContext widget,
        SensorInfo sensor)
    {
        // Sensor name
        DrawText(ctx, sensor.Name, widget.ContentX, widget.ContentY,
                 widget.Fonts.Title, widget.Colors.Accent);

        // Temperature (large)
        DrawCenteredText(ctx, $"{sensor.TemperatureCelsius:F0}°C",
                         widget.Center.X, widget.Center.Y,
                         widget.Fonts.Value,
                         widget.Colors.GetTemperatureColor(sensor.TemperatureCelsius));

        // Min/Max range
        DrawText(ctx, $"Range: {sensor.MinTemp:F0}° - {sensor.MaxTemp:F0}°",
                 widget.ContentX, widget.Bounds.Y + widget.Bounds.Height - 40,
                 widget.Fonts.Small, widget.Colors.TextMuted);
    }
}
```

### Phase 4: System Info Provider Extensions

#### 4.1 ISystemInfoProvider Interface Extensions
```csharp
// Add to ISystemInfoProvider or create separate interfaces
public interface INetworkInfoProvider
{
    Task<IReadOnlyList<NetworkInterfaceInfo>> GetNetworkInterfacesAsync(CancellationToken ct);
}

public interface IStorageInfoProvider
{
    Task<IReadOnlyList<StorageDriveInfo>> GetStorageDrivesAsync(CancellationToken ct);
}

public interface ISensorInfoProvider
{
    Task<IReadOnlyList<SensorInfo>> GetSensorsAsync(CancellationToken ct);
}
```

#### 4.2 Data Models
```csharp
public sealed record NetworkInterfaceInfo(
    string Name,
    string IpAddress,
    string MacAddress,
    int SpeedMbps,
    string Status,
    string RxRate,
    string TxRate);

public sealed record StorageDriveInfo(
    string Label,
    string MountPoint,
    float TotalGb,
    float UsedGb,
    float UsagePercent,
    string FileSystem);

public sealed record SensorInfo(
    string Name,
    string Category,
    float TemperatureCelsius,
    float? MinTemp,
    float? MaxTemp);
```

### Phase 5: Panel Registration

#### 5.1 Update PanelFactory
```csharp
// Add to panel type registry
{ "network-info", new PanelMetadata("Network Interfaces", "System", true, false) },
{ "storage-info", new PanelMetadata("Storage Drives", "System", true, false) },
{ "sensors-info", new PanelMetadata("Temperature Sensors", "System", true, false) },
```

## Panels That Would Use This System

### New Panels (Implement)
| Panel ID | Description | Item Type |
|----------|-------------|-----------|
| `network-info` | Network interfaces | NetworkInterfaceInfo |
| `storage-info` | Storage drives | StorageDriveInfo |
| `sensors-info` | Temperature sensors | SensorInfo |
| `docker-info` | Docker containers | ContainerInfo |
| `services-info` | System services | ServiceInfo |

### Existing Panels (Could Migrate Later)
| Panel ID | Current Approach | Migration Benefit |
|----------|------------------|-------------------|
| `proxmox-vms` | Table with rows | Better scaling for 1-4 VMs |
| `basic-info` | 3-column fixed | Could adapt to 1-3 components |

### Panels That Do NOT Need This
| Category | Examples | Reason |
|----------|----------|--------|
| Single-value | cpu-usage-text, ram-usage-text | Always one item |
| Graphical | cpu-usage-graphic, gpu-usage-graphic | Full-screen visualizations |
| Media | animated-gif, video, web | Content fills screen |
| Screensavers | All screensaver panels | Full-screen effects |

## File Structure

```
src/LCDPossible/Panels/
├── Layout/
│   ├── WidgetSize.cs
│   ├── WidgetBounds.cs
│   ├── WidgetFontSet.cs
│   ├── WidgetLayout.cs
│   └── WidgetRenderContext.cs
├── SmartLayoutPanel.cs
├── NetworkPanels.cs
├── StoragePanels.cs
├── SensorsPanels.cs
└── ... (existing panels unchanged)
```

## Implementation Order

### Step 1: Core Layout Infrastructure
1. Create `Layout/` directory
2. Implement `WidgetSize.cs`
3. Implement `WidgetBounds.cs`
4. Implement `WidgetFontSet.cs`
5. Implement `WidgetLayout.cs`
6. Implement `WidgetRenderContext.cs`

### Step 2: Base Panel Class
1. Implement `SmartLayoutPanel<T>` abstract class
2. Add default overflow widget rendering
3. Unit tests for layout calculations

### Step 3: First Concrete Panel
1. Implement `NetworkPanels.cs` with `NetworkInfoPanel`
2. Add `INetworkInfoProvider` interface
3. Implement Windows provider using `System.Net.NetworkInformation`
4. Register in `PanelFactory`
5. End-to-end testing

### Step 4: Additional Panels
1. Implement `StoragePanels.cs`
2. Implement `SensorsPanels.cs`
3. Add corresponding provider interfaces and implementations

### Step 5: Documentation & Polish
1. Update CLAUDE.md with new panel types
2. Add usage examples
3. Consider configuration options (overflow behavior, item priority)

## Future Enhancements

1. **Configurable item priority** - Let users choose which items to show when overflow occurs
2. **Alternative layouts** - Horizontal stack, 1+3 layout for 4 items
3. **Widget borders/separators** - Visual distinction between widgets
4. **Animation on layout change** - Smooth transitions when item count changes
5. **Plugin support** - Allow plugins to provide SmartLayoutPanel implementations

## Testing Strategy

### Unit Tests
- `WidgetLayout.Calculate()` returns correct bounds for 1-5+ items
- `WidgetFontSet.Create()` returns proportionally scaled fonts
- `SmartLayoutPanel` correctly delegates to abstract methods

### Integration Tests
- Panels render correctly with 1, 2, 3, 4, 5+ items
- Font scaling is visually appropriate at each tier
- Overflow indicator displays correct count

### Visual Testing
- Manual verification on actual LCD hardware
- Screenshot comparison for regression testing
