using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace LCDPossible.Panels;

/// <summary>
/// Helper class for handling media files and URLs.
/// </summary>
public static class MediaHelper
{
    private static readonly HttpClient _httpClient = new(new HttpClientHandler
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

    private static readonly YoutubeClient _youtubeClient = new();

    private static readonly string _cacheDirectory = Path.Combine(
        Path.GetTempPath(),
        "LCDPossible",
        "MediaCache");

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
    /// Checks if a URL is a YouTube video URL.
    /// </summary>
    public static bool IsYouTubeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return url.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("youtube.com/embed/", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("youtube.com/shorts/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the direct stream URL for a YouTube video.
    /// Returns the best quality stream URL that LibVLC can play.
    /// </summary>
    /// <param name="youtubeUrl">The YouTube video URL.</param>
    /// <param name="preferredQuality">Preferred quality (360, 480, 720, 1080). Default: 480.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Direct stream URL for the video.</returns>
    public static async Task<string> GetYouTubeStreamUrlAsync(
        string youtubeUrl,
        int preferredQuality = 480,
        CancellationToken cancellationToken = default)
    {
        var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(youtubeUrl, cancellationToken);

        // Try to get a muxed stream (video + audio combined) for simplicity
        var muxedStreams = streamManifest.GetMuxedStreams()
            .OrderBy(s => Math.Abs(s.VideoResolution.Height - preferredQuality))
            .ToList();

        if (muxedStreams.Count > 0)
        {
            return muxedStreams[0].Url;
        }

        // Fallback to video-only stream if no muxed available
        var videoStreams = streamManifest.GetVideoOnlyStreams()
            .OrderBy(s => Math.Abs(s.VideoResolution.Height - preferredQuality))
            .ToList();

        if (videoStreams.Count > 0)
        {
            return videoStreams[0].Url;
        }

        throw new InvalidOperationException($"No suitable video stream found for: {youtubeUrl}");
    }

    /// <summary>
    /// Resolves a video path/URL to a streamable URL.
    /// Handles local files, direct URLs, and YouTube URLs.
    /// </summary>
    /// <param name="pathOrUrl">Local file path, direct URL, or YouTube URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>URL or file path that can be played by LibVLC.</returns>
    public static async Task<string> ResolveVideoSourceAsync(string pathOrUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            throw new ArgumentException("Path or URL cannot be empty.", nameof(pathOrUrl));
        }

        // Check if it's a YouTube URL
        if (IsYouTubeUrl(pathOrUrl))
        {
            return await GetYouTubeStreamUrlAsync(pathOrUrl, cancellationToken: cancellationToken);
        }

        // Check if it's a regular URL (LibVLC can stream directly)
        if (IsUrl(pathOrUrl))
        {
            return pathOrUrl;
        }

        // It's a local file path
        if (!File.Exists(pathOrUrl))
        {
            throw new FileNotFoundException($"Video file not found: {pathOrUrl}");
        }

        return pathOrUrl;
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
        Directory.CreateDirectory(_cacheDirectory);

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

        var cacheFilePath = Path.Combine(_cacheDirectory, $"{urlHash}{extension}");

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
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(cacheFilePath);
        await stream.CopyToAsync(fileStream, cancellationToken);

        return cacheFilePath;
    }

    /// <summary>
    /// Clears the media cache directory.
    /// </summary>
    public static void ClearCache()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            try
            {
                Directory.Delete(_cacheDirectory, recursive: true);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }

    /// <summary>
    /// Clears cache files older than the specified age.
    /// </summary>
    public static void ClearOldCache(TimeSpan maxAge)
    {
        if (!Directory.Exists(_cacheDirectory))
        {
            return;
        }

        var cutoff = DateTime.UtcNow - maxAge;

        foreach (var file in Directory.EnumerateFiles(_cacheDirectory))
        {
            try
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTimeUtc < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }
}
