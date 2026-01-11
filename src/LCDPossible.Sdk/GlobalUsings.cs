// Re-export common types from Core for plugin developer convenience
global using LCDPossible.Core.Rendering;
global using LCDPossible.Core.Monitoring;
global using LCDPossible.Core.Configuration;
global using LCDPossible.Core.Plugins;

// Expose plugin types at the LCDPossible.Sdk namespace level
global using IPanelPlugin = LCDPossible.Core.Plugins.IPanelPlugin;
global using IPluginContext = LCDPossible.Core.Plugins.IPluginContext;
global using PanelTypeInfo = LCDPossible.Core.Plugins.PanelTypeInfo;
global using PanelCreationContext = LCDPossible.Core.Plugins.PanelCreationContext;
global using SdkVersion = LCDPossible.Core.Plugins.SdkVersion;

// Common types for plugin development
global using IDisplayPanel = LCDPossible.Core.Rendering.IDisplayPanel;
global using PanelConfig = LCDPossible.Core.Rendering.PanelConfig;
global using SystemMetrics = LCDPossible.Core.Monitoring.SystemMetrics;
global using ISystemInfoProvider = LCDPossible.Core.Monitoring.ISystemInfoProvider;
global using IProxmoxProvider = LCDPossible.Core.Monitoring.IProxmoxProvider;
global using ResolvedColorScheme = LCDPossible.Core.Configuration.ResolvedColorScheme;
