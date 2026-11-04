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
                        talentTier = Mathf.Clamp(talentTier + (vertical < 0f ? 1 : -1), 0, Talents.KeystoneTier(tree) - 1);   // #110: 4 tiers
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

        // The TALENTS tab's body on the wide page (312 x 254), laid out from WoL Art's readability
        // mockup (Finished Art/UI mockups (2026-11-04)/talent_panel_readability_mockup.png, Matt
        // 11-04 "improve this screen"): a header line, each tier a 30 px row with its rule under its
        // name, each node's icon + NAME + rank as text (CLOAK / PACT tags), the keystone row on a
        // darker band, the detail box with upsides in green and the downside in red, and button chips.
        private static readonly Color TCyan = new Color32(0x5f, 0xd6, 0xe8, 0xff), TPact = new Color32(0xe0, 0x5a, 0x4a, 0xff);
        private static readonly Color TBand = new Color32(0x3b, 0x36, 0x31, 0xff);

        private void DrawTalents(Func<float, float, float, float, Rect> R, int s, int font, Texture2D inset, Texture2D slots)
        {
            GUIStyle text = PixelUI.Style(font, GText);
            GUIStyle label = PixelUI.Style(font, GLabel);
            GUIStyle labelRight = PixelUI.Style(font, GLabel, TextAnchor.UpperRight);
            GUIStyle accent = PixelUI.Style(font, PixelUI.Accent);
            GUIStyle gold = PixelUI.Style(font, Gold);
            string cloak = Talents.CloakOf(player);
            Talents.Tree tree = Talents.TreeFor(cloak);
            int unspent = Talents.Unspent(save, cloak), spent = Talents.Spent(save, cloak);
            int tiers = Talents.KeystoneTier(tree);
            var perkKeys = new HashSet<string>();
            Talents.Node[] perks = CloakPerks.NodesFor(cloak);
            if (perks != null)
                foreach (Talents.Node n in perks)
                    perkKeys.Add(n.Key);

            // ---- Header: CLOAK  TREE ..... N POINTS TO SPEND  N SPENT ----
            string cloakName = Talents.CloakName(cloak).ToUpperInvariant() + " CLOAK";
            float cw = Width(cloakName, accent, s);
            Label(R(6, 19, cw + 2, 9), cloakName, accent);
            Label(R(6 + cw + 4, 19, 150 - cw, 9), Fit(tree.Name.ToUpperInvariant() + " TREE", label, (150 - cw) * s), label);
            string spentText = $"{spent} SPENT";
            float sw = Width(spentText, labelRight, s);
            Label(R(306 - sw - 2, 19, sw + 2, 9), spentText, labelRight);
            string pointsText = unspent > 0 ? $"{unspent} POINTS TO SPEND" : "NO POINTS TO SPEND";
            Label(R(150, 19, 306 - sw - 8 - 150, 9), pointsText, PixelUI.Style(font, unspent > 0 ? Gold : GLabel, TextAnchor.UpperRight));

            // ---- The tree ----
            NineSlice(inset, R(4, 31, 304, 125), s);
            Texture2D padlock = Piece("ui_lock"), cursor = Piece("ui_slot_cursor");
            Talents.Node chosen = TalentAtCursor(tree);
            bool keystonePicked = Talents.Spent(save, cloak, tiers) > 0;
            for (int tier = 1; tier <= tiers; tier++)
            {
                bool keystoneRow = tier == tiers && tiers > 1;
                float ty = keystoneRow ? 127 : 37 + (tier - 1) * 30;
                if (keystoneRow)
                    PixelUI.Fill(R(6, 122, 300, 32), TBand);
                bool open = Talents.TierOpen(save, cloak, tier);
                List<Talents.Node> row = TierNodes(tree, tier);

                // The tier's name and its rule under it.
                Label(R(9, ty + 3, 60, 9), $"TIER {tier}", open ? text : label);
                string rule;
                if (keystoneRow)
                    rule = open ? "PICK 1" : $"NEEDS {Talents.PointsToOpenNextTier} IN T{tier - 1}";
                else if (tier == 1)
                {
                    int most = 1;
                    foreach (Talents.Node n in row)
                        if (!perkKeys.Contains(n.Key))
                            most = Mathf.Max(most, n.MaxPoints);
                    rule = most > 1 ? $"{most} PTS EACH" : "1 PT EACH";
                }
                else
                    rule = $"NEEDS {Talents.PointsToOpenNextTier} IN T{tier - 1}";
                Label(R(9, ty + 11, 62, 9), rule, keystoneRow && open ? accent : label);

                for (int i = 0; i < row.Count; i++)
                {
                    Talents.Node node = row[i];
                    float nx = 72 + i * 79;
                    int points = Talents.Points(save, cloak, node.Key);
                    bool canLearn = Talents.CantLearn(save, cloak, node) == null;
                    bool pact = node.Effect != null && node.Effect.StartsWith("PACT");
                    bool perk = perkKeys.Contains(node.Key);
                    // A keystone not picked, once another is: greyed (shown, not hidden).
                    bool passedOver = keystoneRow && keystonePicked && points == 0;
                    bool grey = !open || passedOver;

                    // ui_slot_frames: 0 empty, 2 blue (can learn), 3 gold (learned).
                    Frame(slots, 5, points > 0 ? 3 : canLearn ? 2 : 0, R(nx, ty, 20, 20));
                    if (pact && points > 0)
                        Outline(R, nx, ty, 20, 20, PixelUI.Accent);
                    Texture2D icon = Piece("talent_" + node.Key);
                    if (icon != null)
                    {
                        Color previous = GUI.color;
                        if (grey)
                            GUI.color = new Color(0.55f, 0.55f, 0.55f, 1f);
                        GUI.DrawTexture(R(nx + 2, ty + 2, 16, 16), icon);
                        GUI.color = previous;
                    }
                    else if (open || padlock == null)
                        Label(R(nx, ty + 6, 20, 9), node.Name.Substring(0, 1), PixelUI.Style(font, points > 0 ? Gold : open ? GText : GLabel, TextAnchor.UpperCenter));
                    if (!open && padlock != null)
                        GUI.DrawTexture(R(nx + 7, ty + 7, 5, 5), padlock);
                    if (node == chosen && tier == talentTier + 1 && cursor != null)
                        Frame(cursor, 2, (int)(Time.unscaledTime * 3f) & 1, R(nx - 1, ty - 1, 22, 22));

                    // The name (two lines if it's long), then the rank and its tag.
                    GUIStyle nameStyle = grey ? label : text;
                    List<string> lines = Wrap(node.Name.ToUpperInvariant(), nameStyle, 54 * s, 2);
                    float ly = lines.Count > 1 ? ty : ty + 3;
                    foreach (string line in lines)
                    {
                        Label(R(nx + 24, ly, 56, 9), line, nameStyle);
                        ly += 7;
                    }
                    ly += 1;
                    string rank = points > 0 ? $"{points}/{node.MaxPoints}" : keystoneRow ? "-" : $"0/{node.MaxPoints}";
                    GUIStyle rankStyle = PixelUI.Style(font, points > 0 ? GBetter : GLabel);
                    float rw = Width(rank, rankStyle, s);
                    Label(R(nx + 24, ly, rw + 2, 9), rank, rankStyle);
                    if (perk || pact)
                        Label(R(nx + 24 + rw + 3, ly, 54 - rw - 3, 9), perk ? "CLOAK" : "PACT", PixelUI.Style(font, perk ? TCyan : TPact));
                }
            }

            // ---- The detail box ----
            NineSlice(inset, R(4, 158, 304, 72), s);
            if (chosen != null)
            {
                int points = Talents.Points(save, cloak, chosen.Key);
                bool keystone = chosen.Tier == tiers && tiers > 1;
                bool pact = chosen.Effect != null && chosen.Effect.StartsWith("PACT");
                bool perk = perkKeys.Contains(chosen.Key);
                string title = chosen.Name.ToUpperInvariant();
                float tw = Width(title, gold, s);
                Label(R(8, 162, tw + 2, 9), title, gold);
                string kind = keystone ? $"TIER {chosen.Tier} KEYSTONE" : $"TIER {chosen.Tier}";
                float kw = Width(kind, label, s);
                Label(R(8 + tw + 6, 162, kw + 2, 9), kind, label);
                if (pact || perk)
                    Label(R(8 + tw + 6 + kw + 6, 162, 60, 9), perk ? "CLOAK PERK" : "PACT", PixelUI.Style(font, perk ? TCyan : TPact));
                string why = Talents.CantLearn(save, cloak, chosen);
                string state = points >= chosen.MaxPoints ? (keystone ? "PICKED" : "LEARNED")
                    : why == null ? "CAN LEARN" : why.ToUpperInvariant();
                Label(R(170, 162, 134, 9), state, PixelUI.Style(font, points > 0 ? GBetter : why == null ? Gold : GLabel, TextAnchor.UpperRight));

                // Upsides as green '+' lines, the downside (after "but") as a red '-' line.
                float ey = 173;
                int left = 3;
                List<string> ups, downs;
                SplitEffect(chosen.Effect ?? "", out ups, out downs);
                foreach (string up in ups)
                    ey = EffectLine(R, s, font, ey, "+", up, GBetter, GText, ref left);
                foreach (string down in downs)
                    ey = EffectLine(R, s, font, ey, "-", down, GWorse, GWorse, ref left);

                PixelUI.Fill(R(8, 201, 296, 1), GBox);
                Label(R(8, 205, 296, 9), keystone
                    ? "YOU CAN PICK ONE KEYSTONE. TO SWAP, RESPEC (FREE IN THE HOUSE)."
                    : "RESPEC IS FREE IN THE HOUSE.", label);
                Label(R(8, 214, 296, 9), $"TIER RULE: SPEND {Talents.PointsToOpenNextTier} POINTS IN A TIER TO OPEN THE NEXT ONE.", label);
            }

            // ---- Footer: button chips ----
            float fx = 8;
            fx = Chip(R, s, font, fx, "A", "LEARN");
            fx = Chip(R, s, font, fx, "B", "BACK");
            Chip(R, s, font, fx, "Y", "RESPEC");
        }

        private static float Width(string value, GUIStyle style, int s)
        {
            labelMeasure.text = value;
            return style.CalcSize(labelMeasure).x / s;
        }

        private static void Outline(Func<float, float, float, float, Rect> R, float x, float y, float w, float h, Color c)
        {
            PixelUI.Fill(R(x, y, w, 1), c);
            PixelUI.Fill(R(x, y + h - 1, w, 1), c);
            PixelUI.Fill(R(x, y, 1, h), c);
            PixelUI.Fill(R(x + w - 1, y, 1, h), c);
        }

        // "PACT: a, b; but c" -> ups [a, b], downs [c]. A plain effect is one upside; ", but" /
        // "; but" starts the downside; a pact's upsides split on commas outside brackets.
        private static void SplitEffect(string effect, out List<string> ups, out List<string> downs)
        {
            ups = new List<string>();
            downs = new List<string>();
            string e = effect.Trim();
            bool pact = e.StartsWith("PACT:");
            if (pact)
                e = e.Substring(5).Trim();
            int but = e.IndexOf("; but ", StringComparison.Ordinal);
            if (but < 0)
                but = e.IndexOf(", but ", StringComparison.Ordinal);
            string up = but >= 0 ? e.Substring(0, but) : e;
            if (but >= 0)
                downs.Add(e.Substring(but + 6).Trim());
            if (!pact)
            {
                ups.Add(up.Trim());
                return;
            }
            int depth = 0, start = 0;
            for (int i = 0; i < up.Length; i++)
            {
                char c = up[i];
                if (c == '(')
                    depth++;
                else if (c == ')')
                    depth--;
                else if (c == ',' && depth == 0)
                {
                    ups.Add(up.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }
            ups.Add(up.Substring(start).Trim());
        }

        private float EffectLine(Func<float, float, float, float, Rect> R, int s, int font, float y, string mark, string line, Color markColour, Color colour, ref int left)
        {
            if (left <= 0 || string.IsNullOrEmpty(line))
                return y;
            GUIStyle style = PixelUI.Style(font, colour);
            Label(R(8, y, 8, 9), mark, PixelUI.Style(font, markColour));
            foreach (string part in Wrap(line.ToUpperInvariant(), style, 286 * s, left))
            {
                Label(R(16, y, 290, 9), part, style);
                y += 9;
                left--;
            }
            return y;
        }
    }
}
