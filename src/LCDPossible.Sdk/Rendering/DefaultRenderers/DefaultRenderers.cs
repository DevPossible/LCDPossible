using System.Text;
using LCDPossible.Sdk.Controls;

namespace LCDPossible.Sdk.Rendering;

/// <summary>
/// Provides all default control renderers.
/// </summary>
public static class DefaultRenderers
{
    /// <summary>
    /// Gets all default renderers as a collection.
    /// </summary>
    public static IEnumerable<IControlRenderer> GetAll()
    {
        yield return new SingleValueRenderer();
        yield return new GaugeRenderer();
        yield return new ProgressBarRenderer();
        yield return new InfoListRenderer();
        yield return new HistoricalSeriesRenderer();
        yield return new CurrentStatusSeriesRenderer();
        yield return new ToggleRenderer();
        yield return new StatusIndicatorRenderer();
    }
}

/// <summary>
/// Default renderer for SingleValueControl.
/// </summary>
public class SingleValueRenderer : IControlRenderer
{
    public string ControlType => "single_value";

    public string Render(SemanticControl control, ControlRenderContext context)
    {
        var c = (SingleValueControl)control;

        var sizeClass = c.Size switch
        {
            ValueSize.Small => "lcd-stat-card-small",
            ValueSize.Medium => "lcd-stat-card-medium",
            ValueSize.Large => "lcd-stat-card-large",
            ValueSize.XLarge => "lcd-stat-card-xlarge",
            ValueSize.Hero => "lcd-stat-card-hero",
            _ => "lcd-stat-card-large"
        };

        var (labelSize, valueSize) = c.Size switch
        {
            ValueSize.Small => ("text-sm", "text-2xl"),
            ValueSize.Medium => ("text-base", "text-3xl"),
            ValueSize.Large => ("text-lg", "text-5xl"),
            ValueSize.XLarge => ("text-xl", "text-6xl"),
            ValueSize.Hero => ("text-xl", "text-7xl"),
            _ => ("text-base", "text-3xl")
        };

        var statusClass = c.Status switch
        {
            StatusLevel.Info => "text-info",
            StatusLevel.Success => "text-success",
            StatusLevel.Warning => "text-warning",
            StatusLevel.Error => "text-error",
            _ => "text-primary"
        };

        var valueText = string.IsNullOrEmpty(c.Unit) ? c.Value : $"{c.Value}{c.Unit}";
        var subtitleHtml = string.IsNullOrEmpty(c.Subtitle)
            ? ""
            : $@"<div class=""text-base text-base-content/60"">{c.Subtitle}</div>";

        return $@"<div class=""card bg-base-200 h-full {sizeClass}"">
    <div class=""card-body p-4 flex flex-col items-center justify-center text-center"">
        <div class=""{labelSize} uppercase tracking-wider text-base-content/70"">{c.Label}</div>
        <div class=""{valueSize} font-bold {statusClass}"">{valueText}</div>
        {subtitleHtml}
    </div>
</div>";
    }
}

/// <summary>
/// Default renderer for GaugeControl.
/// </summary>
public class GaugeRenderer : IControlRenderer
{
    public string ControlType => "gauge";

    public string Render(SemanticControl control, ControlRenderContext context)
    {
        var c = (GaugeControl)control;

        var percent = Math.Min(100, Math.Max(0, (c.Value - c.Min) / (c.Max - c.Min) * 100));
        var colorClass = GetColorClass(percent, c.Style);

        var displayValue = c.Style == GaugeStyle.Temperature
            ? $"{c.Value:F0}°{c.Unit ?? ""}"
            : $"{percent:F0}%";

        var labelHtml = string.IsNullOrEmpty(c.Label)
            ? ""
            : $@"<div class=""text-xl uppercase tracking-wider text-base-content/70"">{c.Label}</div>";

        return $@"<div class=""flex flex-col items-center justify-center h-full gap-2"">
    {labelHtml}
    <div class=""radial-progress {colorClass} text-4xl font-mono"" style=""--value:{percent:F0};--size:10rem;--thickness:8px;"" role=""progressbar"">
        {displayValue}
    </div>
</div>";
    }

    private static string GetColorClass(double percent, GaugeStyle style)
    {
        if (style == GaugeStyle.Temperature)
        {
            return percent switch
            {
                >= 85 => "text-error",
                >= 70 => "text-warning",
                _ => "text-info"
            };
        }

        return percent switch
        {
            >= 90 => "text-error",
            >= 70 => "text-warning",
            >= 50 => "text-accent",
            _ => "text-success"
        };
    }
}

/// <summary>
/// Default renderer for ProgressBarControl.
/// </summary>
public class ProgressBarRenderer : IControlRenderer
{
    public string ControlType => "progress_bar";

    public string Render(SemanticControl control, ControlRenderContext context)
    {
        var c = (ProgressBarControl)control;

        var percent = Math.Min(100, Math.Max(0, c.Value / c.Max * 100));
        var colorClass = c.Status switch
        {
            StatusLevel.Info => "progress-info",
            StatusLevel.Success => "progress-success",
            StatusLevel.Warning => "progress-warning",
            StatusLevel.Error => "progress-error",
            _ => GetUsageColorClass(percent)
        };

        var labelHtml = "";
        if (!string.IsNullOrEmpty(c.Label))
        {
            var percentHtml = c.ShowPercent
                ? $@"<span class=""text-3xl font-mono {GetTextColorClass(percent)}"">{percent:F0}%</span>"
                : "";

            labelHtml = $@"<div class=""flex justify-between items-baseline"">
        <span class=""text-xl uppercase tracking-wider"">{c.Label}</span>
        {percentHtml}
    </div>";
        }

        var orientationClass = c.Orientation == BarOrientation.Vertical
            ? "w-8 h-full"
            : "h-8 w-full";

        return $@"<div class=""flex flex-col justify-center h-full gap-2 p-2"">
    {labelHtml}
    <progress class=""progress {colorClass} {orientationClass}"" value=""{percent:F0}"" max=""100""></progress>
</div>";
    }

    private static string GetUsageColorClass(double percent) => percent switch
    {
        >= 90 => "progress-error",
        >= 70 => "progress-warning",
        >= 50 => "progress-accent",
        _ => "progress-success"
    };

    private static string GetTextColorClass(double percent) => percent switch
    {
        >= 90 => "text-error",
        >= 70 => "text-warning",
        >= 50 => "text-accent",
        _ => "text-success"
    };
}

/// <summary>
/// Default renderer for InfoListControl.
/// </summary>
public class InfoListRenderer : IControlRenderer
{
    public string ControlType => "info_list";

    public string Render(SemanticControl control, ControlRenderContext context)
    {
        var c = (InfoListControl)control;

        var itemsHtml = new StringBuilder();
        foreach (var item in c.Items)
        {
            var valueClass = item.Status switch
            {
                StatusLevel.Info => "text-info",
                StatusLevel.Success => "text-success",
                StatusLevel.Warning => "text-warning",
                StatusLevel.Error => "text-error",
                _ => ""
            };

            itemsHtml.AppendLine($@"<div class=""flex justify-between items-center py-1 border-b border-base-300 last:border-0"">
    <span class=""text-lg text-base-content/70 uppercase"">{item.Label}</span>
    <span class=""text-xl font-mono {valueClass}"">{item.Value}</span>
</div>");
        }

        var titleHtml = string.IsNullOrEmpty(c.Title)
            ? ""
            : $@"<h2 class=""card-title text-xl text-primary uppercase tracking-wider"">{c.Title}</h2>";

        return $@"<div class=""card bg-base-200 h-full"">
    <div class=""card-body p-4"">
        {titleHtml}
        <div class=""flex flex-col gap-1 flex-1 justify-center"">
            {itemsHtml}
        </div>
    </div>
</div>";
    }
}

/// <summary>
/// Default renderer for HistoricalSeriesControl (sparkline).
/// </summary>
public class HistoricalSeriesRenderer : IControlRenderer
{
    public string ControlType => "historical_series";

    public string Render(SemanticControl control, ControlRenderContext context)
    {
        var c = (HistoricalSeriesControl)control;

        if (c.Values.Count == 0)
        {
            return @"<div class=""flex items-center justify-center h-full"">
    <span class=""text-lg text-base-content/50"">No data</span>
</div>";
        }

        var minVal = c.Min ?? c.Values.Min();
        var maxVal = c.Max ?? c.Values.Max();
        var range = maxVal - minVal;
        if (range < 0.001) range = 1;

        var pathPoints = new StringBuilder();
        var areaPoints = new StringBuilder();
        const float width = 100f;
        const float height = 60f;
        const float padding = 5f;

        for (var i = 0; i < c.Values.Count; i++)
        {
            var x = padding + (width - 2 * padding) * i / Math.Max(1, c.Values.Count - 1);
            var y = padding + (height - 2 * padding) * (1 - (c.Values[i] - minVal) / range);
            pathPoints.Append(i == 0 ? $"M{x:F1},{y:F1}" : $" L{x:F1},{y:F1}");

            if (c.Style == SparklineStyle.Area)
            {
                areaPoints.Append(i == 0 ? $"M{padding:F1},{height - padding:F1} L{x:F1},{y:F1}" : $" L{x:F1},{y:F1}");
            }
        }

        if (c.Style == SparklineStyle.Area)
        {
            areaPoints.Append($" L{width - padding:F1},{height - padding:F1} Z");
        }

        var strokeColor = "oklch(var(--p))";
        var fillHtml = c.Style == SparklineStyle.Area
            ? $@"<path d=""{areaPoints}"" fill=""{strokeColor}"" fill-opacity=""0.2""/>"
            : "";

        var labelHtml = string.IsNullOrEmpty(c.Label)
            ? ""
            : $@"<div class=""text-lg uppercase tracking-wider text-base-content/70 mb-2"">{c.Label}</div>";

        return $@"<div class=""card bg-base-200 h-full p-4 flex flex-col"">
    {labelHtml}
    <div class=""flex-1 flex items-center"">
        <svg viewBox=""0 0 {width} {height}"" preserveAspectRatio=""none"" class=""w-full h-full"">
            {fillHtml}
            <path d=""{pathPoints}"" fill=""none"" stroke=""{strokeColor}"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""/>
        </svg>
    </div>
    <div class=""flex justify-between text-sm text-base-content/60"">
        <span>{minVal:F0}</span>
        <span>{maxVal:F0}</span>
    </div>
</div>";
    }
}

/// <summary>
/// Default renderer for CurrentStatusSeriesControl.
/// </summary>
public class CurrentStatusSeriesRenderer : IControlRenderer
{
    public string ControlType => "current_status_series";

    public string Render(SemanticControl control, ControlRenderContext context)
    {
        var c = (CurrentStatusSeriesControl)control;

        if (c.Items.Count == 0)
        {
            return @"<div class=""flex items-center justify-center h-full"">
    <span class=""text-lg text-base-content/50"">No items</span>
</div>";
        }

        var gridClass = c.Layout switch
        {
            SeriesLayout.List => "flex flex-col gap-2",
            SeriesLayout.Compact => "flex flex-wrap gap-2",
            _ => $"grid grid-cols-{Math.Min(c.Items.Count, 4)} gap-2"
        };

        var itemsHtml = new StringBuilder();
        foreach (var item in c.Items)
        {
            var percent = Math.Min(100, Math.Max(0, item.Value / item.Max * 100));
            var colorClass = item.Status != StatusLevel.Normal
                ? GetStatusColorClass(item.Status)
                : GetUsageColorClass(percent);

            if (c.Layout == SeriesLayout.Compact)
            {
                itemsHtml.AppendLine($@"<div class=""badge badge-lg {colorClass}"">{item.Label}: {percent:F0}%</div>");
            }
            else
            {
                itemsHtml.AppendLine($@"<div class=""flex flex-col items-center gap-1"">
    <span class=""text-sm text-base-content/70"">{item.Label}</span>
    <div class=""radial-progress {colorClass} text-lg"" style=""--value:{percent:F0};--size:3rem;--thickness:4px;"">
        {percent:F0}%
    </div>
</div>");
            }
        }

        var titleHtml = string.IsNullOrEmpty(c.Title)
            ? ""
            : $@"<div class=""text-lg text-primary uppercase tracking-wider mb-2"">{c.Title}</div>";

        return $@"<div class=""card bg-base-200 h-full p-4"">
    {titleHtml}
    <div class=""{gridClass} flex-1 items-center justify-center"">
        {itemsHtml}
    </div>
</div>";
    }

    private static string GetUsageColorClass(double percent) => percent switch
    {
        >= 90 => "text-error",
        >= 70 => "text-warning",
        >= 50 => "text-accent",
        _ => "text-success"
    };

    private static string GetStatusColorClass(StatusLevel status) => status switch
    {
        StatusLevel.Info => "text-info",
        StatusLevel.Success => "text-success",
        StatusLevel.Warning => "text-warning",
        StatusLevel.Error => "text-error",
        _ => "text-primary"
    };
}

/// <summary>
/// Default renderer for ToggleControl.
/// </summary>
public class ToggleRenderer : IControlRenderer
{
    public string ControlType => "toggle";

    public string Render(SemanticControl control, ControlRenderContext context)
    {
        var c = (ToggleControl)control;

        var statusText = c.Value ? c.TrueText : c.FalseText;
        var statusClass = c.Value ? "badge-success" : "badge-error";
        var dotClass = c.Value ? "bg-success" : "bg-error";

        return c.Style switch
        {
            ToggleStyle.Switch => RenderSwitch(c, statusClass),
            ToggleStyle.Dot => RenderDot(c, dotClass, statusText),
            ToggleStyle.Text => RenderText(c),
            _ => RenderBadge(c, statusClass, statusText)
        };
    }

    private static string RenderBadge(ToggleControl c, string statusClass, string statusText)
    {
        return $@"<div class=""card bg-base-200 h-full"">
    <div class=""card-body p-4 flex flex-col items-center justify-center"">
        <span class=""text-lg text-base-content/70 uppercase mb-2"">{c.Label}</span>
        <span class=""badge badge-lg {statusClass}"">{statusText}</span>
    </div>
</div>";
    }

    private static string RenderSwitch(ToggleControl c, string statusClass)
    {
        var checkedAttr = c.Value ? "checked" : "";
        return $@"<div class=""card bg-base-200 h-full"">
    <div class=""card-body p-4 flex flex-col items-center justify-center"">
        <span class=""text-lg text-base-content/70 uppercase mb-2"">{c.Label}</span>
        <input type=""checkbox"" class=""toggle toggle-lg {statusClass}"" {checkedAttr} disabled />
    </div>
</div>";
    }

    private static string RenderDot(ToggleControl c, string dotClass, string statusText)
    {
        return $@"<div class=""card bg-base-200 h-full"">
    <div class=""card-body p-4 flex items-center justify-center gap-3"">
        <span class=""w-4 h-4 rounded-full {dotClass}""></span>
        <span class=""text-lg uppercase"">{c.Label}</span>
        <span class=""text-base-content/70"">{statusText}</span>
    </div>
</div>";
    }

    private static string RenderText(ToggleControl c)
    {
        var textClass = c.Value ? "text-success" : "text-error";
        var statusText = c.Value ? c.TrueText : c.FalseText;

        return $@"<div class=""card bg-base-200 h-full"">
    <div class=""card-body p-4 flex flex-col items-center justify-center"">
        <span class=""text-lg text-base-content/70 uppercase"">{c.Label}</span>
        <span class=""text-3xl font-bold {textClass}"">{statusText}</span>
    </div>
</div>";
    }
}

/// <summary>
/// Default renderer for StatusIndicatorControl.
/// </summary>
public class StatusIndicatorRenderer : IControlRenderer
{
    public string ControlType => "status_indicator";

    public string Render(SemanticControl control, ControlRenderContext context)
    {
        var c = (StatusIndicatorControl)control;

        var (dotClass, textClass, defaultText) = c.Status switch
        {
            StatusLevel.Info => ("bg-info", "text-info", "Info"),
            StatusLevel.Success => ("bg-success", "text-success", "OK"),
            StatusLevel.Warning => ("bg-warning", "text-warning", "Warning"),
            StatusLevel.Error => ("bg-error", "text-error", "Error"),
            _ => ("bg-base-content", "text-base-content", "Normal")
        };

        var statusText = c.StatusText ?? defaultText;

        return $@"<div class=""card bg-base-200 h-full"">
    <div class=""card-body p-4 flex items-center justify-center gap-3"">
        <span class=""w-4 h-4 rounded-full {dotClass} animate-pulse""></span>
        <span class=""text-xl uppercase"">{c.Label}</span>
        <span class=""text-lg {textClass}"">{statusText}</span>
    </div>
</div>";
    }
}
