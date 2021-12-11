using BubbleBuffs.Config;
using BubbleBuffs.Utilities;
using HarmonyLib;
using Kingmaker;
using Kingmaker.AreaLogic.Cutscenes;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.Common.Animations;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._PCView.IngameMenu;
using Kingmaker.UI.MVVM._PCView.Other;
using Kingmaker.UI.MVVM._PCView.Party;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Spellbook.KnownSpells;
using Kingmaker.UI.MVVM._VM.Other;
using Kingmaker.UI.MVVM._VM.Tooltip.Bricks;
using Kingmaker.UI.MVVM._VM.Tooltip.Templates;
using Kingmaker.UI.MVVM._VM.Tooltip.Utils;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.Utility;
using Newtonsoft.Json;
using Owlcat.Runtime.UI.Controls.Button;
using Owlcat.Runtime.UI.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using BubbleBuffs.Extensions;
using UnityEngine.SceneManagement;
using Kingmaker.UI.SettingsUI;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;

namespace BubbleBuffs {

    struct SpinnerButtons {
        public OwlcatButton up;
        public OwlcatButton down;
    }

    public class BubbleAnimator : MonoBehaviour {
        private static int NextId = 0;

        public Material Target;
        private float Start = UnityEngine.Random.Range(0, 20.0f);
        private float Warmup = 0;
        private double TimeAtDisable;
        private int Id = NextId++;
        private bool WasDisabled = true;

        void Awake() {
            Target.SetFloat("_Warmup", 1);
        }

        void Update() {
            Target.SetFloat("_BubbleTime", Start + Time.unscaledTime);
        }
    }


    public class BubbleBuffSpellbookController : MonoBehaviour {
        private GameObject ToggleButton;
        private bool Buffing => PartyView.m_Hide;
        private GameObject MainContainer;
        private GameObject NoSpellbooksContainer;
        public RectTransform TooltipRoot;

        private bool WasMainShown = false;

        private PartyPCView PartyView;
        private SavedBufferState save;
        public BufferState state;
        private BuffExecutor Executor;
        private BufferView view;

        private GameObject Root;

        private bool WindowCreated = false;

        public static Dictionary<string, TooltipBaseTemplate> AbilityTooltips = new();

        public static TooltipBaseTemplate TooltipForAbility(BlueprintAbility ability) {
            var key = ability.AssetGuid.ToString();
            if (!AbilityTooltips.TryGetValue(key, out var tooltip)) {
                tooltip = new TooltipTemplateAbility(ability);
                AbilityTooltips[key] = tooltip;
            }
            return tooltip;
        }

        public static string SettingsPath => $"{ModSettings.ModEntry.Path}UserSettings/bubblebuff-{Game.Instance.Player.GameId}.json";

        public void TryFixEILayout() {
            Transform eiToggle0 = UIHelpers.SpellbookScreen.Find("MainContainer/KnownSpells/ToggleAllSpells");

            if (eiToggle0 != null) {
                Main.Verbose("Tweaking stuff", "interop");
                var eiToggle2 = UIHelpers.SpellbookScreen.Find("MainContainer/KnownSpells/ToggleMetamagic");
                var eiToggle1 = UIHelpers.SpellbookScreen.Find("MainContainer/KnownSpells/TogglePossibleSpells");

                RectTransform[] eiToggles = { (RectTransform)eiToggle0, (RectTransform)eiToggle1, (RectTransform)eiToggle2 };

                for (int i = 0; i < eiToggles.Length; i++) {
                    eiToggles[i].localPosition = new Vector2(430.0f, -392.0f - 30f * i);
                    eiToggles[i].localScale = new Vector3(0.8f, 0.8f, .8f);
                }

                var eiLearnAll = UIHelpers.SpellbookScreen.Find("MainContainer/LearnAllSpells").transform as RectTransform;

                eiLearnAll.localPosition = new Vector2(800.0f, -400.0f);
            }
        }

        public void CreateBuffstate() {
            if (File.Exists(SettingsPath)) {
                using (var settingsReader = File.OpenText(SettingsPath))
                using (var jsonReader = new JsonTextReader(settingsReader)) {
                    save = JsonSerializer.CreateDefault().Deserialize<SavedBufferState>(jsonReader);

                    if (save.Version == 0) {
                        MigrateSaveToV1();
                    }
                }
            } else {
                save = new SavedBufferState();
            }

            state = new(save);
            view = new(state);
            Executor = new(state);

            view.widgetCache = new();
            view.widgetCache.PrefabGenerator = () => {
                SpellbookKnownSpellPCView spellPrefab = null;
                var listPrefab = UIHelpers.SpellbookScreen.Find("MainContainer/KnownSpells");
                var spellsKnownView = listPrefab.GetComponent<SpellbookKnownSpellsPCView>();

                if (spellsKnownView != null)
                    spellPrefab = listPrefab.GetComponent<SpellbookKnownSpellsPCView>().m_KnownSpellView;
                else {
                    foreach (var component in UIHelpers.SpellbookScreen.gameObject.GetComponents<Component>()) {
                        if (component.GetType().FullName == "EnhancedInventory.Controllers.SpellbookController") {
                            Main.Verbose(" ** INSTALLING WORKAROUND FOR ENHANCED INVENTORY **");
                            var fieldHandle = component.GetType().GetField("m_known_spell_prefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            Main.Verbose($"Got field handle: {fieldHandle != null}");
                            spellPrefab = (SpellbookKnownSpellPCView)fieldHandle.GetValue(component);
                            Main.Verbose($"Found spellPrefab: {spellPrefab != null}");

                            break;
                        }
                    }
                }

                var spellRoot = GameObject.Instantiate(spellPrefab.gameObject);
                spellRoot.name = "BubbleSpellView";
                spellRoot.DestroyComponents<SpellbookKnownSpellPCView>();
                spellRoot.DestroyChildrenImmediate("Icon/Decoration", "Icon/Domain", "Icon/MythicArtFrame", "Icon/ArtArrowImage", "RemoveButton", "Level");

                return spellRoot;

            };
        }

        private void MigrateSaveToV1() {
            Dictionary<string, string> nameToId = new();
            foreach (var ch in Game.Instance.Player.AllCharacters) {
                nameToId[ch.CharacterName] = ch.UniqueId;
            }

            foreach (SavedBuffState s in save.Buffs.Values) {
                HashSet<string> newWanted = new(s.Wanted.Where(name => nameToId.ContainsKey(name)).Select(name => nameToId[name]));
                s.Wanted = newWanted;

                Dictionary<CasterKey, SavedCasterState> casters = new();
                foreach (var casterEntry in s.Casters) {
                    var key = new CasterKey {
                        Name = nameToId[casterEntry.Key.Name],
                        Spellbook = casterEntry.Key.Spellbook
                    };
                    casters[key] = casterEntry.Value;
                }
                s.Casters = casters;
            }
        }

        private static void FadeOut(GameObject obj) {
            obj.GetComponent<FadeAnimator>().DisappearAnimation();
        }
        private static void FadeIn(GameObject obj) {
            obj.GetComponent<FadeAnimator>().AppearAnimation();
        }

        private List<Material> _ToAnimate = new();

        void Update() {
            foreach (var mat in _ToAnimate)
                mat.SetFloat("_BubbleTime", Time.unscaledTime);
        }

        private void Awake() {
            TryFixEILayout();

            MainContainer = transform.Find("MainContainer").gameObject;
            NoSpellbooksContainer = transform.Find("NoSpellbooksContainer").gameObject;

            PartyView = UIHelpers.StaticRoot.Find("PartyPCView").gameObject.GetComponent<PartyPCView>();

            GameObject.Destroy(transform.Find("bubblebuff-toggle")?.gameObject);
            GameObject.Destroy(transform.Find("bubblebuff-root")?.gameObject);

            ToggleButton = GameObject.Instantiate(transform.Find("MainContainer/MetamagicButton").gameObject, transform);
            (ToggleButton.transform as RectTransform).anchoredPosition = new Vector2(1400, 0);
            ToggleButton.name = "bubblebuff-toggle";
            ToggleButton.GetComponentInChildren<TextMeshProUGUI>().text = "buffsetup".i8();

            {
                var button = ToggleButton.GetComponentInChildren<OwlcatButton>();
                button.OnLeftClick.RemoveAllListeners();
                button.OnLeftClick.AddListener(() => {
                    PartyView.HideAnimation(!Buffing);

                    if (Buffing) {
                        WasMainShown = MainContainer.activeSelf;
                        if (WasMainShown)
                            FadeOut(MainContainer);
                        else
                            FadeOut(NoSpellbooksContainer);
                        MainContainer.SetActive(false);
                        NoSpellbooksContainer.SetActive(false);
                        ShowBuffWindow();
                    } else {
                        Hide();
                        if (WasMainShown) {
                            FadeIn(MainContainer);
                            MainContainer.SetActive(true);
                        } else {
                            FadeIn(NoSpellbooksContainer);
                            NoSpellbooksContainer.SetActive(true);
                        }
                    }

                });
            }

            Root = new GameObject("bubblebuff-root", typeof(RectTransform));
            Root.SetActive(false);
            Root.transform.SetParent(transform);
            var rect = Root.transform as RectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.localPosition = Vector3.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            var group = Root.AddComponent<CanvasGroup>();
            var fader = Root.AddComponent<FadeAnimator>();
        }

        internal void Hide() {
            FadeOut(Root);
            Root.SetActive(false);
        }

        private static GameObject MakeToggle(GameObject togglePrefab, Transform parent, float x, float y, string text, string name, float scale = 1) {
            var toggle = GameObject.Instantiate(togglePrefab, parent);
            toggle.name = name;
            var blacklistRect = toggle.transform as RectTransform;
            blacklistRect.localPosition = Vector2.zero;
            blacklistRect.anchoredPosition = Vector2.zero;
            blacklistRect.anchorMin = new Vector2(x, y);
            blacklistRect.anchorMax = new Vector2(x, y);
            blacklistRect.pivot = new Vector2(0.5f, 0.5f);
            blacklistRect.localScale = new Vector3(scale, scale, scale);
            toggle.GetComponentInChildren<TextMeshProUGUI>().text = text;
            toggle.SetActive(true);
            return toggle;
        }

        private Portrait CreatePortrait(float groupHeight, Transform groupRect, bool createLabel, bool createPopout, Portrait[] group = null, GameObject popout = null) {
            var portrait = new Portrait();
            float width = groupHeight * .75f;
            float height = groupHeight;

            var (p, pRect) = UIHelpers.Create($"bubblebuff-portrait", groupRect);

            portrait.GameObject = p;
            portrait.Image = p.AddComponent<Image>();
            p.MakeComponent<LayoutElement>(l => {
                l.preferredWidth = width;
                l.preferredHeight = height;
            });

            var normalBorder = AssetLoader.Sprites["UI_HudCharacterFrameBorder_Default"];
            var hoverBorder = AssetLoader.Sprites["UI_HudCharacterFrameBorder_Hover"];

            var (fullOverlay, fullOverlayRect) = UIHelpers.Create("full-overlay", pRect);
            fullOverlayRect.FillParent();
            portrait.FullOverlay = fullOverlay.MakeComponent<Image>(img => {
                img.material = AssetLoader.Materials["bubble_overlay_full"];
                img.gameObject.SetActive(false);
                img.color = new Color(1, 1, 1, UnityEngine.Random.Range(0f, 1.0f));
            });

            var (aoeOverlay, aoeOverlayRect) = UIHelpers.Create("aoe-overlay", pRect);
            aoeOverlayRect.FillParent();
            //aoeOverlayRect.anchorMax = new Vector2(1, 0.4f);
            portrait.Overlay = aoeOverlay.MakeComponent<Image>(img => {
                img.material = AssetLoader.Materials["bubbly_overlay"];
                img.gameObject.SetActive(false);
                img.color = new Color(0, 1, 0, UnityEngine.Random.Range(0f, 1.0f));
            });

            var (frameObj, _) = UIHelpers.Create("child-image", pRect);
            var frame = frameObj.AddComponent<Image>();
            frame.type = Image.Type.Sliced;
            frameObj.FillParent();
            frame.sprite = normalBorder;

            portrait.Button = p.AddComponent<OwlcatButton>();
            portrait.Button.OnHover.AddListener(h => {
                frame.sprite = h ? hoverBorder : normalBorder;
            });


            if (createLabel) {
                var labelPrefab = UIHelpers.SpellbookScreen.Find("MainContainer/MemorizingPanelContainer/MemorizingPanel/SubstituteContainer/Label").gameObject;
                var label = GameObject.Instantiate(labelPrefab, pRect);
                label.Rect().SetAnchor(0.5, 0.5, -.15, -.15);
                label.Rect().sizeDelta = new Vector2(width, 1);
                label.SetActive(true);
                portrait.Text = label.GetComponentInChildren<TextMeshProUGUI>();
                portrait.Text.richText = true;
                portrait.Text.lineSpacing = -15.0f;
                portrait.Text.text = "HELLO";
            }

            if (createPopout) {
                var expand = GameObject.Instantiate(expandButtonPrefab, pRect);
                expand.Rect().pivot = new Vector2(0.5f, 0.5f);
                expand.Rect().SetAnchor(0.5, 1);
                expand.GetComponent<OwlcatButton>().Interactable = true;
                expand.SetActive(true);
                portrait.Expand = expand.GetComponent<OwlcatButton>();
                portrait.Expand.OnLeftClick.AddListener(() => {
                    portrait.SetExpanded(!portrait.Expand.IsSelected);
                    if (portrait.Expand.IsSelected) {
                        foreach (var p in group)
                            if (p != portrait)
                                p.SetExpanded(false);

                        popout.transform.SetParent(pRect);
                        popout.Rect().anchoredPosition = new Vector2(0, 18);
                        popout.Rect().pivot = new Vector2(0.5f, 0);
                        popout.Rect().SetAnchor(0.5, 1);
                        popout.SetActive(true);
                    } else {
                        popout.SetActive(false);
                    }
                });
                portrait.SetExpanded(false);
            }

            return portrait;
        }

        public ReactiveProperty<bool> ShowNotRequested = new ReactiveProperty<bool>(true);
        public ReactiveProperty<bool> ShowRequested = new ReactiveProperty<bool>(true);
        public ReactiveProperty<bool> ShowShort = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> ShowHidden = new ReactiveProperty<bool>(false);
        public ReactiveProperty<string> NameFilter = new ReactiveProperty<string>("");
        public ButtonGroup<Category> CurrentCategory;

        private List<UnitEntityData> Group => Game.Instance.SelectionCharacter.ActualGroup;

        public GameObject expandButtonPrefab;

        private void CreateWindow() {
            var staticRoot = UIHelpers.StaticRoot;

            var portraitPrefab = staticRoot.Find("PartyPCView/PartyCharacterView_01").gameObject;
            Main.Verbose("Got portrait prefab");
            var listPrefab = UIHelpers.SpellbookScreen.Find("MainContainer/KnownSpells");
            Main.Verbose("Got list prefab");
            var spellsKnownView = listPrefab.GetComponent<SpellbookKnownSpellsPCView>();

            Main.Verbose("Got spell prefab");
            var framePrefab = UIHelpers.MythicInfoView.Find("Window/MainContainer/MythicInfoProgressionView/Progression/Frame").gameObject;
            Main.Verbose("Got frame prefab");
            expandButtonPrefab = UIHelpers.EncyclopediaView.Find("EncyclopediaPageView/HistoryManagerGroup/HistoryGroup/PreviousButton").gameObject;
            Main.Verbose("Got expandButton prefab");
            var toggleTransform = UIHelpers.SpellbookScreen.Find("MainContainer/KnownSpells/Toggle");
            if (toggleTransform == null)
                toggleTransform = UIHelpers.SpellbookScreen.Find("MainContainer/KnownSpells/TogglePossibleSpells");


            var togglePrefab = toggleTransform.gameObject;
            Main.Verbose("Got toggle prefab: ");
            buttonPrefab = UIHelpers.SpellbookScreen.Find("MainContainer/MetamagicContainer/Metamagic/Button").gameObject;
            Main.Verbose("Got button prefab: ");
            selectedPrefab = UIHelpers.CharacterScreen.Find("Menu/Button/Selected").gameObject;
            Main.Verbose("Got selected prefab");
            var nextPrefab = staticRoot.Find("PartyPCView/Background/Next").gameObject;
            Main.Verbose("Got next prefab");
            var prevPrefab = staticRoot.Find("PartyPCView/Background/Prev").gameObject;
            Main.Verbose("Got prev prefab");

            var content = Root.transform;
            Main.Verbose("got root.transform");

            view.listPrefab = listPrefab.gameObject;
            view.content = content;
            Main.Verbose("set view prefabs");

            view.MakeSummary();

            view.MakeBuffsList();

            MakeFilters(togglePrefab, content);

            MakeGroupHolder(portraitPrefab, expandButtonPrefab, buttonPrefab, content);
            Main.Verbose("made group holder");

            MakeDetailsView(portraitPrefab, framePrefab, nextPrefab, prevPrefab, togglePrefab, expandButtonPrefab, content);
            Main.Verbose("made details view");

            var partialOverlay = AssetLoader.Materials["bubbly_overlay"];
            partialOverlay.SetFloat("_Speed", 0.3f);
            partialOverlay.SetFloat("_Warmup", 1);
            _ToAnimate.Add(partialOverlay);
            var fullOverlay = AssetLoader.Materials["bubble_overlay_full"];
            fullOverlay.SetFloat("_Speed", 0.3f);
            fullOverlay.SetFloat("_Warmup", 1);
            _ToAnimate.Add(fullOverlay);

            MakeSettings(togglePrefab, content);

            var tooltipRootObj = new GameObject("tooltip-root", typeof(RectTransform));
            TooltipRoot = tooltipRootObj.Rect();
            TooltipRoot.AddTo(Root);
            TooltipRoot.SetAsLastSibling();



            ShowHidden.Subscribe<bool>(show => {
                RefreshFiltering();
            });
            ShowNotRequested.Subscribe<bool>(show => {
                RefreshFiltering();
            });
            ShowRequested.Subscribe<bool>(show => {
                RefreshFiltering();
            });
            ShowShort.Subscribe<bool>(show => {
                RefreshFiltering();
            });
            NameFilter.Subscribe<string>(val => {
                if (search.InputField.text != val)
                    search.InputField.text = val;
                RefreshFiltering();
            });



            view.currentSelectedSpell.Subscribe(val => {
                try {
                    HideCasterPopout?.Invoke();
                    if (view.currentSelectedSpell.HasValue && view.currentSelectedSpell.Value != null) {
                        var buff = view.Selected;

                        if (buff == null) {
                            currentSpellView.SetActive(false);
                            return;
                        }

                        currentSpellView.SetActive(true);
                        BubbleSpellView.BindBuffToView(buff, currentSpellView);

                        view.addToAll.SetActive(true);
                        view.removeFromAll.SetActive(true);

                        //float actualWidth = (buff.CasterQueue.Count - 1) * castersHolder.GetComponent<HorizontalLayoutGroup>().spacing;
                        //(castersHolder.transform as RectTransform).anchoredPosition = new Vector2(-actualWidth / 2.0f, 0);
                        view.Update();
                    } else {
                        currentSpellView.SetActive(false);

                        view.addToAll.SetActive(false);
                        view.removeFromAll.SetActive(false);

                        foreach (var caster in view.casterPortraits)
                            caster.GameObject.SetActive(false);

                        foreach (var portrait in view.targets) {
                            portrait.FullOverlay.gameObject.SetActive(false);
                            portrait.Button.Interactable = true;
                        }
                    }
                } catch (Exception e) {
                    Main.Error(e, "SELECTING SPELL");
                }
            });
            view.OnUpdate = () => {
                UpdateDetailsView?.Invoke();
            };
            WindowCreated = true;
        }

        private static (ToggleWorkaround, TextMeshProUGUI) MakeSettingsToggle(GameObject prefab, Transform content, string text) {
            var toggleObj = GameObject.Instantiate(prefab, content);
            toggleObj.SetActive(true);
            toggleObj.Rect().localPosition = Vector3.zero;
            toggleObj.GetComponent<HorizontalLayoutGroup>().childControlWidth = true;
            var label = toggleObj.GetComponentInChildren<TextMeshProUGUI>();
            label.text = text;
            return (toggleObj.GetComponentInChildren<ToggleWorkaround>(), label);
        }

        private void MakeSettings(GameObject togglePrefab, Transform content) {
            var staticRoot = Game.Instance.UI.Canvas.transform;
            var button = staticRoot.Find("HUDLayout/IngameMenuView/ButtonsPart/Container/SettingsButton").gameObject;

            var toggleSettings = GameObject.Instantiate(button, content);
            toggleSettings.Rect().anchoredPosition = Vector3.zero;
            toggleSettings.Rect().pivot = new Vector2(1, 0);
            toggleSettings.Rect().SetAnchor(.93, .10);

            var actionBarView = UIHelpers.StaticRoot.Find("ActionBarPcView").GetComponent<ActionBarPCView>();
            var panel = GameObject.Instantiate(actionBarView.m_DragSlot.m_ConvertedView.gameObject, toggleSettings.transform);
            panel.DestroyComponents<ActionBarConvertedPCView>();
            panel.DestroyComponents<GridLayoutGroup>();
            panel.SetActive(false);
            panel.Rect().SetAnchor(0, 1);
            panel.Rect().sizeDelta = new Vector2(100, 100);
            panel.Rect().pivot = new Vector2(1, 0);
            panel.Rect().anchoredPosition = Vector3.zero;
            panel.Rect().anchoredPosition = new Vector2(-3, 3);

            var popGrid = panel.AddComponent<GridLayoutGroup>();
            popGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            popGrid.constraintCount = 1;
            int width = 450;
            if (Language.Locale == Locale.deDE)
                width = 550;
            popGrid.cellSize = new Vector2(width, 40);
            popGrid.padding.left = 25;
            popGrid.padding.top = 12;
            popGrid.padding.bottom = 50;
            popGrid.startCorner = GridLayoutGroup.Corner.LowerLeft;

            var (toggle, _) = MakeSettingsToggle(togglePrefab, panel.transform, "setting-in-combat".i8());
            toggle.isOn = state.AllowInCombat;
            toggle.onValueChanged.AddListener(enabled => {
                state.AllowInCombat = enabled;
                bool allow = !Game.Instance.Player.IsInCombat || enabled;
                GlobalBubbleBuffer.Instance.Buttons.ForEach(b => b.Interactable = allow);
            });

            var (toggle1, _) = MakeSettingsToggle(togglePrefab, panel.transform, "setting-overwritebuff".i8());
            toggle1.isOn = state.OverwriteBuff;
            toggle1.onValueChanged.AddListener(enabled => {
                state.OverwriteBuff = enabled;
                
            });

            var b = toggleSettings.GetComponent<OwlcatButton>();
            b.SetTooltip(new TooltipTemplateSimple("settings".i8(), "settings-toggle".i8()), new TooltipConfig {
                InfoCallMethod = InfoCallMethod.None
            });

            b.OnLeftClick.AddListener(() => {
                panel.SetActive(!panel.activeSelf);
                b.IsPressed = panel.activeSelf;
            });
        }

        private void RegenerateWidgetCache(Transform listPrefab, SpellbookKnownSpellsPCView spellsKnownView) {
            if (view.widgetCache == null) {
            }
        }

        public static GameObject buttonPrefab;
        public static GameObject selectedPrefab;

        public static GameObject MakeButton(string title, Transform parent) {
            var button = GameObject.Instantiate(buttonPrefab, parent);
            button.GetComponentInChildren<TextMeshProUGUI>().text = title;
            var buttonRect = button.transform as RectTransform;
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(1, 1);
            buttonRect.localPosition = Vector3.zero;
            buttonRect.anchoredPosition = Vector2.zero;
            return button;
        }

        public class ButtonGroup<T> {
            public ReactiveProperty<T> Selected = new();
            private readonly Transform content;

            public ButtonGroup(Transform content) {
                this.content = content;
            }

            public T Value {
                get => Selected.Value;
                set => Selected.Value = value;
            }

            public void Add(T value, string title) {
                var button = MakeButton(title, content);

                var selection = GameObject.Instantiate(selectedPrefab, button.transform);
                selection.SetActive(false);

                Selected.Subscribe<T>(s => {
                    selection.SetActive(EqualityComparer<T>.Default.Equals(s, value));
                });
                button.GetComponentInChildren<OwlcatButton>().Interactable = true;
                button.GetComponentInChildren<OwlcatButton>().OnLeftClick.AddListener(() => {
                    Selected.Value = value;
                });
            }

        }

        public static RectTransform MakeVerticalRect(string name, Transform parent) {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.AddComponent<VerticalLayoutGroup>().childForceExpandHeight = false;
            var rect = obj.Rect();
            rect.SetParent(parent);
            rect.anchoredPosition3D = Vector3.zero;
            return rect;
        }

        private void MakeFilters(GameObject togglePrefab, Transform content) {
            var filterRect = MakeVerticalRect("filters", content);
            //filterToggles.AddComponent<Image>().color = Color.green;
            filterRect.anchorMin = new Vector2(0.13f, 0.1f);
            filterRect.anchorMax = new Vector2(0.215f, .4f);
            filterRect.gameObject.EditComponent<VerticalLayoutGroup>(v => {
                v.childScaleHeight = true;
                v.childScaleWidth = true;

            });

            search = new SearchBar(filterRect, "...", false, "bubble-search-buff");

            const float scale = 1.0f;
            GameObject showHidden = MakeToggle(togglePrefab, filterRect, 0.8f, .5f, "showhidden".i8(), "bubble-toggle-show-hidden", scale);
            GameObject showShort = MakeToggle(togglePrefab, filterRect, .8f, .5f, "showshort".i8(), "bubble-toggle-show-short", scale);
            GameObject showRequested = MakeToggle(togglePrefab, filterRect, .8f, .5f, "showreq".i8(), "bubble-toggle-show-requested", scale);
            GameObject showNotRequested = MakeToggle(togglePrefab, filterRect, .8f, .5f, "showNOTreq".i8(), "bubble-toggle-show-not-requested", scale);

            search.InputField.onValueChanged.AddListener(val => {
                NameFilter.Value = val;
            });

            var categoryRect = MakeVerticalRect("categories", content);
            categoryRect.anchorMin = new Vector2(1 - filterRect.anchorMax.x, 0.1f);
            categoryRect.anchorMax = new Vector2(1 - filterRect.anchorMin.x, 0.4f);

            CurrentCategory = new ButtonGroup<Category>(categoryRect);
            CurrentCategory.Selected.Subscribe<Category>(_ => RefreshFiltering());

            CurrentCategory.Add(Category.Spell, "cat.spells".i8());
            CurrentCategory.Add(Category.Ability, "cat.Abilities".i8());
            CurrentCategory.Add(Category.Item, "cat.Items".i8());
            CurrentCategory.Add(Category.Consumable, "cat.Consumables".i8());


            ShowShort.BindToView(showShort);
            ShowHidden.BindToView(showHidden);
            ShowRequested.BindToView(showRequested);
            ShowNotRequested.BindToView(showNotRequested);

            CurrentCategory.Selected.Value = Category.Spell;
        }

        private void RefreshFiltering() {
            if (state.BuffList == null)
                return;

            foreach (var buff in state.BuffList) {

                if (!view.buffWidgets.TryGetValue(buff.Key, out var widget) || widget == null)
                    continue;

                bool show = true;

                if (buff.Category != CurrentCategory.Value)
                    show = false;

                bool showForRequested = ShowRequested.Value && buff.Requested > 0 || ShowNotRequested.Value && buff.Requested == 0;
                if (!showForRequested)
                    show = false;

                if (NameFilter.Value.Length > 0) {
                    var filterString = NameFilter.Value.ToLower();
                    if (!buff.NameLower.Contains(filterString))
                        show = false;
                }

                if (!ShowHidden.Value && buff.HideBecause(HideReason.Blacklisted))
                    show = false;
                if (!ShowShort.value && buff.HideBecause(HideReason.Short))
                    show = false;

                widget.SetActive(show);
            }
        }

        private Action HideCasterPopout;
        private Action UpdateDetailsView;

        private static BlueprintFeature PowerfulChangeFeature => Resources.GetBlueprint<BlueprintFeature>("5e01e267021bffe4e99ebee3fdc872d1");
        private static BlueprintFeature ShareTransmutationFeature => Resources.GetBlueprint<BlueprintFeature>("c4ed8d1a90c93754eacea361653a7d56");

        private void MakeDetailsView(GameObject portraitPrefab, GameObject framePrefab, GameObject nextPrefab, GameObject prevPrefab, GameObject togglePrefab, GameObject expandButtonPrefab, Transform content) {
            var detailsHolder = GameObject.Instantiate(framePrefab, content);
            var detailsRect = detailsHolder.GetComponent<RectTransform>();
            GameObject.Destroy(detailsHolder.transform.Find("FrameDecor").gameObject);
            detailsRect.localPosition = Vector2.zero;
            detailsRect.sizeDelta = Vector2.zero;
            detailsRect.anchorMin = new Vector2(0.25f, 0.225f);
            detailsRect.anchorMax = new Vector2(0.75f, 0.475f);

            currentSpellView = view.widgetCache.Get(detailsRect);

            currentSpellView.GetComponentInChildren<OwlcatButton>().Interactable = false;
            currentSpellView.SetActive(false);
            var currentSpellRect = currentSpellView.transform as RectTransform;
            currentSpellRect.SetAnchor(.5, .8);


            ReactiveProperty<int> SelectedCaster = new ReactiveProperty<int>(-1);

            var actionBarView = UIHelpers.StaticRoot.Find("ActionBarPcView").GetComponent<ActionBarPCView>();
            var popout = GameObject.Instantiate(actionBarView.m_DragSlot.m_ConvertedView.gameObject, content);

            HideCasterPopout = () => {
                view.casterPortraits.ForEach(x => x.SetExpanded(false));
                popout.SetActive(false);
            };
            popout.DestroyComponents<ActionBarConvertedPCView>();
            popout.DestroyComponents<GridLayoutGroup>();
            popout.Rect().anchoredPosition3D = Vector3.zero;
            popout.Rect().localPosition = Vector3.zero;
            popout.Rect().SetAnchor(0, 0);
            popout.SetActive(false);
            popout.ChildObject("Background").GetComponent<Image>().raycastTarget = true;

            var popGrid = popout.AddComponent<GridLayoutGroup>();
            popGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            popGrid.constraintCount = 1;
            int width = 450;
            if (Language.Locale == Locale.deDE)
                width = 650;
            popGrid.cellSize = new Vector2(width, 40);
            popGrid.padding.left = 25;
            popGrid.padding.top = 12;
            popGrid.padding.bottom = 12;

            GameObject MakeLabel(string text) {
                var labelRoot = GameObject.Instantiate(togglePrefab, popout.transform);
                Main.Verbose($"Label root: {labelRoot == null}");
                labelRoot.DestroyComponents<ToggleWorkaround>();
                labelRoot.DestroyChildren("Background");
                labelRoot.GetComponentInChildren<TextMeshProUGUI>().text = text;
                labelRoot.SetActive(true);

                return labelRoot;
            }

            var hideSpell = MakeToggle(togglePrefab, detailsRect.transform, 0.03f, 0.8f, "hideability".i8(), "hide-spell");
            hideSpell.transform.SetSiblingIndex(0);
            hideSpell.SetActive(false);
            hideSpell.Rect().pivot = new Vector2(0, 0.5f);
            var hideSpellToggle = hideSpell.GetComponentInChildren<ToggleWorkaround>();

            view.addToAll = GameObject.Instantiate(buttonPrefab, detailsRect);
            view.addToAll.GetComponentInChildren<TextMeshProUGUI>().text = "add-all".i8();
            var addToAllRect = view.addToAll.transform as RectTransform;
            addToAllRect.localPosition = Vector3.zero;
            addToAllRect.anchoredPosition = Vector3.zero;
            addToAllRect.pivot = new Vector2(0, 0);
            addToAllRect.sizeDelta = new Vector2(180, 50);
            addToAllRect.SetAnchor(0.03f, 0.1f);

            view.removeFromAll = GameObject.Instantiate(view.addToAll, detailsRect);
            view.removeFromAll.GetComponentInChildren<TextMeshProUGUI>().text = "remove-all".i8();
            var removeFromAllRect = view.removeFromAll.transform as RectTransform;
            removeFromAllRect.SetAnchor(0.03f, 0.3f);

            view.addToAll.GetComponentInChildren<OwlcatButton>().OnLeftClick.AddListener(() => {
                var buff = view.Selected;
                if (buff == null) return;

                for (int i = 0; i < Group.Count; i++) {
                    if (view.targets[i].Button.Interactable && !buff.UnitWants(Group[i])) {
                        buff.SetUnitWants(Group[i], true);
                    }
                }
                state.Recalculate(true);

            });
            view.removeFromAll.GetComponentInChildren<OwlcatButton>().OnLeftClick.AddListener(() => {
                var buff = view.Selected;
                if (buff == null) return;

                for (int i = 0; i < Group.Count; i++) {
                    if (buff.UnitWants(Group[i])) {
                        buff.SetUnitWants(Group[i], false);
                    }
                }
                state.Recalculate(true);
            });





            var capLabel = MakeLabel("  " + "limitcasts".i8());

            var (blacklistToggle, _) = MakePopoutToggle("bancasts".i8());
            var (powerfulChangeToggle, powerfulChangeLabel) = MakePopoutToggle("use.powerfulchange".i8());
            var (shareTransmutationToggle, shareTransmutationLabel) = MakePopoutToggle("use.sharetransmutation".i8());
            var defaultLabelColor = shareTransmutationLabel.color;

            (ToggleWorkaround, TextMeshProUGUI) MakePopoutToggle(string text) {
                var toggleObj = GameObject.Instantiate(togglePrefab, popout.transform);
                toggleObj.SetActive(true);
                toggleObj.Rect().localPosition = Vector3.zero;
                toggleObj.GetComponent<HorizontalLayoutGroup>().childControlWidth = true;
                var label = toggleObj.GetComponentInChildren<TextMeshProUGUI>();
                label.text = text;
                return (toggleObj.GetComponentInChildren<ToggleWorkaround>(), label);

            }
            MakeLabel("warn.arcanepool".i8());

            float capChangeScale = 0.7f;
            var decreaseCustomCap = GameObject.Instantiate(expandButtonPrefab, capLabel.transform);
            decreaseCustomCap.Rect().localScale = new Vector3(capChangeScale, capChangeScale, capChangeScale);
            decreaseCustomCap.Rect().pivot = new Vector2(.5f, .5f);
            decreaseCustomCap.Rect().SetRotate2D(90);
            decreaseCustomCap.Rect().anchoredPosition = Vector2.zero;
            decreaseCustomCap.SetActive(true);
            var decreaseCustomCapButton = decreaseCustomCap.GetComponent<OwlcatButton>();

            var capValueLabel = GameObject.Instantiate(togglePrefab.GetComponentInChildren<TextMeshProUGUI>().gameObject, capLabel.transform);
            var capValueText = capValueLabel.GetComponent<TextMeshProUGUI>();
            capValueText.text = "nolimit".i8();
            //capValueLabel.AddComponent<LayoutElement>().preferredWidth = 80;

            var increaseCustomCap = GameObject.Instantiate(expandButtonPrefab, capLabel.transform);
            increaseCustomCap.Rect().pivot = new Vector2(.5f, .5f);
            increaseCustomCap.Rect().localScale = new Vector3(capChangeScale, capChangeScale, capChangeScale);
            increaseCustomCap.Rect().SetRotate2D(-90);
            increaseCustomCap.Rect().anchoredPosition = Vector2.zero;
            increaseCustomCap.SetActive(true);
            var increaseCustomCapButton = increaseCustomCap.GetComponent<OwlcatButton>();


            void AdjustCap(int delta) {
                var buff = view.currentSelectedSpell.Value;
                if (buff == null) return;
                if (SelectedCaster.Value < 0) return;

                buff.AdjustCap(SelectedCaster.Value, delta);
                state.Recalculate(true);
            }

            decreaseCustomCapButton.OnLeftClick.AddListener(() => {
                AdjustCap(-1);
            });
            increaseCustomCapButton.OnLeftClick.AddListener(() => {
                AdjustCap(1);
            });

            decreaseCustomCapButton.Interactable = false;
            increaseCustomCapButton.Interactable = false;

            view.casterPortraits = new Portrait[totalCasters];

            shareTransmutationToggle.onValueChanged.AddListener(allow => {
                if (SelectedCaster.Value >= 0 && view.Get(out var buff)) {
                    buff.CasterQueue[SelectedCaster.Value].ShareTransmutation = allow;
                    state.Recalculate(true);
                }
            });
            powerfulChangeToggle.onValueChanged.AddListener(allow => {
                if (SelectedCaster.Value >= 0 && view.Get(out var buff)) {
                    buff.CasterQueue[SelectedCaster.Value].PowerfulChange = allow;
                    state.Recalculate(true);
                }
            });

            blacklistToggle.onValueChanged.AddListener((blacklisted) => {
                var buff = view.currentSelectedSpell?.Value;
                if (buff == null)
                    return;
                if (SelectedCaster.Value < 0)
                    return;

                var caster = buff.CasterQueue[SelectedCaster.value];

                if (blacklisted != caster.Banned) {
                    caster.Banned = blacklisted;
                    state.Recalculate(true);
                }
            });

            const float groupHeight = 90f;
            var (groupHolder, castersRect) = UIHelpers.Create("CastersHolder", detailsRect);
            castersHolder = groupHolder;
            groupHolder.MakeComponent<ContentSizeFitter>(f => {
                f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            });
            castersRect.anchorMin = new Vector2(0.5f, 0.22f);
            castersRect.anchorMax = new Vector2(0.5f, 0.57f);
            castersRect.SetAnchor(0.5f, 0.2f);
            castersRect.sizeDelta = new Vector2(300, groupHeight);
            castersRect.pivot = new Vector2(0.5f, 0);

            var horizontalGroup = groupHolder.AddComponent<HorizontalLayoutGroup>();
            horizontalGroup.spacing = 6;
            horizontalGroup.childControlHeight = true;
            horizontalGroup.childForceExpandHeight = true;

            for (int i = 0; i < totalCasters; i++) {
                var portrait = CreatePortrait(groupHeight, castersRect, true, true, view.casterPortraits, popout);
                view.casterPortraits[i] = portrait;
                portrait.Image.color = Color.yellow;

                portrait.Text.fontSizeMax = 18;
                portrait.Text.alignment = TextAlignmentOptions.Center;
                portrait.Text.fontSize = 18;
                portrait.Text.color = Color.black;
                portrait.Text.gameObject.transform.parent.gameObject.SetActive(true);
                portrait.Text.text = "12/12";
                int casterIndex = i;

                portrait.Expand?.OnLeftClick.AddListener(() => {
                    if (portrait.Expand?.IsSelected ?? false) {
                        SelectedCaster.Value = casterIndex;
                        UpdateDetailsView();
                    } else {
                        SelectedCaster.Value = -1;
                    }
                });
            }

            var groupRect = MakeVerticalRect("buff-group", detailsRect);
            groupRect.gameObject.SetActive(false);
            groupRect.SetAnchor(0.9f, 0.6f);
            groupRect.anchoredPosition = new Vector2(-20, 0);
            groupRect.sizeDelta = new Vector2(140, 100);

            var buffGroup = new ButtonGroup<BuffGroup>(groupRect);

            buffGroup.Add(BuffGroup.Long, "group.normal".i8());
            buffGroup.Add(BuffGroup.Important, "group.important".i8());
            buffGroup.Add(BuffGroup.Short, "group.short".i8());

            castersRect.SetAsLastSibling();

            buffGroup.Selected.Subscribe<BuffGroup>(g => {
                if (view.Get(out var buff)) {
                    buff.InGroup = g;
                    state.Save();
                }
            });

            hideSpellToggle.onValueChanged.AddListener(shouldHide => {
                if (view.Get(out var buff)) {
                    buff.SetHidden(HideReason.Blacklisted, shouldHide);
                    state.Save();
                    RefreshFiltering();
                }
            });

            UpdateDetailsView = () => {
                bool hasBuff = view.Get(out var buff);

                groupRect.gameObject.SetActive(hasBuff);
                hideSpell.SetActive(hasBuff);

                if (!hasBuff)
                    return;

                buffGroup.Selected.Value = buff.InGroup;
                hideSpellToggle.isOn = buff.HideBecause(HideReason.Blacklisted);

                if (SelectedCaster.Value >= 0 && popout.activeSelf) {
                    var who = buff.CasterQueue[SelectedCaster.value];
                    int actualCap = who.CustomCap < 0 ? who.MaxCap : who.CustomCap;
                    if (who.MaxCap < 100)
                        capValueText.text = $"{actualCap}/{who.MaxCap}";
                    else
                        capValueText.text = $"available.atwill".i8();

                    blacklistToggle.isOn = who.Banned;
                    shareTransmutationToggle.isOn = who.ShareTransmutation;
                    powerfulChangeToggle.isOn = who.PowerfulChange;

                    var skidmarkable = who.spell.IsArcanistSpell && who.spell.Blueprint.School == Kingmaker.Blueprints.Classes.Spells.SpellSchool.Transmutation;
                    shareTransmutationToggle.interactable = skidmarkable && who.who.HasFact(ShareTransmutationFeature);
                    powerfulChangeToggle.interactable = skidmarkable && who.who.HasFact(PowerfulChangeFeature);

                    shareTransmutationLabel.color = shareTransmutationToggle.interactable ? defaultLabelColor : Color.gray;
                    powerfulChangeLabel.color = powerfulChangeToggle.interactable ? defaultLabelColor : Color.gray;

                    increaseCustomCapButton.Interactable = who.AvailableCredits < 100 && who.CustomCap != -1;
                    decreaseCustomCapButton.Interactable = who.AvailableCredits < 100 && who.CustomCap != 0;

                }

            };
        }


        private int totalCasters = 0;
        private GameObject currentSpellView;
        private SearchBar search;

        private void MakeGroupHolder(GameObject portraitPrefab, GameObject expandButtonPrefab, GameObject buttonPrefab, Transform content) {
            var groupHolder = new GameObject("GroupHolder", typeof(RectTransform));
            var groupRect = groupHolder.GetComponent<RectTransform>();
            groupRect.AddTo(content);
            groupHolder.MakeComponent<ContentSizeFitter>(f => {
                f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            });

            float requiredWidthHalf = Group.Count * 0.033f;

            const float groupHeight = 166.25f;

            Main.Log($"z-before-anchor: {groupRect.transform.position.z}");
            groupRect.SetAnchor(0.5f, 0.08f);
            Main.Log($"after-anchor-z: {groupRect.transform.position.z}");
            groupRect.sizeDelta = new Vector2(300, groupHeight);
            groupRect.pivot = new Vector2(0.5f, 0);

            var horizontalGroup = groupHolder.AddComponent<HorizontalLayoutGroup>();
            horizontalGroup.spacing = 6;
            horizontalGroup.childControlHeight = true;
            horizontalGroup.childForceExpandHeight = true;

            view.targets = new Portrait[Group.Count];

            for (int i = 0; i < Group.Count; i++) {
                Portrait portrait = CreatePortrait(groupHeight, groupRect, false, false);

                portrait.GameObject.SetActive(true);
                var aspect = portrait.GameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                aspect.aspectRatio = 0.75f;

                portrait.Image.sprite = Group[i].Portrait.SmallPortrait;

                int personIndex = i;

                portrait.Button.OnLeftClick.AddListener(() => {
                    UnitEntityData me = Group[personIndex];
                    var buff = view.Selected;
                    if (buff == null)
                        return;

                    if (!buff.CanTarget(me))
                        return;

                    if (buff.UnitWants(me)) {
                        buff.SetUnitWants(me, false);
                    } else {
                        buff.SetUnitWants(me, true);
                    }

                    try {
                        state.Recalculate(true);
                    } catch (Exception ex) {
                        Main.Error(ex, "Recalculating spell list?");
                    }

                });
                view.targets[i] = portrait;

                totalCasters += Group[i].Spellbooks?.Count() ?? 0;
            }
        }

        private void ShowBuffWindow() {

            if (!WindowCreated) {
                try {
                    CreateWindow();
                } catch (Exception ex) {
                    Main.Error(ex, "Creating window?");
                }
            }
            state.Recalculate(true);
            RefreshFiltering();
            Root.SetActive(true);
            FadeIn(Root);
        }

        public void Destroy() {
            GameObject.Destroy(Root);
            GameObject.Destroy(ToggleButton);
        }

        private void OnDestroy() {
        }

        private GameObject castersHolder;



        internal void Execute(BuffGroup group) {
            UnitBuffPartView.StartSuppression();
            Executor.Execute(group);
            Invoke("EndBuffPartViewSuppression", 1.0f);
        }



        internal void RevalidateSpells() {
            if (state.GroupIsDirty(Group)) {
                AbilityCache.Revalidate();
            }

            state.InputDirty = true;
        }

        public void EndBuffPartViewSuppression() {
            UnitBuffPartView.EndSuppresion();
        }

    }

    public class CasterCacheEntry {
        public Ability PowerfulChange;
        public Ability ShareTransmutation;
    }

    public class AbilityCache {

        public static Dictionary<string, CasterCacheEntry> CasterCache = new();

        public static void Revalidate() {
            Main.Verbose("Revalidating Caster Cache");
            CasterCache.Clear();
            foreach (var u in Bubble.Group) {
                var entry = new CasterCacheEntry {
                    PowerfulChange = u.Abilities.GetAbility(BubbleBlueprints.PowerfulChange),
                    ShareTransmutation = u.Abilities.GetAbility(BubbleBlueprints.ShareTransmutation)
                };
                CasterCache[u.UniqueId] = entry;
            }
        }
    }


    //[HarmonyPatch(typeof(UnitBuffPartPCView), "DrawBuffs")]
    static class UnitBuffPartView {

        private static bool suppress;

        public static void StartSuppression() {
            suppress = true;
        }

        public static void EndSuppresion() {
            suppress = false;
            int count = toUpdate.Count;
            foreach (var view in toUpdate)
                view.DrawBuffs();
            toUpdate.Clear();
            Main.Verbose($"Suppressed {suppressed} draws across {count} views");
            suppressed = 0;
        }

        private static int suppressed = 0;

        private static HashSet<UnitBuffPartPCView> toUpdate = new();

        static bool Prefix(UnitBuffPartPCView __instance) {
            if (suppress) {
                suppressed++;
                toUpdate.Add(__instance);
                return false;
            }

            __instance.Clear();
            bool flag = __instance.ViewModel.Buffs.Count > 6;
            __instance.m_AdditionalTrigger.gameObject.SetActive(flag);
            int num = 0;
            foreach (BuffVM viewModel in __instance.ViewModel.Buffs) {
                BuffPCView widget = WidgetFactory.GetWidget<BuffPCView>(__instance.m_BuffView, true);
                widget.Bind(viewModel);
                if (flag && num >= 5) {
                    widget.transform.SetParent(__instance.m_AdditionalContainer, false);
                } else {
                    widget.transform.SetParent(__instance.m_MainContainer, false);
                }
                num++;
                __instance.m_BuffList.Add(widget);
            }

            return false;
        }

    }

    class TooltipTemplateBuffer : TooltipBaseTemplate {
        public class BuffResult {
            public BubbleBuff buff;
            public List<string> messages;
            public int count;
            public BuffResult(BubbleBuff buff) {
                this.buff = buff;
            }
        };
        private List<BuffResult> good = new();
        private List<BuffResult> bad = new();
        private List<BuffResult> skipped = new();

        public BuffResult AddBad(BubbleBuff buff) {
            BuffResult result = new(buff);
            result.messages = new();
            bad.Add(result);
            return result;
        }
        public BuffResult AddSkip(BubbleBuff buff) {
            BuffResult result = new(buff);
            skipped.Add(result);
            return result;
        }
        public BuffResult AddGood(BubbleBuff buff) {
            BuffResult result = new(buff);
            good.Add(result);
            return result;
        }

        public override IEnumerable<ITooltipBrick> GetHeader(TooltipTemplateType type) {
            yield return new TooltipBrickEntityHeader($"BubbleBuff {"tooltip.results".i8()}", null);
            yield break;
        }
        public override IEnumerable<ITooltipBrick> GetBody(TooltipTemplateType type) {
            List<ITooltipBrick> elements = new();
            AddResultsNoMessages("tooltip.applied".i8(), elements, good);
            AddResultsNoMessages("tooltip.skipped".i8(), elements, skipped);

            if (!bad.Empty()) {
                elements.Add(new TooltipBrickTitle("tooltip.failed".i8()));
                elements.Add(new TooltipBrickSeparator());

                foreach (var r in bad) {
                    elements.Add(new TooltipBrickIconAndName(r.buff.Spell.Icon, $"<b>{r.buff.Name}</b>", TooltipBrickElementType.Small));
                    foreach (var msg in r.messages)
                        elements.Add(new TooltipBrickText("   " + msg));

                }
            }

            return elements;
        }

        private void AddResultsNoMessages(string title, List<ITooltipBrick> elements, List<BuffResult> result) {
            if (!result.Empty()) {
                elements.Add(new TooltipBrickTitle(title));
                elements.Add(new TooltipBrickSeparator());
                foreach (var r in result) {
                    elements.Add(new TooltipBrickIconAndName(r.buff.Spell.Icon, $"<b>{r.buff.NameMeta}</b> x{r.count}", TooltipBrickElementType.Small));
                }
            }
        }
    }

    class ServiceWindowWatcher : IUIEventHandler {
        public void HandleUIEvent(UIEventType type) {
            GlobalBubbleBuffer.Instance.SpellbookController?.Hide();
        }
    }

    class SyncBubbleHud : MonoBehaviour {
        private GameObject bubbleHud => GlobalBubbleBuffer.Instance.bubbleHud;

        private void OnEnable() {
            if (bubbleHud != null && !bubbleHud.activeSelf)
                bubbleHud.SetActive(true);
        }
        private void OnDisable() {
            if (bubbleHud != null && bubbleHud.activeSelf)
            bubbleHud?.SetActive(false);
        }

        public void Destroy() { }

    }

    class GlobalBubbleBuffer {
        public BubbleBuffSpellbookController SpellbookController;
        private ButtonSprites applyBuffsSprites;
        private ButtonSprites applyBuffsShortSprites;
        private ButtonSprites applyBuffsImportantSprites;
        private GameObject buttonsContainer;
        public GameObject bubbleHud;
        public GameObject hudLayout;

        public static Sprite[] UnitFrameSprites = new Sprite[2];

        public List<OwlcatButton> Buttons = new();

        internal void TryInstallUI() {
            try {
                Main.Verbose("Installing ui");
                Main.Verbose($"spellscreennull: {UIHelpers.SpellbookScreen == null}");
                var spellScreen = UIHelpers.SpellbookScreen.gameObject;
                Main.Verbose("got spell screen");

                UnitFrameSprites[0] = AssetLoader.LoadInternal("icons", "UI_HudCharacterFrameBorder_Default.png", new Vector2Int(31, 80));
                UnitFrameSprites[1] = AssetLoader.LoadInternal("icons", "UI_HudCharacterFrameBorder_Hover.png", new Vector2Int(31, 80));

#if DEBUG
                RemoveOldController(spellScreen);
#endif

                if (spellScreen.transform.root.GetComponent<BubbleBuffGlobalController>() == null) {
                    Main.Verbose("Creating new global controller");
                    spellScreen.transform.root.gameObject.AddComponent<BubbleBuffGlobalController>();
                }

                if (spellScreen.GetComponent<BubbleBuffSpellbookController>() == null) {
                    Main.Verbose("Creating new controller");
                    SpellbookController = spellScreen.AddComponent<BubbleBuffSpellbookController>();
                    SpellbookController.CreateBuffstate();
                }

                Main.Verbose("loading sprites");
                if (applyBuffsSprites == null)
                    applyBuffsSprites = ButtonSprites.Load("apply_buffs", new Vector2Int(95, 95));
                if (applyBuffsShortSprites == null)
                    applyBuffsShortSprites = ButtonSprites.Load("apply_buffs_short", new Vector2Int(95, 95));
                if (applyBuffsImportantSprites == null)
                    applyBuffsImportantSprites = ButtonSprites.Load("apply_buffs_important", new Vector2Int(95, 95));

                var staticRoot = Game.Instance.UI.Canvas.transform;
                Main.Verbose("got static root");
                hudLayout = staticRoot.Find("HUDLayout/").gameObject;
                Main.Verbose("got hud layout");

                Main.Verbose("Removing old bubble root");
                var oldBubble = hudLayout.transform.parent.Find("BUBBLEMODS_ROOT");
                if (oldBubble != null) {
                    GameObject.Destroy(oldBubble.gameObject);
                }

                bubbleHud = GameObject.Instantiate(hudLayout, hudLayout.transform.parent);
                Main.Verbose("instantiated root");
                bubbleHud.name = "BUBBLEMODS_ROOT";
                var rect = bubbleHud.transform as RectTransform;
                rect.anchoredPosition = new Vector2(0, 96);
                rect.SetSiblingIndex(hudLayout.transform.GetSiblingIndex() + 1);
                Main.Verbose("set sibling index");

                bubbleHud.DestroyComponents<HUDLayout>();
                bubbleHud.DestroyComponents<UISectionHUDController>();

                GameObject.Destroy(rect.Find("CombatLog_New").gameObject);
                GameObject.Destroy(rect.Find("Console_InitiativeTrackerHorizontalPC").gameObject);
                GameObject.Destroy(rect.Find("IngameMenuView/CompassPart").gameObject);

                bubbleHud.ChildObject("IngameMenuView").DestroyComponents<IngameMenuPCView>();

                Main.Verbose("destroyed old stuff");

                var buttonPanelRect = rect.Find("IngameMenuView/ButtonsPart");
                Main.Verbose("got button panel");
                GameObject.Destroy(buttonPanelRect.Find("TBMMultiButton").gameObject);
                GameObject.Destroy(buttonPanelRect.Find("InventoryButton").gameObject);
                GameObject.Destroy(buttonPanelRect.Find("Background").gameObject);

                Main.Verbose("destroyed more old stuff");

                buttonsContainer = buttonPanelRect.Find("Container").gameObject;
                var buttonsRect = buttonsContainer.transform as RectTransform;
                buttonsRect.anchoredPosition = Vector2.zero;
                buttonsRect.sizeDelta = new Vector2(47.7f * 8, buttonsRect.sizeDelta.y);
                Main.Verbose("set buttons rect");

                buttonsContainer.GetComponent<GridLayoutGroup>().startCorner = GridLayoutGroup.Corner.LowerLeft;

                var prefab = buttonsContainer.transform.GetChild(0).gameObject;
                prefab.SetActive(false);

                int toRemove = buttonsContainer.transform.childCount;

                //Loop from 1 and destroy child[1] since we want to keep child[0] as our prefab, which is super hacky but.
                for (int i = 1; i < toRemove; i++) {
                    GameObject.DestroyImmediate(buttonsContainer.transform.GetChild(1).gameObject);
                }

                void AddButton(string text, string tooltip, ButtonSprites sprites, Action act) {
                    var applyBuffsButton = GameObject.Instantiate(prefab, buttonsContainer.transform);
                    applyBuffsButton.SetActive(true);
                    OwlcatButton button = applyBuffsButton.GetComponentInChildren<OwlcatButton>();
                    button.m_CommonLayer[0].SpriteState = new SpriteState {
                        pressedSprite = sprites.down,
                        highlightedSprite = sprites.hover,
                    };
                    button.OnLeftClick.AddListener(() => {
                        act();
                    });
                    button.SetTooltip(new TooltipTemplateSimple(text, tooltip), new TooltipConfig {
                        InfoCallMethod = InfoCallMethod.None
                    });

                    Buttons.Add(button);

                    applyBuffsButton.GetComponentInChildren<Image>().sprite = sprites.normal;

                }

                AddButton("group.normal.tooltip.header".i8(), "group.normal.tooltip.desc".i8(), applyBuffsSprites, () => GlobalBubbleBuffer.Execute(BuffGroup.Long));
                AddButton("group.important.tooltip.header".i8(), "group.important.tooltip.desc".i8(), applyBuffsImportantSprites, () => GlobalBubbleBuffer.Execute(BuffGroup.Important));
                AddButton("group.short.tooltip.header".i8(), "group.short.tooltip.desc".i8(), applyBuffsShortSprites, () => GlobalBubbleBuffer.Execute(BuffGroup.Short));

                Main.Verbose("remove old bubble?");
#if debug
                RemoveOldController<SyncBubbleHud>(hudLayout.ChildObject("IngameMenuView"));
#endif
                if (hudLayout.ChildObject("IngameMenuView").GetComponent<SyncBubbleHud>() == null) {
                    hudLayout.ChildObject("IngameMenuView").AddComponent<SyncBubbleHud>();
                    Main.Verbose("installed hud sync");
                }



                Main.Verbose("Finished early ui setup");
            } catch (Exception ex) {
                Main.Error(ex, "installing");
            }
        }

#if DEBUG
        private static void RemoveOldController<T>(GameObject on) {
            List<Component> toDelete = new();

            foreach (var component in on.GetComponents<Component>()) {
                Main.Verbose($"checking: {component.name}", "remove-old");
                if (component.GetType().FullName == typeof(T).FullName && component.GetType() != typeof(T)) {
                    var method = component.GetType().GetMethod("Destroy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    method.Invoke(component, new object[] { });
                    toDelete.Add(component);
                }
                Main.Verbose($"checked: {component.name}", "remove-old");
            }

            int count = toDelete.Count;
            for (int i = 0; i < count; i++) {
                GameObject.Destroy(toDelete[0]);
            }

        }

        private static void RemoveOldController(GameObject spellScreen) {
            RemoveOldController<BubbleBuffSpellbookController>(spellScreen);
            RemoveOldController<BubbleBuffGlobalController>(spellScreen.transform.root.gameObject);
        }
#endif

        internal void SetButtonState(bool v) {
            buttonsContainer?.SetActive(v);
        }

        public static List<UnitEntityData> Group => Game.Instance.SelectionCharacter.ActualGroup;

        public static GlobalBubbleBuffer Instance;
        private static ServiceWindowWatcher UiEventSubscriber;
        private static SpellbookWatcher SpellMemorizeHandler;
        private static HideBubbleButtonsWatcher ButtonHiderHandler;

        public static void Install() {

            Instance = new();
            UiEventSubscriber = new();
            SpellMemorizeHandler = new();
            ButtonHiderHandler = new();
            EventBus.Subscribe(Instance);
            EventBus.Subscribe(UiEventSubscriber);
            EventBus.Subscribe(SpellMemorizeHandler);
            EventBus.Subscribe(ButtonHiderHandler);

        }

        public static void Execute(BuffGroup group) {
            Instance.SpellbookController.Execute(group);
        }


        public static void Uninstall() {
            EventBus.Unsubscribe(Instance);
            EventBus.Unsubscribe(UiEventSubscriber);
            EventBus.Unsubscribe(SpellMemorizeHandler);
            EventBus.Unsubscribe(ButtonHiderHandler);
        }
    }


    [Flags]
    public enum HideReason {
        Short = 1,
        Blacklisted = 2,
    };


    public class CasterKey {
        [JsonProperty]
        public string Name;
        [JsonProperty]
        public Guid Spellbook;

        public override bool Equals(object obj) {
            return obj is CasterKey key &&
                   Name == key.Name &&
                   Spellbook.Equals(key.Spellbook);
        }

        public override int GetHashCode() {
            int hashCode = -1362747006;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + Spellbook.GetHashCode();
            return hashCode;
        }
    }
     public enum Category {
        Spell,
        Ability,
        Item,
        Consumable,
    }

    public enum BuffGroup {
        Long,
        Short,
        Important,
    }


    class ButtonSprites {
        public Sprite normal;
        public Sprite hover;
        public Sprite down;

        public static ButtonSprites Load(string name, Vector2Int size) {
            return new ButtonSprites {
                normal = AssetLoader.LoadInternal("icons", $"{name}_normal.png", size),
                hover = AssetLoader.LoadInternal("icons", $"{name}_hover.png", size),
                down = AssetLoader.LoadInternal("icons", $"{name}_down.png", size),
            };
        }
    }


    public static class ReactiveBindings {

        public static void BindToView(this IReactiveProperty<bool> prop, GameObject toggle) {
            var view = toggle.GetComponentInChildren<ToggleWorkaround>();
            prop.Subscribe<bool>(val => {
                if (view.isOn != val) {
                    view.isOn = val;
                }
            });

            view.onValueChanged.AddListener(val => {
                if (prop.Value != val)
                    prop.Value = val;
            });

        }

    }
    class Portrait {
        public Image Image;
        public OwlcatButton Button;
        public GameObject GameObject;
        public TextMeshProUGUI Text;
        public OwlcatButton Expand;
        public Image Overlay;
        public Image FullOverlay;

        public void ExpandOff() {
            Expand.IsSelected = false;
        }

        internal void SetExpanded(bool selected) {
            Expand.IsSelected = selected;
            Expand.gameObject.ChildRect("Image").eulerAngles = new Vector3(0, 0, Expand.IsSelected ? 90 : -90);
        }

        public RectTransform Transform { get { return GameObject.transform as RectTransform; } }
    }

    class BubbleCleanup : IDisposable {

        private readonly List<IDisposable> Trash = new();
        public void AddTrash(IDisposable trash) {
            Trash.Add(trash);
        }

        public void Dispose() {
            foreach (var trash in Trash)
                trash.Dispose();
        }

    }

    class BubbleSpellView {
        private static TooltipConfig NoInfo = new TooltipConfig { InfoCallMethod = InfoCallMethod.None };
        public static void BindBuffToView(BubbleBuff buff, GameObject view, bool tooltipOnRightClickOnly = false) {
            var button = view.GetComponent<OwlcatButton>();
            string text = buff.Name;
            //if (buff.Spell.Blueprint.LocalizedDuration.TryGetString(out var duration)) {
            //    text += $"\n<size=70%>{duration}</size>";
            //}
            view.ChildObject("Name/NameLabel").GetComponent<TextMeshProUGUI>().text = text;

            view.ChildObject("Icon/IconImage").GetComponent<Image>().sprite = buff.Spell.Blueprint.Icon;
            view.ChildObject("Icon/IconImage").GetComponent<Image>().color = buff.Key.Archmage ? Color.yellow : Color.white;
            view.ChildObject("Icon/FrameImage").GetComponent<Image>().color = buff.Key.Archmage ? Color.yellow : Color.white;


            if (buff.Spell.Blueprint.School != Kingmaker.Blueprints.Classes.Spells.SpellSchool.None)
                view.ChildObject("School/SchoolLabel").GetComponent<TextMeshProUGUI>().text = buff.Spell.Blueprint.School.ToString();
            else
                view.ChildObject("School/SchoolLabel").GetComponent<TextMeshProUGUI>().text = "";
            var metamagicContainer = view.ChildObject("Metamagic");
            if (buff.Spell.IsMetamagicked()) {
                for (int i = 0; i < metamagicContainer.transform.childCount; i++) {
                    var icon = metamagicContainer.transform.GetChild(i).gameObject;
                    if (i < buff.Metamagics.Length) {
                        icon.SetActive(true);
                        icon.GetComponent<Image>().sprite = buff.Metamagics[i].SpellIcon();
                    } else
                        icon.SetActive(false);
                }
                metamagicContainer.SetActive(true);
            } else
                metamagicContainer.SetActive(false);

            var tooltip = BubbleBuffSpellbookController.TooltipForAbility(buff.Spell.Blueprint);
            
            //button.OnRightClick.RemoveAllListeners();
            //button.OnRightClick.AddListener(() => {
            //    TooltipHelper.ShowInfo(tooltip);
            //});
            TooltipHelper.SetTooltip(button, tooltip);

        }
    }

    class BubbleProfiler : IDisposable {

        private readonly Stopwatch watch = new();
        private readonly string name;


        public BubbleProfiler(string name) {
            this.name = name;
            watch.Start();
        }
        public void Dispose() {
            watch.Stop();
            Main.Verbose($">>> {name} => {watch.Elapsed.TotalSeconds}s");
        }
    }

    class WidgetCache {
        public int Hits;
        public int Misses;
        private GameObject prefab;
        private readonly List<GameObject> cache = new();

        public Func<GameObject> PrefabGenerator;

        public void ResetStats() {
            Hits = 0;
            Misses = 0;
        }

        public WidgetCache() { }

        public GameObject Get(Transform parent) {
            if (prefab == null) {
                prefab = PrefabGenerator.Invoke();
                if (prefab == null)
                    throw new Exception("null prefab in widget cache");
            }
            GameObject ret;
            if (cache.Empty()) {
                ret = GameObject.Instantiate(prefab, parent);
                Misses++;
            } else {
                Hits++;
                ret = cache.Last();
                ret.transform.SetParent(parent);
                cache.RemoveLast();
            }
            ret.SetActive(true);
            return ret;
        }

        public void Return(IEnumerable<GameObject> widgets) {
            //cache.AddRange(widgets);
        }

    }

    class BufferView {
        public Dictionary<BuffKey, GameObject> buffWidgets = new();

        public GameObject buffWindow;
        public GameObject removeFromAll;
        public GameObject addToAll;
        public Portrait[] targets;
        private BufferState state;
        public Portrait[] casterPortraits;

        public GameObject listPrefab;
        public Transform content;

        public WidgetCache widgetCache;

        public BufferView(BufferState state) {
            this.state = state;
            state.OnRecalculated = Update;
        }

        private static GameObject BigLabelPrefab => UIHelpers.CharacterScreen.Find("NamePortrait/CharName/CharacterName").gameObject;

        public void ReorderTargetPortraits() {
            var group = Bubble.Group;
            for (int i = 0; i < group.Count; i++) {
                targets[i].Image.sprite = group[i].Portrait.SmallPortrait;
            }
        }

        public void MakeBuffsList() {
            Main.Verbose("here");
            if (!state.Dirty)
                return;
            state.Dirty = false;
            Main.Verbose("state was dirty");

            widgetCache.Return(buffWidgets.Values);
            Main.Verbose("returned widget cache");
            GameObject.Destroy(content.Find("AvailableBuffList")?.gameObject);
            Main.Verbose("destroyed old buff list");
            buffWidgets.Clear();
            Main.Verbose("cleared widgets");

            var availableBuffs = GameObject.Instantiate(listPrefab.gameObject, content);
            availableBuffs.transform.SetAsFirstSibling();
            Main.Verbose("made new buff list");
            availableBuffs.name = "AvailableBuffList";
            availableBuffs.GetComponentInChildren<GridLayoutGroupWorkaround>().constraintCount = 5;
            Main.Verbose("set constraint count");
            var listRect = availableBuffs.transform as RectTransform;
            listRect.localPosition = Vector2.zero;
            listRect.sizeDelta = Vector2.zero;
            listRect.anchorMin = new Vector2(0.125f, 0.47f);
            listRect.anchorMax = new Vector2(0.875f, 0.87f);
            GameObject.Destroy(listRect.Find("Toggle")?.gameObject);
            GameObject.Destroy(listRect.Find("TogglePossibleSpells")?.gameObject);
            GameObject.Destroy(listRect.Find("ToggleAllSpells")?.gameObject);
            GameObject.Destroy(listRect.Find("ToggleMetamagic")?.gameObject);
            var scrollContent = availableBuffs.transform.Find("StandardScrollView/Viewport/Content");
            scrollContent.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            Main.Verbose("got scroll content");
            Main.Verbose($"destroying old stuff: {scrollContent.childCount}");
            int toDestroy = scrollContent.childCount;
            for (int i = 0; i < toDestroy; i++) {
                GameObject.DestroyImmediate(scrollContent.GetChild(0).gameObject);
            }

            Main.Verbose($"destroyed old stuff: {scrollContent.childCount}");
            //widgetListDrawHandle = buffWidgetList.DrawEntries<IWidgetView>(models, new List<IWidgetView> { spellPrefab });

            Color goodSpellColor = new Color(0.2f, 0.7f, 0.2f);

            OwlcatButton previousSelection = null;
            widgetCache.ResetStats();
            using (new BubbleProfiler("making widgets")) {
                foreach (var buff in state.BuffList) {
                    GameObject widget = widgetCache.Get(scrollContent);
                    var button = widget.GetComponent<OwlcatButton>();
                    button.OnHover.RemoveAllListeners();
                    button.OnHover.AddListener(hover => {
                        PreviewReceivers(hover ? buff : null);
                    });
                    button.OnSingleLeftClick.AddListener(() => {
                        if (previousSelection != null && previousSelection != button) {
                            previousSelection.SetSelected(false);
                        }
                        if (!button.IsSelected) {
                            button.SetSelected(true);
                        }
                        currentSelectedSpell.Value = buff;
                        previousSelection = button;
                    });
                    var label = widget.ChildObject("School/SchoolLabel").GetComponent<TextMeshProUGUI>();
                    var textImage = widget.ChildObject("Name/BackgroundName").GetComponent<Image>();
                    buff.OnUpdate = () => {
                        if (widget == null)
                            return;
                        var (availNormal, availSelf) = buff.AvailableAndSelfOnly;
                        if (availNormal < 100)
                            label.text = $"{"casting".i8()}: {buff.Fulfilled}/{buff.Requested} + {"available".i8()}: {availNormal}+{availSelf}";
                        else
                            label.text = $"{"casting".i8()}: {buff.Fulfilled}/{buff.Requested} + {"available".i8()}: {"available.atwill".i8()}";
                        if (buff.Requested > 0) {
                            if (buff.Fulfilled != buff.Requested) {
                                textImage.color = Color.red;
                            } else {
                                textImage.color = goodSpellColor;
                            }

                        } else {
                            textImage.color = Color.white;
                        }
                    };
                    BubbleSpellView.BindBuffToView(buff, widget, button);
                    widget.ChildObject("School").SetActive(true);
                    widget.SetActive(true);

                    buffWidgets[buff.Key] = widget;
                }
            }

            Main.Verbose($"Widget cache: created={widgetCache.Hits + widgetCache.Misses}");

            foreach (var buff in state.BuffList) {
                buff.OnUpdate();
            }
        }

        public void Update() {
            if (state.Dirty) {
                try {
                    MakeBuffsList();
                    ReorderTargetPortraits();
                } catch (Exception ex) {
                    Main.Error(ex, "revalidating dirty");
                }
            }

            foreach (BuffGroup group in Enum.GetValues(typeof(BuffGroup))) {
                try {
                    if (groupSummaryLabels.TryGetValue(group, out var label)) {
                        var list = state.BuffList.Where(b => b.InGroup == group)
                                                                   .Select(b => (b.Requested, b.Fulfilled));
                        if (!list.Empty()) {
                            var (requested, fulfilled) = list.Aggregate((a, b) => (a.Requested + b.Requested, a.Fulfilled + b.Fulfilled));
                            label.text = MakeSummaryLabel(group, requested, fulfilled);
                        } else {
                            label.text = MakeSummaryLabel(group, 0, 0);
                        }
                    }
                } catch (Exception e) {
                    Main.Error(e, "");
                }
            }

            if (currentSelectedSpell.Value == null)
                return;

            PreviewReceivers(null);
            UpdateCasterDetails(Selected);
            OnUpdate?.Invoke();
        }

        private string MakeSummaryLabel(BuffGroup group, int requested, int fulfilled) {
             return $"{group.i8().MakeTitle()}\n{fulfilled}/{requested}";
        }


        public Action OnUpdate;

        Color massGoodColor = new Color(0, 1, 0, 0.4f);
        Color massBadColor = new Color(1, 1, 0, 0.4f);

        public void PreviewReceivers(BubbleBuff buff) {
            if (buff == null && currentSelectedSpell.Value != null)
                buff = Selected;

            for (int p = 0; p < Bubble.Group.Count; p++)
                UpdateTargetBuffColor(buff, p);
        }

        private void UpdateTargetBuffColor(BubbleBuff buff, int i) {
            var fullOverlay = targets[i].FullOverlay;
            targets[i].Button.Interactable = true;
            if (buff == null) {
                fullOverlay.gameObject.SetActive(false);
                return;
            }
            bool isMass = false;
            bool massGood = false;

            if (buff.IsMass && buff.Requested > 0) {
                isMass = true;
                if (buff.Fulfilled > 0)
                    massGood = true;
            }

            var me = Bubble.Group[i];


            if (isMass && !buff.UnitWants(me)) {
                var target = massGood ? massGoodColor : massBadColor;
                targets[i].Overlay.gameObject.SetActive(true);
                var current = targets[i].Overlay.color;
                targets[i].Overlay.color = new Color(target.r, target.g, target.b, current.a);
            } else {
                targets[i].Overlay.gameObject.SetActive(false);
            }

            fullOverlay.gameObject.SetActive(true);

            if (!buff.CanTarget(me)) {
                fullOverlay.color = Color.red;
                targets[i].Button.Interactable = false;

            } else if (buff.UnitWants(me)) {
                if (buff.UnitGiven(me)) {
                    fullOverlay.color = Color.green;
                } else {
                    fullOverlay.color = Color.yellow;
                }
            } else {
                fullOverlay.color = Color.gray;
            }
        }

        private void UpdateCasterDetails(BubbleBuff buff) {
            for (int i = 0; i < casterPortraits.Length; i++) {
                casterPortraits[i].GameObject.SetActive(i < buff.CasterQueue.Count);
                if (i < buff.CasterQueue.Count) {
                    var who = buff.CasterQueue[i];
                    casterPortraits[i].Image.sprite = targets[who.CharacterIndex].Image.sprite;
                    var bookName = who.book?.Blueprint.Name ?? "";
                    if (who.AvailableCredits < 100)
                        casterPortraits[i].Text.text = $"{who.spent}+{who.AvailableCredits}\n<i>{bookName}</i>";
                    else
                        casterPortraits[i].Text.text = $"{"available.atwill".i8()}\n<i>{bookName}</i>";
                    casterPortraits[i].Text.fontSize = 12;
                    casterPortraits[i].Text.outlineWidth = 0;
                    casterPortraits[i].Image.color = who.Banned ? Color.red : Color.white;
                }
            }
            addToAll.GetComponentInChildren<OwlcatButton>().Interactable = buff.Requested != Bubble.Group.Count;
            removeFromAll.GetComponentInChildren<OwlcatButton>().Interactable = buff.Requested > 0;
        }

        public IReactiveProperty<BubbleBuff> currentSelectedSpell = new ReactiveProperty<BubbleBuff>();

        public bool Get(out BubbleBuff buff) {
            buff = currentSelectedSpell.Value;
            if (currentSelectedSpell.Value == null)
                return false;
            return true;
        }

        private Dictionary<BuffGroup, TextMeshProUGUI> groupSummaryLabels = new();

        internal void MakeSummary() {
            groupSummaryLabels.Clear();
            var rect = new GameObject("summary", typeof(RectTransform));
            rect.AddTo(content);
            rect.Rect().sizeDelta = Vector3.zero;

            rect.MakeComponent<GridLayoutGroupWorkaround>(h => {
                h.constraint = GridLayoutGroup.Constraint.FixedRowCount;
                h.constraintCount = 1;
                h.childAlignment = TextAnchor.MiddleCenter;
                h.cellSize = new Vector2(400, 100);
            });

            foreach (BuffGroup group in Enum.GetValues(typeof(BuffGroup))) {
                var l = GameObject.Instantiate(BigLabelPrefab, rect.transform);
                var label = l.GetComponent<TextMeshProUGUI>();
                label.text = MakeSummaryLabel(group, 0, 0);
                l.SetActive(true);
                groupSummaryLabels[group] = label;
            }
            rect.Rect().SetAnchor(0.15, 0.85, 0.88, 0.93);

        }

        public BubbleBuff Selected {
            get {
                if (currentSelectedSpell == null)
                    return null;
                return currentSelectedSpell.Value;
            }
        }
    }

    static class Bubble {
        public static List<UnitEntityData> Group => Game.Instance.SelectionCharacter.ActualGroup;
        public static Dictionary<string, UnitEntityData> GroupById = new();
    }


    internal class SpellbookWatcher : ISpellBookUIHandler, IAreaHandler, ILevelUpCompleteUIHandler, IPartyChangedUIHandler, ISpellBookCustomSpell {
        public static void Safely(Action a) {
            try {
                a.Invoke();
            } catch (Exception ex) {
                Main.Error(ex, "");
            }
        }
        public void HandleForgetSpell(AbilityData data, UnitDescriptor owner) {
            Safely(() => GlobalBubbleBuffer.Instance.SpellbookController?.RevalidateSpells());
        }

        public void HandleLevelUpComplete(UnitEntityData unit, bool isChargen) {
            Safely(() => GlobalBubbleBuffer.Instance.SpellbookController?.RevalidateSpells());
        }

        public void HandleMemorizedSpell(AbilityData data, UnitDescriptor owner) {
            Safely(() => GlobalBubbleBuffer.Instance.SpellbookController?.RevalidateSpells());
        }

        public void HandlePartyChanged() {
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Main.Log("HERE");
            Safely(() => GlobalBubbleBuffer.Instance.SpellbookController?.RevalidateSpells());
        }

        public void OnAreaDidLoad() {
            Main.Verbose("Loaded area...");
            GlobalBubbleBuffer.Instance.TryInstallUI();
            AbilityCache.Revalidate();
        }

        public void OnAreaBeginUnloading() { }

        void ISpellBookCustomSpell.AddSpellHandler(AbilityData ability) {
            Safely(() => GlobalBubbleBuffer.Instance.SpellbookController?.RevalidateSpells());
        }

        void ISpellBookCustomSpell.RemoveSpellHandler(AbilityData ability) {
            Safely(() => GlobalBubbleBuffer.Instance.SpellbookController?.RevalidateSpells());
        }
    }
    internal class HideBubbleButtonsWatcher : ICutsceneHandler, IPartyCombatHandler {
        public void HandleCutscenePaused(CutscenePlayerData cutscene, CutscenePauseReason reason) { }

        public void HandleCutsceneRestarted(CutscenePlayerData cutscene) { }

        public void HandleCutsceneResumed(CutscenePlayerData cutscene) { }

        public void HandleCutsceneStarted(CutscenePlayerData cutscene, bool queued) { }

        public void HandleCutsceneStopped(CutscenePlayerData cutscene) { }

        public void HandlePartyCombatStateChanged(bool inCombat) {
            bool allow = !inCombat || GlobalBubbleBuffer.Instance.SpellbookController.state.AllowInCombat;
            GlobalBubbleBuffer.Instance.Buttons.ForEach(b => b.Interactable = allow);
        }
    }

    static class BubbleBlueprints {
        public static BlueprintAbility ShareTransmutation => Resources.GetBlueprint<BlueprintAbility>("749567e4f652852469316f787921e156");
        public static BlueprintAbility PowerfulChange => Resources.GetBlueprint<BlueprintAbility>("a45f3dae9c64ec848b35f85568f4b220");
    }
}
