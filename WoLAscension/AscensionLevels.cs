using System.Collections.Generic;

namespace WoLAscension
{
    // What each Ascension level does. Levels are cumulative: level N includes everything below it,
    // and a few later levels raise an earlier modifier instead of adding a new one.
    public static class AscensionLevels
    {
        public const int Max = 10;

        public static float EnemyHealth(int level) => level >= 8 ? 1.30f : level >= 1 ? 1.15f : 1f;
        public static float EnemyDamage(int level) => level >= 9 ? 1.30f : level >= 2 ? 1.15f : 1f;
        public static float Healing(int level) => level >= 10 ? 0.50f : level >= 3 ? 0.75f : 1f;
        public static float EnemySpeed(int level) => level >= 10 ? 1.20f : level >= 4 ? 1.10f : 1f;
        public static float Gold(int level) => level >= 5 ? 0.80f : 1f;
        public static float BossHealth(int level) => level >= 6 ? 1.25f : 1f;
        public static float PlayerMaxHealth(int level) => level >= 7 ? 0.85f : 1f;
        // The reward: +4% chaos gems per level (+40% at 10).
        public static float Gems(int level) => 1f + 0.04f * UnityEngine.Mathf.Clamp(level, 0, Max);

        // What a level adds on top of the one below it (as in the README's table).
        public static string Added(int level)
        {
            switch (level)
            {
                case 1: return "Enemies +15% health";
                case 2: return "Enemies deal +15% damage";
                case 3: return "Your healing -25%";
                case 4: return "Enemies move 10% faster";
                case 5: return "Gold drops -20%";
                case 6: return "Bosses +25% health";
                case 7: return "Your max health -15%";
                case 8: return "Enemies +30% health";
                case 9: return "Enemies deal +30% damage";
                case 10: return "Healing -50%, enemies 20% faster";
                default: return "";
            }
        }

        // One short line for the game's on-screen notice.
        public static string Summary(int level)
        {
            if (level <= 0)
                return "Off: the normal game";
            int gems = UnityEngine.Mathf.RoundToInt((Gems(level) - 1f) * 100f);
            return $"Adds: {Added(level)}. Chaos gems +{gems}%";
        }

        // One line per active modifier.
        public static List<string> Describe(int level)
        {
            var lines = new List<string>();
            if (level <= 0)
            {
                lines.Add("No modifiers - the normal game.");
                return lines;
            }
            Add(lines, EnemyHealth(level), "Enemies have {0} health");
            Add(lines, EnemyDamage(level), "Enemies deal {0} damage");
            Add(lines, EnemySpeed(level), "Enemies move {0} faster");
            Add(lines, BossHealth(level), "Bosses have {0} more health");
            Add(lines, Healing(level), "Your healing {0}");
            Add(lines, PlayerMaxHealth(level), "Your max health {0}");
            Add(lines, Gold(level), "Gold drops {0}");
            Add(lines, Gems(level), "Chaos gems {0}");
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
