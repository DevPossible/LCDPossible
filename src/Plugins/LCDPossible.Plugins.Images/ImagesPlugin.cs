using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;
using LCDPossible.Plugins.Images.Panels;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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

    // Test defaults for panels that require parameters
    private static class TestDefaults
    {
        // CC-BY-SA rotating Earth from Wikimedia Commons
        public const string AnimatedGif = "https://upload.wikimedia.org/wikipedia/commons/2/2c/Rotating_earth_%28large%29.gif";

        // Placeholder for image sequence test folder (created on demand)
        public static string? ImageSequence => GetOrCreateTestImageSequenceFolder();

        private static string? _testImageSequenceFolder;

        private static string? GetOrCreateTestImageSequenceFolder()
        {
            if (_testImageSequenceFolder != null && Directory.Exists(_testImageSequenceFolder))
            {
                return _testImageSequenceFolder;
            }

            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "LCDPossible", "test-sequence");
                Directory.CreateDirectory(tempDir);

                // Generate a few simple test frames with different colors
                var testColors = new[]
                {
                    new Rgba32(255, 0, 0),     // Red
                    new Rgba32(0, 255, 0),     // Green
                    new Rgba32(0, 0, 255),     // Blue
                    new Rgba32(255, 255, 0),   // Yellow
                    new Rgba32(255, 0, 255)    // Magenta
                };

                for (var i = 0; i < testColors.Length; i++)
                {
                    var imagePath = Path.Combine(tempDir, $"frame_{i:D4}.png");
                    if (!File.Exists(imagePath))
                    {
                        using var image = new Image<Rgba32>(320, 240, testColors[i]);
                        image.SaveAsPng(imagePath);
                    }
                }

                _testImageSequenceFolder = tempDir;
                return _testImageSequenceFolder;
            }
            catch
            {
                return null;
            }
        }
    }

    public IDisplayPanel? CreatePanel(string panelTypeId, PanelCreationContext context)
    {
        // Extract path/URL from the panel type (e.g., "animated-gif:/path/to/file.gif")
        var path = ExtractPath(panelTypeId);
        var typePrefix = panelTypeId.ToLowerInvariant().Split(':')[0];

        // Use test defaults when no path is specified
        if (string.IsNullOrEmpty(path))
        {
            path = typePrefix switch
            {
                "animated-gif" => TestDefaults.AnimatedGif,
                "image-sequence" => TestDefaults.ImageSequence,
                _ => null
            };

            if (string.IsNullOrEmpty(path))
            {
                _logger?.LogWarning("Cannot create {PanelType}: no path specified and no test default available", panelTypeId);
                return null;
            }

            _logger?.LogInformation("Using test default for {PanelType}: {Path}", panelTypeId, path);
        }

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
