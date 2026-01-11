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

    /// <summary>
    /// Detailed help text explaining how to use the panel.
    /// </summary>
    [JsonPropertyName("helpText")]
    public string? HelpText { get; set; }

    /// <summary>
    /// Example usages of the panel.
    /// </summary>
    [JsonPropertyName("examples")]
    public List<PanelExampleMetadata>? Examples { get; set; }

    /// <summary>
    /// Parameters accepted by the panel (for parameterized panels).
    /// </summary>
    [JsonPropertyName("parameters")]
    public List<PanelParameterMetadata>? Parameters { get; set; }

    /// <summary>
    /// Gets the display identifier (prefix pattern or type ID).
    /// </summary>
    [JsonIgnore]
    public string DisplayId => PrefixPattern ?? TypeId;
}

/// <summary>
/// Example usage metadata for a panel.
/// </summary>
public sealed class PanelExampleMetadata
{
    /// <summary>
    /// The example command or panel string.
    /// </summary>
    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this example does.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Parameter metadata for parameterized panels.
/// </summary>
public sealed class PanelParameterMetadata
{
    /// <summary>
    /// Parameter name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Parameter description.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether the parameter is required.
    /// </summary>
    [JsonPropertyName("required")]
    public bool Required { get; set; }

    /// <summary>
    /// Default value if not specified.
    /// </summary>
    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Example values.
    /// </summary>
    [JsonPropertyName("exampleValues")]
    public List<string>? ExampleValues { get; set; }
}
