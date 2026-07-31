using System;
using System.Collections.Generic;
using System.Text;

namespace VoidOS
{
    public static class Logger
    {
        public static void Log(string message)
        {
            var logLine = $"[{DateTime.UtcNow:HH:mm:ss}] {message}";
            Console.WriteLine(logLine);
            try { File.AppendAllText("/mnt/logs/system.log", logLine + "\n"); } catch { }
        }

        public static void LogError(string message)
        {
            var logLine = $"[{DateTime.UtcNow:HH:mm:ss}] [ERROR] {message}";
            Console.WriteLine(logLine);
            try { File.AppendAllText("/mnt/logs/error.log", logLine + "\n"); } catch { }
        }
    }
}
