using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.GameModes;
using Kingmaker.Globalmap.Blueprints;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityModManagerNet;
using UnityEngine.Events;

namespace BubbleTweaks {

    internal static class SpeedTweaks {
        private class SpeedTweakTimeController : MonoBehaviour {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "unity method")]
            private void Awake() {
                UnityEngine.Object.DontDestroyOnLoad(this);
                Main.Log("SpeedTweakController initialized");
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "unity method")]
            private void OnDestroy() {
                Main.Log("SpeedTweakController destroyed");
            }

            private GameModeType lastGameMode = GameModeType.Default;

            [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "unity method")]
            private void LateUpdate() {
                if (!Main.Enabled || Game.Instance.IsPaused || Game.Instance.InvertPauseButtonPressed || Game.Instance.Player == null) {
                    return;
                }
                if (Game.Instance.CurrentMode == GameModeType.Default) {
                    if (Game.Instance.Player.IsInCombat) {
                        Game.Instance.TimeController.PlayerTimeScale = BubbleSettings.Instance.InCombatSpeed.GetValue();
                    } else {
                        Game.Instance.TimeController.PlayerTimeScale = BubbleSettings.Instance.OutOfCombatSpeed.GetValue();
                    }
                } else if (Game.Instance.CurrentMode == GameModeType.TacticalCombat) {
                    Game.Instance.TimeController.PlayerTimeScale = BubbleSettings.Instance.TacticalCombatSpeed.GetValue();

                } else {
                    if (lastGameMode != Game.Instance.CurrentMode) {
                        lastGameMode = Game.Instance.CurrentMode;
                    }
                    Game.Instance.TimeController.PlayerTimeScale = 1f;
                }
            }
        }


        public static float visualSpeedBase;

        private static bool mInit;

        private static void UpdateSpeedOnSceneLoad(Scene scene, LoadSceneMode mode) {
            if (scene.name == "UI_Globalmap_Scene" || scene.name == "UI_Ingame_Scene") {
                UpdateSpeed();
            }

        }

        private static GameObject timeController;

        public static bool Uninstall() {
            SceneManager.sceneLoaded -= UpdateSpeedOnSceneLoad;
            GameObject.Destroy(timeController);

            return true;
        }

        public static bool Install() {
            SceneManager.sceneLoaded += UpdateSpeedOnSceneLoad;

            timeController = new GameObject("SpeedTweakTravelTimeController", typeof(SpeedTweakTimeController));
            return true;
        }

        public static void UpdateSpeed() {
            BlueprintGlobalMap blueprintGlobalMap = Game.Instance?.Player?.GlobalMap?.LastActivated?.Blueprint;
            if (blueprintGlobalMap == null) {
                return;
            }
            if (Main.Enabled) {
                if (!mInit) {
                    visualSpeedBase = blueprintGlobalMap.VisualSpeedBase;
                    mInit = true;
                }
                blueprintGlobalMap.VisualSpeedBase = visualSpeedBase * BubbleSettings.Instance.GlobalMapSpeed.GetValue();
            } else if (mInit) {
                blueprintGlobalMap.VisualSpeedBase = visualSpeedBase;
            }
        }
    }

}
