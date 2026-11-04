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
        // The GEAR tab's v2 layout (WoL Art's gear_panel_readability_mockup_v2, Matt "do it all"
        // 14:27): a bigger page, 312 x 254 mockup pixels, 5 list rows of 20.
        private const int GearW = 312, GearH = 254, GearRows = 5, GearRowH = 20;
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

        // #51: the panel's tabs, 44 x 10 at x 16 / 62 / 108 (ui_panel_tab.png's frames: normal,
        // selected, locked), labels in the game font; a locked tab has ui_lock.png's padlock and a
        // dim label, lit for a moment when LB / RB tries it.
        private const int PanelTabW = 44;
        private static float PanelTabX(int index) => 16 + index * (PanelTabW + 2);

        private void PanelTabRow(Func<float, float, float, float, Rect> R, int font, GUIStyle dim)
        {
            Texture2D tabs = Piece("ui_panel_tab"), padlock = Piece("ui_lock");
            Label(R(4, 2, 12, 9), "LB", dim);
            for (int i = 0; i < PanelTabNames.Length; i++)
            {
                float tx = PanelTabX(i);
                bool open = PanelTabIsOpen(i), chosen = i == panelTab;
                bool lit = i == lockedTab && Time.unscaledTime < lockedUntil;
                if (tabs != null)
                    Frame(tabs, 3, chosen ? 1 : open ? 0 : 2, R(tx, 2, PanelTabW, 10));
                Color colour = chosen ? PixelUI.Accent : open || lit ? PixelUI.Text : PixelUI.Dim;
                if (open || padlock == null)
                    Label(R(tx, 3, PanelTabW, 9), PanelTabNames[i], PixelUI.Style(font, colour, TextAnchor.UpperCenter));
                else
                {
                    GUI.DrawTexture(R(tx + 3, 4, 5, 5), padlock);
                    Label(R(tx + 8, 3, PanelTabW - 9, 9), PanelTabNames[i], PixelUI.Style(font, colour, TextAnchor.UpperCenter));
                }
            }
            Label(R(PanelTabX(PanelTabNames.Length) - 1, 2, 12, 9), "RB", dim);
        }

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
                // Two open at once (0.17.0): each takes the largest whole scale at which both fit
                // side by side with a 4-pixel margin at the edges and between them (Matt: bigger, to
                // read in two-player). Up to 0.28 each was held to 48% of the width, which floored
                // 1920 px to scale 3 although two scale-4 panels fit.
                int activePlayers = 0;
                if (GameController.activePlayers != null)
                    foreach (Player p in GameController.activePlayers)
                        if (p != null && p.gameObject.activeInHierarchy)
                            activePlayers++;
                // #113 (0.36.4, Matt): in two-player the panel ALWAYS opens at the two-player size
                // and on its player's side, even when only one is open (it used to jump between the
                // big centred size and the half size as the other panel opened or closed).
                bool gearPage = panelTab != 1 && !forgeOpen;
                int pw = gearPage ? GearW : ArtW, ph = gearPage ? GearH : ArtH;
                float tall = gearPage ? 0.97f : 0.92f;
                int s = activePlayers > 1 || OpenCount > 1
                    ? Mathf.FloorToInt(Mathf.Min(Screen.height * tall / ph, Screen.width / (2f * pw + 12f)))
                    : Mathf.FloorToInt(Mathf.Min(Screen.height * tall / ph, Screen.width * 0.96f / pw));
                s = Mathf.Max(2, s);
                float w = pw * s, h = ph * s;
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

                if (DimScreen)
                    PixelUI.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.45f));
                NineSlice(frame, R(0, 0, pw, ph), s);
                float dx = pw - ArtW;   // the header's right end moves with the wider gear page

                // Header (#51, character_panel_gear_1x.png): LB [GEAR] [TALENTS] [SKILLS] RB in place
                // of the title, then the level and dust; the experience along the rule under it,
                // which the chosen tab opens through.
                PanelTabRow(R, Mathf.Max(9, 6 * s), dim);
                Label(R(166 + dx, 2, 34, 9), $"LV {save.level}", dimRight);
                Texture2D dust = Piece("ui_chaos_dust_8");
                if (dust != null)
                    GUI.DrawTexture(R(202 + dx, 3, 8, 8), dust);
                Label(R(150 + dx, 2, 76, 9), save.dust.ToString(), textRight);
                int need = ChaosDepthsPlugin.XpToNext(save.level);
                float xpEnd = 4 + (226 + dx) * Mathf.Clamp01(save.xp / (float)Mathf.Max(1, need));
                float opening = PanelTabX(panelTab);
                void RuleFrom(float a, float b)
                {
                    if (b <= a)
                        return;
                    PixelUI.Fill(R(a, 12, b - a, 1), Rule);
                    if (Mathf.Min(b, xpEnd) > a)
                        PixelUI.Fill(R(a, 12, Mathf.Min(b, xpEnd) - a, 1), PixelUI.Accent);
                }
                RuleFrom(4, opening);
                RuleFrom(opening + PanelTabW, 230 + dx);

                if (panelTab == 1)
                {
                    DrawTalents(R, s, font, inset, slots);
                    return true;
                }
                // #79: the GEAR tab's FORGE sub-tab (ForgePanel.cs).
                if (forgeOpen)
                {
                    DrawForge(R, s, font, inset, slots);
                    return true;
                }

                DrawGearPage(R, s, font);
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
