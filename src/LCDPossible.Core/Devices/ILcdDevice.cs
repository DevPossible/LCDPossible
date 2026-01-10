namespace LCDPossible.Core.Devices;

/// <summary>
/// Interface for LCD device drivers.
/// </summary>
public interface ILcdDevice : IDisposable
{
    /// <summary>
    /// Gets metadata about this device.
    /// </summary>
    DeviceInfo Info { get; }

    /// <summary>
    /// Gets the capabilities of this LCD device.
    /// </summary>
    LcdCapabilities Capabilities { get; }

    /// <summary>
    /// Gets whether the device is currently connected and ready.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects to the device and initializes it.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="IOException">Failed to connect to device.</exception>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a frame to the LCD display.
    /// </summary>
    /// <param name="frameData">The encoded frame data (JPEG or raw pixel data).</param>
    /// <param name="format">The format of the frame data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Device is not connected.</exception>
    /// <exception cref="IOException">Failed to send frame.</exception>
    Task SendFrameAsync(ReadOnlyMemory<byte> frameData, ColorFormat format, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the display brightness.
    /// </summary>
    /// <param name="brightness">Brightness level (0-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Device is not connected or doesn't support brightness.</exception>
    Task SetBrightnessAsync(byte brightness, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the display orientation.
    /// </summary>
    /// <param name="orientation">The desired orientation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Device is not connected or doesn't support orientation.</exception>
    Task SetOrientationAsync(Orientation orientation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when the device is disconnected unexpectedly.
    /// </summary>
    event EventHandler? Disconnected;
}
