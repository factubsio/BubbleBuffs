using Kingmaker.Blueprints.Classes;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BubbleBuffs.Subscriptions {
    public class ZippyMagicBeforeRulebookEventTriggerHandler : IBeforeRulebookEventTriggerHandler<RuleCastSpell> {
        private readonly CastTask _castTask;

        public ZippyMagicBeforeRulebookEventTriggerHandler(CastTask castTask) {
            _castTask = castTask;
        }

        public void OnBeforeRulebookEventTrigger(RuleCastSpell evt) {
            if (_castTask.SpellToCast == evt.Spell && _castTask.Target == evt.SpellTarget) {
                try {
                    var hasAzataZippyMagicFact = _castTask.Caster.HasFact(Resources.GetBlueprint<BlueprintFeature>("30b4200f897ba25419ba3a292aed4053"));
                    var isSpellAOE = _castTask.SpellToCast.IsAOE;
                    var canCastOnOthers = _castTask.ShareTransmutation || !_castTask.BuffProvider.SelfCastOnly;

                    evt.Context.DisableLog = true;
                    evt.DisableBattleLogSelf = true;
                    evt.IsDuplicateSpellApplied = _castTask.BuffProvider.AzataZippyMagic && hasAzataZippyMagicFact && !isSpellAOE && canCastOnOthers;

                    if (_castTask.BuffProvider.AzataZippyMagic && _castTask.IsDuplicateSpellApplied) {
                        evt.Spell.ExtraSpellSlotCost = -evt.Spell.SpellSlotCost;
                    }
                }
                finally {
                    EventBus.Unsubscribe(this);
                }
            }
        }
    }
}
