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
        // Coalescing: Aynı dosya için art arda gelen değişiklikleri tek işlemde birleştir.
        private readonly TimeSpan _quietWindow = TimeSpan.FromSeconds(2);
        private ConcurrentDictionary<string, System.Threading.CancellationTokenSource> _pendingReads = new();
        private ConcurrentDictionary<string, string> _lastContentFingerprint = new();

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
            foreach (var kv in _pendingReads)
            {
                try { kv.Value.Cancel(); kv.Value.Dispose(); }
                catch { }
            }
            _pendingReads.Clear();
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

            // Event fırtınasını tek bir okumaya indir: son eventten _quietWindow sonra oku.
            if (_pendingReads.TryRemove(path, out var existingCts))
            {
                try { existingCts.Cancel(); existingCts.Dispose(); }
                catch { }
            }

            var cts = new System.Threading.CancellationTokenSource();
            _pendingReads[path] = cts;

            Task.Run(async () => {
                try
                {
                    await Task.Delay(_quietWindow, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (_pendingReads.TryGetValue(path, out var pendingCts) && !ReferenceEquals(pendingCts, cts))
                {
                    return;
                }

                var now = DateTime.Now;
                if (_lastProcessed.TryGetValue(path, out var lastTime) && now - lastTime < _debounceTime)
                {
                    return;
                }
                _lastProcessed[path] = now;

                try
                {
                    OnLog?.Invoke($"📄 Dosya değişikliği tespit edildi: {Path.GetFileName(path)}");

                    string content = "";
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs))
                    {
                        content = sr.ReadToEnd();
                    }

                    // Aynı içerik tekrar geldiyse gereksiz işlem yapma.
                    string fingerprint = $"{content.Length}:{content.GetHashCode()}";
                    if (_lastContentFingerprint.TryGetValue(path, out var oldFp) && oldFp == fingerprint)
                    {
                        return;
                    }
                    _lastContentFingerprint[path] = fingerprint;
                    
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
                finally
                {
                    if (_pendingReads.TryGetValue(path, out var activeCts) && ReferenceEquals(activeCts, cts))
                    {
                        _pendingReads.TryRemove(path, out _);
                    }
                    cts.Dispose();
                }
            });
        }
    }
}

