using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace XiDeAI_Pro.Services
{
    public class MaintenanceService
    {
        private readonly Action<string> _logger;

        public MaintenanceService(Action<string>? logger = null)
        {
            _logger = logger ?? Console.WriteLine;
        }

        /// <summary>
        /// Sistemde başıboş (zombie) kalan chrome ve chromedriver süreçlerini temizle.
        /// Özellikle 5 dakikadan uzun süredir açık kalanları hedefler.
        /// </summary>
        public void CleanZombieProcesses()
        {
            try
            {
                _logger("🧹 Zombie Killer: Atıl süreç temizliği başlatılıyor...");
                int killedCount = 0;

                string[] targetProcesses = { "chrome", "chromedriver" };
                var now = DateTime.Now;

                foreach (var procName in targetProcesses)
                {
                    var processes = Process.GetProcessesByName(procName);
                    foreach (var proc in processes)
                    {
                        try
                        {
                            // 5 dakikadan uzun süredir açık olan süreçleri "zombie" kabul et
                            var age = now - proc.StartTime;
                            if (age.TotalMinutes > 5)
                            {
                                _logger($"🔪 Sonlandırılıyor: {procName} (PID: {proc.Id}, Yaş: {(int)age.TotalMinutes} dk)");
                                proc.Kill(true); // Kill entire tree
                                killedCount++;
                            }
                        }
                        catch (System.ComponentModel.Win32Exception) { /* Access denied, likely system process */ }
                        catch (Exception ex)
                        {
                             _logger($"⚠️ Süreç sonlandırılamadı ({procName}): {ex.Message}");
                        }
                    }
                }

                if (killedCount > 0)
                    _logger($"✅ Temizlik tamamlandı. {killedCount} adet atıl süreç infaz edildi.");
                else
                    _logger("✅ Temizlik tamamlandı. Atıl süreç bulunamadı.");
            }
            catch (Exception ex)
            {
                _logger($"❌ MaintenanceService Hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Arka planda periyodik temizlik başlat
        /// </summary>
        public void StartPeriodicCleanup(TimeSpan interval)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(interval);
                    CleanZombieProcesses();
                }
            });
        }
    }
}

