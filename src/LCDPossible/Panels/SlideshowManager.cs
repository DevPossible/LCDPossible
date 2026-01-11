using LCDPossible.Core.Configuration;
using LCDPossible.Core.Rendering;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Panels;

/// <summary>
/// Manages slideshow playback of panels and images.
/// </summary>
public sealed class SlideshowManager : IDisposable
{
    private readonly PanelFactory _panelFactory;
    private readonly ILogger<SlideshowManager>? _logger;
    private readonly List<SlideshowItem> _items;
    private readonly List<IDisplayPanel> _panels = [];
    private readonly Dictionary<string, Image<Rgba32>> _imageCache = [];
    private readonly Dictionary<string, (Image<Rgba32> Frame, DateTime LastUpdate)> _panelFrameCache = [];

    private int _currentIndex;
    private DateTime _slideStartTime;
    private bool _initialized;
    private bool _disposed;

    public SlideshowManager(
        PanelFactory panelFactory,
        List<SlideshowItem> items,
        ILogger<SlideshowManager>? logger = null)
    {
        _panelFactory = panelFactory ?? throw new ArgumentNullException(nameof(panelFactory));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _logger = logger;
    }

    /// <summary>
    /// Gets the current slideshow item.
    /// </summary>
    public SlideshowItem? CurrentItem => _items.Count > 0 && _currentIndex < _items.Count
        ? _items[_currentIndex]
        : null;

    /// <summary>
    /// Initializes all panels in the slideshow.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        _logger?.LogInformation("Initializing slideshow with {Count} items", _items.Count);

        foreach (var item in _items)
        {
            if (item.Type == "panel")
            {
                var panel = _panelFactory.CreatePanel(item.Source);
                if (panel != null)
                {
                    await panel.InitializeAsync(cancellationToken);
                    _panels.Add(panel);
                    _logger?.LogDebug("Initialized panel: {PanelId}", panel.PanelId);
                }
                else
                {
                    _logger?.LogWarning("Failed to create panel: {Source}", item.Source);
                }
            }
            else if (item.Type == "image" && !string.IsNullOrEmpty(item.Source))
            {
                if (File.Exists(item.Source) && !_imageCache.ContainsKey(item.Source))
                {
                    try
                    {
                        _imageCache[item.Source] = Image.Load<Rgba32>(item.Source);
                        _logger?.LogDebug("Loaded image: {Path}", item.Source);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to load image: {Path}", item.Source);
                    }
                }
            }
        }

        _slideStartTime = DateTime.UtcNow;
        _initialized = true;
    }

    /// <summary>
    /// Renders the current frame, advancing slides as needed.
    /// </summary>
    public async Task<Image<Rgba32>?> RenderCurrentFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        if (_items.Count == 0)
        {
            return null;
        }

        // Check if we need to advance to the next slide
        CheckSlideTransition();

        var currentItem = _items[_currentIndex];

        if (currentItem.Type == "image")
        {
            return RenderImage(currentItem.Source, width, height);
        }
        else // panel
        {
            return await RenderPanelAsync(currentItem, width, height, cancellationToken);
        }
    }

    private void CheckSlideTransition()
    {
        var currentItem = _items[_currentIndex];
        var elapsed = DateTime.UtcNow - _slideStartTime;

        if (elapsed.TotalSeconds >= currentItem.DurationSeconds)
        {
            _currentIndex = (_currentIndex + 1) % _items.Count;
            _slideStartTime = DateTime.UtcNow;
            _logger?.LogDebug("Advancing to slide {Index}: {Source}", _currentIndex, _items[_currentIndex].Source);
        }
    }

    private Image<Rgba32>? RenderImage(string imagePath, int width, int height)
    {
        if (!_imageCache.TryGetValue(imagePath, out var sourceImage))
        {
            return null;
        }

        // Clone and resize to target dimensions
        var result = sourceImage.Clone();
        if (result.Width != width || result.Height != height)
        {
            result.Mutate(ctx => ctx.Resize(width, height));
        }

        return result;
    }

    private async Task<Image<Rgba32>?> RenderPanelAsync(SlideshowItem item, int width, int height, CancellationToken cancellationToken)
    {
        // Find the panel for this item
        // For standard panels (cpu-info, etc.), PanelId matches Source directly
        // For media panels (animated-gif:path, video:url, etc.), we need to match by index since
        // the PanelId is simplified but the Source contains the full path/URL
        var itemIndex = _items.Where(i => i.Type == "panel").ToList().IndexOf(item);
        var panel = itemIndex >= 0 && itemIndex < _panels.Count ? _panels[itemIndex] : null;

        // Fallback: try exact match for standard panels
        if (panel == null)
        {
            panel = _panels.FirstOrDefault(p =>
                p.PanelId.Equals(item.Source, StringComparison.OrdinalIgnoreCase));
        }

        if (panel == null)
        {
            return null;
        }

        // For animated panels (GIF, video, image sequence), don't cache - they manage their own timing
        if (panel.IsAnimated)
        {
            return await panel.RenderFrameAsync(width, height, cancellationToken);
        }

        var cacheKey = $"{item.Source}_{width}x{height}";
        var now = DateTime.UtcNow;
        var updateInterval = TimeSpan.FromSeconds(item.UpdateIntervalSeconds);

        // Check if we have a cached frame that's still valid
        if (_panelFrameCache.TryGetValue(cacheKey, out var cached))
        {
            var elapsed = now - cached.LastUpdate;
            if (elapsed < updateInterval)
            {
                // Return a clone of the cached frame
                return cached.Frame.Clone();
            }
        }

        // Render a new frame
        var frame = await panel.RenderFrameAsync(width, height, cancellationToken);

        // Apply background image if specified
        if (!string.IsNullOrEmpty(item.BackgroundImage) && File.Exists(item.BackgroundImage))
        {
            frame = ApplyBackground(frame, item.BackgroundImage, width, height);
        }

        // Dispose old cached frame and cache the new one
        if (_panelFrameCache.TryGetValue(cacheKey, out var oldCached))
        {
            oldCached.Frame.Dispose();
        }
        _panelFrameCache[cacheKey] = (frame.Clone(), now);

        return frame;
    }

    private Image<Rgba32> ApplyBackground(Image<Rgba32> frame, string backgroundPath, int width, int height)
    {
        try
        {
            // Load or get cached background
            if (!_imageCache.TryGetValue(backgroundPath, out var bgImage))
            {
                bgImage = Image.Load<Rgba32>(backgroundPath);
                _imageCache[backgroundPath] = bgImage;
            }

            var result = bgImage.Clone();
            if (result.Width != width || result.Height != height)
            {
                result.Mutate(ctx => ctx.Resize(width, height));
            }

            // Draw the panel frame on top with some transparency handling
            result.Mutate(ctx => ctx.DrawImage(frame, 1f));
            frame.Dispose();

            return result;
        }
        catch
        {
            // Return original frame if background fails
            return frame;
        }
    }

    /// <summary>
    /// Advances to the next slide immediately.
    /// </summary>
    public void NextSlide()
    {
        if (_items.Count > 0)
        {
            _currentIndex = (_currentIndex + 1) % _items.Count;
            _slideStartTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Goes back to the previous slide.
    /// </summary>
    public void PreviousSlide()
    {
        if (_items.Count > 0)
        {
            _currentIndex = (_currentIndex - 1 + _items.Count) % _items.Count;
            _slideStartTime = DateTime.UtcNow;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var panel in _panels)
        {
            panel.Dispose();
        }
        _panels.Clear();

        foreach (var image in _imageCache.Values)
        {
            image.Dispose();
        }
        _imageCache.Clear();

        foreach (var cached in _panelFrameCache.Values)
        {
            cached.Frame.Dispose();
        }
        _panelFrameCache.Clear();
    }
}
