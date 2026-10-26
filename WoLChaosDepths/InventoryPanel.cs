using System;
using System.Collections.Generic;
using Rewired;
using UnityEngine;

namespace WoLChaosDepths
{
    // M0 technical test: one custom screen, controller first. Opens over a paused game and shows
    // the character (level, experience bar), the three equipped slots and the inventory. Move with
    // the stick or the arrow keys, Confirm (A / Enter) equips or unequips, holding Confirm (or
    // Delete) salvages the item into Chaos Dust, Cancel (B / Esc) or the open key closes.
    //
    // Drawn with IMGUI in the pixel style of the Ascension altar prompt (PixelUI), in whole pixel
    // units so it stays crisp at any resolution.
    public class InventoryPanel
    {
        private const float RepeatFirst = 0.3f, RepeatNext = 0.11f, SalvageHold = 0.7f;
        private const int Rows = 9;
        private static readonly int[] DustByRarity = { 1, 3, 8, 20 };

        public bool Open { get; private set; }
        private Player player;
        private CharacterSave save;
        private int selected;
        private int scroll;
        private float nextRepeat;
        private float confirmHeldSince = -1f;
        private bool confirmConsumed;
        private bool pausedByUs;
        private readonly Action<Player, CharacterSave> onChanged;

        public InventoryPanel(Action<Player, CharacterSave> onChanged)
        {
            this.onChanged = onChanged;
        }

        // ---- Opening and closing ----

        public void Show(Player player, CharacterSave save)
        {
            this.player = player;
            this.save = save;
            selected = 0;
            scroll = 0;
            confirmHeldSince = -1f;
            confirmConsumed = true; // the press that opened it isn't an equip
            Open = true;
            try
            {
                if (!TimeScaleController.paused)
                {
                    TimeScaleController.Pause(true);
                    pausedByUs = true;
                }
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"pause: {e.Message}");
                Time.timeScale = 0f;
                pausedByUs = true;
            }
            try
            {
                GameController.PauseAllPlayers(true);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"pause players: {e.Message}");
            }
            ChaosDepthsPlugin.Log($"Inventory opened by player {player.playerID + 1}: {save.inventory.Count} item(s)");
        }

        public void Close()
        {
            if (!Open)
                return;
            Open = false;
            ChaosDepthsPlugin.Log("Inventory closed");
            try
            {
                if (pausedByUs)
                    TimeScaleController.Pause(false);
            }
            catch
            {
                Time.timeScale = 1f;
            }
            pausedByUs = false;
            try
            {
                GameController.PauseAllPlayers(false);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"unpause players: {e.Message}");
            }
        }

        // ---- Input (unscaled time: the game is paused) ----

        public void Update()
        {
            if (!Open)
                return;
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                Close();
                return;
            }
            Rewired.Player pad = player.inputDevice != null ? player.inputDevice.rewiredPlayer : null;

            if (Pressed(pad, "Cancel") || Input.GetKeyDown(KeyCode.Escape) || ChaosDepthsPlugin.OpenKeyPressed())
            {
                Close();
                return;
            }

            // Up and down with repeat.
            float vertical = 0f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
                vertical = -1f;
            else if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
                vertical = 1f;
            else if (pad != null)
            {
                try
                {
                    vertical = pad.GetAxis("MoveVertical");
                }
                catch
                {
                }
            }
            int count = Mathf.Max(1, save.inventory.Count);
            if (Mathf.Abs(vertical) > 0.5f)
            {
                if (Time.unscaledTime >= nextRepeat)
                {
                    bool first = nextRepeat == 0f;
                    nextRepeat = Time.unscaledTime + (first ? RepeatFirst : RepeatNext);
                    selected = (selected + (vertical < 0f ? 1 : count - 1)) % count;
                    if (selected < scroll)
                        scroll = selected;
                    if (selected >= scroll + Rows)
                        scroll = selected - Rows + 1;
                }
            }
            else
                nextRepeat = 0f;

            // Confirm: tap equips, hold salvages.
            bool confirmDown = Held(pad, "Confirm") || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Space);
            if (confirmDown)
            {
                if (confirmHeldSince < 0f)
                    confirmHeldSince = Time.unscaledTime;
                else if (!confirmConsumed && Time.unscaledTime - confirmHeldSince >= SalvageHold)
                {
                    confirmConsumed = true;
                    Salvage();
                }
            }
            else
            {
                if (confirmHeldSince >= 0f && !confirmConsumed)
                    ToggleEquip();
                confirmHeldSince = -1f;
                confirmConsumed = false;
            }
            if (Input.GetKeyDown(KeyCode.Delete))
                Salvage();
        }

        private static bool Pressed(Rewired.Player pad, string action)
        {
            try
            {
                return pad != null && pad.GetButtonDown(action);
            }
            catch
            {
                return false;
            }
        }

        private static bool Held(Rewired.Player pad, string action)
        {
            try
            {
                return pad != null && pad.GetButton(action);
            }
            catch
            {
                return false;
            }
        }

        // ---- Actions ----

        private ItemData Selected => save.inventory.Count == 0 ? null : save.inventory[Mathf.Clamp(selected, 0, save.inventory.Count - 1)];

        private int SlotOf(ItemData item) => Array.IndexOf(LootTable.Slots, item.slot);

        private bool IsEquipped(ItemData item)
        {
            int slot = SlotOf(item);
            return slot >= 0 && save.equipped[slot] == item.id;
        }

        private void ToggleEquip()
        {
            ItemData item = Selected;
            if (item == null)
                return;
            int slot = SlotOf(item);
            if (slot < 0)
                return;
            save.equipped[slot] = IsEquipped(item) ? "" : item.id;
            ChaosDepthsPlugin.Log($"{(IsEquipped(item) ? "Equipped" : "Unequipped")} {item.rarity} {item.name}");
            onChanged(player, save);
        }

        private void Salvage()
        {
            ItemData item = Selected;
            if (item == null)
                return;
            if (IsEquipped(item))
            {
                ChaosDepthsPlugin.Log($"Salvage refused: {item.rarity} {item.name} is equipped");
                ChaosDepthsPlugin.Notice("Unequip it before salvaging");
                return;
            }
            int rarity = Mathf.Clamp(LootTable.RarityIndex(item.rarity), 0, DustByRarity.Length - 1);
            save.dust += DustByRarity[rarity];
            save.inventory.Remove(item);
            if (selected >= save.inventory.Count)
                selected = Mathf.Max(0, save.inventory.Count - 1);
            ChaosDepthsPlugin.Log($"Salvaged {item.rarity} {item.name} for {DustByRarity[rarity]} dust ({save.dust} in all)");
            onChanged(player, save);
        }

        // ---- Drawing ----

        public void Draw()
        {
            if (!Open || save == null)
                return;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.depth = -100;
            try
            {
                // A little bigger than the altar prompt: 3 units and 12 px at 720p, 4 and 16 at 1080p.
                int u = Mathf.Max(3, Mathf.FloorToInt(Screen.height / 240f));
                int font = Mathf.Max(12, 4 * u);
                float w = 176 * u, h = 118 * u;
                var panel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
                PixelUI.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.45f));
                PixelUI.Panel(panel, u);

                GUIStyle text = PixelUI.Style(font, PixelUI.Text);
                GUIStyle dim = PixelUI.Style(font, PixelUI.Dim);
                GUIStyle right = PixelUI.Style(font, PixelUI.Dim, TextAnchor.UpperRight);
                float x = panel.x + 5 * u, y = panel.y + 4 * u, lineH = 5 * u;

                // Header: name, level, experience bar, dust.
                Label(new Rect(x, y, w, lineH), $"{save.name}   Level {save.level}", text);
                Label(new Rect(x, y, w - 10 * u, lineH), $"Chaos Dust {save.dust}   Kills {save.kills}", right);
                y += lineH;
                int need = ChaosDepthsPlugin.XpToNext(save.level);
                float barW = w - 10 * u;
                PixelUI.Fill(new Rect(x, y + u, barW, 2 * u), PixelUI.PipEmpty);
                PixelUI.Fill(new Rect(x, y + u, barW * Mathf.Clamp01(save.xp / (float)Mathf.Max(1, need)), 2 * u), PixelUI.Accent);
                Label(new Rect(x, y + 3 * u, w, lineH), $"{save.xp} / {need} xp", dim);
                y += lineH + 3 * u;
                PixelUI.Fill(new Rect(x, y, barW, u), PixelUI.InnerLine);
                y += 2 * u;

                // Left: equipped slots.  Right: inventory list.
                float leftW = 58 * u, listX = x + leftW + 4 * u, listW = barW - leftW - 4 * u;
                float top = y;
                Label(new Rect(x, y, leftW, lineH), "Equipped", dim);
                y += lineH;
                for (int i = 0; i < LootTable.Slots.Length; i++)
                {
                    ItemData item = StatMods.FindItem(save, save.equipped[i]);
                    Label(new Rect(x, y, leftW, lineH), LootTable.Slots[i], dim);
                    y += lineH;
                    Label(new Rect(x + 2 * u, y, leftW, lineH), item != null ? item.name : "-", Rarity(item, font));
                    y += lineH;
                    if (item != null)
                    {
                        foreach (string line in Wrap(LootTable.Describe(item), 26))
                        {
                            Label(new Rect(x + 2 * u, y, leftW, lineH), line, dim);
                            y += lineH;
                        }
                    }
                    y += u;
                }

                y = top;
                Label(new Rect(listX, y, listW, lineH), $"Inventory ({save.inventory.Count})", dim);
                y += lineH;
                if (save.inventory.Count == 0)
                    Label(new Rect(listX, y, listW, lineH), "Nothing yet. Items drop from enemies in the trials.", dim);
                for (int row = 0; row < Rows; row++)
                {
                    int index = scroll + row;
                    if (index >= save.inventory.Count)
                        break;
                    ItemData item = save.inventory[index];
                    var line = new Rect(listX, y, listW, lineH);
                    if (index == selected)
                        PixelUI.Fill(line, new Color(1f, 1f, 1f, 0.12f));
                    string mark = IsEquipped(item) ? "* " : "  ";
                    Label(new Rect(listX + u, y, listW, lineH), mark + item.name, Rarity(item, font));
                    Label(new Rect(listX, y, listW - u, lineH), item.rarity, Rarity(item, font, TextAnchor.UpperRight));
                    y += lineH;
                }
                if (save.inventory.Count > Rows)
                    Label(new Rect(listX, y, listW, lineH), $"{scroll + 1}-{Mathf.Min(scroll + Rows, save.inventory.Count)} of {save.inventory.Count}", dim);

                // Selected item's details, and the equipped one for its slot.
                float detailY = panel.yMax - 22 * u;
                PixelUI.Fill(new Rect(x, detailY - 2 * u, barW, u), PixelUI.InnerLine);
                ItemData chosen = Selected;
                if (chosen != null)
                {
                    GUIStyle chosenStyle = Rarity(chosen, font);
                    Label(new Rect(x, detailY, barW, lineH), Fit($"{chosen.rarity} {chosen.name} (item level {chosen.itemLevel}): {LootTable.Describe(chosen)}", chosenStyle, barW), chosenStyle);
                    int slot = SlotOf(chosen);
                    ItemData worn = slot >= 0 ? StatMods.FindItem(save, save.equipped[slot]) : null;
                    string compare = worn == null ? "Slot empty"
                        : worn == chosen ? "Equipped"
                        : $"Equipped {worn.rarity} {worn.name}: {LootTable.Describe(worn)}";
                    Label(new Rect(x, detailY + lineH, barW, lineH), Fit(compare, dim, barW), dim);
                    if (confirmHeldSince >= 0f && !confirmConsumed)
                    {
                        float held = Mathf.Clamp01((Time.unscaledTime - confirmHeldSince) / SalvageHold);
                        PixelUI.Fill(new Rect(x, detailY + 2 * lineH + u, barW * held, u), PixelUI.Accent);
                    }
                }
                Label(new Rect(x, panel.yMax - 8 * u, barW, lineH),
                    "A / Enter: equip or unequip    hold A / Delete: salvage    B / Esc: close", dim);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"inventory draw: {e.Message}");
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
            }
        }

        // A label with a one-pixel dark shadow, so the game's thin font reads on the panel.
        // Shortens text to fit the width, ending in "...".
        private static string Fit(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || style.CalcSize(new GUIContent(text)).x <= width)
                return text;
            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (style.CalcSize(new GUIContent(text.Substring(0, mid) + "...")).x <= width)
                    lo = mid;
                else
                    hi = mid - 1;
            }
            return text.Substring(0, lo).TrimEnd(' ', ',', ':') + "...";
        }

        private static void Label(Rect rect, string text, GUIStyle style)
        {
            Color color = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        private static GUIStyle Rarity(ItemData item, int font, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            Color color = PixelUI.Text;
            if (item != null)
            {
                switch (item.rarity)
                {
                    case "Magic": color = new Color32(0x6f, 0xa8, 0xff, 0xff); break;
                    case "Rare": color = new Color32(0xff, 0xe0, 0x66, 0xff); break;
                    case "Legendary": color = new Color32(0xff, 0x8a, 0x3d, 0xff); break;
                }
            }
            return PixelUI.Style(font, color, anchor);
        }

        private static IEnumerable<string> Wrap(string text, int width)
        {
            string line = "";
            foreach (string word in text.Split(' '))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > width)
                {
                    yield return line;
                    line = word;
                }
                else
                    line = line.Length == 0 ? word : line + " " + word;
            }
            if (line.Length > 0)
                yield return line;
        }
    }
}
