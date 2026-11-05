using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    // #79 THE FORGE: the GEAR tab's FORGE sub-tab (WoL Art's forge_subtab_1x.png), on WoL
    // Player's Forge.cs logic. F (or the pad's Back / Select) switches the GEAR tab between ITEMS
    // and FORGE. The forge works on the item chosen in the list; left / right step to the
    // previous / next item. Up / down move a cursor over the item's affixes (A rerolls the one
    // under it), then UPGRADE (A upgrades its rarity) and TRANSMUTE (A adds or takes the item off
    // the three picks; with three picked, A transmutes them into one item for the slot Y picks;
    // X clears the picks). The character is saved after every success.
    public partial class InventoryPanel
    {
        private static readonly string[] SelectNames = { "back", "select", "view", "share", "minus" };
        private static readonly Color Cant = new Color32(0xe0, 0x50, 0x50, 0xff);
        private bool forgeOpen;
        private int forgeCursor;            // 0..affixes-1: reroll that affix; then upgrade; then transmute
        private readonly List<ItemData> picks = new List<ItemData>();
        private int pickSlot;
        private float nextForgeRepeat;
        private string forgeMessage;
        private bool forgeMessageOk;

        private int ForgeAffixRows => Selected != null ? Selected.affixes.Count : 0;
        private int UpgradeRow => ForgeAffixRows;
        private int TransmuteRow => ForgeAffixRows + 1;

        // From the panel's Update on the GEAR tab: F / Back toggles the sub-tab. True while the
        // forge has the input.
        private bool ForgeToggleAndInput(Rewired.Player pad, bool useKeys)
        {
            if ((useKeys && Input.GetKeyDown(KeyCode.F)) || PadButton(pad, 6, SelectNames))
            {
                forgeOpen = !forgeOpen;
                forgeMessage = null;
                forgeCursor = 0;
                ChaosDepthsPlugin.Log($"Panel: GEAR / {(forgeOpen ? "FORGE" : "ITEMS")} (player {player.playerID + 1})");
            }
            if (!forgeOpen)
                return false;
            ForgeInput(pad, useKeys);
            return true;
        }

        private void ForgeInput(Rewired.Player pad, bool useKeys)
        {
            bool Key(KeyCode k) => useKeys && Input.GetKey(k);
            bool KeyDown(KeyCode k) => useKeys && Input.GetKeyDown(k);
            picks.RemoveAll(p => p == null || !save.inventory.Contains(p));

            float horizontal = 0f, vertical = 0f;
            if (Key(KeyCode.LeftArrow) || Key(KeyCode.A))
                horizontal = -1f;
            else if (Key(KeyCode.RightArrow) || Key(KeyCode.D))
                horizontal = 1f;
            if (Key(KeyCode.DownArrow) || Key(KeyCode.S))
                vertical = -1f;
            else if (Key(KeyCode.UpArrow) || Key(KeyCode.W))
                vertical = 1f;
            if (pad != null && horizontal == 0f && vertical == 0f)
            {
                try
                {
                    horizontal = pad.GetAxis("MoveHorizontal");
                    vertical = pad.GetAxis("MoveVertical");
                }
                catch
                {
                }
            }
            if (Mathf.Abs(horizontal) > 0.5f || Mathf.Abs(vertical) > 0.5f)
            {
                if (Time.unscaledTime >= nextForgeRepeat)
                {
                    bool first = nextForgeRepeat == 0f;
                    nextForgeRepeat = Time.unscaledTime + (first ? RepeatFirst : RepeatNext * 2f);
                    if (Mathf.Abs(vertical) > Mathf.Abs(horizontal))
                    {
                        // Stops at the first and last row rather than wrapping (Matt 2026-11-03).
                        forgeCursor = Mathf.Clamp(forgeCursor + (vertical < 0f ? 1 : -1), 0, TransmuteRow);
                    }
                    else if (view.Count > 0)
                    {
                        // The previous / next item in the list (the list's own order and tab).
                        bool onTransmute = forgeCursor == TransmuteRow;
                        selected = Mathf.Clamp(selected + (horizontal > 0f ? 1 : -1), 0, view.Count - 1);
                        if (selected < scroll)
                            scroll = selected;
                        if (selected >= scroll + rows)
                            scroll = selected - rows + 1;
                        // Stay on the same action; an affix row past the new item's last affix
                        // moves to its last one.
                        forgeCursor = onTransmute ? TransmuteRow : Mathf.Min(forgeCursor, UpgradeRow);
                    }
                    forgeMessage = null;
                }
            }
            else
                nextForgeRepeat = 0f;
            forgeCursor = Mathf.Clamp(forgeCursor, 0, TransmuteRow);

            // Y (or Tab): the transmuted item's slot. X (or G): clear the picks.
            if (PadY(pad) || KeyDown(KeyCode.Tab))
                pickSlot = (pickSlot + 1) % LootTable.Slots.Length;
            if (PadButton(pad, 2, XNames) || KeyDown(KeyCode.G))
            {
                picks.Clear();
                forgeMessage = null;
            }

            // A (Enter / Space): the action under the cursor.
            if (Pressed(pad, "Confirm") || KeyDown(KeyCode.Return) || KeyDown(KeyCode.Space))
                ForgeAct();
        }

        private void ForgeAct()
        {
            ItemData item = Selected;
            Forge.Result result;
            if (forgeCursor == TransmuteRow)
            {
                if (picks.Count < Forge.TransmuteCount)
                {
                    // Not three yet: the current item goes on or comes off the picks.
                    if (item == null)
                        return;
                    if (picks.Contains(item))
                        picks.Remove(item);
                    else if (IsEquipped(item))
                        Say("Unequip it first", false);
                    else if (picks.Count > 0 && picks[0].rarity != item.rarity)
                        Say("All of one rarity", false);
                    else
                        picks.Add(item);
                    return;
                }
                result = Forge.Transmute(save, picks, LootTable.Slots[pickSlot]);
                if (result.ok)
                {
                    picks.Clear();
                    Rebuild();
                    int at = view.IndexOf(result.item);
                    if (at >= 0)
                        selected = at;
                }
            }
            else if (item == null)
                return;
            else if (forgeCursor == UpgradeRow)
                result = Forge.Upgrade(save, item);
            else
                result = Forge.Reroll(save, item, forgeCursor);
            Say(result.message, result.ok);
            if (!result.ok)
                return;
            Rebuild();
            onChanged(player, save);
        }

        private void Say(string message, bool ok)
        {
            forgeMessage = message;
            forgeMessageOk = ok;
        }

        // The FORGE sub-tab's body, under the panel's tab row, in the mockup's pixels (R). With no
        // art pieces (the coded look), inset and slots are null and plain frames are drawn instead.
        private void DrawForge(Func<float, float, float, float, Rect> R, int s, int font, Texture2D inset, Texture2D slots)
        {
            GUIStyle text = PixelUI.Style(font, PixelUI.Text);
            GUIStyle dim = PixelUI.Style(font, PixelUI.Dim);
            GUIStyle dimRight = PixelUI.Style(font, PixelUI.Dim, TextAnchor.UpperRight);
            GUIStyle accent = PixelUI.Style(font, PixelUI.Accent);
            void Box(Rect r)
            {
                if (inset != null)
                    NineSlice(inset, r, s);
                else
                {
                    PixelUI.Fill(r, new Color(0f, 0f, 0f, 0.25f));
                    Frame(r, PixelUI.InnerLine, s);
                }
            }
            void Slot(Rect r, ItemData it)
            {
                int frame = it != null ? 1 + Mathf.Clamp(LootTable.RarityIndex(it.rarity), 0, 3) : 0;
                if (slots != null)
                    Frame(slots, 5, frame, r);
                else
                    Frame(r, it != null ? RarityColour(it) : PixelUI.InnerLine, s);
                if (it != null)
                    Icon(it, new Rect(r.x + 2 * s, r.y + 2 * s, r.width - 4 * s, r.height - 4 * s));
            }
            void Cost(Rect row, int cost, bool afford)
            {
                if (cost < 0)
                {
                    Label(row, "MAX", dimRight);
                    return;
                }
                Texture2D dust = Piece("ui_chaos_dust_8");
                Label(row, cost.ToString(), PixelUI.Style(font, afford ? PixelUI.Text : Cant, TextAnchor.UpperRight));
                if (dust != null)
                    GUI.DrawTexture(new Rect(row.xMax - 24 * s, row.y + s, 8 * s, 8 * s), dust);
            }

            ItemData item = Selected;
            picks.RemoveAll(p => p == null || !save.inventory.Contains(p));

            // The sub-tab row: ITEMS | FORGE, with the dust on the right.
            Texture2D tabs = Piece("ui_panel_tab");
            if (tabs != null)
            {
                Frame(tabs, 3, 0, R(8, 16, PanelTabW, 10));
                Frame(tabs, 3, 1, R(8 + PanelTabW + 2, 16, PanelTabW, 10));
            }
            Label(R(8, 17, PanelTabW, 9), "ITEMS", PixelUI.Style(font, PixelUI.Dim, TextAnchor.UpperCenter));
            Label(R(8 + PanelTabW + 2, 17, PanelTabW, 9), "FORGE", PixelUI.Style(font, PixelUI.Accent, TextAnchor.UpperCenter));
            Label(R(118, 17, 108, 9), $"DUST {save.dust}", PixelUI.Style(font, PixelUI.Text, TextAnchor.UpperRight));

            // Left: the item and its affixes, the cursor's affix lit.
            Box(R(4, 29, 88, 84));
            Label(R(8, 31, 80, 9), "ITEM", dim);
            Slot(R(8, 40, 20, 20), item);
            if (item == null)
                Label(R(31, 45, 58, 9), "NO ITEM", dim);
            else
            {
                GUIStyle nameStyle = Rarity(item, font);
                Label(R(31, 41, 58, 9), Fit(item.name.ToUpperInvariant(), nameStyle, 58 * s), nameStyle);
                Label(R(31, 50, 58, 9), $"{item.rarity.ToUpperInvariant()}  LV {item.itemLevel}", dim);
                Label(R(8, 63, 80, 9), "AFFIXES", dim);
                for (int i = 0; i < item.affixes.Count && i < 4; i++)
                {
                    float ay = 72 + i * 10;
                    if (forgeCursor == i)
                    {
                        PixelUI.Fill(R(7, ay - 1, 83, 10), RowLight);
                        PixelUI.Fill(R(6, ay - 1, 1, 10), Gold);
                    }
                    string line = LootTable.Describe(new ItemData { affixes = new List<AffixData> { item.affixes[i] } });
                    Label(R(9, ay, 80, 9), Fit(line.ToUpperInvariant(), text, 80 * s), PixelUI.Style(font, Gain));
                }
            }

            // Right: the three actions.
            Box(R(96, 29, 133, 84));
            bool onReroll = forgeCursor < UpgradeRow, onUpgrade = forgeCursor == UpgradeRow, onTransmute = forgeCursor == TransmuteRow;
            void RowLit(float ry, bool lit)
            {
                if (!lit)
                    return;
                PixelUI.Fill(R(99, ry - 1, 127, 10), RowLight);
                PixelUI.Fill(R(98, ry - 1, 1, 10), Gold);
            }
            int rerollCost = item != null ? Forge.Cost(Forge.Op.Reroll, item) : 0;
            RowLit(33, onReroll);
            Label(R(101, 33, 100, 9), "REROLL AFFIX", onReroll ? text : dim);
            if (item != null)
                Cost(R(101, 33, 123, 9), rerollCost, Forge.CanAfford(save, rerollCost));
            int upgradeCost = item != null ? Forge.Cost(Forge.Op.Upgrade, item) : -1;
            int next = item != null ? LootTable.RarityIndex(item.rarity) + 1 : -1;
            var rarities = (LootTable.Current ?? LootTable.Defaults()).rarities;
            string upgradeTo = next > 0 && next < rarities.Count ? rarities[next].name.ToUpperInvariant() : "MAX";
            RowLit(45, onUpgrade);
            Label(R(101, 45, 100, 9), upgradeCost < 0 ? "UPGRADE" : $"UPGRADE TO {upgradeTo}", upgradeCost < 0 ? dim : PixelUI.Style(font, onUpgrade ? PixelUI.Accent : PixelUI.Text));
            if (item != null)
                Cost(R(101, 45, 123, 9), upgradeCost, Forge.CanAfford(save, upgradeCost));
            RowLit(58, onTransmute);
            string pickRarity = picks.Count > 0 ? picks[0].rarity.ToUpperInvariant() : item != null ? item.rarity.ToUpperInvariant() : "";
            Label(R(101, 58, 120, 9), $"TRANSMUTE {Forge.TransmuteCount} {pickRarity}", onTransmute ? text : dim);
            for (int i = 0; i < Forge.TransmuteCount; i++)
                Slot(R(101 + i * 23, 70, 20, 20), i < picks.Count ? picks[i] : null);
            Label(R(171, 75, 10, 9), ">", accent);
            Slot(R(183, 70, 20, 20), null);
            Label(R(183, 75, 20, 9), "?", PixelUI.Style(font, PixelUI.Accent, TextAnchor.UpperCenter));
            Label(R(101, 96, 70, 9), $"SLOT: {LootTable.Slots[pickSlot].ToUpperInvariant()}", dim);
            Label(R(160, 96, 64, 9), "(Y) CHANGE", dimRight);

            // The detail box: what the action under the cursor does, and the last result.
            Box(R(4, 115, 225, 38));
            string title, about;
            if (onReroll)
            {
                title = "REROLL AFFIX";
                about = $"ROLLS A NEW STAT AND VALUE FOR THE LIT AFFIX. COST BY RARITY: 20 40 80 150.";
            }
            else if (onUpgrade)
            {
                title = upgradeCost < 0 ? "UPGRADE" : $"UPGRADE TO {upgradeTo}";
                about = "KEEPS THE AFFIXES, SCALED UP, AND ADDS A NEW ONE. MAGIC 150, RARE 400.";
            }
            else
            {
                title = $"TRANSMUTE {Forge.TransmuteCount}";
                about = picks.Count < Forge.TransmuteCount
                    ? $"(A) ADDS OR TAKES OFF THIS ITEM ({picks.Count} OF {Forge.TransmuteCount}). ALL ONE RARITY, NONE WORN. (X) CLEARS."
                    : $"(A) TURNS THE {Forge.TransmuteCount} INTO ONE RANDOM {pickRarity} {LootTable.Slots[pickSlot].ToUpperInvariant()}.";
            }
            Label(R(8, 117, 217, 9), title, PixelUI.Style(font, PixelUI.Accent));
            float ly = 125;
            foreach (string line in Wrap(about, text, 217 * s, 2))
            {
                Label(R(8, ly, 217, 8), line, text);
                ly += 7;
            }
            string why = forgeMessage;
            if (why == null && item != null)
                why = onReroll ? Forge.CantReroll(save, item, forgeCursor) : onUpgrade ? Forge.CantUpgrade(save, item) : null;
            if (why != null)
                Label(R(8, 140, 217, 9), Fit(why.ToUpperInvariant(), dim, 217 * s), PixelUI.Style(font, forgeMessage != null && forgeMessageOk ? Gain : forgeMessage != null ? Cant : PixelUI.Dim));
            Label(R(8, 156, 220, 9), "(A) FORGE   (B) BACK   < > ITEM   (X) CLEAR   (F / SELECT) ITEMS", dim);
        }

        // The coded look's forge: the same layout on plain frames, at the art panel's size.
        private void DrawForgeCoded()
        {
            int s = OpenCount > 1
                ? Mathf.FloorToInt(Mathf.Min(Screen.height * 0.92f / ArtH, Screen.width / (2f * ArtW + 12f)))
                : Mathf.FloorToInt(Mathf.Min(Screen.height * 0.92f / ArtH, Screen.width * 0.96f / ArtW));
            s = Mathf.Max(2, s);
            float w = ArtW * s, h = ArtH * s;
            float left = Mathf.Floor((Screen.width - w) / 2f);
            if (OpenCount > 1 && player != null)
                left = player.playerID == 0 ? 4f * s : Screen.width - w - 4f * s;
            float top = Mathf.Floor((Screen.height - h) / 2f);
            Func<float, float, float, float, Rect> R = (x, y, rw, rh) => new Rect(left + x * s, top + y * s, rw * s, rh * s);
            if (DimScreen)
                PixelUI.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.45f));
            PixelUI.Panel(R(0, 0, ArtW, ArtH), s);
            Label(R(4, 3, 220, 9), "LB   GEAR   RB", PixelUI.Style(Mathf.Max(9, 6 * s), PixelUI.Accent));
            DrawForge(R, s, Mathf.Max(10, 7 * s), null, null);
        }
    }
}
