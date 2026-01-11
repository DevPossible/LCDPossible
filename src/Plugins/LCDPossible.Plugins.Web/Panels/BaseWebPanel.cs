using LCDPossible.Core.Rendering;
using PuppeteerSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Plugins.Web.Panels;

/// <summary>
/// Base class for panels that render web content using Puppeteer.
/// </summary>
public abstract class BaseWebPanel : IDisplayPanel
{
    private static IBrowser? _sharedBrowser;
    private static readonly SemaphoreSlim BrowserLock = new(1, 1);
    private static int _instanceCount;

    protected IPage? Page { get; private set; }
    protected int TargetWidth { get; private set; } = 1280;
    protected int TargetHeight { get; private set; } = 480;

    private Image<Rgba32>? _lastFrame;
    private DateTime _lastRender;
    private readonly TimeSpan _refreshInterval;
    private bool _initialized;
    private bool _disposed;

    protected BaseWebPanel(TimeSpan? refreshInterval = null)
    {
        _refreshInterval = refreshInterval ?? TimeSpan.FromSeconds(5);
    }

    public abstract string PanelId { get; }
    public abstract string DisplayName { get; }
    public bool IsLive => true;
    public bool IsAnimated => false; // Web panels use refresh interval, not frame-by-frame

    /// <summary>
    /// Gets or creates the shared browser instance.
    /// </summary>
    private static async Task<IBrowser> GetOrCreateBrowserAsync()
    {
        await BrowserLock.WaitAsync();
        try
        {
            if (_sharedBrowser == null || !_sharedBrowser.IsConnected)
            {
                // Download browser if needed
                var browserFetcher = new BrowserFetcher();
                await browserFetcher.DownloadAsync();

                _sharedBrowser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    Args =
                    [
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-gpu"
                    ]
                });
            }

            Interlocked.Increment(ref _instanceCount);
            return _sharedBrowser;
        }
        finally
        {
            BrowserLock.Release();
        }
    }

    /// <summary>
    /// Releases a reference to the shared browser.
    /// </summary>
    private static async Task ReleaseBrowserAsync()
    {
        await BrowserLock.WaitAsync();
        try
        {
            var remaining = Interlocked.Decrement(ref _instanceCount);
            if (remaining <= 0 && _sharedBrowser != null)
            {
                await _sharedBrowser.CloseAsync();
                _sharedBrowser.Dispose();
                _sharedBrowser = null;
            }
        }
        finally
        {
            BrowserLock.Release();
        }
    }

    public virtual async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        var browser = await GetOrCreateBrowserAsync();
        Page = await browser.NewPageAsync();

        await Page.SetViewportAsync(new ViewPortOptions
        {
            Width = TargetWidth,
            Height = TargetHeight,
            DeviceScaleFactor = 1
        });

        // Navigate to initial content
        await NavigateAsync(cancellationToken);

        _initialized = true;
    }

    /// <summary>
    /// Override to navigate to the desired content.
    /// </summary>
    protected abstract Task NavigateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Override to refresh/reload content if needed.
    /// </summary>
    protected virtual Task RefreshAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Update target dimensions if changed
        if (width != TargetWidth || height != TargetHeight)
        {
            TargetWidth = width;
            TargetHeight = height;

            if (Page != null)
            {
                await Page.SetViewportAsync(new ViewPortOptions
                {
                    Width = width,
                    Height = height,
                    DeviceScaleFactor = 1
                });
            }
        }

        // Check if we need to refresh
        var now = DateTime.UtcNow;
        var elapsed = now - _lastRender;

        if (_lastFrame != null && elapsed < _refreshInterval)
        {
            return _lastFrame.Clone();
        }

        // Refresh content if needed
        await RefreshAsync(cancellationToken);

        // Take screenshot
        if (Page != null)
        {
            var screenshotData = await Page.ScreenshotDataAsync(new ScreenshotOptions
            {
                Type = ScreenshotType.Png,
                FullPage = false
            });

            _lastFrame?.Dispose();
            _lastFrame = Image.Load<Rgba32>(screenshotData);
            _lastRender = now;

            return _lastFrame.Clone();
        }

        return new Image<Rgba32>(width, height, Color.Black);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _lastFrame?.Dispose();
            _lastFrame = null;

            if (Page != null)
            {
                try
                {
                    // Synchronously close the page with timeout
                    Page.CloseAsync().Wait(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // Ignore errors during cleanup
                }
                Page = null;
            }

            // Release browser reference synchronously with timeout
            try
            {
                ReleaseBrowserAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore timeout - browser will be cleaned up eventually
            }
        }
    }
}
