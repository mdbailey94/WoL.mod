using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WoLSlingshotDash
{
    // The HUD's arcana icons only show the normal dash's cooldown. While a slingshot recharges,
    // this drives the game's own cooldown display for that icon (darkened icon, border, countdown
    // and fill) right after the game updates it each frame, then hands it back once it's ready.
    [HarmonyPatch(typeof(CooldownEntry), nameof(CooldownEntry.EntryUpdate))]
    public static class SlingshotHud
    {
        private static readonly HashSet<CooldownEntry> driven = new HashSet<CooldownEntry>();
        private static bool loggedError;
        private static bool loggedShown;

        private static void Postfix(CooldownEntry __instance)
        {
            try
            {
                Apply(__instance);
            }
            catch (Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    SlingshotDashPlugin.Log($"Slingshot HUD failed: {e}");
                }
            }
        }

        private static void Apply(CooldownEntry entry)
        {
            Player.SkillState state = entry.skillState;
            float remaining, total;
            if (state == null || !ChargedDashState.TryGetCooldown(state.parent, state.skillID, out remaining, out total))
            {
                // Recharged: give the icon back to the game.
                if (driven.Remove(entry) && entry.remainingCD <= 0f)
                    entry.SetReady();
                return;
            }
            // The normal dash's own cooldown is longer: let the game show that.
            if (entry.remainingCD >= remaining)
                return;

            entry.remainingCD = remaining;
            entry.SetCooldown();
            if (entry.skillBGOverlay != null)
                entry.skillBGOverlay.fillAmount = Mathf.Clamp01(remaining / Mathf.Max(0.01f, total));
            if (entry.skillCooldownText != null)
            {
                entry.skillCooldownText.enabled = true;
                entry.skillCooldownText.text = Mathf.CeilToInt(remaining).ToString();
            }
            driven.Add(entry);
            if (!loggedShown)
            {
                loggedShown = true;
                SlingshotDashPlugin.Log($"HUD: showing the slingshot cooldown on {state.skillID} " +
                    $"(overlay {(entry.skillBGOverlay != null ? entry.skillBGOverlay.type.ToString() : "none")}, " +
                    $"text {(entry.skillCooldownText != null ? "yes" : "none")})");
            }
        }
    }
}
