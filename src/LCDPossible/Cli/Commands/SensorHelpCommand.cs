using System.Runtime.Versioning;
using LCDPossible.Cli.Framework;

namespace LCDPossible.Cli.Commands;

/// <summary>
/// Shows help for sensor commands.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SensorHelpCommand : ICliCommand
{
    public string Name => "help";
    public string[] Aliases => ["?", "h"];
    public string? Parent => "sensor";
    public string Description => "Show help for sensor commands";

    public Task<int> ExecuteAsync(CliContext context, CancellationToken ct = default)
    {
        Console.WriteLine(@"
Sensor Commands
===============

Query hardware sensors using LibreHardwareMonitor.

USAGE:
    lcdpossible sensor <command> [options]

COMMANDS:
    list, ls              List all available sensors by category
    read, get <id>        Read a sensor value
    watch, w <id>         Watch a sensor value in real-time
    help, ?               Show this help

EXAMPLES:
    lcdpossible sensor list
    lcdpossible sensor list --all
    lcdpossible sensor read lhm.cpu.temperature.cpu-package
    lcdpossible sensor watch lhm.cpu.load.cpu-total -i 500

SENSOR ID FORMAT:
    lhm.{category}.{type}.{name}

    Categories: cpu, gpu, memory, storage, network, motherboard
    Types: temperature, load, clock, voltage, power, fan, data

    Examples:
      lhm.cpu.temperature.cpu-package  - CPU package temperature
      lhm.cpu.load.cpu-total           - Total CPU load
      lhm.gpu.temperature.gpu-core     - GPU core temperature
      lhm.memory.load.memory           - Memory usage percentage

OPTIONS:
    --all, -a             Include storage sensors (list command)
    --interval, -i <ms>   Update interval for watch command (default: 1000)

NOTE: Run as Administrator for full sensor access.
      Sensor commands require Windows and LibreHardwareMonitor.
");
        return Task.FromResult(0);
    }
}
