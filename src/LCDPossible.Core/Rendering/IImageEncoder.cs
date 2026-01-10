using LCDPossible.Core.Devices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Core.Rendering;

/// <summary>
/// Interface for encoding images to device-compatible formats.
/// </summary>
public interface IImageEncoder
{
    /// <summary>
    /// Gets the output format this encoder produces.
    /// </summary>
    ColorFormat OutputFormat { get; }

    /// <summary>
    /// Encodes an image to the device-compatible format.
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <param name="capabilities">The device capabilities to encode for.</param>
    /// <returns>The encoded image data.</returns>
    ReadOnlyMemory<byte> Encode(Image<Rgba32> image, LcdCapabilities capabilities);
}
