using LCDPossible.Core.Configuration;

namespace LCDPossible.Cli;

/// <summary>
/// Parses inline profile format for quick CLI usage.
///
/// Formats supported:
///   Panel only:
///     basic-info
///     bouncing-logo
///
///   With system parameters (@ prefix):
///     basic-info|@duration=10
///     basic-info|@duration=10@interval=5
///
///   With custom panel settings (pipe-separated key=value):
///     bouncing-logo|text=HELLO|color=red|size=large
///     bouncing-logo|text=DVD|color=#FF0000|3d=true|rotate=true
///
///   Mixed system and custom parameters:
///     bouncing-logo|@duration=30|text=HELLO|color=rainbow
///     bouncing-logo|text=TEST|@duration=15|size=large|3d=true
///
///   Multiple panels (comma-separated):
///     basic-info|@duration=10,cpu-usage-graphic|@duration=15
///     bouncing-logo|text=DVD|color=cycle,starfield|@duration=20
///
/// System parameters (@ prefix, apply to slideshow behavior):
///   @duration=N  - Display duration in seconds
///   @interval=N  - Update interval in seconds
///   @background=path - Background image path
///   @path=path   - Image source path (for type=image)
///   @theme=name  - Theme override (cyberpunk, rgb-gaming, executive, clean)
///   @effect=name - Page effect (hologram, matrix-rain, scanlines, etc.)
///
/// Custom parameters (no @ prefix, passed to panel plugin):
///   Any key=value pair without @ is treated as a custom panel setting.
///   These are passed to the panel plugin via Settings dictionary.
/// </summary>
public static class InlineProfileParser
{
    // System parameters that affect slideshow behavior (not passed to panel)
    private static readonly HashSet<string> SystemParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "duration",
        "interval",
        "background",
        "path",
        "transition",
        "transition_duration",
        "theme",
        "effect"
    };

    /// <summary>
    /// Parses an inline profile string into slideshow items.
    /// </summary>
    /// <param name="profile">The inline profile string.</param>
    /// <returns>List of slideshow items.</returns>
    public static List<SlideshowItem> Parse(string profile)
    {
        var items = new List<SlideshowItem>();

        if (string.IsNullOrWhiteSpace(profile))
        {
            return items;
        }

        // Split by comma to get individual panel specs
        var specs = profile.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var spec in specs)
        {
            var item = ParseItem(spec);
            if (item != null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private static SlideshowItem? ParseItem(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return null;
        }

        // Split by | to separate panel name from parameters
        var parts = spec.Split('|');
        var panelOrType = parts[0].Trim();

        // Parse all parameters from remaining parts
        var (systemParams, customSettings) = ParseAllParameters(parts.Skip(1));

        // Create the slideshow item
        var item = new SlideshowItem();

        // Check if this is an image type
        if (panelOrType.Equals("image", StringComparison.OrdinalIgnoreCase))
        {
            item.Type = "image";
            item.Source = systemParams.GetValueOrDefault("path", string.Empty);
        }
        else
        {
            item.Type = "panel";
            item.Source = panelOrType;
        }

        // Apply system parameters
        if (systemParams.TryGetValue("duration", out var durationStr) && int.TryParse(durationStr, out var duration))
        {
            item.DurationSeconds = duration;
        }

        if (systemParams.TryGetValue("interval", out var intervalStr) && int.TryParse(intervalStr, out var interval))
        {
            item.UpdateIntervalSeconds = interval;
        }

        if (systemParams.TryGetValue("background", out var background))
        {
            item.BackgroundImage = background;
        }

        if (systemParams.TryGetValue("theme", out var theme))
        {
            item.Theme = theme;
        }

        if (systemParams.TryGetValue("effect", out var effect))
        {
            item.PageEffect = effect;
        }

        // For image type, also check "path" as source
        if (item.Type == "image" && systemParams.TryGetValue("path", out var path))
        {
            item.Source = path;
        }

        // Set custom panel settings if any exist
        if (customSettings.Count > 0)
        {
            item.Settings = customSettings;
        }

        return item;
    }

    /// <summary>
    /// Parses parameters from pipe-separated segments.
    /// Supports both @ prefix format and plain key=value format.
    /// </summary>
    /// <returns>Tuple of (system parameters, custom panel settings)</returns>
    private static (Dictionary<string, string> system, Dictionary<string, string> custom) ParseAllParameters(IEnumerable<string> segments)
    {
        var systemParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var customSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            var trimmed = segment.Trim();

            // Check if this segment uses @ format (can contain multiple @key=value pairs)
            if (trimmed.StartsWith('@') || trimmed.Contains('@'))
            {
                // Parse @ format: @key=value@key=value
                var atParams = ParseAtFormatParameters(trimmed);
                foreach (var (key, value) in atParams)
                {
                    // All @ prefixed params are system params
                    systemParams[key] = value;
                }
            }
            else if (trimmed.Contains('='))
            {
                // Plain key=value format
                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = trimmed[..eqIndex].Trim();
                    var value = trimmed[(eqIndex + 1)..].Trim();

                    if (SystemParameters.Contains(key))
                    {
                        systemParams[key] = value;
                    }
                    else
                    {
                        customSettings[key] = value;
                    }
                }
            }
        }

        return (systemParams, customSettings);
    }

    /// <summary>
    /// Parses the @ format: @key=value@key=value...
    /// </summary>
    private static Dictionary<string, string> ParseAtFormatParameters(string paramsString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Remove leading @ if present
        paramsString = paramsString.TrimStart('@');

        // Split by @ to get individual param=value pairs
        var pairs = paramsString.Split('@', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var pair in pairs)
        {
            var eqIndex = pair.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = pair[..eqIndex].Trim();
                var value = pair[(eqIndex + 1)..].Trim();
                result[key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Validates an inline profile string and returns any errors.
    /// </summary>
    public static (bool IsValid, string? Error) Validate(string profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            return (false, "Profile string is empty");
        }

        var items = Parse(profile);

        if (items.Count == 0)
        {
            return (false, "No valid items found in profile");
        }

        foreach (var item in items)
        {
            if (item.Type == "panel")
            {
                if (string.IsNullOrEmpty(item.Source))
                {
                    return (false, "Panel type specified but no panel name provided");
                }

                if (!IsValidPanelType(item.Source))
                {
                    return (false, $"Unknown panel type: {item.Source}");
                }
            }
            else if (item.Type == "image")
            {
                if (string.IsNullOrEmpty(item.Source))
                {
                    return (false, "Image type specified but no path provided (use @path=...)");
                }
            }
        }

        return (true, null);
    }

    /// <summary>
    /// Checks if a panel type string is valid.
    /// Handles both exact matches (cpu-info) and prefix-based panels (animated-gif:path, video:url).
    /// Note: Since plugins are loaded on-demand, we can only validate known patterns here.
    /// The actual panel loading will fail later if the panel type doesn't exist.
    /// </summary>
    private static bool IsValidPanelType(string panelType)
    {
        var lowerType = panelType.ToLowerInvariant();

        // Check for prefix-based media panels
        string[] mediaPrefixes = ["animated-gif:", "image-sequence:", "video:", "html:", "web:"];
        foreach (var prefix in mediaPrefixes)
        {
            if (lowerType.StartsWith(prefix))
            {
                return true;
            }
        }

        // Check for proxmox panels
        if (lowerType.StartsWith("proxmox"))
        {
            return true;
        }

        // Known built-in panel types (core plugin)
        string[] coreTypes =
        [
            "basic-info", "basic-usage-text", "os-info", "os-status", "os-notifications",
            "cpu-info", "cpu-usage-text", "cpu-usage-graphic",
            "gpu-info", "gpu-usage-text", "gpu-usage-graphic",
            "ram-info", "ram-usage-text", "ram-usage-graphic"
        ];

        foreach (var coreType in coreTypes)
        {
            if (coreType.Equals(lowerType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Allow any panel type that looks valid (will be validated at load time)
        // This allows user plugins without requiring plugin loading for validation
        return !string.IsNullOrWhiteSpace(panelType) && !panelType.Contains(' ');
    }
}
