using SixLabors.ImageSharp;

namespace LCDPossible.Sdk.Layout;

/// <summary>
/// Provides rendering context for a single widget within a panel layout.
/// Contains pre-scaled fonts, bounds, colors, and helper properties for positioning.
/// </summary>
public sealed class WidgetRenderContext
{
    /// <summary>
    /// The bounds of this widget (position and dimensions).
    /// </summary>
    public WidgetBounds Bounds { get; }

    /// <summary>
    /// Pre-scaled fonts appropriate for this widget's size.
    /// </summary>
    public WidgetFontSet Fonts { get; }

    /// <summary>
    /// Color scheme for rendering.
    /// </summary>
    public ResolvedColorScheme Colors { get; }

    /// <summary>
    /// Zero-based index of this widget in the layout.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// True if this is the overflow indicator widget (displays "+N more").
    /// </summary>
    public bool IsOverflowWidget { get; }

    /// <summary>
    /// Number of items not displayed (only meaningful when IsOverflowWidget is true).
    /// </summary>
    public int OverflowCount { get; }

    /// <summary>
    /// Padding from widget edges for content placement.
    /// Scales proportionally with widget size.
    /// </summary>
    public int Padding { get; }

    /// <summary>
    /// Creates a new widget render context.
    /// </summary>
    public WidgetRenderContext(
        WidgetBounds bounds,
        WidgetFontSet fonts,
        ResolvedColorScheme colors,
        int index,
        bool isOverflowWidget = false,
        int overflowCount = 0)
    {
        Bounds = bounds;
        Fonts = fonts;
        Colors = colors;
        Index = index;
        IsOverflowWidget = isOverflowWidget;
        OverflowCount = overflowCount;

        // Padding scales with widget height (approximately 3% of height, clamped)
        Padding = Math.Clamp((int)(bounds.Height * 0.03f), 8, 25);
    }

    #region Convenience Properties for Positioning

    /// <summary>
    /// Center point of the widget.
    /// </summary>
    public PointF Center => Bounds.Center;

    /// <summary>
    /// X coordinate where content should start (after padding).
    /// </summary>
    public int ContentX => Bounds.X + Padding;

    /// <summary>
    /// Y coordinate where content should start (after padding).
    /// </summary>
    public int ContentY => Bounds.Y + Padding;

    /// <summary>
    /// Width available for content (excluding padding on both sides).
    /// </summary>
    public int ContentWidth => Math.Max(0, Bounds.Width - Padding * 2);

    /// <summary>
    /// Height available for content (excluding padding on both sides).
    /// </summary>
    public int ContentHeight => Math.Max(0, Bounds.Height - Padding * 2);

    /// <summary>
    /// X coordinate of the right content edge.
    /// </summary>
    public int ContentRight => Bounds.X + Bounds.Width - Padding;

    /// <summary>
    /// Y coordinate of the bottom content edge.
    /// </summary>
    public int ContentBottom => Bounds.Y + Bounds.Height - Padding;

    /// <summary>
    /// Center X coordinate for centered content.
    /// </summary>
    public float ContentCenterX => Bounds.X + Bounds.Width / 2f;

    /// <summary>
    /// Center Y coordinate for centered content.
    /// </summary>
    public float ContentCenterY => Bounds.Y + Bounds.Height / 2f;

    #endregion

    #region Layout Helper Methods

    /// <summary>
    /// Calculates Y position for vertically distributing items within the widget.
    /// </summary>
    public int GetItemY(int itemIndex, int totalItems, int itemHeight)
    {
        if (totalItems <= 0) return ContentY;

        int totalItemsHeight = totalItems * itemHeight;
        int availableHeight = ContentHeight;

        // If items fit, distribute evenly; otherwise pack from top
        if (totalItemsHeight <= availableHeight)
        {
            int spacing = (availableHeight - totalItemsHeight) / (totalItems + 1);
            return ContentY + spacing + itemIndex * (itemHeight + spacing);
        }

        return ContentY + itemIndex * itemHeight;
    }

    /// <summary>
    /// Gets a scaled value based on widget height.
    /// Useful for proportional spacing, margins, etc.
    /// </summary>
    public int ScaleValue(int baseValue)
    {
        float scale = Bounds.Height / 480f;
        return Math.Max(1, (int)(baseValue * scale));
    }

    /// <summary>
    /// Gets a scaled float value based on widget height.
    /// </summary>
    public float ScaleValue(float baseValue)
    {
        float scale = Bounds.Height / 480f;
        return baseValue * scale;
    }

    #endregion
}
