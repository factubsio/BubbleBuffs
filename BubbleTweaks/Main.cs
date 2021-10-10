using HarmonyLib;
using JetBrains.Annotations;
using Kingmaker;
using Kingmaker.Blueprints.JsonSystem;
using System;
using BubbleTweaks.Config;
using BubbleTweaks.Utilities;
using UnityModManagerNet;
using UnityEngine;
using Kingmaker.Localization;
using Kingmaker.UI.SettingsUI;
using Kingmaker.Settings;
using Kingmaker.PubSubSystem;
using Kingmaker.Globalmap;
using UnityEngine.UI;
using Owlcat.Runtime.UI.Controls.Button;
using Kingmaker.UI.Common;
using Kingmaker.Globalmap.State;

namespace BubbleTweaks {

    public class BubbleSettings {
        public SettingsEntityFloat TacticalCombatSpeed = new("bubbles.settings.game.tactical.time-scale", 1.0f);
        public SettingsEntityFloat InCombatSpeed = new("bubbles.settings.game.in-combat.time-scale", 1.0f);
        public SettingsEntityFloat OutOfCombatSpeed = new("bubbles.settings.game.out-of-combat.time-scale", 1.0f);
        public SettingsEntityFloat GlobalMapSpeed = new("bubbles.settings.game.global-map.time-scale", 1.0f);

        public UISettingsEntitySliderFloat TacticalCombatSpeedSlider;
        public UISettingsEntitySliderFloat InCombatSpeedSlider;
        public UISettingsEntitySliderFloat OutOfCombatSpeedSlider;
        public UISettingsEntitySliderFloat GlobalMapSpeedSlider;

        private BubbleSettings() { }

        public static UISettingsEntitySliderFloat MakeSliderFloat(string key, string name, string tooltip, float min, float max, float step) {
            var slider = ScriptableObject.CreateInstance<UISettingsEntitySliderFloat>();
            slider.m_Description = Helpers.CreateString($"{key}.description", name);
            slider.m_TooltipDescription = Helpers.CreateString($"{key}.tooltip-description", tooltip);
            slider.m_MinValue = min;
            slider.m_MaxValue = max;
            slider.m_Step = step;
            slider.m_ShowValueText = true;
            slider.m_DecimalPlaces = 1;
            slider.m_ShowVisualConnection = true;

            return slider;
        }

        public static UISettingsGroup MakeSettingsGroup(string key, string name, params UISettingsEntityBase[] settings) {
            UISettingsGroup group = ScriptableObject.CreateInstance<UISettingsGroup>();
            group.Title = Helpers.CreateString(key, name);

            group.SettingsList = settings;

            return group;
        }

        public void Initialize() {
            TacticalCombatSpeedSlider = MakeSliderFloat("settings.game.tactical.time-scale", "Increase tactical combat animation speed", "Speeds up the animation speed of the all characters in tactical battle mode.", 1, 10, 0.1f);
            TacticalCombatSpeedSlider.LinkSetting(TacticalCombatSpeed);

            InCombatSpeedSlider = MakeSliderFloat("settings.game.in-combat.time-scale", "Increase animation speed when in combat", "Speeds up the animation speed of the all characters when engaged in combat.", 1, 10, 0.1f);
            InCombatSpeedSlider.LinkSetting(InCombatSpeed);

            OutOfCombatSpeedSlider = MakeSliderFloat("settings.game.out-of-combat.time-scale", "Increase animation speed when not in combat", "Speeds up the animation speed of the all characters when not engaged in combat.", 1, 10, 0.1f);
            OutOfCombatSpeedSlider.LinkSetting(OutOfCombatSpeed);

            GlobalMapSpeedSlider = MakeSliderFloat("settings.game.global-map.time-scale", "Increase animation speed when on the global map", "Speeds up the animation speed of all tokens on the global map.", 1, 10, 0.1f);
            GlobalMapSpeedSlider.LinkSetting(GlobalMapSpeed);

        }

        private static readonly BubbleSettings instance = new();
        public static BubbleSettings Instance { get { return instance; } }
    }




    [HarmonyPatch(typeof(UISettingsManager), "Initialize")]
    static class SettingsInjector {
        static bool Initialized = false;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "harmony patch")]
        static void Postfix() {
            if (Initialized) return;
            Initialized = true;
            Main.LogHeader("Injecting settings");

            BubbleSettings.Instance.Initialize();

            Game.Instance.UISettingsManager.m_GameSettingsList.Add(
                BubbleSettings.MakeSettingsGroup("bubble.speed-tweaks", "Bubble speed tweaks",
                    BubbleSettings.Instance.GlobalMapSpeedSlider,
                    BubbleSettings.Instance.InCombatSpeedSlider,
                    BubbleSettings.Instance.OutOfCombatSpeedSlider,
                    BubbleSettings.Instance.TacticalCombatSpeedSlider));
        }
    }

#if DEBUG
    [EnableReloading]
#endif
    static class Main {
        private static Harmony harmony;
        public static bool Enabled;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "harmony method")]
        static bool Load(UnityModManager.ModEntry modEntry) {
            harmony = new Harmony(modEntry.Info.Id);
#if DEBUG
            modEntry.OnUnload = OnUnload;
#endif
            modEntry.OnUpdate = OnUpdate;
            ModSettings.ModEntry = modEntry;
            ModSettings.LoadAllSettings();
            harmony.PatchAll();
            PostPatchInitializer.Initialize();
            Enabled = true;
            SpeedTweaks.Install();
            Crusade.Install();


            return true;
        }

        static void OnUpdate(UnityModManager.ModEntry modEntry, float delta) {

            if (Input.GetKeyDown(KeyCode.B) && Input.GetKey(KeyCode.LeftControl)) {

            }
        }

        static bool OnUnload(UnityModManager.ModEntry modEntry) {
            harmony.UnpatchAll();
            SpeedTweaks.Uninstall();
            Crusade.Uninstall();

            return true;

        }

        internal static void LogPatch(string v, object coupDeGraceAbility) {
            throw new NotImplementedException();
        }

        public static void Log(string msg) {
            ModSettings.ModEntry.Logger.Log(msg);
        }
        [System.Diagnostics.Conditional("DEBUG")]
        public static void LogDebug(string msg) {
            ModSettings.ModEntry.Logger.Log(msg);
        }
        public static void LogPatch(string action, [NotNull] IScriptableObjectWithAssetId bp) {
            Log($"{action}: {bp.AssetGuid} - {bp.name}");
        }
        public static void LogHeader(string msg) {
            Log($"--{msg.ToUpper()}--");
        }
        public static void Error(Exception e, string message) {
            Log(message);
            Log(e.ToString());
            PFLog.Mods.Error(message);
        }
        public static void Error(string message) {
            Log(message);
            PFLog.Mods.Error(message);
        }
    }
}