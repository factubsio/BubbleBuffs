using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using Kingmaker.UnitLogic.Mechanics.Actions;
using BubbleBuffs.Extensions;
using Newtonsoft.Json;
using Kingmaker.Utility;

namespace BubbleBuffs {
    public struct BuffKey {
        [JsonProperty]
        public readonly Guid Guid;
        [JsonProperty]
        public readonly Metamagic MetamagicMask;
        [JsonProperty]
        public readonly bool Archmage;

        public BuffKey(AbilityData ability, bool archmage) {
            Guid = ability.Blueprint.AssetGuid.m_Guid;
            if (ability.IsMetamagicked())
                MetamagicMask = ability.MetamagicData.MetamagicMask;
            else
                MetamagicMask = 0;
            Archmage = archmage;
        }

        public override bool Equals(object obj) {
            return obj is BuffKey key &&
                   Guid.Equals(key.Guid) &&
                   MetamagicMask == key.MetamagicMask &&
                   Archmage == key.Archmage;
        }

        public override int GetHashCode() {
            int hashCode = 1282151259;
            hashCode = hashCode * -1521134295 + Guid.GetHashCode();
            hashCode = hashCode * -1521134295 + MetamagicMask.GetHashCode();
            hashCode = hashCode * -1521134295 + Archmage.GetHashCode();
            return hashCode;
        }
    }
    public class BubbleBuff {
        public BuffGroup InGroup = BuffGroup.Long;
        public AbilityData Spell;
        HashSet<string> wanted = new();
        HashSet<string> notWanted = new();
        HashSet<string> given = new();

        public bool IsMass;

        public readonly BuffKey Key;

        public HideReason HiddenBecause;

        public bool Hidden { get { return HiddenBecause != 0; } }

        public AbilityCombinedEffects BuffsApplied;

        public int Requested {
            get => wanted.Count;
        }
        public int Fulfilled {
            get => given.Count;
        }

        public int Available {
            get => CasterQueue.Sum(caster => caster.AvailableCredits);
        }
        public (int, int) AvailableAndSelfOnly {
            get {
                var normal = 0;
                var self = 0;
                foreach (var c in CasterQueue) {
                    if (c.clamp == 1)
                        self += c.AvailableCredits;
                    else
                        normal += c.AvailableCredits;
                }

                return (normal, self);
            }
        }

        private string metaMagicRendered = null;
        private string MetaMagicFlags {
            get {
                if (Metamagics == null)
                    return "";
                if (metaMagicRendered == null) {
                    metaMagicRendered = "[";
                    foreach (Metamagic flag in Enum.GetValues(typeof(Metamagic))) {
                        if (Spell.MetamagicData.Has(flag))
                            metaMagicRendered += flag.Initial();
                    }
                    metaMagicRendered += "]";
                }
                return metaMagicRendered;

            }

        }


        public string Name => Key.Archmage ? "Archmage Armor" : Spell.Name;
        public string NameMeta => $"{Spell.Name} {MetaMagicFlags}";

        public bool UnitWants(UnitEntityData unit) => wanted.Contains(unit.UniqueId);
        public bool UnitWantsRemoved(UnitEntityData unit) => notWanted.Contains(unit.UniqueId);
        public bool UnitGiven(UnitEntityData unit) => given.Contains(unit.UniqueId);

        public List<BuffProvider> CasterQueue = new();
        public List<(string, BuffProvider)> ActualCastQueue;

        public Metamagic[] Metamagics;

        public BubbleBuff(AbilityData spell, bool archmageArmor) {
            this.Spell = spell;
            this.NameLower = spell.Name.ToLower();
            this.Key = new BuffKey(spell, archmageArmor);

            if (Spell.IsMetamagicked()) {
                Metamagics = spell.GetMetamagicks().ToArray();
            }
        }

        public Action OnUpdate = null;
        internal String NameLower;
        internal Spellbook book;
        internal Category Category = Category.Spell;
        internal SavedBuffState SavedState;

        public void AddProvider(UnitEntityData provider, Spellbook book, AbilityData spell, AbilityData baseSpell, IReactiveProperty<int> credits, bool newCredit, int creditClamp, int u) {
            if (this.book == null)
                this.book = book;
            foreach (var buffer in CasterQueue) {
                if (buffer.who == provider && buffer.book?.Blueprint.AssetGuid == book?.Blueprint.AssetGuid) {
                    if (!Key.Archmage)
                        buffer.AddCredits(1);
                    return;
                }
            }
            var providerHandle = new BuffProvider(credits) { who = provider, spent = 0, clamp = creditClamp, book = book, spell = spell, baseSpell = baseSpell, CharacterIndex = u, ArchmageArmor = this.Key.Archmage };
            //providerHandle.InstallDebugListeners();
            CasterQueue.Add(providerHandle);
        }

        internal void SetUnitWants(UnitEntityData unit, bool v) {
            if (v) {
                wanted.Add(unit.UniqueId);
                notWanted.Remove(unit.UniqueId);
            } else {
                wanted.Remove(unit.UniqueId);
                notWanted.Add(unit.UniqueId);
            }
        }

        public void InitialiseFromSave(SavedBuffState state) {
            InGroup = state.InGroup;
            for (int i = 0; i < Bubble.Group.Count; i++) {
                UnitEntityData u = Bubble.Group[i];
                if (state.Wanted.Contains(u.UniqueId))
                    SetUnitWants(u, true);
            }
            SetHidden(HideReason.Blacklisted, state.Blacklisted);
            foreach (var caster in CasterQueue) {
                if (state.Casters.TryGetValue(caster.Key, out var casterState)) {
                    caster.Banned = casterState.Banned;
                    caster.CustomCap = casterState.Cap;
                    caster.ShareTransmutation = casterState.ShareTransmutation;
                    caster.PowerfulChange = casterState.PowerfulChange;
                }
            }
        }

        public void Invalidate() {
            foreach (var caster in CasterQueue) {
                if (caster == null) continue;

                caster.AddCredits(caster.spent);
                caster.spent = 0;
                caster.MaxCap = caster.AvailableCreditsNoCap;
            }
            given.Clear();

            if (ActualCastQueue != null)
                ActualCastQueue.Clear();

        }

        public bool CanTarget(UnitEntityData who) {
            foreach (var caster in CasterQueue) {
                if (caster.CanTarget(who.UniqueId))
                    return true;
            }
            return false;
        }

        public void Validate() {
            foreach (var target in wanted) {

                for (int n = 0; n < CasterQueue.Count; n++) {
                    var caster = CasterQueue[n];
                    if (caster.AvailableCredits > 0) {
                        //Main.Verbose($"checking if: {caster.who.CharacterName} => {Name} => {Bubble.Group[i].CharacterName}");
                        if (!caster.CanTarget(target)) continue;

                        //Main.Verbose($"casting: {caster.who.CharacterName} => {Name} => {Bubble.Group[i].CharacterName}");
                        caster.ChargeCredits(1);
                        caster.spent++;
                        given.Add(target);

                        if (ActualCastQueue == null)
                            ActualCastQueue = new();
                        ActualCastQueue.Add((target, caster));
                        break;
                    }
                }
            }
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

        internal void SortProviders() {
            CasterQueue.Sort((a, b) => {
                if (a.Priority == b.Priority) {
                    int aScore = 0;
                    int bScore = 0;

                    if (!a.SelfCastOnly)
                        aScore += 10_000;
                    if (!b.SelfCastOnly)
                        bScore += 10_000;

                    return aScore - bScore;
                } else {
                    return a.Priority - b.Priority;
                }
            });
        }

        internal void ClearRemovals() {
            notWanted.Clear();
        }

        internal void AdjustCap(int casterIndex, int v) {
            var caster = CasterQueue[casterIndex];
            if (caster.CustomCap == -1) {
                if (v > 0)
                    Main.Error("Error: can't increase cap above max");
                caster.CustomCap = caster.MaxCap - 1;
            } else {
                caster.CustomCap += v;
                if (caster.CustomCap == caster.MaxCap)
                    caster.CustomCap = -1;
            }
        }
    }
    public class BuffProvider {
        public CasterKey Key => new() {
            Name = who.UniqueId,
            Spellbook = book?.Blueprint.AssetGuid.m_Guid ?? Guid.Empty
        };

        public bool ArchmageArmor = false;
        public bool ShareTransmutation;
        public bool PowerfulChange;
        public UnitEntityData who;
        public AbilityData baseSpell;
        public Spellbook book;
        private IReactiveProperty<int> credits;
        public int spent;
        public int clamp;
        public AbilityData spell;
        public int CharacterIndex;

        public void InstallDebugListeners() {
            credits.Subscribe<int>(c => {
                Main.Verbose($"{spell.Name}/{who.CharacterName} => credits changed to: {c}");
            });
        }

        private int ClampValue => ShareTransmutation ? int.MaxValue : clamp;

        public int ClampCredits(int clamp, int value, int spent) {
            if (clamp < int.MaxValue)
                return clamp - spent;
            else
                return value;
        }

        public bool Banned = false;
        public int CustomCap = -1;
        private int ClampForCap => CustomCap == -1 ? int.MaxValue : CustomCap;
        internal int MaxCap;

        public BuffProvider(IReactiveProperty<int> credits) {
            this.credits = credits;
        }

        public int AvailableCredits {
            get {
                if (Banned)
                    return 0;
                return ClampCredits(Math.Min(ClampValue, ClampForCap), credits.Value, spent);
            }
        }
        public int AvailableCreditsNoCap => Banned ? 0 : ClampCredits(ClampValue, credits.Value, spent);


        public AbilityData SlottedSpell => baseSpell ?? spell;

        public int Priority {
            get {
                if (book == null)
                    return 0;

                if (book.Blueprint.Spontaneous) {
                    return 100 - book.CasterLevel;
                } else {
                    return 0 - book.CasterLevel;
                }
            }
        }

        public class ForceShareTransmutation : IDisposable {
            private BuffProvider unit;


            public ForceShareTransmutation(BuffProvider unit) {
                this.unit = unit;
                if (unit.ShareTransmutation)
                    unit.who.State.Features.ShareTransmutation.Retain();
            }

            public void Dispose() {
                if (unit.ShareTransmutation)
                    unit.who.State.Features.ShareTransmutation.Release();
            }
        }

        public bool SelfCastOnly => spell.TargetAnchor == Kingmaker.UnitLogic.Abilities.Blueprints.AbilityTargetAnchor.Owner;

        public bool CanTarget(string targetId) {
            if (ArchmageArmor)
                return targetId == who.UniqueId;

            using (new ForceShareTransmutation(this)) {
                if (!spell.CanTarget(new TargetWrapper(Bubble.GroupById[targetId])))
                    return false;

                if (SelfCastOnly)
                    return targetId == who.UniqueId;
                return true;
            }
        }

        internal void AddCredits(int v) {
            credits.Value += v;
        }

        internal void ChargeCredits(int v) {
            credits.Value -= v;
        }
    }
}
