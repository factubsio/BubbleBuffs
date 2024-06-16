﻿using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.ElementsSystem;
using Kingmaker.Localization;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using BubbleBuffs.Utilities;
using UnityEngine;
using static Kingmaker.Blueprints.Classes.Prerequisites.Prerequisite;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.UnitLogic.Mechanics.Conditions;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.EntitySystem;
using Kingmaker.UnitLogic.Abilities.Components.AreaEffects;
using BubbleBuffs;

namespace BubbleBuffs.Extensions {
    static class AbilityExtensions {

        public static char Initial(this Metamagic flag) {
            switch (flag) {
                case Metamagic.Empower:
                    return 'E';
                case Metamagic.Maximize:
                    return 'M';
                case Metamagic.Quicken:
                    return 'Q';
                case Metamagic.Extend:
                    return 'X';
                case Metamagic.Heighten:
                    return 'H';
                case Metamagic.Reach:
                    return 'R';
                case Metamagic.CompletelyNormal:
                    return 'N';
                case Metamagic.Persistent:
                    return 'P';
                case Metamagic.Selective:
                    return 'S';
                case Metamagic.Bolstered:
                    return 'B';
                default:
                    return '?';
            }
        }

        public static bool IsMetamagicked(this AbilityData data) => data.MetamagicData?.NotEmpty ?? false;

        public static IEnumerable<Metamagic> GetMetamagicks(this AbilityData data) {
            foreach (Metamagic maybe in Enum.GetValues(typeof(Metamagic))) {
                if (data.MetamagicData.Has(maybe)) {
                    yield return maybe;
                }
            }

        }

        public static bool IsLong(this ContextActionApplyBuff action) {
            return action.Permanent || (action.UseDurationSeconds ? action.DurationSeconds >= 60 : action.DurationValue.Rate != DurationRate.Rounds);
        }
        public static bool IsLong(this ContextActionEnchantWornItem action) {
            return action.Permanent || action.DurationValue.Rate != DurationRate.Rounds;
        }

        public static Guid BGuid(this EntityFact fact) {
            return fact.Blueprint.AssetGuid.m_Guid;
        }
    }

    static class ExtentionMethods {

        private static void LogVerbose(int level, string message) {
#if DEBUG && false
            Main.Log($"{level.Indent()} {message}");
#endif

        }

        public static IEnumerable<IBeneficialEffect> GetBeneficialBuffs(this GameAction action, int level = 0) {
            if (action != null) {
                LogVerbose(level, $"Extracting buffs from: {action.name}, {action.GetType().Name}");
                if (action is ContextActionApplyBuff applyBuff && applyBuff.Buff.IsBeneficial(level + 1)) {
                    yield return new BuffEffect(applyBuff);
                    LogVerbose(level + 1, $"FOUND: applyBuff {action.name}");
                } else if (action is ContextActionsOnPet enchantPet) {
                    LogVerbose(level + 1, $"FOUND: enchantPet {action.name}");
                    foreach (var subEffect in enchantPet.Actions.Actions.Where(a => a != null).SelectMany(a => a.GetBeneficialBuffs(level + 1))) {
                        subEffect.PetType = enchantPet.PetType;
                        yield return subEffect;
                    }
                } else if (action is ContextActionEnchantWornItem enchantItem) {
                    LogVerbose(level + 1, $"FOUND: enchantItem {action.name}");
                    yield return new WornItemEnchantmentEffect(enchantItem);
                } else if (action.GetType().Name.Equals("ContextActionApplyBuffRanks")) {
                    // This is from TabletopTweaks-Core. Since it's all reflection don't bother with the full logic,
                    // just add it, treat it as long, and let users hide if they want.
                    LogVerbose(level + 1, $"FOUND: applyBuffRanks {action.name}");
                    var buffRef = (BlueprintBuffReference) action.GetType().GetField("m_Buff").GetValue(action);
                    yield return new BuffEffect(buffRef.deserializedGuid.m_Guid);
                } else if (action is ContextActionPartyMembers applyParty) {
                    LogVerbose(level, $"recursing into partyMembers");
                    foreach (var subEffect in applyParty.Action.Actions.Where(a => a != null).SelectMany(a => a.GetBeneficialBuffs(level + 1)))
                        yield return subEffect;
                } else if (action is ContextActionSpawnAreaEffect spawnArea) {
                    LogVerbose(level, $"recursing into spawnArea");
                    if (spawnArea.AreaEffect.TryGetComponent<AbilityAreaEffectBuff>(out var areaBuff) && areaBuff.Buff.IsBeneficial(level + 1)) {
                        LogVerbose(level, $"FOUND: areaBuff {areaBuff.name}");
                        yield return new AreaBuffEffect(areaBuff, spawnArea.DurationValue.Rate != DurationRate.Rounds);
                    }
                } else if (action is Conditional maybe) {
                    bool takeYes = true;
                    bool takeNo = true;
                    LogVerbose(level, $"recursing into Conditional");
                    foreach (var c in maybe.ConditionsChecker.Conditions) {
                        if (c is ContextConditionIsAlly ally) {
                            if (ally.Not)
                                takeYes = false;
                            else
                                takeNo = false;
                        }
                    }
                    if (takeNo) {
                        LogVerbose(level, $"recursing into ifFalse");
                        foreach (var b in maybe.IfFalse.Actions.SelectMany(a => a.GetBeneficialBuffs(level + 1)))
                            yield return b;
                    }
                    if (takeYes) {
                        LogVerbose(level, $"recursing into ifTrue");
                        foreach (var b in maybe.IfTrue.Actions.SelectMany(a => a.GetBeneficialBuffs(level + 1)))
                            yield return b;
                    }
                } else if (action is ContextActionCastSpell spellCast) {
                    LogVerbose(level, $"recursing into spellCast");
                    foreach (var b in spellCast.Spell.GetBeneficialBuffs(level + 1))
                        yield return b;
                }
            }
        }

        private static string[] indents = MakeIndents();

        private static string[] MakeIndents() {
            string[] ret = new string[100];
            ret[0] = "";
            for (int i = 1; i < ret.Length; i++)
                ret[i] = ret[i - 1] + "   ";
            return ret;
        }

        public static string Indent(this int level) {
            return indents[Math.Min(level, indents.Length - 1)];
        }

        public static bool IsBeneficial(this BlueprintBuff buff, int level = 0) {
            var contextApply = buff.GetComponent<AddFactContextActions>();
            if (contextApply == null)
                return true;

            if (contextApply.Activated?.Actions?.Any(action => action is ContextActionSavingThrow) ?? false) {
                return false;
            }

            return true;
        }

        public static BlueprintAbility DeTouchify(this BlueprintAbility spell) {
            if (spell.TryGetComponent<AbilityEffectStickyTouch>(out var touch))
                return touch.TouchDeliveryAbility;
            else
                return spell;
        }

        public static IEnumerable<IBeneficialEffect> GetBeneficialBuffs(this BlueprintAbility spell, int level = 0) {
            LogVerbose(level, $"getting buffs for spell: {spell.Name}");
            spell = spell.DeTouchify();
            LogVerbose(level, $"detouchified-to: {spell.Name}");
            if (spell.TryGetComponent<AbilityEffectRunAction>(out var runAction)) {
                return runAction.Actions.Actions.SelectMany(a => a.GetBeneficialBuffs(level + 1));
            } else {
                return new IBeneficialEffect[] { };
            }
        }


        public static bool IsMass(this BlueprintAbility spell) {
            spell = spell.DeTouchify();
            if (spell.HasComponent<AbilityTargetsAround>())
                return true;

            if (spell.TryGetComponent<AbilityEffectRunAction>(out var run)) {
                if (run.Actions.Actions.Any(a => a != null && a is ContextActionSpawnAreaEffect))
                    return true;

            }

            return false;

        }

        public static bool HasComponent<T>(this BlueprintScriptableObject bp) => bp.GetComponent<T>() != null;
        public static bool HasComponents<T1, T2>(this BlueprintScriptableObject bp) => bp.HasComponent<T1>() && bp.HasComponent<T2>();
        public static bool HasComponents<T1, T2, T3>(this BlueprintScriptableObject bp) => bp.HasComponent<T1>() && bp.HasComponent<T2>() && bp.HasComponent<T3>();
        public static bool HasComponents<T1, T2, T3, T4>(this BlueprintScriptableObject bp) => bp.HasComponent<T1>() && bp.HasComponent<T2>() && bp.HasComponent<T3>() && bp.HasComponent<T4>();
        public static bool HasAnyComponents<T1, T2>(this BlueprintScriptableObject bp) => bp.HasComponent<T1>() || bp.HasComponent<T2>();
        public static bool HasAnyComponents<T1, T2, T3>(this BlueprintScriptableObject bp) => bp.HasComponent<T1>() || bp.HasComponent<T2>() || bp.HasComponent<T3>();
        public static bool HasAnyComponents<T1, T2, T3, T4>(this BlueprintScriptableObject bp) => bp.HasComponent<T1>() || bp.HasComponent<T2>() || bp.HasComponent<T3>() || bp.HasComponent<T4>();

        public static bool TryGetComponent<T>(this BlueprintScriptableObject bp, out T component) {
            component = bp.GetComponent<T>();
            return component != null;
        }

          public static IEnumerable<GameAction> FlattenAllActions(this BlueprintScriptableObject blueprint, Func<Action, bool> descend = null) {
            List<GameAction> actions = new List<GameAction>();
            foreach (var component in blueprint.ComponentsArray) {
                Type type = component.GetType();
                var foundActions = AccessTools.GetDeclaredFields(type)
                    .Where(f => f.FieldType == typeof(ActionList))
                    .SelectMany(field => ((ActionList)field.GetValue(component)).Actions);
                actions.AddRange(FlattenAllActions(foundActions));
            }
            return actions;
        }



        public static IEnumerable<GameAction> FlattenAllActions(this IEnumerable<GameAction> actions, Func<Action, bool> descend = null) {
            List<GameAction> newActions = new List<GameAction>();
            foreach (var action in actions) {
                Type type = action?.GetType();
                var foundActions = AccessTools.GetDeclaredFields(type)?
                    .Where(f => f?.FieldType == typeof(ActionList))
                    .SelectMany(field => ((ActionList)field.GetValue(action)).Actions);
                if (foundActions != null) { newActions.AddRange(foundActions); }
            }
            if (newActions.Count > 0) {
                return actions.Concat(FlattenAllActions(newActions));
            }
            return actions;
        }
        public static IEnumerable<BlueprintAbility> AbilityAndVariants(this BlueprintAbility ability) {
            var List = new List<BlueprintAbility>() { ability };
            var varriants = ability.GetComponent<AbilityVariants>();
            if (varriants != null) {
                List.AddRange(varriants.Variants);
            }
            return List;
        }
        public static V PutIfAbsent<K, V>(this IDictionary<K, V> self, K key, V value) where V : class {
            V oldValue;
            if (!self.TryGetValue(key, out oldValue)) {
                self.Add(key, value);
                return value;
            }
            return oldValue;
        }

        public static V PutIfAbsent<K, V>(this IDictionary<K, V> self, K key, Func<V> ifAbsent) where V : class {
            V value;
            if (!self.TryGetValue(key, out value)) {
                self.Add(key, value = ifAbsent());
                return value;
            }
            return value;
        }

        public static T[] AppendToArray<T>(this T[] array, T value) {
            var len = array.Length;
            var result = new T[len + 1];
            Array.Copy(array, result, len);
            result[len] = value;
            return result;
        }

        public static T[] RemoveFromArrayByType<T, V>(this T[] array) {
            List<T> list = new List<T>();

            foreach (var c in array) {
                if (!(c is V)) {
                    list.Add(c);
                }
            }

            return list.ToArray();
        }

        public static T[] AppendToArray<T>(this T[] array, params T[] values) {
            var len = array.Length;
            var valueLen = values.Length;
            var result = new T[len + valueLen];
            Array.Copy(array, result, len);
            Array.Copy(values, 0, result, len, valueLen);
            return result;
        }

        public static T[] AppendToArray<T>(this T[] array, IEnumerable<T> values) => AppendToArray(array, values.ToArray());

        public static T[] InsertBeforeElement<T>(this T[] array, T value, T element) {
            var len = array.Length;
            var result = new T[len + 1];
            int x = 0;
            bool added = false;
            for (int i = 0; i < len; i++) {
                if (array[i].Equals(element) && !added) {
                    result[x++] = value;
                    added = true;
                }
                result[x++] = array[i];
            }
            return result;
        }

        public static T[] InsertAfterElement<T>(this T[] array, T value, T element) {
            var len = array.Length;
            var result = new T[len + 1];
            int x = 0;
            bool added = false;
            for (int i = 0; i < len; i++) {
                if (array[i].Equals(element) && !added) {
                    result[x++] = array[i];
                    result[x++] = value;
                    added = true;
                } else {
                    result[x++] = array[i];
                }

            }
            return result;
        }

        public static T[] RemoveFromArray<T>(this T[] array, T value) {
            var list = array.ToList();
            return list.Remove(value) ? list.ToArray() : array;
        }

        public static string StringJoin<T>(this IEnumerable<T> array, Func<T, string> map, string separator = " ") => string.Join(separator, array.Select(map));

        public static void SetFeatures(this BlueprintFeatureSelection selection, IEnumerable<BlueprintFeature> features) {
            SetFeatures(selection, features.ToArray());
        }

        public static void SetFeatures(this BlueprintFeatureSelection selection, params BlueprintFeature[] features) {
            selection.m_AllFeatures = selection.m_Features = features.Select(bp => bp.ToReference<BlueprintFeatureReference>()).ToArray();
        }

        public static void RemoveFeatures(this BlueprintFeatureSelection selection, params BlueprintFeature[] features) {
            foreach (var feature in features) {
                var featureReference = feature.ToReference<BlueprintFeatureReference>();
                if (selection.m_AllFeatures.Contains(featureReference)) {
                    selection.m_AllFeatures = selection.m_AllFeatures.Where(f => !f.Equals(featureReference)).ToArray();
                }
                if (selection.m_Features.Contains(featureReference)) {
                    selection.m_Features = selection.m_Features.Where(f => !f.Equals(featureReference)).ToArray();
                }
            }
            selection.m_AllFeatures = selection.m_AllFeatures.OrderBy(feature => feature.Get().Name).ToArray();
            selection.m_Features = selection.m_Features.OrderBy(feature => feature.Get().Name).ToArray();
        }

        public static void AddFeatures(this BlueprintFeatureSelection selection, params BlueprintFeature[] features) {
            foreach (var feature in features) {
                var featureReference = feature.ToReference<BlueprintFeatureReference>();
                if (!selection.m_AllFeatures.Contains(featureReference)) {
                    selection.m_AllFeatures = selection.m_AllFeatures.AppendToArray(featureReference);
                }
                if (!selection.m_Features.Contains(featureReference)) {
                    selection.m_Features = selection.m_Features.AppendToArray(featureReference);
                }
            }
            selection.m_AllFeatures = selection.m_AllFeatures.OrderBy(feature => feature.Get().Name).ToArray();
            selection.m_Features = selection.m_Features.OrderBy(feature => feature.Get().Name).ToArray();
        }
        public static void AddPrerequisiteFeature(this BlueprintFeature obj, BlueprintFeature feature) {
            obj.AddPrerequisiteFeature(feature, GroupType.All);
        }
        public static void AddPrerequisiteFeature(this BlueprintFeature obj, BlueprintFeature feature, GroupType group) {
            obj.AddComponent(Helpers.Create<PrerequisiteFeature>(c => {
                c.m_Feature = feature.ToReference<BlueprintFeatureReference>();
                c.Group = group;
            }));
            if (feature.IsPrerequisiteFor == null) { feature.IsPrerequisiteFor = new List<BlueprintFeatureReference>(); }
            if (!feature.IsPrerequisiteFor.Contains(obj.ToReference<BlueprintFeatureReference>())) {
                feature.IsPrerequisiteFor.Add(obj.ToReference<BlueprintFeatureReference>());
            }
        }
        public static void AddPrerequisiteFeaturesFromList(this BlueprintFeature obj, int amount, params BlueprintFeature[] features) {
            obj.AddPrerequisiteFeaturesFromList(amount, GroupType.All, features);
        }
        public static void AddPrerequisiteFeaturesFromList(this BlueprintFeature obj, int amount, GroupType group = GroupType.All, params BlueprintFeature[] features) {
            obj.AddComponent(Helpers.Create<PrerequisiteFeaturesFromList>(c => {
                c.m_Features = features.Select(f => f.ToReference<BlueprintFeatureReference>()).ToArray();
                c.Amount = amount;
                c.Group = group;
            }));
            features.ForEach(feature => {
                if (feature.IsPrerequisiteFor == null) { feature.IsPrerequisiteFor = new List<BlueprintFeatureReference>(); }
                if (!feature.IsPrerequisiteFor.Contains(obj.ToReference<BlueprintFeatureReference>())) {
                    feature.IsPrerequisiteFor.Add(obj.ToReference<BlueprintFeatureReference>());
                }
            });
        }

        public static void AddPrerequisite<T>(this BlueprintFeature obj, Action<T> init = null) where T : Prerequisite, new() {
            obj.AddPrerequisite(Helpers.Create(init));
        }

        public static void AddPrerequisite<T>(this BlueprintFeature obj, T prerequisite) where T : Prerequisite {
            obj.AddComponent(prerequisite);
            switch (prerequisite) {
                case PrerequisiteFeature p:
                    var feature = p.Feature;
                    if (feature.IsPrerequisiteFor == null) { feature.IsPrerequisiteFor = new List<BlueprintFeatureReference>(); }
                    if (!feature.IsPrerequisiteFor.Contains(obj.ToReference<BlueprintFeatureReference>())) {
                        feature.IsPrerequisiteFor.Add(obj.ToReference<BlueprintFeatureReference>());
                    }
                    break;
                case PrerequisiteFeaturesFromList p:
                    var features = p.Features;
                    features.ForEach(f => {
                        if (f.IsPrerequisiteFor == null) { f.IsPrerequisiteFor = new List<BlueprintFeatureReference>(); }
                        if (!f.IsPrerequisiteFor.Contains(obj.ToReference<BlueprintFeatureReference>())) {
                            f.IsPrerequisiteFor.Add(obj.ToReference<BlueprintFeatureReference>());
                        }
                    });
                    break;
                default:
                    break;
            }
        }

        public static void AddPrerequisites<T>(this BlueprintFeature obj, params T[] prerequisites) where T : Prerequisite {
            foreach (var prerequisite in prerequisites) {
                obj.AddPrerequisite(prerequisite);
            }
        }

        public static void RemovePrerequisite<T>(this BlueprintFeature obj, T prerequisite) where T : Prerequisite {
            obj.RemoveComponent(prerequisite);
            switch (prerequisite) {
                case PrerequisiteFeature p:
                    var feature = p.Feature;
                    if (feature.IsPrerequisiteFor == null) { feature.IsPrerequisiteFor = new List<BlueprintFeatureReference>(); }
                    break;
                case PrerequisiteFeaturesFromList p:
                    var features = p.Features;
                    features.ForEach(f => {
                        if (f.IsPrerequisiteFor == null) { f.IsPrerequisiteFor = new List<BlueprintFeatureReference>(); }
                        f.IsPrerequisiteFor.RemoveAll(v => v.Guid == obj.AssetGuid);
                    });
                    break;
                default:
                    break;
            }
        }

        public static void RemovePrerequisites<T>(this BlueprintFeature obj, params T[] prerequisites) where T : Prerequisite {
            foreach (var prerequisite in prerequisites) {
                obj.RemovePrerequisite(prerequisite);
            }
        }

        public static void RemovePrerequisites<T>(this BlueprintFeature obj, Predicate<T> predicate) where T : Prerequisite {
            foreach (var prerequisite in obj.GetComponents<T>()) {
                if (predicate(prerequisite)) {
                    obj.RemovePrerequisite(prerequisite);
                }
            }
        }

        public static void RemovePrerequisites<T>(this BlueprintFeature obj) where T : Prerequisite {
            foreach (var prerequisite in obj.GetComponents<T>()) {
                obj.RemovePrerequisite(prerequisite);
            }
        }

        public static void InsertComponent(this BlueprintScriptableObject obj, int index, BlueprintComponent component) {
            var components = obj.ComponentsArray.ToList();
            components.Insert(index, component);
            obj.SetComponents(components);
        }

        public static void AddComponent(this BlueprintScriptableObject obj, BlueprintComponent component) {
            obj.SetComponents(obj.ComponentsArray.AppendToArray(component));
        }

        public static void AddComponent<T>(this BlueprintScriptableObject obj, Action<T> init = null) where T : BlueprintComponent, new() {
            obj.SetComponents(obj.ComponentsArray.AppendToArray(Helpers.Create(init)));
        }

        public static void RemoveComponent(this BlueprintScriptableObject obj, BlueprintComponent component) {
            obj.SetComponents(obj.ComponentsArray.RemoveFromArray(component));
        }

        public static void RemoveComponents<T>(this BlueprintScriptableObject obj) where T : BlueprintComponent {
            var compnents_to_remove = obj.GetComponents<T>().ToArray();
            foreach (var c in compnents_to_remove) {
                obj.SetComponents(obj.ComponentsArray.RemoveFromArray(c));
            }
        }

        public static void RemoveComponents<T>(this BlueprintScriptableObject obj, Predicate<T> predicate) where T : BlueprintComponent {
            var compnents_to_remove = obj.GetComponents<T>().ToArray();
            foreach (var c in compnents_to_remove) {
                if (predicate(c)) {
                    obj.SetComponents(obj.ComponentsArray.RemoveFromArray(c));
                }
            }
        }

        public static void AddComponents(this BlueprintScriptableObject obj, IEnumerable<BlueprintComponent> components) => AddComponents(obj, components.ToArray());

        public static void AddComponents(this BlueprintScriptableObject obj, params BlueprintComponent[] components) {
            var c = obj.ComponentsArray.ToList();
            c.AddRange(components);
            obj.SetComponents(c.ToArray());
        }

        public static void SetComponents(this BlueprintScriptableObject obj, params BlueprintComponent[] components) {
            // Fix names of components. Generally this doesn't matter, but if they have serialization state,
            // then their name needs to be unique.
            var names = new HashSet<string>();
            foreach (var c in components) {
                if (string.IsNullOrEmpty(c.name)) {
                    c.name = $"${c.GetType().Name}";
                }
                if (!names.Add(c.name)) {
                    String name;
                    for (int i = 0; !names.Add(name = $"{c.name}${i}"); i++) ;
                    c.name = name;
                }
            }
            obj.ComponentsArray = components;
            obj.OnEnable(); // To make sure components are fully initialized
        }

        public static void SetComponents(this BlueprintScriptableObject obj, IEnumerable<BlueprintComponent> components) {
            SetComponents(obj, components.ToArray());
        }

        public static T CreateCopy<T>(this T original, Action<T> action = null) where T : UnityEngine.Object {
            var clone = UnityEngine.Object.Instantiate(original);
            if (action != null) {
                action(clone);
            }
            return clone;
        }

        public static void SetNameDescription(this BlueprintUnitFact feature, String displayName, String description) {
            feature.SetName(Helpers.CreateString(feature.name + ".Name", displayName));
            feature.SetDescription(description);
        }

        public static void SetNameDescription(this BlueprintUnitFact feature, BlueprintUnitFact other) {
            feature.m_DisplayName = other.m_DisplayName;
            feature.m_Description = other.m_Description;
        }

        public static void SetName(this BlueprintUnitFact feature, LocalizedString name) {
            feature.m_DisplayName = name;
        }

        public static void SetName(this BlueprintUnitFact feature, String name) {
            feature.m_DisplayName = Helpers.CreateString(feature.name + ".Name", name);
        }

        public static void SetDescriptionUntagged(this BlueprintUnitFact feature, String description) {
            feature.m_Description = Helpers.CreateString(feature.name + ".Description", description);
        }

        public static void SetDescription(this BlueprintUnitFact feature, LocalizedString description) {
            feature.m_Description = description;
            //blueprintUnitFact_set_Description(feature) = description;
        }

        public static void SetDescription(this BlueprintUnitFact feature, String description) {
            var taggedDescription = DescriptionTools.TagEncyclopediaEntries(description);
            feature.m_Description = Helpers.CreateString(feature.name + ".Description", taggedDescription);
        }

        public static bool HasFeatureWithId(this LevelEntry level, String id) {
            return level.Features.Any(f => HasFeatureWithId(f, id));
        }

        public static bool HasFeatureWithId(this BlueprintUnitFact fact, String id) {
            if (fact.AssetGuid == id) return true;
            foreach (var c in fact.ComponentsArray) {
                var addFacts = c as AddFacts;
                if (addFacts != null) return addFacts.Facts.Any(f => HasFeatureWithId(f, id));
            }
            return false;
        }

        public static void FixDomainSpell(this BlueprintAbility spell, int level, string spellListId) {
            var spellList = Resources.GetBlueprint<BlueprintSpellList>(spellListId);
            var spells = spellList.SpellsByLevel.First(s => s.SpellLevel == level).Spells;
            spells.Clear();
            spells.Add(spell);
        }


        public static bool HasAreaEffect(this BlueprintAbility spell) {
            return spell.AoERadius.Meters > 0f || spell.ProjectileType != AbilityProjectileType.Simple;
        }

        internal static IEnumerable<BlueprintComponent> WithoutSpellComponents(this IEnumerable<BlueprintComponent> components) {
            return components.Where(c => !(c is SpellComponent) && !(c is SpellListComponent));
        }

        internal static int GetCost(this BlueprintAbility.MaterialComponentData material) {
            var item = material?.Item;
            return item == null ? 0 : item.Cost * material.Count;
        }

        public static AddConditionImmunity CreateImmunity(this UnitCondition condition) {
            var b = new AddConditionImmunity() {
                Condition = condition
            };
            return b;
        }

        public static AddCondition CreateAddCondition(this UnitCondition condition) {
            var a = new AddCondition() {
                Condition = condition
            };
            return a;
        }

        public static BuffDescriptorImmunity CreateBuffImmunity(this SpellDescriptor spell) {
            var b = new BuffDescriptorImmunity() {
                Descriptor = spell
            };
            return b;
        }

        public static SpellImmunityToSpellDescriptor CreateSpellImmunity(this SpellDescriptor spell) {
            var s = new SpellImmunityToSpellDescriptor() {
                Descriptor = spell
            };
            return s;
        }

        public static void AddAction(this Kingmaker.UnitLogic.Abilities.Components.AbilityEffectRunAction action, Kingmaker.ElementsSystem.GameAction game_action) {
            if (action.Actions != null) {
                action.Actions = Helpers.CreateActionList(action.Actions.Actions);
                action.Actions.Actions = action.Actions.Actions.AppendToArray(game_action);
            } else {
                action.Actions = Helpers.CreateActionList(game_action);
            }
        }

        public static void ReplaceComponents(this BlueprintScriptableObject blueprint, Predicate<BlueprintComponent> predicate, BlueprintComponent newComponent) {
            bool found = false;
            foreach (var component in blueprint.ComponentsArray) {
                if (predicate(component)) {
                    blueprint.SetComponents(blueprint.ComponentsArray.RemoveFromArray(component));
                    found = true;
                }
            }
            if (found) {
                blueprint.AddComponent(newComponent);
            }
        }

        public static void ReplaceComponents<T>(this BlueprintScriptableObject blueprint, BlueprintComponent newComponent) where T : BlueprintComponent {
            blueprint.ReplaceComponents<T>(c => true, newComponent);
        }

        public static void ReplaceComponents<T>(this BlueprintScriptableObject blueprint, Predicate<T> predicate, BlueprintComponent newComponent) where T : BlueprintComponent {
            var components = blueprint.GetComponents<T>().ToArray();
            bool found = false;
            foreach (var c in components) {
                if (predicate(c)) {
                    blueprint.SetComponents(blueprint.ComponentsArray.RemoveFromArray(c));
                    found = true;
                }
            }
            if (found) {
                blueprint.AddComponent(newComponent);
            }
        }

        public static bool TryGetString(this LocalizedString lstring, out string str) {
            if (!lstring?.IsEmpty() ?? false) {
                str = lstring;
                return true;
            } else {
                str = "<null>";
                return false;
            }
        }
    }
}

namespace System.Linq {
    /// <summary>
    /// From dotnet https://github.com/dotnet/runtime/blob/main/src/libraries/System.Linq/src/System/Linq/Chunk.cs
    /// </summary>
    public static partial class DotNetEnumerable {
        /// <summary>
        /// Split the elements of a sequence into chunks of size at most <paramref name="size"/>.
        /// </summary>
        /// <remarks>
        /// Every chunk except the last will be of size <paramref name="size"/>.
        /// The last chunk will contain the remaining elements and may be of a smaller size.
        /// </remarks>
        /// <param name="source">
        /// An <see cref="IEnumerable{T}"/> whose elements to chunk.
        /// </param>
        /// <param name="size">
        /// Maximum size of each chunk.
        /// </param>
        /// <typeparam name="TSource">
        /// The type of the elements of source.
        /// </typeparam>
        /// <returns>
        /// An <see cref="IEnumerable{T}"/> that contains the elements the input sequence split into chunks of size <paramref name="size"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="source"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="size"/> is below 1.
        /// </exception>
        public static IEnumerable<TSource[]> Chunk<TSource>(this IEnumerable<TSource> source, int size) {
            if (source == null) {
                Main.Error(new ArgumentNullException(), "Batch source is empty");
                //ThrowHelper.ThrowArgumentNullException(ExceptionArgument.source);
            }

            if (size < 1) {
                Main.Error(new ArgumentOutOfRangeException(), "Size must be greater than 0");
                //ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.size);
            }

            return ChunkIterator(source, size);
        }

        private static IEnumerable<TSource[]> ChunkIterator<TSource>(IEnumerable<TSource> source, int size) {
            using IEnumerator<TSource> e = source.GetEnumerator();

            // Before allocating anything, make sure there's at least one element.
            if (e.MoveNext()) {
                // Now that we know we have at least one item, allocate an initial storage array. This is not
                // the array we'll yield.  It starts out small in order to avoid significantly overallocating
                // when the source has many fewer elements than the chunk size.
                int arraySize = Math.Min(size, 4);
                int i;
                do {
                    var array = new TSource[arraySize];

                    // Store the first item.
                    array[0] = e.Current;
                    i = 1;

                    if (size != array.Length) {
                        // This is the first chunk. As we fill the array, grow it as needed.
                        for (; i < size && e.MoveNext(); i++) {
                            if (i >= array.Length) {
                                arraySize = (int)Math.Min((uint)size, 2 * (uint)array.Length);
                                Array.Resize(ref array, arraySize);
                            }

                            array[i] = e.Current;
                        }
                    } else {
                        // For all but the first chunk, the array will already be correctly sized.
                        // We can just store into it until either it's full or MoveNext returns false.
                        TSource[] local = array; // avoid bounds checks by using cached local (`array` is lifted to iterator object as a field)
                        Debug.Assert(local.Length == size);
                        for (; (uint)i < (uint)local.Length && e.MoveNext(); i++) {
                            local[i] = e.Current;
                        }
                    }

                    if (i != array.Length) {
                        Array.Resize(ref array, i);
                    }

                    yield return array;
                }
                while (i >= size && e.MoveNext());
            }
        }
    }
}
