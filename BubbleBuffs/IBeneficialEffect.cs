using BubbleBuffs.Extensions;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.UnitLogic.Abilities.Components.AreaEffects;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Utility;
using System;
using System.Linq;
using System.Collections.Generic;
using Kingmaker.Enums;

namespace BubbleBuffs {

    public class UnitBuffData {
        public readonly UnitEntityData Unit;
        public readonly HashSet<Guid> Buffs;

        public UnitBuffData(UnitEntityData u) {
            Unit = u;
            Buffs = new(u.Buffs.RawFacts.Select(b => b.BGuid()));
        }
    }

    public class AbilityCombinedEffects {

        private HashSet<Guid> AppliedBuffs;
        private HashSet<Guid> AppliedPetBuffs;
        private HashSet<Guid> PrimaryWeaponEnchants;
        private HashSet<Guid> SecondaryWeaponEnchants;
        public PetType? PetType = null;

        private void Add(ref HashSet<Guid> set, Guid fact) {
            if (set == null)
                set = new();
            set.Add(fact);
        }

        public void AddPetBuff(Guid buff, PetType type, bool isLong) {
            if (PetType != null && type != PetType) {
                Main.Error("Could not add pet buff with different pet type");
                return;
            }

            Add(ref AppliedPetBuffs, buff);
            PetType = type;
            IsLong |= isLong;
        }

        public void AddBuff(Guid buff, bool isLong) {
            Add(ref AppliedBuffs, buff);
            IsLong |= isLong;
        }
        public void AddPrimaryWeaponEnchant(Guid buff, bool isLong) {
            Add(ref PrimaryWeaponEnchants, buff);
            IsLong |= isLong;
        }
        public void AddSecondaryWeaponEnchnant(Guid buff, bool isLong) {
            Add(ref SecondaryWeaponEnchants, buff);
            IsLong |= isLong;
        }

        public AbilityCombinedEffects(IEnumerable<IBeneficialEffect> beneficialEffects) {
            foreach (var effect in beneficialEffects.EmptyIfNull())
                effect.AppendTo(this);
            Empty = AppliedBuffs == null && AppliedPetBuffs == null && PrimaryWeaponEnchants == null && SecondaryWeaponEnchants == null;
        }


        internal bool IsPresent(UnitBuffData unitBuffData) {
            if (AppliedPetBuffs != null) {
                var pet = unitBuffData.Unit.GetPet(PetType.Value);
                var existingBuffs = new HashSet<Guid>(pet.Buffs.RawFacts.Select(b => b.BGuid()));
                if (existingBuffs.Overlaps(AppliedPetBuffs))
                    return true;
            }

            if (AppliedBuffs != null) {
                if (unitBuffData.Buffs.Overlaps(AppliedBuffs))
                    return true;
            }

            if (PrimaryWeaponEnchants != null) {
                foreach (var enchant in unitBuffData.Unit.Body.PrimaryHand.MaybeWeapon.Enchantments) {
                    if (PrimaryWeaponEnchants.Contains(enchant.BGuid()))
                        return true;
                }
            }
            if (SecondaryWeaponEnchants != null) {
                foreach (var enchant in unitBuffData.Unit.Body.SecondaryHand.MaybeWeapon.Enchantments) {
                    if (SecondaryWeaponEnchants.Contains(enchant.BGuid()))
                        return true;
                }
            }

            return false;
        }

        public readonly bool Empty = true;
        public bool IsLong { get; private set; }
    }

    public interface IBeneficialEffect {
        public void AppendTo(AbilityCombinedEffects effect);
        public PetType? PetType { get; set; }
    }
    public class AreaBuffEffect : IBeneficialEffect {

        public readonly Guid Applied;
        public readonly bool IsLong;
        public AreaBuffEffect(AbilityAreaEffectBuff action, bool isLong) {
            Applied = action.Buff.AssetGuid.m_Guid;
            IsLong = isLong;
        }

        public PetType? PetType { get; set; }

        public void AppendTo(AbilityCombinedEffects effect) {
            if (PetType != null)
                effect.AddPetBuff(Applied, PetType.Value, IsLong);
            else
                effect.AddBuff(Applied, IsLong);
        }
    }

    public class BuffEffect : IBeneficialEffect {

        public readonly Guid Applied = Guid.Empty;
        public readonly bool IsLong;
        public  BuffEffect(ContextActionApplyBuff action) {
            if (action.Buff == null) return;
            Applied = action.Buff.AssetGuid.m_Guid;
            IsLong = action.IsLong();
        }

        public PetType? PetType { get; set; }

        public void AppendTo(AbilityCombinedEffects effect) {
            if (Applied != Guid.Empty) {
            if (PetType != null)
                effect.AddPetBuff(Applied, PetType.Value, IsLong);
                else
                    effect.AddBuff(Applied, IsLong);
            }
        }
    }

    public class WornItemEnchantmentEffect : IBeneficialEffect {
        public readonly Guid Applied;
        public readonly bool PrimaryWeapon;
        public readonly bool SecondaryWeapon;
        public readonly bool IsLong;

        public PetType? PetType { get; set; }
        
        public WornItemEnchantmentEffect(ContextActionEnchantWornItem action) {
            Applied = action.Enchantment.AssetGuid.m_Guid;
            if (action.Slot == Kingmaker.UI.GenericSlot.EquipSlotBase.SlotType.PrimaryHand)
                PrimaryWeapon = true;
            if (action.Slot == Kingmaker.UI.GenericSlot.EquipSlotBase.SlotType.SecondaryHand)
                SecondaryWeapon = true;

            IsLong = action.IsLong();
        }
        public void AppendTo(AbilityCombinedEffects effect) {
            if (PrimaryWeapon)
                effect.AddPrimaryWeaponEnchant(Applied, IsLong);
            else if (SecondaryWeapon)
                effect.AddSecondaryWeaponEnchnant(Applied, IsLong);
        }
    }

}
