namespace LCDPossible.Core.Plugins;

/// <summary>
/// Event arguments for when a virtual device handler processes data.
/// </summary>
public sealed class DataProcessedEventArgs : EventArgs
{
    /// <summary>
    /// The processed frame data (decoded image in JPEG or raw format).
    /// </summary>
    public ReadOnlyMemory<byte>? FrameData { get; init; }

    /// <summary>
    /// The format of the frame data if available.
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// Width of the frame if applicable.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    /// Height of the frame if applicable.
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// Custom data from the handler (device-specific).
    /// </summary>
    public object? CustomData { get; init; }

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Whether processing was successful.
    /// </summary>
    public bool IsSuccess => Error is null;
}
