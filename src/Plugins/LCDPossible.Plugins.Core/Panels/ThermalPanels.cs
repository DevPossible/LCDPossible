using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// CPU thermal display with temperature pie chart and thermal metrics.
/// </summary>
public sealed class CpuThermalGraphicPanel : BaseLivePanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "cpu-thermal-graphic";
    public override string DisplayName => "CPU Thermal";

    public CpuThermalGraphicPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics?.Cpu == null || !FontsLoaded)
            {
                DrawCenteredText(ctx, "CPU Thermal Data Unavailable", width / 2f, height / 2f - 20, TitleFont!, SecondaryTextColor);
                return;
            }

            var cpu = metrics.Cpu;
            var temp = cpu.TemperatureCelsius ?? 0;

            // Title
            DrawCenteredText(ctx, "CPU THERMAL", width / 2f, 25, TitleFont!, AccentColor);

            // Pie chart in center
            var pieRadius = Math.Min(width, height) / 3.5f;
            var pieCenterX = width / 2f;
            var pieCenterY = height / 2f - 10;

            DrawTemperaturePieChart(ctx, temp, pieCenterX, pieCenterY, pieRadius);

            // Temperature text in center of pie
            var tempColor = GetTemperatureColor(temp);
            DrawCenteredText(ctx, $"{temp:F0}°C", pieCenterX, pieCenterY + 10, ValueFont!, tempColor);

            // Additional info at bottom
            var infoY = height - 80;

            if (cpu.PowerWatts.HasValue)
            {
                DrawText(ctx, $"Power: {cpu.PowerWatts.Value:F1}W", 40, infoY, LabelFont!, SecondaryTextColor, 200);
            }

            DrawCenteredText(ctx, TruncateText(cpu.Name, 40), width / 2f, infoY, SmallFont!, SecondaryTextColor);

            if (cpu.UsagePercent > 0)
            {
                DrawRightText(ctx, $"Load: {cpu.UsagePercent:F0}%", width - 40, infoY, LabelFont!, GetUsageColor(cpu.UsagePercent));
            }

            DrawTimestamp(ctx, width, height);
        });

        return image;
    }

    private void DrawTemperaturePieChart(IImageProcessingContext ctx, float temperature, float centerX, float centerY, float radius)
    {
        var tempPercent = Math.Clamp(temperature / 100f, 0, 1);
        var sweepAngle = tempPercent * 360f;
        var tempColor = GetTemperatureColor(temperature);

        // Background circle (unfilled portion)
        var bgPath = new EllipsePolygon(centerX, centerY, radius);
        ctx.Fill(Colors.BarBackground, bgPath);

        // Filled pie slice for temperature
        if (sweepAngle > 0.1f)
        {
            var piePath = BuildPieSlice(centerX, centerY, radius, -90, sweepAngle);
            ctx.Fill(tempColor, piePath);
        }

        // Inner circle to create donut effect
        var innerRadius = radius * 0.6f;
        var innerPath = new EllipsePolygon(centerX, centerY, innerRadius);
        ctx.Fill(Colors.Background, innerPath);

        // Outer border
        ctx.Draw(Colors.BarBorder, 3f, bgPath);
        ctx.Draw(Colors.BarBorder, 2f, innerPath);
    }

    private static IPath BuildPieSlice(float centerX, float centerY, float radius, float startAngle, float sweepAngle)
    {
        var pathBuilder = new PathBuilder();
        pathBuilder.MoveTo(new PointF(centerX, centerY));

        var startRad = startAngle * MathF.PI / 180f;
        var endRad = (startAngle + sweepAngle) * MathF.PI / 180f;

        // Start point on circle
        var startX = centerX + radius * MathF.Cos(startRad);
        var startY = centerY + radius * MathF.Sin(startRad);
        pathBuilder.LineTo(new PointF(startX, startY));

        // Arc along the circle
        var segments = Math.Max(8, (int)(sweepAngle / 5));
        for (int i = 1; i <= segments; i++)
        {
            var angle = startRad + (endRad - startRad) * i / segments;
            var x = centerX + radius * MathF.Cos(angle);
            var y = centerY + radius * MathF.Sin(angle);
            pathBuilder.LineTo(new PointF(x, y));
        }

        pathBuilder.CloseFigure();
        return pathBuilder.Build();
    }
}

/// <summary>
/// GPU thermal display with temperature pie chart and thermal metrics.
/// </summary>
public sealed class GpuThermalGraphicPanel : BaseLivePanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "gpu-thermal-graphic";
    public override string DisplayName => "GPU Thermal";

    public GpuThermalGraphicPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics?.Gpu == null || !FontsLoaded)
            {
                DrawCenteredText(ctx, "GPU Thermal Data Unavailable", width / 2f, height / 2f - 20, TitleFont!, SecondaryTextColor);
                return;
            }

            var gpu = metrics.Gpu;
            var temp = gpu.TemperatureCelsius ?? 0;

            // Title
            DrawCenteredText(ctx, "GPU THERMAL", width / 2f, 25, TitleFont!, AccentColor);

            // Pie chart in center
            var pieRadius = Math.Min(width, height) / 3.5f;
            var pieCenterX = width / 2f;
            var pieCenterY = height / 2f - 10;

            DrawTemperaturePieChart(ctx, temp, pieCenterX, pieCenterY, pieRadius);

            // Temperature text in center of pie
            var tempColor = GetTemperatureColor(temp);
            DrawCenteredText(ctx, $"{temp:F0}°C", pieCenterX, pieCenterY + 10, ValueFont!, tempColor);

            // Additional info at bottom
            var infoY = height - 80;

            if (gpu.PowerWatts.HasValue)
            {
                DrawText(ctx, $"Power: {gpu.PowerWatts.Value:F0}W", 40, infoY, LabelFont!, SecondaryTextColor, 200);
            }

            if (gpu.FanSpeedPercent.HasValue)
            {
                DrawCenteredText(ctx, $"Fan: {gpu.FanSpeedPercent.Value:F0}%", width / 2f, infoY, LabelFont!, SecondaryTextColor);
            }

            if (gpu.UsagePercent > 0)
            {
                DrawRightText(ctx, $"Load: {gpu.UsagePercent:F0}%", width - 40, infoY, LabelFont!, GetUsageColor(gpu.UsagePercent));
            }

            // GPU name at very bottom
            DrawCenteredText(ctx, TruncateText(gpu.Name, 50), width / 2f, height - 45, SmallFont!, SecondaryTextColor);

            DrawTimestamp(ctx, width, height);
        });

        return image;
    }

    private void DrawTemperaturePieChart(IImageProcessingContext ctx, float temperature, float centerX, float centerY, float radius)
    {
        var tempPercent = Math.Clamp(temperature / 100f, 0, 1);
        var sweepAngle = tempPercent * 360f;
        var tempColor = GetTemperatureColor(temperature);

        // Background circle
        var bgPath = new EllipsePolygon(centerX, centerY, radius);
        ctx.Fill(Colors.BarBackground, bgPath);

        // Filled pie slice
        if (sweepAngle > 0.1f)
        {
            var piePath = BuildPieSlice(centerX, centerY, radius, -90, sweepAngle);
            ctx.Fill(tempColor, piePath);
        }

        // Inner circle (donut)
        var innerRadius = radius * 0.6f;
        var innerPath = new EllipsePolygon(centerX, centerY, innerRadius);
        ctx.Fill(Colors.Background, innerPath);

        // Borders
        ctx.Draw(Colors.BarBorder, 3f, bgPath);
        ctx.Draw(Colors.BarBorder, 2f, innerPath);
    }

    private static IPath BuildPieSlice(float centerX, float centerY, float radius, float startAngle, float sweepAngle)
    {
        var pathBuilder = new PathBuilder();
        pathBuilder.MoveTo(new PointF(centerX, centerY));

        var startRad = startAngle * MathF.PI / 180f;
        var endRad = (startAngle + sweepAngle) * MathF.PI / 180f;

        var startX = centerX + radius * MathF.Cos(startRad);
        var startY = centerY + radius * MathF.Sin(startRad);
        pathBuilder.LineTo(new PointF(startX, startY));

        var segments = Math.Max(8, (int)(sweepAngle / 5));
        for (int i = 1; i <= segments; i++)
        {
            var angle = startRad + (endRad - startRad) * i / segments;
            var x = centerX + radius * MathF.Cos(angle);
            var y = centerY + radius * MathF.Sin(angle);
            pathBuilder.LineTo(new PointF(x, y));
        }

        pathBuilder.CloseFigure();
        return pathBuilder.Build();
    }
}

/// <summary>
/// Combined CPU and GPU thermal display showing both temperatures side by side with pie charts.
/// </summary>
public sealed class SystemThermalGraphicPanel : BaseLivePanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "system-thermal-graphic";
    public override string DisplayName => "System Thermal";

    public SystemThermalGraphicPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics == null || !FontsLoaded)
            {
                DrawCenteredText(ctx, "Thermal Data Unavailable", width / 2f, height / 2f - 20, TitleFont!, SecondaryTextColor);
                return;
            }

            var halfWidth = width / 2;
            var cpuTemp = metrics.Cpu?.TemperatureCelsius ?? 0;
            var gpuTemp = metrics.Gpu?.TemperatureCelsius ?? 0;

            // Title
            DrawCenteredText(ctx, "SYSTEM THERMAL", width / 2f, 25, TitleFont!, AccentColor);

            // Calculate pie chart size - smaller for side by side view
            var pieRadius = Math.Min(halfWidth, height) / 4f;

            // CPU Section (left half)
            var cpuX = halfWidth / 2;
            var pieCenterY = height / 2f;

            DrawCenteredText(ctx, "CPU", cpuX, 65, LabelFont!, AccentColor);

            // CPU Pie Chart
            DrawTemperaturePieChart(ctx, cpuTemp, cpuX, pieCenterY, pieRadius);

            // Temperature text in center of pie
            DrawCenteredText(ctx, $"{cpuTemp:F0}°C", cpuX, pieCenterY + 8, TitleFont!, GetTemperatureColor(cpuTemp));

            if (metrics.Cpu?.PowerWatts.HasValue == true)
            {
                DrawCenteredText(ctx, $"{metrics.Cpu.PowerWatts.Value:F0}W", cpuX, height - 60, SmallFont!, SecondaryTextColor);
            }

            if (metrics.Cpu?.UsagePercent > 0)
            {
                DrawCenteredText(ctx, $"Load: {metrics.Cpu.UsagePercent:F0}%", cpuX, height - 35, SmallFont!, GetUsageColor(metrics.Cpu.UsagePercent));
            }

            // GPU Section (right half)
            var gpuX = halfWidth + halfWidth / 2;

            DrawCenteredText(ctx, "GPU", gpuX, 65, LabelFont!, AccentColor);

            // GPU Pie Chart
            DrawTemperaturePieChart(ctx, gpuTemp, gpuX, pieCenterY, pieRadius);

            // Temperature text in center of pie
            DrawCenteredText(ctx, $"{gpuTemp:F0}°C", gpuX, pieCenterY + 8, TitleFont!, GetTemperatureColor(gpuTemp));

            if (metrics.Gpu?.PowerWatts.HasValue == true)
            {
                DrawCenteredText(ctx, $"{metrics.Gpu.PowerWatts.Value:F0}W", gpuX, height - 60, SmallFont!, SecondaryTextColor);
            }

            if (metrics.Gpu?.UsagePercent > 0)
            {
                DrawCenteredText(ctx, $"Load: {metrics.Gpu.UsagePercent:F0}%", gpuX, height - 35, SmallFont!, GetUsageColor(metrics.Gpu.UsagePercent));
            }

            // Divider line
            ctx.DrawLine(Colors.BarBorder, 2f, new PointF(halfWidth, 60), new PointF(halfWidth, height - 25));

            DrawTimestamp(ctx, width, height);
        });

        return image;
    }

    private void DrawTemperaturePieChart(IImageProcessingContext ctx, float temperature, float centerX, float centerY, float radius)
    {
        var tempPercent = Math.Clamp(temperature / 100f, 0, 1);
        var sweepAngle = tempPercent * 360f;
        var tempColor = GetTemperatureColor(temperature);

        // Background circle
        var bgPath = new EllipsePolygon(centerX, centerY, radius);
        ctx.Fill(Colors.BarBackground, bgPath);

        // Filled pie slice
        if (sweepAngle > 0.1f)
        {
            var piePath = BuildPieSlice(centerX, centerY, radius, -90, sweepAngle);
            ctx.Fill(tempColor, piePath);
        }

        // Inner circle (donut)
        var innerRadius = radius * 0.55f;
        var innerPath = new EllipsePolygon(centerX, centerY, innerRadius);
        ctx.Fill(Colors.Background, innerPath);

        // Borders
        ctx.Draw(Colors.BarBorder, 2f, bgPath);
        ctx.Draw(Colors.BarBorder, 1.5f, innerPath);
    }

    private static IPath BuildPieSlice(float centerX, float centerY, float radius, float startAngle, float sweepAngle)
    {
        var pathBuilder = new PathBuilder();
        pathBuilder.MoveTo(new PointF(centerX, centerY));

        var startRad = startAngle * MathF.PI / 180f;
        var endRad = (startAngle + sweepAngle) * MathF.PI / 180f;

        var startX = centerX + radius * MathF.Cos(startRad);
        var startY = centerY + radius * MathF.Sin(startRad);
        pathBuilder.LineTo(new PointF(startX, startY));

        var segments = Math.Max(8, (int)(sweepAngle / 5));
        for (int i = 1; i <= segments; i++)
        {
            var angle = startRad + (endRad - startRad) * i / segments;
            var x = centerX + radius * MathF.Cos(angle);
            var y = centerY + radius * MathF.Sin(angle);
            pathBuilder.LineTo(new PointF(x, y));
        }

        pathBuilder.CloseFigure();
        return pathBuilder.Build();
    }
}
