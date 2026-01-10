using LCDPossible.Core.Devices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Core.Rendering;

/// <summary>
/// Encodes images to JPEG format for LCD display.
/// </summary>
public sealed class JpegImageEncoder : IImageEncoder
{
    /// <summary>
    /// Gets or sets the JPEG quality (1-100). Default is 95.
    /// </summary>
    public int Quality { get; set; } = 95;

    public ColorFormat OutputFormat => ColorFormat.Jpeg;

    public ReadOnlyMemory<byte> Encode(Image<Rgba32> image, LcdCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(capabilities);

        // Resize image to match device resolution if needed
        var resized = image;
        var shouldDispose = false;

        if (image.Width != capabilities.Width || image.Height != capabilities.Height)
        {
            resized = image.Clone(ctx => ctx.Resize(capabilities.Width, capabilities.Height));
            shouldDispose = true;
        }

        try
        {
            using var ms = new MemoryStream();
            resized.SaveAsJpeg(ms, new JpegEncoder { Quality = Quality });
            return ms.ToArray();
        }
        finally
        {
            if (shouldDispose)
            {
                resized.Dispose();
            }
        }
    }
}
