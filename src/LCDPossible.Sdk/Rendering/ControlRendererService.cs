using LCDPossible.Core.Configuration;
using LCDPossible.Sdk.Controls;

namespace LCDPossible.Sdk.Rendering;

/// <summary>
/// Resolves and invokes control renderers with theme override support.
/// Theme-provided renderers take priority over default renderers.
/// </summary>
public class ControlRendererService
{
    private readonly Dictionary<string, IControlRenderer> _defaultRenderers;
    private readonly Dictionary<string, IControlRenderer> _themeRenderers;

    /// <summary>
    /// Creates a new renderer service with default and optional theme renderers.
    /// </summary>
    /// <param name="defaultRenderers">Built-in default renderers for each control type.</param>
    /// <param name="themeRenderers">Optional theme-provided custom renderers (templates).</param>
    public ControlRendererService(
        IEnumerable<IControlRenderer> defaultRenderers,
        Dictionary<string, string>? themeRenderers = null)
    {
        _defaultRenderers = defaultRenderers.ToDictionary(r => r.ControlType);
        _themeRenderers = LoadThemeRenderers(themeRenderers);
    }

    /// <summary>
    /// Creates a renderer service with only default renderers.
    /// </summary>
    public static ControlRendererService CreateDefault()
    {
        return new ControlRendererService(DefaultRenderers.GetAll());
    }

    /// <summary>
    /// Creates a renderer service with default renderers and theme overrides.
    /// </summary>
    /// <param name="themeRenderers">Dictionary of control type to Scriban template content.</param>
    public static ControlRendererService CreateWithTheme(Dictionary<string, string>? themeRenderers)
    {
        return new ControlRendererService(DefaultRenderers.GetAll(), themeRenderers);
    }

    /// <summary>
    /// Renders a semantic control to HTML using the appropriate renderer.
    /// Theme renderers take priority over default renderers.
    /// </summary>
    public string RenderControl(SemanticControl control, ControlRenderContext context)
    {
        // Theme renderer takes priority
        if (_themeRenderers.TryGetValue(control.ControlType, out var themeRenderer))
            return themeRenderer.Render(control, context);

        // Fall back to default renderer
        if (_defaultRenderers.TryGetValue(control.ControlType, out var defaultRenderer))
            return defaultRenderer.Render(control, context);

        throw new InvalidOperationException(
            $"No renderer found for control type: {control.ControlType}. " +
            $"Available: {string.Join(", ", _defaultRenderers.Keys)}");
    }

    /// <summary>
    /// Checks if a renderer exists for the given control type.
    /// </summary>
    public bool HasRenderer(string controlType)
    {
        return _themeRenderers.ContainsKey(controlType) || _defaultRenderers.ContainsKey(controlType);
    }

    private static Dictionary<string, IControlRenderer> LoadThemeRenderers(Dictionary<string, string>? templates)
    {
        if (templates is null || templates.Count == 0)
            return new Dictionary<string, IControlRenderer>();

        var renderers = new Dictionary<string, IControlRenderer>();

        foreach (var (controlType, templateContent) in templates)
        {
            try
            {
                renderers[controlType] = new ScribanControlRenderer(controlType, templateContent);
            }
            catch (Exception ex)
            {
                // Log warning but continue - fall back to default renderer
                Console.Error.WriteLine($"Warning: Failed to parse theme template for '{controlType}': {ex.Message}");
            }
        }

        return renderers;
    }
}
