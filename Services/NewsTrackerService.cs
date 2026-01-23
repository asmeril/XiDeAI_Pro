using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro.Services
{
    public class NewsItem
    {
        public string Title { get; set; } = "";
        public string Link { get; set; } = "";
        public string Source { get; set; } = "";
        public DateTime PubDate { get; set; }
    }

    public class NewsTrackerService
    {
        // Optional logger from UI
        public Action<string, string>? LogAction { get; set; }

        private readonly List<string> _rssFeeds = new List<string>
        {
            "https://www.bloomberght.com/rss",
            "https://www.foreks.com/rss",
            "https://www.aa.com.tr/rss/ekonomi",
            "https://www.dunya.com/rss",
            "https://tr.investing.com/rss/news.rss",
            "https://www.hurriyet.com.tr/rss/ekonomi",
            "https://www.cnbc.com/id/10000664/device/rss/rss.html" // CNBC Finance
        };

        private readonly HashSet<string> _seenLinks = new HashSet<string>();
        private readonly HashSet<string> _seenTitles = new HashSet<string>(); // v3.5.2: String similarity fallback
        private readonly HttpClient _client;

        private readonly GeminiService _gemini = null!;
        private readonly SocialIntelService _socialIntel = null!;

        public event Action<NewsItem>? OnNewsDetected;
        
        private System.Timers.Timer? _timer;

        public void Start()
        {
            if (_timer != null) return;

            int intervalMinutes = ConfigManager.Current.NewsCheckInterval;
            if (intervalMinutes < 1) intervalMinutes = 5;

            _timer = new System.Timers.Timer(intervalMinutes * 60 * 1000);
            _timer.Elapsed += async (s, e) => await CheckFeeds();
            _timer.AutoReset = true;
            _timer.Start();

            // Run immediately once
            _ = Task.Run(async () => await CheckFeeds());
            
            LogAction?.Invoke($"📰 Haber takibi aktif. (Aralık: {intervalMinutes}dk)", "News");
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
            LogAction?.Invoke("🛑 Haber takibi durduruldu.", "News");
        }
        
        public NewsTrackerService(GeminiService gemini, SocialIntelService socialIntel)
        {
            _gemini = gemini;
            _socialIntel = socialIntel;

            _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(90); // v3.6.5: Increased for AA RSS latency
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            
            // Determine path for history file
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = System.IO.Path.Combine(appData, "XiDeAI");
            System.IO.Directory.CreateDirectory(dir);
            _historyPath = System.IO.Path.Combine(dir, "news_seen_links.json");

            LoadHistory();
            // _ = MarkAllAsSeen(); // Removed: Wait for Start() to trigger the first check properly
        }

        private readonly string _historyPath;

        private void LoadHistory()
        {
            try
            {
                if (System.IO.File.Exists(_historyPath))
                {
                    string json = System.IO.File.ReadAllText(_historyPath);
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null)
                    {
                        foreach(var l in list) _seenLinks.Add(l);
                    }
                }
            }
            catch { }
        }

        private void SaveHistory()
        {
            try
            {
                var list = _seenLinks.ToList();
                string json = System.Text.Json.JsonSerializer.Serialize(list);
                System.IO.File.WriteAllText(_historyPath, json);
            }
            catch { }
        }

        private async Task MarkAllAsSeen()
        {
            try
            {
                var news = await FetchAllNews();
                foreach (var n in news) _seenLinks.Add(n.Link);
            }
            catch { }
        }

        public async Task CheckFeeds()
        {
            LogAction?.Invoke("🔍 Haberler taranıyor...", "News");
            var newsList = await FetchAllNews();
            // v3.8.2: Extended to 48h to capture weekend flow
            var threshold = DateTime.Now.AddHours(-48);

            // Sort by date (newest first)
            newsList = newsList.OrderByDescending(n => n.PubDate).ToList();

            foreach (var news in newsList)
            {
                // v3.6.4: Skip legacy news (Recency Filter)
                if (news.PubDate < threshold) continue;

                string cleanTitle = news.Title.Trim().ToLower();
                bool isNewLink = !_seenLinks.Contains(news.Link);
                bool isNewTitle = !_seenTitles.Contains(cleanTitle);

                if (isNewLink && isNewTitle)
                {
                    _seenLinks.Add(news.Link);
                    _seenTitles.Add(cleanTitle);
                    
                    // Keep cache size manageable
                    if (_seenLinks.Count > 1000) _seenLinks.Remove(_seenLinks.First());
                    if (_seenTitles.Count > 1000) _seenTitles.Remove(_seenTitles.First());
                        
                    SaveHistory(); // Persist on change

                    // Fire event directly - queue was never processed
                    OnNewsDetected?.Invoke(news);
                }
            }
        }

        private async Task<List<NewsItem>> FetchAllNews()
        {
            var allNews = new List<NewsItem>();

            foreach (var url in _rssFeeds)
            {
                try
                {
                    var items = await FetchFeed(url);
                    Console.WriteLine($"[RSS] {url}: {items.Count} items");
                    allNews.AddRange(items);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RSS Error ({url}): {ex.Message}");
                }
            }

            // 2. Selenium X News (Blocking call, run in parallel or careful)
            try 
            {
               LogAction?.Invoke("📡 X (Twitter) üzerinden son dakika haberleri çekiliyor...", "News");
               await FetchXNews(allNews); 
            }
            catch (Exception ex)
            {
                LogAction?.Invoke($"❌ X haber çekilirken kritik hata: {ex.Message}", "News");
            }

            return allNews;
        }

        private async Task FetchXNews(List<NewsItem> allNews)
        {
            var result = await _socialIntel.FetchBreakingNews();
            if (result == null)
            {
                LogAction?.Invoke("❌ X haber çekme sonucu null döndü", "News");
                return;
            }

            if (result.status == "error")
            {
                LogAction?.Invoke("❌ X haber hatası: Bilinmiyor", "News");
                return;
            }

            if (result.data == null || result.data.Length == 0)
            {
                LogAction?.Invoke("ℹ️ X haber bulunamadı (0 sonuç)", "News");
                return;
            }

            LogAction?.Invoke($"✅ X haber çekildi: {result.data.Length} sonuç", "News");

            if (result != null && result.status == "success" && result.data != null)
            {
                foreach (var item in result.data)
                {
                    // Basic duplicate check is done later by Link/Content
                    // v3.6.5: Use specific tweet URL for robust deduplication
                    string tweetUrl = string.IsNullOrWhiteSpace(item.url) ? $"https://X.com/{item.source}" : item.url;
                    
                    DateTime pubDate = DateTime.Now;
                    if (!string.IsNullOrEmpty(item.time) && DateTime.TryParse(item.time, out DateTime parsedDt))
                    {
                        pubDate = parsedDt;
                    }

                    allNews.Add(new NewsItem
                    {
                        Title = item.text.Length > 100 ? item.text.Substring(0, 97) + "..." : item.text,
                        Link = tweetUrl,
                        Source = $"X ({item.source})",
                        PubDate = pubDate 
                    });
                }
            }
        }

        private async Task<List<NewsItem>> FetchFeed(string url)
        {
            var items = new List<NewsItem>();
            try
            {
                var xmlString = await _client.GetStringAsync(url);
                var doc = XDocument.Parse(xmlString);

                var entries = doc.Descendants("item");
                foreach (var entry in entries)
                {
                    var title = entry.Element("title")?.Value;
                    var link = entry.Element("link")?.Value;
                    var pubDateStr = entry.Element("pubDate")?.Value;

                    if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(link))
                    {
                        DateTime pubDate = DateTime.Now;
                        if (DateTime.TryParse(pubDateStr, out DateTime dt)) pubDate = dt;

                        // Identify source from URL
                        string source = "RSS";
                        if (url.Contains("bloomberg")) source = "BloombergHT";
                        else if (url.Contains("foreks")) source = "Foreks";
                        else if (url.Contains("aa.com")) source = "AA";
                        else if (url.Contains("dunya")) source = "Dünya";
                        else if (url.Contains("investing")) source = "Investing";
                        else if (url.Contains("cnbc")) source = "CNBC";

                        items.Add(new NewsItem
                        {
                            Title = title.Trim(),
                            Link = link.Trim(),
                            Source = source,
                            PubDate = pubDate
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LogAction?.Invoke($"⚠️ RSS Beslemesi Hatası ({url}): {ex.Message}", "News");
            }
            return items;
        }
    }
}


