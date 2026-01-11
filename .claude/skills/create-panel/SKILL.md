---
name: create-panel
description: |
  Creates new display panels for LCDPossible LCD controller.
  Use when: creating a new panel, adding display panel, implementing panel type,
  new LCD screen content, new monitoring panel, new info panel.
  Guides through plugin vs core decision, consistent styling, and registration.
allowed-tools: Read, Write, Edit, Glob, Grep, Bash, AskUserQuestion
---

# Create Panel Skill

Guide for creating new display panels in LCDPossible with consistent styling and proper architecture.

## Decision: Plugin vs Core

Before creating a panel, determine where it should live:

### Create as Plugin When:
- Panel requires **external dependencies** (NuGet packages, native libraries)
- Panel is **optional functionality** most users won't need
- Panel integrates with **external services** (APIs, databases, specific hardware)
- Example: Video playback (LibVLC), Web rendering (PuppeteerSharp), Proxmox integration

### Add to Core Plugin When:
- Panel uses only **standard .NET libraries** (System.*, no extra NuGet)
- Panel is **generally useful** to most users
- Panel displays **system information** available on all platforms
- Example: CPU info, RAM usage, Network info, Disk usage

## File Locations

### Core Plugin Panel
```
src/Plugins/LCDPossible.Plugins.Core/Panels/{PanelName}Panel.cs
```

### New Plugin (for external dependencies)
```
src/Plugins/LCDPossible.Plugins.{PluginName}/
├── {PluginName}Plugin.cs          # IPanelPlugin implementation
├── Panels/
│   └── {PanelName}Panel.cs        # Panel implementation
└── LCDPossible.Plugins.{PluginName}.csproj
```

## Panel Implementation Template

```csharp
using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.{PluginName}.Panels;

/// <summary>
/// {Description of what this panel displays}.
/// </summary>
public sealed class {PanelName}Panel : BaseLivePanel
{
    // Inject dependencies via constructor if needed
    private readonly ISystemInfoProvider? _provider;

    public override string PanelId => "{panel-id}";  // lowercase-with-hyphens
    public override string DisplayName => "{Panel Display Name}";

    // Set to true only if panel manages its own frame timing (GIF, video)
    public override bool IsAnimated => false;

    public {PanelName}Panel(/* dependencies */)
    {
        // Initialize fields
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(
        int width, int height,
        CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);

        image.Mutate(ctx =>
        {
            if (!FontsLoaded)
            {
                DrawCenteredText(ctx, "Loading...", width / 2f, height / 2f,
                    TitleFont!, SecondaryTextColor);
                return;
            }

            // Render panel content here
            // See "Standard Layout Patterns" below
        });

        return Task.FromResult(image);
    }
}
```

## Standard Layout Patterns

### Single Column Info Panel
```
┌─────────────────────────────────────────────────────┐
│  TITLE                                              │  <- AccentColor, TitleFont
│  Subtitle or description                            │  <- SecondaryTextColor, SmallFont
│                                                     │
│  Label: Value                                       │  <- Label: SecondaryTextColor
│  Label: Value                                       │  <- Value: PrimaryTextColor
│  Label: Value                                       │
│                                                     │
│  [Progress Bar ████████░░░░░░░░░░░░░░░]            │
│                                                     │
│                                          HH:MM:SS  │  <- DrawTimestamp()
└─────────────────────────────────────────────────────┘
```

### Multi-Column Layout (2-3 columns)
```
┌─────────────────────────────────────────────────────┐
│  SECTION 1          │  SECTION 2          │  SEC 3 │
│  ──────────         │  ──────────         │  ───── │
│  Value              │  Value              │  Value │
│  [Bar]              │  [Bar]              │  [Bar] │
└─────────────────────────────────────────────────────┘
```

Code for multi-column:
```csharp
var colWidth = (width - 40) / 3;  // 3 columns with margins
var col1X = 20;
var col2X = 20 + colWidth;
var col3X = 20 + colWidth * 2;
```

### Graphic Panel with Large Value
```
┌─────────────────────────────────────────────────────┐
│                                                     │
│                      72%                            │  <- ValueFont, GetUsageColor()
│                    42°C                             │  <- TitleFont, GetTemperatureColor()
│                                                     │
│  [═══════════════════════████████████████████]     │  <- DrawProgressBar()
│                                                     │
└─────────────────────────────────────────────────────┘
```

## Available Drawing Methods (from BaseLivePanel)

### Text Drawing
```csharp
// Left-aligned text
DrawText(ctx, "Text", x, y, Font, Color, maxWidth);

// Center-aligned text
DrawCenteredText(ctx, "Text", centerX, y, Font, Color);

// Truncate long text
var truncated = TruncateText(longText, maxChars);
```

### Progress Bars
```csharp
// Horizontal progress bar (auto-colors by percentage)
DrawProgressBar(ctx, percentage, x, y, width, height);
DrawProgressBar(ctx, percentage, x, y, width, height, customColor);

// Vertical bar
DrawVerticalBar(ctx, percentage, x, y, width, height);
```

### Color Helpers
```csharp
// Usage-based color (green -> yellow -> red)
var color = GetUsageColor(percentage);  // 0-100

// Temperature-based color (cool -> warm -> hot)
var color = GetTemperatureColor(celsius);

// Timestamp in bottom-right corner
DrawTimestamp(ctx, width, height);
```

### Available Fonts
| Font | Size | Use For |
|------|------|---------|
| `TitleFont` | 36pt Bold | Section headers, labels |
| `ValueFont` | 72pt Bold | Large percentage values |
| `LabelFont` | 24pt Regular | Field labels, descriptions |
| `SmallFont` | 18pt Regular | Secondary info, timestamps |

### Available Colors (from ColorScheme)
| Property | Use For |
|----------|---------|
| `PrimaryTextColor` | Main text, values |
| `SecondaryTextColor` | Labels, descriptions |
| `AccentColor` | Headers, highlights |
| `WarningColor` | Warning states (70-90%) |
| `CriticalColor` | Critical states (>90%) |
| `SuccessColor` | Good/success states |
| `BackgroundColor` | Panel background |

## Registration Checklist

> **CRITICAL**: All 4 registration steps must be completed. Missing any step will cause the panel to fail silently!

### 1. Add to plugin.json Manifest (REQUIRED with Full Metadata)
In `{PluginName}/plugin.json`, add to the `panelTypes` array:

```json
{
  "typeId": "{panel-id}",
  "displayName": "{Panel Name}",
  "description": "{Brief description}",
  "category": "System|CPU|GPU|Memory|Network|Storage|Thermal|Proxmox|Media|Web|Screensaver",
  "isLive": true,
  "isAnimated": false,
  "helpText": "Detailed help text explaining:\n- What the panel displays\n- Features and capabilities\n- Any configuration options\n- Platform requirements",
  "examples": [
    {
      "command": "lcdpossible show {panel-id}",
      "description": "Display the panel"
    },
    {
      "command": "lcdpossible show {panel-id}|@duration=30",
      "description": "Display for 30 seconds"
    }
  ],
  "parameters": [
    {
      "name": "{param-name}",
      "description": "Description of this parameter",
      "required": false,
      "defaultValue": "{default}",
      "exampleValues": ["value1", "value2"]
    }
  ],
  "dependencies": ["Optional list of NuGet packages or features required"]
}
```

> **Why this is critical**: The plugin manager reads panel types from `plugin.json` to discover available panels. The help system uses this metadata to display panel information via `lcdpossible list-panels` and `lcdpossible help-panel {id}`.

### Required Metadata Fields
| Field | Required | Description |
|-------|----------|-------------|
| `typeId` | Yes | Panel identifier (lowercase-with-hyphens) |
| `displayName` | Yes | Human-readable name |
| `description` | Yes | One-line description (shown in list-panels) |
| `category` | Yes | Grouping category for organization |
| `isLive` | Yes | true if panel updates in real-time |
| `isAnimated` | No | true if panel manages own frame timing (GIF, video) |
| `helpText` | Yes | Multi-line detailed help (shown in help-panel) |
| `examples` | Yes | At least one usage example |
| `parameters` | Conditional | Required for parameterized panels (prefix:value format) |
| `dependencies` | No | External dependencies for documentation |

### Parameterized Panel Pattern
For panels that accept arguments (like `video:path`, `web:url`):

```json
{
  "typeId": "{panel-id}",
  "prefixPattern": "{panel-id}:",
  "parameters": [
    {
      "name": "source",
      "description": "Path to file, URL, or other source",
      "required": true,
      "exampleValues": ["C:\\path\\file.ext", "https://example.com/resource"]
    }
  ]
}
```

### 2. Add PanelTypeInfo to Plugin Class
In `{PluginName}Plugin.cs`, add to `PanelTypes` dictionary:

```csharp
["{panel-id}"] = new PanelTypeInfo
{
    TypeId = "{panel-id}",
    DisplayName = "{Panel Name}",
    Description = "{Brief description for CLI help}",
    Category = "{System|Thermal|Media|Network|Custom}",
    IsLive = true  // true for real-time data panels
}
```

### 3. Add Case to CreatePanel
In `{PluginName}Plugin.cs`, add to `CreatePanel` switch:

```csharp
"{panel-id}" => new {PanelName}Panel(/* dependencies */),
```

### 4. Update Default Profile (if appropriate)
In `DisplayProfile.CreateDefault()`, add new slide:

```csharp
new SlideDefinition { Panel = "{panel-id}" }
```

### 5. Update Documentation

Update `CLAUDE.md` Available Panel Types table:
```markdown
| `{panel-id}` | {Brief description} |
```

## Creating a New Plugin

If the panel needs external dependencies, create a new plugin:

### 1. Create Project Structure
```powershell
mkdir src/Plugins/LCDPossible.Plugins.{Name}
mkdir src/Plugins/LCDPossible.Plugins.{Name}/Panels
```

### 2. Create .csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\LCDPossible.Sdk\LCDPossible.Sdk.csproj" />
    <ProjectReference Include="..\..\LCDPossible.Core\LCDPossible.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Add external NuGet dependencies here -->
  </ItemGroup>
</Project>
```

### 3. Create Plugin Class
```csharp
using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;

namespace LCDPossible.Plugins.{Name};

public sealed class {Name}Plugin : IPanelPlugin
{
    public string PluginId => "lcdpossible.{name}";
    public string DisplayName => "{Name} Panels";
    public Version Version => new(1, 0, 0);
    public string Author => "LCDPossible Team";
    public Version MinimumSdkVersion => new(1, 0, 0);

    public IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes { get; } =
        new Dictionary<string, PanelTypeInfo>
    {
        // Panel type definitions
    };

    public Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
    {
        // Initialize plugin resources
        return Task.CompletedTask;
    }

    public IDisplayPanel? CreatePanel(string panelTypeId, PanelCreationContext context)
    {
        return panelTypeId.ToLowerInvariant() switch
        {
            // Panel creation
            _ => null
        };
    }

    public void Dispose() { }
}
```

### 4. Add to Solution
```powershell
dotnet sln src/LCDPossible.sln add src/Plugins/LCDPossible.Plugins.{Name}
```

### 5. Reference from Main Project
Add to `src/LCDPossible/LCDPossible.csproj`:
```xml
<ProjectReference Include="..\Plugins\LCDPossible.Plugins.{Name}\LCDPossible.Plugins.{Name}.csproj" />
```

## Workflow Summary

1. **Ask**: Plugin or Core? (based on dependencies)
2. **Create**: Panel class extending `BaseLivePanel`
3. **Implement**: `RenderFrameAsync` with consistent styling
4. **Register** (ALL REQUIRED):
   - Add to `plugin.json` manifest (**CRITICAL** - panel discovery depends on this!)
   - Add to plugin's `PanelTypes` dictionary in code
   - Add case to `CreatePanel` switch statement
5. **Document**: Update CLAUDE.md panel table
6. **Test**: Build and verify with `./start-app.ps1 show {panel-id}`
7. **Profile**: Optionally add to default profile

## Example Panels for Reference

| Panel | Location | Pattern |
|-------|----------|---------|
| BasicInfoPanel | Core/Panels/BasicInfoPanels.cs | 3-column layout |
| CpuUsageGraphicPanel | Core/Panels/CpuPanels.cs | Large value + bars |
| NetworkInfoPanel | Core/Panels/NetworkInfoPanel.cs | 2-column info |
| VideoPanel | Video/Panels/VideoPanel.cs | Animated media |
