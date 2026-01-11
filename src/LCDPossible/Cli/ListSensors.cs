using System.Management;
using LibreHardwareMonitor.Hardware;

namespace LCDPossible.Cli;

/// <summary>
/// Lists all hardware sensors detected by LibreHardwareMonitor and WMI.
/// Useful for debugging temperature detection issues.
/// </summary>
public static class ListSensors
{
    public static Task<int> RunAsync()
    {
        Console.WriteLine("=== Hardware Sensors Diagnostic ===\n");
        Console.WriteLine("NOTE: Run as Administrator for full sensor access!\n");

        // Test LibreHardwareMonitor
        Console.WriteLine("--- LibreHardwareMonitor ---\n");
        try
        {
            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true
            };

            computer.Open();

            foreach (var hardware in computer.Hardware)
            {
                hardware.Update();
                PrintHardware(hardware, 0);

                foreach (var subHardware in hardware.SubHardware)
                {
                    subHardware.Update();
                    PrintHardware(subHardware, 1);
                }
            }

            computer.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LibreHardwareMonitor Error: {ex.Message}");
        }

        // Test WMI
        Console.WriteLine("\n--- WMI Temperature Sources ---\n");
        TestWmiTemperature();

        Console.WriteLine("\n=== Done ===");
        return Task.FromResult(0);
    }

    private static void TestWmiTemperature()
    {
        // Try MSAcpi_ThermalZoneTemperature
        Console.WriteLine("MSAcpi_ThermalZoneTemperature (root\\WMI):");
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT * FROM MSAcpi_ThermalZoneTemperature");

            var found = false;
            foreach (var obj in searcher.Get())
            {
                found = true;
                var tempKelvin = Convert.ToDouble(obj["CurrentTemperature"]);
                var tempCelsius = (tempKelvin / 10.0) - 273.15;
                var instanceName = obj["InstanceName"]?.ToString() ?? "Unknown";
                Console.WriteLine($"  - {instanceName}: {tempCelsius:F1}°C (raw: {tempKelvin})");
            }
            if (!found)
            {
                Console.WriteLine("  (No data available)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error: {ex.Message}");
        }

        // Try Win32_PerfFormattedData_Counters_ThermalZoneInformation
        Console.WriteLine("\nWin32_PerfFormattedData_Counters_ThermalZoneInformation:");
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2",
                "SELECT * FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");

            var found = false;
            foreach (var obj in searcher.Get())
            {
                found = true;
                var temp = obj["Temperature"];
                var name = obj["Name"]?.ToString() ?? "Unknown";
                if (temp != null)
                {
                    // This is in Kelvin
                    var tempKelvin = Convert.ToDouble(temp);
                    var tempCelsius = tempKelvin - 273.15;
                    Console.WriteLine($"  - {name}: {tempCelsius:F1}°C (Kelvin: {tempKelvin})");
                }
            }
            if (!found)
            {
                Console.WriteLine("  (No data available)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error: {ex.Message}");
        }

        // Try Win32_TemperatureProbe
        Console.WriteLine("\nWin32_TemperatureProbe:");
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2",
                "SELECT * FROM Win32_TemperatureProbe");

            var found = false;
            foreach (var obj in searcher.Get())
            {
                found = true;
                var temp = obj["CurrentReading"];
                var name = obj["Name"]?.ToString() ?? "Unknown";
                Console.WriteLine($"  - {name}: {temp ?? "N/A"}");
            }
            if (!found)
            {
                Console.WriteLine("  (No data available)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error: {ex.Message}");
        }
    }

    private static void PrintHardware(IHardware hardware, int indent)
    {
        var prefix = new string(' ', indent * 2);
        Console.WriteLine($"{prefix}[{hardware.HardwareType}] {hardware.Name}");

        var tempSensors = hardware.Sensors.Where(s => s.SensorType == SensorType.Temperature).ToList();
        var loadSensors = hardware.Sensors.Where(s => s.SensorType == SensorType.Load).ToList();
        var powerSensors = hardware.Sensors.Where(s => s.SensorType == SensorType.Power).ToList();
        var clockSensors = hardware.Sensors.Where(s => s.SensorType == SensorType.Clock).ToList();

        if (tempSensors.Any())
        {
            Console.WriteLine($"{prefix}  Temperature sensors:");
            foreach (var sensor in tempSensors)
            {
                Console.WriteLine($"{prefix}    - {sensor.Name}: {sensor.Value?.ToString("F1") ?? "N/A"}°C");
            }
        }

        if (loadSensors.Any())
        {
            Console.WriteLine($"{prefix}  Load sensors:");
            foreach (var sensor in loadSensors.Take(5)) // Limit output
            {
                Console.WriteLine($"{prefix}    - {sensor.Name}: {sensor.Value?.ToString("F1") ?? "N/A"}%");
            }
            if (loadSensors.Count > 5)
            {
                Console.WriteLine($"{prefix}    ... and {loadSensors.Count - 5} more");
            }
        }

        if (powerSensors.Any())
        {
            Console.WriteLine($"{prefix}  Power sensors:");
            foreach (var sensor in powerSensors)
            {
                Console.WriteLine($"{prefix}    - {sensor.Name}: {sensor.Value?.ToString("F1") ?? "N/A"}W");
            }
        }

        Console.WriteLine();
    }
}
