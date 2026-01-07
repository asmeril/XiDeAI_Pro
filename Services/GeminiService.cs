using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using XiDeAI_Pro.Config;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace XiDeAI_Pro.Services
{
    public class GeminiService
    {
        private static readonly HttpClient client = new HttpClient() { Timeout = TimeSpan.FromSeconds(120) };
        private readonly PromptManager _prompts = new PromptManager();
        private readonly MemoryEngine _memory;
        private readonly StatsEngine _stats;
        public string LastError { get; private set; } = "";

        public GeminiService(MemoryEngine memory, StatsEngine stats)
        {
            _memory = memory;
            _stats = stats;
            
            // v3.0: SSL stability for older environments
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        }

        public async Task<string?> GenerateTweetContent(SignalData sig, string screenshotPath, string influencerCitations = "")
        {
            string context = _memory.GetSymbolContext(sig.Symbol);
            
            // Load indicator guide if exists
            string indicatorContext = "";
            try
            {
                string indicatorGuidePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "IndicatorGuide.md");
                if (System.IO.File.Exists(indicatorGuidePath))
                {
                    indicatorContext = System.IO.File.ReadAllText(indicatorGuidePath);
                    indicatorContext = $@"
=== GÖSTERGE REHBERİ ===
{indicatorContext}
=== GÖSTERGE REHBERİ SONU ===

";
                }
            }
            catch { /* Indicator guide optional */ }
            
            // Format score as fraction for AI context
            string scoreStr = $"{sig.Score}/{sig.MaxScore}";
            
            string prompt = _prompts.GetSignalAnalysisPrompt(
                sig.Symbol, 
                sig.Strategy, 
                scoreStr, 
                sig.Price.ToString("N2"), 
                indicatorContext, // Add indicator context
                influencerCitations);

            if (!string.IsNullOrEmpty(context))
            {
                prompt += "\n\n" + context;
            }

            string? result = null;
            
            // v3.0: Multimodal support for signals
            if (!string.IsNullOrEmpty(screenshotPath) && System.IO.File.Exists(screenshotPath))
            {
                byte[] imageBytes = System.IO.File.ReadAllBytes(screenshotPath);
                string imageBase64 = Convert.ToBase64String(imageBytes);
                result = await SendMultimodalRequest(prompt, imageBase64);
            }
            else
            {
                result = await SendRequest(prompt);
            }

            if (!string.IsNullOrEmpty(result))
            {
                _memory.StoreAnalysis(sig.Symbol, sig.Strategy, result);
            }
            return result;
        }

        // Legacy support (to be removed after full migration)
        public async Task<string?> GenerateTweetContent(string symbol, string price, string score, string strategy, string trendList)
        {
            return await GenerateTweetContent(new SignalData { Symbol = symbol, Price = decimal.Parse(price), Score = int.Parse(score), Strategy = strategy }, "");
        }

        public async Task<string?> GenerateMarketAnalysis(string symbol, string marketType)
        {
            string prompt = $@"Sen uzman bir finansal analist ve portföy yöneticisisin.
{symbol} ({marketType}) için Kapsamlı Teknik ve Temel Analiz hazırla.

--- ANALİZ PLANI ---

1. 🏢 **Temel Görünüm & Sektör:**
   (Şirketin/Varlığın hikayesi ne? Sektöründeki konumu, son haber akışları ve temel rasyoları nasıl?)

2. 📐 **Teknik Göstergeler:**
   (RSI, MACD, Hareketli Ortalamalar ve Hacim ne söylüyor?)

3. 🛡️ **Kritik Seviyeler:**
   • Destekler: [Seviyeler]
   • Dirençler: [Seviyeler]

4. 🌍 **Global & Benchmark:**
   ({(marketType == "KRİPTO" ? "Bitcoin dominansı" : marketType == "FOREX" ? "DXY endeksi" : "BIST100 endeksi")} ile korelasyonu nasıl?)

5. 🧠 **Yönetici Özeti:**
   (Kısa, orta ve uzun vade beklentin nedir?)

⚠️ _Yasal Uyarı: Yatırım tavsiyesi değildir._";

            return await SendRequest(prompt);
        }

        /// <summary>
        /// Generate market analysis WITH real-time price data from APIs
        /// </summary>
        public async Task<string?> GenerateMarketAnalysisWithPrice(string symbol, string marketType, string priceContext, string influencerCitations = "")
        {
            // Load indicator guide if exists
            string indicatorContext = "";
            try
            {
                string indicatorGuidePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "IndicatorGuide.md");
                if (System.IO.File.Exists(indicatorGuidePath))
                {
                    indicatorContext = System.IO.File.ReadAllText(indicatorGuidePath);
                    indicatorContext = $@"
=== GÖSTERGESİ REHBERI ===
{indicatorContext}
=== GÖSTERGE REHBERİ SONU ===

";
                }
            }
            catch { /* Indicator guide optional */ }

            string influencerSection = string.IsNullOrEmpty(influencerCitations) 
                ? "" 
                : $@"
9. PİYASA GÖRÜŞÜ (Sosyal Medya Fenomen Analizi):
{influencerCitations}
   (Bu görüşleri kendi analizinle harmanla ve dengeli yaz)";

            string prompt = $@"Sen deneyimli bir {marketType} analistsin.
{symbol} için detaylı analiz yap.

{priceContext}

{indicatorContext}

Analiz İçeriği:
1. Güncel fiyat ve % değişim (YUKARIDAKİ VERİYİ KULLAN!)
2. Teknik Göstergeler: RSI, MACD, ADX ve Hacim durumu
3. Kritik Destek ve Direnç seviyeleri (Pivot seviyeleri varsa kullan)
4. Fibonacci Oranları: 38.2%, 50%, 61.8% (varsa)
5. Smart Money Sinyalleri: Order Block, FVG, MSB (varsa)
6. Benchmark Karşılaştırma:
   - BIST ise XU100 ile
   - Kripto ise BTC ile
   - Forex ise DXY ile kıyasla
7. Kısa ve Orta Vadeli Beklenti
8. Risk Faktörleri{influencerSection}

Kurallar:
- Türkçe ve profesyonel finans dili kullan
- Maddeler halinde yaz
- Emoji kullan (📈📉 seviyesinde vb.)
- Maksimum 600 karakter
- Sonuna '⚠️ Yatırım tavsiyesi değildir.' ekle
- FİYAT VERİSİNİ KENDİN UYDURMA, yukarıdaki gerçek veriyi kullan!";

            return await SendRequest(prompt);
        }

        public async Task<string?> AnalyzeNewsImpact(string title, string source)
        {
            // v3.0: Editor Mode (Confidence Scoring)
            string prompt = _prompts.GetNewsEditorPrompt(title, source);
            return await SendRequest(prompt);
        }

        /// <summary>
        /// Parse symbols, periods and the name of the scan/table from a Guru's screenshot table
        /// </summary>
        public async Task<(List<(string Symbol, string Period)> Items, string TableName)> ParseGuruTableFromImage(string imageUrl)
        {
            var results = new List<(string Symbol, string Period)>();
            string tableName = "Teknik Tarama Listesi";
            try
            {
                // Download image to temporary buffer for Gemini
                byte[] imageBytes = await client.GetByteArrayAsync(imageUrl);
                string imageBase64 = Convert.ToBase64String(imageBytes);

                string prompt = @"Bu bir borsa tarama sonuç tablosudur (Genellikle Efe HMA, Efelerin Efesi gibi başlıkları olur). 
Lütfen bu görseldeki tablodan 'Sembol' (hisse adı) ve 'Periyot' (15, 60, 180, 240, G vb.) sütunlarını oku.
Ayrıca görselin başlığından veya tablo isminden bu taramanın adını (Örn: 'EFE HMA', 'EFELERİN EFESİ STAR') tespit et.

Sadece şu JSON formatında döndür:
{
  ""TableName"": ""Tespit Edilen Tarama Adı"",
  ""Items"": [{""Symbol"": ""THYAO"", ""Period"": ""240""}, {""Symbol"": ""EBEBK"", ""Period"": ""180""}]
}

Eğer tablo/isim bulunamazsa TableName kısmına 'Teknik Tarama' yaz.";

                string? jsonResponse = await SendMultimodalRequest(prompt, imageBase64);
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    // Robust cleanup: extract JSON object from any surrounding text/markdown
                    int startIdx = jsonResponse.IndexOf('{');
                    int endIdx = jsonResponse.LastIndexOf('}');
                    if (startIdx >= 0 && endIdx > startIdx)
                    {
                        jsonResponse = jsonResponse.Substring(startIdx, endIdx - startIdx + 1);
                    }
                    else
                    {
                        // Fallback cleanup if strict braces aren't found (rare)
                        jsonResponse = jsonResponse.Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                                                   .Replace("```", "")
                                                   .Replace("'''", "")
                                                   .Trim();
                    }
                    
                    using var doc = JsonDocument.Parse(jsonResponse);
                    if (doc.RootElement.TryGetProperty("TableName", out var tn)) tableName = tn.GetString() ?? tableName;
                    
                    if (doc.RootElement.TryGetProperty("Items", out var items))
                    {
                        foreach (var item in items.EnumerateArray())
                        {
                            string sym = item.GetProperty("Symbol").GetString() ?? "";
                            string per = item.GetProperty("Period").GetString() ?? "";
                            if (!string.IsNullOrEmpty(sym))
                                results.Add((sym, per));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.AI($"❌ ParseGuruTableFromImage Hatası: {ex.Message}");
                // Log raw response for debugging (truncated to avoid massive logs if image data somehow leaks, though improbable here)
                // We cannot access jsonResponse here easily because of scope, but the intent is clear.
                // Let's rely on the fix for now.
            }
            return (results, tableName);
        }

        public async Task<string?> GenerateGuruHonoringThread(string symbol, string period, string guruHandle, string originalTweetUrl, string tableName = "EFE HMA", string guruName = "Efelerin Efesi", string technicalContext = "", string? imagePath = null, PivotData? pivotData = null)
        {
            // 1) Geçmiş analiz hafızası (kendi önceki yorumların) – guru thread'ine referans için
            string pastContext = _memory.GetSymbolContext(symbol);

            // 2) Gösterge rehberi (IndicatorGuide) – derin teknik analiz için
            string indicatorContext = "";
            try
            {
                string indicatorGuidePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "IndicatorGuide.md");
                if (System.IO.File.Exists(indicatorGuidePath))
                {
                    indicatorContext = System.IO.File.ReadAllText(indicatorGuidePath);
                    indicatorContext = $@"=== GRAFİKTEKİ GÖSTERGE REHBERİ ===
{indicatorContext}
=== GÖSTERGE REHBERİ SONU ===";
                }
            }
            catch { /* indicator rehberi isteğe bağlı */ }

            // 2.5) Görselden otomatik gösterge çıkarımı (RSI/MACD/Pivot/Fibo/Divergence)
            try
            {
                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    var extractor = new IndicatorExtractor(this, msg => Logger.AI(msg));
                    var ind = await extractor.ExtractIndicatorsFromScreenshot(imagePath);
                    if (!string.IsNullOrEmpty(ind.SummaryContext))
                    {
                        technicalContext += $"\n\nGÖSTERGE ÖZETİ (Otomatik):\n{ind.SummaryContext}";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.AI($"⚠️ Guru analizinde gösterge çıkarma hatası: {ex.Message}");
            }

            // 3) Derin teknik analiz + guru onurlandırma + geçmiş analiz karşılaştırması
            string prompt = $@"Sen X'iDeAI Pro'nun mavi tikli (verified) baş analisti ve sosyal medya yöneticisisin.
Kıymetli hocamız {guruName} (@{guruHandle.TrimStart('@')}) '{tableName}' taramasında #{symbol} sembolünü vurgulayınca, grafikleri detaylı analiz ettim ve bulgularımı paylaşıyorum.

TARAMA REFERANSI: {guruName} (@{guruHandle}) - {tableName}
HİSSE: #{symbol} ({period})
ORİJİNAL TWEET: {originalTweetUrl}

TEKNİK VERİLER (Güncel + Otomatik Göstergeler - Tarama Günü):
{technicalContext}
(Boşsa uydurma rakam verme, sadece gözlemsel yorum yap. Pivot ve Fibo seviyeleri tarama günü itibariyledir.)

KRİTİK SEVİYELER (yFinance'ten hesaplanan Pivot):
{(pivotData != null ? $@"📍 PIVOT SEVİYELERİ ({pivotData.CalculatedFromDate} verilerine göre):
  • Direnç Seviyeleri: R1={pivotData.R1:F2} → R2={pivotData.R2:F2} → R3={pivotData.R3:F2}
  • Pivot: {pivotData.Pivot:F2}
  • Destek Seviyeleri: S1={pivotData.S1:F2} → S2={pivotData.S2:F2} → S3={pivotData.S3:F2}" : "❌ Pivot seviyeleri kullanılamadı (tarama dosyası eksik)")}

GEÇMİŞ TRENDLER (Son 30 gün analiz hafızası):
{(string.IsNullOrEmpty(pastContext) ? "(İlk tarama, geçmiş analiz yok)" : pastContext)}

GÖRSEL ANALİZ TALİMATI (DERİN, MANUEL ANALİZ FORMATINDA):
• **GÖSTERGE ÖNCELİKLİ**: RSI/MACD/Divergence + Fibonacci (%38.2, %50, %61.8) — Bu değerleri vurgula ve yorumla!
• Pivot Seviyeleri: P, S1-S3, R1-R3 (Tarama günü pivot'larını kullan ve tarihi belirt)
• Formasyon: Bayrak/Flama, OBO/TOBO, Çanak/Fincan-Kulp, Üçgen/Takoz, İkili Dip/Tepe
• Smart Money: Order Block, FVG, MSB (Market Structure Break), likidite boşlukları
• Destek/Direnç: Grafikten doğrula ve seviyeleri ver
• Piyasa görüşü bölümü yazma; sadece teknik ve strateji
{(string.IsNullOrEmpty(indicatorContext) ? "" : "\nGÖSTERGE REHBERİ:\n" + indicatorContext)}

RAPOR FORMAT PLANI (3 Tweet, '|||' ile ayır):
1) GIRIŞ & TESPİT: #{symbol} ({period}) sembolü grafiklere baktığımda [Özel bulgu veya formasyon]. Detaylı analiz:
2) TEKNIK ANALIZ: RSI, MACD, Divergence, **Fibonacci (%38.2/%50/%61.8)** [VURGULa!], Pivot (P/S1-S3/R1-R3), Formasyon, Smart Money (OB/FVG/MSB)
3) STRATEJİ & HASHTAG: Destek/Direnç, hedef/stop önerisi, risk uyarısı. Sona hashtag bloğu ekle:
   #BIST100 #Borsa #TeknikAnaliz #{symbol} @{guruHandle.TrimStart('@')}

KURALLAR:
• BENİM ANLAYIŞIMMIŞ GİBİ YAZ (AI markalanması yok, samimi ve kişisel)
• Placeholder bırakma; bilmiyorsan o cümleyi yazma
• Her tweette derin teknik detay (Pivot/Fibo/RSI/MACD değerleri)
• Hocaya saygılı referans ama asıl analiz SENİN gözlemim
• Verified hesap: net, profesyonel, güvenilir ton
• 3. tweete HER ZAMAN hashtag bloğu ekle (yukarıdaki gibi)";

            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                byte[] imageBytes = File.ReadAllBytes(imagePath);
                string base64 = Convert.ToBase64String(imageBytes);
                return await SendMultimodalRequest(prompt, base64);
            }

            return await SendRequest(prompt);
        }



        public async Task<string?> AnalyzeNewsForThread(string title, string source, string link = "")
        {
            string prompt = GeneratePremiumNewsAnalysisPrompt(title, source, link);
            return await SendRequest(prompt);
        }

        public async Task<string?> GeneratePerformanceSynthesis(DailyReport report)
        {
            var summaryData = new
            {
                report.TotalSignals,
                HitRate = report.TotalSignals > 0 ? (decimal)report.Winners / report.TotalSignals * 100 : 0,
                report.AvgReturn,
                report.MarketReturn,
                report.AvgAlpha,
                Strategies = report.StrategyStats.Select(s => new { s.Key, s.Value.WinRate, s.Value.TotalSignals })
            };

            string json = JsonSerializer.Serialize(summaryData);
            string prompt = _prompts.GetPerformanceReportPrompt(
                json, 
                report.BestPerformer?.Symbol ?? "N/A", 
                report.WorstPerformer?.Symbol ?? "N/A"
            );

            return await SendRequest(prompt);
        }

        private string GeneratePremiumNewsAnalysisPrompt(string title, string source, string link = "")
        {
            // Link varsa prompt'a ekle, yoksa sadece kaynak adını kullan
            string linkSection = !string.IsNullOrEmpty(link) 
                ? $"🔗 {link}" 
                : $"🔗 Kaynak: {source}";
            
            return $@"Sen Deneyimli bir Baş Ekonomist ve Stratejist'sin. 
Mavi tik'li premium hesap sahibinin haber analizidir - İKİ TWEET FORMATINDA.

Haber: {title}
Kaynak: {source}

GÖREV: İKİ TWEET ÜRETİMI (aralarında '|||' separator ile)

=== TWEET 1 (Başlık & Teaser) ===
Formatı:
🚨 [BAŞLIK - çarpıcı ve merak uyandırıcı]

📰 [1 cümle özeti]

{source} kaynaklı haberi analiz ettim. Önemi [YÜKSEK/ÇOK YÜKSEK]! 🔥
{linkSection}

#BIST100 #Haber #Borsa #SonDakika
👇 Detaylar aşağıda...

|||

=== TWEET 2 (DERİN ANALİZ - DETAYLI) ===
Formatı:
🧠 DERİN ANALİZ:

• [Doğrudan Etki - Haber ne anlama geliyor?]
• [Piyasa Reaksiyonu - Hisse/Döviz/Altın nasıl hareket edecek?]
• [Zincirleme Etki - Başka sektörler nasıl etkilenir?]
• [Makroekonomik Perspektif - Genel ekonomiye etkisi?]
• [Risk Analizi - Yukarı/aşağı senaryolar?]
• [Yatırımcı Aksiyonu - Ne yapmalı/neleri izlemeli?]

⚠️ Yatırım tavsiyesi değildir.

KURALLAR:
1. Eğer etki düşükse tüm response'u 'NO_IMPACT' yaz
2. Hashtag'ler sadece TWEET 1'de yer almalı
3. Her bullet detaylı ve anlamlı olmalı (50+ karakter, açıklayıcı)
4. Zincirleme etkiyi açıkla: Örn: 'Petrol artarsa → Havayolları kârları düşer → Turizm etkilenir → XU100 düşer'
5. ASLA placeholder/şablon ifadeler kullanma, sadece gerçek, spesifik analiz yaz
6. DERİN ANALİZ'deki her bullet point birbirinden bağımsız ama bütünsel bir resim oluştur
7. İkinci tweet'i doğrudan '🧠 DERİN ANALİZ:' ile başlat";
        }

        private string GenerateStandardNewsAnalysisPrompt(string title, string source)
        {
            return $@"Sen Deneyimli bir Baş Ekonomist ve Stratejist'sin. 
Aşağıdaki haberi küresel ve yerel piyasalar açısından derinlemesine analiz et.

Haber: {title}
Kaynak: {source}

GÖREVLER:
1. 🧠 **Analiz:** Haberin sadece ne olduğunu değil, **""İkinci Derece Etkilerini""** (Second-Order Effects) düşün. (Örn: Petrol artarsa -> Havayolları düşer -> Turizm etkilenir). Zincirleme reaksiyonları bul.
2. 🚨 **Etki Düzeyi:** Bu haber piyasalar (BIST, Döviz, Altın, Kripto) için YÜKSEK veya KRİTİK öneme sahip mi? Değilse 'NO_IMPACT' döndür.
3. #️⃣ **Hashtag:** Haberle ilgili en çok etkileşim alacak, spesifik ve akıllı MAKSİMUM 3 hashtag seç. (Sadece #Borsa deme, #TUPRS #Brent gibi spesifik ol).

ÇIKTI FORMATI (Sadece Tweet Metni):
🚨 PİYASA ALARMI: [Çarpıcı Başlık]

📰 [Haberin Özeti - 1 Cümle]

🧠 DERİN ANALİZ:
• [Doğrudan Etki]
• [Zincirleme/Dolaylı Etki - Kritik!]
• [Yatırımcı Ne Yapmalı?]

#Hashtag1 #Hashtag2 #Hashtag3 ...
⚠️ Yatırım tavsiyesi değildir.

NOT: Eğer etki düşükse sadece 'NO_IMPACT' yaz.";
        }

        public async Task<string?> GenerateGenericContent(string prompt)
        {
            return await SendRequest(prompt);
        }

        public async Task<string?> SendRequest(string prompt)
        {
            try
            {
                // _stats.RecordAiUsage(ConfigManager.Current.GeminiModel, prompt.Length / 4);
                
                if (ConfigManager.Current == null)
                {
                    LastError = "ConfigManager.Current is null";
                    Logger.Sys(LastError);
                    return null;
                }

                var apiKey = ConfigManager.Current.GeminiApiKey;
                if (string.IsNullOrEmpty(apiKey)) 
                {
                    LastError = "Gemini API Key eksik.";
                    return null;
                }

                var requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    }
                };

                string json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var model = !string.IsNullOrEmpty(ConfigManager.Current.GeminiModel) ? ConfigManager.Current.GeminiModel : "gemini-2.0-flash-exp";
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                var response = await client.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Parse JSON response
                JsonDocument jsonDoc = JsonDocument.Parse(responseContent);
                JsonElement root = jsonDoc.RootElement;
                
                string? result = null;
                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var contentElement)) // Renamed to avoid conflict with StringContent 'content'
                    {
                        if (contentElement.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                        {
                            var firstPart = parts[0];
                            if (firstPart.TryGetProperty("text", out var text))
                            {
                                result = text.GetString();
                            }
                        }
                    }
                }
                
                // OPTIMIZATION: Retry logic for empty responses (prevents API quota waste)
                if (string.IsNullOrWhiteSpace(result) || result == "null")
                {
                    Logger.AI("⚠️ Gemini boş yanıt döndü, 2 saniye sonra tekrar deneniyor...");
                    await Task.Delay(2000);
                    
                    // Retry once
                    response = await client.PostAsync(url, content); // Use 'client' instead of '_httpClient'
                    responseContent = await response.Content.ReadAsStringAsync();
                    jsonDoc = JsonDocument.Parse(responseContent);
                    root = jsonDoc.RootElement;
                    
                    if (root.TryGetProperty("candidates", out candidates) && candidates.GetArrayLength() > 0)
                    {
                        var firstCandidate = candidates[0];
                        if (firstCandidate.TryGetProperty("content", out var contentElement2)) // Renamed to avoid conflict
                        {
                            if (contentElement2.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                            {
                                var firstPart = parts[0];
                                if (firstPart.TryGetProperty("text", out var text))
                                {
                                    result = text.GetString();
                                }
                            }
                        }
                    }
                    
                    if (string.IsNullOrWhiteSpace(result))
                    {
                        Logger.AI("❌ Gemini 2. denemede de boş yanıt döndü");
                    }
                    else
                    {
                        Logger.AI("✅ Gemini 2. denemede başarılı yanıt aldı");
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Logger.AI($"❌ Gemini Exception: {ex.Message}");
            }
            return null;
        }

        public async Task<List<string>> GetAvailableModels(string apiKey)
        {
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("models", out var modelsElement))
                        {
                            var models = new List<string>();
                            foreach (var model in modelsElement.EnumerateArray())
                            {
                                var name = model.GetProperty("name").GetString();
                                if (name != null)
                                {
                                    models.Add(name.Replace("models/", ""));
                                }
                            }
                            // Filter for gemini models
                            return models.Where(m => m.ToLower().Contains("gemini")).ToList();
                        }
                    }
                }
                else
                {
                    LastError = $"HTTP {(int)response.StatusCode}: {json}";
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
            return new List<string>();
        }

        /// <summary>
        /// Send multimodal request (text + image) to Gemini
        /// </summary>
        public async Task<string?> SendMultimodalRequest(string prompt, string imageBase64)
        {
            try
            {
                if (ConfigManager.Current == null)
                {
                    LastError = "ConfigManager.Current is null";
                    return null;
                }

                var apiKey = ConfigManager.Current.GeminiApiKey;
                if (string.IsNullOrEmpty(apiKey)) 
                {
                    LastError = "Gemini API Key eksik.";
                    return null;
                }

                var requestBody = new
                {
                    contents = new[]
                    {
                        new { 
                            parts = new object[] { 
                                new { text = prompt },
                                new { 
                                    inline_data = new {
                                        mime_type = "image/png",
                                        data = imageBase64
                                    }
                                }
                            } 
                        }
                    }
                };

                string json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var model = !string.IsNullOrEmpty(ConfigManager.Current.GeminiModel) ? ConfigManager.Current.GeminiModel : "gemini-2.0-flash-exp";
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                var response = await client.PostAsync(url, content);
                var resultJson = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    using (JsonDocument doc = JsonDocument.Parse(resultJson))
                    {
                        var candidates = doc.RootElement.GetProperty("candidates");
                        if (candidates.GetArrayLength() > 0)
                        {
                            var text = candidates[0]
                                      .GetProperty("content")
                                      .GetProperty("parts")[0]
                                      .GetProperty("text").GetString();
                             return text;
                        }
                    }
                }
                else
                {
                    LastError = $"HTTP {(int)response.StatusCode}: {resultJson}";
                    Logger.AI($"❌ Gemini Multimodal API Error: {LastError}");
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Logger.AI($"❌ Gemini Multimodal Exception: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Generate market analysis WITH visual chart analysis
        /// </summary>
        public async Task<string?> GenerateMarketAnalysisWithChart(string symbol, string marketType, string priceContext, string screenshotPath, string influencerCitations = "")
        {
            try
            {
                // Convert screenshot to base64
                if (!System.IO.File.Exists(screenshotPath))
                {
                    Console.WriteLine($"Screenshot not found: {screenshotPath}");
                    // Fallback to text-only analysis
                    return await GenerateMarketAnalysisWithPrice(symbol, marketType, priceContext, influencerCitations);
                }

                byte[] imageBytes = System.IO.File.ReadAllBytes(screenshotPath);
                string imageBase64 = Convert.ToBase64String(imageBytes);

                // Load indicator guide if exists
                string indicatorContext = "";
                try
                {
                    string indicatorGuidePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "IndicatorGuide.md");
                    if (System.IO.File.Exists(indicatorGuidePath))
                    {
                        indicatorContext = System.IO.File.ReadAllText(indicatorGuidePath);
                        indicatorContext = $@"
=== GRAFİKTEKİ GÖSTERGE REHBERİ ===
{indicatorContext}
=== GÖSTERGE REHBERİ SONU ===

";
                    }
                }
                catch { /* Indicator guide optional */ }

                // Build influencer section if provided
                string influencerSection = string.IsNullOrEmpty(influencerCitations)
                    ? ""
                    : $@"

PİYASA GÖRÜŞÜ (Sosyal Medya Fenomen Analizi):
{influencerCitations}
(Bu görüşleri kendi grafiksel analizinle harmanla ve dengeli yaz)";

                // USE NEW CENTRALIZED PROMPT (Formations + Smart Money)
                string prompt = _prompts.GetDeepTechnicalAnalysisPrompt(symbol, marketType, priceContext, indicatorContext);
                prompt += influencerSection;

                return await SendMultimodalRequest(prompt, imageBase64);
            }
            catch (Exception ex)
            {
                Logger.AI($"⚠️ Error in GenerateMarketAnalysisWithChart: {ex.Message}");
                // Fallback to text-only
                return await GenerateMarketAnalysisWithPrice(symbol, marketType, priceContext, influencerCitations);
            }
        }

        /// <summary>
        /// Get the model name from config or use default
        /// </summary>
        private string GetSelectedModel()
        {
            var model = ConfigManager.Current.GeminiModel;
            return string.IsNullOrEmpty(model) ? "gemini-2.0-flash-exp" : model;
        }

        /// <summary>
        /// Get list of available Gemini models for the given API key
        /// </summary>
        public async Task<List<string>> GetAvailableModelsAsync()
        {
            var apiKey = ConfigManager.Current.GeminiApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                LastError = "API Key boş";
                return new List<string>();
            }

            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                var response = await client.GetStringAsync(url);
                
                using var doc = JsonDocument.Parse(response);
                var models = new List<string>();
                
                if (doc.RootElement.TryGetProperty("models", out var modelsArray))
                {
                    foreach (var model in modelsArray.EnumerateArray())
                    {
                        var name = model.GetProperty("name").GetString()?.Replace("models/", "");
                        if (name != null && name.Contains("gemini"))
                        {
                            models.Add(name);
                        }
                    }
                }
                
                return models;
            }
            catch (Exception ex)
            {
                LastError = $"Model listesi alınamadı: {ex.Message}";
                return new List<string>();
            }
        }

        /// <summary>
        /// Test connection to Gemini API with a simple prompt
        /// </summary>
        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            var apiKey = ConfigManager.Current.GeminiApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                return (false, "API Key girilmemiş!");
            }

            try
            {
                var model = GetSelectedModel();
                var testPrompt = "Merhaba! Bu bir API bağlantı testidir. Sadece 'Bağlantı başarılı!' yaz.";
                
                var requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = testPrompt } } }
                    }
                };

                string json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                var response = await client.PostAsync(url, content);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(result);
                    var text = doc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text").GetString();
                    
                    return (true, $"✅ Bağlantı başarılı!\nModel: {model}\nYanıt: {text?.Substring(0, Math.Min(text.Length, 100))}");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return (false, $"❌ API Hatası ({(int)response.StatusCode}): {error.Substring(0, Math.Min(error.Length, 200))}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"❌ Bağlantı hatası: {ex.Message}");
            }
        }

        public async Task<string?> SynthesizeInfluencerAnalyses(string symbol, string marketType, string priceContext, string visualAnalysis, List<InfluencerPost> influencerPosts)
        {
            // Ensure list is not null
            influencerPosts = influencerPosts ?? new List<InfluencerPost>();

            var influencerContext = "";
            if (influencerPosts.Count == 0)
            {
                influencerContext = "Henüz kayda değer influencer yorumu bulunamadı. (Nötr/Belirsiz Sentiment)";
            }
            else
            {
                for (int i = 0; i < influencerPosts.Count; i++)
                {
                    var post = influencerPosts[i];
                    influencerContext += $"\n{i + 1}. @{post.Handle}: \"{CleanTweetContent(post.Content)}\"\n   (Etkileşim: {post.Engagement})\n";
                }
            }
            
            // Geçmiş analiz kontrolü (priceContext içinde geliyorsa parse et)
            string historyNote = "";
            if (priceContext.Contains("GEÇMİŞ ANALİZ:"))
            {
                var parts = priceContext.Split(new[] { "GEÇMİŞ ANALİZ:" }, StringSplitOptions.None);
                if (parts.Length > 1)
                {
                    historyNote = "\n\n📜 GEÇMİŞ DEĞERLENDİRME:\n" + parts[1].Trim();
                    priceContext = parts[0].Trim();
                }
            }

            string prompt = $@"Sen piyasaya yıllarını vermiş usta bir tradersın.
Aşağıdaki verileri (Grafik, Fiyat, Influencer Görüşleri ve Geçmiş Analizin) harmanlayarak KENDİ ANALİZİNİ oluştur.

--- INFLUENCER GÖRÜŞLERİ ---
{influencerContext}
----------------------------
--- TEKNİK VERİLER ---
{priceContext}
GRAFİK ANALİZİ:
{visualAnalysis}{historyNote}
--------------------------------

GÖREV:
#{symbol} için kişisel, samimi ve profesyonel bir analiz yazısı hazırla.

⚠️ KRİTİK KURALLAR (HATA YAPMA):
1. **BAZ ANALİZ ÖNCELİĞİ:** Eğer `TEKNİK VERİLER` içinde ""KOMPOZİT ANALİZ"" veya ""DÖVİZ BAZLI ANALİZ"" uyarısı varsa, analizi tamamen bu baz üzerinden kurgula. XU100 bazında ise mutlaka ""Kompozit Analiz"" terminolojisini kullan.
2. **İLGİSİZ TWEETLERİ YOK SAY:** Influencer listesinde eğer #{symbol} ile DOĞRUDAN ilgili olmayan (genel piyasa, siyaset veya başka hisse) tweetler varsa, onları SENTEZE DAHİL ETME.
3. **TEKNİK VERİYİ KORU:** Görsel analizden gelen Teknik Seviyeleri (Destek, Direnç, Order Block, FVG, Fiyatlar) ASLA değiştirme, yuvarlama veya yumuşatma. Orijinal rapordaki rakam neyse onu kullan.
3. **YÖN ve TERMİNOLOJİ:**
    - Eğer düşüş bekliyorsan (Short) MUTLAKA ""AÇIĞA SATIŞ"" veya ""DÜŞÜŞ YÖNLÜ"" terimini kullan.
    - Açığa Satış (Short) stratejisinde: HEDEF fiyat GÜNCEL fiyattan DÜŞÜK, STOP fiyatı GÜNCEL fiyattan YÜKSEK olmalıdır. (Mantık hatası yapma!)
    - Yükseliş bekliyorsan (Long) ""UZUN POZİSYON"" veya ""YÜKSELİŞ YÖNLÜ"" de.
4. **SAMİMİYET vs VERİ:** Üslubun samimi olsun (""'Baktığımda'"", ""'Görüyorum ki'"") AMMA teknik veriler (Rakamlar) robotik kesinlikte kalsın.

Yazıyı İKİ BÖLÜME ayır ve aralarına '|||' işareti koy.

BÖLÜM 1 (Genel Görünüm - Max 260 karakter):
- Grafikte ne gördüğünü net anlat.
- Influencer havasını (Korku/Coşku) süzgeçten geçir.
- Geçmiş analizin varsa tutarlılığını yorumla.
- ""Açığa Satış"" veya ""Uzun Pozisyon"" yönünü belirt.

|||

BÖLÜM 2 (Seviyeler ve Strateji - Max 260 karakter):
- Kritik destek/dirençleri NET RAKAMLA ver.
- Stratejini kur: ""Hedef: X, Stop: Y"" (Short ise Stop > Giriş > Hedef kuralına uy).
- Risk uyarısı yap.
- SON CÜMLE OLARAK: Takipçilere soru sor (CTA). Örn: ""Sizce yön neresi? Yorumlara yazın"" veya ""Sırada hangi hisseyi inceleyelim?""

KURALLAR:
- ASLA Kollektif Zeka, Rapor, Bot, Merhaba deme.
- ""Yatırım tavsiyesi değildir"" EKLEME (Sistem ekliyor).
- Emoji kullan ama abartma.

ÖRNEK SHORT ÇIKTI:
Genel Görünüm: THYAO'da 272.35 altındaki zayıf seyir ""Düşüş Yönlü"" (Açığa Satış) senaryomu teyit ediyor. Kurumsal satış baskısı (OB) net. Ayılar sahada, tepkiler cılız kalıyor.
|||
Açığa Satış Stratejim: Direnç 276.00 TL (Stop). Hedefim 260.00 TL ana kale. 265'teki likidite boşluğu dolmadan dönüş beklemiyorum. Oyun planım sabır. Sizce düşüş sürer mi? Yorumları alalım. 👇";


            return await SendRequest(prompt);
        }

        public async Task<string?> GenerateMotivationTweet()
        {
            string prompt = _prompts.GetMotivationPrompt();
            return await SendRequest(prompt);
        }

        public async Task<string?> GenerateCloseSummary(string marketData)
        {
            string prompt = $@"Sen profesyonel, samimi bir borsa yorumcususun.
GÖREV: BIST100 kapanışı itibariyle verileri yorumla ve 'Günü Bitirirken' tweeti at.

VERİLER:
{marketData}

KURALLAR:
1. Piyasaların kapandığını belirt.
2. Verilen rakamları (BIST, Dolar, Altın) kısaca özetle (yükseldi/düştü/sakin gibi yorumla).
3. Genel bir değerlendirme yap (Boğa/Ayı/Nötr).
4. 'Yarın yeni fırsatlar' mesajı ver.
5. #BIST100 #Borsa #Altın #Dolar hashtaglerini ekle.
6. Maksimum 280 karakter.
7. ASLA '[...]' gibi placeholder/şablon kullanma! Sadece tam cümleler kur.
8. Veri yoksa 'Piyasa verileri karışık' de.";

            return await SendRequest(prompt);
        }

        public async Task<string?> GenerateReply(string tweetContent, string authorHandle)
        {
            string prompt = _prompts.GetReplyPrompt(tweetContent, authorHandle);
            return await SendRequest(prompt);
        }

        public async Task<string?> GenerateMarketCloseTableTweet(string indicesData, string topGainers, string topLosers, string topVolume)
        {
            string prompt = _prompts.GetMarketClosePrompt(indicesData, topGainers, topLosers, topVolume);
            return await SendRequest(prompt);
        }

        private string CleanTweetContent(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            raw = System.Text.RegularExpressions.Regex.Replace(raw, @"http[s]?://\S+", "");
            raw = raw.Trim();
            if (raw.Length > 200) raw = raw.Substring(0, 200) + "...";
            return raw;
        }
    }
}


