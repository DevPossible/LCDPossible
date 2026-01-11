using LCDPossible.Core.Rendering;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Panels;

/// <summary>
/// A panel that displays an error message. Used when a requested panel type
/// is not found or fails to initialize.
/// </summary>
public sealed class ErrorPanel : IDisplayPanel
{
    private readonly string _panelTypeId;
    private readonly string _errorMessage;
    private Font? _titleFont;
    private Font? _messageFont;
    private Font? _hintFont;
    private bool _fontsLoaded;

    public ErrorPanel(string panelTypeId, string errorMessage)
    {
        _panelTypeId = panelTypeId ?? "unknown";
        _errorMessage = errorMessage ?? "Unknown error";
    }

    public string PanelId => $"error:{_panelTypeId}";
    public string DisplayName => $"Error: {_panelTypeId}";
    public bool IsLive => false;
    public bool IsAnimated => false;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        LoadFonts();
        return Task.CompletedTask;
    }

    private void LoadFonts()
    {
        if (_fontsLoaded) return;

        try
        {
            foreach (var fontName in new[] { "Segoe UI", "Arial", "DejaVu Sans", "Liberation Sans" })
            {
                if (SystemFonts.TryGet(fontName, out var family))
                {
                    _titleFont = family.CreateFont(32, FontStyle.Bold);
                    _messageFont = family.CreateFont(20, FontStyle.Regular);
                    _hintFont = family.CreateFont(16, FontStyle.Italic);
                    _fontsLoaded = true;
                    return;
                }
            }

            // Fallback to any available font
            if (SystemFonts.Collection.Families.Any())
            {
                var family = SystemFonts.Collection.Families.First();
                _titleFont = family.CreateFont(32, FontStyle.Bold);
                _messageFont = family.CreateFont(20, FontStyle.Regular);
                _hintFont = family.CreateFont(16, FontStyle.Italic);
                _fontsLoaded = true;
            }
        }
        catch
        {
            // Font loading failed - will render without text
        }
    }

    public Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = new Image<Rgba32>(width, height);

        // Dark red/maroon background
        image.Mutate(ctx => ctx.BackgroundColor(new Rgba32(50, 15, 15)));

        if (!_fontsLoaded || _titleFont == null || _messageFont == null || _hintFont == null)
        {
            return Task.FromResult(image);
        }

        var errorColor = new Rgba32(255, 80, 80);
        var textColor = new Rgba32(220, 220, 220);
        var hintColor = new Rgba32(140, 140, 140);
        var panelColor = new Rgba32(255, 200, 100);

        image.Mutate(ctx =>
        {
            var centerX = width / 2f;
            var y = height * 0.2f;

            // Draw error icon (X mark)
            var iconSize = 35f;
            ctx.DrawLine(errorColor, 5f,
                new PointF(centerX - iconSize, y - iconSize),
                new PointF(centerX + iconSize, y + iconSize));
            ctx.DrawLine(errorColor, 5f,
                new PointF(centerX + iconSize, y - iconSize),
                new PointF(centerX - iconSize, y + iconSize));

            y += 70;

            // Title
            var titleOptions = new RichTextOptions(_titleFont)
            {
                Origin = new PointF(centerX, y),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            ctx.DrawText(titleOptions, "Unknown Panel Type", errorColor);

            y += 55;

            // Panel type ID
            var panelOptions = new RichTextOptions(_messageFont)
            {
                Origin = new PointF(centerX, y),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            ctx.DrawText(panelOptions, $"\"{_panelTypeId}\"", panelColor);

            y += 45;

            // Error message
            var displayError = _errorMessage.Length > 100
                ? _errorMessage[..97] + "..."
                : _errorMessage;
            var errorOptions = new RichTextOptions(_messageFont)
            {
                Origin = new PointF(centerX, y),
                HorizontalAlignment = HorizontalAlignment.Center,
                WrappingLength = width - 60
            };
            ctx.DrawText(errorOptions, displayError, textColor);

            y += 70;

            // Hint
            var hintOptions = new RichTextOptions(_hintFont)
            {
                Origin = new PointF(centerX, y),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            ctx.DrawText(hintOptions, "Check your profile configuration", hintColor);

            y += 30;

            // Available panels hint
            var availableOptions = new RichTextOptions(_hintFont)
            {
                Origin = new PointF(centerX, y),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            ctx.DrawText(availableOptions, "Run 'lcdpossible panels' to see available panel types", hintColor);
        });

        return Task.FromResult(image);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}
