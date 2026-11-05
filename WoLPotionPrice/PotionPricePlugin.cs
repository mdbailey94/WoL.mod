using System;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLPotionPrice
{
    // Health potions cost more each time you buy one in a run. (The game has the setting for it,
    // but at zero: each potion only heals less than the last.) Every potion on sale gets a price
    // mod of IncreasePercent of its base price for each potion already bought this run, counted by
    // the game itself (HealStoreItem.healBuyCount, reset with each run).
    //
    // Done the way the game's own price relics do it: the mod goes on as a potion spawns and again
    // on every potion left in the shop after one is bought, and each time the price tag is told to
    // redraw (UpdatePrice), since the game only writes the tag when the item appears. A check a few
    // times a second catches the Enabled switch and config edits.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class PotionPricePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.potionprice";
        public const string PluginName = "Potion Price";
        public const string PluginVersion = "0.2.0";
        private const string ModID = "PotionPrice_Increase";
        private const float CheckInterval = 0.25f;

        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<float> increasePercent;
        private float nextCheck;
        private int loggedCount = -1;
        private bool loggedError;

        private void Awake()
        {
            modEnabled = Config.Bind("General", "Enabled", true,
                "Health potions cost more each time you buy one in a run (also in the title screen Mods menu).");
            increasePercent = Config.Bind("General", "IncreasePercent", 25f,
                new ConfigDescription("How much more each potion costs for every potion already bought this run, " +
                    "as a percent of its base price (25 = +25% a potion).",
                    new AcceptableValueRange<float>(0f, 500f)));
            HealStoreItem.spawnEventHandlers += OnPotionSpawned;
            HealStoreItem.buyEventHandlers += OnPotionBought;
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded (+{increasePercent.Value:0}% a potion)");
        }

        private void OnDestroy()
        {
            HealStoreItem.spawnEventHandlers -= OnPotionSpawned;
            HealStoreItem.buyEventHandlers -= OnPotionBought;
        }

        // A potion has just appeared in a shop (its tag was written a moment ago with the base price).
        private void OnPotionSpawned(HealStoreItem potion)
        {
            Guard(() => SetMod(potion, Change(), Bought()));
        }

        // One was bought: the game has counted it, so the ones still on sale go up.
        private void OnPotionBought(HealStoreItem bought)
        {
            Guard(PriceAll);
        }

        // A few times a second as well, for the Enabled switch and config edits.
        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + CheckInterval;
            Guard(PriceAll);
        }

        private void PriceAll()
        {
            float change = Change();
            int bought = Bought();
            foreach (HealStoreItem potion in HealStoreItem.healItemList)
                SetMod(potion, change, bought);
        }

        private static int Bought() => Mathf.Max(0, HealStoreItem.healBuyCount);

        private float Change() => modEnabled.Value ? Mathf.Max(0f, increasePercent.Value) / 100f * Bought() : 0f;

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
            // The tag only redraws when asked.
            potion.UpdatePrice();
            if (bought != loggedCount || change == 0f && index >= 0)
            {
                loggedCount = bought;
                Logger.LogInfo($"Potions bought this run: {bought}; potion price {cost.BaseValue:0} -> {potion.Cost}");
            }
        }

        private void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (loggedError)
                    return;
                loggedError = true;
                Logger.LogError($"Couldn't price the potions: {e.Message}");
            }
        }
    }
}
