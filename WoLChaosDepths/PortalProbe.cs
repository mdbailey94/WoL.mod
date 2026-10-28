using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WoLChaosDepths
{
    // Task #33 (the Hub's trials portal ignored Interact on 0.13.6): on every Interact press by a
    // wizard outside the trials, logs each level loader (portal or exit pad) within reach and
    // what the game's NextLevelLoader.Update checks before it loads: whether the loader has
    // caught the wizard (its 'player'), the distance (it lets go past 2 units), whether the
    // wizard IsAvailable (not 'Paused' or 'TeleToLoc') and whether it was already entered.
    public static class PortalProbe
    {
        private static ConfigEntry<bool> on;
        private static readonly FieldInfo loaderPlayer = AccessTools.Field(typeof(NextLevelLoader), "player");
        private static readonly FieldInfo loaderEntered = AccessTools.Field(typeof(NextLevelLoader), "portalEntered");

        public static void Bind(ConfigFile config)
        {
            on = config.Bind("Debug", "PortalLog", true,
                "Log what the game's level loaders (portals, exit pads) see on each Interact press outside the trials, to find why one ignores it (#33).");
        }

        public static void Update(bool inTrials)
        {
            if (on == null || !on.Value || inTrials)
                return;
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            foreach (Player p in players)
            {
                if (p == null || p.inputDevice == null || p.inputDevice.rewiredPlayer == null)
                    continue;
                bool pressed;
                try
                {
                    pressed = p.inputDevice.rewiredPlayer.GetButtonDown("Interact");
                }
                catch
                {
                    continue;
                }
                if (pressed)
                    Report(p);
            }
        }

        private static void Report(Player p)
        {
            try
            {
                Vector2 at = p.transform.position;
                string state = p.fsm != null ? p.fsm.currentStateName : "?";
                int found = 0;
                foreach (NextLevelLoader loader in UnityEngine.Object.FindObjectsOfType<NextLevelLoader>())
                {
                    if (loader == null)
                        continue;
                    float d = Vector2.Distance(at, loader.transform.position);
                    if (d > 5f)
                        continue;
                    found++;
                    object caught = loaderPlayer != null ? loaderPlayer.GetValue(loader) : null;
                    object entered = loaderEntered != null ? loaderEntered.GetValue(loader) : null;
                    ChaosDepthsPlugin.Log($"Portal probe: player {p.playerID + 1} pressed Interact {d:0.00} u from {loader.GetType().Name} '{loader.name}' " +
                        $"(to '{loader.nextLevelName}', active {loader.isActiveAndEnabled}, has caught a wizard: {(caught as Player) != null}" +
                        $"{((caught as Player) != null && (caught as Player) != p ? " (another one)" : "")}, entered {entered}); " +
                        $"wizard state '{state}', available {p.IsAvailable}, paused {TimeScaleController.paused}, at ({at.x:0.0}, {at.y:0.0})");
                }
                if (found == 0)
                    ChaosDepthsPlugin.Log($"Portal probe: player {p.playerID + 1} pressed Interact at ({at.x:0.0}, {at.y:0.0}) in state '{state}' with no level loader within 5 u");
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"portal probe: {e.Message}");
            }
        }
    }
}
