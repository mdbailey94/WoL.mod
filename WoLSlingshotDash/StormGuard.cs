using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WoLSlingshotDash
{
    // While Storm Slingshot's Mag Sphere holds projectiles, they can't hit (or break on) the wizard
    // who's charging, other players, or anything on their side. The projectiles themselves are left
    // exactly as they are, so the Mag Sphere still treats them as enemy projectiles and does
    // everything to them that it normally does.
    public static class StormGuard
    {
        // Held projectile -> the player holding it, and the same for its attack box.
        private static readonly Dictionary<Projectile, Player> heldProjectiles = new Dictionary<Projectile, Player>();
        private static readonly Dictionary<Attack, Player> heldAttacks = new Dictionary<Attack, Player>();
        private static bool loggedError;
        private static bool loggedBlock;

        public static void Install(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(AttackEnterPatch)).Patch();
            harmony.CreateClassProcessor(typeof(AttackExecutePatch)).Patch();
            harmony.CreateClassProcessor(typeof(ProjectileEnterPatch)).Patch();
        }

        public static bool IsHeld(Projectile p) => p != null && heldProjectiles.ContainsKey(p);

        public static void Hold(Projectile p, Player holder)
        {
            if (p == null)
                return;
            heldProjectiles[p] = holder;
            if (p.attackBox != null)
                heldAttacks[p.attackBox] = holder;
        }

        public static void Release(Projectile p)
        {
            if (p == null)
                return;
            heldProjectiles.Remove(p);
            if (p.attackBox != null)
                heldAttacks.Remove(p.attackBox);
        }

        // Everything one player holds (when the charge ends however it ends).
        public static void ReleaseAll(Player holder)
        {
            var mine = new List<Projectile>();
            foreach (KeyValuePair<Projectile, Player> entry in heldProjectiles)
            {
                if (entry.Value == holder || entry.Key == null)
                    mine.Add(entry.Key);
            }
            foreach (Projectile p in mine)
                heldProjectiles.Remove(p);
            var attacks = new List<Attack>();
            foreach (KeyValuePair<Attack, Player> entry in heldAttacks)
            {
                if (entry.Value == holder || entry.Key == null)
                    attacks.Add(entry.Key);
            }
            foreach (Attack a in attacks)
                heldAttacks.Remove(a);
        }

        // You, other players, and anything on your side (decoys, allies).
        private static bool OnYourSide(Collider2D col, Player holder)
        {
            if (col == null)
                return false;
            Entity target = col.GetComponentInParent<Entity>();
            if (target == null)
                return false;
            if (target is Player)
                return true;
            return holder != null && !string.IsNullOrEmpty(target.skillCategory)
                && target.skillCategory == holder.skillCategory;
        }

        private static bool Allow(Player holder, Collider2D col)
        {
            try
            {
                if (!OnYourSide(col, holder))
                    return true;
                if (!loggedBlock)
                {
                    loggedBlock = true;
                    SlingshotDashPlugin.Log("Storm: a held projectile passed over your side harmlessly");
                }
                return false;
            }
            catch (Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    SlingshotDashPlugin.Log($"Storm hit guard failed: {e.Message}");
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Attack), nameof(Attack.OnTriggerEnter2D))]
        private static class AttackEnterPatch
        {
            private static bool Prefix(Attack __instance, Collider2D col)
            {
                Player holder;
                return heldAttacks.Count == 0 || !heldAttacks.TryGetValue(__instance, out holder) || Allow(holder, col);
            }
        }

        [HarmonyPatch(typeof(Attack), nameof(Attack.ExecuteAttack))]
        private static class AttackExecutePatch
        {
            private static bool Prefix(Attack __instance, Collider2D col)
            {
                Player holder;
                return heldAttacks.Count == 0 || !heldAttacks.TryGetValue(__instance, out holder) || Allow(holder, col);
            }
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.OnTriggerEnter2D))]
        private static class ProjectileEnterPatch
        {
            private static bool Prefix(Projectile __instance, Collider2D other)
            {
                Player holder;
                return heldProjectiles.Count == 0 || !heldProjectiles.TryGetValue(__instance, out holder) || Allow(holder, other);
            }
        }
    }
}
