using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro.Services
{
    public class AthleteDiscoveryService
    {
        private readonly SocialIntelService _socialIntel;

        public AthleteDiscoveryService(SocialIntelService socialIntel)
        {
            _socialIntel = socialIntel;
        }

        public async Task DiscoverAthletes()
        {
            Logger.FanZone("🌍 [Keşif] Sporcu Twitter hesapları taranıyor (Auto-Discovery)...");

            // Mock List for v1.0 (Otomatik tarama altyapısı hazır, veriler şimdilik statik liste)
            // Bu liste Google'da aratılıp Twitter hesabı bulunacak.
            var squad = new Dictionary<string, string>
            {
                // Futbol (A Takım)
                {"Dominik Livakovic", "Futbol"}, {"Mert Müldür", "Futbol"}, {"Alexander Djiku", "Futbol"}, 
                {"Rodrigo Becao", "Futbol"}, {"Jayden Oosterwolde", "Futbol"}, {"Fred", "Futbol"}, 
                {"İsmail Yüksek", "Futbol"}, {"Sebastian Szymanski", "Futbol"}, {"İrfan Can Kahveci", "Futbol"}, 
                {"Dusan Tadic", "Futbol"}, {"Edin Dzeko", "Futbol"}, {"Cengiz Ünder", "Futbol"}, 
                {"Cenk Tosun", "Futbol"}, {"Sofyan Amrabat", "Futbol"}, {"Allan Saint-Maximin", "Futbol"},
                {"Youssef En-Nesyri", "Futbol"}, {"Çağlar Söyüncü", "Futbol"}, {"Bright Osayi-Samuel", "Futbol"},

                // Basketbol
                {"Nigel Hayes-Davis", "Basketbol"}, {"Melih Mahmutoğlu", "Basketbol"}, 
                {"Sertaç Şanlı", "Basketbol"}, {"Tarik Biberovic", "Basketbol"}, {"Marko Guduric", "Basketbol"},
                {"Scottie Wilbekin", "Basketbol"}, {"Dyshawn Pierre", "Basketbol"},

                // Voleybol
                {"Eda Erdem", "Voleybol"}, {"Melissa Vargas", "Voleybol"}, {"Gizem Örge", "Voleybol"},
                {"Aslı Kalaç", "Voleybol"}, {"Arina Fedorovtseva", "Voleybol"} // Arina gitti mi? Kontrol edilmeli ama Google doğrusunu bulur (veya bulmaz)
            };

            int newCount = 0;
            foreach(var player in squad)
            {
                // Zaten var mı kontrol et (İsim veya Handle çakışması)
                if (ConfigManager.Current.FenerbahceAthletes.Any(a => a.Name.Equals(player.Key, StringComparison.OrdinalIgnoreCase)))
                    continue;

                Logger.FanZone($"🔍 Aranıyor: {player.Key}...");
                
                // Handle bul (Google Search)
                // 3 deneme yap (Google bazen captcha verebilir, gerçi headless da zor ama retry iyidir)
                string? handle = await _socialIntel.FindTwitterHandle(player.Key);
                
                // Rate Limit koruması
                await Task.Delay(3000); 

                if (!string.IsNullOrEmpty(handle) && !handle.Contains("Error"))
                {
                    // Handle zaten listede var mı? (Başka bir isimle eklenmiş olabilir)
                    if (ConfigManager.Current.FenerbahceAthletes.Any(a => a.Handle.Equals(handle, StringComparison.OrdinalIgnoreCase)))
                    {
                        Logger.FanZone($"ℹ️ {player.Key} zaten ekli ({handle}).");
                        continue;
                    }

                    ConfigManager.Current.FenerbahceAthletes.Add(new FenerbahceAthlete 
                    { 
                        Name = player.Key, 
                        Handle = handle, 
                        Sport = player.Value 
                    });
                    newCount++;
                    Logger.FanZone($"✅ [Keşif] EKLENDİ: {player.Key} -> {handle} ({player.Value})");
                    ConfigManager.Save();
                }
                else
                {
                    Logger.FanZone($"⚠️ [Keşif] {player.Key} için Twitter adresi bulunamadı.");
                }
            }

            if (newCount > 0)
                Logger.FanZone($"🎉 [Keşif] Tamamlandı. {newCount} yeni sporcu listeye eklendi.");
            else
                Logger.FanZone("ℹ️ [Keşif] Tamamlandı. Yeni sporcu bulunamadı (Liste güncel).");
        }
    }
}
