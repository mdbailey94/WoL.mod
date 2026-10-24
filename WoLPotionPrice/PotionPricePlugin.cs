using System;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLPotionPrice
{
    // Health potions cost more each time you buy one in a run. (The game has the setting for it,
    // but at zero: each potion only heals less than the last.) Every potion on sale gets a price
    // mod of IncreasePercent of its base price for each potion already bought this run, counted by
    // the game itself.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class PotionPricePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.potionprice";
        public const string PluginName = "Potion Price";
        public const string PluginVersion = "0.1.0";
        private const string ModID = "PotionPrice_Increase";

        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<float> increasePercent;
        private float nextCheck;
        private int loggedCount = -1;

        private void Awake()
        {
            modEnabled = Config.Bind("General", "Enabled", true,
                "Health potions cost more each time you buy one in a run (also in the title screen Mods menu).");
            increasePercent = Config.Bind("General", "IncreasePercent", 25f,
                new ConfigDescription("How much more each potion costs for every potion already bought this run, " +
                    "as a percent of its base price (25 = +25% a potion).",
                    new AcceptableValueRange<float>(0f, 500f)));
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        // A few times a second: every potion on sale priced for the purchases so far.
        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + 0.25f;
            try
            {
                int bought = Mathf.Max(0, HealStoreItem.healBuyCount);
                float change = modEnabled.Value ? Mathf.Max(0f, increasePercent.Value) / 100f * bought : 0f;
                foreach (HealStoreItem potion in FindObjectsOfType<HealStoreItem>())
                    SetMod(potion, change, bought);
            }
            catch (Exception e)
            {
                Logger.LogError($"Couldn't price the potions: {e.Message}");
                nextCheck = float.MaxValue;
            }
        }

        private void SetMod(HealStoreItem potion, float change, int bought)
        {
            NumVarStat cost = potion != null ? potion.costStat : null;
            if (cost == null)
                return;
            int index = cost.GetModIndex(ModID);
            if (index >= 0)
            {
                var existing = cost.modifiers[index] as NumVarStatMod;
                if (existing != null && Mathf.Approximately(existing.modValue, change))
                    return;
                cost.RemoveMod(ModID);
            }
            if (change > 0f)
                cost.AddMod(new NumVarStatMod(ModID, change, 10, VarStatModType.Multiplicative));
            if (bought != loggedCount)
            {
                loggedCount = bought;
                Logger.LogInfo($"Potions bought this run: {bought}; price {cost.BaseValue:0} -> {cost.ModifiedValue:0}");
            }
        }
    }
}
