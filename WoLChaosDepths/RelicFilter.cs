using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    // Matt, 2026-11-02: no relic should do what a cloak talent does; where one does, the relic
    // leaves Chaos Depths and the talent stays (Log - Player 20:10 audit, 20:20 decision). While
    // Phase 1 is on, these relics are taken out of the game's pools of relics it can offer (shops,
    // chests, drops: LootManager.availableItemIDList and lockedItemIDList). The lists are rebuilt
    // by the game (a new run, unlocks), so they're cleaned on every level load and checked again
    // every couple of seconds. LootManager itself isn't patched. Relics are matched by their class
    // name, so their IDs don't need to be known. One already owned stays owned.
    public static class RelicFilter
    {
        private static readonly HashSet<string> Removed = new HashSet<string>
        {
            "OverhealGrantsShield", "CritAfterEvade", "ResetCDChance", "DamageUpWithDash", "RelicDiscount",
            "Vampiricism", "VampireSoul", "OverdriveOnTakeDamage", "ArmorUpAtLowHealth", "DamageUpAtLowHealth",
            "GlassCannon", "MaxHPDownDamageUpPercent", "DeathProofCrown", "IgnoreDeathChance", "LimitedGuard",
            "FirstHitDamageUp", "DamageUpWithCC", "DamageUpWithKills", "OverdriveToResetCDs", "IgnoreHurtKnockbackDown",
            "MaxHealthUpRegen",
            // 20:20's unique-stat nodes: Lucky Strike, Deep Reserves / Second Gust.
            "DoubleAttackChance", "ExtraSkillCharge",
        };
        private const float CheckEvery = 2f;
        private static readonly List<string> ids = new List<string>();
        private static bool logged;

        public static void Install()
        {
            var host = new GameObject("ChaosDepths RelicFilter");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<Runner>();
            GameController.levelLoadEventHandlers += (next, previous) => Clean();
        }

        private class Runner : MonoBehaviour
        {
            private float next;

            private void Update()
            {
                if (Time.unscaledTime < next)
                    return;
                next = Time.unscaledTime + CheckEvery;
                Clean();
            }
        }

        private static void Clean()
        {
            if (!PhaseOne.On || !ChaosDepthsPlugin.InProfile)
                return;
            try
            {
                if (ids.Count == 0)
                {
                    if (LootManager.completeItemDict == null || LootManager.completeItemDict.Count == 0)
                        return;
                    foreach (KeyValuePair<string, Item> pair in LootManager.completeItemDict)
                        if (pair.Value != null && Removed.Contains(pair.Value.GetType().Name))
                            ids.Add(pair.Key);
                }
                int taken = Take(LootManager.availableItemIDList) + Take(LootManager.lockedItemIDList);
                if (!logged && ids.Count > 0)
                {
                    logged = true;
                    ChaosDepthsPlugin.Log($"Relic filter: {ids.Count} relics that copy cloak talents are out of Chaos Depths' pools " +
                        $"({string.Join(", ", ids.ToArray())}){(taken > 0 ? $", {taken} taken out now" : "")}");
                }
            }
            catch (Exception e)
            {
                if (!logged)
                {
                    logged = true;
                    ChaosDepthsPlugin.LogError($"Relic filter: {e.Message}");
                }
            }
        }

        private static int Take(List<string> list)
        {
            if (list == null)
                return 0;
            int taken = 0;
            foreach (string id in ids)
                while (list.Remove(id))
                    taken++;
            return taken;
        }
    }
}
