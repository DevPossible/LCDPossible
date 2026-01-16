namespace LCDPossible.Cli.Framework;

/// <summary>
/// Shared context for CLI commands.
/// </summary>
public sealed class CliContext : IDisposable
{
    /// <summary>
    /// The raw command line arguments.
    /// </summary>
    public string[] Args { get; }

    /// <summary>
    /// The primary command (e.g., "sensor").
    /// </summary>
    public string? Command { get; }

    /// <summary>
    /// The subcommand (e.g., "list", "read", "watch").
    /// </summary>
    public string? SubCommand { get; }

    /// <summary>
    /// Remaining arguments after command and subcommand.
    /// </summary>
    public string[] RemainingArgs { get; }

    public CliContext(string[] args)
    {
        Args = args;
        (Command, SubCommand, RemainingArgs) = ParseArgs(args);
    }

    /// <summary>
    /// Parses command line arguments into command, subcommand, and remaining args.
    /// </summary>
    private static (string? Command, string? SubCommand, string[] RemainingArgs) ParseArgs(string[] args)
    {
        // Skip flags at the beginning
        var nonFlagArgs = args.Where(a => !a.StartsWith("-") && !a.StartsWith("/")).ToArray();

        var command = nonFlagArgs.Length > 0 ? nonFlagArgs[0].ToLowerInvariant() : null;
        var subCommand = nonFlagArgs.Length > 1 ? nonFlagArgs[1].ToLowerInvariant() : null;

        // Remaining args are everything after the subcommand (including flags)
        var remaining = new List<string>();
        var skipped = 0;
        foreach (var arg in args)
        {
            if (!arg.StartsWith("-") && !arg.StartsWith("/") && skipped < 2)
            {
                skipped++;
                continue;
            }
            remaining.Add(arg);
        }

        return (command, subCommand, remaining.ToArray());
    }

    /// <summary>
    /// Gets a named argument value (e.g., --interval 500).
    /// </summary>
    public string? GetNamedArg(params string[] names)
    {
        for (var i = 0; i < RemainingArgs.Length - 1; i++)
        {
            if (names.Contains(RemainingArgs[i], StringComparer.OrdinalIgnoreCase))
            {
                return RemainingArgs[i + 1];
            }
        }
        return null;
    }

    /// <summary>
    /// Checks if a flag is present (e.g., --verbose).
    /// </summary>
    public bool HasFlag(params string[] names)
    {
        return RemainingArgs.Any(a => names.Contains(a, StringComparer.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        // Nothing to dispose currently
    }
}
