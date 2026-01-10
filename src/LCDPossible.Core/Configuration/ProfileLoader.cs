using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LCDPossible.Core.Configuration;

/// <summary>
/// Loads display profiles from system-wide configuration locations.
/// </summary>
public sealed class ProfileLoader
{
    /// <summary>
    /// Default profile filename.
    /// </summary>
    public const string DefaultProfileFileName = "display-profile.yaml";

    /// <summary>
    /// Alternative profile filename.
    /// </summary>
    public const string AlternativeProfileFileName = "profile.yaml";

    private readonly ILogger<ProfileLoader>? _logger;
    private readonly IDeserializer _deserializer;

    public ProfileLoader(ILogger<ProfileLoader>? logger = null)
    {
        _logger = logger;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Gets the system-wide configuration directory for the current platform.
    /// </summary>
    public static string GetSystemConfigDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: C:\ProgramData\LCDPossible
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(programData, "LCDPossible");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux: /etc/lcdpossible
            return "/etc/lcdpossible";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: /Library/Application Support/LCDPossible
            return "/Library/Application Support/LCDPossible";
        }
        else
        {
            // Fallback to current directory
            return Path.Combine(AppContext.BaseDirectory, "config");
        }
    }

    /// <summary>
    /// Gets all possible profile file paths in order of priority.
    /// </summary>
    public static IEnumerable<string> GetProfileSearchPaths()
    {
        var systemDir = GetSystemConfigDirectory();

        // System-wide locations (highest priority)
        yield return Path.Combine(systemDir, DefaultProfileFileName);
        yield return Path.Combine(systemDir, AlternativeProfileFileName);

        // Application directory (fallback)
        yield return Path.Combine(AppContext.BaseDirectory, DefaultProfileFileName);
        yield return Path.Combine(AppContext.BaseDirectory, AlternativeProfileFileName);

        // Current working directory (lowest priority)
        yield return Path.Combine(Directory.GetCurrentDirectory(), DefaultProfileFileName);
        yield return Path.Combine(Directory.GetCurrentDirectory(), AlternativeProfileFileName);
    }

    /// <summary>
    /// Loads the display profile from the first available location.
    /// Returns the default profile if no file is found.
    /// </summary>
    public DisplayProfile LoadProfile()
    {
        foreach (var path in GetProfileSearchPaths())
        {
            if (File.Exists(path))
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
                        return profile;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load profile from {Path}, trying next location", path);
                }
            }
        }

        _logger?.LogInformation("No profile file found, using default profile");
        return DisplayProfile.CreateDefault();
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
    /// Saves a profile to the system configuration directory.
    /// </summary>
    public void SaveProfile(DisplayProfile profile, string? fileName = null)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var systemDir = GetSystemConfigDirectory();
        Directory.CreateDirectory(systemDir);

        var filePath = Path.Combine(systemDir, fileName ?? DefaultProfileFileName);
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
