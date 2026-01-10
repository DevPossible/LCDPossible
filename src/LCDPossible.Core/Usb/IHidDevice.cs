namespace LCDPossible.Core.Usb;

/// <summary>
/// Abstraction over a USB HID device for cross-platform communication.
/// </summary>
public interface IHidDevice : IDisposable
{
    /// <summary>
    /// Gets the device path (platform-specific identifier).
    /// </summary>
    string DevicePath { get; }

    /// <summary>
    /// Gets the USB vendor ID.
    /// </summary>
    ushort VendorId { get; }

    /// <summary>
    /// Gets the USB product ID.
    /// </summary>
    ushort ProductId { get; }

    /// <summary>
    /// Gets the manufacturer name.
    /// </summary>
    string? Manufacturer { get; }

    /// <summary>
    /// Gets the product name.
    /// </summary>
    string? ProductName { get; }

    /// <summary>
    /// Gets whether the device is currently open for communication.
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// Gets the maximum size of output reports (data sent to device).
    /// </summary>
    int MaxOutputReportLength { get; }

    /// <summary>
    /// Gets the maximum size of input reports (data received from device).
    /// </summary>
    int MaxInputReportLength { get; }

    /// <summary>
    /// Opens the device for communication.
    /// </summary>
    /// <exception cref="InvalidOperationException">Device is already open.</exception>
    /// <exception cref="IOException">Failed to open device.</exception>
    void Open();

    /// <summary>
    /// Closes the device connection.
    /// </summary>
    void Close();

    /// <summary>
    /// Writes data to the device synchronously.
    /// </summary>
    /// <param name="data">The data to write.</param>
    /// <exception cref="InvalidOperationException">Device is not open.</exception>
    /// <exception cref="IOException">Write operation failed.</exception>
    void Write(ReadOnlySpan<byte> data);

    /// <summary>
    /// Reads data from the device synchronously.
    /// </summary>
    /// <param name="buffer">The buffer to read into.</param>
    /// <param name="timeout">Read timeout in milliseconds.</param>
    /// <returns>The number of bytes read.</returns>
    /// <exception cref="InvalidOperationException">Device is not open.</exception>
    /// <exception cref="TimeoutException">Read operation timed out.</exception>
    int Read(Span<byte> buffer, int timeout = 1000);

    /// <summary>
    /// Writes data to the device asynchronously.
    /// </summary>
    /// <param name="data">The data to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Device is not open.</exception>
    /// <exception cref="IOException">Write operation failed.</exception>
    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads data from the device asynchronously.
    /// </summary>
    /// <param name="buffer">The buffer to read into.</param>
    /// <param name="timeout">Read timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of bytes read.</returns>
    /// <exception cref="InvalidOperationException">Device is not open.</exception>
    /// <exception cref="TimeoutException">Read operation timed out.</exception>
    Task<int> ReadAsync(Memory<byte> buffer, int timeout = 1000, CancellationToken cancellationToken = default);
}
