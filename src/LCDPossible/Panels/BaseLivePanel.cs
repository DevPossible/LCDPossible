using LCDPossible.Core.Configuration;
using LCDPossible.Core.Rendering;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Panels;

/// <summary>
/// Base class for live display panels with common rendering utilities.
/// </summary>
public abstract class BaseLivePanel : IDisplayPanel
{
    /// <summary>
    /// Color scheme for rendering. Can be updated at runtime.
    /// </summary>
    protected ResolvedColorScheme Colors { get; private set; } = ResolvedColorScheme.CreateDefault();

    // Legacy color properties for backward compatibility - delegate to color scheme
    protected Color BackgroundColor => Colors.Background;
    protected Color PrimaryTextColor => Colors.TextPrimary;
    protected Color SecondaryTextColor => Colors.TextSecondary;
    protected Color AccentColor => Colors.Accent;
    protected Color WarningColor => Colors.Warning;
    protected Color CriticalColor => Colors.Critical;
    protected Color SuccessColor => Colors.Success;

    protected Font? TitleFont { get; private set; }
    protected Font? ValueFont { get; private set; }
    protected Font? LabelFont { get; private set; }
    protected Font? SmallFont { get; private set; }
    protected bool FontsLoaded { get; private set; }

    public abstract string PanelId { get; }
    public abstract string DisplayName { get; }
    public bool IsLive => true;

    protected bool _disposed;

    /// <summary>
    /// Sets the color scheme for this panel.
    /// </summary>
    public void SetColorScheme(ResolvedColorScheme colors)
    {
        Colors = colors ?? ResolvedColorScheme.CreateDefault();
    }

    public virtual Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        LoadFonts();
        return Task.CompletedTask;
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
            // Font loading failed
        }
    }

    public abstract Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default);

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

    protected Color GetUsageColor(float percentage)
    {
        return Colors.GetUsageColor(percentage);
    }

    protected Color GetTemperatureColor(float celsius)
    {
        return Colors.GetTemperatureColor(celsius);
    }

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

    public virtual void Dispose()
    {
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
