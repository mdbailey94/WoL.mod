using System;
using HarmonyLib;
using UnityEngine;

namespace WoLChaosDepths
{
    // #114 step 3 (Player's proposal 14:17, Matt "Yes" 14:24): every cloak's own crit chance, evade,
    // move speed, cooldown and armor go on POINTS like everything else. When the game puts a cloak's
    // mods on a wizard (Outfit.SetMods, after Cloak Upgrades has scaled them for the tier), those five
    // are switched off again and their values go into the point register as "Cloak.<type>":
    //   CritChance / Evade (added fractions) and Speed (a multiplier) -> the same number of points;
    //   Cooldown -> cooldown points (a cut is negative); Armor -> DAMAGE TAKEN points, 1% = 1 point.
    // Everything else on a cloak (crit damage, damage, health, healing, gold...) stays as the game
    // applies it. Taking the cloak off clears its points.
    public static class CloakPoints
    {
        private static bool loggedError;

        public static void Install(Harmony harmony)
        {
            try
            {
                harmony.Patch(AccessTools.Method(typeof(Outfit), nameof(Outfit.SetMods)),
                    postfix: new HarmonyMethod(typeof(CloakPoints), nameof(AfterSetMods)));
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"cloak points hook: {e.Message}");
            }
        }

        private static string KeyOf(OutfitModStat.OutfitModType type)
        {
            switch (type)
            {
                case OutfitModStat.OutfitModType.CritChance: return "CritChance";
                case OutfitModStat.OutfitModType.Evade: return "Evade";
                case OutfitModStat.OutfitModType.Speed: return "MoveSpeed";
                case OutfitModStat.OutfitModType.Cooldown: return "Cooldown";
                case OutfitModStat.OutfitModType.Armor: return "DamageTaken";
                default: return null;
            }
        }

        private static readonly OutfitModStat.OutfitModType[] Types =
        {
            OutfitModStat.OutfitModType.CritChance, OutfitModStat.OutfitModType.Evade, OutfitModStat.OutfitModType.Speed,
            OutfitModStat.OutfitModType.Cooldown, OutfitModStat.OutfitModType.Armor
        };

        private static void AfterSetMods(Outfit __instance, bool status)
        {
            try
            {
                Player p = __instance != null ? __instance.parentPlayer : null;
                if (p == null)
                    return;
                foreach (OutfitModStat.OutfitModType t in Types)
                    Points.Set(p, KeyOf(t), "Cloak." + t, 0f);
                if (!status || !ChaosDepthsPlugin.ModOn || __instance.modList == null)
                    return;
                var sums = new float[Types.Length];
                foreach (OutfitModStat stat in __instance.modList)
                {
                    int i = stat != null ? Array.IndexOf(Types, stat.modType) : -1;
                    if (i < 0)
                        continue;
                    float add = stat.addModifier != null ? stat.addModifier.modValue : 0f;
                    float multi = stat.multiModifier != null ? stat.multiModifier.modValue : 0f;
                    float size = Mathf.Abs(!Mathf.Approximately(add, 0f) ? add : multi);
                    if (Mathf.Approximately(size, 0f))
                        continue;
                    // A bonus is + points (a cooldown or damage-taken CUT is - points); a drawback the other way.
                    bool lowerIsBetter = stat.modType == OutfitModStat.OutfitModType.Cooldown;
                    bool bonus = lowerIsBetter ? !stat.isIncrease : stat.isIncrease;
                    float points = size * 100f;
                    if (stat.modType == OutfitModStat.OutfitModType.Cooldown || stat.modType == OutfitModStat.OutfitModType.Armor)
                        points = bonus ? -points : points;   // armor bonus = a damage-taken cut
                    else
                        points = bonus ? points : -points;
                    sums[i] += points;
                    stat.SetModStatus(p, false);   // the game's own mod off; the points replace it
                }
                for (int i = 0; i < Types.Length; i++)
                    if (!Mathf.Approximately(sums[i], 0f))
                    {
                        Points.Set(p, KeyOf(Types[i]), "Cloak." + Types[i], sums[i]);
                        ChaosDepthsPlugin.Log($"Cloak {__instance.outfitID} on player {p.playerID + 1}: {Types[i]} as {sums[i]:+0;-0} points");
                    }
            }
            catch (Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    ChaosDepthsPlugin.LogError($"cloak points: {e.Message}");
                }
            }
        }
    }
}
