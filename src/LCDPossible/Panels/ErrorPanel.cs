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
    private readonly string[] _availablePanels;
    private Font? _titleFont;
    private Font? _messageFont;
    private Font? _hintFont;
    private Font? _smallFont;
    private bool _fontsLoaded;

    public ErrorPanel(string panelTypeId, string errorMessage, string[]? availablePanels = null)
    {
        _panelTypeId = panelTypeId ?? "unknown";
        _errorMessage = errorMessage ?? "Unknown error";
        _availablePanels = availablePanels ?? [];
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
                    _titleFont = family.CreateFont(28, FontStyle.Bold);
                    _messageFont = family.CreateFont(18, FontStyle.Regular);
                    _hintFont = family.CreateFont(14, FontStyle.Italic);
                    _smallFont = family.CreateFont(12, FontStyle.Regular);
                    _fontsLoaded = true;
                    return;
                }
            }

            // Fallback to any available font
            if (SystemFonts.Collection.Families.Any())
            {
                var family = SystemFonts.Collection.Families.First();
                _titleFont = family.CreateFont(28, FontStyle.Bold);
                _messageFont = family.CreateFont(18, FontStyle.Regular);
                _hintFont = family.CreateFont(14, FontStyle.Italic);
                _smallFont = family.CreateFont(12, FontStyle.Regular);
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

        if (!_fontsLoaded || _titleFont == null || _messageFont == null || _hintFont == null || _smallFont == null)
        {
            return Task.FromResult(image);
        }

        var errorColor = new Rgba32(255, 80, 80);
        var textColor = new Rgba32(220, 220, 220);
        var hintColor = new Rgba32(140, 140, 140);
        var panelColor = new Rgba32(255, 200, 100);
        var availableColor = new Rgba32(100, 180, 100);

        image.Mutate(ctx =>
        {
            var centerX = width / 2f;
            var y = 30f;

            // Draw error icon (X mark) - smaller
            var iconSize = 25f;
            ctx.DrawLine(errorColor, 4f,
                new PointF(centerX - iconSize, y - iconSize),
                new PointF(centerX + iconSize, y + iconSize));
            ctx.DrawLine(errorColor, 4f,
                new PointF(centerX + iconSize, y - iconSize),
                new PointF(centerX - iconSize, y + iconSize));

            y += 50;

            // Title
            var titleOptions = new RichTextOptions(_titleFont)
            {
                Origin = new PointF(centerX, y),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            ctx.DrawText(titleOptions, "Unknown Panel Type", errorColor);

            y += 40;

            // Panel type ID
            var panelOptions = new RichTextOptions(_messageFont)
            {
                Origin = new PointF(centerX, y),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            ctx.DrawText(panelOptions, $"\"{_panelTypeId}\"", panelColor);

            y += 35;

            // Error message (shortened)
            var displayError = _errorMessage.Length > 60
                ? _errorMessage[..57] + "..."
                : _errorMessage;
            var errorOptions = new RichTextOptions(_hintFont)
            {
                Origin = new PointF(centerX, y),
                HorizontalAlignment = HorizontalAlignment.Center,
                WrappingLength = width - 40
            };
            ctx.DrawText(errorOptions, displayError, textColor);

            y += 40;

            // Available panels section
            if (_availablePanels.Length == 0)
            {
                // No plugins found - this is the real issue
                var noPluginsOptions = new RichTextOptions(_messageFont)
                {
                    Origin = new PointF(centerX, y),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                ctx.DrawText(noPluginsOptions, "No plugins discovered!", errorColor);

                y += 30;

                var checkOptions = new RichTextOptions(_hintFont)
                {
                    Origin = new PointF(centerX, y),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                ctx.DrawText(checkOptions, "Check that 'plugins/' folder exists next to executable", hintColor);
            }
            else
            {
                // Show available panels
                var availableLabelOptions = new RichTextOptions(_hintFont)
                {
                    Origin = new PointF(centerX, y),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                ctx.DrawText(availableLabelOptions, $"Available panels ({_availablePanels.Length}):", hintColor);

                y += 22;

                // Show panels in columns (up to 12 panels, 2 columns)
                var maxToShow = Math.Min(_availablePanels.Length, 12);
                var leftX = width * 0.25f;
                var rightX = width * 0.75f;

                for (int i = 0; i < maxToShow; i++)
                {
                    var x = (i % 2 == 0) ? leftX : rightX;
                    if (i % 2 == 0 && i > 0)
                    {
                        y += 18;
                    }

                    var itemOptions = new RichTextOptions(_smallFont)
                    {
                        Origin = new PointF(x, y),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    ctx.DrawText(itemOptions, _availablePanels[i], availableColor);
                }

                if (_availablePanels.Length > maxToShow)
                {
                    y += 22;
                    var moreOptions = new RichTextOptions(_smallFont)
                    {
                        Origin = new PointF(centerX, y),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    ctx.DrawText(moreOptions, $"... and {_availablePanels.Length - maxToShow} more", hintColor);
                }
            }
        });

        return Task.FromResult(image);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}
