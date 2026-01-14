using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using LCDPossible.Core.Rendering;
namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Falling raindrops with splash effects.
/// </summary>
public sealed class RainPanel : CanvasPanel
{
    private const int DropCount = 150;
    private const int MaxSplashes = 20;
    private const float MinSpeed = 300f;
    private const float MaxSpeed = 600f;

    private readonly Random _random;
    private Raindrop[]? _drops;
    private readonly List<Splash> _splashes = new();
    private DateTime _lastUpdate;
    private int _width;
    private int _height;

    public override string PanelId => "rain";
    public override string DisplayName => "Rain";
    public override PanelRenderMode RenderMode => PanelRenderMode.Stream;

    public RainPanel()
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
        // Initialize drops if dimensions changed
        if (_width != width || _height != height || _drops == null)
        {
            _width = width;
            _height = height;
            InitializeDrops();
        }

        // Calculate delta time
        var now = DateTime.UtcNow;
        var deltaTime = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        // Update
        UpdateDrops(deltaTime);
        UpdateSplashes(deltaTime);

        // Render with dark blue gradient background
        var image = new Image<Rgba32>(width, height);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var t = (float)y / height;
                var r = (byte)(5 + t * 10);
                var g = (byte)(10 + t * 15);
                var b = (byte)(30 + t * 20);
                var bgColor = new Rgba32(r, g, b);

                for (var x = 0; x < width; x++)
                {
                    row[x] = bgColor;
                }
            }
        });

        image.Mutate(ctx =>
        {
            // Draw raindrops
            foreach (var drop in _drops!)
            {
                var alpha = (byte)(100 + drop.Speed / MaxSpeed * 155);
                var color = new Rgba32(150, 180, 220, alpha);

                var length = 5f + drop.Speed / MaxSpeed * 15f;
                ctx.DrawLine(color, 1.5f,
                    new PointF(drop.X, drop.Y),
                    new PointF(drop.X - drop.Wind * 0.1f, drop.Y - length));
            }

            // Draw splashes
            foreach (var splash in _splashes)
            {
                var alpha = (byte)(200 * (1f - splash.Age / splash.MaxAge));
                var color = new Rgba32(180, 200, 230, alpha);

                var radius = splash.Radius * (0.5f + splash.Age / splash.MaxAge * 0.5f);

                // Draw splash ring
                for (var i = 0; i < 8; i++)
                {
                    var angle = i * MathF.PI / 4f;
                    var px = splash.X + MathF.Cos(angle) * radius;
                    var py = splash.Y + MathF.Sin(angle) * radius * 0.3f; // Flatten for perspective

                    ctx.Fill(color, new SixLabors.ImageSharp.Drawing.EllipsePolygon(px, py, 2, 1));
                }
            }
        });

        return Task.FromResult(image);
    }

    private void InitializeDrops()
    {
        _drops = new Raindrop[DropCount];

        for (var i = 0; i < DropCount; i++)
        {
            _drops[i] = CreateDrop(randomY: true);
        }
    }

    private Raindrop CreateDrop(bool randomY)
    {
        return new Raindrop
        {
            X = _random.NextSingle() * _width,
            Y = randomY ? _random.NextSingle() * _height : -10f,
            Speed = MinSpeed + _random.NextSingle() * (MaxSpeed - MinSpeed),
            Wind = -20f + _random.NextSingle() * 10f // Slight wind effect
        };
    }

    private void UpdateDrops(float deltaTime)
    {
        for (var i = 0; i < _drops!.Length; i++)
        {
            var drop = _drops[i];

            drop.Y += drop.Speed * deltaTime;
            drop.X += drop.Wind * deltaTime;

            // Reset drop when it reaches bottom
            if (drop.Y > _height)
            {
                // Create splash
                if (_splashes.Count < MaxSplashes)
                {
                    _splashes.Add(new Splash
                    {
                        X = drop.X,
                        Y = _height - 5f,
                        Radius = 5f + _random.NextSingle() * 10f,
                        Age = 0f,
                        MaxAge = 0.3f + _random.NextSingle() * 0.2f
                    });
                }

                _drops[i] = CreateDrop(randomY: false);
            }
            else
            {
                _drops[i] = drop;
            }
        }
    }

    private void UpdateSplashes(float deltaTime)
    {
        for (var i = _splashes.Count - 1; i >= 0; i--)
        {
            var splash = _splashes[i];
            splash.Age += deltaTime;

            if (splash.Age >= splash.MaxAge)
            {
                _splashes.RemoveAt(i);
            }
            else
            {
                _splashes[i] = splash;
            }
        }
    }

    private struct Raindrop
    {
        public float X;
        public float Y;
        public float Speed;
        public float Wind;
    }

    private struct Splash
    {
        public float X;
        public float Y;
        public float Radius;
        public float Age;
        public float MaxAge;
    }
}
