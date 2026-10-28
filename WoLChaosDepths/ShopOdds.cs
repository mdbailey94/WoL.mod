using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WoLChaosDepths
{
    // For test runs (Matt, 0.15.11): the plaza's arcana shop offers mod arcana more often. The
    // shop's cards (paid in gems) each take a locked arcana from LootManager.GetSkillID(locked);
    // outside the trials, with [Testing] ShopModArcanaChance, a card that drew a vanilla arcana
    // gets a locked mod arcana instead, and the vanilla one goes back for a later card. Mod
    // arcana are LegendAPI's catalog; one hidden or failing its unlock condition (a mod's own
    // switch, e.g. Mod Menu's Rolling Gale) is never offered.
    public static class ShopOdds
    {
        private static ConfigEntry<float> chance;
        private static IDictionary catalog;
        private static FieldInfo hiddenField, unlockField;

        public static void Install(ConfigFile config, Harmony harmony)
        {
            chance = config.Bind("Testing", "ShopModArcanaChance", 0.5f,
                new ConfigDescription("For test runs: chance each arcana card in the plaza shop is a mod arcana you haven't unlocked yet, while any are left (0 = the game's own odds).", new AcceptableValueRange<float>(0f, 1f)));
            MethodInfo m = AccessTools.Method(typeof(LootManager), nameof(LootManager.GetSkillID));
            harmony.Patch(m, postfix: new HarmonyMethod(typeof(ShopOdds), nameof(AfterGetSkillID)));
        }

        private static void AfterGetSkillID(bool locked, bool signature, ref string __result)
        {
            try
            {
                if (!locked || signature || chance.Value <= 0f || ChaosDepthsPlugin.InTrialsNow())
                    return;
                if (!string.IsNullOrEmpty(__result) && Catalog() != null && Catalog().Contains(__result))
                    return;
                if (UnityEngine.Random.value >= chance.Value)
                    return;
                List<string> pool = LockedModArcana();
                if (pool.Count == 0)
                    return;
                string pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                if (!string.IsNullOrEmpty(__result))
                {
                    LootManager.lockedSkillList.Add(__result);
                    LootManager.availableSkillList.Add(__result);
                }
                LootManager.lockedSkillList.Remove(pick);
                LootManager.availableSkillList.Remove(pick);
                ChaosDepthsPlugin.Log($"Shop: offering mod arcana {pick} in place of {(string.IsNullOrEmpty(__result) ? "nothing" : __result)} (testing, {pool.Count} locked mod arcana left)");
                __result = pick;
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"shop odds: {e.Message}");
            }
        }

        // Mod arcana still locked in this save, not on a wizard and not already on a card.
        private static List<string> LockedModArcana()
        {
            var pool = new List<string>();
            IDictionary cat = Catalog();
            GameData data = GameDataManager.gameData;
            if (cat == null || data == null)
                return pool;
            foreach (DictionaryEntry entry in cat)
            {
                string id = entry.Key as string;
                if (string.IsNullOrEmpty(id) || data.CheckSkillDataEntry(id) || !Offerable(entry.Value)
                    || LootManager.currentPlayerSkills.Contains(id) || SkillStoreItem.currentSkillIDs.Contains(id))
                    continue;
                pool.Add(id);
            }
            return pool;
        }

        private static bool Offerable(object info)
        {
            if (info == null)
                return false;
            if (hiddenField == null)
            {
                hiddenField = info.GetType().GetField("hidden");
                unlockField = info.GetType().GetField("unlockCondition");
            }
            if (hiddenField != null && (bool)hiddenField.GetValue(info))
                return false;
            var condition = unlockField != null ? unlockField.GetValue(info) as Func<bool> : null;
            return condition == null || condition();
        }

        // LegendAPI keeps its catalog internal: read it by reflection, once.
        private static IDictionary Catalog()
        {
            if (catalog != null)
                return catalog;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = a.GetType("LegendAPI.Skills", false);
                FieldInfo f = t?.GetField("SkillCatalog", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null)
                    return catalog = f.GetValue(null) as IDictionary;
            }
            return null;
        }
    }
}
