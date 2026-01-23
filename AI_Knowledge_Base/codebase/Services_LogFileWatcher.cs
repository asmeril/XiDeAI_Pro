using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace XiDeAI_Pro.Services
{
    public class LogFileWatcher
    {
        private List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        public event Action<string, string>? OnSignalDetected; // content, source
        
        public bool IsRunning { get; private set; } = false;
        public bool IsPaused { get; private set; } = false;

        public event Action<string>? OnLog;
        
        // Debounce: Son işlenen dosya ve zamanı
        private ConcurrentDictionary<string, DateTime> _lastProcessed = new();
        private readonly TimeSpan _debounceTime = TimeSpan.FromSeconds(2);

        public void Start(string[] folders)
        {
            Stop();
            int validCount = 0;
            foreach (var folder in folders)
            {
                if (Directory.Exists(folder))
                {
                    var fsw = new FileSystemWatcher(folder, "Sinyal_*.txt");
                    fsw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;
                    fsw.Changed += (s, e) => ProcessFileSafe(e.FullPath);
                    fsw.Created += (s, e) => ProcessFileSafe(e.FullPath);
                    fsw.EnableRaisingEvents = true;
                    _watchers.Add(fsw);
                    validCount++;
                    OnLog?.Invoke($"✅ Klasör izleniyor: {folder}");
                }
                else
                {
                    OnLog?.Invoke($"⚠️ Klasör bulunamadı (atlandı): {folder}");
                }
            }
            IsRunning = _watchers.Count > 0;
            IsPaused = false;
            OnLog?.Invoke($"📁 Toplam {validCount} klasör izleniyor.");
        }

        public void Stop()
        {
            foreach (var w in _watchers) w.Dispose();
            _watchers.Clear();
            _lastProcessed.Clear();
            IsRunning = false;
            IsPaused = false;
        }
        
        public void Pause()
        {
            if (!IsRunning) return;
            foreach (var w in _watchers)
            {
                w.EnableRaisingEvents = false;
            }
            IsPaused = true;
        }
        
        public void Resume()
        {
            if (!IsRunning) return;
            foreach (var w in _watchers)
            {
                w.EnableRaisingEvents = true;
            }
            IsPaused = false;
        }

        private void ProcessFileSafe(string path)
        {
            // Skip if paused
            if (IsPaused) return;
            
            // DEBOUNCE: Aynı dosya 2 saniye içinde tekrar işlenmesin
            var now = DateTime.Now;
            if (_lastProcessed.TryGetValue(path, out var lastTime))
            {
                if (now - lastTime < _debounceTime)
                {
                    // Çok yakın zamanda işlendi, atla
                    return;
                }
            }
            _lastProcessed[path] = now;
            
            OnLog?.Invoke($"📄 Dosya değişikliği tespit edildi: {Path.GetFileName(path)}");
            
            // Dosya yazimi bitmesi icin kisa bekleme
            Task.Run(async () => {
                await Task.Delay(500);
                try
                {
                    string content = "";
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs))
                    {
                        content = sr.ReadToEnd();
                    }
                    
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        // DÜZELTME: Klasör yoluna bak, dosya adına değil!
                        string folderPath = Path.GetDirectoryName(path) ?? "";
                        string source;
                        
                        if (folderPath.Contains("KING", StringComparison.OrdinalIgnoreCase))
                            source = "KING";
                        else if (folderPath.Contains("DIP", StringComparison.OrdinalIgnoreCase))
                            source = "DIP";
                        else if (folderPath.Contains("ANKA", StringComparison.OrdinalIgnoreCase))
                            source = "ANKA";
                        else if (folderPath.Contains("HAFTALIK", StringComparison.OrdinalIgnoreCase))
                            source = "HAFTALIK";
                        else
                            source = "UNKNOWN";
                        
                        OnLog?.Invoke($"📢 Sinyal işleniyor: {source} - {content.Length} karakter");
                        OnSignalDetected?.Invoke(content, source);
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"❌ Dosya okuma hatası: {ex.Message}");
                }
            });
        }
    }
}

