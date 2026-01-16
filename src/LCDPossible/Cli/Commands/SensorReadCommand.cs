using System.Runtime.Versioning;
using LCDPossible.Cli.Framework;
using LibreHardwareMonitor.Hardware;

namespace LCDPossible.Cli.Commands;

/// <summary>
/// Reads a specific sensor value using LibreHardwareMonitor.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SensorReadCommand : ICliCommand
{
    public string Name => "read";
    public string[] Aliases => ["get", "r"];
    public string? Parent => "sensor";
    public string Description => "Read a sensor value";

    public Task<int> ExecuteAsync(CliContext context, CancellationToken ct = default)
    {
        // Get sensor ID from remaining args
        var sensorId = context.RemainingArgs.FirstOrDefault(a => !a.StartsWith("-"));

        if (string.IsNullOrEmpty(sensorId))
        {
            Console.Error.WriteLine("Usage: lcdpossible sensor read <sensor-id>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Example: lcdpossible sensor read lhm.cpu.temperature.cpu-package");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Use 'lcdpossible sensor list' to see available sensor IDs.");
            return Task.FromResult(1);
        }

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
                var result = FindAndPrintSensor(hardware, sensorId);
                if (result) return Task.FromResult(0);

                foreach (var subHardware in hardware.SubHardware)
                {
                    subHardware.Update();
                    result = FindAndPrintSensor(subHardware, sensorId);
                    if (result) return Task.FromResult(0);
                }
            }

            computer.Close();

            Console.Error.WriteLine($"Sensor not found: {sensorId}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Use 'lcdpossible sensor list' to see available sensors.");
            return Task.FromResult(1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading sensor: {ex.Message}");
            return Task.FromResult(1);
        }
    }

    private static bool FindAndPrintSensor(IHardware hardware, string targetId)
    {
        var category = hardware.HardwareType switch
        {
            HardwareType.Cpu => "cpu",
            HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => "gpu",
            HardwareType.Memory => "memory",
            HardwareType.Storage => "storage",
            HardwareType.Network => "network",
            HardwareType.Motherboard => "motherboard",
            HardwareType.SuperIO => "superio",
            HardwareType.Cooler => "cooler",
            HardwareType.EmbeddedController => "ec",
            HardwareType.Psu => "psu",
            HardwareType.Battery => "battery",
            _ => "other"
        };

        foreach (var sensor in hardware.Sensors)
        {
            var sensorTypeName = sensor.SensorType switch
            {
                SensorType.Temperature => "temperature",
                SensorType.Load => "load",
                SensorType.Clock => "clock",
                SensorType.Voltage => "voltage",
                SensorType.Power => "power",
                SensorType.Fan => "fan",
                SensorType.Flow => "flow",
                SensorType.Control => "control",
                SensorType.Level => "level",
                SensorType.Data => "data",
                SensorType.SmallData => "smalldata",
                SensorType.Factor => "factor",
                SensorType.Frequency => "frequency",
                SensorType.Throughput => "throughput",
                SensorType.TimeSpan => "timespan",
                SensorType.Energy => "energy",
                SensorType.Noise => "noise",
                SensorType.Humidity => "humidity",
                _ => "unknown"
            };

            var sensorName = sensor.Name.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("#", "")
                .Replace("(", "")
                .Replace(")", "");

            var id = $"lhm.{category}.{sensorTypeName}.{sensorName}";

            if (id.Equals(targetId, StringComparison.OrdinalIgnoreCase))
            {
                var unit = sensor.SensorType switch
                {
                    SensorType.Temperature => "°C",
                    SensorType.Load => "%",
                    SensorType.Clock => "MHz",
                    SensorType.Voltage => "V",
                    SensorType.Power => "W",
                    SensorType.Fan => "RPM",
                    SensorType.Data => "GB",
                    SensorType.SmallData => "MB",
                    SensorType.Frequency => "Hz",
                    SensorType.Throughput => "B/s",
                    _ => ""
                };

                var value = sensor.Value.HasValue ? $"{sensor.Value.Value:F1}" : "N/A";
                Console.WriteLine($"{id}: {value}{unit}");
                return true;
            }
        }

        return false;
    }
}
