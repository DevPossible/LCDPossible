using System.Runtime.InteropServices;

namespace LCDPossible.Core;

/// <summary>
/// Cross-platform path utilities for user data, plugins, and configuration.
/// </summary>
public static class PlatformPaths
{
    private const string AppName = "LCDPossible";

    /// <summary>
    /// Environment variable name for overriding the data directory.
    /// Useful for testing and portable installations.
    /// </summary>
    public const string DataDirectoryEnvVar = "LCDPOSSIBLE_DATA_DIR";

    /// <summary>
    /// Gets the user-specific application data directory.
    /// Checks LCDPOSSIBLE_DATA_DIR environment variable first for override.
    /// Windows: %APPDATA%\LCDPossible (C:\Users\{user}\AppData\Roaming\LCDPossible)
    /// Linux:   ~/.config/LCDPossible (respects XDG_CONFIG_HOME)
    /// macOS:   ~/Library/Application Support/LCDPossible
    /// </summary>
    public static string GetUserDataDirectory()
    {
        // Check for environment variable override (useful for testing and portable mode)
        var envOverride = Environment.GetEnvironmentVariable(DataDirectoryEnvVar);
        if (!string.IsNullOrEmpty(envOverride))
        {
            return envOverride;
        }

        string basePath;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Respect XDG Base Directory Specification
            var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            basePath = !string.IsNullOrEmpty(xdgConfig)
                ? xdgConfig
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support");
        }
        else
        {
            // Fallback for other platforms
            basePath = AppContext.BaseDirectory;
        }

        return Path.Combine(basePath, AppName);
    }

    /// <summary>
    /// Gets the user plugins directory: {UserData}/plugins/
    /// Third-party plugins are loaded from this location.
    /// </summary>
    public static string GetUserPluginsDirectory()
    {
        return Path.Combine(GetUserDataDirectory(), "plugins");
    }

    /// <summary>
    /// Gets the built-in plugins directory: {AppDir}/plugins/
    /// Plugins shipped with the application are loaded from here.
    /// </summary>
    public static string GetBuiltInPluginsDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, "plugins");
    }

    /// <summary>
    /// Gets the profile path: {UserData}/profile.yaml
    /// </summary>
    public static string GetProfilePath()
    {
        return Path.Combine(GetUserDataDirectory(), "profile.yaml");
    }

    /// <summary>
    /// Gets the plugin-specific data directory: {UserData}/plugin-data/{pluginId}/
    /// Each plugin can store configuration, cache, and other data here.
    /// </summary>
    public static string GetPluginDataDirectory(string pluginId)
    {
        // Sanitize plugin ID for use as directory name
        var safeName = SanitizeDirectoryName(pluginId);
        return Path.Combine(GetUserDataDirectory(), "plugin-data", safeName);
    }

    /// <summary>
    /// Gets the logs directory: {UserData}/logs/
    /// </summary>
    public static string GetLogsDirectory()
    {
        return Path.Combine(GetUserDataDirectory(), "logs");
    }

    /// <summary>
    /// Gets the cache directory: {UserData}/cache/
    /// For temporary cached data that can be safely deleted.
    /// </summary>
    public static string GetCacheDirectory()
    {
        return Path.Combine(GetUserDataDirectory(), "cache");
    }

    /// <summary>
    /// Ensures the user data directory and common subdirectories exist.
    /// </summary>
    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(GetUserDataDirectory());
        Directory.CreateDirectory(GetUserPluginsDirectory());
        Directory.CreateDirectory(GetLogsDirectory());
    }

    /// <summary>
    /// Ensures the plugin data directory exists for a specific plugin.
    /// </summary>
    public static string EnsurePluginDataDirectory(string pluginId)
    {
        var path = GetPluginDataDirectory(pluginId);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Gets possible profile paths in order of priority.
    /// Searches user directory first, then current directory, then app directory.
    /// </summary>
    public static IEnumerable<string> GetProfileSearchPaths()
    {
        // User data directory (preferred)
        yield return GetProfilePath();

        // Current working directory (legacy support)
        yield return Path.Combine(Directory.GetCurrentDirectory(), "profile.yaml");

        // Application directory (installed alongside app)
        yield return Path.Combine(AppContext.BaseDirectory, "profile.yaml");
    }

    /// <summary>
    /// Finds the first existing profile from search paths.
    /// </summary>
    public static string? FindExistingProfile()
    {
        foreach (var path in GetProfileSearchPaths())
        {
            if (File.Exists(path))
            {
                return path;
            }
        }
        return null;
    }

    /// <summary>
    /// Sanitizes a string for use as a directory name.
    /// </summary>
    private static string SanitizeDirectoryName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "unknown";
        }

        // Replace invalid characters with underscores
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

        // Limit length
        if (sanitized.Length > 100)
        {
            sanitized = sanitized[..100];
        }

        return sanitized;
    }
}
