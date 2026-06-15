using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XiDeAI_Pro.Services
{
    /// <summary>
    /// v3.0 Akıllı Hafıza Motoru (Knowledge Base)
    /// Tweetleri depolar, kategorize eder ve anlık analizlerde kaynak olarak sunar.
    /// </summary>
    public class MemoryEngine
    {
        private readonly string _historyPath; // Analiz geçmişi (Eski yapı)
        private readonly string _knowledgeBasePath; // Tweet veritabanı (Yeni yapı)
        
        // Symbol -> Tweets Index
        private Dictionary<string, List<TweetMemoryItem>> _knowledgeIndex;
        // Raw Timeline (Hronolojik)
        private List<TweetMemoryItem> _rawTimeline;
        // v3.2 Interaction Memory
        private HashSet<string> _interactionMemory;

        // Eski analiz hafızası (Legacy support)
        private Dictionary<string, List<AnalysisMemory>> _analysisMemory;
        private const int MaxHistoryPerSymbol = 3;

        public event Action<string, string>? OnLog;

        public MemoryEngine(string storagePath)
        {
            _historyPath = storagePath;
            // KnowledgeBase dosyasını history dosyasıyla aynı klasörde tut
            _knowledgeBasePath = Path.Combine(Path.GetDirectoryName(storagePath) ?? "", "KnowledgeBase.json");

            _knowledgeIndex = new Dictionary<string, List<TweetMemoryItem>>(StringComparer.OrdinalIgnoreCase);
            _rawTimeline = new List<TweetMemoryItem>();
            _interactionMemory = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _analysisMemory = new Dictionary<string, List<AnalysisMemory>>(StringComparer.OrdinalIgnoreCase);

            Load();
        }

        #region Core Knowledge Methods (v3.0)

        /// <summary>
        /// Yeni bir tweet'i hafızaya kaydeder.
        /// </summary>
        public bool Learn(InfluencerPost post)
        {
            if (post == null || string.IsNullOrWhiteSpace(post.Content)) return false;

            // 1. Duplicate Check (URL veya İçerik Hash)
            if (_rawTimeline.Any(t => t.Url == post.Url)) return false;

            // 2. Extract Symbols (Regex)
            var symbols = ExtractSymbols(post.Content);

            var memoryItem = new TweetMemoryItem
            {
                Id = Guid.NewGuid().ToString(),
                Author = post.Handle,
                Content = post.Content,
                PostDate = post.PostDate,
                FetchedAt = DateTime.Now,
                Url = post.Url,
                RelevanceScore = post.RelevanceScore,
                Engagement = post.Engagement,
                RelatedSymbols = symbols
            };

            // 3. Store in Raw Timeline
            _rawTimeline.Add(memoryItem);

            // 4. Index by Symbol
            foreach (var sym in symbols)
            {
                if (!_knowledgeIndex.ContainsKey(sym))
                    _knowledgeIndex[sym] = new List<TweetMemoryItem>();
                
                _knowledgeIndex[sym].Add(memoryItem);
            }

            // 5. Auto-Save (Throttle yapılabilir ama şimdilik her işlemde)
            // SaveKnowledgeBase(); // Performans için manuel çağrılması daha iyi olabilir
            
            return true;
        }

        /// <summary>
        /// Belirli bir sembol için hafızadaki tweetleri getirir.
        /// </summary>
        public List<TweetMemoryItem> Recall(string symbol, int maxAgeHours = 24)
        {
            var result = new List<TweetMemoryItem>();
            
            // 1. Sembol indeksinden getir
            if (_knowledgeIndex.ContainsKey(symbol))
            {
                result.AddRange(_knowledgeIndex[symbol]);
            }

            // 2. Genel piyasa yorumlarını da ekle (Sembol belirtilmemiş genel "Borsa" yorumları)
            // Opsiyonel: Sadece spesifik sembol isteniyorsa burayı atlayabiliriz.
            
            // 3. Filtrele (Zaman ve Kopya)
            var cutoff = DateTime.Now.AddHours(-maxAgeHours);
            return result
                .Where(t => t.FetchedAt > cutoff || t.PostDate > cutoff) // Taze veriyi getir
                .DistinctBy(t => t.Url)
                .OrderByDescending(t => t.PostDate)
                .ToList();
        }

        /// <summary>
        /// Genel piyasa görünümü için tweets getirir.
        /// </summary>
        public List<TweetMemoryItem> RecallGeneralMarket(int maxLimit = 50)
        {
            // Genellikle sembol içermeyen ama "Borsa", "Endeks", "XU100" içerenler
            // Veya sadece _rawTimeline'dan en yeni yüksek puanlıları getir.
            return _rawTimeline
                .OrderByDescending(t => t.PostDate)
                .Take(maxLimit)
                .ToList();
        }

        public void SaveKnowledgeBase()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_rawTimeline, options);
                File.WriteAllText(_knowledgeBasePath, json);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"KB Save Error: {ex.Message}", "Memory");
            }
        }

        public List<TweetMemoryItem> GetKnowledgeBase() => _rawTimeline.ToList();

        public void Save() => SaveKnowledgeBase();

        private List<string> ExtractSymbols(string content)
        {
            var symbols = new HashSet<string>();
            
            // Regex for $SYMBOL, #SYMBOL or classical capital words if look like ticker
            // Basitçe: XU100, THYAO, $SASA, #GARAN
            // Kelime bazlı tarama
            var words = content.Split(new[] { ' ', '\n', '.', ',', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var word in words)
            {
                string clean = word.Trim().ToUpperInvariant();
                
                // BIST Ticker Pattern (Kabaca)
                if (clean.StartsWith("$") || clean.StartsWith("#"))
                {
                    clean = clean.Substring(1);
                }

                // En az 3, en fazla 5 harfli, hepsi büyük harf veya rakam (XU100)
                if (clean.Length >= 3 && clean.Length <= 5 && IsAllUpperOrDigit(clean))
                {
                    // Kara liste kontrolü (VAR, AMA, BEN, SEN vb.)
                    if (!IsStopWord(clean))
                    {
                        symbols.Add(clean);
                    }
                }
            }
            return symbols.ToList();
        }

        private bool IsAllUpperOrDigit(string s)
        {
            foreach (char c in s)
            {
                if (!char.IsLetterOrDigit(c)) return false;
                if (char.IsLetter(c) && !char.IsUpper(c)) return false;
            }
            return true;
        }

        private bool IsStopWord(string s)
        {
            var stops = new HashSet<string> { "VAR", "YOK", "AMA", "BEN", "SEN", "BIR", "IKI", "BU", "SU", "O", "ILE", "VE", "VEYA", "EVET", "HAYIR", "OLDU", "BITTI", "SON", "ILK", "KUS", "SES", "YOL", "AL", "SAT", "TUT", "SASA" }; // SASA hariç :)
            return stops.Contains(s) && s != "XU100" && s != "SASA"; // SASA, XU100 istisna
        }

        public bool HasInteracted(string url) => _interactionMemory.Contains(url);
        public void RecordInteraction(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            _interactionMemory.Add(url);
            SaveKnowledgeBase();
        }

        #endregion

        #region Legacy Analysis Memory Methods

        public void StoreAnalysis(string symbol, string strategy, string content, string url = "")
        {
            if (string.IsNullOrEmpty(symbol)) return;

            if (!_analysisMemory.ContainsKey(symbol))
                _analysisMemory[symbol] = new List<AnalysisMemory>();

            var history = _analysisMemory[symbol];
            history.Add(new AnalysisMemory
            {
                Timestamp = DateTime.Now,
                Strategy = strategy,
                Content = content,
                Url = url ?? ""
            });

            if (history.Count > MaxHistoryPerSymbol)
                history.RemoveRange(0, history.Count - MaxHistoryPerSymbol);

            SaveHistory();
        }

        public string GetSymbolContext(string symbol)
        {
            if (string.IsNullOrEmpty(symbol) || !_analysisMemory.ContainsKey(symbol))
                return "";

            var history = _analysisMemory[symbol];
            if (history.Count == 0) return "";

            var contextParts = history.Select(h => 
                $"[{h.Timestamp:dd.MM HH:mm}] ({h.Strategy}): {h.Content}"
            );

            return "GEÇMİŞ ANALİZ ÖZETLERİ (Tutarlılık için referans al):\n" + 
                   string.Join("\n---\n", contextParts);
        }

        /// <summary>
        /// Son 7 gündeki başarılı (hedef tutmuş) analizi getirir
        /// Thread prompt'larında geçmiş başarı hatırlatması için kullanılır
        /// </summary>
        public string? GetLastSuccessfulAnalysis(string symbol, int maxDays = 7)
        {
            if (string.IsNullOrEmpty(symbol) || !_analysisMemory.ContainsKey(symbol))
                return null;

            var cutoffDate = DateTime.Now.AddDays(-maxDays);
            var history = _analysisMemory[symbol]
                .Where(h => h.Timestamp >= cutoffDate)
                .OrderByDescending(h => h.Timestamp)
                .ToList();

            if (history.Count == 0) return null;

            // Başarılı analiz anahtar kelimeleri
            var successKeywords = new[] { "hedef", "Hedef", "tuttu", "beklenen", "başarı", "ulaştı", "gerçekleşti" };

            foreach (var analysis in history)
            {
                if (successKeywords.Any(k => analysis.Content.Contains(k)))
                {
                    // Özet formatında döndür
                    string summary = analysis.Content.Length > 200 
                        ? analysis.Content.Substring(0, 200) + "..." 
                        : analysis.Content;
                    
                    return $"[{analysis.Timestamp:dd.MM}] {summary}";
                }
            }

            return null;
        }

        /// <summary>
        /// Bu hafta içinde aynı sembol için kaç thread atıldığını döner
        /// Haftalık spam kontrolü için kullanılır
        /// </summary>
        public int GetWeeklyThreadCount(string symbol)
        {
            if (string.IsNullOrEmpty(symbol) || !_analysisMemory.ContainsKey(symbol))
                return 0;

            var weekStart = DateTime.Now.AddDays(-7);
            return _analysisMemory[symbol]
                .Count(h => h.Timestamp >= weekStart && h.Strategy.Contains("THREAD", StringComparison.OrdinalIgnoreCase));
        }

        public (DateTime Timestamp, string Strategy, string Content, string Url)? GetLatestThreadContext(string symbol, int maxDays = 7)
        {
            if (string.IsNullOrEmpty(symbol) || !_analysisMemory.ContainsKey(symbol))
                return null;

            var cutoff = DateTime.Now.AddDays(-maxDays);
            var latest = _analysisMemory[symbol]
                .Where(h => h.Timestamp >= cutoff && h.Strategy.Contains("THREAD", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(h => h.Timestamp)
                .FirstOrDefault();

            return latest == null ? null : (latest.Timestamp, latest.Strategy, latest.Content, latest.Url);
        }

        /// <summary>
        /// Thread gönderildiğinde hafızaya kaydet (haftalık kontrol için)
        /// </summary>
        public void RecordThreadPosted(string symbol, string threadContent, string url = "")
        {
            StoreAnalysis(symbol, "THREAD", threadContent, url);
        }

        #endregion

        #region Persistence

        private void Load()
        {
            // 1. Load Legacy History
            try
            {
                if (File.Exists(_historyPath))
                {
                    string json = File.ReadAllText(_historyPath);
                    _analysisMemory = JsonSerializer.Deserialize<Dictionary<string, List<AnalysisMemory>>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"History Load Error: {ex.Message}", "Memory");
            }

            // 2. Load Knowledge Base (New)
            try
            {
                if (File.Exists(_knowledgeBasePath))
                {
                    string json = File.ReadAllText(_knowledgeBasePath);
                    _rawTimeline = JsonSerializer.Deserialize<List<TweetMemoryItem>>(json) ?? new();

                    // Rebuild Index
                    foreach (var item in _rawTimeline)
                    {
                        if (item.RelatedSymbols != null)
                        {
                            foreach (var sym in item.RelatedSymbols)
                            {
                                if (!_knowledgeIndex.ContainsKey(sym))
                                    _knowledgeIndex[sym] = new List<TweetMemoryItem>();
                                _knowledgeIndex[sym].Add(item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"KB Load Error: {ex.Message}", "Memory");
            }
        }

        private void SaveHistory()
        {
            try
            {
                string json = JsonSerializer.Serialize(_analysisMemory, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_historyPath, json);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"History Save Error: {ex.Message}", "Memory");
            }
        }

        #endregion
    }

    public class AnalysisMemory
    {
        public DateTime Timestamp { get; set; }
        public string Strategy { get; set; } = "";
        public string Content { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public class TweetMemoryItem
    {
        public string Id { get; set; } = "";
        public string Author { get; set; } = "";
        public string Content { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime PostDate { get; set; }
        public DateTime FetchedAt { get; set; }
        public int RelevanceScore { get; set; }
        public int Engagement { get; set; }
        public List<string> RelatedSymbols { get; set; } = new List<string>();
    }
}
