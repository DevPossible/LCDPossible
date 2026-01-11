using System.Runtime.InteropServices;
using LCDPossible.Core.Rendering;
using LibVLCSharp.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Panels;

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
    /// <param name="gifPath">Path to the animated GIF file.</param>
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
        var resolvedPath = await MediaHelper.ResolveToLocalPathAsync(_gifPath, cancellationToken);

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

/// <summary>
/// Panel that displays a sequence of numbered images from a folder, cycling through them.
/// This provides video-like playback without requiring external dependencies.
/// </summary>
public sealed class ImageSequencePanel : IDisplayPanel
{
    private readonly string _folderPath;
    private readonly int _frameRateMs;
    private readonly bool _loop;
    private List<string> _framePaths = [];
    private readonly Dictionary<string, Image<Rgba32>> _frameCache = [];
    private int _currentFrameIndex;
    private DateTime _lastFrameTime;
    private bool _disposed;

    /// <summary>
    /// Creates a new image sequence panel.
    /// </summary>
    /// <param name="folderPath">Path to folder containing numbered image files.</param>
    /// <param name="frameRateMs">Milliseconds per frame (default: 33ms = ~30fps).</param>
    /// <param name="loop">Whether to loop the sequence (default: true).</param>
    public ImageSequencePanel(string folderPath, int frameRateMs = 33, bool loop = true)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("Folder path cannot be empty.", nameof(folderPath));
        }

        _folderPath = folderPath;
        _frameRateMs = Math.Max(1, frameRateMs);
        _loop = loop;
    }

    public string PanelId => $"image-sequence:{Path.GetFileName(_folderPath)}";
    public string DisplayName => $"Image Sequence: {Path.GetFileName(_folderPath)}";
    public bool IsLive => true;
    public bool IsAnimated => true;

    /// <summary>
    /// Gets the total number of frames in the sequence.
    /// </summary>
    public int FrameCount => _framePaths.Count;

    /// <summary>
    /// Gets the current frame index.
    /// </summary>
    public int CurrentFrameIndex => _currentFrameIndex;

    /// <summary>
    /// Gets the frame rate in frames per second.
    /// </summary>
    public double FrameRate => 1000.0 / _frameRateMs;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_framePaths.Count > 0)
        {
            return Task.CompletedTask;
        }

        if (!Directory.Exists(_folderPath))
        {
            throw new DirectoryNotFoundException($"Image sequence folder not found: {_folderPath}");
        }

        // Find all image files and sort them naturally (so frame_2 comes before frame_10)
        var extensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

        _framePaths = Directory.EnumerateFiles(_folderPath)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, new NaturalSortComparer())
            .ToList();

        if (_framePaths.Count == 0)
        {
            throw new InvalidOperationException($"No image files found in folder: {_folderPath}");
        }

        _currentFrameIndex = 0;
        _lastFrameTime = DateTime.UtcNow;

        return Task.CompletedTask;
    }

    public Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_framePaths.Count == 0)
        {
            return Task.FromResult(new Image<Rgba32>(width, height));
        }

        // Check if we need to advance to the next frame
        var now = DateTime.UtcNow;
        var elapsed = now - _lastFrameTime;

        if (elapsed.TotalMilliseconds >= _frameRateMs)
        {
            var nextIndex = _currentFrameIndex + 1;

            if (nextIndex >= _framePaths.Count)
            {
                if (_loop)
                {
                    _currentFrameIndex = 0;
                }
                // If not looping, stay on last frame
            }
            else
            {
                _currentFrameIndex = nextIndex;
            }

            _lastFrameTime = now;
        }

        // Load frame (with caching for recently used frames)
        var framePath = _framePaths[_currentFrameIndex];
        var frame = GetOrLoadFrame(framePath);

        // Clone and resize if needed
        var result = frame.Clone();

        if (result.Width != width || result.Height != height)
        {
            result.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            }));

            // Center on black background if smaller than target
            if (result.Width != width || result.Height != height)
            {
                var centered = new Image<Rgba32>(width, height, Color.Black);
                var x = (width - result.Width) / 2;
                var y = (height - result.Height) / 2;
                centered.Mutate(ctx => ctx.DrawImage(result, new Point(x, y), 1f));
                result.Dispose();
                result = centered;
            }
        }

        return Task.FromResult(result);
    }

    private Image<Rgba32> GetOrLoadFrame(string path)
    {
        if (_frameCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        // Load the frame
        var frame = Image.Load<Rgba32>(path);

        // Limit cache size to prevent memory issues
        const int maxCacheSize = 100;
        if (_frameCache.Count >= maxCacheSize)
        {
            // Remove oldest entries
            var toRemove = _frameCache.Keys.Take(maxCacheSize / 2).ToList();
            foreach (var key in toRemove)
            {
                if (_frameCache.TryGetValue(key, out var old))
                {
                    old.Dispose();
                    _frameCache.Remove(key);
                }
            }
        }

        _frameCache[path] = frame;
        return frame;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var frame in _frameCache.Values)
        {
            frame.Dispose();
        }
        _frameCache.Clear();
        _framePaths.Clear();
    }

    /// <summary>
    /// Natural sort comparer that handles numbered filenames correctly.
    /// Sorts "frame2.png" before "frame10.png".
    /// </summary>
    private sealed class NaturalSortComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var xParts = SplitIntoChunks(Path.GetFileName(x));
            var yParts = SplitIntoChunks(Path.GetFileName(y));

            var minLen = Math.Min(xParts.Count, yParts.Count);

            for (var i = 0; i < minLen; i++)
            {
                var xPart = xParts[i];
                var yPart = yParts[i];

                var xIsNum = long.TryParse(xPart, out var xNum);
                var yIsNum = long.TryParse(yPart, out var yNum);

                int cmp;
                if (xIsNum && yIsNum)
                {
                    cmp = xNum.CompareTo(yNum);
                }
                else
                {
                    cmp = string.Compare(xPart, yPart, StringComparison.OrdinalIgnoreCase);
                }

                if (cmp != 0)
                {
                    return cmp;
                }
            }

            return xParts.Count.CompareTo(yParts.Count);
        }

        private static List<string> SplitIntoChunks(string s)
        {
            var chunks = new List<string>();
            var current = new System.Text.StringBuilder();
            var inNumber = false;

            foreach (var c in s)
            {
                var isDigit = char.IsDigit(c);

                if (current.Length > 0 && isDigit != inNumber)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                }

                current.Append(c);
                inNumber = isDigit;
            }

            if (current.Length > 0)
            {
                chunks.Add(current.ToString());
            }

            return chunks;
        }
    }
}

/// <summary>
/// Panel that plays video files using LibVLCSharp.
/// Supports any format VLC can play (MP4, MKV, AVI, WebM, etc.).
/// </summary>
public sealed class VideoPanel : IDisplayPanel
{
    private readonly string _videoPath;
    private readonly bool _loop;
    private readonly float _volume;

    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private Media? _media;

    private byte[]? _frameBuffer;
    private Image<Rgba32>? _currentFrame;
    private readonly object _frameLock = new();

    private int _videoWidth;
    private int _videoHeight;
    private uint _pitch;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Creates a new video panel.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <param name="loop">Whether to loop the video (default: true).</param>
    /// <param name="volume">Audio volume 0-100 (default: 0 for muted).</param>
    public VideoPanel(string videoPath, bool loop = true, float volume = 0)
    {
        if (string.IsNullOrWhiteSpace(videoPath))
        {
            throw new ArgumentException("Video path cannot be empty.", nameof(videoPath));
        }

        _videoPath = videoPath;
        _loop = loop;
        _volume = Math.Clamp(volume, 0, 100);
    }

    public string PanelId => $"video:{Path.GetFileName(_videoPath)}";
    public string DisplayName => $"Video: {Path.GetFileName(_videoPath)}";
    public bool IsLive => true;
    public bool IsAnimated => true;

    /// <summary>
    /// Gets whether the video is currently playing.
    /// </summary>
    public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;

    /// <summary>
    /// Gets the current playback position (0-1).
    /// </summary>
    public float Position => _mediaPlayer?.Position ?? 0;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        // Resolve the video source (handles local files, URLs, and YouTube)
        var resolvedSource = await MediaHelper.ResolveVideoSourceAsync(_videoPath, cancellationToken);

        // Initialize LibVLC
        LibVLCSharp.Shared.Core.Initialize();

        // Create LibVLC with options for headless rendering
        _libVLC = new LibVLC(
            "--no-audio" + (_volume > 0 ? "" : ""), // Disable audio if volume is 0
            "--no-xlib" // Disable X11 for headless operation
        );

        _mediaPlayer = new MediaPlayer(_libVLC);

        // Set up video format and callbacks for frame capture
        _mediaPlayer.SetVideoFormat("RV32", 1280, 480, 1280 * 4);
        _videoWidth = 1280;
        _videoHeight = 480;
        _pitch = 1280 * 4;
        _frameBuffer = new byte[_videoHeight * _pitch];

        _mediaPlayer.SetVideoCallbacks(
            Lock,
            null, // unlock
            Display
        );

        // Handle end of media for looping
        _mediaPlayer.EndReached += (_, _) =>
        {
            if (_loop && _mediaPlayer != null)
            {
                // Schedule restart on thread pool to avoid deadlock
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        _mediaPlayer?.Stop();
                        _mediaPlayer?.Play();
                    }
                    catch
                    {
                        // Ignore errors during loop restart
                    }
                });
            }
        };

        // Set volume
        if (_volume > 0)
        {
            _mediaPlayer.Volume = (int)_volume;
        }

        // Load and play the video (supports file paths and URLs)
        _media = MediaHelper.IsUrl(resolvedSource)
            ? new Media(_libVLC, new Uri(resolvedSource))
            : new Media(_libVLC, new Uri(Path.GetFullPath(resolvedSource)));

        _mediaPlayer.Media = _media;
        _mediaPlayer.Play();

        _initialized = true;
    }

    private IntPtr Lock(IntPtr opaque, IntPtr planes)
    {
        // Allocate buffer for VLC to write frame data
        if (_frameBuffer != null)
        {
            var handle = GCHandle.Alloc(_frameBuffer, GCHandleType.Pinned);
            Marshal.WriteIntPtr(planes, handle.AddrOfPinnedObject());
            return GCHandle.ToIntPtr(handle);
        }
        return IntPtr.Zero;
    }

    private void Display(IntPtr opaque, IntPtr picture)
    {
        if (picture == IntPtr.Zero || _frameBuffer == null)
        {
            return;
        }

        try
        {
            // Free the pinned handle
            var handle = GCHandle.FromIntPtr(picture);
            if (handle.IsAllocated)
            {
                handle.Free();
            }

            // Convert frame buffer to ImageSharp image
            lock (_frameLock)
            {
                _currentFrame?.Dispose();

                // LibVLC RV32 format is BGRA
                _currentFrame = new Image<Rgba32>(_videoWidth, _videoHeight);

                _currentFrame.ProcessPixelRows(accessor =>
                {
                    for (var y = 0; y < _videoHeight; y++)
                    {
                        var rowSpan = accessor.GetRowSpan(y);
                        var srcOffset = y * (int)_pitch;

                        for (var x = 0; x < _videoWidth; x++)
                        {
                            var pixelOffset = srcOffset + x * 4;
                            // BGRA to RGBA conversion
                            rowSpan[x] = new Rgba32(
                                _frameBuffer![pixelOffset + 2], // R from B position
                                _frameBuffer[pixelOffset + 1], // G
                                _frameBuffer[pixelOffset + 0], // B from R position
                                _frameBuffer[pixelOffset + 3]  // A
                            );
                        }
                    }
                });
            }
        }
        catch
        {
            // Ignore frame conversion errors
        }
    }

    public Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Image<Rgba32> result;

        lock (_frameLock)
        {
            if (_currentFrame == null)
            {
                // Return black frame if no video frame available yet
                return Task.FromResult(new Image<Rgba32>(width, height, Color.Black));
            }

            result = _currentFrame.Clone();
        }

        // Resize if needed
        if (result.Width != width || result.Height != height)
        {
            result.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            }));

            // Center on black background if smaller than target
            if (result.Width != width || result.Height != height)
            {
                var centered = new Image<Rgba32>(width, height, Color.Black);
                var x = (width - result.Width) / 2;
                var y = (height - result.Height) / 2;
                centered.Mutate(ctx => ctx.DrawImage(result, new Point(x, y), 1f));
                result.Dispose();
                result = centered;
            }
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// Pauses video playback.
    /// </summary>
    public void Pause()
    {
        _mediaPlayer?.Pause();
    }

    /// <summary>
    /// Resumes video playback.
    /// </summary>
    public void Play()
    {
        _mediaPlayer?.Play();
    }

    /// <summary>
    /// Seeks to a position in the video.
    /// </summary>
    /// <param name="position">Position from 0 (start) to 1 (end).</param>
    public void Seek(float position)
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Position = Math.Clamp(position, 0, 1);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _mediaPlayer?.Stop();

        lock (_frameLock)
        {
            _currentFrame?.Dispose();
            _currentFrame = null;
        }

        _media?.Dispose();
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();

        _frameBuffer = null;
    }
}
