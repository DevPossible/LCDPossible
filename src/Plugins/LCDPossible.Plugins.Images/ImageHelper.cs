using LCDPossible.Core.Caching;

namespace LCDPossible.Plugins.Images;

/// <summary>
/// Helper class for handling image files and URLs.
/// Delegates URL caching to the centralized AssetCache.
/// </summary>
internal static class ImageHelper
{
    /// <summary>
    /// Checks if a path is a URL.
    /// </summary>
    public static bool IsUrl(string path) => AssetCache.IsUrl(path);

    /// <summary>
    /// Resolves a path or URL to a local file path.
    /// If the input is a URL, downloads the file to a cache directory.
    /// </summary>
    /// <param name="pathOrUrl">Local file path or URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to the local file.</returns>
    public static Task<string> ResolveToLocalPathAsync(string pathOrUrl, CancellationToken cancellationToken = default)
        => AssetCache.ResolveToLocalPathAsync(pathOrUrl, cancellationToken);

    /// <summary>
    /// Clears the image cache directory.
    /// </summary>
    public static void ClearCache() => AssetCache.Clear();
}
