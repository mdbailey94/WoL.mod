using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    /// <summary>
    /// #108 BUFF BAR (Matt 11-04: "a buff bar below your health bar"; 17:10: "it should just show
    /// the icon of the ability, # of stacks if applicable"). WoL Player's file. Under each wizard's
    /// health bar (the game's own PlayerStatusBar, P1 top-left, P2 top-right), one 16 px icon per
    /// active buff (the talent's own icon, talent_[key].png), with a stack count in its corner
    /// when it stacks. No names, no timers. Drawn in the run's HUD while talents are on.
    /// Buffs come from ArchetypeMechanics, TalentMechanics and TreeMechanics (each lists its own).
    /// </summary>
    internal static class BuffBar
    {
        private const int IconPx = 16, GapPx = 2, BelowPx = 3;
        private static readonly List<KeyValuePair<string, int>> buffs = new List<KeyValuePair<string, int>>();
        private static readonly Vector3[] corners = new Vector3[4];
        private static bool failed;
        private static BepInEx.Configuration.ConfigEntry<bool> show;

        public static void Draw()
        {
            if (show == null && ChaosDepthsPlugin.Instance != null)
                show = ChaosDepthsPlugin.Instance.Config.Bind("HUD", "BuffBar", true,
                    "#108: icons of your active talent buffs under each health bar, with a stack count when they stack.");
            if (!Talents.Enabled || failed || (show != null && !show.Value))
                return;
            try
            {
                Player[] players = GameController.activePlayers;
                if (players == null)
                    return;
                foreach (Player player in players)
                {
                    if (player == null || !player.gameObject.activeInHierarchy)
                        continue;
                    buffs.Clear();
                    ArchetypeMechanics.Buffs(player, buffs);
                    TalentMechanics.Buffs(player, buffs);
                    TreeMechanics.Buffs(player, buffs);
                    if (buffs.Count == 0)
                        continue;
                    Rect bar;
                    if (!HealthBarRect(player, out bar))
                        continue;
                    DrawRow(bar, player.playerID != 0);
                }
            }
            catch (Exception e)
            {
                failed = true;
                ChaosDepthsPlugin.LogError($"buff bar: {e.Message} (turned off for this session)");
            }
        }

        // The game's health bar group for this wizard, in GUI pixels (y from the top).
        private static bool HealthBarRect(Player player, out Rect rect)
        {
            rect = default(Rect);
            PlayerStatusBar[] bars = GameController.playerStatusBars;
            int index = player.playerID != 0 ? 1 : 0;
            if (bars == null || bars.Length <= index || bars[index] == null || !bars[index].gameObject.activeInHierarchy)
                return false;
            Transform group = bars[index].transform.Find("HealthBar");
            RectTransform rt = group != null ? group as RectTransform : null;
            if (rt == null)
                return false;
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.enabled)
                return false;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            rt.GetWorldCorners(corners);
            float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
            foreach (Vector3 c in corners)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, c);
                xMin = Mathf.Min(xMin, sp.x);
                xMax = Mathf.Max(xMax, sp.x);
                yMin = Mathf.Min(yMin, sp.y);
                yMax = Mathf.Max(yMax, sp.y);
            }
            if (xMax <= xMin || yMax <= yMin)
                return false;
            rect = new Rect(xMin, Screen.height - yMax, xMax - xMin, yMax - yMin);
            return true;
        }

        // One row of icons under the bar: from its left edge for P1, its right edge for P2.
        private static void DrawRow(Rect bar, bool rightAligned)
        {
            int s = Mathf.Max(2, Mathf.FloorToInt(Screen.height / 360f));
            float size = IconPx * s, gap = GapPx * s;
            float y = Mathf.Floor(bar.yMax + BelowPx * s);
            float step = size + gap;
            int font = Mathf.Max(10, 6 * s);
            GUIStyle count = PixelUI.Style(font, PixelUI.Text, TextAnchor.LowerRight);
            GUIStyle shadow = PixelUI.Style(font, new Color(0f, 0f, 0f, 0.9f), TextAnchor.LowerRight);
            for (int i = 0; i < buffs.Count; i++)
            {
                float x = rightAligned ? bar.xMax - size - i * step : bar.xMin + i * step;
                x = Mathf.Floor(x);
                var r = new Rect(x, y, size, size);
                Texture2D icon = ItemArt.LoadPng("talent_" + buffs[i].Key);
                PixelUI.Fill(r, new Color(0f, 0f, 0f, 0.45f));
                if (icon != null)
                    GUI.DrawTexture(r, icon);
                int stacks = buffs[i].Value;
                if (stacks > 0)
                {
                    string n = stacks.ToString();
                    var tr = new Rect(x, y, size + s, size + s);
                    GUI.Label(new Rect(tr.x + s, tr.y + s, tr.width, tr.height), n, shadow);
                    GUI.Label(tr, n, count);
                }
            }
        }
    }
}
