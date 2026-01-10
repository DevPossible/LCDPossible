namespace LCDPossible.Core.Configuration;

/// <summary>
/// Root configuration options for LCDPossible.
/// </summary>
public sealed class LcdPossibleOptions
{
    public const string SectionName = "LCDPossible";

    public GeneralOptions General { get; set; } = new();
    public Dictionary<string, DeviceOptions> Devices { get; set; } = [];
    public ProxmoxOptions Proxmox { get; set; } = new();
}

/// <summary>
/// General application settings.
/// </summary>
public sealed class GeneralOptions
{
    /// <summary>
    /// Target frame rate for animated content (default: 30 FPS).
    /// </summary>
    public int TargetFrameRate { get; set; } = 30;

    /// <summary>
    /// Auto-start display on service startup.
    /// </summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// Directory containing theme files.
    /// </summary>
    public string ThemesDirectory { get; set; } = "themes";

    /// <summary>
    /// JPEG encoding quality (1-100, default: 95).
    /// </summary>
    public int JpegQuality { get; set; } = 95;

    /// <summary>
    /// Default update interval for panels in seconds (default: 5).
    /// Panels will only re-render their content at this interval to reduce CPU usage.
    /// </summary>
    public int DefaultPanelUpdateIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Default duration for each panel/slide in a slideshow in seconds (default: 15).
    /// </summary>
    public int DefaultPanelDurationSeconds { get; set; } = 15;
}

/// <summary>
/// Per-device configuration options.
/// </summary>
public sealed class DeviceOptions
{
    /// <summary>
    /// Display mode: "panel", "slideshow", "static", "animation", "clock", "off".
    /// Use "panel" for a single live panel, "slideshow" to cycle through multiple panels/images.
    /// Default is "slideshow" which cycles through the display profile panels.
    /// </summary>
    public string Mode { get; set; } = "slideshow";

    /// <summary>
    /// Screen brightness (0-100).
    /// </summary>
    public int Brightness { get; set; } = 100;

    /// <summary>
    /// Display orientation in degrees (0, 90, 180, 270).
    /// </summary>
    public int Orientation { get; set; } = 0;

    /// <summary>
    /// Path to static image file (for "static" mode).
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// Path to animated GIF or video file (for "animation" mode with file source).
    /// </summary>
    public string? AnimationPath { get; set; }

    /// <summary>
    /// Theme name to use (for themed display modes).
    /// </summary>
    public string? Theme { get; set; }

    /// <summary>
    /// Single panel type to display (for "panel" mode).
    /// Options: cpu-info, cpu-usage-text, cpu-usage-graphic, ram-info, ram-usage-text,
    /// ram-usage-graphic, gpu-info, gpu-usage-text, gpu-usage-graphic, basic-info,
    /// basic-usage-text, os-info, os-notifications, os-status, proxmox-summary, proxmox-vms.
    /// </summary>
    public string? Panel { get; set; }

    /// <summary>
    /// Slideshow configuration (for "slideshow" mode).
    /// Format: "{slidetypeid}|{seconds}|{background},{slidetypeid}|{seconds},..."
    /// - slidetypeid: Panel type or "image" for static images
    /// - seconds: Duration in seconds
    /// - background: Optional background image path (for panels) or image path (for "image" type)
    ///
    /// Examples:
    /// - "basic-info|10" - Basic info panel for 10 seconds
    /// - "cpu-usage-graphic|15|C:\bg\cpu-bg.png" - CPU panel with background image
    /// - "image|5|C:\pics\logo.png" - Static image for 5 seconds
    /// </summary>
    public string? Slideshow { get; set; }

    /// <summary>
    /// Parses the slideshow configuration string into items.
    /// </summary>
    public List<SlideshowItem> GetSlideshowItems()
    {
        if (string.IsNullOrWhiteSpace(Slideshow))
        {
            return [];
        }

        var items = new List<SlideshowItem>();
        var parts = Slideshow.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var segments = part.Split('|');
            if (segments.Length >= 1)
            {
                var source = segments[0].Trim();
                var duration = segments.Length > 1 && int.TryParse(segments[1], out var d) ? d : 10;
                var param2 = segments.Length > 2 ? segments[2].Trim() : null;

                var item = new SlideshowItem
                {
                    DurationSeconds = duration
                };

                if (source.Equals("image", StringComparison.OrdinalIgnoreCase))
                {
                    // "image|10|C:\path\to\image.png"
                    item.Type = "image";
                    item.Source = param2 ?? string.Empty;
                }
                else
                {
                    // Panel with optional background
                    item.Type = "panel";
                    item.Source = source;
                    item.BackgroundImage = param2;
                }

                items.Add(item);
            }
        }

        return items;
    }
}

/// <summary>
/// A single item in a slideshow - can be a panel or an image.
/// </summary>
public sealed class SlideshowItem
{
    /// <summary>
    /// Type of item: "panel" or "image".
    /// </summary>
    public string Type { get; set; } = "panel";

    /// <summary>
    /// Panel type (if Type is "panel") or image path (if Type is "image").
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// How long to display this item in seconds (default: 15).
    /// </summary>
    public int DurationSeconds { get; set; } = 15;

    /// <summary>
    /// How often to re-render the panel content in seconds (default: 5).
    /// Only applies to panel type items. Images are rendered once.
    /// </summary>
    public int UpdateIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Optional background image path (for panel type only).
    /// </summary>
    public string? BackgroundImage { get; set; }
}

/// <summary>
/// Proxmox VE connection options.
/// </summary>
public sealed class ProxmoxOptions
{
    /// <summary>
    /// Whether Proxmox integration is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Proxmox API URL (e.g., "https://proxmox.local:8006").
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// API token ID (format: "user@realm!tokenid").
    /// </summary>
    public string TokenId { get; set; } = string.Empty;

    /// <summary>
    /// API token secret.
    /// </summary>
    public string TokenSecret { get; set; } = string.Empty;

    /// <summary>
    /// Whether to skip SSL certificate verification (for self-signed certs).
    /// </summary>
    public bool IgnoreSslErrors { get; set; } = false;

    /// <summary>
    /// Polling interval in seconds for fetching metrics.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Whether to show individual VM status.
    /// </summary>
    public bool ShowVms { get; set; } = true;

    /// <summary>
    /// Whether to show individual container status.
    /// </summary>
    public bool ShowContainers { get; set; } = true;

    /// <summary>
    /// Whether to show cluster alerts.
    /// </summary>
    public bool ShowAlerts { get; set; } = true;

    /// <summary>
    /// Maximum number of items to display per category.
    /// </summary>
    public int MaxDisplayItems { get; set; } = 10;
}
