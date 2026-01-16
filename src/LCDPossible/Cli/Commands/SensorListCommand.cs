using System.Runtime.Versioning;
using LCDPossible.Cli.Framework;
using LibreHardwareMonitor.Hardware;

namespace LCDPossible.Cli.Commands;

/// <summary>
/// Lists all available sensors by category using LibreHardwareMonitor.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SensorListCommand : ICliCommand
{
    public string Name => "list";
    public string[] Aliases => ["ls"];
    public string? Parent => "sensor";
    public string Description => "List all available sensors by category";

    public Task<int> ExecuteAsync(CliContext context, CancellationToken ct = default)
    {
        var showAll = context.HasFlag("--all", "-a");

        Console.WriteLine("Available Sensors");
        Console.WriteLine("=================");
        Console.WriteLine();

        try
        {
            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = showAll
            };

            computer.Open();

            var sensors = new List<(string Category, string Id, string Name, string? Unit, float? Value)>();

            foreach (var hardware in computer.Hardware)
            {
                hardware.Update();
                CollectSensors(hardware, sensors);

                foreach (var subHardware in hardware.SubHardware)
                {
                    subHardware.Update();
                    CollectSensors(subHardware, sensors);
                }
            }

            // Group by category
            var byCategory = sensors
                .GroupBy(s => s.Category)
                .OrderBy(g => g.Key);

            foreach (var group in byCategory)
            {
                Console.WriteLine($"[{group.Key.ToUpperInvariant()}]");
                foreach (var sensor in group.OrderBy(s => s.Id))
                {
                    var unitStr = string.IsNullOrEmpty(sensor.Unit) ? "" : $" ({sensor.Unit})";
                    var valueStr = sensor.Value.HasValue ? $" = {sensor.Value.Value:F1}{sensor.Unit}" : "";
                    Console.WriteLine($"  {sensor.Id}{valueStr}");
                }
                Console.WriteLine();
            }

            Console.WriteLine($"Total: {sensors.Count} sensors");

            if (!showAll)
            {
                Console.WriteLine();
                Console.WriteLine("Use --all to include storage sensors.");
            }

            computer.Close();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading sensors: {ex.Message}");
            return Task.FromResult(1);
        }

        return Task.FromResult(0);
    }

    private static void CollectSensors(
        IHardware hardware,
        List<(string Category, string Id, string Name, string? Unit, float? Value)> sensors)
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

        var hwName = hardware.Name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("(", "")
            .Replace(")", "");

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
                _ => null
            };

            var id = $"lhm.{category}.{sensorTypeName}.{sensorName}";
            sensors.Add((category, id, sensor.Name, unit, sensor.Value));
        }
    }
}
