using System.Collections.Generic;
using UnityEngine;

namespace WoLStoneCharge
{
    // Stone Charge's look. Where the game's own earth sprites are loaded they're played as they
    // are (0.4.1), so it matches the game exactly:
    // - stone chunks circling the wizard: the four rocks of the RockMed debris sheet, tumbling by
    //   quarter turns as the game's debris does;
    // - rock shards flung from the crash: the bigger stones of the Rocks sheet (Rocks_N);
    // - cracks in the floor behind the running wizard and the crash's crater: the
    //   FloorCrackEffects sheet (the crack spreading to a hole, as Earth Axe and Boulder Drop
    //   make);
    // - a ring of rock spikes erupting round the crash: the EarthWave sheet (8 spikes shooting up
    //   and crumbling, 8 frames each).
    // Where they aren't (a level that hasn't loaded those sheets) the drawn frames below stand in,
    // in the game's colours sampled from its sheets (the rock debris browns and, for the force
    // glowing through, its fire tones), no outlines, fading by alpha. Embers rising off the
    // wizard at full force are always drawn (6 frames, fire tones), and so are the cracks and
    // rune in the shield (ShieldRunes). Each is logged once, saying which it uses.
    public static class StoneArt
    {
        // Index 0 is clear. The stone tones are the game's rock debris sheets' (Rocks, RockMed),
        // the ember tones its fire sheets' (FireProjectiles, MeteorStrike).
        public static readonly Color32[] Palette =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x33, 0x27, 0x19, 0xff), // 1 deepest shade (cracks, grooves)
            new Color32(0x4d, 0x37, 0x20, 0xff), // 2 shade
            new Color32(0x73, 0x50, 0x2a, 0xff), // 3 body
            new Color32(0x82, 0x68, 0x3e, 0xff), // 4 lit face
            new Color32(0x9a, 0x7c, 0x4c, 0xff), // 5 highlight
            new Color32(0xda, 0x6e, 0x00, 0xff), // 6 deep orange
            new Color32(0xed, 0x93, 0x0e, 0xff), // 7 orange
            new Color32(0xf9, 0xd2, 0x3c, 0xff), // 8 yellow
            new Color32(0xf6, 0xed, 0xa4, 0xff), // 9 pale yellow-white
        };

        public const int TumbleFrames = 4;
        private const float FloorSquash = 0.55f;   // floor shapes are seen at the game's 3/4 angle
        private const float CrackSquash = 0.75f;   // cracks less so: flatter, they'd run in level bars

        // ---- The game's own sprites (null when not loaded: the drawn ones stand in) ----

        // Content centres / feet in the game's cells, in cell pixels from the bottom-left
        // (measured from the sheets).
        private static readonly Vector2 RockMedCentre = new Vector2(16.5f, 15f);   // RockMed: 32x32 cells, ~10 px rocks
        private static readonly Vector2 CrackCentre = new Vector2(23.5f, 25.5f);   // FloorCrackEffects: 48x48 cells
        public const float CrackWidth = 39f / PixelArt.Ppu;                         // its last frame's width, in units
        private static readonly Vector2 SpikeFoot = new Vector2(17.5f, 11f);       // EarthWave: 32x32 cells, the spike's base
        public const int SpikeRows = 8;                                             // EarthWave: 8 spikes of 8 frames
        // The bigger stones on the Rocks sheet (its first two columns).
        private static readonly int[] ShardRocks = { 0, 1, 4, 5, 8, 9, 12, 13 };

        // One of the four rocks of the game's RockMed debris sheet (a single frame: it tumbles by
        // turning).
        public static Sprite[] GameChunk(int i)
        {
            Sprite[] rocks = Game("stone chunk", "RockMed sheet (4 rocks in 32x32 cells)", GameSprites.Column("RockMed", 32, 32, 0, 4, RockMedCentre));
            return rocks != null ? new[] { rocks[i % rocks.Length] } : null;
        }

        // A stone off the game's Rocks debris sheet (the named Rocks_N sprites).
        public static Sprite[] GameShard(int i)
        {
            Sprite[] rocks = Game("rock shard", "Rocks/Rocks_0-15", GameSprites.Named("Rocks", "Rocks_", 0, 16));
            return rocks != null ? new[] { rocks[ShardRocks[i % ShardRocks.Length]] } : null;
        }

        // The game's floor crack (the FloorCrackEffects sheet's first row): its first two frames
        // for the cracks behind you, all four (to the hole) for the crash.
        public static Sprite[] GameCrack => Game("floor crack", "FloorCrackEffects sheet row 1, frames 1-2 (48x48)",
            GameSprites.Cells("FloorCrackEffects", 48, 48, 0, 0, 2, CrackCentre));
        public static Sprite[] GameCrater => Game("crater", "FloorCrackEffects sheet row 1, frames 1-4 (48x48)",
            GameSprites.Cells("FloorCrackEffects", 48, 48, 0, 0, 4, CrackCentre));

        // One of the game's eight rock spikes (the EarthWave sheet: a spike shooting up and
        // crumbling, 8 frames), pivoted at its foot.
        public static Sprite[] GameSpike(int row)
        {
            Sprite[] first = Game("rock spikes", "EarthWave sheet (8 rows of 8 frames, 32x32)",
                GameSprites.Cells("EarthWave", 32, 32, 0, 0, 8, SpikeFoot));
            return first == null ? null : GameSprites.Cells("EarthWave", 32, 32, row % SpikeRows, 0, 8, SpikeFoot);
        }

        private static readonly Dictionary<string, bool> usingGame = new Dictionary<string, bool>();

        // Logs once per visual (and again if that changes) whether it's the game's sprites or the
        // drawn stand-ins.
        private static Sprite[] Game(string visual, string source, Sprite[] frames)
        {
            bool found = PixelArt.Alive(frames);
            bool was;
            if (!usingGame.TryGetValue(visual, out was) || was != found)
            {
                usingGame[visual] = found;
                if (found)
                    StoneChargeState.Log($"visual '{visual}' uses the game's {source} ({frames.Length} sprites of " +
                        $"{frames[0].rect.width:0}x{frames[0].rect.height:0}, ppu {frames[0].pixelsPerUnit:0})");
                else
                    StoneChargeState.Log($"visual '{visual}': the game's {source} isn't loaded here, using the drawn frames");
            }
            return found ? frames : null;
        }

        // ---- Drawn stand-ins ----

        private static Sprite[][] chunks;          // [shape * 2 + (charged ? 1 : 0)]
        private static Sprite[][] shards;
        private static Sprite[] crack, crackGlow, crater, craterGlow, ember;
        private static readonly HashSet<string> ready = new HashSet<string>();

        // Hand-drawn shapes ('#' = rock).
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

        // An ember: a lick of flame that bends, shrinks and goes out as it rises, in the game's
        // fire tones (6 deep orange, 7 orange, 8 yellow, 9 pale), like the bits its fire sheds.
        private static readonly string[][] EmberRows =
        {
            new[] { ".....", "..8..", ".898.", "..8..", "....." },
            new[] { "..8..", ".898.", ".888.", "..7..", "....." },
            new[] { "..8..", ".87..", ".77..", "..7..", "....." },
            new[] { ".....", "..7..", ".77..", "..6..", "....." },
            new[] { ".....", "..6..", ".6...", ".....", "....." },
            new[] { ".....", ".....", "..6..", ".....", "....." },
        };

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

        public static Sprite[] Ember
        {
            get
            {
                if (!PixelArt.Alive(ember))
                {
                    ember = PixelArt.Sheet(EmberGrids(), Palette, new Vector2(0.5f, 0.5f));
                    Ready("ember", ember.Length, ember);
                }
                return ember;
            }
        }

        private static PixelArt.Grid[] EmberGrids()
        {
            var frames = new PixelArt.Grid[EmberRows.Length];
            for (int f = 0; f < frames.Length; f++)
            {
                frames[f] = new PixelArt.Grid(5, 5);
                frames[f].Stamp(EmberRows[f], ".123456789", 0, 0);
            }
            return frames;
        }

        private static void Ready(string visual, int count, Sprite[] sample)
        {
            if (!ready.Add(visual) || !PixelArt.Alive(sample))
                return;
            Rect r = sample[0].rect;
            StoneChargeState.Log($"visual '{visual}' ready ({count} sprites of {r.width:0}x{r.height:0}, ppu {sample[0].pixelsPerUnit:0})");
        }

        // ---- Rocks ----

        // Four frames of a rock tumbling: the shape turned a quarter each frame and shaded afresh
        // with the light from the top-left (top edge highlight, left edge lit, bottom and right
        // edges in shade, a few pits in the body), as the game's debris rocks are, with no
        // outline. A charged rock has a glowing vein through it.
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
                    else if (!down)
                        index = 1;
                    else if (!right)
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
        // the frames (the last two hold the finished crack), drawn like the game's floor cracks:
        // thin lines in the deepest shade round a hole, no rims. A glowing one has a yellow-to-
        // orange core (pale near the centre), cooling a tone in the last frame. The crater adds a
        // ring of rubble chips thrown out over the first frames.
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
                    // Chips thrown out to their ring over three frames, each a 2x2 stone lit on
                    // top and shaded below.
                    float out01 = Mathf.Min(1f, f / 3f);
                    for (int i = 0; i < chipAngle.Length; i++)
                    {
                        float rad = chipAngle[i] * Mathf.Deg2Rad;
                        float r = reach * chipRadius[i] * (0.45f + 0.55f * out01);
                        int x = Mathf.FloorToInt(cx + Mathf.Cos(rad) * r), y = Mathf.FloorToInt(cy + Mathf.Sin(rad) * r * FloorSquash);
                        g.Set(x, y + 1, 5);
                        g.Set(x + 1, y + 1, 4);
                        g.Set(x, y, 3);
                        g.Set(x + 1, y, 2);
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

        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }

    // The stone chunks circling the wizard while the force is high: two at half force, three at
    // three quarters, four at full (then warmed and shedding embers), on a flat ellipse about
    // their waist, bobbing, tumbling, behind them on the far side and in front on the near side.
    // Unloading flings them forward.
    public class StoneOrbit
    {
        private const float RadiusX = 1.0f;
        private const float RadiusY = 0.5f;
        private const float Lift = 0.55f;          // above the feet
        private const float Bob = 0.08f;
        private const float BobSpeed = 6f;         // radians a second
        private const float TumbleFps = 8f;        // drawn frames a second, or quarter turns of the game's rock
        private static readonly int[] CountAt = { 0, 0, 2, 3, 4 };            // by force step (25% each)
        private static readonly float[] SpeedAt = { 0f, 0f, 200f, 250f, 320f }; // degrees a second
        // At full force the game's rocks are warmed towards the fire tones (the drawn ones get a
        // glowing vein instead).
        private static readonly Color ChargedTint = new Color(1f, 0.86f, 0.68f, 1f);

        private readonly List<FxHandle> chunks = new List<FxHandle>();
        private bool charged;
        private float turn;
        private float clock;

        public int Count => chunks.Count;

        // Where one of the chunks is now (for embers shed off them), or false if there are none.
        public bool AnyChunk(out Vector2 at)
        {
            at = Vector2.zero;
            if (chunks.Count == 0)
                return false;
            FxSprite fx = chunks[Random.Range(0, chunks.Count)].Get;
            if (fx == null)
                return false;
            at = fx.transform.position;
            return true;
        }

        public void Update(Player player, int step, object owner)
        {
            step = Mathf.Clamp(step, 0, CountAt.Length - 1);
            float dt = Time.deltaTime;
            clock += dt;
            Vector2 feet = player.transform.position;
            bool glow = step >= 4;
            while (chunks.Count < CountAt[step])
            {
                int i = chunks.Count;
                Sprite[] game = StoneArt.GameChunk(i);
                FxSprite fx = FxSprite.Spawn(game ?? StoneArt.Chunk(i, glow), feet + new Vector2(0f, Lift), "Actor", 0);
                if (fx == null)
                    break;
                fx.loop = true;
                fx.fps = TumbleFps;
                if (game != null)
                {
                    // One frame: tumble it by quarter turns, each its own way.
                    fx.spin = (i % 2 == 0 ? 90f : -90f) * TumbleFps;
                    fx.spinStep = 90f;
                    fx.angle = 90f * i;
                }
                fx.owner = owner;
                fx.sortRef = player.spriteRenderer;
                fx.Skip(i);
                chunks.Add(new FxHandle(fx));
            }
            if (glow != charged)
            {
                charged = glow;
                for (int i = 0; i < chunks.Count; i++)
                {
                    FxSprite fx = chunks[i].Get;
                    if (fx == null)
                        continue;
                    if (fx.frames.Length == 1)
                        fx.tint = glow ? ChargedTint : Color.white;
                    else
                        fx.SetFrames(StoneArt.Chunk(i, glow));
                }
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
        // tumbling faster, then fades.
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
                bool single = fx.frames.Length == 1;
                Color tint = fx.tint;
                float angle = fx.transform.localEulerAngles.z;
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
                thrown.tint = tint;
                if (single)
                {
                    thrown.angle = angle;
                    thrown.spin = 90f * TumbleFps * 2f;
                    thrown.spinStep = 90f;
                }
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
