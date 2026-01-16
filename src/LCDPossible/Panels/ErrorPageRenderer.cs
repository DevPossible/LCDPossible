using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Panels;

/// <summary>
/// Renders error page images for display when panels fail.
/// M014 fix: Extracted from duplicate code in LcdWorker and SlideshowManager.
/// </summary>
public static class ErrorPageRenderer
{
    // M002: Extract magic numbers to constants
    private const int TitleFontSize = 28;
    private const int MessageFontSize = 18;
    private const int HintFontSize = 14;
    private const int ErrorIconSize = 30;
    private const float ErrorIconStrokeWidth = 4f;
    private const int MaxErrorMessageLength = 80;
    private const int ErrorMessageTruncateLength = 77;
    private const int TextPadding = 40;

    /// <summary>
    /// Generates an error page image for display when a panel fails.
    /// </summary>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="panelName">Name of the failed panel.</param>
    /// <param name="errorMessage">Error message to display.</param>
    /// <param name="hintText">Optional hint text (default: "Panel error - check logs").</param>
    /// <returns>An error page image.</returns>
    public static Image<Rgba32> Generate(
        int width,
        int height,
        string panelName,
        string errorMessage,
        string hintText = "Panel error - check logs")
    {
        var image = new Image<Rgba32>(width, height);

        // Dark red gradient background
        image.Mutate(ctx =>
        {
            ctx.BackgroundColor(new Rgba32(40, 10, 10));
        });

        // Try to load system font for error text
        Font? titleFont = null;
        Font? messageFont = null;
        Font? hintFont = null;

        try
        {
            if (SystemFonts.TryGet("Segoe UI", out var family) ||
                SystemFonts.TryGet("Arial", out family) ||
                SystemFonts.TryGet("DejaVu Sans", out family))
            {
                titleFont = family.CreateFont(TitleFontSize, FontStyle.Bold);
                messageFont = family.CreateFont(MessageFontSize, FontStyle.Regular);
                hintFont = family.CreateFont(HintFontSize, FontStyle.Italic);
            }
        }
        catch
        {
            // Font loading failed, we'll render without text
        }

        if (titleFont != null && messageFont != null && hintFont != null)
        {
            var errorColor = new Rgba32(255, 100, 100);
            var textColor = new Rgba32(220, 220, 220);
            var hintColor = new Rgba32(150, 150, 150);

            image.Mutate(ctx =>
            {
                var y = height * 0.25f;
                var centerX = width / 2f;

                // Error icon (simple X)
                ctx.DrawLine(errorColor, ErrorIconStrokeWidth,
                    new PointF(centerX - ErrorIconSize, y - ErrorIconSize),
                    new PointF(centerX + ErrorIconSize, y + ErrorIconSize));
                ctx.DrawLine(errorColor, ErrorIconStrokeWidth,
                    new PointF(centerX + ErrorIconSize, y - ErrorIconSize),
                    new PointF(centerX - ErrorIconSize, y + ErrorIconSize));

                y += 60;

                // Title
                var titleOptions = new RichTextOptions(titleFont)
                {
                    Origin = new PointF(centerX, y),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                ctx.DrawText(titleOptions, "Panel Error", errorColor);

                y += 50;

                // Panel name
                var panelText = $"Panel: {panelName}";
                var panelOptions = new RichTextOptions(messageFont)
                {
                    Origin = new PointF(centerX, y),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                ctx.DrawText(panelOptions, panelText, textColor);

                y += 35;

                // Error message (truncate if too long)
                var displayError = errorMessage.Length > MaxErrorMessageLength
                    ? errorMessage[..ErrorMessageTruncateLength] + "..."
                    : errorMessage;
                var errorOptions = new RichTextOptions(messageFont)
                {
                    Origin = new PointF(centerX, y),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    WrappingLength = width - TextPadding
                };
                ctx.DrawText(errorOptions, displayError, textColor);

                y += 60;

                // Hint
                var hintOptions = new RichTextOptions(hintFont)
                {
                    Origin = new PointF(centerX, y),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                ctx.DrawText(hintOptions, hintText, hintColor);
            });
        }

        return image;
    }
}
