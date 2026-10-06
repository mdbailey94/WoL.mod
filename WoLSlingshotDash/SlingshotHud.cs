using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WoLSlingshotDash
{
    // On a slingshot arcana's HUD icon, only the slingshot's own cooldown shows: the game's
    // display (darkened icon, fill, countdown) is driven from it right after the game updates the
    // icon each frame, and the normal dash's cooldown and its "ready" flash are hidden. The flash
    // plays when the slingshot is ready instead.
    public static class SlingshotHud
    {
        private static readonly HashSet<CooldownEntry> cooling = new HashSet<CooldownEntry>();
        private static bool allowPing;
        private static bool loggedError;
        private static bool loggedShown;

        public static void Install(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(EntryPatch)).Patch();
            harmony.CreateClassProcessor(typeof(PingPatch)).Patch();
        }

        public static bool IsSlingshot(string skillID) =>
            skillID == SlingshotDashState.staticID || skillID == BlazingSlingshotState.staticID
            || skillID == FrostSlingshotState.staticID || skillID == VineSlingshotState.staticID
            || skillID == StormSlingshotState.staticID;

        [HarmonyPatch(typeof(CooldownEntry), nameof(CooldownEntry.EntryUpdate))]
        private static class EntryPatch
        {
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
        }

        // The normal dash's "ready" flash is hidden for slingshot arcana (ours plays instead).
        [HarmonyPatch(typeof(CooldownUI), nameof(CooldownUI.RequestCDPing))]
        private static class PingPatch
        {
            private static bool Prefix(string skillID) => allowPing || !IsSlingshot(skillID);
        }

        private static void Apply(CooldownEntry entry)
        {
            Player.SkillState state = entry.skillState;
            if (state == null || !IsSlingshot(state.skillID))
            {
                cooling.Remove(entry);
                return;
            }

            float remaining, total;
            if (ChargedDashState.TryGetCooldown(state.parent, state.skillID, out remaining, out total))
            {
                entry.remainingCD = remaining;
                entry.remainingChargeCD = 0f;
                entry.SetCooldown();
                if (entry.skillBGOverlay != null)
                    entry.skillBGOverlay.fillAmount = Mathf.Clamp01(remaining / Mathf.Max(0.01f, total));
                if (entry.skillCooldownText != null)
                {
                    entry.skillCooldownText.enabled = true;
                    entry.skillCooldownText.text = Mathf.CeilToInt(remaining).ToString();
                }
                cooling.Add(entry);
                if (!loggedShown)
                {
                    loggedShown = true;
                    SlingshotDashPlugin.Log($"HUD: showing the slingshot cooldown on {state.skillID}");
                }
                return;
            }

            // Slingshot ready: always show the icon as ready, whatever the normal dash is doing.
            entry.remainingCD = 0f;
            entry.remainingChargeCD = 0f;
            entry.SetReady();
            if (cooling.Remove(entry) && entry.cooldownUI != null)
            {
                allowPing = true;
                try
                {
                    entry.cooldownUI.RequestCDPing(state.skillID);
                }
                finally
                {
                    allowPing = false;
                }
            }
        }
    }
}
