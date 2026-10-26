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
    //
    // The hook is on GameController.LoadLevel, the one call every scene change goes through: the
    // Hub's trials portal doesn't use NextLevelLoader.LoadNextLevel (the first test, and the
    // Ascension mod before it, found that hook silent on the way into a run).
    public static class DiveDirector
    {
        private const string HubScene = "Hub";
        private static int bossesCleared;
        private static bool inRun;
        private static bool loggedNames;
        private static string bossSceneCounted;
        // Set when the dive ends: the game calls LoadLevel more than once per transition, so
        // every call is sent to the Hub until the Hub has actually loaded.
        private static bool returningToHub;

        public static bool IsFinalBoss(string scene)
        {
            return !string.IsNullOrEmpty(scene) && scene.IndexOf("FinalBoss", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void Install(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(LoadLevelPatch)).Patch();
            GameController.levelLoadEventHandlers += OnLevelLoad;
        }

        [HarmonyPatch(typeof(GameController), nameof(GameController.LoadLevel))]
        private static class LoadLevelPatch
        {
            private static void Prefix(ref string givenLevelName)
            {
                try
                {
                    string chosen = OnDestination(givenLevelName ?? "");
                    if (chosen != null && chosen != givenLevelName)
                        givenLevelName = chosen;
                }
                catch (Exception e)
                {
                    ChaosDepthsPlugin.LogError($"dive load: {e.Message}");
                }
            }
        }

        // A scene is about to load: the destination can still be changed. Returns the new one,
        // or null to leave it.
        private static string OnDestination(string destination)
        {
            if (!ChaosDepthsPlugin.DiveEnabled)
                return null;
            // The active scene's own name ('Fire', 'FireBossLevel'), not the display name the
            // GameController reports ('Burning Furnaces'), which the trial checks don't know.
            string current = CurrentScene();
            bool here = IsTrial(current) || (inRun && GameController.stageCount > 0);
            bool there = IsTrial(destination);
            ChaosDepthsPlugin.Log($"Dive: loading '{destination}' from '{current}' (trial here {here}, there {there})");

            if (returningToHub)
            {
                if (destination != HubScene)
                    ChaosDepthsPlugin.Log($"Dive: still returning to the Hub: {destination} -> {HubScene}");
                return HubScene;
            }

            // Nothing comes after the final boss: a dive that reaches it ends in the Hub.
            if (here && IsFinalBoss(current) && destination != HubScene && !IsFinalBoss(destination))
            {
                ChaosDepthsPlugin.Log($"Dive complete after the final boss: {destination} -> {HubScene}");
                EndDive();
                return HubScene;
            }

            if (!here && there && !NextLevelLoader.InBossScene(destination))
            {
                // Starting a run from the Hub: pick the realm and the floor count.
                bossesCleared = 0;
                bossSceneCounted = null;
                inRun = true;
                string result = null;
                string realm = ChaosDepthsPlugin.DiveRealm;
                if (!string.IsNullOrEmpty(realm) && !realm.Equals("Game", StringComparison.OrdinalIgnoreCase))
                {
                    string scene = PutRealmFirst(realm);
                    if (scene != null && scene != destination)
                    {
                        ChaosDepthsPlugin.Log($"Dive: first realm {destination} -> {scene}");
                        result = scene;
                    }
                }
                ApplyFloors("run start");
                return result;
            }

            if (here && NextLevelLoader.InBossScene(current) && there && bossSceneCounted != current && !BossStillAlive(current))
            {
                // Leaving a boss arena for the next realm: maybe the dive is over instead.
                bossSceneCounted = current;
                bossesCleared++;
                int limit = ChaosDepthsPlugin.RealmsPerDive;
                ChaosDepthsPlugin.Log($"Dive: boss {bossesCleared} cleared in '{current}' (dive ends after {(limit > 0 ? limit.ToString() : "all")})");
                if (limit > 0 && bossesCleared >= limit)
                {
                    ChaosDepthsPlugin.Log($"Dive complete: {destination} -> {HubScene}");
                    EndDive();
                    return HubScene;
                }
            }
            return null;
        }

        // Twice a second from the plugin: the game may reset its floor counter, so keep it set
        // for the whole run.
        public static void Tick()
        {
            if (inRun && ChaosDepthsPlugin.DiveEnabled && IsTrial(""))
                ApplyFloors("tick");
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

        // The game's counter read 1 while it played its usual two floors before a boss, so the
        // setting (floors you want) is one more than the counter. If a test shows otherwise, this
        // is the line to change.
        private static void ApplyFloors(string when)
        {
            int floors = ChaosDepthsPlugin.FloorsPerRealm;
            if (floors <= 0)
            {
                if (when == "run start")
                    ChaosDepthsPlugin.Log($"Dive (run start): floors per realm left to the game (its counter is {GameController.stagesPerTier}; set [Dive] FloorsPerRealm to change it)");
                return;
            }
            int counter = Mathf.Max(0, floors - 1);
            int before = GameController.stagesPerTier;
            if (before == counter)
            {
                if (when == "run start")
                    ChaosDepthsPlugin.Log($"Dive (run start): floors per realm {floors}: the game's counter is already {counter}");
                return;
            }
            GameController.stagesPerTier = counter;
            ChaosDepthsPlugin.Log($"Dive ({when}): floors per realm {floors} (the game's counter {before} -> {counter})");
        }

        // Every level load: keep the floor count, and log what the game thinks the run is.
        private static void EndDive()
        {
            inRun = false;
            returningToHub = true;
            try
            {
                GameUI.BroadcastNoticeMessage("Dive complete!", 3f);
            }
            catch
            {
            }
        }

        private static void OnLevelLoad(string next, string previous)
        {
            try
            {
                if (returningToHub && !IsTrial(next))
                    returningToHub = false;
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

        // True when the arena's boss has not been defeated (a forced or debug exit, not a win).
        public static bool BossStillAlive(string scene)
        {
            try
            {
                BossRoomEventHandler room = UnityEngine.Object.FindObjectOfType<BossRoomEventHandler>();
                if (room == null)
                    return false;
                // The handler's defeated flag can read true while the boss stands at full health
                // (the developer skip sets it), so ask the boss itself.
                bool alive = false;
                if (room.boss != null && room.boss.health != null && room.boss.gameObject.activeInHierarchy)
                    alive = room.boss.health.CurrentHealthValue > 0;
                else if (GameController.bosses != null)
                    foreach (GameObject go in GameController.bosses)
                    {
                        Entity e = go != null && go.activeInHierarchy ? go.GetComponent<Entity>() : null;
                        if (e != null && e.health != null && e.health.CurrentHealthValue > 0)
                            alive = true;
                    }
                if (!alive && (room.bossDefeated || room.bossSpawned))
                    return false;
                if (!alive && !room.bossSpawned)
                    ChaosDepthsPlugin.Log($"Dive: leaving '{scene}' before its boss ever appeared; not counted");
                else
                    ChaosDepthsPlugin.Log($"Dive: leaving '{scene}' with its boss alive (health {room.boss?.health?.CurrentHealthValue}); not counted");
                return true;
            }
            catch
            {
                return false;
            }
        }

        // The loaded scene's name, as the trial checks know it.
        public static string CurrentScene()
        {
            try
            {
                string name = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
            catch
            {
            }
            return GameController.currentLevelName ?? GameController.CurrentSceneName ?? "";
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
