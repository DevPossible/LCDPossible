using YamlDotNet.Serialization;

namespace LCDPossible.Core.Configuration;

/// <summary>
/// Defines the color scheme for display panels.
/// All colors use hex format (#RRGGBB or #AARRGGBB) or common color names.
/// </summary>
public sealed class ColorScheme
{
    // === Background Colors ===

    /// <summary>
    /// Main panel background color.
    /// </summary>
    [YamlMember(Alias = "background")]
    public string Background { get; set; } = "#0F0F19";

    /// <summary>
    /// Secondary/alternate background for contrast areas.
    /// </summary>
    [YamlMember(Alias = "background_secondary")]
    public string BackgroundSecondary { get; set; } = "#282832";

    /// <summary>
    /// Background for progress bars and meters.
    /// </summary>
    [YamlMember(Alias = "bar_background")]
    public string BarBackground { get; set; } = "#282832";

    /// <summary>
    /// Border color for progress bars and containers.
    /// </summary>
    [YamlMember(Alias = "bar_border")]
    public string BarBorder { get; set; } = "#505064";

    // === Text Colors ===

    /// <summary>
    /// Primary text color for important values.
    /// </summary>
    [YamlMember(Alias = "text_primary")]
    public string TextPrimary { get; set; } = "#FFFFFF";

    /// <summary>
    /// Secondary/dimmed text color for labels.
    /// </summary>
    [YamlMember(Alias = "text_secondary")]
    public string TextSecondary { get; set; } = "#B4B4C8";

    /// <summary>
    /// Muted text for timestamps and less important info.
    /// </summary>
    [YamlMember(Alias = "text_muted")]
    public string TextMuted { get; set; } = "#6E6E82";

    // === Semantic/Status Colors ===

    /// <summary>
    /// Accent color for titles and highlights.
    /// </summary>
    [YamlMember(Alias = "accent")]
    public string Accent { get; set; } = "#0096FF";

    /// <summary>
    /// Secondary accent for variety.
    /// </summary>
    [YamlMember(Alias = "accent_secondary")]
    public string AccentSecondary { get; set; } = "#00D4AA";

    /// <summary>
    /// Success/good status color.
    /// </summary>
    [YamlMember(Alias = "success")]
    public string Success { get; set; } = "#32C864";

    /// <summary>
    /// Warning status color.
    /// </summary>
    [YamlMember(Alias = "warning")]
    public string Warning { get; set; } = "#FFB400";

    /// <summary>
    /// Critical/error status color.
    /// </summary>
    [YamlMember(Alias = "critical")]
    public string Critical { get; set; } = "#FF3232";

    /// <summary>
    /// Info/neutral status color.
    /// </summary>
    [YamlMember(Alias = "info")]
    public string Info { get; set; } = "#00AAFF";

    // === Usage Level Colors (for progress bars) ===

    /// <summary>
    /// Color for low usage (0-50%).
    /// </summary>
    [YamlMember(Alias = "usage_low")]
    public string UsageLow { get; set; } = "#32C864";

    /// <summary>
    /// Color for medium usage (50-70%).
    /// </summary>
    [YamlMember(Alias = "usage_medium")]
    public string UsageMedium { get; set; } = "#0096FF";

    /// <summary>
    /// Color for high usage (70-90%).
    /// </summary>
    [YamlMember(Alias = "usage_high")]
    public string UsageHigh { get; set; } = "#FFB400";

    /// <summary>
    /// Color for critical usage (90%+).
    /// </summary>
    [YamlMember(Alias = "usage_critical")]
    public string UsageCritical { get; set; } = "#FF3232";

    // === Temperature Colors ===

    /// <summary>
    /// Color for cool temperatures.
    /// </summary>
    [YamlMember(Alias = "temp_cool")]
    public string TempCool { get; set; } = "#32C864";

    /// <summary>
    /// Color for warm temperatures.
    /// </summary>
    [YamlMember(Alias = "temp_warm")]
    public string TempWarm { get; set; } = "#FFB400";

    /// <summary>
    /// Color for hot/critical temperatures.
    /// </summary>
    [YamlMember(Alias = "temp_hot")]
    public string TempHot { get; set; } = "#FF3232";

    // === Chart/Graph Colors ===

    /// <summary>
    /// Colors for multi-series charts and graphs.
    /// </summary>
    [YamlMember(Alias = "chart_colors")]
    public List<string> ChartColors { get; set; } =
    [
        "#0096FF",  // Blue
        "#00D4AA",  // Teal
        "#FFB400",  // Orange
        "#FF6B9D",  // Pink
        "#A855F7",  // Purple
        "#32C864",  // Green
        "#FF3232",  // Red
        "#00AAFF"   // Light Blue
    ];

    /// <summary>
    /// Creates the default dark color scheme.
    /// </summary>
    public static ColorScheme CreateDefault() => new();

    /// <summary>
    /// Creates a light color scheme.
    /// </summary>
    public static ColorScheme CreateLight() => new()
    {
        Background = "#F5F5F5",
        BackgroundSecondary = "#E0E0E0",
        BarBackground = "#D0D0D0",
        BarBorder = "#A0A0A0",
        TextPrimary = "#1A1A1A",
        TextSecondary = "#4A4A4A",
        TextMuted = "#7A7A7A",
        Accent = "#0066CC",
        AccentSecondary = "#009988",
        Success = "#28A745",
        Warning = "#FFA000",
        Critical = "#DC3545",
        Info = "#17A2B8"
    };

    /// <summary>
    /// Creates a high-contrast color scheme.
    /// </summary>
    public static ColorScheme CreateHighContrast() => new()
    {
        Background = "#000000",
        BackgroundSecondary = "#1A1A1A",
        BarBackground = "#1A1A1A",
        BarBorder = "#FFFFFF",
        TextPrimary = "#FFFFFF",
        TextSecondary = "#FFFF00",
        TextMuted = "#00FFFF",
        Accent = "#00FF00",
        AccentSecondary = "#FF00FF",
        Success = "#00FF00",
        Warning = "#FFFF00",
        Critical = "#FF0000",
        Info = "#00FFFF",
        UsageLow = "#00FF00",
        UsageMedium = "#00FFFF",
        UsageHigh = "#FFFF00",
        UsageCritical = "#FF0000"
    };
}
