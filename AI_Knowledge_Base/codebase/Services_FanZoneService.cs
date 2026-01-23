using System.IO;
using System.Text.Json;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro.Services
{
    public class FanZoneTweet
    {
        public string Handle { get; set; } = "";
        public string Text { get; set; } = "";
        public string Link { get; set; } = "";
        public DateTime DetectedAt { get; set; }
        public string Source { get; set; } = ""; // "Resmi", "Muhabir", "Fan"
        public string AIReaction { get; set; } = ""; // Fanatik tepki
    }

    public class FanZoneService
    {
        private readonly SocialIntelService _socialIntel;
        private readonly GeminiService _gemini;
        private System.Timers.Timer? _timer;
        private readonly HashSet<string> _seenIds = new HashSet<string>();
        private readonly string _seenIdsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "XiDeAI", 
            "FanZone_SeenIds.json"
        );
        private readonly System.Threading.SemaphoreSlim _checkLock = new System.Threading.SemaphoreSlim(1, 1);
        
        // Events
        public event Action<FanZoneTweet>? OnNewFanContent;
        public Action<string>? LogAction { get; set; }

        public FanZoneService(SocialIntelService socialIntel, GeminiService gemini)
        {
            _socialIntel = socialIntel;
            _gemini = gemini;
            LoadSeenLinks();
        }

        private void LoadSeenLinks()
        {
            try
            {
                if (File.Exists(_seenIdsFile))
                {
                    string json = File.ReadAllText(_seenIdsFile);
                    var ids = JsonSerializer.Deserialize<List<string>>(json);
                    if (ids != null)
                    {
                        foreach (var id in ids) _seenIds.Add(id);
                    }
                }
            }
            catch (Exception ex)
            {
                LogAction?.Invoke($"⚠️ FanZone Geçmişi Yüklenemedi: {ex.Message}");
            }
        }

        private void SaveSeenLinks()
        {
            try
            {
                lock (_seenIds)
                {
                    string json = JsonSerializer.Serialize(_seenIds.ToList());
                    File.WriteAllText(_seenIdsFile, json);
                }
            }
            catch { }
        }

        private string ExtractTweetId(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            // Örn: https://x.com/user/status/123456789... veya https://twitter.com/user/status/123456789
            var match = System.Text.RegularExpressions.Regex.Match(url, @"status/(\d+)");
            return match.Success ? match.Groups[1].Value : url;
        }

        private bool AddToSeen(string linkOrId)
        {
            if (string.IsNullOrEmpty(linkOrId)) return false;
            string id = linkOrId.Contains("status/") ? ExtractTweetId(linkOrId) : linkOrId;
            
            lock (_seenIds)
            {
                if (_seenIds.Contains(id)) return false;
                _seenIds.Add(id);
                SaveSeenLinks();
                return true;
            }
        }

        public void Start()
        {
            if (!ConfigManager.Current.FanZoneEnabled)
            {
                LogAction?.Invoke("⚠️ FanZone (Fenerbahçe Modu) kapalı.");
                return;
            }

            if (_timer != null) return;

            // Her 3 dakikada bir kontrol (Agresif takip)
            _timer = new System.Timers.Timer(3 * 60 * 1000);
            _timer.Elapsed += async (s, e) => await CheckUpdates();
            _timer.AutoReset = true;
            _timer.Start();

            LogAction?.Invoke("💙💛 Fenerbahçe Fan Zone Aktif! Saldır Fener!");
            
            // İlk açılışta hemen bir tur at
            _ = Task.Run(async () => await CheckUpdates());
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
            LogAction?.Invoke("FanZone durduruldu.");
        }

        public async Task CheckUpdates()
        {
            if (!await _checkLock.WaitAsync(0)) return;

            try
            {
                var accounts = ConfigManager.Current.FenerbahceAccounts;
                if (accounts == null || accounts.Count == 0) return;

                // 1. KRİTİK HESAP TARAMASI (Hızlı Tur - Son Tweetler)
                foreach (var account in accounts.Take(10)) 
                {
                    string handle = account.TrimStart('@');
                    var result = await _socialIntel.FindInfluencerTweet(handle);

                    if (result != null && result.status == "success" && !string.IsNullOrEmpty(result.link))
                    {
                        if (AddToSeen(result.link))
                        {
                            await ProcessNewTweetDirectly(result, account);
                        }
                    }
                    await Task.Delay(2000);
                }

                // 2. GENİŞ TARAMA (Gündem ve Diğerleri) - Her 10 dakikada bir
                if (DateTime.Now.Minute % 10 == 0) 
                {
                    // Hashtag Taraması
                    LogAction?.Invoke("🔍 Gündem Taraması: #Fenerbahçe #FB");
                    var keywords = ConfigManager.Current.FenerbahceKeywords ?? new List<string> { "#Fenerbahçe" };
                    foreach (var keyword in keywords.Take(2))
                    {
                        var posts = await _socialIntel.FindInfluencerAnalyses(keyword, "BIST");
                        if (posts != null)
                        {
                            foreach (var post in posts.OrderByDescending(p => p.Engagement).Take(2))
                            {
                                await ProcessTweet(post, post.Handle, "Gündem");
                            }
                        }
                        await Task.Delay(3000);
                    }

                    // Sporcu Taraması
                    if (ConfigManager.Current.FenerbahceAthletes.Count == 0) InitializeDefaultAthletes();
                    foreach (var athlete in ConfigManager.Current.FenerbahceAthletes)
                    {
                        if ((DateTime.Now - athlete.LastInteraction).TotalMinutes < 45) continue;

                        var tweets = await _socialIntel.FindInfluencerAnalyses("", "Fenerbahce", new List<string> { athlete.Handle });
                        foreach (var tweet in tweets)
                        {
                            await ProcessTweet(tweet, athlete.Handle, $"Sporcu ({athlete.Sport})");
                        }
                        
                        athlete.LastInteraction = DateTime.Now;
                        ConfigManager.Save();
                        await Task.Delay(2000);
                    }
                }
            }
            catch (Exception ex)
            {
                LogAction?.Invoke($"FanZone Hata: {ex.Message}");
            }
            finally
            {
                _checkLock.Release();
            }
        }

        private async Task ProcessNewTweetDirectly(dynamic result, string account)
        {
            LogAction?.Invoke($"🔥 YENİ TWEET: {account} - {result.link}");
            
            // Beğeni
            _ = Task.Run(async () => await _socialIntel.LikeTweet(result.link));

            // RT (Resmi/Muhabir)
            string sourceType = DetermineSourceType(account);
            if (sourceType == "Resmi" || sourceType == "Muhabir")
            {
                _ = Task.Run(async () => await _socialIntel.Retweet(result.link));
            }

            // Yanıt
            string reaction = await GenerateFanaticReaction(result.text, account);
            if (!string.IsNullOrEmpty(reaction))
            {
                var repRes = await _socialIntel.ReplyToTweetAsync(result.link, reaction);
                LogAction?.Invoke(repRes.status == "success" ? "✅ Yanıt gönderildi." : $"❌ Yanıt Hatası: {repRes.text}");
            }

            OnNewFanContent?.Invoke(new FanZoneTweet {
                Handle = account,
                Text = result.text,
                Link = result.link,
                DetectedAt = DateTime.Now,
                Source = sourceType,
                AIReaction = reaction
            });
        }

        private void InitializeDefaultAthletes()
        {
            var defaults = new List<FenerbahceAthlete>
            {
                new FenerbahceAthlete { Name = "Edin Dzeko", Handle = "@edindzeko", Sport = "Futbol" },
                new FenerbahceAthlete { Name = "Dusan Tadic", Handle = "@DT10_Official", Sport = "Futbol" },
                new FenerbahceAthlete { Name = "Melih Mahmutoğlu", Handle = "@melihmahmutoglu", Sport = "Basketbol" },
                new FenerbahceAthlete { Name = "Eda Erdem", Handle = "@edaerdem14", Sport = "Voleybol" }
            };
            ConfigManager.Current.FenerbahceAthletes.AddRange(defaults);
            ConfigManager.Save();
        }

        private async Task ProcessTweet(InfluencerPost tweet, string account, string sourceType)
        {
            // Dedup check
            if (!AddToSeen(tweet.Url)) return;

            // v3.3.4: Age check - Ignore if older than 6 hours
            if (tweet.PostDate != DateTime.MinValue && (DateTime.Now - tweet.PostDate).TotalHours > 6)
            {
                LogAction?.Invoke($"ℹ️ Eski tweet atlanıyor ({tweet.PostDate:HH:mm}): {account}");
                return;
            }

            LogAction?.Invoke($"🔥 YENİ İÇERİK: {account} ({sourceType}) - {tweet.Url}");

            // AI Tepkisi
            string reaction = await GenerateFanaticReaction(tweet.Content, account);
            
            // v3.3.1 FIX: Resmi ve Sporcu hesaplarına anında yanıt ver (Eşik yok)
            // Fanlar için eşik 30, diğerleri için 0
            int threshold = (sourceType == "Fan" || sourceType == "Gündem") ? 30 : 0;

            if (!string.IsNullOrEmpty(reaction))
            {
                if (tweet.Engagement >= threshold)
                {
                    var repRes = await _socialIntel.ReplyToTweetAsync(tweet.Url, reaction);
                    LogAction?.Invoke(repRes.status == "success" ? $"✅ [{sourceType}] Yanıt gönderildi ({tweet.Handle})" : $"❌ [{sourceType}] Yanıt Hatası: {repRes.text}");
                }
                else
                {
                    LogAction?.Invoke($"⚠️ [{sourceType}] Yanıt atlandı. Etkileşim düşük ({tweet.Engagement} < {threshold}) - {tweet.Url}");
                }
            }

            // ... UI Update Logic (Call event)
            var fanTweet = new FanZoneTweet
            {
                Handle = !string.IsNullOrEmpty(tweet.Handle) ? tweet.Handle : account,
                Text = tweet.Content,
                Link = tweet.Url,
                DetectedAt = DateTime.Now, // Use current time for detection, not post time
                AIReaction = reaction,
                Source = sourceType // Updated source type
            };
            OnNewFanContent?.Invoke(fanTweet);
        }

        private string DetermineSourceType(string handle)
        {
            if (handle.Contains("Fenerbahce") || handle.Contains("FBBasketbol")) return "Resmi";
            if (handle.Contains("yago") || handle.Contains("sercan") || handle.Contains("konanc")) return "Muhabir";
            return "Fan";
        }

        private async Task<string> GenerateFanaticReaction(string tweetText, string account)
        {
            try
            {
                string prompt = $@"
KİMLİK: Sen XiDeAI Pro'nun Fenerbahçe odaklı uzman analistisin. Tecrübeli, saygılı ve birleştirici bir karakterin var.

ÜSLUP:
- Robotik olma; bir camia ferdi gibi doğal ve sıcak bir dil kullan. 
- Kendini tanıtma (isim, yaş vb. söyleme), doğrudan tweet içeriğine odaklan.
- Rakiplere cevap verirken küfürden ve saygısızlıktan kaçın; zekanla ve nezaketinle fark yarat.
- Fenerbahçeli dostlarınla konuşurken samimi bir ""biz"" dili kullan.

GÖREV: Aşağıdaki tweet'e bu karakterle, camianın hakkını savunan ama kimseyi kırmayan zeki bir yanıt ver.

TWEET (Yazan: {account}):
""{tweetText}""

GENEL KURALLAR:
- Maksimum 2 cümle. 
- Doğal insan yazımı gibi (küçük harf kullanımı, ünlemler vb.) serbesttir.
- Takipçilerini asla üzme, her zaman nazik ve birleştirici ol.
- KESİNLİKLE kendi kimliğini açıklama.

CEVAP (Sadece yanıt metni):";
                // GeminiService üzerinden text üretimi (GenerateText metodu varsayılıyor)
                // Not: GeminiService.GenerateText implementation'ına göre burası değişebilir.
                // Şimdilik varsayılan Chat/Prompt metodunu kullanacağız.
                
                var response = await _gemini.GenerateGenericContent(prompt, XiDeAI_Pro.Services.AI.TaskType.FanZoneReaction);
                return response?.Trim() ?? "Saldır Fener!";
            }
            catch
            {
                return "💙💛 Saldır Fener!";
            }
        }
    }
}
