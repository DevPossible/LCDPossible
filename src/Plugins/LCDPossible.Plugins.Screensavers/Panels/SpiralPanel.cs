using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using LCDPossible.Core.Rendering;
namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Hypnotic rotating spiral pattern.
/// </summary>
public sealed class SpiralPanel : CanvasPanel
{
    private DateTime _startTime;

    public override string PanelId => "spiral";
    public override string DisplayName => "Spiral";
    public override PanelRenderMode RenderMode => PanelRenderMode.Stream;

    public SpiralPanel()
    {
        _startTime = DateTime.UtcNow;
    }

    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var time = (float)(DateTime.UtcNow - _startTime).TotalSeconds;
        var image = new Image<Rgba32>(width, height);

        var centerX = width / 2f;
        var centerY = height / 2f;
        var maxDist = MathF.Sqrt(centerX * centerX + centerY * centerY);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);

                for (var x = 0; x < width; x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;

                    var distance = MathF.Sqrt(dx * dx + dy * dy);
                    var angle = MathF.Atan2(dy, dx);

                    // Create spiral pattern
                    var spiralValue = angle + distance * 0.05f - time * 2f;

                    // Add concentric rings
                    var rings = MathF.Sin(distance * 0.1f - time * 3f);

                    // Combine patterns
                    var pattern = MathF.Sin(spiralValue * 3f) * 0.5f + rings * 0.5f;

                    // Normalize to 0-1
                    pattern = (pattern + 1f) / 2f;

                    // Apply color based on distance and pattern
                    var hue = (angle * 180f / MathF.PI + 180f + time * 30f) % 360f;
                    var saturation = 0.8f;
                    var lightness = 0.3f + pattern * 0.4f;

                    // Fade out at edges
                    var edgeFade = 1f - MathF.Pow(distance / maxDist, 2f);
                    lightness *= edgeFade;

                    row[x] = HslToRgba(hue, saturation, lightness);
                }
            }
        });

        return Task.FromResult(image);
    }

    private static Rgba32 HslToRgba(float h, float s, float l)
    {
        h = ((h % 360f) + 360f) % 360f;

        var c = (1f - MathF.Abs(2f * l - 1f)) * s;
        var x = c * (1f - MathF.Abs((h / 60f) % 2f - 1f));
        var m = l - c / 2f;

        float r, g, b;

        if (h < 60f) { r = c; g = x; b = 0f; }
        else if (h < 120f) { r = x; g = c; b = 0f; }
        else if (h < 180f) { r = 0f; g = c; b = x; }
        else if (h < 240f) { r = 0f; g = x; b = c; }
        else if (h < 300f) { r = x; g = 0f; b = c; }
        else { r = c; g = 0f; b = x; }

        return new Rgba32(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }
}
