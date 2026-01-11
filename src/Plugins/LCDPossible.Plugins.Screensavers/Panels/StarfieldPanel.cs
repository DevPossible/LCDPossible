using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Classic starfield warp effect - stars streaming outward from center.
/// </summary>
public sealed class StarfieldPanel : BaseLivePanel
{
    private const int StarCount = 200;
    private const float MaxDepth = 32f;
    private const float Speed = 0.5f;

    private readonly Star[] _stars;
    private readonly Random _random;
    private DateTime _lastUpdate;
    private int _width;
    private int _height;

    public override string PanelId => "starfield";
    public override string DisplayName => "Starfield";
    public override bool IsAnimated => true;

    public StarfieldPanel()
    {
        _random = new Random();
        _stars = new Star[StarCount];
        _lastUpdate = DateTime.UtcNow;
    }

    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Stars will be initialized on first render when we know the dimensions
        return Task.CompletedTask;
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        // Initialize stars if dimensions changed
        if (_width != width || _height != height)
        {
            _width = width;
            _height = height;
            InitializeStars();
        }

        // Calculate delta time
        var now = DateTime.UtcNow;
        var deltaTime = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        // Update stars
        UpdateStars(deltaTime);

        // Render
        var image = new Image<Rgba32>(width, height, BackgroundColor);

        image.Mutate(ctx =>
        {
            var centerX = width / 2f;
            var centerY = height / 2f;

            foreach (var star in _stars)
            {
                // Project 3D position to 2D screen
                var factor = 128f / star.Z;
                var screenX = centerX + star.X * factor;
                var screenY = centerY + star.Y * factor;

                // Skip if outside screen
                if (screenX < 0 || screenX >= width || screenY < 0 || screenY >= height)
                {
                    continue;
                }

                // Calculate star size and brightness based on depth
                var brightness = 1f - (star.Z / MaxDepth);
                var size = Math.Max(1f, (1f - star.Z / MaxDepth) * 3f);

                // Draw the star as a filled circle
                var gray = (byte)(brightness * 255);
                var color = new Rgba32(gray, gray, gray);

                if (size <= 1.5f)
                {
                    // Draw as single pixel for small stars (faster)
                    image[(int)screenX, (int)screenY] = color;
                }
                else
                {
                    // Draw as circle for larger stars
                    ctx.Fill(color, new EllipsePolygon(screenX, screenY, size));
                }

                // Draw motion trail for fast-moving stars
                if (star.Z < MaxDepth * 0.3f)
                {
                    var trailFactor = 128f / (star.Z + Speed * 5);
                    var trailX = centerX + star.X * trailFactor;
                    var trailY = centerY + star.Y * trailFactor;

                    var trailBrightness = brightness * 0.3f;
                    var trailGray = (byte)(trailBrightness * 255);
                    var trailColor = new Rgba32(trailGray, trailGray, trailGray);

                    ctx.DrawLine(trailColor, 1f, new PointF(trailX, trailY), new PointF(screenX, screenY));
                }
            }
        });

        return Task.FromResult(image);
    }

    private void InitializeStars()
    {
        for (var i = 0; i < StarCount; i++)
        {
            _stars[i] = CreateRandomStar(randomZ: true);
        }
    }

    private Star CreateRandomStar(bool randomZ)
    {
        return new Star
        {
            X = (_random.NextSingle() - 0.5f) * _width,
            Y = (_random.NextSingle() - 0.5f) * _height,
            Z = randomZ ? _random.NextSingle() * MaxDepth : MaxDepth
        };
    }

    private void UpdateStars(float deltaTime)
    {
        var moveAmount = Speed * deltaTime * 60; // Normalize to ~60fps

        for (var i = 0; i < StarCount; i++)
        {
            _stars[i].Z -= moveAmount;

            // Reset star when it reaches the viewer
            if (_stars[i].Z <= 0)
            {
                _stars[i] = CreateRandomStar(randomZ: false);
            }
        }
    }

    private struct Star
    {
        public float X;
        public float Y;
        public float Z;
    }
}
