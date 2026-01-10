using SixLabors.ImageSharp;

namespace LCDPossible.Core.Configuration;

/// <summary>
/// Runtime-resolved color scheme with parsed Color values.
/// Use ColorScheme.Resolve() to create this from a ColorScheme.
/// </summary>
public sealed class ResolvedColorScheme
{
    // === Background Colors ===
    public Color Background { get; init; }
    public Color BackgroundSecondary { get; init; }
    public Color BarBackground { get; init; }
    public Color BarBorder { get; init; }

    // === Text Colors ===
    public Color TextPrimary { get; init; }
    public Color TextSecondary { get; init; }
    public Color TextMuted { get; init; }

    // === Semantic/Status Colors ===
    public Color Accent { get; init; }
    public Color AccentSecondary { get; init; }
    public Color Success { get; init; }
    public Color Warning { get; init; }
    public Color Critical { get; init; }
    public Color Info { get; init; }

    // === Usage Level Colors ===
    public Color UsageLow { get; init; }
    public Color UsageMedium { get; init; }
    public Color UsageHigh { get; init; }
    public Color UsageCritical { get; init; }

    // === Temperature Colors ===
    public Color TempCool { get; init; }
    public Color TempWarm { get; init; }
    public Color TempHot { get; init; }

    // === Chart Colors ===
    public IReadOnlyList<Color> ChartColors { get; init; } = [];

    /// <summary>
    /// Creates a ResolvedColorScheme from a ColorScheme configuration.
    /// </summary>
    public static ResolvedColorScheme FromColorScheme(ColorScheme scheme)
    {
        return new ResolvedColorScheme
        {
            Background = ColorParser.Parse(scheme.Background),
            BackgroundSecondary = ColorParser.Parse(scheme.BackgroundSecondary),
            BarBackground = ColorParser.Parse(scheme.BarBackground),
            BarBorder = ColorParser.Parse(scheme.BarBorder),

            TextPrimary = ColorParser.Parse(scheme.TextPrimary),
            TextSecondary = ColorParser.Parse(scheme.TextSecondary),
            TextMuted = ColorParser.Parse(scheme.TextMuted),

            Accent = ColorParser.Parse(scheme.Accent),
            AccentSecondary = ColorParser.Parse(scheme.AccentSecondary),
            Success = ColorParser.Parse(scheme.Success),
            Warning = ColorParser.Parse(scheme.Warning),
            Critical = ColorParser.Parse(scheme.Critical),
            Info = ColorParser.Parse(scheme.Info),

            UsageLow = ColorParser.Parse(scheme.UsageLow),
            UsageMedium = ColorParser.Parse(scheme.UsageMedium),
            UsageHigh = ColorParser.Parse(scheme.UsageHigh),
            UsageCritical = ColorParser.Parse(scheme.UsageCritical),

            TempCool = ColorParser.Parse(scheme.TempCool),
            TempWarm = ColorParser.Parse(scheme.TempWarm),
            TempHot = ColorParser.Parse(scheme.TempHot),

            ChartColors = scheme.ChartColors.Select(c => ColorParser.Parse(c)).ToList()
        };
    }

    /// <summary>
    /// Creates the default resolved color scheme.
    /// </summary>
    public static ResolvedColorScheme CreateDefault()
    {
        return FromColorScheme(ColorScheme.CreateDefault());
    }

    /// <summary>
    /// Gets the appropriate color for a usage percentage.
    /// </summary>
    public Color GetUsageColor(float percentage)
    {
        return percentage switch
        {
            >= 90 => UsageCritical,
            >= 70 => UsageHigh,
            >= 50 => UsageMedium,
            _ => UsageLow
        };
    }

    /// <summary>
    /// Gets the appropriate color for a temperature in Celsius.
    /// </summary>
    public Color GetTemperatureColor(float celsius)
    {
        return celsius switch
        {
            >= 85 => TempHot,
            >= 70 => TempWarm,
            _ => TempCool
        };
    }

    /// <summary>
    /// Gets a chart color by index (wraps around if index exceeds available colors).
    /// </summary>
    public Color GetChartColor(int index)
    {
        if (ChartColors.Count == 0)
        {
            return Accent;
        }
        return ChartColors[index % ChartColors.Count];
    }
}

/// <summary>
/// Extension methods for ColorScheme.
/// </summary>
public static class ColorSchemeExtensions
{
    /// <summary>
    /// Resolves a ColorScheme to a ResolvedColorScheme with parsed Color values.
    /// </summary>
    public static ResolvedColorScheme Resolve(this ColorScheme scheme)
    {
        return ResolvedColorScheme.FromColorScheme(scheme);
    }
}
