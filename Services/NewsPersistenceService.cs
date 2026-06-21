using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
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
        private const int SIMILARITY_THRESHOLD = 50; // v5.6.2: %60'dan %50'ye düşürüldü (Farklı kaynakların aynı haberlerini daha agresif yakalamak için)
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
                    .Where(x => x.ImportanceScore >= 9 || x.Status == "PUBLISHED") // v5.3.1: yalnız yüksek önem veya yayınlanmış haberler
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

                    double tokenSimilarity = TokenSimilarity(cleanTitle, storedTitle);
                    if (tokenSimilarity >= 0.50) // v5.6.2: %72'den %50'ye düşürüldü
                    {
                        reason = $"Benzer haber bulundu: '{item.Title}' (token %{tokenSimilarity * 100:F1} benzerlik)";
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsKnownUrl(string url, out string reason)
        {
            reason = string.Empty;
            string cleanUrl = NormalizeUrl(url);
            if (string.IsNullOrWhiteSpace(cleanUrl)) return false;

            lock (_lock)
            {
                var existing = _history
                    .Where(x => x.ProcessedAt >= DateTime.Now.AddDays(-MAX_HISTORY_DAYS))
                    .FirstOrDefault(x => NormalizeUrl(x.Url).Equals(cleanUrl, StringComparison.OrdinalIgnoreCase));
                if (existing == null) return false;
                reason = $"Aynı URL daha önce işlendi: {existing.Status}";
                return true;
            }
        }

        public void AddParsedNews(string title, string source, string url, int score, bool tweeted, string status = "PUBLISHED")
        {
            lock (_lock)
            {
                string cleanUrl = NormalizeUrl(url);
                var existing = !string.IsNullOrWhiteSpace(cleanUrl)
                    ? _history.FirstOrDefault(x => NormalizeUrl(x.Url).Equals(cleanUrl, StringComparison.OrdinalIgnoreCase))
                    : null;

                if (existing != null)
                {
                    existing.Title = NormalizeTitle(title);
                    existing.Source = source;
                    existing.Url = url;
                    existing.ProcessedAt = DateTime.Now;
                    existing.ImportanceScore = Math.Max(existing.ImportanceScore, score);
                    existing.WasTweeted = existing.WasTweeted || tweeted;
                    existing.Status = tweeted ? "PUBLISHED" : status;
                    SaveHistory();
                    return;
                }

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
            string normalized = RemoveDiacritics(title.Trim().ToLowerInvariant());
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"https?://\S+", " ");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+-\s+.*$", "");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[\r\n\t]+|\s{2,}", " ");
            return normalized.Trim();
        }

        private static string RemoveDiacritics(string text)
        {
            string formD = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char ch in formD)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC)
                .Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u')
                .Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c');
        }

        private static double TokenSimilarity(string a, string b)
        {
            var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ve", "ile", "icin", "için", "bir", "son", "dakika", "haber", "haberi",
                "the", "and", "of", "to", "in", "on"
            };
            var A = System.Text.RegularExpressions.Regex.Split(a, @"\W+")
                .Where(t => t.Length >= 3 && !stop.Contains(t))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var B = System.Text.RegularExpressions.Regex.Split(b, @"\W+")
                .Where(t => t.Length >= 3 && !stop.Contains(t))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (A.Count == 0 || B.Count == 0) return 0;
            int intersection = A.Intersect(B, StringComparer.OrdinalIgnoreCase).Count();
            int union = A.Union(B, StringComparer.OrdinalIgnoreCase).Count();
            return union == 0 ? 0 : (double)intersection / union;
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            string clean = url.Trim();
            int hashIdx = clean.IndexOf('#');
            if (hashIdx >= 0) clean = clean.Substring(0, hashIdx);
            int qIdx = clean.IndexOf('?');
            if (qIdx >= 0) clean = clean.Substring(0, qIdx);
            return clean.TrimEnd('/');
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
