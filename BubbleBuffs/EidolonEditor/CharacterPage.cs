using BubbleBuffs;
using BubbleBuffs.Utilities;
using dnlib.DotNet;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Designers.EventConditionActionSystem.Evaluators;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.CharacterInfo;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.CharacterInfo.Menu;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.CharacterInfo.Sections.Abilities;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Spellbook.KnownSpells;
using Kingmaker.UI.MVVM._VM.ServiceWindows.CharacterInfo;
using Kingmaker.UnitLogic;
using Kingmaker.Utility;
using Owlcat.Runtime.Core.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleBuffs.EidolonEditor {
    public struct CharInfoPageType_EXT {
        public static CharInfoPageType Eidolon = (CharInfoPageType)200;
    }
    public struct CharInfoComponentType_EXT {
        public static CharInfoComponentType Eidolon = (CharInfoComponentType)200;
    }

    [HarmonyPatch(typeof(CharacterInfoVM))]
    static class CharacterInfoVM_Patches {
        [HarmonyPostfix, HarmonyPatch(MethodType.Constructor)]
        static void _ctor(CharacterInfoVM __instance) {
            Main.LogDebug("modifying constructor");
            __instance.ComponentVMs[CharInfoComponentType_EXT.Eidolon] = new();
        }

        [HarmonyPostfix, HarmonyPatch("CreateVM")]
        static void CreateVM(CharInfoComponentType type, CharacterInfoVM __instance, ref CharInfoComponentVM __result) {
            Main.LogDebug("modifying CreateVM: " + type);
            if (type == CharInfoComponentType_EXT.Eidolon)
                __result = new EidolonEvolutionsComponentVM(__instance.UnitDescriptor);
        }

    };

    [HarmonyPatch(typeof(CharInfoWindowUtility))]
    static class CharInfoWindowUtility_Patches {
        [HarmonyPatch(nameof(CharInfoWindowUtility.GetPageLabel)), HarmonyPostfix]
        static void GetPageLabel(CharInfoPageType page, ref string __result) {
            Main.LogDebug("GetPageLabel: " + page);
            if (page == CharInfoPageType_EXT.Eidolon) {
                Main.LogDebug("Intercepting and returning label title for eidolon page");
                __result = "Eidolon";
            }
        }
    }

    [HarmonyPatch(typeof(CharInfoMenuSelectorView))]
    static class CharInfoMenuSelectorView_Patches {
        [HarmonyPatch("Initialize"), HarmonyPostfix]
        static void Initialize(CharInfoMenuSelectorView __instance) {
            Main.LogDebug("Modifying CharInfoMenuSelectorView.Initialize");
            for (int i = __instance.m_MenuEntities.Count; i < CharInfoWindowUtility.GetPagesList().Count; i++) {
                var prefab = __instance.GetComponentInChildren<CharInfoMenuEntityView>().gameObject;
                var added = GameObject.Instantiate(prefab, __instance.gameObject.transform);
                __instance.m_MenuEntities.Add(added.GetComponent<CharInfoMenuEntityView>());
            }
        }
    }


    class EidolonEvolutionsComponentVM : CharInfoComponentVM {
        public EidolonEvolutionsComponentVM(IReactiveProperty<UnitDescriptor> unit) : base(unit) {
        }
    }

    public static class Utils {
        public static Action<Action> Once() {
            bool hasRun = false;
            return act => {
                if (!hasRun) {
                    hasRun = true;
                    act();
                }
            };
        }

    }

    class EidolonEvolutionsPCView : CharInfoComponentView<EidolonEvolutionsComponentVM> {


        public static IUIProvider UIProvider;
        private static int numReloads = 1;
        public static void ReloadUIProvider() {
            var bytes = File.ReadAllBytes(Main.ModPath + "/EidolonUI.dll");
            var module = ModuleDefMD.Load(bytes);
            Main.Log("got assembly bytes...:" + bytes.Length);
            module.Assembly.Name += (numReloads++);

            using var buf = new MemoryStream();
            module.Write(buf);
            var asm = Assembly.Load(buf.ToArray());

            Main.Log("got comms assembly...:" + asm);
            var type = Array.Find(asm.GetTypes(), x => typeof(IUIProvider).IsAssignableFrom(x));
            object rawHandler = asm.CreateInstance(type.FullName);
            Main.Log("got raw handler: " + rawHandler + " (" + rawHandler.GetType() + ")");
            UIProvider?.Unload();
            UIProvider = (IUIProvider)rawHandler;
            UIProvider?.Load();
            UIProvider.Log = str => Main.Log("ui: " + str);
            Main.Log("Got handler, id: " + UIProvider.ID);
        }

        private void TryBuildUI() {
            Main.LogDebug("Building view");

            GetComponent<CanvasGroup>().blocksRaycasts = true;
            var content = transform.Find("StandardScrollView/Viewport/Content");

            content.Children().ForEach(x => GameObject.Destroy(x.gameObject));

            UIProvider.BuildUI(content);
        }

        private readonly Action<Action> MakeUI = Utils.Once();

        private Transform ServiceWindow {
            get {
                Main.LogDebug($"current mode: {Game.Instance.CurrentMode}");
                if (Game.Instance.UI.GlobalMapCanvas != null) {
                    return Game.Instance.UI.GlobalMapCanvas.transform.Find("ServiceWindowsConfig");
                } else {
                    return Game.Instance.UI.Canvas.transform.Find("ServiceWindowsPCView");
                }
            }
        }

        private GameObject LabelPrefab {
            get {
                Main.LogDebug($"service window: {ServiceWindow != null}");
                return ServiceWindow.Find("Background/Windows/CharacterInfoPCView/CharacterScreen/LevelClassScores/RaceGenderAlighment/Alignment/Alignment").gameObject;
            }
        }
        private GameObject HeadingPrefab {
            get {
                Main.LogDebug($"service window: {ServiceWindow != null}");
                return ServiceWindow.Find("Background/Windows/CharacterInfoPCView/CharacterScreen/NamePortrait/CharName/CharacterName").gameObject;
            }
        }

        private void UpdateCharacter() {

        }

        public override void RefreshView() {

            try {

                base.RefreshView();

                if (UIProvider == null) {
                    ReloadUIProvider();
                }
                TryBuildUI();

                Main.Safely(UpdateCharacter);
            } catch (Exception ex) {
                Main.Error(ex);
            }
        }


    }

    [HarmonyPatch(typeof(CharacterInfoPCView))]
    static class CharacterInfoPCView_Patches {
        [HarmonyPatch("BindViewImplementation"), HarmonyPrefix]
        static void BindViewImplementation(CharacterInfoPCView __instance) {
            Main.Log("HERE HER HERE");
            try {
                if (!__instance.m_ComponentViews.ContainsKey(CharInfoComponentType_EXT.Eidolon)) {
                    Main.LogDebug($"abilities parent: {__instance.m_AbilitiesView.transform.parent.name}");
                    var prefab = __instance.m_AbilitiesView.gameObject;
                    Main.LogDebug("got prefab");

                    var eidolonEditorGameObject = GameObject.Instantiate(prefab, prefab.transform.parent);
                    Main.LogDebug("made eidolon view");
                    var eidolonEditor = eidolonEditorGameObject.AddComponent<EidolonEvolutionsPCView>();
                    Main.LogDebug("added component");

                    eidolonEditorGameObject.GetComponent<CanvasGroup>().alpha = 1.0f;

                    GameObject.DestroyImmediate(eidolonEditorGameObject.GetComponent<CharInfoAbilitiesPCView>());
                    Main.LogDebug("destroy old component");
                    var toRemove = eidolonEditorGameObject.GetComponentsInChildren<CharInfoFeatureGroupPCView>();
                    foreach (var r in toRemove)
                        GameObject.DestroyImmediate(r.gameObject);
                    Main.LogDebug("and subobjects");

                    __instance.m_ComponentViews[CharInfoComponentType_EXT.Eidolon] = eidolonEditor;
                }
            } catch (Exception ex) {
                Main.Error(ex, "injecting eidolon editor");
            }

        }
    }

    [HarmonyPatch]
    static class EidolonEditor {


        internal static void Install() {
            Main.LogDebug("Instaling Eidolon editor module");
            if (!CharInfoWindowUtility.PagesOrderPC[UnitType.Unknown].Contains(CharInfoPageType_EXT.Eidolon)) {
                Main.LogDebug("Attempting enum injection");

                try {
                    CharInfoWindowUtility.PagesOrderPC[UnitType.Pet].Add(CharInfoPageType_EXT.Eidolon);
                    CharInfoWindowUtility.PagesOrderPC[UnitType.Unknown].Add(CharInfoPageType_EXT.Eidolon);

                    CharInfoWindowUtility.PagesContent[CharInfoPageType_EXT.Eidolon] = new CharInfoPage {
                        ComponentsForAll = new() {
                            CharInfoComponentType.NameAndPortrait,
                            CharInfoComponentType.LevelClassScores,
                            CharInfoComponentType.AttackMain,
                            CharInfoComponentType.DefenceMain,
                            CharInfoComponentType_EXT.Eidolon,
                        }
                    };

                } catch (Exception e) {
                    Main.Error(e);
                }
            } else {
                Game.ResetUI();
            }
        }


        internal static void Uninstall() {
        }
    }



}
