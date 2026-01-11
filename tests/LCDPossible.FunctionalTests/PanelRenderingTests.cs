using LCDPossible.FunctionalTests.Helpers;

namespace LCDPossible.FunctionalTests;

/// <summary>
/// Functional tests for panel rendering using the 'test' CLI command.
/// Each test verifies that a panel type can be created and rendered to a JPEG file.
/// </summary>
public sealed class PanelRenderingTests : IDisposable
{
    private readonly CliRunner _cli;
    private readonly string _outputDir;

    public PanelRenderingTests()
    {
        _cli = new CliRunner();
        _outputDir = Path.Combine(_cli.TestDataDir, "rendered");
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        _cli.Dispose();
    }

    /// <summary>
    /// Helper to run the test command and verify output file was created.
    /// </summary>
    private void TestPanelRendering(string panelType, string? expectedFilePart = null)
    {
        // Set output directory via environment
        var env = new Dictionary<string, string>
        {
            ["USERPROFILE"] = _cli.TestDataDir,
            ["HOME"] = _cli.TestDataDir
        };

        var result = _cli.RunWithEnvironment(env, "test", panelType);

        result.ShouldSucceed();

        // Verify output mentions the panel or file was created
        if (expectedFilePart != null)
        {
            result.ShouldContainOutput(expectedFilePart);
        }

        // Verify at least one JPG file was created in the output
        result.Stdout.ShouldContain(".jpg");
    }

    #region Core Plugin Panels

    [Fact]
    public void Test_CpuInfo_RendersSuccessfully()
    {
        TestPanelRendering("cpu-info");
    }

    [Fact]
    public void Test_CpuUsageText_RendersSuccessfully()
    {
        TestPanelRendering("cpu-usage-text");
    }

    [Fact]
    public void Test_CpuUsageGraphic_RendersSuccessfully()
    {
        TestPanelRendering("cpu-usage-graphic");
    }

    [Fact]
    public void Test_CpuThermalGraphic_RendersSuccessfully()
    {
        TestPanelRendering("cpu-thermal-graphic");
    }

    [Fact]
    public void Test_GpuInfo_RendersSuccessfully()
    {
        TestPanelRendering("gpu-info");
    }

    [Fact]
    public void Test_GpuUsageText_RendersSuccessfully()
    {
        TestPanelRendering("gpu-usage-text");
    }

    [Fact]
    public void Test_GpuUsageGraphic_RendersSuccessfully()
    {
        TestPanelRendering("gpu-usage-graphic");
    }

    [Fact]
    public void Test_GpuThermalGraphic_RendersSuccessfully()
    {
        TestPanelRendering("gpu-thermal-graphic");
    }

    [Fact]
    public void Test_RamInfo_RendersSuccessfully()
    {
        TestPanelRendering("ram-info");
    }

    [Fact]
    public void Test_RamUsageText_RendersSuccessfully()
    {
        TestPanelRendering("ram-usage-text");
    }

    [Fact]
    public void Test_RamUsageGraphic_RendersSuccessfully()
    {
        TestPanelRendering("ram-usage-graphic");
    }

    [Fact]
    public void Test_BasicInfo_RendersSuccessfully()
    {
        TestPanelRendering("basic-info");
    }

    [Fact]
    public void Test_BasicUsageText_RendersSuccessfully()
    {
        TestPanelRendering("basic-usage-text");
    }

    [Fact]
    public void Test_NetworkInfo_RendersSuccessfully()
    {
        TestPanelRendering("network-info");
    }

    [Fact]
    public void Test_SystemThermalGraphic_RendersSuccessfully()
    {
        TestPanelRendering("system-thermal-graphic");
    }

    #endregion

    #region Screensaver Plugin Panels

    [Fact]
    public void Test_Starfield_RendersSuccessfully()
    {
        TestPanelRendering("starfield");
    }

    [Fact]
    public void Test_MatrixRain_RendersSuccessfully()
    {
        TestPanelRendering("matrix-rain");
    }

    [Fact]
    public void Test_BouncingLogo_RendersSuccessfully()
    {
        TestPanelRendering("bouncing-logo");
    }

    [Fact]
    public void Test_Mystify_RendersSuccessfully()
    {
        TestPanelRendering("mystify");
    }

    [Fact]
    public void Test_Plasma_RendersSuccessfully()
    {
        TestPanelRendering("plasma");
    }

    [Fact]
    public void Test_Fire_RendersSuccessfully()
    {
        TestPanelRendering("fire");
    }

    [Fact]
    public void Test_GameOfLife_RendersSuccessfully()
    {
        TestPanelRendering("game-of-life");
    }

    [Fact]
    public void Test_Bubbles_RendersSuccessfully()
    {
        TestPanelRendering("bubbles");
    }

    [Fact]
    public void Test_Rain_RendersSuccessfully()
    {
        TestPanelRendering("rain");
    }

    [Fact]
    public void Test_Spiral_RendersSuccessfully()
    {
        TestPanelRendering("spiral");
    }

    [Fact]
    public void Test_Clock_RendersSuccessfully()
    {
        TestPanelRendering("clock");
    }

    [Fact]
    public void Test_Noise_RendersSuccessfully()
    {
        TestPanelRendering("noise");
    }

    [Fact]
    public void Test_WarpTunnel_RendersSuccessfully()
    {
        TestPanelRendering("warp-tunnel");
    }

    [Fact]
    public void Test_Pipes_RendersSuccessfully()
    {
        TestPanelRendering("pipes");
    }

    [Fact]
    public void Test_Asteroids_RendersSuccessfully()
    {
        TestPanelRendering("asteroids");
    }

    [Fact]
    public void Test_MissileCommand_RendersSuccessfully()
    {
        TestPanelRendering("missile-command");
    }

    [Fact]
    public void Test_FallingBlocks_RendersSuccessfully()
    {
        TestPanelRendering("falling-blocks");
    }

    [Fact]
    public void Test_ScreensaverRandom_RendersSuccessfully()
    {
        // screensaver: picks a random screensaver
        TestPanelRendering("screensaver");
    }

    #endregion

    #region Images Plugin Panels

    [Fact]
    public void Test_AnimatedGif_WithDefault_RendersSuccessfully()
    {
        // Uses default rotating earth GIF from Wikimedia Commons
        TestPanelRendering("animated-gif:", "Rotating_earth");
    }

    [Fact]
    public void Test_ImageSequence_WithDefault_RendersSuccessfully()
    {
        // Uses auto-generated test image sequence
        TestPanelRendering("image-sequence:", "test-sequence");
    }

    #endregion

    #region Video Plugin Panels

    [Fact]
    public void Test_Video_WithDefault_RendersSuccessfully()
    {
        // Uses Big Buck Bunny from Archive.org
        TestPanelRendering("video:", "big_buck_bunny");
    }

    #endregion

    #region Web Plugin Panels

    [Fact]
    public void Test_Html_WithDefault_RendersSuccessfully()
    {
        // Uses auto-generated test HTML file
        TestPanelRendering("html:", "test-panel");
    }

    [Fact]
    public void Test_Web_WithDefault_RendersSuccessfully()
    {
        // Uses wttr.in weather page
        TestPanelRendering("web:", "wttr");
    }

    #endregion

    #region Proxmox Plugin Panels (Demo Mode)

    [Fact]
    public void Test_ProxmoxSummary_Demo_RendersSuccessfully()
    {
        // Without configuration, should render demo panel
        TestPanelRendering("proxmox-summary");
    }

    [Fact]
    public void Test_ProxmoxVms_Demo_RendersSuccessfully()
    {
        // Without configuration, should render demo panel
        TestPanelRendering("proxmox-vms");
    }

    #endregion

    #region Wildcard Tests

    [Fact]
    public void Test_WildcardAll_RendersAllPanels()
    {
        var env = new Dictionary<string, string>
        {
            ["USERPROFILE"] = _cli.TestDataDir,
            ["HOME"] = _cli.TestDataDir
        };

        var result = _cli.RunWithEnvironment(env, "test", "*");

        result.ShouldSucceed();
        result.ShouldContainOutput("Rendered");
        result.ShouldContainOutput("panel(s)");

        // Should have rendered many panels
        var jpgCount = result.Stdout.Split(".jpg").Length - 1;
        jpgCount.ShouldBeGreaterThan(30); // We have 40 panel types
    }

    [Fact]
    public void Test_WildcardCpu_RendersOnlyCpuPanels()
    {
        var env = new Dictionary<string, string>
        {
            ["USERPROFILE"] = _cli.TestDataDir,
            ["HOME"] = _cli.TestDataDir
        };

        var result = _cli.RunWithEnvironment(env, "test", "cpu-*");

        result.ShouldSucceed();
        result.ShouldContainOutput("cpu-info");
        result.ShouldContainOutput("cpu-usage");
        result.ShouldContainOutput("cpu-thermal");
        result.Stdout.ShouldNotContain("gpu-");
        result.Stdout.ShouldNotContain("ram-");
    }

    [Fact]
    public void Test_WildcardGraphic_RendersGraphicPanels()
    {
        var env = new Dictionary<string, string>
        {
            ["USERPROFILE"] = _cli.TestDataDir,
            ["HOME"] = _cli.TestDataDir
        };

        var result = _cli.RunWithEnvironment(env, "test", "*-graphic");

        result.ShouldSucceed();
        result.ShouldContainOutput("cpu-usage-graphic");
        result.ShouldContainOutput("gpu-usage-graphic");
        result.ShouldContainOutput("ram-usage-graphic");
    }

    #endregion

    #region Multiple Panel Tests

    [Fact]
    public void Test_MultiplePanels_RendersAll()
    {
        var env = new Dictionary<string, string>
        {
            ["USERPROFILE"] = _cli.TestDataDir,
            ["HOME"] = _cli.TestDataDir
        };

        var result = _cli.RunWithEnvironment(env, "test", "basic-info,cpu-info,gpu-info");

        result.ShouldSucceed();
        result.ShouldContainOutput("basic-info.jpg");
        result.ShouldContainOutput("cpu-info.jpg");
        result.ShouldContainOutput("gpu-info.jpg");
        result.ShouldContainOutput("Rendered 3 panel(s)");
    }

    #endregion

    #region Error Cases

    [Fact]
    public void Test_InvalidPanelType_Fails()
    {
        var env = new Dictionary<string, string>
        {
            ["USERPROFILE"] = _cli.TestDataDir,
            ["HOME"] = _cli.TestDataDir
        };

        var result = _cli.RunWithEnvironment(env, "test", "nonexistent-panel-type");

        result.ShouldFail();
        result.ShouldContainError("Could not create panel");
    }

    [Fact]
    public void Test_EmptyPattern_Fails()
    {
        var env = new Dictionary<string, string>
        {
            ["USERPROFILE"] = _cli.TestDataDir,
            ["HOME"] = _cli.TestDataDir
        };

        var result = _cli.RunWithEnvironment(env, "test", "nonexistent-*");

        result.ShouldFail();
        result.ShouldContainError("No panels matched");
    }

    #endregion

    #region Default Profile Test

    [Fact]
    public void Test_NoArguments_RendersDefaultPanels()
    {
        var env = new Dictionary<string, string>
        {
            ["USERPROFILE"] = _cli.TestDataDir,
            ["HOME"] = _cli.TestDataDir
        };

        var result = _cli.RunWithEnvironment(env, "test");

        result.ShouldSucceed();
        result.ShouldContainOutput("No panels specified, using default profile");
        result.ShouldContainOutput("Rendered 4 panel(s)");
        result.ShouldContainOutput("basic-info.jpg");
        result.ShouldContainOutput("cpu-usage-graphic.jpg");
        result.ShouldContainOutput("gpu-usage-graphic.jpg");
        result.ShouldContainOutput("ram-usage-graphic.jpg");
    }

    #endregion

    #region Debug Mode Tests

    [Fact]
    public void Test_DebugMode_ShowsVerboseOutput()
    {
        var env = new Dictionary<string, string>
        {
            ["USERPROFILE"] = _cli.TestDataDir,
            ["HOME"] = _cli.TestDataDir
        };

        var result = _cli.RunWithEnvironment(env, "test", "basic-info", "--debug");

        result.ShouldSucceed();
        result.ShouldContainOutput("[DEBUG]");
        result.ShouldContainOutput("PluginManager");
    }

    #endregion
}
