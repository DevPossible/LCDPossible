using LCDPossible.Sdk.Controls;

namespace LCDPossible.Sdk.Rendering;

/// <summary>
/// Renders a semantic control to HTML.
/// Implementations can be built-in default renderers or theme-provided custom renderers.
/// </summary>
public interface IControlRenderer
{
    /// <summary>
    /// The control type identifier this renderer handles (e.g., "single_value", "gauge").
    /// Must match the <see cref="SemanticControl.ControlType"/> of the controls it renders.
    /// </summary>
    string ControlType { get; }

    /// <summary>
    /// Renders the control to an HTML string.
    /// </summary>
    /// <param name="control">The semantic control to render.</param>
    /// <param name="context">Rendering context with colors and paths.</param>
    /// <returns>HTML string for the control's inner content.</returns>
    string Render(SemanticControl control, ControlRenderContext context);
}
