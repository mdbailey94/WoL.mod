using System;
using HarmonyLib;

namespace WoLExtendedStats
{
    // Each patch swallows its own exceptions: a stats mod must never break the game.

    [HarmonyPatch(typeof(Fall), nameof(Fall.TransitionToFall))]
    internal static class FallPatch
    {
        private static void Postfix(Fall __instance)
        {
            try
            {
                RunTracker.OnEntityFall(__instance.entityScript ?? __instance.GetComponent<Entity>());
            }
            catch (Exception e)
            {
                ExtendedStatsPlugin.LogError("fall", e);
            }
        }
    }

    [HarmonyPatch(typeof(Player.BaseDashState), nameof(Player.BaseDashState.OnEnter))]
    internal static class DashPatch
    {
        private static void Postfix(Player.BaseDashState __instance)
        {
            try
            {
                RunTracker.OnDash(__instance.parent);
            }
            catch (Exception e)
            {
                ExtendedStatsPlugin.LogError("dash", e);
            }
        }
    }

    // ResetStats(true) clears the game's whole-run totals, i.e. a new run is starting.
    [HarmonyPatch(typeof(PlayerEndStats), nameof(PlayerEndStats.ResetStats))]
    internal static class ResetStatsPatch
    {
        private static void Postfix(PlayerEndStats __instance, bool resetTotals)
        {
            if (resetTotals)
                RunTracker.ResetRun(__instance);
        }
    }

    // The end-of-run screen (death or victory). Snapshot before it activates so nothing
    // the game does while showing it can change our numbers.
    [HarmonyPatch(typeof(DeathSummaryUI), nameof(DeathSummaryUI.Activate))]
    internal static class SummaryShowPatch
    {
        private static void Prefix(DeathSummaryUI __instance)
        {
            try
            {
                ExtendedStatsPlugin.ShowRunSummary(__instance);
            }
            catch (Exception e)
            {
                ExtendedStatsPlugin.LogError("summary", e);
            }
        }
    }

    [HarmonyPatch(typeof(DeathSummaryUI), nameof(DeathSummaryUI.Deactivate))]
    internal static class SummaryHidePatch
    {
        private static void Prefix() => ExtendedStatsPlugin.HideRunSummary();
    }
}
