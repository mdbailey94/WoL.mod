using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    // #79 THE FORGE (Matt 2026-11-03), the Chaos Dust sink: the logic behind the GEAR tab's FORGE
    // sub-tab (WoL Modder's UI in InventoryPanel calls these and saves after a success; nothing
    // here saves).
    //   Reroll: one affix of an item rolls again (any affix the item doesn't already have, the
    //     same one included), for 20 / 40 / 80 / 150 dust by rarity.
    //   Upgrade: Magic -> Rare (150 dust) -> Legendary (400). The affixes keep their rolls, scaled
    //     to the new rarity, and one new affix is rolled. It never turns into a unique.
    //   Transmute: 3 items of one rarity (none equipped) -> 1 random item of that rarity for a
    //     chosen slot, at their average item level. A unique that can't go in that slot is
    //     rolled again.
    // Each can be checked first (Cant... gives the reason, or null), and each returns a Result
    // with a line to show.
    public static class Forge
    {
        public enum Op { Reroll, Upgrade, Transmute }

        public class Result
        {
            public bool ok;
            public string message;
            public ItemData item;   // the changed item (reroll, upgrade) or the new one (transmute)
            public int spent;
        }

        // By rarity index (Common, Magic, Rare, Legendary).
        private static readonly int[] RerollCosts = { 20, 40, 80, 150 };
        // By the rarity upgraded from: Magic -> Rare 150, Rare -> Legendary 400; -1 can't.
        private static readonly int[] UpgradeCosts = { -1, 150, 400, -1 };
        public const int TransmuteCost = 0;
        public const int TransmuteCount = 3;

        private static LootTable.Data Data => LootTable.Current ?? LootTable.Defaults();

        public static int Cost(Op op, ItemData item)
        {
            int rarity = item != null ? LootTable.RarityIndex(item.rarity) : 0;
            switch (op)
            {
                case Op.Reroll:
                    return RerollCosts[Mathf.Clamp(rarity, 0, RerollCosts.Length - 1)];
                case Op.Upgrade:
                    return rarity >= 0 && rarity < UpgradeCosts.Length && rarity + 1 < Data.rarities.Count ? UpgradeCosts[rarity] : -1;
                default:
                    return TransmuteCost;
            }
        }

        public static bool CanAfford(CharacterSave save, int cost) => save != null && cost >= 0 && save.dust >= cost;

        // ---- Reroll ----

        public static string CantReroll(CharacterSave save, ItemData item, int affixIndex)
        {
            if (save == null || item == null)
                return "No item";
            if (affixIndex < 0 || affixIndex >= item.affixes.Count)
                return "Pick an affix";
            int cost = Cost(Op.Reroll, item);
            return CanAfford(save, cost) ? null : $"Needs {cost} dust";
        }

        public static Result Reroll(CharacterSave save, ItemData item, int affixIndex)
        {
            string why = CantReroll(save, item, affixIndex);
            if (why != null)
                return Fail(why);
            LootTable.Data data = Data;
            LootTable.RarityDef rarity = data.rarities[Mathf.Clamp(LootTable.RarityIndex(item.rarity), 0, data.rarities.Count - 1)];
            var others = new List<string>();
            for (int i = 0; i < item.affixes.Count; i++)
                if (i != affixIndex)
                    others.Add(item.affixes[i].key);
            AffixData rolled = LootTable.RollAffix(data, rarity, item.itemLevel, others, item.slot);   // 0.40.0: the slot's theme (Modder)
            if (rolled == null)
                return Fail("No affix to roll");
            int cost = Cost(Op.Reroll, item);
            string was = Line(item.affixes[affixIndex]);
            item.affixes[affixIndex] = rolled;
            LootTable.Rename(item);
            save.dust -= cost;
            return Done(item, cost, $"Rerolled {was} into {Line(rolled)} (-{cost} dust)");
        }

        // ---- Upgrade ----

        public static string CantUpgrade(CharacterSave save, ItemData item)
        {
            if (save == null || item == null)
                return "No item";
            int cost = Cost(Op.Upgrade, item);
            if (cost < 0)
                return LootTable.RarityIndex(item.rarity) >= Data.rarities.Count - 1 ? "Already Legendary" : "Only Magic and Rare upgrade";
            return CanAfford(save, cost) ? null : $"Needs {cost} dust";
        }

        public static Result Upgrade(CharacterSave save, ItemData item)
        {
            string why = CantUpgrade(save, item);
            if (why != null)
                return Fail(why);
            LootTable.Data data = Data;
            int from = LootTable.RarityIndex(item.rarity);
            LootTable.RarityDef old = data.rarities[from], next = data.rarities[from + 1];
            int cost = Cost(Op.Upgrade, item);
            float scale = old.valueScale > 0f ? next.valueScale / old.valueScale : 1f;
            foreach (AffixData affix in item.affixes)
                affix.value = LootTable.Whole(affix.value * scale);   // 0.40.0: whole points (Modder)
            var keys = new List<string>();
            foreach (AffixData affix in item.affixes)
                keys.Add(affix.key);
            string added = null;
            while (item.affixes.Count < next.affixes)
            {
                AffixData rolled = LootTable.RollAffix(data, next, item.itemLevel, keys, item.slot);   // 0.40.0: theme, then any (Modder)
                if (rolled == null)
                    break;
                item.affixes.Add(rolled);
                keys.Add(rolled.key);
                added = added == null ? Line(rolled) : added + ", " + Line(rolled);
            }
            item.rarity = next.name;
            LootTable.Rename(item);
            save.dust -= cost;
            return Done(item, cost, $"Upgraded to {next.name}{(added != null ? ", new: " + added : "")} (-{cost} dust)");
        }

        // ---- Transmute ----

        public static string CantTransmute(CharacterSave save, IList<ItemData> items, string slot)
        {
            if (save == null || items == null || items.Count != TransmuteCount)
                return $"Pick {TransmuteCount} items";
            if (System.Array.IndexOf(LootTable.Slots, slot) < 0)
                return "Pick a slot";
            var ids = new HashSet<string>();
            foreach (ItemData item in items)
            {
                if (item == null || !save.inventory.Contains(item))
                    return "Pick items from the bag";
                if (!ids.Add(item.id))
                    return $"Pick {TransmuteCount} different items";
                if (save.equipped.Contains(item.id))
                    return "Unequip it first";
                if (item.rarity != items[0].rarity)
                    return "All of one rarity";
            }
            return CanAfford(save, TransmuteCost) ? null : $"Needs {TransmuteCost} dust";
        }

        public static Result Transmute(CharacterSave save, IList<ItemData> items, string slot)
        {
            string why = CantTransmute(save, items, slot);
            if (why != null)
                return Fail(why);
            int rarity = LootTable.RarityIndex(items[0].rarity);
            int level = 0;
            foreach (ItemData item in items)
                level += item.itemLevel;
            level = Mathf.Max(1, Mathf.RoundToInt(level / (float)items.Count));
            // 0.40.0 (Modder): rolled for that slot, so its stats follow the slot's theme (a unique
            // of another slot becomes a plain Legendary, as before).
            ItemData made = LootTable.Roll(level, exactRarity: rarity, slot: slot);
            foreach (ItemData item in new List<ItemData>(items))
                save.inventory.Remove(item);
            save.inventory.Add(made);
            save.dust -= TransmuteCost;
            return Done(made, TransmuteCost, $"Transmuted {TransmuteCount} {items[0].rarity} items into {made.name}");
        }

        // ---- Helpers ----

        // "+12% damage"
        private static string Line(AffixData affix) => LootTable.Describe(new ItemData { affixes = new List<AffixData> { affix } });

        private static Result Fail(string why) => new Result { ok = false, message = why };

        private static Result Done(ItemData item, int spent, string message)
        {
            ChaosDepthsPlugin.Log("Forge: " + message);
            return new Result { ok = true, item = item, spent = spent, message = message };
        }
    }
}
