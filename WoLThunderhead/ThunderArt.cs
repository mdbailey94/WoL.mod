using System.Collections.Generic;
using UnityEngine;

namespace WoLThunderhead
{
    // The pixel art for the drawn lightning effects (ThunderVisuals), drawn in code once and kept.
    // One lightning palette for all of it, dark to light (an inky outline, deep blue, blue, cyan,
    // a pale yellow and white for the hottest cores), and three soot greys for the scorch marks.
    // Every frame is outlined, lit from above (the top half of a ring a shade lighter) and has
    // its brightest colour at the core; animations are a handful of hand-timed frames.
    internal static class ThunderArt
    {
        internal static readonly Color32 Ink = new Color32(26, 20, 56, 255);
        internal static readonly Color32 Deep = new Color32(52, 58, 150, 255);
        internal static readonly Color32 Blue = new Color32(84, 140, 232, 255);
        internal static readonly Color32 Cyan = new Color32(150, 222, 255, 255);
        internal static readonly Color32 Pale = new Color32(255, 242, 160, 255);
        internal static readonly Color32 White = new Color32(255, 255, 255, 255);
        internal static readonly Color32 Soot0 = new Color32(22, 20, 28, 255);
        internal static readonly Color32 Soot1 = new Color32(40, 37, 50, 255);
        internal static readonly Color32 Soot2 = new Color32(64, 60, 78, 255);

        private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);
        private const float Tau = PixelCanvas.Tau;

        // ---- Strike glyph: the mark where a bolt is about to land ----
        // A dashed ring the size of the strike's hit circle, turning, with four ticks pointing in
        // and a small bolt rune at the centre; an inner ring closes in on the centre as the strike
        // nears and everything heats up from blue to white. The last frame is the flash it
        // flickers with just before the bolt lands.
        public const int GlyphFrames = 6;                 // the charge-up (then the flash frame)
        public const int GlyphFlashFrame = GlyphFrames;
        private const int GlyphDashes = 10;
        private const float GlyphDashOn = 0.62f;          // the share of each dash that's drawn
        private const float GlyphDashTurn = 0.08f;        // how far the dashes turn each frame (of a dash)

        // ---- Strike scorch: the impact and the mark it leaves ----
        // A white starburst, a ring of cyan breaking out over fresh soot, then the soot with its
        // cracks still glowing, cooling from cyan to blue to dead black.
        public static readonly float[] ScorchTimes = { 0.05f, 0.06f, 0.15f, 0.25f, 0.4f, 60f };
        private const int ScorchVariants = 3;
        private const float ScorchShare = 0.6f;           // the soot's radius, of the strike's

        // ---- Arcs crackling round the wizard ----
        // A jagged bolt reaching out (half), at full length with a branch, cooling, breaking up,
        // and a few last sparks. Drawn in eight directions so nothing has to be rotated.
        public const float ArcFrameTime = 0.035f;
        private const int ArcVariants = 8;
        private const int ArcSize = 28;

        // ---- Landing ring: where the slam will land ----
        public const int RingFrames = 6;
        public const int RingFlashFrame = RingFrames;
        private const int RingChevrons = 12;

        // ---- Shockwave: a ring of lightning racing out over the ground ----
        public const float WaveFrameTime = 0.045f;
        private static readonly float[] WaveRadius = { 0.22f, 0.45f, 0.66f, 0.84f, 0.98f, 1.1f };
        private static readonly float[] WaveThickness = { 3.5f, 3.5f, 3f, 2.2f, 1.6f, 1.2f };
        private static readonly float[] WaveKept = { 1f, 1f, 1f, 0.7f, 0.5f, 0.3f }; // the share of the ring left as it breaks up

        // ---- Crater: the slam's big scorch ----
        public static readonly float[] CraterTimes = { 0.12f, 0.3f, 0.5f, 60f };

        // ---- Lightning inside the storm cloud ----
        public const float CloudBoltFrameTime = 0.035f;

        private static float ppu = -1f;
        private static readonly Dictionary<int, Sprite[]> glyphs = new Dictionary<int, Sprite[]>();
        private static readonly Dictionary<int, Sprite[]> rings = new Dictionary<int, Sprite[]>();
        private static readonly Dictionary<int, Sprite[]> waves = new Dictionary<int, Sprite[]>();
        private static readonly Dictionary<int, Sprite[]> craters = new Dictionary<int, Sprite[]>();
        private static readonly Dictionary<int, Sprite[][]> scorches = new Dictionary<int, Sprite[][]>();
        private static Sprite[][] arcs, cloudBolts;

        // The pixel size everything is drawn at (a change throws the cached art away).
        public static void Use(float pixelsPerUnit)
        {
            if (Mathf.Approximately(ppu, pixelsPerUnit))
                return;
            ppu = pixelsPerUnit;
            glyphs.Clear();
            rings.Clear();
            waves.Clear();
            craters.Clear();
            scorches.Clear();
            arcs = cloudBolts = null;
        }

        public static float Ppu => ppu > 0f ? ppu : 16f;

        public static int Pixels(float units, int min, int max) => Mathf.Clamp(Mathf.RoundToInt(units * Ppu), min, max);

        // ---- Strike glyph ----

        public static Sprite[] StrikeGlyph(int r)
        {
            Sprite[] frames;
            if (glyphs.TryGetValue(r, out frames) && PixelCanvas.Alive(frames))
                return frames;
            frames = new Sprite[GlyphFrames + 1];
            int n = 2 * r + 5;
            int cx = (n - 1) / 2;
            Color32[] innerRamp = { Deep, Blue, Blue, Cyan, Pale, White };
            for (int k = 0; k < GlyphFrames; k++)
            {
                float p = k / (float)(GlyphFrames - 1);
                var c = new PixelCanvas(n, n);
                Color32 lit = k >= 4 ? Cyan : Blue, shade = k >= 4 ? Blue : Deep;
                float inner = Mathf.Lerp(r - 2.5f, 1.5f, p);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float d = c.Distance(x, y);
                        if (Mathf.Abs(d - r) < 0.55f)
                        {
                            float dash = PixelCanvas.Frac(c.Angle(x, y) / Tau * GlyphDashes + k * GlyphDashTurn);
                            if (dash < GlyphDashOn)
                                c.Set(x, y, y > c.CenterY ? lit : shade);
                        }
                        else if (Mathf.Abs(d - inner) < 0.55f && r > 6)
                            c.Set(x, y, innerRamp[k]);
                    }
                }
                Color32 tick = k < 3 ? Blue : k < 5 ? Cyan : Pale;
                for (int s = 1; s <= 2; s++)
                {
                    c.Set(cx + r - s, cx, tick);
                    c.Set(cx - r + s, cx, tick);
                    c.Set(cx, cx + r - s, tick);
                    c.Set(cx, cx - r + s, tick);
                }
                Rune(c, cx, k < 2 ? Deep : k < 4 ? Cyan : White);
                c.Outline(Ink);
                frames[k] = c.ToSprite(Ppu, Middle);
            }
            // The flash: a burning disc in a white ring, glowing.
            var f = new PixelCanvas(n, n);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = f.Distance(x, y);
                    if (Mathf.Abs(d - r) < 0.6f)
                        f.Set(x, y, White);
                    else if (d < r * 0.22f)
                        f.Set(x, y, White);
                    else if (d < r * 0.42f)
                        f.Set(x, y, Pale);
                }
            }
            Rune(f, cx, White);
            f.Outline(Cyan);
            f.Outline(Ink);
            frames[GlyphFlashFrame] = f.ToSprite(Ppu, Middle);
            glyphs[r] = frames;
            return frames;
        }

        // A little zigzag bolt, 3 by 5.
        private static void Rune(PixelCanvas c, int cx, Color32 color)
        {
            string[] rows = { "..#", ".#.", "###", ".#.", "#.." };
            for (int row = 0; row < rows.Length; row++)
                for (int col = 0; col < 3; col++)
                    if (rows[row][col] == '#')
                        c.Set(cx - 1 + col, cx + 2 - row, color);
        }

        // ---- Strike scorch ----

        public static Sprite[][] StrikeScorch(int strikeRadius)
        {
            Sprite[][] variants;
            if (scorches.TryGetValue(strikeRadius, out variants) && PixelCanvas.Alive(variants[0]))
                return variants;
            int rs = Mathf.Max(5, Mathf.RoundToInt(strikeRadius * ScorchShare));
            int n = 2 * rs + 7;
            variants = new Sprite[ScorchVariants][];
            for (int v = 0; v < ScorchVariants; v++)
            {
                var rng = new System.Random(1000 + v * 77 + rs);
                float phase1 = (float)rng.NextDouble() * Tau, phase2 = (float)rng.NextDouble() * Tau;
                List<List<Pixel>> cracks = Cracks(n, rs * 0.95f, 5, 1, rng);
                float turn = (float)rng.NextDouble() * Tau;
                var frames = new Sprite[ScorchTimes.Length];

                // The impact: a white starburst.
                var c = new PixelCanvas(n, n);
                int cx = (n - 1) / 2;
                for (int i = 0; i < 8; i++)
                {
                    float a = turn + i * Tau / 8f;
                    float length = (i % 2 == 0 ? rs + 2 : rs - 1);
                    var ray = new List<Pixel>();
                    PixelCanvas.Trace(cx, cx, cx + Mathf.RoundToInt(Mathf.Cos(a) * length), cx + Mathf.RoundToInt(Mathf.Sin(a) * length), ray);
                    for (int j = 0; j < ray.Count; j++)
                        c.Set(ray[j].X, ray[j].Y, j < ray.Count / 2 ? White : j < ray.Count - 1 ? Pale : Cyan);
                }
                Disc(c, 2.5f, White);
                c.Outline(Cyan);
                c.Outline(Deep);
                frames[0] = c.ToSprite(Ppu, Middle);

                // The burst: a broken cyan ring over fresh soot.
                c = new PixelCanvas(n, n);
                Soot(c, rs * 0.75f, phase1, phase2);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float d = c.Distance(x, y);
                        if (Mathf.Abs(d - rs * 0.9f) < 0.7f && Wobble(c.Angle(x, y), phase1) < 0.75f)
                            c.Set(x, y, y > c.CenterY ? Cyan : Blue);
                    }
                }
                for (int i = 0; i < 8; i++)
                {
                    float a = turn + (i + 0.5f) * Tau / 8f;
                    c.Line(cx + Mathf.RoundToInt(Mathf.Cos(a) * rs * 0.35f), cx + Mathf.RoundToInt(Mathf.Sin(a) * rs * 0.35f),
                        cx + Mathf.RoundToInt(Mathf.Cos(a) * rs * 0.65f), cx + Mathf.RoundToInt(Mathf.Sin(a) * rs * 0.65f), Blue);
                }
                Disc(c, 1.8f, Pale);
                frames[1] = c.ToSprite(Ppu, Middle);

                // The mark, its cracks cooling.
                for (int heat = 3; heat >= 0; heat--)
                {
                    c = new PixelCanvas(n, n);
                    Soot(c, rs * 0.75f, phase1, phase2);
                    Crack(c, cracks, heat);
                    frames[2 + (3 - heat)] = c.ToSprite(Ppu, Middle);
                }
                variants[v] = frames;
            }
            scorches[strikeRadius] = variants;
            return variants;
        }

        // A soot blob, lit at its upper-left rim and outlined.
        private static void Soot(PixelCanvas c, float radius, float phase1, float phase2)
        {
            for (int y = 0; y < c.Height; y++)
            {
                for (int x = 0; x < c.Width; x++)
                {
                    float a = c.Angle(x, y);
                    float edge = radius * (1f + 0.2f * (0.6f * Mathf.Sin(3f * a + phase1) + 0.4f * Mathf.Sin(5f * a + phase2)));
                    float d = c.Distance(x, y);
                    if (d > edge)
                        continue;
                    bool rim = d > edge - 1.3f && y > c.CenterY && x < c.CenterX;
                    c.Set(x, y, rim ? Soot2 : Soot1);
                }
            }
            c.Outline(Soot0);
        }

        // Cracks out from the centre: jagged paths, each kept as its pixels in order.
        private static List<List<Pixel>> Cracks(int n, float length, int count, int jag, System.Random rng)
        {
            var cracks = new List<List<Pixel>>();
            int cx = (n - 1) / 2;
            for (int i = 0; i < count; i++)
            {
                float a = i * Tau / count + (float)(rng.NextDouble() - 0.5) * 0.6f;
                float reach = length * (0.7f + 0.35f * (float)rng.NextDouble());
                var path = new List<Pixel>();
                int px = cx, py = cx;
                const int segments = 3;
                for (int s = 1; s <= segments; s++)
                {
                    float along = reach * s / segments;
                    float side = s < segments ? rng.Next(-jag, jag + 1) : 0;
                    int nx = cx + Mathf.RoundToInt(Mathf.Cos(a) * along - Mathf.Sin(a) * side);
                    int ny = cx + Mathf.RoundToInt(Mathf.Sin(a) * along + Mathf.Cos(a) * side);
                    PixelCanvas.Trace(px, py, nx, ny, path);
                    px = nx;
                    py = ny;
                }
                cracks.Add(path);
            }
            return cracks;
        }

        // The cracks at a heat: 3 glowing (pale at the centre, cyan out), 2 cyan and blue, 1 deep
        // blue, 0 dead.
        private static void Crack(PixelCanvas c, List<List<Pixel>> cracks, int heat)
        {
            Color32[] core = { Soot0, Blue, Cyan, Pale };
            Color32[] body = { Soot0, Deep, Blue, Cyan };
            foreach (List<Pixel> crack in cracks)
                for (int i = 0; i < crack.Count; i++)
                    c.Set(crack[i].X, crack[i].Y, i < 2 ? core[heat] : body[heat]);
        }

        private static void Disc(PixelCanvas c, float radius, Color32 color)
        {
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                    if (c.Distance(x, y) <= radius)
                        c.Set(x, y, color);
        }

        // Smooth noise round a circle, 0 to 1 (where a ring breaks up).
        private static float Wobble(float angle, float phase) =>
            0.5f + 0.5f * Mathf.Sin(7f * angle + phase) * Mathf.Cos(3f * angle + 2f * phase);

        // ---- Arcs ----

        public static Sprite[][] Arcs()
        {
            if (arcs != null && PixelCanvas.Alive(arcs[0]))
                return arcs;
            arcs = new Sprite[ArcVariants][];
            int cx = (ArcSize - 1) / 2;
            for (int v = 0; v < ArcVariants; v++)
            {
                var rng = new System.Random(500 + v * 31);
                float a = v * Tau / ArcVariants + (float)(rng.NextDouble() - 0.5) * 0.4f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), side = new Vector2(-dir.y, dir.x);
                // The main bolt, from near the middle out to the edge.
                var main = new List<Pixel>();
                var points = new List<Vector2>();
                const int segments = 5;
                for (int s = 0; s <= segments; s++)
                {
                    float along = 3f + 10.5f * s / segments;
                    float jag = s == 0 || s == segments ? 0f : (float)(rng.NextDouble() * 6.0 - 3.0);
                    points.Add(new Vector2(cx, cx) + dir * along + side * jag);
                }
                for (int s = 1; s < points.Count; s++)
                    PixelCanvas.Trace(Mathf.RoundToInt(points[s - 1].x), Mathf.RoundToInt(points[s - 1].y),
                        Mathf.RoundToInt(points[s].x), Mathf.RoundToInt(points[s].y), main);
                // A branch forking off partway.
                var branch = new List<Pixel>();
                Vector2 from = points[2 + rng.Next(0, 2)];
                float ba = a + (rng.Next(0, 2) == 0 ? 0.75f : -0.75f);
                Vector2 bdir = new Vector2(Mathf.Cos(ba), Mathf.Sin(ba));
                Vector2 mid = from + bdir * 2.5f + new Vector2(-bdir.y, bdir.x) * 1.2f, end = from + bdir * 5.5f;
                PixelCanvas.Trace(Mathf.RoundToInt(from.x), Mathf.RoundToInt(from.y), Mathf.RoundToInt(mid.x), Mathf.RoundToInt(mid.y), branch);
                PixelCanvas.Trace(Mathf.RoundToInt(mid.x), Mathf.RoundToInt(mid.y), Mathf.RoundToInt(end.x), Mathf.RoundToInt(end.y), branch);

                // The bright frames have just a cyan glow (thin and electric); cooling, a blue
                // glow with a deep edge.
                var frames = new Sprite[5];
                // Reaching out.
                var c = new PixelCanvas(ArcSize, ArcSize);
                c.Draw(main.GetRange(0, Mathf.Max(1, main.Count * 55 / 100)), White);
                c.Outline(Cyan);
                frames[0] = c.ToSprite(Ppu, Middle);
                // Full length, with its branch and a bright contact spot.
                c = new PixelCanvas(ArcSize, ArcSize);
                c.Draw(main, White);
                c.Draw(branch, White);
                c.Set(main[0].X, main[0].Y, Pale);
                c.Outline(Cyan);
                frames[1] = c.ToSprite(Ppu, Middle);
                // Cooling.
                c = new PixelCanvas(ArcSize, ArcSize);
                c.Draw(main, Pale);
                c.Draw(branch.GetRange(0, Mathf.Max(1, branch.Count / 2)), Pale);
                c.Outline(Blue);
                frames[2] = c.ToSprite(Ppu, Middle);
                // Breaking up.
                c = new PixelCanvas(ArcSize, ArcSize);
                for (int i = 0; i < main.Count; i++)
                    if ((i / 3) % 2 == 0)
                        c.Set(main[i].X, main[i].Y, Cyan);
                c.Outline(Deep);
                frames[3] = c.ToSprite(Ppu, Middle);
                // Last sparks.
                c = new PixelCanvas(ArcSize, ArcSize);
                Spark(c, main[main.Count - 1]);
                Spark(c, branch[branch.Count - 1]);
                Spark(c, main[main.Count / 2]);
                frames[4] = c.ToSprite(Ppu, Middle);
                arcs[v] = frames;
            }
            return arcs;
        }

        // A one-pixel spark with a little cross of glow.
        private static void Spark(PixelCanvas c, Pixel p)
        {
            c.Set(p.X, p.Y, Pale);
            c.Under(p.X + 1, p.Y, Blue);
            c.Under(p.X - 1, p.Y, Blue);
            c.Under(p.X, p.Y + 1, Blue);
            c.Under(p.X, p.Y - 1, Blue);
        }

        // ---- Landing ring ----

        public static Sprite[] LandingRing(int r)
        {
            Sprite[] frames;
            if (rings.TryGetValue(r, out frames) && PixelCanvas.Alive(frames))
                return frames;
            frames = new Sprite[RingFrames + 1];
            int n = 2 * r + 7;
            Color32[] innerRamp = { Deep, Blue, Blue, Cyan, Cyan, Pale };
            Color32[] chevronRamp = { Blue, Blue, Cyan, Cyan, Pale, White };
            var rng = new System.Random(900 + r);
            var crackleAngles = new float[4];
            for (int i = 0; i < crackleAngles.Length; i++)
                crackleAngles[i] = (float)rng.NextDouble() * Tau;
            for (int k = 0; k <= RingFrames; k++)
            {
                bool flash = k == RingFlashFrame;
                int step = Mathf.Min(k, RingFrames - 1);
                float p = step / (float)(RingFrames - 1);
                var c = new PixelCanvas(n, n);
                float inner = Mathf.Lerp(r * 0.85f, r * 0.3f, p);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float d = c.Distance(x, y), a = c.Angle(x, y);
                        bool top = y > c.CenterY;
                        if (Mathf.Abs(d - r) < (flash ? 1.1f : 0.6f))
                            c.Set(x, y, flash ? White : top ? (k >= 3 ? Cyan : Blue) : (k >= 3 ? Blue : Deep));
                        else if (!flash && Mathf.Abs(d - (r - 2.5f)) < 0.5f && PixelCanvas.Frac(a / Tau * r * 0.7f) < 0.34f)
                            c.Set(x, y, Deep);
                        else if (Mathf.Abs(d - inner) < 0.55f && PixelCanvas.Frac(a / Tau * 16f + k * 0.12f) < 0.6f)
                            c.Set(x, y, flash ? Pale : innerRamp[step]);
                        else if (flash && d < r * 0.5f && x % 4 == 0 && y % 4 == (x % 8 == 0 ? 0 : 2))
                            c.Set(x, y, Pale);   // a sparse scatter of sparks (no gradient)
                    }
                }
                // Chevrons pointing in, turning slowly.
                for (int i = 0; i < RingChevrons; i++)
                {
                    float a = i * Tau / RingChevrons + step * 0.045f;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), side = new Vector2(-dir.y, dir.x);
                    Vector2 centre = new Vector2(c.CenterX, c.CenterY);
                    Vector2 tip = centre + dir * (r - 3.5f), left = centre + dir * (r - 1.5f) + side * 1.6f, right = centre + dir * (r - 1.5f) - side * 1.6f;
                    Color32 chevron = flash ? White : chevronRamp[step];
                    c.Line(Mathf.RoundToInt(tip.x), Mathf.RoundToInt(tip.y), Mathf.RoundToInt(left.x), Mathf.RoundToInt(left.y), chevron);
                    c.Line(Mathf.RoundToInt(tip.x), Mathf.RoundToInt(tip.y), Mathf.RoundToInt(right.x), Mathf.RoundToInt(right.y), chevron);
                }
                // Crackling between the rings once it's charged.
                if (step >= 3)
                {
                    foreach (float a0 in crackleAngles)
                    {
                        float a = a0 + step * 0.9f;
                        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), side = new Vector2(-dir.y, dir.x);
                        Vector2 centre = new Vector2(c.CenterX, c.CenterY);
                        Vector2 p0 = centre + dir * (inner + 1f), p1 = centre + dir * Mathf.Lerp(inner, r, 0.5f) + side * 1.5f, p2 = centre + dir * (r - 1f);
                        Color32 color = flash || step == RingFrames - 1 ? White : Cyan;
                        c.Line(Mathf.RoundToInt(p0.x), Mathf.RoundToInt(p0.y), Mathf.RoundToInt(p1.x), Mathf.RoundToInt(p1.y), color);
                        c.Line(Mathf.RoundToInt(p1.x), Mathf.RoundToInt(p1.y), Mathf.RoundToInt(p2.x), Mathf.RoundToInt(p2.y), color);
                    }
                }
                if (flash)
                    c.Outline(Cyan);
                c.Outline(Ink);
                frames[k] = c.ToSprite(Ppu, Middle);
            }
            rings[r] = frames;
            return frames;
        }

        // ---- Shockwave ----

        public static Sprite[] Shockwave(int r)
        {
            Sprite[] frames;
            if (waves.TryGetValue(r, out frames) && PixelCanvas.Alive(frames))
                return frames;
            frames = new Sprite[WaveRadius.Length];
            int n = 2 * Mathf.CeilToInt(r * WaveRadius[WaveRadius.Length - 1]) + 9;
            float phase = r * 0.37f;
            // Bright leading edge, darker trailing edge, then the outline, cooling frame by frame.
            Color32[] lead = { White, Pale, Cyan, Blue, Deep, Deep };
            Color32[] trail = { Pale, Cyan, Blue, Deep, Deep, Deep };
            Color32[] edge = { Cyan, Blue, Deep, Ink, Ink, Ink };
            Color32[] crackle = { White, Cyan, Blue };
            for (int k = 0; k < frames.Length; k++)
            {
                var c = new PixelCanvas(n, n);
                float rk = Mathf.Max(2f, r * WaveRadius[k]), half = WaveThickness[k] / 2f;
                // Crackles from the centre out to the ring, while it's young.
                if (k < crackle.Length)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Tau / 6f + phase + k * 0.4f;
                        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), side = new Vector2(-dir.y, dir.x);
                        Vector2 centre = new Vector2(c.CenterX, c.CenterY);
                        Vector2 p0 = centre + dir * 2f, p1 = centre + dir * rk * 0.5f + side * ((i % 2 == 0) ? 1.5f : -1.5f), p2 = centre + dir * (rk - half - 1f);
                        c.Line(Mathf.RoundToInt(p0.x), Mathf.RoundToInt(p0.y), Mathf.RoundToInt(p1.x), Mathf.RoundToInt(p1.y), crackle[k]);
                        c.Line(Mathf.RoundToInt(p1.x), Mathf.RoundToInt(p1.y), Mathf.RoundToInt(p2.x), Mathf.RoundToInt(p2.y), crackle[k]);
                    }
                }
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float d = c.Distance(x, y);
                        if (Mathf.Abs(d - rk) > half)
                            continue;
                        if (WaveKept[k] < 1f && Wobble(c.Angle(x, y), phase) > WaveKept[k])
                            continue;
                        c.Set(x, y, d >= rk ? lead[k] : trail[k]);
                    }
                }
                c.Outline(edge[k]);
                frames[k] = c.ToSprite(Ppu, Middle);
            }
            waves[r] = frames;
            return frames;
        }

        // ---- Crater ----

        public static Sprite[] Crater(int r)
        {
            Sprite[] frames;
            if (craters.TryGetValue(r, out frames) && PixelCanvas.Alive(frames))
                return frames;
            frames = new Sprite[CraterTimes.Length];
            int n = 2 * Mathf.CeilToInt(r * 1.4f) + 7;
            var rng = new System.Random(1300 + r);
            float phase1 = (float)rng.NextDouble() * Tau, phase2 = (float)rng.NextDouble() * Tau;
            List<List<Pixel>> cracks = Cracks(n, r * 1.35f, 8, 2, rng);
            for (int heat = 3; heat >= 0; heat--)
            {
                var c = new PixelCanvas(n, n);
                Soot(c, r, phase1, phase2);
                // Rubble round the rim.
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                        if (c.Has(x, y) && PixelCanvas.Hash(x, y, r) < 0.08f)
                            c.Set(x, y, Soot2);
                Crack(c, cracks, heat);
                frames[3 - heat] = c.ToSprite(Ppu, Middle);
            }
            craters[r] = frames;
            return frames;
        }

        // ---- Lightning inside the storm cloud ----
        // Two bolts (one mirrored): a zigzag crawling across the cloud's belly, then forking down
        // out of its underside, then cooling and breaking up.

        public static Sprite[][] CloudBolts()
        {
            if (cloudBolts != null && PixelCanvas.Alive(cloudBolts[0]))
                return cloudBolts;
            const int w = 30, h = 16;
            cloudBolts = new Sprite[2][];
            for (int v = 0; v < 2; v++)
            {
                var rng = new System.Random(700 + v);
                var main = new List<Pixel>();
                int px = 3, py = 9;
                for (int x = 7; x <= 27; x += 4)
                {
                    int ny = 9 + rng.Next(-2, 3);
                    PixelCanvas.Trace(px, py, x, ny, main);
                    px = x;
                    py = ny;
                }
                Pixel forkFrom = main[main.Count / 2];
                var fork = new List<Pixel>();
                PixelCanvas.Trace(forkFrom.X, forkFrom.Y, forkFrom.X + 2, forkFrom.Y - 4, fork);
                PixelCanvas.Trace(forkFrom.X + 2, forkFrom.Y - 4, forkFrom.X, forkFrom.Y - 6, fork);
                PixelCanvas.Trace(forkFrom.X, forkFrom.Y - 6, forkFrom.X + 1, 1, fork);
                if (v == 1)
                {
                    main = Mirror(main, w);
                    fork = Mirror(fork, w);
                }
                var frames = new Sprite[5];
                var c = new PixelCanvas(w, h);
                c.Draw(main.GetRange(0, main.Count * 2 / 5), White);
                c.Outline(Pale);
                frames[0] = c.ToSprite(Ppu, Middle);
                c = new PixelCanvas(w, h);
                c.Draw(main, White);
                c.Outline(Pale);
                frames[1] = c.ToSprite(Ppu, Middle);
                c = new PixelCanvas(w, h);
                c.Draw(main, White);
                c.Draw(fork, White);
                c.Outline(Pale);
                frames[2] = c.ToSprite(Ppu, Middle);
                c = new PixelCanvas(w, h);
                c.Draw(main, Cyan);
                c.Draw(fork, Cyan);
                c.Outline(Blue);
                frames[3] = c.ToSprite(Ppu, Middle);
                c = new PixelCanvas(w, h);
                for (int i = 0; i < main.Count; i++)
                    if ((i / 3) % 2 == 0)
                        c.Set(main[i].X, main[i].Y, Blue);
                frames[4] = c.ToSprite(Ppu, Middle);
                cloudBolts[v] = frames;
            }
            return cloudBolts;
        }

        private static List<Pixel> Mirror(List<Pixel> path, int width)
        {
            var mirrored = new List<Pixel>(path.Count);
            foreach (Pixel p in path)
                mirrored.Add(new Pixel(width - 1 - p.X, p.Y));
            return mirrored;
        }

        public static int Count(Sprite[][] variants)
        {
            int count = 0;
            foreach (Sprite[] frames in variants)
                count += frames.Length;
            return count;
        }
    }
}
