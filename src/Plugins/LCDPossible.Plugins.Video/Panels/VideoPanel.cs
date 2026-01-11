using System.Runtime.InteropServices;
using LCDPossible.Core.Rendering;
using LibVLCSharp.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Video.Panels;

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
        var resolvedSource = await VideoHelper.ResolveVideoSourceAsync(_videoPath, cancellationToken);

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
        _media = VideoHelper.IsUrl(resolvedSource)
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
