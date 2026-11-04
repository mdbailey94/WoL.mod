using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;

namespace WoLChaosDepths
{
    /// <summary>
    /// Arcana whose mod has been taken out (#43: the M4 rifle; Momentum Spear, 2026-11-03). The game, finding an arcana it
    /// doesn't know in a wizard's saved loadout, resets the whole loadout to the starting one.
    /// Just before it checks, each removed arcana is swapped for the game's own default in that
    /// slot type, so the rest of the loadout stays as it was.
    /// </summary>
    public static class RemovedArcana
    {
        // Removed arcana ID -> the vanilla arcana that takes its place.
        private static readonly Dictionary<string, string> Replacements = new Dictionary<string, string>
        {
            { "M4Basic", Player.WindSlashBasic.staticID },
            { "MomentumSpearBasic", Player.WindSlashBasic.staticID },   // Matt 2026-11-03: Giant Air Spear replaces it
        };

        private static ManualLogSource log;

        public static void Install(Harmony harmony, ManualLogSource logger)
        {
            log = logger;
            harmony.Patch(AccessTools.Method(typeof(Player), nameof(Player.InitSkills)),
                new HarmonyMethod(typeof(RemovedArcana), nameof(BeforeInitSkills)));
        }

        private static void BeforeInitSkills(Player __instance)
        {
            try
            {
                var skills = __instance.playerData?.skills;
                if (skills == null)
                    return;
                for (int i = 0; i < skills.Length; i++)
                {
                    string replacement;
                    if (skills[i] == null || !Replacements.TryGetValue(skills[i], out replacement))
                        continue;
                    log?.LogInfo($"Loadout: {skills[i]} was removed; player {__instance.playerID + 1} gets {replacement} in its slot");
                    skills[i] = replacement;
                }
            }
            catch (Exception e)
            {
                log?.LogWarning($"Loadout: couldn't replace removed arcana ({e.Message})");
            }
        }
    }
}
