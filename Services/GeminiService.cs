using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using XiDeAI_Pro.Config;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.IO;

namespace XiDeAI_Pro.Services
{
    public class GeminiService
    {
        private static readonly HttpClient client = new HttpClient() { Timeout = TimeSpan.FromSeconds(120) };
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1); // v3.5.2: Sequential AI Traffic Control
        private readonly PromptManager _prompts = new PromptManager();
        private readonly MemoryEngine _memory;
        private readonly StatsEngine _stats;
        public string LastError { get; private set; } = "";
        
        // Multi-Model Fallback Support
        public XiDeAI_Pro.Services.AI.ModelManager? ModelManager { get; set; }
        
        // v4.0.0: AI Training Data Collection
        private static readonly string TrainingDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XiDeAI", "training_data.jsonl");
        
        /// <summary>
        /// Logs prompt-response pairs for local AI fine-tuning
        /// </summary>
        private void LogTrainingData(string prompt, string response, string taskType = "general")
        {
            try
            {
                // Ensure directory exists
                var dir = Path.GetDirectoryName(TrainingDataPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                
                var entry = new
                {
                    timestamp = DateTime.UtcNow.ToString("o"),
                    task = taskType,
                    prompt = prompt,
                    response = response
                };
                
                string jsonLine = JsonSerializer.Serialize(entry);
                File.AppendAllText(TrainingDataPath, jsonLine + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Logger.Sys($"⚠️ Training data log failed: {ex.Message}");
            }
        }

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
                indicatorContext, 
                sig.Period,
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
        public async Task<string?> GenerateMarketAnalysisWithPrice(string symbol, string marketType, string priceContext, string influencerCitations = "", string newsContext = "", string marketOverview = "")
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

            string prompt = _prompts.GetDeepManualAnalysisPrompt(symbol, marketType, priceContext, indicatorContext, influencerCitations, newsContext, marketOverview);

            return await SendRequest(prompt);
        }

        public async Task<string?> AnalyzeNewsImpact(string title, string source)
        {
            // v3.0: Editor Mode (Confidence Scoring)
            string prompt = _prompts.GetNewsEditorPrompt(title, source);

            if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
            {
                return await ModelManager.SendRequest(XiDeAI_Pro.Services.AI.TaskType.NewsAnalysis, prompt);
            }

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

        public async Task<string?> GenerateGuruHonoringThread(string symbol, string period, string guruHandle, string originalTweetUrl, string tableName = "EFE HMA", string guruName = "Efelerin Efesi", string technicalContext = "", string? imagePath = null, string visualContext = "", string priceContext = "")
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

            // 3) Prompt çağrısı (Visual + Price context eklendi)
            string prompt = _prompts.GetGuruHonoringThreadPrompt(
                symbol, 
                tableName, // E.g., "EFE HMA"
                "N/A",           
                priceContext, 
                technicalContext, 
                guruName, 
                guruHandle, // Yeni parametre
                $"{guruName} (@{guruHandle}) - {tableName}",
                visualContext
            );

            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                byte[] imageBytes = File.ReadAllBytes(imagePath);
                string base64 = Convert.ToBase64String(imageBytes);
                return await SendMultimodalRequest(prompt, base64);
            }

            return await SendRequest(prompt);
        }

        /// <summary>
        /// Sadece grafiği analiz edip teknik verileri metin olarak döner.
        /// </summary>
        public async Task<string> AnalyzeChartImage(string symbol, string imagePath)
        {
            if (!File.Exists(imagePath)) return "Grafik bulunamadı.";

            string prompt = $@"GÖREV: #{symbol} grafiğini bir teknik analist gözüyle incele.
Aşağıdakileri maddeler halinde çıkar:
1. Trend Yönü (Yükselen/Düşen/Yatay)
2. Kritik Destek ve Dirençler (Fiyat etiketlerini oku)
3. Formasyonlar (Varsa: Bayrak, Flama, OBO, TOBO, İkili Dip vb.)
4. Mum Çubukları (Doji, Engulfing, Pinbar gibi belirgin yapılar)
5. İndikatörler (RSI, MACD, Hareketli Ortalamalar - grafikte ne görüyorsan)

ÇIKTI: Sadece teknik verileri, net ve kısa cümlelerle yaz. Yorum katma, sadece gördüğünü raporla.";

            byte[] imageBytes = File.ReadAllBytes(imagePath);
            string base64 = Convert.ToBase64String(imageBytes);
            
            var result = await SendMultimodalRequest(prompt, base64);
            return result ?? "Görsel analiz yapılamadı.";
        }


        /// <summary>
        /// Yeni bir fenomenin "Guru" (Hoca) olmaya uygun olup olmadığını analiz eder.
        /// </summary>
        public async Task<bool> AnalyzePotentialGuru(string content, string author)
        {
            try
            {
                var prompt = $@"
Sen bir 'Hoca Kaşifi' yapay zekasın. 
Aşağıdaki paylaşımın sahibi olan @{author} kullanıcısının paylaştığı içerik, bir finansal guru (Hoca) kalitesinde mi?

İÇERİK: {content}

KRİTERLER:
- Paylaşım teknik analiz, piyasa stratejisi veya derin finansal bilgi içeriyor mu?
- Kişisel anı veya alakasız yorumlardan uzak mı?
- Başkalarına rehberlik edecek nitelikte mi?

CEVAP: Sadece 'EVET' veya 'HAYIR' cevabını ver. Başka açıklama yapma.
";
                string? response = null;
                if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
                {
                    response = await ModelManager.SendRequest(XiDeAI_Pro.Services.AI.TaskType.PotentialGuruAnalysis, prompt);
                }
                else
                {
                    response = await SendRequest(prompt, 0.3);
                }

                return (response ?? "").Trim().ToUpper().Contains("EVET");
            }
            catch { return false; }
        }



        public async Task<string?> AnalyzeNewsForThread(string title, string source, string summary, string link = "", string? description = null, bool isFlash = false)
        {
            string prompt = GeneratePremiumNewsAnalysisPrompt(title, source, summary, link, description, isFlash);

            if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
            {
                return await ModelManager.SendRequest(XiDeAI_Pro.Services.AI.TaskType.NewsThreadGeneration, prompt);
            }

            // v4.6.6: Lower temperature for news threads (0.3) for stability and consistent separator usage
            return await SendRequest(prompt, temperature: 0.3);
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

        private string GeneratePremiumNewsAnalysisPrompt(string title, string source, string summary, string link = "", string? description = null, bool isFlash = false)
        {
            // v4.6.18: X Algorithm & Distribution Hack Optimized Prompt
            string linkSection = !string.IsNullOrEmpty(link) ? $"🔗 {link}" : $"🔗 Kaynak: {source}";
            
            return $@"KİMLİK: Sen deneyimli ve profesyonel bir Baş Ekonomist ve Stratejistsin.

GÖREV: Aşağıdaki haberi, X platformunda viral olacak, derinlemesine yorumlayan, 3 ile 4 tweet arasında değişen bir analiz thread'ine dönüştür. Haberin sadece ekonomik etkileri değil, toplumsal, jeopolitik ve stratejik silsilesini (Second-Order Effects) düşün.
Haberin önemi yüksekse bunu yansıt — 'güvenli' ve sıkıcı olmaktan kaçın. Okuyucuyu durduracak bir kanca kur.

HABER BİLGİLERİ:
- Başlık: {title}
- Özet: {summary}
- Kaynak: {source}
- Link: {linkSection}

X ALGORİTMASI DAĞITIM STRATEJİLERİ (UYGULA):
1. 🎯 AKILLI ETİKETLEME (Auto-Mention): SADECE resmi kurumları, kamu şirketlerini veya zararsız teknoloji oluşumlarını etiketle (Örn: @TCMB, @aselsan, @Savunma_SSB). KESİN YASAK: Asla siyasetçileri, parti liderlerini, bakanları veya tartışmalı figürleri etiketleme. Bireyleri etiketlemekten uzak dur, metne doğal yedir ancak @ kullanma.
2. 🧲 KANCA (Hook): İlk tweet o kadar çarpıcı olmalı ki kullanıcı kaydırmayı durdurmalı. Şok edici bir veri, retorik soru veya sarsıcı bir tespitle başla.
3. 🧠 DERİN ANALİZ: Haberin yüzeyini değil, altını analiz et. ""Bu neden oldu?"" ve ""Bunun sonucunda 3 ay, 1 yıl sonra ne olacak?"" sorularını cevapla. Zincirleme etkiler (domino) ortaya çıkar.
4. 📈 SEMANTİK KELİMELER: X algoritmasının ""For You"" kısmında öne çıkardığı anahtar kelimeleri (Borsa, Ekonomi, Teknoloji, Yatırım, Savunma, Jeopolitik) hashtag kullanmadan metne doğal yedir.

ÇIKTI FORMATI VE KURALLAR:
- Analizini KISA ve ÖZ şekilde 3 veya 4 tweet halinde yaz. Asla destan yazma, kelime israfından kaçın. Her tweet 270 karakterin ALTINDA KALMAK ZORUNDADIR. (Haber çok önemliyse 4, orta düzeydeyse 3 tweet.)
- Tweetleri birbirinden ayırmak için KESİNLİKLE aralarına üç boru karakteri koy (ayırıcı).
- Bolca profesyonel emoji kullan.
- İlk tweet, çarpıcı kancayı ve sonuna {linkSection} eklemeli.
- Son tweet akıllı hashtagler (#BIST100 #Ekonomi #Savunma vb.) ve Y.T.D. ile bitmelidir.
- Tweet numaraları veya bölüm başlıkları ASLA yazma. Çıktın SADECE tweet metinleri ve aralarındaki ayırıcılardan oluşmalı.";

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
            if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
            {
                // v3.6.6: Default to GeneralAnalysis if no specific task provided
                return await ModelManager.SendRequest(XiDeAI_Pro.Services.AI.TaskType.GeneralAnalysis, prompt);
            }
            return await SendRequest(prompt);
        }

        // New overload for specific task types
        public async Task<string?> GenerateGenericContent(string prompt, XiDeAI_Pro.Services.AI.TaskType taskType)
        {
            if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
            {
                return await ModelManager.SendRequest(taskType, prompt);
            }
            return await SendRequest(prompt);
        }

        public async Task<string?> SendRequest(string prompt, double temperature = 0.5, double topP = 0.95, int topK = 40, int maxOutputTokens = 2048)
        {
            await _semaphore.WaitAsync(); // v3.5.2: Queue requests
            try 
            {
                // Wait 500ms between requests to avoid burst rate limits
                await Task.Delay(500).ConfigureAwait(false);

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
                    },
                    generationConfig = new
                    {
                        temperature = temperature,
                        topP = topP,
                        topK = topK,
                        maxOutputTokens = maxOutputTokens
                    }
                };

                string json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var model = !string.IsNullOrEmpty(ConfigManager.Current.GeminiModel) ? ConfigManager.Current.GeminiModel : "gemini-2.0-flash-exp";
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                
                var response = await client.PostAsync(url, content).ConfigureAwait(false);
                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode == 429) // Too Many Requests
                    {
                        Logger.AI("⚠️ Gemini API 429 (Too Many Requests) hatası. 10 saniye bekleniyor...");
                        await Task.Delay(10000).ConfigureAwait(false);
                        
                        // Retry with backoff
                        response = await client.PostAsync(url, content).ConfigureAwait(false);
                        responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        
                        if (!response.IsSuccessStatusCode)
                        {
                            LastError = $"Gemini 429 Retry Failed. Status: {response.StatusCode}";
                            Logger.AI($"❌ {LastError}");
                            return null;
                        }
                    }
                    else
                    {
                        LastError = $"Gemini API Error ({response.StatusCode}): {responseContent}";
                        Logger.AI($"❌ {LastError}");
                        return null;
                    }
                }
                
                // Parse JSON response
                string? result = null;
                using (JsonDocument jsonDoc = JsonDocument.Parse(responseContent))
                {
                    JsonElement root = jsonDoc.RootElement;
                    if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var firstCandidate = candidates[0];
                        if (firstCandidate.TryGetProperty("content", out var contentElement))
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
                }
                
                // OPTIMIZATION: Retry logic for empty responses (prevents API quota waste)
                if (string.IsNullOrWhiteSpace(result) || result == "null")
                {
                    Logger.AI("⚠️ Gemini boş yanıt döndü, 3 saniye sonra tekrar deneniyor...");
                    await Task.Delay(3000).ConfigureAwait(false);
                    
                    // Retry once
                    response = await client.PostAsync(url, content).ConfigureAwait(false);
                    responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        using (JsonDocument jsonDoc = JsonDocument.Parse(responseContent))
                        {
                            JsonElement root = jsonDoc.RootElement;
                            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                            {
                                var firstCandidate = candidates[0];
                                if (firstCandidate.TryGetProperty("content", out var contentElement2))
                                {
                                    if (contentElement2.TryGetProperty("parts", out var partsRetry) && partsRetry.GetArrayLength() > 0)
                                    {
                                        var firstPart = partsRetry[0];
                                        if (firstPart.TryGetProperty("text", out var text))
                                        {
                                            result = text.GetString();
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    if (string.IsNullOrWhiteSpace(result))
                    {
                        // Check for Safety Block or other feedback
                        string failReason = "Gemini returned empty response.";
                        try {
                            using (JsonDocument checkDoc = JsonDocument.Parse(responseContent)) {
                                if (checkDoc.RootElement.TryGetProperty("promptFeedback", out var feedback)) {
                                    if (feedback.TryGetProperty("blockReason", out var reason)) {
                                        failReason = $"Gemini BLOCKED: {reason.GetString()}";
                                    }
                                }
                            }
                        } catch {}

                        LastError = failReason;
                        Logger.AI($"❌ {LastError}");
                    }
                    else
                    {
                        Logger.AI("✅ Gemini 2. denemede başarılı yanıt aldı");
                    }
                }
                
                // v4.0.0: Log successful prompt-response for AI training
                if (!string.IsNullOrWhiteSpace(result))
                {
                    LogTrainingData(prompt, result, "text_generation");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Logger.AI($"❌ Gemini Exception: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
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
            await _semaphore.WaitAsync();
            try
            {
                // Wait 500ms between requests to avoid burst rate limits
                await Task.Delay(500).ConfigureAwait(false);

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
                
                var response = await client.PostAsync(url, content).ConfigureAwait(false);
                var resultJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                
                if (!response.IsSuccessStatusCode && (int)response.StatusCode == 429)
                {
                    Logger.AI("⚠️ Gemini Multimodal 429 hatası. 10 saniye bekleniyor...");
                    await Task.Delay(10000).ConfigureAwait(false);
                    response = await client.PostAsync(url, content).ConfigureAwait(false);
                    resultJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }

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
                            
                            // v4.0.0: Log successful multimodal prompt-response for AI training
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                LogTrainingData(prompt, text, "vision_analysis");
                            }
                            
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
                Logger.AI($"❌ Gemini Multimodal Exception: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
            return null;
        }

        /// <summary>
        /// Generate market analysis WITH visual chart analysis
        /// </summary>
        public async Task<string?> GenerateMarketAnalysisWithChart(string symbol, string marketType, string priceContext, string screenshotPath, string influencerCitations = "", string newsContext = "", string marketOverview = "")
        {
            try
            {
                // Convert screenshot to base64
                if (!System.IO.File.Exists(screenshotPath))
                {
                    Console.WriteLine($"Screenshot not found: {screenshotPath}");
                    // Fallback to text-only analysis
                    return await GenerateMarketAnalysisWithPrice(symbol, marketType, priceContext, influencerCitations, newsContext, marketOverview);
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
                string prompt = _prompts.GetDeepTechnicalAnalysisPrompt(symbol, marketType, priceContext, indicatorContext, influencerCitations, newsContext, marketOverview);

                return await SendMultimodalRequest(prompt, imageBase64);
            }
            catch (Exception ex)
            {
                Logger.AI($"⚠️ Error in GenerateMarketAnalysisWithChart: {ex.Message}");
                // Fallback to text-only
                return await GenerateMarketAnalysisWithPrice(symbol, marketType, priceContext, influencerCitations, newsContext, marketOverview);
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

            string prompt = _prompts.GetSignalSynthesisPrompt(symbol, priceContext, visualAnalysis, influencerContext, historyNote);
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

        public async Task<string?> GenerateFanZoneReaction(string prompt)
        {
            // FanZone Specific High-Creativity Config
            // Temp: 0.8, TopP: 0.95, TopK: 40, MaxTokens: 150
            return await SendRequest(prompt, 0.8, 0.95, 40, 150);
        }

        // ===========================
        // TWO-STEP BOT INTERACTION (v4.2.0)
        // ===========================

        /// <summary>
        /// Step 1: Tweet kategorisini tespit eder
        /// </summary>
        public async Task<string> DetectTweetCategory(string tweetContent)
        {
            try
            {
                string prompt = _prompts.GetCategoryDetectionPrompt(tweetContent);
                // Category detection uses low temperature for consistency
                var result = await SendRequest(prompt, 0.2, 0.9, 20, 30);
                
                if (!string.IsNullOrEmpty(result))
                {
                    // Clean and normalize the category
                    string category = result.Trim().ToUpper()
                        .Replace("\"", "")
                        .Replace(".", "")
                        .Replace(":", "");
                    
                    // Validate against known categories
                    string[] validCategories = { "FINANS", "KULTUR_EGLENCE", "MILLI_TOPLUM", "BILGE_KULTUR", "INSAN_RUH", "GUNLUK_MIZAH" };
                    if (validCategories.Contains(category))
                    {
                        Logger.AI($"📊 Kategori Tespiti: {category}");
                        return category;
                    }
                }
                
                Logger.AI("⚠️ Kategori tespiti başarısız, GUNLUK_MIZAH fallback kullanılıyor.");
                return "GUNLUK_MIZAH"; // Default fallback
            }
            catch (Exception ex)
            {
                Logger.AI($"❌ Kategori tespiti hatası: {ex.Message}");
                return "GUNLUK_MIZAH";
            }
        }

        /// <summary>
        /// Step 2: Kategoriye özel yanıt üretir
        /// </summary>
        public async Task<string?> GenerateCategorizedReply(string category, string tweetContent, string authorHandle)
        {
            try
            {
                // Get category-specific prompt
                string prompt = _prompts.GetCategorizedReplyPrompt(category, tweetContent, authorHandle);
                
                // Get category-specific AI config
                var config = _prompts.GetCategoryConfig(category);
                
                Logger.AI($"🤖 Yanıt üretiliyor: Kategori={category}, Temp={config.Temp}");
                
                return await SendRequest(prompt, config.Temp, config.TopP, config.TopK, config.MaxTokens);
            }
            catch (Exception ex)
            {
                Logger.AI($"❌ Kategorize yanıt hatası: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Full Two-Step Reply Generation (Convenience Method)
        /// </summary>
        public async Task<(string Category, string? Reply)> GenerateTwoStepReply(string tweetContent, string authorHandle)
        {
            // Step 1: Detect Category
            string category = await DetectTweetCategory(tweetContent);
            
            // Step 2: Generate Categorized Reply
            string? reply = await GenerateCategorizedReply(category, tweetContent, authorHandle);
            
            return (category, reply);
        }

        // ===========================
        // NEWS TWO-STEP LOGIC (v4.2.2)
        // ===========================

        /// <summary>
        /// Step 1: Haber kategorisini tespit eder
        /// </summary>
        public async Task<string> DetectNewsCategory(string title, string source)
        {
            string prompt = _prompts.GetNewsCategoryDetectionPrompt(title, source);
            
            string? response = null;
            if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
            {
                response = await ModelManager.SendRequest(XiDeAI_Pro.Services.AI.TaskType.NewsAnalysis, prompt);
            }
            else
            {
                response = await SendRequest(prompt, 0.2); // Düşük sıcaklık, tutarlı kategori
            }
            
            // Parse kategori - sadece ilk kelimeyi al
            if (!string.IsNullOrEmpty(response))
            {
                string cleaned = response.Trim().ToUpper()
                    .Replace(".", "")
                    .Replace(":", "")
                    .Split('\n')[0]
                    .Split(' ')[0];
                
                // Geçerli kategoriler
                string[] validCategories = { "EKONOMI", "SIYASET", "TEKNOLOJI", "GLOBAL", "KRIPTO", "SPOR", "YASAM" };
                if (validCategories.Contains(cleaned))
                    return cleaned;
            }
            
            return "EKONOMI"; // Fallback
        }

        /// <summary>
        /// Full Two-Step News Analysis (1-10 Scale + Category)
        /// Önce kategori tespit eder, sonra kategoriye göre puanlar
        /// </summary>
        public async Task<string?> AnalyzeNewsImpactTwoStep(string title, string source)
        {
            // Step 1: Kategori Tespiti
            string category = await DetectNewsCategory(title, source);
            Logger.AI($"📰 Haber Kategorisi: {category}");
            
            // Step 2: Kategoriye özel puanlama (1-10)
            string prompt = _prompts.GetNewsEditorPromptV2(title, source, category);
            
            // Kategoriye göre config al
            var config = _prompts.GetNewsCategoryConfig(category);
            
            if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
            {
                return await ModelManager.SendRequest(XiDeAI_Pro.Services.AI.TaskType.NewsAnalysis, prompt);
            }
            
            return await SendRequest(prompt, config.Temp, config.TopP, config.TopK, config.MaxTokens);
        }

        /// <summary>
        /// Kategoriye özel haber analiz thread'i üretir
        /// Sadece skor 9-10 olan haberler için kullanılır
        /// </summary>
        public async Task<string?> GenerateNewsCategoryAnalysis(string category, string title, string source, string link, string? description = null, bool isFlash = false)
        {
            string prompt = _prompts.GetNewsCategoryAnalysisPrompt(category, title, source, link, description, isFlash);
            
            // Kategoriye göre config al
            var config = _prompts.GetNewsCategoryConfig(category);
            
            if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
            {
                return await ModelManager.SendRequest(XiDeAI_Pro.Services.AI.TaskType.NewsThreadGeneration, prompt);
            }
            
            return await SendRequest(prompt, config.Temp, config.TopP, config.TopK, config.MaxTokens);
        }

        /// <summary>
        /// v4.3.0: Hybrid Signal Intelligence - Tier Based Generation
        /// </summary>
        public async Task<string?> GenerateStrategySpecificAnalysis(SignalData sig, string priceContext, string influencerCitations)
        {
            try
            {
                Logger.AI($"🧠 Hybrid Analysis ({sig.Strategy}): Tier={sig.Tier}, Score={sig.FinalScore}");
                
                string prompt = _prompts.GetStrategySpecificPrompt(sig, priceContext, influencerCitations);
                
                // Tier bazlı token optimizasyonu
                int maxTokens = sig.Tier switch
                {
                    ContentTier.Premium => 1500, // Detaylı
                    ContentTier.Standard => 1000, // Standart
                    ContentTier.Summary => 600,  // Özet
                    _ => 300
                };

                // ModelManager varsa (v3.1+)
                if (ModelManager != null && ConfigManager.Current?.EnableMultiModel == true)
                {
                    // Premium threadler için daha güçlü model kullanılabilir
                    var taskType = sig.Tier == ContentTier.Premium 
                        ? XiDeAI_Pro.Services.AI.TaskType.DeepScan 
                        : XiDeAI_Pro.Services.AI.TaskType.ShortThreadGeneration;
                        
                    return await ModelManager.SendRequest(taskType, prompt, maxTokens: maxTokens);
                }
                
                // Standart Gemini isteği
                return await SendRequest(prompt, maxOutputTokens: maxTokens);
            }
            catch (Exception ex)
            {
                Logger.Sys($"Hybrid Analysis Error: {ex.Message}");
                return null;
            }
        }


        public async Task<string?> GenerateMarketCloseTableTweet(string indicesData, string topGainers, string topLosers, string topVolume)
        {
            string prompt = _prompts.GetMarketClosePrompt(indicesData, topGainers, topLosers, topVolume);
            return await SendRequest(prompt);
        }

        /// <summary>
        /// Enhanced tweet content cleaner (v3.8)
        /// - Removes URLs
        /// - Reduces excessive emojis
        /// - Cleans multiple spaces/newlines
        /// - Truncates to max length
        /// </summary>
        private string CleanTweetContent(string raw, int maxLength = 200)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            
            // 1. Remove URLs
            raw = System.Text.RegularExpressions.Regex.Replace(raw, @"http[s]?://\S+", "");
            
            // 2. Reduce excessive emojis (max 3 consecutive emoji)
            raw = System.Text.RegularExpressions.Regex.Replace(raw, @"([\u{1F600}-\u{1F64F}\u{1F300}-\u{1F5FF}\u{1F680}-\u{1F6FF}\u{2600}-\u{26FF}]){4,}", "$1$1$1");
            
            // 3. Clean multiple spaces and newlines
            raw = System.Text.RegularExpressions.Regex.Replace(raw, @"\s{2,}", " ");
            raw = System.Text.RegularExpressions.Regex.Replace(raw, @"\n{2,}", "\n");
            
            // 4. Trim whitespace
            raw = raw.Trim();
            
            // 5. Truncate if too long
            if (raw.Length > maxLength) 
                raw = raw.Substring(0, maxLength) + "...";
            
            return raw;
        }

        /// <summary>
        /// Generate engaging 4-tweet thread with optional history callback
        /// Uses visual analysis + influencer context + past successful analysis
        /// </summary>
        public async Task<string?> GenerateShortThreadWithHistory(
            string symbol, 
            string marketType, 
            string priceContext, 
            string visualAnalysis,
            string influencerContext,
            string periyot,
            string? screenshotPath = null,
            string lastWeekAnalysis = "")
        {
            try
            {
                Logger.AI($"📝 Generating Short Thread for #{symbol} ({periyot})...");

                // Get the prompt from PromptManager
                string prompt = _prompts.GetShortThreadPromptWithHistory(
                    symbol, 
                    marketType, 
                    priceContext, 
                    visualAnalysis, 
                    influencerContext, 
                    periyot,
                    lastWeekAnalysis
                );

                string? response;

                // If screenshot available, use multimodal
                if (!string.IsNullOrEmpty(screenshotPath) && File.Exists(screenshotPath))
                {
                    Logger.AI($"🖼️ Using multimodal with screenshot: {screenshotPath}");
                    byte[] imageBytes = File.ReadAllBytes(screenshotPath);
                    string imageBase64 = Convert.ToBase64String(imageBytes);
                    response = await SendMultimodalRequest(prompt, imageBase64);
                }
                else
                {
                    response = await SendRequest(prompt);
                }

                if (string.IsNullOrEmpty(response))
                {
                    Logger.AI("⚠️ AI returned empty response for short thread");
                    return null;
                }

                Logger.AI($"✅ Short thread generated: {response.Substring(0, Math.Min(100, response.Length))}...");
                return response;
            }
            catch (Exception ex)
            {
                Logger.AI($"❌ Short thread generation error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get last week's successful analysis for a symbol from memory
        /// Returns formatted string for history callback feature
        /// </summary>
        public string GetLastWeekSuccessfulAnalysis(string symbol)
        {
            try
            {
                if (_memory == null) return "";

                // Get symbol context from memory (includes past analyses)
                string context = _memory.GetSymbolContext(symbol);
                
                if (string.IsNullOrEmpty(context))
                    return "";

                // Look for keywords indicating successful predictions
                if (context.Contains("hedef") || context.Contains("Hedef") || 
                    context.Contains("başarı") || context.Contains("tuttu") ||
                    context.Contains("beklenen"))
                {
                    // Extract the relevant part (first 200 chars)
                    string summary = context.Length > 200 
                        ? context.Substring(0, 200) + "..." 
                        : context;
                    
                    Logger.AI($"📜 Found past analysis for {symbol}: {summary.Substring(0, Math.Min(50, summary.Length))}...");
                    return summary;
                }

                return "";
            }
            catch (Exception ex)
            {
                Logger.AI($"⚠️ Error fetching past analysis: {ex.Message}");
                return "";
            }
        }

    }
}


