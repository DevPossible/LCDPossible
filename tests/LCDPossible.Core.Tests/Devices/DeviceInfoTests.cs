using LCDPossible.Core.Devices;
using Shouldly;

namespace LCDPossible.Core.Tests.Devices;

public class DeviceInfoTests
{
    [Fact]
    public void UniqueId_ContainsVidAndPid()
    {
        var deviceInfo = new DeviceInfo(
            VendorId: 0x0416,
            ProductId: 0x5302,
            Name: "Test Device",
            Manufacturer: "Test Manufacturer",
            DriverName: "TestDriver",
            DevicePath: "/dev/hidraw0");

        deviceInfo.UniqueId.ShouldContain("0416");
        deviceInfo.UniqueId.ShouldContain("5302");
    }

    [Fact]
    public void ToString_ContainsDeviceName()
    {
        var deviceInfo = new DeviceInfo(
            VendorId: 0x0416,
            ProductId: 0x5302,
            Name: "Trofeo Vision",
            Manufacturer: "Thermalright",
            DriverName: "TestDriver",
            DevicePath: "/dev/hidraw0");

        deviceInfo.ToString().ShouldContain("Trofeo Vision");
        deviceInfo.ToString().ShouldContain("Thermalright");
    }

    [Fact]
    public void ToString_ContainsVidPid()
    {
        var deviceInfo = new DeviceInfo(
            VendorId: 0x0416,
            ProductId: 0x5302,
            Name: "Test Device",
            Manufacturer: "Test",
            DriverName: "TestDriver",
            DevicePath: "/dev/hidraw0");

        deviceInfo.ToString().ShouldContain("0416");
        deviceInfo.ToString().ShouldContain("5302");
    }

    [Fact]
    public void SerialNumber_CanBeNull()
    {
        var deviceInfo = new DeviceInfo(
            VendorId: 0x0416,
            ProductId: 0x5302,
            Name: "Test Device",
            Manufacturer: "Test",
            DriverName: "TestDriver",
            DevicePath: "/dev/hidraw0",
            SerialNumber: null);

        deviceInfo.SerialNumber.ShouldBeNull();
    }

    [Fact]
    public void SerialNumber_CanBeSet()
    {
        var deviceInfo = new DeviceInfo(
            VendorId: 0x0416,
            ProductId: 0x5302,
            Name: "Test Device",
            Manufacturer: "Test",
            DriverName: "TestDriver",
            DevicePath: "/dev/hidraw0",
            SerialNumber: "SN12345");

        deviceInfo.SerialNumber.ShouldBe("SN12345");
    }
}
