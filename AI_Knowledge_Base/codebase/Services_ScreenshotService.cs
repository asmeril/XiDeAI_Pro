using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace XiDeAI_Pro.Services
{
    public class ScreenshotService
    {
        private readonly string _scriptPath;
        private readonly string _outputDir;
        private readonly Action<string> _logger;
        private static readonly System.Threading.SemaphoreSlim _semaphore = new System.Threading.SemaphoreSlim(1, 1); // v3.7.1: Serialized Screenshots

        public ScreenshotService(string scriptPath, string outputDir, Action<string>? logger = null)
        {
            _scriptPath = scriptPath;
            _outputDir = outputDir;
            _logger = logger ?? Console.WriteLine;

            if (!Directory.Exists(_outputDir))
                Directory.CreateDirectory(_outputDir);
            
            // Auto-cleanup old screenshots on service start
            CleanupOldScreenshots();
        }

        /// <summary>
        /// TradingView grafiğinin ekran görüntüsünü al
        /// </summary>
        public async Task<string?> CaptureChart(string symbol, string period = "60", string chartId = "GDHgGCEv")
        {
            // v3.7.1: Concurrency Control
            await _semaphore.WaitAsync();
            try
            {
                if (string.IsNullOrEmpty(chartId)) chartId = "GDHgGCEv";

                // Check script existence
                if (!File.Exists(_scriptPath))
                {
                    _logger($"⚠️ Screenshot scripti bulunamadı: {_scriptPath}");
                    return null;
                }

                // Get ChromeDriver path (Check AppData and Local App Dir)
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XiDeAI");
                
                string[] possiblePaths = new[] 
                {
                    Path.Combine(localAppData, "drivers", "chromedriver.exe"), // Priority 1: User Data
                    Path.Combine(appDir, "drivers", "chromedriver.exe"),       // Priority 2: Portable/Install Dir
                    Path.Combine(appDir, "chromedriver.exe")                   // Priority 3: Root
                };

                string chromedriverPath = possiblePaths.FirstOrDefault(File.Exists);
                
                // Fallback: If not found in specific paths, check if it's in PATH or try to find it recursively in likely places
                if (string.IsNullOrEmpty(chromedriverPath))
                {
                    _logger($"⚠️ ChromeDriver standart yollarda bulunamadı, alternatifler aranıyor...");
                     // Check common installation paths
                    string[] extraPaths = {
                         @"C:\Program Files\Google\Chrome\Application\chromedriver.exe",
                         @"C:\Program Files (x86)\Google\Chrome\Application\chromedriver.exe",
                         Path.Combine(appDir, "..", "drivers", "chromedriver.exe"),
                         @"C:\Users\asmeril\AppData\Local\XiDeAI\drivers\chromedriver.exe" // Server Specific Path
                    };
                    chromedriverPath = extraPaths.FirstOrDefault(File.Exists);
                }

                if (string.IsNullOrEmpty(chromedriverPath))
                {
                     chromedriverPath = possiblePaths[0]; // Default to appdata path to let script complain or try its own resolution
                     _logger($"⚠️ KRİTİK: ChromeDriver hiçbir yerde bulunamadı! Varsayılan yol denenecek: {chromedriverPath}");
                }
                else
                {
                    _logger($"✅ ChromeDriver bulundu: {chromedriverPath}");
                }
                
                if (!File.Exists(chromedriverPath))
                {
                    _logger($"⚠️ ChromeDriver bulunamadı! (Aranan: {string.Join(", ", possiblePaths)})");
                }
                
                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{_scriptPath}\" {symbol} {period} \"{_outputDir}\" \"{chartId}\" \"{chromedriverPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_scriptPath) // Set working dir to script folder
                };

                _logger($"📸 Script Başlatılıyor: python {psi.Arguments}");

                using var process = Process.Start(psi);
                if (process == null) 
                {
                    _logger("❌ Process başlatılamadı (null returned).");
                    return null;
                }

                // 120 second timeout to prevent server lockup (increased from 90s)
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(120));
                
                string output = "";
                string error = "";

                try
                {
                    // Read output and error in parallel to avoid deadlocks
                    var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
                    var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

                    await Task.WhenAll(outputTask, errorTask);
                    await process.WaitForExitAsync(cts.Token);

                    output = outputTask.Result;
                    error = errorTask.Result;
                }
                catch (OperationCanceledException)
                {
                    _logger("⚠️ Screenshot işlemi ZAMAN AŞIMINA uğradı (120s). Süreç sonlandırılıyor...");
                    try { process.Kill(true); } catch { } // Kill entire tree (python + chrome + driver)
                    return null;
                }

                // Log errors if any
                if (!string.IsNullOrEmpty(error))
                {
                    _logger($"⚠️ Screenshot stderr: {error}");
                }

                // Parse output - Check lines for SUCCESS: or ERROR:
                if (!string.IsNullOrEmpty(output))
                {
                    var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Trim().StartsWith("SUCCESS:"))
                        {
                            string path = line.Trim().Replace("SUCCESS:", "").Trim();
                            return path;
                        }
                        if (line.Trim().StartsWith("ERROR:"))
                        {
                            _logger($"❌ Screenshot script hatası: {line.Trim()}");
                            return null;
                        }
                    }
                    
                    _logger($"ℹ️ Script çıktısı (SUCCESS/ERROR bulunamadı): {output}");
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger($"❌ Screenshot exception: {ex.Message}");
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Eski screenshot'ları temizle (24 saatten eski)
        /// </summary>
        public void CleanupOldScreenshots()
        {
            try
            {
                var cutoff = DateTime.Now.AddHours(-1); // Keep only last 1 hour (aggressive cleanup for 4K images)
                foreach (var file in Directory.GetFiles(_outputDir, "*.png"))
                {
                    if (File.GetCreationTime(file) < cutoff)
                        File.Delete(file);
                }
            }
            catch { }
        }
    }
}
