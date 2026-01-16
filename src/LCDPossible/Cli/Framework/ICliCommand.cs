namespace LCDPossible.Cli.Framework;

/// <summary>
/// Interface for CLI commands.
/// </summary>
public interface ICliCommand
{
    /// <summary>
    /// The primary name of the command.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Alternative names for the command.
    /// </summary>
    string[] Aliases => [];

    /// <summary>
    /// The parent command name for subcommands. Null for top-level commands.
    /// </summary>
    string? Parent => null;

    /// <summary>
    /// A brief description of what the command does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Whether this command should use IPC when the service is running.
    /// </summary>
    bool UseIpcWhenServiceRunning => false;

    /// <summary>
    /// Execute the command.
    /// </summary>
    Task<int> ExecuteAsync(CliContext context, CancellationToken ct = default);
}
