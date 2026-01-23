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
        private const int SIMILARITY_THRESHOLD = 80; // %80 benzerlik
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
                    .Where(x => x.ImportanceScore >= 4) // Score 4+ (Important)
                    .OrderByDescending(x => x.ProcessedAt)
                    .Take(count)
                    .ToList();
            }
        }

        public bool IsDuplicate(string title, string source, out string reason)
        {
            reason = string.Empty;
            string cleanTitle = title.Trim();
            
            lock (_lock)
            {
                // 1. Exact Match (URL veya ID)
                // (Burada URL kontrolü de yapılabilir ama şimdilik başlık odaklı gidiyoruz)

                // 2. Fuzzy Match (Levenshtein)
                // Son 24 saatteki haberlere bakmak performans açısından daha iyi olabilir ama
                // şimdilik tüm history (max 7 gün) taranıyor.
                foreach (var item in _history)
                {
                    // Kaynak farklı olsa bile çok benzer başlık risklidir (Copy-paste habercilik)
                    int distance = LevenshteinDistance(cleanTitle.ToLower(), item.Title.ToLower());
                    int maxLength = Math.Max(cleanTitle.Length, item.Title.Length);
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

        public void AddParsedNews(string title, string source, string url, int score, bool tweeted)
        {
            lock (_lock)
            {
                var item = new NewsHistoryItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = title.Trim(),
                    Source = source,
                    Url = url,
                    ProcessedAt = DateTime.Now,
                    ImportanceScore = score,
                    WasTweeted = tweeted
                };
                
                _history.Add(item);
                SaveHistory();
            }
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

