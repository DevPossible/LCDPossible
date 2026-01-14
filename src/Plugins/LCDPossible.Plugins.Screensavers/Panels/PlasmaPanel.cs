using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using LCDPossible.Core.Rendering;
namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Classic demoscene plasma effect using sine wave interference patterns.
/// </summary>
public sealed class PlasmaPanel : CanvasPanel
{
    private const int ScaleFactor = 4; // Render at lower resolution for performance

    private DateTime _startTime;
    private float[]? _sinTable;
    private float[]? _cosTable;
    private float _seed1, _seed2, _seed3, _seed4, _seed5;

    public override string PanelId => "plasma";
    public override string DisplayName => "Plasma";
    public override PanelRenderMode RenderMode => PanelRenderMode.Stream;

    public PlasmaPanel()
    {
        _startTime = DateTime.UtcNow;

        // Random seeds for variation
        var rng = new Random();
        _seed1 = rng.NextSingle() * 100f;
        _seed2 = rng.NextSingle() * 100f;
        _seed3 = rng.NextSingle() * 100f;
        _seed4 = rng.NextSingle() * 100f;
        _seed5 = rng.NextSingle() * 100f;
    }

    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Pre-compute sine and cosine tables for faster lookup
        const int tableSize = 512;
        _sinTable = new float[tableSize];
        _cosTable = new float[tableSize];

        for (var i = 0; i < tableSize; i++)
        {
            var angle = i * MathF.PI * 2f / tableSize;
            _sinTable[i] = MathF.Sin(angle);
            _cosTable[i] = MathF.Cos(angle);
        }

        return Task.CompletedTask;
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var time = (float)(DateTime.UtcNow - _startTime).TotalSeconds;

        // Render at lower resolution for performance
        var scaledWidth = width / ScaleFactor;
        var scaledHeight = height / ScaleFactor;

        var image = new Image<Rgba32>(width, height);

        // Generate plasma at scaled resolution
        var plasmaBuffer = new float[scaledWidth * scaledHeight];

        // Moving center points for circular waves
        var cx1 = 0.5f + FastSin(time * 0.7f + _seed1) * 0.3f;
        var cy1 = 0.5f + FastCos(time * 0.5f + _seed2) * 0.3f;
        var cx2 = 0.5f + FastSin(time * 0.4f + _seed3) * 0.4f;
        var cy2 = 0.5f + FastCos(time * 0.6f + _seed4) * 0.4f;
        var cx3 = 0.3f + FastSin(time * 0.3f + _seed5) * 0.2f;
        var cy3 = 0.7f + FastCos(time * 0.8f + _seed1) * 0.2f;

        for (var sy = 0; sy < scaledHeight; sy++)
        {
            var y = (float)sy / scaledHeight;

            for (var sx = 0; sx < scaledWidth; sx++)
            {
                var x = (float)sx / scaledWidth;

                // Multiple overlapping sine wave patterns with random offsets
                var value = 0f;

                // Horizontal waves with varying frequency
                value += FastSin((x * 8f + time * 0.5f + _seed1) * MathF.PI * 2f);
                value += FastSin((x * 13f - time * 0.3f + _seed2) * MathF.PI * 2f) * 0.5f;

                // Vertical waves with varying frequency
                value += FastSin((y * 7f + time * 0.4f + _seed3) * MathF.PI * 2f);
                value += FastSin((y * 11f - time * 0.6f + _seed4) * MathF.PI * 2f) * 0.5f;

                // Diagonal waves in different directions
                value += FastSin(((x + y) * 6f + time * 0.7f + _seed5) * MathF.PI * 2f) * 0.7f;
                value += FastSin(((x - y) * 9f - time * 0.4f + _seed1) * MathF.PI * 2f) * 0.5f;

                // Multiple circular waves from moving centers
                var dx1 = x - cx1;
                var dy1 = y - cy1;
                var dist1 = MathF.Sqrt(dx1 * dx1 + dy1 * dy1);
                value += FastSin((dist1 * 15f - time * 1.2f) * MathF.PI * 2f);

                var dx2 = x - cx2;
                var dy2 = y - cy2;
                var dist2 = MathF.Sqrt(dx2 * dx2 + dy2 * dy2);
                value += FastSin((dist2 * 12f + time * 0.8f) * MathF.PI * 2f) * 0.8f;

                var dx3 = x - cx3;
                var dy3 = y - cy3;
                var dist3 = MathF.Sqrt(dx3 * dx3 + dy3 * dy3);
                value += FastSin((dist3 * 18f - time * 0.5f) * MathF.PI * 2f) * 0.6f;

                // Swirling pattern
                var angle = MathF.Atan2(y - 0.5f, x - 0.5f);
                var swirl = MathF.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
                value += FastSin((angle * 3f + swirl * 10f + time) * MathF.PI) * 0.4f;

                // Normalize to 0-1 (dividing by approximate max amplitude)
                value = (value / 7f + 1f) / 2f;
                value = Math.Clamp(value, 0f, 1f);

                plasmaBuffer[sy * scaledWidth + sx] = value;
            }
        }

        // Upscale and apply color palette using ProcessPixelRowsAsVector
        image.ProcessPixelRows(accessor =>
        {
            for (var py = 0; py < height; py++)
            {
                var sy = py / ScaleFactor;
                if (sy >= scaledHeight) sy = scaledHeight - 1;

                var row = accessor.GetRowSpan(py);

                for (var px = 0; px < width; px++)
                {
                    var sx = px / ScaleFactor;
                    if (sx >= scaledWidth) sx = scaledWidth - 1;

                    var value = plasmaBuffer[sy * scaledWidth + sx];

                    // Apply animated color palette
                    row[px] = GetPlasmaColor(value, time);
                }
            }
        });

        return Task.FromResult(image);
    }

    private float FastSin(float angle)
    {
        if (_sinTable == null) return MathF.Sin(angle);

        // Normalize angle to 0-2PI range
        var normalized = angle % (MathF.PI * 2f);
        if (normalized < 0) normalized += MathF.PI * 2f;

        // Convert to table index
        var index = (int)(normalized / (MathF.PI * 2f) * _sinTable.Length) % _sinTable.Length;
        return _sinTable[index];
    }

    private float FastCos(float angle)
    {
        if (_cosTable == null) return MathF.Cos(angle);

        var normalized = angle % (MathF.PI * 2f);
        if (normalized < 0) normalized += MathF.PI * 2f;

        var index = (int)(normalized / (MathF.PI * 2f) * _cosTable.Length) % _cosTable.Length;
        return _cosTable[index];
    }

    private static Rgba32 GetPlasmaColor(float value, float time)
    {
        // Create a cycling color palette
        var phase = value * MathF.PI * 2f + time * 0.5f;

        // Three phase-shifted sine waves for RGB
        var r = (MathF.Sin(phase) + 1f) / 2f;
        var g = (MathF.Sin(phase + MathF.PI * 2f / 3f) + 1f) / 2f;
        var b = (MathF.Sin(phase + MathF.PI * 4f / 3f) + 1f) / 2f;

        // Add some variation based on time
        r = MathF.Pow(r, 0.8f + MathF.Sin(time * 0.2f) * 0.2f);
        g = MathF.Pow(g, 0.8f + MathF.Sin(time * 0.3f + 1f) * 0.2f);
        b = MathF.Pow(b, 0.8f + MathF.Sin(time * 0.4f + 2f) * 0.2f);

        return new Rgba32(
            (byte)(r * 255),
            (byte)(g * 255),
            (byte)(b * 255));
    }
}
