using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LCDPossible.Core.Configuration;

/// <summary>
/// Loads display profiles from user and system configuration locations.
/// Uses PlatformPaths for cross-platform path resolution.
/// </summary>
public sealed class ProfileLoader
{
    /// <summary>
    /// Default profile filename.
    /// </summary>
    public const string DefaultProfileFileName = "profile.yaml";

    /// <summary>
    /// Alternative profile filename (legacy).
    /// </summary>
    public const string AlternativeProfileFileName = "display-profile.yaml";

    private readonly ILogger<ProfileLoader>? _logger;
    private readonly IDeserializer _deserializer;
    private readonly Func<DisplayProfile>? _defaultProfileFactory;

    /// <summary>
    /// Creates a new ProfileLoader with optional custom default profile factory.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    /// <param name="defaultProfileFactory">Optional factory to create default profile when no file is found.</param>
    public ProfileLoader(ILogger<ProfileLoader>? logger = null, Func<DisplayProfile>? defaultProfileFactory = null)
    {
        _logger = logger;
        _defaultProfileFactory = defaultProfileFactory;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Gets the user data directory for LCDPossible.
    /// </summary>
    public static string GetUserDataDirectory() => PlatformPaths.GetUserDataDirectory();

    /// <summary>
    /// Gets the primary profile path.
    /// </summary>
    public static string GetProfilePath() => PlatformPaths.GetProfilePath();

    /// <summary>
    /// Gets all possible profile file paths in order of priority.
    /// User directory first, then app directory, then current directory.
    /// </summary>
    public static IEnumerable<string> GetProfileSearchPaths()
    {
        // User data directory (highest priority)
        yield return PlatformPaths.GetProfilePath();

        // Also check for alternative filename in user directory
        var userDir = PlatformPaths.GetUserDataDirectory();
        yield return Path.Combine(userDir, AlternativeProfileFileName);

        // Application directory
        yield return Path.Combine(AppContext.BaseDirectory, DefaultProfileFileName);
        yield return Path.Combine(AppContext.BaseDirectory, AlternativeProfileFileName);

        // Current working directory (lowest priority, for development)
        var cwd = Directory.GetCurrentDirectory();
        if (!cwd.Equals(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(cwd, DefaultProfileFileName);
            yield return Path.Combine(cwd, AlternativeProfileFileName);
        }
    }

    /// <summary>
    /// Loads the display profile from the first available location.
    /// Returns the default profile if no file is found.
    /// </summary>
    public DisplayProfile LoadProfile() => LoadProfileWithPath().Profile;

    /// <summary>
    /// Loads the display profile and returns both the profile and the path it was loaded from.
    /// Returns the default profile with null path if no file is found.
    /// </summary>
    public (DisplayProfile Profile, string? Path) LoadProfileWithPath()
    {
        _logger?.LogDebug("Searching for profile in {Count} locations...", GetProfileSearchPaths().Count());

        foreach (var path in GetProfileSearchPaths())
        {
            var exists = File.Exists(path);
            _logger?.LogDebug("  Checking: {Path} - {Status}", path, exists ? "EXISTS" : "not found");

            if (exists)
            {
                try
                {
                    _logger?.LogInformation("Loading display profile from: {Path}", path);
                    var yaml = File.ReadAllText(path);
                    var profile = _deserializer.Deserialize<DisplayProfile>(yaml);

                    if (profile != null)
                    {
                        _logger?.LogInformation("Loaded profile '{Name}' with {Count} slides",
                            profile.Name, profile.Slides.Count);
                        return (profile, path);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load profile from {Path}, trying next location", path);
                }
            }
        }

        _logger?.LogInformation("No profile file found, using default profile");
        var defaultProfile = _defaultProfileFactory?.Invoke() ?? DisplayProfile.CreateDefault();
        return (defaultProfile, null);
    }

    /// <summary>
    /// Loads the display profile from a specific file path.
    /// </summary>
    public DisplayProfile LoadProfileFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Profile file not found: {filePath}", filePath);
        }

        _logger?.LogInformation("Loading display profile from: {Path}", filePath);
        var yaml = File.ReadAllText(filePath);
        var profile = _deserializer.Deserialize<DisplayProfile>(yaml);

        if (profile == null)
        {
            throw new InvalidOperationException($"Failed to parse profile from: {filePath}");
        }

        _logger?.LogInformation("Loaded profile '{Name}' with {Count} slides",
            profile.Name, profile.Slides.Count);
        return profile;
    }

    /// <summary>
    /// Saves a profile to the user data directory.
    /// </summary>
    public void SaveProfile(DisplayProfile profile, string? fileName = null)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var userDir = PlatformPaths.GetUserDataDirectory();
        Directory.CreateDirectory(userDir);

        var filePath = Path.Combine(userDir, fileName ?? DefaultProfileFileName);
        var yaml = serializer.Serialize(profile);
        File.WriteAllText(filePath, yaml);

        _logger?.LogInformation("Saved profile to: {Path}", filePath);
    }

    /// <summary>
    /// Generates a sample profile YAML string.
    /// </summary>
    public static string GenerateSampleProfileYaml()
    {
        var profile = new DisplayProfile
        {
            Name = "Sample Profile",
            Description = "Example display profile demonstrating all options",
            DefaultUpdateIntervalSeconds = 5,
            DefaultDurationSeconds = 15,
            Colors = new ColorScheme
            {
                // Customize any colors here - these are the defaults
                Background = "#0F0F19",
                BackgroundSecondary = "#282832",
                BarBackground = "#282832",
                BarBorder = "#505064",
                TextPrimary = "#FFFFFF",
                TextSecondary = "#B4B4C8",
                TextMuted = "#6E6E82",
                Accent = "#0096FF",
                AccentSecondary = "#00D4AA",
                Success = "#32C864",
                Warning = "#FFB400",
                Critical = "#FF3232",
                Info = "#00AAFF",
                UsageLow = "#32C864",
                UsageMedium = "#0096FF",
                UsageHigh = "#FFB400",
                UsageCritical = "#FF3232",
                TempCool = "#32C864",
                TempWarm = "#FFB400",
                TempHot = "#FF3232",
                ChartColors = ["#0096FF", "#00D4AA", "#FFB400", "#FF6B9D", "#A855F7", "#32C864"]
            },
            Slides =
            [
                new SlideDefinition
                {
                    Panel = "basic-info"
                },
                new SlideDefinition
                {
                    Panel = "cpu-usage-graphic",
                    UpdateInterval = 2
                },
                new SlideDefinition
                {
                    Panel = "gpu-usage-graphic"
                },
                new SlideDefinition
                {
                    Panel = "ram-usage-graphic"
                },
                new SlideDefinition
                {
                    Type = "image",
                    Source = "/path/to/custom-image.png",
                    Duration = 5
                },
                new SlideDefinition
                {
                    Panel = "cpu-usage-text",
                    Background = "/path/to/background.png"
                }
            ]
        };

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        return serializer.Serialize(profile);
    }
}
