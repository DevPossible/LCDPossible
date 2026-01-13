# Scriban Template Examples

Detailed examples for common patterns in LCDPossible panel templates.

## Example 1: Simple Info Panel

A panel that displays a list of info items:

```csharp
public sealed class NetworkInfoWidgetPanel : WidgetPanel
{
    public override string PanelId => "network-info-widget";
    public override string DisplayName => "Network Info (Widget)";

    protected override async Task<object> GetPanelDataAsync(CancellationToken ct)
    {
        var interfaces = await GetNetworkInterfacesAsync(ct);
        return new { interfaces };
    }

    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        dynamic data = panelData;

        foreach (var iface in data.interfaces)
        {
            yield return new WidgetDefinition(
                Component: "lcd-info-list",
                ColSpan: 6,
                RowSpan: 2,
                Props: new {
                    items = new[] {
                        new { label = "Name", value = iface.Name },
                        new { label = "IP", value = iface.IpAddress },
                        new { label = "Gateway", value = iface.Gateway }
                    }
                }
            );
        }
    }
}
```

## Example 2: Usage Bars Panel

A panel with multiple usage bars:

```csharp
protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
{
    var metrics = (SystemMetrics)panelData;

    // CPU usage bar - full width
    yield return WidgetDefinition.FullWidth("lcd-usage-bar", rowSpan: 1, props: new {
        value = metrics.CpuUsage,
        max = 100,
        label = "CPU"
    });

    // RAM usage bar - half width
    yield return WidgetDefinition.HalfWidth("lcd-usage-bar", rowSpan: 1, props: new {
        value = metrics.RamUsage,
        max = 100,
        label = "RAM"
    });

    // GPU usage bar - half width
    yield return WidgetDefinition.HalfWidth("lcd-usage-bar", rowSpan: 1, props: new {
        value = metrics.GpuUsage,
        max = 100,
        label = "GPU"
    });
}
```

## Example 3: Temperature Gauges

A panel with temperature gauges:

```csharp
protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
{
    var temps = (ThermalData)panelData;

    // CPU temperature gauge
    yield return new WidgetDefinition(
        Component: "lcd-temp-gauge",
        ColSpan: 4,
        RowSpan: 4,
        Props: new {
            value = temps.CpuTemp,
            max = 100,
            label = "CPU"
        }
    );

    // GPU temperature gauge
    yield return new WidgetDefinition(
        Component: "lcd-temp-gauge",
        ColSpan: 4,
        RowSpan: 4,
        Props: new {
            value = temps.GpuTemp,
            max = 100,
            label = "GPU"
        }
    );

    // Stat cards for additional info
    yield return new WidgetDefinition(
        Component: "lcd-stat-card",
        ColSpan: 4,
        RowSpan: 2,
        Props: new {
            title = "Fan Speed",
            value = temps.FanRpm.ToString(),
            unit = "RPM"
        }
    );
}
```

## Example 4: Custom Template Override

For advanced layouts, override `TemplateContent`:

```csharp
public class CustomLayoutPanel : HtmlPanel
{
    protected override string TemplateContent => $@"<!DOCTYPE html>
<html>
<head>
    <style>
        {{{{ colors_css }}}}

        .custom-grid {{
            display: flex;
            flex-direction: column;
            height: 100vh;
            padding: 16px;
            box-sizing: border-box;
        }}

        .header {{
            height: 60px;
            background: var(--color-accent);
        }}

        .content {{
            flex: 1;
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 16px;
        }}
    </style>
    <script src=""file://{{{{ assets_path }}}}/js/components.js""></script>
</head>
<body style=""margin:0;background:var(--color-background);"">
    <div class=""custom-grid"">
        <div class=""header"">
            <h1 style=""color:white;margin:0;padding:16px;"">{{{{ data.title }}}}</h1>
        </div>
        <div class=""content"">
            {{{{ for item in data.items }}}}
            <lcd-stat-card
                title=""{{{{ item.label }}}}""
                value=""{{{{ item.value }}}}""
                unit=""{{{{ item.unit }}}}"">
            </lcd-stat-card>
            {{{{ end }}}}
        </div>
    </div>
</body>
</html>";

    protected override Task<object> GetDataModelAsync(CancellationToken ct)
    {
        return Task.FromResult<object>(new {
            title = "Custom Panel",
            items = new[] {
                new { label = "Metric 1", value = "42", unit = "%" },
                new { label = "Metric 2", value = "128", unit = "MB" }
            }
        });
    }
}
```

## Example 5: Conditional Rendering

Using Scriban conditionals in templates:

```csharp
protected override string TemplateContent => $@"<!DOCTYPE html>
<html>
<head>
    <style>
        {{{{ colors_css }}}}
        .status {{ padding: 8px; border-radius: 4px; }}
        .status-ok {{ background: var(--color-usage-low); }}
        .status-warn {{ background: var(--color-usage-high); }}
        .status-error {{ background: var(--color-usage-critical); }}
    </style>
</head>
<body style=""margin:0;padding:16px;background:var(--color-background);"">
    {{{{ for service in data.services }}}}
    <div class=""status {{{{ if service.status == 'ok' }}}}status-ok{{{{ else if service.status == 'warn' }}}}status-warn{{{{ else }}}}status-error{{{{ end }}}}"">
        <strong>{{{{ service.name }}}}</strong>: {{{{ service.message }}}}
    </div>
    {{{{ end }}}}
</body>
</html>";
```

## Example 6: Accessing Nested Data

```scriban
{{ data.panel_data.cpu.name }}
{{ data.panel_data.cpu.cores | math.format '0' }}
{{ data.items[0].metrics.usage }}

{{ for core in data.panel_data.cpu.per_core_usage }}
  Core {{ for.index }}: {{ core }}%
{{ end }}
```

## Example 7: String Formatting

```scriban
{{ value | math.format '0.0' }}%
{{ bytes | math.format '0.00' }} GB
{{ name | string.truncate 20 }}
{{ timestamp | date.to_string '%H:%M:%S' }}
```

## Debugging Tips

### 1. Check Template Output

Add a debug panel to see rendered HTML:

```csharp
protected override async Task<Image<Rgba32>> RenderFrameAsync(...)
{
    var dataModel = await GetDataModelAsync(ct);
    var html = RenderTemplate(dataModel);

    // Log the rendered HTML for debugging
    Console.WriteLine("=== RENDERED HTML ===");
    Console.WriteLine(html);
    Console.WriteLine("=====================");

    return await base.RenderFrameAsync(width, height, ct);
}
```

### 2. Validate Brace Counts

Count your braces in the C# string:
- CSS blocks: 2 open `{{`, 2 close `}}`
- Scriban expressions: 4 open `{{{{`, 4 close `}}}}`
- C# interpolation: 1 open `{`, 1 close `}`

### 3. Test Incrementally

Start with a minimal template and add complexity:

```csharp
// Step 1: Static HTML only
protected override string TemplateContent => @"<!DOCTYPE html>
<html><body>Hello</body></html>";

// Step 2: Add CSS (with proper escaping)
protected override string TemplateContent => $@"<!DOCTYPE html>
<html><head><style>body {{ background: red; }}</style></head>
<body>Hello</body></html>";

// Step 3: Add Scriban expressions
protected override string TemplateContent => $@"<!DOCTYPE html>
<html><head><style>body {{ background: red; }}</style></head>
<body>Hello {{{{ data.name }}}}</body></html>";
```
