using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace XiDeAI_Pro.Services
{
    public class GuruPersistenceService
    {
        private readonly string _filePath;
        private HashSet<string> _processedUrls;

        public GuruPersistenceService()
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XiDeAI");
            Directory.CreateDirectory(appData);
            _filePath = Path.Combine(appData, "processed_guru_tweets.json");
            _processedUrls = LoadUrls();
        }

        private HashSet<string> LoadUrls()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    return JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GuruPersistenceService Load Error: {ex.Message}");
            }
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public void SaveUrls()
        {
            try
            {
                string json = JsonSerializer.Serialize(_processedUrls, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GuruPersistenceService Save Error: {ex.Message}");
            }
        }

        public bool IsProcessed(string url)
        {
            return _processedUrls.Contains(url);
        }

        public void MarkAsProcessed(string url)
        {
            if (_processedUrls.Add(url))
            {
                SaveUrls();
            }
        }
    }
}
