using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace XiDeAI_Pro.Services
{
    public class Influencer
    {
        [JsonIgnore]
        public string Category { get; set; } = ""; // v3.5.2: Track category for UI (Not saved to JSON to keep file clean)
        public string Handle { get; set; } = "";
        public int Score { get; set; } = 0;
        public DateTime AddedDate { get; set; } = DateTime.Now;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    public class InfluencerControlService
    {
        private readonly string _dbPath;
        private Dictionary<string, List<Influencer>> _database;
        private Dictionary<string, List<Influencer>> _metaTeacherDb; // Phase 2: Hierarchical Data
        private readonly object _lock = new object();

        // v3.5.2: Event for UI Auto-Refresh
        public event Action? OnDatabaseChanged;

        public InfluencerControlService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _dbPath = Path.Combine(appData, "XiDeAI", "InfluencerData.json");
            _database = new Dictionary<string, List<Influencer>>();
            _metaTeacherDb = new Dictionary<string, List<Influencer>>();
            
            LoadDatabase();
        }

        public void LoadDatabase()
        {
            lock (_lock)
            {
                _database = new Dictionary<string, List<Influencer>>();
                _metaTeacherDb = new Dictionary<string, List<Influencer>>();

                if (File.Exists(_dbPath))
                {
                    try
                    {
                        string json = File.ReadAllText(_dbPath);
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            foreach (var element in doc.RootElement.EnumerateObject())
                            {
                                if (element.Name == "META_TEACHER")
                                {
                                    // Handle Nested Dictionary
                                    if (element.Value.ValueKind == JsonValueKind.Object)
                                    {
                                        foreach(var catProp in element.Value.EnumerateObject())
                                        {
                                            try {
                                                var list = JsonSerializer.Deserialize<List<Influencer>>(catProp.Value.GetRawText());
                                                if(list != null) {
                                                    foreach(var i in list) i.Category = catProp.Name;
                                                    _metaTeacherDb[catProp.Name] = list;
                                                }
                                            } catch { /* Ignored */ }
                                        }
                                    }
                                }
                                else
                                {
                                    // Handle Legacy Flat List (BIST, CRYPTO, FOREX)
                                    try {
                                        var list = JsonSerializer.Deserialize<List<Influencer>>(element.Value.GetRawText());
                                        if(list != null) {
                                            foreach(var i in list) i.Category = element.Name;
                                            _database[element.Name] = list;
                                        }
                                    } catch { /* Ignored */ }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"InfluencerDB Load Error: {ex.Message}");
                    }
                }

                // Ensure defaults exist
                if (!_database.ContainsKey("BIST")) _database["BIST"] = new List<Influencer>();
                if (!_database.ContainsKey("CRYPTO")) _database["CRYPTO"] = new List<Influencer>();
                if (!_database.ContainsKey("FOREX")) _database["FOREX"] = new List<Influencer>();
            }
        }

        public void SaveDatabase()
        {
            lock (_lock)
            {
                try
                {
                    // Construct a composite object for serialization
                    var exportDict = new Dictionary<string, object>();
                    foreach(var kvp in _database) exportDict[kvp.Key] = kvp.Value;
                    
                    if (_metaTeacherDb.Count > 0)
                    {
                        exportDict["META_TEACHER"] = _metaTeacherDb;
                    }

                    string json = JsonSerializer.Serialize(exportDict, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_dbPath, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"InfluencerDB Save Error: {ex.Message}");
                }
            }
        }

        public void InitializeDefaultSeeds(bool silent = false)
        {
            // User requested robust seed list
            AddInfluencer("FOREX", "@boga_avcisi", 100, silent: true);
            AddInfluencer("FOREX", "@TA_Purvesh", 100, silent: true);
            AddInfluencer("FOREX", "@YShirley_XAUUSD", 100, silent: true);
            AddInfluencer("BIST", "@DAYIBORSA", 100, silent: true);
            AddInfluencer("BIST", "@koc_baba", 90, silent: true);
            AddInfluencer("BIST", "@borsa_adami", 85, silent: true);
            AddInfluencer("CRYPTO", "@kripto_fenomen", 90, silent: true); 
            AddInfluencer("BIST", "@EFELERiiNEFESi3", 100, silent: true); 
            AddInfluencer("FOREX", "@moneyhoca", 95, silent: true);

            if (!silent)
            {
                SaveDatabase();
                OnDatabaseChanged?.Invoke();
            }
        }

        // --- Standard Methods (Flat DB) ---
        public bool DeleteInfluencer(string category, string handle)
        {
            category = category.ToUpper();
            handle = handle.Trim();
            if (!handle.StartsWith("@")) handle = "@" + handle;

            bool removed = false;
            lock (_lock)
            {
                // Check Legacy DB
                if (_database.ContainsKey(category))
                {
                    var list = _database[category];
                    var item = list.FirstOrDefault(i => i.Handle.Equals(handle, StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                    {
                        list.Remove(item);
                        removed = true;
                    }
                }
                
                // Check Meta DB if not removed
                if (!removed && _metaTeacherDb.ContainsKey(category))
                {
                    var list = _metaTeacherDb[category];
                    var item = list.FirstOrDefault(i => i.Handle.Equals(handle, StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                    {
                        list.Remove(item);
                        removed = true;
                    }
                }
            }

            if (removed)
            {
                SaveDatabase();
                OnDatabaseChanged?.Invoke();
            }
            return removed;
        }

        public bool AddInfluencer(string category, string handle, int initialScore = 50, bool silent = false)
        {
            category = category.ToUpper();
            handle = handle.Trim();
            if (!handle.StartsWith("@")) handle = "@" + handle;

            bool added = false;
            lock (_lock)
            {
                // GLOBAL UNIQUENESS CHECK: Ensure handle doesn't exist in ANY category
                var allGurus = new List<Influencer>();
                foreach(var kvp in _database) allGurus.AddRange(kvp.Value);
                foreach(var kvp in _metaTeacherDb) allGurus.AddRange(kvp.Value);

                if (allGurus.Any(i => i.Handle.Equals(handle, StringComparison.OrdinalIgnoreCase)))
                {
                    return false; // Already exists globally
                }

                // Determine target DB
                var legacyCategories = new[] { "BIST", "CRYPTO", "FOREX" };
                Dictionary<string, List<Influencer>> targetDb = legacyCategories.Contains(category) ? _database : _metaTeacherDb;

                if (!targetDb.ContainsKey(category)) targetDb[category] = new List<Influencer>();

                var list = targetDb[category];
                list.Add(new Influencer
                {
                    Category = category,
                    Handle = handle,
                    Score = initialScore,
                    AddedDate = DateTime.Now,
                    LastUpdated = DateTime.Now
                });
                added = true;
            }

            if (added && !silent)
            {
                SaveDatabase();
                OnDatabaseChanged?.Invoke();
            }
            return added;
        }

        public List<Influencer> GetInfluencers(string category)
        {
            category = category.ToUpper();
            lock (_lock)
            {
                if (_database.ContainsKey(category)) return _database[category].ToList();
                if (_metaTeacherDb.ContainsKey(category)) return _metaTeacherDb[category].ToList();
            }
            return new List<Influencer>();
        }
        
        public List<Influencer> GetAllInfluencers()
        {
            var all = new List<Influencer>();
            lock (_lock)
            {
                foreach(var kvp in _database) all.AddRange(kvp.Value);
                foreach(var kvp in _metaTeacherDb) all.AddRange(kvp.Value);
            }
            return all.OrderByDescending(x => x.Score).ToList();
        }

        public List<string> GetCategories()
        {
            lock (_lock)
            {
                var cats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var k in _database.Keys) cats.Add(k);
                foreach(var k in _metaTeacherDb.Keys) cats.Add(k);
                return cats.OrderBy(x => x).ToList();
            }
        }

        public List<string> GetTopInfluencers(string symbol, int count = 5)
        {
            string category = "BIST"; 
            if (symbol.EndsWith("USDT") || symbol == "BTC" || symbol == "ETH") category = "CRYPTO";
            else if (symbol == "XAUUSD" || symbol == "EURUSD" || symbol.Contains("USD")) category = "FOREX";

            var list = GetInfluencers(category); // Uses unified search across all DBs
            if (list.Count > 0)
            {
                return list
                        .OrderByDescending(i => i.Score)
                        .Take(count)
                        .Select(i => i.Handle)
                        .ToList();
            }
            return new List<string>();
        }

        public void ResetDatabase()
        {
            lock (_lock)
            {
                _database.Clear();
                _metaTeacherDb.Clear();
                _database["BIST"] = new List<Influencer>();
                _database["CRYPTO"] = new List<Influencer>();
                _database["FOREX"] = new List<Influencer>();
                InitializeDefaultSeeds(silent: true);
                SaveDatabase();
            }
            OnDatabaseChanged?.Invoke();
        }

        // --- Phase 2: Meta-Teacher Access ---
        public List<Influencer> GetMetaTeacherInfluencers(string? category = null)
        {
            lock(_lock)
            {
                if (string.IsNullOrEmpty(category) || category.ToUpper() == "ALL")
                {
                    // Return ALL Meta-Teacher influencers
                   var all = new List<Influencer>();
                   foreach(var kvp in _metaTeacherDb) all.AddRange(kvp.Value);
                   return all;
                }
                
                string catKey = category.ToUpper();
                if (_metaTeacherDb.ContainsKey(catKey))
                {
                    return _metaTeacherDb[catKey];
                }
                return new List<Influencer>();
            }
        }
    }
}
