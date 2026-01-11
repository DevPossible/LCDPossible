using PuppeteerSharp;

namespace LCDPossible.Plugins.Web.Panels;

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
            WaitUntil = [WaitUntilNavigation.Networkidle2],
            Timeout = 30000
        });
    }

    protected override async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (_autoRefresh && Page != null)
        {
            await Page.ReloadAsync(new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.Networkidle2],
                Timeout = 30000
            });
        }
    }
}
