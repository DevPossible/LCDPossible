using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;

namespace LCDPossible.Core.Services;

/// <summary>
/// Registry for panel types and creation.
/// </summary>
public interface IPanelRegistry
{
    /// <summary>
    /// Get all registered panel types.
    /// </summary>
    IReadOnlyList<PanelTypeInfo> GetPanelTypes();

    /// <summary>
    /// Get panel types by category.
    /// </summary>
    IReadOnlyList<PanelTypeInfo> GetPanelTypesByCategory(string category);

    /// <summary>
    /// Resolve a panel type ID (handles wildcards, aliases).
    /// </summary>
    PanelTypeInfo? ResolvePanelType(string typeIdOrAlias);

    /// <summary>
    /// Create a panel by type ID.
    /// </summary>
    IDisplayPanel? CreatePanel(string typeId, PanelCreationContext? context = null);
}
