using Cosmos.Kernel.HAL.Interfaces.Devices;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Filesystems.Fat;
using Cosmos.Kernel.System.Network;
using Cosmos.Kernel.System.Network.Config;
using Cosmos.Kernel.System.Network.IPv4;
using Cosmos.Kernel.System.Network.IPv4.UDP.DHCP;
using Cosmos.Kernel.System.Storage;
using Cosmos.Kernel.System.Timer;
using Cosmos.Kernel.System.Vfs;
using VoidOS.Ssh;
using Sys = Cosmos.Kernel.System;
using Thread = System.Threading.Thread;
using ThreadStart = System.Threading.ThreadStart;


namespace VoidOS;

public class Kernel : Sys.Kernel
{
    public static string CurrentPath = "/";
    public static string RemotePassword = "1234";
    public static DateTime BootTime { get; private set; }
    public static bool SshRunning { get; private set; }  

    public static List<Thread> ThreadList { get; } = new List<Thread>();

    private static readonly object _lock = new object();

    protected override void BeforeRun()
    {
        BootTime = DateTime.UtcNow;
        RegisterCommands();
        
        try
        {
            InitializeNetwork();
            InitializeFileSystem();
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

        Console.Clear();
        Console.WriteLine(@"
 /$$    /$$          /$$       /$$        /$$$$$$   /$$$$$$ 
| $$   | $$         |__/      | $$       /$$__  $$ /$$__  $$
| $$   | $$ /$$$$$$  /$$  /$$$$$$$      | $$  \ $$| $$  \__/
|  $$ / $$//$$__  $$| $$ /$$__  $$      | $$  | $$|  $$$$$$ 
 \  $$ $$/| $$  \ $$| $$| $$  | $$      | $$  | $$ \____  $$
  \  $$$/ | $$  | $$| $$| $$  | $$      | $$  | $$ /$$  \ $$
   \  $/  |  $$$$$$/| $$|  $$$$$$$      |  $$$$$$/|  $$$$$$/
    \_/    \______/ |__/ \_______/       \______/  \______/  
                              v2.0.0 - Cosmos Gen3
");

        Console.WriteLine("\nType 'help' for available commands.");
    }

    private void InitializeFileSystem()
    {
        Console.Write("[INIT] File system... ");
        try
        {
            Console.WriteLine($"Disks found: {StorageManager.DeviceCount}");
            Console.WriteLine($"Partitions found: {StorageManager.Partitions.Count}");
            Console.WriteLine($"PrimaryDevice null? {StorageManager.PrimaryDevice == null}");

            FatFilesystemType fat = new(StorageManager.PrimaryDevice);
            VfsManager.RegisterFilesystem("fat", fat);

            bool mounted = VfsManager.TryMount("fat", "", MountFlags.None, "/mnt", out var mount);
            if (!mounted)
            {
                Console.WriteLine("Not formatted yet, formatting...");
                if (!fat.TryFormat(default, new FatFormatOptions { Type = FatType.Fat32 }))
                {
                    Console.WriteLine("FAILED: format failed");
                    return;
                }

                mounted = VfsManager.TryMount("fat", "", MountFlags.None, "/mnt", out mount);
                if (!mounted)
                {
                    Console.WriteLine("FAILED: mount failed even after formatting");
                    return;
                }
            }

            var test1 = Directory.GetDirectories("/mnt");
            var test2 = Directory.GetFiles("/mnt");

            if (!Directory.Exists("/mnt/system")) Directory.CreateDirectory("/mnt/system");
            if (!Directory.Exists("/mnt/logs")) Directory.CreateDirectory("/mnt/logs");
            if (!Directory.Exists("/mnt/temp")) Directory.CreateDirectory("/mnt/temp");

            Console.Write("OK \n");
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
        Console.WriteLine("Starting SSH Server...");
        try
        {
            StartSshServer();
        }
        catch (Exception ex)
        {
            SshRunning = false;
            Console.WriteLine($"Failed to start SSH: {ex.Message} \n");
        }
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
            case "clr":
            case "clear":
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
        Logger.Log("System shutdown");
        SshRunning = false;
        SshServer.Stop();
        Thread.Sleep(500);
        Stop();
    }

    private void Reboot()
    {
        Console.WriteLine("Rebooting...");
        Logger.Log("System reboot");
        SshServer.Stop();
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
    }

    public static void StartSshServer()
    {
        if (SshRunning) return;
        SshRunning = true;
        RunServiceAsync(() => SshServer.Start());
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
}

internal sealed class MemoryBlockDevice : IBlockDevice
{
    private readonly byte[] _storage;

    public MemoryBlockDevice(string name, ulong blockSize, ulong blockCount)
    {
        Name = name;
        BlockSize = blockSize;
        BlockCount = blockCount;
        _storage = new byte[blockSize * blockCount];
    }

    public string Name { get; }
    public ulong BlockSize { get; }
    public ulong BlockCount { get; }

    public void ReadBlock(ulong blockNo, ulong blockCount, Span<byte> data)
        => _storage.AsSpan((int)(blockNo * BlockSize), (int)(blockCount * BlockSize)).CopyTo(data);

    public void WriteBlock(ulong blockNo, ulong blockCount, ReadOnlySpan<byte> data)
        => data.Slice(0, (int)(blockCount * BlockSize)).CopyTo(_storage.AsSpan((int)(blockNo * BlockSize)));

    public void Flush() { }
}
