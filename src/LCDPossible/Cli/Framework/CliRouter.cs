namespace LCDPossible.Cli.Framework;

/// <summary>
/// Routes CLI commands to their handlers.
/// </summary>
public sealed class CliRouter
{
    private readonly Dictionary<string, ICliCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ICliCommand>> _subCommands = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Register a command with the router.
    /// </summary>
    public void RegisterCommand(ICliCommand command)
    {
        // Register by name and aliases
        var names = new[] { command.Name }.Concat(command.Aliases);

        foreach (var name in names)
        {
            if (command.Parent != null)
            {
                // Subcommand - register under parent
                if (!_subCommands.TryGetValue(command.Parent, out var subs))
                {
                    subs = [];
                    _subCommands[command.Parent] = subs;
                }
                subs.Add(command);
            }
            else
            {
                // Top-level command
                _commands[name] = command;
            }
        }
    }

    /// <summary>
    /// Execute the appropriate command based on context.
    /// </summary>
    public async Task<int> ExecuteAsync(CliContext context, CancellationToken ct = default)
    {
        var command = context.Command;
        var subCommand = context.SubCommand;

        // Look for subcommand first
        if (command != null && _subCommands.TryGetValue(command, out var subs))
        {
            // If no subcommand specified or subcommand is "help", show help
            if (string.IsNullOrEmpty(subCommand) || subCommand == "help")
            {
                var helpCmd = subs.FirstOrDefault(c => c.Name == "help");
                if (helpCmd != null)
                {
                    return await helpCmd.ExecuteAsync(context, ct);
                }
            }

            // Find matching subcommand
            var matchingCmd = subs.FirstOrDefault(c =>
                c.Name.Equals(subCommand, StringComparison.OrdinalIgnoreCase) ||
                c.Aliases.Contains(subCommand!, StringComparer.OrdinalIgnoreCase));

            if (matchingCmd != null)
            {
                return await matchingCmd.ExecuteAsync(context, ct);
            }

            // Unknown subcommand
            Console.Error.WriteLine($"Unknown subcommand: {command} {subCommand}");
            Console.Error.WriteLine($"Use '{command} help' for available commands.");
            return 1;
        }

        // Look for top-level command
        if (command != null && _commands.TryGetValue(command, out var topCmd))
        {
            return await topCmd.ExecuteAsync(context, ct);
        }

        // No matching command found
        Console.Error.WriteLine($"Unknown command: {command}");
        return 1;
    }

    /// <summary>
    /// Get all registered commands for a parent (for help display).
    /// </summary>
    public IReadOnlyList<ICliCommand> GetSubCommands(string parent)
    {
        return _subCommands.TryGetValue(parent, out var subs) ? subs : [];
    }
}
