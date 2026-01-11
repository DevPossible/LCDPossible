namespace LCDPossible.Core.Ipc;

/// <summary>
/// Interface for the IPC server that listens for CLI commands.
/// </summary>
public interface IIpcServer : IDisposable
{
    /// <summary>
    /// Starts the IPC server and begins accepting connections.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the IPC server and closes all connections.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Whether the server is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Event raised when a request is received from a client.
    /// The handler should process the request and call SendResponseAsync.
    /// </summary>
    event EventHandler<IpcRequestEventArgs>? RequestReceived;
}

/// <summary>
/// Event arguments for IPC request received events.
/// </summary>
public sealed class IpcRequestEventArgs : EventArgs
{
    /// <summary>
    /// The request received from the client.
    /// </summary>
    public required IpcRequest Request { get; init; }

    /// <summary>
    /// Callback to send the response back to the client.
    /// </summary>
    public required Func<IpcResponse, CancellationToken, Task> SendResponseAsync { get; init; }
}
