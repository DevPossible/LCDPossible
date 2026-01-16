namespace LCDPossible.Core.Plugins;

/// <summary>
/// Handles data received by a virtual device (simulator).
/// The plugin implements this to decode protocol bytes and expose processed data.
/// </summary>
public interface IVirtualDeviceHandler : IDisposable
{
    /// <summary>
    /// The protocol ID this handler processes.
    /// </summary>
    string ProtocolId { get; }

    /// <summary>
    /// Device capabilities for this protocol.
    /// </summary>
    DeviceCapabilities Capabilities { get; }

    /// <summary>
    /// Processes raw protocol data received from the network.
    /// Call this for each UDP packet received.
    /// </summary>
    /// <param name="data">Raw protocol bytes.</param>
    void ProcessData(ReadOnlySpan<byte> data);

    /// <summary>
    /// Resets the handler state (e.g., clears accumulated packet data).
    /// </summary>
    void Reset();

    /// <summary>
    /// Event raised when data has been processed and a frame is ready.
    /// </summary>
    event EventHandler<DataProcessedEventArgs>? DataProcessed;
}
