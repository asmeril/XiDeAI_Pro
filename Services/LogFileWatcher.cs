using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace XiDeAI_Pro.Services
{
    /// <summary>
    /// Alpha ve PreMove robotlarının sinyal veritabanını (C:\iDeal\Sinyal_Log_Database.txt)
    /// tail-style izler. Yeni satır eklendiğinde OnSignalDetected olayı tetiklenir.
    /// Robotlar zaten kendi eşiklerini uygular (Alpha≥90, PreMove≥75); burada filtre yok.
    /// </summary>
    public class LogFileWatcher
    {
        private FileSystemWatcher? _dbWatcher;
        private readonly object _dbLock = new object();
        private readonly HashSet<string> _seenSignalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public event Action<string, string>? OnSignalDetected; // line, "ALPHA"|"PREMOVE"
        public event Action<string>? OnLog;

        public bool IsRunning { get; private set; } = false;
        public bool IsPaused { get; private set; } = false;

        private const string DbPath = @"C:\iDeal\Sinyal_Log_Database.txt";

        public void Start()
        {
            Stop();

            if (!File.Exists(DbPath))
            {
                OnLog?.Invoke($"⚠️ Alpha/PreMove DB dosyası bulunamadı: {DbPath}");
                return;
            }

            LoadSeenKeys(DbPath);

            string dir = Path.GetDirectoryName(DbPath)!;
            string file = Path.GetFileName(DbPath);

            _dbWatcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _dbWatcher.Changed += (s, e) => ProcessDbFileSafe(e.FullPath);
            _dbWatcher.EnableRaisingEvents = true;
            IsRunning = true;
            OnLog?.Invoke($"✅ Alpha/PreMove DB izleniyor: {DbPath}");
        }

        private void ProcessDbFileSafe(string path)
        {
            if (IsPaused) return;
            Task.Run(() =>
            {
                try
                {
                    System.Threading.Thread.Sleep(500); // Kısa debounce
                    lock (_dbLock)
                    {
                        foreach (var line in ReadStableLines(path))
                        {
                            if (TryBuildSignalKey(line, out var key, out var strategy))
                            {
                                if (_seenSignalKeys.Add(key))
                                {
                                    OnLog?.Invoke($"📄 DB Sinyal: {line}");
                                    OnSignalDetected?.Invoke(line, strategy);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"❌ DB Dosya Okuma Hatası: {ex.Message}");
                }
            });
        }

        private void LoadSeenKeys(string path)
        {
            lock (_dbLock)
            {
                _seenSignalKeys.Clear();
                foreach (var line in ReadStableLines(path))
                {
                    if (TryBuildSignalKey(line, out var key, out _))
                        _seenSignalKeys.Add(key);
                }
            }
        }

        private static List<string> ReadStableLines(string path)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs);
                    return sr.ReadToEnd()
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();
                }
                catch when (attempt < 2)
                {
                    System.Threading.Thread.Sleep(250);
                }
                catch
                {
                    return new List<string>();
                }
            }

            return new List<string>();
        }

        private static bool TryBuildSignalKey(string line, out string key, out string strategy)
        {
            key = string.Empty;
            strategy = string.Empty;
            var parts = line.Split('|');
            if (parts.Length < 6) return false;
            if (parts[0].Trim().Equals("Sembol", StringComparison.OrdinalIgnoreCase)) return false;

            string symbol = SymbolNormalizer.NormalizeSignalSymbol(parts[0]);
            if (!SymbolNormalizer.IsKnownBistSymbol(symbol)) return false;

            strategy = parts[1].Trim().ToUpperInvariant();
            if (strategy != "ALPHA" && strategy != "PREMOVE") return false;

            string period = parts[2].Trim().ToUpperInvariant();
            if (period == "D") period = "G";
            string detectedAt = parts[3].Trim();
            string status = parts[5].Trim().ToUpperInvariant();
            if (status.Contains("ROKET")) status = "AKTIF";
            if (status == "KAPALI") return false;
            if (status != "AKTIF" && status != "PULLBACK_ADAY") return false;

            key = $"{symbol}|{strategy}|{period}|{detectedAt}|{status}";
            return true;
        }

        public void Stop()
        {
            _dbWatcher?.Dispose();
            _dbWatcher = null;
            IsRunning = false;
            IsPaused = false;
        }

        public void Pause()
        {
            if (_dbWatcher != null) _dbWatcher.EnableRaisingEvents = false;
            IsPaused = true;
        }

        public void Resume()
        {
            if (_dbWatcher != null) _dbWatcher.EnableRaisingEvents = true;
            IsPaused = false;
        }
    }
}
