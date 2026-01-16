namespace LCDPossible.Core.Services;

/// <summary>
/// Registry for display themes.
/// </summary>
public interface IThemeRegistry
{
    /// <summary>
    /// Get the current active theme.
    /// </summary>
    ThemeInfo CurrentTheme { get; }

    /// <summary>
    /// Get all available themes.
    /// </summary>
    IReadOnlyList<ThemeInfo> GetThemes();

    /// <summary>
    /// Get a theme by ID.
    /// </summary>
    ThemeInfo? GetTheme(string themeId);

    /// <summary>
    /// Set the active theme.
    /// </summary>
    void SetTheme(string themeId);
}

/// <summary>
/// Theme information.
/// </summary>
/// <param name="ThemeId">Unique theme identifier.</param>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="Category">Theme category (e.g., "Gamer", "Corporate").</param>
/// <param name="Description">Theme description.</param>
public record ThemeInfo(
    string ThemeId,
    string DisplayName,
    string Category,
    string? Description = null);
