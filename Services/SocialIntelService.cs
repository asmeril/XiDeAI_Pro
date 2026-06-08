
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
using System.Net.Http;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro.Services
{
    public class SocialIntelResult
    {
        public string status { get; set; } = "";
        public string handle { get; set; } = "";
        public string link { get; set; } = "";
        public string tweet_url { get; set; } = "";
        public string text { get; set; } = "";
        public string message { get; set; } = "";  // Python returns 'message' for errors
        public int posted_count { get; set; } = 0;
        public int total_chunks { get; set; } = 0;
        
        // Helper: Get error message from either property
        public string ErrorMessage => !string.IsNullOrEmpty(message) ? message : text;
    }

    public class SocialIntelNewsItem
    {
        public string source { get; set; } = "";
        public string text { get; set; } = "";
        public string time { get; set; } = "";
        public string url { get; set; } = ""; // EKLENDİ: Gerçek tweet linki için
        public string? media { get; set; } // v4.6.18: Visual Hook - Tweet görseli
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
        public string Url { get; set; } = string.Empty;
        public string? OriginalAuthor { get; set; }
        public bool IsRetweet { get; set; }
        public int Engagement { get; set; } = 0;
        public int RelevanceScore { get; set; } = 0; // NEW: Track analysis quality
        public int FollowerCount { get; set; } = 0;
        public DateTime PostDate { get; set; } = DateTime.MinValue;
        public string? ImageUrl { get; set; } // Supporting guru tables
        public string Market { get; set; } = ""; // EKLENDİ
        public string Symbol { get; set; } = ""; // EKLENDİ
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

    public class SocialIntelReply
    {
        public string handle { get; set; } = "";
        public string text { get; set; } = "";
        public string url { get; set; } = "";
    }

    public class SocialIntelReplyResult
    {
        public string status { get; set; } = "";
        public List<SocialIntelReply> data { get; set; } = new List<SocialIntelReply>();
        public string message { get; set; } = "";
    }

    public class SocialIntelRetweeter
    {
        public string handle { get; set; } = "";
    }

    public class SocialIntelRetweetersResult
    {
        public string status { get; set; } = "";
        public List<SocialIntelRetweeter> data { get; set; } = new List<SocialIntelRetweeter>();
        public string message { get; set; } = "";
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
        // private bool _isFirstMetaRun = true; // Deep Scan Flag - Removed unused
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
        private static bool _authMethodLogged = false;

        // v4.4.1: Search Rate Limiting to prevent X account locks
        private static DateTime _lastSearchCompletedUtc = DateTime.MinValue;
        private static readonly TimeSpan SearchCooldown = TimeSpan.FromSeconds(20);

        // v4.6.14: Timeout raised to 10min - thread posting takes 4-12min per run.
        // 60s timeout was causing C# to abort TCP connection before Python finished,
        // resulting in WinError 10053 (ConnectionAbortedError) on every thread post.
        private static readonly HttpClient _daemonClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        private static Process? _daemonProcess;
        private static bool _daemonAvailable = false;
        private const string DAEMON_URL = "http://127.0.0.1:5580";
        public bool UseDaemon { get; set; } = true; // Enable daemon by default

        /// <summary>Start the X daemon process if not running</summary>
        public async Task StartDaemonAsync()
        {
            if (_daemonProcess != null && !_daemonProcess.HasExited)
            {
                _daemonAvailable = await CheckDaemonHealthAsync();
                return;
            }

            try
            {
                string daemonScript = Path.Combine(Path.GetDirectoryName(_scriptPath) ?? "", "x_daemon.py");
                if (!File.Exists(daemonScript))
                {
                    Logger.Sys($"[Daemon] Script not found: {daemonScript}");
                    _daemonAvailable = false;
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{daemonScript}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                if (IsVisibleMode)
                    psi.EnvironmentVariables["X_VISIBLE"] = "true";

                _daemonProcess = Process.Start(psi);
                
                // v4.6.12: ASYNC STREAM CONSUMPTION TO PREVENT OS BUFFER DEADLOCK (<4KB pipe freeze)
                if (_daemonProcess != null)
                {
                    _daemonProcess.OutputDataReceived += (sender, e) => {
                        // Consuming standard output prevents the internal OS pipe buffer from filling up
                    };
                    _daemonProcess.ErrorDataReceived += (sender, e) => {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Logger.Sys($"[Daemon Log] {e.Data}");
                        }
                    };
                    
                    _daemonProcess.BeginOutputReadLine();
                    _daemonProcess.BeginErrorReadLine();
                }

                Logger.Sys("[Daemon] Starting X daemon...");

                // Wait for daemon to start
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(500);
                    if (await CheckDaemonHealthAsync())
                    {
                        _daemonAvailable = true;
                        Logger.Sys("[Daemon] X daemon is ready");
                        return;
                    }
                }

                Logger.Sys("[Daemon] Daemon startup timeout");
                _daemonAvailable = false;
            }
            catch (Exception ex)
            {
                Logger.Sys($"[Daemon] Start error: {ex.Message}");
                _daemonAvailable = false;
            }
        }

        /// <summary>Stop the daemon process</summary>
        public void StopDaemon()
        {
            try
            {
                // Send shutdown command
                _ = _daemonClient.PostAsync($"{DAEMON_URL}/shutdown", null).Result;
            }
            catch { }

            try
            {
                if (_daemonProcess != null && !_daemonProcess.HasExited)
                {
                    _daemonProcess.Kill(true);
                    _daemonProcess = null;
                }
            }
            catch { }

            _daemonAvailable = false;
            Logger.Sys("[Daemon] Stopped");
        }

        /// <summary>Check if daemon is healthy</summary>
        private async Task<bool> CheckDaemonHealthAsync()
        {
            try
            {
                var response = await _daemonClient.GetAsync($"{DAEMON_URL}/health");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return json.Contains("\"status\"") && json.Contains("ok");
                }
            }
            catch { }
            return false;
        }

        /// <summary>Send request to daemon</summary>
        private async Task<string> DaemonRequestAsync(string endpoint, object? payload = null)
        {
            try
            {
                var content = payload != null 
                    ? new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                    : null;

                var response = await _daemonClient.PostAsync($"{DAEMON_URL}{endpoint}", content);
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                return $"{{\"status\": \"error\", \"message\": \"Daemon request failed: {ex.Message}\"}}";
            }
        } 

        // v3.7.2: Common words that conflict with stock symbols
        private static readonly HashSet<string> COMMON_STOCK_WORDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
        { 
            "LOGO", "INFO", "LINK", "DATA", "SAFE", "ARK", "NEAR", "BIST", "GOLD", "OIL", "GAS" 
        };

        // NOTE: WebView2 Bridge events deprecated - Using Selenium/Python exclusively
        #pragma warning disable CS0067
        public event Func<string, string?, Task<SocialIntelResult>>? OnPostTweetRequested;
        public event Func<List<string>, string?, Task<SocialIntelResult>>? OnPostThreadRequested;
        public event Func<string, string, string, List<string>?, Task<List<InfluencerPost>>>? OnSearchRequested;
        public event Func<Task<ProfileStats?>>? OnGetStatsRequested;
        public event Func<Task<string[]>>? OnGetTrendsRequested;
        public event Func<string, string, Task<SocialIntelResult>>? OnReplyRequested;

        public event Func<string[], Task<SocialIntelInteractResult>>? OnInteractTargetsRequested;
        // Phase 1 (Hive): Event for detecting content during deep scan (triggered for Meta-Teacher analysis)
        public event Func<InfluencerPost, Task>? OnDeepScanPostDetected;
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

                // python social_intel.py search_influencer --query SYMBOL --market MARKET
                // Credentials passed via Environment Variables for security (Hiding from Task Manager)
                string safeSymbol = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(symbol ?? string.Empty));
                string args = $"\"{_scriptPath}\" search_influencer --query \"{safeSymbol}\" --base64 --market {marketType}";
                
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

        public async Task<SocialIntelReplyResult?> FetchReplies(string tweetUrl)
        {
            try
            {
                var cfg = ConfigManager.Current;
                string args = $"\"{_scriptPath}\" fetch_replies --url \"{tweetUrl}\"";
                
                string json = await RunPythonScript(args, cfg.XLoginUser, cfg.XLoginPass);
                if (string.IsNullOrEmpty(json) || json == "{}") return null;
                
                return JsonSerializer.Deserialize<SocialIntelReplyResult>(json);
            }
            catch (Exception ex)
            {
                Logger.Sys($"❌ FetchReplies HATA: {ex.Message}");
                return null;
            }
        }

        public async Task<SocialIntelRetweetersResult?> FetchRetweeters(string tweetUrl)
        {
            try
            {
                var cfg = ConfigManager.Current;
                string args = $"\"{_scriptPath}\" fetch_retweeters --url \"{tweetUrl}\"";
                
                string json = await RunPythonScript(args, cfg.XLoginUser, cfg.XLoginPass);
                if (string.IsNullOrEmpty(json) || json == "{}") return null;
                
                return JsonSerializer.Deserialize<SocialIntelRetweetersResult>(json);
            }
            catch (Exception ex)
            {
                Logger.Sys($"❌ FetchRetweeters HATA: {ex.Message}");
                return null;
            }
        }

        private async Task<string> RunPythonScript(string arguments, string? user = null, string? pass = null, int timeoutSeconds = 90)
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
                psi.EnvironmentVariables["PYTHONUTF8"] = "1";
            }
            else
            {
                psi.EnvironmentVariables["PYTHONUTF8"] = "1";
            }

            using var process = Process.Start(psi);
            if (process == null) return "{}";

            // Timeout for X automation tasks
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            string output = "";
            string error = "";

            try
            {
                // Read both streams asynchronously
                var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
                var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

                // Wait for the process to exit OR the cancellation token to trigger
                await process.WaitForExitAsync(cts.Token);

                // If process exited normally, wait briefly for streams to finish
                try {
                    await Task.WhenAll(outputTask, errorTask).WaitAsync(TimeSpan.FromSeconds(2));
                } catch { }

                output = outputTask.IsCompletedSuccessfully ? outputTask.Result.Trim() : "";
                error = errorTask.IsCompletedSuccessfully ? errorTask.Result.Trim() : "";
            }
            catch (OperationCanceledException)
            {
                Logger.Twitter($"⚠️ X otomasyon işlemi ZAMAN AŞIMINA uğradı ({timeoutSeconds}s). Süreç sonlandırılıyor...");
                try { process.Kill(true); } catch { }
                return "{\"status\": \"error\", \"message\": \"Process timeout (" + timeoutSeconds + "s)\"}";
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
            if (text.Length > 280)
            {
                Logger.Twitter($"❌ Tekil tweet metni 280 karakteri aşıyor ({text.Length}); otomatik thread dönüşümü kapalı.");
                return new SocialIntelResult { status = "error", message = "Tweet 280 karakteri aşıyor; thread gönderimi açıkça kullanılmalı." };
            }

            // Internal WebView2 bridge can report success without a verified /status/ URL.
            // Use Playwright as the canonical posting engine until the bridge has URL verification.
            bool useInternalBridge = false;
            if (useInternalBridge && OnPostTweetRequested != null)
            {
                try
                {
                    var res = await OnPostTweetRequested.Invoke(text, mediaPath);
                    if (res != null && res.status == "success")
                    {
                        if (HasVerifiedTweetUrl(res))
                        {
                            ConfigManager.AddWebUsage();
                            return res;
                        }
                        Logger.Twitter("⚠️ Dahili tweet success doğrulanamadı (/status/ URL yok). Playwright fallback deneniyor...");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Twitter($"⚠️ Dahili tweet gönderim hatası: {ex.Message}");
                }
            }

            // 2. Playwright Subprocess Fallback (X-Hive Engine)
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

                string playwrightScript = Path.Combine(Path.GetDirectoryName(_scriptPath) ?? "", "playwright_daemon.py");
                string visibilityFlag = IsVisibleMode ? " --visible" : "";
                string args = $"\"{playwrightScript}\" post_tweet --file \"{tempFile}\"{visibilityFlag}";
                
                string json = await RunPythonScript(args, null, null, 180); // single tweet can need retries on X compose
                var result = JsonSerializer.Deserialize<SocialIntelResult>(json);
                
                if (result != null && result.status == "success")
                {
                     if (!HasVerifiedTweetUrl(result))
                     {
                         result.status = "error";
                         result.message = "Tweet success doğrulanamadı: /status/ URL yok.";
                     }
                     else
                     {
                         ConfigManager.AddWebUsage();
                     }
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

        public async Task<SocialIntelResult> ReplyAsync(string tweetUrl, string text)
        {
            try
            {
                string args = $"\"{_scriptPath}\" reply_to_tweet --url \"{tweetUrl}\" --text \"{text}\"";
                string json = await RunPythonScript(args);
                var result = JsonSerializer.Deserialize<SocialIntelResult>(json);
                return result ?? new SocialIntelResult { status = "error", text = "Empty response" };
            }
            catch (Exception ex)
            {
                return new SocialIntelResult { status = "error", message = ex.Message };
            }
        }


        public async Task<SocialIntelResult> PostThreadAsync(List<string> tweets, string? mediaPath = null)
        {
            tweets = (tweets ?? new List<string>())
                .Select(t => t?.Trim() ?? string.Empty)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (tweets.Count == 0)
            {
                return new SocialIntelResult { status = "error", message = "Thread payload is empty after normalization." };
            }

            // Internal WebView2 bridge can close/type without actually creating a post.
            // Use Playwright as the canonical posting engine until it returns verified status URLs.
            bool useInternalBridge = false;
            if (useInternalBridge && OnPostThreadRequested != null)
            {
                try
                {
                    Logger.Twitter("🔄 Dahili (WebView2) thread posting başlatılıyor...");
                    var res = await OnPostThreadRequested.Invoke(tweets, mediaPath);
                    if (res != null)
                    {
                        if (res.status == "success") 
                        {
                            if (HasVerifiedThreadResult(res, tweets.Count))
                            {
                                Logger.Twitter("✅ Dahili thread gönderimi doğrulandı.");
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
                            Logger.Twitter("⚠️ Dahili thread success doğrulanamadı (/status/ veya parça sayısı yok). Playwright fallback deneniyor...");
                        }
                        else if (res.status == "cancelled")
                        {
                            Logger.Twitter("⚠️ Dahili thread kullanıcı tarafından iptal edildi.");
                            return res;
                        }
                        // If it's an error, we fall through to Python fallback
                        Logger.Twitter($"⚠️ Dahili thread hatası ({res.status}): {res.ErrorMessage}. Playwright fallback deneniyor...");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Twitter($"⚠️ Dahili thread kritik hatası: {ex.Message}");
                }
            }

            // 2. Playwright Subprocess Fallback (X-Hive Engine)
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
                    media = mediaPath,
                    preserve_chunks = true
                };

                // Serialize to JSON and write to temp file
                string jsonContent = JsonSerializer.Serialize(payload);
                tempFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(tempFile, jsonContent);

                // Pass the FILE PATH to Playwright
                string playwrightScript = Path.Combine(Path.GetDirectoryName(_scriptPath) ?? "", "playwright_daemon.py");
                string visibilityFlag = IsVisibleMode ? " --visible" : "";
                string args = $"\"{playwrightScript}\" post_thread --file \"{tempFile}\"{visibilityFlag}";
                
                string json = await RunPythonScript(args, null, null, 480); // Extended timeout for reply-chain thread posting (4 tweets × ~60s each + safety margin)
                
                var result = JsonSerializer.Deserialize<SocialIntelResult>(json);
                _lastPostUtc = DateTime.UtcNow;

                if (result != null && result.status == "success")
                {
                    if (!HasVerifiedThreadResult(result, tweets.Count))
                    {
                        result.status = "error";
                        result.message = "Thread success doğrulanamadı: /status/ URL veya tam parça sayısı yok.";
                    }
                    else
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

        private static bool HasVerifiedTweetUrl(SocialIntelResult result)
        {
            string url = !string.IsNullOrWhiteSpace(result.tweet_url) ? result.tweet_url : result.link;
            return !string.IsNullOrWhiteSpace(url) && url.Contains("/status/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasVerifiedThreadResult(SocialIntelResult result, int expectedCount)
        {
            if (!HasVerifiedTweetUrl(result)) return false;
            if (expectedCount <= 1) return true;
            return result.posted_count >= expectedCount && result.total_chunks >= expectedCount;
        }

        public async Task<SocialIntelResult> ReplyToTweetAsync(string url, string text)
        {
            // SECURITY: Prevent standalone tweets on profile/main pages
            if (string.IsNullOrEmpty(url) || !url.Contains("/status/"))
            {
                Logger.FanZone($"⚠️ [SocialIntel] Geçersiz cevap linki (Status içermiyor): {url}");
                return new SocialIntelResult { status = "error", text = "Invalid tweet URL for reply." };
            }

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
                Logger.FanZone($"[SocialIntel] Reply Raw Output: {json}");
                
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
        
        public async Task<SocialIntelResult> LikeTweet(string url)
        {
            try
            {
                Logger.FanZone($"[SocialIntel] LikeTweet isteniyor: {url}");
                await _postLock.WaitAsync();
                var now = DateTime.UtcNow;
                var delta = now - _lastPostUtc;
                if (delta < PostCooldown)
                {
                    try { await Task.Delay(PostCooldown - delta); } catch { }
                }

                // v4.4.0: Try daemon first
                if (UseDaemon && _daemonAvailable)
                {
                    try
                    {
                        string daemonJson = await DaemonRequestAsync("/like", new { url });
                        var daemonResult = JsonSerializer.Deserialize<SocialIntelResult>(daemonJson);
                        if (daemonResult?.status == "success")
                        {
                            Logger.FanZone($"✅ [DAEMON] Like Başarılı: {url}");
                            _lastPostUtc = DateTime.UtcNow;
                            return daemonResult;
                        }
                    }
                    catch { }
                }

                // Fallback to subprocess
                string args = $"\"{_scriptPath}\" like_tweet --url \"{url}\"";
                string json = await RunPythonScript(args);
                
                Logger.FanZone($"[SocialIntel] LikeTweet Raw Output: {json}");

                var result = JsonSerializer.Deserialize<SocialIntelResult>(json);
                if (result != null && result.status == "success")
                {
                     // Update counters if needed
                     Logger.FanZone($"✅ Like Başarılı: {url}");
                }
                else
                {
                     Logger.FanZone($"❌ Like Başarısız: {result?.ErrorMessage ?? "Unknown"}");
                }

                _lastPostUtc = DateTime.UtcNow;
                return result ?? new SocialIntelResult { status = "error", text = "Empty response" };
            }
            catch (Exception ex)
            {
                Logger.FanZone($"❌ Like Exception: {ex.Message}");
                return new SocialIntelResult { status = "error", text = ex.Message };
            }
            finally
            {
                try { _postLock.Release(); } catch { }
            }
        }

        public async Task<SocialIntelResult> Retweet(string url)
        {
            try
            {
                Logger.FanZone($"[SocialIntel] Retweet isteniyor: {url}");
                await _postLock.WaitAsync();
                var now = DateTime.UtcNow;
                var delta = now - _lastPostUtc;
                if (delta < PostCooldown)
                {
                    try { await Task.Delay(PostCooldown - delta); } catch { }
                }

                string args = $"\"{_scriptPath}\" retweet --url \"{url}\"";
                string json = await RunPythonScript(args);
                
                Logger.FanZone($"[SocialIntel] Retweet Raw Output: {json}");

                var result = JsonSerializer.Deserialize<SocialIntelResult>(json);
                 if (result != null && result.status == "success")
                {
                     Logger.FanZone($"✅ Retweet Başarılı: {url}");
                }
                else
                {
                     Logger.FanZone($"❌ Retweet Başarısız: {result?.ErrorMessage ?? "Unknown"}");
                }
                
                _lastPostUtc = DateTime.UtcNow;
                return result ?? new SocialIntelResult { status = "error", text = "Empty response" };
            }
            catch (Exception ex)
            {
                Logger.FanZone($"❌ Retweet Exception: {ex.Message}");
                return new SocialIntelResult { status = "error", text = ex.Message };
            }
            finally
            {
                try { _postLock.Release(); } catch { }
            }
        }

        /// <summary>
        /// v4.5.3: Quote Retweet with custom text
        /// </summary>
        public async Task<SocialIntelResult> QuoteRetweet(string url, string quoteText)
        {
            try
            {
                Logger.FanZone($"[SocialIntel] Quote RT isteniyor: {url} - \"{quoteText}\"");
                await _postLock.WaitAsync();
                var now = DateTime.UtcNow;
                var delta = now - _lastPostUtc;
                if (delta < PostCooldown)
                {
                    try { await Task.Delay(PostCooldown - delta); } catch { }
                }

                // Escape quotes in text
                string safeText = quoteText.Replace("\"", "\\\"");
                string args = $"\"{_scriptPath}\" quote_retweet --url \"{url}\" --text \"{safeText}\"";
                string json = await RunPythonScript(args);
                
                Logger.FanZone($"[SocialIntel] QuoteRT Raw Output: {json}");

                var result = JsonSerializer.Deserialize<SocialIntelResult>(json);
                if (result != null && result.status == "success")
                {
                    Logger.FanZone($"✅ Quote RT Başarılı: {url}");
                }
                else
                {
                    Logger.FanZone($"❌ Quote RT Başarısız: {result?.ErrorMessage ?? "Unknown"}");
                }
                
                _lastPostUtc = DateTime.UtcNow;
                return result ?? new SocialIntelResult { status = "error", text = "Empty response" };
            }
            catch (Exception ex)
            {
                Logger.FanZone($"❌ Quote RT Exception: {ex.Message}");
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
                 string args = $"\"{_scriptPath}\" interact_with_targets --targets \"{usersArg}\" --max-age-hours 6";
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
        public async Task<string?> FindTwitterHandle(string name)
        {
            try
            {
                // v4.4.0: Try daemon first
                if (UseDaemon && _daemonAvailable)
                {
                    string daemonJson = await DaemonRequestAsync("/find_handle", new { name });
                    using var doc = JsonDocument.Parse(daemonJson);
                    if (doc.RootElement.TryGetProperty("status", out var status) && status.GetString() == "success")
                    {
                        if (doc.RootElement.TryGetProperty("handle", out var handle))
                        {
                            return handle.GetString();
                        }
                    }
                    // If daemon returned error, fall through to subprocess
                }

                // Fallback to subprocess
                string args = $"\"{_scriptPath}\" find_handle --name \"{name}\"";
                string json = await RunPythonScript(args);
                
                using var fallbackDoc = JsonDocument.Parse(json);
                if (fallbackDoc.RootElement.TryGetProperty("status", out var st) && st.GetString() == "success")
                {
                    if (fallbackDoc.RootElement.TryGetProperty("handle", out var h))
                    {
                        return h.GetString();
                    }
                }
                return null;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[SocialIntel] Handle search error: {ex.Message}");
                return null;
            }
        }

        public async Task<List<string>> DiscoverInfluencers(string category)
        {
            try
            {
                string args = $"\"{_scriptPath}\" discover_influencers --category {category}";
                // Console.WriteLine($"[Discovery] Running: python {args}"); 
                string json = await RunPythonScript(args);
                
                // Console.WriteLine($"[Discovery] Response: {json}");
                
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
                                string handle = item.GetString() ?? "";
                                if (!string.IsNullOrEmpty(handle))
                                {
                                    list.Add(handle);
                                    // Auto-add to database
                                    _influencerControl?.AddInfluencer(category, handle);
                                }
                            }
                            // SaveDatabase removed (now automatic)
                            // Console.WriteLine($"[Discovery] Success: {list.Count} influencers found in {category}");
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
                Console.WriteLine($"[Discovery] Exception: {ex.Message}");
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
                string cleanSymbol = symbol.Replace("$", "").Replace("#", "").ToUpperInvariant();
                bool isCommon = COMMON_STOCK_WORDS.Contains(cleanSymbol);

                if (isCommon)
                {
                    // For common words, ONLY use # and $ prefixed versions to avoid noise
                    terms.Add($"${cleanSymbol}");
                    terms.Add($"#{cleanSymbol}");
                }
                else
                {
                    terms.Add(symbol);
                    terms.Add($"${cleanSymbol}");
                    terms.Add($"#{cleanSymbol}");
                }

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
        public async Task<List<InfluencerPost>> FindInfluencerAnalyses(string symbol, string market, List<string>? vipHandles = null, int limit = 10, string? sinceDate = null)
        {
            var posts = new List<InfluencerPost>();
            try
            {
                // Dedup check (Avoid duplicate searches within 30s) - SKIP FOR DEEP SCAN
                string scopeKey = vipHandles != null && vipHandles.Count > 0
                    ? string.Join(",", vipHandles.Take(5).Select(h => h.Trim().TrimStart('@')).OrderBy(h => h, StringComparer.OrdinalIgnoreCase))
                    : "GENERAL";
                string cacheKey = $"{symbol}|{market}|{scopeKey}|{limit}";
                if (string.IsNullOrEmpty(sinceDate) && _lastSearchTimes.TryGetValue(cacheKey, out DateTime lastTime) && (DateTime.Now - lastTime).TotalSeconds < 30)
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
                         await FetchInfluencerPostsFromPython(symbol, market, handle, pythonResults, limit, sinceDate);
                         if (pythonResults.Count >= 3) break;
                    }
                }
                
                // VIP'lerde yoksa genel arama yap
                if (pythonResults.Count == 0)
                {
                    await FetchInfluencerPostsFromPython(symbol, market, null, pythonResults, limit, sinceDate);
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
        private async Task FetchInfluencerPostsFromPython(string symbol, string market, string? handle, List<InfluencerPost> sink, int limit = 10, string? sinceDate = null)
        {
            try
            {
                // v4.4.1: Enforce search cooldown to prevent X rate limiting
                var timeSinceLastSearch = DateTime.UtcNow - _lastSearchCompletedUtc;
                if (timeSinceLastSearch < SearchCooldown)
                {
                    var waitTime = SearchCooldown - timeSinceLastSearch;
                    Logger.Twitter($"⏳ Search cooldown: Waiting {waitTime.TotalSeconds:F1}s before next search...");
                    await Task.Delay(waitTime);
                }

                // Clean @ symbol from handle for proper from: syntax
                string cleanHandle = handle?.TrimStart('@') ?? "";
                
                // v4.4.0: Try daemon first for normal searches (not deep scans)
                if (UseDaemon && _daemonAvailable && string.IsNullOrEmpty(sinceDate))
                {
                    try
                    {
                        string endpoint = string.IsNullOrEmpty(cleanHandle) ? "/search" : "/timeline";
                        var payload = string.IsNullOrEmpty(cleanHandle)
                            ? new { query = symbol, market = market, limit = limit }
                            : (object)new { handle = cleanHandle, limit = limit };
                        
                        string daemonJson = await DaemonRequestAsync(endpoint, payload);
                        using var daemonDoc = JsonDocument.Parse(daemonJson);
                        
                        if (daemonDoc.RootElement.TryGetProperty("status", out var st) && st.GetString() == "success")
                        {
                            if (daemonDoc.RootElement.TryGetProperty("data", out var daemonData) && daemonData.ValueKind == JsonValueKind.Array)
                            {
                                int count = 0;
                                foreach (var item in daemonData.EnumerateArray())
                                {
                                    try
                                    {
                                        string author = item.TryGetProperty("author", out var a) ? a.GetString() ?? "" : "";
                                        string content = item.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                                        string url = item.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                                        string postDateStr = item.TryGetProperty("postDate", out var pd) ? pd.GetString() ?? "" : "";
                                        string? imageUrl = item.TryGetProperty("imageUrl", out var img) && img.ValueKind == JsonValueKind.String ? img.GetString() : null;
                                        int engagement = item.TryGetProperty("engagement", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : 0;
                                        
                                        if (IsBadSocialResult(author, content, url)) continue;

                                        // v5.1.8: Allow short text if it contains an image (e.g., 'Efe HMA')
                                        if ((string.IsNullOrWhiteSpace(content) || content.Length < 10) && string.IsNullOrEmpty(imageUrl)) continue;
                                        
                                        sink.Add(new InfluencerPost
                                        {
                                            Handle = author,
                                            Content = content,
                                            Url = url,
                                            PostDate = DateTime.TryParse(postDateStr, out var dt) ? dt : DateTime.Now,
                                            ImageUrl = imageUrl,
                                            Engagement = engagement,
                                            Market = market,
                                            Symbol = symbol
                                        });
                                        count++;
                                    }
                                    catch { }
                                }
                                
                                if (count > 0)
                                {
                                    Logger.Twitter($"📊 [DAEMON] Found {count} posts for {symbol}");
                                    // Engagement bazlı skor güncelle
                                    foreach (var post in sink.TakeLast(count))
                                    {
                                        if (!string.IsNullOrEmpty(post.Handle) && post.Handle != "X-User" && post.Engagement > 0)
                                        {
                                            int delta = Math.Min(post.Engagement / 10, 5); // max +5 per tweet
                                            _influencerControl?.UpdateScore(post.Handle, delta);
                                        }
                                    }
                                    return; // Success via daemon
                                }
                            }
                        }
                    }
                    catch (Exception dex)
                    {
                        Logger.Twitter($"⚠️ Daemon search failed, falling back to subprocess: {dex.Message}");
                    }
                }

                // Fallback to subprocess (or for deep scans)
                // FIX: If symbol already contains "from:", it's a full query - don't duplicate
                string query = symbol.Contains("from:") ? symbol : (string.IsNullOrWhiteSpace(cleanHandle) ? symbol : (string.IsNullOrWhiteSpace(symbol) ? $"from:{cleanHandle}" : $"from:{cleanHandle} {symbol}"));
                string base64Query = Convert.ToBase64String(Encoding.UTF8.GetBytes(query));
                string visibilityFlag = IsVisibleMode ? " --visible" : "";
                
                string sinceArg = sinceDate != null ? $" --since {sinceDate}" : "";

                // v3.5 SPART STOP: Sadece standart izleme modunda (sinceDate null iken) hafıza sınırını kullan.
                // Eğer Derin Tarama (Meta-Teacher) yapılıyorsa, aradaki "boşlukları" doldurmak için sınırı DEVRE DIŞI BIRAK.
                string untilArg = "";
                if (string.IsNullOrEmpty(sinceDate) && Memory != null && !string.IsNullOrEmpty(cleanHandle))
                {
                    var knownTweets = Memory.GetKnowledgeBase().Where(t => t.Author.Equals("@" + cleanHandle, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (knownTweets.Any())
                    {
                        var newestDate = knownTweets.Max(t => t.PostDate);
                        // 1 gün pay bırak (scroll kaçırmasın diye)
                        untilArg = $" --until_date {newestDate.AddDays(-1):yyyy-MM-dd}";
                    }
                }

                string args = $"\"{_scriptPath}\" search_influencer --query \"{base64Query}\" --base64 --market \"{market}\" --limit {limit}{sinceArg}{untilArg}{visibilityFlag}";
                
                // Deep scans (sinceDate != null) need more time
                int timeout = string.IsNullOrEmpty(sinceDate) ? 90 : 1200; 
                var cfg = ConfigManager.Current;
                string json = await RunPythonScript(args, cfg.XLoginUser, cfg.XLoginPass, timeout);

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

                int arrayLength = data.GetArrayLength();
                Logger.Twitter($"📊 DATA ARRAY LENGTH: {arrayLength} for query: {symbol}");
                
                foreach (var item in data.EnumerateArray())
                {
                    try
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                        {
                            Logger.Twitter($"⚠️ Skipping non-object item in JSON: {item.ValueKind}");
                            continue;
                        }

                        string author = item.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() ?? (handle ?? "") : (handle ?? "");
                        string content = item.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? string.Empty : string.Empty;
                        string url = item.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() ?? string.Empty : string.Empty;
                        if (IsBadSocialResult(author, content, url)) continue;
                        
                        // Robust integer parsing
                        int engagement = 0;
                        if (item.TryGetProperty("engagement", out var e))
                        {
                            try {
                                if (e.ValueKind == JsonValueKind.Number) engagement = e.GetInt32();
                                else if (e.ValueKind == JsonValueKind.String && int.TryParse(e.GetString(), out var ev)) engagement = ev;
                            } catch { engagement = 0; }
                        }

                        int followers = 0;
                        if (item.TryGetProperty("followerCount", out var fc))
                        {
                            if (fc.ValueKind == JsonValueKind.Number) followers = fc.GetInt32();
                            else if (fc.ValueKind == JsonValueKind.String && int.TryParse(fc.GetString(), out var fcv)) followers = fcv;
                        }

                        DateTime postDate = DateTime.Now;
                        if (item.TryGetProperty("postDate", out var pd) && pd.ValueKind == JsonValueKind.String && DateTime.TryParse(pd.GetString(), out var parsed))
                        {
                            postDate = parsed;
                        }

                        string? imageUrl = item.TryGetProperty("imageUrl", out var img) && img.ValueKind == JsonValueKind.String ? img.GetString() : (item.TryGetProperty("image", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : null);

                        if ((string.IsNullOrWhiteSpace(content) || content.Length < 10) && string.IsNullOrEmpty(imageUrl)) 
                        {
                            continue;
                        }

                        // GURU DETECTION
                        bool isGuru = author?.Equals("EFELERiiNEFESi3", StringComparison.OrdinalIgnoreCase) ?? false;
                        if (!isGuru && author != null && author.Contains("EFELER", StringComparison.OrdinalIgnoreCase)) isGuru = true; 

                        if (!isGuru && ContentQualityGuard.ContainsPrivateLinks(content))
                        {
                            continue;
                        }
                        
                        bool isRetweet = item.TryGetProperty("is_retweet", out var ort) && (ort.ValueKind == JsonValueKind.True || ort.ValueKind == JsonValueKind.False) && ort.GetBoolean();
                        string? origAuthor = item.TryGetProperty("original_author", out var oa) && oa.ValueKind == JsonValueKind.String ? oa.GetString() : null;

                        string finalAuthor = string.IsNullOrWhiteSpace(author) ? (handle ?? "Unknown") : author;
                        if (!finalAuthor.StartsWith("@")) finalAuthor = "@" + finalAuthor;

                        sink.Add(new InfluencerPost
                        {
                            Handle = finalAuthor,
                            OriginalAuthor = origAuthor,
                            IsRetweet = isRetweet,
                            Content = content.Length > 500 ? content.Substring(0, 497) + "..." : content,
                            Url = url,
                            PostDate = postDate,
                            Engagement = engagement,
                            FollowerCount = followers,
                            Market = market,
                            Symbol = symbol.Replace("from:", "").Split(' ')[0],
                            ImageUrl = imageUrl
                        });

                        // LIMIT POLICY: 
                        if (string.IsNullOrEmpty(sinceDate) && sink.Count >= 20) break;
                    }
                    catch (Exception itemEx)
                    {
                        Logger.Twitter($"⚠️ Skipping malformed result item: {itemEx.Message} | JSON: {item.GetRawText().Substring(0, Math.Min(500, item.GetRawText().Length))}...");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Twitter($"⚠️ Influencer search error: {ex.Message}");
            }
            finally
            {
                // v4.4.1: Record search completion time for rate limiting
                _lastSearchCompletedUtc = DateTime.UtcNow;
            }
        }

        private static bool IsBadSocialResult(string author, string content, string url)
        {
            string cleanAuthor = (author ?? string.Empty).Trim().TrimStart('@');
            string currentUser = ConfigManager.Current.XLoginUser?.Trim().TrimStart('@') ?? string.Empty;
            if (cleanAuthor.Equals("ERROR_404", StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(currentUser) && cleanAuthor.Equals(currentUser, StringComparison.OrdinalIgnoreCase)) return true;

            string lower = (content ?? string.Empty).ToLowerInvariant();
            if (lower.Contains("acc_not_found")) return true;
            if (lower.Contains("piyasa görüşleri") || lower.Contains("teknik analizim") || lower.Contains("xideai")) return true;
            if (string.IsNullOrWhiteSpace(url) || url.TrimEnd('/').Equals("https://x.com", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
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


        
        // v3.4.3: Meta-Teacher Events
        public event Action<string>? OnMetaTeacherLog;
        public event Func<InfluencerPost, string, Task>? OnMetaTeacherInsight;

        // public static readonly List<string> CouncilMembers = ... (Moved to JSON)

        /// <summary>
        /// Dedicated Loop for "Council of AI" (Meta-Teacher)
        /// Runs independently, logs separately, triggers UI insights.
        /// </summary>
        public async Task PerformMetaTeacherLoopAsync()
        {
            // Lock to prevent parallel runs of the SAME module
            if (!await _deepScanLock.WaitAsync(0))
            {
                OnMetaTeacherLog?.Invoke("⚠️ Kaynak meşgul (Deep Scan veya başka işlem sürüyor). Bekleniyor...");
                return; 
            }

            try
            {
                OnMetaTeacherLog?.Invoke("🚀 Meta-Teacher (Wisdom) Döngüsü Başlatıldı...");
                
                // Phase 2: Get Dynamic List from JSON
                var councilList = _influencerControl?.GetMetaTeacherInfluencers() ?? new List<Influencer>();
                
                if (councilList.Count == 0)
                {
                    OnMetaTeacherLog?.Invoke("⚠️ Konsey listesi boş! (InfluencerData.json kontrol ediniz)");
                }

                int processedCount = 0;

                foreach (var influencer in councilList)
                {
                    string handle = influencer.Handle;
                    OnMetaTeacherLog?.Invoke($"🔍 Analiz Ediliyor: {handle}...");
                    
                    // v3.5 REACH UNLIMITED: Check if we have data since Jan 1st for this handle (Gap Filling)
                    bool needsDeepScan = true;
                    if (Memory != null)
                    {
                        var h = handle.StartsWith("@") ? handle : "@" + handle;
                        var handleData = Memory.GetKnowledgeBase().Where(p => p.Author.Equals(h, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (handleData.Any())
                        {
                            var oldest = handleData.Min(p => p.PostDate);
                            // If we have data reaching back to Jan 1st (within 3 days buffer), we don't need deep scan
                            if (oldest <= new DateTime(2025, 1, 4)) needsDeepScan = false;
                        }
                    }

                    int limit = needsDeepScan ? 500 : 25; 
                    string? sinceDate = needsDeepScan ? "2025-01-01" : null;

                    if (needsDeepScan) OnMetaTeacherLog?.Invoke($"📜 Boşluk Doldurma Modu (Hedef: 2025-01-01, Limit: 500)...");

                    string query = $"from:{handle.TrimStart('@')} include:nativeretweets";
                    var posts = await FindInfluencerAnalyses(query, "CRYPTO", null, limit, sinceDate); // Pass sinceDate

                    if (posts != null && posts.Count > 0)
                    {
                        // v3.6.6: Dynamic delay based on time of day
                        var now = DateTime.Now;
                        bool isBusinessHours = now.DayOfWeek != DayOfWeek.Saturday 
                                            && now.DayOfWeek != DayOfWeek.Sunday
                                            && now.Hour >= 9 && now.Hour < 20;

                        foreach (var post in posts)
                        {
                            // 1. Memory Learn (Standard)
                            bool learnt = _memoryEngine?.Learn(post) ?? false;
                            
                            // 2. META-TEACHER SPECIAL ANALYSIS
                            if (OnMetaTeacherInsight != null)
                            {
                                await OnMetaTeacherInsight.Invoke(post, "MetaTeacherCycle"); 
                                
                                // v3.6.6: Extra delay between INSIGHTS to protect AI rate limits
                                // During business hours, we wait 15-30s between AI analyses to keep the priority for Signals/News
                                // At night, we wait 5s.
                                int aiDelay = isBusinessHours ? 20000 : 5000;
                                await Task.Delay(aiDelay);
                            }
                        }
                        OnMetaTeacherLog?.Invoke($"✅ {handle}: {posts.Count} yeni veri incelendi.");
                        processedCount++;
                    }
                    else
                    {
                        OnMetaTeacherLog?.Invoke($"ℹ️ {handle}: Yeni veri yok.");
                    }

                    // Respect rate limits hard between influencers
                    await Task.Delay(5000); 
                }
                
                // _isFirstMetaRun = false; // Removed
                OnMetaTeacherLog?.Invoke("🏁 Döngü Tamamlandı. Dinlenmeye geçiliyor (10dk).");
            }
            catch (Exception ex)
            {
                OnMetaTeacherLog?.Invoke($"❌ HATA: {ex.Message}");
            }
            finally
            {
                _deepScanLock.Release();
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
                            
                            // Trigger Event for "Hive" Brain to process (e.g. Meta-Teacher)
                            if (OnDeepScanPostDetected != null) 
                            { 
                                await OnDeepScanPostDetected.Invoke(post); 
                            }

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
