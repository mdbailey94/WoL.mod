using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WoLChaosDepths
{
    // M0 technical test: can a dive be shaped? Three things the plan needs, each a config setting
    // so the game's own behaviour is the default:
    // - Realm: which elemental realm the run starts in (the game picks the order itself).
    // - FloorsPerRealm: how many floors before that realm's boss (the game's stagesPerTier).
    // - RealmsPerDive: how many realm bosses before the dive ends and you go back to the Hub
    //   (the game runs all realms, then the final boss).
    // Everything the game decides about a run is logged at each level load, so the test shows
    // what it does with the changes.
    public static class DiveDirector
    {
        private const string HubScene = "Hub";
        private static int bossesCleared;
        private static bool inRun;
        private static bool loggedNames;

        public static void Install(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(PortalPatch)).Patch();
            GameController.levelLoadEventHandlers += OnLevelLoad;
        }

        [HarmonyPatch(typeof(NextLevelLoader), nameof(NextLevelLoader.LoadNextLevel))]
        private static class PortalPatch
        {
            private static bool Prefix(NextLevelLoader __instance)
            {
                try
                {
                    OnPortal(__instance);
                }
                catch (Exception e)
                {
                    ChaosDepthsPlugin.LogError($"dive portal: {e.Message}");
                }
                return true;
            }
        }

        // The portal is about to load the game's choice: here it can still be changed.
        private static void OnPortal(NextLevelLoader loader)
        {
            if (!ChaosDepthsPlugin.DiveEnabled)
                return;
            string destination = loader.nextLevelName ?? "";
            bool here = IsTrial("");
            bool there = IsTrial(destination);

            if (!here && there)
            {
                // Starting a run from the Hub: pick the realm and the floor count.
                bossesCleared = 0;
                inRun = true;
                string realm = ChaosDepthsPlugin.DiveRealm;
                if (!string.IsNullOrEmpty(realm) && !realm.Equals("Game", StringComparison.OrdinalIgnoreCase))
                {
                    string scene = PutRealmFirst(realm);
                    if (scene != null && scene != destination)
                    {
                        ChaosDepthsPlugin.Log($"Dive: first realm {destination} -> {scene}");
                        loader.nextLevelName = scene;
                    }
                }
                ApplyFloors("run start");
                return;
            }

            if (here && NextLevelLoader.InBossScene("") && there)
            {
                // Leaving a boss arena for the next realm: maybe the dive is over instead.
                bossesCleared++;
                int limit = ChaosDepthsPlugin.RealmsPerDive;
                ChaosDepthsPlugin.Log($"Dive: boss {bossesCleared} cleared (dive ends after {(limit > 0 ? limit.ToString() : "all")})");
                if (limit > 0 && bossesCleared >= limit)
                {
                    ChaosDepthsPlugin.Log($"Dive complete: {destination} -> {HubScene}");
                    loader.nextLevelName = HubScene;
                    inRun = false;
                    try
                    {
                        GameUI.BroadcastNoticeMessage("Dive complete!", 3f);
                    }
                    catch
                    {
                    }
                }
            }
        }

        // Moves the realm's tier scene to the front of the game's order; returns it, or null.
        private static string PutRealmFirst(string realm)
        {
            List<string> order = GameController.levelNameList;
            if (order == null)
            {
                ChaosDepthsPlugin.Log("Dive: the game has no realm order list to change");
                return null;
            }
            string match = null;
            foreach (string name in order)
            {
                if (name != null && name.IndexOf(realm, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    match = name;
                    break;
                }
            }
            if (match == null)
            {
                ChaosDepthsPlugin.Log($"Dive: no realm named like '{realm}' in [{string.Join(", ", order.ToArray())}]");
                return null;
            }
            order.Remove(match);
            order.Insert(0, match);
            ChaosDepthsPlugin.Log($"Dive: realm order now [{string.Join(", ", order.ToArray())}]");
            return match;
        }

        private static void ApplyFloors(string when)
        {
            int floors = ChaosDepthsPlugin.FloorsPerRealm;
            if (floors <= 0)
                return;
            int before = GameController.stagesPerTier;
            GameController.stagesPerTier = floors;
            if (before != floors)
                ChaosDepthsPlugin.Log($"Dive ({when}): floors per realm {before} -> {floors}");
        }

        // Every level load: keep the floor count, and log what the game thinks the run is.
        private static void OnLevelLoad(string next, string previous)
        {
            try
            {
                if (!ChaosDepthsPlugin.DiveEnabled)
                    return;
                if (inRun && IsTrial(next))
                    ApplyFloors("level load");
                if (IsTrial(previous) && !IsTrial(next))
                    inRun = false;
                string order = GameController.levelNameList != null ? string.Join(", ", GameController.levelNameList.ToArray()) : "none";
                ChaosDepthsPlugin.Log($"Dive: '{previous}' -> '{next}': stage {GameController.stageCount}, tier {GameController.tierCount}, " +
                    $"loop {GameController.loopCount}, floors per realm {GameController.stagesPerTier}, " +
                    $"tier scene '{Safe(() => NextLevelLoader.GetCurrentTierScene(-1))}', " +
                    $"boss scene {NextLevelLoader.InBossScene(next)}, tier scene {NextLevelLoader.InTierScene(next)}, order [{order}]");
                if (!loggedNames && GameController.bossLevelNameDict != null)
                {
                    loggedNames = true;
                    var pairs = new List<string>();
                    foreach (KeyValuePair<string, string> pair in GameController.bossLevelNameDict)
                        pairs.Add($"{pair.Key} -> {pair.Value}");
                    ChaosDepthsPlugin.Log($"Dive: boss scenes: {string.Join(", ", pairs.ToArray())}");
                }
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"dive level load: {e.Message}");
            }
        }

        private static bool IsTrial(string scene)
        {
            try
            {
                return NextLevelLoader.InTierScene(scene) || NextLevelLoader.InBossScene(scene);
            }
            catch
            {
                return false;
            }
        }

        private static string Safe(Func<string> get)
        {
            try
            {
                return get() ?? "";
            }
            catch (Exception e)
            {
                return "? " + e.Message;
            }
        }
    }
}
