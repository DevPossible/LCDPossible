using LCDPossible.Core.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Images.Panels;

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
