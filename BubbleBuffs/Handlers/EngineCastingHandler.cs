using BubbleBuffs.Extensions;
using JetBrains.Annotations;
using Kingmaker;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Controllers.Rest;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.Utility;
using System;
using System.Linq;

namespace BubbleBuffs.Handlers {
    public class EngineCastingHandler : IAbilityExecutionProcessHandler, IRulebookEventAboutToTriggerHook {
        #region Fields

        private readonly CastTask _castTask;
        private readonly bool _spendSpellSlot;
        private ModifiableValue.Modifier _casterLevelModifier;

        #endregion

        #region Properties

        private int ArcaneReservoirPointsAvailable {
            get {
                return _castTask.Caster?.Resources?.PersistantResources?.Where(x => x.Blueprint.AssetGuidThreadSafe == "cac948cbbe79b55459459dd6a8fe44ce")?.First()?.Amount ?? 0;
            }
            set {
                var arcaneReserviorResource = _castTask.Caster?.Resources?.PersistantResources?.Where(x => x.Blueprint.AssetGuidThreadSafe == "cac948cbbe79b55459459dd6a8fe44ce")?.First();
                if (arcaneReserviorResource != null) arcaneReserviorResource.Amount = value;
            }
        }

        private int ArcaneReservoirPointsNeeded {
            get {
                var PowerfulChangeRssLogic = AbilityCache.CasterCache[_castTask.Caster.UniqueId]?.PowerfulChange?.GetComponent<AbilityResourceLogic>();
                var ShareTransmutationCost = PowerfulChangeRssLogic ? PowerfulChangeRssLogic.CalculateCost(_castTask.SpellToCast) : 1;
                var ShareTransmutationRssLogic = AbilityCache.CasterCache[_castTask.Caster.UniqueId]?.ShareTransmutation?.GetComponent<AbilityResourceLogic>();
                var PowerfulChangeCost = ShareTransmutationRssLogic ? ShareTransmutationRssLogic.CalculateCost(_castTask.SpellToCast) : 1;
                var ReservoirCLBuffRssLogic = AbilityCache.CasterCache[_castTask.Caster.UniqueId]?.ReservoirCLBuff?.GetComponent<AbilityResourceLogic>();
                var ReservoirCLBuffCost = ReservoirCLBuffRssLogic ? ReservoirCLBuffRssLogic.CalculateCost(_castTask.SpellToCast) : 1;
                var points = 0;
                if (_castTask.ShareTransmutation && _castTask.Caster != _castTask.Target.Unit) points += Math.Max(0, ShareTransmutationCost);
                if (_castTask.PowerfulChange) points += Math.Max(0, PowerfulChangeCost);
                if (_castTask.ReservoirCLBuff) points += Math.Max(0, ReservoirCLBuffCost);
                return points;
            }
        }

        private bool IsControllingAzataZippyMagicSecondaryCast {
            get {
                // Azata Zippy Magic checks
                var hasAzataZippyMagicFact = _castTask.Caster.HasFact(Resources.GetBlueprint<BlueprintFeature>("30b4200f897ba25419ba3a292aed4053"));
                var isSpellMass = _castTask.SpellToCast.Blueprint.IsMass();
                var canCastOnOthers = _castTask.ShareTransmutation || !_castTask.SelfCastOnly;

                return _castTask.AzataZippyMagic && hasAzataZippyMagicFact && !isSpellMass && canCastOnOthers;
            }
        }

        private bool IsAzataZippyMagicSecondaryCast {
            get {
                return IsControllingAzataZippyMagicSecondaryCast && _castTask.IsDuplicateSpellApplied;
            }
        }

        private bool PriorSpellResistance { get; set; }

        private AbilityExecutionContext Context { get; set; }

        #endregion

        #region Constructors

        public EngineCastingHandler(CastTask castTask, bool spendSpellSlot = false) {
            // Set fields
            _castTask = castTask;
            _spendSpellSlot = spendSpellSlot;

            // Set retentions
            SetAllRetentions();

            // Remove spell resistance
            RemoveSpellResistance();

            ModifyCasterLevel();
            // If this is an Azata Zippy magic secondary cast, then increase the number of spell slots available to offset the spell cast
            if (IsAzataZippyMagicSecondaryCast) {
                IncreaseSpellSlotsAvailable(_castTask.SpellToCast, _castTask.SpellToCast.SpellSlotCost);
                AddMaterialComponentsForSpell(_castTask.SpellToCast, _castTask.SpellToCast.SpellSlotCost);
            }
        }

        #endregion

        #region IAbilityExecutionProcessHandler

        /// <summary>
        /// Needed for interface, but not used
        /// </summary>
        /// <param name="context"></param>
        public void HandleExecutionProcessStart(AbilityExecutionContext context) { }

        /// <summary>
        /// Release retentions and remove the subscription
        /// </summary>
        /// <param name="context"></param>
        public void HandleExecutionProcessEnd(AbilityExecutionContext context) {
            if (Context != null && Context == context) {
                try {
                    // Remove retentions
                    ReleaseAllRetentions();

                    // Reset Spell resistance
                    ResetSpellResistance();

                    RestoreCasterLevel();
                } catch (Exception ex) {
                    Main.Error(ex, "Casting: HandleExecutionProcessEnd");
                } finally {
                    // Remove from event bus
                    EventBus.Unsubscribe(this);
                }
            }
        }

        #endregion

        #region IRulebookEventAboutToTriggerHook

        /// <summary>
        /// This event handler is very handy for watching all the rule book events around casting
        /// </summary>
        /// <param name="rule"></param>
        public void OnBeforeEventAboutToTrigger([NotNull] RulebookEvent rule) {
            if (rule is RuleCastSpell evt) {
                if (_castTask.SpellToCast == evt.Spell && _castTask.Target == evt.SpellTarget) {
                    try {
                        // Set proper context so retentions may be released
                        Context = evt.Context;

                        // Check for needed arcanist reservoir points
                        // Don't spend points if this is an Azata Zippy Magic secondary cast
                        if (ArcaneReservoirPointsNeeded > 0 && !IsAzataZippyMagicSecondaryCast) {
                            if (ArcaneReservoirPointsAvailable >= ArcaneReservoirPointsNeeded) {
                                DecreaseArcanePoolPoints(ArcaneReservoirPointsNeeded);
                            } 
                            else {
                                // Not enough points are available, so cancel the cast
                                Main.Error($"Unable to cast {_castTask.SpellToCast.Name} for {_castTask.Target.Unit.CharacterName} because {ArcaneReservoirPointsNeeded} arcane reservoir points are needed but only {ArcaneReservoirPointsAvailable} arcane reservoir points are available");
                                evt.CancelAbilityExecution();
                                return;
                            }
                        }

                        // Disable the logs for this cast
                        evt.Context.DisableLog = true;
                        evt.DisableBattleLogSelf = true;

                        // Always set to true if controlling Azata Zippy Magic Secondary casts
                        // This prevents the game's secondary cast from triggering, and allows us to control casting
                        if (IsControllingAzataZippyMagicSecondaryCast) {
                            evt.IsDuplicateSpellApplied = true;
                        }

                        // Spend spell slots if requested (e.g. cast directly from a rule trigger)
                        if (_spendSpellSlot) {
                            _castTask.SpellToCast.Spend();
                        }
                    } 
                    catch (Exception ex) {
                        Main.Error(ex, "Casting: OnBeforeRulebookEventTrigger");
                    }
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Set spell modifier retentions
        /// </summary>
        private void SetAllRetentions() {
            if (_castTask.Retentions.ShareTransmutation) _castTask.Caster.State.Features.ShareTransmutation.Retain();
            if (_castTask.Retentions.ImprovedShareTransmutation) _castTask.Caster.State.Features.ImprovedShareTransmutation.Retain();
            if (_castTask.Retentions.PowerfulChange) _castTask.Caster.State.Features.PowerfulChange.Retain();
            if (_castTask.Retentions.ImprovedPowerfulChange) _castTask.Caster.State.Features.ImprovedPowerfulChange.Retain();
        }

        /// <summary>
        /// Release spell modifier retentions
        /// </summary>
        public void ReleaseAllRetentions() {
            if (_castTask.Retentions.ShareTransmutation) _castTask.Caster.State.Features.ShareTransmutation.Release();
            if (_castTask.Retentions.ImprovedShareTransmutation) _castTask.Caster.State.Features.ImprovedShareTransmutation.Release();
            if (_castTask.Retentions.PowerfulChange) _castTask.Caster.State.Features.PowerfulChange.Release();
            if (_castTask.Retentions.ImprovedPowerfulChange) _castTask.Caster.State.Features.ImprovedPowerfulChange.Release();
        }

        private void RemoveSpellResistance() {
            PriorSpellResistance = _castTask.SpellToCast.Blueprint.SpellResistance;
            _castTask.SpellToCast.Blueprint.SpellResistance = false;
        }

        private void ResetSpellResistance() => _castTask.SpellToCast.Blueprint.SpellResistance = PriorSpellResistance;

        /// <summary>
        /// Decrease arcane pool points
        /// </summary>
        /// <param name="amount"></param>
        private void DecreaseArcanePoolPoints(int amount) => ArcaneReservoirPointsAvailable -= amount;

        /// <summary>
        /// Get the Spell Level (in a given spell book) of the spell specified
        /// </summary>
        /// <param name="spell"></param>
        /// <returns></returns>
        private int SpellLevel(AbilityData spell) {
            // Check if this is a converted spell.  A good test example is Magic Weapon, Primary
            if (spell.ConvertedFrom != null) {
                return SpellLevel(spell.ConvertedFrom);
            }

            return ((spell != null) ? spell.Spellbook.GetSpellLevel(spell) : spell.Spellbook.GetMinSpellLevel(spell.Blueprint));
        }

        /// <summary>
        /// Increase the number of casting slots
        /// Used to add spell slots back when controlling Azata Zippy magic secondary buff
        /// </summary>
        /// <param name="spell"></param>
        /// <param name="amount"></param>
        private void IncreaseSpellSlotsAvailable(AbilityData spell, int amount) {
            // Check if this is a converted spell.  A good test example is Magic Weapon, Primary
            if (spell.ConvertedFrom != null) {
                IncreaseSpellSlotsAvailable(spell.ConvertedFrom, amount);
                return;
            }

            // Get the spell level
            var spellLevel = SpellLevel(spell);

            if (spell.Spellbook.Blueprint.Spontaneous) {
                // Increase the number of spontaneous spell slots
                spell.Spellbook.m_SpontaneousSlots[spellLevel] += amount;
            } else {
                // Find spent slots we can reactivate
                var spentSpellSlots = spell.Spellbook?.SureMemorizedSpells(spellLevel)?.Where(x => !x.Available && x.Spell == spell)?.Take(amount);

                // Make sure we found enough spell slots
                if (spentSpellSlots == null || spentSpellSlots.Count() != amount) {
                    return;
                }

                // Iterate through each spell slot reactivating each
                spentSpellSlots.ForEach(x => {
                    // Reactivate the spell slot
                    x.Available = true;

                    // Check for linked spell slots that need to also be reactivated
                    if (x.LinkedSlots != null && x.IsOpposition) {
                        x.LinkedSlots.ToList().ForEach(x => x.Available = true);
                    }
                });
            }
        }

        private void AddMaterialComponentsForSpell(AbilityData spell, int amount) {
            // Check if this is a converted spell.  A good test example is Magic Weapon, Primary
            if (spell.ConvertedFrom != null) {
                AddMaterialComponentsForSpell(spell.ConvertedFrom, amount);
                return;
            }

            // Get the material cost
            if (spell.Blueprint.MaterialComponent != null && spell.Blueprint.MaterialComponent.Item != null) {
                // Get the cost
                var item = spell.Blueprint.MaterialComponent.Item;
                var itemCost = spell.Blueprint.MaterialComponent.Count;

                // Add the cost to the inventory
                if (itemCost > 0) Game.Instance.Player.Inventory.Add(item, itemCost * amount);
            }
        }

        /// <summary>
        /// Change caster level based on modifiers
        /// </summary>
        private void ModifyCasterLevel() {
            var bonus = 0;
            // caster level from arcanist reservoir CL buff ability
            if (_castTask.ReservoirCLBuff) {
                var potent = _castTask.Caster.HasFact(Resources.GetBlueprint<BlueprintFeature>("995110cc948d5164a820403a9e903151"));
                bonus += potent ? 2 : 1;
            }
            _casterLevelModifier = new() {
                ModValue = bonus,
                ModDescriptor = Kingmaker.Enums.ModifierDescriptor.None,
                StackMode = ModifiableValue.StackMode.ForceStack
            };
            _castTask.Caster.Stats.BonusCasterLevel.AddModifier(_casterLevelModifier);
        }

        /// <summary>
        /// Restore caster level to original state
        /// </summary>
        private void RestoreCasterLevel() {
            _castTask.Caster.Stats.BonusCasterLevel.RemoveModifier(_casterLevelModifier);
        }

        #endregion
    }
}