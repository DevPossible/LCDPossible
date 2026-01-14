namespace LCDPossible.Core.Configuration;

/// <summary>
/// Represents a display theme with colors, fonts, and effects.
/// Themes can be user-defined in YAML or use built-in presets.
/// </summary>
public sealed class Theme
{
    /// <summary>
    /// Theme identifier (e.g., "cyberpunk", "minimal", "classic").
    /// </summary>
    public string Id { get; set; } = "cyberpunk";

    /// <summary>
    /// Human-readable theme name for display.
    /// </summary>
    public string Name { get; set; } = "Cyberpunk";

    // Background colors
    public string Background { get; set; } = "#050508";
    public string BackgroundSecondary { get; set; } = "#0a0a12";
    public string Surface { get; set; } = "#12121a";

    // Text colors
    public string TextPrimary { get; set; } = "#eaeaea";
    public string TextSecondary { get; set; } = "#888899";
    public string TextMuted { get; set; } = "#555566";

    // Accent colors
    public string Accent { get; set; } = "#00ffff";
    public string AccentSecondary { get; set; } = "#ff00aa";
    public string AccentTertiary { get; set; } = "#00ff88";

    // Status colors
    public string Success { get; set; } = "#00ff88";
    public string Warning { get; set; } = "#ffaa00";
    public string Critical { get; set; } = "#ff4444";
    public string Info { get; set; } = "#00aaff";

    // Fonts
    public string FontDisplay { get; set; } = "'Orbitron', 'Segoe UI', sans-serif";
    public string FontBody { get; set; } = "'Inter', 'Segoe UI', sans-serif";
    public string FontData { get; set; } = "'JetBrains Mono', 'Consolas', monospace";

    // Effects
    public bool EnableGlow { get; set; } = true;
    public bool EnableScanlines { get; set; } = true;
    public bool EnableGradients { get; set; } = true;
    public float GlassBlur { get; set; } = 20f;
    public float GlassOpacity { get; set; } = 0.85f;

    // Border styling
    public string BorderColor { get; set; } = "#00ffff33";
    public float BorderRadius { get; set; } = 4f;
    public float BorderWidth { get; set; } = 1f;

    // Custom control renderers (Scriban templates keyed by control type)
    /// <summary>
    /// Optional custom control renderer templates.
    /// Keys are control type identifiers (e.g., "single_value", "gauge").
    /// Values are Scriban template strings.
    /// </summary>
    public Dictionary<string, string>? ControlRenderers { get; set; }

    /// <summary>
    /// Optional JavaScript code to be injected into HTML panels.
    /// Should define an LCDTheme object with lifecycle hooks:
    /// - onDomReady(): Called after DOM is ready, before first frame capture
    /// - onTransitionEnd(): Called after transition animation completes
    /// - onBeforeRender(): Called before each frame render (for animations)
    /// </summary>
    public string? ScriptContent { get; set; }

    /// <summary>
    /// Widget style preferences for this theme.
    /// Maps abstract widget types to preferred implementations/styles.
    /// Keys: "gauge", "donut", "sparkline", "progress"
    /// Values: Implementation-specific style configs (e.g., "echarts:arc", "daisy:lg", "echarts:ring")
    /// </summary>
    public Dictionary<string, string> WidgetStyles { get; set; } = new()
    {
        ["gauge"] = "echarts:arc",
        ["donut"] = "echarts:default",
        ["sparkline"] = "echarts:area",
        ["progress"] = "daisy:default"
    };

    /// <summary>
    /// Gets the preferred widget implementation for a widget type.
    /// Returns (implementation, style) tuple.
    /// </summary>
    public (string Implementation, string Style) GetWidgetStyle(string widgetType)
    {
        if (WidgetStyles.TryGetValue(widgetType, out var value))
        {
            var parts = value.Split(':');
            return (parts[0], parts.Length > 1 ? parts[1] : "default");
        }
        return ("echarts", "default");
    }

    /// <summary>
    /// Converts this theme to a ColorScheme for use with canvas panels.
    /// </summary>
    public ColorScheme ToColorScheme() => new()
    {
        Background = Background,
        BackgroundSecondary = BackgroundSecondary,
        BarBackground = Surface,
        BarBorder = BorderColor,
        TextPrimary = TextPrimary,
        TextSecondary = TextSecondary,
        TextMuted = TextMuted,
        Accent = Accent,
        AccentSecondary = AccentSecondary,
        Success = Success,
        Warning = Warning,
        Critical = Critical,
        Info = Info,
        UsageLow = Success,
        UsageMedium = Info,
        UsageHigh = Warning,
        UsageCritical = Critical,
        TempCool = Success,
        TempWarm = Warning,
        TempHot = Critical,
        ChartColors = [Accent, AccentSecondary, AccentTertiary, Success, Warning, Info]
    };

    /// <summary>
    /// Generates CSS custom properties from this theme.
    /// </summary>
    public string ToCssVariables()
    {
        var css = new System.Text.StringBuilder();
        css.AppendLine(":root {");

        // Background
        css.AppendLine($"    --color-background: {Background};");
        css.AppendLine($"    --color-background-secondary: {BackgroundSecondary};");
        css.AppendLine($"    --color-surface: {Surface};");

        // Text
        css.AppendLine($"    --color-text-primary: {TextPrimary};");
        css.AppendLine($"    --color-text-secondary: {TextSecondary};");
        css.AppendLine($"    --color-text-muted: {TextMuted};");

        // Accent
        css.AppendLine($"    --color-accent: {Accent};");
        css.AppendLine($"    --color-accent-secondary: {AccentSecondary};");
        css.AppendLine($"    --color-accent-tertiary: {AccentTertiary};");

        // Status
        css.AppendLine($"    --color-success: {Success};");
        css.AppendLine($"    --color-warning: {Warning};");
        css.AppendLine($"    --color-critical: {Critical};");
        css.AppendLine($"    --color-info: {Info};");

        // Fonts
        css.AppendLine($"    --font-display: {FontDisplay};");
        css.AppendLine($"    --font-body: {FontBody};");
        css.AppendLine($"    --font-data: {FontData};");

        // Effects
        css.AppendLine($"    --glass-blur: blur({GlassBlur}px);");
        css.AppendLine($"    --glass-bg: rgba(8, 12, 20, {GlassOpacity:F2});");
        css.AppendLine($"    --glow-strength: {(EnableGlow ? "1" : "0")};");
        css.AppendLine($"    --scanlines-opacity: {(EnableScanlines ? "0.03" : "0")};");

        // Borders
        css.AppendLine($"    --border-color: {BorderColor};");
        css.AppendLine($"    --border-radius: {BorderRadius}px;");
        css.AppendLine($"    --border-width: {BorderWidth}px;");

        css.AppendLine("}");
        return css.ToString();
    }
}
