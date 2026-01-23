using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XiDeAI_Pro.Services
{
    /// <summary>
    /// Manages external dependencies like ChromeDriver and Python packages.
    /// Checks and updates them on application startup.
    /// </summary>
    public class DependencyManager
    {
        private readonly Action<string> _log;
        private readonly string _driversPath;
        private readonly string _pythonScriptsPath;
        
        // ChromeDriver download URLs (Google's new JSON endpoints)
        private const string CHROME_VERSION_URL = "https://googlechromelabs.github.io/chrome-for-testing/last-known-good-versions-with-downloads.json";
        
        public DependencyManager(Action<string> logger)
        {
            _log = logger ?? Console.WriteLine;
            
            // Use AppData for writable directories (Program Files is read-only)
            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "XiDeAI"
            );
            
            // Ensure drivers exist in AppData
            _driversPath = Path.Combine(appDataDir, "drivers");
            Directory.CreateDirectory(_driversPath);

            // Scripts logic: Priority 1: Installation Directory, Priority 2: AppData
            string installDirScripts = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts");
            string appDataScripts = Path.Combine(appDataDir, "scripts");
            
            if (Directory.Exists(installDirScripts) && File.Exists(Path.Combine(installDirScripts, "social_intel.py")))
            {
                _pythonScriptsPath = installDirScripts;
            }
            else
            {
                _pythonScriptsPath = appDataScripts;
                Directory.CreateDirectory(_pythonScriptsPath);
            }
        }

        /// <summary>
        /// Run all dependency checks on startup
        /// </summary>
        public async Task CheckAndUpdateAllAsync()
        {
            _log("🔧 Bağımlılık kontrolü başlatılıyor...");
            
            try
            {
                // 1. Check and update ChromeDriver
                await CheckChromeDriverAsync();
                
                // 2. Check Python packages
                await CheckPythonPackagesAsync();
                
                _log("✅ Bağımlılık kontrolü tamamlandı.");
            }
            catch (Exception ex)
            {
                _log($"⚠️ Bağımlılık kontrolünde hata: {ex.Message}");
            }
        }

        #region ChromeDriver Management

        /// <summary>
        /// Detects installed Chrome version and ensures matching ChromeDriver exists
        /// </summary>
        public async Task CheckChromeDriverAsync()
        {
            _log("🌐 Chrome/ChromeDriver kontrolü...");
            
            // 1. Get installed Chrome version
            string? chromeVersion = GetInstalledChromeVersion();
            if (string.IsNullOrEmpty(chromeVersion))
            {
                _log("⚠️ Chrome bulunamadı. Edge kullanılacak.");
                // Try Edge instead
                chromeVersion = GetInstalledEdgeVersion();
                if (string.IsNullOrEmpty(chromeVersion))
                {
                    _log("❌ Hiçbir tarayıcı bulunamadı!");
                    return;
                }
            }
            
            _log($"📌 Tespit edilen tarayıcı sürümü: {chromeVersion}");
            
            // 2. Get current ChromeDriver version
            string chromeDriverPath = Path.Combine(_driversPath, "chromedriver.exe");
            string? currentDriverVersion = GetChromeDriverVersion(chromeDriverPath);
            
            // 3. Check if versions match (major version)
            string chromeMajor = chromeVersion.Split('.')[0];
            string? driverMajor = currentDriverVersion?.Split('.')[0];
            
            if (chromeMajor == driverMajor)
            {
                _log($"✅ ChromeDriver güncel (v{currentDriverVersion})");
                return;
            }
            
            // 4. Download matching ChromeDriver
            _log($"📥 ChromeDriver {chromeMajor}.x indiriliyor...");
            await DownloadChromeDriverAsync(chromeMajor);
        }

        private string? GetInstalledChromeVersion()
        {
            string[] chromePaths = new[]
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe")
            };

            foreach (var path in chromePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var versionInfo = FileVersionInfo.GetVersionInfo(path);
                        return versionInfo.FileVersion;
                    }
                    catch { }
                }
            }
            return null;
        }

        private string? GetInstalledEdgeVersion()
        {
            string[] edgePaths = new[]
            {
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
            };

            foreach (var path in edgePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var versionInfo = FileVersionInfo.GetVersionInfo(path);
                        return versionInfo.FileVersion;
                    }
                    catch { }
                }
            }
            return null;
        }

        private string? GetChromeDriverVersion(string driverPath)
        {
            if (!File.Exists(driverPath)) return null;
            
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = driverPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var proc = Process.Start(psi);
                if (proc == null) return null;
                
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);
                
                // Parse: "ChromeDriver 120.0.6099.109 (...)"
                var match = Regex.Match(output, @"ChromeDriver\s+(\d+\.\d+\.\d+\.\d+)");
                return match.Success ? match.Groups[1].Value : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task DownloadChromeDriverAsync(string majorVersion)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(5);
                
                // Get latest versions JSON
                var json = await client.GetStringAsync(CHROME_VERSION_URL);
                using var doc = JsonDocument.Parse(json);
                
                // Find matching version
                string? downloadUrl = null;
                string? foundVersion = null;
                
                // 1. Try ALL channels: Stable, Beta, Dev, Canary
                // Because user might be on v142 (Dev) while Stable is v143
                string[] channelsToTry = { "Stable", "Beta", "Dev", "Canary" };
                
                if (doc.RootElement.TryGetProperty("channels", out var channels))
                {
                    foreach (var channelName in channelsToTry)
                    {
                        if (channels.TryGetProperty(channelName, out var channel))
                        {
                            var version = channel.GetProperty("version").GetString();
                            
                            // Check if this channel's major version matches our browser's major version
                            if (version?.Split('.')[0] == majorVersion)
                            {
                                if (channel.TryGetProperty("downloads", out var downloads) &&
                                    downloads.TryGetProperty("chromedriver", out var chromedriver))
                                {
                                    foreach (var platform in chromedriver.EnumerateArray())
                                    {
                                        if (platform.GetProperty("platform").GetString() == "win64")
                                        {
                                            downloadUrl = platform.GetProperty("url").GetString();
                                            foundVersion = version;
                                            _log($"✅ ChromeDriver {version} ({channelName}) bulundu (Sürüm Eşleşti).");
                                            goto DownloadFound; // Break out of all loops
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                DownloadFound:

                // 2. Fallback: Check the full "known-good-versions" list if not found in latest channels
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    _log($"⚠️ Chrome {majorVersion} 'kanallarında' bulunamadı. Geçmiş sürümler taranıyor...");
                    
                    try 
                    {
                        string fullListUrl = "https://googlechromelabs.github.io/chrome-for-testing/known-good-versions-with-downloads.json";
                        var fullJson = await client.GetStringAsync(fullListUrl);
                        using var fullDoc = JsonDocument.Parse(fullJson);
                        
                        if (fullDoc.RootElement.TryGetProperty("versions", out var versions))
                        {
                            // Iterate backwards to find the latest version that matches our major version
                            foreach (var verObj in versions.EnumerateArray())
                            {
                                var ver = verObj.GetProperty("version").GetString();
                                if (ver?.StartsWith(majorVersion + ".") == true)
                                {
                                    if (verObj.TryGetProperty("downloads", out var downloads) &&
                                        downloads.TryGetProperty("chromedriver", out var chromedriver))
                                    {
                                        foreach (var platform in chromedriver.EnumerateArray())
                                        {
                                            if (platform.GetProperty("platform").GetString() == "win64")
                                            {
                                                downloadUrl = platform.GetProperty("url").GetString();
                                                foundVersion = ver;
                                                // Keep searching - we want the latest match (which comes later in file usually? No, actually JSON array might be sorted. Let's just take the first match or scan all to find max? 
                                                // Usually they are sorted by version. Let's take the last one found.
                                            }
                                        }
                                    }
                                }
                            }
                            
                            if (!string.IsNullOrEmpty(downloadUrl))
                            {
                                _log($"✅ ChromeDriver {foundVersion} (Arşiv) bulundu.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log($"⚠️ Geçmiş sürümler taranırken hata: {ex.Message}");
                    }
                }

                // 3. Fallback: If major version not found, try nearest older version (Chrome updates faster than drivers sometimes)
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    _log($"⚠️ Chrome {majorVersion} için ChromeDriver bulunamadı. En yakın önceki sürüm aranıyor...");
                    
                    int targetMajor = int.Parse(majorVersion);
                    string? bestMatchUrl = null;
                    string? bestMatchVersion = null;
                    int closestMajor = 0;
                    
                    try 
                    {
                        string fullListUrl = "https://googlechromelabs.github.io/chrome-for-testing/known-good-versions-with-downloads.json";
                        var fullJson = await client.GetStringAsync(fullListUrl);
                        using var fullDoc = JsonDocument.Parse(fullJson);
                        
                        if (fullDoc.RootElement.TryGetProperty("versions", out var allVersions))
                        {
                            // Find the highest version that's <= our target major version
                            foreach (var verObj in allVersions.EnumerateArray())
                            {
                                var ver = verObj.GetProperty("version").GetString();
                                if (string.IsNullOrEmpty(ver)) continue;
                                
                                if (int.TryParse(ver.Split('.')[0], out int verMajor))
                                {
                                    // Take the closest version that's <= target
                                    if (verMajor <= targetMajor && verMajor > closestMajor)
                                    {
                                        if (verObj.TryGetProperty("downloads", out var downloads) &&
                                            downloads.TryGetProperty("chromedriver", out var chromedriver))
                                        {
                                            foreach (var platform in chromedriver.EnumerateArray())
                                            {
                                                if (platform.GetProperty("platform").GetString() == "win64")
                                                {
                                                    bestMatchUrl = platform.GetProperty("url").GetString();
                                                    bestMatchVersion = ver;
                                                    closestMajor = verMajor;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log($"⚠️ Eski sürüm taramasında hata: {ex.Message}");
                    }
                    
                    if (!string.IsNullOrEmpty(bestMatchUrl))
                    {
                        downloadUrl = bestMatchUrl;
                        foundVersion = bestMatchVersion;
                        _log($"✅ ChromeDriver {foundVersion} (Chrome {majorVersion}'e en yakın) bulundu.");
                    }
                }
                
                // 4. Last Resort: Download latest STABLE (will likely have version mismatch warning)
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    _log($"⚠️ Uyumlu ChromeDriver bulunamadı. Son çare olarak en güncel Stable denenecek...");
                    
                    if (doc.RootElement.TryGetProperty("channels", out var fallbackChannels) &&
                        fallbackChannels.TryGetProperty("Stable", out var stableChannel) &&
                        stableChannel.TryGetProperty("downloads", out var stableDownloads) &&
                        stableDownloads.TryGetProperty("chromedriver", out var stableChromedriver))
                    {
                        foreach (var platform in stableChromedriver.EnumerateArray())
                        {
                            if (platform.GetProperty("platform").GetString() == "win64")
                            {
                                downloadUrl = platform.GetProperty("url").GetString();
                                foundVersion = stableChannel.GetProperty("version").GetString();
                                _log($"⚠️ ChromeDriver {foundVersion} (Stable) kullanılacak. Chrome'u güncelleyin!");
                                break;
                            }
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    _log($"❌ ChromeDriver indirilemedi. Lütfen manuel yükleyin.");
                    return;
                }
                
                // Download ZIP
                _log($"📥 İndiriliyor: {downloadUrl}");
                var zipBytes = await client.GetByteArrayAsync(downloadUrl);
                
                string zipPath = Path.Combine(Path.GetTempPath(), "chromedriver.zip");
                await File.WriteAllBytesAsync(zipPath, zipBytes);
                
                // Extract
                string extractPath = Path.Combine(Path.GetTempPath(), "chromedriver_extract");
                if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
                ZipFile.ExtractToDirectory(zipPath, extractPath);
                
                // Find chromedriver.exe in extracted folder
                var driverFiles = Directory.GetFiles(extractPath, "chromedriver.exe", SearchOption.AllDirectories);
                if (driverFiles.Length > 0)
                {
                    string destPath = Path.Combine(_driversPath, "chromedriver.exe");
                    
                    // Delete old
                    if (File.Exists(destPath)) File.Delete(destPath);
                    
                    // Copy new
                    File.Copy(driverFiles[0], destPath);
                    _log($"✅ ChromeDriver güncellendi: {destPath}");
                }
                
                // Cleanup
                try { File.Delete(zipPath); Directory.Delete(extractPath, true); } catch { }
            }
            catch (Exception ex)
            {
                _log($"❌ ChromeDriver indirme hatası: {ex.Message}");
            }
        }

        #endregion

        #region Python Package Management

        /// <summary>
        /// Checks and updates Python packages (selenium, etc.)
        /// </summary>
        public async Task CheckPythonPackagesAsync()
        {
            _log("🐍 Python paketleri kontrol ediliyor...");
            
            if (!IsPythonInstalled())
            {
                _log("⚠️ Python yüklü değil. Screenshot özelliği çalışmayabilir.");
                return;
            }
            
            // Required packages
            string[] requiredPackages = new[] { "selenium", "webdriver-manager", "pillow", "pyperclip" };
            
            foreach (var package in requiredPackages)
            {
                await EnsurePythonPackageAsync(package);
            }
            
            _log("✅ Python paketleri hazır.");
        }

        private bool IsPythonInstalled()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var proc = Process.Start(psi);
                proc?.WaitForExit(5000);
                return proc?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task EnsurePythonPackageAsync(string packageName)
        {
            try
            {
                // Check if installed
                var checkPsi = new ProcessStartInfo
                {
                    FileName = "pip",
                    Arguments = $"show {packageName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var checkProc = Process.Start(checkPsi);
                if (checkProc == null)
                {
                    _log($"⚠️ {packageName} kontrolü başlatılamadı.");
                    return;
                }
                
                // Wait for exit and ensure process has exited before accessing ExitCode
                bool exited = checkProc.WaitForExit(10000);
                
                // Only access ExitCode if process has actually exited
                bool packageMissing = !exited || !checkProc.HasExited || checkProc.ExitCode != 0;
                
                if (packageMissing)
                {
                    _log($"📦 {packageName} yükleniyor...");
                    
                    // Install package
                    var installPsi = new ProcessStartInfo
                    {
                        FileName = "pip",
                        Arguments = $"install --upgrade {packageName}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    
                    using var installProc = Process.Start(installPsi);
                    if (installProc == null)
                    {
                        _log($"⚠️ {packageName} yüklenemedi (başlatma hatası).");
                        return;
                    }
                    
                    bool installExited = installProc.WaitForExit(60000);
                    
                    if (installExited && installProc.HasExited && installProc.ExitCode == 0)
                    {
                        _log($"✅ {packageName} yüklendi.");
                    }
                    else
                    {
                        _log($"⚠️ {packageName} yüklenemedi.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log($"⚠️ {packageName} kontrol hatası: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }

        #endregion

        /// <summary>
        /// Returns the path to chromedriver.exe
        /// </summary>
        public string GetChromeDriverPath()
        {
            return Path.Combine(_driversPath, "chromedriver.exe");
        }

        /// <summary>
        /// Check if all dependencies are ready
        /// </summary>
        public bool AreDependenciesReady()
        {
            string driverPath = GetChromeDriverPath();
            return File.Exists(driverPath);
        }

        public string GetScriptPath(string scriptName)
        {
            return Path.Combine(_pythonScriptsPath, scriptName);
        }
    }
}

