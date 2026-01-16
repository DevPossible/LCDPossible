using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using LCDPossible.Core.Configuration;
using LCDPossible.Core.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
public abstract class HtmlPanel : BasePanel, IAsyncDisposable
{
    // Shared browser instance management
    private static IBrowser? _sharedBrowser;
    private static readonly SemaphoreSlim BrowserLock = new(1, 1);

    // Compiled regex for body extraction (M006 - avoid recompilation)
    private static readonly Regex BodyStartRegex = new(
        @"<body[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Static logger for static methods (M005 - replace Console.Error)
    private static ILogger? _staticLogger;

    /// <summary>
    /// Sets the static logger used for asset loading diagnostics.
    /// Should be called once during application startup.
    /// </summary>
    public static void SetStaticLogger(ILogger logger) => _staticLogger = logger;
    private static int _instanceCount;

    // Page and rendering state
    protected IPage? Page { get; private set; }
    protected int TargetWidth { get; private set; } = 1280;
    protected int TargetHeight { get; private set; } = 480;

    private Template? _compiledTemplate;
    private Image<Rgba32>? _lastFrame;
    private DateTime _lastRender;
    private DateTime _lastFrameTime; // For calculating deltaTime between frames (for animations)
    private bool _initialized;
    private bool _pageLoaded; // True after first navigation - subsequent updates use JS injection

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
    private static readonly ConcurrentDictionary<string, string> _themeScriptCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> _effectScriptCache = new(StringComparer.OrdinalIgnoreCase);
    private Theme? _currentTheme;
    private PageEffect? _currentPageEffect;
    private PanelRenderMode? _renderModeOverride;

    /// <summary>
    /// Gets the rendering mode for this panel.
    /// When a page effect requiring live mode is active, this returns Stream.
    /// </summary>
    public override PanelRenderMode RenderMode =>
        _renderModeOverride ?? base.RenderMode;

    /// <summary>
    /// Gets the current theme applied to this panel.
    /// </summary>
    protected Theme? CurrentTheme => _currentTheme;

    /// <summary>
    /// Gets the current page effect applied to this panel.
    /// </summary>
    protected PageEffect? CurrentPageEffect => _currentPageEffect;

    /// <summary>
    /// Gets the last rendered HTML content (for debugging purposes).
    /// </summary>
    public string? LastRenderedHtml { get; private set; }

    private bool _domReadyCalled;

    private static string LoadAssetFile(string subfolder, string filename, string fallback)
    {
        var debug = Environment.GetEnvironmentVariable("LCDPOSSIBLE_DEBUG") == "1";
        var pathsTried = new List<string>();

        // Try 1: Assembly location (works for non-single-file publish)
        var assemblyDir = Path.GetDirectoryName(typeof(HtmlPanel).Assembly.Location) ?? ".";
        var filePath = Path.Combine(assemblyDir, "html_assets", subfolder, filename);
        pathsTried.Add(filePath);

        if (File.Exists(filePath))
        {
            var content = File.ReadAllText(filePath);
            if (debug) _staticLogger?.LogDebug("Asset loaded: {Filename} from {Path} ({Length} bytes)", filename, filePath, content.Length);
            return content;
        }

        // Try 2: Process executable directory (works for single-file publish on Linux)
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
        if (exeDir != assemblyDir)
        {
            var exePath = Path.Combine(exeDir, "html_assets", subfolder, filename);
            pathsTried.Add(exePath);

            if (File.Exists(exePath))
            {
                var content = File.ReadAllText(exePath);
                if (debug) _staticLogger?.LogDebug("Asset loaded: {Filename} from {Path} ({Length} bytes)", filename, exePath, content.Length);
                return content;
            }
        }

        // Try 3: Current directory
        var cwdPath = Path.Combine(Environment.CurrentDirectory, "html_assets", subfolder, filename);
        if (!pathsTried.Contains(cwdPath))
        {
            pathsTried.Add(cwdPath);

            if (File.Exists(cwdPath))
            {
                var content = File.ReadAllText(cwdPath);
                if (debug) _staticLogger?.LogDebug("Asset loaded: {Filename} from {Path} ({Length} bytes)", filename, cwdPath, content.Length);
                return content;
            }
        }

        // Try 4: /opt/lcdpossible (standard Linux install location)
        var optPath = Path.Combine("/opt/lcdpossible", "html_assets", subfolder, filename);
        if (!pathsTried.Contains(optPath))
        {
            pathsTried.Add(optPath);

            if (File.Exists(optPath))
            {
                var content = File.ReadAllText(optPath);
                if (debug) _staticLogger?.LogDebug("Asset loaded: {Filename} from {Path} ({Length} bytes)", filename, optPath, content.Length);
                return content;
            }
        }

        // Log when asset file isn't found (helps debug deployment issues)
        _staticLogger?.LogWarning("Asset not found: {Filename} - tried: {Paths}", filename, string.Join(", ", pathsTried));

        return fallback;
    }

    private static string GetAssetsPath()
    {
        // Try 1: Assembly location (works for non-single-file publish)
        var assemblyDir = Path.GetDirectoryName(typeof(HtmlPanel).Assembly.Location) ?? ".";
        var assetsPath = Path.Combine(assemblyDir, "html_assets");

        if (Directory.Exists(assetsPath))
        {
            return Path.GetFullPath(assetsPath);
        }

        // Try 2: Process executable directory (works for single-file publish on Linux)
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
        if (exeDir != assemblyDir)
        {
            assetsPath = Path.Combine(exeDir, "html_assets");
            if (Directory.Exists(assetsPath))
            {
                return Path.GetFullPath(assetsPath);
            }
        }

        // Try 3: Current directory
        assetsPath = Path.Combine(Environment.CurrentDirectory, "html_assets");
        if (Directory.Exists(assetsPath))
        {
            return Path.GetFullPath(assetsPath);
        }

        // Try 4: /opt/lcdpossible (standard Linux install location)
        assetsPath = "/opt/lcdpossible/html_assets";
        if (Directory.Exists(assetsPath))
        {
            return Path.GetFullPath(assetsPath);
        }

        // Last resort: return relative path
        return "html_assets";
    }

    /// <summary>
    /// Gets the JavaScript content for a specific theme.
    /// Looks for {themeId}.js in html_assets/js/themes/ folder.
    /// Returns empty string if no theme JS exists.
    /// </summary>
    protected string GetThemeScript(string? themeId)
    {
        if (string.IsNullOrEmpty(themeId))
            return GetDefaultThemeHooks();

        // Check Theme.ScriptContent first (allows programmatic themes)
        if (_currentTheme?.ScriptContent != null)
        {
            return _themeScriptCache.GetOrAdd(themeId, _ => _currentTheme.ScriptContent);
        }

        // Thread-safe cache lookup with factory
        return _themeScriptCache.GetOrAdd(themeId, id =>
        {
            // Try to load from file
            var script = LoadAssetFile("js/themes", $"{id}.js", "");

            // If no theme-specific file, provide default hooks structure
            return string.IsNullOrEmpty(script) ? GetDefaultThemeHooks() : script;
        });
    }

    /// <summary>
    /// Gets the default theme hooks structure (no-op implementations).
    /// </summary>
    private static string GetDefaultThemeHooks() => """
        // Default theme hooks (no-op)
        window.LCDTheme = {
            // Called after DOM is ready, before first frame capture
            onDomReady: function() {},
            // Called after transition animation completes
            onTransitionEnd: function() {},
            // Called before each frame render (for animations)
            onBeforeRender: function() {}
        };
        """;

    /// <summary>
    /// Gets the JavaScript content for a specific page effect.
    /// Looks for {effectId}.js in html_assets/js/effects/ folder.
    /// Returns empty string if no effect JS exists.
    /// </summary>
    protected string GetPageEffectScript(string? effectId)
    {
        if (string.IsNullOrEmpty(effectId) || effectId == "none")
            return GetDefaultEffectHooks();

        // Check PageEffect.ScriptContent first (allows programmatic effects)
        if (_currentPageEffect?.ScriptContent != null)
        {
            return _effectScriptCache.GetOrAdd(effectId, _ => _currentPageEffect.ScriptContent);
        }

        // Thread-safe cache lookup with factory
        return _effectScriptCache.GetOrAdd(effectId, id =>
        {
            // Try to load from file
            var script = LoadAssetFile("js/effects", $"{id}.js", "");

            // If no effect-specific file, provide default hooks structure
            return string.IsNullOrEmpty(script) ? GetDefaultEffectHooks() : script;
        });
    }

    /// <summary>
    /// Gets the default page effect hooks structure (no-op implementations).
    /// </summary>
    private static string GetDefaultEffectHooks() => """
        // Default page effect hooks (no-op)
        window.LCDEffect = {
            // Called once after DOM is ready
            onInit: function(options) {},
            // Called when any value changes (receives { element, oldValue, newValue })
            onValueChange: function(change) {},
            // Called before each frame render
            onBeforeRender: function(deltaTime) {},
            // Called after each frame render
            onAfterRender: function(deltaTime) {},
            // Called when widget enters warning/critical state
            onWarning: function(element, level) {},
            // Cleanup when effect is removed
            onDestroy: function() {}
        };
        """;

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

    /// <summary>
    /// Sets the page effect for this panel.
    /// Page effects add visual enhancements like animations, transitions, and interactive elements.
    /// When an effect requiring live mode is set, the panel automatically switches to Stream render mode.
    /// </summary>
    /// <param name="effect">The page effect to use, or null for no effect.</param>
    public void SetPageEffect(PageEffect? effect)
    {
        _currentPageEffect = effect;
        UpdateRenderModeForEffect(effect);
    }

    /// <summary>
    /// Sets the page effect by ID, looking it up from PageEffectManager.
    /// When an effect requiring live mode is set, the panel automatically switches to Stream render mode.
    /// </summary>
    /// <param name="effectId">The effect ID (e.g., "glow-on-change", "matrix-rain"), or null/empty/"none" for no effect.</param>
    public void SetPageEffectById(string? effectId)
    {
        var debug = Environment.GetEnvironmentVariable("LCDPOSSIBLE_DEBUG") == "1";
        var previousEffect = _currentPageEffect?.Id;

        _currentPageEffect = PageEffectManager.Instance.GetEffect(effectId);
        UpdateRenderModeForEffect(_currentPageEffect);

        if (debug)
        {
            if (_currentPageEffect != null)
            {
                _staticLogger?.LogDebug("[{PanelId}] Effect set: '{EffectId}' ({EffectName})", PanelId, _currentPageEffect.Id, _currentPageEffect.DisplayName);
                _staticLogger?.LogDebug("[{PanelId}]   RequiresLiveMode: {RequiresLiveMode}, RenderMode: {RenderMode}", PanelId, _currentPageEffect.RequiresLiveMode, RenderMode);
            }
            else if (!string.IsNullOrEmpty(previousEffect))
            {
                _staticLogger?.LogDebug("[{PanelId}] Effect cleared (was: '{PreviousEffect}')", PanelId, previousEffect);
            }
        }
    }

    /// <summary>
    /// Updates the render mode based on the page effect requirements.
    /// </summary>
    private void UpdateRenderModeForEffect(PageEffect? effect)
    {
        if (effect?.RequiresLiveMode == true)
        {
            _renderModeOverride = PanelRenderMode.Stream;
            _staticLogger?.LogDebug("[{PanelId}] Page effect '{EffectId}' requires live mode - switching to Stream render mode", PanelId, effect.Id);
        }
        else
        {
            _renderModeOverride = null;
        }
    }

    /// <summary>
    /// Gets the current page effect ID.
    /// Returns null if no page effect is set.
    /// </summary>
    protected string? CurrentPageEffectId => _currentPageEffect?.Id;

    #region Browser Management

    private static Exception? _browserLaunchError;

    private static async Task<IBrowser> GetOrCreateBrowserAsync()
    {
        await BrowserLock.WaitAsync();
        try
        {
            // If we already failed to launch, don't retry
            if (_browserLaunchError != null)
            {
                throw new InvalidOperationException(
                    $"Browser launch previously failed: {_browserLaunchError.Message}",
                    _browserLaunchError);
            }

            if (_sharedBrowser == null || !_sharedBrowser.IsConnected)
            {
                var browserFetcher = new BrowserFetcher();
                var installedBrowser = await browserFetcher.DownloadAsync();

                var executablePath = installedBrowser.GetExecutablePath();
                _staticLogger?.LogDebug("Browser path: {ExecutablePath}", executablePath);

                try
                {
                    _sharedBrowser = await Puppeteer.LaunchAsync(new LaunchOptions
                    {
                        Headless = true,
                        ExecutablePath = executablePath,
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

                    _staticLogger?.LogDebug("Browser launched successfully");
                }
                catch (Exception ex)
                {
                    _browserLaunchError = ex;
                    _staticLogger?.LogError(ex, "Failed to launch browser: {Message}", ex.Message);
                    throw;
                }
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

        // Add theme-specific JavaScript (lifecycle hooks)
        scriptObject["theme_script"] = GetThemeScript(_currentTheme?.Id);

        // Add page effect JavaScript (lifecycle hooks)
        scriptObject["page_effect_script"] = GetPageEffectScript(_currentPageEffect?.Id);
        scriptObject["page_effect_id"] = _currentPageEffect?.Id ?? "none";

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

        // Add console message handler for debugging
        Page.Console += (sender, e) =>
        {
            var type = e.Message.Type.ToString().ToUpperInvariant();
            var text = e.Message.Text;

            // Always show errors and warnings
            if (type == "ERROR" || type == "WARNING")
            {
                _staticLogger?.LogWarning("[BROWSER {Type}] [{PanelId}] {Text}", type, PanelId, text);
            }
            // In debug mode, also show effect-related console messages
            else if (Environment.GetEnvironmentVariable("LCDPOSSIBLE_DEBUG") == "1")
            {
                // Show messages from effects (LCDEffect) or themes (LCDTheme)
                if (text.Contains("LCDEffect") || text.Contains("LCDTheme") ||
                    text.Contains("[EFFECT]") || text.Contains("[THEME]"))
                {
                    _staticLogger?.LogDebug("[BROWSER LOG] [{PanelId}] {Text}", PanelId, text);
                }
            }
        };

        // Add page error handler
        Page.PageError += (sender, e) =>
        {
            _staticLogger?.LogError("[BROWSER PAGE ERROR] [{PanelId}] {Message}", PanelId, e.Message);
        };

        // Add request failed handler for debugging resource loading
        Page.RequestFailed += (sender, e) =>
        {
            _staticLogger?.LogWarning("[BROWSER REQUEST FAILED] [{PanelId}] {Url}", PanelId, e.Request.Url);
        };

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

        var debug = Environment.GetEnvironmentVariable("LCDPOSSIBLE_DEBUG") == "1";

        // If we have a URL, navigate to it (URL panels don't support data injection)
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
            _pageLoaded = true;
            return;
        }

        // Get fresh data and render template
        var dataModel = await GetDataModelAsync(cancellationToken);
        var html = RenderTemplate(dataModel);

        // If page is already loaded, inject updated content via JavaScript instead of re-navigating
        // This preserves JS state including running page effects
        if (_pageLoaded)
        {
            // Still update LastRenderedHtml for debugging
            LastRenderedHtml = html;

            await InjectHtmlContentAsync(html, dataModel);
            _staticLogger?.LogDebug("[{PanelId}] Updated panel content via JS injection (preserving page effects)", PanelId);
            return;
        }

        // First render: navigate to temp file
        // (Using GoToAsync instead of SetContentAsync so file:// URLs resolve correctly)

        // Store the rendered HTML for debugging purposes
        LastRenderedHtml = html;

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

        _pageLoaded = true;
        _staticLogger?.LogDebug("[{PanelId}] Initial page loaded from {TempPath}", PanelId, tempHtmlPath);
    }

    /// <summary>
    /// Injects updated HTML content into the page via JavaScript without re-navigating.
    /// This preserves all JS state including running page effects.
    /// </summary>
    private async Task InjectHtmlContentAsync(string fullHtml, object dataModel)
    {
        if (Page == null) return;

        try
        {
            // Extract the body content from the full HTML
            var bodyContent = ExtractBodyContent(fullHtml);

            // Inject the new body content and update panel data
            await Page.EvaluateFunctionAsync(@"(bodyContent, data) => {
                // Update the body's inner HTML with new widget content
                // This preserves <script> elements that are already loaded
                const body = document.body;
                if (body) {
                    // Find the main grid container and update only that
                    const gridContainer = body.querySelector('.grid');
                    if (gridContainer && bodyContent.includes('class=""grid')) {
                        // Extract grid content from new HTML
                        const parser = new DOMParser();
                        const doc = parser.parseFromString('<body>' + bodyContent + '</body>', 'text/html');
                        const newGrid = doc.body.querySelector('.grid');
                        if (newGrid) {
                            gridContainer.innerHTML = newGrid.innerHTML;
                        }
                    }
                }

                // Store the new data globally
                window.panelData = data;

                // Dispatch a custom event for components that listen for data changes
                window.dispatchEvent(new CustomEvent('panelDataUpdated', { detail: data }));
            }", bodyContent, dataModel);
        }
        catch (Exception ex)
        {
            _staticLogger?.LogDebug(ex, "Failed to inject HTML content for panel {PanelId}", PanelId);
        }
    }

    /// <summary>
    /// Extracts the body content from a full HTML document.
    /// </summary>
    private static string ExtractBodyContent(string fullHtml)
    {
        // Use pre-compiled regex (M006 fix)
        var bodyStartMatch = BodyStartRegex.Match(fullHtml);

        if (!bodyStartMatch.Success)
            return fullHtml;

        var bodyStart = bodyStartMatch.Index + bodyStartMatch.Length;
        var bodyEnd = fullHtml.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);

        if (bodyEnd < bodyStart)
            return fullHtml;

        return fullHtml[bodyStart..bodyEnd];
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Early exit if disposed or no page (can happen during reload)
        var page = Page;
        if (page == null || _disposed)
        {
            return _lastFrame?.Clone() ?? new Image<Rgba32>(width, height, Color.Black);
        }

        // Update viewport if dimensions changed
        if (width != TargetWidth || height != TargetHeight)
        {
            TargetWidth = width;
            TargetHeight = height;

            try
            {
                await page.SetViewportAsync(new ViewPortOptions
                {
                    Width = width,
                    Height = height,
                    DeviceScaleFactor = 1
                });
            }
            catch (ObjectDisposedException)
            {
                // Panel disposed during viewport update
                return _lastFrame?.Clone() ?? new Image<Rgba32>(width, height, Color.Black);
            }
        }

        // Check if we need to refresh DATA (not screenshot)
        var now = DateTime.UtcNow;
        var elapsed = now - _lastRender;
        var isStreamMode = RenderMode == PanelRenderMode.Stream;

        // In non-stream mode, return cached frame if within refresh interval
        // In stream mode, we always need a fresh screenshot (for animations)
        var cachedFrame = _lastFrame;
        if (!isStreamMode && cachedFrame != null && elapsed < RefreshInterval)
        {
            return cachedFrame.Clone();
        }

        // Re-render template with fresh data only at refresh interval
        // (In stream mode, we still only refresh data periodically, but always capture screenshots)
        var shouldRefreshData = elapsed >= RefreshInterval || !_pageLoaded;
        if (shouldRefreshData)
        {
            try
            {
                await RenderToPageAsync(cancellationToken);
                _lastRender = now;
            }
            catch (ObjectDisposedException)
            {
                // Panel disposed during render
                return _lastFrame?.Clone() ?? new Image<Rgba32>(width, height, Color.Black);
            }
        }

        // Re-check page after async operation (could have been disposed)
        page = Page;
        if (page == null || _disposed)
        {
            return _lastFrame?.Clone() ?? new Image<Rgba32>(width, height, Color.Black);
        }

        try
        {
            // Call onDomReady hook once (before first frame)
            if (!_domReadyCalled)
            {
                await CallThemeHookAsync("onDomReady");
                await InitializePageEffectAsync();
                _domReadyCalled = true;
            }

            // Calculate deltaTime for animation hooks
            var frameNow = DateTime.UtcNow;
            var deltaTime = _lastFrameTime == default ? 0.016 : (frameNow - _lastFrameTime).TotalSeconds;
            _lastFrameTime = frameNow;

            // Call onBeforeRender hooks before each screenshot (with deltaTime for animations)
            await CallThemeHookAsync("onBeforeRender");
            await CallPageEffectHookAsync("onBeforeRender", deltaTime);

            var screenshotData = await page.ScreenshotDataAsync(new ScreenshotOptions
            {
                Type = ScreenshotType.Png,
                FullPage = false
            });

            _lastFrame?.Dispose();
            _lastFrame = Image.Load<Rgba32>(screenshotData);

            // Call onAfterRender hook after screenshot
            await CallPageEffectHookAsync("onAfterRender", deltaTime);

            return _lastFrame.Clone();
        }
        catch (ObjectDisposedException)
        {
            // Panel disposed during screenshot
            return _lastFrame?.Clone() ?? new Image<Rgba32>(width, height, Color.Black);
        }
    }

    /// <summary>
    /// Calls a theme lifecycle hook if it exists.
    /// </summary>
    /// <param name="hookName">The hook name (onDomReady, onBeforeRender, onTransitionEnd)</param>
    protected async Task CallThemeHookAsync(string hookName)
    {
        if (Page == null) return;

        try
        {
            await Page.EvaluateFunctionAsync(@"(hookName) => {
                if (window.LCDTheme && typeof window.LCDTheme[hookName] === 'function') {
                    window.LCDTheme[hookName]();
                }
            }", hookName);
        }
        catch
        {
            // Ignore errors from theme hooks - they shouldn't break rendering
        }
    }

    /// <summary>
    /// Notifies the theme that a transition has completed.
    /// Call this from slideshow managers after panel transitions.
    /// </summary>
    public async Task NotifyTransitionEndAsync()
    {
        await CallThemeHookAsync("onTransitionEnd");
    }

    /// <summary>
    /// Calls a page effect lifecycle hook if it exists.
    /// </summary>
    /// <param name="hookName">The hook name (onInit, onValueChange, onBeforeRender, onAfterRender, onWarning, onDestroy)</param>
    /// <param name="args">Optional arguments to pass to the hook.</param>
    protected async Task CallPageEffectHookAsync(string hookName, object? args = null)
    {
        if (Page == null || _currentPageEffect == null) return;

        try
        {
            if (args != null)
            {
                await Page.EvaluateFunctionAsync(@"(hookName, args) => {
                    if (window.LCDEffect && typeof window.LCDEffect[hookName] === 'function') {
                        window.LCDEffect[hookName](args);
                    }
                }", hookName, args);
            }
            else
            {
                await Page.EvaluateFunctionAsync(@"(hookName) => {
                    if (window.LCDEffect && typeof window.LCDEffect[hookName] === 'function') {
                        window.LCDEffect[hookName]();
                    }
                }", hookName);
            }
        }
        catch
        {
            // Ignore errors from effect hooks - they shouldn't break rendering
        }
    }

    /// <summary>
    /// Initializes the page effect with optional configuration.
    /// Called after DOM is ready.
    /// </summary>
    protected async Task InitializePageEffectAsync()
    {
        if (_currentPageEffect == null) return;

        var debug = Environment.GetEnvironmentVariable("LCDPOSSIBLE_DEBUG") == "1";

        var options = new Dictionary<string, object>
        {
            ["effectId"] = _currentPageEffect.Id,
            ["effectName"] = _currentPageEffect.DisplayName
        };

        // Merge default options from effect definition
        foreach (var kvp in _currentPageEffect.DefaultOptions)
        {
            options[kvp.Key] = kvp.Value;
        }

        _staticLogger?.LogDebug("[{PanelId}] Calling effect onInit for '{EffectId}'", PanelId, _currentPageEffect.Id);
        if (_currentPageEffect.DefaultOptions.Count > 0)
        {
            _staticLogger?.LogDebug("[{PanelId}]   Options: {Options}", PanelId, string.Join(", ", options.Select(kv => $"{kv.Key}={kv.Value}")));
        }

        await CallPageEffectHookAsync("onInit", options);

        _staticLogger?.LogDebug("[{PanelId}] Effect '{EffectId}' initialized", PanelId, _currentPageEffect.Id);
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lastFrame?.Dispose();
        _lastFrame = null;
        _pageLoaded = false;
        _domReadyCalled = false;

        if (Page != null)
        {
            try
            {
                Page.CloseAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                // H012 fix: Log instead of swallow
                _staticLogger?.LogDebug(ex, "Error closing browser page during disposal for panel {PanelId}", PanelId);
            }
            Page = null;
        }

        try
        {
            ReleaseBrowserAsync().Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            // H012 fix: Log instead of swallow
            _staticLogger?.LogDebug(ex, "Timeout or error releasing browser during disposal for panel {PanelId}", PanelId);
        }

        base.Dispose();
    }

    /// <summary>
    /// Asynchronously disposes of browser resources.
    /// Preferred over <see cref="Dispose"/> to avoid sync-over-async blocking.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _lastFrame?.Dispose();
        _lastFrame = null;
        _pageLoaded = false;
        _domReadyCalled = false;

        if (Page != null)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await Page.CloseAsync().WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _staticLogger?.LogDebug("Timeout closing browser page during async disposal for panel {PanelId}", PanelId);
            }
            catch (Exception ex)
            {
                _staticLogger?.LogDebug(ex, "Error closing browser page during async disposal for panel {PanelId}", PanelId);
            }
            Page = null;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await ReleaseBrowserAsync().WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _staticLogger?.LogDebug("Timeout releasing browser during async disposal for panel {PanelId}", PanelId);
        }
        catch (Exception ex)
        {
            _staticLogger?.LogDebug(ex, "Error releasing browser during async disposal for panel {PanelId}", PanelId);
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
