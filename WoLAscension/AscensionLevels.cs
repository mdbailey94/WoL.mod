using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLAscension
{
    // What each Ascension level does: WoL Player's table, approved by Matt (task sheet #27/#28,
    // 0.7.0). Since 0.8.0 (Matt) every value of every level is a setting, one config section per
    // level ([Level 01] to [Level 10]), its defaults the table below. Each section holds that
    // level's whole value (levels don't add up in the file: Level 05's EnemyHealth 1.7 means +70%).
    // The elite, boss-affix, rift, loot and experience parts are applied by Chaos Depths, which
    // reads them from here (AscensionLink).
    public static class AscensionLevels
    {
        public const int Max = 10;

        // ---- The defaults (A0 is the normal game and isn't a setting) ----
        //                                           A0     A1     A2     A3     A4     A5     A6     A7     A8     A9     A10
        private static readonly float[] DefHealth = { 1f, 1.20f, 1.30f, 1.40f, 1.55f, 1.70f, 1.85f, 2.00f, 2.20f, 2.40f, 2.60f };
        private static readonly float[] DefDamage = { 1f, 1.10f, 1.20f, 1.30f, 1.40f, 1.50f, 1.60f, 1.75f, 1.90f, 2.05f, 2.20f };
        private static readonly float[] DefElites = { 0f, 0.12f, 0.15f, 0.18f, 0.22f, 0.25f, 0.28f, 0.31f, 0.34f, 0.37f, 0.40f };
        private static readonly float[] DefHealing = Table(l => l >= 8 ? 0.50f : l >= 2 ? 0.75f : 1f);
        private static readonly float[] DefSpeed = Table(l => l >= 7 ? 1.20f : l >= 3 ? 1.10f : 1f);
        // Gold -20% was dropped from the table (0.7.0); it's 1 unless set.
        private static readonly float[] DefGold = Table(l => 1f);
        private static readonly float[] DefBossHealth = Table(l => l >= 9 ? 1.60f : l >= 5 ? 1.30f : 1f);
        private static readonly float[] DefMaxHealth = Table(l => l >= 6 ? 0.85f : 1f);
        // The rewards: +5% chaos gems and +5% experience (Chaos Depths) per level.
        private static readonly float[] DefGems = Table(l => 1f + 0.05f * l);
        private static readonly float[] DefXp = Table(l => 1f + 0.05f * l);
        private static readonly float[] DefTwoAffix = Table(l => l >= 10 ? 1f : l >= 8 ? 0.5f : l >= 4 ? 0.25f : 0f);
        private static readonly int[] DefBossAffixes = TableInt(l => l >= 5 ? 1 : 0);
        private static readonly int[] DefRealmBossAffixes = TableInt(l => l >= 10 ? 2 : l >= 5 ? 1 : 0);
        private static readonly bool[] DefPairs = TableBool(l => l >= 9);
        private static readonly float[] DefRift = Table(l => l >= 7 ? 0.40f : 0f);
        private static readonly int[] DefItemLevel = TableInt(l => 2 * l);
        private static readonly bool[] DefDropRare = TableBool(l => l >= 5);
        private static readonly bool[] DefLegendary = TableBool(l => l >= 10);

        // ---- The settings ----
        private static readonly ConfigEntry<float>[] health = new ConfigEntry<float>[Max + 1], damage = new ConfigEntry<float>[Max + 1],
            elites = new ConfigEntry<float>[Max + 1], healing = new ConfigEntry<float>[Max + 1], speed = new ConfigEntry<float>[Max + 1],
            gold = new ConfigEntry<float>[Max + 1], bossHealth = new ConfigEntry<float>[Max + 1], maxHealth = new ConfigEntry<float>[Max + 1],
            gems = new ConfigEntry<float>[Max + 1], xp = new ConfigEntry<float>[Max + 1], twoAffix = new ConfigEntry<float>[Max + 1],
            rift = new ConfigEntry<float>[Max + 1];
        private static readonly ConfigEntry<int>[] bossAffixes = new ConfigEntry<int>[Max + 1], realmBossAffixes = new ConfigEntry<int>[Max + 1],
            itemLevel = new ConfigEntry<int>[Max + 1];
        private static readonly ConfigEntry<bool>[] pairs = new ConfigEntry<bool>[Max + 1], dropRare = new ConfigEntry<bool>[Max + 1],
            legendary = new ConfigEntry<bool>[Max + 1];

        // From the plugin's Awake. Each applies when a run starts at that level (or at once, for the
        // run under way, for the parts read as things happen).
        public static void Bind(ConfigFile config)
        {
            for (int l = 1; l <= Max; l++)
            {
                string s = $"Level {l:00}";
                string at = $" at Ascension {l}";
                health[l] = F(config, s, "EnemyHealth", DefHealth[l], 0.1f, 10f, "Enemy health, times normal" + at + " (1.2 = +20%).");
                damage[l] = F(config, s, "EnemyDamage", DefDamage[l], 0.1f, 10f, "Damage enemies deal to the wizard, times normal" + at + ".");
                elites[l] = F(config, s, "EliteChance", DefElites[l], 0f, 1f,
                    "Share of enemies that are elites" + at + " (Chaos Depths; 0 = its own base chance).");
                healing[l] = F(config, s, "Healing", DefHealing[l], 0f, 2f, "The wizard's healing, times normal" + at + " (0.75 = -25%).");
                speed[l] = F(config, s, "EnemySpeed", DefSpeed[l], 0.5f, 3f, "How fast enemies move and attack, times normal" + at + ".");
                gold[l] = F(config, s, "Gold", DefGold[l], 0f, 5f, "Gold picked up, times normal" + at + ".");
                bossHealth[l] = F(config, s, "BossHealth", DefBossHealth[l], 0.1f, 10f,
                    "Boss health, times EnemyHealth" + at + " (1.3 = bosses +30% on top of the enemy health).");
                maxHealth[l] = F(config, s, "PlayerMaxHealth", DefMaxHealth[l], 0.1f, 2f, "The wizard's max health, times normal" + at + " (0.85 = -15%).");
                gems[l] = F(config, s, "ChaosGems", DefGems[l], 0f, 10f, "Chaos gems picked up, times normal" + at + ".");
                xp[l] = F(config, s, "Experience", DefXp[l], 0f, 10f, "Experience from kills, times normal" + at + " (Chaos Depths).");
                twoAffix[l] = F(config, s, "TwoAffixEliteChance", DefTwoAffix[l], 0f, 1f,
                    "Share of elites with two affixes" + at + " (Chaos Depths; 0.25 = 1 in 4).");
                bossAffixes[l] = I(config, s, "BossAffixes", DefBossAffixes[l], 0, 2, "Elite affixes on a floor boss" + at + " (Chaos Depths).");
                realmBossAffixes[l] = I(config, s, "RealmBossAffixes", DefRealmBossAffixes[l], 0, 2,
                    "Elite affixes on a realm boss" + at + " (Chaos Depths).");
                pairs[l] = config.Bind(s, "ElitesInPairs", DefPairs[l], "Elites come in pairs" + at + " (Chaos Depths).");
                rift[l] = F(config, s, "RiftChance", DefRift[l], 0f, 1f, "Chance of a rift on a floor" + at + " (Chaos Depths; 0 = its own chance).");
                itemLevel[l] = I(config, s, "ItemLevelBonus", DefItemLevel[l], 0, 100, "Levels added to dropped gear" + at + " (Chaos Depths).");
                dropRare[l] = config.Bind(s, "ElitesDropRare", DefDropRare[l],
                    "Elites' items are Rare or better" + at + " (Chaos Depths; the rarity is its [Elites] DropMinRarityFromAscension5).");
                legendary[l] = config.Bind(s, "RealmBossDropsLegendary", DefLegendary[l],
                    "Realm bosses drop a Legendary" + at + " (Chaos Depths; also needs its [Loot] RealmBossLegendaryAtAscension10).");
            }
        }

        private static ConfigEntry<float> F(ConfigFile c, string s, string key, float value, float min, float max, string text) =>
            c.Bind(s, key, value, new ConfigDescription(text, new AcceptableValueRange<float>(min, max)));

        private static ConfigEntry<int> I(ConfigFile c, string s, string key, int value, int min, int max, string text) =>
            c.Bind(s, key, value, new ConfigDescription(text, new AcceptableValueRange<int>(min, max)));

        private static int Clamp(int level) => Mathf.Clamp(level, 0, Max);

        private static float Get(ConfigEntry<float>[] entries, float[] fallback, int level, float min, float max)
        {
            level = Clamp(level);
            return level > 0 && entries[level] != null ? Mathf.Clamp(entries[level].Value, min, max) : fallback[level];
        }

        private static int Get(ConfigEntry<int>[] entries, int[] fallback, int level, int min, int max)
        {
            level = Clamp(level);
            return level > 0 && entries[level] != null ? Mathf.Clamp(entries[level].Value, min, max) : fallback[level];
        }

        private static bool Get(ConfigEntry<bool>[] entries, bool[] fallback, int level)
        {
            level = Clamp(level);
            return level > 0 && entries[level] != null ? entries[level].Value : fallback[level];
        }

        public static float EnemyHealth(int level) => Get(health, DefHealth, level, 0.1f, 10f);
        // Damage the wizard takes.
        public static float EnemyDamage(int level) => Get(damage, DefDamage, level, 0.1f, 10f);
        public static float Healing(int level) => Get(healing, DefHealing, level, 0f, 2f);
        public static float EnemySpeed(int level) => Get(speed, DefSpeed, level, 0.5f, 3f);
        public static float Gold(int level) => Get(gold, DefGold, level, 0f, 5f);
        public static float BossHealth(int level) => Get(bossHealth, DefBossHealth, level, 0.1f, 10f);
        public static float PlayerMaxHealth(int level) => Get(maxHealth, DefMaxHealth, level, 0.1f, 2f);
        public static float Gems(int level) => Get(gems, DefGems, level, 0f, 10f);
        public static float Experience(int level) => Get(xp, DefXp, level, 0f, 10f);

        // Chaos Depths' parts of the table.
        public static float EliteChance(int level) => Get(elites, DefElites, level, 0f, 1f);   // 0 = Chaos Depths' own base chance
        public static float TwoAffixChance(int level) => Get(twoAffix, DefTwoAffix, level, 0f, 1f);
        public static int BossAffixes(int level, bool realmBoss) =>
            realmBoss ? Get(realmBossAffixes, DefRealmBossAffixes, level, 0, 2) : Get(bossAffixes, DefBossAffixes, level, 0, 2);
        public static bool ElitesInPairs(int level) => Get(pairs, DefPairs, level);
        public static float RiftChance(int level) => Get(rift, DefRift, level, 0f, 1f);   // 0 = Chaos Depths' own chance
        public static int ItemLevelBonus(int level) => Get(itemLevel, DefItemLevel, level, 0, 100);
        public static bool ElitesDropRare(int level) => Get(dropRare, DefDropRare, level);
        public static bool RealmBossDropsLegendary(int level) => Get(legendary, DefLegendary, level);

        // What a level adds on top of the one below it, worked out from the settings (so it stays
        // right when they're changed). Enemy health and damage, gems, experience and item level
        // change every level and are in the summary instead; the elite share only when elites
        // first appear.
        public static string Added(int level)
        {
            if (level <= 0)
                return "";
            int below = level - 1;
            var parts = new List<string>();
            if (EliteChance(below) <= 0f && EliteChance(level) > 0f)
                parts.Add($"Elites {Mathf.RoundToInt(EliteChance(level) * 100f)}%");
            Changed(parts, Healing(below), Healing(level), "Your healing {0}");
            if (EnemySpeed(level) != EnemySpeed(below))
                parts.Add($"Enemies move and attack {Mathf.RoundToInt((EnemySpeed(level) - 1f) * 100f)}% faster");
            if (TwoAffixChance(level) != TwoAffixChance(below))
                parts.Add(TwoAffixChance(level) >= 1f ? "Every elite has two affixes" : $"{Mathf.RoundToInt(TwoAffixChance(level) * 100f)}% of elites have two affixes");
            Changed(parts, BossHealth(below), BossHealth(level), "Bosses {0} health");
            Count(parts, BossAffixes(below, false), BossAffixes(level, false), "floor boss");
            Count(parts, BossAffixes(below, true), BossAffixes(level, true), "realm boss");
            Changed(parts, PlayerMaxHealth(below), PlayerMaxHealth(level), "Your max health {0}");
            Changed(parts, Gold(below), Gold(level), "Gold {0}");
            if (RiftChance(level) != RiftChance(below))
                parts.Add($"Rifts {Mathf.RoundToInt(RiftChance(level) * 100f)}%");
            Flag(parts, ElitesInPairs(below), ElitesInPairs(level), "Elites come in pairs");
            Flag(parts, ElitesDropRare(below), ElitesDropRare(level), "Elites drop Rare+");
            Flag(parts, RealmBossDropsLegendary(below), RealmBossDropsLegendary(level), "Realm boss drops a Legendary");
            return string.Join("; ", parts.ToArray());
        }

        private static void Changed(List<string> parts, float before, float now, string format)
        {
            if (now == before)
                return;
            int percent = Mathf.RoundToInt((now - 1f) * 100f);
            parts.Add(string.Format(format, (percent > 0 ? "+" : "") + percent + "%"));
        }

        private static void Count(List<string> parts, int before, int now, string who)
        {
            if (now != before)
                parts.Add(now == 0 ? $"No affix on a {who}" : $"{(now == 1 ? "An" : "Two")} elite affix{(now == 1 ? "" : "es")} on a {who}");
        }

        private static void Flag(List<string> parts, bool before, bool now, string text)
        {
            if (now != before)
                parts.Add(now ? text : "No longer: " + text.ToLowerInvariant());
        }

        // One short line for the game's on-screen notice.
        public static string Summary(int level)
        {
            if (level <= 0)
                return "Off: the normal game";
            int bonus = Mathf.RoundToInt((Gems(level) - 1f) * 100f);
            string added = Added(level);
            return $"{(added.Length > 0 ? $"Adds: {added}. " : "")}Enemies +{Pct(EnemyHealth(level))}% health, +{Pct(EnemyDamage(level))}% damage. Gems and xp +{bonus}%";
        }

        private static int Pct(float multiplier) => Mathf.RoundToInt((multiplier - 1f) * 100f);

        // One line per active modifier.
        public static List<string> Describe(int level)
        {
            var lines = new List<string>();
            if (level <= 0)
            {
                lines.Add("No modifiers - the normal game.");
                return lines;
            }
            lines.Add($"Elites {Mathf.RoundToInt(EliteChance(level) * 100f)}% of enemies");
            Add(lines, EnemyHealth(level), "Enemies have {0} health");
            Add(lines, EnemyDamage(level), "Enemies deal {0} damage");
            Add(lines, EnemySpeed(level), "Enemies move and attack {0} faster");
            Add(lines, BossHealth(level), "Bosses have {0} more health");
            Add(lines, Healing(level), "Your healing {0}");
            Add(lines, PlayerMaxHealth(level), "Your max health {0}");
            Add(lines, Gold(level), "Gold {0}");
            Add(lines, Gems(level), "Chaos gems {0}");
            Add(lines, Experience(level), "Experience {0}");
            lines.Add($"Item level +{ItemLevelBonus(level)}");
            return lines;
        }

        private static void Add(List<string> lines, float multiplier, string format)
        {
            if (multiplier == 1f)
                return;
            int percent = Mathf.RoundToInt((multiplier - 1f) * 100f);
            lines.Add(string.Format(format, (percent > 0 ? "+" : "") + percent + "%"));
        }

        private static float[] Table(Func<int, float> at)
        {
            var t = new float[Max + 1];
            for (int l = 0; l <= Max; l++)
                t[l] = at(l);
            return t;
        }

        private static int[] TableInt(Func<int, int> at)
        {
            var t = new int[Max + 1];
            for (int l = 0; l <= Max; l++)
                t[l] = at(l);
            return t;
        }

        private static bool[] TableBool(Func<int, bool> at)
        {
            var t = new bool[Max + 1];
            for (int l = 0; l <= Max; l++)
                t[l] = at(l);
            return t;
        }
    }
}
