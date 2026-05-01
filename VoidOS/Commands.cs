using System;
using System.Collections.Generic;
using System.Text;

namespace VoidOS;

public interface ICommand
{
    string Name { get; }
    string[] Aliases { get; }
    string Description { get; }
    string Execute(string[] args);
}

public static class CommandManager
{
    private static readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<ICommand> _allCommands = new();

    /// <summary>
    /// Register a command with all its aliases
    /// </summary>
    public static void Register(ICommand command)
    {
        _allCommands.Add(command);
        _commands[command.Name] = command;

        foreach (var alias in command.Aliases)
            _commands[alias] = command;
    }

    /// <summary>
    /// Execute a command string
    /// </summary>
    public static string Execute(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        try
        {
            var parts = ParseArgs(input);
            var cmdName = parts[0];
            var args = parts[1..];

            if (_commands.TryGetValue(cmdName, out var command))
                return command.Execute(args);
            else
                return $"Unknown command: {cmdName}. Type 'help' for available commands.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Get all registered commands (for help listing)
    /// </summary>
    public static IEnumerable<ICommand> GetAllCommands() => _allCommands;

    private static string[] ParseArgs(string input)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        foreach (char c in input.Trim())
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0) { parts.Add(current.ToString()); current.Clear(); }
                continue;
            }
            current.Append(c);
        }
        if (current.Length > 0) parts.Add(current.ToString());
        return parts.Count > 0 ? parts.ToArray() : new[] { "" };
    }
}

public abstract class BaseCommand : ICommand
{
    public abstract string Name { get; }
    public virtual string[] Aliases => Array.Empty<string>();
    public abstract string Description { get; }
    public abstract string Execute(string[] args);
}


public class HelpCommand : BaseCommand
{
    public override string Name => "help";
    public override string[] Aliases => new[] { "?", "/?" };
    public override string Description => "Show available commands";

    public override string Execute(string[] args)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Available commands:");

        foreach (var cmd in CommandManager.GetAllCommands())
        {
            var aliases = cmd.Aliases.Length > 0
                ? $" ({string.Join(", ", cmd.Aliases)})"
                : "";
            sb.AppendLine($"  {cmd.Name,-12} {aliases,-15} - {cmd.Description}");
        }

        return sb.ToString();
    }
}

public class SysInfoCommand : BaseCommand
{
    public override string Name => "sysinfo";
    public override string[] Aliases => new[] { "os", "osinfo" };
    public override string Description => "Display system information";

    public override string Execute(string[] args)
    {
        return $"VoidOS - Cosmos Gen3 Kernel\n" +
               $"   CPU : x64";
    }
}

public class ClearCommand : BaseCommand
{
    public override string Name => "clear";
    public override string[] Aliases => new[] { "cls", "clr" };
    public override string Description => "Clear the screen";

    public override string Execute(string[] args)
    {
        Console.Clear();
        return "";
    }
}

public class EchoCommand : BaseCommand
{
    public override string Name => "echo";
    public override string Description => "Echo text back";

    public override string Execute(string[] args)
    {
        if (args.Length == 0)
            return "Usage: echo <text>";
        else
            return string.Join(" ", args);
    }
}
