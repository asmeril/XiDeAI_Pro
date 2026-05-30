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
            var parsedParts = ParseParts(aiThreadContent, 275);
            if (LooksLikePromptLeak(aiThreadContent) && parsedParts.Count > 6)
            {
                return new List<string>();
            }

            tweets.AddRange(parsedParts);
            MergeShortTailParts(tweets, minimumLength: 80, preserveFirstTweet: true);
            return tweets.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        public static List<string> BuildNewsThread(NewsItem item, string threadContent)
        {
            var tweets = ParseParts(threadContent, 275)
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

            return tweets;
        }

        public static List<string> ParseParts(string content, int limit)
        {
            if (string.IsNullOrWhiteSpace(content)) return new List<string>();

            content = ExtractPublishableThreadContent(content);
            if (string.IsNullOrWhiteSpace(content)) return new List<string>();

            var segments = content.Contains("|||")
                ? content.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
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
                return jsonTweets;
            }

            trimmed = ExtractPublishableThreadContent(trimmed);
            if (string.IsNullOrWhiteSpace(trimmed)) return new List<string>();

            if (!trimmed.Contains("|||", StringComparison.Ordinal))
            {
                return new List<string> { trimmed };
            }

            return ParseParts(trimmed, limit);
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
            return $"{headerTag} #{cleanSymbol} ({periodStr}) Teknik Analizim\n\n" +
                   $"💰 Fiyat: {signal.Price:N2} {currency}\n" +
                   $"📊 Durum: {signal.Durum}{(signal.IsRoket ? " 🚀" : "")}";
        }

        private static string BuildNewsLeadTweet(NewsItem item)
        {
            string leadTitle = item.Title?.Trim() ?? "Önemli Haber";
            if (leadTitle.Length > 140) leadTitle = leadTitle.Substring(0, 140).TrimEnd() + "...";
            return $"📰 {leadTitle}\n\nKaynak: {item.Source}";
        }

        private static void MergeShortTailParts(List<string> tweets, int minimumLength, bool preserveFirstTweet)
        {
            int startIndex = preserveFirstTweet ? 2 : 1;
            for (int i = tweets.Count - 1; i >= startIndex; i--)
            {
                if (tweets[i].Trim().Length < minimumLength)
                {
                    tweets[i - 1] = tweets[i - 1].TrimEnd() + " " + tweets[i].Trim();
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
            return line;
        }

        private static bool LooksLikePromptLeak(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return false;

            string lower = content.ToLowerInvariant();
            string[] markers =
            {
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
                "görev:",
                "çıktı formatı",
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
                "görev:",
                "çıktı formatı",
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
    }
}