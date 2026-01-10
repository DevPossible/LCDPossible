using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Core.Configuration;

/// <summary>
/// Parses color strings from configuration into ImageSharp colors.
/// Supports hex format (#RGB, #RRGGBB, #AARRGGBB) and common color names.
/// </summary>
public static class ColorParser
{
    private static readonly Dictionary<string, Rgba32> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        // Basic colors
        ["white"] = new Rgba32(255, 255, 255),
        ["black"] = new Rgba32(0, 0, 0),
        ["red"] = new Rgba32(255, 0, 0),
        ["green"] = new Rgba32(0, 128, 0),
        ["blue"] = new Rgba32(0, 0, 255),
        ["yellow"] = new Rgba32(255, 255, 0),
        ["cyan"] = new Rgba32(0, 255, 255),
        ["magenta"] = new Rgba32(255, 0, 255),
        ["orange"] = new Rgba32(255, 165, 0),
        ["purple"] = new Rgba32(128, 0, 128),
        ["pink"] = new Rgba32(255, 192, 203),
        ["brown"] = new Rgba32(139, 69, 19),
        ["gray"] = new Rgba32(128, 128, 128),
        ["grey"] = new Rgba32(128, 128, 128),

        // Extended colors
        ["lime"] = new Rgba32(0, 255, 0),
        ["navy"] = new Rgba32(0, 0, 128),
        ["teal"] = new Rgba32(0, 128, 128),
        ["maroon"] = new Rgba32(128, 0, 0),
        ["olive"] = new Rgba32(128, 128, 0),
        ["silver"] = new Rgba32(192, 192, 192),
        ["aqua"] = new Rgba32(0, 255, 255),
        ["fuchsia"] = new Rgba32(255, 0, 255),
        ["coral"] = new Rgba32(255, 127, 80),
        ["crimson"] = new Rgba32(220, 20, 60),
        ["gold"] = new Rgba32(255, 215, 0),
        ["indigo"] = new Rgba32(75, 0, 130),
        ["ivory"] = new Rgba32(255, 255, 240),
        ["khaki"] = new Rgba32(240, 230, 140),
        ["lavender"] = new Rgba32(230, 230, 250),
        ["salmon"] = new Rgba32(250, 128, 114),
        ["tan"] = new Rgba32(210, 180, 140),
        ["turquoise"] = new Rgba32(64, 224, 208),
        ["violet"] = new Rgba32(238, 130, 238),

        // Dark variants
        ["darkgray"] = new Rgba32(64, 64, 64),
        ["darkgrey"] = new Rgba32(64, 64, 64),
        ["darkred"] = new Rgba32(139, 0, 0),
        ["darkgreen"] = new Rgba32(0, 100, 0),
        ["darkblue"] = new Rgba32(0, 0, 139),
        ["darkcyan"] = new Rgba32(0, 139, 139),
        ["darkmagenta"] = new Rgba32(139, 0, 139),
        ["darkorange"] = new Rgba32(255, 140, 0),

        // Light variants
        ["lightgray"] = new Rgba32(211, 211, 211),
        ["lightgrey"] = new Rgba32(211, 211, 211),
        ["lightblue"] = new Rgba32(173, 216, 230),
        ["lightgreen"] = new Rgba32(144, 238, 144),
        ["lightcyan"] = new Rgba32(224, 255, 255),
        ["lightpink"] = new Rgba32(255, 182, 193),
        ["lightyellow"] = new Rgba32(255, 255, 224),

        // Transparent
        ["transparent"] = new Rgba32(0, 0, 0, 0),
    };

    /// <summary>
    /// Parses a color string to an ImageSharp Color.
    /// </summary>
    /// <param name="colorString">Color string (hex or name).</param>
    /// <param name="fallback">Fallback color if parsing fails.</param>
    /// <returns>Parsed color or fallback.</returns>
    public static Color Parse(string? colorString, Color? fallback = null)
    {
        var rgba = ParseToRgba32(colorString, fallback.HasValue
            ? new Rgba32(fallback.Value.ToPixel<Rgba32>().R, fallback.Value.ToPixel<Rgba32>().G, fallback.Value.ToPixel<Rgba32>().B, fallback.Value.ToPixel<Rgba32>().A)
            : new Rgba32(255, 255, 255));
        return new Color(rgba);
    }

    /// <summary>
    /// Parses a color string to an Rgba32 value.
    /// </summary>
    /// <param name="colorString">Color string (hex or name).</param>
    /// <param name="fallback">Fallback color if parsing fails.</param>
    /// <returns>Parsed Rgba32 or fallback.</returns>
    public static Rgba32 ParseToRgba32(string? colorString, Rgba32? fallback = null)
    {
        var defaultColor = fallback ?? new Rgba32(255, 255, 255);

        if (string.IsNullOrWhiteSpace(colorString))
        {
            return defaultColor;
        }

        colorString = colorString.Trim();

        // Try named colors first
        if (NamedColors.TryGetValue(colorString, out var namedColor))
        {
            return namedColor;
        }

        // Try hex format
        if (colorString.StartsWith('#'))
        {
            return ParseHex(colorString[1..], defaultColor);
        }

        // Try without # prefix
        if (IsHexString(colorString))
        {
            return ParseHex(colorString, defaultColor);
        }

        // Try rgb(r, g, b) format
        if (colorString.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
        {
            return ParseRgb(colorString, defaultColor);
        }

        // Try rgba(r, g, b, a) format
        if (colorString.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
        {
            return ParseRgba(colorString, defaultColor);
        }

        return defaultColor;
    }

    private static Rgba32 ParseHex(string hex, Rgba32 fallback)
    {
        try
        {
            return hex.Length switch
            {
                // #RGB -> #RRGGBB
                3 => new Rgba32(
                    (byte)(Convert.ToByte(hex[0..1], 16) * 17),
                    (byte)(Convert.ToByte(hex[1..2], 16) * 17),
                    (byte)(Convert.ToByte(hex[2..3], 16) * 17)),

                // #RGBA -> #RRGGBBAA
                4 => new Rgba32(
                    (byte)(Convert.ToByte(hex[0..1], 16) * 17),
                    (byte)(Convert.ToByte(hex[1..2], 16) * 17),
                    (byte)(Convert.ToByte(hex[2..3], 16) * 17),
                    (byte)(Convert.ToByte(hex[3..4], 16) * 17)),

                // #RRGGBB
                6 => new Rgba32(
                    Convert.ToByte(hex[0..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16)),

                // #RRGGBBAA
                8 => new Rgba32(
                    Convert.ToByte(hex[0..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16),
                    Convert.ToByte(hex[6..8], 16)),

                _ => fallback
            };
        }
        catch
        {
            return fallback;
        }
    }

    private static Rgba32 ParseRgb(string rgb, Rgba32 fallback)
    {
        try
        {
            var content = rgb[4..^1]; // Remove "rgb(" and ")"
            var parts = content.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length == 3)
            {
                return new Rgba32(
                    byte.Parse(parts[0]),
                    byte.Parse(parts[1]),
                    byte.Parse(parts[2]));
            }
        }
        catch { }
        return fallback;
    }

    private static Rgba32 ParseRgba(string rgba, Rgba32 fallback)
    {
        try
        {
            var content = rgba[5..^1]; // Remove "rgba(" and ")"
            var parts = content.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length == 4)
            {
                var alpha = float.Parse(parts[3]);
                return new Rgba32(
                    byte.Parse(parts[0]),
                    byte.Parse(parts[1]),
                    byte.Parse(parts[2]),
                    (byte)(alpha * 255));
            }
        }
        catch { }
        return fallback;
    }

    private static bool IsHexString(string s)
    {
        return s.Length is 3 or 4 or 6 or 8 &&
               s.All(c => char.IsAsciiHexDigit(c));
    }

    /// <summary>
    /// Converts a Color to hex string format (#RRGGBB).
    /// </summary>
    public static string ToHexString(Color color)
    {
        var pixel = color.ToPixel<Rgba32>();
        return $"#{pixel.R:X2}{pixel.G:X2}{pixel.B:X2}";
    }

    /// <summary>
    /// Converts an Rgba32 to hex string format (#RRGGBB or #RRGGBBAA).
    /// </summary>
    public static string ToHexString(Rgba32 color, bool includeAlpha = false)
    {
        return includeAlpha
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}"
            : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
