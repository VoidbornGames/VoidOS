using Cosmos.Kernel.System.Network;
using Cosmos.Kernel.System.Network.Config;
using Cosmos.Kernel.System.Network.IPv4.UDP.DHCP;
using Cosmos.Kernel.System.Timer;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Sys = Cosmos.Kernel.System;

namespace VoidOS;

public class Kernel : Sys.Kernel
{
    public static string CurrentPath = "0:/";

    protected override void BeforeRun()
    {
        RegisterCommands();

        Console.WriteLine("VoidOS booted successfully!");
        Console.WriteLine("Initializing network...");

        try
        {
            var device = NetworkManager.PrimaryDevice;
            if (device == null) { Console.WriteLine("No network device!"); return; }

            int attempts = 0;
            while (!device.LinkUp && attempts < 20) { TimerManager.Wait(100); attempts++; }
            if (!device.LinkUp) { Console.WriteLine("Link down!"); return; }

            NetworkStack.Initialize();
            Console.WriteLine("Network stack initialized.");

            var dhcp = new DHCPClient();
            if (dhcp.SendDiscoverPacket() == -1) { Console.WriteLine("DHCP failed!"); return; }

            var config = NetworkConfigManager.Get(device);
            Console.WriteLine($"IP: {config.IPAddress} | Gateway: {config.DefaultGateway}");

            RunServiceAsync(() =>
            {
                Console.WriteLine("VRS running on port 23...");
                Console.WriteLine("                         ");

                TcpListener listener = null;
                while (true)
                {
                    listener = new TcpListener(IPAddress.Any, 23);
                    listener.Start();

                    try
                    {
                        while (!listener.Pending())
                            TimerManager.Wait(100);

                        var client = listener.AcceptTcpClient();
                        HandleClient(client, listener);
                    }
                    catch (Exception ex)
                    {
                        listener.Stop();
                        listener.Dispose();
                        listener = null;

                        Console.WriteLine($"[VRS] Error: {ex.Message}");
                        TimerManager.Wait(1000);
                    }
                }
            });
        }
        catch (Exception ex) { Console.WriteLine($"Init error: {ex.Message}"); }
    }

    protected override void Run()
    {
        Console.Write($"{CurrentPath} $> ");
        var input = Console.ReadLine();
        if (!string.IsNullOrEmpty(input))
        {
            if (input.Trim().Equals("halt", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Shutting Down...");
                Cosmos.Kernel.Kernel.Halt();
                Stop();
            }
            else
            {
                string result = CommandManager.Execute(input);
                Console.Write(result);
                Console.WriteLine();
            }
        }
    }

    private static void HandleClient(TcpClient client, TcpListener listener)
    {
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

            Send(writer, "Connected To VRS \n" + "Welcome To VoidOS! \n");

            while (true)
            {
                string? cmd = SafeReadLine(reader, stream);
                if (cmd == null) break;
                if (cmd.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    client.Close();
                    client.Dispose();
                    client = null;

                    listener.Stop();
                    listener.Dispose();
                    listener = null;
                    break;
                }

                string res = CommandManager.Execute(cmd);
                Send(writer, res);
            }
        }
        catch
        {
            client.Close();
            client.Dispose();
            client = null;

            listener.Stop();
            listener.Dispose();
            listener = null;
        }
    }

    private static void Send(StreamWriter writer, string data)
    {
        writer.WriteLine(data);
        writer.Write("\u001E");
        writer.Flush();
    }

    private static string? SafeReadLine(StreamReader reader, NetworkStream stream)
    {
        int waited = 0;
        while (!stream.DataAvailable && waited < 300)
        {
            TimerManager.Wait(100);
            waited++;
        }
        if (!stream.DataAvailable)
            return null;
        return reader.ReadLine();
    }

    private static void RegisterCommands()
    {
        CommandManager.Register(new HelpCommand());
        CommandManager.Register(new SysInfoCommand());
        CommandManager.Register(new ClearCommand());
        CommandManager.Register(new EchoCommand());
    }

    public static void RunAsync(ThreadStart action)
    {
        new Thread(action) { IsBackground = true }.Start();
    }
    public static void RunServiceAsync(ThreadStart action)
    {
        new Thread(action) { IsBackground = true, Priority = ThreadPriority.AboveNormal }.Start();
    }
    public static void RunSystemServiceAsync(ThreadStart action)
    {
        new Thread(action) { IsBackground = true, Priority = ThreadPriority.Highest }.Start();
    }
}