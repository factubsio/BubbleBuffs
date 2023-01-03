using HarmonyLib;
using JetBrains.Annotations;
using Kingmaker;
using Kingmaker.Blueprints.JsonSystem;
using System;
using BubbleBuffs.Config;
using BubbleBuffs.Utilities;
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
using Kingmaker.UI.ServiceWindow;
using BubbleBuffs;
using System.Collections.Generic;
using Kingmaker.UI.AbilityTarget;
using Kingmaker.UI;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers.Rest;
using System.Reflection;
using System.IO;
using Kingmaker.Cheats;
using Newtonsoft.Json;
using Kingmaker.Blueprints.Items.Shields;
using Kingmaker.Designers;
using System.Linq.Expressions;

namespace BubbleBuffs {

    public class BubbleSettings {

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
        }

        private static readonly BubbleSettings instance = new();
        public static BubbleSettings Instance { get { return instance; } }
    }


    //[HarmonyPatch(typeof(UISettingsManager), "Initialize")]
    //static class SettingsInjector {
    //    static bool Initialized = false;

    //    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "harmony patch")]
    //    static void Postfix() {
    //        if (Initialized) return;
    //        Initialized = true;
    //        Main.LogHeader("Injecting settings");

    //        BubbleSettings.Instance.Initialize();

    //        //Game.Instance.UISettingsManager.m_GameSettingsList.Add(
    //        //    BubbleSettings.MakeSettingsGroup("bubble.speed-tweaks", "Bubble speed tweaks",
    //        //        BubbleSettings.Instance.GlobalMapSpeedSlider,
    //        //        BubbleSettings.Instance.InCombatSpeedSlider,
    //        //        BubbleSettings.Instance.OutOfCombatSpeedSlider,
    //        //        BubbleSettings.Instance.TacticalCombatSpeedSlider));
    //    }
    //}

#if DEBUG
    [EnableReloading]
#endif
    static class Main {
        public static string ModPath;
        private static Harmony harmony;
        public static bool Enabled;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "harmony method")]
        static bool Load(UnityModManager.ModEntry modEntry) {
            harmony = new Harmony(modEntry.Info.Id);
#if DEBUG
            modEntry.OnUnload = OnUnload;
#endif
            CheatsCommon.SendAnalyticEvents?.Set(false);
            modEntry.OnUpdate = OnUpdate;
            ModSettings.ModEntry = modEntry;
            ModPath = modEntry.Path;
            Main.Log("LOADING");
            ModSettings.LoadAllSettings();

            AssetLoader.AddBundle("tutorialcanvas");

            if (UnityModManager.gameVersion.Major == 2) {
                UIHelpers.WidgetPaths = new WidgetPaths_2_0();
            } else if (UnityModManager.gameVersion.Major == 1) {

                if (UnityModManager.gameVersion.Minor == 4)
                    UIHelpers.WidgetPaths = new WidgetPaths_1_4();
                else if (UnityModManager.gameVersion.Minor == 3)
                    UIHelpers.WidgetPaths = new WidgetPaths_1_2();
                else if (UnityModManager.gameVersion.Minor == 2)
                    UIHelpers.WidgetPaths = new WidgetPaths_1_2();
                else if (UnityModManager.gameVersion.Minor == 1)
                    UIHelpers.WidgetPaths = new WidgetPaths_1_1();
                else
                    UIHelpers.WidgetPaths = new WidgetPaths_1_0();
            }

            harmony.PatchAll();

            GlobalBubbleBuffer.Install();


            return true;
        }

        static void OnUpdate(UnityModManager.ModEntry modEntry, float delta) {

#if DEBUG
            if (Input.GetKeyDown(KeyCode.I) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) {
                Main.Log("installing ui...");
                GlobalBubbleBuffer.Instance.TryInstallUI();
                AbilityCache.Revalidate();
                Main.Log("Recalcting?");
                GlobalBubbleBuffer.Instance.SpellbookController.state.RecalculateAvailableBuffs(Bubble.Group);
            }
            if (Input.GetKeyDown(KeyCode.R) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) {
                //foreach (var unit in Bubble.Group)
                //    RestController.ApplyRest(unit);
                var shield = Resources.GetBlueprint<BlueprintItemShield>("6a1a429f023e5134fb16a4962cb79257");
                //Game.Instance.Player.Inventory.Add();
                GameHelper.GetPlayerCharacter().Inventory.Add(shield, 1, null);
            }
            if (Input.GetKeyDown(KeyCode.B) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) {
                modEntry.GetType().GetMethod("Reload", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(modEntry, new object[] {});
            }
#endif
        }

        internal static void LogError(JsonSerializationException ex) {
            ModSettings.ModEntry.Logger.LogException(ex);
        }

        static bool OnUnload(UnityModManager.ModEntry modEntry) {
            harmony.UnpatchAll();
            GlobalBubbleBuffer.Uninstall();

            return true;
        }

        internal static void LogPatch(string v, object coupDeGraceAbility) {
            throw new NotImplementedException();
        }

        public static void Safely(Action a) {
            try {
                a();
            } catch (Exception ex) {
                Error(ex);
            }
        } 

        public static void Log(string msg) {
            //if (bubbleLog != null) {
            //    bubbleLog.WriteLine(msg);
            //    bubbleLog.Flush();
            //}

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
        public static void Error(Exception e) {
            Log(e.ToString());
            PFLog.Mods.Error(e.Message);
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

        static HashSet<string> filtersEnabled = new() {
            //"state",
            //"minority",
            //"rejection",
            "interop",
            "apply",
        };

        static bool suppressUnfiltered = false;

        internal static void VerboseNotNull(Expression<Func<object>> getter, string filter = null) {
#if true && DEBUG
            Verbose($"{getter} is not-null: {getter.Compile().Invoke() != null}", filter);
#endif
        }

        internal static void Verbose(string v, string filter = null) {
#if true && DEBUG
            if ((filter == null && !suppressUnfiltered) || filtersEnabled.Contains(filter))
                Main.Log(v);
#endif
        }
    }
}