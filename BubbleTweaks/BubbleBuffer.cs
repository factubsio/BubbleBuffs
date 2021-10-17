using BubbleBuffs.Config;
using BubbleBuffs.Utilities;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Controllers;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.Common.Animations;
using Kingmaker.UI.Log.CombatLog_ThreadSystem;
using Kingmaker.UI.Log.CombatLog_ThreadSystem.LogThreads.Common;
using Kingmaker.UI.MVVM._PCView.Party;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Spellbook.KnownSpells;
using Kingmaker.UI.MVVM._VM.ServiceWindows;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.KnownSpells;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.MemorizingPanel;
using Kingmaker.UI.MVVM._VM.Tooltip.Bricks;
using Kingmaker.UI.MVVM._VM.Tooltip.Templates;
using Kingmaker.UI.MVVM._VM.Tooltip.Utils;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Utility;
using Newtonsoft.Json;
using Owlcat.Runtime.UI.Controls.Button;
using Owlcat.Runtime.UI.Controls.Other;
using Owlcat.Runtime.UI.MVVM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleBuffs {

    static class CanvasFinder {
        public static Transform StaticRoot => Game.Instance.UI.Canvas.transform;
        public static Transform ServiceWindow => StaticRoot.Find("ServiceWindowsPCView");
        public static Transform SpellbookScreen => ServiceWindow.Find("SpellbookView/SpellbookScreen");
    }

    public class BubbleBuffSpellbookController : MonoBehaviour {
        private GameObject ToggleButton;
        private bool Buffing => PartyView.m_Hide;
        private GameObject MainContainer;
        private GameObject NoSpellbooksContainer;

        private bool WasMainShown = false;

        private PartyPCView PartyView;
        private SavedBufferState save;
        private BufferState state;
        private BufferView view;

        private GameObject Root;

        private bool WindowCreated = false;

        public static string SettingsPath => $"{ModSettings.ModEntry.Path}UserSettings/bubblebuff.json";

        public void CreateBuffstate() {
            if (File.Exists(SettingsPath)) {
                using (var settingsReader = File.OpenText(SettingsPath))
                using (var jsonReader = new JsonTextReader(settingsReader)) {
                    save = JsonSerializer.CreateDefault().Deserialize<SavedBufferState>(jsonReader);
                }
            } else {
                save = new SavedBufferState();
            }

            state = new BufferState(save);
            view = new BufferView(state);

            Main.Log("Getting buff list");
            state.RefreshBuffsList(Group);
        }

        private static void FadeOut(GameObject obj) {
            obj.GetComponent<FadeAnimator>().DisappearAnimation();
        }
        private static void FadeIn(GameObject obj) {
            obj.GetComponent<FadeAnimator>().AppearAnimation();
        }


        private void Awake() {
            MainContainer = transform.Find("MainContainer").gameObject;
            NoSpellbooksContainer = transform.Find("NoSpellbooksContainer").gameObject;

            PartyView = CanvasFinder.StaticRoot.Find("PartyPCView").gameObject.GetComponent<PartyPCView>();

            GameObject.Destroy(transform.Find("bubblebuff-toggle")?.gameObject);
            GameObject.Destroy(transform.Find("bubblebuff-root")?.gameObject);

            ToggleButton = GameObject.Instantiate(transform.Find("MainContainer/MetamagicButton").gameObject, transform);
            (ToggleButton.transform as RectTransform).anchoredPosition = new Vector2(1400, 0);
            ToggleButton.name = "bubblebuff-toggle";
            ToggleButton.GetComponentInChildren<TextMeshProUGUI>().text = "Buff Setup";

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
            var group = Root.AddComponent<CanvasGroup>();
            var fader = Root.AddComponent<FadeAnimator>();
        }

        internal void Hide() {
            FadeOut(Root);
            Root.SetActive(false);
        }

        private static GameObject MakeToggle(GameObject togglePrefab, Transform parent, float x, float y, string text, string name) {
            var toggle = GameObject.Instantiate(togglePrefab, parent);
            toggle.name = name;
            var blacklistRect = toggle.transform as RectTransform;
            blacklistRect.localPosition = Vector2.zero;
            blacklistRect.anchoredPosition = Vector2.zero;
            blacklistRect.anchorMin = new Vector2(x, y);
            blacklistRect.anchorMax = new Vector2(x, y);
            blacklistRect.pivot = new Vector2(0.5f, 0.5f);
            toggle.GetComponentInChildren<TextMeshProUGUI>().text = text;
            return toggle;
        }

        private static Portrait MakePortrait(GameObject portraitPrefab, RectTransform groupRect, bool destroyHealth) {
            var portrait = GameObject.Instantiate(portraitPrefab, groupRect);
            var handle = new Portrait {
                GameObject = portrait
            };
            GameObject.Destroy(portrait.transform.Find("Health").gameObject);
            GameObject.Destroy(portrait.transform.Find("PartBuffView").gameObject);
            GameObject.Destroy(portrait.transform.Find("BuffMain").gameObject);
            GameObject.Destroy(portrait.transform.Find("EncumbranceIndicator").gameObject);
            GameObject.Destroy(portrait.transform.Find("Levelups").gameObject);
            GameObject.Destroy(portrait.transform.Find("Bark").gameObject);

            if (destroyHealth)
                GameObject.Destroy(portrait.transform.Find("HitPoint").gameObject);
            else
                handle.Text = portrait.transform.Find("HitPoint").GetComponentInChildren<TextMeshProUGUI>();

            GameObject.Destroy(portrait.transform.Find("Portrait").GetComponent<UnitPortraitPartView>());
            GameObject.Destroy(portrait.GetComponent<PartyCharacterPCView>());

            var portraitRect = portrait.transform.Find("Portrait") as RectTransform;
            var frameRect = portrait.transform.Find("Frame") as RectTransform;
            portraitRect.anchorMin = frameRect.anchorMin;
            portraitRect.anchorMax = frameRect.anchorMax;
            portraitRect.anchoredPosition = frameRect.anchoredPosition;
            portraitRect.sizeDelta = frameRect.sizeDelta;
            portrait.transform.localPosition = Vector3.zero;

            handle.Image = portraitRect.Find("LifePortrait").gameObject.GetComponent<Image>();
            handle.Button = frameRect.gameObject.GetComponent<OwlcatMultiButton>();

            return handle;
        }

        public ReactiveProperty<bool> ShowOnlyRequested = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> ShowShort = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> ShowHidden = new ReactiveProperty<bool>(false);
        public ReactiveProperty<string> NameFilter = new ReactiveProperty<string>("");

        private List<UnitEntityData> Group => Game.Instance.SelectionCharacter.ActualGroup;

        private void CreateWindow() {
            var staticRoot = CanvasFinder.StaticRoot;

            var portraitPrefab = staticRoot.Find("PartyPCView/PartyCharacterView_01").gameObject;
            var listPrefab = staticRoot.Find("ServiceWindowsPCView/SpellbookView/SpellbookScreen/MainContainer/KnownSpells");
            var spellPrefab = listPrefab.GetComponent<SpellbookKnownSpellsPCView>().m_KnownSpellView;
            var framePrefab = staticRoot.Find("ServiceWindowsPCView/MythicInfoView/Window/MainContainer/MythicInfoProgressionView/Progression/Frame").gameObject;
            var togglePrefab = staticRoot.Find("ServiceWindowsPCView/SpellbookView/SpellbookScreen/MainContainer/KnownSpells/Toggle").gameObject;
            var buttonPrefab = staticRoot.Find("ServiceWindowsPCView/SpellbookView/SpellbookScreen/MainContainer/MetamagicContainer/Metamagic/Button").gameObject;
            var nextPrefab = staticRoot.Find("PartyPCView/Background/Next").gameObject;
            var prevPrefab = staticRoot.Find("PartyPCView/Background/Prev").gameObject;

            var content = Root.transform;

            view.spellPrefab = spellPrefab;
            view.listPrefab = listPrefab.gameObject;
            view.content = content;

            MakeAddRemoveAllButtons(buttonPrefab, content);

            MakeGroupHolder(portraitPrefab, content);

            MakeDetailsView(portraitPrefab, spellPrefab, framePrefab, nextPrefab, prevPrefab, togglePrefab, content);

            view.MakeBuffsList();

            MakeFilters(togglePrefab,content);


            ShowHidden.Subscribe<bool>(show => {
                RefreshFiltering();
            });
            ShowOnlyRequested.Subscribe<bool>(show => {
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
                    if (view.currentSelectedSpell.HasValue && view.currentSelectedSpell.Value != null) {
                        //toggleBlacklist.SetActive(true);
                        currentSpellView.gameObject.SetActive(true);
                        currentSpellView.Bind(view.currentSelectedSpell.Value);

                        var buff = view.Selected;

                        if (buff == null) {
                            return;
                        }

                        view.addToAll.SetActive(true);
                        view.removeFromAll.SetActive(true);

                        view.Update();

                        //toggleBlacklist.GetComponentInChildren<ToggleWorkaround>().isOn = buff.Hidden;

                    } else {
                        currentSpellView.gameObject.SetActive(false);
                        //toggleBlacklist.SetActive(false);

                        view.addToAll.SetActive(false);
                        view.removeFromAll.SetActive(false);

                        foreach (var caster in view.casterPortraits)
                            caster.GameObject.SetActive(false);

                        foreach (var portrait in view.targets) {
                            portrait.Image.color = Color.white;
                            portrait.Button.Interactable = true;
                        }
                    }
                } catch (Exception e) {
                    Main.Error(e, "SELECTING SPELL");
                }
            });
            WindowCreated = true;
        }

        private void MakeFilters(GameObject togglePrefab, Transform content) {

            var filterToggles = new GameObject("filters", typeof(RectTransform));
            filterToggles.AddComponent<VerticalLayoutGroup>().childForceExpandHeight = false;
            //filterToggles.AddComponent<Image>().color = Color.green;
            var filterRect = filterToggles.transform as RectTransform;
            filterRect.SetParent(content);
            filterRect.anchoredPosition3D = Vector3.zero;
            filterRect.anchorMin = new Vector2(0.13f, 0.1f);
            filterRect.anchorMax = new Vector2(0.23f, .455f);

            search = new SearchBar(filterRect, "...", false, "bubble-search-buff");
            GameObject showHidden = MakeToggle(togglePrefab, filterRect, 0.8f, .5f, "Show Hidden", "bubble-toggle-show-hidden");
            GameObject showShort = MakeToggle(togglePrefab, filterRect, .8f, .5f, "Show Short", "bubble-toggle-show-short");
            GameObject showOnlyRequested = MakeToggle(togglePrefab, filterRect, .8f, .5f, "Only Requested", "bubble-toggle-show-requested");

            search.InputField.onValueChanged.AddListener(val => {
                NameFilter.Value = val;
            });


            ShowShort.BindToView(showShort);
            ShowHidden.BindToView(showHidden);
            ShowOnlyRequested.BindToView(showOnlyRequested);

        }

        private void RefreshFiltering() {

            foreach (var buff in state.metadata) {
                if (view.buffWidgets[buff.Index] == null) {
                    continue;
                }

                var widget = view.buffWidgets[buff.Index];

                bool show = true;

                if (ShowOnlyRequested.Value && buff.Requested == 0)
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


        private void MakeDetailsView(GameObject portraitPrefab, SpellbookKnownSpellPCView spellPrefab, GameObject framePrefab, GameObject nextPrefab, GameObject prevPrefab, GameObject togglePrefab, Transform content) {
            var detailsHolder = GameObject.Instantiate(framePrefab, content);
            var detailsRect = detailsHolder.GetComponent<RectTransform>();
            GameObject.Destroy(detailsHolder.transform.Find("FrameDecor").gameObject);
            detailsRect.localPosition = Vector2.zero;
            detailsRect.sizeDelta = Vector2.zero;
            detailsRect.anchorMin = new Vector2(0.25f, 0.30f);
            detailsRect.anchorMax = new Vector2(0.75f, 0.51f);

            currentSpellView = GameObject.Instantiate(spellPrefab, detailsRect);

            currentSpellView.ViewModel = view.currentSelectedSpell.Value;
            currentSpellView.GetComponentInChildren<OwlcatButton>().Interactable = false;
            currentSpellView.gameObject.SetActive(false);
            var currentSpellRect = currentSpellView.transform as RectTransform;
            currentSpellRect.anchorMin = new Vector2(.5f, .8f);
            currentSpellRect.anchorMax = new Vector2(.5f, .8f);

            var castersHolder = new GameObject("CastersHolder", typeof(RectTransform));
            var castersRect = castersHolder.GetComponent<RectTransform>();
            castersRect.SetParent(detailsRect);
            castersRect.localPosition = Vector2.zero;
            castersRect.sizeDelta = Vector2.zero;
            castersRect.anchorMin = new Vector2(0.5f, 0.1f);
            castersRect.anchorMax = new Vector2(0.5f, 0.4f);
            castersRect.pivot = new Vector2(0.5f, 0.4f);
            castersRect.anchoredPosition = new Vector2(-(60 * totalCasters) / 2.0f, 0.0f);

            var castersHorizontalGroup = castersHolder.AddComponent<HorizontalLayoutGroup>();
            castersHorizontalGroup.spacing = 60;
            castersHorizontalGroup.childControlHeight = true;
            castersHorizontalGroup.childForceExpandHeight = true;

            //var promoteCaster = GameObject.Instantiate(prevPrefab, detailsRect);
            //var promoteRect = promoteCaster.transform as RectTransform;
            //promoteCaster.SetActive(true);
            //promoteRect.localPosition = Vector2.zero;
            //promoteRect.anchoredPosition = Vector2.zero;
            //promoteRect.anchorMin = new Vector2(0.5f, 0.2f);
            //promoteRect.anchorMax = new Vector2(0.5f, 0.2f);
            //promoteRect.pivot = new Vector2(0.5f, 0.5f);
            //promoteRect.anchoredPosition = new Vector2(castersRect.anchoredPosition.x + 30, 0);

            //var demoteCaster = GameObject.Instantiate(nextPrefab, detailsRect);
            //var demoteRect = demoteCaster.transform as RectTransform;
            //demoteCaster.SetActive(true);
            //demoteRect.localPosition = Vector2.zero;
            //demoteRect.anchoredPosition = Vector2.zero;
            //demoteRect.anchorMin = new Vector2(0.5f, 0.2f);
            //demoteRect.anchorMax = new Vector2(0.5f, 0.2f);
            //demoteRect.pivot = new Vector2(0.5f, 0.5f);
            //demoteRect.anchoredPosition = new Vector2(castersRect.anchoredPosition.x + 60, 0);

            view.casterPortraits = new Portrait[totalCasters];

            for (int i = 0; i < totalCasters; i++) {
                var casterPortrait = MakePortrait(portraitPrefab, castersRect, false);
                view.casterPortraits[i] = casterPortrait;
                var textRoot = casterPortrait.Text.gameObject.transform.parent as RectTransform;
                textRoot.anchoredPosition = new Vector2(0, 75);
                casterPortrait.Text.fontSizeMax = 18;
                casterPortrait.Text.fontSize = 18;
                casterPortrait.Text.color = Color.green;
                casterPortrait.Text.gameObject.transform.parent.gameObject.SetActive(true);
                casterPortrait.Text.text = "12/12";
                var aspect = casterPortrait.GameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                aspect.aspectRatio = 0.75f;
                casterPortrait.Button.m_CommonLayer.RemoveAt(1);
            }

            GameObject toggleBlacklist = MakeToggle(togglePrefab, detailsRect, .2f, .85f, "Hide Spell", "bubble-toggle-spell-blacklist");
            toggleBlacklist.SetActive(false);
        }

        private void MakeAddRemoveAllButtons(GameObject buttonPrefab, Transform content) {
            view.addToAll = GameObject.Instantiate(buttonPrefab, content);
            view.addToAll.GetComponentInChildren<TextMeshProUGUI>().text = "Add To All";
            var addToAllRect = view.addToAll.transform as RectTransform;
            addToAllRect.anchorMin = new Vector2(0.48f, 0.305f);
            addToAllRect.anchorMax = new Vector2(0.48f, 0.31f);
            addToAllRect.pivot = new Vector2(1, 1);
            addToAllRect.localPosition = Vector3.zero;
            addToAllRect.anchoredPosition = Vector2.zero;

            view.removeFromAll = GameObject.Instantiate(buttonPrefab, content);
            view.removeFromAll.GetComponentInChildren<TextMeshProUGUI>().text = "Remove From All";
            var removeFromAllRect = view.removeFromAll.transform as RectTransform;
            removeFromAllRect.anchorMin = new Vector2(0.52f, 0.305f);
            removeFromAllRect.anchorMax = new Vector2(0.52f, 0.31f);
            removeFromAllRect.pivot = new Vector2(0, 1);
            removeFromAllRect.localPosition = Vector3.zero;
            removeFromAllRect.anchoredPosition = Vector2.zero;

            view.addToAll.GetComponentInChildren<OwlcatButton>().OnLeftClick.AddListener(() => {
                var buff = view.Selected;
                if (buff == null) return;

                for (int i = 0; i < Group.Count; i++) {
                    if (view.targets[i].Button.Interactable && !buff.UnitWants(i)) {
                        buff.SetUnitWants(i, true);
                    }
                }
                state.Recalculate(true);

            });
            view.removeFromAll.GetComponentInChildren<OwlcatButton>().OnLeftClick.AddListener(() => {
                var buff = view.Selected;
                if (buff == null) return;

                for (int i = 0; i < Group.Count; i++) {
                    if (buff.UnitWants(i)) {
                        buff.SetUnitWants(i, false);
                    }
                }
                state.Recalculate(true);
            });
        }

        private int totalCasters = 0;
        private SpellbookKnownSpellPCView currentSpellView;
        private SearchBar search;

        private void MakeGroupHolder(GameObject portraitPrefab, Transform content) {
            var groupHolder = new GameObject("GroupHolder", typeof(RectTransform));
            var groupRect = groupHolder.GetComponent<RectTransform>();
            groupRect.SetParent(content);
            groupRect.localPosition = Vector2.zero;
            groupRect.sizeDelta = Vector2.zero;

            float requiredWidthHalf = Group.Count * 0.033f;
            //groupRect.anchoredPosition = new Vector2(-(120 * Group.Count) / 2.0f, 0);

            var horizontalGroup = groupHolder.AddComponent<HorizontalLayoutGroup>();
            horizontalGroup.spacing = 124;
            horizontalGroup.childControlHeight = true;
            horizontalGroup.childForceExpandHeight = true;

            view.targets = new Portrait[Group.Count];

            for (int i = 0; i < Group.Count; i++) {
                Portrait portrait = MakePortrait(portraitPrefab, groupRect, true);

                var aspect = portrait.GameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                aspect.aspectRatio = 0.75f;

                portrait.Image.sprite = Group[i].Portrait.SmallPortrait;


                int personIndex = i;

                portrait.Button.OnLeftClick.AddListener(() => {
                    var buff = view.Selected;
                    if (buff == null)
                        return;

                    if (!buff.CanTarget(personIndex))
                        return;

                    if (buff.UnitWants(personIndex)) {
                        buff.SetUnitWants(personIndex, false);
                    } else {
                        buff.SetUnitWants(personIndex, true);
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
            groupRect.anchorMin = new Vector2(0.5f, 0.115f);
            groupRect.anchorMax = new Vector2(0.5f, 0.255f);
            groupRect.pivot = new Vector2(0f, 0.5f);

            float actualWidth = (Group.Count - 1) * horizontalGroup.spacing;
            groupRect.anchoredPosition = new Vector2(-actualWidth / 2.0f, 0);
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

        private void OnDestroy() {
        }

        private float lastExecuted = -1;

        internal void Execute() {
            if (lastExecuted > 0 && (Time.realtimeSinceStartup - lastExecuted) < .5f) {
                return;
            }
            lastExecuted = Time.realtimeSinceStartup;

            TargetWrapper[] targets = Group.Select(u => new TargetWrapper(u)).ToArray();
            int attemptedCasts = 0;
            int skippedCasts = 0;
            int actuallyCast = 0;

            var tooltip = new TooltipTemplateBuffer();

            foreach (var buff in state.metadata.Where(b => b.Fulfilled > 0)) {
                var needed = new HashSet<BlueprintBuff>(buff.BuffsApplied.Select(x => x.Buff));
                int thisBuffGood = 0;
                int thisBuffBad = 0;
                int thisBuffSkip = 0;
                TooltipTemplateBuffer.BuffResult badResult = null;
                foreach (var (target, caster) in buff.ActualCastQueue) {
                    bool needsCast = true;
                    foreach (var effect in Group[target].Buffs) {
                        if (needed.Contains(effect.Blueprint)) {
                            needsCast = false;
                            break;
                        }
                    }
                    if (!needsCast) {
                        thisBuffSkip++;
                        skippedCasts++;
                        continue;
                    }



                    attemptedCasts++;

                    var modifiedSpell = caster.spell;
                    if (!caster.SlottedSpell.IsAvailable) {
                        if (badResult == null)
                            badResult = tooltip.AddBad(buff);
                        badResult.messages.Add($"  [{caster.who.CharacterName}] => [{Group[target].CharacterName}], no free spell-slot");
                        thisBuffBad++;
                        continue;
                    }
                    var touching = caster.spell.Blueprint.GetComponent<AbilityEffectStickyTouch>();
                    AbilityExecutionContext context;
                    if (touching) {
                        modifiedSpell = new AbilityData(touching.TouchDeliveryAbility, caster.who);
                    } else {
                        modifiedSpell = new AbilityData(modifiedSpell.Blueprint, caster.who);
                    }
                    var oldResistance = modifiedSpell.Blueprint.SpellResistance;
                    modifiedSpell.Blueprint.SpellResistance = false;
                    var spellParams = modifiedSpell.CalculateParams();

                    if (touching) {
                        context = new AbilityExecutionContext(modifiedSpell, spellParams, Vector3.zero);
                        AbilityExecutionProcess.ApplyEffectImmediate(context, targets[target].Unit);
                    } else {
                        context = new AbilityExecutionContext(caster.spell, spellParams, targets[target]);
                        modifiedSpell.Cast(context);
                    }
                    caster.SlottedSpell.Spend();

                    modifiedSpell.Blueprint.SpellResistance = oldResistance;
                    actuallyCast++;
                    thisBuffGood++;
                }

                if (thisBuffGood > 0)
                    tooltip.AddGood(buff).count = thisBuffGood;
                if (thisBuffSkip > 0)
                    tooltip.AddSkip(buff).count = thisBuffSkip;

            }

            var messageString = $"Buffed! Applied {actuallyCast}/{attemptedCasts} (skipped: {skippedCasts}) buffs";

            var message = new CombatLogMessage(messageString, Color.blue, PrefixIcon.RightArrow, tooltip, true);

            var messageLog = LogThreadController.Instance.m_Logs[LogChannelType.Common].First(x => x is MessageLogThread);
            messageLog.AddMessage(message);
        }

        internal void RevalidateSpells() {
            state.RefreshBuffsList(Group);
        }
    }

    class TooltipTemplateBuffer : TooltipBaseTemplate {
        public class BuffResult {
            public Buff buff;
            public List<string> messages;
            public int count;
            public BuffResult(Buff buff) {
                this.buff = buff;
            }
        };
        private List<BuffResult> good = new();
        private List<BuffResult> bad = new();
        private List<BuffResult> skipped = new();

        public BuffResult AddBad(Buff buff) {
            BuffResult result = new(buff);
            result.messages = new();
            bad.Add(result);
            return result;
        }
        public BuffResult AddSkip(Buff buff) {
            BuffResult result = new(buff);
            skipped.Add(result);
            return result;
        }
        public BuffResult AddGood(Buff buff) {
            BuffResult result = new(buff);
            good.Add(result);
            return result;
        }

        public override IEnumerable<ITooltipBrick> GetHeader(TooltipTemplateType type) {
            yield return new TooltipBrickEntityHeader("BubbleBuff results", null);
            yield break;
        }
        public override IEnumerable<ITooltipBrick> GetBody(TooltipTemplateType type) {
            List<ITooltipBrick> elements = new();
            AddResultsNoMessages("Buffs Applied", elements, good);
            AddResultsNoMessages("Buffs Skipped", elements, skipped);

            if (!bad.Empty()) {
                elements.Add(new TooltipBrickTitle("Buffs Failed"));
                elements.Add(new TooltipBrickSeparator());

                foreach (var r in bad) {
                    elements.Add(new TooltipBrickIconAndName(r.buff.Spell.Icon, $"<b>{r.buff.Spell.Name}</b>", TooltipBrickElementType.Small));
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
                    elements.Add(new TooltipBrickIconAndName(r.buff.Spell.Icon, $"<b>{r.buff.Spell.Name}</b> x{r.count}", TooltipBrickElementType.Small));
                }
            }
        }
    }

    class ServiceWindowWatcher : IUIEventHandler {
        public void HandleUIEvent(UIEventType type) {
            BubbleBuffer.InstallHandler.controller.Hide();
        }
    }

    class InstallComponentHandler : IWarningNotificationUIHandler {
        public BubbleBuffSpellbookController controller;


        public void HandleWarning(WarningNotificationType warningType, bool addToLog = true) {
            if (warningType == WarningNotificationType.GameLoaded) {

                Main.Log("GAME LOADED???");

                var spellScreen = CanvasFinder.SpellbookScreen.gameObject;

                if (spellScreen.GetComponent<BubbleBuffSpellbookController>() == null) {
                    controller = spellScreen.AddComponent<BubbleBuffSpellbookController>();
                    controller.CreateBuffstate();
                }
                controller.RevalidateSpells();

                var applyBuffsSprites = ButtonSprites.Load("apply_buffs", new Vector2Int(95, 95));

                var staticRoot = Game.Instance.UI.Canvas.transform;
                var fadeCanvas = Game.Instance.UI.FadeCanvas;
                //var buttonPrefab = fadeCanvas.transform.Find("EscMenuView/Window/ButtonBlock/SaveButton").gameObject;
                var hudLayout = staticRoot.Find("HUDLayout/").gameObject;

                var oldBubble = hudLayout.transform.parent.Find("BUBBLEMODS_ROOT");
                if (oldBubble != null) {
                    GameObject.Destroy(oldBubble.gameObject);
                }

                var root = GameObject.Instantiate(hudLayout, hudLayout.transform.parent);
                root.name = "BUBBLEMODS_ROOT";
                var rect = root.transform as RectTransform;
                rect.anchoredPosition = new Vector2(0, 96);
                rect.SetSiblingIndex(hudLayout.transform.GetSiblingIndex() + 1);

                GameObject.Destroy(rect.Find("CombatLog_New").gameObject);
                GameObject.Destroy(rect.Find("Console_InitiativeTrackerHorizontalPC").gameObject);
                GameObject.Destroy(rect.Find("IngameMenuView/CompassPart").gameObject);

                var buttonPanelRect = rect.Find("IngameMenuView/ButtonsPart");
                GameObject.Destroy(buttonPanelRect.Find("TBMMultiButton").gameObject);
                GameObject.Destroy(buttonPanelRect.Find("InventoryButton").gameObject);
                GameObject.Destroy(buttonPanelRect.Find("Background").gameObject);

                var buttonsContainer = buttonPanelRect.Find("Container").gameObject;
                var buttonsRect = buttonsContainer.transform as RectTransform;
                buttonsRect.anchoredPosition = Vector2.zero;
                buttonsRect.sizeDelta = new Vector2(47.7f * 8, buttonsRect.sizeDelta.y);

                buttonsContainer.GetComponent<GridLayoutGroup>().startCorner = GridLayoutGroup.Corner.LowerLeft;

                var prefab = buttonsContainer.transform.GetChild(0).gameObject;
                prefab.SetActive(false);

                for (int i = 1; i < buttonsContainer.transform.childCount; i++) {
                    GameObject.Destroy(buttonsContainer.transform.GetChild(i).gameObject);
                }

                var applyBuffsButton = GameObject.Instantiate(prefab, buttonsContainer.transform);
                applyBuffsButton.SetActive(true);
                applyBuffsButton.GetComponentInChildren<OwlcatButton>().m_CommonLayer[0].SpriteState = new SpriteState {
                    pressedSprite = applyBuffsSprites.down,
                    highlightedSprite = applyBuffsSprites.hover,
                };
                applyBuffsButton.GetComponentInChildren<OwlcatButton>().OnLeftClick.AddListener(() => {
                    BubbleBuffer.Execute();
                });
                applyBuffsButton.GetComponentInChildren<OwlcatButton>().SetTooltip(new TooltipTemplateSimple("Buff!", "Try to cast spells set in the buff window"), InfoCallMethod.None);

                applyBuffsButton.GetComponentInChildren<Image>().sprite = applyBuffsSprites.normal;


            }
        }

        public void HandleWarning(string text, bool addToLog = true) {
        }
    }

    public class BuffProvider {
        public UnitEntityData who;
        public AbilityData baseSpell;
        public Spellbook book;
        public Guid classGuid;
        public IReactiveProperty<int> credits;
        public int spent;
        public int clamp;
        public AbilityData spell;
        public int CharacterIndex;

        public int AvailableCredits {
            get {
                if (clamp < int.MaxValue)
                    return clamp - spent;
                else
                    return credits.Value;
            }
        }

        public AbilityData SlottedSpell => baseSpell ?? spell;

        public bool CanTarget(int dude) {
            if (spell.TargetAnchor == Kingmaker.UnitLogic.Abilities.Blueprints.AbilityTargetAnchor.Owner)
                return dude == CharacterIndex;
            return true;
        }
    }


    static class BuffHelper {
		public static List<ContextActionApplyBuff> BuffsInAbility(BlueprintAbility ad)
		{
			AbilityEffectStickyTouch component = ad.GetComponent<AbilityEffectStickyTouch>();
			bool flag = component;
			if (flag)
			{
				ad = component.TouchDeliveryAbility;
			}
            return Enumerable.OfType<ContextActionApplyBuff>(ad.FlattenAllActions()).ToList();
			//return Enumerable.Select<ContextActionApplyBuff, BlueprintBuff>(Enumerable.OfType<ContextActionApplyBuff>(ad.FlattenAllActions()), (ContextActionApplyBuff c) => c.Buff));
		}

		public static ContextActionApplyBuff[] GetAbilityContextActionApplyBuffs(BlueprintAbility Ability)
		{
			return Enumerable.ToArray<ContextActionApplyBuff>(
                Enumerable.Where<ContextActionApplyBuff>(
                    Enumerable.SelectMany<AbilityEffectRunAction, ContextActionApplyBuff>(
                        Ability.GetComponents<AbilityEffectRunAction>(), (AbilityEffectRunAction c) => Enumerable.Concat<ContextActionApplyBuff>(
                            Enumerable.Concat<ContextActionApplyBuff>(
                                Enumerable.OfType<ContextActionApplyBuff>(c.Actions.Actions), Enumerable.SelectMany<ContextActionConditionalSaved, ContextActionApplyBuff>(
                                    Enumerable.OfType<ContextActionConditionalSaved>(c.Actions.Actions), (ContextActionConditionalSaved a) => Enumerable.OfType<ContextActionApplyBuff>(a.Failed.Actions))), Enumerable.SelectMany<Conditional, ContextActionApplyBuff>(
                                        Enumerable.OfType<Conditional>(c.Actions.Actions), (Conditional a) => Enumerable.Concat<ContextActionApplyBuff>(
                                            Enumerable.OfType<ContextActionApplyBuff>(a.IfTrue.Actions), Enumerable.OfType<ContextActionApplyBuff>(a.IfFalse.Actions))))), (ContextActionApplyBuff c) => c.Buff != null));
		}

		public static DurationRate[] GetAbilityBuffDurations(BlueprintAbility Ability)
		{
			ContextActionApplyBuff[] abilityContextActionApplyBuffs = GetAbilityContextActionApplyBuffs(Ability);
            return Enumerable.ToArray<DurationRate>(Enumerable.Select<ContextActionApplyBuff, DurationRate>(abilityContextActionApplyBuffs,
                (ContextActionApplyBuff a) => a.UseDurationSeconds ? DurationRate.Rounds : a.DurationValue.Rate));
        }

        public static GameAction[] FlattenAllActions(this BlueprintAbility Ability)
		{
			return Enumerable.ToArray<GameAction>(Enumerable.Concat<GameAction>(Enumerable.SelectMany<AbilityExecuteActionOnCast, GameAction>(Ability.GetComponents<AbilityExecuteActionOnCast>(), (AbilityExecuteActionOnCast a) => a.FlattenAllActions()), Enumerable.SelectMany<AbilityEffectRunAction, GameAction>(Ability.GetComponents<AbilityEffectRunAction>(), (AbilityEffectRunAction a) => a.FlattenAllActions())));
		}

		public static GameAction[] FlattenAllActions(this AbilityExecuteActionOnCast Action)
		{
			return FlattenAllActions(Action.Actions.Actions);
		}

		public static GameAction[] FlattenAllActions(this AbilityEffectRunAction Action)
		{
			return FlattenAllActions(Action.Actions.Actions);
		}

		public static GameAction[] FlattenAllActions(GameAction[] Actions)
		{
			List<GameAction> list = new List<GameAction>();
			list.AddRange(Enumerable.SelectMany<ContextActionConditionalSaved, GameAction>(Enumerable.OfType<ContextActionConditionalSaved>(Actions), (ContextActionConditionalSaved a) => a.Failed.Actions));
			list.AddRange(Enumerable.SelectMany<ContextActionConditionalSaved, GameAction>(Enumerable.OfType<ContextActionConditionalSaved>(Actions), (ContextActionConditionalSaved a) => a.Succeed.Actions));
			list.AddRange(Enumerable.SelectMany<Conditional, GameAction>(Enumerable.OfType<Conditional>(Actions), (Conditional a) => a.IfFalse.Actions));
			list.AddRange(Enumerable.SelectMany<Conditional, GameAction>(Enumerable.OfType<Conditional>(Actions), (Conditional a) => a.IfTrue.Actions));
			bool flag = list.Count > 0;
			GameAction[] result;
			if (flag)
			{
				result = Enumerable.ToArray<GameAction>(Actions.Concat(FlattenAllActions(list.ToArray())));
			}
			else
			{
				result = Enumerable.ToArray<GameAction>(Actions);
			}
			return result;
		}
    }

    [Flags]
    public enum HideReason {
        Short = 1,
        Blacklisted = 2,
    };

    public class SavedBuffState {
        [JsonProperty]
        public bool Blacklisted;
        [JsonProperty]
        public ISet<string> Wanted;
        [JsonProperty]
        public List<string> CasterPriority;
        [JsonProperty]
        public Guid BaseSpell;
    }

    public class Buff {
        public AbilityData Spell;
        byte[] wanted = new byte[16];
        byte[] given = new byte[16];

        public Guid Key {
            get {
                return Spell.Blueprint.AssetGuid.m_Guid;
            }
        }

        public HideReason HiddenBecause;

        public bool Hidden { get { return HiddenBecause != 0; } }

        public List<ContextActionApplyBuff> BuffsApplied;

        public int TotalAvailable = 0;

        public int Requested {
            get => wanted.Count(b => b != 0);
        }
        public int Fulfilled {
            get => given.Count(b => b != 0);
        }

        public int Available {
            get => CasterQueue.Sum(caster => caster.AvailableCredits);
        }


        public bool UnitWants(int unit) { return wanted[unit] != 0; }
        public bool UnitGiven(int unit) { return given[unit] != 0; }

        public List<BuffProvider> CasterQueue = new();
        public List<(int, BuffProvider)> ActualCastQueue;

        public Buff(AbilityData spell) {
            this.Spell = spell;
            this.NameLower = spell.Name.ToLower();
        }

        public Action OnUpdate = null;
        public int Index;
        internal String NameLower;
        internal Spellbook book;

        public void AddProvider(UnitEntityData provider, Spellbook book, AbilityData spell, AbilityData baseSpell, IReactiveProperty<int> initialCount, bool newCredit, int creditClamp, int u) {
            if (this.book == null)
                this.book = book;
            foreach (var buffer in CasterQueue) {
                if (buffer.who == provider && buffer.book.Blueprint.AssetGuid == book.Blueprint.AssetGuid) {
                    buffer.credits.Value++;
                    return;
                }
            }

            CasterQueue.Add(new BuffProvider { credits = initialCount, who = provider, spent = 0, clamp = creditClamp, book = book, spell = spell, baseSpell = baseSpell, CharacterIndex = u });
        }

        internal void SetUnitWants(int unit, bool v) {
            wanted[unit] = v ? (byte)1 : (byte)0;
        }

        public void Invalidate() {
            foreach (var caster in CasterQueue) {
                if (caster == null) continue;

                caster.credits.Value += caster.spent;
                caster.spent = 0;
            }
            for (int i = 0; i < given.Length; i++)
                given[i] = 0;

            if (ActualCastQueue != null)
                ActualCastQueue.Clear();

        }

        public bool CanTarget(int who) {
            foreach (var caster in CasterQueue) {
                if (caster.CanTarget(who))
                    return true;
            }
            return false;
        }

        public void Validate() {
            int lastCaster = 0;
            for (int i = 0; i < wanted.Length; i++) {
                given[i] = 0;
                if (wanted[i] == 0) continue;


                for (int n = lastCaster; n < CasterQueue.Count; n++) {
                    var caster = CasterQueue[n];
                    if (caster.AvailableCredits > 0) {
                        if (!caster.CanTarget(i)) continue;
                        CasterQueue[n].credits.Value--;
                        CasterQueue[n].spent++;
                        given[i] = 1;
                        lastCaster = n;

                        if (ActualCastQueue == null)
                            ActualCastQueue = new();
                        ActualCastQueue.Add((i, caster));
                        break;
                    }
                }
            }
            OnUpdate?.Invoke();
        }

        internal void SetHidden(HideReason reason, bool set) {
            if (set)
                HiddenBecause |= reason;
            else
                HiddenBecause &= ~reason;
        }

        internal bool HideBecause(HideReason reason) {
            return (HiddenBecause & reason) != 0;
        }
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
        public OwlcatMultiButton Button;
        public GameObject GameObject;
        public TextMeshProUGUI Text;

        public RectTransform Transform { get { return GameObject.transform as RectTransform; } }
    }

    class BufferView {

        public IViewModel[] ViewModels {
            get {
                return state.metadata.Select(buff => new AbilityDataVM(buff.Spell, buff.book, currentSelectedSpell)).ToArray<IViewModel>(); ;
            }
        }

        private WidgetListMVVM buffWidgetList;
        private IViewModel[] models;
        private IDisposable widgetListDrawHandle;


        public List<GameObject> buffWidgets = new();

        public GameObject buffWindow;
        public GameObject removeFromAll;
        public GameObject addToAll;
        public Portrait[] targets;
        private BufferState state;
        public Portrait[] casterPortraits;

        public IWidgetView spellPrefab;
        public GameObject listPrefab;
        public Transform content;

        public BufferView(BufferState state) {
            this.state = state;
            state.OnRecalculated = Update;
        }

        public void MakeBuffsList() {
            GameObject.Destroy(content.Find("AvailableBuffList")?.gameObject);
            buffWidgets.Clear();

            var availableBuffs = GameObject.Instantiate(listPrefab.gameObject, content);
            availableBuffs.name = "AvailableBuffList";
            availableBuffs.GetComponentInChildren<GridLayoutGroupWorkaround>().constraintCount = 4;
            var listRect = availableBuffs.transform as RectTransform;
            listRect.localPosition = Vector2.zero;
            listRect.sizeDelta = Vector2.zero;
            listRect.anchorMin = new Vector2(0.125f, 0.5f);
            listRect.anchorMax = new Vector2(0.875f, 0.9f);
            GameObject.Destroy(listRect.Find("Toggle").gameObject);
            var scrollContent = availableBuffs.transform.Find("StandardScrollView/Viewport/Content");
            for (int i = 0; i < scrollContent.childCount; i++) {
                GameObject.DestroyImmediate(scrollContent.GetChild(i).gameObject);
            }
            buffWidgetList = availableBuffs.GetComponent<WidgetListMVVM>();
            models = ViewModels;
            widgetListDrawHandle = buffWidgetList.DrawEntries<IWidgetView>(models, new List<IWidgetView> { spellPrefab });
            OwlcatButton previousSelection = null;
            for (int i = 0; i < buffWidgetList.m_Entries.Count; i++) {
                IWidgetView widget = buffWidgetList.m_Entries[i];
                var button = widget.MonoBehaviour.GetComponent<OwlcatButton>();
                Buff buff = state.metadata[i];
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
                    previousSelection = button;
                });
                buff.TotalAvailable = buff.Available;
                var label = widget.MonoBehaviour.transform.Find("School/SchoolLabel").gameObject.GetComponent<TextMeshProUGUI>();
                buff.OnUpdate = () => {
                    if (widget.MonoBehaviour == null)
                        return;
                    label.text = $"casting: {buff.Fulfilled}/{buff.Requested} + available: {buff.Available}/{buff.TotalAvailable}";
                    var textImage = widget.MonoBehaviour.transform.Find("Name/BackgroundName").GetComponent<Image>();
                    if (buff.Requested > 0 && buff.Fulfilled != buff.Requested) {
                        textImage.color = Color.red;
                    } else {
                        textImage.color = Color.white;
                    }
                };
                label.text = $"casting: {buff.Fulfilled}/{buff.Requested} + available: {buff.Available}/{buff.TotalAvailable}";
                widget.MonoBehaviour.transform.Find("School").gameObject.SetActive(true);
                widget.MonoBehaviour.gameObject.SetActive(true);

                buffWidgets.Add(widget.MonoBehaviour.gameObject);
            }

            foreach (var buff in state.metadata) {
                buff.OnUpdate();
            }
        }

        public void Update() {
            if (state.Dirty) {
                MakeBuffsList();
                state.Dirty = false;
            }

            if (currentSelectedSpell.Value == null)
                return;

            PreviewReceivers(null);
            UpdateCasterDetails(Selected);
        }

        public void PreviewReceivers(Buff buff) {
            if (buff == null && currentSelectedSpell.Value != null)
                buff = Selected;

            for (int p = 0; p < BubbleBuffer.Group.Count; p++)
                UpdateTargetBuffColor(buff, p);
        }

        private void UpdateTargetBuffColor(Buff buff, int i) {
            var portrait = targets[i].Image;
            targets[i].Button.Interactable = true;
            if (buff == null) {
                portrait.color = Color.white;
                return;
            }
            if (!buff.CanTarget(i)) {
                portrait.color = Color.red;
                targets[i].Button.Interactable = false;
            } else if (buff.UnitWants(i)) {
                if (buff.UnitGiven(i)) {
                    portrait.color = Color.green;
                } else {
                    portrait.color = Color.yellow;
                }
            } else {
                portrait.color = Color.gray;
            }
        }

        private void UpdateCasterDetails(Buff buff) {
            for (int i = 0; i < casterPortraits.Length; i++) {
                casterPortraits[i].GameObject.SetActive(i < buff.CasterQueue.Count);
                if (i < buff.CasterQueue.Count) {
                    var who = buff.CasterQueue[i];
                    casterPortraits[i].Image.sprite = targets[who.CharacterIndex].Image.sprite;
                    casterPortraits[i].Text.text = $"{who.spent}+{who.AvailableCredits}";
                }
            }
            addToAll.GetComponentInChildren<OwlcatButton>().Interactable = buff.Requested != BubbleBuffer.Group.Count; ;
            removeFromAll.GetComponentInChildren<OwlcatButton>().Interactable = buff.Requested > 0;

        }

        public IReactiveProperty<AbilityDataVM> currentSelectedSpell = new ReactiveProperty<AbilityDataVM>();

        public Buff Selected {
            get {
                if (currentSelectedSpell == null)
                    return null;
                var guid = currentSelectedSpell.Value.SpellData.Blueprint.AssetGuid.m_Guid;
                if (state.buffsWithCount.TryGetValue(guid, out var buff))
                    return buff;
                return null;
            }
        }
    }

    class SavedBufferState {
        [JsonProperty]
        public Dictionary<Guid, SavedBuffState> Buffs = new();
    }

    class BufferState {
        public Dictionary<Guid, Buff> buffsWithCount = new();
        public List<Buff> metadata = new();

        public bool Dirty = false;

        public Action OnRecalculated;

        public void RefreshBuffsList(List<UnitEntityData> Group) {
            Dirty = true;
            buffsWithCount.Clear();
            metadata.Clear();

            int characterIndex = 0;
            foreach (var dude in Group) {
                foreach (var book in dude.Spellbooks) {
                    if (book.Blueprint.Spontaneous) {
                        for (int level = 1; level < book.LastSpellbookLevel; level++) {
                            ReactiveProperty<int> credits = new ReactiveProperty<int>(book.GetSpellsPerDay(level));
                            foreach (var spell in book.GetKnownSpells(level))
                                AddBuff(dude, book, spell, null, credits, false, int.MaxValue, characterIndex);
                        }
                    } else {
                        foreach (var slot in book.GetAllMemorizedSpells()) {
                            AddBuff(dude, book, slot.Spell, null, new ReactiveProperty<int>(1), true, int.MaxValue, characterIndex);
                        }
                    }
                }
                characterIndex++;
            }

            //foreach (var (id, buff) in SavedState.Buffs) {
            //    if (!buffsWithCount.ContainsKey(id)) {
            //        Main.Log("from save: buff with no caster");
            //        AddBuff(null, null, new AbilityData(Resources.GetBlueprint<BlueprintAbility>(id.ToString()), Group[0]), null, null, false, -1, -1);
            //    }
            //}
            Recalculate(false);
        }

        public SavedBufferState SavedState;

        public BufferState(SavedBufferState save) {
            this.SavedState = save;
        }

        internal void Recalculate(bool updateUi) {
            foreach (var gbuff in metadata)
                gbuff.Invalidate();
            foreach (var gbuff in metadata)
                gbuff.Validate();

            if (updateUi) {
                OnRecalculated?.Invoke();
            }

            Save();
        }

        public void Save() {
            static void updateSavedBuff(Buff buff, SavedBuffState save) {
                save.Blacklisted = buff.HideBecause(HideReason.Blacklisted);
                save.Wanted.Clear();
                for (int i = 0; i < BubbleBuffer.Group.Count; i++) {
                    if (buff.UnitWants(i)) {
                        save.Wanted.Add(BubbleBuffer.Group[i].CharacterName);
                    }
                }
            }

            foreach (var buff in metadata) {
                var key = buff.Key;
                if (SavedState.Buffs.TryGetValue(key, out var save)) {
                    if (buff.Requested == 0) {
                        SavedState.Buffs.Remove(key);
                    } else {
                        updateSavedBuff(buff, save);
                    }
                } else if (buff.Requested > 0 || buff.HideBecause(HideReason.Blacklisted)) {
                    save = new();
                    save.Wanted = new HashSet<string>();
                    updateSavedBuff(buff, save);
                    SavedState.Buffs[key] = save;
                }
            }


            using (var settingsWriter = File.CreateText(BubbleBuffSpellbookController.SettingsPath)) {
                JsonSerializer.CreateDefault().Serialize(settingsWriter, SavedState);
            }
        }

        public void AddBuff(UnitEntityData dude, Kingmaker.UnitLogic.Spellbook book, AbilityData spell, AbilityData baseSpell, IReactiveProperty<int> credits, bool newCredit, int creditClamp, int charIndex) {
            if (spell.TargetAnchor == Kingmaker.UnitLogic.Abilities.Blueprints.AbilityTargetAnchor.Point)
                return;

            if (spell.Blueprint.HasVariants) {
                var variantsComponent = spell.Blueprint.Components.First(c => typeof(AbilityVariants).IsAssignableFrom(c.GetType())) as AbilityVariants;
                foreach (var variant in variantsComponent.Variants) {
                    var data = new AbilityData(variant, book, spell.SpellLevel);
                    AddBuff(dude, book, data, spell, credits, false, creditClamp, charIndex);
                }
                return;
            }
            

            var guid = spell.Blueprint.AssetGuid.m_Guid;
            int clamp = int.MaxValue;
            if (spell.TargetAnchor == Kingmaker.UnitLogic.Abilities.Blueprints.AbilityTargetAnchor.Owner) {
                clamp = 1;
            }

            if (buffsWithCount.TryGetValue(guid, out var buff)) {
                buff.AddProvider(dude, book, spell, baseSpell, credits, newCredit, clamp, charIndex);
            } else {
                var buffList = BuffHelper.BuffsInAbility(spell.Blueprint);
                if (buffList.Empty())
                    return;

                bool isShort = false;
                isShort = !buffList.Any(buff => buff.Permanent || (buff.UseDurationSeconds && buff.DurationSeconds >= 60) || buff.DurationValue.Rate != DurationRate.Rounds);

                buff = new Buff(spell) {
                    BuffsApplied = buffList,
                    Index = metadata.Count
                };

                if (SavedState.Buffs.TryGetValue(guid, out var fromSave)) {
                    for (int i = 0; i < BubbleBuffer.Group.Count; i++) {
                        UnitEntityData u = BubbleBuffer.Group[i];
                        if (fromSave.Wanted.Contains(u.CharacterName))
                            buff.SetUnitWants(i, true);
                    }
                }

                buff.SetHidden(HideReason.Short, isShort);

                if (dude != null) {
                    buff.AddProvider(dude, book, spell, baseSpell, credits, newCredit, clamp, charIndex);
                }

                buffsWithCount[guid] = buff;
                metadata.Add(buff);
            }
        }
    }

    class BubbleBuffer {

        public static List<UnitEntityData> Group => Game.Instance.SelectionCharacter.ActualGroup;

        public static InstallComponentHandler InstallHandler;


        private static ServiceWindowWatcher UiEventSubscriber;
        private static SpellbookWatcher SpellMemorizeHandler;

        public static void Install() {

            InstallHandler = new();
            UiEventSubscriber = new();
            SpellMemorizeHandler = new();
            EventBus.Subscribe(InstallHandler);
            EventBus.Subscribe(UiEventSubscriber);
            EventBus.Subscribe(SpellMemorizeHandler);

        }

        public static void Execute() {
            
            InstallHandler.controller.Execute();
        }


        public static void Uninstall() {
            EventBus.Unsubscribe(InstallHandler);
            EventBus.Unsubscribe(UiEventSubscriber);
            EventBus.Unsubscribe(SpellMemorizeHandler);
        }
    }

    internal class SpellbookWatcher : ISpellBookUIHandler, IAreaHandler, ILevelUpCompleteUIHandler, IPartyChangedUIHandler {
        public void HandleForgetSpell(AbilityData data, UnitDescriptor owner) {
            BubbleBuffer.InstallHandler.controller.RevalidateSpells();
        }

        public void HandleLevelUpComplete(UnitEntityData unit, bool isChargen) {
            BubbleBuffer.InstallHandler.controller.RevalidateSpells();
        }

        public void HandleMemorizedSpell(AbilityData data, UnitDescriptor owner) {
            BubbleBuffer.InstallHandler.controller.RevalidateSpells();
        }

        public void HandlePartyChanged() {
            BubbleBuffer.InstallHandler.controller.RevalidateSpells();
        }

        public void OnAreaDidLoad() {
            BubbleBuffer.InstallHandler.controller.RevalidateSpells();
        }

        public void OnAreaBeginUnloading() {
        }

    }
}
