using LCDPossible.Sdk;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Classic bouncing logo screensaver (DVD-style).
/// </summary>
public sealed class BouncingLogoPanel : BaseLivePanel
{
    private const string LogoText = "LCD";
    private const float Speed = 100f; // Pixels per second
    private const int LogoWidth = 180;
    private const int LogoHeight = 80;

    private static readonly Rgba32[] LogoColors =
    [
        new Rgba32(255, 0, 0),     // Red
        new Rgba32(0, 255, 0),     // Green
        new Rgba32(0, 0, 255),     // Blue
        new Rgba32(255, 255, 0),   // Yellow
        new Rgba32(255, 0, 255),   // Magenta
        new Rgba32(0, 255, 255),   // Cyan
        new Rgba32(255, 128, 0),   // Orange
        new Rgba32(128, 0, 255),   // Purple
    ];

    private readonly Random _random;
    private float _x;
    private float _y;
    private float _vx;
    private float _vy;
    private int _colorIndex;
    private DateTime _lastUpdate;
    private Font? _logoFont;
    private int _width;
    private int _height;
    private bool _initialized;

    public override string PanelId => "bouncing-logo";
    public override string DisplayName => "Bouncing Logo";
    public override bool IsAnimated => true;

    public BouncingLogoPanel()
    {
        _random = new Random();
        _colorIndex = _random.Next(LogoColors.Length);
        _lastUpdate = DateTime.UtcNow;
    }

    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Load a bold font for the logo
        try
        {
            var families = SystemFonts.Families.ToArray();
            var family = families.FirstOrDefault(f =>
                f.Name.Contains("Arial", StringComparison.OrdinalIgnoreCase) ||
                f.Name.Contains("Helvetica", StringComparison.OrdinalIgnoreCase));

            if (family.Name != null)
            {
                _logoFont = family.CreateFont(64, FontStyle.Bold);
            }
            else if (families.Length > 0)
            {
                _logoFont = families[0].CreateFont(64, FontStyle.Bold);
            }
        }
        catch
        {
            // Font loading failed - we'll draw a rectangle
        }

        return Task.CompletedTask;
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        // Initialize position if dimensions changed
        if (_width != width || _height != height || !_initialized)
        {
            _width = width;
            _height = height;
            InitializePosition();
            _initialized = true;
        }

        // Calculate delta time
        var now = DateTime.UtcNow;
        var deltaTime = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        // Update position
        UpdatePosition(deltaTime);

        // Render
        var image = new Image<Rgba32>(width, height, new Rgba32(0, 0, 0));

        image.Mutate(ctx =>
        {
            var color = LogoColors[_colorIndex];

            if (_logoFont != null)
            {
                // Draw text logo
                ctx.DrawText(LogoText, _logoFont, color, new PointF(_x, _y));
            }
            else
            {
                // Fallback: draw a colored rectangle
                var rect = new RectangleF(_x, _y, LogoWidth, LogoHeight);
                ctx.Fill(color, rect);

                // Add a border
                var borderColor = new Rgba32(
                    (byte)Math.Min(255, color.R + 50),
                    (byte)Math.Min(255, color.G + 50),
                    (byte)Math.Min(255, color.B + 50));
                ctx.Draw(borderColor, 3f, rect);
            }
        });

        return Task.FromResult(image);
    }

    private void InitializePosition()
    {
        // Start at random position
        _x = _random.Next(0, Math.Max(1, _width - LogoWidth));
        _y = _random.Next(0, Math.Max(1, _height - LogoHeight));

        // Random direction with slight angle variation
        var angle = _random.NextSingle() * MathF.PI * 2;
        _vx = MathF.Cos(angle) * Speed;
        _vy = MathF.Sin(angle) * Speed;

        // Ensure minimum velocity in both directions
        if (MathF.Abs(_vx) < Speed * 0.3f) _vx = Speed * 0.5f * MathF.Sign(_vx);
        if (MathF.Abs(_vy) < Speed * 0.3f) _vy = Speed * 0.5f * MathF.Sign(_vy);
    }

    private void UpdatePosition(float deltaTime)
    {
        _x += _vx * deltaTime;
        _y += _vy * deltaTime;

        var bounced = false;

        // Bounce off edges
        if (_x <= 0)
        {
            _x = 0;
            _vx = MathF.Abs(_vx);
            bounced = true;
        }
        else if (_x >= _width - LogoWidth)
        {
            _x = _width - LogoWidth;
            _vx = -MathF.Abs(_vx);
            bounced = true;
        }

        if (_y <= 0)
        {
            _y = 0;
            _vy = MathF.Abs(_vy);
            bounced = true;
        }
        else if (_y >= _height - LogoHeight)
        {
            _y = _height - LogoHeight;
            _vy = -MathF.Abs(_vy);
            bounced = true;
        }

        // Change color on bounce
        if (bounced)
        {
            _colorIndex = (_colorIndex + 1) % LogoColors.Length;
        }
    }
}
