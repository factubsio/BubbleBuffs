using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace BubbleBuffs.Config {
    public class AbilityWhitelist {
        private static AbilityWhitelist _instance;
        private static HashSet<string> _whitelistedGuids;
        private static readonly string FileName = "AbilityWhitelist.json";

        public List<WhitelistEntry> Entries { get; set; } = new();

        public class WhitelistEntry {
            public string Guid { get; set; }
            public string Comment { get; set; }
        }

        public static HashSet<string> WhitelistedGuids {
            get {
                if (_whitelistedGuids == null) {
                    Load();
                }
                return _whitelistedGuids;
            }
        }

        private static void Load() {
            _whitelistedGuids = new HashSet<string>();
            var path = Path.Combine(ModSettings.ModEntry.Path, FileName);

            if (File.Exists(path)) {
                try {
                    var json = File.ReadAllText(path);
                    _instance = JsonConvert.DeserializeObject<AbilityWhitelist>(json);
                    foreach (var entry in _instance.Entries) {
                        _whitelistedGuids.Add(entry.Guid);
                    }
                    Main.Log($"Loaded {_instance.Entries.Count} whitelisted abilities from {FileName}");
                } catch {
                    Main.Error($"Failed to load {FileName}");
                }
            } else {
                Main.Log($"{FileName} not found, no abilities whitelisted");
            }
        }
    }
}
