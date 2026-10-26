using System.Collections.Generic;
using UnityEngine;

namespace WoLTrailblazer
{
    // The pixel art for Trailblazer's drawn fire (BlazeLook), drawn in code once and kept. One fire
    // palette, dark to light (a deep ember outline, red, orange, amber, yellow and a near-white for
    // the hottest cores), soot for the char marks and greys for the last puff of smoke. Flames are
    // outlined, hottest low down in the middle and cooling to red at their edges; every animation
    // is a handful of hand-timed frames.
    internal static class FireArt
    {
        internal static readonly Color32 Char = new Color32(72, 16, 24, 255);
        internal static readonly Color32 Red = new Color32(182, 40, 32, 255);
        internal static readonly Color32 Orange = new Color32(238, 110, 36, 255);
        internal static readonly Color32 Amber = new Color32(252, 172, 54, 255);
        internal static readonly Color32 Yellow = new Color32(255, 228, 112, 255);
        internal static readonly Color32 White = new Color32(255, 250, 222, 255);
        internal static readonly Color32 Soot0 = new Color32(28, 22, 24, 255);
        internal static readonly Color32 Soot1 = new Color32(50, 38, 36, 255);
        internal static readonly Color32 Soot2 = new Color32(78, 60, 52, 255);
        internal static readonly Color32 Smoke0 = new Color32(40, 36, 42, 255);
        internal static readonly Color32 Smoke1 = new Color32(70, 64, 72, 255);
        internal static readonly Color32 Smoke2 = new Color32(108, 100, 108, 255);
        internal static readonly Color32 Smoke3 = new Color32(146, 138, 142, 255);

        private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);
        private const float Tau = PixelCanvas.Tau;

        // ---- Flame tongues: on the wizard and on the trail ----
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

        // The dying flame when a trail patch goes out: shrinking and cooling to an ember and smoke.
        public const float DyingFrameTime = 0.07f;
        private static readonly float[] DyingHeight = { 0.7f, 0.45f, 0.25f };

        // ---- Embers flicking off ----
        public static readonly float[] EmberTimes = { 0.08f, 0.12f, 0.14f, 0.14f, 0.12f };

        // ---- Char marks left on the floor ----
        // Four soot shapes in each size, squashed into the floor's perspective; frames hot (glowing
        // yellow and amber in the middle), cooling (orange and red), warm (a few red specks), cold.
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
        // A column of fire leaping up round the wizard, then sinking into a puff of smoke.
        public static readonly float[] FlareTimes = { 0.05f, 0.05f, 0.07f, 0.07f, 0.07f, 0.07f, 0.14f };
        private static readonly float[] FlareHeight = { 0.35f, 0.75f, 1f, 0.9f, 0.6f, 0.3f };
        private static readonly int[] FlareHeat = { 3, 3, 3, 3, 2, 1 };
        private const int FlareWidth = 13, FlareTall = 32;

        private static float ppu = -1f;
        private static Sprite[][][] tongues;      // [tier][lean + 1]
        private static Sprite[][] dying;          // [tier]
        private static Sprite[][][] chars;        // [tier][variant]
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
            chars = null;
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
                            Flame(c, BaseX(c), 1, TongueWidth[t], TongueHeight[t], l, k / (float)TongueFrames, 3, k == 2 || k == 3);
                            c.Outline(Char);
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
                        Flame(c, BaseX(c), 1, TongueWidth[t] * (1f - 0.15f * k), TongueHeight[t] * DyingHeight[k], 0, k * 0.3f, 2 - k, false);
                        c.Outline(Char);
                        frames[k] = c.ToSprite(Ppu, BasePivot(c));
                    }
                    // The last ember, with a wisp of smoke over it.
                    PixelCanvas e = TongueCanvas(t);
                    int x = BaseX(e);
                    e.Set(x, 1, Orange);
                    e.Set(x - 1, 1, Red);
                    e.Set(x + 1, 1, Red);
                    e.Set(x, 3, Smoke2);
                    e.Set(x + 1, 4, Smoke1);
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
        // leaning (-1 to 1) and swaying with the flicker (phase 0 to 1 round it). heat 3 has a
        // near-white core; lower heats lose the hottest colours. wisp: a lick breaking off the tip.
        private static void Flame(PixelCanvas c, int baseX, int baseY, float width, float height, float lean, float phase, int heat, bool wisp)
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
                for (int x = Mathf.FloorToInt(centre - w - 1f); x <= Mathf.CeilToInt(centre + w + 1f); x++)
                {
                    float dx = Mathf.Abs(x - centre);
                    if (dx > w && !(y == rows - 1 && dx < 0.5f))
                        continue;
                    float inner = 1f - dx / Mathf.Max(w, 0.5f);
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

        // Hottest low down in the middle.
        private static Color32 FlameColor(float inner, float u, int heat)
        {
            float hot = inner * (1.1f - u * 0.6f) + (heat - 3) * 0.18f;
            if (hot > 0.78f)
                return White;
            if (hot > 0.6f)
                return Yellow;
            if (hot > 0.4f)
                return Amber;
            if (hot > 0.2f)
                return Orange;
            return Red;
        }

        // ---- Embers ----

        public static Sprite[] Embers()
        {
            if (PixelCanvas.Alive(embers))
                return embers;
            // Each 5 by 5: y bright yellow, w white, a amber, o orange, r red, s ash.
            string[][] art =
            {
                new[] { ".....", "..y..", ".ywy.", "..y..", "....." },
                new[] { ".....", "..a..", ".aya.", "..a..", "....." },
                new[] { ".....", "..r..", ".roo.", "..r..", "....." },
                new[] { ".....", ".....", "..r..", ".....", "....." },
                new[] { ".....", ".....", "..s..", ".....", "....." },
            };
            var colors = new Dictionary<char, Color32>
            {
                { 'w', White }, { 'y', Yellow }, { 'a', Amber }, { 'o', Orange }, { 'r', Red }, { 's', Smoke2 }
            };
            embers = new Sprite[art.Length];
            for (int k = 0; k < art.Length; k++)
            {
                var c = new PixelCanvas(5, 5);
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 5; col++)
                        if (colors.ContainsKey(art[k][row][col]))
                            c.Set(col, 4 - row, colors[art[k][row][col]]);
                if (k == 0)
                    c.Outline(Amber);
                embers[k] = c.ToSprite(Ppu, Middle);
            }
            return embers;
        }

        // ---- Char marks ----

        public static Sprite[] CharMark(int tier, int variant)
        {
            if (chars == null || !PixelCanvas.Alive(chars[0][0]))
            {
                chars = new Sprite[Tiers][][];
                for (int t = 0; t < Tiers; t++)
                {
                    chars[t] = new Sprite[CharVariants][];
                    for (int v = 0; v < CharVariants; v++)
                        chars[t][v] = MakeChar(CharRadiusX[t], CharRadiusY[t], 300 + t * 10 + v);
                }
            }
            return chars[Mathf.Clamp(tier, 0, Tiers - 1)][Mathf.Abs(variant) % CharVariants];
        }

        public static int CharCount => Tiers * CharVariants * 4;

        private static Sprite[] MakeChar(float rx, float ry, int seed)
        {
            var rng = new System.Random(seed);
            float phase1 = (float)rng.NextDouble() * Tau, phase2 = (float)rng.NextDouble() * Tau;
            int w = 2 * Mathf.CeilToInt(rx * 1.2f) + 5, h = 2 * Mathf.CeilToInt(ry * 1.2f) + 5;
            var frames = new Sprite[4];
            for (int heat = 3; heat >= 0; heat--)
            {
                var c = new PixelCanvas(w, h);
                var glow = new List<int>();
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float dx = (x - c.CenterX) / rx, dy = (y - c.CenterY) / ry;
                        float q = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Atan2(dy, dx);
                        float edge = 1f + 0.2f * (0.6f * Mathf.Sin(3f * a + phase1) + 0.4f * Mathf.Sin(5f * a + phase2));
                        if (q > edge)
                            continue;
                        // Soot, its far rim lit.
                        c.Set(x, y, q > edge - 0.28f && dy > 0f && dx < 0.3f ? Soot2 : Soot1);
                        float n = PixelCanvas.Hash(x, y, seed);
                        Color32? hot = Heat(heat, q / edge, n);
                        if (hot.HasValue)
                            c.Set(x, y, hot.Value);
                    }
                }
                c.Outline(Soot0);
                frames[3 - heat] = c.ToSprite(Ppu, Middle);
            }
            return frames;
        }

        // The glow left in a char mark at a heat (3 hot to 0 cold), by how far out the pixel is
        // (0 centre to 1 edge) and its noise.
        private static Color32? Heat(int heat, float q, float noise)
        {
            if (q > 0.62f)
                return heat == 0 && noise < 0.06f ? (Color32?)Soot2 : null;
            switch (heat)
            {
                case 3:
                    if (noise < 0.6f)
                        return q < 0.25f ? Yellow : q < 0.45f ? Amber : Orange;
                    return null;
                case 2:
                    if (noise < 0.42f)
                        return q < 0.3f ? Amber : noise < 0.2f ? Orange : Red;
                    return null;
                case 1:
                    return noise < 0.18f ? (Color32?)Red : null;
                default:
                    return noise < 0.1f ? (Color32?)Soot2 : null;
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
                        // The far side darker, the near side brighter, with embers running round it.
                        bool far = y > c.CenterY;
                        bool glint = PixelCanvas.Frac(c.Angle(x, y) / Tau * r * 0.5f + k * 0.25f) < 0.3f;
                        c.Set(x, y, glint ? Amber : far ? Red : Orange);
                    }
                }
                // Flames licking up off the ring, creeping round it and changing height each frame.
                for (int i = 0; i < licks; i++)
                {
                    float a = (i + k / (float)RingFrames) * Tau / licks;
                    int bx = Mathf.RoundToInt(c.CenterX + Mathf.Cos(a) * r), by = Mathf.RoundToInt(c.CenterY + Mathf.Sin(a) * r);
                    int tall = 1 + (i + k) % 3;
                    for (int s = 1; s <= tall; s++)
                        c.Set(bx, by + s, s == tall ? (tall == 3 ? White : Yellow) : Amber);
                }
                c.Outline(Char);
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
            Color32[] lead = { White, Yellow, Amber, Orange, Red, Red };
            Color32[] trail = { Yellow, Amber, Orange, Red, Red, Char };
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
                            c.Set(x, y, k == 0 ? White : Yellow);
                        if (Mathf.Abs(d - rk) > half)
                            continue;
                        if (breaking && Wobble(c.Angle(x, y), phase) > 1.15f - 0.2f * k)
                            continue;
                        c.Set(x, y, d >= rk ? lead[k] : trail[k]);
                    }
                }
                // Flames licking up off the ring.
                int licks = 12;
                for (int i = 0; i < licks; i++)
                {
                    float a = i * Tau / licks + phase;
                    if (breaking && Wobble(a, phase) > 1.15f - 0.2f * k)
                        continue;
                    int bx = Mathf.RoundToInt(c.CenterX + Mathf.Cos(a) * rk), by = Mathf.RoundToInt(c.CenterY + Mathf.Sin(a) * rk);
                    int tall = Mathf.Max(1, BurstLick[k] - i % 2);
                    for (int s = 1; s <= tall; s++)
                        c.Set(bx, by + s, s == tall ? (k < 2 ? White : Yellow) : s > tall / 2 ? Amber : lead[k]);
                }
                c.Outline(Char);
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
            int w = FlareWidth + 8, h = FlareTall + 6;
            flare = new Sprite[FlareHeight.Length + 1];
            for (int k = 0; k < FlareHeight.Length; k++)
            {
                var c = new PixelCanvas(w, h);
                Flame(c, (w - 1) / 2, 1, FlareWidth * (k >= 4 ? 0.85f : 1f), FlareTall * FlareHeight[k], 0f, k / 6f, FlareHeat[k], k == 2 || k == 3);
                // Two side tongues leaping with it at its height.
                if (k >= 1 && k <= 4)
                {
                    Flame(c, (w - 1) / 2 - 4, 1, 5f, FlareTall * FlareHeight[k] * 0.55f, -1f, k / 6f + 0.3f, FlareHeat[k] - 1, false);
                    Flame(c, (w - 1) / 2 + 4, 1, 5f, FlareTall * FlareHeight[k] * 0.5f, 1f, k / 6f + 0.6f, FlareHeat[k] - 1, false);
                }
                c.Outline(Char);
                flare[k] = c.ToSprite(Ppu, new Vector2(((w - 1) / 2 + 0.5f) / w, 1.5f / h));
            }
            // The puff of smoke it sinks into, with the last embers under it.
            var s = new PixelCanvas(w, h);
            int mid = (w - 1) / 2;
            Vector3[] puffs =
            {
                new Vector3(mid - 5, 7, 3.4f), new Vector3(mid, 9, 4.2f), new Vector3(mid + 5, 7, 3.4f),
                new Vector3(mid - 2, 13, 3.2f), new Vector3(mid + 3, 13, 3f)
            };
            // Each puff lit on its top, so the smoke reads as a cluster of round billows.
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float lit = PuffLight(x, y, puffs);
                    if (lit < -1.5f)
                        continue;
                    s.Set(x, y, lit > 0.45f ? Smoke3 : lit > -0.2f ? Smoke2 : Smoke1);
                }
            }
            s.Outline(Smoke0);
            s.Set((w - 1) / 2 - 2, 1, Red);
            s.Set((w - 1) / 2 + 1, 1, Orange);
            s.Set((w - 1) / 2 + 3, 2, Red);
            flare[FlareHeight.Length] = s.ToSprite(Ppu, new Vector2(((w - 1) / 2 + 0.5f) / w, 1.5f / h));
            return flare;
        }

        // How high up its puff a pixel sits (-1 bottom to 1 top), taking the highest of the puffs
        // it's in; -2 when it's in none.
        private static float PuffLight(int x, int y, Vector3[] puffs)
        {
            float best = -2f;
            foreach (Vector3 p in puffs)
            {
                float dx = x - p.x, dy = y - p.y;
                if (dx * dx + dy * dy <= p.z * p.z)
                    best = Mathf.Max(best, dy / p.z);
            }
            return best;
        }
    }
}
