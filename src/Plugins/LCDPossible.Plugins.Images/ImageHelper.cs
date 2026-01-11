namespace LCDPossible.Plugins.Images;

/// <summary>
/// Helper class for handling image files and URLs.
/// </summary>
internal static class ImageHelper
{
    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromMinutes(5),
        DefaultRequestHeaders =
        {
            { "User-Agent", "LCDPossible/1.0 (https://github.com/DevPossible/LCDPossible)" }
        }
    };

    private static readonly string CacheDirectory = Path.Combine(
        Path.GetTempPath(),
        "LCDPossible",
        "ImageCache");

    /// <summary>
    /// Checks if a path is a URL.
    /// </summary>
    public static bool IsUrl(string path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves a path or URL to a local file path.
    /// If the input is a URL, downloads the file to a cache directory.
    /// </summary>
    /// <param name="pathOrUrl">Local file path or URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to the local file.</returns>
    public static async Task<string> ResolveToLocalPathAsync(string pathOrUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            throw new ArgumentException("Path or URL cannot be empty.", nameof(pathOrUrl));
        }

        if (!IsUrl(pathOrUrl))
        {
            // Already a local path
            if (!File.Exists(pathOrUrl))
            {
                throw new FileNotFoundException($"File not found: {pathOrUrl}");
            }
            return pathOrUrl;
        }

        // It's a URL - download to cache
        return await DownloadToCache(pathOrUrl, cancellationToken);
    }

    /// <summary>
    /// Downloads a URL to the cache directory and returns the local path.
    /// Uses content-based caching to avoid re-downloading unchanged files.
    /// </summary>
    private static async Task<string> DownloadToCache(string url, CancellationToken cancellationToken)
    {
        // Ensure cache directory exists
        Directory.CreateDirectory(CacheDirectory);

        // Generate cache filename based on URL hash
        var urlHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(url)))[..16];

        var uri = new Uri(url);
        var extension = Path.GetExtension(uri.AbsolutePath);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".bin";
        }

        var cacheFilePath = Path.Combine(CacheDirectory, $"{urlHash}{extension}");

        // Check if file already exists and is recent (within 1 hour)
        if (File.Exists(cacheFilePath))
        {
            var fileInfo = new FileInfo(cacheFilePath);
            if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc < TimeSpan.FromHours(1))
            {
                return cacheFilePath;
            }
        }

        // Download the file
        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(cacheFilePath);
        await stream.CopyToAsync(fileStream, cancellationToken);

        return cacheFilePath;
    }

    /// <summary>
    /// Clears the image cache directory.
    /// </summary>
    public static void ClearCache()
    {
        if (Directory.Exists(CacheDirectory))
        {
            try
            {
                Directory.Delete(CacheDirectory, recursive: true);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }
}
