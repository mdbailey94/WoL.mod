using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using LegendAPI;

namespace WoLDuelistCloak
{
    // A new cloak, the Duelist's Cloak: faster, with more frequent and harder crits, but every hit
    // you take hurts more. Speed and crits are the game's own cloak stats, so the game applies and
    // describes them like any other cloak's. "Damage taken" isn't a cloak stat (and a negative Armor
    // stat would do nothing on its own: the game clamps armour at 0), so it's a LegendAPI custom
    // stat that puts a multiplier on the wearer's damage-taken stat while the cloak is worn.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class DuelistCloakPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.duelistcloak";
        public const string PluginName = "Duelist's Cloak";
        public const string PluginVersion = "0.1.0";

        // The game's ID for the cloak: kept in your save once bought, and the key Cloak Upgrades
        // stores its tier under. Don't change it, or a bought cloak is lost.
        public const string OutfitID = "DuelistCloak";
        public const string DisplayName = "Duelist's Cloak";

        // Row of the cloak palette its colours come from. Vanilla cloaks use 0, 2-9, 11, 14, 15 and
        // 17-20 (22 is the locked-cloak grey, 24 Chaos); 13 isn't any vanilla cloak's.
        private const int ColorIndex = 13;

        // +15% movement speed (a multiplier, like Pace's +15%).
        private const float SpeedBonus = 0.15f;
        // +10% critical hit chance (added, like Awe's +10%).
        private const float CritChanceBonus = 0.10f;
        // +30% critical damage (added; Awe gives +25%).
        private const float CritDamageBonus = 0.30f;
        // +25% damage taken: every hit on the wearer is multiplied by 1.25 (before armour).
        public const float DamageTakenIncrease = 0.25f;

        // The mod put on the wearer's damage-taken stat. The same ID each time, so putting it on
        // twice just replaces it and taking it off always finds it.
        private static readonly NumVarStatMod damageTakenMod = new NumVarStatMod(
            "DuelistCloakDamageTaken", DamageTakenIncrease, 10, VarStatModType.Multiplicative);

        internal static DuelistCloakPlugin Instance;

        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<bool> alwaysOnSale;

        private void Awake()
        {
            Instance = this;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer the Duelist's Cloak at the cloak merchant (also in the title screen Mods menu). " +
                "Turning it off doesn't take it away once you've bought it.");
            alwaysOnSale = Config.Bind("General", "AlwaysOnSale", true,
                "While the cloak is still locked, the cloak merchant always has it in one of their three " +
                "slots. Off: only once fewer than three vanilla cloaks are left to buy (LegendAPI's own rule).");

            var outfit = new Outfit(OutfitID, ColorIndex, new List<OutfitModStat>
            {
                new OutfitModStat(OutfitModStat.OutfitModType.Speed, 0f, SpeedBonus),
                new OutfitModStat(OutfitModStat.OutfitModType.CritChance, CritChanceBonus),
                new OutfitModStat(OutfitModStat.OutfitModType.CritDamage, CritDamageBonus),
                // The damage-taken drawback. Stored as a negative number so the game and Cloak
                // Upgrades both see a drawback (Cloak Upgrades never scales those); the amount
                // actually used is DamageTakenIncrease, so nothing that rescales stats (Cloak
                // Upgrades, the in-run tailor) can make the penalty bigger.
                new OutfitModStat(Outfits.CustomModType, -DamageTakenIncrease)
            });

            Outfits.Register(new OutfitInfo
            {
                outfit = outfit,
                name = DisplayName,
                // LegendAPI only offers it at the merchant while this is true.
                unlockCondition = () => modEnabled.Value,
                customMod = SetDamageTaken,
                customDesc = DescribeDamageTaken
            });

            new Harmony(PluginGuid).CreateClassProcessor(typeof(MerchantSlotPatch)).Patch();

            Logger.LogInfo($"{PluginName} {PluginVersion} registered (ID {OutfitID}): " +
                $"speed +{Percent(SpeedBonus)}, crit chance +{Percent(CritChanceBonus)}, " +
                $"crit damage +{Percent(CritDamageBonus)}, damage taken +{Percent(DamageTakenIncrease)}");
        }

        private static string Percent(float value) => $"{value * 100f:0.##}%";

        // LegendAPI calls this whenever the cloak's custom stat goes on or off a player: equipping
        // and unequipping it (also each time a level loads), and when Cloak Upgrades refreshes it.
        private void SetDamageTaken(Player player, bool on, bool onEquip, OutfitModStat modifier)
        {
            if (player == null || player.health == null || player.health.damageTakenStat == null)
                return;
            player.health.damageTakenStat.Modify(damageTakenMod, on);
            Logger.LogInfo($"{DisplayName} {(on ? "equipped" : "removed")} (player {player.playerID + 1}): " +
                $"damage taken +{Percent(DamageTakenIncrease)} {(on ? "on" : "off")}, " +
                $"damage taken multiplier now x{player.health.damageTakenStat.CurrentValue:0.###}");
        }

        // The custom stat's line in the cloak's description, worded like the game's own lines. The
        // wardrobe and the merchant show it without the number, the pause menu with it.
        private static string DescribeDamageTaken(bool addExtra, OutfitModStat modifier)
        {
            // The game's own lines carry their "- " bullet in the text files, so it's added here too.
            string text = "- Increased Damage Taken";
            if (addExtra)
                text += " <color=#009999>( </color><color=#00dddd>" + Globals.PercentToStr(DamageTakenIncrease, "+") +
                    "</color><color=#009999> )</color>";
            return text;
        }

        // Called after the cloak merchant fills their three slots. LegendAPI only offers mod cloaks
        // in slots no vanilla cloak needs, so while vanilla cloaks are still locked this cloak would
        // never come up; this puts it in the last slot instead (an empty one if there is one).
        internal void ReserveMerchantSlot(OutfitMerchantNpc merchant)
        {
            if (!modEnabled.Value || !alwaysOnSale.Value || merchant == null)
                return;
            Outfit cloak;
            if (!Outfit.OutfitDict.TryGetValue(OutfitID, out cloak) || cloak == null || cloak.unlocked)
                return;

            OutfitStoreItem[] items = merchant.GetComponentsInChildren<OutfitStoreItem>(true);
            if (items == null || items.Length == 0)
                return;
            OutfitStoreItem slot = null;
            foreach (OutfitStoreItem item in items)
            {
                if (item == null)
                    continue;
                if (item.outfitID == OutfitID)
                {
                    Logger.LogInfo($"{DisplayName} is on sale at the cloak merchant");
                    return;
                }
                if (slot == null && string.IsNullOrEmpty(item.outfitID))
                    slot = item;
            }
            if (slot == null)
                slot = items[items.Length - 1];
            if (slot == null)
                return;

            // The store item reads its ID in Start, which hasn't run yet.
            string replaced = string.IsNullOrEmpty(slot.outfitID) ? "an empty slot" : slot.outfitID;
            slot.outfitID = OutfitID;
            Logger.LogInfo($"{DisplayName} is on sale at the cloak merchant (in place of {replaced})");
        }

        [HarmonyPatch(typeof(OutfitMerchantNpc), nameof(OutfitMerchantNpc.LoadOutfitItems))]
        private static class MerchantSlotPatch
        {
            private static void Postfix(OutfitMerchantNpc __instance)
            {
                try
                {
                    Instance?.ReserveMerchantSlot(__instance);
                }
                catch (Exception e)
                {
                    Instance?.Logger.LogWarning($"Couldn't put the {DisplayName} on sale: {e.Message}");
                }
            }
        }
    }
}
