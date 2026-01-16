using LCDPossible.Core;
using LCDPossible.Core.Configuration;
using LCDPossible.Core.Ipc;
using LCDPossible.Core.Transitions;
using LCDPossible.Ipc;

namespace LCDPossible.Cli;

/// <summary>
/// CLI command handler for profile management operations.
/// </summary>
public static class ProfileCommands
{
    /// <summary>
    /// Runs a profile sub-command.
    /// </summary>
    public static int Run(string[] args)
    {
        // Find the sub-command (first arg after "profile")
        var subCommand = GetSubCommand(args);

        if (string.IsNullOrEmpty(subCommand))
        {
            return ShowProfileHelp();
        }

        return subCommand switch
        {
            "new" => NewProfile(args),
            "list" or "list-profiles" => ListProfiles(args),
            "list-panels" => ListPanels(args),
            "append-panel" or "add" or "add-panel" => AppendPanel(args),
            "remove-panel" or "remove" => RemovePanel(args),
            "move-panel" or "move" => MovePanel(args),
            "set-defaults" => SetDefaults(args),
            "set-panelparam" or "set-param" or "set" => SetPanelParam(args),
            "get-panelparam" or "get-param" or "get" => GetPanelParam(args),
            "clear-panelparams" or "clear-params" or "clear" => ClearPanelParams(args),
            "delete" or "remove-profile" => DeleteProfile(args),
            "show" => ShowProfile(args),
            "reload" => ReloadProfile(args),
            "help" or "-h" or "--help" or "/?" => ShowProfileHelp(),
            _ => UnknownSubCommand(subCommand)
        };
    }

    private static string? GetSubCommand(string[] args)
    {
        // Find first arg after "profile" that isn't a flag
        var foundProfile = false;
        foreach (var arg in args)
        {
            if (arg.Equals("profile", StringComparison.OrdinalIgnoreCase))
            {
                foundProfile = true;
                continue;
            }

            if (foundProfile && !arg.StartsWith("-") && !arg.StartsWith("/"))
            {
                return arg.ToLowerInvariant();
            }
        }
        return null;
    }

    private static string? GetProfileName(string[] args)
    {
        // Look for --profile or -p flag, or use positional arg after sub-command
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--profile" or "-p")
            {
                return args[i + 1];
            }
        }
        return null; // Will use default profile
    }

    private static string? GetArgValue(string[] args, params string[] names)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            foreach (var name in names)
            {
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
        }
        return null;
    }

    private static int? GetIntArg(string[] args, params string[] names)
    {
        var value = GetArgValue(args, names);
        return int.TryParse(value, out var result) ? result : null;
    }

    private static string? GetPositionalArg(string[] args, int position)
    {
        // Get the nth non-flag argument after "profile sub-command"
        // Must skip both flags AND their values (e.g., -p test-profile)
        var count = 0;
        var foundSubCommand = false;
        var skipNext = false;

        // Known flags that take a value (must skip the following arg)
        var flagsWithValue = new[] { "-p", "--profile", "-f", "--format", "-d", "--duration",
            "-i", "--interval", "--index", "-n", "--name", "-v", "--value",
            "-b", "--background", "--description", "--from", "--to", "-t",
            "--transition", "--transition-duration" };

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            // Check if this is a flag
            if (arg.StartsWith("-") || arg.StartsWith("/"))
            {
                // If it's a flag that takes a value, skip the next arg too
                if (flagsWithValue.Any(f => arg.Equals(f, StringComparison.OrdinalIgnoreCase)))
                {
                    skipNext = true;
                }
                continue;
            }

            if (arg.Equals("profile", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!foundSubCommand)
            {
                foundSubCommand = true; // Skip sub-command itself
                continue;
            }

            if (count == position)
            {
                return arg;
            }
            count++;
        }
        return null;
    }

    private static bool HasFlag(string[] args, params string[] names)
    {
        foreach (var arg in args)
        {
            foreach (var name in names)
            {
                if (arg.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static int NewProfile(string[] args)
    {
        var profileName = GetPositionalArg(args, 0) ?? GetArgValue(args, "--name", "-n");

        if (string.IsNullOrEmpty(profileName))
        {
            Console.Error.WriteLine("Error: Profile name is required.");
            Console.Error.WriteLine("Usage: lcdpossible profile new <profile-name>");
            return 1;
        }

        var description = GetArgValue(args, "--description", "-d");
        var manager = new ProfileManager();

        if (manager.ProfileExists(profileName))
        {
            Console.Error.WriteLine($"Error: Profile '{profileName}' already exists.");
            Console.Error.WriteLine($"Path: {ProfileManager.GetProfilePath(profileName)}");
            return 1;
        }

        var profile = manager.CreateNewProfile(profileName, description);
        var path = ProfileManager.GetProfilePath(profileName);

        Console.WriteLine($"Created new profile: {profileName}");
        Console.WriteLine($"Path: {path}");
        Console.WriteLine();
        Console.WriteLine("Profile is empty. Add panels with:");
        Console.WriteLine($"  lcdpossible profile append-panel basic-info -p {profileName}");

        return 0;
    }

    private static int ListProfiles(string[] args)
    {
        var manager = new ProfileManager();
        var profiles = manager.ListProfiles().ToList();
        var outputFormat = GetArgValue(args, "--format", "-f")?.ToLowerInvariant();

        if (profiles.Count == 0)
        {
            Console.WriteLine("No profiles found.");
            Console.WriteLine($"Profiles directory: {ProfileManager.ProfilesDirectory}");
            Console.WriteLine();
            Console.WriteLine("Create a new profile with:");
            Console.WriteLine("  lcdpossible profile new my-profile");
            return 0;
        }

        if (outputFormat == "json")
        {
            var json = System.Text.Json.JsonSerializer.Serialize(profiles, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            Console.WriteLine(json);
            return 0;
        }

        Console.WriteLine($"Profiles ({profiles.Count}):");
        Console.WriteLine($"Directory: {ProfileManager.ProfilesDirectory}");
        Console.WriteLine();

        foreach (var profileName in profiles)
        {
            try
            {
                var profile = manager.LoadProfile(profileName);
                Console.WriteLine($"  {profileName}");
                Console.WriteLine($"    Name: {profile.Name}");
                Console.WriteLine($"    Slides: {profile.Slides.Count}");
                if (!string.IsNullOrEmpty(profile.Description))
                {
                    Console.WriteLine($"    Description: {profile.Description}");
                }
            }
            catch
            {
                Console.WriteLine($"  {profileName} (error loading)");
            }
        }

        return 0;
    }

    private static int ListPanels(string[] args)
    {
        var profileName = GetProfileName(args) ?? GetPositionalArg(args, 0);
        var outputFormat = GetArgValue(args, "--format", "-f")?.ToLowerInvariant();

        // If service is running and no specific profile requested, get info from server
        // This ensures we show what the server is actually using
        if (profileName == null && IpcPaths.IsServiceRunning())
        {
            return ListPanelsFromServer(outputFormat);
        }

        // Otherwise, load from disk
        var manager = new ProfileManager();

        DisplayProfile profile;
        string profilePath;
        try
        {
            profile = manager.LoadProfile(profileName);
            profilePath = ProfileManager.GetProfilePath(profileName);
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine($"Error: Profile not found: {ProfileManager.GetProfilePath(profileName)}");
            return 1;
        }

        return DisplayProfileInfo(profile, profilePath, outputFormat, manager);
    }

    private static int ListPanelsFromServer(string? outputFormat)
    {
        try
        {
            var response = IpcClientHelper.SendCommandAsync("profile-info").GetAwaiter().GetResult();

            if (!response.Success)
            {
                Console.Error.WriteLine($"Error from server: {response.Error}");
                return 1;
            }

            if (response.Data == null)
            {
                Console.Error.WriteLine("Error: No profile data returned from server");
                return 1;
            }

            // Parse the response data
            var json = System.Text.Json.JsonSerializer.Serialize(response.Data);

            if (outputFormat == "json")
            {
                // Pretty print the JSON
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                var doc = System.Text.Json.JsonDocument.Parse(json);
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(doc, options));
                return 0;
            }

            // Parse JSON into a document for reading
            using var doc2 = System.Text.Json.JsonDocument.Parse(json);
            var root = doc2.RootElement;

            var name = root.GetProperty("name").GetString() ?? "Unknown";
            var description = root.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
            var path = root.TryGetProperty("path", out var pathProp) ? pathProp.GetString() : null;
            var defaultDuration = root.GetProperty("defaultDurationSeconds").GetInt32();
            var defaultInterval = root.GetProperty("defaultUpdateIntervalSeconds").GetInt32();
            var defaultTransition = root.TryGetProperty("defaultTransition", out var transProp) ? transProp.GetString() : null;
            var defaultTransitionDuration = root.TryGetProperty("defaultTransitionDurationMs", out var transDurProp) ? transDurProp.GetInt32() : 0;

            // Human-readable format
            Console.WriteLine($"Profile: {name}");
            Console.WriteLine($"Path: {path ?? "(default/built-in)"}");
            Console.WriteLine($"Source: running service");
            if (!string.IsNullOrEmpty(description))
            {
                Console.WriteLine($"Description: {description}");
            }
            Console.WriteLine($"Default Duration: {defaultDuration}s");
            Console.WriteLine($"Default Update Interval: {defaultInterval}s");
            Console.WriteLine($"Default Transition: {defaultTransition ?? "random"}");
            Console.WriteLine($"Default Transition Duration: {defaultTransitionDuration}ms");
            Console.WriteLine();

            var slides = root.GetProperty("slides");
            if (slides.GetArrayLength() == 0)
            {
                Console.WriteLine("No panels configured.");
                return 0;
            }

            Console.WriteLine($"Panels ({slides.GetArrayLength()}):");
            foreach (var slide in slides.EnumerateArray())
            {
                var index = slide.GetProperty("index").GetInt32();
                var panel = slide.TryGetProperty("panel", out var panelProp) && panelProp.ValueKind != System.Text.Json.JsonValueKind.Null
                    ? panelProp.GetString() : null;
                var source = slide.TryGetProperty("source", out var sourceProp) && sourceProp.ValueKind != System.Text.Json.JsonValueKind.Null
                    ? sourceProp.GetString() : null;
                var panelType = panel ?? source ?? "(unknown)";

                Console.WriteLine($"  [{index}] {panelType}");

                if (slide.TryGetProperty("type", out var typeProp) && typeProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                {
                    Console.WriteLine($"       type: {typeProp.GetString()}");
                }
                if (slide.TryGetProperty("duration", out var durProp) && durProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                {
                    Console.WriteLine($"       duration: {durProp.GetInt32()}s");
                }
                if (slide.TryGetProperty("updateInterval", out var intProp) && intProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                {
                    Console.WriteLine($"       update_interval: {intProp.GetInt32()}s");
                }
                if (slide.TryGetProperty("background", out var bgProp) && bgProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                {
                    Console.WriteLine($"       background: {bgProp.GetString()}");
                }
                if (slide.TryGetProperty("transition", out var slideTransProp) && slideTransProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                {
                    Console.WriteLine($"       transition: {slideTransProp.GetString()}");
                }
                if (slide.TryGetProperty("transitionDurationMs", out var slideTransDurProp) && slideTransDurProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                {
                    Console.WriteLine($"       transition_duration: {slideTransDurProp.GetInt32()}ms");
                }
                if (!string.IsNullOrEmpty(source) && source != panel)
                {
                    Console.WriteLine($"       source: {source}");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error communicating with service: {ex.Message}");
            Console.Error.WriteLine("Falling back to disk...");

            // Fall back to disk
            var manager = new ProfileManager();
            try
            {
                var profile = manager.LoadProfile(null);
                var profilePath = ProfileManager.GetProfilePath(null);
                return DisplayProfileInfo(profile, profilePath, null, manager);
            }
            catch (FileNotFoundException)
            {
                Console.Error.WriteLine($"Error: Default profile not found");
                return 1;
            }
        }
    }

    private static int DisplayProfileInfo(DisplayProfile profile, string profilePath, string? outputFormat, ProfileManager manager)
    {
        if (outputFormat == "json")
        {
            Console.WriteLine(manager.ToJson(profile));
            return 0;
        }

        if (outputFormat == "yaml")
        {
            Console.WriteLine(manager.ToYaml(profile));
            return 0;
        }

        // Human-readable format
        Console.WriteLine($"Profile: {profile.Name}");
        Console.WriteLine($"Path: {profilePath}");
        Console.WriteLine($"Source: disk");
        if (!string.IsNullOrEmpty(profile.Description))
        {
            Console.WriteLine($"Description: {profile.Description}");
        }
        Console.WriteLine($"Default Duration: {profile.DefaultDurationSeconds}s");
        Console.WriteLine($"Default Update Interval: {profile.DefaultUpdateIntervalSeconds}s");
        Console.WriteLine($"Default Transition: {profile.DefaultTransition}");
        Console.WriteLine($"Default Transition Duration: {profile.DefaultTransitionDurationMs}ms");
        Console.WriteLine();

        if (profile.Slides.Count == 0)
        {
            Console.WriteLine("No panels configured.");
            return 0;
        }

        Console.WriteLine($"Panels ({profile.Slides.Count}):");
        for (var i = 0; i < profile.Slides.Count; i++)
        {
            var slide = profile.Slides[i];
            var panelType = slide.Panel ?? slide.Source ?? "(unknown)";
            var type = slide.Type ?? "panel";

            Console.WriteLine($"  [{i}] {panelType}");

            if (slide.Type != null)
            {
                Console.WriteLine($"       type: {slide.Type}");
            }
            if (slide.Duration.HasValue)
            {
                Console.WriteLine($"       duration: {slide.Duration}s");
            }
            if (slide.UpdateInterval.HasValue)
            {
                Console.WriteLine($"       update_interval: {slide.UpdateInterval}s");
            }
            if (!string.IsNullOrEmpty(slide.Background))
            {
                Console.WriteLine($"       background: {slide.Background}");
            }
            if (!string.IsNullOrEmpty(slide.Transition))
            {
                Console.WriteLine($"       transition: {slide.Transition}");
            }
            if (slide.TransitionDurationMs.HasValue)
            {
                Console.WriteLine($"       transition_duration: {slide.TransitionDurationMs}ms");
            }
            if (!string.IsNullOrEmpty(slide.Source) && slide.Source != slide.Panel)
            {
                Console.WriteLine($"       source: {slide.Source}");
            }
        }

        return 0;
    }

    private static int AppendPanel(string[] args)
    {
        var profileName = GetProfileName(args);
        var panelType = GetPositionalArg(args, 0);

        if (string.IsNullOrEmpty(panelType))
        {
            Console.Error.WriteLine("Error: Panel type is required.");
            Console.Error.WriteLine("Usage: lcdpossible profile append-panel <panel-type> [-p <profile>]");
            Console.Error.WriteLine("       lcdpossible profile append-panel cpu-usage-graphic");
            return 1;
        }

        var duration = GetIntArg(args, "--duration", "-d");
        var interval = GetIntArg(args, "--interval", "-i", "--update-interval");
        var background = GetArgValue(args, "--background", "-b");

        var manager = new ProfileManager();

        try
        {
            var (index, slide) = manager.AppendPanel(profileName, panelType, duration, interval, background);

            Console.WriteLine($"Added panel at index {index}:");
            Console.WriteLine($"  Panel: {panelType}");
            if (duration.HasValue) Console.WriteLine($"  Duration: {duration}s");
            if (interval.HasValue) Console.WriteLine($"  Update Interval: {interval}s");
            if (!string.IsNullOrEmpty(background)) Console.WriteLine($"  Background: {background}");

            NotifyServiceReload();
            return 0;
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine($"Error: Profile not found: {ProfileManager.GetProfilePath(profileName)}");
            Console.Error.WriteLine("Create a new profile first with: lcdpossible profile new <name>");
            return 1;
        }
    }

    private static int RemovePanel(string[] args)
    {
        var profileName = GetProfileName(args);
        var indexStr = GetPositionalArg(args, 0) ?? GetArgValue(args, "--index", "-i");

        if (!int.TryParse(indexStr, out var index))
        {
            Console.Error.WriteLine("Error: Valid panel index is required.");
            Console.Error.WriteLine("Usage: lcdpossible profile remove-panel <index> [-p <profile>]");
            Console.Error.WriteLine("       lcdpossible profile remove-panel 2");
            return 1;
        }

        var manager = new ProfileManager();

        try
        {
            var removed = manager.RemovePanel(profileName, index);
            var panelType = removed?.Panel ?? removed?.Source ?? "(unknown)";
            Console.WriteLine($"Removed panel at index {index}: {panelType}");
            NotifyServiceReload();
            return 0;
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine($"Error: Profile not found: {ProfileManager.GetProfilePath(profileName)}");
            return 1;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int MovePanel(string[] args)
    {
        var profileName = GetProfileName(args);
        var fromStr = GetPositionalArg(args, 0) ?? GetArgValue(args, "--from", "-f");
        var toStr = GetPositionalArg(args, 1) ?? GetArgValue(args, "--to", "-t");

        if (!int.TryParse(fromStr, out var fromIndex) || !int.TryParse(toStr, out var toIndex))
        {
            Console.Error.WriteLine("Error: Valid from and to indices are required.");
            Console.Error.WriteLine("Usage: lcdpossible profile move-panel <from-index> <to-index> [-p <profile>]");
            Console.Error.WriteLine("       lcdpossible profile move-panel 0 3");
            return 1;
        }

        var manager = new ProfileManager();

        try
        {
            manager.MovePanel(profileName, fromIndex, toIndex);
            Console.WriteLine($"Moved panel from index {fromIndex} to index {toIndex}");
            NotifyServiceReload();
            return 0;
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine($"Error: Profile not found: {ProfileManager.GetProfilePath(profileName)}");
            return 1;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int SetDefaults(string[] args)
    {
        var profileName = GetProfileName(args);
        var name = GetArgValue(args, "--name", "-n");
        var description = GetArgValue(args, "--description", "-d");
        var duration = GetIntArg(args, "--duration");
        var interval = GetIntArg(args, "--interval", "--update-interval");
        var transition = GetArgValue(args, "--transition");
        var transitionDuration = GetIntArg(args, "--transition-duration");
        var pageEffect = GetArgValue(args, "--page-effect", "--effect");

        if (name == null && description == null && !duration.HasValue && !interval.HasValue &&
            transition == null && !transitionDuration.HasValue && pageEffect == null)
        {
            Console.Error.WriteLine("Error: At least one setting is required.");
            Console.Error.WriteLine("Usage: lcdpossible profile set-defaults [-p <profile>] [options]");
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --name <name>                   Set profile name");
            Console.Error.WriteLine("  --description <text>            Set profile description");
            Console.Error.WriteLine("  --duration <seconds>            Set default panel duration");
            Console.Error.WriteLine("  --interval <seconds>            Set default update interval");
            Console.Error.WriteLine("  --transition <type>             Set default transition effect");
            Console.Error.WriteLine("  --transition-duration <ms>      Set default transition duration");
            Console.Error.WriteLine("  --page-effect <effect>          Set default page effect");
            Console.Error.WriteLine();
            // Generate transition list from registry
            var transitionRegistry = new TransitionRegistry();
            var transitionIds = transitionRegistry.GetTransitionTypes().Select(t => t.TransitionId).ToList();
            Console.Error.WriteLine($"Transition types: {string.Join(", ", transitionIds)}");
            Console.Error.WriteLine();

            // Generate page effects list from registry
            var effectIds = new List<string> { "none" };
            effectIds.AddRange(PageEffectManager.Instance.Effects.Keys.OrderBy(k => k));
            effectIds.Add("random");
            Console.Error.WriteLine($"Page effects: {string.Join(", ", effectIds)}");
            return 1;
        }

        var manager = new ProfileManager();

        try
        {
            manager.SetDefaults(profileName, name, description, duration, interval, transition, transitionDuration, pageEffect);

            Console.WriteLine("Updated profile defaults:");
            if (name != null) Console.WriteLine($"  Name: {name}");
            if (description != null) Console.WriteLine($"  Description: {(string.IsNullOrEmpty(description) ? "(cleared)" : description)}");
            if (duration.HasValue) Console.WriteLine($"  Default Duration: {duration}s");
            if (interval.HasValue) Console.WriteLine($"  Default Update Interval: {interval}s");
            if (transition != null) Console.WriteLine($"  Default Transition: {transition}");
            if (transitionDuration.HasValue) Console.WriteLine($"  Default Transition Duration: {transitionDuration}ms");
            if (pageEffect != null) Console.WriteLine($"  Default Page Effect: {pageEffect}");

            NotifyServiceReload();
            return 0;
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine($"Error: Profile not found: {ProfileManager.GetProfilePath(profileName)}");
            return 1;
        }
    }

    private static int SetPanelParam(string[] args)
    {
        var profileName = GetProfileName(args);
        var indexStr = GetArgValue(args, "--index", "-i");
        var paramName = GetArgValue(args, "--name", "-n");
        var paramValue = GetArgValue(args, "--value", "-v");

        if (!int.TryParse(indexStr, out var index))
        {
            Console.Error.WriteLine("Error: Valid panel index is required (--index or -i).");
            Console.Error.WriteLine("Usage: lcdpossible profile set-panelparam -i <index> -n <name> -v <value>");
            Console.Error.WriteLine("       lcdpossible profile set-panelparam -i 2 -n duration -v 30");
            return 1;
        }

        if (string.IsNullOrEmpty(paramName))
        {
            Console.Error.WriteLine("Error: Parameter name is required (--name or -n).");
            Console.Error.WriteLine("Valid parameters: panel, type, source, duration, interval, background, transition, transition_duration");
            return 1;
        }

        var manager = new ProfileManager();

        try
        {
            manager.SetPanelParameter(profileName, index, paramName, paramValue);

            if (string.IsNullOrEmpty(paramValue))
            {
                Console.WriteLine($"Cleared parameter '{paramName}' for panel at index {index}");
            }
            else
            {
                Console.WriteLine($"Set '{paramName}' = '{paramValue}' for panel at index {index}");
            }

            NotifyServiceReload();
            return 0;
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine($"Error: Profile not found: {ProfileManager.GetProfilePath(profileName)}");
            return 1;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine("Valid parameters: panel, type, source, duration, interval, background, transition, transition_duration");
            return 1;
        }
    }

    private static int GetPanelParam(string[] args)
    {
        var profileName = GetProfileName(args);
        var indexStr = GetArgValue(args, "--index", "-i");
        var paramName = GetArgValue(args, "--name", "-n");

        if (!int.TryParse(indexStr, out var index))
        {
            Console.Error.WriteLine("Error: Valid panel index is required (--index or -i).");
            Console.Error.WriteLine("Usage: lcdpossible profile get-panelparam -i <index> -n <name>");
            return 1;
        }

        if (string.IsNullOrEmpty(paramName))
        {
            Console.Error.WriteLine("Error: Parameter name is required (--name or -n).");
            Console.Error.WriteLine("Valid parameters: panel, type, source, duration, interval, background, transition, transition_duration");
            return 1;
        }

        var manager = new ProfileManager();

        try
        {
            var value = manager.GetPanelParameter(profileName, index, paramName);

            if (value == null)
            {
                Console.WriteLine($"(not set)");
            }
            else
            {
                Console.WriteLine(value);
            }

            return 0;
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine($"Error: Profile not found: {ProfileManager.GetProfilePath(profileName)}");
            return 1;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int ClearPanelParams(string[] args)
    {
        var profileName = GetProfileName(args);
        var indexStr = GetPositionalArg(args, 0) ?? GetArgValue(args, "--index", "-i");

        if (!int.TryParse(indexStr, out var index))
        {
            Console.Error.WriteLine("Error: Valid panel index is required.");
            Console.Error.WriteLine("Usage: lcdpossible profile clear-panelparams <index> [-p <profile>]");
            return 1;
        }

        var manager = new ProfileManager();

        try
        {
            manager.ClearPanelParameters(profileName, index);
            Console.WriteLine($"Cleared all parameters for panel at index {index}");
            NotifyServiceReload();
            return 0;
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine($"Error: Profile not found: {ProfileManager.GetProfilePath(profileName)}");
            return 1;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int DeleteProfile(string[] args)
    {
        var profileName = GetPositionalArg(args, 0);

        if (string.IsNullOrEmpty(profileName))
        {
            Console.Error.WriteLine("Error: Profile name is required.");
            Console.Error.WriteLine("Usage: lcdpossible profile delete <profile-name>");
            return 1;
        }

        // Safety check for default profile
        if (profileName.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            var force = HasFlag(args, "--force", "-f");
            if (!force)
            {
                Console.Error.WriteLine("Warning: You are about to delete the default profile.");
                Console.Error.WriteLine("Use --force to confirm: lcdpossible profile delete default --force");
                return 1;
            }
        }

        var manager = new ProfileManager();
        var path = ProfileManager.GetProfilePath(profileName);

        if (manager.DeleteProfile(profileName))
        {
            Console.WriteLine($"Deleted profile: {profileName}");
            Console.WriteLine($"Path: {path}");
            return 0;
        }
        else
        {
            Console.Error.WriteLine($"Error: Profile not found: {path}");
            return 1;
        }
    }

    private static int ShowProfile(string[] args)
    {
        var profileName = GetProfileName(args) ?? GetPositionalArg(args, 0);
        var outputFormat = GetArgValue(args, "--format", "-f")?.ToLowerInvariant();

        // Delegate to list-panels which shows detailed panel info
        return ListPanels(args);
    }

    private static int ReloadProfile(string[] args)
    {
        if (!IpcPaths.IsServiceRunning())
        {
            Console.Error.WriteLine("Error: LCDPossible service is not running.");
            Console.Error.WriteLine("Start the service with: lcdpossible serve");
            return 1;
        }

        try
        {
            var success = NotifyServiceReloadAsync(silent: true).GetAwaiter().GetResult();

            if (success)
            {
                Console.WriteLine("Profile reloaded successfully.");
                return 0;
            }
            else
            {
                Console.Error.WriteLine("Failed to reload profile. Check service logs for details.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error communicating with service: {ex.Message}");
            return 1;
        }
    }

    private static int UnknownSubCommand(string subCommand)
    {
        Console.Error.WriteLine($"Unknown profile sub-command: {subCommand}");
        Console.Error.WriteLine("Use 'lcdpossible profile help' for available commands.");
        return 1;
    }

    /// <summary>
    /// Notifies the running service to reload the profile.
    /// Returns true if reload was successful, false otherwise.
    /// </summary>
    /// <param name="silent">If true, don't print success message (for automatic reload after changes).</param>
    private static async Task<bool> NotifyServiceReloadAsync(bool silent = false)
    {
        if (!IpcPaths.IsServiceRunning())
        {
            return false;
        }

        try
        {
            var response = await IpcClientHelper.SendCommandAsync("reload");
            if (response.Success)
            {
                if (!silent)
                {
                    Console.WriteLine("  [OK] Service notified to reload profile.");
                }
                return true;
            }
            return false;
        }
        catch
        {
            // Service not running or communication error
            return false;
        }
    }

    /// <summary>
    /// Synchronously notifies the service to reload (helper for non-async methods).
    /// </summary>
    private static void NotifyServiceReload()
    {
        try
        {
            NotifyServiceReloadAsync(silent: false).GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore any errors
        }
    }

    private static int ShowProfileHelp()
    {
        Console.WriteLine(@"
PROFILE - Manage display profiles

USAGE: lcdpossible profile <command> [options]

COMMANDS:
  Profile Management:
    show                        Show current profile panels
    list                        List all available profiles
    new <name>                  Create a new profile
    delete <name>               Delete a profile
    reload                      Reload profile in running service

  Panel Management:
    add <panel> [options]       Add a panel to the profile
    remove <index>              Remove panel at index
    move <from> <to>            Move panel to new position

  Settings:
    set <index> <param> <value> Set a panel parameter
    get <index> <param>         Get a panel parameter
    clear <index>               Clear all panel parameters
    set-defaults [options]      Set profile-wide defaults

OPTIONS:
    -p, --profile <name>        Target profile (default: 'default')
    -f, --format json|yaml      Output format for show/list

  add options:
    -d, --duration <sec>        Display duration
    -i, --interval <sec>        Update interval
    -b, --background <path>     Background image

  set-defaults options:
    --duration <sec>            Default display duration
    --interval <sec>            Default update interval
    --transition <type>         Default transition effect
    --transition-duration <ms>  Default transition duration

TRANSITIONS:
    none, fade, crossfade, slide-left, slide-right, slide-up, slide-down,
    wipe-left, wipe-right, wipe-up, wipe-down, zoom-in, zoom-out,
    push-left, push-right, random (default)

EXAMPLES:
    lcdpossible profile show
    lcdpossible profile add cpu-info
    lcdpossible profile add gpu-info -d 30
    lcdpossible profile remove 2
    lcdpossible profile set 0 duration 20
    lcdpossible profile set-defaults --transition fade
    lcdpossible profile new gaming-profile
    lcdpossible profile reload

LOCATION: " + ProfileManager.ProfilesDirectory + @"
");
        return 0;
    }
}
