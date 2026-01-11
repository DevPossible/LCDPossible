using LCDPossible.Sdk.Layout;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Sdk;

/// <summary>
/// Base class for panels that display a variable number of items using a smart widget layout system.
/// Automatically calculates optimal layout (1-4 widgets) based on item count and scales fonts proportionally.
/// </summary>
/// <typeparam name="TItem">The type of item to display in each widget.</typeparam>
public abstract class SmartLayoutPanel<TItem> : BaseLivePanel
{
    private FontFamily? _fontFamily;

    /// <summary>
    /// Gets items to display. Override this to provide the data source.
    /// Return an empty list for empty state, which will be handled by RenderEmptyState.
    /// </summary>
    protected abstract Task<IReadOnlyList<TItem>> GetItemsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Renders a single item into its widget area.
    /// Use the widget context for positioning and scaled fonts.
    /// </summary>
    protected abstract void RenderWidget(
        IImageProcessingContext ctx,
        WidgetRenderContext widget,
        TItem item);

    /// <summary>
    /// Renders the empty state when no items are available.
    /// Override to customize the empty state appearance.
    /// </summary>
    protected virtual void RenderEmptyState(IImageProcessingContext ctx, int width, int height)
    {
        if (!FontsLoaded || TitleFont == null)
        {
            return;
        }

        var message = GetEmptyStateMessage();
        DrawCenteredText(ctx, message, width / 2f, height / 2f - 20, TitleFont, Colors.TextMuted);
    }

    /// <summary>
    /// Gets the message to display when no items are available.
    /// Override to customize per panel type.
    /// </summary>
    protected virtual string GetEmptyStateMessage() => "No items available";

    /// <summary>
    /// Renders the overflow indicator widget when there are more items than can be displayed.
    /// Override to customize the overflow appearance.
    /// </summary>
    protected virtual void RenderOverflowWidget(IImageProcessingContext ctx, WidgetRenderContext widget)
    {
        // Draw a subtle background to distinguish overflow widget
        var bgRect = new RectangleF(
            widget.Bounds.X + 2,
            widget.Bounds.Y + 2,
            widget.Bounds.Width - 4,
            widget.Bounds.Height - 4);
        ctx.Fill(Colors.BackgroundSecondary, bgRect);

        // Draw centered "+N more" message
        var message = $"+{widget.OverflowCount} more";
        var options = new RichTextOptions(widget.Fonts.Title)
        {
            Origin = new PointF(widget.ContentCenterX, widget.ContentCenterY - 10),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ctx.DrawText(options, message, Colors.TextSecondary);
    }

    /// <summary>
    /// Optional: Renders a background or border for each widget.
    /// Override to add visual separation between widgets.
    /// </summary>
    protected virtual void RenderWidgetBackground(IImageProcessingContext ctx, WidgetRenderContext widget)
    {
        // Default: no background - widgets share panel background
    }

    /// <summary>
    /// Template method that handles layout calculation and delegates to abstract/virtual methods.
    /// This method is sealed to ensure consistent layout behavior.
    /// </summary>
    public sealed override async Task<Image<Rgba32>> RenderFrameAsync(
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        var items = await GetItemsAsync(cancellationToken);
        var image = CreateBaseImage(width, height);

        image.Mutate(ctx =>
        {
            // Handle empty state
            if (items.Count == 0)
            {
                RenderEmptyState(ctx, width, height);
                DrawTimestamp(ctx, width, height);
                return;
            }

            // Calculate layout for items
            var layout = WidgetLayout.Calculate(width, height, items.Count);

            // Render item widgets
            for (int i = 0; i < layout.DisplayedCount; i++)
            {
                var widgetBounds = layout.GetWidget(i);
                var widgetContext = CreateWidgetContext(widgetBounds, i, layout);

                RenderWidgetBackground(ctx, widgetContext);
                RenderWidget(ctx, widgetContext, items[i]);
            }

            // Render overflow widget if needed
            if (layout.HasOverflow)
            {
                var overflowBounds = layout.GetOverflowWidget();
                var overflowContext = CreateOverflowContext(overflowBounds, layout);

                RenderWidgetBackground(ctx, overflowContext);
                RenderOverflowWidget(ctx, overflowContext);
            }

            DrawTimestamp(ctx, width, height);
        });

        return image;
    }

    /// <summary>
    /// Creates a widget render context for a regular item widget.
    /// </summary>
    private WidgetRenderContext CreateWidgetContext(WidgetBounds bounds, int index, WidgetLayout layout)
    {
        var fonts = CreateFontSet(bounds);
        return new WidgetRenderContext(bounds, fonts, Colors, index);
    }

    /// <summary>
    /// Creates a widget render context for the overflow indicator widget.
    /// </summary>
    private WidgetRenderContext CreateOverflowContext(WidgetBounds bounds, WidgetLayout layout)
    {
        var fonts = CreateFontSet(bounds);
        return new WidgetRenderContext(
            bounds,
            fonts,
            Colors,
            layout.Widgets.Count - 1,
            isOverflowWidget: true,
            overflowCount: layout.OverflowCount);
    }

    /// <summary>
    /// Creates a scaled font set for the given widget bounds.
    /// </summary>
    private WidgetFontSet CreateFontSet(WidgetBounds bounds)
    {
        var family = GetFontFamily();
        return WidgetFontSet.Create(family, bounds);
    }

    /// <summary>
    /// Gets the font family for creating font sets.
    /// </summary>
    private FontFamily GetFontFamily()
    {
        if (_fontFamily != null)
        {
            return _fontFamily.Value;
        }

        var fontCollection = SystemFonts.Collection;

        foreach (var fontName in new[] { "Segoe UI", "Arial", "Roboto", "DejaVu Sans", "Liberation Sans" })
        {
            if (fontCollection.TryGet(fontName, out var family))
            {
                _fontFamily = family;
                return family;
            }
        }

        // Fallback to first available font
        if (fontCollection.Families.Any())
        {
            var family = fontCollection.Families.First();
            _fontFamily = family;
            return family;
        }

        throw new InvalidOperationException("No fonts available on the system");
    }

    #region Helper Methods for Subclasses

    /// <summary>
    /// Draws left-aligned text within a widget.
    /// </summary>
    protected new void DrawText(
        IImageProcessingContext ctx,
        string text,
        float x,
        float y,
        Font font,
        Color color,
        float maxWidth = float.MaxValue)
    {
        if (string.IsNullOrEmpty(text))
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
    /// Draws centered text within a widget.
    /// </summary>
    protected new void DrawCenteredText(
        IImageProcessingContext ctx,
        string text,
        float centerX,
        float y,
        Font font,
        Color color)
    {
        if (string.IsNullOrEmpty(text))
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
    /// Draws a horizontal progress bar within a widget.
    /// </summary>
    protected new void DrawProgressBar(
        IImageProcessingContext ctx,
        float percentage,
        int x,
        int y,
        int width,
        int height,
        Color? fillColor = null)
    {
        var bgRect = new RectangleF(x, y, width, height);
        ctx.Fill(Colors.BarBackground, bgRect);

        var fillWidth = (int)(width * Math.Clamp(percentage, 0, 100) / 100f);
        if (fillWidth > 0)
        {
            var color = fillColor ?? Colors.GetUsageColor(percentage);
            var fillRect = new RectangleF(x, y, fillWidth, height);
            ctx.Fill(color, fillRect);
        }

        ctx.Draw(Colors.BarBorder, 2f, bgRect);
    }

    /// <summary>
    /// Draws a vertical progress bar within a widget.
    /// </summary>
    protected new void DrawVerticalBar(
        IImageProcessingContext ctx,
        float percentage,
        int x,
        int y,
        int width,
        int height,
        Color? fillColor = null)
    {
        var bgRect = new RectangleF(x, y, width, height);
        ctx.Fill(Colors.BarBackground, bgRect);

        var fillHeight = (int)(height * Math.Clamp(percentage, 0, 100) / 100f);
        if (fillHeight > 0)
        {
            var color = fillColor ?? Colors.GetUsageColor(percentage);
            var fillRect = new RectangleF(x, y + height - fillHeight, width, fillHeight);
            ctx.Fill(color, fillRect);
        }

        ctx.Draw(Colors.BarBorder, 2f, bgRect);
    }

    #endregion
}
