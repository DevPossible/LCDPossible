using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using LCDPossible.Core.Rendering;
namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Flying through a colorful warp tunnel effect.
/// </summary>
public sealed class WarpTunnelPanel : CanvasPanel
{
    private DateTime _startTime;

    public override string PanelId => "warp-tunnel";
    public override string DisplayName => "Warp Tunnel";
    public override PanelRenderMode RenderMode => PanelRenderMode.Stream;

    public WarpTunnelPanel()
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

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);

                for (var x = 0; x < width; x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;

                    // Calculate polar coordinates
                    var distance = MathF.Sqrt(dx * dx + dy * dy);
                    var angle = MathF.Atan2(dy, dx);

                    // Avoid division by zero at center
                    if (distance < 1f) distance = 1f;

                    // Create tunnel depth effect (inverse distance)
                    var depth = 100f / distance;

                    // Animate tunnel movement
                    var tunnelZ = depth - time * 5f;

                    // Create ring pattern
                    var ring = MathF.Sin(tunnelZ * 2f) * 0.5f + 0.5f;

                    // Create stripe pattern
                    var stripes = MathF.Sin(angle * 8f + time * 2f) * 0.5f + 0.5f;

                    // Combine patterns
                    var pattern = ring * 0.6f + stripes * 0.4f;

                    // Color based on depth and angle
                    var hue = (angle * 180f / MathF.PI + 180f + tunnelZ * 20f) % 360f;
                    var saturation = 0.8f;
                    var lightness = pattern * 0.5f;

                    // Fade to black at edges
                    var edgeFade = MathF.Min(1f, distance / (MathF.Min(centerX, centerY) * 0.9f));
                    lightness *= 1f - MathF.Pow(edgeFade, 3f) * 0.7f;

                    // Brighten center
                    var centerFade = 1f - MathF.Min(1f, distance / 50f);
                    lightness += centerFade * 0.3f;

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
