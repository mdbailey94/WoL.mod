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
            public string slot = "";  // 0.40.0: the slot whose theme it belongs to ("" = any)
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
            public int version;   // 2 = the simplified 9-stat table (0.40.0)
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
                        // 0.40.0 (Matt 14:17, "a general simplification of item stats"): an older
                        // table gets the 9-stat affix list with slot themes; its rarities are kept.
                        if (Current.version < TableVersion)
                        {
                            Current.affixes = Defaults().affixes;
                            Current.version = TableVersion;
                            File.WriteAllText(FilePath, Json.Write(Current));
                            ChaosDepthsPlugin.Log($"Loot table: {FilePath} moved to the simplified 9 stats (rarities kept)");
                        }
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

        public const int TableVersion = 2;

        // One stat of the table: whole points, a short line ("+8 DMG"), and the slot theme it
        // rolls on (Matt 14:17: one theme per slot).
        private static AffixDef Stat(string slot, string key, string label, float min, float max, float perLevel, string suffix, int weight) => new AffixDef
        { slot = slot, key = key, label = label, percent = true, min = min, max = max, perLevel = perLevel, suffix = suffix, weight = weight };

        public static Data Defaults()
        {
            var data = new Data();
            // The fresh save's own loot.json starts with WoL Player's balanced weights (#31:
            // 62 / 28 / 8.5 / 1.5, doubled to whole numbers); the Debugging save keeps 55 / 30 / 12 / 3.
            bool fresh = ProfileSave.Fresh;
            data.rarities.Add(new RarityDef { name = "Common", weight = fresh ? 124 : 55, affixes = 1, valueScale = 0.6f });
            data.rarities.Add(new RarityDef { name = "Magic", weight = fresh ? 56 : 30, affixes = 2, valueScale = 1f });
            data.rarities.Add(new RarityDef { name = "Rare", weight = fresh ? 17 : 12, affixes = 3, valueScale = 1.25f });
            data.rarities.Add(new RarityDef { name = "Legendary", weight = 3, affixes = 4, valueScale = 1.6f });
            // 0.40.0 (Matt 14:17): 9 stats, 3 per slot. AMULET = offense, RING = utility,
            // CHARM = defense. Values and weights as before; the 11 arcana-type stats and evade are gone
            // (old items convert, SaveData.Upgrade). A Legendary's 4th stat comes from another slot.
            data.version = TableVersion;
            data.affixes.Add(Stat("Amulet", "Damage", "+{0} DMG", 0.04f, 0.10f, 0.002f, "Might", 12));
            data.affixes.Add(Stat("Amulet", "CritChance", "+{0} CRIT", 0.02f, 0.05f, 0.001f, "Precision", 10));
            data.affixes.Add(Stat("Amulet", "CritDamage", "+{0} CRIT DMG", 0.10f, 0.25f, 0.005f, "Ruin", 8));
            data.affixes.Add(Stat("Ring", "Cooldown", "{0} COOLDOWN", -0.04f, -0.10f, -0.002f, "Haste", 8));
            data.affixes.Add(Stat("Ring", "MoveSpeed", "+{0} SPEED", 0.03f, 0.08f, 0.001f, "the Wind", 8));
            data.affixes.Add(Stat("Ring", "ActivationSpeed", "+{0} CAST SPEED", 0.04f, 0.10f, 0.002f, "Swiftness", 6));
            data.affixes.Add(Stat("Charm", "Health", "+{0} HEALTH", 0.05f, 0.12f, 0.003f, "Vigor", 12));
            data.affixes.Add(Stat("Charm", "DamageTaken", "{0} DMG TAKEN", -0.03f, -0.08f, -0.002f, "Warding", 8));
            data.affixes.Add(Stat("Charm", "Healing", "+{0} HEALING", 0.10f, 0.25f, 0.004f, "Mending", 6));
            return data;
        }

        public static int RarityIndex(string name)
        {
            Data data = Current ?? Defaults();
            for (int i = 0; i < data.rarities.Count; i++)
                if (data.rarities[i].name == name)
                    return i;
            return 0;
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
        // rarityBoost scales the weight of Magic and better (0.3 = +30%), e.g. from Ascension.
        public static ItemData Roll(int itemLevel, int minRarity = 0, float rarityBoost = 0f, int exactRarity = -1, string slot = null)
        {
            Data data = Current ?? (Current = Defaults());
            int rarityIndex = exactRarity >= 0 ? Mathf.Clamp(exactRarity, 0, data.rarities.Count - 1) : PickRarity(data, minRarity, rarityBoost);
            RarityDef rarity = data.rarities[rarityIndex];
            bool chosen = !string.IsNullOrEmpty(slot);
            if (!chosen)
                slot = Slots[UnityEngine.Random.Range(0, Slots.Length)];

            var item = new ItemData
            {
                id = Guid.NewGuid().ToString("N"),
                slot = slot,
                rarity = rarity.name,
                itemLevel = Mathf.Max(1, itemLevel)
            };
            // A unique picks its own slot, so it's decided first and the stats follow that slot's
            // theme (a slot chosen by the Forge only takes a unique of that slot).
            if (rarityIndex == data.rarities.Count - 1)
            {
                Uniques.MaybeMakeUnique(item);
                if (chosen && item.slot != slot)
                {
                    item.unique = "";
                    item.slot = slot;
                }
            }
            var taken = new List<string>();
            AffixDef strongest = null;
            float strongestShare = -1f;
            for (int i = 0; i < rarity.affixes; i++)
            {
                AffixDef def;
                float t;
                AffixData affix = RollAffix(data, rarity, item.itemLevel, taken, item.slot, out def, out t);
                if (affix == null)
                    break;
                taken.Add(affix.key);
                item.affixes.Add(affix);
                if (t > strongestShare)
                {
                    strongestShare = t;
                    strongest = def;
                }
            }
            if (string.IsNullOrEmpty(item.unique))
                item.name = strongest != null ? $"{item.slot} of {strongest.suffix}" : item.slot;
            return item;
        }

        // One affix rolled for an item of this rarity and level, never one of the keys in exclude
        // (the Forge's reroll and upgrade use it too); null when every affix is excluded.
        // 0.40.0: slot = the item's slot: its theme's stats first; once they're all on the item (a
        // Legendary's 4th line), any other stat.
        public static AffixData RollAffix(Data data, RarityDef rarity, int itemLevel, ICollection<string> exclude, string slot = null)
        {
            AffixDef def;
            float share;
            return RollAffix(data, rarity, itemLevel, exclude, slot, out def, out share);
        }

        private static AffixData RollAffix(Data data, RarityDef rarity, int itemLevel, ICollection<string> exclude, string slot, out AffixDef def, out float share)
        {
            var pool = new List<AffixDef>();
            foreach (AffixDef a in data.affixes)
                if ((exclude == null || !exclude.Contains(a.key)) && (string.IsNullOrEmpty(slot) || a.slot == slot))
                    pool.Add(a);
            if (pool.Count == 0 && !string.IsNullOrEmpty(slot))
                foreach (AffixDef a in data.affixes)
                    if (exclude == null || !exclude.Contains(a.key))
                        pool.Add(a);
            def = null;
            share = 0f;
            if (pool.Count == 0)
                return null;
            def = PickWeighted(pool);
            float lo = def.min + def.perLevel * (itemLevel - 1);
            float hi = def.max + def.perLevel * (itemLevel - 1);
            share = UnityEngine.Random.value;
            return new AffixData { key = def.key, value = Whole(Mathf.Lerp(lo, hi, share) * rarity.valueScale) };
        }

        // Whole points (Matt 14:17): 0.0837 -> 0.08; never rounded to 0 (at least 1 point).
        public static float Whole(float value)
        {
            float points = Mathf.Round(value * 100f);
            if (Mathf.Approximately(points, 0f) && !Mathf.Approximately(value, 0f))
                points = Mathf.Sign(value);
            return points / 100f;
        }

        // "{slot} of {suffix}" after the item's affixes changed (the Forge): the suffix of the affix
        // rolled highest within its range. A unique keeps its own name.
        public static void Rename(ItemData item)
        {
            if (!string.IsNullOrEmpty(item.unique))
                return;
            AffixDef strongest = null;
            float best = -1f;
            foreach (AffixData affix in item.affixes)
            {
                AffixDef def = Affix(affix.key);
                if (def == null)
                    continue;
                float top = Mathf.Abs(def.max + def.perLevel * (item.itemLevel - 1));
                float share = top > 0f ? Mathf.Abs(affix.value) / top : 0f;
                if (share > best)
                {
                    best = share;
                    strongest = def;
                }
            }
            item.name = strongest != null && !string.IsNullOrEmpty(strongest.suffix) ? $"{item.slot} of {strongest.suffix}" : item.slot;
        }

        private static float Weight(Data data, int index, float rarityBoost)
        {
            float w = Mathf.Max(0, data.rarities[index].weight);
            return index > 0 ? w * (1f + Mathf.Max(0f, rarityBoost)) : w;
        }

        private static int PickRarity(Data data, int minRarity, float rarityBoost)
        {
            float total = 0f;
            for (int i = minRarity; i < data.rarities.Count; i++)
                total += Weight(data, i, rarityBoost);
            float roll = UnityEngine.Random.Range(0f, Mathf.Max(0.0001f, total));
            for (int i = minRarity; i < data.rarities.Count; i++)
            {
                roll -= Weight(data, i, rarityBoost);
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
        public static bool IsPoints(string key) =>
            key == "CritChance" || key == "Evade" || key == "DamageTaken" || key == "MoveSpeed" || (key != null && key.EndsWith("Cooldown"));

        public static string Describe(ItemData item)
        {
            var parts = new string[item.affixes.Count];
            for (int i = 0; i < item.affixes.Count; i++)
            {
                AffixData affix = item.affixes[i];
                AffixDef def = Affix(affix.key);
                // #114: point stats (crit chance, cooldowns, evade, damage taken, move speed) show as
                // points; the effective % is on the stat sheet, since points from all gear add up first.
                // 0.40.0: whole numbers, short ("+8 DMG", "-6 COOLDOWN"); the stat sheet's TOTALS
                // show what they come to.
                string value = def != null && def.percent
                    ? Mathf.RoundToInt(affix.value * 100f).ToString()
                    : affix.value.ToString("0.##");
                parts[i] = def != null ? string.Format(def.label, value) : $"{affix.key} {value}";
            }
            string power = Uniques.PowerText(item);
            string affixes = string.Join(", ", parts);
            return power != null ? power + (affixes.Length > 0 ? "; " + affixes : "") : affixes;
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
            if (!string.IsNullOrEmpty(item.unique))
                score += 1.5f; // a power is worth more than an affix
            return score;
        }
    }
}
