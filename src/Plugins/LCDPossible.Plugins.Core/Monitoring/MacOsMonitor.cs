using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using LCDPossible.Core.Monitoring;

namespace LCDPossible.Plugins.Core.Monitoring;

/// <summary>
/// macOS hardware monitor using sysctl, vm_stat, and GPU driver tools.
/// Provides CPU info/usage, RAM, and GPU data via nvidia-smi or system_profiler.
/// Note: Temperature requires osx-cpu-temp or similar third-party tool.
/// </summary>
internal sealed partial class MacOsMonitor : IPlatformMonitor
{
    private string? _cpuName;
    private long _totalMemoryBytes;
    private float _lastCpuUsage;
    private DateTime _lastCpuCheck = DateTime.MinValue;
    private bool _hasNvidiaSmi;
    private bool _hasOsxCpuTemp;
    private string? _gpuName;

    public string PlatformName => "macOS";
    public bool IsAvailable { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Get CPU name
            _cpuName = await RunSysctlAsync("machdep.cpu.brand_string", cancellationToken);
            if (string.IsNullOrEmpty(_cpuName))
            {
                _cpuName = await RunSysctlAsync("hw.model", cancellationToken);
            }

            // Get total memory
            var memStr = await RunSysctlAsync("hw.memsize", cancellationToken);
            if (!string.IsNullOrEmpty(memStr) && long.TryParse(memStr, out var mem))
            {
                _totalMemoryBytes = mem;
            }

            // Check for GPU tools
            _hasNvidiaSmi = CanRunCommand("nvidia-smi", "--version");
            _hasOsxCpuTemp = CanRunCommand("osx-cpu-temp", "-h") || CanRunCommand("/usr/local/bin/osx-cpu-temp", "-h");

            // Get GPU name via system_profiler
            _gpuName = await GetGpuNameAsync(cancellationToken);

            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public async Task<SystemMetrics?> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return null;
        }

        var metrics = new SystemMetrics
        {
            Cpu = await GetCpuMetricsAsync(cancellationToken),
            Memory = await GetMemoryMetricsAsync(cancellationToken),
            Gpu = await GetGpuMetricsAsync(cancellationToken),
            Timestamp = DateTime.UtcNow
        };

        return metrics;
    }

    private async Task<CpuMetrics> GetCpuMetricsAsync(CancellationToken cancellationToken)
    {
        var cpu = new CpuMetrics
        {
            Name = _cpuName ?? "Apple Silicon"
        };

        // Get CPU usage via top command (sampled)
        // Only sample every 2 seconds to avoid overhead
        if ((DateTime.UtcNow - _lastCpuCheck).TotalSeconds >= 2)
        {
            var output = await RunCommandAsync("top", "-l 1 -n 0 -stats cpu", cancellationToken);
            if (!string.IsNullOrEmpty(output))
            {
                // Parse "CPU usage: X.X% user, X.X% sys, X.X% idle"
                var match = CpuUsageRegex().Match(output);
                if (match.Success)
                {
                    if (float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var user) &&
                        float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var sys))
                    {
                        _lastCpuUsage = user + sys;
                    }
                }
            }
            _lastCpuCheck = DateTime.UtcNow;
        }

        cpu.UsagePercent = _lastCpuUsage;

        // Try to get temperature via osx-cpu-temp
        if (_hasOsxCpuTemp)
        {
            var tempOutput = await RunCommandAsync("osx-cpu-temp", "", cancellationToken);
            if (!string.IsNullOrEmpty(tempOutput))
            {
                // Output format: "65.0°C"
                var tempMatch = TempValueRegex().Match(tempOutput);
                if (tempMatch.Success && float.TryParse(tempMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var temp))
                {
                    cpu.TemperatureCelsius = temp;
                }
            }
        }

        return cpu;
    }

    private async Task<MemoryMetrics> GetMemoryMetricsAsync(CancellationToken cancellationToken)
    {
        var memory = new MemoryMetrics
        {
            TotalGb = _totalMemoryBytes / (1024f * 1024f * 1024f)
        };

        // Use vm_stat to get memory info
        var output = await RunCommandAsync("vm_stat", "", cancellationToken);
        if (!string.IsNullOrEmpty(output))
        {
            // Get page size
            var pageSizeMatch = PageSizeRegex().Match(output);
            long pageSize = 4096; // Default
            if (pageSizeMatch.Success && long.TryParse(pageSizeMatch.Groups[1].Value, out var ps))
            {
                pageSize = ps;
            }

            // Parse pages
            long freePages = 0, inactivePages = 0, purgablePages = 0, speculativePages = 0;

            var freeMatch = PagesFreeRegex().Match(output);
            if (freeMatch.Success) long.TryParse(freeMatch.Groups[1].Value, out freePages);

            var inactiveMatch = PagesInactiveRegex().Match(output);
            if (inactiveMatch.Success) long.TryParse(inactiveMatch.Groups[1].Value, out inactivePages);

            var purgableMatch = PagesPurgableRegex().Match(output);
            if (purgableMatch.Success) long.TryParse(purgableMatch.Groups[1].Value, out purgablePages);

            var speculativeMatch = PagesSpeculativeRegex().Match(output);
            if (speculativeMatch.Success) long.TryParse(speculativeMatch.Groups[1].Value, out speculativePages);

            // Calculate available memory (free + inactive + purgeable + speculative)
            var availableBytes = (freePages + inactivePages + purgablePages + speculativePages) * pageSize;
            memory.AvailableGb = availableBytes / (1024f * 1024f * 1024f);
            memory.UsedGb = memory.TotalGb - memory.AvailableGb;

            if (memory.TotalGb > 0)
            {
                memory.UsagePercent = (memory.UsedGb / memory.TotalGb) * 100f;
            }
        }

        return memory;
    }

    private async Task<GpuMetrics> GetGpuMetricsAsync(CancellationToken cancellationToken)
    {
        // Try NVIDIA first (for older Macs with NVIDIA GPUs)
        if (_hasNvidiaSmi)
        {
            var nvidia = await GetNvidiaGpuMetricsAsync(cancellationToken);
            if (nvidia != null) return nvidia;
        }

        // For Apple Silicon or AMD, we have limited info
        return new GpuMetrics
        {
            Name = _gpuName ?? "Apple GPU",
            UsagePercent = 0 // No easy way to get GPU usage on macOS without IOKit
        };
    }

    private async Task<GpuMetrics?> GetNvidiaGpuMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var output = await RunCommandAsync("nvidia-smi",
                "--query-gpu=name,utilization.gpu,temperature.gpu,memory.used,memory.total " +
                "--format=csv,noheader,nounits",
                cancellationToken);

            if (string.IsNullOrEmpty(output)) return null;

            var parts = output.Split(',').Select(p => p.Trim()).ToArray();
            if (parts.Length < 5) return null;

            var gpu = new GpuMetrics
            {
                Name = parts[0]
            };

            if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var usage))
                gpu.UsagePercent = usage;
            if (float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var temp))
                gpu.TemperatureCelsius = temp;
            if (float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var memUsed))
                gpu.MemoryUsedMb = memUsed;
            if (float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var memTotal))
                gpu.MemoryTotalMb = memTotal;

            if (gpu.MemoryTotalMb > 0 && gpu.MemoryUsedMb.HasValue)
            {
                gpu.MemoryUsagePercent = (gpu.MemoryUsedMb / gpu.MemoryTotalMb) * 100f;
            }

            return gpu;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> GetGpuNameAsync(CancellationToken cancellationToken)
    {
        try
        {
            var output = await RunCommandAsync("system_profiler", "SPDisplaysDataType", cancellationToken);
            if (!string.IsNullOrEmpty(output))
            {
                var match = ChipsetModelRegex().Match(output);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    private static async Task<string?> RunSysctlAsync(string key, CancellationToken cancellationToken)
    {
        var output = await RunCommandAsync("sysctl", $"-n {key}", cancellationToken);
        return output?.Trim();
    }

    private static bool CanRunCommand(string command, string args)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit(1000);
            return true; // If it ran at all, command exists
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string?> RunCommandAsync(string command, string args, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return output.Trim();
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        // No resources to dispose
    }

    // Regex patterns
    [GeneratedRegex(@"CPU usage:\s*(\d+\.?\d*)%\s*user,\s*(\d+\.?\d*)%\s*sys")]
    private static partial Regex CpuUsageRegex();

    [GeneratedRegex(@"(\d+\.?\d*)°?C")]
    private static partial Regex TempValueRegex();

    [GeneratedRegex(@"page size of (\d+) bytes")]
    private static partial Regex PageSizeRegex();

    [GeneratedRegex(@"Pages free:\s*(\d+)")]
    private static partial Regex PagesFreeRegex();

    [GeneratedRegex(@"Pages inactive:\s*(\d+)")]
    private static partial Regex PagesInactiveRegex();

    [GeneratedRegex(@"Pages purgeable:\s*(\d+)")]
    private static partial Regex PagesPurgableRegex();

    [GeneratedRegex(@"Pages speculative:\s*(\d+)")]
    private static partial Regex PagesSpeculativeRegex();

    [GeneratedRegex(@"Chipset Model:\s*(.+)")]
    private static partial Regex ChipsetModelRegex();
}
