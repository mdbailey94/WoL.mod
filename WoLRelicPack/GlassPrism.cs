using System.Collections.Generic;

namespace WoLRelicPack
{
    // Glass Prism (0.3.0 rework, Matt 2026-11-03): while you're at FULL health, every arcana deals
    // DamageBonus more. The first hit shatters it, and it comes back only once you're healed to
    // full again. No crit and no max-health cost any more, so it no longer overlaps Duelist's
    // Cloak (crits and speed for more damage taken) or the Gambler's All In (gold as the stake).
    //
    // Built on the game's ModWithHP (the base of its low-health relics), flipped to "at or above
    // full": it switches the damage mod on and off as health crosses the line and shows that on
    // the relic bar. The mod is the game's own (StatManager.ModifyAllStatData on the damage stat),
    // so it stacks with other relics and comes off cleanly.
    public class GlassPrism : ModWithHP
    {
        public static string staticID = RelicPackPlugin.IDPrefix + "GlassPrism";

        private const float DamageBonus = 0.30f;   // extra arcana damage while at full health
        private const float FullHealth = 0.999f;   // health fraction that counts as full

        private readonly Dictionary<string, NumVarStatMod> skillMods;

        public override string ExtraInfo => PercentToStr(DamageBonus, "+");

        public GlassPrism()
        {
            ID = staticID;
            category = Category.Offense;
            comparisonType = IntComparisonType.GreaterThanEquals;
            HPThreshold = FullHealth;
            statMod = new NumVarStatMod(ID, DamageBonus, 10, VarStatModType.Multiplicative);
            skillMods = new Dictionary<string, NumVarStatMod>
            {
                { StatData.damageStr, new NumVarStatMod(ID, DamageBonus, 10, VarStatModType.Multiplicative) }
            };
        }

        public override void Activate()
        {
            UpdateItemBar(ItemStatusBar.ItemState.Disabled);
            base.Activate();
        }

        public override void Deactivate()
        {
            base.Deactivate();
            RemoveFromItemBar();
        }

        // Alive and at full health.
        public override bool CheckHPThreshold()
        {
            return parentPlayer != null && parentPlayer.health != null && parentPlayer.health.CurrentHealthValue > 0
                && parentPlayer.health.HealthPercentage >= HPThreshold;
        }

        public override void ActivatePowerup()
        {
            base.ActivatePowerup();
            SetDamage(true);
            UpdateItemBar(ItemStatusBar.ItemState.Active);
        }

        public override void DeactivatePowerup()
        {
            bool was = activated;
            base.DeactivatePowerup();
            if (was)
                SetDamage(false);
            UpdateItemBar(ItemStatusBar.ItemState.Disabled);
        }

        private void SetDamage(bool givenStatus)
        {
            try
            {
                StatManager.ModifyAllStatData(skillMods, parentSkillCategory, IgnoreStatusConditional, givenStatus);
                string who = parentPlayer != null ? $"player {parentPlayer.playerID + 1}" : parentSkillCategory;
                RelicPackPlugin.LogRateLimited("GlassPrism" + who + givenStatus, 1f,
                    givenStatus ? $"Glass Prism whole for {who}: +{DamageBonus * 100f:0}% arcana damage at full health"
                                : $"Glass Prism shattered for {who} (no longer at full health)");
            }
            catch (System.Exception e)
            {
                RelicPackPlugin.LogErrorOnce("Glass Prism", e);
            }
        }
    }
}
