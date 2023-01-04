using Kingmaker.Enums;
using System;
using System.Collections.Generic;

namespace BubbleBuffs.Config {
    public class ModBuffs : IUpdatableSettings {
        public Dictionary<string, List<ModBuff>> Buffs = new();

        public void OverrideSettings(IUpdatableSettings userSettings) {
            // Append rather than override. This does mean you can't remove buffs but it feels better than the 
            // alternative, where user settings result in new pushes being ignored!
            foreach (var entry in (userSettings as ModBuffs).Buffs) {
                Buffs[entry.Key] = entry.Value;
            }
        }

        public List<IBeneficialEffect> GetEffects(Guid buff) {
            var guidStr = buff.ToString();
            if (!Buffs.ContainsKey(guidStr)) {
                Main.Error(new ArgumentException(), $"No mod buff with guid: {buff}");
                return new();
            }

            var effects = new List<IBeneficialEffect>();
            foreach (var effect in Buffs[guidStr]) {
                switch (effect.Type) {
                    case ModBuffType.Simple:
                        effects.Add(new BuffEffect(effect));
                        break;
                    case ModBuffType.Area:
                        effects.Add(new AreaBuffEffect(effect));
                        break;
                    case ModBuffType.PrimaryWeapon:
                    case ModBuffType.SecondaryWeapon:
                        effects.Add(new WornItemEnchantmentEffect(effect));
                        break;
                    default:
                        Main.Error(new ArgumentException(), $"Unknown buff type: {effect.Type}");
                        break;
                }
            }
            return effects;
        }

        /// <summary>
        /// Indicates the type of buff.
        /// </summary>
        public enum ModBuffType {
            Simple,
            Area,
            PrimaryWeapon,
            SecondaryWeapon
        }

        public class ModBuff {
            /// <summary>
            /// AssetID of the applied effect, e.g. Buff for Simple/Area or Enchantment for PrimaryWeapon/SecondaryWeapon.
            /// </summary>
            public string AssetID;

            /// <summary>
            /// Type of mod buff, defaults to simple.
            /// </summary>
            public ModBuffType Type = ModBuffType.Simple;

            /// <summary>
            /// Set to true for buffs that last longer than a minute.
            /// </summary>
            public bool IsLong;

            /// <summary>
            /// Type of pet the buff applies to.
            /// </summary>
            public PetType? PetType;
        }
    }
}
