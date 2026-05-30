using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace XiDeAI_Pro.Services
{
    public class NewsHistoryItem
    {
        public string Id { get; set; } = ""; // Hash of Title + Source
        public string Title { get; set; } = "";
        public string Source { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime ProcessedAt { get; set; }
        public int ImportanceScore { get; set; }
        public bool WasTweeted { get; set; }
        public string Status { get; set; } = "PUBLISHED"; // PUBLISHED, PENDING, REJECTED
        public string Details { get; set; } = ""; // AI Summary or Reasoning
    }

    public class NewsPersistenceService
    {
        private readonly string _historyFilePath;
        private List<NewsHistoryItem> _history = new List<NewsHistoryItem>();
        private readonly object _lock = new object();
        private const int SIMILARITY_THRESHOLD = 60; // v4.6.11: %80'den %60'a düşürüldü (Farklı kaynakların benzer/eş anlamlı başlıklarını yakalamak için)
        private const int MAX_HISTORY_DAYS = 7; // 7 günlük geçmiş tut

        public NewsPersistenceService()
        {
            // Dosya yolu: AppData/XiDeAI/news_history.json
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appData, "XiDeAI");
            if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
            
            _historyFilePath = Path.Combine(appFolder, "news_history.json");
            LoadHistory();
        }

        private void LoadHistory()
        {
            lock (_lock)
            {
                if (File.Exists(_historyFilePath))
                {
                    try
                    {
                        string json = File.ReadAllText(_historyFilePath);
                        _history = JsonConvert.DeserializeObject<List<NewsHistoryItem>>(json) ?? new List<NewsHistoryItem>();
                        
                        // Eski kayıtları temizle (7 günden eski)
                        _history.RemoveAll(x => x.ProcessedAt < DateTime.Now.AddDays(-MAX_HISTORY_DAYS));
                    }
                    catch
                    {
                        _history = new List<NewsHistoryItem>();
                    }
                }
                else
                {
                    _history = new List<NewsHistoryItem>();
                }
            }
        }

        private void SaveHistory()
        {
            lock (_lock)
            {
                try
                {
                    // Eski kayıtları temizle (kaydederken de)
                    _history.RemoveAll(x => x.ProcessedAt < DateTime.Now.AddDays(-MAX_HISTORY_DAYS));
                    
                    string json = JsonConvert.SerializeObject(_history, Formatting.Indented);
                    File.WriteAllText(_historyFilePath, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving news history: {ex.Message}");
                }
            }
        }

        public List<NewsHistoryItem> GetRecentImportantNews(int count)
        {
            lock (_lock)
            {
                return _history
                    .Where(x => x.ImportanceScore >= 7) // Score 7+ (Önemli haberler — gerçek aralık 7-10)
                    .OrderByDescending(x => x.ProcessedAt)
                    .Take(count)
                    .ToList();
            }
        }

        public bool IsDuplicate(string title, string source, out string reason)
        {
            reason = string.Empty;
            string cleanTitle = NormalizeTitle(title);
            
            lock (_lock)
            {
                // Performans: Levenshtein O(m×n) için sadece son 24 saati tara (7 gün değil)
                var recent = _history.Where(x => x.ProcessedAt >= DateTime.Now.AddHours(-24)).ToList();
                foreach (var item in recent)
                {
                    string storedTitle = NormalizeTitle(item.Title);
                    int distance = LevenshteinDistance(cleanTitle.ToLower(), storedTitle.ToLower());
                    int maxLength = Math.Max(cleanTitle.Length, storedTitle.Length);
                    if (maxLength == 0) continue;

                    double similarity = 1.0 - ((double)distance / maxLength);

                    if (similarity * 100 >= SIMILARITY_THRESHOLD)
                    {
                        reason = $"Benzer haber bulundu: '{item.Title}' (%{similarity*100:F1} benzerlik)";
                        return true;
                    }
                }
            }

            return false;
        }

        public void AddParsedNews(string title, string source, string url, int score, bool tweeted, string status = "PUBLISHED")
        {
            lock (_lock)
            {
                var item = new NewsHistoryItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = NormalizeTitle(title), // v4.6.14: Normalize before saving (strips \n, extra spaces)
                    Source = source,
                    Url = url,
                    ProcessedAt = DateTime.Now,
                    ImportanceScore = score,
                    WasTweeted = tweeted,
                    Status = status
                };
                
                _history.Add(item);
                SaveHistory();
            }
        }

        /// <summary>Normalize title: collapse whitespace/newlines to single space</summary>
        private static string NormalizeTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return string.Empty;
            // Replace any sequence of whitespace (including \r\n, \n, \t) with a single space
            return System.Text.RegularExpressions.Regex
                .Replace(title.Trim(), @"[\r\n\t]+|\s{2,}", " ");
        }

        // Levenshtein Distance Algoritması
        private int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }
    }
}

