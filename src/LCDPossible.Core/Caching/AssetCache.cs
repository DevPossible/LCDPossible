using System.Security.Cryptography;
using System.Text;

namespace LCDPossible.Core.Caching;

/// <summary>
/// Provides centralized URL-to-file caching for downloaded assets.
/// Cache has no automatic expiration - it persists until explicitly cleared (e.g., on profile reload).
/// </summary>
public static class AssetCache
{
    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromMinutes(5),
        DefaultRequestHeaders =
        {
            { "User-Agent", "LCDPossible/1.0 (https://github.com/DevPossible/lcd-possible)" }
        }
    };

    private static readonly object CacheLock = new();

    /// <summary>
    /// Gets the cache directory path.
    /// </summary>
    public static string CacheDirectory { get; } = Path.Combine(
        Path.GetTempPath(),
        "LCDPossible",
        "AssetCache");

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
    /// If the input is a URL, downloads the file to cache (if not already cached).
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

        // It's a URL - download to cache if not already present
        return await DownloadToCacheAsync(pathOrUrl, cancellationToken);
    }

    /// <summary>
    /// Downloads a URL to the cache directory and returns the local path.
    /// If the file is already cached, returns the cached path without re-downloading.
    /// </summary>
    /// <param name="url">The URL to download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to the cached file.</returns>
    public static async Task<string> DownloadToCacheAsync(string url, CancellationToken cancellationToken = default)
    {
        // Generate cache filename based on URL hash
        var urlHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..16];

        var uri = new Uri(url);
        var extension = Path.GetExtension(uri.AbsolutePath);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".bin";
        }

        var cacheFilePath = Path.Combine(CacheDirectory, $"{urlHash}{extension}");

        // Check if file already exists in cache (no expiration check)
        if (File.Exists(cacheFilePath))
        {
            return cacheFilePath;
        }

        // Ensure cache directory exists
        lock (CacheLock)
        {
            Directory.CreateDirectory(CacheDirectory);
        }

        // Download the file
        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Write to a temp file first, then move (atomic operation)
        var tempFilePath = cacheFilePath + ".tmp";
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = File.Create(tempFilePath);
            await stream.CopyToAsync(fileStream, cancellationToken);
        }
        catch
        {
            // Clean up temp file on failure
            try { File.Delete(tempFilePath); } catch { /* ignore */ }
            throw;
        }

        // Move temp file to final location
        lock (CacheLock)
        {
            if (File.Exists(cacheFilePath))
            {
                // Another thread already downloaded it
                try { File.Delete(tempFilePath); } catch { /* ignore */ }
            }
            else
            {
                File.Move(tempFilePath, cacheFilePath);
            }
        }

        return cacheFilePath;
    }

    /// <summary>
    /// Checks if a URL is already cached.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <returns>True if the URL is cached, false otherwise.</returns>
    public static bool IsCached(string url)
    {
        var urlHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..16];
        var uri = new Uri(url);
        var extension = Path.GetExtension(uri.AbsolutePath);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".bin";
        }

        var cacheFilePath = Path.Combine(CacheDirectory, $"{urlHash}{extension}");
        return File.Exists(cacheFilePath);
    }

    /// <summary>
    /// Gets the cached file path for a URL, or null if not cached.
    /// </summary>
    /// <param name="url">The URL to look up.</param>
    /// <returns>Path to the cached file, or null if not cached.</returns>
    public static string? GetCachedPath(string url)
    {
        var urlHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..16];
        var uri = new Uri(url);
        var extension = Path.GetExtension(uri.AbsolutePath);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".bin";
        }

        var cacheFilePath = Path.Combine(CacheDirectory, $"{urlHash}{extension}");
        return File.Exists(cacheFilePath) ? cacheFilePath : null;
    }

    /// <summary>
    /// Clears the entire URL cache.
    /// Called on profile reload to ensure fresh content.
    /// </summary>
    public static void Clear()
    {
        lock (CacheLock)
        {
            if (Directory.Exists(CacheDirectory))
            {
                try
                {
                    Directory.Delete(CacheDirectory, recursive: true);
                }
                catch
                {
                    // If we can't delete the directory, try to delete individual files
                    try
                    {
                        foreach (var file in Directory.GetFiles(CacheDirectory))
                        {
                            try { File.Delete(file); } catch { /* ignore */ }
                        }
                    }
                    catch
                    {
                        // Ignore errors during cleanup
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets statistics about the cache.
    /// </summary>
    /// <returns>Cache statistics including file count and total size.</returns>
    public static (int FileCount, long TotalBytes) GetStatistics()
    {
        if (!Directory.Exists(CacheDirectory))
        {
            return (0, 0);
        }

        try
        {
            var files = Directory.GetFiles(CacheDirectory);
            var totalBytes = files.Sum(f => new FileInfo(f).Length);
            return (files.Length, totalBytes);
        }
        catch
        {
            return (0, 0);
        }
    }
}
