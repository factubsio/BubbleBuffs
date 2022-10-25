using Kingmaker.Blueprints.Classes;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using System;
using System.Linq;

namespace BubbleBuffs.Handlers {
    public class EngineCastingHandler : IBeforeRulebookEventTriggerHandler<RuleCastSpell>, IAbilityExecutionProcessHandler {
        #region Fields

        private readonly CastTask _castTask;
        private readonly int _spellSlotsToSpend;

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
                var temp = _castTask.Caster?.Resources?.PersistantResources?.Where(x => x.Blueprint.AssetGuidThreadSafe == "cac948cbbe79b55459459dd6a8fe44ce")?.First();
                if (temp != null) temp.Amount = value;
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

        private bool PriorSpellResistance { get; set; }

        private AbilityExecutionContext Context { get; set; }

        #endregion

        #region Constructors

        public EngineCastingHandler(CastTask castTask, int spellSlotsToSpend = 0) {
            // Set fields
            _castTask = castTask;
            _spellSlotsToSpend = spellSlotsToSpend;

            // Set retentions
            SetAllRetentions();

            // Remove spell resistance
            RemoveSpellResistance();
        }

        #endregion

        #region IBeforeRulebookEventTriggerHandler<RuleCastSpell>

        public void OnBeforeRulebookEventTrigger(RuleCastSpell evt) {
            if (_castTask.SpellToCast == evt.Spell && _castTask.Target == evt.SpellTarget) {
                try {
                    // Set proper context so retentions may be released
                    Context = evt.Context;

                    // Check for needed arcanist reservoir point
                    if (ArcaneReservoirPointsNeeded > 0) {
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

                    // Azata Zippy Magic checks
                    var hasAzataZippyMagicFact = _castTask.Caster.HasFact(Resources.GetBlueprint<BlueprintFeature>("30b4200f897ba25419ba3a292aed4053"));
                    var isSpellAOE = _castTask.SpellToCast.IsAOE;
                    var canCastOnOthers = _castTask.ShareTransmutation || !_castTask.BuffProvider.SelfCastOnly;

                    // Set logs and flags as appropriate
                    evt.Context.DisableLog = true;
                    evt.DisableBattleLogSelf = true;
                    evt.IsDuplicateSpellApplied = _castTask.BuffProvider.AzataZippyMagic && hasAzataZippyMagicFact && !isSpellAOE && canCastOnOthers;

                    // Correct casting slots expended when on zippy magic secondary cast
                    if (_castTask.BuffProvider.AzataZippyMagic && _castTask.IsDuplicateSpellApplied) {
                        // Undo the spell slot spend
                        evt.Spell.ExtraSpellSlotCost = -evt.Spell.SpellSlotCost;

                        // Undo the Arcane Reservoir spend
                        if (ArcaneReservoirPointsNeeded > 0) {
                            IncreaseArcanePoolPoints(ArcaneReservoirPointsNeeded);
                        }
                    }

                    // Spend spell slots if requested (e.g. cast directly from a rule trigger)
                    if (_spellSlotsToSpend != 0) {
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

        public void HandleExecutionProcessStart(AbilityExecutionContext context) { }

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

        #region Methods

        private void SetAllRetentions() {
            if (UseShareTransmutation) _castTask.Caster.State.Features.ShareTransmutation.Retain();
            if (UseImprovedShareTransmutation) _castTask.Caster.State.Features.ImprovedShareTransmutation.Retain();
            if (UsePowerfulChange) _castTask.Caster.State.Features.PowerfulChange.Retain();
            if (UseImprovedPowerfulChange) _castTask.Caster.State.Features.ImprovedPowerfulChange.Retain();
        }

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

        private void IncreaseArcanePoolPoints(int amount) => ArcaneReservoirPointsAvailable += amount;
        private void DecreaseArcanePoolPoints(int amount) => ArcaneReservoirPointsAvailable -= amount;

        #endregion
    }
}