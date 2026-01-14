namespace LCDPossible.Core.Configuration;

/// <summary>
/// Manages display themes including built-in presets and user-defined themes.
/// </summary>
public static class ThemeManager
{
    private static readonly Dictionary<string, Theme> _presets = new(StringComparer.OrdinalIgnoreCase);

    static ThemeManager()
    {
        // Register built-in presets (2 gamer + 2 corporate)
        RegisterPreset(CreateCyberpunkTheme());      // Gamer: Neon cyan/magenta
        RegisterPreset(CreateRgbGamingTheme());      // Gamer: RGB rainbow
        RegisterPreset(CreateExecutiveTheme());       // Corporate: Dark blue/gold
        RegisterPreset(CreateCleanTheme());           // Corporate: Minimal light
    }

    /// <summary>
    /// Gets all available preset theme IDs.
    /// </summary>
    public static IReadOnlyCollection<string> PresetIds => _presets.Keys.ToList().AsReadOnly();

    /// <summary>
    /// Gets all available themes with metadata.
    /// </summary>
    public static IReadOnlyList<(string Id, string Name, string Category)> GetThemeList()
    {
        return
        [
            ("cyberpunk", "Cyberpunk", "Gamer"),
            ("rgb-gaming", "RGB Gaming", "Gamer"),
            ("executive", "Executive", "Corporate"),
            ("clean", "Clean", "Corporate")
        ];
    }

    /// <summary>
    /// Gets a theme by ID. Returns the default theme if not found.
    /// </summary>
    public static Theme GetTheme(string? themeId)
    {
        if (string.IsNullOrEmpty(themeId) || !_presets.TryGetValue(themeId, out var theme))
        {
            return _presets["cyberpunk"];
        }
        return theme;
    }

    /// <summary>
    /// Checks if a theme ID exists.
    /// </summary>
    public static bool HasTheme(string themeId)
        => _presets.ContainsKey(themeId);

    /// <summary>
    /// Registers a custom theme preset.
    /// </summary>
    public static void RegisterPreset(Theme theme)
    {
        _presets[theme.Id] = theme;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GAMER THEMES
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cyberpunk HUD - Neon cyan/magenta on deep space black.
    /// </summary>
    public static Theme CreateCyberpunkTheme() => new()
    {
        Id = "cyberpunk",
        Name = "Cyberpunk",
        Background = "#050508",
        BackgroundSecondary = "#0a0a12",
        Surface = "#12121a",
        TextPrimary = "#ffffff",
        TextSecondary = "#88aacc",
        TextMuted = "#446688",
        Accent = "#00ffff",
        AccentSecondary = "#ff00aa",
        AccentTertiary = "#00ff88",
        Success = "#00ff88",
        Warning = "#ffaa00",
        Critical = "#ff2244",
        Info = "#00aaff",
        FontDisplay = "'Orbitron', 'Segoe UI', sans-serif",
        FontBody = "'Inter', 'Segoe UI', sans-serif",
        FontData = "'JetBrains Mono', 'Consolas', monospace",
        EnableGlow = true,
        EnableScanlines = true,
        EnableGradients = true,
        GlassBlur = 20f,
        GlassOpacity = 0.85f,
        BorderColor = "#00ffff33",
        BorderRadius = 4f,
        BorderWidth = 1f,
        // Cyberpunk: Sleek arc gauges with glow effects
        WidgetStyles = new()
        {
            ["gauge"] = "echarts:arc",
            ["donut"] = "echarts:default",
            ["sparkline"] = "echarts:area",
            ["progress"] = "echarts:default"
        }
    };

    /// <summary>
    /// RGB Gaming - Vibrant rainbow colors with animated effects.
    /// </summary>
    public static Theme CreateRgbGamingTheme() => new()
    {
        Id = "rgb-gaming",
        Name = "RGB Gaming",
        Background = "#0a0a0a",
        BackgroundSecondary = "#121212",
        Surface = "#1a1a1a",
        TextPrimary = "#ffffff",
        TextSecondary = "#cccccc",
        TextMuted = "#888888",
        Accent = "#ff0055",             // Hot pink
        AccentSecondary = "#00ff99",    // Electric green
        AccentTertiary = "#ffaa00",     // Amber
        Success = "#00ff66",
        Warning = "#ffcc00",
        Critical = "#ff3333",
        Info = "#00ccff",
        FontDisplay = "'Rajdhani', 'Impact', sans-serif",
        FontBody = "'Roboto', 'Arial', sans-serif",
        FontData = "'Share Tech Mono', 'Consolas', monospace",
        EnableGlow = true,
        EnableScanlines = false,
        EnableGradients = true,
        GlassBlur = 15f,
        GlassOpacity = 0.9f,
        BorderColor = "#ff005544",
        BorderRadius = 2f,
        BorderWidth = 2f,
        // RGB Gaming: Bold ring gauges with speedometer style
        WidgetStyles = new()
        {
            ["gauge"] = "echarts:ring",
            ["donut"] = "daisy:lg",
            ["sparkline"] = "echarts:line",
            ["progress"] = "daisy:lg"
        }
    };

    // ─────────────────────────────────────────────────────────────────────────
    // CORPORATE THEMES
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executive - Professional dark blue with gold accents.
    /// </summary>
    public static Theme CreateExecutiveTheme() => new()
    {
        Id = "executive",
        Name = "Executive",
        Background = "#0d1421",         // Deep navy
        BackgroundSecondary = "#141d2b",
        Surface = "#1a2535",
        TextPrimary = "#f0f4f8",
        TextSecondary = "#a8b5c4",
        TextMuted = "#6b7a8a",
        Accent = "#c9a227",             // Gold
        AccentSecondary = "#4a90d9",    // Corporate blue
        AccentTertiary = "#68c4af",     // Teal
        Success = "#4caf50",
        Warning = "#ff9800",
        Critical = "#f44336",
        Info = "#2196f3",
        FontDisplay = "'Montserrat', 'Segoe UI', sans-serif",
        FontBody = "'Open Sans', 'Segoe UI', sans-serif",
        FontData = "'Roboto Mono', 'Consolas', monospace",
        EnableGlow = false,
        EnableScanlines = false,
        EnableGradients = true,
        GlassBlur = 16f,
        GlassOpacity = 0.92f,
        BorderColor = "#c9a22733",
        BorderRadius = 6f,
        BorderWidth = 1f,
        // Executive: Clean, professional DaisyUI components
        WidgetStyles = new()
        {
            ["gauge"] = "daisy:lg",
            ["donut"] = "daisy:lg",
            ["sparkline"] = "echarts:line",
            ["progress"] = "daisy:lg"
        }
    };

    /// <summary>
    /// Clean - Modern minimal white/light theme.
    /// </summary>
    public static Theme CreateCleanTheme() => new()
    {
        Id = "clean",
        Name = "Clean",
        Background = "#ffffff",
        BackgroundSecondary = "#f5f5f5",
        Surface = "#fafafa",
        TextPrimary = "#1a1a1a",
        TextSecondary = "#555555",
        TextMuted = "#888888",
        Accent = "#0066cc",             // Corporate blue
        AccentSecondary = "#00994d",    // Green
        AccentTertiary = "#cc6600",     // Orange
        Success = "#2e7d32",
        Warning = "#ed6c02",
        Critical = "#d32f2f",
        Info = "#0288d1",
        FontDisplay = "'Inter', 'Segoe UI', sans-serif",
        FontBody = "'Inter', 'Segoe UI', sans-serif",
        FontData = "'SF Mono', 'Consolas', monospace",
        EnableGlow = false,
        EnableScanlines = false,
        EnableGradients = false,
        GlassBlur = 0f,
        GlassOpacity = 1f,
        BorderColor = "#0066cc22",
        BorderRadius = 8f,
        BorderWidth = 1f,
        // Clean: Minimal, flat DaisyUI components
        WidgetStyles = new()
        {
            ["gauge"] = "daisy:md",
            ["donut"] = "daisy:md",
            ["sparkline"] = "echarts:line",
            ["progress"] = "daisy:md"
        }
    };
}
