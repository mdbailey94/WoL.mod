using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLChaosDepths
{
    // Task #16: the panel drawn with WoL Art's pieces (ui_*.png) on the layout of her mockup
    // (inventory_mockup_1x.png, 232 x 168), every position in the mockup's pixels times a whole
    // number s, so the pieces stay crisp. [Panel] Look = Coded draws the older code-drawn panel.
    // Beyond the mockup: the character's level beside the title and its experience along the
    // title's rule, and the list's item count in the tab row.
    public partial class InventoryPanel
    {
        private const int ArtW = 232, ArtH = 168, ListRows = 4, ListRowH = 18;
        private static ConfigEntry<string> look;
        private static readonly Color Rule = new Color32(0x61, 0x63, 0x74, 0xff);
        private static readonly Color RowLight = new Color32(0x5c, 0x5e, 0x6f, 0xff);
        private static readonly Color Gold = new Color32(0xff, 0xe0, 0x66, 0xff);

        public static void BindLook(ConfigFile config)
        {
            look = config.Bind("Panel", "Look", "Drawn",
                "Drawn: the inventory built from WoL Art's pieces on her mockup's layout. Coded: the older code-drawn panel.");
        }

        private static bool DrawnLook => look == null || !string.Equals((look.Value ?? "").Trim(), "Coded", StringComparison.OrdinalIgnoreCase);

        private static Texture2D Piece(string name) => ItemArt.LoadPng(name);

        // False (so the code-drawn panel is used) when the pieces can't be had.
        private bool DrawArt()
        {
            Texture2D frame = Piece("ui_panel_9slice"), inset = Piece("ui_panel_inset_9slice"), slots = Piece("ui_slot_frames");
            if (frame == null || inset == null || slots == null)
                return false;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.depth = -100;
            try
            {
                int s = Mathf.Max(2, Mathf.FloorToInt(Mathf.Min(Screen.height * 0.92f / ArtH, Screen.width * 0.96f / ArtW)));
                float w = ArtW * s, h = ArtH * s;
                int activePlayers = 0;
                if (GameController.activePlayers != null)
                    foreach (Player p in GameController.activePlayers)
                        if (p != null && p.gameObject.activeInHierarchy)
                            activePlayers++;
                float left = (Screen.width - w) / 2f;
                if (activePlayers > 1 && player != null)
                    left = player.playerID == 0 ? 4f * s : Screen.width - w - 4f * s;
                left = Mathf.Floor(Mathf.Clamp(left, 0f, Mathf.Max(0f, Screen.width - w)));
                float top = Mathf.Floor((Screen.height - h) / 2f);
                // A rect in the mockup's pixels.
                Func<float, float, float, float, Rect> R = (x, y, rw, rh) => new Rect(left + x * s, top + y * s, rw * s, rh * s);

                // 7 a mockup pixel: about the mockup's 5-px capitals (6 read small in game).
                int font = Mathf.Max(10, 7 * s);
                GUIStyle text = PixelUI.Style(font, PixelUI.Text);
                GUIStyle dim = PixelUI.Style(font, PixelUI.Dim);
                GUIStyle dimRight = PixelUI.Style(font, PixelUI.Dim, TextAnchor.UpperRight);
                GUIStyle textRight = PixelUI.Style(font, PixelUI.Text, TextAnchor.UpperRight);

                PixelUI.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.45f));
                NineSlice(frame, R(0, 0, ArtW, ArtH), s);

                // Header: title, level, dust; the experience along the rule under it.
                Label(R(6, 2, 100, 9), "CHAOS DEPTHS", PixelUI.Style(font, PixelUI.Accent));
                Label(R(70, 2, 110, 9), $"LV {save.level}", dim);
                Texture2D dust = Piece("ui_chaos_dust_8");
                if (dust != null)
                    GUI.DrawTexture(R(202, 3, 8, 8), dust);
                Label(R(150, 2, 76, 9), save.dust.ToString(), textRight);
                int need = ChaosDepthsPlugin.XpToNext(save.level);
                PixelUI.Fill(R(4, 12, 226, 1), Rule);
                PixelUI.Fill(R(4, 12, 226 * Mathf.Clamp01(save.xp / (float)Mathf.Max(1, need)), 1), PixelUI.Accent);

                // Tab row: < ALL amulet ring charm >, the sort on the right.
                Texture2D chevL = Piece("ui_chevron_left"), chevR = Piece("ui_chevron_right"), tabArt = Piece("ui_tab");
                // The idle (dim) frame, as in the mockup; the bright one while < or > is held.
                bool tabKeys = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);
                if (chevL != null)
                    Frame(chevL, 2, tabKeys && Input.GetKey(KeyCode.LeftArrow) ? 1 : 0, R(6, 20, 4, 7));
                if (chevR != null)
                    Frame(chevR, 2, tabKeys && Input.GetKey(KeyCode.RightArrow) ? 1 : 0, R(111, 20, 4, 7));
                for (int i = 0; i < TabNames.Length; i++)
                {
                    float tx = 14 + i * 24;
                    if (tabArt != null)
                        Frame(tabArt, 2, i == tab ? 1 : 0, R(tx, 16, 22, 16));
                    if (i == 0)
                        Label(R(tx, 18, 22, 9), "ALL", PixelUI.Style(font, i == tab ? PixelUI.Text : PixelUI.Dim, TextAnchor.UpperCenter));
                    else
                        Icon(new ItemData { slot = TabNames[i], rarity = i == tab ? "Rare" : "Common" }, R(tx + 4, 17, 14, 14));
                }
                Label(R(118, 16, 108, 9), $"SORT: {SortNames[sortMode].ToUpperInvariant()}", textRight);
                Label(R(118, 24, 108, 9), "(Y) CHANGE", dimRight);
                Label(R(118, 24, 60, 9), $"{view.Count} ITEM{(view.Count == 1 ? "" : "S")}", dim);

                // Left: the equipped slots, each in its rarity's frame.
                NineSlice(inset, R(4, 35, 59, 78), s);
                Label(R(8, 37, 54, 9), "EQUIPPED", text);
                for (int i = 0; i < LootTable.Slots.Length; i++)
                {
                    float sy = 47 + i * 22;
                    ItemData item = StatMods.FindItem(save, save.equipped[i]);
                    Frame(slots, 5, item != null ? 1 + Mathf.Clamp(LootTable.RarityIndex(item.rarity), 0, 3) : 0, R(8, sy, 20, 20));
                    if (item != null)
                        Icon(item, R(10, sy + 2, 16, 16));
                    else
                    {
                        Texture2D empty = Piece("ui_empty_" + LootTable.Slots[i].ToLowerInvariant());
                        if (empty != null)
                            GUI.DrawTexture(R(10, sy + 2, 16, 16), empty);
                    }
                    Label(R(31, sy + 5, 31, 9), item != null ? LootTable.Slots[i].ToUpperInvariant() : "EMPTY", dim);
                }

                // Right: the list, a name and a level a row, equipped first.
                NineSlice(inset, R(66, 35, 163, 78), s);
                rows = ListRows;
                if (selected >= scroll + rows)
                    scroll = Mathf.Max(0, selected - rows + 1);
                if (view.Count == 0)
                    Label(R(72, 40, 152, 9), save.inventory.Count == 0 ? "NOTHING YET" : "NOTHING OF THIS TYPE", dim);
                Texture2D cursor = Piece("ui_slot_cursor");
                for (int row = 0; row < rows; row++)
                {
                    int index = scroll + row;
                    if (index >= view.Count)
                        break;
                    ItemData item = view[index];
                    float y0 = 37 + row * ListRowH;
                    if (index == selected)
                    {
                        PixelUI.Fill(R(70, y0, 157, ListRowH), RowLight);
                        PixelUI.Fill(R(68, y0, 1, ListRowH), Gold);
                    }
                    Frame(slots, 5, 1 + Mathf.Clamp(LootTable.RarityIndex(item.rarity), 0, 3), R(70, y0, 20, 20));
                    Icon(item, R(72, y0 + 2, 16, 16));
                    if (index == selected && cursor != null)
                        Frame(cursor, 2, (int)(Time.unscaledTime * 3f) & 1, R(69, y0 - 1, 22, 22));
                    GUIStyle nameStyle = Rarity(item, font);
                    Label(R(93, y0 + 1, 128, 9), Fit(item.name.ToUpperInvariant(), nameStyle, 104 * s), nameStyle);
                    Label(R(93, y0 + 9, 128, 9), $"LV {item.itemLevel}", dim);
                    if (IsEquipped(item))
                        Label(R(196, y0 + 1, 28, 9), "E", PixelUI.Style(font, EquippedMark, TextAnchor.UpperRight));
                }

                // The detail box: name and type, two lines of description, VS EQUIPPED.
                NineSlice(inset, R(4, 115, 225, 38), s);
                ItemData chosen = Selected;
                if (chosen != null)
                {
                    GUIStyle chosenStyle = Rarity(chosen, font);
                    string kind = (string.IsNullOrEmpty(chosen.unique) ? chosen.rarity : "Unique") + " " + chosen.slot;
                    Label(R(8, 117, 217, 9), Fit($"{chosen.name}  ({kind})".ToUpperInvariant(), chosenStyle, 217 * s), chosenStyle);
                    float ly = 125;
                    foreach (string line in Wrap(LootTable.Describe(chosen).ToUpperInvariant(), text, 217 * s, 2))
                    {
                        Label(R(8, ly, 217, 8), line, text);
                        ly += 7;
                    }
                    int slot = SlotOf(chosen);
                    ItemData worn = slot >= 0 ? StatMods.FindItem(save, save.equipped[slot]) : null;
                    if (worn == null)
                        Label(R(8, 140, 217, 9), "VS EQUIPPED: SLOT EMPTY, ALL OF IT IS A GAIN", dim);
                    else if (worn == chosen)
                        Label(R(8, 140, 217, 9), "EQUIPPED", dim);
                    else
                    {
                        float cx = left + 8 * s;
                        Rect vs = R(8, 140, 217, 9);
                        Label(vs, "VS EQUIPPED: ", dim);
                        labelMeasure.text = "VS EQUIPPED: ";
                        cx += dim.CalcSize(labelMeasure).x;
                        foreach (KeyValuePair<string, bool> part in Compare(chosen, worn))
                        {
                            GUIStyle st = PixelUI.Style(font, part.Value ? Gain : Loss);
                            labelMeasure.text = part.Key.ToUpperInvariant() + "   ";
                            float pw = st.CalcSize(labelMeasure).x;
                            if (cx + pw > vs.xMax)
                                break;
                            Label(new Rect(cx, vs.y, pw, vs.height), part.Key.ToUpperInvariant(), st);
                            cx += pw;
                        }
                    }
                    if (confirmHeldSince >= 0f && !confirmConsumed)
                    {
                        float held = Mathf.Clamp01((Time.unscaledTime - confirmHeldSince) / SalvageHold);
                        PixelUI.Fill(R(8, 149, 217 * held, 1), PixelUI.Accent);
                    }
                }
                Label(R(8, 156, 220, 9), "(A) EQUIP   (B) BACK   (HOLD A) SALVAGE   < > TAB   (Y) SORT", dim);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"inventory draw (art): {e.Message}");
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
            }
            return true;
        }

        private static readonly GUIContent labelMeasure = new GUIContent();

        // Frame `index` of a strip of `count` frames side by side, drawn into `screen`.
        private static void Frame(Texture2D tex, int count, int index, Rect screen)
        {
            float fw = 1f / count;
            GUI.DrawTextureWithTexCoords(screen, tex, new Rect(index * fw, 0f, fw, 1f));
        }

        // A 9-slice piece (4 px borders, as WoL Art drew them) stretched over `r` at scale s.
        private static void NineSlice(Texture2D tex, Rect r, int s, int border = 4)
        {
            float b = border * s;
            float[] xs = { r.x, r.x + b, r.xMax - b, r.xMax };
            float[] ys = { r.y, r.y + b, r.yMax - b, r.yMax };
            float bu = border / (float)tex.width, bv = border / (float)tex.height;
            float[] us = { 0f, bu, 1f - bu, 1f };
            float[] vs = { 1f, 1f - bv, bv, 0f };   // texture rows run bottom-up
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var screen = new Rect(xs[i], ys[j], xs[i + 1] - xs[i], ys[j + 1] - ys[j]);
                    if (screen.width <= 0f || screen.height <= 0f)
                        continue;
                    GUI.DrawTextureWithTexCoords(screen, tex, new Rect(us[i], vs[j + 1], us[i + 1] - us[i], vs[j] - vs[j + 1]));
                }
        }
    }
}
