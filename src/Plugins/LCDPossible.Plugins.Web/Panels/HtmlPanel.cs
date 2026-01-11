using PuppeteerSharp;

namespace LCDPossible.Plugins.Web.Panels;

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
            WaitUntil = [WaitUntilNavigation.Networkidle2]
        });
    }

    protected override async Task RefreshAsync(CancellationToken cancellationToken)
    {
        // Reload the page to pick up any changes
        if (Page != null)
        {
            await Page.ReloadAsync(new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.Networkidle2]
            });
        }
    }
}
