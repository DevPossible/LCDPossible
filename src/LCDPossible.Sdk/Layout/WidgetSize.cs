namespace LCDPossible.Sdk.Layout;

/// <summary>
/// Defines the size category of a widget within the layout.
/// Used for font scaling and layout calculations.
/// </summary>
public enum WidgetSize
{
    /// <summary>
    /// Full panel size (100% width x 100% height).
    /// Used when displaying a single item with maximum detail.
    /// </summary>
    Full,

    /// <summary>
    /// Half panel size (50% width x 100% height).
    /// Used for side-by-side layouts or the large widget in 3-item layouts.
    /// </summary>
    Half,

    /// <summary>
    /// Quarter panel size (50% width x 50% height).
    /// Used for 2x2 grid layouts or stacked widgets in 3-item layouts.
    /// </summary>
    Quarter
}
