using LCDPossible.Core.Configuration;
using LCDPossible.Core.Rendering;
using LCDPossible.Core.Transitions;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
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
    private readonly HashSet<int> _failedPanelIndices = [];
    private readonly Dictionary<int, (Image<Rgba32> ErrorFrame, string ErrorMessage)> _errorFrameCache = [];

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
                var panel = _panelFactory.CreatePanel(item.Source, item.Settings);
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

            // Clear error state for the panel we're leaving
            ClearPanelErrorState(previousIndex);

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
        var panelIndex = _items.IndexOf(item);
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

        // If this panel has previously failed, return the cached error frame
        // This prevents continuous retry attempts until the slide changes
        if (_failedPanelIndices.Contains(panelIndex))
        {
            if (_errorFrameCache.TryGetValue(panelIndex, out var errorCached))
            {
                return errorCached.ErrorFrame.Clone();
            }
        }

        try
        {
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
        catch (OperationCanceledException)
        {
            // Don't treat cancellation as an error
            throw;
        }
        catch (Exception ex)
        {
            // Log the error
            _logger?.LogError(ex, "Panel '{PanelId}' failed to render: {Message}", panel.PanelId, ex.Message);

            // Mark this panel as failed to stop further update attempts
            _failedPanelIndices.Add(panelIndex);

            // Generate and cache the error page
            var errorFrame = GenerateErrorPage(width, height, panel.PanelId, ex.Message);
            _errorFrameCache[panelIndex] = (errorFrame, ex.Message);

            _logger?.LogWarning("Panel '{PanelId}' marked as failed - displaying error page until next slide", panel.PanelId);

            return errorFrame.Clone();
        }
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
    /// Generates an error page image for display when a panel fails.
    /// </summary>
    private Image<Rgba32> GenerateErrorPage(int width, int height, string panelName, string errorMessage)
    {
        var image = new Image<Rgba32>(width, height);

        // Dark red gradient background
        image.Mutate(ctx =>
        {
            ctx.BackgroundColor(new Rgba32(40, 10, 10));
        });

        // Try to load system font for error text
        Font? titleFont = null;
        Font? messageFont = null;
        Font? hintFont = null;

        try
        {
            if (SystemFonts.TryGet("Segoe UI", out var family) ||
                SystemFonts.TryGet("Arial", out family) ||
                SystemFonts.TryGet("DejaVu Sans", out family))
            {
                titleFont = family.CreateFont(28, FontStyle.Bold);
                messageFont = family.CreateFont(18, FontStyle.Regular);
                hintFont = family.CreateFont(14, FontStyle.Italic);
            }
        }
        catch
        {
            // Font loading failed, we'll render without text
        }

        if (titleFont != null && messageFont != null && hintFont != null)
        {
            var errorColor = new Rgba32(255, 100, 100);
            var textColor = new Rgba32(220, 220, 220);
            var hintColor = new Rgba32(150, 150, 150);

            image.Mutate(ctx =>
            {
                var y = height * 0.25f;

                // Error icon (simple X)
                var centerX = width / 2f;
                ctx.DrawLine(errorColor, 4f, new PointF(centerX - 30, y - 30), new PointF(centerX + 30, y + 30));
                ctx.DrawLine(errorColor, 4f, new PointF(centerX + 30, y - 30), new PointF(centerX - 30, y + 30));

                y += 60;

                // Title
                var title = "Panel Error";
                var titleOptions = new RichTextOptions(titleFont)
                {
                    Origin = new PointF(centerX, y),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                ctx.DrawText(titleOptions, title, errorColor);

                y += 50;

                // Panel name
                var panelText = $"Panel: {panelName}";
                var panelOptions = new RichTextOptions(messageFont)
                {
                    Origin = new PointF(centerX, y),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                ctx.DrawText(panelOptions, panelText, textColor);

                y += 35;

                // Error message (truncate if too long)
                var displayError = errorMessage.Length > 80
                    ? errorMessage[..77] + "..."
                    : errorMessage;
                var errorOptions = new RichTextOptions(messageFont)
                {
                    Origin = new PointF(centerX, y),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    WrappingLength = width - 40
                };
                ctx.DrawText(errorOptions, displayError, textColor);

                y += 60;

                // Hint
                var hint = "Waiting for next panel...";
                var hintOptions = new RichTextOptions(hintFont)
                {
                    Origin = new PointF(centerX, y),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                ctx.DrawText(hintOptions, hint, hintColor);
            });
        }

        return image;
    }

    /// <summary>
    /// Clears the error state for a panel index when transitioning away.
    /// </summary>
    private void ClearPanelErrorState(int panelIndex)
    {
        if (_failedPanelIndices.Remove(panelIndex))
        {
            if (_errorFrameCache.TryGetValue(panelIndex, out var cached))
            {
                cached.ErrorFrame.Dispose();
                _errorFrameCache.Remove(panelIndex);
            }
            _logger?.LogDebug("Cleared error state for panel index {Index}", panelIndex);
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

        foreach (var cached in _errorFrameCache.Values)
        {
            cached.ErrorFrame.Dispose();
        }
        _errorFrameCache.Clear();
        _failedPanelIndices.Clear();
    }
}
