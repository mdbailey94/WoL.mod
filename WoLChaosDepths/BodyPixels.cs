using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    // The wizard's picture read as pixels, so the worn gear can find the hand and chest in each
    // frame from what is really drawn. The sprite's outline polygon is only an approximation: a
    // corner of it can stick out well past the picture, which in a side run put the ring out in
    // front of the face (#24). The game's sheets aren't readable, so each is copied once through
    // a render texture; each frame's sprite is then scanned once into per-row spans.
    public static class BodyPixels
    {
        // One frame's opaque pixels: for each row of its rect (0 = bottom), the leftmost and
        // rightmost opaque column (-1 when the row is empty), and the lowest and highest rows.
        public class Shape
        {
            public int[] minX, maxX;
            public int bottom, top;
            public Vector2 origin;  // the rect's bottom-left in the sprite's local pixels, from its pivot
            public float ppu;

            public bool Row(int y, out int lo, out int hi)
            {
                lo = hi = -1;
                if (y < 0 || y >= minX.Length || minX[y] < 0)
                    return false;
                lo = minX[y];
                hi = maxX[y];
                return true;
            }

            // A pixel's centre in the sprite's local units.
            public Vector2 Local(int x, int y) => new Vector2((origin.x + x + 0.5f) / ppu, (origin.y + y + 0.5f) / ppu);
        }

        private static readonly Dictionary<Texture2D, Color32[]> texels = new Dictionary<Texture2D, Color32[]>();
        private static readonly HashSet<Texture2D> unreadable = new HashSet<Texture2D>();
        private static readonly Dictionary<Sprite, Shape> shapes = new Dictionary<Sprite, Shape>();

        // Null when the frame can't be read (tightly packed or rotated in an atlas, too big).
        public static Shape Of(Sprite sprite)
        {
            if (sprite == null)
                return null;
            Shape shape;
            if (shapes.TryGetValue(sprite, out shape))
                return shape;
            shape = null;
            try
            {
                shape = Scan(sprite);
            }
            catch
            {
            }
            shapes[sprite] = shape;
            return shape;
        }

        // On a scene change: forget sheets and frames the game has unloaded (kept ones aren't
        // read again).
        public static void Prune()
        {
            foreach (Texture2D t in new List<Texture2D>(texels.Keys))
                if (t == null)
                    texels.Remove(t);
            unreadable.RemoveWhere(t => t == null);
            keyedSheets.RemoveWhere(t => t == null);
            foreach (Sprite s in new List<Sprite>(shapes.Keys))
                if (s == null)
                    shapes.Remove(s);
        }

        private static Shape Scan(Sprite sprite)
        {
            if (sprite.packed && sprite.packingMode == SpritePackingMode.Tight)
                return null;
            if (sprite.packed && sprite.packingRotation != SpritePackingRotation.None)
                return null;
            Texture2D tex = sprite.texture;
            Color32[] px = Texels(tex);
            if (px == null)
                return null;
            Rect r = sprite.textureRect;
            int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y), w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            if (w <= 0 || h <= 0 || x0 < 0 || y0 < 0 || x0 + w > tex.width || y0 + h > tex.height)
                return null;
            var shape = new Shape { minX = new int[h], maxX = new int[h], bottom = -1, top = -1, ppu = sprite.pixelsPerUnit };
            bool keyed = keyedSheets.Contains(tex);
            Color32 key = px[y0 * tex.width + x0];
            int solid = ScanRows(shape, px, tex.width, x0, y0, w, h, keyed, key);
            // The wizard's sheet can come back from the copy with every pixel opaque, so each
            // row spanned the whole frame and the pendant and ring sat at its front edge while
            // running (0.15.5). Then the frame's corner colour is its background.
            if (!keyed && solid == w * h)
            {
                keyedSheets.Add(tex);
                ChaosDepthsPlugin.Log($"adornments: {tex.name} reads fully opaque; using its background colour ({key.r},{key.g},{key.b},{key.a}) as empty");
                solid = ScanRows(shape, px, tex.width, x0, y0, w, h, true, key);
            }
            if (loggedSheets.Add(tex))
                ChaosDepthsPlugin.Log($"adornments: {tex.name} frame {sprite.name}: {solid} of {w * h} pixels drawn, rows {shape.bottom}-{shape.top}");
            if (shape.bottom < 0 || solid == w * h)
                return null;
            // The pivot is in pixels from the sprite's full rect; the texture rect may be offset
            // inside it.
            shape.origin = sprite.textureRectOffset - sprite.pivot;
            return shape;
        }

        // Sheets whose empty pixels are told by the background colour, not by alpha.
        private static readonly HashSet<Texture2D> keyedSheets = new HashSet<Texture2D>(), loggedSheets = new HashSet<Texture2D>();

        // Fills the shape's row spans; returns how many pixels counted as drawn.
        private static int ScanRows(Shape shape, Color32[] px, int texWidth, int x0, int y0, int w, int h, bool keyed, Color32 key)
        {
            int count = 0;
            shape.bottom = shape.top = -1;
            for (int y = 0; y < h; y++)
            {
                int lo = -1, hi = -1, row = (y0 + y) * texWidth + x0;
                for (int x = 0; x < w; x++)
                {
                    Color32 c = px[row + x];
                    bool drawn = keyed ? c.a > 127 && (c.r != key.r || c.g != key.g || c.b != key.b) : c.a > 127;
                    if (!drawn)
                        continue;
                    count++;
                    if (lo < 0)
                        lo = x;
                    hi = x;
                }
                shape.minX[y] = lo;
                shape.maxX[y] = hi;
                if (lo >= 0)
                {
                    if (shape.bottom < 0)
                        shape.bottom = y;
                    shape.top = y;
                }
            }
            return count;
        }

        private static Color32[] Texels(Texture2D tex)
        {
            if (tex == null || unreadable.Contains(tex))
                return null;
            Color32[] px;
            if (texels.TryGetValue(tex, out px))
                return px;
            px = null;
            if (tex.width <= 4096 && tex.height <= 4096)
            {
                try
                {
                    px = tex.GetPixels32();
                }
                catch
                {
                    px = null;
                }
                if (px == null)
                {
                    RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    RenderTexture previous = RenderTexture.active;
                    Texture2D copy = null;
                    try
                    {
                        Graphics.Blit(tex, rt);
                        RenderTexture.active = rt;
                        copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                        copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                        copy.Apply();
                        px = copy.GetPixels32();
                    }
                    catch (Exception e)
                    {
                        ChaosDepthsPlugin.LogWarning($"adornments: couldn't read the wizard's sheet {tex.name} ({e.Message}); using its outline");
                        px = null;
                    }
                    finally
                    {
                        RenderTexture.active = previous;
                        RenderTexture.ReleaseTemporary(rt);
                        if (copy != null)
                            UnityEngine.Object.Destroy(copy);
                    }
                }
            }
            if (px == null)
            {
                unreadable.Add(tex);
                return null;
            }
            texels[tex] = px;
            return px;
        }
    }
}
