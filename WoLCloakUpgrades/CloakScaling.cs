using System;
using System.Collections.Generic;
using HarmonyLib;

namespace WoLCloakUpgrades
{
    // Applies each cloak's tier to its stat modifiers. The cloak's original values are recorded
    // the first time it's seen (per cloak and per modifier slot, so copies of a cloak share them)
    // and every scaling starts again from those, so nothing ever compounds. Scaling happens just
    // before a cloak applies its modifiers and before it describes itself, so the wardrobe shows
    // the upgraded numbers too.
    public static class CloakScaling
    {
        // cloak ID -> per modifier slot: (add value, multiply value) as the game made them.
        private static readonly Dictionary<string, List<KeyValuePair<float, float>>> originals =
            new Dictionary<string, List<KeyValuePair<float, float>>>();

        public static Func<string, int> TierOf = id => 0;
        private static readonly HashSet<string> logged = new HashSet<string>();
        private static bool loggedError;

        public static void Install(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(SetModsPatch)).Patch();
            harmony.CreateClassProcessor(typeof(DescriptionPatch)).Patch();
        }

        [HarmonyPatch(typeof(Outfit), nameof(Outfit.SetMods))]
        private static class SetModsPatch
        {
            private static void Prefix(Outfit __instance) => Scale(__instance);
        }

        [HarmonyPatch(typeof(Outfit), nameof(Outfit.GetDescription))]
        private static class DescriptionPatch
        {
            private static void Prefix(Outfit __instance) => Scale(__instance);
        }

        public static void Scale(Outfit outfit)
        {
            try
            {
                if (outfit == null || outfit.modList == null || string.IsNullOrEmpty(outfit.outfitID))
                    return;
                List<KeyValuePair<float, float>> values;
                if (!originals.TryGetValue(outfit.outfitID, out values))
                {
                    values = new List<KeyValuePair<float, float>>();
                    foreach (OutfitModStat stat in outfit.modList)
                    {
                        values.Add(new KeyValuePair<float, float>(
                            stat != null && stat.addModifier != null ? stat.addModifier.modValue : 0f,
                            stat != null && stat.multiModifier != null ? stat.multiModifier.modValue : 0f));
                    }
                    originals[outfit.outfitID] = values;
                }
                int tier = TierOf(outfit.outfitID);
                for (int i = 0; i < outfit.modList.Count && i < values.Count; i++)
                {
                    OutfitModStat stat = outfit.modList[i];
                    if (stat == null)
                        continue;
                    float factor = CloakTiers.IsBonus(stat) ? CloakTiers.Factor(stat.modType, tier) : 1f;
                    if (stat.addModifier != null)
                        stat.addModifier.modValue = values[i].Key * factor;
                    if (stat.multiModifier != null)
                        stat.multiModifier.modValue = values[i].Value * factor;
                    // Logged once per cloak and tier, so the log shows what an upgrade really did.
                    if (tier > 0 && logged.Add(outfit.outfitID + "/" + tier + "/" + i))
                        CloakUpgradesPlugin.Log($"{outfit.outfitID} tier {tier}: {stat.modType} " +
                            $"{(CloakTiers.IsBonus(stat) ? "bonus" : "drawback")} x{factor:0.###} " +
                            $"(add {values[i].Key:0.###} -> {values[i].Key * factor:0.###}, " +
                            $"multiply {values[i].Value:0.###} -> {values[i].Value * factor:0.###})");
                }
            }
            catch (Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    CloakUpgradesPlugin.Log($"Couldn't scale a cloak: {e.Message}");
                }
            }
        }
    }
}
