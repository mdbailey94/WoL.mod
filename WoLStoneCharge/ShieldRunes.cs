using System.Collections.Generic;
using UnityEngine;

namespace WoLStoneCharge
{
    // Glowing cracks spreading through the stone shield as force builds, and an earth rune
    // burning in it at full force. Drawn over the game's own shield sprite as a child of it (so
    // it turns, flips and goes with it), cut to the shield's actual outline: the sprite's pixels
    // are copied once through a render texture (game textures can't be read directly) for its
    // alpha, falling back to an ellipse inside its bounds if that can't be done. Each of the
    // shield's sprites (one per facing) gets its own cracks, made once and kept.
    //
    // A crack is a groove: shade on its upper lip and an ember core whose light runs outward
    // along it (4 frames, looping), in the game's fire tones: orange at 25% force, yellow at 50%,
    // pale yellow-white from 75%; more and longer cracks each step. No outlines (0.4.1).
    public class ShieldRunes : MonoBehaviour
    {
        public const int Stages = 4;               // force steps of 25%
        private const int GlowFrames = 4;
        private const float GlowFps = 10f;
        private const float FullGlowFps = 15f;     // the light races at full force
        private const int MaxCracks = 6;
        private const int MaxLength = 18;          // pixels
        private static readonly int[] CracksAt = { 0, 3, 4, 5, 6 };    // cracks showing at each stage
        private static readonly int[] LengthAt = { 0, 6, 10, 14, 18 }; // and how far along they've run
        private static readonly int[] BrightAt = { 0, 7, 8, 9, 9 };    // the brightest ember tone at each stage

        // An earth rune: an open diamond (a cut stone) round a cross.
        private static readonly string[] Rune =
        {
            "...#...",
            "..#.#..",
            ".#...#.",
            "#..#..#",
            "#.###.#",
            "#..#..#",
            ".#...#.",
            "..#.#..",
            "...#...",
        };

        private struct CrackPixel
        {
            public int X, Y, Crack, Step;
        }

        private class Art
        {
            public Sprite[][] Stages;              // [stage][frame], stage 0 unused
        }

        private static readonly Dictionary<Sprite, Art> cache = new Dictionary<Sprite, Art>();
        private static bool failed;

        private SpriteRenderer source;
        private SpriteRenderer sr;
        private int stage;
        private Sprite builtFor;
        private int builtStage = -1;
        private Sprite[] frames;
        private float clock;

        public int Stage => stage;

        // On the shield's sprite renderer; null (logged) if it can't be made.
        public static ShieldRunes Attach(SpriteRenderer shieldSprite)
        {
            if (shieldSprite == null || failed)
                return null;
            var go = new GameObject("StoneChargeShieldRunes");
            go.transform.SetParent(shieldSprite.transform, false);
            ShieldRunes runes = go.AddComponent<ShieldRunes>();
            runes.source = shieldSprite;
            runes.sr = go.AddComponent<SpriteRenderer>();
            runes.sr.enabled = false;
            return runes;
        }

        public void SetStage(int value) => stage = Mathf.Clamp(value, 0, Stages);

        private void LateUpdate()
        {
            if (source == null)
            {
                Destroy(gameObject);
                return;
            }
            if (failed || stage == 0 || source.sprite == null || !source.enabled)
            {
                sr.enabled = false;
                return;
            }
            sr.sortingLayerID = source.sortingLayerID;
            sr.sortingOrder = source.sortingOrder + 1;
            sr.flipX = source.flipX;
            if (source.sprite != builtFor || builtStage != stage)
            {
                builtFor = source.sprite;
                builtStage = stage;
                frames = null;
                try
                {
                    frames = For(source.sprite).Stages[stage];
                }
                catch (System.Exception e)
                {
                    failed = true;
                    StoneChargeState.Log($"effect 'shield cracks' unavailable, skipping it: {e.Message}");
                }
            }
            if (!PixelArt.Alive(frames))
            {
                sr.enabled = false;
                return;
            }
            sr.enabled = true;
            clock += Time.deltaTime * (stage >= Stages ? FullGlowFps : GlowFps);
            sr.sprite = frames[Mathf.FloorToInt(clock) % frames.Length];
        }

        // ---- Drawing ----

        private static Art For(Sprite sprite)
        {
            Art art;
            if (cache.TryGetValue(sprite, out art) && art.Stages[1] != null && PixelArt.Alive(art.Stages[1]))
                return art;
            int w = Mathf.RoundToInt(sprite.rect.width), h = Mathf.RoundToInt(sprite.rect.height);
            bool read;
            bool[] mask = Mask(sprite, w, h, out read);
            bool[] inner = new bool[w * h];
            int count = 0;
            float sx = 0f, sy = 0f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!In(mask, w, h, x, y))
                        continue;
                    count++;
                    sx += x;
                    sy += y;
                    inner[y * w + x] = In(mask, w, h, x + 1, y) && In(mask, w, h, x - 1, y) && In(mask, w, h, x, y + 1) && In(mask, w, h, x, y - 1);
                }
            }
            int cx = count > 0 ? Mathf.RoundToInt(sx / count) : w / 2, cy = count > 0 ? Mathf.RoundToInt(sy / count) : h / 2;
            List<CrackPixel> cracks = Grow(inner, w, h, cx, cy, new System.Random(w * 73 + h * 31 + count));
            bool rune = Fits(inner, w, h, cx, cy);

            art = new Art { Stages = new Sprite[Stages + 1][] };
            Vector2 pivot = new Vector2(sprite.pivot.x / Mathf.Max(1f, sprite.rect.width), sprite.pivot.y / Mathf.Max(1f, sprite.rect.height));
            for (int s = 1; s <= Stages; s++)
            {
                var sheet = new PixelArt.Grid[GlowFrames];
                for (int g = 0; g < GlowFrames; g++)
                    sheet[g] = Frame(mask, cracks, w, h, s, g, rune && s == Stages, cx, cy);
                art.Stages[s] = PixelArt.Sheet(sheet, StoneArt.Palette, pivot, sprite.pixelsPerUnit);
            }
            cache[sprite] = art;
            StoneChargeState.Log($"visual 'shield cracks' ready for {sprite.name} ({Stages * GlowFrames} sprites of {w}x{h}, ppu {sprite.pixelsPerUnit:0}, " +
                $"{cracks.Count} crack pixels{(rune ? ", rune" : ", no room for the rune")}, " +
                (read ? $"cut to the shield's outline ({count} px)" : "an ellipse in its bounds (couldn't read the sprite)") + ")");
            return art;
        }

        // Cracks radiating from one point of strain at the middle, spread round the compass
        // (alternate sides first, so even two read as a split through the stone), wandering a
        // little, staying a pixel inside the outline. Each pixel remembers its crack and how far
        // along it is.
        private static readonly int[] CrackOrder = { 0, 3, 1, 4, 2, 5 };

        private static List<CrackPixel> Grow(bool[] inner, int w, int h, int cx, int cy, System.Random rng)
        {
            var pixels = new List<CrackPixel>();
            var seen = new HashSet<int>();
            for (int c = 0; c < MaxCracks; c++)
            {
                int x0 = cx + rng.Next(-1, 2), y0 = cy + rng.Next(-1, 2);
                if (!In(inner, w, h, x0, y0))
                {
                    x0 = cx;
                    y0 = cy;
                }
                float angle = CrackOrder[c] * 360f / MaxCracks + 30f + (float)rng.NextDouble() * 30f - 15f;
                float fx = x0 + 0.5f, fy = y0 + 0.5f;
                for (int step = 0; step < MaxLength; step++)
                {
                    int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
                    if (!In(inner, w, h, ix, iy))
                        break;
                    if (seen.Add(iy * w + ix))
                        pixels.Add(new CrackPixel { X = ix, Y = iy, Crack = c, Step = step });
                    if (rng.NextDouble() < 0.35)
                        angle += (float)rng.NextDouble() * 80f - 40f;
                    fx += Mathf.Cos(angle * Mathf.Deg2Rad);
                    fy += Mathf.Sin(angle * Mathf.Deg2Rad);
                }
            }
            return pixels;
        }

        private static PixelArt.Grid Frame(bool[] mask, List<CrackPixel> cracks, int w, int h, int stage, int glow, bool rune, int cx, int cy)
        {
            var g = new PixelArt.Grid(w, h);
            var on = new HashSet<int>();
            foreach (CrackPixel p in cracks)
            {
                if (p.Crack < CracksAt[stage] && p.Step < LengthAt[stage])
                    on.Add(p.Y * w + p.X);
            }
            // The groove's upper lip in shade (the light comes from the top, as on the game's
            // rocks); no rims otherwise.
            foreach (int i in on)
            {
                int x = i % w, y = i / w;
                if (In(mask, w, h, x, y + 1) && !on.Contains((y + 1) * w + x) && g.Get(x, y + 1) == 0)
                    g.Set(x, y + 1, 2);
            }
            // The ember core, its light running outward: brightest where (step - 2 * frame) is
            // a multiple of 8, a tone dimmer every two pixels behind.
            foreach (CrackPixel p in cracks)
            {
                if (!on.Contains(p.Y * w + p.X))
                    continue;
                int b = (((p.Step - glow * 2) % 8) + 8) % 8;
                int index = b < 2 ? 9 : b < 4 ? 8 : b < 6 ? 7 : 6;
                g.Set(p.X, p.Y, Mathf.Min(index, BrightAt[stage]));
            }
            if (rune)
            {
                // Burning in the middle, pulsing pale and yellow, cut into the stone: a shade
                // along the top of its strokes (as the cracks' lips), no outline.
                int rw = Rune[0].Length, rh = Rune.Length;
                int ox = cx - rw / 2, oy = cy - rh / 2;
                var glyph = new PixelArt.Grid(w, h);
                glyph.Stamp(Rune, ".#", ox, oy);
                int body = glow % 2 == 0 ? 9 : 8;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (glyph.Get(x, y) != 0)
                            g.Set(x, y, body);
                        else if (In(mask, w, h, x, y) && glyph.Get(x, y - 1) != 0)
                            g.Set(x, y, 2);
                    }
                }
            }
            return g;
        }

        // Room for the rune (and its outline) inside the shield at its middle.
        private static bool Fits(bool[] inner, int w, int h, int cx, int cy)
        {
            int rw = Rune[0].Length, rh = Rune.Length;
            int ox = cx - rw / 2, oy = cy - rh / 2;
            for (int y = oy - 1; y <= oy + rh; y++)
            {
                for (int x = ox - 1; x <= ox + rw; x++)
                {
                    if (!In(inner, w, h, x, y))
                        return false;
                }
            }
            return true;
        }

        private static bool In(bool[] mask, int w, int h, int x, int y) => x >= 0 && y >= 0 && x < w && y < h && mask[y * w + x];

        // The shield sprite's solid pixels (alpha over half), in its rect's space; an ellipse in
        // its bounds if the pixels can't be read (read = false).
        private static bool[] Mask(Sprite sprite, int w, int h, out bool read)
        {
            var mask = new bool[w * h];
            read = false;
            try
            {
                Rect region = sprite.textureRect;
                Vector2 offset = sprite.textureRectOffset;
                Color32[] pixels = ReadRegion(sprite.texture, region);
                int tw = Mathf.RoundToInt(region.width), th = Mathf.RoundToInt(region.height);
                int ox = Mathf.RoundToInt(offset.x), oy = Mathf.RoundToInt(offset.y);
                int solid = 0;
                if (pixels != null && pixels.Length >= tw * th)
                {
                    for (int y = 0; y < th; y++)
                    {
                        for (int x = 0; x < tw; x++)
                        {
                            int mx = x + ox, my = y + oy;
                            if (pixels[y * tw + x].a > 128 && mx >= 0 && my >= 0 && mx < w && my < h)
                            {
                                mask[my * w + mx] = true;
                                solid++;
                            }
                        }
                    }
                }
                read = solid >= 16;
            }
            catch
            {
                read = false;
            }
            if (read)
                return mask;
            for (int i = 0; i < mask.Length; i++)
                mask[i] = false;
            float rx = w * 0.42f, ry = h * 0.42f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - w * 0.5f) / rx, dy = (y + 0.5f - h * 0.5f) / ry;
                    mask[y * w + x] = dx * dx + dy * dy <= 1f;
                }
            }
            return mask;
        }

        // Game textures aren't readable from code: copy one through a render texture and read
        // the region back (the Slingshot mod's SpriteReader does the same).
        private static Color32[] ReadRegion(Texture source, Rect region)
        {
            if (source == null)
                return null;
            RenderTexture previous = RenderTexture.active;
            RenderTexture rt = null;
            Texture2D copy = null;
            try
            {
                int w = Mathf.RoundToInt(region.width), h = Mathf.RoundToInt(region.height);
                if (w <= 0 || h <= 0)
                    return null;
                rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                copy.ReadPixels(region, 0, 0);
                copy.Apply();
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                if (rt != null)
                    RenderTexture.ReleaseTemporary(rt);
                if (copy != null)
                    Object.Destroy(copy);
            }
        }
    }
}
