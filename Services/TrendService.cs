using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using XiDeAI_Pro.Config;

namespace XiDeAI_Pro.Services
{
    public class TrendService
    {
        private static readonly HttpClient client = new HttpClient();

        /// <summary>
        /// X (Twitter) API ile Türkiye'deki finans trendlerini çeker.
        /// Not: Twitter API v2 "trends" endpoint'i OAuth 2.0 App-Only token gerektirir.
        /// Basitlik için şimdilik manuel listeyle çalışacağız + Gemini ile zenginleştireceğiz.
        /// </summary>
        public async Task<List<string>> GetFinanceTrendsAsync()
        {
            // Twitter Trends API çağrısı (OAuth 2.0 Bearer Token gerekli)
            // Şimdilik statik + Gemini ile dinamik kombine ediyoruz
            var baseTrends = new List<string> { "#BIST100", "#Borsa", "#XU100" };

            try
            {
                // Gemini'den günün finans trendlerini sor
                var geminiKey = ConfigManager.Current.GeminiApiKey;
                if (!string.IsNullOrEmpty(geminiKey))
                {
                    string prompt = "Bugün Türkiye'de borsa ve finans ile ilgili Twitter/X'te trend olan 5 hashtag yaz. " +
                                    "Sadece hashtag'leri yaz, açıklama yapma. Her biri # ile başlasın.";

                    var requestBody = new
                    {
                        contents = new[] { new { parts = new[] { new { text = prompt } } } }
                    };

                    var content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");
                    var response = await client.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent?key={geminiKey}", content);

                    if (response.IsSuccessStatusCode)
                    {
                        var resultJson = await response.Content.ReadAsStringAsync();
                        using (JsonDocument doc = JsonDocument.Parse(resultJson))
                        {
                            var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                // Hashtag'leri regex ile daha temiz ayıkla
                                var regex = new System.Text.RegularExpressions.Regex(@"#[\wçÇğĞıİöÖşŞüÜ]+");
                                var matches = regex.Matches(text);
                                
                                foreach (System.Text.RegularExpressions.Match match in matches)
                                {
                                    string tag = match.Value;
                                    if (!baseTrends.Contains(tag, StringComparer.OrdinalIgnoreCase))
                                    {
                                        baseTrends.Add(tag);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return baseTrends;
        }

        /// <summary>
        /// Verilen hisse sembolü için uygun hashtag oluşturur
        /// </summary>
        public string GetSymbolHashtag(string symbol)
        {
            // VIP prefix'i temizle
            var clean = symbol.Replace("VIP-", "").Replace("VIP'", "").Trim();
            return "#" + clean.ToUpperInvariant();
        }
    }
}


