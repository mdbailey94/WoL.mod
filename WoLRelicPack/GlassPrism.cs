using System.Collections.Generic;

namespace WoLRelicPack
{
    // Glass Prism: much higher critical hit chance on every arcana, at the cost of max health.
    //
    // Modeled on Analytical Monocle (CritChanceUp: a crit chance mod on all of the wizard's skill
    // stats via StatManager.ModifyAllStatData), Jeremiah's Needle (CritChanceDownDamageUp, the
    // game's own crit trade-off relic) and the cursed max-health relic MaxHPDownRegenAtLowHP (a
    // multiplicative mod on health.healthStat). Both are the game's stat mods, so they stack with
    // other relics and come off cleanly when the relic is removed.
    public class GlassPrism : Item
    {
        public static string staticID = RelicPackPlugin.IDPrefix + "GlassPrism";

        private const float CritChanceBonus = 0.25f;  // added critical hit chance (Analytical Monocle gives 0.08)
        private const float MaxHealthLoss = 0.25f;    // max health lost, as a fraction (0.25 = a quarter)

        private readonly Dictionary<string, NumVarStatMod> skillMods;
        private readonly NumVarStatMod healthMod;

        // Public here only because the reference DLL is publicized (the game's own is protected).
        public override string ExtraInfo => PercentToStr(CritChanceBonus, "+") + " | " + PercentToStr(MaxHealthLoss, "-");

        public GlassPrism()
        {
            ID = staticID;
            category = Category.Offense;
            skillMods = new Dictionary<string, NumVarStatMod>
            {
                { StatData.critChStr, new NumVarStatMod(ID, CritChanceBonus) }
            };
            healthMod = new NumVarStatMod(ID, -MaxHealthLoss, 10, VarStatModType.Multiplicative);
        }

        public override void Activate()
        {
            SetModStatus(true);
        }

        public override void Deactivate()
        {
            SetModStatus(false);
        }

        private void SetModStatus(bool givenStatus)
        {
            try
            {
                StatManager.ModifyAllStatData(skillMods, parentSkillCategory, IgnoreStatusConditional, givenStatus);
                if (parentEntity != null && parentEntity.health != null)
                {
                    NumVarStat health = parentEntity.health.healthStat;
                    float before = health.ModifiedValue;
                    health.Modify(healthMod, givenStatus);
                    Player player = parentEntity as Player;
                    string who = player != null ? $"player {player.playerID + 1}" : parentSkillCategory;
                    RelicPackPlugin.Log($"Glass Prism {(givenStatus ? "on" : "off")} for {who}: " +
                        $"crit chance {(givenStatus ? "+" : "-")}{CritChanceBonus * 100f:0}%, max health {before:0} -> {health.ModifiedValue:0}");
                }
            }
            catch (System.Exception e)
            {
                RelicPackPlugin.LogErrorOnce("Glass Prism", e);
            }
        }
    }
}
