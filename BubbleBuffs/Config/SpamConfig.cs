using Newtonsoft.Json;
using System.IO;

namespace BubbleBuffs.Config {
    public class SpamConfig {
        private static SpamConfig _instance;
        private const string FileName = "SpamConfig.json";

        [JsonProperty]
        public bool UseSmartReapply { get; set; } = true;

        [JsonProperty]
        public float ReapplyThresholdSeconds { get; set; } = 6f;

        [JsonProperty]
        public float CheckIntervalSeconds { get; set; } = 1f;

        [JsonProperty]
        public bool SkipIfUnitHasPendingCommands { get; set; } = true;

        public static SpamConfig Instance {
            get {
                if (_instance == null)
                    Load();
                return _instance;
            }
        }

        private static void Load() {
            string userConfigFolder = ModSettings.ModEntry.Path + "UserSettings";
            Directory.CreateDirectory(userConfigFolder);
            var path = Path.Combine(userConfigFolder, FileName);

            if (File.Exists(path)) {
                try {
                    var json = File.ReadAllText(path);
                    _instance = JsonConvert.DeserializeObject<SpamConfig>(json);
                    Main.Log($"Loaded SpamConfig: UseSmartReapply={_instance.UseSmartReapply}, ReapplyThreshold={_instance.ReapplyThresholdSeconds}s, CheckInterval={_instance.CheckIntervalSeconds}s");
                } catch {
                    Main.Error("Failed to load SpamConfig.json, using defaults.");
                    _instance = new SpamConfig();
                }
            } else {
                _instance = new SpamConfig();
                // Write default config so user can edit it
                Save();
                Main.Log($"Created default SpamConfig.json");
            }
        }

        public static void Reload() {
            _instance = null;
            Load();
        }

        public static void Save() {
            string userConfigFolder = ModSettings.ModEntry.Path + "UserSettings";
            Directory.CreateDirectory(userConfigFolder);
            var path = Path.Combine(userConfigFolder, FileName);
            File.WriteAllText(path, JsonConvert.SerializeObject(_instance, Formatting.Indented));
        }
    }
}
