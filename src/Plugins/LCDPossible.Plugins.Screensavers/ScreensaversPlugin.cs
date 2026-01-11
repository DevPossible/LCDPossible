using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;
using LCDPossible.Plugins.Screensavers.Panels;

namespace LCDPossible.Plugins.Screensavers;

/// <summary>
/// Plugin providing animated screensaver panels.
/// </summary>
public sealed class ScreensaversPlugin : IPanelPlugin
{
    public string PluginId => "lcdpossible.screensavers";
    public string DisplayName => "Screensaver Panels";
    public Version Version => new(1, 0, 0);
    public string Author => "LCDPossible Team";
    public Version MinimumSdkVersion => new(1, 0, 0);

    public IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes { get; } = new Dictionary<string, PanelTypeInfo>
    {
        ["screensaver"] = new PanelTypeInfo { TypeId = "screensaver", DisplayName = "Screensaver", Description = "Random screensaver effect", Category = "Screensaver", IsLive = true },
        ["starfield"] = new PanelTypeInfo { TypeId = "starfield", DisplayName = "Starfield", Description = "Classic starfield warp effect", Category = "Screensaver", IsLive = true },
        ["matrix-rain"] = new PanelTypeInfo { TypeId = "matrix-rain", DisplayName = "Matrix Rain", Description = "Digital rain effect", Category = "Screensaver", IsLive = true },
        ["bouncing-logo"] = new PanelTypeInfo { TypeId = "bouncing-logo", DisplayName = "Bouncing Logo", Description = "Bouncing logo screensaver", Category = "Screensaver", IsLive = true },
        ["mystify"] = new PanelTypeInfo { TypeId = "mystify", DisplayName = "Mystify", Description = "Bouncing connected polygons", Category = "Screensaver", IsLive = true },
        ["plasma"] = new PanelTypeInfo { TypeId = "plasma", DisplayName = "Plasma", Description = "Classic plasma effect", Category = "Screensaver", IsLive = true },
        ["fire"] = new PanelTypeInfo { TypeId = "fire", DisplayName = "Fire", Description = "Classic fire effect", Category = "Screensaver", IsLive = true },
        ["game-of-life"] = new PanelTypeInfo { TypeId = "game-of-life", DisplayName = "Game of Life", Description = "Conway's cellular automaton", Category = "Screensaver", IsLive = true },
        ["bubbles"] = new PanelTypeInfo { TypeId = "bubbles", DisplayName = "Bubbles", Description = "Floating bubbles", Category = "Screensaver", IsLive = true },
        ["rain"] = new PanelTypeInfo { TypeId = "rain", DisplayName = "Rain", Description = "Falling raindrops", Category = "Screensaver", IsLive = true },
        ["spiral"] = new PanelTypeInfo { TypeId = "spiral", DisplayName = "Spiral", Description = "Hypnotic spiral pattern", Category = "Screensaver", IsLive = true },
        ["clock"] = new PanelTypeInfo { TypeId = "clock", DisplayName = "Clock", Description = "Analog clock", Category = "Screensaver", IsLive = true },
        ["noise"] = new PanelTypeInfo { TypeId = "noise", DisplayName = "Static", Description = "TV static effect", Category = "Screensaver", IsLive = true },
        ["warp-tunnel"] = new PanelTypeInfo { TypeId = "warp-tunnel", DisplayName = "Warp Tunnel", Description = "Flying through a tunnel", Category = "Screensaver", IsLive = true },
        ["pipes"] = new PanelTypeInfo { TypeId = "pipes", DisplayName = "Pipes", Description = "3D pipes (classic Windows)", Category = "Screensaver", IsLive = true },
        ["asteroids"] = new PanelTypeInfo { TypeId = "asteroids", DisplayName = "Asteroids", Description = "Asteroids game simulation", Category = "Screensaver", IsLive = true },
        ["missile-command"] = new PanelTypeInfo { TypeId = "missile-command", DisplayName = "Missile Command", Description = "Defend cities from missiles", Category = "Screensaver", IsLive = true },
        ["falling-blocks"] = new PanelTypeInfo { TypeId = "falling-blocks", DisplayName = "Falling Blocks", Description = "Tetris-style falling blocks with AI", Category = "Screensaver", IsLive = true }
    };

    // Available screensaver types for random selection
    private static readonly string[] ScreensaverTypes =
    [
        "starfield", "matrix-rain", "bouncing-logo", "mystify", "plasma",
        "fire", "game-of-life", "bubbles", "rain", "spiral",
        "clock", "noise", "warp-tunnel", "pipes", "asteroids",
        "missile-command", "falling-blocks"
    ];

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public IDisplayPanel? CreatePanel(string panelTypeId, PanelCreationContext context)
    {
        var typeId = panelTypeId.ToLowerInvariant();

        // Handle "screensaver" or "screensaver:type" format
        if (typeId == "screensaver" || typeId.StartsWith("screensaver:"))
        {
            var specificType = typeId == "screensaver" ? "random" : typeId[12..];

            // Empty parameter means random (e.g., "screensaver:")
            if (string.IsNullOrEmpty(specificType) || specificType == "random")
            {
                // Pick a random screensaver
                var random = new Random();
                specificType = ScreensaverTypes[random.Next(ScreensaverTypes.Length)];
            }

            return CreateScreensaverPanel(specificType, context);
        }

        return CreateScreensaverPanel(typeId, context);
    }

    private static IDisplayPanel? CreateScreensaverPanel(string typeId, PanelCreationContext context)
    {
        // Normalize: strip trailing colon if no parameter provided (e.g., "falling-blocks:" -> "falling-blocks")
        var colonIndex = typeId.IndexOf(':');
        if (colonIndex >= 0 && colonIndex == typeId.Length - 1)
        {
            typeId = typeId[..colonIndex];
        }

        // Handle falling-blocks with player count parameter (e.g., "falling-blocks:2")
        if (typeId.StartsWith("falling-blocks:") || typeId.StartsWith("fallingblocks:") ||
            typeId.StartsWith("tetris:") || typeId.StartsWith("blocks:"))
        {
            colonIndex = typeId.IndexOf(':');
            var param = typeId[(colonIndex + 1)..];
            if (int.TryParse(param, out var playerCount))
            {
                var blocksPanel = new FallingBlocksPanel();
                blocksPanel.SetPlayerCount(playerCount);
                ApplyColorScheme(blocksPanel, context);
                return blocksPanel;
            }
        }

        // Handle bouncing-logo with parameters (e.g., "bouncing-logo:CustomText")
        if (typeId.StartsWith("bouncing-logo:") || typeId.StartsWith("bouncing:") || typeId.StartsWith("dvd:"))
        {
            var panel = CreateBouncingLogoPanel(typeId, context);
            ApplyColorScheme(panel, context);
            return panel;
        }

        // Handle base bouncing-logo with settings from context
        if (typeId is "bouncing-logo" or "bouncing" or "dvd")
        {
            var panel = CreateBouncingLogoPanel(typeId, context);
            ApplyColorScheme(panel, context);
            return panel;
        }

        IDisplayPanel? basePanel = typeId switch
        {
            "starfield" => new StarfieldPanel(),
            "matrix-rain" or "matrix" => new MatrixRainPanel(),
            "mystify" => new MystifyPanel(),
            "plasma" => new PlasmaPanel(),
            "fire" => new FirePanel(),
            "game-of-life" or "gameoflife" or "life" => new GameOfLifePanel(),
            "bubbles" => new BubblesPanel(),
            "rain" => new RainPanel(),
            "spiral" => new SpiralPanel(),
            "clock" => new ClockPanel(),
            "noise" or "static" => new NoisePanel(),
            "warp-tunnel" or "tunnel" or "warp" => new WarpTunnelPanel(),
            "pipes" => new PipesPanel(),
            "asteroids" => new AsteroidsPanel(),
            "missile-command" or "missilecommand" or "missile" => new MissileCommandPanel(),
            "falling-blocks" or "fallingblocks" or "tetris" or "blocks" => new FallingBlocksPanel(),
            _ => null
        };

        ApplyColorScheme(basePanel, context);
        return basePanel;
    }

    private static BouncingLogoPanel CreateBouncingLogoPanel(string typeId, PanelCreationContext context)
    {
        var panel = new BouncingLogoPanel();

        // Extract text from colon syntax (e.g., "bouncing-logo:MyText")
        var colonIndex = typeId.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < typeId.Length - 1)
        {
            var text = typeId[(colonIndex + 1)..];
            if (!string.IsNullOrWhiteSpace(text))
            {
                panel.SetText(text);
            }
        }

        // Apply settings from context (for profile-based configuration)
        if (context.Settings != null)
        {
            // Text parameter
            if (context.Settings.TryGetValue("text", out var textValue) && !string.IsNullOrWhiteSpace(textValue))
            {
                panel.SetText(textValue);
            }

            // Color parameter: "random", "cycle", "rainbow", or color value
            if (context.Settings.TryGetValue("color", out var colorValue) && !string.IsNullOrWhiteSpace(colorValue))
            {
                switch (colorValue.ToLowerInvariant())
                {
                    case "random":
                        panel.SetColorRandom();
                        break;
                    case "cycle":
                        panel.SetColorCycle();
                        break;
                    case "rainbow":
                        panel.SetColorRainbow();
                        break;
                    default:
                        panel.SetFixedColor(colorValue);
                        break;
                }
            }

            // Size parameter: "small", "medium", "large"
            if (context.Settings.TryGetValue("size", out var sizeValue) && !string.IsNullOrWhiteSpace(sizeValue))
            {
                var size = sizeValue.ToLowerInvariant() switch
                {
                    "small" or "s" => BouncingLogoPanel.SizePreset.Small,
                    "medium" or "m" => BouncingLogoPanel.SizePreset.Medium,
                    "large" or "l" or "big" => BouncingLogoPanel.SizePreset.Large,
                    _ => BouncingLogoPanel.SizePreset.Medium
                };
                panel.SetSize(size);
            }

            // 3D parameter
            if (context.Settings.TryGetValue("3d", out var is3dValue))
            {
                var is3d = is3dValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                           is3dValue.Equals("1", StringComparison.Ordinal) ||
                           is3dValue.Equals("yes", StringComparison.OrdinalIgnoreCase);
                panel.Set3D(is3d);
            }

            // Rotate parameter
            if (context.Settings.TryGetValue("rotate", out var rotateValue))
            {
                var rotate = rotateValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                             rotateValue.Equals("1", StringComparison.Ordinal) ||
                             rotateValue.Equals("yes", StringComparison.OrdinalIgnoreCase);
                panel.SetRotate(rotate);
            }

            // Thickness parameter (1-10, or "random")
            if (context.Settings.TryGetValue("thickness", out var thicknessValue))
            {
                if (thicknessValue.Equals("random", StringComparison.OrdinalIgnoreCase))
                {
                    panel.SetThickness(-1); // Random
                }
                else if (int.TryParse(thicknessValue, out var thickness))
                {
                    panel.SetThickness(thickness);
                }
            }

            // Speed parameter (1-10: 1=slow, 5=normal, 10=fast)
            if (context.Settings.TryGetValue("speed", out var speedValue) &&
                int.TryParse(speedValue, out var speed))
            {
                panel.SetSpeed(speed);
            }
        }

        return panel;
    }

    private static void ApplyColorScheme(IDisplayPanel? panel, PanelCreationContext context)
    {
        if (panel is LCDPossible.Sdk.BaseLivePanel livePanel && context.ColorScheme != null)
        {
            livePanel.SetColorScheme(context.ColorScheme);
        }
    }

    public void Dispose()
    {
    }
}
