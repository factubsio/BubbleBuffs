using BubbleBuffs.Handlers;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleBuffs {
    public class InstantExecutionEngine : IBuffExecutionEngine {
        
        public const int BATCH_SIZE = 8;
        public const float DELAY = 0.05f;

        private RuleCastSpell Cast(CastTask task) {
            try {
                // Subscribe to the RuleCastSpell event that will be executed by the trigger
                EventBus.Subscribe(new EngineCastingHandler(task, true));

                // Trigger the RuleCastSpell
                return Rulebook.Trigger<RuleCastSpell>(new(task.SpellToCast, task.Target));
            } 
            catch (Exception ex) {
                Main.Error(ex, "Instant Engine Casting");
                return null;
            } 
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