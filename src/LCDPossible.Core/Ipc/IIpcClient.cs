namespace LCDPossible.Core.Ipc;

/// <summary>
/// Interface for the IPC client that sends commands to the running service.
/// </summary>
public interface IIpcClient : IDisposable
{
    /// <summary>
    /// Whether the client is currently connected to the service.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects to the running service.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for connection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="TimeoutException">Connection timed out.</exception>
    /// <exception cref="IpcException">Connection failed.</exception>
    Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a request to the service and waits for a response.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response from the service.</returns>
    /// <exception cref="IpcException">Communication error.</exception>
    Task<IpcResponse> SendAsync(IpcRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Disconnects from the service.
    /// </summary>
    void Disconnect();
}

/// <summary>
/// Exception thrown when IPC operations fail.
/// </summary>
public class IpcException : Exception
{
    /// <summary>
    /// Error code associated with the exception.
    /// </summary>
    public string? ErrorCode { get; }

    public IpcException(string message) : base(message)
    {
    }

    public IpcException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public IpcException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public IpcException(string message, string errorCode, Exception innerException) : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
