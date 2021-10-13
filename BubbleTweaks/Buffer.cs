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
        public IReactiveProperty<int> credits;
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
            get => CasterQueue.Sum(caster => caster?.credits?.Value ?? 0);
        }

        public BuffProvider[] CasterQueue = new BuffProvider[13];

        public void AddUnit(int unitIndex) {
            wanted[unitIndex] = 1;

            for (int i = 0; i < CasterQueue.Length; i++) {
                if (CasterQueue[i].credits.Value > 0) {
                    CasterQueue[i].credits.Value--;
                    given[unitIndex] = 1;
                    break;
                }
            }
        }
        public Buff(AbilityDataVM spell) {
            this.Spell = spell;
        }

        public void AddProvider(UnitEntityData provider, IReactiveProperty<int> initialCount, int prio, bool newCredit) {
            Main.Log($"adding provider for spell: {Spell.DisplayName}, provider[{prio}]:{provider.CharacterName}");
            if (CasterQueue[prio] != null) {
                if (newCredit) {
                    Main.Log($"adding credits =>  {CasterQueue[prio].credits.Value + 1}");
                    CasterQueue[prio].credits.Value++;
                } else {
                    Main.Log($"not adding credits for duplicate spell");
                }
            } else {
                Main.Log($"initialising credits => {initialCount.Value}");
                CasterQueue[prio] = new BuffProvider { credits = initialCount, who = provider };
            }
        }
    }


    class Buffer {

        public static List<UnitEntityData> Group => Game.Instance.SelectionCharacter.ActualGroup;

        public static MenuOpenedHandler menuHandler;

        public static void Install() {
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

                var portraitPrefab = staticRoot.Find("PartyPCView/PartyCharacterView_01/Portrait/LifePortrait");
                var listPrefab = staticRoot.Find("ServiceWindowsPCView/SpellbookView/SpellbookScreen/MainContainer/KnownSpells");
                var spellPrefab = listPrefab.GetComponent<SpellbookKnownSpellsPCView>().m_KnownSpellView;

                var vendorPCView = staticRoot.Find("VendorPCView").gameObject;
                buffWindow = GameObject.Instantiate(vendorPCView, vendorPCView.transform.parent);
                GameObject.Destroy(buffWindow.transform.Find("MainContent/Doll").gameObject);
                GameObject.Destroy(buffWindow.transform.Find("MainContent/PlayerStash").gameObject);
                GameObject.Destroy(buffWindow.transform.Find("MainContent/PlayerExchangePart").gameObject);
                GameObject.Destroy(buffWindow.transform.Find("MainContent/VendorExchangePart").gameObject);
                GameObject.Destroy(buffWindow.transform.Find("MainContent/VendorBlock").gameObject);
                GameObject.Destroy(buffWindow.transform.Find("MainContent/DealBlock").gameObject);

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
                groupRect.anchorMin = new Vector2(0.3f, 0.07f);
                groupRect.anchorMax = new Vector2(0.7f, 0.17f);

                var horizontalGroup = groupHolder.AddComponent<HorizontalLayoutGroup>();
                horizontalGroup.spacing = 8;

                for (int i = 0; i < Group.Count; i++) {
                    var portrait = GameObject.Instantiate(portraitPrefab, groupRect);
                    portrait.localPosition = Vector2.zero;
                    portrait.GetComponent<Image>().sprite = Group[i].Portrait.SmallPortrait;
                }

                var buffWidgetList = availableBuffs.GetComponent<WidgetListMVVM>();
                List<AbilityDataVM> allBuffs = new();
                List<Buff> metadata = new();

                IReactiveProperty<AbilityDataVM> currentSelectedSpell = new ReactiveProperty<AbilityDataVM>();


                Dictionary<Guid, Buff> buffsWithCount = new();

                TargetWrapper[] selfTargets = new TargetWrapper[Group.Count];
                for (int i = 0; i < Group.Count; i++) {
                    selfTargets[i] = new TargetWrapper(Group[i]);
                }

                int prio = 0;
                foreach (var dude in Group) {
                    foreach (var book in dude.Spellbooks) {
                        if (book.Blueprint.Spontaneous) {
                            for (int level = 1; level < book.LastSpellbookLevel; level++) {
                                ReactiveProperty<int> credits = new ReactiveProperty<int>(book.GetSpellsPerDay(level));
                                foreach (var spell in book.GetKnownSpells(level))
                                    AddBuff(metadata, allBuffs, currentSelectedSpell, buffsWithCount, prio, dude, book, spell, credits, false);
                            }
                        } else {
                            foreach (var slot in book.GetAllMemorizedSpells()) {
                                AddBuff(metadata, allBuffs, currentSelectedSpell, buffsWithCount, prio, dude, book, slot.Spell, new ReactiveProperty<int>(1), true);
                            }
                        }
                    }
                    prio++;
                }

                IViewModel[] models = allBuffs.ToArray<IViewModel>();
                buffWidgetList.DrawEntries<IWidgetView>(models, new List<IWidgetView> { spellPrefab });

                for (int i = 0; i < buffWidgetList.m_Entries.Count; i++) {
                    IWidgetView widget = buffWidgetList.m_Entries[i];
                    Buff buff = metadata[i];
                    buff.TotalAvailable = buff.Available;
                    widget.MonoBehaviour.transform.Find("Level").gameObject.SetActive(true);
                    widget.MonoBehaviour.transform.Find("Level/LevelLabel").gameObject.GetComponent<TextMeshProUGUI>().text = $"{buff.Available}/{buff.TotalAvailable}";
                    widget.MonoBehaviour.transform.Find("Level/LevelLabel").gameObject.GetComponent<TextMeshProUGUI>().fontSizeMin = 12.0f;
                    widget.MonoBehaviour.transform.Find("Level/LevelLabel").gameObject.GetComponent<TextMeshProUGUI>().fontSize = 12.0f;
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
        }

        private static void AddBuff(List<Buff> metadata, List<AbilityDataVM> allBuffs, IReactiveProperty<AbilityDataVM> currentSelectedSpell, Dictionary<Guid, Buff> buffsWithCount, int prio, UnitEntityData dude, Kingmaker.UnitLogic.Spellbook book, AbilityData spell, IReactiveProperty<int> credits, bool newCredit) {
            if (spell.Blueprint.HasVariants) {
                var variantsComponent = spell.Blueprint.Components.First(c => typeof(AbilityVariants).IsAssignableFrom(c.GetType())) as AbilityVariants;
                foreach (var variant in variantsComponent.Variants) {
                    var data = new AbilityData(variant, book, spell.SpellLevel);
                    AddBuff(metadata, allBuffs, currentSelectedSpell, buffsWithCount, prio, dude, book, data, credits, false);
                }
                return;
            }

            var guid = spell.Blueprint.AssetGuid.m_Guid;
            if (buffsWithCount.TryGetValue(guid, out var buff)) {
                buff.AddProvider(dude, credits, prio, newCredit);
            } else {
                var vm = new AbilityDataVM(spell, book, currentSelectedSpell);
                buff = new Buff(vm);
                buff.AddProvider(dude, credits, prio, newCredit);

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
