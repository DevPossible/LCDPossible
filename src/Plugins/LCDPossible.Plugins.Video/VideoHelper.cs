using System.Diagnostics;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace LCDPossible.Plugins.Video;

/// <summary>
/// Helper class for handling video files and URLs.
/// </summary>
internal static class VideoHelper
{
    private static readonly YoutubeClient YoutubeClient = new();
    private static bool? _ytDlpAvailable;

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
    /// Tries yt-dlp first (more reliable), falls back to YoutubeExplode.
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
        // Try yt-dlp first (more reliable with YouTube's anti-bot measures)
        if (IsYtDlpAvailable())
        {
            try
            {
                var url = await GetYouTubeStreamUrlViaYtDlpAsync(youtubeUrl, preferredQuality, cancellationToken);
                if (!string.IsNullOrEmpty(url))
                {
                    return url;
                }
            }
            catch
            {
                // Fall through to YoutubeExplode
            }
        }

        // Fallback to YoutubeExplode
        return await GetYouTubeStreamUrlViaExplodeAsync(youtubeUrl, preferredQuality, cancellationToken);
    }

    /// <summary>
    /// Checks if yt-dlp is available on the system.
    /// </summary>
    private static bool IsYtDlpAvailable()
    {
        if (_ytDlpAvailable.HasValue)
        {
            return _ytDlpAvailable.Value;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit(3000);
            _ytDlpAvailable = process.ExitCode == 0;
        }
        catch
        {
            _ytDlpAvailable = false;
        }

        return _ytDlpAvailable.Value;
    }

    /// <summary>
    /// Gets YouTube stream URL using yt-dlp command-line tool.
    /// </summary>
    private static async Task<string> GetYouTubeStreamUrlViaYtDlpAsync(
        string youtubeUrl,
        int preferredQuality,
        CancellationToken cancellationToken)
    {
        // Format: best video+audio up to specified height, or best available
        var formatSpec = $"bestvideo[height<={preferredQuality}]+bestaudio/best[height<={preferredQuality}]/best";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp",
                Arguments = $"-f \"{formatSpec}\" -g \"{youtubeUrl}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"yt-dlp failed: {error}");
        }

        // yt-dlp -g returns the URL(s), take the first one (video URL)
        var urls = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return urls.Length > 0 ? urls[0].Trim() : string.Empty;
    }

    /// <summary>
    /// Gets YouTube stream URL using YoutubeExplode library.
    /// </summary>
    private static async Task<string> GetYouTubeStreamUrlViaExplodeAsync(
        string youtubeUrl,
        int preferredQuality,
        CancellationToken cancellationToken)
    {
        var streamManifest = await YoutubeClient.Videos.Streams.GetManifestAsync(youtubeUrl, cancellationToken);

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
}
