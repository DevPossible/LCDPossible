using LCDPossible.Core.Configuration;
using LCDPossible.Core.Monitoring;
using LCDPossible.Core.Rendering;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Panels;

/// <summary>
/// Factory for creating display panels by type ID.
/// </summary>
public sealed class PanelFactory
{
    private readonly ISystemInfoProvider _systemProvider;
    private readonly IProxmoxProvider? _proxmoxProvider;
    private readonly ILogger<PanelFactory>? _logger;
    private ResolvedColorScheme _colorScheme = ResolvedColorScheme.CreateDefault();

    /// <summary>
    /// List of all available panel type IDs.
    /// </summary>
    public static readonly string[] AvailablePanels =
    [
        "cpu-info",
        "cpu-usage-text",
        "cpu-usage-graphic",
        "ram-info",
        "ram-usage-text",
        "ram-usage-graphic",
        "gpu-info",
        "gpu-usage-text",
        "gpu-usage-graphic",
        "basic-info",
        "basic-usage-text",
        "proxmox-summary",
        "proxmox-vms"
    ];

    public PanelFactory(
        ISystemInfoProvider systemProvider,
        IProxmoxProvider? proxmoxProvider = null,
        ILogger<PanelFactory>? logger = null)
    {
        _systemProvider = systemProvider ?? throw new ArgumentNullException(nameof(systemProvider));
        _proxmoxProvider = proxmoxProvider;
        _logger = logger;
    }

    /// <summary>
    /// Sets the color scheme used for all created panels.
    /// </summary>
    public void SetColorScheme(ResolvedColorScheme colorScheme)
    {
        _colorScheme = colorScheme ?? ResolvedColorScheme.CreateDefault();
    }

    /// <summary>
    /// Sets the color scheme from a ColorScheme configuration.
    /// </summary>
    public void SetColorScheme(ColorScheme colorScheme)
    {
        _colorScheme = colorScheme?.Resolve() ?? ResolvedColorScheme.CreateDefault();
    }

    /// <summary>
    /// Creates a panel instance by type ID.
    /// </summary>
    /// <param name="panelTypeId">The panel type identifier.</param>
    /// <returns>The panel instance, or null if type is unknown.</returns>
    public IDisplayPanel? CreatePanel(string panelTypeId)
    {
        var normalizedId = panelTypeId.ToLowerInvariant().Trim();

        BaseLivePanel? panel = normalizedId switch
        {
            "cpu-info" => new CpuInfoPanel(_systemProvider),
            "cpu-usage-text" => new CpuUsageTextPanel(_systemProvider),
            "cpu-usage-graphic" => new CpuUsageGraphicPanel(_systemProvider),

            "ram-info" => new RamInfoPanel(_systemProvider),
            "ram-usage-text" => new RamUsageTextPanel(_systemProvider),
            "ram-usage-graphic" => new RamUsageGraphicPanel(_systemProvider),

            "gpu-info" => new GpuInfoPanel(_systemProvider),
            "gpu-usage-text" => new GpuUsageTextPanel(_systemProvider),
            "gpu-usage-graphic" => new GpuUsageGraphicPanel(_systemProvider),

            "basic-info" => new BasicInfoPanel(_systemProvider),
            "basic-usage-text" => new BasicUsageTextPanel(_systemProvider),

            "proxmox-summary" when _proxmoxProvider != null => new ProxmoxSummaryPanel(_proxmoxProvider),
            "proxmox-vms" when _proxmoxProvider != null => new ProxmoxVmsPanel(_proxmoxProvider),

            _ => null
        };

        if (panel != null)
        {
            panel.SetColorScheme(_colorScheme);
            return panel;
        }

        return HandleUnknownPanel(normalizedId);
    }

    private IDisplayPanel? HandleUnknownPanel(string panelTypeId)
    {
        _logger?.LogWarning("Unknown panel type: {PanelType}", panelTypeId);

        if (panelTypeId.StartsWith("proxmox") && _proxmoxProvider == null)
        {
            _logger?.LogWarning("Proxmox panel requested but Proxmox provider is not available");
        }

        return null;
    }
}
