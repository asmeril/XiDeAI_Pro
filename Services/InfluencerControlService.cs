using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace XiDeAI_Pro.Services
{
    public class Influencer
    {
        public string Handle { get; set; } = "";
        public int Score { get; set; } = 0;
        public DateTime AddedDate { get; set; } = DateTime.Now;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    public class InfluencerControlService
    {
        private readonly string _dbPath;
        private Dictionary<string, List<Influencer>> _database;
        private readonly object _lock = new object();

        public InfluencerControlService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _dbPath = Path.Combine(appData, "XiDeAI", "InfluencerData.json");
            _database = new Dictionary<string, List<Influencer>>();
            
            LoadDatabase();
        }

        public void LoadDatabase()
        {
            lock (_lock)
            {
                if (File.Exists(_dbPath))
                {
                    try
                    {
                        string json = File.ReadAllText(_dbPath);
                        _database = JsonSerializer.Deserialize<Dictionary<string, List<Influencer>>>(json) 
                                    ?? new Dictionary<string, List<Influencer>>();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"InfluencerDB Load Error: {ex.Message}");
                        _database = new Dictionary<string, List<Influencer>>();
                    }
                }
                else
                {
                    // Initialize empty structure
                    _database["BIST"] = new List<Influencer>();
                    _database["CRYPTO"] = new List<Influencer>();
                    _database["FOREX"] = new List<Influencer>();
                }
            }
        }

        public void SaveDatabase()
        {
            lock (_lock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(_database, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_dbPath, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"InfluencerDB Save Error: {ex.Message}");
                }
            }
        }

        public void InitializeDefaultSeeds()
        {
            // User requested robust seed list
            AddInfluencer("FOREX", "@boga_avcisi", 100);
            AddInfluencer("FOREX", "@TA_Purvesh", 100);
            AddInfluencer("FOREX", "@YShirley_XAUUSD", 100);
            AddInfluencer("BIST", "@DAYIBORSA", 100);
            AddInfluencer("BIST", "@koc_baba", 90);
            AddInfluencer("BIST", "@borsa_adami", 85);
            AddInfluencer("CRYPTO", "@kripto_fenomen", 90); // Placeholder, will be filled by discovery
            AddInfluencer("BIST", "@EFELERiiNEFESi3", 100); // Guru
            AddInfluencer("FOREX", "@moneyhoca", 95);

            SaveDatabase();
        }

        public bool DeleteInfluencer(string category, string handle)
        {
            category = category.ToUpper();
            if (!_database.ContainsKey(category)) return false;

            handle = handle.Trim();
            if (!handle.StartsWith("@")) handle = "@" + handle;

            var list = _database[category];
            var item = list.FirstOrDefault(i => i.Handle.Equals(handle, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                list.Remove(item);
                SaveDatabase();
                return true;
            }
            return false;
        }

        public bool AddInfluencer(string category, string handle, int initialScore = 50)
        {
            category = category.ToUpper();
            if (!_database.ContainsKey(category)) _database[category] = new List<Influencer>();

            handle = handle.Trim();
            if (!handle.StartsWith("@")) handle = "@" + handle;

            var list = _database[category];
            if (list.Any(i => i.Handle.Equals(handle, StringComparison.OrdinalIgnoreCase)))
            {
                return false; // Already exists
            }

            list.Add(new Influencer
            {
                Handle = handle,
                Score = initialScore,
                AddedDate = DateTime.Now,
                LastUpdated = DateTime.Now
            });
            return true;
        }

        public List<Influencer> GetInfluencers(string category)
        {
            category = category.ToUpper();
            return _database.ContainsKey(category) ? _database[category] : new List<Influencer>();
        }
        
        public List<Influencer> GetAllInfluencers()
        {
            var all = new List<Influencer>();
            foreach(var kvp in _database) all.AddRange(kvp.Value);
            return all;
        }

        public List<string> GetTopInfluencers(string symbol, int count = 5)
        {
            // Determine category from symbol
            string category = "BIST"; // Default
            if (symbol.EndsWith("USDT") || symbol == "BTC" || symbol == "ETH") category = "CRYPTO";
            else if (symbol == "XAUUSD" || symbol == "EURUSD" || symbol.Contains("USD")) category = "FOREX";

            if (_database.ContainsKey(category))
            {
                return _database[category]
                        .OrderByDescending(i => i.Score)
                        .Take(count)
                        .Select(i => i.Handle)
                        .ToList();
            }
            return new List<string>();
        }

        public void ResetDatabase()
        {
            _database.Clear();
            _database["BIST"] = new List<Influencer>();
            _database["CRYPTO"] = new List<Influencer>();
            _database["FOREX"] = new List<Influencer>();
            InitializeDefaultSeeds();
            SaveDatabase();
        }
    }
}

