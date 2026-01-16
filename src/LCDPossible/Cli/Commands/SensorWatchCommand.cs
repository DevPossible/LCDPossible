using System.Runtime.Versioning;
using LCDPossible.Cli.Framework;
using LibreHardwareMonitor.Hardware;

namespace LCDPossible.Cli.Commands;

/// <summary>
/// Watches a sensor value in real-time using LibreHardwareMonitor.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SensorWatchCommand : ICliCommand
{
    public string Name => "watch";
    public string[] Aliases => ["w", "monitor"];
    public string? Parent => "sensor";
    public string Description => "Watch a sensor value in real-time";

    public async Task<int> ExecuteAsync(CliContext context, CancellationToken ct = default)
    {
        // Get sensor ID from remaining args
        var sensorId = context.RemainingArgs.FirstOrDefault(a => !a.StartsWith("-"));

        if (string.IsNullOrEmpty(sensorId))
        {
            Console.Error.WriteLine("Usage: lcdpossible sensor watch <sensor-id> [--interval <ms>]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --interval, -i <ms>    Update interval in milliseconds (default: 1000)");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Example: lcdpossible sensor watch lhm.cpu.temperature.cpu-package -i 500");
            return 1;
        }

        // Parse interval
        var intervalStr = context.GetNamedArg("--interval", "-i");
        var interval = 1000;
        if (!string.IsNullOrEmpty(intervalStr) && int.TryParse(intervalStr, out var parsed))
        {
            interval = Math.Max(100, parsed); // Minimum 100ms
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

            // Find the hardware and sensor once
            (IHardware hardware, ISensor sensor, string unit)? found = null;

            foreach (var hw in computer.Hardware)
            {
                hw.Update();
                var match = FindSensor(hw, sensorId);
                if (match != null)
                {
                    found = match;
                    break;
                }

                foreach (var subHw in hw.SubHardware)
                {
                    subHw.Update();
                    match = FindSensor(subHw, sensorId);
                    if (match != null)
                    {
                        found = match;
                        break;
                    }
                }

                if (found != null) break;
            }

            if (found == null)
            {
                computer.Close();
                Console.Error.WriteLine($"Sensor not found: {sensorId}");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Use 'lcdpossible sensor list' to see available sensors.");
                return 1;
            }

            var (targetHardware, targetSensor, unitStr) = found.Value;

            Console.WriteLine($"Watching {sensorId} (Ctrl+C to stop)");
            Console.WriteLine(new string('-', 50));

            while (!ct.IsCancellationRequested)
            {
                targetHardware.Update();
                var value = targetSensor.Value.HasValue ? $"{targetSensor.Value.Value:F1}" : "N/A";
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

                // Clear line and write new value
                Console.Write($"\r{timestamp}: {value}{unitStr}".PadRight(60));

                try
                {
                    await Task.Delay(interval, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Stopped.");
            computer.Close();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error watching sensor: {ex.Message}");
            return 1;
        }
    }

    private static (IHardware hardware, ISensor sensor, string unit)? FindSensor(IHardware hardware, string targetId)
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

                return (hardware, sensor, unit);
            }
        }

        return null;
    }
}
