using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace XiDeAI_Pro.Services
{
    public class GuruPersistenceService
    {
        private readonly string _filePath;
        private readonly string _historyFilePath;
        private HashSet<string> _processedUrls;
        private List<GuruHistoryEntry> _history;

        public GuruPersistenceService()
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XiDeAI");
            Directory.CreateDirectory(appData);
            _filePath = Path.Combine(appData, "processed_guru_tweets.json");
            _historyFilePath = Path.Combine(appData, "guru_history.json");
            _processedUrls = LoadUrls();
            _history = LoadHistory();
        }

        private HashSet<string> LoadUrls()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    return JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GuruPersistenceService Load Error: {ex.Message}");
            }
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public void SaveUrls()
        {
            try
            {
                string json = JsonSerializer.Serialize(_processedUrls, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GuruPersistenceService Save Error: {ex.Message}");
            }
        }

        public bool IsProcessed(string url)
        {
            return _processedUrls.Contains(url);
        }

        public void MarkAsProcessed(string url)
        {
            if (_processedUrls.Add(url))
            {
                SaveUrls();
            }
        }

        private List<GuruHistoryEntry> LoadHistory()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    string json = File.ReadAllText(_historyFilePath);
                    return JsonSerializer.Deserialize<List<GuruHistoryEntry>>(json) ?? new List<GuruHistoryEntry>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GuruPersistenceService History Load Error: {ex.Message}");
            }
            return new List<GuruHistoryEntry>();
        }

        private void SaveHistory()
        {
            try
            {
                _history = _history
                    .Where(x => x.Timestamp >= DateTime.Now.AddDays(-30))
                    .OrderByDescending(x => x.Timestamp)
                    .Take(250)
                    .ToList();
                string json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_historyFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GuruPersistenceService History Save Error: {ex.Message}");
            }
        }

        public void RecordHistory(string symbol, string status, string sourceUrl, string content)
        {
            _history.Add(new GuruHistoryEntry
            {
                Timestamp = DateTime.Now,
                Symbol = symbol ?? string.Empty,
                Status = status ?? string.Empty,
                SourceUrl = sourceUrl ?? string.Empty,
                ContentPreview = string.IsNullOrWhiteSpace(content) ? string.Empty : (content.Length > 500 ? content.Substring(0, 500) : content)
            });
            SaveHistory();
        }

        public List<GuruHistoryEntry> GetRecentHistory(int count = 50)
        {
            return _history
                .OrderByDescending(x => x.Timestamp)
                .Take(count)
                .ToList();
        }
    }

    public class GuruHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string ContentPreview { get; set; } = string.Empty;
    }
}
