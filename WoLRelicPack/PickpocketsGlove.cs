using System.Collections.Generic;

namespace WoLRelicPack
{
    // Pickpocket's Glove: while your health is at HealthThreshold or below, all gold drops are
    // GoldBonus bigger (enemies, and the chests and pots that drop gold too).
    //
    // Modeled on the game's low-health relics (ModWithHP, the base of the Auditor's Talisman,
    // CritUpAtLowHealth: switches on and off as health crosses a threshold, shown on the relic
    // bar) and Midas Glove (GoldUp: a multiplicative mod on LootManager.globalGoldModifier, which
    // every gold drop is scaled by). That modifier is shared by all wizards, so in co-op the bonus
    // is on while any wizard holding a glove is low, and never applied twice.
    public class PickpocketsGlove : ModWithHP
    {
        public static string staticID = RelicPackPlugin.IDPrefix + "PickpocketsGlove";

        private const float HealthThreshold = 0.5f;  // health fraction at or below which it's active (0.5 = half)
        private const float GoldBonus = 0.5f;        // extra gold while active (0.5 = +50%; Midas Glove gives 0.15)

        private static readonly NumVarStatMod goldMod = new NumVarStatMod(staticID, GoldBonus, 10, VarStatModType.Multiplicative);
        // Players whose glove is active right now.
        private static readonly HashSet<int> activeHolders = new HashSet<int>();

        public PickpocketsGlove()
        {
            ID = staticID;
            category = Category.Misc;
            comparisonType = IntComparisonType.LessThanEquals;
            HPThreshold = HealthThreshold;
            // ModWithHP shows this in the relic's description (+50%).
            statMod = new NumVarStatMod(ID, GoldBonus, 10, VarStatModType.Multiplicative);
        }

        public override void Activate()
        {
            UpdateItemBar(ItemStatusBar.ItemState.Disabled);
            base.Activate();
            if (parentPlayer != null)
                RelicPackPlugin.Log($"Pickpocket's Glove held by player {parentPlayer.playerID + 1} (active at {HealthThreshold * 100f:0}% health or below)");
        }

        public override void Deactivate()
        {
            base.Deactivate();
            RemoveFromItemBar();
        }

        // Alive and at or below the threshold (a dead wizard doesn't count as low).
        public override bool CheckHPThreshold()
        {
            return parentPlayer != null && parentPlayer.health != null && parentPlayer.health.CurrentHealthValue > 0
                && parentPlayer.health.HealthPercentage <= HPThreshold;
        }

        public override void ActivatePowerup()
        {
            base.ActivatePowerup();
            SetHolder(true);
            UpdateItemBar(ItemStatusBar.ItemState.Active);
        }

        public override void DeactivatePowerup()
        {
            base.DeactivatePowerup();
            SetHolder(false);
            UpdateItemBar(ItemStatusBar.ItemState.Disabled);
        }

        private void SetHolder(bool givenStatus)
        {
            try
            {
                if (parentPlayer == null)
                    return;
                int player = parentPlayer.playerID;
                bool changed = givenStatus ? activeHolders.Add(player) : activeHolders.Remove(player);
                if (!changed)
                    return;
                bool on = activeHolders.Count > 0;
                bool applied = LootManager.globalGoldModifier.GetModIndex(goldMod.ID) >= 0;
                if (on != applied)
                    LootManager.globalGoldModifier.Modify(goldMod, on);
                float health = parentPlayer.health != null ? parentPlayer.health.HealthPercentage : 0f;
                RelicPackPlugin.LogRateLimited("Pickpocket" + player + givenStatus, 1f,
                    $"Pickpocket's Glove {(givenStatus ? "active" : "inactive")} for player {player + 1} at {health * 100f:0}% health; " +
                    $"gold drops now x{LootManager.globalGoldModifier.ModifiedValue:0.00}");
            }
            catch (System.Exception e)
            {
                RelicPackPlugin.LogErrorOnce("Pickpocket's Glove", e);
            }
        }
    }
}
