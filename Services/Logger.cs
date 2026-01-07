using System;
using System.IO;
using System.Text;

namespace XiDeAI_Pro.Services
{
    public static class Logger
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "XiDeAI", 
            "Logs"
        );
        private static readonly object _lock = new object();

        static Logger()
        {
            if (!Directory.Exists(LogDir))
            {
                Directory.CreateDirectory(LogDir);
            }
        }

        public static void Log(string category, string message)
        {
            try
            {
                if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);

                string date = DateTime.Now.ToString("yyyy-MM-dd");
                string filename = $"Log_{date}_{category}.txt";
                string path = Path.Combine(LogDir, filename);
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string line = $"[{timestamp}] {message}";

                lock (_lock)
                {
                    File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                // Fallback to debug output if disk write fails
                System.Diagnostics.Debug.WriteLine($"Logger Failure: {ex.Message}");
            }
        }

        // Aliases for common modules
        public static void Sys(string msg) => Log("System", msg);
        public static void Twitter(string msg) => Log("Twitter", msg);
        public static void AI(string msg) => Log("AI", msg);
        public static void Telegram(string msg) => Log("Telegram", msg);
        public static void News(string msg) => Log("News", msg);
    }
}

