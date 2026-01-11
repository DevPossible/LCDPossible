using LCDPossible.Core.Transitions;
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
    /// Default transition effect when switching panels.
    /// Options: none, fade, crossfade, slide-left, slide-right, slide-up, slide-down,
    /// wipe-left, wipe-right, wipe-up, wipe-down, zoom-in, zoom-out, push-left, push-right, random.
    /// Default is "random".
    /// </summary>
    [YamlMember(Alias = "default_transition")]
    public string DefaultTransition { get; set; } = "random";

    /// <summary>
    /// Default transition duration in milliseconds.
    /// Range: 50-2000ms. Default is 1500ms.
    /// </summary>
    [YamlMember(Alias = "default_transition_duration")]
    public int DefaultTransitionDurationMs { get; set; } = 1500;

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
            // Parse transition type (use slide-specific or fall back to profile default)
            var transitionString = slide.Transition ?? DefaultTransition;
            var transitionType = TransitionTypeExtensions.Parse(transitionString);

            // Use slide-specific duration or fall back to profile default
            var transitionDuration = slide.TransitionDurationMs ?? DefaultTransitionDurationMs;
            transitionDuration = Math.Clamp(transitionDuration,
                TransitionEngine.MinDurationMs, TransitionEngine.MaxDurationMs);

            items.Add(new SlideshowItem
            {
                Type = slide.Type ?? "panel",
                Source = slide.Source ?? slide.Panel ?? string.Empty,
                DurationSeconds = slide.Duration ?? DefaultDurationSeconds,
                UpdateIntervalSeconds = slide.UpdateInterval ?? DefaultUpdateIntervalSeconds,
                BackgroundImage = slide.Background,
                Transition = transitionType,
                TransitionDurationMs = transitionDuration
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
                new SlideDefinition { Panel = "ram-usage-graphic" },
                new SlideDefinition { Panel = "network-info" }
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

    /// <summary>
    /// Transition effect when entering this slide (overrides profile default).
    /// Options: none, fade, crossfade, slide-left, slide-right, slide-up, slide-down,
    /// wipe-left, wipe-right, wipe-up, wipe-down, zoom-in, zoom-out, push-left, push-right, random.
    /// </summary>
    [YamlMember(Alias = "transition")]
    public string? Transition { get; set; }

    /// <summary>
    /// Transition duration in milliseconds (overrides profile default).
    /// Range: 50-2000ms.
    /// </summary>
    [YamlMember(Alias = "transition_duration")]
    public int? TransitionDurationMs { get; set; }
}
