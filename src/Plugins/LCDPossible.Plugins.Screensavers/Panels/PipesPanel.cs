using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// 3D pipes growing in random directions (classic Windows screensaver).
/// </summary>
public sealed class PipesPanel : BaseLivePanel
{
    private const int GridSize = 40;
    private const int PipeRadius = 8;
    private const int JointRadius = 12;
    private const int MaxSegments = 200;

    private readonly Random _random;
    private readonly List<PipeSegment> _segments = new();
    private DateTime _lastUpdate;
    private int _width;
    private int _height;
    private int _gridWidth;
    private int _gridHeight;
    private Point3D _currentPos;
    private int _currentDirection;
    private Rgba32 _currentColor;
    private bool[,,]? _occupied;

    public override string PanelId => "pipes";
    public override string DisplayName => "Pipes";
    public override bool IsAnimated => true;

    public PipesPanel()
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
        if (_width != width || _height != height)
        {
            _width = width;
            _height = height;
            _gridWidth = width / GridSize;
            _gridHeight = height / GridSize;
            ResetPipes();
        }

        var now = DateTime.UtcNow;
        var deltaTime = (float)(now - _lastUpdate).TotalSeconds;

        if (deltaTime >= 0.05f) // Add segment every 50ms
        {
            _lastUpdate = now;
            GrowPipe();
        }

        // Render
        var image = new Image<Rgba32>(width, height, new Rgba32(0, 0, 0));

        image.Mutate(ctx =>
        {
            foreach (var segment in _segments)
            {
                DrawSegment(ctx, segment);
            }
        });

        return Task.FromResult(image);
    }

    private void ResetPipes()
    {
        _segments.Clear();
        _occupied = new bool[_gridWidth, _gridHeight, 6]; // 6 depth layers

        // Start at random position
        _currentPos = new Point3D(
            _random.Next(_gridWidth),
            _random.Next(_gridHeight),
            _random.Next(6));

        _currentDirection = _random.Next(6);
        _currentColor = GetRandomColor();
    }

    private void GrowPipe()
    {
        if (_segments.Count >= MaxSegments)
        {
            ResetPipes();
            return;
        }

        // Try to find valid next position
        var attempts = 0;
        var nextPos = _currentPos;
        var foundValid = false;

        while (attempts < 10 && !foundValid)
        {
            // Pick a direction (prefer to continue straight, but sometimes turn)
            var newDir = _random.NextDouble() < 0.7 ? _currentDirection : _random.Next(6);

            nextPos = GetNextPosition(_currentPos, newDir);

            if (IsValidPosition(nextPos))
            {
                _currentDirection = newDir;
                foundValid = true;
            }

            attempts++;
        }

        if (!foundValid)
        {
            // Start new pipe from random position
            _currentPos = new Point3D(
                _random.Next(_gridWidth),
                _random.Next(_gridHeight),
                _random.Next(6));
            _currentColor = GetRandomColor();
            return;
        }

        // Add segment
        _segments.Add(new PipeSegment
        {
            Start = _currentPos,
            End = nextPos,
            Color = _currentColor
        });

        _occupied![nextPos.X, nextPos.Y, nextPos.Z] = true;
        _currentPos = nextPos;

        // Occasionally change color
        if (_random.NextDouble() < 0.05)
        {
            _currentColor = GetRandomColor();
        }
    }

    private Point3D GetNextPosition(Point3D pos, int direction)
    {
        return direction switch
        {
            0 => new Point3D(pos.X + 1, pos.Y, pos.Z), // Right
            1 => new Point3D(pos.X - 1, pos.Y, pos.Z), // Left
            2 => new Point3D(pos.X, pos.Y + 1, pos.Z), // Down
            3 => new Point3D(pos.X, pos.Y - 1, pos.Z), // Up
            4 => new Point3D(pos.X, pos.Y, pos.Z + 1), // Forward (larger)
            5 => new Point3D(pos.X, pos.Y, pos.Z - 1), // Back (smaller)
            _ => pos
        };
    }

    private bool IsValidPosition(Point3D pos)
    {
        if (pos.X < 0 || pos.X >= _gridWidth) return false;
        if (pos.Y < 0 || pos.Y >= _gridHeight) return false;
        if (pos.Z < 0 || pos.Z >= 6) return false;
        return !_occupied![pos.X, pos.Y, pos.Z];
    }

    private void DrawSegment(IImageProcessingContext ctx, PipeSegment segment)
    {
        // Calculate screen positions with depth perspective
        var depthScale1 = 0.5f + segment.Start.Z * 0.15f;
        var depthScale2 = 0.5f + segment.End.Z * 0.15f;

        var x1 = segment.Start.X * GridSize + GridSize / 2f;
        var y1 = segment.Start.Y * GridSize + GridSize / 2f;
        var x2 = segment.End.X * GridSize + GridSize / 2f;
        var y2 = segment.End.Y * GridSize + GridSize / 2f;

        var radius1 = PipeRadius * depthScale1;
        var radius2 = PipeRadius * depthScale2;

        // Adjust color by depth
        var brightness = 0.5f + segment.Start.Z * 0.1f;
        var color = new Rgba32(
            (byte)(segment.Color.R * brightness),
            (byte)(segment.Color.G * brightness),
            (byte)(segment.Color.B * brightness));

        // Draw pipe segment
        ctx.DrawLine(color, radius1 * 2, new PointF(x1, y1), new PointF(x2, y2));

        // Draw joint at start
        var jointRadius = JointRadius * depthScale1;
        ctx.Fill(color, new EllipsePolygon(x1, y1, jointRadius));

        // Draw highlight on joint
        var highlightColor = new Rgba32(
            (byte)Math.Min(255, segment.Color.R + 60),
            (byte)Math.Min(255, segment.Color.G + 60),
            (byte)Math.Min(255, segment.Color.B + 60));
        ctx.Fill(highlightColor, new EllipsePolygon(x1 - jointRadius * 0.3f, y1 - jointRadius * 0.3f, jointRadius * 0.4f));
    }

    private Rgba32 GetRandomColor()
    {
        var colors = new[]
        {
            new Rgba32(220, 50, 50),   // Red
            new Rgba32(50, 220, 50),   // Green
            new Rgba32(50, 50, 220),   // Blue
            new Rgba32(220, 220, 50),  // Yellow
            new Rgba32(220, 50, 220),  // Magenta
            new Rgba32(50, 220, 220),  // Cyan
            new Rgba32(220, 140, 50),  // Orange
            new Rgba32(180, 180, 180), // Silver
        };
        return colors[_random.Next(colors.Length)];
    }

    private record struct Point3D(int X, int Y, int Z);

    private struct PipeSegment
    {
        public Point3D Start;
        public Point3D End;
        public Rgba32 Color;
    }
}
