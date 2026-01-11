using LCDPossible.Core.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Images.Panels;

/// <summary>
/// Panel that displays an animated GIF, cycling through frames at the correct timing.
/// </summary>
public sealed class AnimatedGifPanel : IDisplayPanel
{
    private readonly string _gifPath;
    private Image<Rgba32>? _gif;
    private int _currentFrameIndex;
    private DateTime _lastFrameTime;
    private TimeSpan _currentFrameDelay;
    private bool _disposed;

    /// <summary>
    /// Creates a new animated GIF panel.
    /// </summary>
    /// <param name="gifPath">Path to the animated GIF file or URL.</param>
    public AnimatedGifPanel(string gifPath)
    {
        if (string.IsNullOrWhiteSpace(gifPath))
        {
            throw new ArgumentException("GIF path cannot be empty.", nameof(gifPath));
        }

        _gifPath = gifPath;
    }

    public string PanelId => $"animated-gif:{Path.GetFileName(_gifPath)}";
    public string DisplayName => $"Animated GIF: {Path.GetFileName(_gifPath)}";
    public bool IsLive => true;
    public bool IsAnimated => true;

    /// <summary>
    /// Gets the total number of frames in the GIF.
    /// </summary>
    public int FrameCount => _gif?.Frames.Count ?? 0;

    /// <summary>
    /// Gets the current frame index.
    /// </summary>
    public int CurrentFrameIndex => _currentFrameIndex;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_gif != null)
        {
            return;
        }

        // Resolve the GIF source (handles local files and URLs)
        var resolvedPath = await ImageHelper.ResolveToLocalPathAsync(_gifPath, cancellationToken);

        await using var stream = File.OpenRead(resolvedPath);
        _gif = await Image.LoadAsync<Rgba32>(stream, cancellationToken);

        _currentFrameIndex = 0;
        _lastFrameTime = DateTime.UtcNow;
        _currentFrameDelay = GetFrameDelay(0);
    }

    public Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_gif == null || _gif.Frames.Count == 0)
        {
            // Return a blank image if not initialized
            return Task.FromResult(new Image<Rgba32>(width, height));
        }

        // Check if we need to advance to the next frame
        var now = DateTime.UtcNow;
        var elapsed = now - _lastFrameTime;

        if (elapsed >= _currentFrameDelay)
        {
            // Advance to next frame
            _currentFrameIndex = (_currentFrameIndex + 1) % _gif.Frames.Count;
            _lastFrameTime = now;
            _currentFrameDelay = GetFrameDelay(_currentFrameIndex);
        }

        // Clone the current frame and resize if needed
        var frame = _gif.Frames.CloneFrame(_currentFrameIndex);

        if (frame.Width != width || frame.Height != height)
        {
            frame.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            }));

            // If resized image is smaller than target, center it on a black background
            if (frame.Width != width || frame.Height != height)
            {
                var centered = new Image<Rgba32>(width, height, Color.Black);
                var x = (width - frame.Width) / 2;
                var y = (height - frame.Height) / 2;
                centered.Mutate(ctx => ctx.DrawImage(frame, new Point(x, y), 1f));
                frame.Dispose();
                frame = centered;
            }
        }

        return Task.FromResult(frame);
    }

    private TimeSpan GetFrameDelay(int frameIndex)
    {
        if (_gif == null || frameIndex >= _gif.Frames.Count)
        {
            return TimeSpan.FromMilliseconds(100); // Default 100ms
        }

        var frameMetadata = _gif.Frames[frameIndex].Metadata.GetGifMetadata();
        var delay = frameMetadata.FrameDelay;

        // GIF frame delay is in 1/100ths of a second
        // A delay of 0 or very small values should default to ~100ms per GIF spec
        if (delay <= 1)
        {
            delay = 10; // 100ms
        }

        return TimeSpan.FromMilliseconds(delay * 10);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gif?.Dispose();
        _gif = null;
    }
}
