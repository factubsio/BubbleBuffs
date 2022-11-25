using BubbleBuffs.Extensions;
using BubbleBuffs.Handlers;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
            var tasks_WithRetentions = tasks.Where(x => x.Retentions.Any);
            var batches_WithoutRetentions = tasks.Where(x => !x.Retentions.Any).Chunk(BATCH_SIZE);

            // Batches without retentions
            foreach (var batch in batches_WithoutRetentions) {
                batch.ForEach(task => {
                    Cast(task);
                });

                yield return new WaitForSeconds(DELAY);
            }

            // Batches with retentions
            foreach (var task in tasks_WithRetentions) {
                Cast(task);

                yield return new WaitForSeconds(DELAY);
            }
        }
    }
}