using System.Collections.Generic;
using UnityEngine;

namespace WoLThunderhead
{
    // The pixel art for the drawn lightning effects (ThunderVisuals), drawn in code once and kept,
    // in the style of the game's own lightning (its Lightning sheet): thin white bolts and rings
    // with no outline, wrapped in a soft pale-yellow glow that thins out over a few pixels, and
    // fading by alpha. The colours are sampled from that sheet: white cores, a pale and a stronger
    // yellow for cooling lines, the glow (252,245,168 at alpha 89) and its yellow haze
    // (245,231,44 at low alpha). The soot under the strikes is the game's own burn mark (see
    // ThunderVisuals); these frames are the glowing cracks over it.
    internal static class ThunderArt
    {
        internal static readonly Color32 White = new Color32(255, 255, 255, 255);
        internal static readonly Color32 Pale = new Color32(255, 251, 137, 255);
        internal static readonly Color32 Yellow = new Color32(250, 241, 82, 255);
        internal static readonly Color32 GlowNear = new Color32(252, 245, 168, 89);
        internal static readonly Color32 GlowFar = new Color32(245, 231, 44, 32);
        internal static readonly Color32 Faint = new Color32(250, 241, 82, 120);   // a dying line

        private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);
        private const float Tau = PixelCanvas.Tau;
        private const float GlowReach = 3f;       // how far the glow spreads, in pixels
        private const float SmallGlow = 2.5f;     // on small shapes (arcs, the cloud's bolts)

        // ---- Strike glyph: the mark where a bolt is about to land ----
        // A ring the size of the strike's hit circle, like the game's lightning circle: arcs of a
        // white ring with little crackles sparking off them and a dot at the centre. The arcs grow
        // until they close, an inner ring of sparks closes in on the centre, and it brightens from
        // yellow to white; the last frame is the flash it flickers with just before the bolt lands.
        public const int GlyphFrames = 6;                 // the charge-up (then the flash frame)
        public const int GlyphFlashFrame = GlyphFrames;
        private const int GlyphArcs = 6;
        private const float GlyphArcTurn = 0.03f;         // how far the arcs turn each frame (of an arc)

        // ---- Strike cracks: glowing over the game's burn mark, then cooling ----
        public static readonly float[] CrackTimes = { 0.1f, 0.2f, 0.35f, 60f };
        private const int CrackVariants = 3;
        private const float CrackShare = 0.6f;            // the cracks' reach, of the strike's radius

        // ---- Impact flash (when the game's lightning core frames aren't loaded) ----
        public const float FlashFrameTime = 0.04f;

        // ---- Arcs crackling round the wizard (when the game's MiniLightning frames aren't loaded) ----
        public const float ArcFrameTime = 0.035f;
        private const int ArcVariants = 8;
        private const int ArcSize = 28;

        // ---- Landing ring: where the slam will land ----
        public const int RingFrames = 6;
        public const int RingFlashFrame = RingFrames;
        private const int RingArcs = 8;

        // ---- Shockwave: a ring of lightning racing out over the ground ----
        public const float WaveFrameTime = 0.045f;
        private static readonly float[] WaveRadius = { 0.22f, 0.45f, 0.66f, 0.84f, 0.98f, 1.1f };
        private static readonly float[] WaveThickness = { 2.2f, 1.6f, 1.1f, 1.1f, 1.1f, 1.1f };
        private static readonly float[] WaveKept = { 1f, 1f, 1f, 0.6f, 0.35f, 0.15f }; // the share of the ring left as it breaks into dashes

        // ---- Crater cracks: the slam's, over the game's bigger burn mark ----
        public static readonly float[] CraterTimes = { 0.15f, 0.3f, 0.5f, 60f };

        // ---- Lightning inside the storm cloud ----
        public const float CloudBoltFrameTime = 0.035f;

        private static float ppu = -1f;
        private static readonly Dictionary<int, Sprite[]> glyphs = new Dictionary<int, Sprite[]>();
        private static readonly Dictionary<int, Sprite[]> rings = new Dictionary<int, Sprite[]>();
        private static readonly Dictionary<int, Sprite[]> waves = new Dictionary<int, Sprite[]>();
        private static readonly Dictionary<int, Sprite[]> craters = new Dictionary<int, Sprite[]>();
        private static readonly Dictionary<int, Sprite[]> flashes = new Dictionary<int, Sprite[]>();
        private static readonly Dictionary<int, Sprite[][]> cracks = new Dictionary<int, Sprite[][]>();
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
            flashes.Clear();
            cracks.Clear();
            arcs = cloudBolts = null;
        }

        public static float Ppu => ppu > 0f ? ppu : 16f;

        public static int Pixels(float units, int min, int max) => Mathf.Clamp(Mathf.RoundToInt(units * Ppu), min, max);

        private static Sprite Done(PixelCanvas c, float glow)
        {
            if (glow > 0f)
                c.Glow(GlowNear, GlowFar, glow);
            return c.ToSprite(Ppu, Middle);
        }

        private static Vector2 Polar(PixelCanvas c, float angle, float radius) =>
            new Vector2(c.CenterX + Mathf.Cos(angle) * radius, c.CenterY + Mathf.Sin(angle) * radius);

        private static void Line(PixelCanvas c, Vector2 a, Vector2 b, Color32 color) =>
            c.Line(Mathf.RoundToInt(a.x), Mathf.RoundToInt(a.y), Mathf.RoundToInt(b.x), Mathf.RoundToInt(b.y), color);

        // A little crackle sparking outward off a ring: a kinked line `length` pixels long.
        private static void Crackle(PixelCanvas c, float angle, float from, float length, int kink, Color32 color)
        {
            Vector2 a = Polar(c, angle, from), b = Polar(c, angle + kink * 0.9f / Mathf.Max(from, 1f), from + length * 0.55f),
                d = Polar(c, angle - kink * 0.4f / Mathf.Max(from, 1f), from + length);
            Line(c, a, b, color);
            Line(c, b, d, color);
        }

        // ---- Strike glyph ----

        public static Sprite[] StrikeGlyph(int r)
        {
            Sprite[] frames;
            if (glyphs.TryGetValue(r, out frames) && PixelCanvas.Alive(frames))
                return frames;
            frames = new Sprite[GlyphFrames + 1];
            int n = 2 * r + 13;
            for (int k = 0; k < GlyphFrames; k++)
            {
                float p = k / (float)(GlyphFrames - 1);
                var c = new PixelCanvas(n, n);
                Color32 line = k < 2 ? Yellow : k < 4 ? Pale : White;
                float cover = 0.45f + 0.45f * p;
                float inner = Mathf.Lerp(r - 3f, 2f, p);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float d = c.Distance(x, y), a = c.Angle(x, y);
                        if (Mathf.Abs(d - r) < 0.55f && PixelCanvas.Frac(a / Tau * GlyphArcs + k * GlyphArcTurn) < cover)
                            c.Set(x, y, line);
                        else if (inner > 2.5f && Mathf.Abs(d - inner) < 0.55f && PixelCanvas.Frac(a / Tau * inner * 0.7f) < 0.35f)
                            c.Set(x, y, k < 4 ? Pale : White);
                    }
                }
                // Crackles sparking off where each arc starts.
                if (k >= 2)
                    for (int i = 0; i < GlyphArcs; i++)
                        Crackle(c, (i - k * GlyphArcTurn) * Tau / GlyphArcs, r + 1f, 2f + (k >= 4 ? 1f : 0f), i % 2 == 0 ? 1 : -1, line);
                int cx = (n - 1) / 2;
                c.Set(cx, cx, k < 3 ? Pale : White);
                if (k >= 3)
                {
                    c.Set(cx + 1, cx, White);
                    c.Set(cx - 1, cx, White);
                    c.Set(cx, cx + 1, White);
                    c.Set(cx, cx - 1, White);
                }
                frames[k] = Done(c, GlowReach);
            }
            // The flash: the whole ring, every crackle and a bright core.
            var f = new PixelCanvas(n, n);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = f.Distance(x, y);
                    if (Mathf.Abs(d - r) < 0.6f || d < 2.6f)
                        f.Set(x, y, White);
                }
            }
            for (int i = 0; i < GlyphArcs * 2; i++)
                Crackle(f, i * Tau / (GlyphArcs * 2), r + 1f, 3f, i % 2 == 0 ? 1 : -1, White);
            frames[GlyphFlashFrame] = Done(f, GlowReach + 1f);
            glyphs[r] = frames;
            return frames;
        }

        // ---- Strike cracks ----

        public static Sprite[][] StrikeCracks(int strikeRadius)
        {
            Sprite[][] variants;
            if (cracks.TryGetValue(strikeRadius, out variants) && PixelCanvas.Alive(variants[0]))
                return variants;
            int rs = Mathf.Max(5, Mathf.RoundToInt(strikeRadius * CrackShare));
            int n = 2 * rs + 9;
            variants = new Sprite[CrackVariants][];
            for (int v = 0; v < CrackVariants; v++)
            {
                var rng = new System.Random(1000 + v * 77 + rs);
                variants[v] = Cooling(n, Paths(n, rs * 0.95f, 5, 1, rng), CrackTimes.Length);
            }
            cracks[strikeRadius] = variants;
            return variants;
        }

        // The same cracks hot to cold: white and glowing, pale with a glow, yellow, then a faint
        // yellow (the sprite fades out from there).
        private static Sprite[] Cooling(int n, List<List<Pixel>> paths, int count)
        {
            var frames = new Sprite[count];
            Color32[] colors = { White, Pale, Yellow, Faint };
            float[] glow = { GlowReach, SmallGlow, 0f, 0f };
            for (int heat = 0; heat < count; heat++)
            {
                var c = new PixelCanvas(n, n);
                foreach (List<Pixel> path in paths)
                    c.Draw(path, colors[heat]);
                frames[heat] = Done(c, glow[heat]);
            }
            return frames;
        }

        // Jagged paths out from the centre, each kept as its pixels in order.
        private static List<List<Pixel>> Paths(int n, float length, int count, int jag, System.Random rng)
        {
            var paths = new List<List<Pixel>>();
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
                paths.Add(path);
            }
            return paths;
        }

        // ---- Impact flash (drawn stand-in for the game's lightning core frames) ----

        public static Sprite[] ImpactFlash(int r)
        {
            Sprite[] frames;
            if (flashes.TryGetValue(r, out frames) && PixelCanvas.Alive(frames))
                return frames;
            frames = new Sprite[4];
            int n = 2 * (r + 2) + 9;
            float turn = r * 0.3f;
            for (int k = 0; k < frames.Length; k++)
            {
                var c = new PixelCanvas(n, n);
                if (k == 0)
                {
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                            if (c.Distance(x, y) < 3.2f)
                                c.Set(x, y, White);
                }
                else
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (k == 3 && i % 2 == 1)
                            continue;
                        float a = turn + i * Tau / 8f;
                        float from = k == 1 ? 0f : r * 0.3f * (k - 1);
                        float to = (i % 2 == 0 ? r + 2f : r - 1f) * (k == 3 ? 0.9f : 1f);
                        Line(c, Polar(c, a, from), Polar(c, a, to), k == 1 ? White : k == 2 ? Pale : Yellow);
                    }
                    if (k == 1)
                        for (int y = 0; y < n; y++)
                            for (int x = 0; x < n; x++)
                                if (c.Distance(x, y) < 2.2f)
                                    c.Set(x, y, White);
                }
                frames[k] = Done(c, k < 3 ? GlowReach : 0f);
            }
            flashes[r] = frames;
            return frames;
        }

        // ---- Arcs (drawn stand-in for the game's MiniLightning frames) ----

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
                var main = new List<Pixel>();
                var points = new List<Vector2>();
                const int segments = 5;
                for (int s = 0; s <= segments; s++)
                {
                    float along = 3f + 9f * s / segments;
                    float jag = s == 0 || s == segments ? 0f : (float)(rng.NextDouble() * 6.0 - 3.0);
                    points.Add(new Vector2(cx, cx) + dir * along + side * jag);
                }
                for (int s = 1; s < points.Count; s++)
                    PixelCanvas.Trace(Mathf.RoundToInt(points[s - 1].x), Mathf.RoundToInt(points[s - 1].y),
                        Mathf.RoundToInt(points[s].x), Mathf.RoundToInt(points[s].y), main);
                var branch = new List<Pixel>();
                Vector2 from = points[2 + rng.Next(0, 2)];
                float ba = a + (rng.Next(0, 2) == 0 ? 0.75f : -0.75f);
                Vector2 bdir = new Vector2(Mathf.Cos(ba), Mathf.Sin(ba));
                Vector2 mid = from + bdir * 2.5f + new Vector2(-bdir.y, bdir.x) * 1.2f, end = from + bdir * 5f;
                PixelCanvas.Trace(Mathf.RoundToInt(from.x), Mathf.RoundToInt(from.y), Mathf.RoundToInt(mid.x), Mathf.RoundToInt(mid.y), branch);
                PixelCanvas.Trace(Mathf.RoundToInt(mid.x), Mathf.RoundToInt(mid.y), Mathf.RoundToInt(end.x), Mathf.RoundToInt(end.y), branch);

                var frames = new Sprite[5];
                var c = new PixelCanvas(ArcSize, ArcSize);
                c.Draw(main.GetRange(0, Mathf.Max(1, main.Count * 55 / 100)), White);
                frames[0] = Done(c, SmallGlow);
                c = new PixelCanvas(ArcSize, ArcSize);
                c.Draw(main, White);
                c.Draw(branch, White);
                frames[1] = Done(c, SmallGlow);
                c = new PixelCanvas(ArcSize, ArcSize);
                c.Draw(main, Pale);
                c.Draw(branch.GetRange(0, Mathf.Max(1, branch.Count / 2)), Pale);
                frames[2] = Done(c, SmallGlow);
                c = new PixelCanvas(ArcSize, ArcSize);
                for (int i = 0; i < main.Count; i++)
                    if ((i / 3) % 2 == 0)
                        c.Set(main[i].X, main[i].Y, Yellow);
                frames[3] = Done(c, 0f);
                c = new PixelCanvas(ArcSize, ArcSize);
                c.Set(main[main.Count - 1].X, main[main.Count - 1].Y, Pale);
                c.Set(branch[branch.Count - 1].X, branch[branch.Count - 1].Y, Pale);
                c.Set(main[main.Count / 2].X, main[main.Count / 2].Y, Yellow);
                frames[4] = Done(c, 1.5f);
                arcs[v] = frames;
            }
            return arcs;
        }

        // ---- Landing ring ----
        // The game's lightning circle again, at the slam's size: arcs of a white ring with
        // crackles sparking off, ticks pointing in, and a ring of sparks closing in; crackling
        // across once it's charged, then flashing white through the dive.

        public static Sprite[] LandingRing(int r)
        {
            Sprite[] frames;
            if (rings.TryGetValue(r, out frames) && PixelCanvas.Alive(frames))
                return frames;
            frames = new Sprite[RingFrames + 1];
            int n = 2 * r + 15;
            var rng = new System.Random(900 + r);
            var crackleAngles = new float[4];
            for (int i = 0; i < crackleAngles.Length; i++)
                crackleAngles[i] = (float)rng.NextDouble() * Tau;
            for (int k = 0; k <= RingFrames; k++)
            {
                bool flash = k == RingFlashFrame;
                int step = Mathf.Min(k, RingFrames - 1);
                float p = step / (float)(RingFrames - 1);
                Color32 line = flash || step >= 4 ? White : step >= 2 ? Pale : Yellow;
                var c = new PixelCanvas(n, n);
                float cover = flash ? 1f : 0.55f + 0.4f * p;
                float inner = Mathf.Lerp(r * 0.85f, r * 0.3f, p);
                float turn = step * 0.02f;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float d = c.Distance(x, y), a = c.Angle(x, y);
                        if (Mathf.Abs(d - r) < 0.55f && PixelCanvas.Frac(a / Tau * RingArcs + turn) < cover)
                            c.Set(x, y, line);
                        else if (Mathf.Abs(d - inner) < 0.55f && PixelCanvas.Frac(a / Tau * inner * 0.6f) < 0.3f)
                            c.Set(x, y, flash ? White : step >= 3 ? Pale : Yellow);
                    }
                }
                for (int i = 0; i < RingArcs; i++)
                {
                    float a = (i - turn) * Tau / RingArcs;
                    if (step >= 1 || flash)
                        Crackle(c, a, r + 1f, step >= 4 || flash ? 4f : 3f, i % 2 == 0 ? 1 : -1, line);
                    if (step >= 2 || flash)
                    {
                        float mid = a + 0.5f * Tau / RingArcs;
                        Line(c, Polar(c, mid, r - 2f), Polar(c, mid, r - 3.5f), line);
                    }
                }
                if (step >= 3 || flash)
                {
                    foreach (float a0 in crackleAngles)
                    {
                        float a = a0 + step * 0.9f;
                        Vector2 p0 = Polar(c, a, inner + 1f), p1 = Polar(c, a + 1.5f / Mathf.Max(r, 1f), Mathf.Lerp(inner, r, 0.5f)), p2 = Polar(c, a, r - 1f);
                        Line(c, p0, p1, line);
                        Line(c, p1, p2, line);
                    }
                }
                if (flash)
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                            if (c.Distance(x, y) < 3f)
                                c.Set(x, y, White);
                frames[k] = Done(c, flash ? GlowReach + 1f : GlowReach);
            }
            rings[r] = frames;
            return frames;
        }

        // ---- Shockwave ----
        // A thin white ring racing out with crackles behind it, thinning and breaking into dashes
        // and dots as it goes (as the game's explosion rings do).

        public static Sprite[] Shockwave(int r)
        {
            Sprite[] frames;
            if (waves.TryGetValue(r, out frames) && PixelCanvas.Alive(frames))
                return frames;
            frames = new Sprite[WaveRadius.Length];
            int n = 2 * Mathf.CeilToInt(r * WaveRadius[WaveRadius.Length - 1]) + 13;
            float phase = r * 0.37f;
            Color32[] ring = { White, White, White, Pale, Yellow, Yellow };
            float[] glow = { GlowReach + 0.5f, GlowReach, GlowReach, SmallGlow, 1.5f, 0f };
            for (int k = 0; k < frames.Length; k++)
            {
                var c = new PixelCanvas(n, n);
                float rk = Mathf.Max(2f, r * WaveRadius[k]), half = WaveThickness[k] / 2f;
                if (k < 3)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Tau / 6f + phase + k * 0.4f;
                        Vector2 p0 = Polar(c, a, 2f), p1 = Polar(c, a + (i % 2 == 0 ? 1.5f : -1.5f) / Mathf.Max(rk * 0.5f, 1f), rk * 0.5f),
                            p2 = Polar(c, a, rk - half - 1.5f);
                        Color32 crackle = k == 0 ? White : k == 1 ? Pale : Yellow;
                        Line(c, p0, p1, crackle);
                        Line(c, p1, p2, crackle);
                    }
                }
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        if (Mathf.Abs(c.Distance(x, y) - rk) > half)
                            continue;
                        if (WaveKept[k] < 1f && PixelCanvas.Frac(c.Angle(x, y) / Tau * rk * 0.35f + phase) > WaveKept[k])
                            continue;
                        c.Set(x, y, ring[k]);
                    }
                }
                frames[k] = Done(c, glow[k]);
            }
            waves[r] = frames;
            return frames;
        }

        // ---- Crater cracks ----

        public static Sprite[] Crater(int r)
        {
            Sprite[] frames;
            if (craters.TryGetValue(r, out frames) && PixelCanvas.Alive(frames))
                return frames;
            int n = 2 * Mathf.CeilToInt(r * 1.4f) + 9;
            var rng = new System.Random(1300 + r);
            frames = Cooling(n, Paths(n, r * 1.35f, 8, 2, rng), CraterTimes.Length);
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
            const int w = 32, h = 18;
            cloudBolts = new Sprite[2][];
            for (int v = 0; v < 2; v++)
            {
                var rng = new System.Random(700 + v);
                var main = new List<Pixel>();
                int px = 4, py = 10;
                for (int x = 8; x <= 28; x += 4)
                {
                    int ny = 10 + rng.Next(-2, 3);
                    PixelCanvas.Trace(px, py, x, ny, main);
                    px = x;
                    py = ny;
                }
                Pixel forkFrom = main[main.Count / 2];
                var fork = new List<Pixel>();
                PixelCanvas.Trace(forkFrom.X, forkFrom.Y, forkFrom.X + 2, forkFrom.Y - 4, fork);
                PixelCanvas.Trace(forkFrom.X + 2, forkFrom.Y - 4, forkFrom.X, forkFrom.Y - 6, fork);
                PixelCanvas.Trace(forkFrom.X, forkFrom.Y - 6, forkFrom.X + 1, 2, fork);
                if (v == 1)
                {
                    main = Mirror(main, w);
                    fork = Mirror(fork, w);
                }
                var frames = new Sprite[5];
                var c = new PixelCanvas(w, h);
                c.Draw(main.GetRange(0, main.Count * 2 / 5), White);
                frames[0] = Done(c, SmallGlow);
                c = new PixelCanvas(w, h);
                c.Draw(main, White);
                frames[1] = Done(c, SmallGlow);
                c = new PixelCanvas(w, h);
                c.Draw(main, White);
                c.Draw(fork, White);
                frames[2] = Done(c, SmallGlow);
                c = new PixelCanvas(w, h);
                c.Draw(main, Pale);
                c.Draw(fork, Pale);
                frames[3] = Done(c, 1.5f);
                c = new PixelCanvas(w, h);
                for (int i = 0; i < main.Count; i++)
                    if ((i / 3) % 2 == 0)
                        c.Set(main[i].X, main[i].Y, Yellow);
                frames[4] = Done(c, 0f);
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
