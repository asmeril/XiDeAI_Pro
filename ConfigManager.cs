using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace XiDeAI_Pro.Config
{
    public class AppSettings
    {
        public string TwitterApiKey { get; set; } = "";
        public string TwitterApiSecret { get; set; } = "";
        public string TwitterAccessToken { get; set; } = "";
        public string TwitterTokenSecret { get; set; } = "";
        public string GeminiApiKey { get; set; } = "";
        public string GeminiModel { get; set; } = "gemini-2.5-flash"; // Selected Gemini model
        public string TradingViewUsername { get; set; } = "";
        public string TradingViewChartId { get; set; } = "GDHgGCEv";  // Kullanıcının özel chart ID'si
        public string TradingViewSymbol { get; set; } = "NASDAQ:AAPL"; // Varsayılan sembol (BIST widget'ta çalışmıyor)
        public string XLoginUser { get; set; } = ""; // Selenium Login için
        public string XLoginPass { get; set; } = ""; // Selenium Login için
        public string TelegramBotToken { get; set; } = "";
        public string TelegramChatId { get; set; } = "";
        public string TargetAccounts { get; set; } = "";
        public bool AutoTweet { get; set; } = false;
        public string DailyTrends { get; set; } = "#BIST100 #Borsa";
        public List<string> Influencers { get; set; } = new List<string> 
        { 
            "@BorsaIstanbul", 
            "@KAP_Haber", 
            "@ForeksTurkey", 
            "@BloombergHT", 
            "@hisse_analiz", 
            "@borsagundem", 
            "@zeynepxcorp",
            "@hrrcnes", // v3.4.2: Meta-Teacher (The Operative)
            "@DataChaz", // v3.4.2: Meta-Teacher (The Technician)
            "@TansuYegen", // v3.4.2: Meta-Teacher (The Strategist)
            "@AndrewYNg" // v3.4.2: Meta-Teacher (The Visionary)
        }; // Dönüşümlü etiketlenecek hesaplar

        // X Account Status
        public bool IsVerifiedAccount { get; set; } = true; // Premium/Blue tick user
        public bool IsDarkTheme { get; set; } = true; // Theme Preference
        
        // Quota Limits (User Configurable)
        public int TwitterApiDailyLimit { get; set; } = 50; 
        public int TwitterApiMonthlyLimit { get; set; } = 1500;

        // Sinyal Filtre Ayarları (Alpha/PreMove)
        public int NewsCheckInterval { get; set; } = 15;
        public bool EnableAlpha { get; set; } = true;
        public bool EnablePreMove { get; set; } = true;
        // DB'ye yazılan sinyaller zaten robotun eşiğinden geçti (Alpha≥90, PreMove≥75)
        // Sadece AKTIF (Roket) sinyalleri mi işle, yoksa PULLBACK_ADAY dahil mi?
        public bool AlphaOnlyAktif { get; set; } = false;
        public bool PreMoveOnlyAktif { get; set; } = false;

        // --- Eski Robot Ayarları (Sabit Stub - Derleme Uyumu) ---
        public bool EnableKing => false;
        public bool EnableBomba => false;
        public bool EnableTeFo => false;
        public bool EnableDip => false;
        public bool EnableZirve => false;
        public bool EnableAnka => false;
        public bool EnableMiner => false;
        public bool Period15 => true;
        public bool Period60 => true;
        public bool Period240 => true;
        public bool PeriodDaily => true;
        public bool OnlyCommonSignals => false;
        public System.Collections.Generic.List<string> CommonStrategies => new();
        public int MinScoreKing => 0;
        public int MinScoreDip => 0;
        public int MinScoreAnka => 0;

        // Spam Protection Toggle
        public bool DisableSpamProtection { get; set; } = true; // Legacy/global toggle (deprecated)

        // Module-level spam protection toggles (per user request)
        public bool SpamProtectSignals { get; set; } = false;
        public bool SpamProtectBatches { get; set; } = false;
        public bool SpamProtectManual { get; set; } = false;
        public bool SpamProtectNews { get; set; } = false;
        public bool SpamProtectReports { get; set; } = false;
        public bool SpamProtectMotivation { get; set; } = false;

        // Zamanlama
        public bool RespectSchedule { get; set; } = true;
        public int TweetDelayMinutes { get; set; } = 2;
        public List<string> ScanHours { get; set; } = new List<string>(); // e.g. "10:00", "14:00", "18:00"

        // KOTA SAYAÇLARI
        public int DailyTweetCount { get; set; } = 0; // API Specific
        public int DailyTotalTweetCount { get; set; } = 0; // Selenium + API
        public int MonthlyTweetCount { get; set; } = 0; // API Specific
        public int MonthlyTotalTweetCount { get; set; } = 0; // Selenium + API
        public int FollowersCount { get; set; } = 429;
        public DateTime LastDailyReset { get; set; } = DateTime.Now;
        public DateTime LastMonthlyReset { get; set; } = DateTime.Now;

        // BOT ETKİLEŞİM AYARLARI
        public bool BotInteractionEnabled { get; set; } = false; // Bot aktif mi
        public string BotTopicKeywords { get; set; } = "borsa,bist,dolar,ekonomi,atatürk,fenerbahçe"; // Virgülle ayrılmış
        public int BotMinFollowers { get; set; } = 5000; // Min takipçi
        public int BotMinFavorites { get; set; } = 200; // Min beğeni
        public int BotMaxTweetAgeHours { get; set; } = 24; // Max tweet yaşı (saat)
        
        // v4.5.3: Kategori Bazlı Arama Kelimeleri (Round-Robin) - Updated with X Trend Research
        public Dictionary<string, List<string>> CategorySearchKeywords { get; set; } = new()
        {
            // FINANS: Güncel borsa ve kripto trendleri
            { "FINANS", new List<string> { "#Borsa", "#BIST100", "#Hisse", "#Altın", "#Dolar", "#USDTRY", "#Bitcoin", "#Kripto", "#HalkaArz", "#Ekonomi", "gram altın" } },
            
            // Eğlence/Kültür: Popüler dizi/film/sanat içerikleri (Eşref yerine güncel içerikler)
            { "KULTUR_EGLENCE", new List<string> { "#NetflixTürkiye", "#DiziFragman", "#KültürSanat", "#TürkSineması", "#TiyatroGünü", "dizi izle", "film önerisi" } },
            
            // Milli/Toplumsal: Vatan, millet, önemli günler
            { "MILLI_TOPLUM", new List<string> { "#Atatürk", "#Vatan", "#TürkBayrağı", "#ŞehitlerÖlmez", "#Teknofest", "#SavunmaSanayii", "#MilliTakım", "#19Mayıs", "#29Ekim" } },
            
            // Bilim/Teknoloji: Yapay zeka, uzay, arkeoloji
            { "BILGE_KULTUR", new List<string> { "#YapayZeka", "#ChatGPT", "#SpaceX", "#NASA", "#Arkeoloji", "#Göbeklitepe", "#Bilim", "#Teknoloji", "#TarihteBugün" } },
            
            // Motivasyon/Kişisel Gelişim: İlham verici içerikler
            { "INSAN_RUH", new List<string> { "#motivasyon", "#başarı", "#kişiselgelişim", "#olumlamaları", "#gününsözü", "#farkındalık", "#ilham", "güne güzel başla" } },
            
            // Günlük Mizah: Yüksek etkileşimli günlük paylaşımlar
            { "GUNLUK_MIZAH", new List<string> { "#mizah", "#komik", "#karikatür", "#günaydın", "#Caps", "#espri", "bugün günlerden", "yine oldu" } }
        };

        // HABER ANALİZ AYARLARI
        public int MinNewsImportance { get; set; } = 4; // v2.8: Varsayılan 4 (Sadece Kritik Haberler)
        public bool AutoPostBreakingNews { get; set; } = true; 
        
        // v3.8.2: Debugging & Test Mode
        public bool NewsTestMode { get; set; } = false; // If true, logs why news is skipped but allows it through filters 
        
        // v2.8: Haber İzleme Listesi (Özel Filtre Altyapısı)
        public string NewsWatchlist { get; set; } = "Yapay Zeka, Enerji, Halka Arz"; 
        
        // Phase 4: AI Deep Scan (Pre-Filter)
        public bool EnableDeepScan { get; set; } = false; // AI-powered signal pre-screening
        
        // v4.5.4: Dynamic Trend Engagement
        public bool TrendEngagementEnabled { get; set; } = true; // Dinamik trend paylaşımı
        public int MaxTrendPostsPerDay { get; set; } = 9; // Günlük maksimum trend tweeti
        
        // Multi-Model AI Support (v3.1+)
        public string PerplexityApiKey { get; set; } = ""; // Perplexity AI for real-time news analysis
        public string PerplexityModel { get; set; } = "sonar"; // "sonar" or "sonar-pro"
        public bool EnableMultiModel { get; set; } = true; // Enable intelligent model selection
        public bool EnableAutoFallback { get; set; } = true; // Auto-fallback to alternative models
        public bool IsGuruAutomationEnabled { get; set; } = false; // Phase 4.1: Guru Automation Toggle
        public string GuruHandle { get; set; } = "@EFELERiiNEFESi3"; // Phase 4.1: Takip edilen üstat handle'ı
        public bool EnableMetaTeacher { get; set; } = false; // Phase 1 (Hive): Enables Meta-Teacher Analysis Logic

        // LM Studio / Link Support
        public bool LMStudioEnabled { get; set; } = true; // Default to true if user is asking for it
        public string LMStudioUri { get; set; } = "http://localhost:1234/v1"; 
        public string LMStudioModel { get; set; } = "gemma4"; // Labeled as 'gemma4' in your LM Studio
        public string LMStudioApiKey { get; set; } = "3c668174111958f34bc7f4699322787c"; // The code provided by the user

        // FENERBAHÇE FAN ZONE
        public bool FanZoneEnabled { get; set; } = true;
        public List<string> FenerbahceAccounts { get; set; } = new List<string>
        {
            "@Fenerbahce", 
            "@FBBasketbol",
            "@yagosabuncuoglu", 
            "@sercanhamzaoglu", 
            "@ahmetkonanc",
            "@12numaraorg", 
            "@NexusSports", 
            "@TekYolFener"
        };
        public List<string> FenerbahceKeywords { get; set; } = new List<string> { "#Fenerbahçe", "#FB", "#Sarılacivert", "Mourinho" };
        
        // v3.3 Athlete Tracking
        public List<FenerbahceAthlete> FenerbahceAthletes { get; set; } = new List<FenerbahceAthlete>();

        public void CheckReset()
        {
            if (LastDailyReset.Date != DateTime.Now.Date)
            {
                DailyTweetCount = 0;
                DailyTotalTweetCount = 0;
                LastDailyReset = DateTime.Now;
            }
            if (LastMonthlyReset.Month != DateTime.Now.Month || LastMonthlyReset.Year != DateTime.Now.Year)
            {
                MonthlyTweetCount = 0;
                MonthlyTotalTweetCount = 0;
                LastMonthlyReset = DateTime.Now;
            }
        }
        
        // v4.6.0: Advanced Safety Layer
        public SafetySettings Safety { get; set; } = new SafetySettings();
    }

    public class SafetySettings
    {
        public SafetyLevel Level { get; set; } = SafetyLevel.High;
        public int MinDelayBetweenTweetsSeconds { get; set; } = 300; // 5 mins default
        public int DailyTweetHardLimit { get; set; } = 15;
        public int DailySearchHardLimit { get; set; } = 50;
        public DateTime LastTweetTime { get; set; } = DateTime.MinValue;
        public DateTime LastSearchTime { get; set; } = DateTime.MinValue;
    }

    public enum SafetyLevel
    {
        Low,     // 2 min delay, Higher limits
        Medium,  // 5 min delay, Moderate limits
        High     // 10 min delay, Strict limits
    }

    public class FenerbahceAthlete
    {
        public string Name { get; set; } = string.Empty;
        public string Handle { get; set; } = string.Empty;
        public string Sport { get; set; } = string.Empty;
        public DateTime LastInteraction { get; set; }
    }

    public static class ConfigManager
    {
        // Use AppData for config files (Program Files is read-only)
        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "XiDeAI"
        );
        private static string ConfigPath = Path.Combine(AppDataDir, "config.dat");
        private static string LegacyConfigPath = Path.Combine(AppDataDir, "config.json");
        
        public static AppSettings Current { get; private set; } = new AppSettings();

        public static void AddWebUsage()
        {
            Current.CheckReset();
            Current.DailyTotalTweetCount++;
            Current.MonthlyTotalTweetCount++;
            Save();
        }

        public static void AddApiUsage()
        {
            Current.CheckReset();
            Current.DailyTweetCount++;
            Current.DailyTotalTweetCount++;
            Current.MonthlyTweetCount++;
            Current.MonthlyTotalTweetCount++;
            Save();
        }

        static ConfigManager()
        {
            // Ensure AppData directory exists
            Directory.CreateDirectory(AppDataDir);
            Load();
        }

        public static void Load()
        {
            // 1. Try Loading Encrypted
            if (File.Exists(ConfigPath))
            {
                try
                {
                    byte[] encryptedData = File.ReadAllBytes(ConfigPath);
                    byte[] jsonData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
                    string json = Encoding.UTF8.GetString(jsonData);
                    Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    
                    // v3.4.2: Auto-add Council Members if missing
                    var council = new List<string> { "@hrrcnes", "@DataChaz", "@TansuYegen", "@AndrewYNg" };
                    bool saveNeeded = false;
                    foreach (var member in council)
                    {
                        if (!Current.Influencers.Contains(member))
                        {
                            Current.Influencers.Add(member);
                            saveNeeded = true;
                        }
                    }

                    if (saveNeeded)
                    {
                        Save();
                    }

                    return;
                }
                catch 
                { 
                    // Decryption failed (or file corrupted), fallback to new
                    Current = new AppSettings(); 
                }
            }
            // 2. Migration: Check Legacy Plain JSON
            else if (File.Exists(LegacyConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(LegacyConfigPath);
                    Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    
                    // Immediately encrypt and save to new format
                    Save();
                    
                    // Optional: Backup or Delete legacy
                    // File.Delete(LegacyConfigPath); // Safer to let user delete after verification
                }
                catch { Current = new AppSettings(); }
            }
            else
            {
                Current = new AppSettings();
            }
        }

        public static void Save()
        {
            string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            byte[] jsonData = Encoding.UTF8.GetBytes(json);
            
            // Encrypt with CurrentUser scope (Only this user on this machine can decrypt)
            byte[] encryptedData = ProtectedData.Protect(jsonData, null, DataProtectionScope.CurrentUser);
            
            File.WriteAllBytes(ConfigPath, encryptedData);
        }
    }
}
