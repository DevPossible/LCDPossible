using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;
using LCDPossible.Plugins.Video.Panels;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Plugins.Video;

/// <summary>
/// Video plugin providing video playback panels.
/// </summary>
public sealed class VideoPlugin : IPanelPlugin
{
    private ILogger? _logger;

    public string PluginId => "lcdpossible.video";
    public string DisplayName => "LCDPossible Video Panels";
    public Version Version => new(1, 0, 0);
    public string Author => "LCDPossible Team";
    public Version MinimumSdkVersion => new(1, 0, 0);

    public IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes { get; } = new Dictionary<string, PanelTypeInfo>
    {
        ["video"] = new PanelTypeInfo
        {
            TypeId = "video",
            DisplayName = "Video",
            Description = "Plays video files, URLs, or YouTube links",
            Category = "Media",
            IsLive = true,
            PrefixPattern = "video:"
        }
    };

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.CreateLogger("VideoPlugin");
        _logger.LogInformation("Video plugin initialized");
        return Task.CompletedTask;
    }

    public IDisplayPanel? CreatePanel(string panelTypeId, PanelCreationContext context)
    {
        // Extract path/URL from the panel type (e.g., "video:/path/to/file.mp4")
        var path = ExtractPath(panelTypeId);

        if (string.IsNullOrEmpty(path))
        {
            _logger?.LogWarning("Cannot create {PanelType}: no path specified", panelTypeId);
            return null;
        }

        return CreateVideoPanel(path);
    }

    private static string? ExtractPath(string panelTypeId)
    {
        var colonIndex = panelTypeId.IndexOf(':');
        return colonIndex >= 0 && colonIndex < panelTypeId.Length - 1
            ? panelTypeId[(colonIndex + 1)..]
            : null;
    }

    private static VideoPanel CreateVideoPanel(string path)
    {
        // Path format: file_path or url or youtube_url
        // Options: path;loop=false;volume=50
        var parts = path.Split(';');
        var videoPath = parts[0];
        var loop = true;
        var volume = 0f;

        foreach (var part in parts.Skip(1))
        {
            if (part.StartsWith("loop=", StringComparison.OrdinalIgnoreCase))
            {
                bool.TryParse(part.AsSpan(5), out loop);
            }
            else if (part.StartsWith("volume=", StringComparison.OrdinalIgnoreCase))
            {
                float.TryParse(part.AsSpan(7), out volume);
            }
        }

        return new VideoPanel(videoPath, loop, volume);
    }

    public void Dispose()
    {
        _logger?.LogInformation("Video plugin disposed");
    }
}
