using YamlDotNet.Serialization;

namespace LCDPossible.Core.Configuration;

/// <summary>
/// Display profile loaded from YAML configuration.
/// Defines slideshow panels and their settings.
/// </summary>
public sealed class DisplayProfile
{
    /// <summary>
    /// Profile name for identification.
    /// </summary>
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "Default";

    /// <summary>
    /// Profile description.
    /// </summary>
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    /// <summary>
    /// Default update interval for panels in seconds.
    /// </summary>
    [YamlMember(Alias = "default_update_interval")]
    public int DefaultUpdateIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Default duration for panels in seconds.
    /// </summary>
    [YamlMember(Alias = "default_duration")]
    public int DefaultDurationSeconds { get; set; } = 15;

    /// <summary>
    /// Color scheme for panel rendering.
    /// </summary>
    [YamlMember(Alias = "colors")]
    public ColorScheme Colors { get; set; } = new();

    /// <summary>
    /// List of slides in the slideshow.
    /// </summary>
    [YamlMember(Alias = "slides")]
    public List<SlideDefinition> Slides { get; set; } = [];

    /// <summary>
    /// Converts the profile to a list of SlideshowItems for use with SlideshowManager.
    /// </summary>
    public List<SlideshowItem> ToSlideshowItems()
    {
        var items = new List<SlideshowItem>();

        foreach (var slide in Slides)
        {
            items.Add(new SlideshowItem
            {
                Type = slide.Type ?? "panel",
                Source = slide.Source ?? slide.Panel ?? string.Empty,
                DurationSeconds = slide.Duration ?? DefaultDurationSeconds,
                UpdateIntervalSeconds = slide.UpdateInterval ?? DefaultUpdateIntervalSeconds,
                BackgroundImage = slide.Background
            });
        }

        return items;
    }

    /// <summary>
    /// Converts the profile to a slideshow configuration string for DeviceOptions.
    /// </summary>
    public string ToSlideshowString()
    {
        var parts = new List<string>();

        foreach (var slide in Slides)
        {
            var source = slide.Type == "image" ? "image" : (slide.Panel ?? slide.Source ?? "basic-info");
            var duration = slide.Duration ?? DefaultDurationSeconds;
            var background = slide.Background;

            if (slide.Type == "image" && !string.IsNullOrEmpty(slide.Source))
            {
                // image|duration|path
                parts.Add($"image|{duration}|{slide.Source}");
            }
            else if (!string.IsNullOrEmpty(background))
            {
                // panel|duration|background
                parts.Add($"{source}|{duration}|{background}");
            }
            else
            {
                // panel|duration
                parts.Add($"{source}|{duration}");
            }
        }

        return string.Join(",", parts);
    }

    /// <summary>
    /// Creates the default profile with sensible defaults.
    /// </summary>
    public static DisplayProfile CreateDefault()
    {
        return new DisplayProfile
        {
            Name = "Default",
            Description = "Default display profile with system information panels",
            DefaultUpdateIntervalSeconds = 5,
            DefaultDurationSeconds = 15,
            Slides =
            [
                new SlideDefinition { Panel = "basic-info" },
                new SlideDefinition { Panel = "cpu-usage-graphic" },
                new SlideDefinition { Panel = "gpu-usage-graphic" },
                new SlideDefinition { Panel = "ram-usage-graphic" }
            ]
        };
    }
}

/// <summary>
/// Definition of a single slide in the profile.
/// </summary>
public sealed class SlideDefinition
{
    /// <summary>
    /// Type of slide: "panel" or "image". Defaults to "panel".
    /// </summary>
    [YamlMember(Alias = "type")]
    public string? Type { get; set; }

    /// <summary>
    /// Panel type ID (e.g., "cpu-usage-graphic", "basic-info").
    /// Used when type is "panel" or not specified.
    /// </summary>
    [YamlMember(Alias = "panel")]
    public string? Panel { get; set; }

    /// <summary>
    /// Source path for images, or alternative to panel for flexibility.
    /// </summary>
    [YamlMember(Alias = "source")]
    public string? Source { get; set; }

    /// <summary>
    /// Duration to display this slide in seconds.
    /// If not specified, uses profile default.
    /// </summary>
    [YamlMember(Alias = "duration")]
    public int? Duration { get; set; }

    /// <summary>
    /// Update interval for panel content in seconds.
    /// If not specified, uses profile default.
    /// </summary>
    [YamlMember(Alias = "update_interval")]
    public int? UpdateInterval { get; set; }

    /// <summary>
    /// Optional background image path for panels.
    /// </summary>
    [YamlMember(Alias = "background")]
    public string? Background { get; set; }
}
