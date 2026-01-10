using LCDPossible.Core.Devices;
using LCDPossible.Core.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Shouldly;

namespace LCDPossible.Core.Tests.Rendering;

public class Rgb565ImageEncoderTests
{
    [Fact]
    public void ToRgb565_WithRed_ReturnsCorrectValue()
    {
        // Red (255, 0, 0) should encode to 0xF800 in RGB565
        var result = Rgb565ImageEncoder.ToRgb565(255, 0, 0);
        result.ShouldBe((ushort)0xF800);
    }

    [Fact]
    public void ToRgb565_WithGreen_ReturnsCorrectValue()
    {
        // Green (0, 255, 0) should encode to 0x07E0 in RGB565
        var result = Rgb565ImageEncoder.ToRgb565(0, 255, 0);
        result.ShouldBe((ushort)0x07E0);
    }

    [Fact]
    public void ToRgb565_WithBlue_ReturnsCorrectValue()
    {
        // Blue (0, 0, 255) should encode to 0x001F in RGB565
        var result = Rgb565ImageEncoder.ToRgb565(0, 0, 255);
        result.ShouldBe((ushort)0x001F);
    }

    [Fact]
    public void ToRgb565_WithWhite_ReturnsCorrectValue()
    {
        // White (255, 255, 255) should encode to 0xFFFF in RGB565
        var result = Rgb565ImageEncoder.ToRgb565(255, 255, 255);
        result.ShouldBe((ushort)0xFFFF);
    }

    [Fact]
    public void ToRgb565_WithBlack_ReturnsZero()
    {
        // Black (0, 0, 0) should encode to 0x0000
        var result = Rgb565ImageEncoder.ToRgb565(0, 0, 0);
        result.ShouldBe((ushort)0x0000);
    }

    [Fact]
    public void FromRgb565_RoundTrip_PreservesColor()
    {
        // Test round-trip conversion (with expected precision loss)
        var originalRgb565 = Rgb565ImageEncoder.ToRgb565(200, 150, 100);
        var (r, g, b) = Rgb565ImageEncoder.FromRgb565(originalRgb565);

        // RGB565 has reduced precision, so we check within expected tolerance
        r.ShouldBeInRange((byte)192, (byte)207);  // 5-bit red
        g.ShouldBeInRange((byte)148, (byte)155);  // 6-bit green
        b.ShouldBeInRange((byte)96, (byte)103);   // 5-bit blue
    }

    [Fact]
    public void Encode_WithSmallImage_ReturnsCorrectSize()
    {
        // Arrange
        var encoder = new Rgb565ImageEncoder();
        var capabilities = new LcdCapabilities(
            Width: 10,
            Height: 10,
            SupportedFormats: [ColorFormat.Rgb565],
            PreferredFormat: ColorFormat.Rgb565,
            MaxPacketSize: 512,
            MaxFrameRate: 60);

        using var image = new Image<Rgba32>(10, 10);

        // Act
        var result = encoder.Encode(image, capabilities);

        // Assert
        result.Length.ShouldBe(10 * 10 * 2); // 2 bytes per pixel
    }

    [Fact]
    public void Encode_ResizesImage_WhenDimensionsDontMatch()
    {
        // Arrange
        var encoder = new Rgb565ImageEncoder();
        var capabilities = new LcdCapabilities(
            Width: 20,
            Height: 20,
            SupportedFormats: [ColorFormat.Rgb565],
            PreferredFormat: ColorFormat.Rgb565,
            MaxPacketSize: 512,
            MaxFrameRate: 60);

        using var image = new Image<Rgba32>(10, 10); // Different size

        // Act
        var result = encoder.Encode(image, capabilities);

        // Assert - output should match target dimensions, not source
        result.Length.ShouldBe(20 * 20 * 2);
    }
}
