using System.Collections.Generic;
using UnityEngine;

namespace WoLGaleRider
{
    // Gale Rider's look. Where the game's own wind sprites are loaded they're played as they are
    // (0.2.1), so it matches the game exactly:
    // - the wind ball: WindSlash/WindBall0-5 (6 frames, 48x48), the game's own ball of wind;
    // - the gather as it forms: WindEffects_0-5 (the whirlwind forming at the feet), and the
    //   unravel as the ride ends: WindEffects_8-12 (it lifts and comes apart) with the broken ring
    //   from the WindParticles sheet (row 5, 6 frames) spreading on the floor;
    // - a gust where an enemy is shoved: WindSlash/WindArrowBreak0-5 (a gust arc breaking up);
    // - wind streaks off the back: the WindTrail sheet's first row (8 frames of a wisp).
    // Where they aren't (a level that hasn't loaded those sheets) the drawn frames below stand in,
    // in the game's wind colours sampled from those sheets (#cfebeb, #b6d5d5, #8b9d9d, #697a7a),
    // no outlines, fading by alpha: the ball's spiral streaks (8 frames in four sizes), a streak
    // flick (6), a gust crescent (5) and the gather (6, backwards for the unravel). The gust ring
    // on the floor under the ball is always drawn. Each is logged once, saying which it uses.
    public static class GaleArt
    {
        // Index 0 is clear; the rest run dark to light, so lighter strokes win where they cross.
        // The four tones of the game's wind sheets (WindSlash, WindEffects, WindTrail).
        private static readonly Color32[] Palette =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x69, 0x7a, 0x7a, 0xff), // 1 deep shade (sparing)
            new Color32(0x8b, 0x9d, 0x9d, 0xff), // 2 shade
            new Color32(0xb6, 0xd5, 0xd5, 0xff), // 3 body
            new Color32(0xcf, 0xeb, 0xeb, 0xff), // 4 light
            new Color32(0xcf, 0xeb, 0xeb, 0xff), // 5 light (the game's wind has no white)
        };

        // ---- The game's own frames (null when not loaded) ----

        // Content centres of the game's frames, in their rect's pixels from the bottom-left
        // (measured from the sheets), so they can be placed whatever their pivots are.
        public static readonly Vector2 GameBallCentre = new Vector2(24f, 24f);      // WindBall: 48x48, ~44 px across
        public const float GameBallWidth = 44f / PixelArt.Ppu;
        public static readonly Vector2 GameWhirlBase = new Vector2(61f, 26f);       // WindEffects_*: the whirl's foot
        public static readonly Vector2 GameGustCentre = new Vector2(23.5f, 15.5f);  // WindArrowBreak: apex up
        public const float GameRingWidth = 31f / PixelArt.Ppu;                      // WindParticles ring at its widest

        public static Sprite[] GameBall => Game("wind ball", "WindSlash/WindBall0-5", GameSprites.Named("WindSlash", "WindBall", 0, 6));
        public static Sprite[] GameGather => Game("gather", "WindEffects/WindEffects_0-5", GameSprites.Named("WindEffects", "WindEffects_", 0, 6));
        public static Sprite[] GameUnravel => Game("unravel", "WindEffects/WindEffects_8-12", GameSprites.Named("WindEffects", "WindEffects_", 8, 5));
        public static Sprite[] GameGust => Game("gust puff", "WindSlash/WindArrowBreak0-5", GameSprites.Named("WindSlash", "WindArrowBreak", 0, 6));
        public static Sprite[] GameStreak => Game("wind streak", "WindTrail sheet row 1 (8 cells of 32x32)",
            GameSprites.Cells("WindTrail", 32, 32, 0, 0, 8, new Vector2(16f, 16f)));
        public static Sprite[] GameRing => Game("unravel ring", "WindParticles sheet row 5 (6 cells of 48x48)",
            GameSprites.Cells("WindParticles", 48, 48, 4, 0, 6, new Vector2(24f, 24f)));

        // The lower half of each of the game's ball frames (up to its middle), pivoted to line up
        // with the whole frame: drawn in front of the wizard so they ride inside the ball.
        public static Sprite[] GameBallFront(Sprite[] ball)
        {
            if (!PixelArt.Alive(ball))
                return null;
            if (PixelArt.Alive(ballFrontGame) && ballFrontGame.Length == ball.Length && ballFrontSource == ball[0])
                return ballFrontGame;
            var half = new Sprite[ball.Length];
            try
            {
                for (int i = 0; i < ball.Length; i++)
                {
                    Rect r = ball[i].textureRect;
                    float h = Mathf.Round(GameBallCentre.y);
                    half[i] = Sprite.Create(ball[i].texture, new Rect(r.x, r.y, r.width, h),
                        new Vector2(ball[i].pivot.x / r.width, ball[i].pivot.y / h), ball[i].pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    half[i].hideFlags = HideFlags.HideAndDontSave;
                }
            }
            catch (System.Exception e)
            {
                // A tightly packed atlas has no rectangle to cut: the drawn ball stands in.
                GaleRiderPlugin.Log($"Gale Rider: couldn't cut the game's wind ball in half ({e.Message}); using the drawn ball");
                return null;
            }
            ballFrontSource = ball[0];
            return ballFrontGame = half;
        }

        private static Sprite[] ballFrontGame;
        private static Sprite ballFrontSource;
        private static readonly Dictionary<string, bool> usingGame = new Dictionary<string, bool>();

        // Logs once per visual (and again if that changes) whether it's the game's frames or the
        // drawn stand-ins.
        private static Sprite[] Game(string visual, string source, Sprite[] frames)
        {
            bool found = PixelArt.Alive(frames);
            bool was;
            if (!usingGame.TryGetValue(visual, out was) || was != found)
            {
                usingGame[visual] = found;
                if (found)
                    GaleRiderPlugin.Log($"Gale Rider: visual '{visual}' uses the game's {source} ({frames.Length} sprites of " +
                        $"{frames[0].rect.width:0}x{frames[0].rect.height:0}, ppu {frames[0].pixelsPerUnit:0})");
                else
                    GaleRiderPlugin.Log($"Gale Rider: visual '{visual}': the game's {source} isn't loaded here, using the drawn frames");
            }
            return found ? frames : null;
        }

        // The wind ball's widths in pixels (2.5 to 5.5 units): the nearest is picked for the
        // ball's size and scaled the last bit, so its pixels stay close to the game's.
        public static readonly int[] BallWidths = { 40, 56, 72, 88 };
        private const float BallSquash = 0.6f;  // height / width: the 3/4 view of a spinning disc
        private const int BallFrames = 8;
        private const int BallArms = 3;
        private const float ArmLength = 0.55f;  // of the gap between arms, head to tail
        private const float ArmTwist = 0.8f;    // how far (x0.5 of the radius) a streak spirals in, head to tail
        private const float OuterHead = 0.86f;  // radius of the main streaks' heads (1 = the rim)
        private const float InnerHead = 0.66f;  // and of the fainter streaks between them
        private const float InnerStrength = 0.6f;
        private const float StreakHeadWidth = 0.13f;
        private const float StreakTailWidth = 0.03f;
        private const float InnerRadius = 0.36f; // the wizard stands in the clear middle
        private const int RimDashes = 12;
        private const float GroundSquash = 0.45f;
        private const int RingArcs = 3;         // the floor ring's arcs
        private const float RingArcSpan = 0.62f; // of the gap between arcs, head to tail
        private const float RingRadius = 0.96f;
        private const float RingHeadWidth = 1.2f; // pixels, plus RingTailWidth
        private const float RingTailWidth = 0.9f;

        private static Sprite[][] ballBack, ballFront, ballWhole, groundRing;
        private static Sprite[] streak, puff, gather, unravel;
        private static readonly HashSet<string> ready = new HashSet<string>();

        // The ball sheets for size variant v (an index into BallWidths).
        public static Sprite[] BallBack(int v) { BuildBall(); return ballBack[v]; }
        public static Sprite[] BallFront(int v) { BuildBall(); return ballFront[v]; }
        public static Sprite[] BallWhole(int v) { BuildBall(); return ballWhole[v]; }
        public static Sprite[] GroundRing(int v) { BuildBall(); return groundRing[v]; }

        public static Sprite[] Streak => PixelArt.Alive(streak) ? streak : (streak = Ready("wind streak", BuildStreak()));
        public static Sprite[] Puff => PixelArt.Alive(puff) ? puff : (puff = Ready("gust puff", BuildPuff()));
        public static Sprite[] Gather => PixelArt.Alive(gather) ? gather : (gather = Ready("gather", BuildGather()));

        public static Sprite[] Unravel
        {
            get
            {
                if (!PixelArt.Alive(unravel))
                {
                    Sprite[] g = Gather;
                    unravel = new Sprite[g.Length];
                    for (int i = 0; i < g.Length; i++)
                        unravel[i] = g[g.Length - 1 - i];
                    Ready("unravel", unravel);
                }
                return unravel;
            }
        }

        private static Sprite[] Ready(string visual, Sprite[] sheet)
        {
            if (ready.Add(visual) && PixelArt.Alive(sheet))
            {
                Rect r = sheet[0].rect;
                GaleRiderPlugin.Log($"Gale Rider: visual '{visual}' ready ({sheet.Length} sprites of {r.width:0}x{r.height:0}, ppu {sheet[0].pixelsPerUnit:0})");
            }
            return sheet;
        }

        // ---- The wind ball ----

        private static void BuildBall()
        {
            if (ballBack != null && PixelArt.Alive(ballBack[0]) && PixelArt.Alive(groundRing[0]))
                return;
            int n = BallWidths.Length;
            ballBack = new Sprite[n][];
            ballFront = new Sprite[n][];
            ballWhole = new Sprite[n][];
            groundRing = new Sprite[n][];
            int count = 0;
            for (int v = 0; v < n; v++)
            {
                int w = BallWidths[v];
                int h = Even(w * BallSquash);
                var back = new PixelArt.Grid[BallFrames];
                var front = new PixelArt.Grid[BallFrames];
                var whole = new PixelArt.Grid[BallFrames];
                var ring = new PixelArt.Grid[BallFrames];
                for (int f = 0; f < BallFrames; f++)
                {
                    whole[f] = BallFrame(w, h, f);
                    Split(whole[f], out back[f], out front[f]);
                    ring[f] = RingFrame(w, Even(w * GroundSquash), f);
                }
                var middle = new Vector2(0.5f, 0.5f);
                ballBack[v] = PixelArt.Sheet(back, Palette, middle);
                ballFront[v] = PixelArt.Sheet(front, Palette, middle);
                ballWhole[v] = PixelArt.Sheet(whole, Palette, middle);
                groundRing[v] = PixelArt.Sheet(ring, Palette, middle);
                count += 4 * BallFrames;
            }
            if (ready.Add("wind ball"))
                GaleRiderPlugin.Log($"Gale Rider: visual 'wind ball' ready ({count} sprites: {BallFrames} frames x {n} sizes " +
                    $"({BallWidths[0]} to {BallWidths[n - 1]} px wide) x back, front, glow and floor ring, ppu {PixelArt.Ppu:0})");
        }

        private static int Even(float value) => Mathf.Max(2, Mathf.RoundToInt(value * 0.5f) * 2);

        // One frame of the spinning ball. Each arm is a crescent, brightest at its leading head
        // and thinning to its tail, curving in towards the middle; the back half is a shade
        // darker than the front (it's further away). A dashed rim spins with it. One loop of
        // frames turns it a third (one arm on), so it loops seamlessly.
        private static PixelArt.Grid BallFrame(int w, int h, int f)
        {
            var g = new PixelArt.Grid(w, h);
            float phase = (float)f / BallFrames;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f - w * 0.5f) / (w * 0.5f - 1f);
                    float v = (y + 0.5f - h * 0.5f) / (h * 0.5f - 1f);
                    float r = Mathf.Sqrt(u * u + v * v);
                    if (r > 0.97f || r < InnerRadius)
                        continue;
                    float turn = Mathf.Atan2(v, u) / (2f * Mathf.PI);
                    int index = 0;
                    // Where this pixel falls along an arm: 0 at its head, ArmLength at its tail.
                    float s = Frac(phase - turn * BallArms);
                    float strength = Mathf.Max(ArmStrength(s, r, OuterHead, 1f), ArmStrength(Frac(s + 0.5f), r, InnerHead, InnerStrength));
                    if (strength > 0f)
                        index = strength > 0.78f ? 5 : strength > 0.55f ? 4 : strength > 0.3f ? 3 : strength > 0.12f ? 2 : 0;
                    if (r >= 0.86f)
                    {
                        float dash = Frac(turn * RimDashes - phase * (RimDashes / BallArms));
                        if (dash < 0.14f)
                            index = Mathf.Max(index, 4);
                        else if (dash < 0.42f)
                            index = Mathf.Max(index, 3);
                    }
                    // Depth: the far half a shade darker.
                    if (v > 0.1f && index >= 3)
                        index--;
                    if (index > 0)
                        g.Set(x, y, index);
                }
            }
            return g;
        }

        // How strongly a pixel at arm position s and radius r belongs to a streak whose head
        // rides at radius head: the streak spirals inward from its head (ArmTwist of the radius
        // over its length) and tapers from a fat bright head to a thin dim tail.
        private static float ArmStrength(float s, float r, float head, float peak)
        {
            if (s >= ArmLength)
                return 0f;
            float t = s / ArmLength;
            float centre = head - ArmTwist * 0.5f * t;
            float width = StreakHeadWidth * (1f - t) + StreakTailWidth;
            float d = Mathf.Abs(r - centre);
            if (d >= width)
                return 0f;
            return peak * (1f - 0.85f * t) * (1f - 0.6f * d / width);
        }

        // The upper half (behind the wizard) and the lower half (in front), same size and pivot.
        private static void Split(PixelArt.Grid whole, out PixelArt.Grid back, out PixelArt.Grid front)
        {
            back = new PixelArt.Grid(whole.W, whole.H);
            front = new PixelArt.Grid(whole.W, whole.H);
            int half = whole.H / 2;
            for (int y = 0; y < whole.H; y++)
            {
                for (int x = 0; x < whole.W; x++)
                {
                    int index = whole.Get(x, y);
                    if (y >= half)
                        back.Set(x, y, index);
                    else
                        front.Set(x, y, index);
                }
            }
        }

        // The gust ring on the floor, in the manner of the game's wind rings (WindParticles):
        // three thin arcs chasing round, each a 2 px light head tapering to a 1 px shaded tail,
        // with gaps between. One loop of frames turns it a third, so it loops seamlessly.
        private static PixelArt.Grid RingFrame(int w, int h, int f)
        {
            var g = new PixelArt.Grid(w, h);
            float phase = (float)f / BallFrames;
            float rx = w * 0.5f - 0.5f, ry = h * 0.5f - 0.5f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f - w * 0.5f, py = y + 0.5f - h * 0.5f;
                    float u = px / rx, v = py / ry;
                    float r = Mathf.Sqrt(u * u + v * v);
                    if (r > 1f || r < 0.5f)
                        continue;
                    // Distance from the ring in pixels (the ellipse's local radius at this angle).
                    float local = Mathf.Sqrt(px * px + py * py) / r;
                    float off = (RingRadius - r) * local;
                    float s = Frac(phase - Mathf.Atan2(v, u) / (2f * Mathf.PI) * RingArcs);
                    if (s >= RingArcSpan)
                        continue;
                    float t = s / RingArcSpan;
                    float thickness = RingHeadWidth * (1f - t) + RingTailWidth;
                    if (off < -0.5f || off > thickness - 0.5f)
                        continue;
                    g.Set(x, y, t < 0.2f ? 4 : t < 0.6f ? 3 : 2);
                }
            }
            return g;
        }
        // ---- Wind streak (drawn pointing +x, the way the wizard rides) ----

        private static readonly string[][] StreakRows =
        {
            new[]
            {
                "....................",
                "....................",
                "....................",
                "...........mlw......",
                "....................",
                "....................",
                "....................",
            },
            new[]
            {
                "....................",
                "....................",
                "....................",
                ".......dmmllww......",
                "....................",
                "....................",
                "....................",
            },
            new[]
            {
                "....................",
                "....................",
                "....................",
                "...dmmmmlllwww......",
                "..d.................",
                "...dd...............",
                "....................",
            },
            new[]
            {
                "....................",
                "....................",
                "....................",
                "....dmmmmlll.lw.....",
                "...m................",
                "...d..m.............",
                "....dd..............",
            },
            new[]
            {
                "....................",
                "....................",
                "....................",
                ".....dm.mml...l.....",
                "....d...............",
                "....d..d............",
                ".....dd.............",
            },
            new[]
            {
                "....................",
                "....................",
                "....................",
                "......d...m....d....",
                "....................",
                ".....d..d...........",
                "....................",
            },
        };

        private static Sprite[] BuildStreak() => PixelArt.Sheet(StreakGrids(), Palette, new Vector2(0.5f, 0.5f));

        private static PixelArt.Grid[] StreakGrids()
        {
            var frames = new PixelArt.Grid[StreakRows.Length];
            for (int f = 0; f < frames.Length; f++)
            {
                frames[f] = new PixelArt.Grid(20, 7);
                frames[f].Stamp(StreakRows[f], ".odmlw", 0, 0);
            }
            return frames;
        }

        // ---- Gust puff (drawn pointing +x, the way the enemy is shoved) ----

        private static readonly float[] PuffRadius = { 3f, 4.8f, 6.2f, 7.2f, 7.8f };
        private static readonly float[] PuffThick = { 2.4f, 2.2f, 1.9f, 1.5f, 1.1f };
        private static readonly float[] PuffSpan = { 55f, 68f, 78f, 84f, 86f };

        private static Sprite[] BuildPuff() => PixelArt.Sheet(PuffGrids(), Palette, new Vector2(0.5f, 0.5f));

        private static PixelArt.Grid[] PuffGrids()
        {
            const int size = 20;
            const float c = size * 0.5f;
            var frames = new PixelArt.Grid[PuffRadius.Length];
            for (int f = 0; f < frames.Length; f++)
            {
                var g = new PixelArt.Grid(size, size);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - c + 2f, dy = y + 0.5f - c;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        // Breaking up into dashes in the last frames.
                        if (f >= 3 && Mathf.FloorToInt((a + 180f) / 14f) % 2 == 1)
                            continue;
                        // The main crescent: bright on its inside edge, dimmer to its tips.
                        if (Mathf.Abs(a) <= PuffSpan[f] && Mathf.Abs(d - PuffRadius[f]) <= PuffThick[f] * 0.5f + 0.3f)
                        {
                            float tip = Mathf.Abs(a) / PuffSpan[f];
                            int index = d < PuffRadius[f] ? 5 : 4;
                            if (tip > 0.72f)
                                index = 3;
                            if (f >= 3)
                                index--;
                            g.Plot(x, y, index);
                        }
                        // A smaller trailing crescent inside it (frames 1-3).
                        float inner = PuffRadius[f] - 2.6f;
                        if (f >= 1 && f <= 3 && Mathf.Abs(a) <= PuffSpan[f] * 0.6f && Mathf.Abs(d - inner) <= 0.7f)
                            g.Plot(x, y, f == 3 ? 2 : 3);
                    }
                }
                // The burst at the point of contact.
                if (f == 0)
                {
                    // A little four-point star.
                    g.Disc(c - 2f, c, 1.3f, 5);
                    for (int k = 2; k <= 3; k++)
                    {
                        g.Plot((int)(c - 2f) + k, (int)c, 4);
                        g.Plot((int)(c - 2f) - k, (int)c, 3);
                        g.Plot((int)(c - 2f), (int)c + k, 3);
                        g.Plot((int)(c - 2f), (int)c - k, 3);
                    }
                }
                else if (f == 1)
                    g.Disc(c - 2f, c, 1.2f, 4);
                frames[f] = g;
            }
            return frames;
        }

        // ---- The gather (and, backwards, the unravel) ----

        private static readonly float[] GatherRadius = { 21f, 16f, 11f, 7f, 4f };
        private const int GatherWisps = 4;
        private const float GatherSquash = 0.62f;

        private static Sprite[] BuildGather() => PixelArt.Sheet(GatherGrids(), Palette, new Vector2(0.5f, 0.5f));

        private static PixelArt.Grid[] GatherGrids()
        {
            const int w = 48, h = 36;
            const float cx = w * 0.5f, cy = h * 0.5f;
            var frames = new PixelArt.Grid[GatherRadius.Length + 1];
            for (int f = 0; f < GatherRadius.Length; f++)
            {
                var g = new PixelArt.Grid(w, h);
                float r = GatherRadius[f];
                for (int c = 0; c < GatherWisps; c++)
                {
                    float head = (c * 90f + f * 28f) * Mathf.Deg2Rad;
                    // A comma: a round head, a tail trailing behind it and spiralling outwards.
                    g.Disc(cx + Mathf.Cos(head) * r, cy + Mathf.Sin(head) * r * GatherSquash, 1.6f, 5);
                    for (int k = 1; k <= 7; k++)
                    {
                        float a = head - k * 9f * Mathf.Deg2Rad;
                        float rr = r + k * 0.5f;
                        float px = cx + Mathf.Cos(a) * rr, py = cy + Mathf.Sin(a) * rr * GatherSquash;
                        int index = k <= 2 ? 4 : k <= 4 ? 3 : 2;
                        if (k <= 3)
                            g.Disc(px, py, 1.1f, index);
                        else
                            g.PlotF(px, py, index);
                    }
                }
                frames[f] = g;
            }
            // The flash as the ball forms: a white core, a ring and eight short rays.
            var flash = new PixelArt.Grid(w, h);
            flash.Disc(cx, cy, 3.5f, 5, GatherSquash + 0.2f);
            for (int a = 0; a < 360; a += 4)
            {
                float rad = a * Mathf.Deg2Rad;
                flash.PlotF(cx + Mathf.Cos(rad) * 7f, cy + Mathf.Sin(rad) * 7f * GatherSquash, 4);
                flash.PlotF(cx + Mathf.Cos(rad) * 7.8f, cy + Mathf.Sin(rad) * 7.8f * GatherSquash, 3);
            }
            for (int ray = 0; ray < 8; ray++)
            {
                float rad = (ray * 45f + 22.5f) * Mathf.Deg2Rad;
                for (float d = 9f; d <= 13f; d += 0.5f)
                    flash.PlotF(cx + Mathf.Cos(rad) * d, cy + Mathf.Sin(rad) * d * GatherSquash, d < 11f ? 4 : 3);
            }
            frames[GatherRadius.Length] = flash;
            return frames;
        }

        private static float Frac(float value) => value - Mathf.Floor(value);
    }

    // The wind ball the wizard rides, following them. With the game's own ball (WindSlash/
    // WindBall0-5): the whole ball behind them and its lower half, see-through, in front, so they
    // ride inside it. With the drawn stand-in: its back half behind and front half in front. Either
    // way a fainter copy spins behind it from GlowLevel (brighter each level), and the gust ring
    // turns on the floor. Sized to the ball's hit radius and spinning faster the faster they go.
    public class WindBall
    {
        // Where the ball's middle sits above the wizard's feet (about their waist).
        private const float Lift = 0.5f;
        // The ball's width against the hit circle's: a touch inside it, so what it looks like
        // it touches it does hit.
        private const float Fill = 0.9f;
        private const float FloorLift = 0.02f;
        private const float FloorAlpha = 0.55f;
        // Frames a second of the spin: BaseFps at level 1, PerLevelFps more each level (the
        // game's ball has 6 frames to the drawn one's 8, so it's played at 3/4 of that).
        private const float BaseFps = 12f;
        private const float PerLevelFps = 2.2f;
        private const float GameFpsScale = 0.75f;
        // The glow: from GlowLevel up, a bigger, fainter copy behind, brighter each level.
        private const int GlowLevel = 3;
        private const float GlowScale = 1.14f;
        private const float GlowAlpha = 0.12f;
        private const float GlowPerLevel = 0.035f;
        private const int GlowFrameLead = 3;    // half a loop ahead of the core: a blur, not a copy
        private const float FrontAlpha = 0.9f;  // the drawn front half lets the wizard's legs show through a bit
        private const float GameFrontAlpha = 0.45f; // the game's ball is solid: its front half is see-through

        private FxHandle back, front, glow, floor;
        private Sprite[] game;
        private int variant = -1;
        private static bool loggedSorting;

        public float Width { get; private set; }
        public bool UsesGameSprites => game != null;

        public static WindBall Create(Player player, object owner)
        {
            var ball = new WindBall();
            SpriteRenderer body = player.spriteRenderer;
            ball.game = GaleArt.GameBall;
            Sprite[] gameFront = ball.game != null ? GaleArt.GameBallFront(ball.game) : null;
            if (gameFront == null)
                ball.game = null;
            FxSprite backFx = Make(ball.game ?? GaleArt.BallBack(0), player, owner, body, -1);
            FxSprite frontFx = Make(gameFront ?? GaleArt.BallFront(0), player, owner, body, 1);
            FxSprite glowFx = Make(ball.game ?? GaleArt.BallWhole(0), player, owner, body, -2);
            FxSprite floorFx = FxSprite.Spawn(GaleArt.GroundRing(0), player.transform.position, "TargetLineGround", 0);
            if (floorFx != null)
            {
                floorFx.Follow(player.transform, new Vector2(0f, FloorLift));
                floorFx.loop = true;
                floorFx.owner = owner;
                floorFx.alpha = FloorAlpha;
            }
            if (glowFx != null)
            {
                glowFx.alpha = 0f;
                glowFx.Skip(GlowFrameLead);
            }
            if (frontFx != null)
                frontFx.alpha = ball.game != null ? GameFrontAlpha : FrontAlpha;
            ball.back = new FxHandle(backFx);
            ball.front = new FxHandle(frontFx);
            ball.glow = new FxHandle(glowFx);
            ball.floor = new FxHandle(floorFx);
            if (!loggedSorting && body != null)
            {
                loggedSorting = true;
                GaleRiderPlugin.Log($"Gale Rider: wind ball drawn on the wizard's layer '{body.sortingLayerName}' (order {body.sortingOrder} -1 behind, +1 in front), " +
                    "floor ring on 'TargetLineGround'");
            }
            return ball;
        }

        private static FxSprite Make(Sprite[] frames, Player player, object owner, SpriteRenderer body, int order)
        {
            FxSprite fx = FxSprite.Spawn(frames, player.transform.position, "Actor", 0);
            if (fx == null)
                return null;
            fx.Follow(player.transform, new Vector2(0f, Lift));
            fx.loop = true;
            fx.owner = owner;
            fx.sortRef = body;
            fx.sortOffset = order;
            return fx;
        }

        // Every frame of the ride: size from the ball's hit radius, spin and glow from the level.
        public void Update(float radius, int level, float lookScale)
        {
            Width = 2f * radius * Fill * lookScale;
            FxSprite b = back.Get, f = front.Get, g = glow.Get, fl = floor.Get;
            float fps = BaseFps + PerLevelFps * level;
            int best = 0;
            for (int v = 1; v < GaleArt.BallWidths.Length; v++)
            {
                if (Mathf.Abs(GaleArt.BallWidths[v] / PixelArt.Ppu - Width) < Mathf.Abs(GaleArt.BallWidths[best] / PixelArt.Ppu - Width))
                    best = v;
            }
            if (best != variant)
            {
                variant = best;
                if (game == null)
                {
                    b?.SetFrames(GaleArt.BallBack(best));
                    f?.SetFrames(GaleArt.BallFront(best));
                    g?.SetFrames(GaleArt.BallWhole(best));
                }
                fl?.SetFrames(GaleArt.GroundRing(best));
            }
            float floorScale = Width / (GaleArt.BallWidths[best] / PixelArt.Ppu);
            float scale = floorScale;
            Vector2 middle = new Vector2(0f, Lift);
            Vector2 shift = Vector2.zero, glowShift = Vector2.zero;
            if (game != null)
            {
                // The game's frames, scaled to the width and moved so their middle is the ball's.
                scale = Width / GaleArt.GameBallWidth;
                fps *= GameFpsScale;
                shift = GameSprites.PivotShift(game[0], GaleArt.GameBallCentre, scale);
                glowShift = GameSprites.PivotShift(game[0], GaleArt.GameBallCentre, scale * GlowScale);
            }
            if (b != null)
            {
                b.size = scale;
                b.fps = fps;
                b.followOffset = middle + shift;
            }
            if (f != null)
            {
                f.size = scale;
                f.fps = fps;
                f.followOffset = middle + shift;
            }
            if (fl != null)
            {
                // The floor ring turns at half the ball's pace.
                fl.size = floorScale;
                fl.fps = (BaseFps + PerLevelFps * level) * 0.5f;
            }
            if (g != null)
            {
                g.size = scale * GlowScale;
                g.fps = fps;
                g.followOffset = middle + glowShift;
                g.alpha = level >= GlowLevel ? GlowAlpha + GlowPerLevel * (level - GlowLevel) : 0f;
            }
        }

        public void Stop()
        {
            back.Get?.Stop();
            front.Get?.Stop();
            glow.Get?.Stop();
            floor.Get?.Stop();
            back = front = glow = floor = default(FxHandle);
        }
    }
}