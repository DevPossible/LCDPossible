using LCDPossible.Core.Devices;
using LCDPossible.Core.Monitoring;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Rendering;

/// <summary>
/// Renders system information (CPU, GPU, memory) to LCD frames.
/// </summary>
public sealed class SystemInfoRenderer
{
    private Font? _titleFont;
    private Font? _valueFont;
    private Font? _labelFont;
    private bool _fontsLoaded;

    private static readonly Color BackgroundColor = Color.FromRgb(15, 15, 25);
    private static readonly Color PrimaryTextColor = Color.White;
    private static readonly Color SecondaryTextColor = Color.FromRgb(180, 180, 200);
    private static readonly Color AccentColor = Color.FromRgb(0, 150, 255);
    private static readonly Color WarningColor = Color.FromRgb(255, 180, 0);
    private static readonly Color CriticalColor = Color.FromRgb(255, 50, 50);
    private static readonly Color SuccessColor = Color.FromRgb(50, 200, 100);

    public SystemInfoRenderer()
    {
        LoadFonts();
    }

    private void LoadFonts()
    {
        if (_fontsLoaded)
        {
            return;
        }

        try
        {
            // Try to load system fonts
            var fontCollection = SystemFonts.Collection;

            // Try common fonts
            foreach (var fontName in new[] { "Segoe UI", "Arial", "Roboto", "DejaVu Sans", "Liberation Sans" })
            {
                if (fontCollection.TryGet(fontName, out var family))
                {
                    _titleFont = family.CreateFont(32, FontStyle.Bold);
                    _valueFont = family.CreateFont(48, FontStyle.Bold);
                    _labelFont = family.CreateFont(18, FontStyle.Regular);
                    _fontsLoaded = true;
                    return;
                }
            }

            // Fallback: use any available font
            if (fontCollection.Families.Any())
            {
                var family = fontCollection.Families.First();
                _titleFont = family.CreateFont(32, FontStyle.Bold);
                _valueFont = family.CreateFont(48, FontStyle.Bold);
                _labelFont = family.CreateFont(18, FontStyle.Regular);
                _fontsLoaded = true;
            }
        }
        catch
        {
            // Font loading failed, will use graphics-only mode
        }
    }

    /// <summary>
    /// Renders local hardware metrics to an image.
    /// </summary>
    public Image<Rgba32> RenderSystemMetrics(SystemMetrics metrics, LcdCapabilities capabilities)
    {
        var image = new Image<Rgba32>(capabilities.Width, capabilities.Height);

        image.Mutate(ctx =>
        {
            ctx.Fill(BackgroundColor);

            var y = 20;

            // CPU Section
            if (metrics.Cpu != null)
            {
                RenderMetricBar(ctx, "CPU", metrics.Cpu.UsagePercent, metrics.Cpu.TemperatureCelsius, 20, y, 380, capabilities);
                y += 120;
            }

            // GPU Section
            if (metrics.Gpu != null)
            {
                RenderMetricBar(ctx, "GPU", metrics.Gpu.UsagePercent, metrics.Gpu.TemperatureCelsius, 20, y, 380, capabilities);
                y += 120;
            }

            // Memory Section
            if (metrics.Memory != null)
            {
                RenderMetricBar(ctx, "RAM", metrics.Memory.UsagePercent, null, 20, y, 380, capabilities);
                y += 120;
            }

            // Right side - detailed info
            var rightX = 450;
            y = 20;

            if (metrics.Cpu != null && _fontsLoaded)
            {
                DrawText(ctx, metrics.Cpu.Name, rightX, y, _labelFont!, SecondaryTextColor, capabilities.Width - rightX - 20);
                y += 30;

                if (metrics.Cpu.FrequencyMhz.HasValue)
                {
                    DrawText(ctx, $"Freq: {metrics.Cpu.FrequencyMhz.Value:F0} MHz", rightX, y, _labelFont!, SecondaryTextColor, capabilities.Width - rightX - 20);
                    y += 25;
                }

                if (metrics.Cpu.PowerWatts.HasValue)
                {
                    DrawText(ctx, $"Power: {metrics.Cpu.PowerWatts.Value:F1}W", rightX, y, _labelFont!, SecondaryTextColor, capabilities.Width - rightX - 20);
                    y += 25;
                }
            }

            y += 20;

            if (metrics.Gpu != null && _fontsLoaded)
            {
                DrawText(ctx, metrics.Gpu.Name, rightX, y, _labelFont!, SecondaryTextColor, capabilities.Width - rightX - 20);
                y += 30;

                if (metrics.Gpu.MemoryUsedMb.HasValue && metrics.Gpu.MemoryTotalMb.HasValue)
                {
                    DrawText(ctx, $"VRAM: {metrics.Gpu.MemoryUsedMb.Value:F0}/{metrics.Gpu.MemoryTotalMb.Value:F0} MB", rightX, y, _labelFont!, SecondaryTextColor, capabilities.Width - rightX - 20);
                    y += 25;
                }

                if (metrics.Gpu.PowerWatts.HasValue)
                {
                    DrawText(ctx, $"Power: {metrics.Gpu.PowerWatts.Value:F1}W", rightX, y, _labelFont!, SecondaryTextColor, capabilities.Width - rightX - 20);
                    y += 25;
                }
            }

            y += 20;

            if (metrics.Memory != null && _fontsLoaded)
            {
                DrawText(ctx, $"Memory: {metrics.Memory.UsedGb:F1} / {metrics.Memory.TotalGb:F1} GB", rightX, y, _labelFont!, SecondaryTextColor, capabilities.Width - rightX - 20);
            }

            // Timestamp
            if (_fontsLoaded)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                DrawText(ctx, timestamp, capabilities.Width - 100, capabilities.Height - 30, _labelFont!, SecondaryTextColor, 90);
            }
        });

        return image;
    }

    /// <summary>
    /// Renders Proxmox cluster metrics to an image.
    /// </summary>
    public Image<Rgba32> RenderProxmoxMetrics(ProxmoxMetrics metrics, LcdCapabilities capabilities)
    {
        var image = new Image<Rgba32>(capabilities.Width, capabilities.Height);

        image.Mutate(ctx =>
        {
            ctx.Fill(BackgroundColor);

            if (!_fontsLoaded)
            {
                // Just render a simple indicator without text
                RenderSimpleProxmoxDisplay(ctx, metrics, capabilities);
                return;
            }

            // Header
            DrawText(ctx, $"Proxmox: {metrics.ClusterName}", 20, 15, _titleFont!, AccentColor, capabilities.Width - 40);

            // Summary row
            var summaryY = 60;
            var colWidth = (capabilities.Width - 40) / 4;

            // Nodes
            var nodeColor = metrics.Summary.OnlineNodes == metrics.Summary.TotalNodes ? SuccessColor : WarningColor;
            DrawText(ctx, "NODES", 20, summaryY, _labelFont!, SecondaryTextColor, colWidth);
            DrawText(ctx, $"{metrics.Summary.OnlineNodes}/{metrics.Summary.TotalNodes}", 20, summaryY + 25, _valueFont!, nodeColor, colWidth);

            // VMs
            DrawText(ctx, "VMs", 20 + colWidth, summaryY, _labelFont!, SecondaryTextColor, colWidth);
            DrawText(ctx, $"{metrics.Summary.RunningVms}/{metrics.Summary.TotalVms}", 20 + colWidth, summaryY + 25, _valueFont!, PrimaryTextColor, colWidth);

            // Containers
            DrawText(ctx, "LXC", 20 + colWidth * 2, summaryY, _labelFont!, SecondaryTextColor, colWidth);
            DrawText(ctx, $"{metrics.Summary.RunningContainers}/{metrics.Summary.TotalContainers}", 20 + colWidth * 2, summaryY + 25, _valueFont!, PrimaryTextColor, colWidth);

            // Alerts
            var alertColor = metrics.Summary.CriticalAlerts > 0 ? CriticalColor :
                             metrics.Summary.WarningAlerts > 0 ? WarningColor : SuccessColor;
            DrawText(ctx, "ALERTS", 20 + colWidth * 3, summaryY, _labelFont!, SecondaryTextColor, colWidth);
            var alertCount = metrics.Summary.CriticalAlerts + metrics.Summary.WarningAlerts;
            DrawText(ctx, alertCount > 0 ? alertCount.ToString() : "OK", 20 + colWidth * 3, summaryY + 25, _valueFont!, alertColor, colWidth);

            // Resource usage bars
            var barY = 160;
            RenderHorizontalBar(ctx, "Cluster CPU", metrics.Summary.CpuUsagePercent, 20, barY, capabilities.Width / 2 - 30);
            RenderHorizontalBar(ctx, "Cluster RAM", metrics.Summary.MemoryUsagePercent, capabilities.Width / 2 + 10, barY, capabilities.Width / 2 - 30);

            // VM/Container list
            var listY = 220;
            var itemHeight = 35;
            var maxItems = (capabilities.Height - listY - 40) / itemHeight;

            // Combine running VMs and containers, show most important
            var runningItems = metrics.VirtualMachines
                .Where(v => v.IsRunning)
                .Select(v => new { Name = v.Name, Type = "VM", Cpu = v.CpuUsagePercent, Status = v.Status })
                .Concat(metrics.Containers
                    .Where(c => c.IsRunning)
                    .Select(c => new { Name = c.Name, Type = "CT", Cpu = c.CpuUsagePercent, Status = c.Status }))
                .OrderByDescending(x => x.Cpu)
                .Take(maxItems)
                .ToList();

            foreach (var item in runningItems)
            {
                var typeColor = item.Type == "VM" ? AccentColor : SuccessColor;
                DrawText(ctx, $"[{item.Type}]", 20, listY, _labelFont!, typeColor, 50);
                DrawText(ctx, item.Name, 75, listY, _labelFont!, PrimaryTextColor, capabilities.Width / 2 - 85);
                DrawText(ctx, $"{item.Cpu:F0}%", capabilities.Width / 2, listY, _labelFont!, GetUsageColor(item.Cpu), 60);
                listY += itemHeight;
            }

            // Alerts section (if any)
            if (metrics.Alerts.Count > 0)
            {
                var alertY = capabilities.Height - 60;
                var alert = metrics.Alerts.OrderByDescending(a => a.Severity).First();
                var color = alert.Severity == AlertSeverity.Critical ? CriticalColor : WarningColor;
                DrawText(ctx, $"! {alert.Title}: {alert.Description}", 20, alertY, _labelFont!, color, capabilities.Width - 40);
            }

            // Timestamp
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            DrawText(ctx, timestamp, capabilities.Width - 100, capabilities.Height - 30, _labelFont!, SecondaryTextColor, 90);
        });

        return image;
    }

    private void RenderMetricBar(IImageProcessingContext ctx, string label, float percentage, float? temperature, int x, int y, int width, LcdCapabilities capabilities)
    {
        var barHeight = 30;
        var barY = y + 50;

        // Label and value
        if (_fontsLoaded)
        {
            DrawText(ctx, label, x, y, _titleFont!, PrimaryTextColor, 100);
            DrawText(ctx, $"{percentage:F0}%", x + 100, y, _valueFont!, GetUsageColor(percentage), 150);

            if (temperature.HasValue)
            {
                var tempColor = GetTemperatureColor(temperature.Value);
                DrawText(ctx, $"{temperature.Value:F0}°C", x + 260, y + 10, _titleFont!, tempColor, 100);
            }
        }

        // Background bar
        var barRect = new RectangleF(x, barY, width, barHeight);
        ctx.Fill(Color.FromRgb(40, 40, 50), barRect);

        // Filled portion
        var fillWidth = (int)(width * (percentage / 100f));
        if (fillWidth > 0)
        {
            var fillRect = new RectangleF(x, barY, fillWidth, barHeight);
            ctx.Fill(GetUsageColor(percentage), fillRect);
        }

        // Border
        ctx.Draw(Color.FromRgb(80, 80, 100), 1f, barRect);
    }

    private void RenderHorizontalBar(IImageProcessingContext ctx, string label, float percentage, int x, int y, int width)
    {
        var barHeight = 20;
        var barY = y + 25;

        if (_fontsLoaded)
        {
            DrawText(ctx, $"{label}: {percentage:F0}%", x, y, _labelFont!, SecondaryTextColor, width);
        }

        // Background bar
        var barRect = new RectangleF(x, barY, width, barHeight);
        ctx.Fill(Color.FromRgb(40, 40, 50), barRect);

        // Filled portion
        var fillWidth = (int)(width * (percentage / 100f));
        if (fillWidth > 0)
        {
            var fillRect = new RectangleF(x, barY, fillWidth, barHeight);
            ctx.Fill(GetUsageColor(percentage), fillRect);
        }
    }

    private void RenderSimpleProxmoxDisplay(IImageProcessingContext ctx, ProxmoxMetrics metrics, LcdCapabilities capabilities)
    {
        // Simple colored blocks to represent status when fonts aren't available
        var blockSize = 50;
        var spacing = 10;
        var y = capabilities.Height / 2 - blockSize / 2;

        // Green blocks for running VMs
        var runningVms = metrics.Summary.RunningVms;
        var x = 20;
        for (var i = 0; i < Math.Min(runningVms, 10); i++)
        {
            ctx.Fill(SuccessColor, new RectangleF(x, y, blockSize, blockSize));
            x += blockSize + spacing;
        }

        // Yellow blocks for stopped VMs
        var stoppedVms = metrics.Summary.TotalVms - runningVms;
        for (var i = 0; i < Math.Min(stoppedVms, 5); i++)
        {
            ctx.Fill(WarningColor, new RectangleF(x, y, blockSize, blockSize));
            x += blockSize + spacing;
        }

        // Red block if alerts
        if (metrics.Summary.CriticalAlerts > 0)
        {
            ctx.Fill(CriticalColor, new RectangleF(capabilities.Width - 70, y, blockSize, blockSize));
        }
    }

    private void DrawText(IImageProcessingContext ctx, string text, float x, float y, Font font, Color color, float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var options = new RichTextOptions(font)
        {
            Origin = new PointF(x, y),
            WrappingLength = maxWidth,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        ctx.DrawText(options, text, color);
    }

    private static Color GetUsageColor(float percentage)
    {
        return percentage switch
        {
            >= 90 => CriticalColor,
            >= 70 => WarningColor,
            _ => AccentColor
        };
    }

    private static Color GetTemperatureColor(float celsius)
    {
        return celsius switch
        {
            >= 85 => CriticalColor,
            >= 70 => WarningColor,
            _ => SuccessColor
        };
    }
}
