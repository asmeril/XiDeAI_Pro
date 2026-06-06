using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace XiDeAI_Pro.Services
{
    /// <summary>
    /// PURPOSE: Persist signal deduplication memory across application restarts.
    /// Prevents re-analyzing old signals from iDeal log files after a restart.
    /// </summary>
    public class SignalPersistenceService
    {
        private readonly string _filePath;
        private HashSet<string> _processedKeys;
        private readonly object _lock = new object();

        public SignalPersistenceService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appData, "XiDeAI");
            if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
            
            _filePath = Path.Combine(appFolder, "sig_memory.json");
            _processedKeys = LoadMemory();
        }

        private HashSet<string> LoadMemory()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    var data = JsonSerializer.Deserialize<HashSet<string>>(json);
                    return data ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ SignalPersistenceService Load Error: {ex.Message}");
            }
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public void SaveMemory()
        {
            lock (_lock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(_processedKeys, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_filePath, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ SignalPersistenceService Save Error: {ex.Message}");
                }
            }
        }

        public bool IsProcessed(string symbol, string period)
        {
            string key = $"{symbol}|{period}".ToUpperInvariant();
            lock (_lock)
            {
                return _processedKeys.Contains(key);
            }
        }

        public bool IsProcessed(SignalData signal)
        {
            string key = BuildSignalKey(signal);
            lock (_lock)
            {
                return _processedKeys.Contains(key);
            }
        }

        public void MarkAsProcessed(string symbol, string period)
        {
            string key = $"{symbol}|{period}".ToUpperInvariant();
            MarkKeyAsProcessed(key);
        }

        public void MarkAsProcessed(SignalData signal)
        {
            MarkKeyAsProcessed(BuildSignalKey(signal));
        }

        private void MarkKeyAsProcessed(string key)
        {
            bool changed = false;
            lock (_lock)
            {
                if (_processedKeys.Add(key))
                {
                    changed = true;
                }
            }
            
            if (changed) SaveMemory();
        }

        private static string BuildSignalKey(SignalData signal)
        {
            string dateKey = signal.DetectedAt == DateTime.MinValue ? "NO_DATE" : signal.DetectedAt.ToString("yyyyMMddHHmmss");
            return $"{signal.Symbol}|{signal.Strategy}|{signal.Period}|{signal.Durum}|{dateKey}".ToUpperInvariant();
        }

        public void Clear()
        {
            lock (_lock)
            {
                _processedKeys.Clear();
                SaveMemory();
            }
        }
    }
}
