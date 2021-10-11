using BubbleTweaks.Utilities;
using Kingmaker;
using Kingmaker.GameModes;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.Blueprints;
using Kingmaker.Globalmap.State;
using Kingmaker.Globalmap.View;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Settlements;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.Common;
using Owlcat.Runtime.UI.Controls.Button;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BubbleTweaks {

    public class ArmySelectionHandler : IGlobalMapArmySelectedHandler {
        public GlobalMapArmyState Army;
        public void OnGlobalMapArmySelected(GlobalMapArmyState state) {
            Crusade.TryInitArmyUITweaks();
            Army = state;
        }
    }

    public class GameChangeModeHandler : IGameModeHandler {
        public void OnGameModeStart(GameModeType gameMode) {
            if (gameMode == GameModeType.GlobalMap) {
                Crusade.TryInitJumpToSiege();
            }
        }

        public void OnGameModeStop(GameModeType gameMode) {
        }
    }

    public class SiegeModeChanged : ISettlementSiegeHandler {
        public void OnSiegeFinished(SettlementState settlement, bool wasDestroyed) {
            if (Crusade.JumpToSiegeButton != null)
                Crusade.JumpToSiegeButton.Interactable = Crusade.AnySettlementsUnderSiege;
        }

        public void OnSiegeStarted(SettlementState settlement) {
            if (Crusade.JumpToSiegeButton != null)
                Crusade.JumpToSiegeButton.Interactable = Crusade.AnySettlementsUnderSiege;
        }
    }

    public class Crusade {
        public static void Install() {
            Main.LogHeader("Installing Crusade Tweaks");
            EventBus.Subscribe(armySelectionHandler);
            EventBus.Subscribe(gameModeHandler);
        }

        public static void Uninstall() {
            EventBus.Unsubscribe(armySelectionHandler);
            EventBus.Unsubscribe(gameModeHandler);
        }

        private static readonly ArmySelectionHandler armySelectionHandler = new();
        private static readonly GameChangeModeHandler gameModeHandler = new();

        private static OwlcatButton disbandButton;

        public static bool AnySettlementsUnderSiege => KingdomState.Instance.SettlementsManager.Settlements.Any(s => s.UnderSiege);
        public static SettlementState FirstSettlementUnderSiege => KingdomState.Instance.SettlementsManager.Settlements.First(s => s.UnderSiege);
        public static OwlcatButton JumpToSiegeButton;

        public static void TryInitJumpToSiege() {
            if (GlobalMapUI.Instance != null) {
                var button = GlobalMapUI.Instance.transform.Find("GlobalMapToolbarView/SkipDayButton");

                for (int i = 0; i < button.parent.childCount; i++) {
                    if (button.parent.GetChild(i).name.StartsWith("BUBBLE")) {
                        GameObject.Destroy(button.parent.GetChild(i).gameObject);
                    }
                }

                var buttonNew = GameObject.Instantiate(button.gameObject, button.parent);
                buttonNew.name = "BUBBLE_JUMP_TO_SIEGE";

                buttonNew.GetComponentInChildren<TextMeshProUGUI>().text = "Jump to Siege";

                buttonNew.transform.localPosition -= new Vector3(0, 50, 0);
                JumpToSiegeButton = buttonNew.GetComponentInChildren<OwlcatButton>();
                JumpToSiegeButton.Interactable = AnySettlementsUnderSiege;

                JumpToSiegeButton.m_OnSingleLeftClick = new Button.ButtonClickedEvent();
                JumpToSiegeButton.m_OnSingleLeftClick.AddListener(() => {
                    if (!AnySettlementsUnderSiege) {
                        Main.Log("No settlements under siege?");
                        return;
                    }
                    Game.Instance.UI.GetCameraRig().ScrollTo(FirstSettlementUnderSiege.MarkerManager.m_Marker.transform.position);
                });
            }
        }

        public static void TryInitArmyUITweaks() {
            if (GlobalMapUI.Instance != null && GlobalMapUI.Instance.transform.Find("ArmyHUDPCView/Background/BUBBLEBUT0") == null) {
                Main.Log("Initializing Crusade UI tweaks");
                var button = GlobalMapUI.Instance.transform.Find("ArmyHUDPCView/Background/InfoBlock");

                for (int i = 0; i < button.parent.childCount; i++) {
                    if (button.parent.GetChild(i).name.StartsWith("BUBBLE")) {
                        GameObject.Destroy(button.parent.GetChild(i).gameObject);
                    }
                }

                var buttonNew = GameObject.Instantiate(button.gameObject, button.parent);
                var innerButton = buttonNew.transform.Find("SettingsButton");

                disbandButton = innerButton.GetComponent<OwlcatButton>();
                var layer = disbandButton.m_CommonLayer[0];
                var tex = innerButton.GetComponent<Image>();
                var baseSprite = AssetLoader.LoadInternal("icons", "disband_army.png", new Vector2Int(128, 128));
                var pressedSprite = AssetLoader.LoadInternal("icons", "disband_army_click.png", new Vector2Int(128, 128));
                var highlightedSprite = AssetLoader.LoadInternal("icons", "disband_army_hover.png", new Vector2Int(128, 128));
                tex.sprite = baseSprite;
                layer.m_SpriteState.highlightedSprite = highlightedSprite;
                layer.m_SpriteState.pressedSprite = pressedSprite;
                layer.m_SpriteState.selectedSprite = baseSprite;

                disbandButton.m_OnSingleLeftClick = new Button.ButtonClickedEvent();
                disbandButton.m_OnSingleLeftClick.AddListener(() => {
                    if (armySelectionHandler.Army == null)
                        return;
                    if (armySelectionHandler.Army.Data.m_LeaderGuid != null && armySelectionHandler.Army.Data.m_LeaderGuid.Length > 0) {
                        Main.Log("Trying to disband an army with a general");
                        UIUtility.ShowMessageBox("You cannot disband an army that has a General", Kingmaker.UI.MessageModalBase.ModalType.Message, (buttonType) => {
                        });
                        return;
                    }
                    UIUtility.ShowMessageBox("Are you sure you want to disband this army?", Kingmaker.UI.MessageModalBase.ModalType.Dialog, (buttonType) => {
                        if (buttonType == Kingmaker.UI.MessageModalBase.ButtonType.Yes) {
                            Main.Log($"Disposing of army: {armySelectionHandler.Army.Data.ArmyName.ArmyName}");
                            armySelectionHandler.Army.Data.RemoveAllSquads();
                            Game.Instance.Player.GlobalMap.LastActivated.DestroyArmy(armySelectionHandler.Army);
                        }
                    }, null, 0, "Disband", "Cancel");
                });

                var frame = buttonNew.transform as RectTransform;
                frame.name = "BUBBLEBUT0";
                frame.localPosition += new Vector3(0, frame.rect.height + 4, 0);
            }
        }
    }
}
