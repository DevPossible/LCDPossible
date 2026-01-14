using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Sdk;

/// <summary>
/// Base class for panels that render using ImageSharp direct drawing.
/// Provides font loading, drawing helpers, and animation timing utilities.
/// Use this for screensavers, effects, and other custom-drawn content.
/// </summary>
/// <remarks>
/// For HTML/CSS-based panels, use <see cref="HtmlPanel"/> or <see cref="WidgetPanel"/> instead.
/// </remarks>
public abstract class CanvasPanel : BasePanel
{
    // Convenience color properties that delegate to color scheme
    protected Color BackgroundColor => Colors.Background;
    protected Color PrimaryTextColor => Colors.TextPrimary;
    protected Color SecondaryTextColor => Colors.TextSecondary;
    protected Color AccentColor => Colors.Accent;
    protected Color WarningColor => Colors.Warning;
    protected Color CriticalColor => Colors.Critical;
    protected Color SuccessColor => Colors.Success;

    // Font properties
    protected Font? TitleFont { get; private set; }
    protected Font? ValueFont { get; private set; }
    protected Font? LabelFont { get; private set; }
    protected Font? SmallFont { get; private set; }
    protected bool FontsLoaded { get; private set; }

    // Animation timing
    private DateTime _startTime;
    private DateTime _lastFrameTime;

    /// <summary>
    /// Total elapsed time in seconds since the panel was initialized.
    /// Useful for animation timing.
    /// </summary>
    protected float ElapsedSeconds { get; private set; }

    /// <summary>
    /// Time in seconds since the last frame was rendered (delta time).
    /// Useful for frame-rate independent animation.
    /// </summary>
    protected float DeltaSeconds { get; private set; }

    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        LoadFonts();
        _startTime = DateTime.UtcNow;
        _lastFrameTime = _startTime;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates the timing properties before rendering.
    /// Call this at the start of RenderFrameAsync for animated panels.
    /// </summary>
    protected void UpdateTiming()
    {
        var now = DateTime.UtcNow;
        ElapsedSeconds = (float)(now - _startTime).TotalSeconds;
        DeltaSeconds = (float)(now - _lastFrameTime).TotalSeconds;
        _lastFrameTime = now;
    }

    private void LoadFonts()
    {
        if (FontsLoaded)
        {
            return;
        }

        try
        {
            var fontCollection = SystemFonts.Collection;

            // Try common fonts in order of preference
            foreach (var fontName in new[] { "Segoe UI", "Arial", "Roboto", "DejaVu Sans", "Liberation Sans" })
            {
                if (fontCollection.TryGet(fontName, out var family))
                {
                    TitleFont = family.CreateFont(36, FontStyle.Bold);
                    ValueFont = family.CreateFont(72, FontStyle.Bold);
                    LabelFont = family.CreateFont(24, FontStyle.Regular);
                    SmallFont = family.CreateFont(18, FontStyle.Regular);
                    FontsLoaded = true;
                    return;
                }
            }

            // Fallback to first available font
            if (fontCollection.Families.Any())
            {
                var family = fontCollection.Families.First();
                TitleFont = family.CreateFont(36, FontStyle.Bold);
                ValueFont = family.CreateFont(72, FontStyle.Bold);
                LabelFont = family.CreateFont(24, FontStyle.Regular);
                SmallFont = family.CreateFont(18, FontStyle.Regular);
                FontsLoaded = true;
            }
        }
        catch
        {
            // Font loading failed - panel will render without text
        }
    }

    /// <summary>
    /// Creates a base image with the background color or optional background image.
    /// </summary>
    protected Image<Rgba32> CreateBaseImage(int width, int height, string? backgroundImagePath = null)
    {
        var image = new Image<Rgba32>(width, height);

        if (!string.IsNullOrEmpty(backgroundImagePath) && File.Exists(backgroundImagePath))
        {
            try
            {
                using var bgImage = Image.Load<Rgba32>(backgroundImagePath);
                bgImage.Mutate(ctx => ctx.Resize(width, height));
                image.Mutate(ctx => ctx.DrawImage(bgImage, 1f));
            }
            catch
            {
                // Fallback to solid color if image fails to load
                image.Mutate(ctx => ctx.Fill(BackgroundColor));
            }
        }
        else
        {
            image.Mutate(ctx => ctx.Fill(BackgroundColor));
        }

        return image;
    }

    /// <summary>
    /// Draws text at the specified position.
    /// </summary>
    protected void DrawText(IImageProcessingContext ctx, string text, float x, float y, Font font, Color color, float maxWidth = float.MaxValue)
    {
        if (string.IsNullOrEmpty(text) || font == null)
        {
            return;
        }

        var options = new RichTextOptions(font)
        {
            Origin = new PointF(x, y),
            WrappingLength = maxWidth,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        ctx.DrawText(options, text, color);
    }

    /// <summary>
    /// Draws centered text.
    /// </summary>
    protected void DrawCenteredText(IImageProcessingContext ctx, string text, float centerX, float y, Font font, Color color)
    {
        if (string.IsNullOrEmpty(text) || font == null)
        {
            return;
        }

        var options = new RichTextOptions(font)
        {
            Origin = new PointF(centerX, y),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        ctx.DrawText(options, text, color);
    }

    /// <summary>
    /// Draws right-aligned text.
    /// </summary>
    protected void DrawRightText(IImageProcessingContext ctx, string text, float rightX, float y, Font font, Color color)
    {
        if (string.IsNullOrEmpty(text) || font == null)
        {
            return;
        }

        var options = new RichTextOptions(font)
        {
            Origin = new PointF(rightX, y),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        ctx.DrawText(options, text, color);
    }

    /// <summary>
    /// Draws a horizontal progress bar.
    /// </summary>
    protected void DrawProgressBar(IImageProcessingContext ctx, float percentage, int x, int y, int width, int height, Color? fillColor = null)
    {
        var bgRect = new RectangleF(x, y, width, height);
        ctx.Fill(Colors.BarBackground, bgRect);

        var fillWidth = (int)(width * Math.Clamp(percentage, 0, 100) / 100f);
        if (fillWidth > 0)
        {
            var color = fillColor ?? GetUsageColor(percentage);
            var fillRect = new RectangleF(x, y, fillWidth, height);
            ctx.Fill(color, fillRect);
        }

        ctx.Draw(Colors.BarBorder, 2f, bgRect);
    }

    /// <summary>
    /// Draws a vertical bar (bottom-up fill).
    /// </summary>
    protected void DrawVerticalBar(IImageProcessingContext ctx, float percentage, int x, int y, int width, int height, Color? fillColor = null)
    {
        var bgRect = new RectangleF(x, y, width, height);
        ctx.Fill(Colors.BarBackground, bgRect);

        var fillHeight = (int)(height * Math.Clamp(percentage, 0, 100) / 100f);
        if (fillHeight > 0)
        {
            var color = fillColor ?? GetUsageColor(percentage);
            var fillRect = new RectangleF(x, y + height - fillHeight, width, fillHeight);
            ctx.Fill(color, fillRect);
        }

        ctx.Draw(Colors.BarBorder, 2f, bgRect);
    }

    /// <summary>
    /// Gets the appropriate color for a usage percentage.
    /// </summary>
    protected Color GetUsageColor(float percentage)
    {
        return Colors.GetUsageColor(percentage);
    }

    /// <summary>
    /// Gets the appropriate color for a temperature in Celsius.
    /// </summary>
    protected Color GetTemperatureColor(float celsius)
    {
        return Colors.GetTemperatureColor(celsius);
    }

    /// <summary>
    /// Draws a timestamp in the bottom-right corner.
    /// </summary>
    protected void DrawTimestamp(IImageProcessingContext ctx, int width, int height)
    {
        if (!FontsLoaded || SmallFont == null)
        {
            return;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        DrawText(ctx, timestamp, width - 100, height - 30, SmallFont, Colors.TextMuted, 90);
    }

    /// <summary>
    /// Truncates text to the specified maximum length, adding ellipsis if needed.
    /// </summary>
    protected static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text ?? string.Empty;
        }

        return text[..(maxLength - 3)] + "...";
    }
}
