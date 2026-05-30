using System;
using System.IO;
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
        private long _dbLastPosition = 0;
        private readonly object _dbLock = new object();

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

            lock (_dbLock)
            {
                _dbLastPosition = new FileInfo(DbPath).Length;
            }

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
                        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        if (fs.Length <= _dbLastPosition) return;

                        fs.Seek(_dbLastPosition, SeekOrigin.Begin);
                        using var sr = new StreamReader(fs);
                        string? line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                var parts = line.Split('|');
                                if (parts.Length >= 2)
                                {
                                    string strategy = parts[1].Trim().ToUpperInvariant();
                                    // source olarak strateji adını gönder: "ALPHA" veya "PREMOVE"
                                    if (strategy == "ALPHA" || strategy == "PREMOVE")
                                    {
                                        OnLog?.Invoke($"📄 DB Sinyal: {line}");
                                        OnSignalDetected?.Invoke(line, strategy);
                                    }
                                }
                            }
                        }
                        _dbLastPosition = fs.Position;
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"❌ DB Dosya Okuma Hatası: {ex.Message}");
                }
            });
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

