using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleBuffs {
    public class InstantExecutionEngine : IBuffExecutionEngine {
        
        public const int BATCH_SIZE = 8;
        public const float DELAY = 0.05f;

        private void Cast(CastTask task) {
            var oldResistance = task.SpellToCast.Blueprint.SpellResistance;
            task.SpellToCast.Blueprint.SpellResistance = false;
            bool hasShare = false;

            try {

                if (task.ShareTransmutation) {
                    var toggle = AbilityCache.CasterCache[task.Caster.UniqueId].ShareTransmutation;
                    if (toggle?.Data.IsAvailableForCast != true) {
                        return;
                    }

                    toggle.Data.Spend();
                    task.Caster.State.Features.ShareTransmutation.Retain();
                    hasShare = true;
                }

                if (task.PowerfulChange) {
                    var toggle = AbilityCache.CasterCache[task.Caster.UniqueId].PowerfulChange;
                    if (toggle?.Data.IsAvailableForCast != true) {
                        return;
                    }
                    Rulebook.Trigger<RuleCastSpell>(new(toggle.Data, new(task.Caster)) {
                        Context = {
                            DisableLog = true,
                        },
                        DisableBattleLogSelf = true,
                    });
                    toggle.Data.Spend();
                }

                {
                    Rulebook.Trigger<RuleCastSpell>(new(task.SpellToCast, task.Target) {
                        Context = {
                            DisableLog = true,
                        },
                        DisableBattleLogSelf = true,
                    });
                    hasShare = false;
                    task.SlottedSpell.Spend();
                }

            } catch (Exception ex) {
                Main.Error(ex, "casting spell");
            } finally {
                if (hasShare)
                    task.Caster.State.Features.ShareTransmutation.Release();
            }
            task.SpellToCast.Blueprint.SpellResistance = oldResistance;

        }

        public IEnumerator CreateSpellCastRoutine(List<CastTask> tasks) {
            var batchCount = (tasks.Count + BATCH_SIZE - 1) / BATCH_SIZE;
            for (int batch = 0; batch < batchCount; batch++) {
                for (int item = 0; item < BATCH_SIZE; item++) {
                    var index = batch * BATCH_SIZE + item;
                    if (index >= tasks.Count)
                        break;

                    Cast(tasks[index]);
                }

                yield return new WaitForSeconds(DELAY);

            }
            yield return null;
        }

    }
}
