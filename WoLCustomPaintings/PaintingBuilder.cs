using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoLCustomPaintings
{
    // Builds a sprite the same size and pivot as a game painting: the painting's own frame is
    // kept (the outer FrameInset pixels), and the picture fills the canvas inside it, shrunk with
    // a box filter and snapped back to the picture's own colours so it stays crisp pixel art.
    public static class PaintingBuilder
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static readonly Color32 FrameDark = new Color32(43, 29, 20, 255);
        private static readonly Color32 FrameGold = new Color32(201, 154, 58, 255);
        private static readonly Color32 FrameShine = new Color32(240, 208, 106, 255);
        private static readonly Color32 FrameShade = new Color32(122, 84, 24, 255);

        // The game's breakable paintings are layered: a frame sprite, and the artwork as its own
        // sprite on top. This replaces the artwork: the picture fills it, cut to the artwork's own
        // shape (wherever the original is see-through, so is the picture), at `detail` times the
        // game's pixel count in the same space (1 = the game's own pixel size, sharper above).
        public static Sprite BuildArt(Sprite original, Texture2D picture, int detail)
        {
            string key = original.GetInstanceID() + "/" + picture.GetInstanceID() + "/art" + detail;
            Sprite cached;
            if (cache.TryGetValue(key, out cached) && cached != null)
                return cached;

            int w = Mathf.RoundToInt(original.rect.width);
            int h = Mathf.RoundToInt(original.rect.height);
            if (w < 8 || h < 8)
                return null;
            int d = Mathf.Clamp(detail, 1, 4);
            int bw = w * d, bh = h * d;

            Color32[] mask = ReadSprite(original, w, h);
            if (mask != null)
            {
                // A copy that came out (nearly) empty means the read failed: don't cut by it.
                int solid = 0;
                foreach (Color32 c in mask)
                    if (c.a > 8)
                        solid++;
                if (solid < mask.Length / 4)
                    mask = null;
            }

            var pixels = new Color32[bw * bh];
            PaintCanvas(pixels, bw, bh, 0, picture);
            if (mask != null)
            {
                for (int y = 0; y < bh; y++)
                    for (int x = 0; x < bw; x++)
                        if (mask[(y / d) * w + x / d].a <= 8)
                            pixels[y * bw + x] = new Color32(0, 0, 0, 0);
            }

            var texture = new Texture2D(bw, bh, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            Vector2 pivot = new Vector2(original.pivot.x / original.rect.width, original.pivot.y / original.rect.height);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, bw, bh), pivot, original.pixelsPerUnit * d);
            cache[key] = sprite;
            CustomPaintingsPlugin.Log($"Painted a picture over artwork '{original.name}' ({w}x{h}, detail x{d}" +
                (mask != null ? ", cut to its shape)" : ")"));
            return sprite;
        }

        public static Sprite Build(Sprite original, Texture2D picture)
        {
            string key = original.GetInstanceID() + "/" + picture.GetInstanceID();
            Sprite cached;
            if (cache.TryGetValue(key, out cached) && cached != null)
                return cached;

            int w = Mathf.RoundToInt(original.rect.width);
            int h = Mathf.RoundToInt(original.rect.height);
            if (w < 8 || h < 8)
                return null;

            Color32[] pixels = ReadSprite(original, w, h);
            bool keptFrame = pixels != null;
            if (!keptFrame)
                pixels = new Color32[w * h];

            int inset = CustomPaintingsPlugin.FrameInset > 0
                ? CustomPaintingsPlugin.FrameInset
                : Mathf.Max(2, Mathf.RoundToInt(Mathf.Min(w, h) * 0.16f));
            inset = Mathf.Min(inset, Mathf.Min(w, h) / 2 - 2);
            if (!keptFrame)
                DrawFrame(pixels, w, h, inset);

            PaintCanvas(pixels, w, h, inset, picture);

            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            Vector2 pivot = new Vector2(original.pivot.x / original.rect.width, original.pivot.y / original.rect.height);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, w, h), pivot, original.pixelsPerUnit);
            cache[key] = sprite;
            CustomPaintingsPlugin.Log($"Framed a picture for painting '{original.name}' ({w}x{h}, frame {inset}px, " +
                (keptFrame ? "game frame kept)" : "drawn frame)"));
            return sprite;
        }

        // Game textures aren't readable, so copy the sprite through a render texture.
        private static Color32[] ReadSprite(Sprite sprite, int w, int h)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture rt = null;
            try
            {
                Texture source = sprite.texture;
                rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                Rect region = sprite.textureRect;
                var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                copy.SetPixels32(new Color32[w * h]);
                Vector2 offset = sprite.textureRectOffset;
                copy.ReadPixels(region, Mathf.RoundToInt(offset.x), Mathf.RoundToInt(offset.y));
                copy.Apply();
                Color32[] result = copy.GetPixels32();
                UnityEngine.Object.Destroy(copy);
                return result;
            }
            catch (Exception e)
            {
                CustomPaintingsPlugin.Log($"Couldn't read the painting's frame, drawing one instead: {e.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (rt != null)
                    RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static void DrawFrame(Color32[] pixels, int w, int h, int inset)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    if (edge >= inset)
                        continue;
                    // Dark outline inside and out, a bevel (lit from the top left), gold between.
                    Color32 c;
                    if (edge == 0 || edge == inset - 1)
                        c = FrameDark;
                    else if (edge == 1)
                    {
                        int top = h - 1 - y, bottom = y, left = x, right = w - 1 - x;
                        c = Mathf.Min(top, left) <= Mathf.Min(bottom, right) ? FrameShine : FrameShade;
                    }
                    else
                        c = FrameGold;
                    pixels[y * w + x] = c;
                }
            }
        }

        // Crops the picture to the canvas shape (keeping the top, where faces usually are),
        // shrinks it and snaps each pixel to the nearest of the picture's own colours.
        private static void PaintCanvas(Color32[] pixels, int w, int h, int inset, Texture2D picture)
        {
            int cw = w - 2 * inset, ch = h - 2 * inset;
            Color32[] src = picture.GetPixels32();
            int pw = picture.width, ph = picture.height;

            float canvasAspect = (float)cw / ch;
            float cropW = pw, cropH = ph;
            if (pw / (float)ph > canvasAspect)
                cropW = ph * canvasAspect;
            else
                cropH = pw / canvasAspect;
            float cropX = (pw - cropW) / 2f;
            float cropTop = (ph - cropH) * 0.15f; // from the top
            float cropY = ph - cropTop - cropH;   // texture rows start at the bottom

            List<Color32> palette = Palette(src);
            for (int y = 0; y < ch; y++)
            {
                for (int x = 0; x < cw; x++)
                {
                    int x0 = Mathf.FloorToInt(cropX + cropW * x / cw);
                    int x1 = Mathf.Max(x0 + 1, Mathf.FloorToInt(cropX + cropW * (x + 1) / cw));
                    int y0 = Mathf.FloorToInt(cropY + cropH * y / ch);
                    int y1 = Mathf.Max(y0 + 1, Mathf.FloorToInt(cropY + cropH * (y + 1) / ch));
                    int r = 0, g = 0, b = 0, n = 0;
                    for (int sy = y0; sy < y1 && sy < ph; sy++)
                    {
                        for (int sx = x0; sx < x1 && sx < pw; sx++)
                        {
                            Color32 s = src[sy * pw + sx];
                            r += s.r; g += s.g; b += s.b; n++;
                        }
                    }
                    if (n == 0)
                        continue;
                    var avg = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
                    pixels[(y + inset) * w + (x + inset)] = Nearest(palette, avg);
                }
            }
        }

        private static List<Color32> Palette(Color32[] src)
        {
            var seen = new HashSet<int>();
            var palette = new List<Color32>();
            foreach (Color32 c in src)
            {
                int key = (c.r << 16) | (c.g << 8) | c.b;
                if (seen.Add(key))
                {
                    palette.Add(new Color32(c.r, c.g, c.b, 255));
                    if (palette.Count > 64)
                        return new List<Color32>(); // not a pixel-art picture: keep exact averages
                }
            }
            return palette;
        }

        private static Color32 Nearest(List<Color32> palette, Color32 c)
        {
            if (palette.Count == 0)
                return c;
            Color32 best = palette[0];
            int bestDist = int.MaxValue;
            foreach (Color32 p in palette)
            {
                int dr = p.r - c.r, dg = p.g - c.g, db = p.b - c.b;
                int dist = dr * dr * 3 + dg * dg * 4 + db * db * 2;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = p;
                }
            }
            return best;
        }
    }
}
