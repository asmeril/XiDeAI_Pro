
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Windows.Forms;
using System.Reflection;

namespace XiDeAI_Pro.Services
{
    public class UpdateInfo
    {
        public string version { get; set; } = "";
        public string releaseDate { get; set; } = "";
        public string downloadUrl { get; set; } = "";
        public string changelog { get; set; } = "";
    }

    public class UpdaterService
    {
        // GitHub Raw URL for version.json
        private const string UPDATE_URL = "https://raw.githubusercontent.com/marvelariantomarbun-spec/MEGA/main/IdealSmartNotifier/version.json";
        
        private readonly string _currentVersion;
        private readonly string _appPath;
        private readonly Action<string> _logger;

        public event EventHandler<UpdateInfo>? UpdateAvailable;

        public UpdaterService(Action<string>? logger = null)
        {
            _logger = logger ?? Console.WriteLine;
            _appPath = AppDomain.CurrentDomain.BaseDirectory;
            _currentVersion = GetCurrentVersion();
        }

        private string GetCurrentVersion()
        {
            // Try to read from version.txt first (created by installer)
            string versionFile = Path.Combine(_appPath, "version.txt");
            if (File.Exists(versionFile))
            {
                return File.ReadAllText(versionFile).Trim();
            }

            // Fallback to assembly version
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }

        public async Task<bool> CheckForUpdatesAsync(bool silent = true)
        {
            try
            {
                _logger("🔄 Güncelleme kontrolü yapılıyor...");

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Add("User-Agent", "XiDeAI-Updater");

                var json = await client.GetStringAsync(UPDATE_URL);
                var info = JsonSerializer.Deserialize<UpdateInfo>(json);

                if (info == null)
                {
                    _logger("❌ Güncelleme bilgisi alınamadı.");
                    return false;
                }

                // Compare versions
                if (IsNewerVersion(info.version, _currentVersion))
                {
                    _logger($"🆕 Yeni sürüm mevcut: {info.version} (Mevcut: {_currentVersion})");
                    
                    UpdateAvailable?.Invoke(this, info);

                    if (!silent)
                    {
                        var result = MessageBox.Show(
                            $"X'iDeAI {info.version} sürümü mevcut!\n\n" +
                            $"Değişiklikler:\n{info.changelog}\n\n" +
                            "Şimdi indirmek ister misiniz?",
                            "Güncelleme Mevcut",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information);

                        if (result == DialogResult.Yes)
                        {
                            await DownloadAndInstallUpdateAsync(info);
                        }
                    }
                    return true;
                }
                else
                {
                    _logger("✅ En güncel sürümü kullanıyorsunuz.");
                    return false;
                }
            }
            catch (HttpRequestException)
            {
                _logger("⚠️ Güncelleme sunucusuna bağlanılamadı.");
                return false;
            }
            catch (Exception ex)
            {
                _logger($"❌ Güncelleme kontrolü hatası: {ex.Message}");
                if (!silent)
                {
                    MessageBox.Show($"Güncelleme kontrolü başarısız: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }
        }

        private bool IsNewerVersion(string remoteVersion, string localVersion)
        {
            try
            {
                var remote = new Version(remoteVersion);
                var local = new Version(localVersion);
                return remote > local;
            }
            catch
            {
                return false;
            }
        }

        public async Task DownloadAndInstallUpdateAsync(UpdateInfo info)
        {
            try
            {
                _logger($"📥 Güncelleme indiriliyor: {info.downloadUrl}");

                string tempPath = Path.Combine(Path.GetTempPath(), "XiDeAI_Update.exe");

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(10);

                var response = await client.GetAsync(info.downloadUrl);
                response.EnsureSuccessStatusCode();

                await using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
                await response.Content.CopyToAsync(fs);

                _logger("✅ İndirme tamamlandı. Kurulum başlatılıyor...");

                // Run the installer
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });

                // Exit current application
                Application.Exit();
            }
            catch (Exception ex)
            {
                _logger($"❌ Güncelleme indirme hatası: {ex.Message}");
                MessageBox.Show($"Güncelleme indirilemedi:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void RunStartupHealthCheck()
        {
            _logger("🔧 Sistem kontrolü yapılıyor...");

            // Check Python (optional for Selenium features)
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
                if (proc != null)
                {
                    proc.WaitForExit(3000);
                    if (proc.ExitCode == 0)
                    {
                        _logger("✅ Python bulundu.");
                    }
                }
            }
            catch
            {
                _logger("⚠️ Python bulunamadı (opsiyonel).");
            }

            // Check Chrome/Edge for Selenium
            string[] browserPaths = new[]
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
            };

            bool browserFound = false;
            foreach (var path in browserPaths)
            {
                if (File.Exists(path))
                {
                    browserFound = true;
                    break;
                }
            }

            if (browserFound)
            {
                _logger("✅ Web tarayıcı bulundu.");
            }
            else
            {
                _logger("⚠️ Chrome/Edge bulunamadı (Screenshot için gerekli olabilir).");
            }

            _logger("🔧 Sistem kontrolü tamamlandı.");
        }

        public string GetVersionInfo()
        {
            return $"X'iDeAI v{_currentVersion}";
        }
    }
}

