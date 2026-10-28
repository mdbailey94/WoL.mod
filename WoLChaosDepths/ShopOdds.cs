using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLChaosDepths
{
    // For test runs (Matt, 0.15.11): the plaza's arcana shop offers mod arcana more often. Each
    // card the shop puts out (paid in gems) announces itself through SkillStoreItem's spawn
    // event; outside the trials, with [Testing] ShopModArcanaChance, a card showing a vanilla
    // arcana is turned into a locked mod arcana, and the vanilla one goes back for a later card.
    // Mod arcana are LegendAPI's catalog; one hidden or failing its unlock condition (a mod's
    // own switch, e.g. Mod Menu's Rolling Gale) is never offered.
    //
    // 0.15.13: 0.15.11 patched LootManager.GetSkillID with Harmony while the plugin loaded,
    // which ran LootManager's static constructor before the game's asset bundle was ready; it
    // threw, LootManager stayed broken, and the game hung on the title screen. Nothing here
    // touches LootManager until a card spawns in the plaza.
    public static class ShopOdds
    {
        private static ConfigEntry<float> chance;
        private static IDictionary catalog;
        private static FieldInfo hiddenField, unlockField;
        private static bool swapping;

        public static void Install(ConfigFile config)
        {
            chance = config.Bind("Testing", "ShopModArcanaChance", 0.5f,
                new ConfigDescription("For test runs: chance each arcana card in the plaza shop is a mod arcana you haven't unlocked yet, while any are left (0 = the game's own odds).", new AcceptableValueRange<float>(0f, 1f)));
            SkillStoreItem.spawnEventHandlers += OnCardSpawn;
        }

        private static void OnCardSpawn(SkillStoreItem card)
        {
            if (swapping || card == null)
                return;
            try
            {
                // Only the gem-priced cards of locked arcana (the plaza shop), not signatures.
                if (!card.usePlatinumCost || card.signatureOnly || !card.lockedSkillSelected || string.IsNullOrEmpty(card.skillID))
                    return;
                if (chance.Value <= 0f || ChaosDepthsPlugin.InTrialsNow())
                    return;
                IDictionary cat = Catalog();
                if (cat == null || cat.Contains(card.skillID) || UnityEngine.Random.value >= chance.Value)
                    return;
                List<string> pool = LockedModArcana(cat);
                if (pool.Count == 0)
                    return;
                string pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                string old = card.skillID;
                swapping = true;
                try
                {
                    SkillStoreItem.currentSkillIDs.Remove(old);
                    SkillStoreItem.currentSkills.Remove(card);
                    LootManager.lockedSkillList.Add(old);
                    LootManager.availableSkillList.Add(old);
                    LootManager.lockedSkillList.Remove(pick);
                    LootManager.availableSkillList.Remove(pick);
                    card.skillID = pick;
                    SkillStoreItem.currentSkillIDs.Add(pick);
                    card.Initialize();   // picture, name, text and gem price for the new arcana
                }
                finally
                {
                    swapping = false;
                }
                ChaosDepthsPlugin.Log($"Shop: offering mod arcana {pick} in place of {old} (testing, {pool.Count} locked mod arcana were left)");
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"shop odds: {e.Message}");
            }
        }

        // Mod arcana still locked in this save, not on a wizard and not already on a card.
        private static List<string> LockedModArcana(IDictionary cat)
        {
            var pool = new List<string>();
            GameData data = GameDataManager.gameData;
            if (data == null)
                return pool;
            var onWizards = new HashSet<string>();
            Player[] players = GameController.activePlayers;
            if (players != null)
                foreach (Player p in players)
                    if (p != null && p.assignedSkills != null)
                        foreach (Player.SkillState s in p.assignedSkills)
                            if (s != null)
                                onWizards.Add(s.skillID);
            foreach (DictionaryEntry entry in cat)
            {
                string id = entry.Key as string;
                if (string.IsNullOrEmpty(id) || data.CheckSkillDataEntry(id) || !Offerable(entry.Value)
                    || onWizards.Contains(id) || SkillStoreItem.currentSkillIDs.Contains(id))
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
