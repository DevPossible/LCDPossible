using LCDPossible.Core.Devices;
using Shouldly;

namespace LCDPossible.Core.Tests.Devices;

public class LcdCapabilitiesTests
{
    [Fact]
    public void TotalPixels_ReturnsCorrectValue()
    {
        var capabilities = new LcdCapabilities(
            Width: 1280,
            Height: 480,
            SupportedFormats: [ColorFormat.Jpeg],
            PreferredFormat: ColorFormat.Jpeg,
            MaxPacketSize: 512,
            MaxFrameRate: 60);

        capabilities.TotalPixels.ShouldBe(1280 * 480);
    }

    [Fact]
    public void AspectRatio_For1280x480_Returns8to3()
    {
        var capabilities = new LcdCapabilities(
            Width: 1280,
            Height: 480,
            SupportedFormats: [ColorFormat.Jpeg],
            PreferredFormat: ColorFormat.Jpeg,
            MaxPacketSize: 512,
            MaxFrameRate: 60);

        capabilities.AspectRatio.ShouldBe("8:3");
    }

    [Fact]
    public void AspectRatio_For1920x1080_Returns16to9()
    {
        var capabilities = new LcdCapabilities(
            Width: 1920,
            Height: 1080,
            SupportedFormats: [ColorFormat.Jpeg],
            PreferredFormat: ColorFormat.Jpeg,
            MaxPacketSize: 512,
            MaxFrameRate: 60);

        capabilities.AspectRatio.ShouldBe("16:9");
    }

    [Fact]
    public void Rgb565FrameSize_ReturnsCorrectValue()
    {
        var capabilities = new LcdCapabilities(
            Width: 100,
            Height: 50,
            SupportedFormats: [ColorFormat.Rgb565],
            PreferredFormat: ColorFormat.Rgb565,
            MaxPacketSize: 512,
            MaxFrameRate: 60);

        // 2 bytes per pixel
        capabilities.Rgb565FrameSize.ShouldBe(100 * 50 * 2);
    }

    [Fact]
    public void Rgb888FrameSize_ReturnsCorrectValue()
    {
        var capabilities = new LcdCapabilities(
            Width: 100,
            Height: 50,
            SupportedFormats: [ColorFormat.Rgb888],
            PreferredFormat: ColorFormat.Rgb888,
            MaxPacketSize: 512,
            MaxFrameRate: 60);

        // 3 bytes per pixel
        capabilities.Rgb888FrameSize.ShouldBe(100 * 50 * 3);
    }

    [Fact]
    public void SupportsFormat_WithSupportedFormat_ReturnsTrue()
    {
        var capabilities = new LcdCapabilities(
            Width: 1280,
            Height: 480,
            SupportedFormats: [ColorFormat.Jpeg, ColorFormat.Rgb565],
            PreferredFormat: ColorFormat.Jpeg,
            MaxPacketSize: 512,
            MaxFrameRate: 60);

        capabilities.SupportsFormat(ColorFormat.Jpeg).ShouldBeTrue();
        capabilities.SupportsFormat(ColorFormat.Rgb565).ShouldBeTrue();
    }

    [Fact]
    public void SupportsFormat_WithUnsupportedFormat_ReturnsFalse()
    {
        var capabilities = new LcdCapabilities(
            Width: 1280,
            Height: 480,
            SupportedFormats: [ColorFormat.Jpeg],
            PreferredFormat: ColorFormat.Jpeg,
            MaxPacketSize: 512,
            MaxFrameRate: 60);

        capabilities.SupportsFormat(ColorFormat.Rgb565).ShouldBeFalse();
    }
}
