using Kingmaker;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using static UnityModManagerNet.UnityModManager;

namespace BubbleBuffs.Config {
    public static class Language {

        private static Dictionary<Locale, Dictionary<string, string>> Languages = new();


        public static string Get(string key, Locale locale) {
            if (!Languages.TryGetValue(locale, out var pack))
                return Get(key, Locale.enGB);

            if (!pack.TryGetValue(key, out var value))
                return Get(key, Locale.enGB);

            return value;
        }

        private static void AddLanguage(Locale locale, string path) {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream($"BubbleBuffs.Config.{path}");
            using StreamReader reader = new StreamReader(stream);
            using JsonReader jsonReader = new JsonTextReader(reader);

            var json = new JsonSerializer();
            Languages[locale] = json.Deserialize<Dictionary<string, string>>(jsonReader);

            Main.Log($"Added language pack for: {locale}");

        }

        public static void Initialise() {
            AddLanguage(Locale.zhCN, "zh_CN.json");
            AddLanguage(Locale.enGB, "en_GB.json");
            AddLanguage(Locale.deDE, "de_DE.json");
            AddLanguage(Locale.ruRU, "ru_RU.json");
            AddLanguage(Locale.frFR, "fr_FR.json");
        }

        //public static Locale Locale => Locale.ruRU;
        public static Locale Locale => LocalizationManager.CurrentLocale;

        public static string i8(this string str) {
            return Get(str, Locale);
        }
        public static string i8(this BuffGroup buffGroup) {
            return buffGroup switch {
                BuffGroup.Long => "group.normal.log".i8(),
                BuffGroup.Short => "group.short.log".i8(),
                BuffGroup.Important => "group.important.log".i8(),
                _ => "<unknown>"
            };

        }

    }

    static class ModSettings {
        public static ModEntry ModEntry;
        public static Fixes Fixes;
        public static AddedContent AddedContent;
        public static Blueprints Blueprints;

        public static void LoadAllSettings() {
            LoadSettings("Fixes.json", ref Fixes);
            LoadSettings("AddedContent.json", ref AddedContent);
            LoadSettings("Blueprints.json", ref Blueprints);
            Language.Initialise();
        }
        private static void LoadSettings<T>(string fileName, ref T setting) where T : IUpdatableSettings {
            var assembly = Assembly.GetExecutingAssembly();
            string userConfigFolder = ModEntry.Path + "UserSettings";
            Directory.CreateDirectory(userConfigFolder);
            var resourcePath = $"BubbleBuffs.Config.{fileName}";
            var userPath = $"{userConfigFolder}{Path.DirectorySeparatorChar}{fileName}";


            using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
            using (StreamReader reader = new StreamReader(stream)) {
                setting = JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
            }
            if (File.Exists(userPath)) {
                using (StreamReader reader = File.OpenText(userPath)) {
                    try {
                        T userSettings = JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
                        setting.OverrideSettings(userSettings);
                    } catch {
                        Main.Error("Failed to load user settings. Settings will be rebuilt.");
                        try { File.Copy(userPath, userConfigFolder + $"{Path.DirectorySeparatorChar}BROKEN_{fileName}", true); } catch { Main.Error("Failed to archive broken settings."); }
                    }
                }
            }
            File.WriteAllText(userPath, JsonConvert.SerializeObject(setting, Formatting.Indented));
        }
    }
}
