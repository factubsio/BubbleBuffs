using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kingmaker;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.MVVM._PCView.Vendor;
using Owlcat.Runtime.UI.Controls.Button;
using Owlcat.Runtime.UI.Controls.Other;
using UnityEngine;
using UniRx;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Spellbook.KnownSpells;
using Kingmaker.UI.MVVM._PCView.Party;
using Kingmaker.EntitySystem.Entities;
using UnityEngine.UI;
using Kingmaker.UI.Common;
using Owlcat.Runtime.UI.MVVM;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.KnownSpells;
using Kingmaker.UI;
using Kingmaker.Utility;
using BubbleTweaks.Extensions;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities;
using TMPro;
using Owlcat.Runtime.UI.Controls.SelectableState;
using Kingmaker.UnitLogic;
using BubbleTweaks.Utilities;

namespace BubbleTweaks {

    class MenuOpenedHandler : IServiceWindowUIHandler {
        public void HandleOpenCharScreen() {
        }

        public void HandleOpenInventory() {
        }

        public void HandleOpenMap() {
        }

        public void HandleOpenSpellbook() {
        }
    }

    public class BuffProvider {
        public UnitEntityData who;
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

        public bool CanTarget(int dude) {
            if (spell.TargetAnchor == Kingmaker.UnitLogic.Abilities.Blueprints.AbilityTargetAnchor.Owner)
                return dude == CharacterIndex;
            return true;
        }
    }

    public class Buff {
        public AbilityDataVM Spell;
        byte[] wanted = new byte[16];
        byte[] given = new byte[16];

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

        public Buff(AbilityDataVM spell) {
            this.Spell = spell;
        }

        public Action OnUpdate = null;
        public int Index;
        public bool Hidden;

        public void AddProvider(UnitEntityData provider, Spellbook book, AbilityData spell, IReactiveProperty<int> initialCount, bool newCredit, int creditClamp, int u) {
            foreach (var buffer in CasterQueue) {
                if (buffer.who == provider && buffer.book.Blueprint.AssetGuid == book.Blueprint.AssetGuid) {
                    buffer.credits.Value++;
                    return;
                }
            }

            CasterQueue.Add(new BuffProvider { credits = initialCount, who = provider, spent = 0, clamp = creditClamp, book = book, spell = spell, CharacterIndex = u });
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
                        //Main.Log($"giving:{i} - Caster[{n}].credits: {CasterQueue[n].credits.Value}, spent: {CasterQueue[n].spent}");
                        CasterQueue[n].credits.Value--;
                        CasterQueue[n].spent++;
                        given[i] = 1;
                        lastCaster = n;
                        break;
                    }
                }
            }
            OnUpdate?.Invoke();
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


    class Buffer {

        public static List<UnitEntityData> Group => Game.Instance.SelectionCharacter.ActualGroup;

        public static MenuOpenedHandler menuHandler;

        class Portrait {
            public Image Image;
            public OwlcatMultiButton Button;
            public GameObject GameObject;
            public TextMeshProUGUI Text;

            public RectTransform Transform { get { return GameObject.transform as RectTransform; } }
        }

        static ButtonSprites applyBuffsSprites;
        public static void Install() {

            applyBuffsSprites = ButtonSprites.Load("apply_buffs", new Vector2Int(95, 95));

            var staticRoot = Game.Instance.UI.Canvas.transform;
            var fadeCanvas = Game.Instance.UI.FadeCanvas;
            var buttonPrefab = fadeCanvas.transform.Find("EscMenuView/Window/ButtonBlock/SaveButton").gameObject;
            var hudLayout = staticRoot.Find("HUDLayout/").gameObject;

            var oldBubble = hudLayout.transform.parent.Find("BUBBLEMODS_ROOT");
            if (oldBubble != null) {
                Main.Log("poppiing bubble");

                GameObject.Destroy(oldBubble.gameObject);
            }

            var root = GameObject.Instantiate(hudLayout, hudLayout.transform.parent);
            root.name = "BUBBLEMODS_ROOT";
            var rect = root.transform as RectTransform;
            rect.anchoredPosition = new Vector2(0, 96);

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
            applyBuffsButton.GetComponentInChildren<Image>().sprite = applyBuffsSprites.normal;
        }

        static Portrait[] casterPortraits;

        public static void CreateAndShowWindow() {
            GameObject buffWindow = null;
            try {
                //menuHandler = new();
                //EventBus.Subscribe(menuHandler);

                var staticRoot = Game.Instance.UI.Canvas.transform;

                var oldWindow = staticRoot.Find("BUBBLE.Buffer");
                if (oldWindow != null) {
                    Main.Log("DESTROYING OLD WINDOW");
                    oldWindow.gameObject.SetActive(false);
                    GameObject.Destroy(oldWindow.gameObject);
                }

                var partyVM = staticRoot.Find("PartyPCView").GetComponent<PartyPCView>().ViewModel;

                var portraitPrefab = staticRoot.Find("PartyPCView/PartyCharacterView_01").gameObject;
                var listPrefab = staticRoot.Find("ServiceWindowsPCView/SpellbookView/SpellbookScreen/MainContainer/KnownSpells");
                var spellPrefab = listPrefab.GetComponent<SpellbookKnownSpellsPCView>().m_KnownSpellView;
                var framePrefab = staticRoot.Find("ServiceWindowsPCView/MythicInfoView/Window/MainContainer/MythicInfoProgressionView/Progression/Frame").gameObject;
                var togglePrefab = staticRoot.Find("ServiceWindowsPCView/SpellbookView/SpellbookScreen/MainContainer/KnownSpells/Toggle").gameObject;
                var nextPrefab = staticRoot.Find("PartyPCView/Background/Next").gameObject;
                var prevPrefab = staticRoot.Find("PartyPCView/Background/Prev").gameObject;

                var vendorPCView = staticRoot.Find("VendorPCView").gameObject;
                buffWindow = GameObject.Instantiate(vendorPCView, vendorPCView.transform.parent);
                GameObject.Destroy(buffWindow.transform.Find("MainContent/Doll").gameObject);
                GameObject.Destroy(buffWindow.transform.Find("MainContent/PlayerStash").gameObject);
                GameObject.Destroy(buffWindow.transform.Find("MainContent/PlayerExchangePart").gameObject);
                GameObject.Destroy(buffWindow.transform.Find("MainContent/VendorExchangePart").gameObject);
                GameObject.Destroy(buffWindow.transform.Find("MainContent/VendorBlock").gameObject);
                GameObject.Destroy(buffWindow.transform.Find("MainContent/DealBlock").gameObject);

                GameObject.Destroy(buffWindow.transform.Find("MainContent/PapperBackground/Art/Line").gameObject);

                var content = buffWindow.transform.Find("MainContent");

                var availableBuffs = GameObject.Instantiate(listPrefab.gameObject, content);
                availableBuffs.name = "AvailableBuffList";
                availableBuffs.GetComponentInChildren<GridLayoutGroupWorkaround>().constraintCount = 4;
                var listRect = availableBuffs.transform as RectTransform;
                listRect.localPosition = Vector2.zero;
                listRect.sizeDelta = Vector2.zero;
                listRect.anchorMin = new Vector2(0.05f, 0.5f);
                listRect.anchorMax = new Vector2(0.95f, 0.93f);
                GameObject.Destroy(listRect.Find("Toggle").gameObject);
                var scrollContent = availableBuffs.transform.Find("StandardScrollView/Viewport/Content");
                for (int i = 0; i < scrollContent.childCount; i++) {
                    GameObject.Destroy(scrollContent.GetChild(i).gameObject);
                }

                var closeButton = buffWindow.transform.Find("MainContent/CloseButton").GetComponent<OwlcatButton>();
                closeButton.OnLeftClickAsObservable().Subscribe<UniRx.Unit>(_ => { buffWindow.SetActive(false); });

                var groupHolder = new GameObject("GroupHolder", typeof(RectTransform));
                var groupRect = groupHolder.GetComponent<RectTransform>();
                groupRect.SetParent(content);
                groupRect.localPosition = Vector2.zero;
                groupRect.sizeDelta = Vector2.zero;

                float requiredWidthHalf = Group.Count * 0.033f;
                groupRect.anchorMin = new Vector2(0.5f, 0.07f);
                groupRect.anchorMax = new Vector2(0.5f, 0.22f);
                groupRect.anchoredPosition = new Vector2(-(120 * Group.Count) / 2.0f, 0);

                var horizontalGroup = groupHolder.AddComponent<HorizontalLayoutGroup>();
                horizontalGroup.spacing = 124;
                horizontalGroup.childControlHeight = true;
                horizontalGroup.childForceExpandHeight = true;

                var detailsHolder = GameObject.Instantiate(framePrefab, content);
                var detailsRect = detailsHolder.GetComponent<RectTransform>();
                GameObject.Destroy(detailsHolder.transform.Find("FrameDecor").gameObject);
                detailsRect.localPosition = Vector2.zero;
                detailsRect.sizeDelta = Vector2.zero;
                detailsRect.anchorMin = new Vector2(0.2f, 0.24f);
                detailsRect.anchorMax = new Vector2(0.8f, 0.48f);

                var currentSpellView = GameObject.Instantiate(spellPrefab, detailsRect);
                IReactiveProperty<AbilityDataVM> currentSelectedSpell = new ReactiveProperty<AbilityDataVM>();
                Dictionary<Guid, Buff> buffsWithCount = new();
                List<Buff> metadata = new();

                Func<Buff> currentSelectedBuff = () => {
                    var guid = currentSelectedSpell.Value.SpellData.Blueprint.AssetGuid.m_Guid;
                    var buff = buffsWithCount[guid];
                    return buff;
                };

                var toggleBlacklist = GameObject.Instantiate(togglePrefab, detailsRect);
                toggleBlacklist.name = "bubble-toggle-spell-blacklist";
                toggleBlacklist.SetActive(false);
                var blacklistRect = toggleBlacklist.transform as RectTransform;
                blacklistRect.localPosition = Vector2.zero;
                blacklistRect.anchoredPosition = Vector2.zero;
                blacklistRect.anchorMin = new Vector2(0.2f, 0.85f);
                blacklistRect.anchorMax = new Vector2(0.2f, 0.85f);
                blacklistRect.pivot = new Vector2(0.5f, 0.5f);
                toggleBlacklist.GetComponentInChildren<TextMeshProUGUI>().text = "Hide spell";


                Portrait[] targets = new Portrait[Group.Count];

                int totalCasters = 0;

                for (int i = 0; i < Group.Count; i++) {
                    Portrait portrait = MakePortrait(portraitPrefab, groupRect, true);

                    var aspect = portrait.GameObject.AddComponent<AspectRatioFitter>();
                    aspect.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                    aspect.aspectRatio = 0.75f;

                    portrait.Image.sprite = Group[i].Portrait.SmallPortrait;


                    int personIndex = i;

                    portrait.Button.OnLeftClick.AddListener(() => {
                        var buff = currentSelectedBuff();
                        if (buff == null)
                            return;

                        if (!buff.CanTarget(personIndex))
                            return;

                        if (buff.UnitWants(personIndex)) {
                            buff.SetUnitWants(personIndex, false);
                        } else {
                            buff.SetUnitWants(personIndex, true);
                        }

                        foreach (var gbuff in metadata)
                            gbuff.Invalidate();
                        foreach (var gbuff in metadata)
                            gbuff.Validate();

                        Main.Log("UPDATING CASTER PORTRAITS???");

                        for (int p = 0; p < Group.Count; p++)
                            UpdateTargetBuffColor(targets, buff, p);

                        Main.Log("UPDATING CASTER PORTRAITS???");
                        UpdateCasterDetails(targets, casterPortraits, buff);

                    });
                    targets[i] = portrait;

                    totalCasters += Group[i].Spellbooks?.Count() ?? 0;
                }

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

                var promoteCaster = GameObject.Instantiate(prevPrefab, detailsRect);
                var promoteRect = promoteCaster.transform as RectTransform;
                promoteRect.localPosition = Vector2.zero;
                promoteRect.anchoredPosition = Vector2.zero;
                promoteRect.anchorMin = new Vector2(0.5f, 0.2f);
                promoteRect.anchorMax = new Vector2(0.5f, 0.2f);
                promoteRect.pivot = new Vector2(0.5f, 0.5f);
                promoteRect.anchoredPosition = new Vector2(castersRect.anchoredPosition.x - 60, 0);

                var demoteCaster = GameObject.Instantiate(nextPrefab, detailsRect);
                var demoteRect = demoteCaster.transform as RectTransform;
                demoteRect.localPosition = Vector2.zero;
                demoteRect.anchoredPosition = Vector2.zero;
                demoteRect.anchorMin = new Vector2(0.5f, 0.2f);
                demoteRect.anchorMax = new Vector2(0.5f, 0.2f);
                demoteRect.pivot = new Vector2(0.5f, 0.5f);
                demoteRect.anchoredPosition = new Vector2(-castersRect.anchoredPosition.x + 60, 0);

                casterPortraits = new Portrait[totalCasters];

                for (int i = 0; i < totalCasters; i++) {
                    var casterPortrait = MakePortrait(portraitPrefab, castersRect, false);
                    casterPortraits[i] = casterPortrait;
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

                var buffWidgetList = availableBuffs.GetComponent<WidgetListMVVM>();
                List<AbilityDataVM> allBuffs = new();

                toggleBlacklist.GetComponentInChildren<ToggleWorkaround>().onValueChanged.AddListener(doBlacklist => {
                    var buff = currentSelectedBuff();
                    if (buff == null)
                        return;

                    buffWidgetList.m_Entries[buff.Index].MonoBehaviour.gameObject.SetActive(!doBlacklist);
                    buff.Hidden = doBlacklist;
                });

                OwlcatButton previousSelection = null;

                currentSpellView.ViewModel = currentSelectedSpell.Value;
                currentSpellView.GetComponentInChildren<OwlcatButton>().Interactable = false;
                currentSpellView.gameObject.SetActive(false);
                var currentSpellRect = currentSpellView.transform as RectTransform;
                currentSpellRect.anchorMin = new Vector2(.5f, .8f);
                currentSpellRect.anchorMax = new Vector2(.5f, .8f);


                TargetWrapper[] selfTargets = new TargetWrapper[Group.Count];
                for (int i = 0; i < Group.Count; i++) {
                    selfTargets[i] = new TargetWrapper(Group[i]);
                }

                int characterIndex = 0;
                foreach (var dude in Group) {
                    foreach (var book in dude.Spellbooks) {
                        if (book.Blueprint.Spontaneous) {
                            for (int level = 1; level < book.LastSpellbookLevel; level++) {
                                ReactiveProperty<int> credits = new ReactiveProperty<int>(book.GetSpellsPerDay(level));
                                foreach (var spell in book.GetKnownSpells(level))
                                    AddBuff(metadata, allBuffs, currentSelectedSpell, buffsWithCount, dude, book, spell, credits, false, int.MaxValue, characterIndex);
                            }
                        } else {
                            foreach (var slot in book.GetAllMemorizedSpells()) {
                                AddBuff(metadata, allBuffs, currentSelectedSpell, buffsWithCount, dude, book, slot.Spell, new ReactiveProperty<int>(1), true, int.MaxValue, characterIndex);
                            }
                        }
                    }
                    characterIndex++;
                }

                IViewModel[] models = allBuffs.ToArray<IViewModel>();
                buffWidgetList.DrawEntries<IWidgetView>(models, new List<IWidgetView> { spellPrefab });

                currentSelectedSpell.Subscribe(val => {
                    if (currentSelectedSpell.HasValue && currentSelectedSpell.Value != null) {
                        toggleBlacklist.SetActive(true);
                        currentSpellView.gameObject.SetActive(true);
                        currentSpellView.Bind(currentSelectedSpell.Value);

                        var buff = currentSelectedBuff();

                        if (buff == null) {
                            return;
                        }

                        for (int i = 0; i < Group.Count; i++) {
                            UpdateTargetBuffColor(targets, buff, i);
                        }

                        UpdateCasterDetails(targets, casterPortraits, buff);

                        toggleBlacklist.GetComponentInChildren<ToggleWorkaround>().isOn = buff.Hidden;

                    } else {
                        currentSpellView.gameObject.SetActive(false);
                        toggleBlacklist.SetActive(false);

                        foreach (var caster in casterPortraits)
                            caster.GameObject.SetActive(false);

                        foreach (var portrait in targets) {
                            portrait.Image.color = Color.white;
                            portrait.Button.Interactable = true;
                        }

                    }
                });


                for (int i = 0; i < buffWidgetList.m_Entries.Count; i++) {
                    IWidgetView widget = buffWidgetList.m_Entries[i];
                    var button = widget.MonoBehaviour.GetComponent<OwlcatButton>();
                    button.OnHover.RemoveAllListeners();
                    button.OnSingleLeftClick.AddListener(() => {
                        Main.Log("Listener?");
                        if (previousSelection != null && previousSelection != button) {
                            previousSelection.SetSelected(false);
                        }
                        if (!button.IsSelected) {
                            button.SetSelected(true);
                        }
                        previousSelection = button;
                    });
                    Buff buff = metadata[i];
                    buff.TotalAvailable = buff.Available;
                    var label = widget.MonoBehaviour.transform.Find("School/SchoolLabel").gameObject.GetComponent<TextMeshProUGUI>();
                    buff.OnUpdate = () => {
                        label.text = $"casting: {buff.Fulfilled}/{buff.Requested} + available: {buff.Available}/{buff.TotalAvailable}";
                    };
                    label.text = $"casting: {buff.Fulfilled}/{buff.Requested} + available: {buff.Available}/{buff.TotalAvailable}";
                    widget.MonoBehaviour.transform.Find("School").gameObject.SetActive(true);
                    //widget.MonoBehaviour.transform.Find("School/SchoolLabel").gameObject.GetComponent<TextMeshProUGUI>().fontSizeMin = 12.0f;
                    //widget.MonoBehaviour.transform.Find("School/SchoolLabel").gameObject.GetComponent<TextMeshProUGUI>().fontSize = 12.0f;
                }

                buffWindow.name = "BUBBLE.Buffer";
                buffWindow.SetActive(true);
            } catch (Exception e) {
                Main.Error(e, "Loading");
                if (buffWindow != null) {
                    buffWindow.SetActive(false);
                    GameObject.Destroy(buffWindow);
                }
            }

            static void UpdateTargetBuffColor(Portrait[] targets, Buff buff, int i) {
                var portrait = targets[i].Image;
                targets[i].Button.Interactable = true;
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
        }

        private static void UpdateCasterDetails(Portrait[] targets, Portrait[] casterPortraits, Buff buff) {
            Main.Log($"CasteRPortraits.length {casterPortraits.Length}");
            for (int i = 0; i < casterPortraits.Length; i++) {
                casterPortraits[i].GameObject.SetActive(i < buff.CasterQueue.Count);
                if (i < buff.CasterQueue.Count) {
                    var who = buff.CasterQueue[i];
                    casterPortraits[i].Image.sprite = targets[who.CharacterIndex].Image.sprite;
                    casterPortraits[i].Text.text = $"{who.spent}+{who.AvailableCredits}";
                }
            }
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
            portrait.transform.localPosition = Vector2.zero;

            handle.Image = portraitRect.Find("LifePortrait").gameObject.GetComponent<Image>();
            handle.Button = frameRect.gameObject.GetComponent<OwlcatMultiButton>();

            return handle;
        }

        private static void AddBuff(List<Buff> metadata, List<AbilityDataVM> allBuffs, IReactiveProperty<AbilityDataVM> currentSelectedSpell, Dictionary<Guid, Buff> buffsWithCount, UnitEntityData dude, Kingmaker.UnitLogic.Spellbook book, AbilityData spell, IReactiveProperty<int> credits, bool newCredit, int creditClamp, int charIndex) {
            if (spell.TargetAnchor == Kingmaker.UnitLogic.Abilities.Blueprints.AbilityTargetAnchor.Point)
                return;

            if (spell.Blueprint.HasVariants) {
                var variantsComponent = spell.Blueprint.Components.First(c => typeof(AbilityVariants).IsAssignableFrom(c.GetType())) as AbilityVariants;
                foreach (var variant in variantsComponent.Variants) {
                    var data = new AbilityData(variant, book, spell.SpellLevel);
                    AddBuff(metadata, allBuffs, currentSelectedSpell, buffsWithCount, dude, book, data, credits, false, creditClamp, charIndex);
                }
                return;
            }

            var guid = spell.Blueprint.AssetGuid.m_Guid;
            int clamp = int.MaxValue;
            if (spell.TargetAnchor == Kingmaker.UnitLogic.Abilities.Blueprints.AbilityTargetAnchor.Owner) {
                clamp = 1;
            }

            if (buffsWithCount.TryGetValue(guid, out var buff)) {
                buff.AddProvider(dude, book, spell, credits, newCredit, clamp, charIndex);
            } else {
                var vm = new AbilityDataVM(spell, book, currentSelectedSpell);
                buff = new Buff(vm);
                buff.Index = metadata.Count;
                buff.AddProvider(dude, book, spell, credits, newCredit, clamp, charIndex);

                buffsWithCount[guid] = buff;
                metadata.Add(buff);
                allBuffs.Add(vm);
            }
        }

        public static void Uninstall() {
            //EventBus.Unsubscribe(menuHandler);
        }
    }
}
