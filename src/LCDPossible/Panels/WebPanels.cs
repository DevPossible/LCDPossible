using LCDPossible.Core.Rendering;
using PuppeteerSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Panels;

/// <summary>
/// Base class for panels that render web content using Puppeteer.
/// </summary>
public abstract class BaseWebPanel : IDisplayPanel
{
    private static IBrowser? _sharedBrowser;
    private static readonly SemaphoreSlim _browserLock = new(1, 1);
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
        await _browserLock.WaitAsync();
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
                    Args = new[]
                    {
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-gpu"
                    }
                });
            }

            Interlocked.Increment(ref _instanceCount);
            return _sharedBrowser;
        }
        finally
        {
            _browserLock.Release();
        }
    }

    /// <summary>
    /// Releases a reference to the shared browser.
    /// </summary>
    private static async Task ReleaseBrowserAsync()
    {
        await _browserLock.WaitAsync();
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
            _browserLock.Release();
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

    public virtual async void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _lastFrame?.Dispose();
        _lastFrame = null;

        if (Page != null)
        {
            try
            {
                await Page.CloseAsync();
            }
            catch
            {
                // Ignore errors during cleanup
            }
            Page = null;
        }

        await ReleaseBrowserAsync();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Panel that renders a local HTML file.
/// </summary>
public sealed class HtmlPanel : BaseWebPanel
{
    private readonly string _htmlPath;

    /// <summary>
    /// Creates a new HTML panel.
    /// </summary>
    /// <param name="htmlPath">Path to the HTML file.</param>
    /// <param name="refreshInterval">How often to re-render (default: 5 seconds).</param>
    public HtmlPanel(string htmlPath, TimeSpan? refreshInterval = null)
        : base(refreshInterval)
    {
        if (string.IsNullOrWhiteSpace(htmlPath))
        {
            throw new ArgumentException("HTML path cannot be empty.", nameof(htmlPath));
        }

        _htmlPath = Path.GetFullPath(htmlPath);
    }

    public override string PanelId => $"html:{Path.GetFileName(_htmlPath)}";
    public override string DisplayName => $"HTML: {Path.GetFileName(_htmlPath)}";

    protected override async Task NavigateAsync(CancellationToken cancellationToken)
    {
        if (Page == null)
        {
            return;
        }

        if (!File.Exists(_htmlPath))
        {
            throw new FileNotFoundException($"HTML file not found: {_htmlPath}");
        }

        var fileUri = new Uri(_htmlPath).AbsoluteUri;
        await Page.GoToAsync(fileUri, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
        });
    }

    protected override async Task RefreshAsync(CancellationToken cancellationToken)
    {
        // Reload the page to pick up any changes
        if (Page != null)
        {
            await Page.ReloadAsync(new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
            });
        }
    }
}

/// <summary>
/// Panel that renders a live website from a URL.
/// </summary>
public sealed class WebPanel : BaseWebPanel
{
    private readonly string _url;
    private readonly bool _autoRefresh;

    /// <summary>
    /// Creates a new web panel.
    /// </summary>
    /// <param name="url">URL to display.</param>
    /// <param name="refreshInterval">How often to refresh the page (default: 30 seconds).</param>
    /// <param name="autoRefresh">Whether to auto-refresh the page (default: true).</param>
    public WebPanel(string url, TimeSpan? refreshInterval = null, bool autoRefresh = true)
        : base(refreshInterval ?? TimeSpan.FromSeconds(30))
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be empty.", nameof(url));
        }

        // Ensure URL has a scheme
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        _url = url;
        _autoRefresh = autoRefresh;
    }

    public override string PanelId => $"web:{new Uri(_url).Host}";
    public override string DisplayName => $"Web: {new Uri(_url).Host}";

    protected override async Task NavigateAsync(CancellationToken cancellationToken)
    {
        if (Page == null)
        {
            return;
        }

        await Page.GoToAsync(_url, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
            Timeout = 30000
        });
    }

    protected override async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (_autoRefresh && Page != null)
        {
            await Page.ReloadAsync(new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
                Timeout = 30000
            });
        }
    }
}
