using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;
using LCDPossible.Plugins.Images.Panels;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Plugins.Images;

/// <summary>
/// Images plugin providing animated GIF and image sequence panels.
/// </summary>
public sealed class ImagesPlugin : IPanelPlugin
{
    private ILogger? _logger;

    public string PluginId => "lcdpossible.images";
    public string DisplayName => "LCDPossible Image Panels";
    public Version Version => new(1, 0, 0);
    public string Author => "LCDPossible Team";
    public Version MinimumSdkVersion => new(1, 0, 0);

    public IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes { get; } = new Dictionary<string, PanelTypeInfo>
    {
        ["animated-gif"] = new PanelTypeInfo
        {
            TypeId = "animated-gif",
            DisplayName = "Animated GIF",
            Description = "Plays animated GIF files or URLs",
            Category = "Media",
            IsLive = true,
            PrefixPattern = "animated-gif:"
        },
        ["image-sequence"] = new PanelTypeInfo
        {
            TypeId = "image-sequence",
            DisplayName = "Image Sequence",
            Description = "Plays a sequence of numbered images from a folder",
            Category = "Media",
            IsLive = true,
            PrefixPattern = "image-sequence:"
        }
    };

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.CreateLogger("ImagesPlugin");
        _logger.LogInformation("Images plugin initialized");
        return Task.CompletedTask;
    }

    public IDisplayPanel? CreatePanel(string panelTypeId, PanelCreationContext context)
    {
        // Extract path/URL from the panel type (e.g., "animated-gif:/path/to/file.gif")
        var path = ExtractPath(panelTypeId);

        if (string.IsNullOrEmpty(path))
        {
            _logger?.LogWarning("Cannot create {PanelType}: no path specified", panelTypeId);
            return null;
        }

        var typePrefix = panelTypeId.ToLowerInvariant().Split(':')[0];

        return typePrefix switch
        {
            "animated-gif" => new AnimatedGifPanel(path),
            "image-sequence" => CreateImageSequencePanel(path),
            _ => null
        };
    }

    private static string? ExtractPath(string panelTypeId)
    {
        var colonIndex = panelTypeId.IndexOf(':');
        return colonIndex >= 0 && colonIndex < panelTypeId.Length - 1
            ? panelTypeId[(colonIndex + 1)..]
            : null;
    }

    private static ImageSequencePanel CreateImageSequencePanel(string path)
    {
        // Path format: folder_path or folder_path;fps=30
        var parts = path.Split(';', 2);
        var folderPath = parts[0];
        var fps = 30;

        if (parts.Length > 1 && parts[1].StartsWith("fps=", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(parts[1].AsSpan(4), out var parsedFps))
            {
                fps = parsedFps;
            }
        }

        return new ImageSequencePanel(folderPath, 1000 / fps);
    }

    public void Dispose()
    {
        _logger?.LogInformation("Images plugin disposed");
    }
}
