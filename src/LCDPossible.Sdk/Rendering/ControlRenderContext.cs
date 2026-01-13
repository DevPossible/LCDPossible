using LCDPossible.Core.Configuration;

namespace LCDPossible.Sdk.Rendering;

/// <summary>
/// Context passed to control renderers with colors, paths, and grid configuration.
/// </summary>
/// <param name="Colors">The active color scheme for theming.</param>
/// <param name="AssetsPath">Path to HTML assets folder for linking CSS/JS.</param>
/// <param name="GridColumns">Total columns in the panel grid (default 12).</param>
/// <param name="GridRows">Total rows in the panel grid (default 4).</param>
public record ControlRenderContext(
    ColorScheme Colors,
    string AssetsPath,
    int GridColumns = 12,
    int GridRows = 4
)
{
    /// <summary>
    /// Creates a context with default values.
    /// </summary>
    public static ControlRenderContext Default => new(ColorScheme.CreateDefault(), "");
}
