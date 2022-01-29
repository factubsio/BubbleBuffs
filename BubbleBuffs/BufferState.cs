using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.Utility;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniRx;
using BubbleBuffs.Extensions;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic;
using Kingmaker.Blueprints.Classes;

namespace BubbleBuffs {

    public class BufferState {
        private readonly Dictionary<BuffKey, BubbleBuff> BuffsByKey = new();
        //public List<Buff> buffList = new();
        public IEnumerable<BubbleBuff> BuffList;

        public bool Dirty = false;

        public Action OnRecalculated;

        public void RecalculateAvailableBuffs(List<UnitEntityData> Group) {
            Dirty = true;
            BuffsByKey.Clear();

            Main.Verbose("Recalculating full state");

            try {
                for (int characterIndex = 0; characterIndex < Group.Count; characterIndex++) {
                    UnitEntityData dude = Group[characterIndex];
                    Main.Verbose($"Looking at dude: ${dude.CharacterName}", "state");
                    foreach (var book in dude.Spellbooks) {
                        Main.Verbose($"  Looking at spellbook: {book.Blueprint.DisplayName}", "state");
                        foreach (var spell in book.GetCustomSpells(0)) {
                            ReactiveProperty<int> credits = new ReactiveProperty<int>(500);
                            Main.Verbose($"      Adding cantrip (completely normal): {spell.Name}", "state");
                            AddBuff(dude, book, spell, null, credits, false, int.MaxValue, characterIndex);
                        }

                        foreach (var spell in book.GetKnownSpells(0)) {
                            ReactiveProperty<int> credits = new ReactiveProperty<int>(500);
                            Main.Verbose($"      Adding cantrip: {spell.Name}", "state");
                            AddBuff(dude, book, spell, null, credits, false, int.MaxValue, characterIndex);
                        }

                        if (book.Blueprint.Spontaneous) {
                            for (int level = 1; level <= book.LastSpellbookLevel; level++) {
                                Main.Verbose($"    Looking at spont level {level}", "state");
                                ReactiveProperty<int> credits = new ReactiveProperty<int>(book.GetSpellsPerDay(level));
                                foreach (var spell in book.GetKnownSpells(level)) {
                                    Main.Verbose($"      Adding spontaneous buff: {spell.Name}", "state");
                                    AddBuff(dude, book, spell, null, credits, false, int.MaxValue, characterIndex);
                                }
                                foreach (var spell in book.GetCustomSpells(level)) {
                                    Main.Verbose($"      Adding spontaneous (customised) buff: {spell.Name}/{dude.CharacterName}", "state");
                                    AddBuff(dude, book, spell, null, credits, false, int.MaxValue, characterIndex);
                                }
                            }
                        } else {
                            foreach (var slot in book.GetAllMemorizedSpells()) {
                                Main.Verbose($"      Adding prepared buff: {slot.Spell.Name}", "state");
                                AddBuff(dude, book, slot.Spell, null, new ReactiveProperty<int>(1), true, int.MaxValue, characterIndex);
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Main.Error(ex, "finding spells");
            }

            try {
                for (int characterIndex = 0; characterIndex < Group.Count; characterIndex++) {
                    UnitEntityData dude = Group[characterIndex];
                    foreach (Ability ability in dude.Abilities.RawFacts) {
                        ItemEntity sourceItem = ability.SourceItem;
                        if (sourceItem == null || !sourceItem.IsSpendCharges) {
                            var credits = new ReactiveProperty<int>(500);
                            if (ability.Data.Resource != null) {
                                credits.Value = ability.Data.Resource.GetMaxAmount(dude);
                            }
                            AddBuff(dude, null, ability.Data, null, credits, true, int.MaxValue, characterIndex, false, Category.Ability);
                        }
                    }
                }
            } catch (Exception ex) {
                Main.Error(ex, "finding abilities:");
            }

            //foreach (var rejectKey in SpellsWithBeneficialBuffs.Where(kv => kv.Value.EmptyIfNull().Empty()).Select(kv => kv.Key)) {
            //    var name = SpellNames[rejectKey];
            //    Main.Verbose($"Rejected spell: {name}", "spell-rejection");
            //}

            var list = new List<BubbleBuff>(BuffsByKey.Values);
            //list.Sort((a, b) => {
            //    return a.Name.CompareTo(b.Name);
            //});
            //Main.Log("Sorting buffs");
            BuffList = list;

            foreach (var buff in BuffList) {
                if (SavedState.Buffs.TryGetValue(buff.Key, out var fromSave)) {
                    buff.InitialiseFromSave(fromSave);
                }
                buff.SortProviders();
            }



            lastGroup.Clear();
            lastGroup.AddRange(Group.Select(x => x.UniqueId));
            foreach (var u in Group)
                Bubble.GroupById[u.UniqueId] = u;
            InputDirty = false;


        }


        public SavedBufferState SavedState;

        public BufferState(SavedBufferState save) {
            this.SavedState = save;
        }

        internal void Recalculate(bool updateUi) {
            var group = Bubble.Group;
            if (InputDirty || GroupIsDirty(group)) {
                RecalculateAvailableBuffs(group);
            }

            foreach (var gbuff in BuffList)
                gbuff.Invalidate();
            foreach (var gbuff in BuffList)
                gbuff.Validate();
            foreach (var gbuff in BuffList)
                gbuff.OnUpdate?.Invoke();

            if (updateUi) {
                OnRecalculated?.Invoke();
            }

            Save();
        }

        public bool GroupIsDirty(List<UnitEntityData> group) {
            if (lastGroup.Count != group.Count)
                return true;

            for (int i = 0; i < lastGroup.Count; i++) {
                if (lastGroup[i] != group[i].UniqueId)
                    return true;
            }

            return false;
        }

        public void Save(bool shallow = false) {
            static void updateSavedBuff(BubbleBuff buff, SavedBuffState save) {
                save.Blacklisted = buff.HideBecause(HideReason.Blacklisted);
                save.InGroup = buff.InGroup;
                foreach (var u in Bubble.Group) {
                    if (buff.UnitWants(u)) {
                        save.Wanted.Add(u.UniqueId);
                    } else if (buff.UnitWantsRemoved(u)) {
                        save.Wanted.Remove(u.UniqueId);
                    }
                }
                foreach (var caster in buff.CasterQueue) {
                    if (!save.Casters.TryGetValue(caster.Key, out var state)) {
                        state = new SavedCasterState();
                        save.Casters[caster.Key] = state;
                    }
                    state.Banned = caster.Banned;
                    state.Cap = caster.CustomCap;
                    state.ShareTransmutation = caster.ShareTransmutation;
                    state.PowerfulChange = caster.PowerfulChange;
                }
            }


            if (!shallow) {
                foreach (var buff in BuffList) {
                    var key = buff.Key;
                    if (SavedState.Buffs.TryGetValue(key, out var save)) {
                        updateSavedBuff(buff, save);
                        if (save.Wanted.Empty() && !buff.HideBecause(HideReason.Blacklisted)) {
                            SavedState.Buffs.Remove(key);
                        }
                    } else if (buff.Requested > 0 || buff.HideBecause(HideReason.Blacklisted)) {
                        save = new();
                        save.Wanted = new HashSet<string>();
                        updateSavedBuff(buff, save);
                        SavedState.Buffs[key] = save;
                    }
                }
            }

            SavedState.Version = 1;
            using (var settingsWriter = File.CreateText(BubbleBuffSpellbookController.SettingsPath)) {
                JsonSerializer.CreateDefault().Serialize(settingsWriter, SavedState);
            }
        }

        private static Dictionary<Guid, AbilityCombinedEffects> SpellsWithBeneficialBuffs = new();
        private static Dictionary<Guid, string> SpellNames = new();
        private static Guid MageArmorGuid = Guid.Parse("9e1ad5d6f87d19e4d8883d63a6e35568");
        private static BlueprintFeature ArchmageArmorFeature => Resources.GetBlueprint<BlueprintFeature>("c3ef5076c0feb3c4f90c229714e62cd0");

        public bool AllowInCombat {
            get => SavedState.AllowInCombat;
            set {
                SavedState.AllowInCombat = value;
                Save(true);
            }
        }

        public bool OverwriteBuff {
            get => SavedState.OverwriteBuff;
            set {
                SavedState.OverwriteBuff = value;
                Save(true);
            }
        }

        //private static Dictionary<Guid, List<ContextActionApplyBuff>> CachedBuffEffects;

        public void AddBuff(UnitEntityData dude, Kingmaker.UnitLogic.Spellbook book, AbilityData spell, AbilityData baseSpell, IReactiveProperty<int> credits, bool newCredit, int creditClamp, int charIndex, bool archmageArmor = false, Category category = Category.Spell) {
            //if (spell.TargetAnchor == Kingmaker.UnitLogic.Abilities.Blueprints.AbilityTargetAnchor.Point)
            //    Main.Log($"Rejecting {spell.Name} due to being cast-at-point");

            //bool anyTargets = Bubble.Group.Any(t => spell.CanTarget(new TargetWrapper(t)));
            //if (!anyTargets) {
            //    return;
            //} 

            if (spell.Blueprint.AssetGuid.m_Guid == MageArmorGuid && !archmageArmor && dude.HasFact(ArchmageArmorFeature)) {
                AddBuff(dude, book, spell, null, credits, false, creditClamp, charIndex, true, category);
            }


            if (spell.Blueprint.HasVariants) {
                var variantsComponent = spell.Blueprint.Components.First(c => typeof(AbilityVariants).IsAssignableFrom(c.GetType())) as AbilityVariants;
                foreach (var variant in variantsComponent.Variants) {
                    AbilityData data;
                    if (book == null) {
                        data = new AbilityData(variant, dude);
                    } else
                        data = new AbilityData(variant, book, spell.SpellLevel);

                    data.MetamagicData = spell.MetamagicData?.Clone();
                    AddBuff(dude, book, data, spell, credits, false, creditClamp, charIndex, archmageArmor, category);
                }
                return;
            }

            int clamp = int.MaxValue;
            if (archmageArmor || spell.TargetAnchor == Kingmaker.UnitLogic.Abilities.Blueprints.AbilityTargetAnchor.Owner) {
                clamp = 1;
            }

            var key = new BuffKey(spell, archmageArmor);
            if (BuffsByKey.TryGetValue(key, out var buff)) {
                buff.AddProvider(dude, book, spell, baseSpell, credits, newCredit, clamp, charIndex);
            } else {
                var touchAbility = spell.Blueprint.GetComponent<AbilityEffectStickyTouch>()?.TouchDeliveryAbility;

                if (!SpellsWithBeneficialBuffs.TryGetValue(spell.Blueprint.AssetGuid.m_Guid, out var abilityEffect)) {
                    var beneficial = spell.Blueprint.GetBeneficialBuffs();
                    abilityEffect = new AbilityCombinedEffects(beneficial);

                    SpellsWithBeneficialBuffs[spell.Blueprint.AssetGuid.m_Guid] = abilityEffect;
                    SpellNames[spell.Blueprint.AssetGuid.m_Guid] = spell.Name;
                }

                if (abilityEffect.Empty) {
                    Main.Verbose($"Rejecting {spell.Name} because it has no applied effects", "rejection");
                    return;
                }

                buff = new BubbleBuff(spell, archmageArmor) {
                    BuffsApplied = abilityEffect
                };

                buff.IsMass = spell.Blueprint.IsMass();

                buff.Category = category;

                buff.SetHidden(HideReason.Short, !abilityEffect.IsLong);

                if (dude != null) {
                    buff.AddProvider(dude, book, spell, baseSpell, credits, newCredit, clamp, charIndex);
                }

                BuffsByKey[key] = buff;
            }
        }


        private List<string> lastGroup = new();
        internal bool InputDirty = true;

    }

}
