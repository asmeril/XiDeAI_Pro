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
        
        // v4.5.3: Interaction tracking
        public bool Liked { get; set; }
        public bool Retweeted { get; set; }
        public bool QuoteRt { get; set; }
        public bool Replied { get; set; }
        
        // Helper to get interaction icons
        public string InteractionIcons => 
            (Liked ? "❤️" : "") + 
            (Retweeted ? "🔄" : "") + 
            (QuoteRt ? "💬🔄" : "") + 
            (Replied ? "💬" : "");
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
                // v4.0.1 FIX: FindInfluencerAnalyses with vipHandles for timeline scanning
                foreach (var account in accounts.Take(10)) 
                {
                    string handle = account.TrimStart('@');
                    LogAction?.Invoke($"🔍 FanZone: @{handle} taranıyor...");
                    
                    // Use FindInfluencerAnalyses with vipHandles for proper timeline scan
                    var posts = await _socialIntel.FindInfluencerAnalyses(
                        "", // No symbol filter for FanZone
                        "Fenerbahce", 
                        new List<string> { handle }, 
                        limit: 3
                    );

                    if (posts != null && posts.Count > 0)
                    {
                        foreach (var post in posts)
                        {
                            if (!string.IsNullOrEmpty(post.Url))
                            {
                                await ProcessTweet(post, account, DetermineSourceType(account));
                            }
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

            // v4.5.3: Track interaction results
            bool liked = false, retweeted = false, replied = false, quoteRt = false;

            // v4.1.0: INTERACTION LOGIC (Like & RT)
            try 
            {
                // Herkesin tweetini beğen (Fan hariç, veya yüksek etkileşimli Fan)
                if (sourceType != "Fan")
                {
                    var likeRes = await _socialIntel.LikeTweet(tweet.Url);
                    liked = likeRes.status == "success";
                    LogAction?.Invoke(liked ? $"❤️ [{sourceType}] Like yapıldı ({tweet.Handle})" : $"❌ Like Hatası: {likeRes.ErrorMessage}");
                }

                // Resmi hesapları Retweetle
                if (sourceType == "Resmi")
                {
                    var rtRes = await _socialIntel.Retweet(tweet.Url);
                    retweeted = rtRes.status == "success";
                    LogAction?.Invoke(retweeted ? $"🔄 [{sourceType}] RT yapıldı ({tweet.Handle})" : $"❌ RT Hatası: {rtRes.ErrorMessage}");
                }

                // v4.5.3: Hybrid Quote RT (Sporcu veya yüksek etkileşimli içerikler)
                if (!retweeted && (sourceType == "Sporcu" || tweet.Engagement >= 500))
                {
                    bool hasImage = tweet.Content.Contains("pic.twitter") || 
                                   tweet.Content.Contains("pbs.twimg") || 
                                   tweet.Content.Contains("t.co");
                    var (shouldQuote, quoteText) = await ShouldQuoteRetweet(tweet, sourceType, hasImage);
                    if (shouldQuote && !string.IsNullOrEmpty(quoteText))
                    {
                        var qrtRes = await _socialIntel.QuoteRetweet(tweet.Url, quoteText);
                        if (qrtRes.status == "success") quoteRt = true;
                        LogAction?.Invoke(qrtRes.status == "success" 
                            ? $"💬🔄 [{sourceType}] Quote RT: \"{quoteText}\"" 
                            : $"❌ Quote RT Hatası: {qrtRes.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogAction?.Invoke($"⚠️ Etkileşim hatası: {ex.Message}");
            }

            // AI Tepkisi (Reply)
            string reaction = await GenerateFanaticReaction(tweet.Content, account);
            
            // v3.3.1 FIX: Resmi ve Sporcu hesaplarına anında yanıt ver (Eşik yok)
            // Fanlar için eşik 30, diğerleri için 0
            int threshold = (sourceType == "Fan" || sourceType == "Gündem") ? 30 : 0;

            if (!string.IsNullOrEmpty(reaction))
            {
                if (tweet.Engagement >= threshold)
                {
                    var repRes = await _socialIntel.ReplyToTweetAsync(tweet.Url, reaction);
                    if (repRes.status == "success") replied = true;
                    LogAction?.Invoke(repRes.status == "success" ? $"✅ [{sourceType}] Yanıt gönderildi ({tweet.Handle})" : $"❌ [{sourceType}] Yanıt Hatası: {repRes.text}");
                }
                else
                {
                    LogAction?.Invoke($"⚠️ [{sourceType}] Yanıt atlandı. Etkileşim düşük ({tweet.Engagement} < {threshold}) - {tweet.Url}");
                }
            }

            // v4.5.3: UI Update Logic with interaction flags
            var fanTweet = new FanZoneTweet
            {
                Handle = !string.IsNullOrEmpty(tweet.Handle) ? tweet.Handle : account,
                Text = tweet.Content,
                Link = tweet.Url,
                DetectedAt = DateTime.Now, 
                AIReaction = reaction,
                Source = sourceType,
                Liked = liked,
                Retweeted = retweeted,
                QuoteRt = quoteRt,
                Replied = replied
            };
            OnNewFanContent?.Invoke(fanTweet);
        }

        private string DetermineSourceType(string handle)
        {
            // Normalize
            string h = handle.Trim().TrimStart('@');
            string hWithAt = "@" + h;

            // 1. Resmi Hesaplar
            if (h.IndexOf("Fenerbahce", StringComparison.OrdinalIgnoreCase) >= 0 || 
                h.IndexOf("FBBasketbol", StringComparison.OrdinalIgnoreCase) >= 0) return "Resmi";

            // 2. Sporcular (Config Listesinden)
            var athletes = ConfigManager.Current.FenerbahceAthletes;
            if (athletes != null && athletes.Any(a => a.Handle.Replace("@","").Equals(h, StringComparison.OrdinalIgnoreCase)))
            {
                return "Sporcu";
            }

            // 3. Muhabirler (Bilinenler)
            if (h.Contains("yago") || h.Contains("sercan") || h.Contains("konanc") || h.Contains("Nexus")) return "Muhabir";

            // 4. Varsayılan
            return "Fan";
        }

        private async Task<string> GenerateFanaticReaction(string tweetText, string account)
        {
            try
            {
                string prompt = $@"
KİMLİK:
Sen Fenerbahçe'nin ""Dünyanın En Büyük Spor Kulübü"" olduğu bilinciyle yetişmiş, sadece futbolu değil; basketbolu (parkeyi), voleybolu (fileyi), yelkeni ve armanın olduğu her branşı aynı tutkuyla takip eden, spor kültürü çok yüksek bir taraftarsın.

TON VE ÜSLUP:
- ASLA ""Ben bir yapay zekayım"" deme. Sanki tribünde veya ekran başında maçı yaşayan bir ""renktaş"" gibi konuş.
- ROBOTİK OLMA: ""Fenerbahçe Basketbol Takımı kazandı"" demek yerine ""Parkede yine ders verdik, bu armaya yenilmek yok!"" gibi yaşayan cümleler kur.
- Sadece Futbol Odaklı Olma: Tweet'in hangi branşla (Futbol, Basketbol, Voleybol vb.) ilgili olduğunu anla ve o branşın jargonunu kullan (Örn: Basketbol için ""pota altı"", ""son çeyrek""; Voleybol için ""blok"", ""smaç"").
- İmla: Aşırı kurumsal olma. Küçük harf kullanımı, coşku belirten emojiler (💛💙, 🏀, ⚽, 🏐) ve samimi bir dil kullan.

GÖREV:
Aşağıdaki tweet'i analiz et, hangi branşla ilgili olduğunu saptayarak bağlama uygun cevap ver.

DAVRANIŞ MODELLERİ:
1. Fenerbahçeliye Cevap: Tam destek. ""Biz"" dilini kullan. (Örn: ""Futbolda üzdüler ama parkede yüzümüz güldü çok şükür."")
2. Rakibe Cevap: Saygını bozma ama kulübün büyüklüğünü (olimpiyat sporcularını, müze zenginliğini) ima eden ince zekanı kullan. Asla küfür etme.

DİL KURALI (POLYGLOT):
- Tweet TÜRKÇE ise: Sadece Türkçe, bizden biri gibi yanıt ver.
- Tweet YABANCI DİLDE ise: Tweet sahibinin dilinde kısa, o sporun jargonuna uygun bir reaksiyon ver. Yanına o cümlenin verdiği coşkuyu yansıtan Türkçe bir ekleme yap (Birebir çeviri yapma).

KISITLAMALAR:
- Maksimum 2 kısa cümle.
- Asla kendi kimliğini (bot olduğunu) açık etme.
- Politikaya girme, odak noktan sadece ARMA ve SPOR.

TWEET (Yazan: {account}):
""{tweetText}""

CEVAP (Sadece yanıt metni):";

                var response = await _gemini.GenerateFanZoneReaction(prompt);
                return response?.Trim() ?? "Saldır Fener!";
            }
            catch
            {
                return "💙💛 Saldır Fener!";
            }
        }

        /// <summary>
        /// v4.5.3: Hybrid Quote RT Decision - Rules + AI Verification
        /// Returns (shouldQuote, quoteText) tuple
        /// </summary>
        private async Task<(bool shouldQuote, string quoteText)> ShouldQuoteRetweet(
            InfluencerPost tweet, string sourceType, bool hasImage)
        {
            try
            {
                // RULE 1: Skip if low engagement (unless official)
                if (sourceType != "Resmi" && sourceType != "Sporcu" && tweet.Engagement < 100)
                    return (false, "");

                // RULE 2: Official accounts with images -> High priority
                bool ruleBased = (sourceType == "Resmi" && hasImage) ||
                                 (sourceType == "Sporcu") ||
                                 (tweet.Engagement >= 500);

                if (!ruleBased) return (false, "");

                // AI VERIFICATION: Ask Gemini if this deserves a Quote RT
                string prompt = $@"
GÖREV: Bu tweet'i Alıntılı RT açısından değerlendir.

KRİTERLER:
1. Görsel içerik var mı? (Fotoğraf, video = artı puan)
2. Haber değeri yüksek mi? (Transfer, skor, resmi duyuru)
3. Taraftarı heyecanlandırır mı?
4. Viral potansiyeli var mı?

TWEET BİLGİSİ:
- Kaynak: {sourceType}
- Etkileşim: {tweet.Engagement}
- Görsel: {(hasImage ? "VAR" : "YOK")}
- İçerik: ""{tweet.Content}""

KARAR:
Eğer alıntılı RT yapılmalıysa, JSON formatında yanıt ver:
{{""quote"": true, ""text"": ""Alıntı metni (max 50 karakter)""}}

Yapılmamalıysa:
{{""quote"": false}}";

                var response = await _gemini.GenerateFanZoneReaction(prompt);
                
                if (string.IsNullOrEmpty(response)) return (false, "");

                // Parse JSON response
                if (response.Contains("\"quote\": true") || response.Contains("\"quote\":true"))
                {
                    // Extract quote text
                    var match = System.Text.RegularExpressions.Regex.Match(
                        response, @"""text"":\s*""([^""]+)""");
                    string quoteText = match.Success ? match.Groups[1].Value : "💛💙 Gündem!";
                    return (true, quoteText);
                }

                return (false, "");
            }
            catch (Exception ex)
            {
                LogAction?.Invoke($"⚠️ Quote RT AI Error: {ex.Message}");
                return (false, "");
            }
        }
    }
}
