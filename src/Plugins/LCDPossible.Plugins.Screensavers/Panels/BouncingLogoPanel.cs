using System.Numerics;
using LCDPossible.Sdk;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using LCDPossible.Core.Rendering;
namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Classic bouncing logo screensaver (DVD-style) with configurable options.
/// </summary>
public sealed class BouncingLogoPanel : CanvasPanel
{
    /// <summary>
    /// Color mode for the bouncing text.
    /// </summary>
    public enum ColorMode
    {
        /// <summary>Fixed color (from HTML code or name).</summary>
        Fixed,
        /// <summary>Random color on each bounce.</summary>
        Random,
        /// <summary>Cycle through predefined colors on each bounce.</summary>
        Cycle,
        /// <summary>Smoothly transition through rainbow colors.</summary>
        Rainbow
    }

    /// <summary>
    /// Size preset for the text.
    /// </summary>
    public enum SizePreset
    {
        Small,
        Medium,
        Large
    }

    private const float BaseSpeed = 100f; // Pixels per second

    private static readonly Rgba32[] CycleColors =
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

    // Configurable parameters
    private string _text = "LCD";
    private ColorMode _colorMode = ColorMode.Cycle;
    private Rgba32 _fixedColor = new(255, 255, 255);
    private SizePreset _size = SizePreset.Medium;
    private bool _is3D;
    private bool _rotate;
    private int _thickness = -1; // -1 = random, 1-10 = fixed thickness
    private int _speed = 5; // 1 = slow, 5 = normal, 10 = fast

    // Animation state
    private readonly Random _random;
    private float _x;
    private float _y;
    private float _vx;
    private float _vy;
    private int _colorIndex;
    private float _rainbowHue;
    private DateTime _lastUpdate;
    private Font? _logoFont;
    private int _width;
    private int _height;
    private bool _initialized;
    private float _logoWidth;
    private float _logoHeight;

    // Rotation state
    private float _rotationAngle;     // Z-axis rotation (2D plane)
    private float _rotationSpeedZ;    // Rotation speed around Z
    private float _rotationX;         // X-axis tilt (3D only)
    private float _rotationY;         // Y-axis tilt (3D only)
    private float _rotationSpeedX;    // Rotation speed around X (3D only)
    private float _rotationSpeedY;    // Rotation speed around Y (3D only)
    private DateTime _nextSpinChange; // When to randomize spin speeds

    // Resolved thickness (1-10)
    private int _resolvedThickness;

    public override string PanelId => "bouncing-logo";
    public override string DisplayName => "Bouncing Logo";
    public override PanelRenderMode RenderMode => PanelRenderMode.Stream;

    public BouncingLogoPanel()
    {
        _random = new Random();
        _colorIndex = _random.Next(CycleColors.Length);
        _lastUpdate = DateTime.UtcNow;
        _nextSpinChange = DateTime.UtcNow;
        RandomizeSpinSpeeds();
    }

    #region Configuration Methods

    /// <summary>
    /// Sets the text to display.
    /// </summary>
    public void SetText(string text)
    {
        _text = string.IsNullOrWhiteSpace(text) ? "LCD" : text;
        _initialized = false; // Force recalculation of text bounds
    }

    /// <summary>
    /// Sets the color mode to cycle through predefined colors.
    /// </summary>
    public void SetColorCycle()
    {
        _colorMode = ColorMode.Cycle;
    }

    /// <summary>
    /// Sets the color mode to random colors on each bounce.
    /// </summary>
    public void SetColorRandom()
    {
        _colorMode = ColorMode.Random;
    }

    /// <summary>
    /// Sets the color mode to smooth rainbow transition.
    /// </summary>
    public void SetColorRainbow()
    {
        _colorMode = ColorMode.Rainbow;
    }

    /// <summary>
    /// Sets a fixed color from an HTML color code or color name.
    /// </summary>
    public void SetFixedColor(string colorValue)
    {
        _colorMode = ColorMode.Fixed;

        if (TryParseColor(colorValue, out var color))
        {
            _fixedColor = color;
        }
        else
        {
            // Default to white if parsing fails
            _fixedColor = new Rgba32(255, 255, 255);
        }
    }

    /// <summary>
    /// Sets the size preset.
    /// </summary>
    public void SetSize(SizePreset size)
    {
        _size = size;
        _initialized = false; // Force font reload
    }

    /// <summary>
    /// Enables or disables 3D effect.
    /// </summary>
    public void Set3D(bool enabled)
    {
        _is3D = enabled;
    }

    /// <summary>
    /// Enables or disables rotation.
    /// </summary>
    public void SetRotate(bool enabled)
    {
        _rotate = enabled;
        if (enabled)
        {
            RandomizeSpinSpeeds();
        }
    }

    /// <summary>
    /// Sets the text thickness (1-10). Use -1 or 0 for random.
    /// Affects stroke width and 3D depth.
    /// </summary>
    public void SetThickness(int thickness)
    {
        _thickness = thickness <= 0 ? -1 : Math.Clamp(thickness, 1, 10);
    }

    /// <summary>
    /// Sets the movement speed (1-10).
    /// 1 = slow (0.2x), 5 = normal (1.0x), 10 = fast (2.0x).
    /// </summary>
    public void SetSpeed(int speed)
    {
        _speed = Math.Clamp(speed, 1, 10);
    }

    /// <summary>
    /// Gets the speed multiplier based on the configured speed.
    /// </summary>
    private float GetSpeedMultiplier()
    {
        // Piecewise linear: speed 1 → 0.05x (very slow), speed 5 → 1.0x, speed 10 → 2.0x
        if (_speed <= 5)
        {
            // Speed 1-5: 0.05x to 1.0x
            return 0.05f + (_speed - 1) * 0.2375f;
        }
        else
        {
            // Speed 6-10: 1.0x to 2.0x
            return 1.0f + (_speed - 5) * 0.2f;
        }
    }

    #endregion

    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Resolve thickness: random if not specified
        _resolvedThickness = _thickness < 0 ? _random.Next(1, 8) : _thickness;

        LoadLogoFont();
        return Task.CompletedTask;
    }

    private void LoadLogoFont()
    {
        var fontSize = _size switch
        {
            SizePreset.Small => 32f,
            SizePreset.Medium => 64f,
            SizePreset.Large => 96f,
            _ => 64f
        };

        _logoFont = FontHelper.GetPreferredFont(fontSize, FontStyle.Bold);

        // Measure text bounds
        if (_logoFont != null)
        {
            var bounds = TextMeasurer.MeasureBounds(_text, new TextOptions(_logoFont));
            _logoWidth = bounds.Width + 20; // Add padding for rotation
            _logoHeight = bounds.Height + 20;
        }
        else
        {
            // Fallback dimensions
            _logoWidth = _size switch
            {
                SizePreset.Small => 100,
                SizePreset.Medium => 180,
                SizePreset.Large => 280,
                _ => 180
            };
            _logoHeight = _logoWidth * 0.4f;
        }
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        // Initialize or reinitialize if dimensions changed
        if (_width != width || _height != height || !_initialized)
        {
            _width = width;
            _height = height;
            LoadLogoFont(); // Reload font to get correct text bounds
            InitializePosition();
            _initialized = true;
        }

        // Calculate delta time
        var now = DateTime.UtcNow;
        var deltaTime = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        // Clamp delta time to prevent huge jumps
        deltaTime = Math.Min(deltaTime, 0.1f);

        // Update position
        UpdatePosition(deltaTime);

        // Update rotation
        if (_rotate)
        {
            UpdateRotation(deltaTime, now);
        }

        // Update rainbow color (scaled by speed)
        if (_colorMode == ColorMode.Rainbow)
        {
            _rainbowHue += deltaTime * 60f * GetSpeedMultiplier();
            if (_rainbowHue >= 360f) _rainbowHue -= 360f;
        }

        // Render
        var image = new Image<Rgba32>(width, height, new Rgba32(0, 0, 0));

        image.Mutate(ctx =>
        {
            var color = GetCurrentColor();
            RenderLogo(ctx, color);
        });

        return Task.FromResult(image);
    }

    private void InitializePosition()
    {
        // Account for potential rotation increasing bounds
        var effectiveWidth = _rotate ? _logoWidth * 1.5f : _logoWidth;
        var effectiveHeight = _rotate ? _logoHeight * 1.5f : _logoHeight;

        // Start at random position ensuring logo fits on screen
        _x = _random.Next(0, Math.Max(1, (int)(_width - effectiveWidth)));
        _y = _random.Next(0, Math.Max(1, (int)(_height - effectiveHeight)));

        // Apply speed multiplier to base speed
        var actualSpeed = BaseSpeed * GetSpeedMultiplier();

        // Random direction with slight angle variation
        var angle = _random.NextSingle() * MathF.PI * 2;
        _vx = MathF.Cos(angle) * actualSpeed;
        _vy = MathF.Sin(angle) * actualSpeed;

        // Ensure minimum velocity in both directions
        if (MathF.Abs(_vx) < actualSpeed * 0.3f) _vx = actualSpeed * 0.5f * MathF.Sign(_vx == 0 ? 1 : _vx);
        if (MathF.Abs(_vy) < actualSpeed * 0.3f) _vy = actualSpeed * 0.5f * MathF.Sign(_vy == 0 ? 1 : _vy);
    }

    private void UpdatePosition(float deltaTime)
    {
        _x += _vx * deltaTime;
        _y += _vy * deltaTime;

        var bounced = false;

        // Account for potential rotation increasing bounds
        var effectiveWidth = _rotate ? _logoWidth * 1.5f : _logoWidth;
        var effectiveHeight = _rotate ? _logoHeight * 1.5f : _logoHeight;

        // Bounce off edges
        if (_x <= 0)
        {
            _x = 0;
            _vx = MathF.Abs(_vx);
            bounced = true;
        }
        else if (_x >= _width - effectiveWidth)
        {
            _x = _width - effectiveWidth;
            _vx = -MathF.Abs(_vx);
            bounced = true;
        }

        if (_y <= 0)
        {
            _y = 0;
            _vy = MathF.Abs(_vy);
            bounced = true;
        }
        else if (_y >= _height - effectiveHeight)
        {
            _y = _height - effectiveHeight;
            _vy = -MathF.Abs(_vy);
            bounced = true;
        }

        // Change color on bounce (for cycle and random modes)
        if (bounced)
        {
            OnBounce();
        }
    }

    private void OnBounce()
    {
        switch (_colorMode)
        {
            case ColorMode.Cycle:
                _colorIndex = (_colorIndex + 1) % CycleColors.Length;
                break;

            case ColorMode.Random:
                _fixedColor = new Rgba32(
                    (byte)_random.Next(128, 256),
                    (byte)_random.Next(128, 256),
                    (byte)_random.Next(128, 256));
                break;
        }
    }

    private void UpdateRotation(float deltaTime, DateTime now)
    {
        // Update Z rotation (always applies)
        _rotationAngle += _rotationSpeedZ * deltaTime;
        if (_rotationAngle >= 360f) _rotationAngle -= 360f;
        if (_rotationAngle < 0f) _rotationAngle += 360f;

        // Update X and Y rotation for 3D effect
        if (_is3D)
        {
            _rotationX += _rotationSpeedX * deltaTime;
            _rotationY += _rotationSpeedY * deltaTime;

            // Keep angles in bounds
            if (_rotationX >= 360f) _rotationX -= 360f;
            if (_rotationX < 0f) _rotationX += 360f;
            if (_rotationY >= 360f) _rotationY -= 360f;
            if (_rotationY < 0f) _rotationY += 360f;

            // Randomize spin speeds at intervals
            if (now >= _nextSpinChange)
            {
                RandomizeSpinSpeeds();
                _nextSpinChange = now.AddSeconds(2 + _random.NextDouble() * 4);
            }
        }
    }

    private void RandomizeSpinSpeeds()
    {
        var speedMult = GetSpeedMultiplier();

        // Z rotation speed: -90 to +90 degrees per second (scaled by speed)
        _rotationSpeedZ = (_random.NextSingle() - 0.5f) * 180f * speedMult;

        if (_is3D)
        {
            // X and Y rotation speeds for 3D tumbling (scaled by speed)
            _rotationSpeedX = (_random.NextSingle() - 0.5f) * 120f * speedMult;
            _rotationSpeedY = (_random.NextSingle() - 0.5f) * 120f * speedMult;
        }
    }

    private Rgba32 GetCurrentColor()
    {
        return _colorMode switch
        {
            ColorMode.Fixed => _fixedColor,
            ColorMode.Random => _fixedColor,
            ColorMode.Cycle => CycleColors[_colorIndex],
            ColorMode.Rainbow => HslToRgb(_rainbowHue, 1f, 0.5f),
            _ => CycleColors[_colorIndex]
        };
    }

    private void RenderLogo(IImageProcessingContext ctx, Rgba32 color)
    {
        // Calculate center position for rotation
        var centerX = _x + _logoWidth / 2;
        var centerY = _y + _logoHeight / 2;

        if (_logoFont != null)
        {
            if (_is3D)
            {
                Render3DText(ctx, centerX, centerY, color);
            }
            else if (_rotate)
            {
                Render2DRotatedText(ctx, centerX, centerY, color);
            }
            else
            {
                // Simple non-rotated text with thickness stroke
                RenderTextWithStroke(ctx, _x, _y, color, applyTransform: false);
            }
        }
        else
        {
            // Fallback: draw a colored rectangle
            RenderFallbackRectangle(ctx, color);
        }
    }

    private void Render2DRotatedText(IImageProcessingContext ctx, float centerX, float centerY, Rgba32 color)
    {
        // Apply rotation around center
        var transform = Matrix3x2Extensions.CreateRotationDegrees(_rotationAngle, new PointF(centerX, centerY));
        ctx.SetDrawingTransform(transform);

        // Draw text with stroke effect based on thickness
        RenderTextWithStroke(ctx, centerX, centerY, color, applyTransform: true);

        // Reset transform
        ctx.SetDrawingTransform(Matrix3x2.Identity);
    }

    /// <summary>
    /// Renders text with a stroke/outline effect based on thickness.
    /// </summary>
    private void RenderTextWithStroke(IImageProcessingContext ctx, float x, float y, Rgba32 color, bool applyTransform)
    {
        var strokeWidth = _resolvedThickness * 0.5f; // Scale thickness to reasonable stroke
        var strokeColor = new Rgba32(0, 0, 0); // Black outline

        RichTextOptions textOptions;
        if (applyTransform)
        {
            // Centered text (for rotated rendering)
            textOptions = new RichTextOptions(_logoFont!)
            {
                Origin = new PointF(x, y),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else
        {
            // Top-left origin (for simple rendering)
            textOptions = new RichTextOptions(_logoFont!)
            {
                Origin = new PointF(x, y)
            };
        }

        // Draw stroke by rendering text at offsets
        if (_resolvedThickness > 1)
        {
            var offsets = GetStrokeOffsets(strokeWidth);
            foreach (var (ox, oy) in offsets)
            {
                var strokeOptions = new RichTextOptions(_logoFont!)
                {
                    Origin = new PointF(textOptions.Origin.X + ox, textOptions.Origin.Y + oy),
                    HorizontalAlignment = textOptions.HorizontalAlignment,
                    VerticalAlignment = textOptions.VerticalAlignment
                };
                ctx.DrawText(strokeOptions, _text, (Color)strokeColor);
            }
        }

        // Draw main text on top
        ctx.DrawText(textOptions, _text, (Color)color);
    }

    /// <summary>
    /// Gets offset positions for stroke rendering.
    /// </summary>
    private static List<(float x, float y)> GetStrokeOffsets(float strokeWidth)
    {
        var offsets = new List<(float, float)>();
        var steps = Math.Max(4, (int)(strokeWidth * 2));

        for (var i = 0; i < steps; i++)
        {
            var angle = i * MathF.PI * 2 / steps;
            offsets.Add((MathF.Cos(angle) * strokeWidth, MathF.Sin(angle) * strokeWidth));
        }

        return offsets;
    }

    private void Render3DText(IImageProcessingContext ctx, float centerX, float centerY, Rgba32 color)
    {
        // Calculate 3D perspective factors from X and Y rotation
        var radX = _rotationX * MathF.PI / 180f;
        var radY = _rotationY * MathF.PI / 180f;

        // Scale factors to simulate 3D perspective
        var scaleX = MathF.Cos(radY) * 0.3f + 0.7f; // Range 0.4 to 1.0
        var scaleY = MathF.Cos(radX) * 0.3f + 0.7f; // Range 0.4 to 1.0

        // Shear factors for 3D illusion
        var shearX = MathF.Sin(radY) * 0.3f;
        var shearY = MathF.Sin(radX) * 0.2f;

        // Build combined transform: translate to center, apply 3D-ish transform, rotate, translate back
        var toOrigin = Matrix3x2Extensions.CreateTranslation(new PointF(-centerX, -centerY));
        var perspective = new Matrix3x2(
            scaleX, shearY,
            shearX, scaleY,
            0, 0);
        var rotation = Matrix3x2Extensions.CreateRotationDegrees(_rotationAngle);
        var fromOrigin = Matrix3x2Extensions.CreateTranslation(new PointF(centerX, centerY));

        var combined = toOrigin * perspective * rotation * fromOrigin;

        // Shadow layers based on thickness (more thickness = more depth)
        var shadowLayers = Math.Max(2, _resolvedThickness);
        var shadowOffset = 0.5f + _resolvedThickness * 0.4f; // Scale offset with thickness

        // Draw shadow layers for depth effect
        for (var i = shadowLayers; i >= 1; i--)
        {
            var shadowIntensity = (byte)Math.Max(0, 50 - i * (40 / shadowLayers));
            var shadowColor = new Rgba32(shadowIntensity, shadowIntensity, shadowIntensity);

            var shadowTranslate = Matrix3x2Extensions.CreateTranslation(new PointF(i * shadowOffset, i * shadowOffset));
            ctx.SetDrawingTransform(combined * shadowTranslate);

            var shadowOptions = new RichTextOptions(_logoFont!)
            {
                Origin = new PointF(centerX, centerY),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ctx.DrawText(shadowOptions, _text, (Color)shadowColor);
        }

        // Draw main text with stroke outline based on thickness
        ctx.SetDrawingTransform(combined);
        var textOptions = new RichTextOptions(_logoFont!)
        {
            Origin = new PointF(centerX, centerY),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Add stroke outline for thicker text
        if (_resolvedThickness > 2)
        {
            var strokeWidth = _resolvedThickness * 0.3f;
            var strokeColor = new Rgba32(0, 0, 0);
            var strokeOffsets = GetStrokeOffsets(strokeWidth);
            foreach (var (ox, oy) in strokeOffsets)
            {
                var strokeOptions = new RichTextOptions(_logoFont!)
                {
                    Origin = new PointF(centerX + ox, centerY + oy),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                ctx.DrawText(strokeOptions, _text, (Color)strokeColor);
            }
        }

        ctx.DrawText(textOptions, _text, (Color)color);

        // Draw highlight for 3D effect
        var highlightColor = new Rgba32(
            (byte)Math.Min(255, color.R + 80),
            (byte)Math.Min(255, color.G + 80),
            (byte)Math.Min(255, color.B + 80));

        var highlightTranslate = Matrix3x2Extensions.CreateTranslation(new PointF(-1, -1));
        ctx.SetDrawingTransform(combined * highlightTranslate);
        ctx.DrawText(textOptions, _text, Color.FromRgba(highlightColor.R, highlightColor.G, highlightColor.B, 80));

        // Reset transform
        ctx.SetDrawingTransform(Matrix3x2.Identity);
    }

    private void RenderFallbackRectangle(IImageProcessingContext ctx, Rgba32 color)
    {
        var rect = new RectangleF(_x, _y, _logoWidth, _logoHeight);

        if (_rotate)
        {
            var centerX = _x + _logoWidth / 2;
            var centerY = _y + _logoHeight / 2;
            var transform = Matrix3x2Extensions.CreateRotationDegrees(_rotationAngle, new PointF(centerX, centerY));
            ctx.SetDrawingTransform(transform);
        }

        if (_is3D)
        {
            // Draw shadow
            var shadowRect = new RectangleF(_x + 5, _y + 5, _logoWidth, _logoHeight);
            ctx.Fill(new Rgba32(30, 30, 30), shadowRect);
        }

        ctx.Fill((Color)color, rect);

        // Add a border
        var borderColor = new Rgba32(
            (byte)Math.Min(255, color.R + 50),
            (byte)Math.Min(255, color.G + 50),
            (byte)Math.Min(255, color.B + 50));
        ctx.Draw((Color)borderColor, 3f, rect);

        if (_rotate)
        {
            ctx.SetDrawingTransform(Matrix3x2.Identity);
        }
    }

    #region Color Parsing Helpers

    private static bool TryParseColor(string value, out Rgba32 color)
    {
        color = new Rgba32(255, 255, 255);

        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();

        // Try HTML hex format
        if (value.StartsWith('#'))
        {
            return TryParseHexColor(value, out color);
        }

        // Try named colors
        return TryParseNamedColor(value, out color);
    }

    private static bool TryParseHexColor(string hex, out Rgba32 color)
    {
        color = new Rgba32(255, 255, 255);
        hex = hex.TrimStart('#');

        try
        {
            if (hex.Length == 3)
            {
                // Short form #RGB
                var r = Convert.ToByte(new string(hex[0], 2), 16);
                var g = Convert.ToByte(new string(hex[1], 2), 16);
                var b = Convert.ToByte(new string(hex[2], 2), 16);
                color = new Rgba32(r, g, b);
                return true;
            }
            else if (hex.Length == 6)
            {
                // Full form #RRGGBB
                var r = Convert.ToByte(hex[..2], 16);
                var g = Convert.ToByte(hex[2..4], 16);
                var b = Convert.ToByte(hex[4..6], 16);
                color = new Rgba32(r, g, b);
                return true;
            }
        }
        catch
        {
            // Parsing failed
        }

        return false;
    }

    private static bool TryParseNamedColor(string name, out Rgba32 color)
    {
        color = name.ToLowerInvariant() switch
        {
            "red" => new Rgba32(255, 0, 0),
            "green" => new Rgba32(0, 255, 0),
            "blue" => new Rgba32(0, 0, 255),
            "yellow" => new Rgba32(255, 255, 0),
            "cyan" or "aqua" => new Rgba32(0, 255, 255),
            "magenta" or "fuchsia" => new Rgba32(255, 0, 255),
            "white" => new Rgba32(255, 255, 255),
            "black" => new Rgba32(0, 0, 0),
            "orange" => new Rgba32(255, 165, 0),
            "pink" => new Rgba32(255, 192, 203),
            "purple" => new Rgba32(128, 0, 128),
            "violet" => new Rgba32(238, 130, 238),
            "lime" => new Rgba32(0, 255, 0),
            "navy" => new Rgba32(0, 0, 128),
            "teal" => new Rgba32(0, 128, 128),
            "maroon" => new Rgba32(128, 0, 0),
            "olive" => new Rgba32(128, 128, 0),
            "silver" => new Rgba32(192, 192, 192),
            "gray" or "grey" => new Rgba32(128, 128, 128),
            "gold" => new Rgba32(255, 215, 0),
            "coral" => new Rgba32(255, 127, 80),
            "salmon" => new Rgba32(250, 128, 114),
            "hotpink" => new Rgba32(255, 105, 180),
            "indigo" => new Rgba32(75, 0, 130),
            "crimson" => new Rgba32(220, 20, 60),
            "turquoise" => new Rgba32(64, 224, 208),
            "chartreuse" => new Rgba32(127, 255, 0),
            _ => new Rgba32(255, 255, 255)
        };

        return name.ToLowerInvariant() switch
        {
            "red" or "green" or "blue" or "yellow" or "cyan" or "aqua" or
            "magenta" or "fuchsia" or "white" or "black" or "orange" or
            "pink" or "purple" or "violet" or "lime" or "navy" or "teal" or
            "maroon" or "olive" or "silver" or "gray" or "grey" or "gold" or
            "coral" or "salmon" or "hotpink" or "indigo" or "crimson" or
            "turquoise" or "chartreuse" => true,
            _ => false
        };
    }

    private static Rgba32 HslToRgb(float h, float s, float l)
    {
        float r, g, b;

        if (Math.Abs(s) < 0.001f)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            var p = 2f * l - q;
            r = HueToRgb(p, q, h / 360f + 1f / 3f);
            g = HueToRgb(p, q, h / 360f);
            b = HueToRgb(p, q, h / 360f - 1f / 3f);
        }

        return new Rgba32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0f) t += 1f;
        if (t > 1f) t -= 1f;
        if (t < 1f / 6f) return p + (q - p) * 6f * t;
        if (t < 1f / 2f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
        return p;
    }

    #endregion
}
