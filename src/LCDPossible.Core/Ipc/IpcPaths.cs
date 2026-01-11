using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace LCDPossible.Core.Ipc;

/// <summary>
/// Cross-platform utilities for IPC pipe/socket paths and service discovery.
/// </summary>
public static class IpcPaths
{
    /// <summary>
    /// The named pipe name (Windows) or socket file name (Unix).
    /// </summary>
    public const string PipeName = "LCDPossible";

    /// <summary>
    /// Connection timeout for service discovery checks.
    /// </summary>
    private const int DiscoveryTimeoutMs = 200;

    /// <summary>
    /// Gets the platform-specific pipe/socket path.
    /// </summary>
    /// <returns>
    /// Windows: The pipe name (used with NamedPipeClientStream).
    /// Unix: Full path to the Unix domain socket file.
    /// </returns>
    public static string GetPipePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows uses pipe name directly with NamedPipeClientStream
            return PipeName;
        }

        // Unix: Use /run for system-wide socket (works for both systemd service and CLI)
        // This ensures CLI can find the service regardless of XDG_RUNTIME_DIR differences
        // between systemd service context and interactive shell
        if (IsRunningAsRoot())
        {
            return "/run/lcdpossible.sock";
        }

        // Non-root: use XDG_RUNTIME_DIR for user socket, fall back to /tmp
        var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        var baseDir = !string.IsNullOrEmpty(runtimeDir) ? runtimeDir : "/tmp";
        return Path.Combine(baseDir, "lcdpossible.sock");
    }

    /// <summary>
    /// Checks if the current process is running as root (Unix) or elevated (Windows).
    /// </summary>
    private static bool IsRunningAsRoot()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false; // Windows uses named pipes, not affected
        }

        // On Unix, check if effective user ID is 0 (root)
        try
        {
            // Environment.UserName doesn't reliably detect root
            // Use the /proc filesystem to check effective UID
            if (File.Exists("/proc/self/status"))
            {
                var status = File.ReadAllText("/proc/self/status");
                var uidLine = status.Split('\n').FirstOrDefault(l => l.StartsWith("Uid:"));
                if (uidLine != null)
                {
                    var parts = uidLine.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    // Format: Uid: real effective saved filesystem
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var effectiveUid))
                    {
                        return effectiveUid == 0;
                    }
                }
            }

            // Fallback: check username
            return Environment.UserName == "root";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the full Windows named pipe path (for display/logging purposes).
    /// </summary>
    public static string GetFullPipePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $@"\\.\pipe\{PipeName}";
        }
        return GetPipePath();
    }

    /// <summary>
    /// Checks if the LCDPossible service is currently running and accepting connections.
    /// </summary>
    /// <returns>True if service is running and accepting connections.</returns>
    public static bool IsServiceRunning()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return IsServiceRunningWindows();
            }
            else
            {
                return IsServiceRunningUnix();
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the service is running asynchronously.
    /// </summary>
    public static async Task<bool> IsServiceRunningAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await IsServiceRunningWindowsAsync(cancellationToken);
            }
            else
            {
                return await IsServiceRunningUnixAsync(cancellationToken);
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool IsServiceRunningWindows()
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            client.Connect(DiscoveryTimeoutMs);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            // Pipe exists but connection failed - service might be busy
            // Consider it running
            return true;
        }
    }

    private static async Task<bool> IsServiceRunningWindowsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(DiscoveryTimeoutMs);

            await client.ConnectAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (IOException)
        {
            // Pipe exists but connection failed - service might be busy
            return true;
        }
    }

    private static bool IsServiceRunningUnix()
    {
        var socketPath = GetPipePath();
        if (!File.Exists(socketPath))
            return false;

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.ReceiveTimeout = DiscoveryTimeoutMs;
            socket.SendTimeout = DiscoveryTimeoutMs;
            socket.Connect(new UnixDomainSocketEndPoint(socketPath));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static async Task<bool> IsServiceRunningUnixAsync(CancellationToken cancellationToken)
    {
        var socketPath = GetPipePath();
        if (!File.Exists(socketPath))
            return false;

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(DiscoveryTimeoutMs);

            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    /// <summary>
    /// Cleans up stale socket files (Unix only).
    /// Call this when starting the server to remove leftover socket files from crashed processes.
    /// </summary>
    public static void CleanupStaleSocket()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var socketPath = GetPipePath();
        if (!File.Exists(socketPath))
            return;

        // Try to connect - if it fails, the socket is stale
        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.ReceiveTimeout = 100;
            socket.SendTimeout = 100;
            socket.Connect(new UnixDomainSocketEndPoint(socketPath));
            // Connection succeeded - socket is in use, don't delete
        }
        catch (SocketException)
        {
            // Connection failed - socket is stale, delete it
            try
            {
                File.Delete(socketPath);
            }
            catch
            {
                // Ignore deletion errors
            }
        }
    }
}
