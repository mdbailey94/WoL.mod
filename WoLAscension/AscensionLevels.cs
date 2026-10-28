using System.Collections.Generic;

namespace WoLAscension
{
    // What each Ascension level does: WoL Player's table, approved by Matt (task sheet #27/#28,
    // 0.7.0). Levels are cumulative: level N includes everything below it, and a few later levels
    // raise an earlier modifier instead of adding a new one. The elite, boss-affix, rift, loot and
    // experience parts of the table are applied by Chaos Depths, which reads the level from here.
    public static class AscensionLevels
    {
        public const int Max = 10;

        //                                        A0     A1     A2     A3     A4     A5     A6     A7     A8     A9     A10
        private static readonly float[] Health = { 1f, 1.20f, 1.30f, 1.40f, 1.55f, 1.70f, 1.85f, 2.00f, 2.20f, 2.40f, 2.60f };
        private static readonly float[] Damage = { 1f, 1.10f, 1.20f, 1.30f, 1.40f, 1.50f, 1.60f, 1.75f, 1.90f, 2.05f, 2.20f };
        private static readonly float[] Elites = { 0f, 0.12f, 0.15f, 0.18f, 0.22f, 0.25f, 0.28f, 0.31f, 0.34f, 0.37f, 0.40f };

        private static int Clamp(int level) => UnityEngine.Mathf.Clamp(level, 0, Max);

        public static float EnemyHealth(int level) => Health[Clamp(level)];
        // Damage the wizard takes.
        public static float EnemyDamage(int level) => Damage[Clamp(level)];
        public static float Healing(int level) => level >= 8 ? 0.50f : level >= 2 ? 0.75f : 1f;
        public static float EnemySpeed(int level) => level >= 7 ? 1.20f : level >= 3 ? 1.10f : 1f;
        // Gold -20% was dropped from the table (0.7.0).
        public static float Gold(int level) => 1f;
        public static float BossHealth(int level) => level >= 9 ? 1.60f : level >= 5 ? 1.30f : 1f;
        public static float PlayerMaxHealth(int level) => level >= 6 ? 0.85f : 1f;
        // The rewards: +5% chaos gems and +5% experience (Chaos Depths) per level.
        public static float Gems(int level) => 1f + 0.05f * Clamp(level);
        public static float Experience(int level) => 1f + 0.05f * Clamp(level);

        // Chaos Depths' parts of the table.
        public static float EliteChance(int level) => Elites[Clamp(level)];   // 0 = Chaos Depths' own base chance
        public static float TwoAffixChance(int level) => level >= 10 ? 1f : level >= 8 ? 0.5f : level >= 4 ? 0.25f : 0f;
        public static int BossAffixes(int level, bool realmBoss) => level >= 10 && realmBoss ? 2 : level >= 5 ? 1 : 0;
        public static bool ElitesInPairs(int level) => level >= 9;
        public static float RiftChance(int level) => level >= 7 ? 0.40f : 0f;   // 0 = Chaos Depths' own chance
        public static int ItemLevelBonus(int level) => 2 * Clamp(level);
        public static bool ElitesDropRare(int level) => level >= 5;
        public static bool RealmBossDropsLegendary(int level) => level >= 10;

        // What a level adds on top of the one below it (as in the README's table).
        public static string Added(int level)
        {
            switch (level)
            {
                case 1: return "Elites 12%";
                case 2: return "Your healing -25%";
                case 3: return "Enemies 10% faster";
                case 4: return "1 in 4 elites has two affixes";
                case 5: return "Bosses +30% health and an elite affix; elites drop Rare+";
                case 6: return "Your max health -15%";
                case 7: return "Enemies 20% faster; rifts 40%";
                case 8: return "Healing -50%; half of elites have two affixes";
                case 9: return "Elites come in pairs; bosses +60% health";
                case 10: return "Every elite and realm boss has two affixes; realm boss drops a Legendary";
                default: return "";
            }
        }

        // One short line for the game's on-screen notice.
        public static string Summary(int level)
        {
            if (level <= 0)
                return "Off: the normal game";
            int bonus = UnityEngine.Mathf.RoundToInt((Gems(level) - 1f) * 100f);
            return $"Adds: {Added(level)}. Enemies +{Pct(EnemyHealth(level))}% health, +{Pct(EnemyDamage(level))}% damage. Gems and xp +{bonus}%";
        }

        private static int Pct(float multiplier) => UnityEngine.Mathf.RoundToInt((multiplier - 1f) * 100f);

        // One line per active modifier.
        public static List<string> Describe(int level)
        {
            var lines = new List<string>();
            if (level <= 0)
            {
                lines.Add("No modifiers - the normal game.");
                return lines;
            }
            lines.Add($"Elites {UnityEngine.Mathf.RoundToInt(EliteChance(level) * 100f)}% of enemies");
            Add(lines, EnemyHealth(level), "Enemies have {0} health");
            Add(lines, EnemyDamage(level), "Enemies deal {0} damage");
            Add(lines, EnemySpeed(level), "Enemies move {0} faster");
            Add(lines, BossHealth(level), "Bosses have {0} more health");
            Add(lines, Healing(level), "Your healing {0}");
            Add(lines, PlayerMaxHealth(level), "Your max health {0}");
            Add(lines, Gems(level), "Chaos gems and experience {0}");
            lines.Add($"Item level +{ItemLevelBonus(level)}");
            return lines;
        }

        private static void Add(List<string> lines, float multiplier, string format)
        {
            if (multiplier == 1f)
                return;
            int percent = UnityEngine.Mathf.RoundToInt((multiplier - 1f) * 100f);
            lines.Add(string.Format(format, (percent > 0 ? "+" : "") + percent + "%"));
        }
    }
}
