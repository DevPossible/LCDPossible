using LCDPossible.FunctionalTests.Helpers;

namespace LCDPossible.FunctionalTests;

/// <summary>
/// Functional tests for profile CLI commands.
/// These tests execute the actual CLI executable and verify file system changes.
/// </summary>
public sealed class ProfileCommandTests : IDisposable
{
    private readonly CliRunner _cli;

    public ProfileCommandTests()
    {
        _cli = new CliRunner();

        // Ensure clean state - delete any existing default profile
        _cli.DeleteProfile("default");
    }

    public void Dispose()
    {
        _cli.Dispose();
    }

    #region Profile Creation Tests

    [Fact]
    public void Profile_New_CreatesEmptyProfile()
    {
        // Arrange & Act
        var result = _cli.Run("profile", "new", "test-profile");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("Created new profile: test-profile");

        _cli.ProfileExists("test-profile").ShouldBeTrue();

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("test-profile"));
        profile.Name.ShouldBe("test-profile");
        profile.Slides.Count.ShouldBe(0);
    }

    [Fact]
    public void Profile_New_WithDescription_CreatesProfileWithDescription()
    {
        // Arrange & Act
        var result = _cli.Run("profile", "new", "described-profile", "--description", "My test description");

        // Assert
        result.ShouldSucceed();

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("described-profile"));
        profile.Description.ShouldBe("My test description");
    }

    [Fact]
    public void Profile_New_DuplicateName_Fails()
    {
        // Arrange - create profile first
        _cli.Run("profile", "new", "duplicate-profile").ShouldSucceed();

        // Act - try to create again
        var result = _cli.Run("profile", "new", "duplicate-profile");

        // Assert
        result.ShouldFail();
        result.ShouldContainError("already exists");
    }

    [Fact]
    public void Profile_New_NoName_Fails()
    {
        // Act
        var result = _cli.Run("profile", "new");

        // Assert
        result.ShouldFail();
        result.ShouldContainError("Profile name is required");
    }

    #endregion

    #region Profile Delete Tests

    [Fact]
    public void Profile_Delete_RemovesProfile()
    {
        // Arrange
        _cli.Run("profile", "new", "to-delete").ShouldSucceed();
        _cli.ProfileExists("to-delete").ShouldBeTrue();

        // Act
        var result = _cli.Run("profile", "delete", "to-delete");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("Deleted profile: to-delete");
        _cli.ProfileExists("to-delete").ShouldBeFalse();
    }

    [Fact]
    public void Profile_Delete_NonExistent_Fails()
    {
        // Act
        var result = _cli.Run("profile", "delete", "nonexistent-profile");

        // Assert
        result.ShouldFail();
        result.ShouldContainError("not found");
    }

    [Fact]
    public void Profile_Delete_Default_RequiresForce()
    {
        // Arrange - create default profile
        _cli.Run("profile", "new", "default").ShouldSucceed();

        // Act - try without force
        var result = _cli.Run("profile", "delete", "default");

        // Assert
        result.ShouldFail();
        result.ShouldContainError("--force");
    }

    [Fact]
    public void Profile_Delete_Default_WithForce_Succeeds()
    {
        // Arrange
        _cli.Run("profile", "new", "default").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "delete", "default", "--force");

        // Assert
        result.ShouldSucceed();
        _cli.ProfileExists("default").ShouldBeFalse();
    }

    #endregion

    #region Panel Append Tests

    [Fact]
    public void Profile_AppendPanel_AddsPanel()
    {
        // Arrange
        _cli.Run("profile", "new", "append-test").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "append-panel", "cpu-usage-graphic", "-p", "append-test");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("Added panel at index 0");
        result.ShouldContainOutput("cpu-usage-graphic");

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("append-test"));
        profile.Slides.Count.ShouldBe(1);
        ProfileHelper.GetPanelType(profile, 0).ShouldBe("cpu-usage-graphic");
    }

    [Fact]
    public void Profile_AppendPanel_MultiplePanels_IncreasesIndex()
    {
        // Arrange
        _cli.Run("profile", "new", "multi-append").ShouldSucceed();

        // Act & Assert - Add three panels
        _cli.Run("profile", "append-panel", "cpu-usage-graphic", "-p", "multi-append")
            .ShouldSucceed()
            .ShouldContainOutput("index 0");

        _cli.Run("profile", "append-panel", "gpu-usage-graphic", "-p", "multi-append")
            .ShouldSucceed()
            .ShouldContainOutput("index 1");

        _cli.Run("profile", "append-panel", "ram-usage-graphic", "-p", "multi-append")
            .ShouldSucceed()
            .ShouldContainOutput("index 2");

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("multi-append"));
        profile.Slides.Count.ShouldBe(3);
        ProfileHelper.GetPanelType(profile, 0).ShouldBe("cpu-usage-graphic");
        ProfileHelper.GetPanelType(profile, 1).ShouldBe("gpu-usage-graphic");
        ProfileHelper.GetPanelType(profile, 2).ShouldBe("ram-usage-graphic");
    }

    [Fact]
    public void Profile_AppendPanel_WithDuration_SetsDuration()
    {
        // Arrange
        _cli.Run("profile", "new", "duration-test").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "append-panel", "basic-info", "-p", "duration-test", "-d", "30");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("Duration: 30s");

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("duration-test"));
        profile.Slides[0].Duration.ShouldBe(30);
    }

    [Fact]
    public void Profile_AppendPanel_WithInterval_SetsInterval()
    {
        // Arrange
        _cli.Run("profile", "new", "interval-test").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "append-panel", "cpu-usage-text", "-p", "interval-test", "-i", "5");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("Update Interval: 5s");

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("interval-test"));
        profile.Slides[0].UpdateInterval.ShouldBe(5);
    }

    [Fact]
    public void Profile_AppendPanel_ToDefaultProfile_CreatesDefault()
    {
        // Arrange - ensure no default profile exists
        _cli.DeleteProfile("default");

        // Act - append to default (should auto-create)
        var result = _cli.Run("profile", "append-panel", "basic-info");

        // Assert
        result.ShouldSucceed();
        _cli.ProfileExists("default").ShouldBeTrue();

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("default"));
        profile.Slides.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Profile_AppendPanel_NoPanelType_Fails()
    {
        // Arrange
        _cli.Run("profile", "new", "no-panel").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "append-panel", "-p", "no-panel");

        // Assert
        result.ShouldFail();
        result.ShouldContainError("Panel type is required");
    }

    #endregion

    #region Panel Remove Tests

    [Fact]
    public void Profile_RemovePanel_RemovesAtIndex()
    {
        // Arrange
        _cli.Run("profile", "new", "remove-test").ShouldSucceed();
        _cli.Run("profile", "append-panel", "cpu-usage-graphic", "-p", "remove-test").ShouldSucceed();
        _cli.Run("profile", "append-panel", "gpu-usage-graphic", "-p", "remove-test").ShouldSucceed();
        _cli.Run("profile", "append-panel", "ram-usage-graphic", "-p", "remove-test").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "remove-panel", "1", "-p", "remove-test");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("Removed panel at index 1");
        result.ShouldContainOutput("gpu-usage-graphic");

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("remove-test"));
        profile.Slides.Count.ShouldBe(2);
        ProfileHelper.GetPanelType(profile, 0).ShouldBe("cpu-usage-graphic");
        ProfileHelper.GetPanelType(profile, 1).ShouldBe("ram-usage-graphic");
    }

    [Fact]
    public void Profile_RemovePanel_InvalidIndex_Fails()
    {
        // Arrange
        _cli.Run("profile", "new", "remove-invalid").ShouldSucceed();
        _cli.Run("profile", "append-panel", "basic-info", "-p", "remove-invalid").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "remove-panel", "5", "-p", "remove-invalid");

        // Assert
        result.ShouldFail();
        result.ShouldContainError("out of range");
    }

    #endregion

    #region Panel Move Tests

    [Fact]
    public void Profile_MovePanel_ReordersCorrectly()
    {
        // Arrange
        _cli.Run("profile", "new", "move-test").ShouldSucceed();
        _cli.Run("profile", "append-panel", "cpu-usage-graphic", "-p", "move-test").ShouldSucceed();
        _cli.Run("profile", "append-panel", "gpu-usage-graphic", "-p", "move-test").ShouldSucceed();
        _cli.Run("profile", "append-panel", "ram-usage-graphic", "-p", "move-test").ShouldSucceed();

        // Act - move first to last
        var result = _cli.Run("profile", "move-panel", "0", "2", "-p", "move-test");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("Moved panel from index 0 to index 2");

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("move-test"));
        ProfileHelper.GetPanelType(profile, 0).ShouldBe("gpu-usage-graphic");
        ProfileHelper.GetPanelType(profile, 1).ShouldBe("ram-usage-graphic");
        ProfileHelper.GetPanelType(profile, 2).ShouldBe("cpu-usage-graphic");
    }

    [Fact]
    public void Profile_MovePanel_InvalidFromIndex_Fails()
    {
        // Arrange
        _cli.Run("profile", "new", "move-invalid").ShouldSucceed();
        _cli.Run("profile", "append-panel", "basic-info", "-p", "move-invalid").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "move-panel", "5", "0", "-p", "move-invalid");

        // Assert
        result.ShouldFail();
        result.ShouldContainError("out of range");
    }

    #endregion

    #region Set Panel Parameter Tests

    [Fact]
    public void Profile_SetPanelParam_Duration_UpdatesValue()
    {
        // Arrange
        _cli.Run("profile", "new", "param-test").ShouldSucceed();
        _cli.Run("profile", "append-panel", "basic-info", "-p", "param-test").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "set-panelparam", "-i", "0", "-n", "duration", "-v", "45", "-p", "param-test");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("Set 'duration' = '45'");

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("param-test"));
        profile.Slides[0].Duration.ShouldBe(45);
    }

    [Fact]
    public void Profile_SetPanelParam_Interval_UpdatesValue()
    {
        // Arrange
        _cli.Run("profile", "new", "interval-param").ShouldSucceed();
        _cli.Run("profile", "append-panel", "cpu-usage-text", "-p", "interval-param").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "set-panelparam", "-i", "0", "-n", "interval", "-v", "2", "-p", "interval-param");

        // Assert
        result.ShouldSucceed();

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("interval-param"));
        profile.Slides[0].UpdateInterval.ShouldBe(2);
    }

    [Fact]
    public void Profile_SetPanelParam_Transition_UpdatesValue()
    {
        // Arrange
        _cli.Run("profile", "new", "transition-param").ShouldSucceed();
        _cli.Run("profile", "append-panel", "basic-info", "-p", "transition-param").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "set-panelparam", "-i", "0", "-n", "transition", "-v", "fade", "-p", "transition-param");

        // Assert
        result.ShouldSucceed();

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("transition-param"));
        profile.Slides[0].Transition.ShouldBe("fade");
    }

    [Fact]
    public void Profile_SetPanelParam_EmptyValue_ClearsParameter()
    {
        // Arrange
        _cli.Run("profile", "new", "clear-param").ShouldSucceed();
        _cli.Run("profile", "append-panel", "basic-info", "-p", "clear-param", "-d", "30").ShouldSucceed();

        // Verify duration is set
        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("clear-param"));
        profile.Slides[0].Duration.ShouldBe(30);

        // Act - clear by passing empty value
        var result = _cli.Run("profile", "set-panelparam", "-i", "0", "-n", "duration", "-v", "", "-p", "clear-param");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("Cleared parameter");

        profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("clear-param"));
        profile.Slides[0].Duration.ShouldBeNull();
    }

    [Fact]
    public void Profile_SetPanelParam_InvalidParameter_Fails()
    {
        // Arrange
        _cli.Run("profile", "new", "invalid-param").ShouldSucceed();
        _cli.Run("profile", "append-panel", "basic-info", "-p", "invalid-param").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "set-panelparam", "-i", "0", "-n", "nonexistent", "-v", "value", "-p", "invalid-param");

        // Assert
        result.ShouldFail();
        result.ShouldContainError("Unknown parameter");
    }

    #endregion

    #region Get Panel Parameter Tests

    [Fact]
    public void Profile_GetPanelParam_ReturnsValue()
    {
        // Arrange
        _cli.Run("profile", "new", "get-param").ShouldSucceed();
        _cli.Run("profile", "append-panel", "basic-info", "-p", "get-param", "-d", "25").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "get-panelparam", "-i", "0", "-n", "duration", "-p", "get-param");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("25");
    }

    [Fact]
    public void Profile_GetPanelParam_NotSet_ReturnsNotSet()
    {
        // Arrange
        _cli.Run("profile", "new", "get-unset").ShouldSucceed();
        _cli.Run("profile", "append-panel", "basic-info", "-p", "get-unset").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "get-panelparam", "-i", "0", "-n", "duration", "-p", "get-unset");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("(not set)");
    }

    #endregion

    #region Clear Panel Parameters Tests

    [Fact]
    public void Profile_ClearPanelParams_ClearsAllCustomParams()
    {
        // Arrange
        _cli.Run("profile", "new", "clear-all").ShouldSucceed();
        _cli.Run("profile", "append-panel", "basic-info", "-p", "clear-all", "-d", "30", "-i", "5").ShouldSucceed();

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("clear-all"));
        profile.Slides[0].Duration.ShouldBe(30);
        profile.Slides[0].UpdateInterval.ShouldBe(5);

        // Act
        var result = _cli.Run("profile", "clear-panelparams", "0", "-p", "clear-all");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("Cleared all parameters");

        profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("clear-all"));
        profile.Slides[0].Duration.ShouldBeNull();
        profile.Slides[0].UpdateInterval.ShouldBeNull();
        // Panel type should be preserved
        ProfileHelper.GetPanelType(profile, 0).ShouldBe("basic-info");
    }

    #endregion

    #region Set Defaults Tests

    [Fact]
    public void Profile_SetDefaults_Duration_UpdatesDefault()
    {
        // Arrange
        _cli.Run("profile", "new", "set-defaults").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "set-defaults", "--duration", "60", "-p", "set-defaults");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("Default Duration: 60s");

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("set-defaults"));
        profile.DefaultDurationSeconds.ShouldBe(60);
    }

    [Fact]
    public void Profile_SetDefaults_Interval_UpdatesDefault()
    {
        // Arrange
        _cli.Run("profile", "new", "defaults-interval").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "set-defaults", "--interval", "10", "-p", "defaults-interval");

        // Assert
        result.ShouldSucceed();

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("defaults-interval"));
        profile.DefaultUpdateIntervalSeconds.ShouldBe(10);
    }

    [Fact]
    public void Profile_SetDefaults_Transition_UpdatesDefault()
    {
        // Arrange
        _cli.Run("profile", "new", "defaults-transition").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "set-defaults", "--transition", "slide-left", "-p", "defaults-transition");

        // Assert
        result.ShouldSucceed();

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("defaults-transition"));
        profile.DefaultTransition.ShouldBe("slide-left");
    }

    [Fact]
    public void Profile_SetDefaults_Name_UpdatesProfileName()
    {
        // Arrange
        _cli.Run("profile", "new", "name-test").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "set-defaults", "--name", "My Custom Profile", "-p", "name-test");

        // Assert
        result.ShouldSucceed();

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("name-test"));
        profile.Name.ShouldBe("My Custom Profile");
    }

    [Fact]
    public void Profile_SetDefaults_Description_UpdatesDescription()
    {
        // Arrange
        _cli.Run("profile", "new", "desc-test").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "set-defaults", "--description", "A detailed description", "-p", "desc-test");

        // Assert
        result.ShouldSucceed();

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath("desc-test"));
        profile.Description.ShouldBe("A detailed description");
    }

    [Fact]
    public void Profile_SetDefaults_NoOptions_Fails()
    {
        // Arrange
        _cli.Run("profile", "new", "no-opts").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "set-defaults", "-p", "no-opts");

        // Assert
        result.ShouldFail();
        result.ShouldContainError("At least one setting is required");
    }

    #endregion

    #region List Profiles Tests

    [Fact]
    public void Profile_List_ShowsAllProfiles()
    {
        // Arrange
        _cli.Run("profile", "new", "list-test-1").ShouldSucceed();
        _cli.Run("profile", "new", "list-test-2").ShouldSucceed();
        _cli.Run("profile", "new", "list-test-3").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "list");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("list-test-1");
        result.ShouldContainOutput("list-test-2");
        result.ShouldContainOutput("list-test-3");
    }

    [Fact]
    public void Profile_List_EmptyDirectory_ShowsMessage()
    {
        // Act (test data directory is empty)
        var result = _cli.Run("profile", "list");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("No profiles found");
    }

    [Fact]
    public void Profile_List_JsonFormat_OutputsJson()
    {
        // Arrange
        _cli.Run("profile", "new", "json-list").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "list", "--format", "json");

        // Assert
        result.ShouldSucceed();
        result.Stdout.ShouldContain("[");
        result.Stdout.ShouldContain("json-list");
    }

    #endregion

    #region List Panels Tests

    [Fact]
    public void Profile_ListPanels_ShowsAllPanels()
    {
        // Arrange
        _cli.Run("profile", "new", "list-panels").ShouldSucceed();
        _cli.Run("profile", "append-panel", "cpu-usage-graphic", "-p", "list-panels").ShouldSucceed();
        _cli.Run("profile", "append-panel", "gpu-usage-graphic", "-p", "list-panels", "-d", "20").ShouldSucceed();
        _cli.Run("profile", "append-panel", "ram-usage-graphic", "-p", "list-panels").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "list-panels", "-p", "list-panels");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("[0] cpu-usage-graphic");
        result.ShouldContainOutput("[1] gpu-usage-graphic");
        result.ShouldContainOutput("[2] ram-usage-graphic");
        result.ShouldContainOutput("duration: 20s");
    }

    [Fact]
    public void Profile_ListPanels_EmptyProfile_ShowsMessage()
    {
        // Arrange
        _cli.Run("profile", "new", "empty-profile").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "list-panels", "-p", "empty-profile");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("No panels configured");
    }

    [Fact]
    public void Profile_ListPanels_YamlFormat_OutputsYaml()
    {
        // Arrange
        _cli.Run("profile", "new", "yaml-list").ShouldSucceed();
        _cli.Run("profile", "append-panel", "basic-info", "-p", "yaml-list").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "list-panels", "-p", "yaml-list", "--format", "yaml");

        // Assert
        result.ShouldSucceed();
        result.Stdout.ShouldContain("slides:");
        result.Stdout.ShouldContain("panel: basic-info");
    }

    [Fact]
    public void Profile_ListPanels_JsonFormat_OutputsJson()
    {
        // Arrange
        _cli.Run("profile", "new", "json-panels").ShouldSucceed();
        _cli.Run("profile", "append-panel", "basic-info", "-p", "json-panels").ShouldSucceed();

        // Act
        var result = _cli.Run("profile", "list-panels", "-p", "json-panels", "--format", "json");

        // Assert
        result.ShouldSucceed();
        result.Stdout.ShouldContain("{");
        result.Stdout.ShouldContain("\"slides\"");
        result.Stdout.ShouldContain("basic-info");
    }

    #endregion

    #region Help Tests

    [Fact]
    public void Profile_Help_ShowsHelp()
    {
        // Act
        var result = _cli.Run("profile", "help");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("PROFILE MANAGEMENT COMMANDS");
        result.ShouldContainOutput("append-panel");
        result.ShouldContainOutput("remove-panel");
        result.ShouldContainOutput("set-defaults");
    }

    [Fact]
    public void Profile_NoSubcommand_ShowsHelp()
    {
        // Act
        var result = _cli.Run("profile");

        // Assert
        result.ShouldSucceed();
        result.ShouldContainOutput("PROFILE MANAGEMENT COMMANDS");
    }

    [Fact]
    public void Profile_UnknownSubcommand_ShowsError()
    {
        // Act
        var result = _cli.Run("profile", "unknowncmd");

        // Assert
        result.ShouldFail();
        result.ShouldContainError("Unknown profile sub-command");
    }

    #endregion

    #region End-to-End Workflow Tests

    [Fact]
    public void Profile_FullWorkflow_CreateModifyDelete()
    {
        var profileName = "workflow-test";

        // Step 1: Create profile
        _cli.Run("profile", "new", profileName)
            .ShouldSucceed();
        _cli.ProfileExists(profileName).ShouldBeTrue();

        // Step 2: Add panels
        _cli.Run("profile", "append-panel", "basic-info", "-p", profileName).ShouldSucceed();
        _cli.Run("profile", "append-panel", "cpu-usage-graphic", "-p", profileName, "-d", "20").ShouldSucceed();
        _cli.Run("profile", "append-panel", "gpu-usage-graphic", "-p", profileName).ShouldSucceed();

        var profile = ProfileHelper.ReadProfile(_cli.GetProfilePath(profileName));
        profile.Slides.Count.ShouldBe(3);

        // Step 3: Modify panel parameters
        _cli.Run("profile", "set-panelparam", "-i", "0", "-n", "duration", "-v", "30", "-p", profileName).ShouldSucceed();
        _cli.Run("profile", "set-panelparam", "-i", "2", "-n", "transition", "-v", "fade", "-p", profileName).ShouldSucceed();

        profile = ProfileHelper.ReadProfile(_cli.GetProfilePath(profileName));
        profile.Slides[0].Duration.ShouldBe(30);
        profile.Slides[2].Transition.ShouldBe("fade");

        // Step 4: Move panel
        _cli.Run("profile", "move-panel", "0", "2", "-p", profileName).ShouldSucceed();

        profile = ProfileHelper.ReadProfile(_cli.GetProfilePath(profileName));
        ProfileHelper.GetPanelType(profile, 0).ShouldBe("cpu-usage-graphic");
        ProfileHelper.GetPanelType(profile, 2).ShouldBe("basic-info");

        // Step 5: Remove panel
        _cli.Run("profile", "remove-panel", "1", "-p", profileName).ShouldSucceed();

        profile = ProfileHelper.ReadProfile(_cli.GetProfilePath(profileName));
        profile.Slides.Count.ShouldBe(2);

        // Step 6: Set defaults
        _cli.Run("profile", "set-defaults", "--duration", "45", "--transition", "crossfade", "-p", profileName).ShouldSucceed();

        profile = ProfileHelper.ReadProfile(_cli.GetProfilePath(profileName));
        profile.DefaultDurationSeconds.ShouldBe(45);
        profile.DefaultTransition.ShouldBe("crossfade");

        // Step 7: Verify with list
        _cli.Run("profile", "list-panels", "-p", profileName)
            .ShouldSucceed()
            .ShouldContainOutput("cpu-usage-graphic")
            .ShouldContainOutput("basic-info");

        // Step 8: Delete profile
        _cli.Run("profile", "delete", profileName).ShouldSucceed();
        _cli.ProfileExists(profileName).ShouldBeFalse();
    }

    #endregion
}
