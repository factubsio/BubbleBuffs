using BubbleTweaks.Utilities;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.Party;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Spellbook.KnownSpells;
using Kingmaker.UI.MVVM._VM.ServiceWindows;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.KnownSpells;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
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
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleTweaks {

    class MenuOpenedHandler : INewServiceWindowUIHandler {
        public void HandleCloseAll() {
        }

        public void HandleOpenCharacterInfo() {
        }

        public void HandleOpenEncyclopedia() {
        }

        public void HandleOpenEquipment() {
        }

        public void HandleOpenInventory() {
        }

        public void HandleOpenJournal() {
        }

        public void HandleOpenLocalMap() {
        }

        public void HandleOpenMythicInfo() {
        }

        public void HandleOpenSmartItem() {
        }

        public void HandleOpenSpellbook() {
        }

        public void HandleOpenWindowOfType(ServiceWindowsType type) {
            Buffer.ForceClose();
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
    }

    public class Buff {
        public AbilityDataVM Spell;
        byte[] wanted = new byte[16];
        byte[] given = new byte[16];

        public Guid Key {
            get {
                return Spell.SpellData.Blueprint.AssetGuid.m_Guid;
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

        public Buff(AbilityDataVM spell) {
            this.Spell = spell;
            this.NameLower = spell.DisplayName.ToLower();
        }

        public Action OnUpdate = null;
        public int Index;
        internal String NameLower;

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

        public GameObject buffWindow;
        public GameObject removeFromAll;
        public GameObject addToAll;
        public Portrait[] targets;
        private BufferState state;
        public Portrait[] casterPortraits;

        public BufferView(BufferState state) {
            this.state = state;
            state.OnRecalculated = Update;
        }

        public void Update() {
            if (currentSelectedSpell.Value == null)
                return;

            PreviewReceivers(null);
            UpdateCasterDetails(Selected);
        }

        public void PreviewReceivers(Buff buff) {
            if (buff == null && currentSelectedSpell.Value != null)
                buff = Selected;

            for (int p = 0; p < Buffer.Group.Count; p++)
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
            addToAll.GetComponentInChildren<OwlcatButton>().Interactable = buff.Requested != Buffer.Group.Count; ;
            removeFromAll.GetComponentInChildren<OwlcatButton>().Interactable = buff.Requested > 0;

        }

        public IReactiveProperty<AbilityDataVM> currentSelectedSpell = new ReactiveProperty<AbilityDataVM>();

        public Buff Selected {
            get {
                var guid = currentSelectedSpell.Value.SpellData.Blueprint.AssetGuid.m_Guid;
                var buff = state.buffsWithCount[guid];
                return buff;
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
        public List<AbilityDataVM> allBuffs = new();

        public Action OnRecalculated;


        public SavedBufferState SavedState;

        public BufferState(SavedBufferState save) {
            this.SavedState = save;
        }

        internal void Recalculate() {
            foreach (var gbuff in metadata)
                gbuff.Invalidate();
            foreach (var gbuff in metadata)
                gbuff.Validate();

            OnRecalculated?.Invoke();

            Save();
        }

        public void Save() {
            static void updateSavedBuff(Buff buff, SavedBuffState save) {
                save.Blacklisted = buff.HideBecause(HideReason.Blacklisted);
                save.Wanted.Clear();
                for (int i = 0; i < Buffer.Group.Count; i++) {
                    if (buff.UnitWants(i)) {
                        save.Wanted.Add(Buffer.Group[i].CharacterName);
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


            using (var settingsWriter = File.CreateText("bubblebuff.json")) {
                JsonSerializer.CreateDefault().Serialize(settingsWriter, SavedState);
            }
        }

        public void AddBuff(IReactiveProperty<AbilityDataVM> currentSelectedSpell, UnitEntityData dude, Kingmaker.UnitLogic.Spellbook book, AbilityData spell, IReactiveProperty<int> credits, bool newCredit, int creditClamp, int charIndex) {
            if (spell.TargetAnchor == Kingmaker.UnitLogic.Abilities.Blueprints.AbilityTargetAnchor.Point)
                return;

            if (spell.Blueprint.HasVariants) {
                var variantsComponent = spell.Blueprint.Components.First(c => typeof(AbilityVariants).IsAssignableFrom(c.GetType())) as AbilityVariants;
                foreach (var variant in variantsComponent.Variants) {
                    var data = new AbilityData(variant, book, spell.SpellLevel);
                    AddBuff(currentSelectedSpell, dude, book, data, credits, false, creditClamp, charIndex);
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
                var buffList = BuffHelper.BuffsInAbility(spell.Blueprint);
                if (buffList.Empty())
                    return;

                bool isShort = false;
                isShort = !buffList.Any(buff => buff.Permanent || (buff.UseDurationSeconds && buff.DurationSeconds >= 60) || buff.DurationValue.Rate != DurationRate.Rounds);

                buff = new Buff(vm) {
                    BuffsApplied = buffList,
                    Index = metadata.Count
                };

                if (SavedState.Buffs.TryGetValue(guid, out var fromSave)) {
                    for (int i = 0; i < Buffer.Group.Count; i++) {
                        UnitEntityData u = Buffer.Group[i];
                        if (fromSave.Wanted.Contains(u.CharacterName))
                            buff.SetUnitWants(i, true);
                    }
                }

                buff.SetHidden(HideReason.Short, isShort);
                buff.AddProvider(dude, book, spell, credits, newCredit, clamp, charIndex);

                buffsWithCount[guid] = buff;
                metadata.Add(buff);
                allBuffs.Add(vm);
            }
        }
    }

    class Buffer {

        public static List<UnitEntityData> Group => Game.Instance.SelectionCharacter.ActualGroup;

        public static MenuOpenedHandler menuHandler;


        static ButtonSprites applyBuffsSprites;
        public static void Install() {

            menuHandler = new();
            EventBus.Subscribe(menuHandler);

            //applyBuffsSprites = ButtonSprites.Load("apply_buffs", new Vector2Int(95, 95));

            //var staticRoot = Game.Instance.UI.Canvas.transform;
            //var fadeCanvas = Game.Instance.UI.FadeCanvas;
            ////var buttonPrefab = fadeCanvas.transform.Find("EscMenuView/Window/ButtonBlock/SaveButton").gameObject;
            //var hudLayout = staticRoot.Find("HUDLayout/").gameObject;

            //var oldBubble = hudLayout.transform.parent.Find("BUBBLEMODS_ROOT");
            //if (oldBubble != null) {
            //    Main.Log("poppiing bubble");

            //    GameObject.Destroy(oldBubble.gameObject);
            //}

            //var root = GameObject.Instantiate(hudLayout, hudLayout.transform.parent);
            //root.name = "BUBBLEMODS_ROOT";
            //var rect = root.transform as RectTransform;
            //rect.anchoredPosition = new Vector2(0, 96);

            //GameObject.Destroy(rect.Find("CombatLog_New").gameObject);
            //GameObject.Destroy(rect.Find("Console_InitiativeTrackerHorizontalPC").gameObject);
            //GameObject.Destroy(rect.Find("IngameMenuView/CompassPart").gameObject);

            //var buttonPanelRect = rect.Find("IngameMenuView/ButtonsPart");
            //GameObject.Destroy(buttonPanelRect.Find("TBMMultiButton").gameObject);
            //GameObject.Destroy(buttonPanelRect.Find("InventoryButton").gameObject);
            //GameObject.Destroy(buttonPanelRect.Find("Background").gameObject);

            //var buttonsContainer = buttonPanelRect.Find("Container").gameObject;
            //var buttonsRect = buttonsContainer.transform as RectTransform;
            //buttonsRect.anchoredPosition = Vector2.zero;
            //buttonsRect.sizeDelta = new Vector2(47.7f * 8, buttonsRect.sizeDelta.y);

            //buttonsContainer.GetComponent<GridLayoutGroup>().startCorner = GridLayoutGroup.Corner.LowerLeft;

            //var prefab = buttonsContainer.transform.GetChild(0).gameObject;
            //prefab.SetActive(false);

            //for (int i = 1; i < buttonsContainer.transform.childCount; i++) {
            //    GameObject.Destroy(buttonsContainer.transform.GetChild(i).gameObject);
            //}

            //var applyBuffsButton = GameObject.Instantiate(prefab, buttonsContainer.transform);
            //applyBuffsButton.SetActive(true);
            //applyBuffsButton.GetComponentInChildren<OwlcatButton>().m_CommonLayer[0].SpriteState = new SpriteState {
            //    pressedSprite = applyBuffsSprites.down,
            //    highlightedSprite = applyBuffsSprites.hover,
            //};
            //applyBuffsButton.GetComponentInChildren<OwlcatButton>().OnLeftClick.AddListener(() => {
            //    Buffer.Execute();
            //});
            //applyBuffsButton.GetComponentInChildren<Image>().sprite = applyBuffsSprites.normal;
        }

        public static void Execute() {
        }



        public static ReactiveProperty<bool> ShowOnlyRequested = new ReactiveProperty<bool>(false);
        public static ReactiveProperty<bool> ShowShort = new ReactiveProperty<bool>(false);
        public static ReactiveProperty<bool> ShowHidden = new ReactiveProperty<bool>(false);
        public static ReactiveProperty<string> NameFilter = new ReactiveProperty<string>("");

        public static SavedBufferState save;
        public static BufferState state;
        public static BufferView view;
        private static IDisposable escHandler;

        public static void ToggleWindow() {
            if (view != null && view.buffWindow != null) {
                if (view.buffWindow.activeSelf)
                    view.buffWindow.SetActive(false);
                else {
                    view.buffWindow.SetActive(true);
                    Main.Log("Registering esc");
                    //if (escHandler != null)
                    //    escHandler.Dispose();
                    //escHandler = Game.Instance.UI.EscManager.Subscribe(ForceClose);
                }
                return;
            }

            try {
                var staticRoot = Game.Instance.UI.Canvas.transform;

                var oldWindow = staticRoot.Find("BUBBLE.Buffer");
                if (oldWindow != null) {
                    Main.Log("DESTROYING OLD WINDOW");
                    ForceClose();
                    GameObject.Destroy(oldWindow.gameObject);
                }

                using (var settingsReader = File.OpenText("bubblebuff.json"))
                using (var jsonReader = new JsonTextReader(settingsReader)) {
                    save = JsonSerializer.CreateDefault().Deserialize<SavedBufferState>(jsonReader);
                    foreach (var s in save.Buffs) {
                        foreach (var c in s.Value.Wanted) {
                            Main.Log($"   <- {c}");
                        }
                    }
                }

                state = new BufferState(save);
                view = new BufferView(state);

                var partyVM = staticRoot.Find("PartyPCView").GetComponent<PartyPCView>().ViewModel;

                var portraitPrefab = staticRoot.Find("PartyPCView/PartyCharacterView_01").gameObject;
                var listPrefab = staticRoot.Find("ServiceWindowsPCView/SpellbookView/SpellbookScreen/MainContainer/KnownSpells");
                var spellPrefab = listPrefab.GetComponent<SpellbookKnownSpellsPCView>().m_KnownSpellView;
                var framePrefab = staticRoot.Find("ServiceWindowsPCView/MythicInfoView/Window/MainContainer/MythicInfoProgressionView/Progression/Frame").gameObject;
                var togglePrefab = staticRoot.Find("ServiceWindowsPCView/SpellbookView/SpellbookScreen/MainContainer/KnownSpells/Toggle").gameObject;
                var buttonPrefab = staticRoot.Find("ServiceWindowsPCView/SpellbookView/SpellbookScreen/MainContainer/MetamagicContainer/Metamagic/Button").gameObject;
                var nextPrefab = staticRoot.Find("PartyPCView/Background/Next").gameObject;
                var prevPrefab = staticRoot.Find("PartyPCView/Background/Prev").gameObject;


                var vendorPCView = staticRoot.Find("VendorPCView").gameObject;
                view.buffWindow = GameObject.Instantiate(vendorPCView, vendorPCView.transform.parent);
                GameObject.Destroy(view.buffWindow.transform.Find("MainContent/Doll").gameObject);
                GameObject.Destroy(view.buffWindow.transform.Find("MainContent/PlayerStash").gameObject);
                GameObject.Destroy(view.buffWindow.transform.Find("MainContent/PlayerExchangePart").gameObject);
                GameObject.Destroy(view.buffWindow.transform.Find("MainContent/VendorExchangePart").gameObject);
                GameObject.Destroy(view.buffWindow.transform.Find("MainContent/VendorBlock").gameObject);
                GameObject.Destroy(view.buffWindow.transform.Find("MainContent/DealBlock").gameObject);

                GameObject.Destroy(view.buffWindow.transform.Find("MainContent/PapperBackground/Art/Line").gameObject);

                var content = view.buffWindow.transform.Find("MainContent");

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

                var closeButton = view.buffWindow.transform.Find("MainContent/CloseButton").GetComponent<OwlcatButton>();
                closeButton.OnLeftClickAsObservable().Subscribe<UniRx.Unit>(_ => ForceClose());

                view.addToAll = GameObject.Instantiate(buttonPrefab, content);
                view.addToAll.GetComponentInChildren<TextMeshProUGUI>().text = "Add To All";
                var addToAllRect = view.addToAll.transform as RectTransform;
                addToAllRect.anchorMin = new Vector2(0.48f, 0.19f);
                addToAllRect.anchorMax = new Vector2(0.48f, 0.2f);
                addToAllRect.pivot = new Vector2(1, 0);
                addToAllRect.localPosition = Vector2.zero;
                addToAllRect.anchoredPosition = Vector2.zero;

                view.removeFromAll = GameObject.Instantiate(buttonPrefab, content);
                view.removeFromAll.GetComponentInChildren<TextMeshProUGUI>().text = "Remove From All";
                var removeFromAllRect = view.removeFromAll.transform as RectTransform;
                removeFromAllRect.anchorMin = new Vector2(0.52f, 0.19f);
                removeFromAllRect.anchorMax = new Vector2(0.52f, 0.2f);
                removeFromAllRect.pivot = new Vector2(0, 0);
                removeFromAllRect.localPosition = Vector2.zero;
                removeFromAllRect.anchoredPosition = Vector2.zero;

                var groupHolder = new GameObject("GroupHolder", typeof(RectTransform));
                var groupRect = groupHolder.GetComponent<RectTransform>();
                groupRect.SetParent(content);
                groupRect.localPosition = Vector2.zero;
                groupRect.sizeDelta = Vector2.zero;

                float requiredWidthHalf = Group.Count * 0.033f;
                groupRect.anchorMin = new Vector2(0.5f, 0.04f);
                groupRect.anchorMax = new Vector2(0.5f, 0.17f);
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

                GameObject toggleBlacklist = MakeToggle(togglePrefab, detailsRect, .2f, .85f, "Hide Spell", "bubble-toggle-spell-blacklist");
                toggleBlacklist.SetActive(false);

                var filterToggles = new GameObject("filters", typeof(RectTransform));
                filterToggles.AddComponent<VerticalLayoutGroup>().childForceExpandHeight = false;
                //filterToggles.AddComponent<Image>().color = Color.green;
                var filterRect = filterToggles.transform as RectTransform;
                filterRect.SetParent(content);
                filterRect.anchoredPosition3D = Vector3.zero;
                filterRect.anchorMin = new Vector2(0.08f, 0.1f);
                filterRect.anchorMax = new Vector2(0.16f, .40f);

                var search = new SearchBar(filterRect, "...", false, "bubble-search-buff");
                GameObject showHidden = MakeToggle(togglePrefab, filterRect, 0.8f, .5f, "Show Hidden", "bubble-toggle-show-hidden");
                GameObject showShort = MakeToggle(togglePrefab, filterRect, .8f, .5f, "Show Short", "bubble-toggle-show-short");
                GameObject showOnlyRequested = MakeToggle(togglePrefab, filterRect, .8f, .5f, "Only Requested", "bubble-toggle-show-requested");

                search.InputField.onValueChanged.AddListener(val => {
                    NameFilter.Value = val;
                });


                ShowShort.BindToView(showShort);
                ShowHidden.BindToView(showHidden);
                ShowOnlyRequested.BindToView(showOnlyRequested);

                view.targets = new Portrait[Group.Count];

                int totalCasters = 0;

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

                        state.Recalculate();

                    });
                    view.targets[i] = portrait;

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

                var buffWidgetList = availableBuffs.GetComponent<WidgetListMVVM>();

                toggleBlacklist.GetComponentInChildren<ToggleWorkaround>().onValueChanged.AddListener(doBlacklist => {
                    var buff = view.Selected;
                    if (buff == null)
                        return;

                    buff.SetHidden(HideReason.Blacklisted, doBlacklist);
                    RefreshFiltering();
                });

                OwlcatButton previousSelection = null;

                currentSpellView.ViewModel = view.currentSelectedSpell.Value;
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
                                    state.AddBuff(view.currentSelectedSpell, dude, book, spell, credits, false, int.MaxValue, characterIndex);
                            }
                        } else {
                            foreach (var slot in book.GetAllMemorizedSpells()) {
                                state.AddBuff(view.currentSelectedSpell, dude, book, slot.Spell, new ReactiveProperty<int>(1), true, int.MaxValue, characterIndex);
                            }
                        }
                    }
                    characterIndex++;
                }

                IViewModel[] models = state.allBuffs.ToArray<IViewModel>();
                buffWidgetList.DrawEntries<IWidgetView>(models, new List<IWidgetView> { spellPrefab });

                view.addToAll.GetComponentInChildren<OwlcatButton>().OnLeftClick.AddListener(() => {
                    var buff = view.Selected;
                    if (buff == null) return;

                    for (int i = 0; i < Group.Count; i++) {
                        if (view.targets[i].Button.Interactable && !buff.UnitWants(i)) {
                            buff.SetUnitWants(i, true);
                        }
                    }
                    state.Recalculate();

                });
                view.removeFromAll.GetComponentInChildren<OwlcatButton>().OnLeftClick.AddListener(() => {
                    var buff = view.Selected;
                    if (buff == null) return;

                    for (int i = 0; i < Group.Count; i++) {
                        if (buff.UnitWants(i)) {
                            buff.SetUnitWants(i, false);
                        }
                    }
                    state.Recalculate();
                });

                void RefreshFiltering() {
                    foreach (var buff in state.metadata) {
                        var widget = buffWidgetList.m_Entries[buff.Index].MonoBehaviour.gameObject;

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
                    //if (search.InputField.text != val)
                    //    search.InputField.text = val;
                    RefreshFiltering();
                });

                view.currentSelectedSpell.Subscribe(val => {
                    if (view.currentSelectedSpell.HasValue && view.currentSelectedSpell.Value != null) {
                        toggleBlacklist.SetActive(true);
                        currentSpellView.gameObject.SetActive(true);
                        currentSpellView.Bind(view.currentSelectedSpell.Value);

                        var buff = view.Selected;

                        if (buff == null) {
                            return;
                        }

                        view.addToAll.SetActive(true);
                        view.removeFromAll.SetActive(true);

                        view.Update();

                        toggleBlacklist.GetComponentInChildren<ToggleWorkaround>().isOn = buff.Hidden;

                    } else {
                        currentSpellView.gameObject.SetActive(false);
                        toggleBlacklist.SetActive(false);

                        view.addToAll.SetActive(false);
                        view.removeFromAll.SetActive(false);

                        foreach (var caster in view.casterPortraits)
                            caster.GameObject.SetActive(false);

                        foreach (var portrait in view.targets) {
                            portrait.Image.color = Color.white;
                            portrait.Button.Interactable = true;
                        }

                    }
                });


                for (int i = 0; i < buffWidgetList.m_Entries.Count; i++) {
                    IWidgetView widget = buffWidgetList.m_Entries[i];
                    var button = widget.MonoBehaviour.GetComponent<OwlcatButton>();
                    Buff buff = state.metadata[i];
                    button.OnHover.RemoveAllListeners();
                    button.OnHover.AddListener(hover => {
                        view.PreviewReceivers(hover ? buff : null);
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
                        label.text = $"casting: {buff.Fulfilled}/{buff.Requested} + available: {buff.Available}/{buff.TotalAvailable}";
                    };
                    label.text = $"casting: {buff.Fulfilled}/{buff.Requested} + available: {buff.Available}/{buff.TotalAvailable}";
                    widget.MonoBehaviour.transform.Find("School").gameObject.SetActive(true);
                    //widget.MonoBehaviour.transform.Find("School/SchoolLabel").gameObject.GetComponent<TextMeshProUGUI>().fontSizeMin = 12.0f;
                    //widget.MonoBehaviour.transform.Find("School/SchoolLabel").gameObject.GetComponent<TextMeshProUGUI>().fontSize = 12.0f;
                }

                state.Recalculate();
                RefreshFiltering();

                view.buffWindow.name = "BUBBLE.Buffer";
                view.buffWindow.SetActive(true);
                //Game.Instance.UI.EscManager.Subscribe(new Action(ForceClose));
            } catch (Exception e) {
                Main.Error(e, "Loading");
                if (view.buffWindow != null) {
                    view.buffWindow.SetActive(false);
                    GameObject.Destroy(view.buffWindow);
                    view.buffWindow = null;
                }
            }


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


        public static void Uninstall() {
            EventBus.Unsubscribe(menuHandler);
        }

        internal static void ForceClose() {
            if (view?.buffWindow?.activeSelf ?? false) {
                view.buffWindow.SetActive(false);
                //if (escHandler != null) {
                    //Game.Instance.UI.EscManager.Unsubscribe(ForceClose);
                    //escHandler.Dispose();
                    //escHandler = null;
                //}
            }
        }
    }
}
