using System;
using System.IO;
using System.Text;
using System.Linq;

namespace XiDeAI_Pro.Services
{
    /// <summary>
    /// v2.0 Enhanced Logger with log levels, daily rotation, and auto-cleanup
    /// </summary>
    public static class Logger
    {
        public enum LogLevel { Debug, Info, Warning, Error }

        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "XiDeAI", 
            "Logs"
        );
        private static readonly object _lock = new object();
        private static LogLevel _minLevel = LogLevel.Info; // Default minimum level
        private static bool _cleanupDone = false;

        static Logger()
        {
            if (!Directory.Exists(LogDir))
            {
                Directory.CreateDirectory(LogDir);
            }
            
            // Auto-cleanup old logs on first use
            CleanupOldLogs(7);
        }

        /// <summary>
        /// Set minimum log level (logs below this level will be ignored)
        /// </summary>
        public static void SetMinLevel(LogLevel level) => _minLevel = level;

        /// <summary>
        /// Core logging method with level support
        /// </summary>
        public static void Log(string category, string message, LogLevel level = LogLevel.Info)
        {
            if (level < _minLevel) return;

            try
            {
                if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);

                string date = DateTime.Now.ToString("yyyy-MM-dd");
                string filename = $"Log_{date}_{category}.txt";
                string path = Path.Combine(LogDir, filename);
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string levelTag = level switch
                {
                    LogLevel.Debug => "[DBG]",
                    LogLevel.Info => "[INF]",
                    LogLevel.Warning => "[WRN]",
                    LogLevel.Error => "[ERR]",
                    _ => "[???]"
                };
                string line = $"[{timestamp}] {levelTag} {message}";

                lock (_lock)
                {
                    File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Logger Failure: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete logs older than specified days
        /// </summary>
        public static void CleanupOldLogs(int olderThanDays)
        {
            if (_cleanupDone) return;
            _cleanupDone = true;

            try
            {
                if (!Directory.Exists(LogDir)) return;

                var cutoffDate = DateTime.Now.AddDays(-olderThanDays);
                var oldFiles = Directory.GetFiles(LogDir, "Log_*.txt")
                    .Select(f => new FileInfo(f))
                    .Where(f => f.LastWriteTime < cutoffDate)
                    .ToList();

                foreach (var file in oldFiles)
                {
                    try { file.Delete(); }
                    catch { /* ignore individual delete errors */ }
                }

                if (oldFiles.Count > 0)
                {
                    Log("System", $"🧹 Cleaned up {oldFiles.Count} old log files (>{olderThanDays} days)", LogLevel.Info);
                }
            }
            catch { /* Silent fail for cleanup */ }
        }

        // === LEGACY ALIASES (Backward compatibility) ===
        public static void Sys(string msg) => Log("System", msg, LogLevel.Info);
        public static void Twitter(string msg) => Log("Twitter", msg, LogLevel.Info);
        public static void AI(string msg) => Log("AI", msg, LogLevel.Info);
        public static void Telegram(string msg) => Log("Telegram", msg, LogLevel.Info);
        public static void News(string msg) => Log("News", msg, LogLevel.Info);
        public static void FanZone(string msg) => Log("FanZone", msg, LogLevel.Info);

        // === NEW LEVEL-SPECIFIC METHODS ===
        public static void Debug(string category, string msg) => Log(category, msg, LogLevel.Debug);
        public static void Info(string category, string msg) => Log(category, msg, LogLevel.Info);
        public static void Warn(string category, string msg) => Log(category, msg, LogLevel.Warning);
        public static void Error(string category, string msg) => Log(category, msg, LogLevel.Error);
        
        // Shorthand for errors with exceptions
        public static void Error(string category, Exception ex) => 
            Log(category, $"❌ {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
    }
}
