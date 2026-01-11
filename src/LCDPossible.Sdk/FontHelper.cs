using SixLabors.Fonts;

namespace LCDPossible.Sdk;

/// <summary>
/// Cross-platform font loading helper.
/// Provides consistent font access across Windows, Linux, and macOS.
/// </summary>
public static class FontHelper
{
    // Preferred sans-serif font names in order of priority
    private static readonly string[] PreferredFontNames =
    [
        // Windows fonts
        "Arial",
        "Segoe UI",
        "Tahoma",
        // Linux fonts (fonts-dejavu-core, fonts-liberation)
        "DejaVu Sans",
        "Liberation Sans",
        "FreeSans",
        // macOS fonts
        "Helvetica Neue",
        "Helvetica",
        "SF Pro Display",
        // Fallback
        "Sans",
        "sans-serif"
    ];

    // Preferred monospace font names in order of priority
    private static readonly string[] PreferredMonoFontNames =
    [
        // Windows fonts
        "Consolas",
        "Courier New",
        "Lucida Console",
        // Linux fonts (fonts-dejavu-core, fonts-liberation)
        "DejaVu Sans Mono",
        "Liberation Mono",
        "FreeMono",
        // macOS fonts
        "SF Mono",
        "Menlo",
        "Monaco",
        // Fallback
        "Mono",
        "monospace"
    ];

    private static FontFamily? _cachedFamily;
    private static FontFamily? _cachedMonoFamily;
    private static bool _initialized;
    private static bool _monoInitialized;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets a preferred font at the specified size, or null if no fonts are available.
    /// </summary>
    /// <param name="size">Font size in points.</param>
    /// <param name="style">Font style.</param>
    /// <returns>A Font instance or null if no fonts could be loaded.</returns>
    public static Font? GetPreferredFont(float size, FontStyle style = FontStyle.Regular)
    {
        var family = GetPreferredFontFamily();
        return family?.CreateFont(size, style);
    }

    /// <summary>
    /// Gets the preferred font family, searching for common cross-platform fonts.
    /// </summary>
    /// <returns>A FontFamily instance or null if no fonts are available.</returns>
    public static FontFamily? GetPreferredFontFamily()
    {
        if (_initialized)
        {
            return _cachedFamily;
        }

        lock (_lock)
        {
            if (_initialized)
            {
                return _cachedFamily;
            }

            _cachedFamily = FindFontFamily(PreferredFontNames);
            _initialized = true;
        }

        return _cachedFamily;
    }

    /// <summary>
    /// Gets a preferred monospace font at the specified size, or null if no fonts are available.
    /// </summary>
    /// <param name="size">Font size in points.</param>
    /// <param name="style">Font style.</param>
    /// <returns>A Font instance or null if no fonts could be loaded.</returns>
    public static Font? GetPreferredMonoFont(float size, FontStyle style = FontStyle.Regular)
    {
        var family = GetPreferredMonoFontFamily();
        return family?.CreateFont(size, style);
    }

    /// <summary>
    /// Gets the preferred monospace font family, searching for common cross-platform monospace fonts.
    /// </summary>
    /// <returns>A FontFamily instance or null if no fonts are available.</returns>
    public static FontFamily? GetPreferredMonoFontFamily()
    {
        if (_monoInitialized)
        {
            return _cachedMonoFamily;
        }

        lock (_lock)
        {
            if (_monoInitialized)
            {
                return _cachedMonoFamily;
            }

            _cachedMonoFamily = FindFontFamily(PreferredMonoFontNames);
            _monoInitialized = true;
        }

        return _cachedMonoFamily;
    }

    /// <summary>
    /// Checks if any fonts are available on the system.
    /// </summary>
    public static bool HasFonts()
    {
        try
        {
            return SystemFonts.Families.Any();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Lists all available font families on the system.
    /// </summary>
    public static IEnumerable<string> ListAvailableFonts()
    {
        try
        {
            return SystemFonts.Families.Select(f => f.Name).OrderBy(n => n);
        }
        catch
        {
            return [];
        }
    }

    private static FontFamily? FindFontFamily(string[] preferredNames)
    {
        try
        {
            var families = SystemFonts.Families.ToArray();

            if (families.Length == 0)
            {
                return null;
            }

            // Try each preferred font name
            foreach (var preferredName in preferredNames)
            {
                var family = families.FirstOrDefault(f =>
                    f.Name.Equals(preferredName, StringComparison.OrdinalIgnoreCase));

                if (family.Name != null)
                {
                    // Verify the font actually works
                    if (TryCreateFont(family))
                    {
                        return family;
                    }
                }

                // Also try partial matches for variants (e.g., "DejaVu Sans Mono" contains "DejaVu Sans")
                family = families.FirstOrDefault(f =>
                    f.Name.Contains(preferredName, StringComparison.OrdinalIgnoreCase));

                if (family.Name != null)
                {
                    if (TryCreateFont(family))
                    {
                        return family;
                    }
                }
            }

            // Try to find any font that doesn't throw when accessed
            foreach (var family in families)
            {
                if (TryCreateFont(family))
                {
                    return family;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryCreateFont(FontFamily family)
    {
        try
        {
            // Test that the font can actually be used
            var font = family.CreateFont(12, FontStyle.Regular);
            // Font doesn't implement IDisposable, just verify it was created successfully
            return font != null;
        }
        catch
        {
            return false;
        }
    }
}
