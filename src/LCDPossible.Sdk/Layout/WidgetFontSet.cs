using SixLabors.Fonts;

namespace LCDPossible.Sdk.Layout;

/// <summary>
/// A set of fonts scaled appropriately for a widget size.
/// Font sizes are calculated proportionally based on widget height.
/// </summary>
public sealed class WidgetFontSet
{
    /// <summary>
    /// Large font for primary values (percentages, main numbers).
    /// </summary>
    public Font Value { get; }

    /// <summary>
    /// Bold font for section headers and titles.
    /// </summary>
    public Font Title { get; }

    /// <summary>
    /// Regular font for field labels.
    /// </summary>
    public Font Label { get; }

    /// <summary>
    /// Small font for details and secondary information.
    /// </summary>
    public Font Small { get; }

    /// <summary>
    /// The calculated scale factor (for reference/debugging).
    /// </summary>
    public float Scale { get; }

    // Base font sizes at reference height (480px)
    private const float BaseValueSize = 72f;
    private const float BaseTitleSize = 36f;
    private const float BaseLabelSize = 24f;
    private const float BaseSmallSize = 18f;
    private const float ReferenceHeight = 480f;

    // Minimum font sizes for readability
    private const float MinValueSize = 14f;
    private const float MinTitleSize = 10f;
    private const float MinLabelSize = 8f;
    private const float MinSmallSize = 6f;

    private WidgetFontSet(Font value, Font title, Font label, Font small, float scale)
    {
        Value = value;
        Title = title;
        Label = label;
        Small = small;
        Scale = scale;
    }

    /// <summary>
    /// Creates a font set scaled for the given widget dimensions.
    /// Fonts scale proportionally based on widget height relative to a 480px reference.
    /// </summary>
    /// <param name="family">The font family to use.</param>
    /// <param name="bounds">The widget bounds to scale fonts for.</param>
    /// <returns>A new WidgetFontSet with appropriately scaled fonts.</returns>
    public static WidgetFontSet Create(FontFamily family, WidgetBounds bounds)
    {
        // Calculate scale based on widget height relative to reference
        float heightScale = bounds.Height / ReferenceHeight;

        // Apply size multiplier based on widget size category
        float sizeMultiplier = GetSizeMultiplier(bounds.Size);

        // Final scale with reasonable bounds to prevent extremes
        float scale = Math.Clamp(heightScale * sizeMultiplier, 0.3f, 2.0f);

        // Calculate font sizes with minimum thresholds for readability
        float valueSize = Math.Max(BaseValueSize * scale, MinValueSize);
        float titleSize = Math.Max(BaseTitleSize * scale, MinTitleSize);
        float labelSize = Math.Max(BaseLabelSize * scale, MinLabelSize);
        float smallSize = Math.Max(BaseSmallSize * scale, MinSmallSize);

        return new WidgetFontSet(
            family.CreateFont(valueSize, FontStyle.Bold),
            family.CreateFont(titleSize, FontStyle.Bold),
            family.CreateFont(labelSize, FontStyle.Regular),
            family.CreateFont(smallSize, FontStyle.Regular),
            scale);
    }

    /// <summary>
    /// Gets the size multiplier for a given widget size category.
    /// </summary>
    public static float GetSizeMultiplier(WidgetSize size) => size switch
    {
        WidgetSize.Full => 1.0f,
        WidgetSize.Half => 0.85f,
        WidgetSize.Quarter => 0.70f,
        _ => 1.0f
    };
}
