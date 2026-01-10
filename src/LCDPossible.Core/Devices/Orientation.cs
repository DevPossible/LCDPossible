namespace LCDPossible.Core.Devices;

/// <summary>
/// Display orientation options.
/// </summary>
public enum Orientation
{
    /// <summary>
    /// Landscape orientation (0 degrees).
    /// </summary>
    Landscape0 = 0,

    /// <summary>
    /// Portrait orientation (90 degrees clockwise).
    /// </summary>
    Portrait90 = 90,

    /// <summary>
    /// Landscape orientation upside down (180 degrees).
    /// </summary>
    Landscape180 = 180,

    /// <summary>
    /// Portrait orientation (270 degrees clockwise / 90 degrees counter-clockwise).
    /// </summary>
    Portrait270 = 270
}
