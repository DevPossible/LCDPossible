namespace LCDPossible.Core.Devices;

/// <summary>
/// Describes the capabilities of an LCD device.
/// </summary>
/// <param name="Width">Display width in pixels.</param>
/// <param name="Height">Display height in pixels.</param>
/// <param name="SupportedFormats">Color formats supported by the device.</param>
/// <param name="PreferredFormat">The preferred color format for optimal performance.</param>
/// <param name="MaxPacketSize">Maximum USB packet size in bytes.</param>
/// <param name="MaxFrameRate">Maximum refresh rate in frames per second.</param>
/// <param name="SupportsBrightness">Whether brightness control is supported.</param>
/// <param name="SupportsOrientation">Whether orientation changes are supported.</param>
public record LcdCapabilities(
    int Width,
    int Height,
    ColorFormat[] SupportedFormats,
    ColorFormat PreferredFormat,
    int MaxPacketSize,
    int MaxFrameRate,
    bool SupportsBrightness = true,
    bool SupportsOrientation = true)
{
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
            var gcd = Gcd(Width, Height);
            return $"{Width / gcd}:{Height / gcd}";
        }
    }

    /// <summary>
    /// Gets the raw frame size in bytes for RGB565 format.
    /// </summary>
    public int Rgb565FrameSize => Width * Height * 2;

    /// <summary>
    /// Gets the raw frame size in bytes for RGB888 format.
    /// </summary>
    public int Rgb888FrameSize => Width * Height * 3;

    /// <summary>
    /// Checks if a specific color format is supported.
    /// </summary>
    public bool SupportsFormat(ColorFormat format) =>
        SupportedFormats.Contains(format);

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
