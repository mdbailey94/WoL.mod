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
        // sprite on top. This makes the sprite that replaces the artwork: the picture fills the
        // frame's opening (the frame's visible outline, shrunk by FrameBorder), whatever the
        // artwork's own size, at `detail` times the game's pixel count (1 = the game's own pixel
        // size, sharper above), nudged up by NudgeUp of the game's pixels if asked. Without a
        // frame, the artwork's own visible area is filled.
        public static Sprite BuildArt(SpriteRenderer art, SpriteRenderer frame, Texture2D picture, int detail)
        {
            Sprite original = art.sprite;
            int border = CustomPaintingsPlugin.FrameBorder;
            int nudge = CustomPaintingsPlugin.NudgeUp;
            string key = original.GetInstanceID() + "/" + (frame != null && frame.sprite != null ? frame.sprite.GetInstanceID() : 0) +
                "/" + picture.GetInstanceID() + "/art" + detail + "/" + nudge + "/" + border + "/" + CustomPaintingsPlugin.PictureFit;
            Sprite cached;
            if (cache.TryGetValue(key, out cached) && cached != null)
                return cached;

            float ppu = original.pixelsPerUnit;
            Rect area; // in the artwork's local space, in units
            string from;
            if (frame != null && frame.sprite != null && OutlineIn(frame, art.transform, out area))
            {
                float inset = border / ppu;
                area = Rect.MinMaxRect(area.xMin + inset, area.yMin + inset, area.xMax - inset, area.yMax - inset);
                from = $"frame '{frame.sprite.name}' less {border}px";
            }
            else if (OutlineIn(art, art.transform, out area))
                from = "the artwork's outline";
            else
                return null;
            area.y += nudge / ppu;

            int w = Mathf.RoundToInt(area.width * ppu);
            int h = Mathf.RoundToInt(area.height * ppu);
            if (w < 6 || h < 6)
                return null;
            // Fill: the picture's own pixels, untouched. Its size is read, it's cropped to the
            // opening's shape, and the sprite is scaled (pixels per unit) to fit the opening, so
            // nothing is resampled.
            if (CustomPaintingsPlugin.PictureFit == "Fill")
            {
                Sprite own = OwnPixels(picture, area, original.name, from);
                if (own != null)
                {
                    cache[key] = own;
                    return own;
                }
            }

            int d = Mathf.Clamp(detail, 1, 4);
            int bw = w * d, bh = h * d;

            var pixels = new Color32[bw * bh];
            PaintCanvas(pixels, bw, bh, 0, picture);
            var texture = new Texture2D(bw, bh, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            // The renderer's own position is the sprite's pivot: place the area round it.
            Vector2 pivot = new Vector2(-area.xMin / area.width, -area.yMin / area.height);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, bw, bh), pivot, ppu * d);
            cache[key] = sprite;
            CustomPaintingsPlugin.Log($"Picture over artwork '{original.name}' ({original.rect.width}x{original.rect.height}): " +
                $"{w}x{h} from {from}, picture {picture.width}x{picture.height} ({CustomPaintingsPlugin.PictureFit}), detail x{d}" +
                (nudge != 0 ? $", nudged up {nudge}" : ""));
            return sprite;
        }

        // The picture cropped to the area's shape (centred across, keeping the top, where faces
        // usually are), at its own pixels, the sprite sized to the area.
        private static Sprite OwnPixels(Texture2D picture, Rect area, string artName, string from)
        {
            int pw = picture.width, ph = picture.height;
            float areaAspect = area.width / area.height;
            int cw = pw, ch = ph;
            if (pw / (float)ph > areaAspect)
                cw = Mathf.Clamp(Mathf.RoundToInt(ph * areaAspect), 1, pw);
            else
                ch = Mathf.Clamp(Mathf.RoundToInt(pw / areaAspect), 1, ph);
            int cx = (pw - cw) / 2;
            int fromTop = Mathf.RoundToInt((ph - ch) * 0.15f);
            int cy = ph - fromTop - ch; // texture rows start at the bottom
            if (cw < 2 || ch < 2)
                return null;

            var texture = new Texture2D(cw, ch, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] src = picture.GetPixels32();
            var pixels = new Color32[cw * ch];
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    Color32 c = src[(y + cy) * pw + (x + cx)];
                    pixels[y * cw + x] = new Color32(c.r, c.g, c.b, 255);
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            // Pixels per unit so the crop spans the area exactly (its shape matches, to a pixel).
            float ppu = cw / area.width;
            Vector2 pivot = new Vector2(-area.xMin / area.width, -area.yMin / area.height);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, cw, ch), pivot, ppu);
            CustomPaintingsPlugin.Log($"Picture over artwork '{artName}': picture {pw}x{ph} at its own pixels, " +
                $"cropped to {cw}x{ch} for the opening from {from}");
            return sprite;
        }

        // The bounds of a sprite's visible part (its mesh outline), in another transform's local
        // space, in units.
        private static bool OutlineIn(SpriteRenderer renderer, Transform space, out Rect area)
        {
            area = new Rect();
            try
            {
                Vector2[] vertices = renderer.sprite.vertices;
                if (vertices == null || vertices.Length == 0)
                    return false;
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (Vector2 v in vertices)
                {
                    Vector3 world = renderer.transform.TransformPoint(new Vector3(renderer.flipX ? -v.x : v.x, renderer.flipY ? -v.y : v.y, 0f));
                    Vector3 local = space.InverseTransformPoint(world);
                    minX = Mathf.Min(minX, local.x);
                    minY = Mathf.Min(minY, local.y);
                    maxX = Mathf.Max(maxX, local.x);
                    maxY = Mathf.Max(maxY, local.y);
                }
                area = Rect.MinMaxRect(minX, minY, maxX, maxY);
                return area.width > 0f && area.height > 0f;
            }
            catch
            {
                return false;
            }
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
        // Paints the picture into the canvas (the area inside `inset`), shrunk with a box filter
        // and snapped to the picture's own colours. How it's fitted (PictureFit):
        // - Fill: fills the canvas, cropping the sides or the bottom (keeping the top, where faces
        //   usually are);
        // - Fit: the whole picture, scaled to fit, over a dimmed stretched copy of itself where the
        //   shapes differ (no dark bars);
        // - Stretch: the whole picture, stretched to the canvas's shape.
        private static void PaintCanvas(Color32[] pixels, int w, int h, int inset, Texture2D picture)
        {
            int cw = w - 2 * inset, ch = h - 2 * inset;
            Color32[] src = picture.GetPixels32();
            int pw = picture.width, ph = picture.height;
            string fit = CustomPaintingsPlugin.PictureFit;

            // The part of the picture used, and where in the canvas it goes.
            float cropX = 0f, cropY = 0f, cropW = pw, cropH = ph;
            int dx = 0, dy = 0, dw = cw, dh = ch;
            float canvasAspect = (float)cw / ch;
            float pictureAspect = pw / (float)ph;
            if (fit == "Fill")
            {
                if (pictureAspect > canvasAspect)
                    cropW = ph * canvasAspect;
                else
                    cropH = pw / canvasAspect;
                cropX = (pw - cropW) / 2f;
                float cropTop = (ph - cropH) * 0.15f; // from the top
                cropY = ph - cropTop - cropH;         // texture rows start at the bottom
            }
            else if (fit != "Stretch")
            {
                if (pictureAspect > canvasAspect)
                    dh = Mathf.Max(1, Mathf.RoundToInt(cw / pictureAspect));
                else
                    dw = Mathf.Max(1, Mathf.RoundToInt(ch * pictureAspect));
                dx = (cw - dw) / 2;
                dy = (ch - dh) / 2;
                if (dw < cw || dh < ch)
                    Backdrop(pixels, w, inset, cw, ch, src, pw, ph);
            }

            List<Color32> palette = Palette(src);
            for (int y = 0; y < dh; y++)
            {
                for (int x = 0; x < dw; x++)
                {
                    int x0 = Mathf.FloorToInt(cropX + cropW * x / dw);
                    int x1 = Mathf.Max(x0 + 1, Mathf.FloorToInt(cropX + cropW * (x + 1) / dw));
                    int y0 = Mathf.FloorToInt(cropY + cropH * y / dh);
                    int y1 = Mathf.Max(y0 + 1, Mathf.FloorToInt(cropY + cropH * (y + 1) / dh));
                    int r = 0, g = 0, b = 0, n = 0;
                    for (int sy = y0; sy < y1 && sy < ph; sy++)
                    {
                        for (int sx = x0; sx < x1 && sx < pw; sx++)
                        {
                            Color32 c = src[sy * pw + sx];
                            r += c.r; g += c.g; b += c.b; n++;
                        }
                    }
                    if (n == 0)
                        continue;
                    var avg = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
                    pixels[(y + dy + inset) * w + (x + dx + inset)] = Nearest(palette, avg);
                }
            }
        }

        // Behind a fitted picture: the picture itself stretched over the whole canvas, blurred
        // (big blocks averaged) and dimmed, so the gaps carry its colours instead of a dark bar.
        private static void Backdrop(Color32[] pixels, int w, int inset, int cw, int ch, Color32[] src, int pw, int ph)
        {
            const int Blocks = 6;
            for (int y = 0; y < ch; y++)
            {
                for (int x = 0; x < cw; x++)
                {
                    // The block of the picture this canvas pixel falls in.
                    int bx = x * Blocks / cw, by = y * Blocks / ch;
                    int x0 = bx * pw / Blocks, x1 = Mathf.Max(x0 + 1, (bx + 1) * pw / Blocks);
                    int y0 = by * ph / Blocks, y1 = Mathf.Max(y0 + 1, (by + 1) * ph / Blocks);
                    long r = 0, g = 0, b = 0, n = 0;
                    for (int sy = y0; sy < y1; sy += 2)
                        for (int sx = x0; sx < x1; sx += 2)
                        {
                            Color32 c = src[sy * pw + sx];
                            r += c.r; g += c.g; b += c.b; n++;
                        }
                    n = System.Math.Max(1L, n);
                    pixels[(y + inset) * w + (x + inset)] =
                        new Color32((byte)(r / n * 0.55f), (byte)(g / n * 0.55f), (byte)(b / n * 0.55f), 255);
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
