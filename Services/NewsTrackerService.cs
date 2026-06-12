using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
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
        public string? ImageUrl { get; set; } // v4.6.18: Added for Visual Hooks strategy
        public string? Description { get; set; } // v4.7.3: Haber özeti/gövdesi (RSS body)
        public bool IsFlash { get; set; } = false; // v4.7.3: Flaş haber bayrağı ("son dakika", "breaking" vb.)
        public string Category { get; set; } = "EKONOMI";
        public bool IncludesAnalysis { get; set; } = true;
        public DateTime PubDate { get; set; }
    }

    public class NewsTrackerService
    {
        // Optional logger from UI
        public Action<string, string>? LogAction { get; set; }

        private readonly List<string> _rssFeeds = new List<string>
        {
            // YERLİ RESMİ & KALİTELİ KAYNAKLAR
            "https://www.bloomberght.com/rss", // Bloomberg HT
            "https://www.aa.com.tr/rss", // AA Ana RSS
            "https://www.aa.com.tr/rss/ekonomi", // AA Ekonomi
            "https://www.trthaber.com/rss", // TRT Haber
            "https://www.dunya.com/rss", // Dünya Gazetesi (Ekonomi)
            "https://tr.investing.com/rss/news.rss", // Investing TR
            "https://news.google.com/rss/search?q=%22Resmi+Gazete%22&hl=tr&gl=TR&ceid=TR:tr", // Resmi Gazete

            // YABANCI & GLOBAL KAYNAKLAR (Batı)
            "https://content.rssfeed.cnbc.com/rss.cnbc.com/WWL-land-main.xml", // CNBC World
            "http://feeds.bbci.co.uk/news/world/rss.xml", // BBC World News
            "https://feeds.a.dj.com/rss/RSSWorldNews.xml", // Wall Street Journal World News

            // ASYA & DOĞU BLOKU RESMİ AJANSLARI (Jeopolitik Makro)
            "https://tass.com/rss/v2.xml", // TASS (Rusya Resmi Devlet Ajansı)
            "http://www.xinhuanet.com/english/rss/worldrss.xml", // Xinhua (Çin Resmi Devlet Ajansı)
            "https://english.kyodonews.net/news/feed" // Kyodo News (Japonya)
        };

        private readonly HashSet<string> _seenLinks = new HashSet<string>();
        private readonly HashSet<string> _seenTitles = new HashSet<string>(); // v3.5.2: String similarity fallback
        private readonly HttpClient _client;

        private readonly GeminiService _gemini = null!;
        private readonly SocialIntelService _socialIntel = null!;

        public event Action<NewsItem>? OnNewsDetected;
        
        private System.Timers.Timer? _timer;
        private int _isChecking = 0; // Eşzamanlı CheckFeeds çağrısını önler

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
            _historyTitlesPath = System.IO.Path.Combine(dir, "news_seen_titles.json"); // v4.7.2

            LoadHistory();
            // _ = MarkAllAsSeen(); // Removed: Wait for Start() to trigger the first check properly
        }

        private readonly string _historyPath;
        private readonly string _historyTitlesPath; // v4.7.2: Başlıklar da kalıcı olarak kaydediliyor

        private void LoadHistory()
        {
            try
            {
                if (System.IO.File.Exists(_historyPath))
                {
                    string json = System.IO.File.ReadAllText(_historyPath);
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null)
                        foreach(var l in list) _seenLinks.Add(l);
                }
                // v4.7.2: Başlıkları da diskten yükluyoruz (yeniden başlatmada sıfırlanmıyor)
                if (System.IO.File.Exists(_historyTitlesPath))
                {
                    string json = System.IO.File.ReadAllText(_historyTitlesPath);
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null)
                        foreach(var t in list) _seenTitles.Add(t);
                }
            }
            catch { }
        }

        private void SaveHistory()
        {
            try
            {
                string jsonLinks = System.Text.Json.JsonSerializer.Serialize(_seenLinks.ToList());
                System.IO.File.WriteAllText(_historyPath, jsonLinks);

                // v4.7.2: Başlıkları da diske yaz
                string jsonTitles = System.Text.Json.JsonSerializer.Serialize(_seenTitles.ToList());
                System.IO.File.WriteAllText(_historyTitlesPath, jsonTitles);
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
            // Eşzamanlı çağrı koruması: Timer overlap durumunda ikinci çağrı atlanır
            if (Interlocked.Exchange(ref _isChecking, 1) == 1) { LogAction?.Invoke("⏭️ Haber taraması zaten devam ediyor, atlandı.", "News"); return; }
            try
            {
                LogAction?.Invoke("🔍 Haberler taranıyor...", "News");
                var newsList = await FetchAllNews();
                int freshCount = 0, duplicateCount = 0, oldCount = 0;
                // v3.8.2: Extended to 48h to capture weekend flow
                var threshold = DateTime.Now.AddHours(-48);

                // Sort by date (newest first)
                newsList = newsList.OrderByDescending(n => n.PubDate).ToList();

                foreach (var news in newsList)
                {
                    // v3.6.4: Skip legacy news (Recency Filter)
                    if (news.PubDate < threshold) { oldCount++; continue; }

                    string cleanTitle = NormalizeTitle(news.Title);
                    bool isNewLink = !_seenLinks.Contains(news.Link);
                    bool isNewTitle = !_seenTitles.Contains(cleanTitle);

                    // v4.7.2: Fuzzy (token-based) title deduplication
                    // Farklı kaynaklardan gelen aynı haberin farklı başlıklarını yakalar
                    bool isSimilarTitle = false;
                    if (isNewTitle)
                    {
                        isSimilarTitle = IsSimilarToSeenTitle(cleanTitle);
                        if (isSimilarTitle)
                        {
                            LogAction?.Invoke($"⚠️ Benzer haber atlandı (fuzzy match): {news.Title.Substring(0, Math.Min(60, news.Title.Length))}", "News");
                        }
                    }

                    if (isNewLink && isNewTitle && !isSimilarTitle)
                    {
                        _seenLinks.Add(news.Link);
                        _seenTitles.Add(cleanTitle);

                        // Limit aşılınca tüm geçmişi silme; son görülenlerden bir pencere tut.
                        if (_seenLinks.Count > 2000)
                        {
                            var keepLinks = _seenLinks.TakeLast(1500).ToList();
                            _seenLinks.Clear();
                            foreach (var link in keepLinks) _seenLinks.Add(link);
                        }
                        if (_seenTitles.Count > 2000)
                        {
                            var keepTitles = _seenTitles.TakeLast(1500).ToList();
                            _seenTitles.Clear();
                            foreach (var title in keepTitles) _seenTitles.Add(title);
                        }

                        SaveHistory(); // Persist on change

                        // Fire event directly - queue was never processed
                        freshCount++;
                        LogAction?.Invoke($"🆕 Yeni haber bulundu: [{news.Source}] {news.Title}", "News");
                        OnNewsDetected?.Invoke(news);
                    }
                    else duplicateCount++;
                }
                LogAction?.Invoke($"📰 Haber taraması tamamlandı. Toplam={newsList.Count}, Yeni={freshCount}, Eski={oldCount}, Mükerrer={duplicateCount}", "News");
            }
            catch (Exception ex)
            {
                LogAction?.Invoke($"❌ Haber taraması hatası: {ex.Message}", "News");
            }
            finally
            {
                Interlocked.Exchange(ref _isChecking, 0);
            }
        }

        /// <summary>
        /// Başlığı normalize eder: küçük harf, noktalama temizliği, fazla boşluklar kalkar.
        /// </summary>
        private string NormalizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "";
            // Küçük harf
            var s = title.Trim().ToLowerInvariant();
            // Noktalama işaretlerini temizle
            s = System.Text.RegularExpressions.Regex.Replace(s, @"[^\w\s]", " ");
            // Fazla boşlukları sil
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        /// <summary>
        /// v4.7.2: Token-tabanlı fuzzy deduplication.
        /// Gerçek anlamlı kelimelerin (stop-word olmayan) %70'i örtüşüyorsa aynı haber sayılır.
        /// </summary>
        private bool IsSimilarToSeenTitle(string normalizedTitle)
        {
            if (_seenTitles.Count == 0) return false;

            var stopWords = new HashSet<string> { "ve", "ile", "de", "da", "bir", "bu", "o", "en",
                "the", "a", "an", "of", "in", "on", "at", "to", "for", "is", "are", "was", "be" };

            var newTokens = normalizedTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                          .Where(t => t.Length > 2 && !stopWords.Contains(t))
                                          .ToHashSet();

            if (newTokens.Count < 3) return false; // Çok kısa başlıkları atlama — fuzzy yanlış pozitif riskli

            // Son 500 görülen başlık ile kıyasla (performans için sınırla)
            foreach (var seen in _seenTitles.TakeLast(500))
            {
                var seenTokens = seen.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                    .Where(t => t.Length > 2 && !stopWords.Contains(t))
                                    .ToHashSet();

                if (seenTokens.Count < 3) continue;

                int overlap = newTokens.Intersect(seenTokens).Count();
                int minLen = Math.Min(newTokens.Count, seenTokens.Count);

                // %70 token örtüşmesi = mükerrer
                if ((double)overlap / minLen >= 0.70)
                    return true;
            }

            return false;
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
                    LogAction?.Invoke($"⚠️ RSS Error ({url}): {ex.Message}", "News");
                }
            }

            // v4.6.22: Kullanıcı talebi üzerine X (Twitter) haber taraması gereksiz "gürültü" ve magazin/manipülasyon riski sebebiyle İPTAL EDİLMİŞTİR.
            // Sadece yukarıda belirlenen güvenilir ulusal/küresel RSS (Ajans) haberleri kullanılacaktır.

            return allNews;
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
                        DateTime pubDate = DateTime.MinValue;
                        if (DateTime.TryParse(pubDateStr, out DateTime dt)) pubDate = dt;

                        // Identify source from URL
                        string source = "RSS";
                        if (url.Contains("bloomberg")) source = "BloombergHT";
                        else if (url.Contains("aa.com")) source = "Anadolu Ajansı";
                        else if (url.Contains("dunya")) source = "Dünya Gazetesi";
                        else if (url.Contains("investing")) source = "Investing";
                        else if (url.Contains("cnbc")) source = "CNBC";
                        else if (url.Contains("trt")) source = "TRT Haber";
                        else if (url.Contains("bbc")) source = "BBC News";
                        else if (url.Contains("dj.com")) source = "WSJ";
                        else if (url.Contains("google") && url.Contains("Resmi")) source = "Resmi Gazete / Mevzuat";
                        else if (url.Contains("tass")) source = "TASS (Russia)";
                        else if (url.Contains("xinhua")) source = "Xinhua (China)";
                        else if (url.Contains("kyodo")) source = "Kyodo (Japan)";

                        // v4.6.18: Extract Image from RSS
                        string? imageUrl = null;
                        try {
                            XNamespace media = "http://search.yahoo.com/mrss/";
                            imageUrl = entry.Element(media + "content")?.Attribute("url")?.Value 
                                     ?? entry.Element("enclosure")?.Attribute("url")?.Value;
                        } catch { }

                    string rawDesc = entry.Element("description")?.Value ?? "";
                        // HTML tag'larini temizle
                        string cleanDesc = System.Text.RegularExpressions.Regex.Replace(rawDesc, "<.*?>", "").Trim();
                        if (cleanDesc.Length > 500) cleanDesc = cleanDesc.Substring(0, 497) + "...";

                        // Flaş Haber bayrağını belirle (genişletilmiş pattern — AA, Bloomberg, CNBC prefix'leri dahil)
                        string titleLower = title.ToLower();
                        bool isFlash = titleLower.Contains("son dakika") || titleLower.Contains("breaking") ||
                                       titleLower.Contains("⚠") || titleLower.Contains("acil:") ||
                                       titleLower.Contains("flaş:") || titleLower.Contains("urgent:") ||
                                       titleLower.Contains("alert:") || title.StartsWith("⚡") || title.StartsWith("🚨");

                        items.Add(new NewsItem
                        {
                            Title = title.Trim(),
                            Link = link.Trim(),
                            Source = source,
                            Description = string.IsNullOrWhiteSpace(cleanDesc) ? null : cleanDesc,
                            IsFlash = isFlash,
                            ImageUrl = imageUrl,
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
