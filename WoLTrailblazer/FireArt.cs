using System.Collections.Generic;
using UnityEngine;

namespace WoLTrailblazer
{
    // The pixel art for Trailblazer's drawn fire (BlazeLook), drawn in code once and kept, in the
    // style of the game's own fire (its FireProjectiles and MeteorStrike sheets): flat colours with
    // no outline, a cream core inside yellow, amber and orange, ragged licking edges and streaks
    // running up the flame, fading by alpha. The colours are sampled from those sheets (cream
    // 246,237,164; yellow 249,210,60; amber 237,147,14; orange 218,110,0; ember 152,62,34), and the
    // stand-in soot from the game's BurnMark sheet (near-black at about 80% alpha with a thin
    // haze). Where the game's own frames are loaded BlazeLook uses those instead (its trail flames
    // and their dying sparks, and its burn marks under the char marks' glow).
    internal static class FireArt
    {
        internal static readonly Color32 Cream = new Color32(246, 237, 164, 255);
        internal static readonly Color32 Yellow = new Color32(249, 210, 60, 255);
        internal static readonly Color32 Amber = new Color32(237, 147, 14, 255);
        internal static readonly Color32 Orange = new Color32(218, 110, 0, 255);
        internal static readonly Color32 Ember = new Color32(152, 62, 34, 255);
        internal static readonly Color32 Soot = new Color32(2, 3, 3, 199);
        internal static readonly Color32 SootEdge = new Color32(6, 9, 11, 120);
        internal static readonly Color32 SootHaze = new Color32(31, 33, 37, 44);

        private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);
        private const float Tau = PixelCanvas.Tau;

        // ---- Flame tongues: on the wizard (and on the trail when the game's flames aren't loaded) ----
        // Three sizes (small, medium, large: width and height in pixels), each leaning left,
        // upright or right; six frames round a flicker, the tip bobbing and swaying and a lick of
        // flame breaking off the top in two of them.
        public const int Tiers = 3;
        private static readonly int[] TongueWidth = { 7, 9, 11 };
        private static readonly int[] TongueHeight = { 11, 14, 18 };
        public const int TongueFrames = 6;
        public const float TongueFrameTime = 0.07f;      // about 14 a second: a lively flicker that still reads
        private const float LeanPixels = 2.5f;          // how far the tip leans at full lean
        private const float SwayPixels = 1.4f;          // how far the flicker sways it
        private const float RaggedPixels = 0.7f;        // how far the edges lick in and out, row by row

        // The dying flame when a trail patch goes out: shrinking and cooling to an ember.
        public const float DyingFrameTime = 0.07f;
        private static readonly float[] DyingHeight = { 0.7f, 0.45f, 0.25f };

        // ---- Embers flicking off ----
        public static readonly float[] EmberTimes = { 0.08f, 0.12f, 0.14f, 0.14f, 0.12f };

        // ---- Char marks left on the floor ----
        // Four shapes in each size, squashed into the floor's perspective; frames hot (a glowing
        // cream and yellow heart), cooling (amber and orange), warm (a few embers), cold (a last
        // ember or two). Over the game's own burn mark they're just the glow; without it they
        // carry their own soot.
        public const int CharVariants = 4;
        private static readonly float[] CharRadiusX = { 6f, 8f, 10f };
        private static readonly float[] CharRadiusY = { 3.5f, 4.5f, 5.5f };

        // ---- The ring of fire round the wizard ----
        public const int RingFrames = 4;
        public const float RingFrameTime = 0.09f;

        // ---- The ignition burst at the cast ----
        public const float BurstFrameTime = 0.05f;
        private static readonly float[] BurstRadius = { 0.2f, 0.42f, 0.62f, 0.8f, 0.93f, 1f };
        private static readonly float[] BurstThickness = { 4f, 3.5f, 3f, 2.5f, 1.8f, 1.2f };
        private static readonly int[] BurstLick = { 2, 4, 5, 4, 3, 1 };   // flame licks rising off the ring, in pixels

        // ---- The flare-up when it burns out ----
        // A column of fire leaping up round the wizard, then breaking into scattered flecks of
        // flame (as the game's meteor flames do as they burn out).
        public static readonly float[] FlareTimes = { 0.05f, 0.05f, 0.07f, 0.07f, 0.07f, 0.07f, 0.12f };
        private static readonly float[] FlareHeight = { 0.35f, 0.75f, 1f, 0.9f, 0.6f, 0.3f };
        private static readonly int[] FlareHeat = { 3, 3, 3, 3, 2, 1 };
        private const int FlareWidth = 13, FlareTall = 32;

        private static float ppu = -1f;
        private static Sprite[][][] tongues;      // [tier][lean + 1]
        private static Sprite[][] dying;          // [tier]
        private static Sprite[][][] glows, soots; // [tier][variant]
        private static Sprite[] embers, flare;
        private static readonly Dictionary<int, Sprite[]> rings = new Dictionary<int, Sprite[]>();
        private static readonly Dictionary<int, Sprite[]> bursts = new Dictionary<int, Sprite[]>();

        // The pixel size everything is drawn at (a change throws the cached art away).
        public static void Use(float pixelsPerUnit)
        {
            if (Mathf.Approximately(ppu, pixelsPerUnit))
                return;
            ppu = pixelsPerUnit;
            tongues = null;
            dying = null;
            glows = soots = null;
            embers = flare = null;
            rings.Clear();
            bursts.Clear();
        }

        public static float Ppu => ppu > 0f ? ppu : 16f;

        public static int Pixels(float units, int min, int max) => Mathf.Clamp(Mathf.RoundToInt(units * Ppu), min, max);

        // ---- Flame tongues ----

        // lean: -1 left, 0 upright, 1 right.
        public static Sprite[] Tongue(int tier, int lean)
        {
            if (tongues == null || !PixelCanvas.Alive(tongues[0][0]))
            {
                tongues = new Sprite[Tiers][][];
                for (int t = 0; t < Tiers; t++)
                {
                    tongues[t] = new Sprite[3][];
                    for (int l = -1; l <= 1; l++)
                    {
                        var frames = new Sprite[TongueFrames];
                        for (int k = 0; k < TongueFrames; k++)
                        {
                            PixelCanvas c = TongueCanvas(t);
                            Flame(c, BaseX(c), 1, TongueWidth[t], TongueHeight[t], l, k / (float)TongueFrames, 3, k == 2 || k == 3, t * 7 + k);
                            frames[k] = c.ToSprite(Ppu, BasePivot(c));
                        }
                        tongues[t][l + 1] = frames;
                    }
                }
            }
            return tongues[Mathf.Clamp(tier, 0, Tiers - 1)][Mathf.Clamp(lean, -1, 1) + 1];
        }

        public static int TongueCount => Tiers * 3 * TongueFrames;

        public static Sprite[] Dying(int tier)
        {
            if (dying == null || !PixelCanvas.Alive(dying[0]))
            {
                dying = new Sprite[Tiers][];
                for (int t = 0; t < Tiers; t++)
                {
                    var frames = new Sprite[DyingHeight.Length + 1];
                    for (int k = 0; k < DyingHeight.Length; k++)
                    {
                        PixelCanvas c = TongueCanvas(t);
                        Flame(c, BaseX(c), 1, TongueWidth[t] * (1f - 0.15f * k), TongueHeight[t] * DyingHeight[k], 0, k * 0.3f, 2 - k, false, 40 + k);
                        frames[k] = c.ToSprite(Ppu, BasePivot(c));
                    }
                    // The last embers, a fleck drifting up off them.
                    PixelCanvas e = TongueCanvas(t);
                    int x = BaseX(e);
                    e.Set(x, 1, Orange);
                    e.Set(x - 1, 1, Ember);
                    e.Set(x + 1, 2, Amber);
                    e.Set(x - 1, 4, Orange);
                    frames[DyingHeight.Length] = e.ToSprite(Ppu, BasePivot(e));
                    dying[t] = frames;
                }
            }
            return dying[Mathf.Clamp(tier, 0, Tiers - 1)];
        }

        private static PixelCanvas TongueCanvas(int tier) => new PixelCanvas(TongueWidth[tier] + 10, TongueHeight[tier] + 5);

        private static int BaseX(PixelCanvas c) => (c.Width - 1) / 2;

        // Pivoted at the flame's base, so it stands where it's put.
        private static Vector2 BasePivot(PixelCanvas c) => new Vector2((BaseX(c) + 0.5f) / c.Width, 1.5f / c.Height);

        // A flame tongue standing on (baseX, baseY): round at the bottom, pinching to a point,
        // leaning (-1 to 1) and swaying with the flicker (phase 0 to 1 round it), its edges
        // ragged and streaks of the hotter colours running up it. heat 3 has a cream core; lower
        // heats lose the hottest colours. wisp: a lick breaking off the tip. seed: its raggedness.
        private static void Flame(PixelCanvas c, int baseX, int baseY, float width, float height, float lean, float phase, int heat, bool wisp, int seed)
        {
            float half = width / 2f;
            float tall = height * (0.86f + 0.14f * Mathf.Sin(phase * Tau));
            int rows = Mathf.Max(1, Mathf.CeilToInt(tall));
            float tipX = baseX;
            for (int y = 0; y < rows; y++)
            {
                float u = (y + 0.5f) / tall;               // 0 at the base, 1 at the tip
                float w = half * Mathf.Min(1f, 0.55f + u * 3f) * Mathf.Pow(Mathf.Max(0f, 1f - u), 0.75f);
                float centre = baseX + lean * u * u * LeanPixels + Mathf.Sin(phase * Tau + u * 4f) * u * SwayPixels;
                tipX = centre;
                // The edges lick in and out a little, each side on its own.
                float left = w + (PixelCanvas.Hash(y, 1, seed) - 0.5f) * 2f * RaggedPixels * u;
                float right = w + (PixelCanvas.Hash(y, 2, seed) - 0.5f) * 2f * RaggedPixels * u;
                for (int x = Mathf.FloorToInt(centre - left - 1f); x <= Mathf.CeilToInt(centre + right + 1f); x++)
                {
                    float dx = x - centre;
                    float edge = dx < 0f ? left : right;
                    if (Mathf.Abs(dx) > edge && !(y == rows - 1 && Mathf.Abs(dx) < 0.5f))
                        continue;
                    float inner = 1f - Mathf.Abs(dx) / Mathf.Max(edge, 0.5f);
                    // Streaks: some columns run a shade hotter up the flame.
                    if (PixelCanvas.Hash(x, 7, seed) > 0.72f)
                        inner += 0.18f;
                    c.Set(x, baseY + y, FlameColor(inner, u, heat));
                }
            }
            if (wisp)
            {
                int wx = Mathf.RoundToInt(tipX + lean * 0.8f), wy = baseY + rows + 1;
                c.Set(wx, wy, heat >= 3 ? Yellow : Amber);
                c.Set(wx, wy + 1, Amber);
            }
        }

        // Hottest low down in the middle; the outer edge orange, darkening to ember at the base.
        private static Color32 FlameColor(float inner, float u, int heat)
        {
            float hot = inner * (1.1f - u * 0.6f) + (heat - 3) * 0.18f;
            if (hot > 0.78f)
                return Cream;
            if (hot > 0.58f)
                return Yellow;
            if (hot > 0.36f)
                return Amber;
            if (hot > 0.1f || u > 0.3f)
                return Orange;
            return Ember;
        }

        // ---- Embers ----

        public static Sprite[] Embers()
        {
            if (PixelCanvas.Alive(embers))
                return embers;
            // Each 5 by 5: c cream, y yellow, a amber, o orange, e ember.
            string[][] art =
            {
                new[] { ".....", "..y..", ".ycy.", "..y..", "....." },
                new[] { ".....", "..a..", ".aya.", "..a..", "....." },
                new[] { ".....", ".....", "..ao.", ".....", "....." },
                new[] { ".....", ".....", "..o..", ".....", "....." },
                new[] { ".....", ".....", "..e..", ".....", "....." },
            };
            var colors = new Dictionary<char, Color32>
            {
                { 'c', Cream }, { 'y', Yellow }, { 'a', Amber }, { 'o', Orange }, { 'e', Ember }
            };
            embers = new Sprite[art.Length];
            for (int k = 0; k < art.Length; k++)
            {
                var c = new PixelCanvas(5, 5);
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 5; col++)
                        if (colors.ContainsKey(art[k][row][col]))
                            c.Set(col, 4 - row, colors[art[k][row][col]]);
                embers[k] = c.ToSprite(Ppu, Middle);
            }
            return embers;
        }

        // ---- Char marks ----

        // The glow alone (to lie over the game's burn mark).
        public static Sprite[] CharGlow(int tier, int variant) => Char(ref glows, false, tier, variant);

        // With its own soot (when the game's burn marks can't be had).
        public static Sprite[] CharMark(int tier, int variant) => Char(ref soots, true, tier, variant);

        public static int CharCount => Tiers * CharVariants * 4;

        private static Sprite[] Char(ref Sprite[][][] cache, bool soot, int tier, int variant)
        {
            if (cache == null || !PixelCanvas.Alive(cache[0][0]))
            {
                cache = new Sprite[Tiers][][];
                for (int t = 0; t < Tiers; t++)
                {
                    cache[t] = new Sprite[CharVariants][];
                    for (int v = 0; v < CharVariants; v++)
                        cache[t][v] = MakeChar(CharRadiusX[t], CharRadiusY[t], 300 + t * 10 + v, soot);
                }
            }
            return cache[Mathf.Clamp(tier, 0, Tiers - 1)][Mathf.Abs(variant) % CharVariants];
        }

        private static Sprite[] MakeChar(float rx, float ry, int seed, bool soot)
        {
            var rng = new System.Random(seed);
            float phase1 = (float)rng.NextDouble() * Tau, phase2 = (float)rng.NextDouble() * Tau;
            int w = 2 * Mathf.CeilToInt(rx * 1.45f) + 5, h = 2 * Mathf.CeilToInt(ry * 1.45f) + 5;
            var frames = new Sprite[4];
            for (int heat = 3; heat >= 0; heat--)
            {
                var c = new PixelCanvas(w, h);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float dx = (x - c.CenterX) / rx, dy = (y - c.CenterY) / ry;
                        float q = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Atan2(dy, dx);
                        // A splat: a ragged blob with spikes shooting out of it, as the game's burn marks.
                        float body = 1f + 0.15f * Mathf.Sin(3f * a + phase1);
                        float spikes = Mathf.Max(0f, Mathf.Sin(9f * a + phase2) * Mathf.Sin(5f * a + phase1));
                        float n = PixelCanvas.Hash(x, y, seed);
                        if (soot)
                        {
                            if (q <= body * 0.85f)
                                c.Set(x, y, Soot);
                            else if (q <= body + 0.4f * spikes)
                                c.Set(x, y, SootEdge);
                            else if (q <= body * 1.15f + 0.4f * spikes && n < 0.5f)
                                c.Set(x, y, SootHaze);
                        }
                        Color32? hot = Heat(heat, q / body, n);
                        if (hot.HasValue)
                            c.Set(x, y, hot.Value);
                    }
                }
                frames[3 - heat] = c.ToSprite(Ppu, Middle);
            }
            return frames;
        }

        // The glow left in a char mark at a heat (3 hot to 0 cold), by how far out the pixel is
        // (0 centre to 1 edge) and its noise.
        private static Color32? Heat(int heat, float q, float noise)
        {
            if (q > 0.62f)
                return null;
            switch (heat)
            {
                case 3:
                    if (noise < 0.55f)
                        return q < 0.25f ? Cream : q < 0.42f ? Yellow : Amber;
                    return null;
                case 2:
                    if (noise < 0.4f)
                        return q < 0.3f ? Yellow : noise < 0.2f ? Amber : Orange;
                    return null;
                case 1:
                    return noise < 0.16f ? (Color32?)(q < 0.35f ? Amber : Orange) : null;
                default:
                    return noise < 0.05f ? (Color32?)Ember : null;
            }
        }

        // ---- The ring of fire ----

        // r: the ring's radius in pixels (the ring's hit circle).
        public static Sprite[] FireRing(int r)
        {
            Sprite[] frames;
            if (rings.TryGetValue(r, out frames) && PixelCanvas.Alive(frames))
                return frames;
            frames = new Sprite[RingFrames];
            int n = 2 * r + 11;
            int licks = Mathf.Max(10, Mathf.RoundToInt(r * 0.9f));
            for (int k = 0; k < RingFrames; k++)
            {
                var c = new PixelCanvas(n, n);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        if (Mathf.Abs(c.Distance(x, y) - r) >= 0.6f)
                            continue;
                        // The far side deeper orange, the near side amber, with brighter flecks running round it.
                        bool far = y > c.CenterY;
                        bool fleck = PixelCanvas.Frac(c.Angle(x, y) / Tau * r * 0.5f + k * 0.25f) < 0.3f;
                        c.Set(x, y, fleck ? Yellow : far ? Orange : Amber);
                    }
                }
                // Flames licking up off the ring, creeping round it and changing height each frame.
                for (int i = 0; i < licks; i++)
                {
                    float a = (i + k / (float)RingFrames) * Tau / licks;
                    int bx = Mathf.RoundToInt(c.CenterX + Mathf.Cos(a) * r), by = Mathf.RoundToInt(c.CenterY + Mathf.Sin(a) * r);
                    int tall = 1 + (i + k) % 3;
                    for (int s = 1; s <= tall; s++)
                        c.Set(bx, by + s, s == tall ? (tall == 3 ? Cream : Yellow) : Amber);
                }
                frames[k] = c.ToSprite(Ppu, Middle);
            }
            rings[r] = frames;
            return frames;
        }

        // ---- The ignition burst ----

        public static Sprite[] IgniteBurst(int r)
        {
            Sprite[] frames;
            if (bursts.TryGetValue(r, out frames) && PixelCanvas.Alive(frames))
                return frames;
            frames = new Sprite[BurstRadius.Length];
            int n = 2 * (r + 7) + 3;
            Color32[] lead = { Cream, Cream, Yellow, Amber, Orange, Orange };
            Color32[] trail = { Yellow, Yellow, Amber, Orange, Orange, Ember };
            float phase = r * 0.41f;
            for (int k = 0; k < frames.Length; k++)
            {
                var c = new PixelCanvas(n, n);
                float rk = Mathf.Max(2f, r * BurstRadius[k]), half = BurstThickness[k] / 2f;
                bool breaking = k >= 3;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float d = c.Distance(x, y);
                        if (k <= 1 && d <= 3f - k)
                            c.Set(x, y, Cream);
                        if (Mathf.Abs(d - rk) > half)
                            continue;
                        if (breaking && Wobble(c.Angle(x, y), phase) > 1.15f - 0.2f * k)
                            continue;
                        c.Set(x, y, d >= rk ? lead[k] : trail[k]);
                    }
                }
                // Flames licking up off the ring.
                const int licks = 12;
                for (int i = 0; i < licks; i++)
                {
                    float a = i * Tau / licks + phase;
                    if (breaking && Wobble(a, phase) > 1.15f - 0.2f * k)
                        continue;
                    int bx = Mathf.RoundToInt(c.CenterX + Mathf.Cos(a) * rk), by = Mathf.RoundToInt(c.CenterY + Mathf.Sin(a) * rk);
                    int tall = Mathf.Max(1, BurstLick[k] - i % 2);
                    for (int s = 1; s <= tall; s++)
                        c.Set(bx, by + s, s == tall ? (k < 2 ? Cream : Yellow) : s > tall / 2 ? Amber : lead[k]);
                }
                frames[k] = c.ToSprite(Ppu, Middle);
            }
            bursts[r] = frames;
            return frames;
        }

        // Smooth noise round a circle, 0 to 1 (where a ring breaks up).
        private static float Wobble(float angle, float phase) =>
            0.5f + 0.5f * Mathf.Sin(7f * angle + phase) * Mathf.Cos(3f * angle + 2f * phase);

        // ---- The flare-up ----

        public static Sprite[] FlareUp()
        {
            if (PixelCanvas.Alive(flare))
                return flare;
            int w = FlareWidth + 8, h = FlareTall + 6, mid = (w - 1) / 2;
            var pivot = new Vector2((mid + 0.5f) / w, 1.5f / h);
            flare = new Sprite[FlareHeight.Length + 1];
            for (int k = 0; k < FlareHeight.Length; k++)
            {
                var c = new PixelCanvas(w, h);
                Flame(c, mid, 1, FlareWidth * (k >= 4 ? 0.85f : 1f), FlareTall * FlareHeight[k], 0f, k / 6f, FlareHeat[k], k == 2 || k == 3, 60 + k);
                // Two side tongues leaping with it at its height.
                if (k >= 1 && k <= 4)
                {
                    Flame(c, mid - 4, 1, 5f, FlareTall * FlareHeight[k] * 0.55f, -1f, k / 6f + 0.3f, FlareHeat[k] - 1, false, 70 + k);
                    Flame(c, mid + 4, 1, 5f, FlareTall * FlareHeight[k] * 0.5f, 1f, k / 6f + 0.6f, FlareHeat[k] - 1, false, 80 + k);
                }
                flare[k] = c.ToSprite(Ppu, pivot);
            }
            // Burnt out into flecks of flame flying apart.
            var s = new PixelCanvas(w, h);
            var rng = new System.Random(90);
            for (int i = 0; i < 12; i++)
            {
                int x = mid + rng.Next(-8, 9), y = 1 + rng.Next(0, 14);
                Color32 fleck = i % 3 == 0 ? Amber : i % 3 == 1 ? Orange : Ember;
                s.Set(x, y, fleck);
                if (i % 4 == 0)
                    s.Set(x, y + 1, fleck);
            }
            flare[FlareHeight.Length] = s.ToSprite(Ppu, pivot);
            return flare;
        }
    }
}
