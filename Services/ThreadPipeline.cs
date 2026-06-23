using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XiDeAI_Pro.Services
{
    internal static class ThreadPipeline
    {
        public static List<string> BuildSignalThread(SignalData signal, string aiThreadContent, string? accountHandle)
        {
            var tweets = new List<string>
            {
                BuildSignalLeadTweet(signal, accountHandle)
            };

            // The AI content should already follow the "Hook -> Storytelling ->
            // Engagement" structure as defined in PromptManager.cs.
            var allParts = ParseParts(aiThreadContent, 280);
            if (LooksLikePromptLeak(aiThreadContent) && allParts.Count > 6)
            {
                return new List<string>();
            }
            var parsedParts = allParts.Take(3).ToList();

            tweets.AddRange(parsedParts);
            MergeShortTailParts(tweets, minimumLength: 80, preserveFirstTweet: true, maxLength: 280);
            if (tweets.Count > 4) tweets = tweets.Take(4).ToList();
            if (tweets.Count > 0 && !tweets[^1].Contains("Yatırım tavsiyesi", StringComparison.OrdinalIgnoreCase))
            {
                const string suffix = "\n\n⚠️ Yatırım tavsiyesi değildir.";
                var baseText = tweets[^1].Trim();
                if (baseText.Length + suffix.Length > 280) baseText = baseText.Substring(0, Math.Max(0, 277 - suffix.Length)).TrimEnd() + "...";
                tweets[^1] = baseText + suffix;
            }
            return EnsureWithinLimit(tweets, 280);
        }

        public static List<string> BuildNewsThread(NewsItem item, string threadContent)
        {
            var tweets = ParseParts(threadContent, 280)
                .Where(x => x.Length > 8)
                .ToList();

            string leadTweet = BuildNewsLeadTweet(item);
            if (tweets.Count == 0)
            {
                tweets.Add(leadTweet);
                return tweets;
            }

            string first = tweets[0];
            bool hasLeadContext = first.Contains("📰") ||
                                  first.Contains(item.Source ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                                  first.Contains((item.Title ?? string.Empty).Split(' ').FirstOrDefault() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (!hasLeadContext)
            {
                tweets.Insert(0, leadTweet);
            }

            return EnsureWithinLimit(tweets, 280);
        }

        public static List<string> ParseParts(string content, int limit)
        {
            if (string.IsNullOrWhiteSpace(content)) return new List<string>();

            content = ExtractPublishableThreadContent(content);
            if (string.IsNullOrWhiteSpace(content)) return new List<string>();

            var segments = content.Contains("|||")
                ? content.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim().TrimEnd('|').Trim())
                : new[] { content.Trim() };

            var parts = new List<string>();
            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment)) continue;

                if (segment.Length > limit)
                {
                    parts.AddRange(ThreadService.SplitText(segment, limit));
                }
                else
                {
                    parts.Add(segment);
                }
            }

            return parts.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        public static List<string> ParseThreadPayload(string content, int limit)
        {
            if (string.IsNullOrWhiteSpace(content)) return new List<string>();

            string trimmed = content.Trim();
            if (trimmed.StartsWith("{") && TryExtractJsonTweets(trimmed, out var jsonTweets))
            {
                return EnsureWithinLimit(jsonTweets, limit);
            }

            trimmed = ExtractPublishableThreadContent(trimmed);
            if (string.IsNullOrWhiteSpace(trimmed)) return new List<string>();

            return ParseParts(trimmed, limit);
        }

        public static List<string> BuildCompactThread(string content, int limit = 280, int maxTweets = 8, int minUsefulLength = 120, string? finalSuffix = null)
        {
            // v5.4.7: 40 karakterin altındaki cok kisa tweet'leri at (bozuk AI ciktileri)
            var parts = ParseParts(content, limit)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Where(x => x.Length >= 40)  // Minimum karakter filtresi
                .ToList();

            if (parts.Count == 0) return parts;

            // Kisa tweet'leri bir sonrakine birlestir (calidad kontrolu)
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Length < 80 && i < parts.Count - 1)
                {
                    string merged = parts[i] + "\n\n" + parts[i + 1];
                    if (merged.Length <= limit)
                    {
                        parts[i + 1] = merged;
                        parts.RemoveAt(i);
                        i--; // Tekrar kontrol et
                        continue;
                    }
                }
            }

            for (int i = parts.Count - 1; i > 0; i--)
            {
                string merged = parts[i - 1].TrimEnd() + "\n\n" + parts[i].TrimStart();
                if ((parts[i].Length < minUsefulLength || parts.Count > maxTweets) && merged.Length <= limit)
                {
                    parts[i - 1] = merged;
                    parts.RemoveAt(i);
                }
            }

            if (parts.Count > maxTweets)
            {
                var kept = parts.Take(maxTweets).ToList();
                string last = kept[^1].TrimEnd();
                if (!last.EndsWith("...")) kept[^1] = last + "...";
                parts = kept;
            }

            if (!string.IsNullOrWhiteSpace(finalSuffix) && parts.Count > 0 && !parts[^1].Contains(finalSuffix, StringComparison.OrdinalIgnoreCase))
            {
                string suffix = finalSuffix.Trim();
                string last = parts[^1].TrimEnd();
                if (last.Length + suffix.Length + 2 > limit) last = last.Substring(0, Math.Max(0, limit - suffix.Length - 5)).TrimEnd() + "...";
                parts[^1] = last + "\n\n" + suffix;
            }

            return EnsureWithinLimit(parts, limit).Take(maxTweets).ToList();
        }

        public static bool TryParseCommand(string content, string commandPrefix, out string[] parts)
        {
            parts = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(commandPrefix)) return false;

            if (!content.StartsWith(commandPrefix, StringComparison.Ordinal)) return false;

            parts = content.Split(new[] { "|||" }, StringSplitOptions.None)
                .Select(x => x.Trim())
                .ToArray();

            return parts.Length > 0;
        }

        private static string BuildSignalLeadTweet(SignalData signal, string? accountHandle)
        {
            string cleanSymbol = CleanSymbolForX(signal.Symbol);
            string currency = GetCurrencyForSymbol(signal.Symbol);
            string periodStr = signal.Period switch
            {
                "G" => "Günlük",
                "H" => "Haftalık",
                "A" => "Aylık",
                "Y" => "Yıllık",
                _ => signal.Period + "dk"
            };

            string headerTag = string.IsNullOrWhiteSpace(accountHandle) ? "🇹🇷" : $"🇹🇷 @{accountHandle} |";
            return $"{headerTag} #{cleanSymbol} ({periodStr}) sinyal notu\n\n" +
                   $"💰 Fiyat: {signal.Price:N2} {currency}\n" +
                   $"📊 Takip notu: {GetPublicSignalState(signal)}{(signal.IsRoket ? " 🚀" : "")}";
        }

        private static string GetPublicSignalState(SignalData signal)
        {
            return signal.Durum?.ToUpperInvariant() switch
            {
                "AKTIF" => "Sinyal canlı, teyit aranıyor",
                "PULLBACK_ADAY" => "Geri çekilme takibi, acele yok",
                "KAPALI" => "Sinyal kapanmış, paylaşım izleme amaçlı",
                _ => "İzleme listesinde"
            };
        }

        private static string BuildNewsLeadTweet(NewsItem item)
        {
            string leadTitle = item.Title?.Trim() ?? "Önemli Haber";
            if (leadTitle.Length > 120) leadTitle = leadTitle.Substring(0, 120).TrimEnd() + "...";
            string prefix = item.IsFlash ? "🚨 SON DAKİKA" : "📰 HABER";
            string linkLine = !string.IsNullOrEmpty(item.Link) ? $"\n🔗 {item.Link}" : "";
            return $"{prefix}\n\n{leadTitle}\n\nKaynak: {item.Source}{linkLine}";
        }

        public static List<string> EnsureWithinLimit(IEnumerable<string> tweets, int limit)
        {
            var normalized = new List<string>();
            foreach (var tweet in tweets)
            {
                if (string.IsNullOrWhiteSpace(tweet)) continue;
                string trimmed = tweet.Trim();
                if (trimmed.Length <= limit)
                {
                    normalized.Add(trimmed);
                    continue;
                }

                normalized.AddRange(ThreadService.SplitText(trimmed, limit));
            }

            var packed = new List<string>();
            string current = "";

            foreach (var tweet in normalized)
            {
                if (string.IsNullOrWhiteSpace(tweet)) continue;
                string t = tweet.Trim();

                if (string.IsNullOrEmpty(current))
                {
                    current = t;
                }
                else
                {
                    string separator = current.EndsWith("\n") || t.StartsWith("\n") ? "\n" : "\n\n";
                    string merged = current.TrimEnd() + separator + t;
                    if (merged.Length <= limit)
                    {
                        current = merged;
                    }
                    else
                    {
                        merged = current.TrimEnd() + " " + t;
                        if (merged.Length <= limit)
                        {
                            current = merged;
                        }
                        else
                        {
                            packed.Add(current);
                            current = t;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(current))
            {
                packed.Add(current);
            }

            return packed;
        }

        private static void MergeShortTailParts(List<string> tweets, int minimumLength, bool preserveFirstTweet, int maxLength)
        {
            int startIndex = preserveFirstTweet ? 2 : 1;
            for (int i = tweets.Count - 1; i >= startIndex; i--)
            {
                string merged = tweets[i - 1].TrimEnd() + " " + tweets[i].Trim();
                if (tweets[i].Trim().Length < minimumLength && merged.Length <= maxLength)
                {
                    tweets[i - 1] = merged;
                    tweets.RemoveAt(i);
                }
            }
        }

        private static string ExtractPublishableThreadContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;

            string normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
            normalized = normalized.Replace("```", string.Empty).Replace("**", string.Empty).Trim();

            bool hasSeparators = normalized.Contains("|||", StringComparison.Ordinal);
            var rawLines = normalized.Split('\n');
            var cleanedLines = new List<string>();
            bool previousBlank = false;

            foreach (var rawLine in rawLines)
            {
                string line = CleanupLine(rawLine);
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (!previousBlank)
                    {
                        cleanedLines.Add(string.Empty);
                        previousBlank = true;
                    }
                    continue;
                }

                if (IsPromptLeakLine(line))
                {
                    continue;
                }

                cleanedLines.Add(line);
                previousBlank = false;
            }

            string cleaned = string.Join("\n", cleanedLines).Trim();
            if (string.IsNullOrWhiteSpace(cleaned)) return string.Empty;

            cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");

            if (!hasSeparators && LooksLikePromptLeak(cleaned))
            {
                var paragraphs = cleaned
                    .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x) && !IsPromptLeakLine(x))
                    .ToList();

                if (paragraphs.Count > 0)
                {
                    cleaned = string.Join("|||", paragraphs);
                }
            }

            return cleaned.Trim();
        }

        private static string CleanupLine(string rawLine)
        {
            if (string.IsNullOrWhiteSpace(rawLine)) return string.Empty;

            string line = rawLine.Trim();
            line = Regex.Replace(line, @"^[\-•*]+\s*", string.Empty).Trim();
            line = Regex.Replace(line, @"^\d+\/\d+\s*", string.Empty).Trim();
            line = Regex.Replace(line, @"^\(?\d+\)?\s*$", string.Empty).Trim();
            line = Regex.Replace(line, @"^\*?Draft\s*\d+\*?\s*:\s*", string.Empty, RegexOptions.IgnoreCase).Trim();
            line = Regex.Replace(line, @"^\*?Revised\s*\d+\*?\s*:\s*", string.Empty, RegexOptions.IgnoreCase).Trim();
            line = Regex.Replace(line, @"^Tweet\s*\d+\s*[:\-]\s*", string.Empty, RegexOptions.IgnoreCase).Trim();
            
            // Yeni beceri: Robotik numara temizliği (Sadece başındaki sayıyı temizle, metni silme)
            line = Regex.Replace(line, @"^\d+\s*[\)\.\-]\s*", string.Empty).Trim();

            return line;
        }

        private static bool LooksLikePromptLeak(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return false;

            string lower = content.ToLowerInvariant();
            string[] markers =
            {
                // İngilizce iç düşünme belirteçleri
                "işte bir düşünme süreci",
                "strict rules",
                "output format",
                "character count",
                "count constraint",
                "thread structure",
                "first tweet (hook)",
                "phenomenon tagging",
                "let's count",
                "need to trim",
                "i'll",
                "i will",
                "data:",
                "count:",
                "total:",
                // Türkçe prompt başlıkları (### ile gelen)
                "### kimlik",
                "### görev",
                "### veriler",
                "### bağlam",
                "### kısıtlar",
                "### format",
                "### çıktı",
                "### yasak",
                "### sinyal",
                // Türkçe sızıntı ifadeleri
                "görev:",
                "çıktı formatı",
                "kısıtlamalar:",
                "format kurallari",
                "format kuralları",
                "mutlak kurallar",
                "kesin yasaklar",
                "tweet 1/4:",
                "tweet 2/4:",
                "tweet 3/4:",
                "tweet 4/4:"
            };

            return markers.Any(lower.Contains);
        }

        private static bool IsPromptLeakLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return true;

            string lower = line.Trim().ToLowerInvariant();
            if (lower.Length <= 2) return true;

            string[] exactStarts =
            {
                // İngilizce iç düşünme
                "işte bir düşünme süreci",
                "strict rules",
                "output format",
                "character count",
                "count constraint",
                "thread structure",
                "timeframe discipline",
                "phenomenon tagging",
                "first tweet (hook)",
                "hook:",
                "data:",
                "count:",
                "total:",
                "need to",
                "let's count",
                "i'll ",
                "i will ",
                // Türkçe prompt başlıkları
                "### kimlik",
                "### görev",
                "### veriler",
                "### bağlam",
                "### kısıtlar",
                "### format",
                "### çıktı",
                "### yasak",
                "### sinyal",
                "### analiz",
                "### ton:",
                "görev:",
                "çıktı formatı",
                "kısıtlamalar:",
                "format kurallari",
                "format kuralları",
                "mutlak kurallar",
                "kesin yasaklar",
                "strict rules:",
                "2. first tweet",
                "3. phenomenon",
                "4. technical indicators",
                "6. thread structure",
                "9. output format",
                "10. strict character"
            };

            if (exactStarts.Any(lower.StartsWith))
            {
                return true;
            }

            if (Regex.IsMatch(lower, @"^(tweet\s*\d+|\[?\d+\.\s*tweet|\d+\.\s*(strict|first|thread|output|character|phenomenon|timeframe))"))
            {
                return true;
            }

            if (Regex.IsMatch(lower, @"^\(?\d+\)?$"))
            {
                return true;
            }

            return false;
        }

        private static bool TryExtractJsonTweets(string content, out List<string> tweets)
        {
            tweets = new List<string>();

            try
            {
                var threadData = JsonSerializer.Deserialize<JsonElement>(content);
                if (!threadData.TryGetProperty("tweets", out var tweetsArray) || tweetsArray.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                tweets = tweetsArray.EnumerateArray()
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList();

                return tweets.Count > 0;
            }
            catch
            {
                tweets = new List<string>();
                return false;
            }
        }

        private static string CleanSymbolForX(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return string.Empty;
            symbol = symbol.Replace("VIP-", string.Empty, StringComparison.OrdinalIgnoreCase);
            if (symbol.StartsWith("F_", StringComparison.OrdinalIgnoreCase)) symbol = symbol.Substring(2);
            return symbol;
        }

        private static string GetCurrencyForSymbol(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return "TL";

            string upper = symbol.ToUpperInvariant();
            if (upper.Contains("USD") || upper.Contains("USDT") || upper.StartsWith("XAU") || upper.StartsWith("XAG") || upper.StartsWith("BTC") || upper.StartsWith("ETH"))
                return "USD";
            if (upper.Contains("EUR"))
                return "EUR";
            return "TL";
        }

        /// <summary>
        /// v5.6.0: X thread formatlama elemanlarını (|||, tweet numaraları, YTD vb.) temizleyerek
        /// yapay zekaya saf metin (bağlam) olarak iletilebilecek hale getirir.
        /// </summary>
        public static string CleanThreadFormatForContext(string rawContent)
        {
            if (string.IsNullOrWhiteSpace(rawContent)) return string.Empty;

            // 1. Tweet ayraçlarını temizle (|||)
            string cleaned = rawContent.Replace("|||", " ");

            // 2. Tweet numaralarını temizle (örn: [Tweet 1], Tweet 1/3, 1/3 vb.)
            cleaned = Regex.Replace(cleaned, @"^Tweet\s*\d+\s*[:\-]?\s*", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            cleaned = Regex.Replace(cleaned, @"^\[Tweet\s*\d+\]\s*", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            cleaned = Regex.Replace(cleaned, @"^\d+/\d+\s*", "", RegexOptions.Multiline);
            cleaned = Regex.Replace(cleaned, @"\b\d+/\d+\b", "");

            // 3. Yatırım tavsiyesi uyarısını temizle
            cleaned = cleaned.Replace("⚠️ Yatırım tavsiyesi değildir.", "");
            cleaned = cleaned.Replace("Yatırım tavsiyesi değildir.", "");
            cleaned = cleaned.Replace("YTD", "");

            // 4. Tekrarlayan boşlukları ve satır sonlarını temizle
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            return cleaned.Trim();
        }
    }
}
