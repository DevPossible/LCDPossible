using LCDPossible.Core.Configuration;

namespace LCDPossible.Cli;

/// <summary>
/// Parses inline profile format for quick CLI usage.
/// Format: {panel}|@{param}={value}@{param}={value},{panel},...
/// Examples:
///   basic-info
///   basic-info|@duration=10
///   basic-info|@duration=10@interval=5
///   basic-info|@duration=10,cpu-usage-graphic|@duration=15
///   image|@path=C:\pic.jpg@duration=5
/// </summary>
public static class InlineProfileParser
{
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
        var parts = spec.Split('|', 2);
        var panelOrType = parts[0].Trim();
        var paramsString = parts.Length > 1 ? parts[1] : string.Empty;

        // Parse parameters (format: @param=value@param=value)
        var parameters = ParseParameters(paramsString);

        // Create the slideshow item
        var item = new SlideshowItem();

        // Check if this is an image type
        if (panelOrType.Equals("image", StringComparison.OrdinalIgnoreCase))
        {
            item.Type = "image";
            item.Source = parameters.GetValueOrDefault("path", string.Empty);
        }
        else
        {
            item.Type = "panel";
            item.Source = panelOrType;
        }

        // Apply parameters
        if (parameters.TryGetValue("duration", out var durationStr) && int.TryParse(durationStr, out var duration))
        {
            item.DurationSeconds = duration;
        }

        if (parameters.TryGetValue("interval", out var intervalStr) && int.TryParse(intervalStr, out var interval))
        {
            item.UpdateIntervalSeconds = interval;
        }

        if (parameters.TryGetValue("background", out var background))
        {
            item.BackgroundImage = background;
        }

        // For image type, also check "path" as source
        if (item.Type == "image" && parameters.TryGetValue("path", out var path))
        {
            item.Source = path;
        }

        return item;
    }

    private static Dictionary<string, string> ParseParameters(string paramsString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(paramsString))
        {
            return result;
        }

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

        // Check for exact matches in available panels (excluding template entries)
        foreach (var available in Panels.PanelFactory.AvailablePanels)
        {
            // Skip template entries like "animated-gif:<path>"
            if (available.Contains('<'))
            {
                continue;
            }

            if (available.Equals(panelType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
