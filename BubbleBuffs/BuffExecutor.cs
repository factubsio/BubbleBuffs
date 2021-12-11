using BubbleBuffs.Config;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Controllers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.Log.CombatLog_ThreadSystem;
using Kingmaker.UI.Log.CombatLog_ThreadSystem.LogThreads.Common;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BubbleBuffs {
    public class BubbleBuffGlobalController : MonoBehaviour {

        public static BubbleBuffGlobalController Instance { get; private set; }

        public const int BATCH_SIZE = 8;
        public const float DELAY = 0.05f;

        private void Awake() {
            Instance = this;
        }

        public void Destroy() {
        }

        public void CastSpells(List<CastTask> tasks) {
            Main.Verbose("Doing thing");
            var castingCoroutine = CastSpellsInternal(tasks);
            StartCoroutine(castingCoroutine);
        }

        private void Cast(CastTask task) {
            var oldResistance = task.SpellToCast.Blueprint.SpellResistance;
            task.SpellToCast.Blueprint.SpellResistance = false;

            try {

                if (task.ShareTransmutation) {
                    var toggle = AbilityCache.CasterCache[task.Caster.UniqueId].ShareTransmutation;
                    if (toggle == null || !toggle.Data.IsAvailableForCast) {
                        return;
                    }

                    var toggleParams = toggle.Data.CalculateParams();
                    var context = new AbilityExecutionContext(toggle.Data, toggleParams, new TargetWrapper(task.Caster));
                    toggle.Data.Cast(context);
                    toggle.Data.Spend();
                }

                if (task.PowerfulChange) {
                    var toggle = AbilityCache.CasterCache[task.Caster.UniqueId].PowerfulChange;
                    if (toggle != null && toggle.Data.IsAvailableForCast) {
                        var toggleParams = toggle.Data.CalculateParams();
                        var context = new AbilityExecutionContext(toggle.Data, toggleParams, new TargetWrapper(task.Caster));
                        toggle.Data.Cast(context);
                        toggle.Data.Spend();
                    }
                }


                if (task.IsSticky) {
                    var context = new AbilityExecutionContext(task.SpellToCast, task.Params, Vector3.zero);
                    AbilityExecutionProcess.ApplyEffectImmediate(context, task.Target.Unit);
                } else {
                    var context = new AbilityExecutionContext(task.SpellToCast, task.Params, task.Target);
                    context.FxSpawners?.Clear();
                    context.DisableFx = true;
                    task.SpellToCast.Cast(context);
                }

                task.SlottedSpell.Spend();
            } catch (Exception ex) {
                Main.Error(ex, "casting spell");
            }
            task.SpellToCast.Blueprint.SpellResistance = oldResistance;

        }

        private IEnumerator CastSpellsInternal(List<CastTask> tasks) {
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
    public class BuffExecutor {
        public BufferState State;

        public BuffExecutor(BufferState state) {
            State = state;
        }
        private Dictionary<BuffGroup, float> lastExecutedForGroup = new() {
            { BuffGroup.Long, -1 },
            { BuffGroup.Important, -1 },
            { BuffGroup.Short, -1 },
        };

        public void Execute(BuffGroup buffGroup) {
            if (Game.Instance.Player.IsInCombat && !State.AllowInCombat)
                return;

            var lastExecuted = lastExecutedForGroup[buffGroup];
            if (lastExecuted > 0 && (Time.realtimeSinceStartup - lastExecuted) < .5f) {
                return;
            }
            lastExecutedForGroup[buffGroup] = Time.realtimeSinceStartup;

            Main.Verbose($"Begin buff: {buffGroup}");

            State.Recalculate(false);


            TargetWrapper[] targets = Bubble.Group.Select(u => new TargetWrapper(u)).ToArray();
            int attemptedCasts = 0;
            int skippedCasts = 0;
            int actuallyCast = 0;


            var tooltip = new TooltipTemplateBuffer();


            var unitBuffs = Bubble.Group.Select(u => new UnitBuffData(u)).ToDictionary(bd => bd.Unit.UniqueId);

            List<CastTask> tasks = new();

            foreach (var buff in State.BuffList.Where(b => b.InGroup == buffGroup && b.Fulfilled > 0)) {

                try {
                    int thisBuffGood = 0;
                    int thisBuffBad = 0;
                    int thisBuffSkip = 0;
                    TooltipTemplateBuffer.BuffResult badResult = null;

                    foreach (var (target, caster) in buff.ActualCastQueue) {
                        var forTarget = unitBuffs[target];
                        if (buff.BuffsApplied.IsPresent(forTarget) & !State.OverwriteBuff) {
                            thisBuffSkip++;
                            skippedCasts++;
                            continue;
                        }

                        attemptedCasts++;

                        AbilityData spellToCast;
                        if (!caster.SlottedSpell.IsAvailable) {
                            if (badResult == null)
                                badResult = tooltip.AddBad(buff);
                            badResult.messages.Add($"  [{caster.who.CharacterName}] => [{Bubble.GroupById[target].CharacterName}], {"noslot".i8()}");
                            thisBuffBad++;
                            continue;
                        }
                        var touching = caster.spell.Blueprint.GetComponent<AbilityEffectStickyTouch>();
                        if (touching) {
                            spellToCast = new AbilityData(touching.TouchDeliveryAbility, caster.who);
                            if (caster.spell.MetamagicData != null)
                                spellToCast.MetamagicData = caster.spell.MetamagicData.Clone();
                        } else {
                            spellToCast = caster.spell;
                        }
                        //modifiedSpell.Blueprint.SpellResistance = false;
                        var spellParams = spellToCast.CalculateParams();

                        var task = new CastTask {
                            SlottedSpell = caster.SlottedSpell,
                            Params = spellParams,
                            Target = new TargetWrapper(forTarget.Unit),
                            IsSticky = touching,
                            Caster = caster.who,
                            SpellToCast = spellToCast,
                            PowerfulChange = caster.PowerfulChange,
                            ShareTransmutation = caster.ShareTransmutation,
                        };

                        tasks.Add(task);

                        //if (touching) {
                        //    context = new AbilityExecutionContext(modifiedSpell, spellParams, Vector3.zero);
                        //    AbilityExecutionProcess.ApplyEffectImmediate(context, targets[target].Unit);
                        //} else {
                        //    context = new AbilityExecutionContext(caster.spell, spellParams, targets[target]);
                        //    modifiedSpell.Cast(context);
                        //}

                        //caster.SlottedSpell.Spend();

                        actuallyCast++;
                        thisBuffGood++;
                    }

                    if (thisBuffGood > 0)
                        tooltip.AddGood(buff).count = thisBuffGood;
                    if (thisBuffSkip > 0)
                        tooltip.AddSkip(buff).count = thisBuffSkip;

                } catch (Exception ex) {
                    Main.Error(ex, $"casting buff: {buff.Spell.Name}");
                }
            }

            BubbleBuffGlobalController.Instance.CastSpells(tasks);

            string title = buffGroup.i8();
            var messageString = $"{title} {"log.applied".i8()} {actuallyCast}/{attemptedCasts} ({"log.skipped".i8()} {skippedCasts})";
            Main.Verbose(messageString);

            var message = new CombatLogMessage(messageString, Color.blue, PrefixIcon.RightArrow, tooltip, true);

            var messageLog = LogThreadController.Instance.m_Logs[LogChannelType.Common].First(x => x is MessageLogThread);
            messageLog.AddMessage(message);
        }
    }

    public class CastTask {
        public AbilityData SpellToCast;
        public AbilityData SlottedSpell;
        public AbilityParams Params;
        public bool IsSticky;
        public bool PowerfulChange;
        public bool ShareTransmutation;
        public TargetWrapper Target;
        public UnitEntityData Caster;
    }
}
