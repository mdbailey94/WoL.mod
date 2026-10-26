using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WoLChaosDepths
{
    // Rolls items from a data file (BepInEx\config\ChaosDepths\loot.json, written with these
    // defaults the first time and editable after): rarity by weight, then that rarity's number of
    // affixes drawn from the pool without repeats, each rolled between its min and max and scaled
    // by item level.
    public static class LootTable
    {
        public static readonly string[] Slots = { "Amulet", "Ring", "Charm" };

        [Serializable]
        public class AffixDef
        {
            public string key;
            public string label;     // shown to the player, {0} = the value
            public bool percent;     // value shown as a percentage
            public float min;        // at item level 1
            public float max;
            public float perLevel;   // added to min and max per item level above 1
            public string suffix;    // "... of <suffix>" when this is the item's strongest affix
            public int weight = 10;
        }

        [Serializable]
        public class RarityDef
        {
            public string name;
            public int weight;
            public int affixes;
            public float valueScale = 1f;
        }

        [Serializable]
        public class Data
        {
            public List<RarityDef> rarities = new List<RarityDef>();
            public List<AffixDef> affixes = new List<AffixDef>();
        }

        public static Data Current { get; private set; }

        public static string FilePath => Path.Combine(SaveStore.Folder, "loot.json");

        public static void Load()
        {
            bool rewrite = false;
            try
            {
                if (File.Exists(FilePath))
                {
                    string text = File.ReadAllText(FilePath);
                    // 0.1.0 wrote an empty "{}" here (JsonUtility can't see plugin classes):
                    // replace that with the real defaults.
                    if (text.Trim() == "{}")
                    {
                        rewrite = true;
                        throw new FormatException("left empty by 0.1.0, rewriting it with the defaults");
                    }
                    Current = Json.Read<Data>(text);
                    if (Current != null && Current.rarities.Count > 0 && Current.affixes.Count > 0)
                    {
                        ChaosDepthsPlugin.Log($"Loot table: {Current.rarities.Count} rarities, {Current.affixes.Count} affixes from {FilePath}");
                        return;
                    }
                    ChaosDepthsPlugin.LogError($"{FilePath} is empty or incomplete; using the defaults");
                }
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Couldn't read {FilePath} ({e.Message}); using the defaults");
            }
            Current = Defaults();
            try
            {
                Directory.CreateDirectory(SaveStore.Folder);
                if (rewrite || !File.Exists(FilePath))
                {
                    File.WriteAllText(FilePath, Json.Write(Current));
                    ChaosDepthsPlugin.Log($"Loot table: wrote the defaults ({Current.rarities.Count} rarities, {Current.affixes.Count} affixes) to {FilePath}");
                }
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Couldn't write {FilePath}: {e.Message}");
            }
        }

        public static Data Defaults()
        {
            var data = new Data();
            data.rarities.Add(new RarityDef { name = "Common", weight = 55, affixes = 1, valueScale = 0.6f });
            data.rarities.Add(new RarityDef { name = "Magic", weight = 30, affixes = 2, valueScale = 1f });
            data.rarities.Add(new RarityDef { name = "Rare", weight = 12, affixes = 3, valueScale = 1.25f });
            data.rarities.Add(new RarityDef { name = "Legendary", weight = 3, affixes = 4, valueScale = 1.6f });
            data.affixes.Add(new AffixDef { key = "Damage", label = "+{0} damage", percent = true, min = 0.04f, max = 0.10f, perLevel = 0.002f, suffix = "Might", weight = 12 });
            data.affixes.Add(new AffixDef { key = "CritChance", label = "+{0} critical chance", percent = true, min = 0.02f, max = 0.05f, perLevel = 0.001f, suffix = "Precision", weight = 10 });
            data.affixes.Add(new AffixDef { key = "CritDamage", label = "+{0} critical damage", percent = true, min = 0.10f, max = 0.25f, perLevel = 0.005f, suffix = "Ruin", weight = 8 });
            data.affixes.Add(new AffixDef { key = "Cooldown", label = "{0} cooldowns", percent = true, min = -0.04f, max = -0.10f, perLevel = -0.002f, suffix = "Haste", weight = 8 });
            data.affixes.Add(new AffixDef { key = "Health", label = "+{0} health", percent = true, min = 0.05f, max = 0.12f, perLevel = 0.003f, suffix = "Vigor", weight = 12 });
            data.affixes.Add(new AffixDef { key = "MoveSpeed", label = "+{0} move speed", percent = true, min = 0.03f, max = 0.08f, perLevel = 0.001f, suffix = "the Wind", weight = 8 });
            data.affixes.Add(new AffixDef { key = "Healing", label = "+{0} healing", percent = true, min = 0.10f, max = 0.25f, perLevel = 0.004f, suffix = "Mending", weight = 6 });
            data.affixes.Add(new AffixDef { key = "DamageTaken", label = "{0} damage taken", percent = true, min = -0.03f, max = -0.08f, perLevel = -0.002f, suffix = "Warding", weight = 8 });
            data.affixes.Add(new AffixDef { key = "Evade", label = "+{0} evasion", percent = true, min = 0.02f, max = 0.05f, perLevel = 0.001f, suffix = "Shadows", weight = 5 });
            return data;
        }

        public static AffixDef Affix(string key)
        {
            foreach (AffixDef def in Current.affixes)
                if (def.key == key)
                    return def;
            return null;
        }

        // A random item for this item level; minRarity (0-based index into rarities) forces at
        // least that rarity, e.g. for a boss drop.
        public static ItemData Roll(int itemLevel, int minRarity = 0)
        {
            Data data = Current ?? (Current = Defaults());
            int rarityIndex = PickRarity(data, minRarity);
            RarityDef rarity = data.rarities[rarityIndex];
            string slot = Slots[UnityEngine.Random.Range(0, Slots.Length)];

            var item = new ItemData
            {
                id = Guid.NewGuid().ToString("N"),
                slot = slot,
                rarity = rarity.name,
                itemLevel = Mathf.Max(1, itemLevel)
            };
            var pool = new List<AffixDef>(data.affixes);
            AffixDef strongest = null;
            float strongestShare = -1f;
            for (int i = 0; i < rarity.affixes && pool.Count > 0; i++)
            {
                AffixDef def = PickWeighted(pool);
                pool.Remove(def);
                float lo = def.min + def.perLevel * (item.itemLevel - 1);
                float hi = def.max + def.perLevel * (item.itemLevel - 1);
                float t = UnityEngine.Random.value;
                float value = Mathf.Lerp(lo, hi, t) * rarity.valueScale;
                item.affixes.Add(new AffixData { key = def.key, value = value });
                if (t > strongestShare)
                {
                    strongestShare = t;
                    strongest = def;
                }
            }
            item.name = strongest != null ? $"{slot} of {strongest.suffix}" : slot;
            return item;
        }

        private static int PickRarity(Data data, int minRarity)
        {
            int total = 0;
            for (int i = minRarity; i < data.rarities.Count; i++)
                total += Mathf.Max(0, data.rarities[i].weight);
            int roll = UnityEngine.Random.Range(0, Mathf.Max(1, total));
            for (int i = minRarity; i < data.rarities.Count; i++)
            {
                roll -= Mathf.Max(0, data.rarities[i].weight);
                if (roll < 0)
                    return i;
            }
            return data.rarities.Count - 1;
        }

        private static AffixDef PickWeighted(List<AffixDef> pool)
        {
            int total = 0;
            foreach (AffixDef def in pool)
                total += Mathf.Max(1, def.weight);
            int roll = UnityEngine.Random.Range(0, total);
            foreach (AffixDef def in pool)
            {
                roll -= Mathf.Max(1, def.weight);
                if (roll < 0)
                    return def;
            }
            return pool[pool.Count - 1];
        }

        // "+12% damage, +4% critical chance"
        public static string Describe(ItemData item)
        {
            var parts = new string[item.affixes.Count];
            for (int i = 0; i < item.affixes.Count; i++)
            {
                AffixData affix = item.affixes[i];
                AffixDef def = Affix(affix.key);
                string value = def != null && def.percent
                    ? (affix.value >= 0 ? "" : "") + Mathf.RoundToInt(affix.value * 100f) + "%"
                    : affix.value.ToString("0.##");
                parts[i] = def != null ? string.Format(def.label, value) : $"{affix.key} {value}";
            }
            return string.Join(", ", parts);
        }

        // A rough strength, to compare two items for the same slot.
        public static float Score(ItemData item)
        {
            float score = 0f;
            foreach (AffixData affix in item.affixes)
            {
                AffixDef def = Affix(affix.key);
                float top = def != null ? Mathf.Abs(def.max + def.perLevel * (item.itemLevel - 1)) : 1f;
                score += top > 0f ? Mathf.Abs(affix.value) / top : 0f;
            }
            return score;
        }
    }
}
