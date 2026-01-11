using LCDPossible.Core.Configuration;
using LCDPossible.Core.Rendering;
using LCDPossible.Core.Transitions;
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

    // Transition state
    private Image<Rgba32>? _previousFrame;
    private DateTime _transitionStartTime;
    private bool _inTransition;
    private TransitionType _currentTransitionType;

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
    /// Gets the current slide index.
    /// </summary>
    public int CurrentIndex => _currentIndex;

    /// <summary>
    /// Gets the total number of slides.
    /// </summary>
    public int TotalSlides => _items.Count;

    /// <summary>
    /// Returns true when there's only one slide - no cycling or transitions needed.
    /// </summary>
    public bool IsSinglePanelMode => _items.Count <= 1;

    /// <summary>
    /// Gets the current panel ID (or source for images).
    /// </summary>
    public string? CurrentPanelId => CurrentItem?.Source;

    /// <summary>
    /// Gets the time remaining on the current slide.
    /// </summary>
    public TimeSpan TimeRemaining
    {
        get
        {
            if (CurrentItem == null)
                return TimeSpan.Zero;

            var elapsed = DateTime.UtcNow - _slideStartTime;
            var remaining = TimeSpan.FromSeconds(CurrentItem.DurationSeconds) - elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

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

        if (IsSinglePanelMode)
        {
            _logger?.LogDebug("Single-panel mode: transitions disabled, no cycling");
        }
    }

    /// <summary>
    /// Renders the current frame, advancing slides as needed.
    /// Applies transition effects during slide changes.
    /// </summary>
    public async Task<Image<Rgba32>?> RenderCurrentFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        if (_items.Count == 0)
        {
            return null;
        }

        var currentItem = _items[_currentIndex];

        // Single-panel optimization: skip all transition logic
        if (IsSinglePanelMode)
        {
            if (currentItem.Type == "image")
            {
                return RenderImage(currentItem.Source, width, height);
            }
            return await RenderPanelAsync(currentItem, width, height, cancellationToken);
        }

        // Multi-panel mode: check if we need to advance to the next slide
        var (transitioned, previousIndex) = CheckSlideTransition();

        // Re-fetch current item in case index changed
        currentItem = _items[_currentIndex];

        // Render the current (next) frame
        Image<Rgba32>? nextFrame;
        if (currentItem.Type == "image")
        {
            nextFrame = RenderImage(currentItem.Source, width, height);
        }
        else
        {
            nextFrame = await RenderPanelAsync(currentItem, width, height, cancellationToken);
        }

        if (nextFrame == null)
        {
            return null;
        }

        // Handle transition
        if (_inTransition)
        {
            var elapsed = DateTime.UtcNow - _transitionStartTime;
            var transitionDuration = TimeSpan.FromMilliseconds(currentItem.TransitionDurationMs);
            var progress = (float)(elapsed.TotalMilliseconds / transitionDuration.TotalMilliseconds);

            if (progress >= 1f)
            {
                // Transition complete
                _inTransition = false;
                UpdatePreviousFrame(nextFrame);
                return nextFrame;
            }

            // Apply easing for smoother transitions
            var easedProgress = TransitionEngine.EaseInOut(progress);

            // Apply transition effect
            var blendedFrame = TransitionEngine.Apply(
                _currentTransitionType,
                _previousFrame,
                nextFrame,
                easedProgress);

            // Dispose the rendered next frame since we're returning the blended one
            nextFrame.Dispose();
            return blendedFrame;
        }

        // No transition - update previous frame and return
        UpdatePreviousFrame(nextFrame);
        return nextFrame;
    }

    /// <summary>
    /// Updates the previous frame buffer for the next transition.
    /// </summary>
    private void UpdatePreviousFrame(Image<Rgba32> currentFrame)
    {
        _previousFrame?.Dispose();
        _previousFrame = currentFrame.Clone();
    }

    /// <summary>
    /// Checks if slide should advance and initiates transition if needed.
    /// </summary>
    /// <returns>Tuple of (didTransition, previousIndex).</returns>
    private (bool Transitioned, int PreviousIndex) CheckSlideTransition()
    {
        // Don't check for slide change while in transition
        if (_inTransition)
        {
            return (false, _currentIndex);
        }

        var currentItem = _items[_currentIndex];
        var elapsed = DateTime.UtcNow - _slideStartTime;

        if (elapsed.TotalSeconds >= currentItem.DurationSeconds)
        {
            var previousIndex = _currentIndex;
            _currentIndex = (_currentIndex + 1) % _items.Count;
            _slideStartTime = DateTime.UtcNow;

            // Start transition for the new slide
            var newItem = _items[_currentIndex];
            var transitionType = newItem.Transition;

            // Resolve random to an actual transition
            if (transitionType == TransitionType.Random)
            {
                _currentTransitionType = transitionType.Resolve();
            }
            else
            {
                _currentTransitionType = transitionType;
            }

            // Only start transition if not "none"
            if (_currentTransitionType != TransitionType.None)
            {
                _inTransition = true;
                _transitionStartTime = DateTime.UtcNow;
                _logger?.LogDebug("Starting {Transition} transition to slide {Index}: {Source}",
                    _currentTransitionType, _currentIndex, newItem.Source);
            }
            else
            {
                _logger?.LogDebug("Advancing to slide {Index}: {Source} (no transition)",
                    _currentIndex, newItem.Source);
            }

            return (true, previousIndex);
        }

        return (false, _currentIndex);
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
    /// Advances to the next slide immediately with transition.
    /// </summary>
    public void NextSlide()
    {
        if (_items.Count > 0)
        {
            _currentIndex = (_currentIndex + 1) % _items.Count;
            _slideStartTime = DateTime.UtcNow;
            StartManualTransition();
        }
    }

    /// <summary>
    /// Goes back to the previous slide with transition.
    /// </summary>
    public void PreviousSlide()
    {
        if (_items.Count > 0)
        {
            _currentIndex = (_currentIndex - 1 + _items.Count) % _items.Count;
            _slideStartTime = DateTime.UtcNow;
            StartManualTransition();
        }
    }

    /// <summary>
    /// Starts a transition for manual slide changes.
    /// </summary>
    private void StartManualTransition()
    {
        var newItem = _items[_currentIndex];
        var transitionType = newItem.Transition;

        if (transitionType == TransitionType.Random)
        {
            _currentTransitionType = transitionType.Resolve();
        }
        else
        {
            _currentTransitionType = transitionType;
        }

        if (_currentTransitionType != TransitionType.None)
        {
            _inTransition = true;
            _transitionStartTime = DateTime.UtcNow;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Dispose transition state
        _previousFrame?.Dispose();
        _previousFrame = null;

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
