using UnityEngine;
using UnityEngine.UI;

namespace WoLCloakUpgrades
{
    // Small IMGUI helpers that draw crisp pixel-art shapes (frames, pips, arrows) out of solid
    // rectangles, sized in whole "pixel units" so they stay sharp at any resolution, and use the
    // game's own UI font when one can be found.
    public static class PixelUI
    {
        public static readonly Color Background = new Color32(0x1b, 0x15, 0x30, 0xf2);
        public static readonly Color Border = new Color32(0xd9, 0xb0, 0x5c, 0xff);
        public static readonly Color InnerLine = new Color32(0x4a, 0x3b, 0x6b, 0xff);
        public static readonly Color Text = new Color32(0xf2, 0xe8, 0xd0, 0xff);
        public static readonly Color Dim = new Color32(0x9a, 0x92, 0xb0, 0xff);
        public static readonly Color Accent = new Color32(0xff, 0x8a, 0x3d, 0xff);
        public static readonly Color PipEmpty = new Color32(0x3a, 0x30, 0x55, 0xff);

        private static Texture2D white;
        private static Font gameFont;
        private static float nextFontSearch;

        // One "pixel" of UI: 4 screen pixels at 1080p, scaled with the screen height.
        public static int Unit => Mathf.Max(2, Mathf.FloorToInt(Screen.height / 270f));

        public static void Fill(Rect rect, Color color)
        {
            if (white == null)
            {
                white = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                white.SetPixel(0, 0, Color.white);
                white.Apply();
            }
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, white);
            GUI.color = previous;
        }

        // A framed panel with notched (pixel-rounded) corners and an inner accent line.
        public static void Panel(Rect r, int u)
        {
            Fill(new Rect(r.x + u, r.y + u, r.width - 2 * u, r.height - 2 * u), Background);
            Fill(new Rect(r.x + u, r.y, r.width - 2 * u, u), Border);
            Fill(new Rect(r.x + u, r.yMax - u, r.width - 2 * u, u), Border);
            Fill(new Rect(r.x, r.y + u, u, r.height - 2 * u), Border);
            Fill(new Rect(r.xMax - u, r.y + u, u, r.height - 2 * u), Border);

            var inner = new Rect(r.x + 2 * u, r.y + 2 * u, r.width - 4 * u, r.height - 4 * u);
            Fill(new Rect(inner.x, inner.y, inner.width, u), InnerLine);
            Fill(new Rect(inner.x, inner.yMax - u, inner.width, u), InnerLine);
            Fill(new Rect(inner.x, inner.y, u, inner.height), InnerLine);
            Fill(new Rect(inner.xMax - u, inner.y, u, inner.height), InnerLine);
        }

        // A square pip with a one-unit highlight when filled.
        public static void Pip(Rect r, bool filled, int u)
        {
            Fill(r, filled ? Accent : PipEmpty);
            if (filled)
                Fill(new Rect(r.x, r.y, r.width, u), new Color32(0xff, 0xc0, 0x80, 0xff));
        }

        // A solid pixel triangle pointing left or right, `size` units tall (odd numbers look best).
        public static Rect Arrow(float x, float centerY, int size, bool pointRight, Color color, int u)
        {
            int half = size / 2;
            for (int row = 0; row < size; row++)
            {
                int length = half + 1 - Mathf.Abs(row - half);
                float rowX = pointRight ? x : x + (half + 1 - length) * u;
                Fill(new Rect(rowX, centerY - (half - row) * u - u / 2f, length * u, u), color);
            }
            return new Rect(x, centerY - (half + 1) * u, (half + 1) * u, (size + 1) * u);
        }

        // The game's UI font, borrowed from any active UI Text; null until one is found.
        public static Font GameFont()
        {
            if (gameFont == null && Time.unscaledTime >= nextFontSearch)
            {
                nextFontSearch = Time.unscaledTime + 2f;
                foreach (Text text in Object.FindObjectsOfType<Text>())
                {
                    if (text != null && text.font != null)
                    {
                        gameFont = text.font;
                        break;
                    }
                }
            }
            return gameFont;
        }

        public static GUIStyle Style(int fontSize, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                richText = true,
                wordWrap = false,
                alignment = anchor
            };
            Font font = GameFont();
            if (font != null)
                style.font = font;
            style.normal.textColor = color;
            return style;
        }
    }
}
