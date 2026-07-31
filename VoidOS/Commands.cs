using Cosmos.Kernel.Core.Memory;
using Cosmos.Kernel.System.Network;
using Cosmos.Kernel.System.Network.Config;
using Cosmos.Kernel.System.Network.IPv4;
using Cosmos.Kernel.System.Network.IPv4.UDP.DNS;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Thread = System.Threading.Thread;

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
    private static readonly Dictionary<string, ICommand> _commands = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<ICommand> _allCommands = new List<ICommand>();

    public static void Register(ICommand command)
    {
        _allCommands.Add(command);
        _commands[command.Name] = command;
        foreach (var alias in command.Aliases) _commands[alias] = command;
    }

    public static string Execute(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        try
        {
            var parts = ParseArgs(input);
            var cmdName = parts[0];
            var args = new string[parts.Length - 1];
            for (int i = 1; i < parts.Length; i++) args[i - 1] = parts[i];

            if (_commands.TryGetValue(cmdName, out var command)) return command.Execute(args);
            else return $"Unknown command: {cmdName}. Type 'help' for available commands.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

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
        return parts.Count > 0 ? parts.ToArray() : new string[] { "" };
    }
}

public abstract class BaseCommand : ICommand
{
    private static readonly string[] EmptyAliases = new string[0];

    public abstract string Name { get; }
    public virtual string[] Aliases { get { return EmptyAliases; } }
    public abstract string Description { get; }
    public abstract string Execute(string[] args);
}

public class HelpCommand : BaseCommand
{
    private static readonly string[] AliasesArr = new string[] { "?", "/?" };
    public override string Name { get { return "help"; } }
    public override string[] Aliases { get { return AliasesArr; } }
    public override string Description { get { return "Show available commands"; } }

    public override string Execute(string[] args)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Available commands:");
        foreach (var cmd in CommandManager.GetAllCommands())
        {
            var aliases = cmd.Aliases.Length > 0 ? $" ({string.Join(", ", cmd.Aliases)})" : "";
            sb.AppendLine($"  {cmd.Name,-12} {aliases,-15} - {cmd.Description}");
        }
        return sb.ToString();
    }
}

public class ClearCommand : BaseCommand
{
    private static readonly string[] AliasesArr = new string[] { "cls", "clr" };
    public override string Name { get { return "clear"; } }
    public override string[] Aliases { get { return AliasesArr; } }
    public override string Description { get { return "Clear the screen"; } }
    public override string Execute(string[] args) { Console.Clear(); return ""; }
}

public class EchoCommand : BaseCommand
{
    public override string Name { get { return "echo"; } }
    public override string Description { get { return "Echo text back"; } }
    public override string Execute(string[] args) { return args.Length == 0 ? "Usage: echo <text>" : string.Join(" ", args); }
}

public class NslookupCommand : BaseCommand
{
    private static readonly string[] AliasesArr = new string[] { "resolve", "dns" };
    public override string Name { get { return "nslookup"; } }
    public override string[] Aliases { get { return AliasesArr; } }
    public override string Description { get { return "Resolve domain to IP"; } }

    public override string Execute(string[] args)
    {
        if (args.Length == 0) return "Usage: nslookup <domain.com>";
        try
        {
            var dnsClient = new DnsClient();
            dnsClient.Connect(new Address(8, 8, 8, 8));
            dnsClient.SendAsk(args[0]);
            Address resolvedIP = dnsClient.Receive(5000);
            if (resolvedIP != null && resolvedIP.GetHashCode() != 0) return $"{args[0]} resolves to -> {resolvedIP.ToString()}";
            else return "DNS query timed out or failed.";
        }
        catch (Exception ex) { return $"DNS Error: {ex.Message}"; }
    }
}

public class CdCommand : BaseCommand
{
    public override string Name { get { return "cd"; } }
    public override string Description { get { return "Change directory"; } }

    public override string Execute(string[] args)
    {
        if (args.Length <= 0)
        {
            Kernel.CurrentPath = "/";
            return "";
        }

        var targetPath = PathHelper.Resolve(args[0]);

        if (!Directory.Exists(targetPath))
            return $"cd: no such directory: {args[0]}";

        Kernel.CurrentPath = targetPath;
        return "";
    }
}

public class LsCommand : BaseCommand
{
    private static readonly string[] AliasesArr = new string[] { "dir" };
    public override string Name { get { return "ls"; } }
    public override string[] Aliases { get { return AliasesArr; } }
    public override string Description { get { return "List directory contents"; } }

    public override string Execute(string[] args)
    {
        var targetPath = args.Length >= 1 ? PathHelper.Resolve(args[0]) : Kernel.CurrentPath;

        if (!Directory.Exists(targetPath))
            return $"ls: no such directory: {(args.Length >= 1 ? args[0] : targetPath)}";

        try
        {
            var dirs = Directory.GetDirectories(targetPath).OrderBy(d => d).ToArray();
            var files = Directory.GetFiles(targetPath).OrderBy(f => f).ToArray();

            var builder = new StringBuilder();
            builder.AppendLine();
            foreach (var dir in dirs)
                builder.AppendLine($"-- Dir    {Path.GetFileName(dir)}");
            foreach (var file in files)
                builder.AppendLine($"--        {Path.GetFileName(file)}");
            builder.AppendLine();
            builder.AppendLine($"{dirs.Length} Directories | {files.Length} Files");
            builder.AppendLine();
            return builder.ToString();
        }
        catch (UnauthorizedAccessException)
        {
            return $"ls: permission denied: {targetPath}";
        }
    }
}

public class MkdirCommand : BaseCommand
{
    public override string Name { get { return "mkdir"; } }
    public override string Description { get { return "Create directory"; } }

    public override string Execute(string[] args)
    {
        if (args.Length != 1)
            return "Usage: mkdir <name>";

        var targetPath = PathHelper.Resolve(args[0]);

        if (Directory.Exists(targetPath))
            return $"mkdir: directory already exists: {args[0]}";

        try
        {
            Directory.CreateDirectory(targetPath);
            return "";
        }
        catch (Exception ex)
        {
            return $"mkdir: cannot create directory: {ex.Message}";
        }
    }
}

public class RmCommand : BaseCommand
{
    private static readonly string[] AliasesArr = new string[] { "del", "delete" };
    public override string Name { get { return "rm"; } }
    public override string[] Aliases { get { return AliasesArr; } }
    public override string Description { get { return "Remove file or directory"; } }

    public override string Execute(string[] args)
    {
        if (args.Length != 1)
            return "Usage: rm <name>";

        var targetPath = PathHelper.Resolve(args[0]);

        try
        {
            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, recursive: false);
                return "";
            }
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
                return "";
            }
            return $"rm: no such file or directory: {args[0]}";
        }
        catch (IOException)
        {
            return $"rm: directory not empty: {args[0]} (use rm -r to remove recursively)";
        }
        catch (UnauthorizedAccessException)
        {
            return $"rm: permission denied: {args[0]}";
        }
        catch (Exception ex)
        {
            return $"rm: cannot remove: {ex.Message}";
        }
    }
}

public class CatCommand : BaseCommand
{
    private static readonly string[] AliasesArr = new string[] { "type", "more" };
    public override string Name { get { return "cat"; } }
    public override string[] Aliases { get { return AliasesArr; } }
    public override string Description { get { return "Display file contents"; } }

    public override string Execute(string[] args)
    {
        if (args.Length != 1)
            return "Usage: cat <file>";

        var targetPath = PathHelper.Resolve(args[0]);

        if (!File.Exists(targetPath))
            return $"cat: no such file: {args[0]}";

        try
        {
            return File.ReadAllText(targetPath);
        }
        catch (UnauthorizedAccessException)
        {
            return $"cat: permission denied: {args[0]}";
        }
        catch (Exception ex)
        {
            return $"cat: cannot read file: {ex.Message}";
        }
    }
}

public class TouchCommand : BaseCommand
{
    public override string Name { get { return "touch"; } }
    public override string Description { get { return "Create empty file"; } }

    public override string Execute(string[] args)
    {
        if (args.Length != 1)
            return "Usage: touch <file>";

        var targetPath = PathHelper.Resolve(args[0]);

        try
        {
            if (!File.Exists(targetPath))
                File.Create(targetPath).Dispose();
            else
                File.SetLastWriteTime(targetPath, DateTime.Now);
            return "";
        }
        catch (UnauthorizedAccessException)
        {
            return $"touch: permission denied: {args[0]}";
        }
        catch (Exception ex)
        {
            return $"touch: cannot create file: {ex.Message}";
        }
    }
}

public class MemCommand : BaseCommand
{
    public override string Name { get { return "mem"; } }
    public override string Description { get { return "Show memory usage"; } }

    public override string Execute(string[] args)
    {
        return "Memory usage data unavailable.";
    }
}

public class TasksCommand : BaseCommand
{
    private static readonly string[] AliasesArr = new string[] { "ps" };
    public override string Name { get { return "tasks"; } }
    public override string[] Aliases { get { return AliasesArr; } }
    public override string Description { get { return "List running threads"; } }

    public override string Execute(string[] args)
    {
        var threads = Kernel.GetThreadList();
        var sb = new StringBuilder();
        sb.AppendLine($"{"ID",-6}{"PRIORITY",-12}{"STATE",-12}{"NAME"}");
        sb.AppendLine(new string('-', 50));
        int id = 0;
        foreach (Thread thread in threads)
        {
            sb.AppendLine($"{id,-6}{thread.Priority,-12}{thread.ThreadState,-12}{thread.Name ?? "Unnamed"}");
            id++;
        }
        return $"{threads.Count} thread(s) running";
    }
}

public class SysInfoCommand : BaseCommand
{
    private static readonly string[] AliasesArr = new string[] { "info", "system" };
    public override string Name { get { return "sysinfo"; } }
    public override string[] Aliases { get { return AliasesArr; } }
    public override string Description { get { return "Display system information"; } }

    [DllImport("*", EntryPoint = "get_cpu_brand")] public static extern IntPtr get_cpu_brand();
    [DllImport("*", EntryPoint = "get_ram_info")] public static extern IntPtr get_ram_info();

    public override string Execute(string[] args)
    {
        ulong totalPages = PageAllocator.TotalPageCount;
        ulong freePages = PageAllocator.FreePageCount;
        ulong usedPages = totalPages - freePages;
        ulong pageSize = PageAllocator.PageSize;

        ulong total = totalPages * pageSize / 1024 / 1024;
        ulong used = usedPages * pageSize / 1024 / 1024;

        var sb = new StringBuilder();
        sb.AppendLine("+--------------------------------------+");
        sb.AppendLine("|          VOIDOS SYSTEM INFO          |");
        sb.AppendLine("+--------------------------------------+");
        sb.AppendLine(Line("OS Name:", "VoidOS v3.0.49"));
        sb.AppendLine(Line("Kernel:", "Cosmos Gen3"));
        sb.AppendLine(Line("Uptime:", GetUptime()));
        sb.AppendLine(Line("CPU:", Marshal.PtrToStringAnsi(get_cpu_brand())));
        sb.AppendLine(Line("Memory:", $"{used}/{total} MB"));
        sb.AppendLine("+--------------------------------------+");
        sb.AppendLine("|          NETWORK STATUS              |");
        sb.AppendLine("+--------------------------------------+");
        sb.AppendLine(Line("Device:", GetNetDevice()));
        sb.AppendLine(Line("IP:", GetIP()));
        sb.AppendLine(Line("Link:", GetLinkStatus()));
        sb.AppendLine("+--------------------------------------+");
        sb.AppendLine("|          SERVICES                    |");
        sb.AppendLine("+--------------------------------------+");
        sb.AppendLine(Line("SSH Server:", Kernel.SshRunning ? "RUNNING" : "STOPPED"));
        sb.AppendLine("+--------------------------------------+");
        return sb.ToString();
    }

    private static string Line(string label, string value) { return "| " + label + value.PadRight(38 - label.Length) + " |"; }
    private static string GetUptime() { var uptime = DateTime.UtcNow - Kernel.BootTime; return $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s"; }
    private static string GetNetDevice() { try { return NetworkManager.PrimaryDevice?.Name ?? "None"; } catch { return "None"; } }
    private static string GetIP() { try { var config = NetworkConfigManager.Get(NetworkManager.PrimaryDevice); return config?.IPAddress?.ToString() ?? "N/A"; } catch { return "N/A"; } }
    private static string GetLinkStatus() { try { return NetworkManager.PrimaryDevice?.LinkUp == true ? "UP" : "DOWN"; } catch { return "DOWN"; } }
}

public class IfconfigCommand : BaseCommand
{
    private static readonly string[] AliasesArr = new string[] { "ipconfig" };
    public override string Name { get { return "ifconfig"; } }
    public override string[] Aliases { get { return AliasesArr; } }
    public override string Description { get { return "Show network configuration"; } }

    public override string Execute(string[] args)
    {
        try
        {
            var device = NetworkManager.PrimaryDevice;
            if (device == null) return "No network device found.";
            var sb = new StringBuilder();
            sb.AppendLine($"Device: {device.Name}");
            sb.AppendLine($"MAC:    {device.MacAddress}");
            sb.AppendLine($"Link:   {(device.LinkUp ? "UP" : "DOWN")}");
            var config = NetworkConfigManager.Get(device);
            if (config != null)
            {
                sb.AppendLine($"IP:     {config.IPAddress}");
                sb.AppendLine($"Mask:   {config.SubnetMask}");
                sb.AppendLine($"GW:     {config.DefaultGateway}");
            }
            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }
}

public class PingCommand : BaseCommand
{
    public override string Name { get { return "ping"; } }
    public override string Description { get { return "Ping a host (TCP port 80)"; } }

    public override string Execute(string[] args)
    {
        if (args.Length == 0) return "Usage: ping <ip|hostname>";
        try
        {
            var target = args[0];
            Address ip;
            if (!TryParseAddress(target, out ip))
            {
                var dnsClient = new DnsClient();
                dnsClient.Connect(new Address(8, 8, 8, 8));
                dnsClient.SendAsk(target);
                ip = dnsClient.Receive(5000);
                if (ip == null || ip.GetHashCode() == 0) return $"Cannot resolve: {target}";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Pinging {ip} (TCP port 80)...");
            for (int i = 0; i < 4; i++)
            {
                var start = DateTime.UtcNow;
                bool success = false;
                try
                {
                    using var client = new TcpClient(new IPEndPoint(IPAddress.Any, 0));
                    //var result = client.BeginConnect(new IPAddress(ip.ToByteArray()), 80, null, null);
                    //success = result.AsyncWaitHandle.WaitOne(2000, false);
                    //if (success) client.EndConnect(result);
                }
                catch { success = false; }

                var latency = (DateTime.UtcNow - start).TotalMilliseconds;
                sb.AppendLine(success ? $"Reply from {ip}: time={latency:F0}ms" : "Request timed out.");
                if (i < 3) Thread.Sleep(1000);
            }
            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    private static bool TryParseAddress(string ip, out Address address)
    {
        address = null;
        try
        {
            var p = ip.Split('.');
            if (p.Length == 4 && byte.TryParse(p[0], out var b1) && byte.TryParse(p[1], out var b2)
                && byte.TryParse(p[2], out var b3) && byte.TryParse(p[3], out var b4))
            {
                address = new Address(b1, b2, b3, b4);
                return true;
            }
        }
        catch { }
        return false;
    }
}

public class WgetCommand : BaseCommand
{
    public override string Name { get { return "wget"; } }
    public override string Description { get { return "Download file via HTTP"; } }

    public override string Execute(string[] args)
    {
        if (args.Length < 1) return "Usage: wget <url> [output]";
        string url = args[0];
        string output = args.Length > 1 ? PathHelper.Resolve(args[1]) : PathHelper.Resolve(url.Split('/').Last());
        try
        {
            string cleanedUrl = url.Replace("http://", "").Replace("https://", "");
            var slashIndex = cleanedUrl.IndexOf('/');
            string host = slashIndex < 0 ? cleanedUrl : cleanedUrl.Substring(0, slashIndex);
            string path = slashIndex < 0 ? "/" : cleanedUrl.Substring(slashIndex);
            int port = 80;

            if (host.Contains(':'))
            {
                var parts = host.Split(':');
                host = parts[0];
                int.TryParse(parts[1], out port);
            }

            Address ip;
            if (!TryParseAddress(host, out ip))
            {
                var dns = new DnsClient();
                dns.Connect(new Address(8, 8, 8, 8));
                dns.SendAsk(host);
                ip = dns.Receive(5000);
                if (ip == null || ip.GetHashCode() == 0) return $"Cannot resolve host: {host}";
            }

            Console.WriteLine($"Connecting to {host}:{port}...");

            using var client = new TcpClient(new IPEndPoint(IPAddress.Any, 0));
            //client.Connect(new IPAddress(ip.ToByteArray()), port);

            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream) { AutoFlush = true };

            writer.WriteLine($"GET {path} HTTP/1.1");
            writer.WriteLine($"Host: {host}");
            writer.WriteLine("Connection: close");
            writer.WriteLine();

            string response = reader.ReadToEnd();
            int headerEnd = response.IndexOf("\r\n\r\n");
            if (headerEnd < 0) return "Failed to parse HTTP response.";

            string headers = response.Substring(0, headerEnd);
            string body = response.Substring(headerEnd + 4);

            if (!headers.Contains("200 OK")) return $"HTTP Error: {headers.Split('\r')[0]}";

            System.IO.File.WriteAllText(output, body);
            return $"Saved to {output} ({body.Length} bytes)";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    private static bool TryParseAddress(string ip, out Address address)
    {
        address = null;
        try
        {
            var p = ip.Split('.');
            if (p.Length == 4 && byte.TryParse(p[0], out var b1) && byte.TryParse(p[1], out var b2)
                && byte.TryParse(p[2], out var b3) && byte.TryParse(p[3], out var b4))
            {
                address = new Address(b1, b2, b3, b4);
                return true;
            }
        }
        catch { }
        return false;
    }
}