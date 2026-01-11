using System.Diagnostics;
using System.Text;

namespace LCDPossible.FunctionalTests.Helpers;

/// <summary>
/// Helper class for running CLI commands and capturing output.
/// </summary>
public sealed class CliRunner : IDisposable
{
    private readonly string _exePath;
    private readonly string _testDataDir;
    private readonly List<string> _tempFiles = [];
    private readonly List<string> _tempDirs = [];

    public CliRunner()
    {
        // Find the executable - look in build output directories
        var possiblePaths = new[]
        {
            Path.Combine(GetSolutionRoot(), ".build", "LCDPossible", "bin", "Release", "net10.0", "LCDPossible.exe"),
            Path.Combine(GetSolutionRoot(), ".build", "LCDPossible", "bin", "Debug", "net10.0", "LCDPossible.exe"),
            Path.Combine(GetSolutionRoot(), ".build", "LCDPossible", "bin", "Release", "net10.0", "LCDPossible"),
            Path.Combine(GetSolutionRoot(), ".build", "LCDPossible", "bin", "Debug", "net10.0", "LCDPossible"),
        };

        _exePath = possiblePaths.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException(
                $"Could not find LCDPossible executable. Searched paths:\n{string.Join("\n", possiblePaths)}\n" +
                "Run 'dotnet build --configuration Release' first.");

        // Create a unique test data directory for this test run
        _testDataDir = Path.Combine(Path.GetTempPath(), "LCDPossible.FunctionalTests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDataDir);
        _tempDirs.Add(_testDataDir);
    }

    /// <summary>
    /// Gets the test data directory for this test run.
    /// </summary>
    public string TestDataDir => _testDataDir;

    /// <summary>
    /// Runs the CLI with the specified arguments.
    /// </summary>
    public CliResult Run(params string[] args)
    {
        return RunWithEnvironment(null, args);
    }

    /// <summary>
    /// Runs the CLI with custom environment variables.
    /// </summary>
    public CliResult RunWithEnvironment(Dictionary<string, string>? envVars, params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _exePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = GetSolutionRoot(),
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        // Override the user data directory to use our test directory
        startInfo.Environment["LCDPOSSIBLE_DATA_DIR"] = _testDataDir;

        // Add any custom environment variables
        if (envVars != null)
        {
            foreach (var (key, value) in envVars)
            {
                startInfo.Environment[key] = value;
            }
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdout.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completed = process.WaitForExit(TimeSpan.FromSeconds(60));

        if (!completed)
        {
            process.Kill(true);
            throw new TimeoutException($"CLI command timed out: {_exePath} {string.Join(" ", args)}");
        }

        return new CliResult(
            process.ExitCode,
            stdout.ToString().TrimEnd(),
            stderr.ToString().TrimEnd(),
            string.Join(" ", args)
        );
    }

    /// <summary>
    /// Creates a temporary file with the specified content.
    /// </summary>
    public string CreateTempFile(string filename, string content)
    {
        var path = Path.Combine(_testDataDir, filename);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    /// <summary>
    /// Gets the path to a profile in the test data directory.
    /// </summary>
    public string GetProfilePath(string profileName)
    {
        var name = profileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
            ? profileName
            : $"{profileName}.yaml";
        return Path.Combine(_testDataDir, name);
    }

    /// <summary>
    /// Reads a profile file from the test data directory.
    /// </summary>
    public string? ReadProfileFile(string profileName)
    {
        var path = GetProfilePath(profileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>
    /// Checks if a profile exists in the test data directory.
    /// </summary>
    public bool ProfileExists(string profileName)
    {
        return File.Exists(GetProfilePath(profileName));
    }

    /// <summary>
    /// Deletes a profile from the test data directory.
    /// </summary>
    public void DeleteProfile(string profileName)
    {
        var path = GetProfilePath(profileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Gets the solution root directory.
    /// </summary>
    private static string GetSolutionRoot()
    {
        var current = Directory.GetCurrentDirectory();

        // Walk up until we find the solution file
        while (!string.IsNullOrEmpty(current))
        {
            var slnPath = Directory.GetFiles(current, "*.sln").FirstOrDefault();
            if (slnPath != null)
            {
                return Path.GetDirectoryName(slnPath)!;
            }

            // Check parent of src folder
            var srcPath = Path.Combine(current, "src");
            if (Directory.Exists(srcPath) && Directory.GetFiles(srcPath, "*.sln").Any())
            {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        throw new InvalidOperationException("Could not find solution root directory");
    }

    public void Dispose()
    {
        // Clean up temp files
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // Clean up temp directories
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}

/// <summary>
/// Result of running a CLI command.
/// </summary>
public sealed record CliResult(int ExitCode, string Stdout, string Stderr, string Command)
{
    /// <summary>
    /// Returns true if the command succeeded (exit code 0).
    /// </summary>
    public bool Success => ExitCode == 0;

    /// <summary>
    /// Asserts that the command succeeded.
    /// </summary>
    public CliResult ShouldSucceed()
    {
        if (!Success)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected command to succeed but got exit code {ExitCode}.\n" +
                $"Command: {Command}\n" +
                $"Stdout: {Stdout}\n" +
                $"Stderr: {Stderr}");
        }
        return this;
    }

    /// <summary>
    /// Asserts that the command failed.
    /// </summary>
    public CliResult ShouldFail()
    {
        if (Success)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected command to fail but got exit code 0.\n" +
                $"Command: {Command}\n" +
                $"Stdout: {Stdout}");
        }
        return this;
    }

    /// <summary>
    /// Asserts that stdout contains the specified text.
    /// </summary>
    public CliResult ShouldContainOutput(string text)
    {
        if (!Stdout.Contains(text, StringComparison.OrdinalIgnoreCase))
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected stdout to contain '{text}'.\n" +
                $"Command: {Command}\n" +
                $"Actual stdout: {Stdout}");
        }
        return this;
    }

    /// <summary>
    /// Asserts that stderr contains the specified text.
    /// </summary>
    public CliResult ShouldContainError(string text)
    {
        if (!Stderr.Contains(text, StringComparison.OrdinalIgnoreCase))
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected stderr to contain '{text}'.\n" +
                $"Command: {Command}\n" +
                $"Actual stderr: {Stderr}");
        }
        return this;
    }
}
