using LCDPossible.Core.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LCDPossible.FunctionalTests.Helpers;

/// <summary>
/// Helper class for working with profile YAML files in tests.
/// </summary>
public static class ProfileHelper
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Parses a YAML profile file content into a DisplayProfile object.
    /// </summary>
    public static DisplayProfile ParseProfile(string yaml)
    {
        return Deserializer.Deserialize<DisplayProfile>(yaml)
            ?? throw new InvalidOperationException("Failed to parse profile YAML");
    }

    /// <summary>
    /// Reads and parses a profile file.
    /// </summary>
    public static DisplayProfile ReadProfile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Profile not found: {path}");
        }

        var yaml = File.ReadAllText(path);
        return ParseProfile(yaml);
    }

    /// <summary>
    /// Gets the panel type at the specified index.
    /// </summary>
    public static string? GetPanelType(DisplayProfile profile, int index)
    {
        if (index < 0 || index >= profile.Slides.Count)
        {
            return null;
        }

        return profile.Slides[index].Panel ?? profile.Slides[index].Source;
    }
}
