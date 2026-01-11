using System.Text.Json;
using System.Text.Json.Serialization;

namespace LCDPossible.Core.Plugins;

/// <summary>
/// Plugin manifest loaded from plugin.json.
/// </summary>
public sealed class PluginMetadata
{
    /// <summary>
    /// Unique plugin identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable plugin name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Plugin version string.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Plugin author.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Plugin description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Minimum SDK version required.
    /// </summary>
    [JsonPropertyName("minimumSdkVersion")]
    public string MinimumSdkVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Main assembly filename.
    /// </summary>
    [JsonPropertyName("assemblyName")]
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// Panel types provided by this plugin.
    /// </summary>
    [JsonPropertyName("panelTypes")]
    public List<PluginPanelTypeMetadata> PanelTypes { get; set; } = [];

    /// <summary>
    /// Parses the version string.
    /// </summary>
    public Version GetVersion()
    {
        return System.Version.TryParse(Version, out var v) ? v : new Version(1, 0, 0);
    }

    /// <summary>
    /// Parses the minimum SDK version string.
    /// </summary>
    public Version GetMinimumSdkVersion()
    {
        return System.Version.TryParse(MinimumSdkVersion, out var v) ? v : new Version(1, 0, 0);
    }

    /// <summary>
    /// Loads plugin metadata from a plugin.json file.
    /// </summary>
    public static PluginMetadata? LoadFromFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PluginMetadata>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Panel type metadata from plugin.json.
/// </summary>
public sealed class PluginPanelTypeMetadata
{
    /// <summary>
    /// Panel type identifier.
    /// </summary>
    [JsonPropertyName("typeId")]
    public string TypeId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Description of the panel.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Prefix pattern for parameterized panels (e.g., "video:").
    /// </summary>
    [JsonPropertyName("prefixPattern")]
    public string? PrefixPattern { get; set; }

    /// <summary>
    /// Whether the panel shows live data.
    /// </summary>
    [JsonPropertyName("isLive")]
    public bool IsLive { get; set; }

    /// <summary>
    /// Whether the panel manages its own animation.
    /// </summary>
    [JsonPropertyName("isAnimated")]
    public bool IsAnimated { get; set; }

    /// <summary>
    /// Category for grouping.
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    /// <summary>
    /// Dependencies (for documentation).
    /// </summary>
    [JsonPropertyName("dependencies")]
    public List<string>? Dependencies { get; set; }
}
