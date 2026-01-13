using System.Net;
using System.Text;
using System.Text.Json;
using LCDPossible.Core.Configuration;
using LCDPossible.Sdk.Controls;
using LCDPossible.Sdk.Rendering;

namespace LCDPossible.Sdk;

/// <summary>
/// Base class for panels that display widgets in a grid layout using DaisyUI components.
/// Uses a 12-column × 4-row grid optimized for 1280x480 LCD displays.
/// </summary>
/// <remarks>
/// <para>
/// WidgetPanel uses DaisyUI components rendered via Tailwind CSS. Widgets can span
/// 1-12 columns and 1-4 rows. The layout automatically adapts based on content.
/// </para>
/// <para>
/// Available DaisyUI components for widgets:
/// <list type="bullet">
///   <item><c>stat</c> - Value display with title and description</item>
///   <item><c>radial-progress</c> - Circular progress indicator</item>
///   <item><c>progress</c> - Horizontal progress bar</item>
///   <item><c>card</c> - Container with padding and shadow</item>
/// </list>
/// </para>
/// </remarks>
public abstract class WidgetPanel : HtmlPanel
{
    /// <summary>
    /// Padding inside the panel in pixels. Default is 16px.
    /// </summary>
    protected virtual int PanelPadding => 16;

    /// <summary>
    /// Gap between grid cells in pixels. Default is 16px.
    /// </summary>
    protected virtual int GridGap => 16;

    /// <summary>
    /// Number of columns in the grid. Default is 12.
    /// </summary>
    protected virtual int GridCols => 12;

    /// <summary>
    /// Number of rows in the grid. Default is 4.
    /// </summary>
    protected virtual int GridRows => 4;

    /// <summary>
    /// DaisyUI theme name. Uses the current theme when set via SetTheme(), otherwise defaults to "lcd-cyberpunk".
    /// Theme IDs are prefixed with "lcd-" (e.g., "cyberpunk" becomes "lcd-cyberpunk").
    /// </summary>
    protected virtual string DaisyTheme => CurrentThemeId != null ? $"lcd-{CurrentThemeId}" : "lcd-cyberpunk";

    /// <summary>
    /// Defines the widgets to display in the panel.
    /// Called on each refresh with the panel data.
    /// Override this for legacy widget-based panels.
    /// </summary>
    protected virtual IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        yield break;
    }

    /// <summary>
    /// Defines semantic controls to display in the panel.
    /// Called on each refresh with the panel data.
    /// Override this for new panels using semantic controls.
    /// </summary>
    /// <remarks>
    /// Controls defined here are rendered before any widgets from <see cref="DefineWidgets"/>.
    /// You can use both methods together during migration, but prefer DefineControls for new panels.
    /// </remarks>
    protected virtual IEnumerable<SemanticControl> DefineControls(object panelData)
    {
        yield break;
    }

    // Lazy-initialized renderer service for semantic controls
    private ControlRendererService? _rendererService;

    /// <summary>
    /// Provides panel-specific data for template rendering and widget definitions.
    /// Override this instead of <see cref="HtmlPanel.GetDataModelAsync"/>.
    /// </summary>
    protected abstract Task<object> GetPanelDataAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Provides items for variable-count layouts (similar to SmartLayoutPanel).
    /// Override this to return a list of items that should each get their own widget.
    /// </summary>
    protected virtual Task<IReadOnlyList<object>> GetItemsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<object>>([]);

    /// <summary>
    /// Defines a widget for a single item (for variable item counts).
    /// </summary>
    protected virtual WidgetDefinition? DefineItemWidget(object item, int index, int totalItems)
        => null;

    /// <summary>
    /// Gets the message to display when there are no items.
    /// </summary>
    protected virtual string GetEmptyStateMessage() => "No data available";

    /// <summary>
    /// Pre-built grid template using DaisyUI.
    /// </summary>
    protected sealed override string TemplateContent => DaisyGridTemplate;

    /// <summary>
    /// Combines panel data, items, and widget definitions into the data model.
    /// </summary>
    protected sealed override async Task<object> GetDataModelAsync(CancellationToken cancellationToken)
    {
        var panelData = await GetPanelDataAsync(cancellationToken);
        var items = await GetItemsAsync(cancellationToken);

        // Collect rendered widgets (both semantic controls and legacy widgets)
        var renderedWidgets = new List<object>();

        // First, render semantic controls
        foreach (var control in DefineControls(panelData))
        {
            renderedWidgets.Add(new
            {
                component = control.ControlType,
                col_span = control.ColSpan,
                row_span = control.RowSpan,
                props = (object?)null,
                html = RenderControlHtml(control)
            });
        }

        // Then, render legacy widgets
        foreach (var widget in DefineWidgets(panelData))
        {
            renderedWidgets.Add(new
            {
                component = widget.Component,
                col_span = widget.ColSpan,
                row_span = widget.RowSpan,
                props = widget.Props,
                html = RenderWidgetHtml(widget)
            });
        }

        // Render item widgets
        for (var i = 0; i < items.Count; i++)
        {
            var itemWidget = DefineItemWidget(items[i], i, items.Count);
            if (itemWidget != null)
            {
                renderedWidgets.Add(new
                {
                    component = itemWidget.Component,
                    col_span = itemWidget.ColSpan,
                    row_span = itemWidget.RowSpan,
                    props = itemWidget.Props,
                    html = RenderWidgetHtml(itemWidget)
                });
            }
        }

        // Add empty state if no widgets
        if (renderedWidgets.Count == 0)
        {
            var emptyWidget = new WidgetDefinition(
                Component: "empty-state",
                ColSpan: GridCols,
                RowSpan: GridRows,
                Props: new { message = GetEmptyStateMessage() }
            );
            renderedWidgets.Add(new
            {
                component = emptyWidget.Component,
                col_span = emptyWidget.ColSpan,
                row_span = emptyWidget.RowSpan,
                props = emptyWidget.Props,
                html = RenderWidgetHtml(emptyWidget)
            });
        }

        return new
        {
            theme = DaisyTheme,
            grid_cols = GridCols,
            grid_rows = GridRows,
            panel_data = panelData,
            items,
            widgets = renderedWidgets
        };
    }

    /// <summary>
    /// Renders a semantic control to HTML using the renderer service.
    /// </summary>
    private string RenderControlHtml(SemanticControl control)
    {
        _rendererService ??= ControlRendererService.CreateDefault();

        // Get ColorScheme from current theme if set, otherwise create default
        var colorScheme = GetCurrentColorScheme();

        var context = new ControlRenderContext(
            Colors: colorScheme,
            AssetsPath: AssetsPath,
            GridColumns: GridCols,
            GridRows: GridRows
        );

        return _rendererService.RenderControl(control, context);
    }

    /// <summary>
    /// Gets the current ColorScheme from the theme or creates a default.
    /// </summary>
    private ColorScheme GetCurrentColorScheme()
    {
        // Try to get from current theme via reflection (HtmlPanel._currentTheme is private)
        var themeField = typeof(HtmlPanel).GetField("_currentTheme",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var theme = themeField?.GetValue(this) as Theme;

        return theme?.ToColorScheme() ?? ColorScheme.CreateDefault();
    }

    private string RenderWidgetHtml(WidgetDefinition widget)
    {
        if (widget.Component == "empty-state")
        {
            var message = widget.Props?.GetType().GetProperty("message")?.GetValue(widget.Props) ?? GetEmptyStateMessage();
            return $@"<div class=""flex items-center justify-center h-full"">
    <span class=""text-3xl text-base-content/50"">{message}</span>
</div>";
        }

        // Delegate to component-specific renderers
        // Support both short names and lcd-prefixed names
        // Also support new echarts-* and daisy-* prefixed components
        return widget.Component switch
        {
            // Legacy/server-side rendered components
            "stat" or "lcd-stat-card" => RenderStat(widget.Props),
            "radial-progress" or "lcd-donut" => RenderRadialProgress(widget.Props),
            "progress-bar" or "lcd-usage-bar" => RenderProgressBar(widget.Props),
            "info-list" or "lcd-info-list" => RenderInfoList(widget.Props),
            "temp-gauge" or "lcd-temp-gauge" => RenderTempGauge(widget.Props),
            "card" => RenderCard(widget.Props),
            "sparkline" or "lcd-sparkline" => RenderSparkline(widget.Props),

            // New ECharts-based web components (client-side rendered)
            "echarts-gauge" => RenderEChartsComponent("lcd-echarts-gauge", widget.Props),
            "echarts-donut" => RenderEChartsComponent("lcd-echarts-donut", widget.Props),
            "echarts-sparkline" => RenderEChartsComponent("lcd-echarts-sparkline", widget.Props),
            "echarts-progress" => RenderEChartsComponent("lcd-echarts-progress", widget.Props),

            // New DaisyUI-based web components (client-side rendered)
            "daisy-gauge" => RenderDaisyComponent("lcd-daisy-gauge", widget.Props),
            "daisy-progress" => RenderDaisyComponent("lcd-daisy-progress", widget.Props),
            "daisy-stat" => RenderDaisyComponent("lcd-daisy-stat", widget.Props),
            "daisy-donut" => RenderDaisyComponent("lcd-daisy-donut", widget.Props),
            "daisy-sparkline" => RenderDaisyComponent("lcd-daisy-sparkline", widget.Props),
            "daisy-info-list" => RenderDaisyComponent("lcd-daisy-info-list", widget.Props),

            _ => RenderGenericCard(widget.Props)
        };
    }

    private static string RenderStat(object? props)
    {
        var json = JsonSerializer.SerializeToElement(props);
        var title = GetProp(json, "title", "");
        var value = GetProp(json, "value", "0");
        var desc = GetProp(json, "desc", "");
        var status = GetProp(json, "status", ""); // success, warning, error
        var size = GetProp(json, "size", "medium"); // small, medium, large, xlarge, hero

        var valueClass = status switch
        {
            "success" => "text-success",
            "warning" => "text-warning",
            "error" => "text-error",
            _ => "text-primary"
        };

        // Value Prominence: value should be 3-5x larger than label
        // Sizes scale for different container heights
        var (labelSize, valueSize) = size switch
        {
            "small" => ("text-sm", "text-2xl"),       // 3x ratio - compact cards
            "medium" => ("text-base", "text-3xl"),    // 3x ratio - standard cards
            "large" => ("text-lg", "text-5xl"),       // 4x ratio - 2-row cards
            "xlarge" => ("text-xl", "text-6xl"),      // 4x ratio - 3-row cards
            "hero" => ("text-xl", "text-7xl"),        // 5x ratio - 4-row hero cards
            _ => ("text-base", "text-3xl")
        };

        // Centered layout with proper vertical distribution
        return $@"<div class=""card bg-base-200 h-full"">
    <div class=""card-body p-4 flex flex-col items-center justify-center text-center"">
        <div class=""{labelSize} uppercase tracking-wider text-base-content/70"">{title}</div>
        <div class=""{valueSize} font-bold {valueClass}"">{value}</div>
        {(string.IsNullOrEmpty(desc) ? "" : $@"<div class=""text-base text-base-content/60"">{desc}</div>")}
    </div>
</div>";
    }

    private static string RenderRadialProgress(object? props)
    {
        var json = JsonSerializer.SerializeToElement(props);
        var value = GetPropFloat(json, "value", 0);
        var max = GetPropFloat(json, "max", 100);
        var label = GetProp(json, "label", "");
        var size = GetProp(json, "size", "10rem");
        var showValue = GetPropBool(json, "showValue", true);

        var percent = Math.Min(100, Math.Max(0, (value / max) * 100));
        var colorClass = GetUsageColorClass(percent);

        return $@"<div class=""flex flex-col items-center justify-center h-full gap-2"">
    {(string.IsNullOrEmpty(label) ? "" : $@"<div class=""text-xl uppercase tracking-wider text-base-content/70"">{label}</div>")}
    <div class=""radial-progress {colorClass} text-4xl font-mono"" style=""--value:{percent:F0};--size:{size};--thickness:8px;"" role=""progressbar"">
        {(showValue ? $"{percent:F0}%" : "")}
    </div>
</div>";
    }

    private static string RenderTempGauge(object? props)
    {
        var json = JsonSerializer.SerializeToElement(props);
        var value = GetPropFloat(json, "value", 0);
        var max = GetPropFloat(json, "max", 100);
        var label = GetProp(json, "label", "Temp");
        var size = GetProp(json, "size", "10rem");

        var percent = Math.Min(100, Math.Max(0, (value / max) * 100));
        var colorClass = GetTempColorClass(value);

        return $@"<div class=""flex flex-col items-center justify-center h-full gap-2"">
    <div class=""text-xl uppercase tracking-wider text-base-content/70"">{label}</div>
    <div class=""radial-progress {colorClass} text-4xl font-mono"" style=""--value:{percent:F0};--size:{size};--thickness:8px;"" role=""progressbar"">
        {value:F0}°
    </div>
</div>";
    }

    private static string RenderProgressBar(object? props)
    {
        var json = JsonSerializer.SerializeToElement(props);
        var value = GetPropFloat(json, "value", 0);
        var max = GetPropFloat(json, "max", 100);
        var label = GetProp(json, "label", "");
        var showPercent = GetPropBool(json, "showPercent", true);

        var percent = Math.Min(100, Math.Max(0, (value / max) * 100));
        var colorClass = GetUsageColorClass(percent);

        return $@"<div class=""flex flex-col justify-center h-full gap-2 p-2"">
    {(string.IsNullOrEmpty(label) ? "" : $@"<div class=""flex justify-between items-baseline"">
        <span class=""text-xl uppercase tracking-wider"">{label}</span>
        {(showPercent ? $@"<span class=""text-3xl font-mono {colorClass}"">{percent:F0}%</span>" : "")}
    </div>")}
    <progress class=""progress {colorClass} h-8"" value=""{percent:F0}"" max=""100""></progress>
</div>";
    }

    private static string RenderInfoList(object? props)
    {
        var json = JsonSerializer.SerializeToElement(props);
        var title = GetProp(json, "title", "");
        var subtitle = GetProp(json, "subtitle", "");

        var itemsHtml = new StringBuilder();
        if (json.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemsEl.EnumerateArray())
            {
                var itemLabel = GetProp(item, "label", "");
                var itemValue = GetProp(item, "value", "");
                var itemColor = GetProp(item, "color", "");

                var valueStyle = string.IsNullOrEmpty(itemColor) ? "" : $@" style=""color:{itemColor}""";
                itemsHtml.Append($@"<div class=""flex justify-between items-center py-1 border-b border-base-300 last:border-0"">
    <span class=""text-lg text-base-content/70 uppercase"">{itemLabel}</span>
    <span class=""text-xl font-mono""{valueStyle}>{itemValue}</span>
</div>");
            }
        }

        return $@"<div class=""card bg-base-200 h-full"">
    <div class=""card-body p-4"">
        {(string.IsNullOrEmpty(title) ? "" : $@"<h2 class=""card-title text-xl text-primary uppercase tracking-wider"">{title}</h2>")}
        {(string.IsNullOrEmpty(subtitle) ? "" : $@"<p class=""text-base-content/60"">{subtitle}</p>")}
        <div class=""flex flex-col gap-1 flex-1 justify-center"">
            {itemsHtml}
        </div>
    </div>
</div>";
    }

    private static string RenderCard(object? props)
    {
        var json = JsonSerializer.SerializeToElement(props);
        var title = GetProp(json, "title", "");
        var content = GetProp(json, "content", "");

        return $@"<div class=""card bg-base-200 h-full"">
    <div class=""card-body p-4"">
        {(string.IsNullOrEmpty(title) ? "" : $@"<h2 class=""card-title text-2xl text-primary"">{title}</h2>")}
        <p class=""text-xl"">{content}</p>
    </div>
</div>";
    }

    private static string RenderSparkline(object? props)
    {
        var json = JsonSerializer.SerializeToElement(props);
        var label = GetProp(json, "label", "");
        var color = GetProp(json, "color", "oklch(var(--p))");

        // Get values array
        var values = new List<float>();
        if (json.TryGetProperty("values", out var valuesEl) && valuesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in valuesEl.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number)
                    values.Add(item.GetSingle());
            }
        }

        if (values.Count == 0)
        {
            return $@"<div class=""flex items-center justify-center h-full"">
    <span class=""text-lg text-base-content/50"">No data</span>
</div>";
        }

        // Calculate SVG path for sparkline
        var maxVal = values.Max();
        var minVal = values.Min();
        var range = maxVal - minVal;
        if (range < 0.001f) range = 1f;

        var pathPoints = new StringBuilder();
        var width = 100f;
        var height = 60f;
        var padding = 5f;

        for (int i = 0; i < values.Count; i++)
        {
            var x = padding + (width - 2 * padding) * i / (values.Count - 1);
            var y = padding + (height - 2 * padding) * (1 - (values[i] - minVal) / range);
            pathPoints.Append(i == 0 ? $"M{x:F1},{y:F1}" : $" L{x:F1},{y:F1}");
        }

        // Color for stroke - pass through CSS functions (var, oklch, rgb, hsl) as-is
        var strokeColor = color.StartsWith("var(") || color.StartsWith("oklch(") ||
                          color.StartsWith("rgb(") || color.StartsWith("hsl(")
            ? color
            : $"var(--color-{color}, {color})";

        return $@"<div class=""card bg-base-200 h-full p-4 flex flex-col"">
    {(string.IsNullOrEmpty(label) ? "" : $@"<div class=""text-lg uppercase tracking-wider text-base-content/70 mb-2"">{label}</div>")}
    <div class=""flex-1 flex items-center"">
        <svg viewBox=""0 0 {width} {height}"" preserveAspectRatio=""none"" class=""w-full h-full"">
            <path d=""{pathPoints}"" fill=""none"" stroke=""{strokeColor}"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""/>
        </svg>
    </div>
    <div class=""flex justify-between text-sm text-base-content/60"">
        <span>{minVal:F0}</span>
        <span>{maxVal:F0}</span>
    </div>
</div>";
    }

    private static string RenderGenericCard(object? props)
    {
        var json = JsonSerializer.SerializeToElement(props);
        var title = GetProp(json, "title", "");
        var value = GetProp(json, "value", "");

        return $@"<div class=""card bg-base-200 h-full flex items-center justify-center"">
    <div class=""text-center"">
        {(string.IsNullOrEmpty(title) ? "" : $@"<div class=""text-xl text-base-content/70 uppercase mb-2"">{title}</div>")}
        <div class=""text-4xl font-mono text-primary"">{value}</div>
    </div>
</div>";
    }

    /// <summary>
    /// Renders an ECharts-based web component.
    /// </summary>
    private static string RenderEChartsComponent(string tagName, object? props)
    {
        var propsJson = props != null ? JsonSerializer.Serialize(props, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        }) : "{}";
        // Escape for HTML attribute
        var escapedProps = WebUtility.HtmlEncode(propsJson);
        return $@"<{tagName} class=""w-full h-full"" props=""{escapedProps}""></{tagName}>";
    }

    /// <summary>
    /// Renders a DaisyUI-based web component.
    /// </summary>
    private static string RenderDaisyComponent(string tagName, object? props)
    {
        var propsJson = props != null ? JsonSerializer.Serialize(props, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        }) : "{}";
        // Escape for HTML attribute
        var escapedProps = WebUtility.HtmlEncode(propsJson);
        return $@"<{tagName} class=""w-full h-full"" props=""{escapedProps}""></{tagName}>";
    }

    // Helper methods
    private static string GetProp(JsonElement json, string name, string defaultValue)
    {
        if (json.TryGetProperty(name, out var prop))
            return GetStringValue(prop, defaultValue);
        // Try camelCase to snake_case conversion
        var snakeName = ToSnakeCase(name);
        if (json.TryGetProperty(snakeName, out prop))
            return GetStringValue(prop, defaultValue);
        return defaultValue;
    }

    private static string GetStringValue(JsonElement prop, string defaultValue)
    {
        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? defaultValue,
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => defaultValue,
            _ => prop.GetRawText()
        };
    }

    private static float GetPropFloat(JsonElement json, string name, float defaultValue)
    {
        if (json.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
                return prop.GetSingle();
            if (prop.ValueKind == JsonValueKind.String && float.TryParse(prop.GetString(), out var f))
                return f;
        }
        return defaultValue;
    }

    private static bool GetPropBool(JsonElement json, string name, bool defaultValue)
    {
        if (json.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
            if (prop.ValueKind == JsonValueKind.String)
                return prop.GetString()?.ToLowerInvariant() == "true";
        }
        return defaultValue;
    }

    private static string ToSnakeCase(string camelCase)
    {
        if (string.IsNullOrEmpty(camelCase)) return camelCase;
        var sb = new StringBuilder();
        foreach (var c in camelCase)
        {
            if (char.IsUpper(c) && sb.Length > 0)
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static string GetUsageColorClass(float percent) => percent switch
    {
        >= 90 => "text-error",
        >= 70 => "text-warning",
        >= 50 => "text-accent",
        _ => "text-success"
    };

    private static string GetTempColorClass(float celsius) => celsius switch
    {
        >= 85 => "text-error",
        >= 70 => "text-warning",
        _ => "text-info"
    };

    private string DaisyGridTemplate => $@"<!DOCTYPE html>
<html lang=""en"" data-theme=""{{{{ data.theme }}}}"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width={{{{ target_width }}}}, height={{{{ target_height }}}}, initial-scale=1"">
    <script>{{{{ tailwind_script }}}}</script>
    <script>
        tailwind.config = {{
            theme: {{
                extend: {{
                    fontFamily: {{
                        sans: ['system-ui', '-apple-system', 'BlinkMacSystemFont', 'Segoe UI', 'Roboto', 'sans-serif'],
                        mono: ['ui-monospace', 'Cascadia Code', 'Source Code Pro', 'Menlo', 'Consolas', 'monospace'],
                    }},
                }},
            }},
            daisyui: {{
                themes: [""dark"", ""cyberpunk"", ""synthwave"", ""business""],
            }},
        }}
    </script>
    <style type=""text/tailwindcss"">
        {{{{ daisyui_css }}}}
    </style>
    <style>
        {{{{ lcd_themes_css }}}}
    </style>
    <style>
        html, body {{
            height: 100vh;
            width: 100vw;
            overflow: hidden;
            margin: 0;
            padding: 0;
        }}
        .radial-progress {{
            --thickness: 8px;
        }}
    </style>
    <!-- ECharts library for advanced charts -->
    <script>{{{{ echarts_script }}}}</script>
</head>
<body class=""bg-base-100 text-base-content p-{PanelPadding / 4}"">
    <div class=""grid grid-cols-{GridCols} grid-rows-{GridRows} gap-{GridGap / 4} h-full w-full"">
        {{{{ for widget in data.widgets }}}}
        <div class=""col-span-{{{{ widget.col_span }}}} row-span-{{{{ widget.row_span }}}}"">
            {{{{ widget.html }}}}
        </div>
        {{{{ end }}}}
    </div>
    <!-- Web Components (loaded after DOM) -->
    <script>{{{{ daisyui_components_script }}}}</script>
    <script>{{{{ echarts_components_script }}}}</script>
</body>
</html>";
}

/// <summary>
/// Defines a widget to display in a WidgetPanel.
/// </summary>
/// <param name="Component">
/// The component type: "stat", "radial-progress", "progress-bar", "info-list", "temp-gauge", "card"
/// </param>
/// <param name="ColSpan">Number of columns to span (1-6 for default 6-col grid)</param>
/// <param name="RowSpan">Number of rows to span (1-4 for default 4-row grid)</param>
/// <param name="Props">Properties for the component</param>
public record WidgetDefinition(
    string Component,
    int ColSpan,
    int RowSpan,
    object? Props = null
)
{
    /// <summary>Creates a widget spanning the full width.</summary>
    public static WidgetDefinition FullWidth(string component, int rowSpan = 1, object? props = null)
        => new(component, 6, rowSpan, props);

    /// <summary>Creates a widget spanning half width.</summary>
    public static WidgetDefinition HalfWidth(string component, int rowSpan = 1, object? props = null)
        => new(component, 3, rowSpan, props);

    /// <summary>Creates a widget spanning a third width.</summary>
    public static WidgetDefinition ThirdWidth(string component, int rowSpan = 1, object? props = null)
        => new(component, 2, rowSpan, props);
}

/// <summary>
/// Helper class for defining common widget layouts.
/// </summary>
public static class WidgetLayouts
{
    /// <summary>Gets layout for a single item (full panel).</summary>
    public static (int ColSpan, int RowSpan) GetSingleItemLayout() => (6, 4);

    /// <summary>Gets layout for 2 items (side by side).</summary>
    public static (int ColSpan, int RowSpan) GetDualItemLayout() => (3, 4);

    /// <summary>Gets layout for 3 items.</summary>
    public static (int ColSpan, int RowSpan) GetTripleItemLayout(int index)
        => index == 0 ? (3, 4) : (3, 2);

    /// <summary>Gets layout for 4 items (2x2 grid).</summary>
    public static (int ColSpan, int RowSpan) GetQuadItemLayout() => (3, 2);

    /// <summary>Gets the appropriate layout for a given item count.</summary>
    public static (int ColSpan, int RowSpan) GetAutoLayout(int itemIndex, int totalItems)
    {
        return totalItems switch
        {
            1 => GetSingleItemLayout(),
            2 => GetDualItemLayout(),
            3 => GetTripleItemLayout(itemIndex),
            _ => GetQuadItemLayout()
        };
    }
}
