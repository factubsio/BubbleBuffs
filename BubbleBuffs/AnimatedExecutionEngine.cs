using BubbleBuffs.Subscriptions;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BubbleBuffs {
    public class AnimatedExecutionEngine : IBuffExecutionEngine {
        private UnitCommand Cast(CastTask task) {
            try {

                if (task.ShareTransmutation) {
                    var toggle = AbilityCache.CasterCache[task.Caster.UniqueId].ShareTransmutation;
                    if (!task.BuffProvider.AzataZippyMagic || (task.BuffProvider.AzataZippyMagic && !task.IsDuplicateSpellApplied)) {
                        if (toggle?.Data.IsAvailableForCast != true) {
                            Main.Error("Unable to cast share transmutation");
                            return null;
                        }

                        toggle.Data.Spend();
                    }

                    var toggleParams = toggle.Data.CalculateParams();
                    var context = new AbilityExecutionContext(toggle.Data, toggleParams, new TargetWrapper(task.Caster));
                    toggle.Data.Cast(context);
                }

                if (task.PowerfulChange) {
                    var toggle = AbilityCache.CasterCache[task.Caster.UniqueId].PowerfulChange;
                    if (!task.BuffProvider.AzataZippyMagic || (task.BuffProvider.AzataZippyMagic && !task.IsDuplicateSpellApplied)) {
                        if (toggle?.Data.IsAvailableForCast != true) {
                            Main.Error("Unable to cast powerful change");
                            return null;
                        }

                        toggle.Data.Spend();
                    }

                    var toggleParams = toggle.Data.CalculateParams();
                    var context = new AbilityExecutionContext(toggle.Data, toggleParams, new TargetWrapper(task.Caster));
                    toggle.Data.Cast(context);
                }

                // Subscribe to the RuleCastSpell event that will be executed by the cast command
                EventBus.Subscribe(new ZippyMagicBeforeRulebookEventTriggerHandler(task));

                return UnitUseAbility.CreateCastCommand(task.SpellToCast, task.Target);
            } 
            catch (Exception ex) {
                Main.Error(ex, "casting spell");
                return null;
            }
        }

        public IEnumerator CreateSpellCastRoutine(List<CastTask> tasks) {
            var byCaster = tasks.GroupBy(task => task.Caster).Select(x => x.GetEnumerator()).ToList();
            UnitCommand[] running = new UnitCommand[byCaster.Count];

            int remaining = byCaster.Count;

            while (byCaster.Any(x => x != null)) {

                for (int i = 0; i < byCaster.Count; i++) {
                    var current = running[i];
                    if (current != null) {
                        if (current.IsFinished) {
                            running[i] = null;
                        }
                        continue;
                    }


                    var queue = byCaster[i];
                    if (queue == null) {
                        continue;
                    }

                    if (!queue.MoveNext()) {
                        byCaster[i] = null;
                        continue;
                    }

                    current = Cast(queue.Current);
                    queue.Current.Caster.Commands.Run(current);
                    running[i] = current;
                    break;
                }

                yield return new WaitForFixedUpdate();

            }
            yield return null;
        }
    }
}
