
using System;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro.Services
{
    public class SocialIntelResult
    {
        public string status { get; set; } = "";
        public string handle { get; set; } = "";
        public string link { get; set; } = "";
        public string text { get; set; } = "";
        public string message { get; set; } = "";  // Python returns 'message' for errors
        
        // Helper: Get error message from either property
        public string ErrorMessage => !string.IsNullOrEmpty(message) ? message : text;
    }

    public class SocialIntelNewsItem
    {
        public string source { get; set; } = "";
        public string text { get; set; } = "";
        public string time { get; set; } = "";
        public string url { get; set; } = ""; // EKLENDİ: Gerçek tweet linki için
    }

    public class SocialIntelNewsResult
    {
        public string status { get; set; } = "";
        public SocialIntelNewsItem[] data { get; set; } = Array.Empty<SocialIntelNewsItem>();
    }

    public class InfluencerPost
    {
        public string Handle { get; set; } = "";
        public string Content { get; set; } = "";
        public int Engagement { get; set; } = 0;
        public int RelevanceScore { get; set; } = 0; // NEW: Track analysis quality
        public string Url { get; set; } = "";
        public int FollowerCount { get; set; } = 0;
        public DateTime PostDate { get; set; } = DateTime.MinValue;
        public string? ImageUrl { get; set; } // Supporting guru tables
    }

    public class ProfileStats
    {
        public string username { get; set; } = "";
        public string following { get; set; } = "0";
        public string followers { get; set; } = "0";
    }

    public class SocialIntelStatsResult
    {
        public string status { get; set; } = "";
        public ProfileStats? data { get; set; }
    }

    public class SocialIntelTrendsResult
    {
        public string status { get; set; } = "";
        public string[] data { get; set; } = Array.Empty<string>();
    }

    public class SocialIntelDMsResult
    {
        public string status { get; set; } = "";
        public string[] data { get; set; } = Array.Empty<string>();
    }

    public class SocialIntelFinancialResult
    {
        public string status { get; set; } = "";
        public Dictionary<string, string> data { get; set; } = new Dictionary<string, string>();
    }

    public class StockData
    {
        public string Symbol { get; set; } = "";
        public decimal Close { get; set; } = 0;
        public decimal ChangePercent { get; set; } = 0;
        public long Volume { get; set; } = 0;
    }

    public class SocialIntelStockListResult
    {
        public string status { get; set; } = "";
        public List<StockData> data { get; set; } = new List<StockData>();
    }

    public class SocialIntelEngagementItem
    {
        public string text { get; set; } = "";
        public int replies { get; set; } = 0;
        public int retweets { get; set; } = 0;
        public int likes { get; set; } = 0;
    }

    public class SocialIntelEngagementResult
    {
        public string status { get; set; } = "";
        public List<SocialIntelEngagementItem> data { get; set; } = new List<SocialIntelEngagementItem>();
    }

    public class SocialIntelInteractResult
    {
        public string status { get; set; } = "";
        public string message { get; set; } = "";
        public Dictionary<string, string> data { get; set; } = new Dictionary<string, string>();
    }

    public class SocialIntelService
    {
        private DateTime _lastInteractionTime = DateTime.MinValue;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _deepScanLock = new SemaphoreSlim(1, 1); // DeepScan için ayrı kilit
        private MemoryEngine? _memoryEngine; // Sonradan set edilecek
        private InfluencerControlService? _influencerControl;
        private StatsEngine? _stats;

        public void SetMemoryEngine(MemoryEngine engine) { _memoryEngine = engine; }
        public void SetInfluencerControl(InfluencerControlService control) { _influencerControl = control; }
        public void SetStatsEngine(StatsEngine stats) { _stats = stats; }
        
        // Phase 3: Expose Memory for interaction tracking
        public MemoryEngine Memory => _memoryEngine ?? throw new InvalidOperationException("MemoryEngine not set");

        private const int MinTweetLength = 20;
        private readonly string _scriptPath;
        // Global posting lock to serialize Selenium interactions across modules
        private static readonly SemaphoreSlim _postLock = new SemaphoreSlim(1, 1);
        public bool IsVisibleMode { get; set; } = false; // User Request: Must be headless by default
        private static DateTime _lastPostUtc = DateTime.MinValue;
        private static readonly TimeSpan PostCooldown = TimeSpan.FromSeconds(20);
        private static bool _authMethodLogged = false; // Optimization: Log auth method only once

        // NOTE: WebView2 Bridge events deprecated - Using Selenium/Python exclusively
        #pragma warning disable CS0067
        public event Func<string, string?, Task<SocialIntelResult>>? OnPostTweetRequested;
        public event Func<List<string>, string?, Task<SocialIntelResult>>? OnPostThreadRequested;
        public event Func<string, string, string, List<string>?, Task<List<InfluencerPost>>>? OnSearchRequested;
        public event Func<Task<ProfileStats?>>? OnGetStatsRequested;
        public event Func<Task<string[]>>? OnGetTrendsRequested;
        public event Func<string, string, Task<SocialIntelResult>>? OnReplyRequested;
        public event Func<string[], Task<SocialIntelInteractResult>>? OnInteractTargetsRequested;
        #pragma warning restore CS0067

        private static string NormalizeMarketType(string? marketType)
            => string.IsNullOrWhiteSpace(marketType) ? "BIST" : marketType;

        public static string DetectMarket(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return "BIST";
            string upper = symbol.ToUpperInvariant();
            if (upper.Contains("USDT") || upper.Contains("BTC") || upper.Contains("ETH")) return "Kripto";
            if (upper.Contains("XAU") || upper.Contains("XAG") || upper.Contains("EUR") || upper.Contains("GBP") || upper.Contains("USD")) return "Forex";
            return "BIST";
        }

        public SocialIntelService()
        {
            // NEW STRATEGY: Prioritize source directory if it exists, otherwise use bin local
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            string sourcePath = Path.Combine(projectRoot, "Scripts", "social_intel.py");

            if (File.Exists(sourcePath))
            {
                _scriptPath = sourcePath;
            }
            else
            {
                // Fallback to bin/Scripts folder
                _scriptPath = Path.Combine(baseDir, "Scripts", "social_intel.py");
            }

            // CRITICAL: Log exactly which file we are using to avoid "version ghosting"
            Logger.Sys($"[SocialIntel] ACTIVE SCRIPT: {_scriptPath}");
        }

        public async Task<SocialIntelResult?> FindInfluencerTweet(string symbol, string marketType = "BIST")
        {
            try
            {
                marketType = NormalizeMarketType(marketType);
                var cfg = ConfigManager.Current;
                if (string.IsNullOrEmpty(cfg.XLoginUser) || string.IsNullOrEmpty(cfg.XLoginPass))
                    return null;

                // python social_intel.py search_influencer --symbol SYMBOL --market MARKET
                // Credentials passed via Environment Variables for security (Hiding from Task Manager)
                string args = $"\"{_scriptPath}\" search_influencer --symbol {symbol} --market {marketType}";
                
                string json = await RunPythonScript(args, cfg.XLoginUser, cfg.XLoginPass);
                return JsonSerializer.Deserialize<SocialIntelResult>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SocialIntel Error: {ex.Message}");
                return null;
            }
        }

        public async Task<SocialIntelNewsResult?> FetchBreakingNews()
        {
            await _lock.WaitAsync();
            try
            {
                var cfg = ConfigManager.Current;
                
                // Only log auth method once (optimization)
                if (!_authMethodLogged && (string.IsNullOrEmpty(cfg.XLoginUser) || string.IsNullOrEmpty(cfg.XLoginPass)))
                {
                    Logger.News("ℹ️ X cookie auth aktif");
                    _authMethodLogged = true;
                }

                // Pass credentials via Environment Variables (if available)
                string args = $"\"{_scriptPath}\" fetch_news";
                
                Logger.News($"🔍 X haber çekme başlatılıyor...");
                
                string json = await RunPythonScript(args, cfg.XLoginUser, cfg.XLoginPass);
                
                if (string.IsNullOrEmpty(json) || json == "{}")
                {
                    Logger.News("❌ Python script boş sonuç döndü!");
                    return null;
                }
                
                var result = JsonSerializer.Deserialize<SocialIntelNewsResult>(json);
                
                if (result == null)
                {
                    Logger.News("❌ JSON deserialize sonucu null!");
                    return null;
                }
                
                if (result.data?.Length > 0)
                {
                    Logger.News($"✅ {result.data.Length} haber alındı");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Logger.News($"❌ FetchBreakingNews HATA: {ex.GetType().Name} - {ex.Message}");
                return null;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<string> RunPythonScript(string arguments, string? user = null, string? pass = null)
        {
            var startTime = DateTime.Now;
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Secure Credential Passing
            if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
            {
                psi.EnvironmentVariables["X_USER"] = user;
                psi.EnvironmentVariables["X_PASS"] = pass;
            }

            using var process = Process.Start(psi);
            if (process == null) return "{}";

            // 90 second timeout for X automation tasks
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(90));

            string output = "";
            string error = "";

            try
            {
                // Read both streams asynchronously with timeout support
                var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
                var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync(cts.Token);

                output = outputTask.Result.Trim();
                error = errorTask.Result.Trim();
            }
            catch (OperationCanceledException)
            {
                Logger.Twitter("⚠️ X otomasyon işlemi ZAMAN AŞIMINA uğradı (90s). Süreç sonlandırılıyor...");
                try { process.Kill(true); } catch { }
                return "{\"status\": \"error\", \"message\": \"Process timeout (90s)\"}";
            }

            // Log stderr as warnings if present
            if (!string.IsNullOrEmpty(error))
            {
                Logger.Twitter($"Python Warnings/Logs: {error}");
            }

            // ROBUST JSON EXTRACTION (Marker based v3.0)
            if (!string.IsNullOrEmpty(output))
            {
                // Find all JSON blocks between markers
                const string startMarker = "---JSON_START---";
                const string endMarker = "---JSON_END---";

                int startPos = output.LastIndexOf(startMarker);
                int endPos = output.LastIndexOf(endMarker);

                if (startPos != -1 && endPos != -1 && endPos > startPos)
                {
                    string jsonContent = output.Substring(startPos + startMarker.Length, endPos - (startPos + startMarker.Length)).Trim();
                    if (!string.IsNullOrEmpty(jsonContent))
                    {
                        return jsonContent;
                    }
                }

                // Fallback to Regex if markers are missing (for backward compatibility during transition)
                var matches = System.Text.RegularExpressions.Regex.Matches(output, @"\{(?:[^{}]|(?<open>\{)|(?<-open>\}))*\}(?(open)(?!))", System.Text.RegularExpressions.RegexOptions.Singleline);
                if (matches.Count > 0)
                {
                    for (int i = matches.Count - 1; i >= 0; i--)
                    {
                        string candidate = matches[i].Value;
                        if (candidate.Contains("\"status\"") || candidate.Contains("'status'"))
                        {
                            return candidate;
                        }
                    }
                    return matches[matches.Count - 1].Value;
                }
            }

            // If stdout is strictly empty but we have an error that looks like JSON, return it (rare fallback)
            if (string.IsNullOrEmpty(output) && !string.IsNullOrEmpty(error) && error.StartsWith("{") && error.EndsWith("}"))
            {
                 return error;
            }

            // If we have output but it's not JSON (e.g. "Query parse failed")
            if (!string.IsNullOrEmpty(output) && !output.StartsWith("{"))
            {
                 var safeText = System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(output);
                 return $"{{\"status\": \"error\", \"message\": \"Invalid Output: {safeText}\", \"text\": \"Invalid Output\"}}";
            }

            // If completely failed to find JSON
            if (string.IsNullOrEmpty(output) && !string.IsNullOrEmpty(error))
            {
                // Retrieve simple error message (first few lines) to avoid huge logs in JSON
                var safeError = System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(error.Length > 200 ? error.Substring(0, 200) + "..." : error);
                return $"{{\"status\": \"error\", \"message\": \"Python Hatasi: {safeError}\", \"text\": \"{safeError}\"}}";
            }
            
            // If both are empty or no JSON found
            if (string.IsNullOrEmpty(output))
            {
                 return "{\"status\": \"error\", \"message\": \"Python script hicbir cikti uretmedi (Empty Output).\", \"text\": \"No output\"}";
            }

            return output;
        }

        /// <summary>
        /// Login to X with visible browser for 2FA.
        /// After successful login, cookies are saved for future headless sessions.
        /// </summary>
        public async Task<(bool Success, string Message)> ImportCookiesAsync(string jsonContent)
        {
            try
            {
                // Create a temp file to pass JSON content to Python
                // This avoids command line length limits
                string tempFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(tempFile, jsonContent);

                string args = $"\"{_scriptPath}\" import_cookies --file \"{tempFile}\"";
                string json = await RunPythonScript(args);
                
                // Cleanup temp file
                try { File.Delete(tempFile); } catch { }

                // ROBUST JSON PARSING: Handle cases where Python outputs multiple JSON objects
                // Take only the LAST valid JSON object (most recent output)
                json = json.Trim();
                
                // If multiple JSON objects exist (separated by newlines), take the last one
                var lines = json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                string? lastValidJson = null;
                foreach (var line in lines.Reverse())
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                    {
                        lastValidJson = trimmed;
                        break;
                    }
                }
                
                if (string.IsNullOrEmpty(lastValidJson))
                {
                    return (false, $"❌ Python çıktısı geçersiz: {json.Substring(0, Math.Min(100, json.Length))}");
                }

                // Parse JSON result from Python
                using var doc = JsonDocument.Parse(lastValidJson);
                if (doc.RootElement.TryGetProperty("status", out var status) && status.GetString() == "success")
                {
                    string msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "Success" : "Success";
                    return (true, $"✅ {msg}");
                }
                else
                {
                    string err = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "Unknown error" : "Unknown error";
                    return (false, $"❌ {err}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"❌ Hata: {ex.Message}");
            }
        }
        
        public Task<(bool Success, string Message)> LoginAsync()
        {
             // Deprecated in favor of ImportCookiesAsync for Server use
             return Task.FromResult((false, "Bu özellik sunucu versiyonunda kapalıdır. Lütfen 'Çerezleri İçe Aktar' kullanın."));
        }

        public async Task<SocialIntelResult> PostTweet(string text, string? mediaPath = null)
        {
            // Selenium/Python Direct Method (WebView2 bridge disabled)
            string tempFile = "";
            try
            {
                var payload = new
                {
                    text = text,
                    media = mediaPath
                };

                string jsonContent = JsonSerializer.Serialize(payload);
                tempFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(tempFile, jsonContent);

                string visibilityFlag = IsVisibleMode ? " --visible" : "";
                string args = $"\"{_scriptPath}\" post_tweet --file \"{tempFile}\"{visibilityFlag}";
                
                string json = await RunPythonScript(args);
                var result = JsonSerializer.Deserialize<SocialIntelResult>(json);
                
                if (result != null && result.status == "success")
                {
                     ConfigManager.AddWebUsage();
                     _stats?.RecordTweet("SocialIntel", 1, "", text);
                }
                
                return result ?? new SocialIntelResult { status = "error", text = "Empty response" };
            }
            catch (Exception ex)
            {
                return new SocialIntelResult { status = "error", message = ex.Message };
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }


        public async Task<SocialIntelResult> PostThreadAsync(List<string> tweets, string? mediaPath = null)
        {
            // 1. Try Internal Bridge
            if (OnPostThreadRequested != null)
            {
                try
                {
                    Logger.Twitter("🔄 Dahili (WebView2) thread posting başlatılıyor...");
                    var res = await OnPostThreadRequested.Invoke(tweets, mediaPath);
                    if (res != null)
                    {
                        if (res.status == "success") 
                        {
                            Logger.Twitter("✅ Dahili thread gönderimi başarılı.");
                            try
                            {
                                var cfg = ConfigManager.Current;
                                cfg.CheckReset();
                                cfg.DailyTotalTweetCount += tweets.Count;
                                cfg.MonthlyTotalTweetCount += tweets.Count;
                                ConfigManager.Save();
                            }
                            catch { /* sayaç güncelleme hatası yok sayılır */ }
                            return res;
                        }
                        else if (res.status == "cancelled")
                        {
                            Logger.Twitter("⚠️ Dahili thread kullanıcı tarafından iptal edildi.");
                            return res;
                        }
                        // If it's an error, we fall through to Python fallback
                        Logger.Twitter($"⚠️ Dahili thread hatası ({res.status}): {res.ErrorMessage}. Python fallback deneniyor...");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Twitter($"⚠️ Dahili thread kritik hatası: {ex.Message}");
                }
            }

            // 2. Fallback
            string tempFile = "";
            try
            {
                // Serialize posting operations and respect cooldown
                await _postLock.WaitAsync();
                var now = DateTime.UtcNow;
                var delta = now - _lastPostUtc;
                if (delta < PostCooldown)
                {
                    try { await Task.Delay(PostCooldown - delta); } catch { }
                }

                // Create payload object
                var payload = new 
                { 
                    tweets = tweets,
                    media = mediaPath 
                };

                // Serialize to JSON and write to temp file
                string jsonContent = JsonSerializer.Serialize(payload);
                tempFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(tempFile, jsonContent);

                // Pass the FILE PATH to Python
                string visibilityFlag = IsVisibleMode ? " --visible" : "";
                string args = $"\"{_scriptPath}\" post_thread --file \"{tempFile}\"{visibilityFlag}";
                
                string json = await RunPythonScript(args);
                
                var result = JsonSerializer.Deserialize<SocialIntelResult>(json);
                _lastPostUtc = DateTime.UtcNow;

                if (result != null && result.status == "success")
                {
                    try
                    {
                        var cfg = ConfigManager.Current;
                        cfg.CheckReset();
                        cfg.DailyTotalTweetCount += tweets.Count;
                        cfg.MonthlyTotalTweetCount += tweets.Count;
                        ConfigManager.Save();
                    }
                    catch { /* sayaç güncelleme hatası yok sayılır */ }
                }

                return result ?? new SocialIntelResult { status = "error", text = "Empty response" };
            }
            catch (Exception ex)
            {
                return new SocialIntelResult { status = "error", text = ex.Message };
            }
            finally
            {
                try { _postLock.Release(); } catch { }
                // Cleanup
                if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        public async Task<SocialIntelResult> ReplyToTweetAsync(string url, string text)
        {
            // 1. Try Internal
            if (OnReplyRequested != null)
            {
                var res = await OnReplyRequested.Invoke(url, text);
                if (res != null && res.status == "success") return res;
            }

            // 2. Fallback
            try
            {
                await _postLock.WaitAsync();
                var now = DateTime.UtcNow;
                var delta = now - _lastPostUtc;
                if (delta < PostCooldown)
                {
                    try { await Task.Delay(PostCooldown - delta); } catch { }
                }

                var bytes = Encoding.UTF8.GetBytes(text);
                var base64Text = Convert.ToBase64String(bytes);
                
                string args = $"\"{_scriptPath}\" reply_tweet --url \"{url}\" --text \"{base64Text}\" --base64";
                string json = await RunPythonScript(args);
                
                var result = JsonSerializer.Deserialize<SocialIntelResult>(json);
                _lastPostUtc = DateTime.UtcNow;
                return result ?? new SocialIntelResult { status = "error", text = "Empty response" };
            }
            catch (Exception ex)
            {
                return new SocialIntelResult { status = "error", text = ex.Message };
            }
            finally
            {
                try { _postLock.Release(); } catch { }
            }
        }
        
        public async Task<(bool Success, string Message)> ClearSessionAsync()
        {
            try
            {
                string args = $"\"{_scriptPath}\" clear_session";
                string json = await RunPythonScript(args);
                
                if (json.Contains("success"))
                {
                    return (true, "✅ X oturumu temizlendi. Bir sonraki kullanımda tekrar giriş yapmanız gerekecek.");
                }
                return (false, "❌ Oturum temizlenemedi.");
            }
            catch (Exception ex)
            {
                return (false, $"❌ Hata: {ex.Message}");
            }
        }

        public async Task<ProfileStats?> GetProfileStatsAsync()
        {
            // Selenium/Python Direct Method
            try
            {
                string args = $"\"{_scriptPath}\" get_stats";
                string json = await RunPythonScript(args);
                
                var result = JsonSerializer.Deserialize<SocialIntelStatsResult>(json);
                if (result != null && result.status == "success")
                {
                    return result.data;
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Stats Error: {ex.Message}");
                return null;
            }
        }

        public void CheckViralInteractions(string topic)
        {
            // TODO: Move MainForm:CheckForInteractions viral reply logic here
        }

        public async Task<string[]> GetTrendsAsync()
        {
            // Selenium/Python Direct Method
            try
            {
                string args = $"\"{_scriptPath}\" get_trends";
                string json = await RunPythonScript(args);
                
                var result = JsonSerializer.Deserialize<SocialIntelTrendsResult>(json);
                if (result != null && result.status == "success")
                {
                    return result.data ?? Array.Empty<string>();
                }
                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Trends Error: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public async Task<string[]> GetDMsAsync()
        {
            try
            {
                string args = $"\"{_scriptPath}\" get_dms";
                string json = await RunPythonScript(args);
                
                var result = JsonSerializer.Deserialize<SocialIntelDMsResult>(json);
                if (result != null && result.status == "success")
                {
                    return result.data ?? Array.Empty<string>();
                }
                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetDMs Error: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public async Task<List<SocialIntelEngagementItem>> GetRecentEngagementAsync()
        {
            try
            {
                string args = $"\"{_scriptPath}\" get_engagement";
                string json = await RunPythonScript(args);

                var result = JsonSerializer.Deserialize<SocialIntelEngagementResult>(json);
                if (result != null && result.status == "success")
                {
                    return result.data ?? new List<SocialIntelEngagementItem>();
                }
                return new List<SocialIntelEngagementItem>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Engagement Error: {ex.Message}");
                return new List<SocialIntelEngagementItem>();
            }
        }

        public async Task<Dictionary<string, string>> GetFinancialSummaryAsync()
        {
            try
            {
                string args = $"\"{_scriptPath}\" get_financials";
                string json = await RunPythonScript(args);
                
                var result = JsonSerializer.Deserialize<SocialIntelFinancialResult>(json);
                if (result != null && result.status == "success")
                {
                    return result.data ?? new Dictionary<string, string>();
                }
                return new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Financials Error: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        public async Task<Dictionary<string, decimal>> GetStockPricesAsync(IEnumerable<string> symbols)
        {
            try
            {
                string symList = string.Join(",", symbols);
                string args = $"\"{_scriptPath}\" batch_get_prices --symbols \"{symList}\"";
                string json = await RunPythonScript(args);
                
                var result = JsonSerializer.Deserialize<SocialIntelFinancialResult>(json);
                var prices = new Dictionary<string, decimal>();
                
                if (result != null && result.status == "success" && result.data != null)
                {
                    foreach (var kvp in result.data)
                    {
                        if (decimal.TryParse(kvp.Value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var price))
                        {
                            prices[kvp.Key] = price;
                        }
                    }
                }
                return prices;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Batch Price Error: {ex.Message}");
                return new Dictionary<string, decimal>();
            }
        }

        public async Task<SocialIntelInteractResult> InteractWithTargets(string usersStr)
        {
            var users = usersStr.Split(new[] { ',', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            return await InteractTargetsAsync(users);
        }

        public async Task<SocialIntelInteractResult> InteractTargetsAsync(string[] users)
        {
             // 1. Try Internal
             if (OnInteractTargetsRequested != null)
             {
                 var res = await OnInteractTargetsRequested.Invoke(users);
                 if (res != null && res.status == "success") return res;
             }

             // 2. Fallback
             try
             {
                 if(users.Length == 0) return new SocialIntelInteractResult { status = "error", message = "No users provided" };
                 
                 string usersArg = string.Join(",", users);
                 string args = $"\"{_scriptPath}\" interact_with_targets --targets \"{usersArg}\"";
                 string json = await RunPythonScript(args);
                 
                 var result = JsonSerializer.Deserialize<SocialIntelInteractResult>(json);
                 return result ?? new SocialIntelInteractResult { status = "error", message = "No response from script" };
             }
             catch (Exception ex)
             {
                 Console.WriteLine($"Interact Error: {ex.Message}");
                 return new SocialIntelInteractResult { status = "error", message = ex.Message };
             }
        }

        /// <summary>
        /// Find top influencer analyses for a given symbol (Crypto/BIST/Forex)
        /// Returns 2-3 posts with highest engagement from known analysts
        /// </summary>
        public async Task<List<string>> DiscoverInfluencers(string category)
        {
            try
            {
                string args = $"\"{_scriptPath}\" discover_influencers --category {category}";
                Console.WriteLine($"[Discovery] Running: python {args}");
                string json = await RunPythonScript(args);
                
                Console.WriteLine($"[Discovery] Response: {json}");
                
                // Expected: { "status": "success", "count": 10, "data": ["@handle1", "@handle2"] }
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("status", out var status))
                {
                    string? statusStr = status.GetString();
                    if (string.Equals(statusStr, "success", StringComparison.OrdinalIgnoreCase))
                    {
                        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                        {
                            var list = new List<string>();
                            foreach(var item in data.EnumerateArray())
                            {
                                list.Add(item.GetString() ?? "");
                            }
                            Console.WriteLine($"[Discovery] Success: {list.Count} influencers found in {category}");
                            return list;
                        }
                    }
                    else if (string.Equals(statusStr, "error", StringComparison.OrdinalIgnoreCase) && doc.RootElement.TryGetProperty("message", out var message))
                    {
                        Console.WriteLine($"[Discovery] Error from script: {message.GetString()}");
                    }
                }
                Console.WriteLine($"[Discovery] No valid results for {category}");
                return new List<string>();
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[Discovery] Exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return new List<string>();
            }
        }

           private async Task SearchAndParse(string query, List<InfluencerPost> results, string marketType = "BIST")
        {
            marketType = NormalizeMarketType(marketType);

            // 1. Try Internal
            if (OnSearchRequested != null)
            {
                try
                {
                    var internalRes = await OnSearchRequested.Invoke(query, query, marketType, null);
                    if (internalRes != null && internalRes.Count > 0)
                    {
                        results.AddRange(internalRes);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Twitter($"⚠️ Dahili arama hatası: {ex.Message}");
                }
            }

            // 2. Fallback
             // Base64 encode query to avoid CLI issues
             var bytes = Encoding.UTF8.GetBytes(query);
             var b64Query = Convert.ToBase64String(bytes);

             string args = $"\"{_scriptPath}\" search_influencer --query \"{b64Query}\" --base64 --market {marketType}";
             Logger.Twitter($"[SearchAndParse] Running: python {args}");
             
             string json = await RunPythonScript(args);
             Logger.Twitter($"[SearchAndParse] Response: {(json?.Length > 500 ? json.Substring(0, 500) + "..." : json)}");

             if (string.IsNullOrEmpty(json))
             {
                 Logger.Twitter("[SearchAndParse] Empty response from Python!");
                 return;
             }

             try
             {
                 using var doc = JsonDocument.Parse(json);
                 JsonElement dataArray;
                 
                 // Handle new { "status": "success", "data": [...] } OR legacy array
                 if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("data", out var dataProp))
                 {
                     dataArray = dataProp;
                 }
                 else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                 {
                     dataArray = doc.RootElement;
                 }
                 else
                 {
                     Logger.Twitter($"[SearchAndParse] Response is not a valid format: {doc.RootElement.ValueKind}");
                     return;
                 }

                 if (dataArray.ValueKind == JsonValueKind.Array)
                 {
                     Logger.Twitter($"[SearchAndParse] Parsing array with {dataArray.GetArrayLength()} items");
                     
                     foreach (var item in dataArray.EnumerateArray())
                     {
                         // Parse author/handle
                         string handle = "";
                         if (item.TryGetProperty("author", out var authProp)) handle = authProp.GetString() ?? "";
                         else if (item.TryGetProperty("handle", out var handProp)) handle = handProp.GetString() ?? "";

                         // Parse content
                         string content = "";
                         if (item.TryGetProperty("content", out var contProp)) content = contProp.GetString() ?? "";
                         else if (item.TryGetProperty("text", out var textProp)) content = textProp.GetString() ?? "";
                         
                         // STRICT FILTER: If contains Telegram/Discord/Private links, SKIP entirely
                         // This prevents spam analysis content from being used
                         if (ContentQualityGuard.ContainsPrivateLinks(content))
                         {
                             Logger.Twitter($"🚫 [BLOCKED] Spam-blocked influencer content from @{handle}: Contains Telegram/Discord/Private links");
                             continue;
                         }

                         // Parse follower count (optional field)
                         int followerCount = 0;
                         if (item.TryGetProperty("followerCount", out var fcProp))
                         {
                             followerCount = fcProp.GetInt32();
                         }

                         // Parse post date (optional field, ISO8601 format)
                         DateTime? postDate = null;
                         if (item.TryGetProperty("postDate", out var pdProp))
                         {
                             var dateStr = pdProp.GetString();
                             if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var parsed))
                             {
                                 postDate = parsed;
                             }
                         }

                         results.Add(new InfluencerPost
                        {
                            Handle = handle,
                            Content = content,
                            Engagement = item.TryGetProperty("engagement", out var engProp) ? engProp.GetInt32() : 0,
                            RelevanceScore = item.TryGetProperty("relevance_score", out var relProp) ? relProp.GetInt32() : 0,
                            Url = item.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "",
                            FollowerCount = followerCount,
                            PostDate = postDate ?? DateTime.MinValue,
                            ImageUrl = item.TryGetProperty("imageUrl", out var imgProp) ? imgProp.GetString() : (item.TryGetProperty("image", out var iProp) ? iProp.GetString() : null)
                        });
                     }
                 }
                 else
                 {
                     Logger.Twitter($"[SearchAndParse] Data property is not an array: {dataArray.ValueKind}");
                 }
             }
             catch (JsonException jex)
             {
                 Logger.Twitter($"[SearchAndParse] JSON Parse Error: {jex.Message}");
             }
        }
        
        private string BuildSearchQuery(string symbol, string marketType)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return "";

            // Eğer sembol zaten bir operatör içeriyorsa (from:, min_faves:, list: vb.) 
            // veya çoklu kelime ise genişletme yapma, direkt kullan.
            bool isComplex = symbol.Contains(":") || symbol.Contains(" ") || symbol.Contains("\"");
            
            var terms = new List<string>();
            if (isComplex)
            {
                terms.Add(symbol);
            }
            else
            {
                terms.Add(symbol);
                terms.Add($"${symbol.Replace("$", "").Replace("#", "")}");
                terms.Add($"#{symbol.Replace("$", "").Replace("#", "")}");

                // For Crypto, also add base symbol (e.g. BTC for BTCUSDT)
                if (marketType == "Kripto" && symbol.EndsWith("USDT"))
                {
                    string baseSymbol = symbol.Replace("USDT", "");
                    if (baseSymbol.Length >= 3)
                    {
                        terms.Add(baseSymbol);
                        terms.Add($"${baseSymbol}");
                        terms.Add($"#{baseSymbol}");
                    }
                }
            }

            string joinedTerms = terms.Count > 1 ? "(" + string.Join(" OR ", terms.Distinct()) + ")" : terms.First();
            
            // Build platform-specific search query
            return marketType switch
            {
                "Kripto" => $"{joinedTerms} (analiz OR grafik OR chart OR yorum OR teknik)",
                "BIST" => $"{joinedTerms} (analiz OR grafik OR bilanço OR teknik OR yorum)",
                "Forex" => $"{joinedTerms} (analysis OR chart OR forecast OR technique)",
                _ => $"{joinedTerms} (analiz OR chart)"
            };
        }

        private string BuildCryptoQuery(string symbol)
        {
            // Smart Query Builder for Crypto
            // e.g. ETHUSDT -> (ETHUSDT OR $ETHUSDT OR #ETHUSDT OR ETH OR $ETH OR #ETH)
            
            var terms = new List<string>();
            terms.Add(symbol);
            terms.Add($"${symbol}");
            terms.Add($"#{symbol}");

            if (symbol.EndsWith("USDT"))
            {
                string baseSymbol = symbol.Replace("USDT", "");
                if (baseSymbol.Length >= 3) // Avoid too short items
                {
                    terms.Add(baseSymbol);
                    terms.Add($"${baseSymbol}");
                    terms.Add($"#{baseSymbol}");
                }
            }

            string joinedTerms = "(" + string.Join(" OR ", terms) + ")";
            return $"{joinedTerms} (analiz OR grafik OR chart OR yorum) min_faves:10 filter:safe";
        }

        /// <summary>
        /// Get top 10 gainers for the day
        /// </summary>
        public async Task<List<StockData>> GetTopGainersAsync()
        {
            try
            {
                string args = $"\"{_scriptPath}\" get_top_gainers";
                string json = await RunPythonScript(args);
                
                var result = JsonSerializer.Deserialize<SocialIntelStockListResult>(json);
                if (result != null && result.status == "success")
                {
                    return result.data ?? new List<StockData>();
                }
                return new List<StockData>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Top Gainers Error: {ex.Message}");
                return new List<StockData>();
            }
        }

        /// <summary>
        /// Influencer'ların bir sembol hakkındaki son analizlerini topla
        /// </summary>
        public async Task<List<InfluencerPost>> FindInfluencerAnalyses(string symbol, string market, List<string>? vipHandles = null)
        {
            var posts = new List<InfluencerPost>();
            try
            {
                // Dedup check (Avoid duplicate searches within 30s)
                string cacheKey = $"{symbol}|{market}";
                if (_lastSearchTimes.TryGetValue(cacheKey, out DateTime lastTime) && (DateTime.Now - lastTime).TotalSeconds < 30)
                {
                    Logger.Twitter($"🛡️ Arama deduplikasyonu: {symbol} için son arama üzerinden <30s geçti. Kısayol.");
                    return posts;
                }
                _lastSearchTimes[cacheKey] = DateTime.Now;

                // Selenium/Python Primary Method (WebView2 disabled due to technical issues)
                Logger.Twitter($"🐍 Python (Selenium) ile fenomen araştırması başlatılıyor: {symbol}...");
                
                var pythonResults = new List<InfluencerPost>();
                // Eğer VIP handles varsa önce onlara bak
                if (vipHandles != null && vipHandles.Count > 0)
                {
                    foreach (var handle in vipHandles.Take(5))
                    {
                         await FetchInfluencerPostsFromPython(symbol, market, handle, pythonResults);
                         if (pythonResults.Count >= 3) break;
                    }
                }
                
                // VIP'lerde yoksa genel arama yap
                if (pythonResults.Count == 0)
                {
                    await FetchInfluencerPostsFromPython(symbol, market, null, pythonResults);
                }

                if (pythonResults.Count > 0)
                {
                    posts.AddRange(pythonResults);
                    Logger.Twitter($"✅ Python fallback tamamlandı: {pythonResults.Count} analiz bulundu.");
                }

                // WebView2 search sonucu döndür
                Logger.Twitter($"✅ FindInfluencerAnalyses tamamlandı: {symbol} için {posts.Count} analiz toplandı");
                return posts;
            }
            catch (Exception ex)
            {
             Logger.Twitter($"❌ FindInfluencerAnalyses error: {ex.Message}");
                return posts;
            }
        }

        private readonly List<InfluencerPost> _lastInfluencerPosts = new List<InfluencerPost>();
        private readonly ConcurrentDictionary<string, DateTime> _lastSearchTimes = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private async Task FetchInfluencerPostsFromPython(string symbol, string market, string? handle, List<InfluencerPost> sink)
        {
            try
            {
                // Clean @ symbol from handle for proper from: syntax
        string cleanHandle = handle?.TrimStart('@') ?? "";
        string query = string.IsNullOrWhiteSpace(cleanHandle) ? symbol : $"from:{cleanHandle} {symbol}";
                string base64Query = Convert.ToBase64String(Encoding.UTF8.GetBytes(query));
                string visibilityFlag = IsVisibleMode ? " --visible" : "";
                string args = $"\"{_scriptPath}\" search_influencer --query \"{base64Query}\" --base64 --market \"{market}\"{visibilityFlag}";
                string json = await RunPythonScript(args);

                using var doc = JsonDocument.Parse(json);
                JsonElement data;
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    data = doc.RootElement;
                }
                else if (doc.RootElement.TryGetProperty("data", out var dataProp))
                {
                    data = dataProp;
                }
                else
                {
                    return;
                }

                if (data.ValueKind != JsonValueKind.Array) return;

                foreach (var item in data.EnumerateArray())
                {
                    string author = item.TryGetProperty("author", out var a) ? a.GetString() ?? (handle ?? "") : (handle ?? "");
                    string content = item.TryGetProperty("content", out var c) ? c.GetString() ?? string.Empty : string.Empty;
                    string url = item.TryGetProperty("url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
                    int engagement = item.TryGetProperty("engagement", out var e) ? e.GetInt32() : 0;
                    int followers = item.TryGetProperty("followerCount", out var fc) ? fc.GetInt32() : 0;
                    DateTime postDate = DateTime.Now;
                    if (item.TryGetProperty("postDate", out var pd) && DateTime.TryParse(pd.GetString(), out var parsed))
                    {
                        postDate = parsed;
                    }

                    if (string.IsNullOrWhiteSpace(content) || content.Length < 20) 
                    {
                        Logger.Twitter($"⚠️ SKIPPED (Length<20): {content}");
                        continue;
                    }

                    // TELEGRAM/DISCORD LINK FILTER - BUT ALLOW FOR GURU (@EFELERiiNEFESi3)
                    bool isGuru = author?.Equals("EFELERiiNEFESi3", StringComparison.OrdinalIgnoreCase) ?? false;
                    if (!isGuru && ContentQualityGuard.ContainsPrivateLinks(content))
                    {
                        Logger.Twitter($"🚫 BLOCKED (Spam): @{author} - Contains Telegram/Discord/Private links");
                        continue;
                    }
                    
                    if (isGuru && ContentQualityGuard.ContainsPrivateLinks(content))
                    {
                        Logger.Twitter($"✅ GURU EXCEPTION: @{author} - Telegram/Discord links ALLOWED for Hoca");
                    }

                    Logger.Twitter($"✅ ADDING to Sink: {postDate} | Len:{content.Length}");

                    sink.Add(new InfluencerPost
                    {
                        Handle = string.IsNullOrWhiteSpace(author) ? (handle ?? "") : author,
                        Content = content.Length > 500 ? content.Substring(0, 497) + "..." : content,
                        Url = url,
                        Engagement = engagement,
                        RelevanceScore = item.TryGetProperty("relevance_score", out var rs) ? rs.GetInt32() : 0,
                        FollowerCount = followers,
                        PostDate = postDate,
                        ImageUrl = item.TryGetProperty("imageUrl", out var img) ? img.GetString() : (item.TryGetProperty("image", out var i) ? i.GetString() : null)
                    });

                    if (sink.Count >= 20) break;
                }
            }
            catch (Exception ex)
            {
                Logger.Twitter($"⚠️ Influencer search error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get top 10 losers for the day
        /// </summary>
        public async Task<List<StockData>> GetTopLosersAsync()
        {
            try
            {
                string args = $"\"{_scriptPath}\" get_top_losers";
                string json = await RunPythonScript(args);
                
                var result = JsonSerializer.Deserialize<SocialIntelStockListResult>(json);
                if (result != null && result.status == "success")
                {
                    return result.data ?? new List<StockData>();
                }
                return new List<StockData>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Top Losers Error: {ex.Message}");
                return new List<StockData>();
            }
        }

        /// <summary>
        /// Get top 10 highest volume stocks
        /// </summary>
        public async Task<List<StockData>> GetTopVolumeAsync()
        {
            try
            {
                string args = $"\"{_scriptPath}\" get_top_volume";
                string json = await RunPythonScript(args);
                
                var result = JsonSerializer.Deserialize<SocialIntelStockListResult>(json);
                if (result != null && result.status == "success")
                {
                    return result.data ?? new List<StockData>();
                }
                return new List<StockData>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Top Volume Error: {ex.Message}");
                return new List<StockData>();
            }
        }

        /// <summary>
        /// Background process to scan influencers and update Knowledge Base
        /// </summary>
        public async Task PerformDeepScanAsync(Action<string> logger)
        {
            if (!await _deepScanLock.WaitAsync(0))
            {
                logger("⚠️ Deep Scan skipped (already running).");
                return;
            }

            try
            {
                if (_memoryEngine == null || _influencerControl == null)
                {
                    logger("❌ Servisler (Memory/Influencer) başlatılamadı.");
                    return;
                }

                logger("🚀 Derin Hafıza Taraması (Deep Scan) Başlatıldı...");

                // Get all influencers from database
                var allInfluencers = _influencerControl.GetAllInfluencers();
                if (allInfluencers.Count == 0)
                {
                    logger("ℹ️ Veritabanında fenomen bulunamadı. Tarama yapılamıyor.");
                    return;
                }
                
                // Shuffle and pick 10 (randomized coverage)
                var rng = new Random();
                var targets = allInfluencers.OrderBy(x => rng.Next()).Take(10).ToList();

                int totalNewTweets = 0;
                int processedProfiles = 0;
                int removedProfiles = 0;

                foreach (var influencer in targets)
                {
                    string handle = influencer.Handle.TrimStart('@');
                    
                    // Determine category (for Python market parameter)
                    string category = "BIST";
                    // Find actual category from DB to be precise
                    foreach(var cat in new[] { "BIST", "CRYPTO", "FOREX" }) {
                        if (_influencerControl.GetInfluencers(cat).Any(i => i.Handle == influencer.Handle)) {
                            category = cat;
                            break;
                        }
                    }

                    logger($"🔍 Taranıyor: @{handle} ({category})...");

                    // Build smart query for this handle
                    // Python will use find_influencer_tweets_from_timeline which handles 'from:'
                    string query = $"from:{handle} include:nativeretweets";
                    
                    // Call search mechanism
                    var posts = await FindInfluencerAnalyses(query, category);
                    
                    if (posts == null || posts.Count == 0)
                    {
                        // CHECK IF ACCOUNT EXISTS (Simulated check via FindInfluencerAnalyses result logic)
                        // If we didn't get any result, but FindInfluencerAnalyses didn't throw, 
                        // we need to be careful. Let's assume for now Python will return a special marker.
                        // For safety, only remove if we are SURE.
                        logger($"   ℹ️ @{handle}: Yeni veri bulunamadı.");
                    }
                    else
                    {
                        // Check for 'Account Not Found' marker (needs Python side update to return this)
                        var firstPost = posts[0];
                        if (firstPost.Content == "ACC_NOT_FOUND" || firstPost.Handle == "ERROR_404")
                        {
                            logger($"   ❌ @{handle} bulunamadı veya askıya alınmış! Veritabanından siliniyor...");
                            _influencerControl.DeleteInfluencer(category, influencer.Handle);
                            removedProfiles++;
                            continue;
                        }

                        int count = 0;
                        foreach (var post in posts)
                        {
                            bool learnt = _memoryEngine.Learn(post);
                            if (learnt) count++;
                        }
                        
                        if (count > 0) logger($"   ✅ @{handle}: +{count} yeni bilgi hafızaya alındı.");
                        else logger($"   ℹ️ @{handle}: Hafızada zaten mevcut.");
                        
                        totalNewTweets += count;
                    }
                    
                    processedProfiles++;
                    await Task.Delay(4000 + rng.Next(2000)); 
                }

                logger($"🏁 Tarama Tamamlandı. {processedProfiles} profil taranan, {removedProfiles} silinen. +{totalNewTweets} yeni bilgi.");
                _memoryEngine.Save(); 
            }
            catch (Exception ex)
            {
                logger($"❌ Deep Scan Hatası: {ex.Message}");
            }
            finally
            {
                _deepScanLock.Release();
            }
        }
    }
}


