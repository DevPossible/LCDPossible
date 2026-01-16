# Plugin Development

LCDPossible uses a plugin architecture for both display panels and device drivers. This guide covers creating your own plugins.

## Plugin Types

| Type | Purpose | Interface |
|------|---------|-----------|
| Panel Plugin | Display content on LCD | `IPanelPlugin` |
| Device Plugin | Support new LCD hardware | `IDevicePlugin` |

## Quick Start

### Creating a Panel Plugin

1. Create a new .NET class library targeting `net10.0`
2. Reference `LCDPossible.Sdk`
3. Implement `IPanelPlugin`
4. Create a `plugin.json` manifest

```csharp
public class MyPlugin : IPanelPlugin
{
    public string PluginId => "my-plugin";
    public string DisplayName => "My Custom Panels";

    public IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes => new Dictionary<string, PanelTypeInfo>
    {
        ["my-panel"] = new PanelTypeInfo
        {
            TypeId = "my-panel",
            DisplayName = "My Panel",
            Category = "Custom"
        }
    };

    public IDisplayPanel? CreatePanel(string typeId, PanelCreationContext context)
    {
        return typeId switch
        {
            "my-panel" => new MyPanel(context),
            _ => null
        };
    }
}
```

See [Creating Panels](creating-panels.md) for detailed guide.

### Creating a Device Plugin

1. Create a new .NET class library targeting `net10.0`
2. Reference `LCDPossible.Core`
3. Implement `IDevicePlugin`
4. Create a `plugin.json` manifest with `"type": "device"`

See [Creating Devices](creating-devices.md) for detailed guide.

## Topics

- [Creating Panels](creating-panels.md) - Panel development guide
- [Creating Devices](creating-devices.md) - Device driver development
- [Plugin Manifest](plugin-manifest.md) - plugin.json reference
- [SDK Reference](sdk-reference.md) - Base classes and utilities

## Panel Base Classes

| Class | Use Case |
|-------|----------|
| `WidgetPanel` | Grid-based layouts with web components |
| `HtmlPanel` | Custom HTML templates |
| `CanvasPanel` | Direct pixel drawing (screensavers, effects) |

## Plugin Manifest

Every plugin requires a `plugin.json` file:

```json
{
  "id": "my-plugin",
  "type": "panels",
  "name": "My Custom Panels",
  "version": "1.0.0",
  "description": "Custom panels for LCDPossible",
  "assemblyName": "MyPlugin.dll",
  "panelTypes": [
    {
      "typeId": "my-panel",
      "displayName": "My Panel",
      "description": "A custom panel",
      "category": "Custom",
      "isLive": true,
      "isAnimated": false
    }
  ]
}
```

## Plugin Locations

Plugins are loaded from:

| Platform | System Plugins | User Plugins |
|----------|----------------|--------------|
| Windows | `{app}/plugins/` | `%APPDATA%\LCDPossible\plugins\` |
| Linux | `{app}/plugins/` | `~/.local/share/lcdpossible/plugins/` |
| macOS | `{app}/plugins/` | `~/Library/Application Support/LCDPossible/plugins/` |

---

*[Back to Documentation](../README.md)*
