using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using XiDeAI_Pro.Services;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Ensure single instance (Fixes Telegram 409 Conflict & Scraping issues)
            using (var mutex = new System.Threading.Mutex(true, "XiDeAI_Pro_SingleInstance_Mutex", out bool createdNew))
            {
                if (!createdNew)
                {
                    System.Windows.Forms.MessageBox.Show(
                        "X'iDeAI Pro zaten çalışıyor. Lütfen mevcut örneği kapatın.", 
                        "Bilgi", 
                        System.Windows.Forms.MessageBoxButtons.OK, 
                        System.Windows.Forms.MessageBoxIcon.Information
                    );
                    return;
                }

                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? Environment.GetCommandLineArgs()[0];
                var buildDate = File.GetLastWriteTime(exePath);
                Logger.Sys($"Uygulama başlatıldı (v{version} - Build: {buildDate:dd.MM.yyyy HH:mm})");
                
                try 
                {
                    System.Windows.Forms.Application.Run(new MainForm());
                }
                catch (Exception ex)
                {
                    // Catch any unhandled exception during startup
                    System.Windows.Forms.MessageBox.Show(
                        $"UYGULAMA BAŞLATMA HATASI:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                        "KRİTİK HATA", 
                        System.Windows.Forms.MessageBoxButtons.OK, 
                        System.Windows.Forms.MessageBoxIcon.Error
                    );
                }
            }
        }
    }
}
