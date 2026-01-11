using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Classic Mystify screensaver - bouncing connected polygons with color trails.
/// </summary>
public sealed class MystifyPanel : BaseLivePanel
{
    private const int PolygonCount = 2;
    private const int VerticesPerPolygon = 4;
    private const int TrailLength = 8;
    private const float MinSpeed = 50f;
    private const float MaxSpeed = 150f;

    private readonly Random _random;
    private Polygon[]? _polygons;
    private DateTime _lastUpdate;
    private int _width;
    private int _height;

    public override string PanelId => "mystify";
    public override string DisplayName => "Mystify";
    public override bool IsAnimated => true;

    public MystifyPanel()
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
        // Initialize polygons if dimensions changed
        if (_width != width || _height != height || _polygons == null)
        {
            _width = width;
            _height = height;
            InitializePolygons();
        }

        // Calculate delta time
        var now = DateTime.UtcNow;
        var deltaTime = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        // Update polygons
        UpdatePolygons(deltaTime);

        // Render
        var image = new Image<Rgba32>(width, height, new Rgba32(0, 0, 0));

        image.Mutate(ctx =>
        {
            foreach (var polygon in _polygons!)
            {
                DrawPolygonTrail(ctx, polygon);
            }
        });

        return Task.FromResult(image);
    }

    private void InitializePolygons()
    {
        _polygons = new Polygon[PolygonCount];

        for (var i = 0; i < PolygonCount; i++)
        {
            _polygons[i] = CreatePolygon(i);
        }
    }

    private Polygon CreatePolygon(int index)
    {
        var vertices = new Vertex[VerticesPerPolygon];

        for (var i = 0; i < VerticesPerPolygon; i++)
        {
            vertices[i] = new Vertex
            {
                X = _random.NextSingle() * _width,
                Y = _random.NextSingle() * _height,
                Vx = RandomVelocity(),
                Vy = RandomVelocity(),
                Trail = new PointF[TrailLength]
            };

            // Initialize trail to current position
            for (var j = 0; j < TrailLength; j++)
            {
                vertices[i].Trail[j] = new PointF(vertices[i].X, vertices[i].Y);
            }
        }

        // Different base hue for each polygon
        var baseHue = index * (360f / PolygonCount);

        return new Polygon
        {
            Vertices = vertices,
            BaseHue = baseHue,
            HueShift = 0
        };
    }

    private float RandomVelocity()
    {
        var speed = MinSpeed + _random.NextSingle() * (MaxSpeed - MinSpeed);
        return _random.NextSingle() > 0.5f ? speed : -speed;
    }

    private void UpdatePolygons(float deltaTime)
    {
        foreach (var polygon in _polygons!)
        {
            // Shift hue over time
            polygon.HueShift += deltaTime * 30f;
            if (polygon.HueShift > 360f) polygon.HueShift -= 360f;

            foreach (var vertex in polygon.Vertices)
            {
                // Store current position in trail
                for (var i = TrailLength - 1; i > 0; i--)
                {
                    vertex.Trail[i] = vertex.Trail[i - 1];
                }
                vertex.Trail[0] = new PointF(vertex.X, vertex.Y);

                // Update position
                vertex.X += vertex.Vx * deltaTime;
                vertex.Y += vertex.Vy * deltaTime;

                // Bounce off edges
                if (vertex.X <= 0)
                {
                    vertex.X = 0;
                    vertex.Vx = MathF.Abs(vertex.Vx);
                }
                else if (vertex.X >= _width)
                {
                    vertex.X = _width;
                    vertex.Vx = -MathF.Abs(vertex.Vx);
                }

                if (vertex.Y <= 0)
                {
                    vertex.Y = 0;
                    vertex.Vy = MathF.Abs(vertex.Vy);
                }
                else if (vertex.Y >= _height)
                {
                    vertex.Y = _height;
                    vertex.Vy = -MathF.Abs(vertex.Vy);
                }
            }
        }
    }

    private void DrawPolygonTrail(IImageProcessingContext ctx, Polygon polygon)
    {
        // Draw trail (oldest first, so newest is on top)
        for (var t = TrailLength - 1; t >= 0; t--)
        {
            var alpha = 1f - (float)t / TrailLength;
            var hue = (polygon.BaseHue + polygon.HueShift + t * 5f) % 360f;
            var color = HslToRgba(hue, 1f, 0.5f, alpha);

            var points = new PointF[VerticesPerPolygon + 1];
            for (var i = 0; i < VerticesPerPolygon; i++)
            {
                points[i] = polygon.Vertices[i].Trail[t];
            }
            points[VerticesPerPolygon] = points[0]; // Close the polygon

            // Draw connected lines
            for (var i = 0; i < VerticesPerPolygon; i++)
            {
                var next = (i + 1) % VerticesPerPolygon;
                ctx.DrawLine(color, 2f, points[i], points[next]);
            }
        }

        // Draw current position (brightest)
        var currentHue = (polygon.BaseHue + polygon.HueShift) % 360f;
        var currentColor = HslToRgba(currentHue, 1f, 0.6f, 1f);

        var currentPoints = new PointF[VerticesPerPolygon];
        for (var i = 0; i < VerticesPerPolygon; i++)
        {
            currentPoints[i] = new PointF(polygon.Vertices[i].X, polygon.Vertices[i].Y);
        }

        for (var i = 0; i < VerticesPerPolygon; i++)
        {
            var next = (i + 1) % VerticesPerPolygon;
            ctx.DrawLine(currentColor, 2.5f, currentPoints[i], currentPoints[next]);
        }
    }

    private static Rgba32 HslToRgba(float h, float s, float l, float alpha)
    {
        // Normalize hue to 0-360
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

    private class Polygon
    {
        public Vertex[] Vertices { get; set; } = [];
        public float BaseHue { get; set; }
        public float HueShift { get; set; }
    }

    private class Vertex
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Vx { get; set; }
        public float Vy { get; set; }
        public PointF[] Trail { get; set; } = [];
    }
}
