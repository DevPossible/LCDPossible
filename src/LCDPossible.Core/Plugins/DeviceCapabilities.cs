namespace LCDPossible.Core.Plugins;

/// <summary>
/// Describes the capabilities of an LCD device or virtual device protocol.
/// </summary>
public sealed record DeviceCapabilities
{
    /// <summary>
    /// Display width in pixels.
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// Display height in pixels.
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// Maximum packet size in bytes for data transmission.
    /// </summary>
    public required int MaxPacketSize { get; init; }

    /// <summary>
    /// Maximum supported frame rate in FPS.
    /// </summary>
    public int MaxFrameRate { get; init; } = 60;

    /// <summary>
    /// Whether the device supports brightness control.
    /// </summary>
    public bool SupportsBrightness { get; init; }

    /// <summary>
    /// Whether the device supports orientation changes.
    /// </summary>
    public bool SupportsOrientation { get; init; }

    /// <summary>
    /// Gets the total number of pixels.
    /// </summary>
    public int TotalPixels => Width * Height;

    /// <summary>
    /// Gets the aspect ratio as width:height.
    /// </summary>
    public string AspectRatio
    {
        get
        {
            if (Width == 0 || Height == 0) return "0:0";
            var gcd = Gcd(Width, Height);
            return $"{Width / gcd}:{Height / gcd}";
        }
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            var temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }
}
