using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Floating, bouncing translucent bubbles.
/// </summary>
public sealed class BubblesPanel : BaseLivePanel
{
    private const int BubbleCount = 15;
    private const float MinRadius = 20f;
    private const float MaxRadius = 80f;
    private const float MinSpeed = 30f;
    private const float MaxSpeed = 80f;

    private readonly Random _random;
    private Bubble[]? _bubbles;
    private DateTime _lastUpdate;
    private int _width;
    private int _height;

    public override string PanelId => "bubbles";
    public override string DisplayName => "Bubbles";
    public override bool IsAnimated => true;

    public BubblesPanel()
    {
        _random = new Random();
        _lastUpdate = DateTime.UtcNow;
    }

    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        // Initialize bubbles if dimensions changed
        if (_width != width || _height != height || _bubbles == null)
        {
            _width = width;
            _height = height;
            InitializeBubbles();
        }

        // Calculate delta time
        var now = DateTime.UtcNow;
        var deltaTime = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        // Update bubbles
        UpdateBubbles(deltaTime);

        // Render with gradient background
        var image = new Image<Rgba32>(width, height);

        // Dark gradient background
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var intensity = (byte)(10 + (y * 20 / height));
                var bgColor = new Rgba32(intensity, intensity, (byte)(intensity + 10));
                for (var x = 0; x < width; x++)
                {
                    row[x] = bgColor;
                }
            }
        });

        // Draw bubbles (sorted by depth for proper overlapping)
        var sortedBubbles = _bubbles!.OrderBy(b => b.Radius).ToArray();

        image.Mutate(ctx =>
        {
            foreach (var bubble in sortedBubbles)
            {
                DrawBubble(ctx, bubble);
            }
        });

        return Task.FromResult(image);
    }

    private void InitializeBubbles()
    {
        _bubbles = new Bubble[BubbleCount];

        for (var i = 0; i < BubbleCount; i++)
        {
            _bubbles[i] = CreateBubble();
        }
    }

    private Bubble CreateBubble()
    {
        var radius = MinRadius + _random.NextSingle() * (MaxRadius - MinRadius);
        var speed = MinSpeed + _random.NextSingle() * (MaxSpeed - MinSpeed);
        var angle = _random.NextSingle() * MathF.PI * 2f;

        return new Bubble
        {
            X = _random.NextSingle() * _width,
            Y = _random.NextSingle() * _height,
            Radius = radius,
            Vx = MathF.Cos(angle) * speed,
            Vy = MathF.Sin(angle) * speed,
            Hue = _random.NextSingle() * 360f,
            Phase = _random.NextSingle() * MathF.PI * 2f
        };
    }

    private void UpdateBubbles(float deltaTime)
    {
        foreach (var bubble in _bubbles!)
        {
            // Update position
            bubble.X += bubble.Vx * deltaTime;
            bubble.Y += bubble.Vy * deltaTime;

            // Animate phase for shimmer effect
            bubble.Phase += deltaTime * 2f;

            // Bounce off edges
            if (bubble.X - bubble.Radius < 0)
            {
                bubble.X = bubble.Radius;
                bubble.Vx = MathF.Abs(bubble.Vx);
            }
            else if (bubble.X + bubble.Radius > _width)
            {
                bubble.X = _width - bubble.Radius;
                bubble.Vx = -MathF.Abs(bubble.Vx);
            }

            if (bubble.Y - bubble.Radius < 0)
            {
                bubble.Y = bubble.Radius;
                bubble.Vy = MathF.Abs(bubble.Vy);
            }
            else if (bubble.Y + bubble.Radius > _height)
            {
                bubble.Y = _height - bubble.Radius;
                bubble.Vy = -MathF.Abs(bubble.Vy);
            }
        }
    }

    private void DrawBubble(IImageProcessingContext ctx, Bubble bubble)
    {
        // Main bubble body (translucent)
        var mainAlpha = 0.3f + MathF.Sin(bubble.Phase) * 0.1f;
        var mainColor = HslToRgba(bubble.Hue, 0.6f, 0.5f, mainAlpha);

        ctx.Fill(mainColor, new EllipsePolygon(bubble.X, bubble.Y, bubble.Radius));

        // Bubble outline
        var outlineColor = HslToRgba(bubble.Hue, 0.7f, 0.6f, 0.6f);
        ctx.Draw(outlineColor, 2f, new EllipsePolygon(bubble.X, bubble.Y, bubble.Radius));

        // Highlight (top-left shine)
        var highlightX = bubble.X - bubble.Radius * 0.3f;
        var highlightY = bubble.Y - bubble.Radius * 0.3f;
        var highlightRadius = bubble.Radius * 0.25f;
        var highlightColor = new Rgba32(255, 255, 255, 150);

        ctx.Fill(highlightColor, new EllipsePolygon(highlightX, highlightY, highlightRadius, highlightRadius * 0.6f));
    }

    private static Rgba32 HslToRgba(float h, float s, float l, float alpha)
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
            (byte)((b + m) * 255),
            (byte)(alpha * 255));
    }

    private class Bubble
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Radius { get; set; }
        public float Vx { get; set; }
        public float Vy { get; set; }
        public float Hue { get; set; }
        public float Phase { get; set; }
    }
}
