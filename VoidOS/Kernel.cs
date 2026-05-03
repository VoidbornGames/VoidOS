using Cosmos.Kernel.HAL.Interfaces.Devices;
using Cosmos.Kernel.System.Network;
using Cosmos.Kernel.System.Network.Config;
using Cosmos.Kernel.System.Network.IPv4;
using Cosmos.Kernel.System.Network.IPv4.UDP.DHCP;
using Cosmos.Kernel.System.Timer;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Sys = Cosmos.Kernel.System;
using Thread = System.Threading.Thread;
using ThreadStart = System.Threading.ThreadStart;

namespace VoidOS;

public class Kernel : Sys.Kernel
{
    public static string CurrentPath = "";
    public static string RemotePassword = "1234";
    public static DateTime BootTime { get; private set; }
    public static bool VrsRunning { get; private set; }

    public static List<Thread> ThreadList { get; } = new List<Thread>();

    private static readonly object _lock = new object();

    protected override void BeforeRun()
    {
        BootTime = DateTime.UtcNow;
        RegisterCommands();

        Console.WriteLine(@"
 __      __   _     _    ____   _____ 
 \ \    / /  (_)   | |  / __ \ / ____|
  \ \  / /__  _  __| | | |  | | (___  
   \ \/ / _ \| |/ _` | | |  | |\___ \ 
    \  / (_) | | (_| | | |__| |____) |
     \/ \___/|_|\__,_|  \____/|_____/    v3.0.49 - Native AOT
");

        try
        {
            InitializeNetwork();
            StartServices();
        }
        catch (Exception ex)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine($"[FATAL ERROR] OS CRASHED DURING BOOT!");
            Console.WriteLine($"[ERROR TYPE] {ex.GetType().Name}");
            Console.WriteLine($"[MESSAGE] {ex.Message}");
            Console.WriteLine($"[STACK] {ex.StackTrace}");
            Console.WriteLine("==================================================");

            while (true)
            {
                TimerManager.Wait(1000);
            }
        }

        Console.WriteLine("\nType 'help' for available commands.");
    }

    private void InitializeFileSystem()
    {
        Console.Write("[INIT] File system... ");
        try
        {
            var test = Directory.GetDirectories("0:/");

            if (!Directory.Exists("0:/System")) Directory.CreateDirectory("0:/System");
            if (!Directory.Exists("0:/Logs")) Directory.CreateDirectory("0:/Logs");
            if (!Directory.Exists("0:/Temp")) Directory.CreateDirectory("0:/Temp");
            Console.WriteLine("OK");
        }
        catch (Exception ex) { Console.WriteLine($"FAILED: {ex.Message}"); }
    }

    private void InitializeNetwork()
    {
        Console.Write("[INIT] Network... ");
        try
        {
            if (NetworkManager.PrimaryDevice == null)
            {
                Console.WriteLine("No device found (Network disabled). \n");
                return;
            }

            var device = NetworkManager.PrimaryDevice;

            int attempts = 0;
            while (!device.LinkUp && attempts < 30) { TimerManager.Wait(100); attempts++; }

            if (!device.LinkUp) { Console.WriteLine("Link down (Network disabled). \n"); return; }

            NetworkStack.Initialize();

            var dhcp = new DHCPClient();
            if (dhcp.SendDiscoverPacket() == -1)
            {
                Console.WriteLine("[INIT] DHCP failed, using static config... \n");
                SetStaticConfig(device);
                Console.WriteLine("[INIT] Static config applied. \n");
            }
            else
            {
                var config = NetworkConfigManager.Get(device);
                Console.WriteLine($"OK ({config.IPAddress})");
            }

            DNSConfig.Add(new Address(8, 8, 8, 8));
            DNSConfig.Add(new Address(1, 1, 1, 1));
        }
        catch (Exception ex) { Console.WriteLine($"FAILED: {ex.Message} \n"); }
    }

    private void SetStaticConfig(INetworkDevice device)
    {
        var config = new IPConfig(
            new Address(192, 168, 1, 100),
            new Address(255, 255, 255, 0),
            new Address(192, 168, 1, 1)
        );
        NetworkConfigManager.AddConfig(device, config);
    }

    private void StartServices()
    {
        Console.Write("[INIT] VRS Service... ");

        try
        {
            if (NetworkManager.PrimaryDevice == null || !NetworkManager.PrimaryDevice.LinkUp)
            {
                Console.WriteLine("Skipped (No network). \n");
                return;
            }

            RunSystemServiceAsync(() =>
            {
                try { StartVrs(); VrsRunning = true; }
                catch (Exception ex) { VrsRunning = false; LogError($"VRS: {ex.Message}"); }
            });
            Console.WriteLine("Started \n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start: {ex.Message} \n");
        }
    }

    private void StartVrs()
    {
        Console.WriteLine("[VRS] Listening on port 23");

        TcpListener listener = null;
        while (true)
        {
            try
            {
                while (true)
                {
                    listener = new TcpListener(IPAddress.Any, 23);
                    listener.Start();

                    if (listener.Pending())
                    {
                        var client = listener.AcceptTcpClient();
                        string remoteIp;
                        try { remoteIp = client.Client.RemoteEndPoint?.ToString() ?? "Unknown"; }
                        catch { remoteIp = "Unknown"; }

                        HandleClient(client, remoteIp);
                    }

                    listener.Stop();
                    listener.Dispose();
                    listener = null;

                    TimerManager.Wait(100);
                }
            }
            catch (Exception ex)
            {
                listener.Stop();
                listener.Dispose();
                listener = null;

                Console.WriteLine($"[VRS] Error: {ex.Message} \n");
                TimerManager.Wait(1000);
            }
        }
    }

    private static void HandleClient(TcpClient client, string remoteIp)
    {
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

            int failedAttempts = 0;
            const int maxAttempts = 3;

            SendMultiLine(writer, ["\r\nVoidOS Remote Shell v1.0\r\n", "Password: "]);

            while (failedAttempts < maxAttempts)
            {
                var pass = SafeReadLine(reader, stream);
                if (pass == null) return;

                if (pass == RemotePassword) { break; }

                failedAttempts++;
                Send(writer, $"Access denied ({maxAttempts - failedAttempts} attempts left)\r\n> ");
            }

            if (failedAttempts >= maxAttempts)
            {
                Send(writer, "Too many failed attempts. Connection closed.\r\n");
                Log($"VRS: Auth failed from {remoteIp} (brute force?)");
                return;
            }

            SendMultiLine(writer, [ "\r\nWelcome to VoidOS!", $"Connected from: {remoteIp}", $"Local time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC", "Type 'help' for commands, 'exit' to disconnect", "" ]);

            string currentPath = CurrentPath;
            while (true)
            {
                Send(writer, $"{currentPath} $> ");
                var cmd = SafeReadLine(reader, stream);
                if (cmd == null) break;
                cmd = cmd.Trim();

                if (string.IsNullOrEmpty(cmd)) continue;
                if (cmd.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

                if (cmd.StartsWith("cd ", StringComparison.OrdinalIgnoreCase))
                {
                    CommandManager.Execute(cmd);
                    currentPath = Kernel.CurrentPath;
                    continue;
                }

                var output = CommandManager.Execute(cmd);
                if (!string.IsNullOrEmpty(output)) Send(writer, output + "\r\n");
            }

            Send(writer, "\r\nGoodbye!\r\n");
            Log($"VRS: {remoteIp} disconnected");
        }
        catch (Exception ex) { LogError($"VRS Client: {ex.Message}"); }
        finally { try { client.Close(); } catch { } }
    }

    protected override void Run()
    {
        Console.Write($"{CurrentPath} $> ");
        var input = Console.ReadLine();
        if (string.IsNullOrEmpty(input)) return;
        input = input.Trim();

        switch (input.ToLower())
        {
            case "halt":
            case "shutdown":
                Shutdown();
                break;
            case "reboot":
                Reboot();
                break;
            case "cls":
                Console.Clear();
                break;
            default:
                var result = CommandManager.Execute(input);
                if (!string.IsNullOrEmpty(result)) Console.WriteLine(result);
                break;
        }
    }

    private void Shutdown()
    {
        Console.WriteLine("Shutting down...");
        Log("System shutdown");
        VrsRunning = false;
        Thread.Sleep(500);
        Cosmos.Kernel.Kernel.Halt();
        Stop();
    }

    private void Reboot()
    {
        Console.WriteLine("Rebooting...");
        Log("System reboot");
        Cosmos.Kernel.Kernel.Halt();
    }

    public static void Log(string message)
    {
        var logLine = $"[{DateTime.UtcNow:HH:mm:ss}] {message}";
        Console.WriteLine(logLine);
        try { File.AppendAllText("0:/Logs/system.log", logLine + "\n"); } catch { }
    }

    public static void LogError(string message)
    {
        var logLine = $"[{DateTime.UtcNow:HH:mm:ss}] [ERROR] {message}";
        Console.WriteLine(logLine);
        try { File.AppendAllText("0:/Logs/error.log", logLine + "\n"); } catch { }
    }

    public static List<Thread> GetThreadList()
    {
        lock (_lock) return ThreadList.ToList();
    }

    private static void RegisterCommands()
    {
        CommandManager.Register(new LsCommand());
        CommandManager.Register(new CdCommand());
        CommandManager.Register(new CatCommand());
        CommandManager.Register(new TouchCommand());
        CommandManager.Register(new MkdirCommand());
        CommandManager.Register(new RmCommand());

        //CommandManager.Register(new PingCommand());
        CommandManager.Register(new IfconfigCommand());
        //CommandManager.Register(new WgetCommand());
        CommandManager.Register(new NslookupCommand());

        CommandManager.Register(new SysInfoCommand());
        CommandManager.Register(new MemCommand());
        CommandManager.Register(new TasksCommand());
        CommandManager.Register(new ClearCommand());
        CommandManager.Register(new HelpCommand());
        CommandManager.Register(new EchoCommand());

        CommandManager.Register(new ChangeRemotePassCommand());
    }

    #region Async Helpers
    public static void RunAsync(ThreadStart action)
    {
        var t = new Thread(action) { IsBackground = true };
        lock (_lock) ThreadList.Add(t);
        t.Start();
    }

    public static void RunServiceAsync(ThreadStart action)
    {
        var t = new Thread(action) { IsBackground = true, Priority = ThreadPriority.AboveNormal };
        lock (_lock) ThreadList.Add(t);
        t.Start();
    }

    public static void RunSystemServiceAsync(ThreadStart action)
    {
        var t = new Thread(action) { IsBackground = true, Priority = ThreadPriority.Highest };
        lock (_lock) ThreadList.Add(t);
        t.Start();
    }
    #endregion

    #region Network Send Helpers
    private static void Send(StreamWriter writer, string data)
    {
        writer.Write(data);
        writer.Write("\u001E");
        writer.Flush();
    }

    private static void SendMultiLine(StreamWriter writer, IEnumerable<string> data)
    {
        writer.Write(string.Join("\r\n", data) + "\r\n");
        writer.Write("\u001E");
        writer.Flush();
    }

    private static string SafeReadLine(StreamReader reader, NetworkStream stream)
    {
        int waited = 0;
        while (!stream.DataAvailable && waited < 300) { TimerManager.Wait(100); waited++; }
        return stream.DataAvailable ? reader.ReadLine() : null;
    }
    #endregion
}