using HidSharp;
using LCDPossible.Core.Usb;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Cli;

public static class DebugTest
{
    // Command constants from reverse engineering
    private const byte CMD_ONOFF = 0x00;
    private const byte CMD_STATE = 0x01;
    private const byte CMD_GET_STATE = 0x02;
    private const byte CMD_LCD = 0x30;

    public static async Task<int> RunAsync()
    {
        Console.WriteLine("=== LCD Debug Test v6 - Detailed Protocol Analysis ===\n");

        // First list ALL HID devices to see what's available
        Console.WriteLine("--- All HID devices on system with VID 0x0416 ---");
        var allDevices = DeviceList.Local.GetHidDevices(0x0416);
        foreach (var dev in allDevices)
        {
            Console.WriteLine($"\n  PID: 0x{dev.ProductID:X4} - {TryGet(() => dev.GetProductName()) ?? "Unknown"}");
            Console.WriteLine($"    Path: {dev.DevicePath}");
            Console.WriteLine($"    Max I/O/F: {dev.GetMaxInputReportLength()}/{dev.GetMaxOutputReportLength()}/{dev.GetMaxFeatureReportLength()}");

            try
            {
                var reportDesc = dev.GetReportDescriptor();
                foreach (var report in reportDesc.OutputReports)
                {
                    Console.WriteLine($"    Output Report: ID={report.ReportID}, Len={report.Length}");
                }
            }
            catch { }
        }

        var lcdDevices = DeviceList.Local.GetHidDevices(0x0416, 0x5302).ToList();
        if (lcdDevices.Count == 0)
        {
            Console.WriteLine("\nNo 0x5302 device found!");
            return 1;
        }

        var hidDev = lcdDevices[0];
        Console.WriteLine($"\n--- Using device: {TryGet(() => hidDev.GetProductName()) ?? "Unknown"} ---");
        Console.WriteLine($"Max Input: {hidDev.GetMaxInputReportLength()}");
        Console.WriteLine($"Max Output: {hidDev.GetMaxOutputReportLength()}");
        Console.WriteLine($"Max Feature: {hidDev.GetMaxFeatureReportLength()}");

        Console.WriteLine("\n*** IMPORTANT: Make sure TRCC.exe is NOT running! ***\n");

        try
        {
            using var stream = hidDev.Open();
            stream.WriteTimeout = 5000;
            stream.ReadTimeout = 1000;

            // Generate test image (solid red)
            Console.WriteLine("--- Generating test image ---");
            using var image = new Image<Rgba32>(1280, 480);
            image.Mutate(ctx => ctx.BackgroundColor(Color.Red));

            using var ms = new MemoryStream();
            image.SaveAsJpeg(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 95 });
            var jpegData = ms.ToArray();
            Console.WriteLine($"JPEG size: {jpegData.Length} bytes");
            Console.WriteLine($"First 16 bytes of JPEG: {BitConverter.ToString(jpegData.Take(16).ToArray())}");

            // Test 1: Try WITHOUT report ID byte
            Console.WriteLine("\n=== Test 1: Without Report ID (512 byte packets) ===");
            try
            {
                var header = BuildHeader(1280, 480, jpegData.Length, 0x02);
                await SendDataNoReportId(stream, header, jpegData);
                Console.WriteLine("Sent! Waiting...");
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed: {ex.Message}");
            }

            // Test 2: Try big-endian width/height
            Console.WriteLine("\n=== Test 2: Big-endian width/height ===");
            try
            {
                var header = BuildHeaderBigEndian(1280, 480, jpegData.Length, 0x02);
                Console.WriteLine($"Header: {BitConverter.ToString(header)}");
                await SendImageData(stream, hidDev.GetMaxOutputReportLength(), header, jpegData);
                Console.WriteLine("Sent! Waiting...");
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed: {ex.Message}");
            }

            // Test 3: Try RGB565 instead of JPEG
            Console.WriteLine("\n=== Test 3: RGB565 raw format ===");
            try
            {
                var rgb565Data = ConvertToRgb565(image);
                Console.WriteLine($"RGB565 size: {rgb565Data.Length} bytes");

                var header = BuildHeader(1280, 480, rgb565Data.Length, 0x02);
                header[12] = 0x00; // Compression type 0 = raw
                await SendImageData(stream, hidDev.GetMaxOutputReportLength(), header, rgb565Data);
                Console.WriteLine("Sent! Waiting...");
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed: {ex.Message}");
            }

            // Test 4: Try compression type 0x01 (might be JPEG)
            Console.WriteLine("\n=== Test 4: Compression type 0x01 ===");
            try
            {
                var header = BuildHeader(1280, 480, jpegData.Length, 0x02);
                header[12] = 0x01; // Try 0x01 as compression type
                Console.WriteLine($"Header: {BitConverter.ToString(header)}");
                await SendImageData(stream, hidDev.GetMaxOutputReportLength(), header, jpegData);
                Console.WriteLine("Sent! Waiting...");
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed: {ex.Message}");
            }

            // Test 5: Try PA120 style header (FC 00 00 FF at offset 16)
            Console.WriteLine("\n=== Test 5: PA120-style header ===");
            try
            {
                var header = new byte[20];
                header[0] = 0xDA;
                header[1] = 0xDB;
                header[2] = 0xDC;
                header[3] = 0xDD;
                // 12 zero bytes
                header[16] = 0xFC;
                header[17] = 0x00;
                header[18] = 0x00;
                header[19] = 0xFF;
                Console.WriteLine($"Header: {BitConverter.ToString(header)}");

                var fullData = new byte[header.Length + jpegData.Length];
                header.CopyTo(fullData, 0);
                jpegData.CopyTo(fullData, header.Length);
                await SendData(stream, fullData, hidDev.GetMaxOutputReportLength());
                Console.WriteLine("Sent! Waiting...");
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed: {ex.Message}");
            }

            // Test 6: Continuous streaming with 0x02 command
            Console.WriteLine("\n=== Test 6: Continuous streaming (3 sec, cmd 0x02) ===");
            var startTime = DateTime.UtcNow;
            var frameCount = 0;
            while ((DateTime.UtcNow - startTime).TotalSeconds < 3)
            {
                var header = BuildHeader(1280, 480, jpegData.Length, 0x02);
                await SendImageData(stream, hidDev.GetMaxOutputReportLength(), header, jpegData);
                frameCount++;
                await Task.Delay(16);
            }
            Console.WriteLine($"Sent {frameCount} frames");

            // Test 7: Alternative header 12 34 56 78
            Console.WriteLine("\n=== Test 7: Alternative header 12 34 56 78 ===");
            try
            {
                var header = new byte[20];
                header[0] = 0x12;
                header[1] = 0x34;
                header[2] = 0x56;
                header[3] = 0x78;
                header[4] = 0x02;  // command
                header[8] = 0x00; header[9] = 0x05; // 1280 LE
                header[10] = 0xE0; header[11] = 0x01; // 480 LE
                header[12] = 0x02; // JPEG
                header[16] = (byte)(jpegData.Length & 0xFF);
                header[17] = (byte)((jpegData.Length >> 8) & 0xFF);
                header[18] = (byte)((jpegData.Length >> 16) & 0xFF);
                header[19] = (byte)((jpegData.Length >> 24) & 0xFF);
                Console.WriteLine($"Header: {BitConverter.ToString(header)}");
                await SendImageData(stream, hidDev.GetMaxOutputReportLength(), header, jpegData);
                Console.WriteLine("Sent! Waiting...");
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed: {ex.Message}");
            }

            // Test 8: DC DD magic (legacy 2-byte header padded)
            Console.WriteLine("\n=== Test 8: DC DD legacy header ===");
            try
            {
                var header = new byte[20];
                header[0] = 0xDC;
                header[1] = 0xDD;
                header[2] = 0x02;  // command at offset 2
                header[4] = 0x00; header[5] = 0x05; // 1280 LE at offset 4
                header[6] = 0xE0; header[7] = 0x01; // 480 LE
                header[8] = 0x02; // JPEG
                header[12] = (byte)(jpegData.Length & 0xFF);
                header[13] = (byte)((jpegData.Length >> 8) & 0xFF);
                header[14] = (byte)((jpegData.Length >> 16) & 0xFF);
                header[15] = (byte)((jpegData.Length >> 24) & 0xFF);
                Console.WriteLine($"Header: {BitConverter.ToString(header)}");
                await SendImageData(stream, hidDev.GetMaxOutputReportLength(), header, jpegData);
                Console.WriteLine("Sent! Waiting...");
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed: {ex.Message}");
            }

            // Test 9: Show exactly what we're sending in the documented format
            Console.WriteLine("\n=== Test 9: Documented format - exact bytes ===");
            try
            {
                var header = BuildHeader(1280, 480, jpegData.Length, 0x02);
                var firstPacket = new byte[hidDev.GetMaxOutputReportLength()];
                firstPacket[0] = 0x00; // Report ID
                Array.Copy(header, 0, firstPacket, 1, header.Length);
                Array.Copy(jpegData, 0, firstPacket, 1 + header.Length, Math.Min(jpegData.Length, 512 - header.Length));

                Console.WriteLine($"First packet (first 64 bytes):");
                Console.WriteLine($"  {BitConverter.ToString(firstPacket.Take(32).ToArray())}");
                Console.WriteLine($"  {BitConverter.ToString(firstPacket.Skip(32).Take(32).ToArray())}");

                await SendImageData(stream, hidDev.GetMaxOutputReportLength(), header, jpegData);
                Console.WriteLine("Sent! Waiting...");
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed: {ex.Message}");
            }

            // Test 10: Try with 0x30 (LCD command) instead of 0x02
            Console.WriteLine("\n=== Test 10: 0x30 LCD command ===");
            try
            {
                var header = BuildHeader(1280, 480, jpegData.Length, 0x30);
                Console.WriteLine($"Header: {BitConverter.ToString(header)}");
                await SendImageData(stream, hidDev.GetMaxOutputReportLength(), header, jpegData);
                Console.WriteLine("Sent! Waiting...");
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed: {ex.Message}");
            }

            Console.WriteLine("\n=== All tests complete ===");
            Console.WriteLine("Did you see RED on the LCD at any point?");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static async Task SendDataNoReportId(HidStream stream, byte[] header, byte[] jpegData)
    {
        var fullData = new byte[header.Length + jpegData.Length];
        header.CopyTo(fullData, 0);
        jpegData.CopyTo(fullData, header.Length);

        // Send in 512-byte chunks (no report ID)
        var offset = 0;
        while (offset < fullData.Length)
        {
            var chunk = Math.Min(512, fullData.Length - offset);
            var packet = new byte[512];
            Array.Copy(fullData, offset, packet, 0, chunk);
            await stream.WriteAsync(packet);
            offset += 512;
        }
    }

    private static byte[] BuildHeaderBigEndian(int width, int height, int dataLength, byte command)
    {
        var header = new byte[20];
        header[0] = 0xDA;
        header[1] = 0xDB;
        header[2] = 0xDC;
        header[3] = 0xDD;
        header[4] = command;
        // Big-endian width (1280 = 0x0500)
        header[8] = (byte)((width >> 8) & 0xFF);  // 0x05
        header[9] = (byte)(width & 0xFF);         // 0x00
        // Big-endian height (480 = 0x01E0)
        header[10] = (byte)((height >> 8) & 0xFF); // 0x01
        header[11] = (byte)(height & 0xFF);        // 0xE0
        header[12] = 0x02; // JPEG compression
        // Big-endian data length
        header[16] = (byte)((dataLength >> 24) & 0xFF);
        header[17] = (byte)((dataLength >> 16) & 0xFF);
        header[18] = (byte)((dataLength >> 8) & 0xFF);
        header[19] = (byte)(dataLength & 0xFF);
        return header;
    }

    private static byte[] ConvertToRgb565(Image<Rgba32> image)
    {
        var result = new byte[image.Width * image.Height * 2];
        var idx = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                var r = (pixel.R >> 3) & 0x1F;
                var g = (pixel.G >> 2) & 0x3F;
                var b = (pixel.B >> 3) & 0x1F;
                var rgb565 = (ushort)((r << 11) | (g << 5) | b);
                result[idx++] = (byte)(rgb565 & 0xFF);
                result[idx++] = (byte)((rgb565 >> 8) & 0xFF);
            }
        }
        return result;
    }

    private static async Task SendCommand(HidStream stream, int maxReportLength, byte command, byte param)
    {
        var packet = new byte[maxReportLength];
        packet[0] = 0x00; // Report ID
        packet[1] = 0xDA;
        packet[2] = 0xDB;
        packet[3] = 0xDC;
        packet[4] = 0xDD;
        packet[5] = command;
        packet[6] = param;
        await stream.WriteAsync(packet);
    }

    private static async Task SendImageData(HidStream stream, int maxReportLength, byte[] header, byte[] jpegData)
    {
        var fullData = new byte[header.Length + jpegData.Length];
        header.CopyTo(fullData, 0);
        jpegData.CopyTo(fullData, header.Length);
        await SendData(stream, fullData, maxReportLength);
    }

    private static async Task SendHeaderThenData(HidStream stream, int maxReportLength, byte[] jpegData)
    {
        // Send header as first packet
        var headerPacket = new byte[maxReportLength];
        headerPacket[0] = 0x00; // Report ID
        headerPacket[1] = 0xDA;
        headerPacket[2] = 0xDB;
        headerPacket[3] = 0xDC;
        headerPacket[4] = 0xDD;
        headerPacket[5] = CMD_LCD;
        headerPacket[6] = 0x00;
        headerPacket[7] = 0x00;
        headerPacket[8] = 0x00;
        headerPacket[9] = 0x00; // Width low
        headerPacket[10] = 0x05; // Width high (1280)
        headerPacket[11] = 0xE0; // Height low
        headerPacket[12] = 0x01; // Height high (480)
        headerPacket[13] = 0x02; // JPEG compression
        headerPacket[14] = 0x00;
        headerPacket[15] = 0x00;
        headerPacket[16] = 0x00;
        headerPacket[17] = (byte)(jpegData.Length & 0xFF);
        headerPacket[18] = (byte)((jpegData.Length >> 8) & 0xFF);
        headerPacket[19] = (byte)((jpegData.Length >> 16) & 0xFF);
        headerPacket[20] = (byte)((jpegData.Length >> 24) & 0xFF);
        await stream.WriteAsync(headerPacket);

        // Send JPEG data in subsequent packets
        await SendData(stream, jpegData, maxReportLength);
    }

    private static byte[] BuildHeader(int width, int height, int dataLength, byte command)
    {
        var header = new byte[20];
        header[0] = 0xDA;
        header[1] = 0xDB;
        header[2] = 0xDC;
        header[3] = 0xDD;
        header[4] = command;
        header[5] = 0x00;
        header[6] = 0x00;
        header[7] = 0x00;
        header[8] = (byte)(width & 0xFF);
        header[9] = (byte)((width >> 8) & 0xFF);
        header[10] = (byte)(height & 0xFF);
        header[11] = (byte)((height >> 8) & 0xFF);
        header[12] = 0x02; // JPEG
        header[13] = 0x00;
        header[14] = 0x00;
        header[15] = 0x00;
        header[16] = (byte)(dataLength & 0xFF);
        header[17] = (byte)((dataLength >> 8) & 0xFF);
        header[18] = (byte)((dataLength >> 16) & 0xFF);
        header[19] = (byte)((dataLength >> 24) & 0xFF);
        return header;
    }

    private static async Task SendData(HidStream stream, byte[] data, int maxReportLength)
    {
        // maxReportLength includes report ID, so actual data per packet is maxReportLength - 1
        var dataPerPacket = maxReportLength - 1;
        var offset = 0;

        while (offset < data.Length)
        {
            var chunk = Math.Min(dataPerPacket, data.Length - offset);
            var packet = new byte[maxReportLength];
            packet[0] = 0x00; // Report ID
            Array.Copy(data, offset, packet, 1, chunk);

            await stream.WriteAsync(packet);
            offset += chunk;
        }
    }

    private static string? TryGet(Func<string> getter)
    {
        try { return getter(); }
        catch { return null; }
    }
}
