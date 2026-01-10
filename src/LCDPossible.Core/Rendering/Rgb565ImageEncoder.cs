using LCDPossible.Core.Devices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Core.Rendering;

/// <summary>
/// Encodes images to RGB565 format for LCD display.
/// </summary>
public sealed class Rgb565ImageEncoder : IImageEncoder
{
    public ColorFormat OutputFormat => ColorFormat.Rgb565;

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
            var buffer = new byte[capabilities.Rgb565FrameSize];

            resized.ProcessPixelRows(accessor =>
            {
                var offset = 0;
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    foreach (var pixel in row)
                    {
                        // RGB565 encoding (little-endian)
                        // Format: RRRRRGGG GGGBBBBB
                        var rgb565 = (ushort)(
                            ((pixel.R & 0xF8) << 8) |  // 5 bits of red
                            ((pixel.G & 0xFC) << 3) |  // 6 bits of green
                            (pixel.B >> 3)             // 5 bits of blue
                        );

                        // Little-endian byte order
                        buffer[offset++] = (byte)(rgb565 & 0xFF);
                        buffer[offset++] = (byte)(rgb565 >> 8);
                    }
                }
            });

            return buffer;
        }
        finally
        {
            if (shouldDispose)
            {
                resized.Dispose();
            }
        }
    }

    /// <summary>
    /// Converts a single RGB color to RGB565 format.
    /// </summary>
    public static ushort ToRgb565(byte r, byte g, byte b)
    {
        return (ushort)(
            ((r & 0xF8) << 8) |
            ((g & 0xFC) << 3) |
            (b >> 3)
        );
    }

    /// <summary>
    /// Converts RGB565 value back to RGB888.
    /// </summary>
    public static (byte R, byte G, byte B) FromRgb565(ushort rgb565)
    {
        var r = (byte)((rgb565 >> 11) << 3);
        var g = (byte)(((rgb565 >> 5) & 0x3F) << 2);
        var b = (byte)((rgb565 & 0x1F) << 3);
        return (r, g, b);
    }
}
