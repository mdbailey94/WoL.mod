using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    // The GEAR tab, v2 (WoL Art's gear_panel_readability_mockup_v2.png, Log - Art 14:25 / 14:32;
    // Matt 14:27 "do it all"). 312 x 254 mockup pixels:
    //   filter row: ALL / AMULET / RING / CHARM as words, the list position, SORT: ... (Y);
    //   WEARING: each slot's label, the worn item's name (rarity colour) and level;
    //   the list: 5 rows of 20, name + level, then a short summary (unique + top stat), and how many
    //     stats are better / worse than what you wear; a scrollbar;
    //   the detail: name, kind + level, the unique on its own line(s) with its numbers in gold, then
    //     BETTER and WORSE tables (stat | this item | the change) side by side;
    //   footer: button chips.
    public partial class InventoryPanel
    {
        private static readonly Color GText = new Color32(0xf2, 0xef, 0xe6, 0xff);
        private static readonly Color GLabel = new Color32(0xa9, 0xab, 0xba, 0xff);
        private static readonly Color GBetter = new Color32(0x7b, 0xe0, 0x5c, 0xff);
        private static readonly Color GWorse = new Color32(0xf2, 0x55, 0x4a, 0xff);
        private static readonly Color GUnique = new Color32(0xf9, 0xd2, 0x3c, 0xff);
        private static readonly Color GBox = new Color32(0x43, 0x45, 0x4e, 0xff);
        private static readonly Color GDark = new Color32(0x3a, 0x3c, 0x43, 0xff);
        private static readonly Color GSelected = new Color32(0x5c, 0x5e, 0x70, 0xff);
        private static readonly Color GChip = new Color32(0x26, 0x27, 0x2c, 0xff);

        // A few words for each unique, for the list's second line.
        private static readonly Dictionary<string, string> UniqueBrief = new Dictionary<string, string>
        {
            { "Chain", "ARCING HITS" }, { "Echo", "HITS STRIKE TWICE" }, { "Pyre", "BURST ON KILL" },
            { "Gale", "SPEED ON KILL" }, { "Leech", "HEAL ON KILL" }, { "Thorn", "THORNS WHEN HIT" },
            { "Ember", "DARTING ORB" }, { "Moss", "WARD" }, { "Void", "SLOWING AURA" }
        };

        private static string StatName(string key)
        {
            switch (key)
            {
                case "Damage": return "DAMAGE";
                case "CritChance": return "CRIT CHANCE";
                case "CritDamage": return "CRIT DAMAGE";
                case "Cooldown": return "COOLDOWN";
                case "Health": return "HEALTH";
                case "MoveSpeed": return "MOVE SPEED";
                case "Healing": return "HEALING";
                case "DamageTaken": return "DAMAGE TAKEN";
                case "ActivationSpeed": return "CAST SPEED";
                case "Evade": return "EVASION";
                default: return key.ToUpperInvariant();
            }
        }

        // "+67%" for a linear stat, "+5 PT" for a point stat (crit chance, cooldown, speed, damage taken).
        private static string StatValue(string key, float v)
        {
            int n = Mathf.RoundToInt(v * 100f);
            return (n > 0 ? "+" : "") + n + (LootTable.IsPoints(key) ? " PT" : "%");
        }

        private static bool LowerIsBetter(string key) => key == "Cooldown" || key == "DamageTaken" || (key != null && key.EndsWith("Cooldown"));

        private struct StatRow
        {
            public string key, name, value;
            public int change;       // in whole points/percent, this item minus the worn one
            public bool better, has, power;
        }

        // Each stat either item has (and the unique powers), compared with the worn item. worn null =
        // an empty slot (everything is a gain); worn == chosen lists the item's own stats.
        private static List<StatRow> CompareRows(ItemData chosen, ItemData worn)
        {
            var rows = new List<StatRow>();
            var keys = new List<string>();
            foreach (AffixData a in chosen.affixes) if (!keys.Contains(a.key)) keys.Add(a.key);
            if (worn != null && worn != chosen)
                foreach (AffixData a in worn.affixes) if (!keys.Contains(a.key)) keys.Add(a.key);
            foreach (string key in keys)
            {
                float mine = 0f, theirs = 0f;
                bool has = false;
                foreach (AffixData a in chosen.affixes) if (a.key == key) { mine += a.value; has = true; }
                if (worn != null && worn != chosen)
                    foreach (AffixData a in worn.affixes) if (a.key == key) theirs += a.value;
                int change = Mathf.RoundToInt(mine * 100f) - Mathf.RoundToInt(theirs * 100f);
                if (change == 0 && worn != chosen)
                    continue;
                rows.Add(new StatRow
                {
                    key = key, name = StatName(key), value = has ? StatValue(key, mine) : "NONE", change = change, has = has,
                    better = LowerIsBetter(key) ? change < 0 : change > 0
                });
            }
            bool myPower = !string.IsNullOrEmpty(chosen.unique), theirPower = worn != null && worn != chosen && !string.IsNullOrEmpty(worn.unique);
            if (myPower && (!theirPower || worn.unique != chosen.unique) && worn != chosen)
                rows.Insert(0, new StatRow { name = "UNIQUE POWER", value = "YES", better = true, has = true, power = true });
            if (theirPower && !myPower)
                rows.Add(new StatRow { name = "UNIQUE POWER", value = "NONE", better = false, has = false, power = true });
            return rows;
        }

        private ItemData WornFor(ItemData item)
        {
            int slot = SlotOf(item);
            return slot >= 0 ? StatMods.FindItem(save, save.equipped[slot]) : null;
        }

        // The list's second line: the unique's few words, then the best stat lines that fit.
        private static string Summary(ItemData item)
        {
            var parts = new List<string>();
            string brief;
            if (!string.IsNullOrEmpty(item.unique) && UniqueBrief.TryGetValue(item.unique, out brief))
                parts.Add(brief);
            foreach (AffixData a in item.affixes)
                parts.Add(LootTable.Describe(new ItemData { affixes = new List<AffixData> { a } }).ToUpperInvariant());
            return string.Join(", ", parts.ToArray());
        }

        // A small solid triangle, 5 x 3 mockup pixels, pointing up or down.
        private static void Tri(Func<float, float, float, float, Rect> R, float x, float y, bool up, Color c)
        {
            for (int i = 0; i < 3; i++)
            {
                int row = up ? i : 2 - i;
                PixelUI.Fill(R(x + 2 - i, y + row, 1 + 2 * i, 1), c);
            }
        }

        private void DrawGearPage(Func<float, float, float, float, Rect> R, int s, int font)
        {
            GUIStyle text = PixelUI.Style(font, GText), label = PixelUI.Style(font, GLabel);
            GUIStyle labelRight = PixelUI.Style(font, GLabel, TextAnchor.UpperRight);
            GUIStyle textCentre = PixelUI.Style(font, GText, TextAnchor.UpperCenter), labelCentre = PixelUI.Style(font, GLabel, TextAnchor.UpperCenter);
            Texture2D inset = Piece("ui_panel_inset_9slice"), slots = Piece("ui_slot_frames");

            // ---- Filter row ----
            string[] words = { "ALL", "AMULET", "RING", "CHARM" };
            for (int i = 0; i < TabNames.Length; i++)
            {
                float tx = 6 + i * 42;
                PixelUI.Fill(R(tx, 19, 38, 11), i == tab ? GSelected : GBox);
                if (i == tab)
                    PixelUI.Fill(R(tx, 29, 38, 1), Gold);
                Label(R(tx, 20, 38, 9), words[i], i == tab ? textCentre : labelCentre);
            }
            rows = GearRows;
            if (selected >= scroll + rows)
                scroll = Mathf.Max(0, selected - rows + 1);
            string count = view.Count > rows
                ? $"{scroll + 1}-{Mathf.Min(scroll + rows, view.Count)} OF {view.Count}"
                : $"{view.Count} ITEM{(view.Count == 1 ? "" : "S")}";
            Label(R(176, 20, 70, 9), count, label);
            Label(R(200, 20, 106, 9), $"SORT: {SortNames[sortMode].ToUpperInvariant()} (Y)", labelRight);

            // ---- WEARING ----
            if (inset != null)
                NineSlice(inset, R(4, 34, 88, 104), s);
            Label(R(8, 37, 80, 9), "WEARING", label);
            for (int i = 0; i < LootTable.Slots.Length; i++)
            {
                float sy = 46 + i * 31;
                ItemData item = StatMods.FindItem(save, save.equipped[i]);
                if (slots != null)
                    Frame(slots, 5, item != null ? 1 + Mathf.Clamp(LootTable.RarityIndex(item.rarity), 0, 3) : 0, R(7, sy + 3, 20, 20));
                if (item != null)
                    Icon(item, R(9, sy + 5, 16, 16));
                else
                {
                    Texture2D empty = Piece("ui_empty_" + LootTable.Slots[i].ToLowerInvariant());
                    if (empty != null)
                        GUI.DrawTexture(R(9, sy + 5, 16, 16), empty);
                }
                Label(R(29, sy, 62, 9), LootTable.Slots[i].ToUpperInvariant(), label);
                if (item != null)
                {
                    GUIStyle st = Rarity(item, font);
                    Label(R(29, sy + 8, 62, 9), Fit(item.name.ToUpperInvariant(), st, 61 * s), st);
                    Label(R(29, sy + 16, 62, 9), $"LV {item.itemLevel}", label);
                }
                else
                    Label(R(29, sy + 8, 62, 9), "EMPTY", label);
            }

            // ---- The list ----
            if (inset != null)
                NineSlice(inset, R(95, 34, 213, 104), s);
            if (view.Count == 0)
                Label(R(101, 40, 196, 9), save.inventory.Count == 0 ? "NOTHING YET" : "NOTHING OF THIS TYPE", label);
            for (int row = 0; row < rows; row++)
            {
                int index = scroll + row;
                if (index >= view.Count)
                    break;
                ItemData item = view[index];
                float y0 = 36 + row * GearRowH;
                bool chosenRow = index == selected;
                PixelUI.Fill(R(98, y0, 202, GearRowH - 1), chosenRow ? GSelected : GBox);
                if (chosenRow)
                    PixelUI.Fill(R(97, y0, 1, GearRowH - 1), Gold);
                if (slots != null)
                    Frame(slots, 5, 1 + Mathf.Clamp(LootTable.RarityIndex(item.rarity), 0, 3), R(100, y0, 19, 19));
                Icon(item, R(102, y0 + 2, 15, 15));
                GUIStyle nameStyle = Rarity(item, font);
                string name = item.name.ToUpperInvariant();
                Label(R(122, y0 + 1, 120, 9), Fit(name, nameStyle, 92 * s), nameStyle);
                labelMeasure.text = Fit(name, nameStyle, 92 * s) + "  ";
                float nameEnd = nameStyle.CalcSize(labelMeasure).x / s;
                Label(R(122 + nameEnd, y0 + 1, 40, 9), $"LV {item.itemLevel}", label);
                Label(R(122, y0 + 9, 176, 9), Fit(Summary(item), chosenRow ? text : label, 176 * s), chosenRow ? text : label);
                // Better / worse counts against what you wear (WORN on the worn one itself).
                ItemData worn = WornFor(item);
                if (worn == item)
                    Label(R(250, y0 + 1, 48, 9), "WORN", PixelUI.Style(font, EquippedMark, TextAnchor.UpperRight));
                else
                {
                    int up = 0, down = 0;
                    foreach (StatRow r in CompareRows(item, worn))
                        if (r.better) up++; else down++;
                    GUIStyle upStyle = PixelUI.Style(font, up > 0 ? GBetter : GLabel, TextAnchor.UpperRight);
                    GUIStyle downStyle = PixelUI.Style(font, down > 0 ? GWorse : GLabel, TextAnchor.UpperRight);
                    Tri(R, 268, y0 + 3, true, up > 0 ? GBetter : GLabel);
                    Label(R(270, y0 + 1, 12, 9), up.ToString(), upStyle);
                    Tri(R, 286, y0 + 3, false, down > 0 ? GWorse : GLabel);
                    Label(R(287, y0 + 1, 12, 9), down.ToString(), downStyle);
                }
            }
            // Scrollbar.
            if (view.Count > rows)
            {
                float trackY = 36, trackH = rows * GearRowH - 1;
                PixelUI.Fill(R(302, trackY, 2, trackH), GDark);
                float thumbH = Mathf.Max(4f, trackH * rows / view.Count);
                float thumbY = trackY + (trackH - thumbH) * scroll / Mathf.Max(1, view.Count - rows);
                PixelUI.Fill(R(302, thumbY, 2, thumbH), GLabel);
            }

            // ---- The detail ----
            if (inset != null)
                NineSlice(inset, R(4, 141, 304, 91), s);
            ItemData chosen = Selected;
            if (chosen != null)
            {
                GUIStyle chosenStyle = Rarity(chosen, font);
                string kind = (string.IsNullOrEmpty(chosen.unique) ? chosen.rarity : "Unique") + " " + chosen.slot;
                Label(R(8, 144, 180, 9), Fit(chosen.name.ToUpperInvariant(), chosenStyle, 170 * s), chosenStyle);
                Label(R(150, 144, 154, 9), $"{kind.ToUpperInvariant()}  LV {chosen.itemLevel}", labelRight);
                float ty = 155;
                string power = Uniques.PowerText(chosen);
                if (power != null)
                {
                    // "UNIQUE:" in gold, then the effect wrapped at words; its numbers in gold.
                    string body = power.StartsWith("Unique: ") ? power.Substring(8) : power;
                    ty = UniqueLine(R, s, font, body.ToUpperInvariant(), ty);
                    PixelUI.Fill(R(8, ty + 1, 296, 1), GBox);
                    ty += 3;
                }
                ItemData worn = WornFor(chosen);
                List<StatRow> all = CompareRows(chosen, worn);
                if (worn == chosen)
                {
                    Label(R(8, ty, 140, 9), "YOU'RE WEARING THIS", label);
                    float ry = ty + 10;
                    foreach (StatRow r in all)
                    {
                        if (ry > 220)
                            break;
                        Label(R(8, ry, 100, 9), r.name, text);
                        Label(R(60, ry, 56, 9), r.value, PixelUI.Style(font, GText, TextAnchor.UpperRight));
                        ry += 10;
                    }
                }
                else
                {
                    Column(R, s, font, all.FindAll(r => r.better), true, 8, ty, worn == null);
                    PixelUI.Fill(R(157, ty, 1, 226 - ty), GBox);
                    int worse = Column(R, s, font, all.FindAll(r => !r.better), false, 162, ty, worn == null);
                    if (worse <= 3)
                    {
                        Label(R(162, 210, 142, 9), "LOWER COOLDOWN AND DAMAGE", label);
                        Label(R(162, 218, 142, 9), "TAKEN COUNT AS BETTER.", label);
                    }
                }
                if (confirmHeldSince >= 0f && !confirmConsumed)
                {
                    float held = Mathf.Clamp01((Time.unscaledTime - confirmHeldSince) / SalvageHold);
                    PixelUI.Fill(R(8, 229, 296 * held, 1), PixelUI.Accent);
                }
            }

            // ---- Footer: button chips ----
            float fx = 8;
            fx = Chip(R, s, font, fx, "A", "EQUIP");
            fx = Chip(R, s, font, fx, "HOLD A", "SALVAGE");
            fx = Chip(R, s, font, fx, "X", "DROP");
            fx = Chip(R, s, font, fx, "Y", "SORT");
            Chip(R, s, font, fx, "F", "FORGE");
        }

        // One of the two tables; returns how many rows it has.
        private int Column(Func<float, float, float, float, Rect> R, int s, int font, List<StatRow> rows, bool better, float x, float y, bool emptySlot)
        {
            Color head = better ? GBetter : GWorse;
            string title = better ? "BETTER" : "WORSE";
            GUIStyle headStyle = PixelUI.Style(font, head);
            Label(R(x, y, 40, 9), title, headStyle);
            labelMeasure.text = title + " ";
            float after = headStyle.CalcSize(labelMeasure).x / s;
            Label(R(x + after, y, 142 - after, 9), emptySlot ? (better ? "SLOT IS EMPTY" : "") : "THAN WHAT YOU WEAR", PixelUI.Style(font, GLabel));
            float ry = y + 10;
            int shown = 0;
            if (rows.Count == 0)
                Label(R(x, ry, 142, 9), "NOTHING", PixelUI.Style(font, GLabel));
            foreach (StatRow r in rows)
            {
                if (ry > 220)
                    break;
                Label(R(x, ry, 90, 9), r.name, PixelUI.Style(font, GText));
                Label(R(x + 60, ry, 48, 9), r.value, PixelUI.Style(font, r.has ? GText : GLabel, TextAnchor.UpperRight));
                Color c = r.better ? GBetter : GWorse;
                string change = r.power ? (r.better ? "GAIN" : "LOSE") : (r.change > 0 ? "+" : "") + r.change;
                Tri(R, x + 113, ry + 2, r.better, c);   // up = better, as in the mockup
                Label(R(x + 120, ry, 22, 9), change, PixelUI.Style(font, c, TextAnchor.UpperRight));
                ry += 10;
                shown++;
            }
            return shown;
        }

        // The unique's effect over up to two lines, at word boundaries; numbers in gold.
        private float UniqueLine(Func<float, float, float, float, Rect> R, int s, int font, string body, float y)
        {
            GUIStyle gold = PixelUI.Style(font, GUnique), plain = PixelUI.Style(font, GText);
            float x0 = 8, right = 304, x = x0;
            Label(R(x, y, 40, 9), "UNIQUE:", gold);
            labelMeasure.text = "UNIQUE: ";
            x += gold.CalcSize(labelMeasure).x / s;
            int line = 0;
            foreach (string word in body.Split(' '))
            {
                if (word.Length == 0)
                    continue;
                bool number = word.IndexOfAny("0123456789".ToCharArray()) >= 0;
                GUIStyle st = number ? gold : plain;
                labelMeasure.text = word + " ";
                float ww = st.CalcSize(labelMeasure).x / s;
                if (x + ww > right && x > x0)
                {
                    if (++line > 1)
                        break;
                    x = x0;
                    y += 8;
                }
                Label(R(x, y, ww + 2, 9), word, st);
                x += ww;
            }
            return y + 9;
        }

        // A dark chip with the button's name, then what it does; returns the next x.
        private float Chip(Func<float, float, float, float, Rect> R, int s, int font, float x, string button, string action)
        {
            GUIStyle key = PixelUI.Style(font, GText, TextAnchor.UpperCenter), what = PixelUI.Style(font, GLabel);
            labelMeasure.text = button;
            float kw = key.CalcSize(labelMeasure).x / s + 4;
            PixelUI.Fill(R(x, 236, kw, 11), GLabel);
            PixelUI.Fill(R(x + 1, 237, kw - 2, 9), GChip);
            Label(R(x, 237, kw, 9), button, key);
            labelMeasure.text = action;
            float aw = what.CalcSize(labelMeasure).x / s;
            Label(R(x + kw + 3, 237, aw + 2, 9), action, what);
            return x + kw + 3 + aw + 10;
        }
    }
}
