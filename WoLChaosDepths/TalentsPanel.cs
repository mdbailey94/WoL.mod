using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    // Phase 1a: the character panel's TALENTS tab (WoL Art's character_panel_talents_1x.png), open
    // with [Talents] Enabled. The current cloak's tree in three tier rows of node slots (gold:
    // learned, blue: can learn, empty with a padlock: locked), point pips beside each, the
    // keystone row's rule, and the chosen node's text in the detail box. A learns a point, Y (or R)
    // gives every point back outside a run (free respec in the house), B closes.
    public partial class InventoryPanel
    {
        private int talentTier, talentCol;
        private float nextTalentRepeat;

        private bool PanelTabIsOpen(int index) => index == 0 || (index == 1 && Talents.Enabled);

        private List<Talents.Node> TierNodes(Talents.Tree tree, int tier)
        {
            var nodes = new List<Talents.Node>();
            foreach (Talents.Node node in tree.Nodes)
                if (node.Tier == tier)
                    nodes.Add(node);
            return nodes;
        }

        private Talents.Node TalentAtCursor(Talents.Tree tree)
        {
            List<Talents.Node> row = TierNodes(tree, talentTier + 1);
            if (row.Count == 0)
                return null;
            talentCol = Mathf.Clamp(talentCol, 0, row.Count - 1);
            return row[talentCol];
        }

        // The TALENTS tab's input (the panel's Update hands over here while it's showing).
        private void TalentInput(Rewired.Player pad, bool useKeys)
        {
            bool Key(KeyCode k) => useKeys && Input.GetKey(k);
            bool KeyDown(KeyCode k) => useKeys && Input.GetKeyDown(k);
            string cloak = Talents.CloakOf(player);
            Talents.Tree tree = Talents.TreeFor(cloak);

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
                if (Time.unscaledTime >= nextTalentRepeat)
                {
                    bool first = nextTalentRepeat == 0f;
                    nextTalentRepeat = Time.unscaledTime + (first ? RepeatFirst : RepeatNext * 2f);
                    if (Mathf.Abs(vertical) > Mathf.Abs(horizontal))
                        talentTier = Mathf.Clamp(talentTier + (vertical < 0f ? 1 : -1), 0, 2);
                    else
                        talentCol += horizontal > 0f ? 1 : -1;
                    // Stops at the row's ends rather than wrapping (Matt 2026-11-03).
                    int count = Mathf.Max(1, TierNodes(tree, talentTier + 1).Count);
                    talentCol = Mathf.Clamp(talentCol, 0, count - 1);
                }
            }
            else
                nextTalentRepeat = 0f;

            // A (Enter / Space): learn a point.
            if (Pressed(pad, "Confirm") || KeyDown(KeyCode.Return) || KeyDown(KeyCode.Space))
            {
                Talents.Node node = TalentAtCursor(tree);
                string why = Talents.CantLearn(save, cloak, node);
                if (why == null && Talents.Learn(save, cloak, node))
                {
                    onChanged(player, save);
                    ChaosDepthsPlugin.Log($"Talents: player {player.playerID + 1} learned {node.Name} " +
                        $"({Talents.Points(save, cloak, node.Key)}/{node.MaxPoints}) on {cloak}, {Talents.Unspent(save, cloak)} point(s) left");
                }
                else
                    ChaosDepthsPlugin.Notice($"{(node != null ? node.Name : "Talent")}: {why}", 1.5f);
            }

            // Y (or R): free respec, outside a run only.
            if (PadY(pad) || KeyDown(KeyCode.R))
            {
                if (ChaosDepthsPlugin.InTrialsNow())
                    ChaosDepthsPlugin.Notice("Respec in the house", 1.5f);
                else
                {
                    int refunded = Talents.Respec(save, cloak);
                    onChanged(player, save);
                    ChaosDepthsPlugin.Notice($"Talents reset: {refunded} point(s) back", 1.5f);
                    ChaosDepthsPlugin.Log($"Talents: player {player.playerID + 1} respec on {cloak}, {refunded} point(s) back");
                }
            }
        }

        // The TALENTS tab's body, under the tab row (the same mockup pixels as the GEAR tab).
        private void DrawTalents(Func<float, float, float, float, Rect> R, int s, int font, Texture2D inset, Texture2D slots)
        {
            GUIStyle text = PixelUI.Style(font, PixelUI.Text);
            GUIStyle dim = PixelUI.Style(font, PixelUI.Dim);
            GUIStyle dimRight = PixelUI.Style(font, PixelUI.Dim, TextAnchor.UpperRight);
            GUIStyle accent = PixelUI.Style(font, PixelUI.Accent);
            string cloak = Talents.CloakOf(player);
            Talents.Tree tree = Talents.TreeFor(cloak);
            int unspent = Talents.Unspent(save, cloak);

            Label(R(6, 16, 150, 9), Fit(Talents.CloakName(cloak).ToUpperInvariant() + " CLOAK", accent, 150 * s), accent);
            Label(R(6, 24, 150, 9), tree.Name.ToUpperInvariant() + " TREE", dim);
            Label(R(118, 16, 108, 9), $"POINTS: {unspent}", PixelUI.Style(font, unspent > 0 ? PixelUI.Accent : PixelUI.Text, TextAnchor.UpperRight));
            Label(R(118, 24, 108, 9), $"LV {save.level}   {Talents.Spent(save, cloak)} SPENT", dimRight);

            NineSlice(inset, R(4, 35, 225, 78), s);
            Texture2D padlock = Piece("ui_lock"), cursor = Piece("ui_slot_cursor");
            Talents.Node chosen = TalentAtCursor(tree);
            for (int tier = 1; tier <= 3; tier++)
            {
                float ty = 38 + (tier - 1) * 25;
                bool open = Talents.TierOpen(save, cloak, tier);
                Label(R(8, ty + 6, 40, 9), $"TIER {tier}", open ? text : dim);
                List<Talents.Node> row = TierNodes(tree, tier);
                for (int i = 0; i < row.Count; i++)
                {
                    Talents.Node node = row[i];
                    // Three nodes a row 52 px apart, four 40 apart, five (with the cloak's own perk
                    // nodes) 32 apart, six 29 apart (the last pips end at 221, inside the inset).
                    float nx = 52 + i * (row.Count > 5 ? 29 : row.Count > 4 ? 32 : row.Count > 3 ? 40 : 52);
                    int points = Talents.Points(save, cloak, node.Key);
                    bool canLearn = Talents.CantLearn(save, cloak, node) == null;
                    // ui_slot_frames: 0 empty, 2 blue (can learn), 3 gold (learned).
                    Frame(slots, 5, points > 0 ? 3 : canLearn ? 2 : 0, R(nx, ty, 20, 20));
                    // WoL Art's icon (talent_<key>.png, 16x16 at +2,+2), grey at about 55% while
                    // its tier is locked, under the padlock; a letter if the icon isn't there.
                    Texture2D icon = Piece("talent_" + node.Key);
                    if (icon != null)
                    {
                        Color previous = GUI.color;
                        if (!open)
                            GUI.color = new Color(0.55f, 0.55f, 0.55f, 1f);
                        GUI.DrawTexture(R(nx + 2, ty + 2, 16, 16), icon);
                        GUI.color = previous;
                    }
                    else if (open || padlock == null)
                        Label(R(nx, ty + 6, 20, 9), node.Name.Substring(0, 1), PixelUI.Style(font, points > 0 ? Gold : open ? PixelUI.Text : PixelUI.Dim, TextAnchor.UpperCenter));
                    if (!open && padlock != null)
                        GUI.DrawTexture(R(nx + 7, ty + 7, 5, 5), padlock);
                    // A pip per point: lit for each learned.
                    for (int p = 0; p < node.MaxPoints; p++)
                        PixelUI.Fill(R(nx + 22, ty + 14 - p * 4, 2, 2), p < points ? PixelUI.Accent : PixelUI.Dim);
                    if (node == chosen && tier == talentTier + 1 && cursor != null)
                        Frame(cursor, 2, (int)(Time.unscaledTime * 3f) & 1, R(nx - 1, ty - 1, 22, 22));
                }
                if (tier == 3)
                {
                    // Right of the keystones (three with a pact, #75).
                    float lx = row.Count > 2 ? 182 : 160;
                    Label(R(lx, ty + 1, 226 - lx, 9), "PICK 1", dim);
                    if (!open)
                        Label(R(lx, ty + 9, 226 - lx, 9), $"NEEDS {Talents.PointsToOpenNextTier} IN T2", dim);
                }
            }

            // The detail box: name (tier, points), the effect, then why it can't be learned or
            // the respec hint.
            NineSlice(inset, R(4, 115, 225, 38), s);
            if (chosen != null)
            {
                int points = Talents.Points(save, cloak, chosen.Key);
                Label(R(8, 117, 217, 9), $"{chosen.Name.ToUpperInvariant()}   (TIER {chosen.Tier}   {points} OF {chosen.MaxPoints})", PixelUI.Style(font, Gold));
                float ly = 125;
                foreach (string line in Wrap(chosen.Effect.ToUpperInvariant(), text, 217 * s, 2))
                {
                    Label(R(8, ly, 217, 8), line, text);
                    ly += 7;
                }
                string why = Talents.CantLearn(save, cloak, chosen);
                Label(R(8, 140, 217, 9), why != null && why != "maxed" ? why.ToUpperInvariant() : "FREE RESPEC IN THE HOUSE", dim);
            }
            Label(R(8, 156, 220, 9), "(A) LEARN   (B) BACK   (Y) RESPEC   LB / RB TABS", dim);
        }
    }
}
