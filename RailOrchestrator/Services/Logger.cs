using System;
using System.IO;

namespace WpfRagApp.Services
{
    public static class Logger
    {
        private static string LogPath = "debug_log.txt";

        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogPath, $"{DateTime.Now}: {message}\n");
            }
            catch { }
        }

        public static void LogError(string message, Exception ex)
        {
            Log($"ERROR: {message}\nException: {ex}");
        }
    }
}





