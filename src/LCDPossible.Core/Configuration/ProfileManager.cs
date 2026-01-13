using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LCDPossible.Core.Configuration;

/// <summary>
/// Manages display profiles with CRUD operations.
/// Supports loading, saving, and modifying profiles in the user data directory.
/// </summary>
public sealed class ProfileManager
{
    private const string ProfileExtension = ".yaml";
    private const string DefaultProfileName = "profile";  // Must match ProfileLoader's default filename

    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public ProfileManager()
    {
        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Gets the profiles directory (user data directory).
    /// </summary>
    public static string ProfilesDirectory => PlatformPaths.GetUserDataDirectory();

    /// <summary>
    /// Gets the full path for a profile by name.
    /// </summary>
    public static string GetProfilePath(string? profileName = null)
    {
        var name = string.IsNullOrWhiteSpace(profileName) ? DefaultProfileName : profileName;

        // If already ends with .yaml, use as-is
        if (name.EndsWith(ProfileExtension, StringComparison.OrdinalIgnoreCase))
        {
            // If it's a full path, use it directly
            if (Path.IsPathRooted(name))
            {
                return name;
            }
            return Path.Combine(ProfilesDirectory, name);
        }

        return Path.Combine(ProfilesDirectory, $"{name}{ProfileExtension}");
    }

    /// <summary>
    /// Lists all available profiles in the user data directory.
    /// </summary>
    public IEnumerable<string> ListProfiles()
    {
        var dir = ProfilesDirectory;
        if (!Directory.Exists(dir))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(dir, $"*{ProfileExtension}"))
        {
            yield return Path.GetFileNameWithoutExtension(file);
        }
    }

    /// <summary>
    /// Checks if a profile exists.
    /// </summary>
    public bool ProfileExists(string? profileName = null)
    {
        return File.Exists(GetProfilePath(profileName));
    }

    /// <summary>
    /// Loads a profile by name.
    /// For the default profile, returns an in-memory default if file doesn't exist.
    /// For named profiles, throws FileNotFoundException if not found.
    /// </summary>
    public DisplayProfile LoadProfile(string? profileName = null)
    {
        var path = GetProfilePath(profileName);
        var isDefault = string.IsNullOrWhiteSpace(profileName) ||
                        profileName.Equals(DefaultProfileName, StringComparison.OrdinalIgnoreCase);

        if (!File.Exists(path))
        {
            // For the default profile, return an in-memory default
            // It will be persisted when the user modifies it
            if (isDefault)
            {
                return DisplayProfile.CreateDefault();
            }

            throw new FileNotFoundException($"Profile not found: {path}", path);
        }

        var yaml = File.ReadAllText(path);
        var profile = _deserializer.Deserialize<DisplayProfile>(yaml);

        return profile ?? throw new InvalidOperationException($"Failed to parse profile: {path}");
    }

    /// <summary>
    /// Saves a profile by name.
    /// </summary>
    public void SaveProfile(DisplayProfile profile, string? profileName = null)
    {
        var path = GetProfilePath(profileName);
        var dir = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var yaml = _serializer.Serialize(profile);
        File.WriteAllText(path, yaml);
    }

    /// <summary>
    /// Creates a new profile with default settings.
    /// </summary>
    public DisplayProfile CreateNewProfile(string profileName, string? description = null)
    {
        var profile = new DisplayProfile
        {
            Name = profileName,
            Description = description ?? $"Display profile created {DateTime.Now:yyyy-MM-dd}",
            DefaultUpdateIntervalSeconds = 5,
            DefaultDurationSeconds = 15,
            Slides = []
        };

        SaveProfile(profile, profileName);
        return profile;
    }

    /// <summary>
    /// Deletes a profile by name.
    /// </summary>
    public bool DeleteProfile(string profileName)
    {
        var path = GetProfilePath(profileName);

        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    /// <summary>
    /// Adds a panel to the end of a profile.
    /// </summary>
    public (int index, SlideDefinition slide) AppendPanel(
        string? profileName,
        string panelType,
        int? duration = null,
        int? updateInterval = null,
        string? background = null)
    {
        var profile = LoadProfile(profileName);

        var slide = new SlideDefinition
        {
            Panel = panelType,
            Duration = duration,
            UpdateInterval = updateInterval,
            Background = background
        };

        profile.Slides.Add(slide);
        SaveProfile(profile, profileName);

        return (profile.Slides.Count - 1, slide);
    }

    /// <summary>
    /// Removes a panel at the specified index.
    /// </summary>
    public SlideDefinition? RemovePanel(string? profileName, int index)
    {
        var profile = LoadProfile(profileName);

        if (index < 0 || index >= profile.Slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Index {index} is out of range. Valid range: 0-{profile.Slides.Count - 1}");
        }

        var removed = profile.Slides[index];
        profile.Slides.RemoveAt(index);
        SaveProfile(profile, profileName);

        return removed;
    }

    /// <summary>
    /// Moves a panel from one index to another.
    /// </summary>
    public void MovePanel(string? profileName, int fromIndex, int toIndex)
    {
        var profile = LoadProfile(profileName);

        if (fromIndex < 0 || fromIndex >= profile.Slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(fromIndex),
                $"From index {fromIndex} is out of range. Valid range: 0-{profile.Slides.Count - 1}");
        }

        if (toIndex < 0 || toIndex >= profile.Slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(toIndex),
                $"To index {toIndex} is out of range. Valid range: 0-{profile.Slides.Count - 1}");
        }

        if (fromIndex == toIndex)
        {
            return; // No-op
        }

        var slide = profile.Slides[fromIndex];
        profile.Slides.RemoveAt(fromIndex);
        profile.Slides.Insert(toIndex, slide);
        SaveProfile(profile, profileName);
    }

    /// <summary>
    /// Gets a panel parameter value.
    /// </summary>
    public string? GetPanelParameter(string? profileName, int index, string paramName)
    {
        var profile = LoadProfile(profileName);

        if (index < 0 || index >= profile.Slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Index {index} is out of range. Valid range: 0-{profile.Slides.Count - 1}");
        }

        var slide = profile.Slides[index];

        return paramName.ToLowerInvariant() switch
        {
            "panel" => slide.Panel,
            "type" => slide.Type,
            "source" => slide.Source,
            "duration" => slide.Duration?.ToString(),
            "update_interval" or "updateinterval" or "interval" => slide.UpdateInterval?.ToString(),
            "background" => slide.Background,
            "transition" => slide.Transition,
            "transition_duration" or "transitionduration" => slide.TransitionDurationMs?.ToString(),
            _ => throw new ArgumentException($"Unknown parameter: {paramName}")
        };
    }

    /// <summary>
    /// Sets a panel parameter value. Empty value means delete the parameter.
    /// </summary>
    public void SetPanelParameter(string? profileName, int index, string paramName, string? value)
    {
        var profile = LoadProfile(profileName);

        if (index < 0 || index >= profile.Slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Index {index} is out of range. Valid range: 0-{profile.Slides.Count - 1}");
        }

        var slide = profile.Slides[index];
        var normalizedName = paramName.ToLowerInvariant();

        // Empty value means clear/delete the parameter
        var isDelete = string.IsNullOrEmpty(value);

        switch (normalizedName)
        {
            case "panel":
                slide.Panel = isDelete ? null : value;
                break;
            case "type":
                slide.Type = isDelete ? null : value;
                break;
            case "source":
                slide.Source = isDelete ? null : value;
                break;
            case "duration":
                slide.Duration = isDelete ? null : int.TryParse(value, out var d) ? d : null;
                break;
            case "update_interval" or "updateinterval" or "interval":
                slide.UpdateInterval = isDelete ? null : int.TryParse(value, out var u) ? u : null;
                break;
            case "background":
                slide.Background = isDelete ? null : value;
                break;
            case "transition":
                slide.Transition = isDelete ? null : value;
                break;
            case "transition_duration" or "transitionduration":
                slide.TransitionDurationMs = isDelete ? null : int.TryParse(value, out var t) ? t : null;
                break;
            default:
                throw new ArgumentException($"Unknown parameter: {paramName}");
        }

        SaveProfile(profile, profileName);
    }

    /// <summary>
    /// Clears all custom parameters for a panel, resetting to defaults.
    /// </summary>
    public void ClearPanelParameters(string? profileName, int index)
    {
        var profile = LoadProfile(profileName);

        if (index < 0 || index >= profile.Slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Index {index} is out of range. Valid range: 0-{profile.Slides.Count - 1}");
        }

        var slide = profile.Slides[index];
        var panelType = slide.Panel ?? slide.Source; // Preserve panel type

        // Reset to minimal state
        slide.Type = null;
        slide.Source = null;
        slide.Duration = null;
        slide.UpdateInterval = null;
        slide.Background = null;
        slide.Transition = null;
        slide.TransitionDurationMs = null;
        slide.Panel = panelType; // Keep the panel type

        SaveProfile(profile, profileName);
    }

    /// <summary>
    /// Sets profile-level default settings.
    /// </summary>
    public void SetDefaults(
        string? profileName,
        string? name = null,
        string? description = null,
        int? defaultDuration = null,
        int? defaultUpdateInterval = null,
        string? defaultTransition = null,
        int? defaultTransitionDuration = null,
        string? defaultPageEffect = null)
    {
        var profile = LoadProfile(profileName);

        if (!string.IsNullOrEmpty(name))
        {
            profile.Name = name;
        }

        if (description != null) // Allow empty string to clear
        {
            profile.Description = string.IsNullOrEmpty(description) ? null : description;
        }

        if (defaultDuration.HasValue)
        {
            profile.DefaultDurationSeconds = defaultDuration.Value;
        }

        if (defaultUpdateInterval.HasValue)
        {
            profile.DefaultUpdateIntervalSeconds = defaultUpdateInterval.Value;
        }

        if (!string.IsNullOrEmpty(defaultTransition))
        {
            profile.DefaultTransition = defaultTransition;
        }

        if (defaultTransitionDuration.HasValue)
        {
            profile.DefaultTransitionDurationMs = defaultTransitionDuration.Value;
        }

        if (!string.IsNullOrEmpty(defaultPageEffect))
        {
            profile.DefaultPageEffect = defaultPageEffect;
        }

        SaveProfile(profile, profileName);
    }

    /// <summary>
    /// Serializes a profile to YAML.
    /// </summary>
    public string ToYaml(DisplayProfile profile)
    {
        return _serializer.Serialize(profile);
    }

    /// <summary>
    /// Serializes a profile to JSON.
    /// </summary>
    public string ToJson(DisplayProfile profile)
    {
        return System.Text.Json.JsonSerializer.Serialize(profile, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
        });
    }
}
