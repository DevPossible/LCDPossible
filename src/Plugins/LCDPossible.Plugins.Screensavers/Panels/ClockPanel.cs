using LCDPossible.Sdk;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Analog clock screensaver with smooth second hand.
/// </summary>
public sealed class ClockPanel : BaseLivePanel
{
    private Font? _digitFont;

    public override string PanelId => "clock";
    public override string DisplayName => "Clock";
    public override bool IsAnimated => true;

    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _digitFont = FontHelper.GetPreferredFont(16);
        return Task.CompletedTask;
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var image = new Image<Rgba32>(width, height, new Rgba32(10, 10, 20));

        var centerX = width / 2f;
        var centerY = height / 2f;
        var radius = MathF.Min(centerX, centerY) * 0.85f;

        image.Mutate(ctx =>
        {
            // Draw clock face
            var faceColor = new Rgba32(30, 30, 50);
            ctx.Fill(faceColor, new EllipsePolygon(centerX, centerY, radius));

            // Draw outer ring
            var ringColor = new Rgba32(80, 80, 120);
            ctx.Draw(ringColor, 4f, new EllipsePolygon(centerX, centerY, radius));

            // Draw hour markers
            for (var i = 0; i < 12; i++)
            {
                var angle = i * MathF.PI / 6f - MathF.PI / 2f;
                var innerRadius = radius * 0.85f;
                var outerRadius = radius * 0.95f;

                var x1 = centerX + MathF.Cos(angle) * innerRadius;
                var y1 = centerY + MathF.Sin(angle) * innerRadius;
                var x2 = centerX + MathF.Cos(angle) * outerRadius;
                var y2 = centerY + MathF.Sin(angle) * outerRadius;

                var markerWidth = i % 3 == 0 ? 3f : 1.5f;
                ctx.DrawLine(new Rgba32(200, 200, 220), markerWidth, new PointF(x1, y1), new PointF(x2, y2));
            }

            // Draw minute markers
            for (var i = 0; i < 60; i++)
            {
                if (i % 5 == 0) continue; // Skip hour positions

                var angle = i * MathF.PI / 30f - MathF.PI / 2f;
                var innerRadius = radius * 0.92f;
                var outerRadius = radius * 0.95f;

                var x1 = centerX + MathF.Cos(angle) * innerRadius;
                var y1 = centerY + MathF.Sin(angle) * innerRadius;
                var x2 = centerX + MathF.Cos(angle) * outerRadius;
                var y2 = centerY + MathF.Sin(angle) * outerRadius;

                ctx.DrawLine(new Rgba32(100, 100, 120), 1f, new PointF(x1, y1), new PointF(x2, y2));
            }

            // Calculate hand angles (with smooth seconds)
            var seconds = now.Second + now.Millisecond / 1000f;
            var minutes = now.Minute + seconds / 60f;
            var hours = now.Hour % 12 + minutes / 60f;

            var secondAngle = seconds * MathF.PI / 30f - MathF.PI / 2f;
            var minuteAngle = minutes * MathF.PI / 30f - MathF.PI / 2f;
            var hourAngle = hours * MathF.PI / 6f - MathF.PI / 2f;

            // Draw hour hand
            DrawHand(ctx, centerX, centerY, hourAngle, radius * 0.5f, 6f, new Rgba32(220, 220, 240));

            // Draw minute hand
            DrawHand(ctx, centerX, centerY, minuteAngle, radius * 0.75f, 4f, new Rgba32(200, 200, 220));

            // Draw second hand
            DrawHand(ctx, centerX, centerY, secondAngle, radius * 0.85f, 2f, new Rgba32(255, 80, 80));

            // Draw center dot
            ctx.Fill(new Rgba32(255, 100, 100), new EllipsePolygon(centerX, centerY, 6));
            ctx.Fill(new Rgba32(50, 50, 70), new EllipsePolygon(centerX, centerY, 3));

            // Draw digital time below
            if (_digitFont != null)
            {
                var timeStr = now.ToString("HH:mm:ss");
                var textOptions = new RichTextOptions(_digitFont)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Origin = new PointF(centerX, centerY + radius + 20)
                };
                ctx.DrawText(textOptions, timeStr, new Rgba32(150, 150, 180));
            }
        });

        return Task.FromResult(image);
    }

    private static void DrawHand(IImageProcessingContext ctx, float cx, float cy, float angle, float length, float width, Rgba32 color)
    {
        var x = cx + MathF.Cos(angle) * length;
        var y = cy + MathF.Sin(angle) * length;

        // Draw shadow
        var shadowColor = new Rgba32(0, 0, 0, 80);
        ctx.DrawLine(shadowColor, width + 1, new PointF(cx + 2, cy + 2), new PointF(x + 2, y + 2));

        // Draw hand
        ctx.DrawLine(color, width, new PointF(cx, cy), new PointF(x, y));
    }
}
