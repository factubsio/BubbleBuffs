﻿using BubbleBuffs.Config;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Controllers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UI.Models.Log.CombatLog_ThreadSystem;
using Kingmaker.UI.Models.Log.CombatLog_ThreadSystem.LogThreads.Common;
using Kingmaker.UnitLogic;
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

    public interface IBuffExecutionEngine {
        public IEnumerator CreateSpellCastRoutine(List<CastTask> tasks);
    }
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
            var castingCoroutine = Engine.CreateSpellCastRoutine(tasks);
            StartCoroutine(castingCoroutine);
        }

        public static IBuffExecutionEngine Engine =>
            GlobalBubbleBuffer.Instance.SpellbookController.state.VerboseCasting 
                ? new AnimatedExecutionEngine() 
                : new InstantExecutionEngine();
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

            Dictionary<UnitEntityData, int> remainingArcanistPool = new Dictionary<UnitEntityData, int>();
            BlueprintScriptableObject arcanistPoolBlueprint = ResourcesLibrary.TryGetBlueprint<BlueprintScriptableObject>("cac948cbbe79b55459459dd6a8fe44ce");

            foreach (var buff in State.BuffList.Where(b => b.InGroup == buffGroup && b.Fulfilled > 0)) {

                try {
                    int thisBuffGood = 0;
                    int thisBuffBad = 0;
                    int thisBuffSkip = 0;
                    TooltipTemplateBuffer.BuffResult badResult = null;

                    foreach (var (target, caster) in buff.ActualCastQueue) {
                        var forTarget = unitBuffs[target];
                        if (buff.BuffsApplied.IsPresent(forTarget, buff.IgnoreForOverwriteCheck) && !State.OverwriteBuff) {
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

                        // Azata Zippy Magic
                        var priorSpellTasks = tasks.Where(x => x.Caster == caster.who && x.SlottedSpell.UniqueId == caster.SlottedSpell.UniqueId).ToList();
                        
                        // Check to see if this spell does count for casting
                        if (!caster.AzataZippyMagic || (caster.AzataZippyMagic && priorSpellTasks.Count() % 2 == 0)) {
                            int neededArcanistPool = 0;
                            if (caster.PowerfulChange) {
                                var PowerfulChangeRssLogic = AbilityCache.CasterCache[caster.who.UniqueId]?.PowerfulChange?.GetComponent<AbilityResourceLogic>();
                                var PowerfulChangeCost = PowerfulChangeRssLogic ? PowerfulChangeRssLogic.CalculateCost(caster.spell) : 1;
                                neededArcanistPool += Math.Max(0, PowerfulChangeCost);
                            }
                            if (caster.ShareTransmutation && caster.who != forTarget.Unit) {
                                var ShareTransmutationRssLogic = AbilityCache.CasterCache[caster.who.UniqueId]?.ShareTransmutation?.GetComponent<AbilityResourceLogic>();
                                var ShareTransmutationCost = ShareTransmutationRssLogic ? ShareTransmutationRssLogic.CalculateCost(caster.spell) : 1;
                                neededArcanistPool += Math.Max(0, ShareTransmutationCost);
                            }
                            if (caster.ReservoirCLBuff) {
                                var ReservoirCLBuffRssLogic = AbilityCache.CasterCache[caster.who.UniqueId]?.ReservoirCLBuff?.GetComponent<AbilityResourceLogic>();
                                var ReservoirCLBuffCost = ReservoirCLBuffRssLogic ? ReservoirCLBuffRssLogic.CalculateCost(caster.spell) : 1;
                                neededArcanistPool += Math.Max(0, ReservoirCLBuffCost);
                            }

                            if (neededArcanistPool != 0) {
                                int availableArcanistPool;
                                if (remainingArcanistPool.ContainsKey(caster.who)) {
                                    availableArcanistPool = remainingArcanistPool[caster.who];
                                } else {
                                    availableArcanistPool = caster.who.Resources.GetResourceAmount(arcanistPoolBlueprint);
                                }
                                if (availableArcanistPool < neededArcanistPool) {
                                    if (badResult == null)
                                        badResult = tooltip.AddBad(buff);
                                    badResult.messages.Add($"  [{caster.who.CharacterName}] => [{Bubble.GroupById[target].CharacterName}], {"noarcanist".i8()}");
                                    thisBuffBad++;
                                    continue;
                                } else {
                                    remainingArcanistPool[caster.who] = availableArcanistPool - neededArcanistPool;
                                }
                            }
                        }

                        // This is a free cast
                        var IsDuplicateSpellApplied = false;
                        if (caster.AzataZippyMagic && priorSpellTasks.Count() % 2 == 1) {
                            IsDuplicateSpellApplied = true;
                        }


                        var touching = caster.spell.Blueprint.GetComponent<AbilityEffectStickyTouch>();
                        Main.Verbose("Adding cast task for: " + caster.spell.Name, "apply");
                        if (touching) {
                            Main.Verbose("   Switching spell to touch => " + touching.TouchDeliveryAbility.Name, "apply");
                            spellToCast = new AbilityData(caster.spell, touching.TouchDeliveryAbility);
                        } else {
                            spellToCast = caster.spell;
                        }
                        var spellParams = spellToCast.CalculateParams();

                        var task = new CastTask {
                            SlottedSpell = caster.SlottedSpell,
                            Target = new TargetWrapper(forTarget.Unit),
                            Caster = caster.who,
                            SpellToCast = spellToCast,
                            PowerfulChange = caster.PowerfulChange,
                            ShareTransmutation = caster.ShareTransmutation,
                            ReservoirCLBuff = caster.ReservoirCLBuff,
                            AzataZippyMagic = caster.AzataZippyMagic,
                            IsDuplicateSpellApplied = IsDuplicateSpellApplied,
                            SelfCastOnly = caster.SelfCastOnly
                        };

                        tasks.Add(task);

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

            var messageLog = LogThreadService.Instance.m_Logs[LogChannelType.Common].First(x => x is MessageLogThread);
            messageLog.AddMessage(message);
        }
    }
    //castTask.Retentions.Any
    public class CastTask {
        public AbilityData SpellToCast;
        public AbilityData SlottedSpell;
        public bool PowerfulChange;
        public bool ShareTransmutation;
        public bool ReservoirCLBuff;
        public bool AzataZippyMagic;
        public bool IsDuplicateSpellApplied;
        public TargetWrapper Target;
        public UnitEntityData Caster;
        public bool SelfCastOnly;

        public Retentions Retentions {
            get {
                return new Retentions(this);
            }
        }
    }

    public class Retentions {
        private CastTask _castTask;

        public Retentions(CastTask castTask) {
            _castTask = castTask;
        }

        public bool ShareTransmutation {
            get {
                var casterHasAvailable = _castTask.Caster.HasFact(Resources.GetBlueprint<BlueprintFeature>("c4ed8d1a90c93754eacea361653a7d56"));
                var userSelectedForSpell = _castTask.ShareTransmutation;

                return casterHasAvailable && userSelectedForSpell;
            }
        }

        public bool ImprovedShareTransmutation {
            get {
                var casterHasAvailable = _castTask.Caster.HasFact(Resources.GetBlueprint<BlueprintFeature>("c94d764d2ce3cd14f892f7c00d9f3a70"));
                var userSelectedForSpell = _castTask.ShareTransmutation;

                return casterHasAvailable && userSelectedForSpell;
            }
        }

        public bool PowerfulChange {
            get {
                var casterHasAvailable = _castTask.Caster.HasFact(Resources.GetBlueprint<BlueprintFeature>("5e01e267021bffe4e99ebee3fdc872d1"));
                var userSelectedForSpell = _castTask.PowerfulChange;

                return casterHasAvailable && userSelectedForSpell;
            }
        }

        public bool ImprovedPowerfulChange {
            get {
                var casterHasAvailable = _castTask.Caster.HasFact(Resources.GetBlueprint<BlueprintFeature>("c94d764d2ce3cd14f892f7c00d9f3a70"));
                var userSelectedForSpell = _castTask.PowerfulChange;

                return casterHasAvailable && userSelectedForSpell;
            }
        }

        public bool Any {
            get {
                return ShareTransmutation || ImprovedShareTransmutation || PowerfulChange || ImprovedPowerfulChange;
            }
        }
    }
}
