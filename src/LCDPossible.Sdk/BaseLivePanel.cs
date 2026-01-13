using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;

namespace LCDPossible.Sdk;

/// <summary>
/// Base class for live display panels with common rendering utilities.
/// </summary>
/// <remarks>
/// <para>
/// This class is now an alias for <see cref="CanvasPanel"/>.
/// New panels should use <see cref="CanvasPanel"/> directly for ImageSharp drawing,
/// or <see cref="WidgetPanel"/> for HTML/CSS-based rendering.
/// </para>
/// <para>
/// Existing panels using BaseLivePanel will continue to work without modification.
/// </para>
/// </remarks>
[Obsolete("Use CanvasPanel for ImageSharp drawing or WidgetPanel for HTML-based rendering. BaseLivePanel is maintained for backward compatibility.")]
public abstract class BaseLivePanel : CanvasPanel
{
    // All functionality inherited from CanvasPanel.
    // This class exists only for backward compatibility.
}
