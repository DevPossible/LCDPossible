namespace LCDPossible.Core.Devices;

/// <summary>
/// Supported color formats for LCD devices.
/// </summary>
public enum ColorFormat
{
    /// <summary>
    /// 16-bit RGB565 format (5 bits red, 6 bits green, 5 bits blue).
    /// </summary>
    Rgb565,

    /// <summary>
    /// 24-bit RGB888 format (8 bits per channel).
    /// </summary>
    Rgb888,

    /// <summary>
    /// JPEG compressed format.
    /// </summary>
    Jpeg
}
