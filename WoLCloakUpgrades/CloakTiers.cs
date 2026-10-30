using System.Collections.Generic;
using UnityEngine;

namespace WoLCloakUpgrades
{
    // What upgrading does to a cloak. Every tier raises the cloak's own bonuses: by tier 4 they're
    // doubled. Stats that would get out of hand doubled (movement, evasion, armour, cooldowns and
    // the gold/gem cloaks) only go up to half again. Drawbacks a cloak has are never made worse,
    // and anything that isn't a plain number (an on/off effect, an override) stays as it is.
    public static class CloakTiers
    {
        public const int Max = 4;

        // Gems for the next tier: 100, 200, 400, 800 (1500 in all).
        public static int Cost(int currentTier) => currentTier >= Max ? 0 : 100 << currentTier;

        private static readonly HashSet<OutfitModStat.OutfitModType> Capped = new HashSet<OutfitModStat.OutfitModType>
        {
            OutfitModStat.OutfitModType.Speed,
            OutfitModStat.OutfitModType.Run,
            OutfitModStat.OutfitModType.Evade,
            OutfitModStat.OutfitModType.Armor,
            OutfitModStat.OutfitModType.Cooldown,
            OutfitModStat.OutfitModType.Gold,
            OutfitModStat.OutfitModType.Platinum,
        };

        // Left alone: not plain stat boosts, or unclear which way is "better".
        private static readonly HashSet<OutfitModStat.OutfitModType> Untouched = new HashSet<OutfitModStat.OutfitModType>
        {
            OutfitModStat.OutfitModType.None,
            OutfitModStat.OutfitModType.Pride,
            OutfitModStat.OutfitModType.Fall,
            OutfitModStat.OutfitModType.AllowUpgrade,
        };

        // How much a bonus of this kind is multiplied by at a tier.
        public static float Factor(OutfitModStat.OutfitModType type, int tier)
        {
            tier = Mathf.Clamp(tier, 0, Max);
            if (Untouched.Contains(type))
                return 1f;
            return Capped.Contains(type) ? 1f + 0.125f * tier : 1f + 0.25f * tier;
        }

        // Whether this stat is a bonus (not a drawback): lower is better only for cooldowns.
        public static bool IsBonus(OutfitModStat stat) =>
            stat.modType == OutfitModStat.OutfitModType.Cooldown ? !stat.isIncrease : stat.isIncrease;

        // A tier in a few words, for the wardrobe's info box and the banner.
        public static string Short(int tier)
        {
            if (tier <= 0)
                return "not upgraded";
            if (tier >= Max)
                return "bonuses doubled";
            return $"bonuses +{Mathf.RoundToInt((Factor(OutfitModStat.OutfitModType.Damage, tier) - 1f) * 100f)}%";
        }
    }
}
