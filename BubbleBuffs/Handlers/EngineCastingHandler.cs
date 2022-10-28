using JetBrains.Annotations;
using Kingmaker.Blueprints.Classes;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.Utility;
using System;
using System.Linq;

namespace BubbleBuffs.Handlers {
    public class EngineCastingHandler : IBeforeRulebookEventTriggerHandler<RuleCastSpell>, IAbilityExecutionProcessHandler, IRulebookEventAboutToTriggerHook {
        #region Fields

        private readonly CastTask _castTask;
        private readonly bool _spendSpellSlot;

        #endregion

        #region Properties

        private bool UseShareTransmutation {
            get {
                var casterHasAvailable = _castTask.Caster.HasFact(Resources.GetBlueprint<BlueprintFeature>("c4ed8d1a90c93754eacea361653a7d56"));
                var userSelectedForSpell = _castTask.ShareTransmutation;

                return casterHasAvailable && userSelectedForSpell;
            }
        }

        private bool UseImprovedShareTransmutation {
            get {
                var casterHasAvailable = _castTask.Caster.HasFact(Resources.GetBlueprint<BlueprintFeature>("c94d764d2ce3cd14f892f7c00d9f3a70"));
                var userSelectedForSpell = _castTask.ShareTransmutation;

                return casterHasAvailable && userSelectedForSpell;
            }
        }

        private bool UsePowerfulChange {
            get {
                var casterHasAvailable = _castTask.Caster.HasFact(Resources.GetBlueprint<BlueprintFeature>("5e01e267021bffe4e99ebee3fdc872d1"));
                var userSelectedForSpell = _castTask.PowerfulChange;

                return casterHasAvailable && userSelectedForSpell;
            }
        }

        private bool UseImprovedPowerfulChange {
            get {
                var casterHasAvailable = _castTask.Caster.HasFact(Resources.GetBlueprint<BlueprintFeature>("c94d764d2ce3cd14f892f7c00d9f3a70"));
                var userSelectedForSpell = _castTask.PowerfulChange;

                return casterHasAvailable && userSelectedForSpell;
            }
        }

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
                var points = 0;
                if (_castTask.ShareTransmutation) points++;
                if (_castTask.PowerfulChange) points++;
                return points;
            }
        }

        private bool IsControllingAzataZippyMagicSecondaryCast {
            get {
                // Azata Zippy Magic checks
                var hasAzataZippyMagicFact = _castTask.Caster.HasFact(Resources.GetBlueprint<BlueprintFeature>("30b4200f897ba25419ba3a292aed4053"));
                var isSpellAOE = _castTask.SpellToCast.IsAOE;
                var canCastOnOthers = _castTask.ShareTransmutation || !_castTask.SelfCastOnly;

                return _castTask.AzataZippyMagic && hasAzataZippyMagicFact && !isSpellAOE && canCastOnOthers;
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

            // If this is an Azata Zippy magic secondary cast, then increase the number of spell slots available to offset the spell cast
            if (IsAzataZippyMagicSecondaryCast) {
                IncreaseSpellSlotsAvailable(_castTask.SpellToCast, _castTask.SpellToCast.SpellSlotCost);
            }
        }

        #endregion

        #region IBeforeRulebookEventTriggerHandler<RuleCastSpell>

        public void OnBeforeRulebookEventTrigger(RuleCastSpell evt) {
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
                }
                catch (Exception ex) {
                    Main.Error(ex, "Casting: HandleExecutionProcessEnd");
                }
                finally {
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
        public void OnBeforeEventAboutToTrigger([NotNull] RulebookEvent rule) { }

        #endregion

        #region Methods

        /// <summary>
        /// Set spell modifier retentions
        /// </summary>
        private void SetAllRetentions() {
            if (UseShareTransmutation) _castTask.Caster.State.Features.ShareTransmutation.Retain();
            if (UseImprovedShareTransmutation) _castTask.Caster.State.Features.ImprovedShareTransmutation.Retain();
            if (UsePowerfulChange) _castTask.Caster.State.Features.PowerfulChange.Retain();
            if (UseImprovedPowerfulChange) _castTask.Caster.State.Features.ImprovedPowerfulChange.Retain();
        }

        /// <summary>
        /// Release spell modifier retentions
        /// </summary>
        private void ReleaseAllRetentions() {
            if (UseShareTransmutation) _castTask.Caster.State.Features.ShareTransmutation.Release();
            if (UseImprovedShareTransmutation) _castTask.Caster.State.Features.ImprovedShareTransmutation.Release();
            if (UsePowerfulChange) _castTask.Caster.State.Features.PowerfulChange.Release();
            if (UseImprovedPowerfulChange) _castTask.Caster.State.Features.ImprovedPowerfulChange.Release();
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
                var spentSpellSlots = spell.Spellbook?.SureMemorizedSpells(spellLevel)?.Where(x => x.Available)?.Take(amount);

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

        #endregion
    }
}