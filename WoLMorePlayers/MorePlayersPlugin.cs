using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WoLMorePlayers
{
    // Experimental, step 1 of more than two players: raises the game's player limit
    // (GameController.maxPlayerCount, 2 in the game) before the game sets itself up, so it can
    // size its own player lists to fit, and logs what the game and its input library (Rewired)
    // report, to find what stands in the way of a third player joining. The lists themselves are
    // left alone (the game may not expect empty slots), and nothing moves the HUD yet.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class MorePlayersPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.moreplayers";
        public const string PluginName = "More Players";
        public const string PluginVersion = "0.1.0";

        private static BepInEx.Logging.ManualLogSource log;
        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<int> playerLimit;
        private float nextReport;
        private string lastReport;

        public static int Limit => Mathf.Clamp(playerLimit?.Value ?? 3, 2, 4);
        public static bool On => modEnabled?.Value ?? false;

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "EXPERIMENTAL: raise the player limit and log what the game does with it.");
            playerLimit = Config.Bind("General", "PlayerLimit", 3,
                new ConfigDescription("How many players the game may have (the game's own limit is 2).",
                    new AcceptableValueRange<int>(2, 4)));
            try
            {
                new Harmony(PluginGuid).CreateClassProcessor(typeof(GameControllerAwakePatch)).Patch();
            }
            catch (Exception e)
            {
                Logger.LogError($"More Players: hook failed to install: {e.Message}");
            }
            // In case the game set itself up before this plugin loaded.
            Raise("plugin load");
            SceneManager.activeSceneChanged += (from, to) =>
            {
                Raise("scene " + to.name);
                nextReport = 0f;
            };
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded (EXPERIMENTAL), limit {Limit}");
        }

        [HarmonyPatch(typeof(GameController), nameof(GameController.Awake))]
        private static class GameControllerAwakePatch
        {
            private static void Prefix() => Raise("before GameController.Awake");
            private static void Postfix() => Raise("after GameController.Awake");
        }

        // Sets the limit, and logs the sizes of the game's per-player lists.
        private static void Raise(string when)
        {
            if (!On)
                return;
            try
            {
                int limit = Limit;
                int before = GameController.maxPlayerCount;
                if (GameController.maxPlayerCount < limit)
                    GameController.maxPlayerCount = limit;
                log?.LogInfo($"More Players ({when}): player limit {before} -> {GameController.maxPlayerCount}; " +
                    $"arrays: players {Len(GameController.playerScripts)}, active {Len(GameController.activePlayers)}, " +
                    $"status bars {Len(GameController.playerStatusBars)}; spellbook cards {SBPlayerPageUI.maxPlayerCardCount}");
            }
            catch (Exception e)
            {
                log?.LogError($"More Players ({when}): {e.Message}");
            }
        }

        private static string Len<T>(T[] array) => array == null ? "none" : array.Length.ToString();

        // Every couple of seconds, if anything changed: the input players, the controllers, and
        // the game's players.
        private void Update()
        {
            if (!On || Time.unscaledTime < nextReport)
                return;
            nextReport = Time.unscaledTime + 2f;
            string report = Report();
            if (report == lastReport)
                return;
            lastReport = report;
            Logger.LogInfo(report);
        }

        private static string Report()
        {
            var text = new StringBuilder("More Players report:");
            try
            {
                IList<Rewired.Player> players = Rewired.ReInput.players.GetPlayers(true);
                text.Append($" input players {players.Count} [");
                foreach (Rewired.Player p in players)
                    text.Append($"{p.id}:{p.name} ({p.controllers.joystickCount} pad{(p.controllers.hasKeyboard ? " +keys" : "")}) ");
                text.Append("];");
                text.Append($" pads connected {Rewired.ReInput.controllers.joystickCount};");
            }
            catch (Exception e)
            {
                text.Append($" input: {e.Message};");
            }
            try
            {
                text.Append($" InputController players {Len(InputController.Players)};");
            }
            catch (Exception e)
            {
                text.Append($" InputController: {e.Message};");
            }
            try
            {
                text.Append($" limit {GameController.maxPlayerCount}; game players [");
                Player[] active = GameController.activePlayers;
                if (active != null)
                {
                    for (int i = 0; i < active.Length; i++)
                        text.Append(active[i] != null ? $"{i}:id{active[i].playerID} " : $"{i}:- ");
                }
                text.Append("]");
            }
            catch (Exception e)
            {
                text.Append($" game: {e.Message}");
            }
            return text.ToString();
        }
    }
}
