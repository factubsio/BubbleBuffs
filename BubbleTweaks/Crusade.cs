using BubbleTweaks.Utilities;
using Kingmaker;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.State;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.Common;
using Owlcat.Runtime.UI.Controls.Button;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

    public class Crusade {
        public static void Install() {
            Main.LogHeader("Installing Crusade Tweaks");
            EventBus.Subscribe(armySelectionHandler);
        }

        public static void Uninstall() {
            EventBus.Unsubscribe(armySelectionHandler);
        }

        private static bool init = false;
        private static readonly ArmySelectionHandler armySelectionHandler = new();

        private static OwlcatButton disbandButton;

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

                init = true;
            }
        }
    }
}
