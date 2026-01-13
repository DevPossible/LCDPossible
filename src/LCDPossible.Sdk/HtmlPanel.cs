using LCDPossible.Core.Configuration;
using PuppeteerSharp;
using Scriban;
using Scriban.Runtime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Sdk;

/// <summary>
/// Base class for panels that render HTML content using Scriban templates and Puppeteer.
/// Provides templating, live data refresh, and browser screenshot capabilities.
/// </summary>
/// <remarks>
/// <para>
/// HtmlPanel supports three template sources (override one):
/// <list type="bullet">
///   <item><see cref="TemplatePath"/> - Local file path to a template</item>
///   <item><see cref="TemplateUrl"/> - Remote URL to navigate to</item>
///   <item><see cref="TemplateContent"/> - Inline Scriban template string</item>
/// </list>
/// </para>
/// <para>
/// Subclasses must implement <see cref="GetDataModelAsync"/> to provide data for template rendering.
/// The data model is available in templates as <c>data</c>.
/// </para>
/// <para>
/// Color scheme variables are automatically injected as CSS custom properties.
/// </para>
/// </remarks>
public abstract class HtmlPanel : BasePanel
{
    // Shared browser instance management
    private static IBrowser? _sharedBrowser;
    private static readonly SemaphoreSlim BrowserLock = new(1, 1);
    private static int _instanceCount;

    // Page and rendering state
    protected IPage? Page { get; private set; }
    protected int TargetWidth { get; private set; } = 1280;
    protected int TargetHeight { get; private set; } = 480;

    private Template? _compiledTemplate;
    private Image<Rgba32>? _lastFrame;
    private DateTime _lastRender;
    private bool _initialized;

    /// <summary>
    /// How often to refresh the data model and re-render.
    /// Default is 5 seconds.
    /// </summary>
    protected virtual TimeSpan RefreshInterval => TimeSpan.FromSeconds(5);

    /// <summary>
    /// Local file path to a Scriban template.
    /// Override this OR <see cref="TemplateUrl"/> OR <see cref="TemplateContent"/>.
    /// </summary>
    protected virtual string? TemplatePath => null;

    /// <summary>
    /// Remote URL to navigate to (no templating, just screenshot).
    /// Override this OR <see cref="TemplatePath"/> OR <see cref="TemplateContent"/>.
    /// </summary>
    protected virtual string? TemplateUrl => null;

    /// <summary>
    /// Inline Scriban template content.
    /// Override this OR <see cref="TemplatePath"/> OR <see cref="TemplateUrl"/>.
    /// </summary>
    protected virtual string? TemplateContent => null;

    /// <summary>
    /// Provides the data model for template rendering.
    /// Called on each refresh to get fresh data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Object that will be available as <c>data</c> in the template.</returns>
    protected abstract Task<object> GetDataModelAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the path to the html_assets folder.
    /// </summary>
    protected string AssetsPath => GetAssetsPath();

    /// <summary>
    /// Gets the assets path as a file URI (for use in HTML src/href attributes).
    /// Cross-platform: converts backslashes to forward slashes for proper URI format.
    /// </summary>
    protected string AssetsUri => PathToFileUri(AssetsPath);

    /// <summary>
    /// Gets the inline JavaScript for web components.
    /// Reads from components.js and caches the content.
    /// </summary>
    protected string ComponentsScript => _componentsScript ??= LoadAssetFile("js", "components.js", "// components.js not found");

    /// <summary>
    /// Gets the inline theme CSS.
    /// Reads from theme.css and caches the content.
    /// </summary>
    protected string ThemeCss => _themeCss ??= LoadAssetFile("css", "theme.css", "/* theme.css not found */");

    /// <summary>
    /// Gets the Tailwind CSS CDN script (local copy).
    /// Used for generating utility classes at runtime.
    /// </summary>
    protected string TailwindScript => _tailwindScript ??= LoadAssetFile("css", "tailwind-full.js", "// tailwind-full.js not found");

    /// <summary>
    /// Gets the DaisyUI CSS (local copy).
    /// Component library built on Tailwind.
    /// </summary>
    protected string DaisyUiCss => _daisyUiCss ??= LoadAssetFile("css", "daisyui.min.css", "/* daisyui.min.css not found */");

    /// <summary>
    /// Gets the LCD custom themes CSS.
    /// DaisyUI theme definitions for LCD displays.
    /// </summary>
    protected string LcdThemesCss => _lcdThemesCss ??= LoadAssetFile("css", "lcd-themes.css", "/* lcd-themes.css not found */");

    /// <summary>
    /// Gets the ECharts library script (minified).
    /// Used for advanced charts and gauges.
    /// </summary>
    protected string EChartsScript => _echartsScript ??= LoadAssetFile("js", "echarts.min.js", "// echarts.min.js not found");

    /// <summary>
    /// Gets the ECharts-based web components.
    /// Advanced gauge, donut, sparkline components using ECharts.
    /// </summary>
    protected string EChartsComponentsScript => _echartsComponentsScript ??= LoadAssetFile("js", "echarts-components.js", "// echarts-components.js not found");

    /// <summary>
    /// Gets the DaisyUI-based web components.
    /// Components using DaisyUI's built-in radial-progress, progress, stat.
    /// </summary>
    protected string DaisyUiComponentsScript => _daisyUiComponentsScript ??= LoadAssetFile("js", "daisyui-components.js", "// daisyui-components.js not found");

    private static string? _componentsScript;
    private static string? _themeCss;
    private static string? _tailwindScript;
    private static string? _daisyUiCss;
    private static string? _lcdThemesCss;
    private static string? _echartsScript;
    private static string? _echartsComponentsScript;
    private static string? _daisyUiComponentsScript;
    private Theme? _currentTheme;

    private static string LoadAssetFile(string subfolder, string filename, string fallback)
    {
        var assemblyDir = Path.GetDirectoryName(typeof(HtmlPanel).Assembly.Location) ?? ".";
        var filePath = Path.Combine(assemblyDir, "html_assets", subfolder, filename);

        if (File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }

        // Fallback to current directory
        filePath = Path.Combine(Environment.CurrentDirectory, "html_assets", subfolder, filename);
        if (File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }

        return fallback;
    }

    private static string GetAssetsPath()
    {
        // Look for html_assets relative to the executing assembly
        var assemblyDir = Path.GetDirectoryName(typeof(HtmlPanel).Assembly.Location) ?? ".";
        var assetsPath = Path.Combine(assemblyDir, "html_assets");

        if (Directory.Exists(assetsPath))
        {
            return Path.GetFullPath(assetsPath);
        }

        // Fallback: look in current directory
        assetsPath = Path.Combine(Environment.CurrentDirectory, "html_assets");
        if (Directory.Exists(assetsPath))
        {
            return Path.GetFullPath(assetsPath);
        }

        // Last resort: return relative path
        return "html_assets";
    }

    /// <summary>
    /// Converts a file system path to a proper file:// URI.
    /// Handles cross-platform differences (Windows backslashes vs Unix forward slashes).
    /// </summary>
    private static string PathToFileUri(string path)
    {
        // Get absolute path and convert to URI format
        var absolutePath = Path.GetFullPath(path);

        // Replace backslashes with forward slashes for URI
        var uriPath = absolutePath.Replace('\\', '/');

        // On Windows, paths start with drive letter (C:/) - need to add leading slash
        // On Unix, paths already start with / so this is safe
        if (!uriPath.StartsWith('/'))
        {
            uriPath = "/" + uriPath;
        }

        return uriPath;
    }

    /// <summary>
    /// Sets the theme for this panel.
    /// When set, the theme's CSS variables are used instead of the ResolvedColorScheme.
    /// </summary>
    /// <param name="theme">The theme to use, or null to use the default ColorScheme.</param>
    public void SetTheme(Theme? theme)
    {
        _currentTheme = theme;
        if (theme != null)
        {
            // Also update the base Colors for any code that uses ResolvedColorScheme directly
            SetColorScheme(theme.ToColorScheme().Resolve());
        }
    }

    /// <summary>
    /// Gets the current theme ID for use in DaisyUI data-theme attribute.
    /// Returns null if no theme is explicitly set.
    /// </summary>
    protected string? CurrentThemeId => _currentTheme?.Id;

    #region Browser Management

    private static async Task<IBrowser> GetOrCreateBrowserAsync()
    {
        await BrowserLock.WaitAsync();
        try
        {
            if (_sharedBrowser == null || !_sharedBrowser.IsConnected)
            {
                var browserFetcher = new BrowserFetcher();
                var installedBrowser = await browserFetcher.DownloadAsync();

                Console.WriteLine($"[DEBUG] Browser path: {installedBrowser.GetExecutablePath()}");

                _sharedBrowser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    ExecutablePath = installedBrowser.GetExecutablePath(),
                    Args =
                    [
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-gpu",
                        "--allow-file-access-from-files",
                        "--disable-web-security"
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

    #endregion

    #region Template Compilation

    private void CompileTemplate()
    {
        string? templateSource = null;

        if (!string.IsNullOrEmpty(TemplateContent))
        {
            templateSource = TemplateContent;
        }
        else if (!string.IsNullOrEmpty(TemplatePath) && File.Exists(TemplatePath))
        {
            templateSource = File.ReadAllText(TemplatePath);
        }

        if (!string.IsNullOrEmpty(templateSource))
        {
            _compiledTemplate = Template.Parse(templateSource);
            if (_compiledTemplate.HasErrors)
            {
                var errors = string.Join("; ", _compiledTemplate.Messages.Select(m => m.Message));
                throw new InvalidOperationException($"Template compilation failed: {errors}");
            }
        }
    }

    /// <summary>
    /// Renders the Scriban template with the given data model.
    /// </summary>
    protected string RenderTemplate(object dataModel)
    {
        if (_compiledTemplate == null)
        {
            // No template - return empty HTML
            return WrapInBaseHtml("");
        }

        var context = new TemplateContext();
        var scriptObject = new ScriptObject();

        // Add data model
        scriptObject["data"] = dataModel;

        // Add target dimensions
        scriptObject["target_width"] = TargetWidth;
        scriptObject["target_height"] = TargetHeight;

        // Add assets path as URI (cross-platform file:// URL format)
        scriptObject["assets_path"] = AssetsUri;
        scriptObject["assets"] = AssetsUri;

        // Add inline JavaScript for web components
        scriptObject["components_script"] = ComponentsScript;

        // Add inline theme CSS
        scriptObject["theme_css"] = ThemeCss;

        // Add Tailwind CDN script (local copy)
        scriptObject["tailwind_script"] = TailwindScript;

        // Add DaisyUI CSS (local copy)
        scriptObject["daisyui_css"] = DaisyUiCss;

        // Add LCD custom themes CSS
        scriptObject["lcd_themes_css"] = LcdThemesCss;

        // Add ECharts library script
        scriptObject["echarts_script"] = EChartsScript;

        // Add ECharts-based components
        scriptObject["echarts_components_script"] = EChartsComponentsScript;

        // Add DaisyUI-based components
        scriptObject["daisyui_components_script"] = DaisyUiComponentsScript;

        // Add color scheme as CSS variables
        scriptObject["colors"] = CreateColorScriptObject();
        scriptObject["colors_css"] = GenerateColorsCss();

        context.PushGlobal(scriptObject);

        var rendered = _compiledTemplate.Render(context);
        return rendered;
    }

    private ScriptObject CreateColorScriptObject()
    {
        var colors = new ScriptObject
        {
            ["background"] = ColorToHex(Colors.Background),
            ["background_secondary"] = ColorToHex(Colors.BackgroundSecondary),
            ["bar_background"] = ColorToHex(Colors.BarBackground),
            ["bar_border"] = ColorToHex(Colors.BarBorder),
            ["text_primary"] = ColorToHex(Colors.TextPrimary),
            ["text_secondary"] = ColorToHex(Colors.TextSecondary),
            ["text_muted"] = ColorToHex(Colors.TextMuted),
            ["accent"] = ColorToHex(Colors.Accent),
            ["accent_secondary"] = ColorToHex(Colors.AccentSecondary),
            ["success"] = ColorToHex(Colors.Success),
            ["warning"] = ColorToHex(Colors.Warning),
            ["critical"] = ColorToHex(Colors.Critical),
            ["info"] = ColorToHex(Colors.Info),
            ["usage_low"] = ColorToHex(Colors.UsageLow),
            ["usage_medium"] = ColorToHex(Colors.UsageMedium),
            ["usage_high"] = ColorToHex(Colors.UsageHigh),
            ["usage_critical"] = ColorToHex(Colors.UsageCritical),
            ["temp_cool"] = ColorToHex(Colors.TempCool),
            ["temp_warm"] = ColorToHex(Colors.TempWarm),
            ["temp_hot"] = ColorToHex(Colors.TempHot)
        };
        return colors;
    }

    private string GenerateColorsCss()
    {
        // Use theme CSS variables if a theme is set
        if (_currentTheme != null)
        {
            return _currentTheme.ToCssVariables();
        }

        // Fall back to ResolvedColorScheme-based CSS
        return $@":root {{
    --color-background: {ColorToHex(Colors.Background)};
    --color-background-secondary: {ColorToHex(Colors.BackgroundSecondary)};
    --color-surface: {ColorToHex(Colors.BackgroundSecondary)};
    --color-bar-background: {ColorToHex(Colors.BarBackground)};
    --color-bar-border: {ColorToHex(Colors.BarBorder)};
    --color-text-primary: {ColorToHex(Colors.TextPrimary)};
    --color-text-secondary: {ColorToHex(Colors.TextSecondary)};
    --color-text-muted: {ColorToHex(Colors.TextMuted)};
    --color-accent: {ColorToHex(Colors.Accent)};
    --color-accent-secondary: {ColorToHex(Colors.AccentSecondary)};
    --color-accent-tertiary: {ColorToHex(Colors.Accent)};
    --color-success: {ColorToHex(Colors.Success)};
    --color-warning: {ColorToHex(Colors.Warning)};
    --color-critical: {ColorToHex(Colors.Critical)};
    --color-info: {ColorToHex(Colors.Info)};
    --color-usage-low: {ColorToHex(Colors.UsageLow)};
    --color-usage-medium: {ColorToHex(Colors.UsageMedium)};
    --color-usage-high: {ColorToHex(Colors.UsageHigh)};
    --color-usage-critical: {ColorToHex(Colors.UsageCritical)};
    --color-temp-cool: {ColorToHex(Colors.TempCool)};
    --color-temp-warm: {ColorToHex(Colors.TempWarm)};
    --color-temp-hot: {ColorToHex(Colors.TempHot)};
    --font-display: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    --font-body: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    --font-data: ui-monospace, 'Cascadia Code', 'Source Code Pro', Menlo, Consolas, monospace;
    --glass-blur: blur(20px);
    --glass-bg: rgba(8, 12, 20, 0.85);
    --glow-strength: 1;
    --scanlines-opacity: 0;
    --border-color: {ColorToHex(Colors.Accent)}33;
    --border-radius: 4px;
    --border-width: 1px;
}}";
    }

    private static string ColorToHex(Color color)
    {
        var pixel = color.ToPixel<Rgba32>();
        return $"#{pixel.R:X2}{pixel.G:X2}{pixel.B:X2}";
    }

    private string WrapInBaseHtml(string bodyContent)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width={TargetWidth}, height={TargetHeight}, initial-scale=1"">
    <link rel=""stylesheet"" href=""file://{AssetsUri}/css/theme.css"">
    <style>{GenerateColorsCss()}</style>
</head>
<body class=""bg-panel text-primary"" style=""margin:0;padding:0;height:100vh;width:100vw;overflow:hidden;"">
{bodyContent}
</body>
</html>";
    }

    #endregion

    #region Lifecycle

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await base.InitializeAsync(cancellationToken);

        // Compile template
        CompileTemplate();

        // Initialize browser and page
        var browser = await GetOrCreateBrowserAsync();
        Page = await browser.NewPageAsync();

        await Page.SetViewportAsync(new ViewPortOptions
        {
            Width = TargetWidth,
            Height = TargetHeight,
            DeviceScaleFactor = 1
        });

        // Initial render
        await RenderToPageAsync(cancellationToken);

        _initialized = true;
    }

    private async Task RenderToPageAsync(CancellationToken cancellationToken)
    {
        if (Page == null)
        {
            return;
        }

        // If we have a URL, navigate to it
        if (!string.IsNullOrEmpty(TemplateUrl))
        {
            var url = TemplateUrl;
            if (!url.StartsWith("http://") && !url.StartsWith("https://") && !url.StartsWith("file://"))
            {
                url = "https://" + url;
            }

            await Page.GoToAsync(url, new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.Networkidle2],
                Timeout = 30000
            });
            return;
        }

        // Otherwise, render template and navigate to temp file
        // (Using GoToAsync instead of SetContentAsync so file:// URLs resolve correctly)
        var dataModel = await GetDataModelAsync(cancellationToken);
        var html = RenderTemplate(dataModel);

        // Write HTML to temp file and navigate to it
        var tempHtmlPath = Path.Combine(Path.GetTempPath(), $"{PanelId}-debug.html");
        await File.WriteAllTextAsync(tempHtmlPath, html, cancellationToken);

        // Navigate to file so external scripts with file:// URLs load correctly
        var fileUrl = new Uri(tempHtmlPath).AbsoluteUri;
        await Page.GoToAsync(fileUrl, new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.Networkidle0],
            Timeout = 10000
        });
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Update viewport if dimensions changed
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

        if (_lastFrame != null && elapsed < RefreshInterval)
        {
            return _lastFrame.Clone();
        }

        // Re-render template with fresh data
        await RenderToPageAsync(cancellationToken);

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

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lastFrame?.Dispose();
        _lastFrame = null;

        if (Page != null)
        {
            try
            {
                Page.CloseAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore errors during cleanup
            }
            Page = null;
        }

        try
        {
            ReleaseBrowserAsync().Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore timeout
        }

        base.Dispose();
    }

    #endregion
}
