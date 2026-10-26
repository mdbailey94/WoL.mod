using System.Collections.Generic;
using UnityEngine;

namespace WoLStoneCharge
{
    // Stone Charge's drawn effects, at the game's 16 pixels a unit, in two small palettes: five
    // stone browns (outline, shade, body, lit face, highlight) and four ember tones for the force
    // glowing through (deep, orange, gold, white-hot). Each sheet is drawn once, the first time
    // it's wanted, and logged then:
    // - stone chunks tumbling round the wizard at high force (4 frames: the rock turned a
    //   quarter at a time and re-lit from the top-left each time, so it tumbles rather than
    //   spins), glowing at full force;
    // - rock shards flung from the crash (4 tumbling frames, two shapes);
    // - a crack spreading in the floor behind the running wizard (6 frames), glowing at full;
    // - the crash's crater (7 frames: cracks racing out, a ring of rubble thrown up), glowing
    //   from three quarters force;
    // - a pulse ring on the floor at each force step and at the crash (6 frames);
    // - embers rising off the wizard at full force (6 frames);
    // - the cracks and rune in the shield itself (ShieldRunes).
    public static class StoneArt
    {
        // Index 0 is clear.
        public static readonly Color32[] Palette =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x2a, 0x1b, 0x12, 0xff), // 1 outline
            new Color32(0x5b, 0x3d, 0x29, 0xff), // 2 shade
            new Color32(0x8b, 0x65, 0x44, 0xff), // 3 body
            new Color32(0xb9, 0x92, 0x6a, 0xff), // 4 lit face
            new Color32(0xe3, 0xcd, 0xa3, 0xff), // 5 highlight
            new Color32(0x9c, 0x4a, 0x1a, 0xff), // 6 deep ember
            new Color32(0xe8, 0x8a, 0x2a, 0xff), // 7 orange
            new Color32(0xff, 0xd0, 0x50, 0xff), // 8 gold
            new Color32(0xff, 0xf3, 0xc4, 0xff), // 9 white-hot
        };

        public const int TumbleFrames = 4;
        private const float FloorSquash = 0.55f;   // floor shapes are seen at the game's 3/4 angle
        private const float CrackSquash = 0.75f;   // cracks less so: flatter, they'd run in level bars

        private static Sprite[][] chunks;          // [shape * 2 + (charged ? 1 : 0)]
        private static Sprite[][] shards;
        private static Sprite[] crack, crackGlow, crater, craterGlow, pulse, pulseGlow, ember;
        private static readonly HashSet<string> ready = new HashSet<string>();

        // ---- Hand-drawn shapes ('#' = rock) ----

        private static readonly string[][] ChunkShapes =
        {
            new[]
            {
                "..###...",
                ".######.",
                "########",
                "#######.",
                ".#####..",
                "..###...",
            },
            new[]
            {
                ".###..",
                "#####.",
                "######",
                ".#####",
                "..###.",
            },
        };

        private static readonly string[][] ShardShapes =
        {
            new[]
            {
                ".#..",
                ".##.",
                "###.",
                ".###",
                ".###",
                "..##",
                "..#.",
            },
            new[]
            {
                "..##..",
                ".####.",
                "######",
                ".####.",
                "..#...",
            },
        };

        private static readonly string[][] EmberRows =
        {
            new[] { ".....", "..8..", ".898.", "..8..", "....." },
            new[] { "..7..", ".787.", "78987", ".787.", "..7.." },
            new[] { ".....", "..7..", ".787.", "..7..", "....." },
            new[] { ".....", ".....", "..8..", ".....", "....." },
            new[] { ".....", "..6..", ".676.", "..6..", "....." },
            new[] { ".....", ".....", "..6..", ".....", "....." },
        };

        // ---- Sheets ----

        public static Sprite[] Chunk(int shape, bool charged)
        {
            if (chunks == null || !PixelArt.Alive(chunks[0]))
            {
                chunks = new Sprite[ChunkShapes.Length * 2][];
                for (int s = 0; s < ChunkShapes.Length; s++)
                {
                    chunks[s * 2] = Tumble(ChunkShapes[s], false, 11 + s);
                    chunks[s * 2 + 1] = Tumble(ChunkShapes[s], true, 11 + s);
                }
                Ready("stone chunk", chunks.Length * TumbleFrames, chunks[0]);
            }
            return chunks[(shape % ChunkShapes.Length) * 2 + (charged ? 1 : 0)];
        }

        public static Sprite[] Shard(int shape)
        {
            if (shards == null || !PixelArt.Alive(shards[0]))
            {
                shards = new Sprite[ShardShapes.Length][];
                for (int s = 0; s < ShardShapes.Length; s++)
                    shards[s] = Tumble(ShardShapes[s], false, 23 + s);
                Ready("rock shard", shards.Length * TumbleFrames, shards[0]);
            }
            return shards[shape % ShardShapes.Length];
        }

        public static Sprite[] Crack(bool glow)
        {
            if (!PixelArt.Alive(crack) || !PixelArt.Alive(crackGlow))
            {
                crack = Cracks(26, 14, 6, 5, 4, false, false);
                crackGlow = Cracks(26, 14, 6, 5, 4, true, false);
                Ready("floor crack", crack.Length * 2, crack);
            }
            return glow ? crackGlow : crack;
        }

        public static Sprite[] Crater(bool glow)
        {
            if (!PixelArt.Alive(crater) || !PixelArt.Alive(craterGlow))
            {
                crater = Cracks(48, 28, 7, 8, 7, false, true);
                craterGlow = Cracks(48, 28, 7, 8, 7, true, true);
                Ready("crater", crater.Length * 2, crater);
            }
            return glow ? craterGlow : crater;
        }

        public static Sprite[] Pulse(bool glow)
        {
            if (!PixelArt.Alive(pulse) || !PixelArt.Alive(pulseGlow))
            {
                pulse = PulseRing(false);
                pulseGlow = PulseRing(true);
                Ready("pulse ring", pulse.Length * 2, pulse);
            }
            return glow ? pulseGlow : pulse;
        }

        public static Sprite[] Ember
        {
            get
            {
                if (!PixelArt.Alive(ember))
                {
                    var frames = new PixelArt.Grid[EmberRows.Length];
                    for (int f = 0; f < frames.Length; f++)
                    {
                        frames[f] = new PixelArt.Grid(5, 5);
                        frames[f].Stamp(EmberRows[f], ".123456789", 0, 0);
                    }
                    ember = PixelArt.Sheet(frames, Palette, new Vector2(0.5f, 0.5f));
                    Ready("ember", ember.Length, ember);
                }
                return ember;
            }
        }

        private static void Ready(string visual, int count, Sprite[] sample)
        {
            if (!ready.Add(visual) || !PixelArt.Alive(sample))
                return;
            Rect r = sample[0].rect;
            StoneChargeState.Log($"visual '{visual}' ready ({count} sprites of {r.width:0}x{r.height:0}, ppu {sample[0].pixelsPerUnit:0})");
        }

        // ---- Rocks ----

        // Four frames of a rock tumbling: the shape turned a quarter each frame and shaded
        // afresh with the light from the top-left (top edge highlight, left edge lit, bottom and
        // right edges in shade, a few pits in the body), then outlined. A charged rock gets an
        // ember outline and a glowing vein through it.
        private static Sprite[] Tumble(string[] rows, bool charged, int seed) =>
            PixelArt.Sheet(TumbleGrids(rows, charged, seed), Palette, new Vector2(0.5f, 0.5f));

        private static PixelArt.Grid[] TumbleGrids(string[] rows, bool charged, int seed)
        {
            int mw = rows[0].Length, mh = rows.Length;
            bool[] mask = new bool[mw * mh];
            for (int r = 0; r < mh; r++)
            {
                for (int x = 0; x < mw; x++)
                    mask[(mh - 1 - r) * mw + x] = rows[r][x] == '#';
            }
            int cell = Mathf.Max(mw, mh) + 2;
            var frames = new PixelArt.Grid[TumbleFrames];
            for (int f = 0; f < TumbleFrames; f++)
            {
                frames[f] = ShadeRock(mask, mw, mh, cell, charged, seed + f);
                bool[] turned = Rotate(mask, mw, mh);
                int t = mw;
                mw = mh;
                mh = t;
                mask = turned;
            }
            return frames;
        }

        private static PixelArt.Grid ShadeRock(bool[] mask, int mw, int mh, int cell, bool charged, int seed)
        {
            var g = new PixelArt.Grid(cell, cell);
            int ox = (cell - mw) / 2, oy = (cell - mh) / 2;
            for (int y = 0; y < mh; y++)
            {
                for (int x = 0; x < mw; x++)
                {
                    if (!Filled(mask, mw, mh, x, y))
                        continue;
                    bool up = Filled(mask, mw, mh, x, y + 1), down = Filled(mask, mw, mh, x, y - 1);
                    bool left = Filled(mask, mw, mh, x - 1, y), right = Filled(mask, mw, mh, x + 1, y);
                    int index = 3;
                    if (!up)
                        index = 5;
                    else if (!left)
                        index = 4;
                    else if (!down || !right)
                        index = 2;
                    else if (Hash(x, y, seed) % 6 == 0)
                        index = 2;
                    else if (Hash(x, y, seed) % 7 == 1)
                        index = 4;
                    // A glowing vein along the middle diagonal of a charged rock.
                    if (charged && up && down && left && right && x - y == (mw - mh) / 2)
                        index = (x == mw / 2) ? 9 : 8;
                    g.Set(ox + x, oy + y, index);
                }
            }
            g.Outline(charged ? 7 : 1, 1);
            return g;
        }

        private static bool Filled(bool[] mask, int w, int h, int x, int y) => x >= 0 && y >= 0 && x < w && y < h && mask[y * w + x];

        // A quarter turn anticlockwise.
        private static bool[] Rotate(bool[] mask, int w, int h)
        {
            var turned = new bool[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int nx = h - 1 - y, ny = x;
                    turned[ny * h + nx] = mask[y * w + x];
                }
            }
            return turned;
        }

        private static int Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 982451653;
                h = (h ^ (h >> 13)) * 1274126177;
                return (h ^ (h >> 16)) & 0x7fffffff;
            }
        }

        // ---- Floor cracks and the crater ----

        private struct CrackPixel
        {
            public int X, Y;
            public float Birth;     // 0 at the centre to 1 at the longest tip
        }

        // Cracks running out from a centre in rays that wander and fork, revealed tip by tip over
        // the frames (the last two hold the finished crack). A plain crack is a dark groove with
        // a lit lower lip and a shaded upper one; a glowing one has a gold-to-orange core (white-
        // hot near the centre) edged in the outline colour, cooling a shade in the last frame.
        // The crater adds a ring of rubble chips thrown out over the first frames.
        private static Sprite[] Cracks(int w, int h, int frameCount, int rays, int seed, bool glow, bool rubble) =>
            PixelArt.Sheet(CrackGrids(w, h, frameCount, rays, seed, glow, rubble), Palette, new Vector2(0.5f, 0.5f));

        private static PixelArt.Grid[] CrackGrids(int w, int h, int frameCount, int rays, int seed, bool glow, bool rubble)
        {
            var rng = new System.Random(seed);
            float cx = w * 0.5f, cy = h * 0.5f;
            float reach = w * 0.5f - 2f;
            var pixels = new List<CrackPixel>();
            var seen = new HashSet<int>();
            for (int i = 0; i < rays; i++)
            {
                float angle = i * 360f / rays + Range(rng, -18f, 18f);
                float length = reach * Range(rng, 0.6f, 1f);
                Walk(pixels, seen, w, h, cx, cy, angle, length, 0f, reach, rng, true);
            }
            int[] chipAngle = new int[rubble ? 12 : 0];
            float[] chipRadius = new float[chipAngle.Length];
            for (int i = 0; i < chipAngle.Length; i++)
            {
                chipAngle[i] = i * 30 + rng.Next(-10, 11);
                chipRadius[i] = Range(rng, 0.72f, 0.95f);
            }

            var frames = new PixelArt.Grid[frameCount];
            for (int f = 0; f < frameCount; f++)
            {
                var g = new PixelArt.Grid(w, h);
                float shown = Mathf.Min(1f, (f + 1f) / (frameCount - 2f));
                bool last = f == frameCount - 1;
                // Lips first, on clear pixels only; the cores then go over them.
                foreach (CrackPixel p in pixels)
                {
                    if (p.Birth > shown)
                        continue;
                    if (glow)
                    {
                        Lip(g, p.X + 1, p.Y, 1);
                        Lip(g, p.X - 1, p.Y, 1);
                        Lip(g, p.X, p.Y + 1, 1);
                        Lip(g, p.X, p.Y - 1, 1);
                    }
                    else
                        Lip(g, p.X, p.Y - 1, 4);    // a lit lower lip; the groove itself is the shadow
                }
                foreach (CrackPixel p in pixels)
                {
                    if (p.Birth > shown)
                        continue;
                    int core = !glow ? 1 : p.Birth < 0.3f ? 9 : p.Birth < 0.7f ? 8 : 7;
                    if (glow && last)
                        core = Mathf.Max(6, core - 1);
                    g.Set(p.X, p.Y, core);
                }
                // The hole at the centre.
                int hole = !glow ? 1 : last ? 8 : 9;
                float holeRadius = f == 0 ? 1.2f : rubble ? 2.4f : 1.6f;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float dx = x + 0.5f - cx, dy = (y + 0.5f - cy) / FloorSquash;
                        if (dx * dx + dy * dy <= holeRadius * holeRadius)
                            g.Set(x, y, hole);
                    }
                }
                if (rubble && f >= 1)
                {
                    // Chips thrown out to their ring over three frames, each a 2x2 stone with a
                    // highlight on top, outlined.
                    var chips = new PixelArt.Grid(w, h);
                    float out01 = Mathf.Min(1f, f / 3f);
                    for (int i = 0; i < chipAngle.Length; i++)
                    {
                        float rad = chipAngle[i] * Mathf.Deg2Rad;
                        float r = reach * chipRadius[i] * (0.45f + 0.55f * out01);
                        int x = Mathf.FloorToInt(cx + Mathf.Cos(rad) * r), y = Mathf.FloorToInt(cy + Mathf.Sin(rad) * r * FloorSquash);
                        chips.Set(x, y + 1, 5);
                        chips.Set(x + 1, y + 1, 4);
                        chips.Set(x, y, 3);
                        chips.Set(x + 1, y, 2);
                    }
                    chips.Outline(1, 2);
                    for (int i = 0; i < chips.Cells.Length; i++)
                    {
                        if (chips.Cells[i] != 0)
                            g.Cells[i] = chips.Cells[i];
                    }
                }
                frames[f] = g;
            }
            return frames;
        }

        // One ray of a crack: unit steps from (x, y) along angle, wandering, forking once
        // halfway (a shorter branch off at 40 degrees) if fork is set.
        private static void Walk(List<CrackPixel> pixels, HashSet<int> seen, int w, int h, float x, float y, float angle, float length,
            float start, float reach, System.Random rng, bool fork)
        {
            for (int s = 0; s < length; s++)
            {
                int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
                if (ix < 1 || iy < 1 || ix >= w - 1 || iy >= h - 1)
                    return;
                if (seen.Add(iy * w + ix))
                    pixels.Add(new CrackPixel { X = ix, Y = iy, Birth = (start + s) / reach });
                if (rng.NextDouble() < 0.4)
                    angle += Range(rng, -28f, 28f);
                if (fork && s == Mathf.RoundToInt(length * 0.5f) && rng.NextDouble() < 0.7)
                    Walk(pixels, seen, w, h, x, y, angle + (rng.NextDouble() < 0.5 ? -40f : 40f), length * 0.4f, start + s, reach, rng, false);
                x += Mathf.Cos(angle * Mathf.Deg2Rad);
                y += Mathf.Sin(angle * Mathf.Deg2Rad) * CrackSquash;
            }
        }

        private static void Lip(PixelArt.Grid g, int x, int y, int index)
        {
            if (g.Get(x, y) == 0)
                g.Set(x, y, index);
        }

        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        // ---- Pulse ring ----

        // A broken ring of stone (or of embers) spreading over the floor and thinning to dots.
        private static Sprite[] PulseRing(bool glow) => PixelArt.Sheet(PulseGrids(glow), Palette, new Vector2(0.5f, 0.5f));

        private static PixelArt.Grid[] PulseGrids(bool glow)
        {
            const int w = 48, h = 26, frameCount = 6;
            var frames = new PixelArt.Grid[frameCount];
            for (int f = 0; f < frameCount; f++)
            {
                var g = new PixelArt.Grid(w, h);
                float ring = 0.35f + 0.6f * f / (frameCount - 1);
                float thick = (f < 4 ? 2.2f : 1.2f) / (w * 0.5f);
                float gap = f < 4 ? 0.72f : 0.45f;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f - w * 0.5f) / (w * 0.5f - 1f);
                        float v = (y + 0.5f - h * 0.5f) / (h * 0.5f - 1f);
                        float r = Mathf.Sqrt(u * u + v * v);
                        if (Mathf.Abs(r - ring) > thick)
                            continue;
                        float turn = Mathf.Atan2(v, u) / (2f * Mathf.PI) + 0.5f;
                        if (turn * 14f - Mathf.Floor(turn * 14f) >= gap)
                            continue;
                        bool inner = r < ring;
                        int index = glow ? (inner ? 9 : 7) : (inner ? 5 : 3);
                        if (f >= 4)
                            index = glow ? (inner ? 8 : 6) : (inner ? 3 : 2);
                        g.Set(x, y, index);
                    }
                }
                g.Outline(glow ? 6 : 1, 2);
                frames[f] = g;
            }
            return frames;
        }
    }

    // The stone chunks circling the wizard while the force is high: two at half force, three at
    // three quarters, four at full (then glowing), on a flat ellipse about their waist, bobbing,
    // behind them on the far side and in front on the near side. Unloading flings them forward.
    public class StoneOrbit
    {
        private const float RadiusX = 1.0f;
        private const float RadiusY = 0.5f;
        private const float Lift = 0.55f;          // above the feet
        private const float Bob = 0.08f;
        private const float BobSpeed = 6f;         // radians a second
        private const float TumbleFps = 8f;
        private static readonly int[] CountAt = { 0, 0, 2, 3, 4 };            // by force step (25% each)
        private static readonly float[] SpeedAt = { 0f, 0f, 200f, 250f, 320f }; // degrees a second

        private readonly List<FxHandle> chunks = new List<FxHandle>();
        private bool charged;
        private float turn;
        private float clock;

        public int Count => chunks.Count;

        public void Update(Player player, int step, object owner)
        {
            step = Mathf.Clamp(step, 0, CountAt.Length - 1);
            float dt = Time.deltaTime;
            clock += dt;
            Vector2 feet = player.transform.position;
            bool glow = step >= 4;
            while (chunks.Count < CountAt[step])
            {
                FxSprite fx = FxSprite.Spawn(StoneArt.Chunk(chunks.Count, glow), feet + new Vector2(0f, Lift), "Actor", 0);
                if (fx == null)
                    break;
                fx.loop = true;
                fx.fps = TumbleFps;
                fx.owner = owner;
                fx.sortRef = player.spriteRenderer;
                fx.Skip(chunks.Count);
                chunks.Add(new FxHandle(fx));
            }
            if (glow != charged)
            {
                charged = glow;
                for (int i = 0; i < chunks.Count; i++)
                    chunks[i].Get?.SetFrames(StoneArt.Chunk(i, glow));
            }
            turn += SpeedAt[step] * dt;
            for (int i = 0; i < chunks.Count; i++)
            {
                FxSprite fx = chunks[i].Get;
                if (fx == null)
                    continue;
                float a = (turn + i * 360f / chunks.Count) * Mathf.Deg2Rad;
                float bob = Mathf.Sin(clock * BobSpeed + i * 1.7f) * Bob;
                fx.Place(feet + new Vector2(Mathf.Cos(a) * RadiusX, Mathf.Sin(a) * RadiusY + Lift + bob));
                fx.sortOffset = Mathf.Sin(a) > 0f ? -1 : 1;
            }
        }

        // The chunks thrown along the rush: each carries on as loose debris, hopping and
        // tumbling faster, then steps out.
        public void Fling(Vector2 direction)
        {
            Vector2 side = new Vector2(-direction.y, direction.x);
            foreach (FxHandle handle in chunks)
            {
                FxSprite fx = handle.Get;
                if (fx == null)
                    continue;
                Vector2 at = fx.transform.position;
                FxSprite thrown = FxSprite.Spawn(fx.frames, at, "Effect", 0);
                fx.Stop();
                if (thrown == null)
                    continue;
                thrown.velocity = direction * Random.Range(5f, 8f) + side * Random.Range(-1.5f, 1.5f);
                thrown.drag = 3f;
                thrown.hop = Random.Range(0.3f, 0.6f);
                thrown.life = Random.Range(0.45f, 0.6f);
                thrown.hopTime = thrown.life * 0.8f;
                thrown.fps = TumbleFps * 2f;
                thrown.loop = true;
                thrown.fadeAt = 0.7f;
            }
            chunks.Clear();
        }

        public void Stop()
        {
            foreach (FxHandle handle in chunks)
                handle.Get?.Stop();
            chunks.Clear();
        }
    }
}
