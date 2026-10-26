using System.Collections.Generic;
using UnityEngine;

namespace WoLCyclone
{
    // Cyclone's twister, in the game's own wind style: a funnel of stacked spinning bands, each
    // drawn like the game's spinning gust ring (WindParticles2: brushed strokes going round, light
    // on the near rim, the dark inner wall showing across the mouth, tails flicking off; its three
    // flat wind tones, no outline), 6-frame loops at native pixel size, that flare and darken
    // through three shades as it grows, swaying like a snaking tornado, with the game's wind wisps
    // peeling off its sides; dust puffs, rocks and leaves orbiting it at a radius that grows with it
    // (behind the funnel on the far side of their orbit, in front on the near side); and a ring of
    // dust whirling round its base. When it ends it either collapses (let go, dash, or the cast
    // ending) or blows apart in the game's spinning gust ring with its wind slashes racing out and
    // the debris flung outward (the burst). Drawn stand-ins in the same style replace any of the
    // game's sprites that aren't loaded.
    //
    // Visual only. One object per cast, destroyed when its ending has played (or with the level).
    internal class CycloneFunnel : MonoBehaviour
    {
        // Funnel shape, in units (drawn at the game's pixel size), small to full hurricane.
        private const float TopWidthStart = 0.9f;
        private const float TopWidthEnd = 4f;
        private const float BottomWidthStart = 0.45f;
        private const float BottomWidthEnd = 0.9f;
        private const float HeightStart = 0.7f;
        private const float HeightEnd = 3.6f;
        private const float Flare = 1.6f;            // how sharply it widens toward the top
        private const int MinBands = 3;
        private const int MaxBands = 7;
        private const int MaxBandPx = 96;            // wider bands are drawn at this and scaled up
        private const int BandFrames = 6;
        private const float BandFlatten = 0.42f;     // band height / width (a ring seen at the game's angle)
        private const float BandOverlap = 0.6f;      // each band reaches this far (of the spacing) up behind the next
        private const int MaxBandHeightPx = 28;
        private const int BandVariants = 3;          // differently stroked bands, so neighbours don't match
        private const float BandInner = 0.52f;       // the ring's hole, and its three lanes of strokes
        private const float BandLane1 = 0.66f;
        private const float BandLane2 = 0.81f;
        private const float BandOuter = 0.95f;
        private const float BandFpsSlow = 10f;       // spin, small to full (the bottom spins faster)
        private const float BandFpsFast = 22f;
        private const float SwayAmplitude = 0.18f;   // units at the top
        private const float SwayRate = 2.2f;         // radians a second
        private const float GaleAt = 0.4f;           // progress where it darkens to the second shade...
        private const float StormAt = 0.75f;         // ...and to storm grey
        // Wisps peeling off its sides
        private const float WispIntervalSlow = 0.35f; // seconds between wisps, small to full
        private const float WispIntervalFast = 0.12f;
        private const float WispLife = 0.4f;
        private const int WispWidth = 12, WispHeight = 8, WispFrames = 5;
        // Debris orbiting it
        private const int MinDebris = 2;
        private const int MaxDebris = 12;
        private const float DebrisSpinSlow = 3f;     // radians a second, small to full
        private const float DebrisSpinFast = 8f;
        private const float DebrisReach = 1.2f;      // orbit radius, times the funnel's half width at that height
        private const float DebrisFlatten = 0.3f;    // orbits are ellipses seen at the game's angle
        private const float DebrisFps = 10f;
        // Dust ring at the base
        private const float RingWidthStart = 1f;     // units
        private const float RingWidthEnd = 4.4f;
        private const float RingFlatten = 0.38f;
        private const int RingFrames = 6;
        private const float RingFps = 12f;
        private const int RingPuffs = 7;
        private const int MaxRingPx = 96;
        // Endings
        private const float CollapseTime = 0.4f;
        private const float BlowTime = 0.3f;
        private const float BlowWiden = 1.8f;
        // The burst's gust ring and slashes
        private const int GustFrames = 8;
        private const float GustLife = 0.4f;
        private const float GustRingPx = 86f;        // across the ring in the game's AirSpinnerBurst frames
        private const float GustFlatten = 0.6f;
        private const int GustMinPx = 32, GustMaxPx = 128, GustStepPx = 8;
        private const int GustStreaks = 10;
        private const float StreakLife = 0.35f;
        private const float StreakSpeed = 2.6f;      // times the burst radius, a second

        // Funnel shades, from the game's wind tones (index 1 deepest, then dark to light): the
        // game's own, then greyed and darkened in two steps for the gale and the storm.
        private static readonly Color32[][] Shades =
        {
            PixelFx.Air,
            new[]
            {
                new Color32(0, 0, 0, 0),
                new Color32(0x5c, 0x6b, 0x6e, 0xff),
                new Color32(0x7a, 0x8a, 0x8e, 0xff),
                new Color32(0x9f, 0xb8, 0xbb, 0xff),
                new Color32(0xbd, 0xd5, 0xd6, 0xff),
                new Color32(0xbd, 0xd5, 0xd6, 0xff),
            },
            new[]
            {
                new Color32(0, 0, 0, 0),
                new Color32(0x4b, 0x56, 0x59, 0xff),
                new Color32(0x66, 0x73, 0x78, 0xff),
                new Color32(0x87, 0x98, 0xa0, 0xff),
                new Color32(0xa8, 0xba, 0xbf, 0xff),
                new Color32(0xa8, 0xba, 0xbf, 0xff),
            },
        };

        private static readonly Dictionary<int, Sprite[]> bandCache = new Dictionary<int, Sprite[]>();
        private static readonly Dictionary<int, Sprite[]> ringCache = new Dictionary<int, Sprite[]>();
        private static readonly Dictionary<int, Sprite[]> gustCache = new Dictionary<int, Sprite[]>();
        private static Sprite[] wispArt;
        private static bool broken, loggedError;

        private class Band
        {
            public SpriteRenderer sr;
            public float phase;
        }

        private class Orbiter
        {
            public SpriteRenderer sr;
            public Sprite[] frames;
            public Color tint;
            public float angle, speed, height, reach, phase;
        }

        private readonly List<Band> bands = new List<Band>();
        private readonly List<Orbiter> orbiters = new List<Orbiter>();
        private SpriteRenderer ring;
        private Vector2 center;
        private float progress;
        private int ending;  // 0 spinning, 1 collapsing, 2 blowing apart
        private float endAge;
        private float nextWisp;

        // Null if it can't be drawn (CycloneLook then falls back to the game's twister).
        public static CycloneFunnel Create(Vector2 position, Player player)
        {
            if (broken)
                return null;
            PixelFx.Prepare(player);
            GameObject go = null;
            try
            {
                go = new GameObject("CycloneFunnel");
                go.transform.position = position;
                CycloneFunnel funnel = go.AddComponent<CycloneFunnel>();
                for (int i = 0; i < MaxBands; i++)
                    funnel.bands.Add(new Band { sr = funnel.Child("Band" + i, PixelFx.ActorLayer), phase = Random.value * BandFrames });
                funnel.ring = funnel.Child("DustRing", PixelFx.GroundLayer);
                funnel.center = position;
                funnel.Draw(0f, 1f, 1f, 1f);
                PixelFx.Effect("funnel", () => $"{MinBands}-{MaxBands} gust-ring bands x {BandFrames} frames in {Shades.Length} shades of the game's wind tones, ppu {PixelFx.Ppu}");
                PixelFx.Effect("dust ring", () => $"{RingFrames} frames, {funnel.ring.sprite.rect.width} px wide to start, ppu {PixelFx.Ppu}");
                return funnel;
            }
            catch (System.Exception e)
            {
                broken = true;
                if (go != null)
                    Destroy(go);
                PixelFx.Log($"visual 'funnel' unavailable, using the game's twister instead: {e.Message}");
                return null;
            }
        }

        private SpriteRenderer Child(string name, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = layer;
            sr.enabled = false;
            return sr;
        }

        public void Set(Vector2 position, float growth)
        {
            if (ending != 0)
                return;
            center = position;
            progress = growth;
        }

        // Let go (or cut short): it collapses. Burst: it blows apart with a gust ring of `burstRadius`.
        public void End(bool blow, float burstRadius)
        {
            if (ending != 0)
                return;
            ending = blow ? 2 : 1;
            endAge = 0f;
            Vector2 middle = center;
            PixelFx.Effect(blow ? "funnel blowing apart" : "funnel collapse", () =>
            {
                int flung = 0;
                foreach (Orbiter o in orbiters)
                {
                    if (!o.sr.enabled)
                        continue;
                    Vector2 at = o.sr.transform.position;
                    Vector2 away = at - middle;
                    away = away.sqrMagnitude > 0.001f ? away.normalized : Random.insideUnitCircle.normalized;
                    o.sr.enabled = false;
                    FxSprite fx = FxSprite.Spawn(o.frames, at, blow ? 0.5f : 0.4f, DebrisFps);
                    if (fx == null)
                        continue;
                    fx.Tint(o.tint);
                    if (blow)
                        fx.Drift(away * Random.Range(7f, 11f) + Vector2.up * 1.5f, 2.5f).Fade(0f, 0.15f).SortWithActors();
                    else
                        fx.Drift(away * 1.2f + Vector2.down * 2.5f, 1f).Fade(0f, 0.2f).SortWithActors();
                    flung++;
                }
                return $"{(blow ? BlowTime : CollapseTime)} s, {flung} debris {(blow ? "flung out" : "dropped")}";
            });
            if (!blow)
                return;
            PixelFx.Effect("gust ring", () =>
            {
                float wantPx = burstRadius * 2f * PixelFx.Ppu;
                Sprite[] game = GameArt.GustRing();
                if (game != null)
                {
                    FxSprite spinner = FxSprite.Spawn(game, middle, GustLife);
                    if (spinner == null)
                        return null;
                    float size = wantPx / GustRingPx;
                    spinner.Scale(size * 0.9f, size).Fade(0f, 0.12f).Sort(PixelFx.EffectLayer, 0);
                    return "game's AirSpinnerBurst, 8 frames";
                }
                Sprite[] frames = Gust(wantPx, out int px);
                FxSprite fx = FxSprite.Spawn(frames, middle, GustLife);
                if (fx == null)
                    return null;
                float scale = wantPx / px;
                fx.Scale(scale, scale).Fade(0f, 0.12f).Sort(PixelFx.EffectLayer, 0);
                return $"drawn gust ring, {frames.Length} frames, {px} px, ppu {PixelFx.Ppu}";
            });
            PixelFx.Effect("gust streaks", () =>
            {
                Sprite[] slash = PixelFx.GameSlash("Med");
                int made = 0;
                for (int i = 0; i < GustStreaks; i++)
                {
                    float a = (i + Random.value * 0.5f) / GustStreaks * Mathf.PI * 2f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var flat = new Vector2(dir.x, dir.y * GustFlatten);
                    FxSprite fx = FxSprite.Spawn(slash ?? PixelFx.Crescent(dir), middle + flat * burstRadius * 0.35f, StreakLife);
                    if (fx == null)
                        break;
                    if (slash != null)
                        fx.Rotate(PixelFx.SlashAngle(dir));
                    fx.Drift(flat * burstRadius * StreakSpeed, 2.5f).Fade(0f, 0.1f).Sort(PixelFx.EffectLayer, 1);
                    made++;
                }
                if (made == 0)
                    return null;
                return slash != null ? $"{made} x game's WindSlashMed0-6" : $"{made} drawn crescents x {PixelFx.CrescentFrames} frames, ppu {PixelFx.Ppu}";
            });
        }

        private void Update()
        {
            try
            {
                float dt = Time.deltaTime;
                if (ending == 0)
                {
                    Draw(dt, 1f, 1f, 1f);
                    return;
                }
                endAge += dt;
                float t = endAge / (ending == 2 ? BlowTime : CollapseTime);
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                if (ending == 2)
                    Draw(dt, Mathf.Lerp(1f, BlowWiden, t), Mathf.Lerp(1f, 1.15f, t), 1f - t);
                else
                    Draw(dt, Mathf.Lerp(1f, 0.4f, t), Mathf.Lerp(1f, 0.3f, t), 1f - t);
            }
            catch (System.Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    PixelFx.Log($"the drawn funnel failed, removing it: {e.Message}");
                }
                Destroy(gameObject);
            }
        }

        private float WidthAt(float t, float bottom, float top) => bottom + (top - bottom) * Mathf.Pow(t, Flare);

        private float SwayAt(float t, float time) => Mathf.Sin(time * SwayRate + t * 4.8f) * SwayAmplitude * t * (0.4f + progress);

        private static int Frame(float time, float fps, float phase, int count)
        {
            int f = (int)(time * fps + phase) % count;
            return f < 0 ? f + count : f;
        }

        private void Draw(float dt, float widthMul, float heightMul, float alpha)
        {
            float p = progress;
            float time = Time.time;
            float ppu = PixelFx.Ppu;
            float top = Mathf.Lerp(TopWidthStart, TopWidthEnd, p) * widthMul;
            float bottom = Mathf.Lerp(BottomWidthStart, BottomWidthEnd, p) * widthMul;
            float height = Mathf.Lerp(HeightStart, HeightEnd, p) * heightMul;
            int count = MinBands + Mathf.RoundToInt((MaxBands - MinBands) * p);
            int shade = p < GaleAt ? 0 : p < StormAt ? 1 : 2;
            int order = PixelFx.SortOrder(center.y);
            float fade = PixelFx.Steps(alpha);
            var color = new Color(1f, 1f, 1f, fade);

            // Bands, bottom up, each tall enough to overlap well into the one above, whose bottom
            // tucks behind it (lower bands draw in front), so the funnel reads as one coil.
            float step = count > 1 ? height / (count - 1) : height;
            int minHeight = Mathf.RoundToInt(step * ppu * (1f + BandOverlap));
            for (int i = 0; i < bands.Count; i++)
            {
                Band band = bands[i];
                if (i >= count)
                {
                    band.sr.enabled = false;
                    continue;
                }
                float t = count > 1 ? (float)i / (count - 1) : 0f;
                float width = WidthAt(t, bottom, top);
                int px = Mathf.Clamp(Mathf.RoundToInt(width * ppu * 0.5f) * 2, 6, MaxBandPx);
                int tall = Mathf.Clamp(Mathf.Max(Mathf.RoundToInt(px * BandFlatten), minHeight), 5, Mathf.Min(MaxBandHeightPx, px));
                Sprite[] frames = BandArt(px, tall, shade, i == count - 1, i % BandVariants);
                float fps = Mathf.Lerp(BandFpsSlow, BandFpsFast, p) * (1f + 0.4f * (1f - t));
                band.sr.sprite = frames[Frame(time, fps, band.phase, BandFrames)];
                float scale = Mathf.Max(1f, width * ppu / px);
                band.sr.transform.localScale = new Vector3(scale, scale, 1f);
                float bandHeight = band.sr.sprite.rect.height / ppu * scale;
                band.sr.transform.position = PixelFx.Snap(center + new Vector2(SwayAt(t, time), t * height + bandHeight * 0.5f));
                band.sr.sortingOrder = order + MaxBands - i;
                band.sr.color = color;
                band.sr.enabled = true;
            }

            // Debris: more of it as it grows, whirling faster; and wisps peeling off its sides.
            if (ending == 0)
            {
                if (time >= nextWisp)
                {
                    nextWisp = time + Mathf.Lerp(WispIntervalSlow, WispIntervalFast, p);
                    Wisp(order, bottom, top, height, time);
                }
                int want = Mathf.RoundToInt(Mathf.Lerp(MinDebris, MaxDebris, p));
                while (orbiters.Count < want)
                    AddOrbiter();
            }
            float spin = Mathf.Lerp(DebrisSpinSlow, DebrisSpinFast, p);
            foreach (Orbiter o in orbiters)
            {
                if (!o.sr.enabled)
                    continue;
                o.angle += o.speed * spin * dt;
                float reach = WidthAt(o.height, bottom, top) * 0.5f * DebrisReach * o.reach + 0.1f;
                float sin = Mathf.Sin(o.angle);
                var at = center + new Vector2(Mathf.Cos(o.angle) * reach + SwayAt(o.height, time),
                    o.height * height + 0.15f + sin * reach * DebrisFlatten);
                o.sr.transform.position = PixelFx.Snap(at);
                // Behind the funnel on the far side of the orbit, in front on the near side.
                o.sr.sortingOrder = sin > 0f ? order : order + MaxBands + 2;
                o.sr.sprite = o.frames[Frame(time, DebrisFps, o.phase, o.frames.Length)];
                o.sr.color = new Color(o.tint.r, o.tint.g, o.tint.b, o.tint.a * fade);
            }

            // The dust ring round its base.
            float ringWidth = Mathf.Lerp(RingWidthStart, RingWidthEnd, p) * (ending == 2 ? widthMul * widthMul : widthMul);
            int ringPx = Mathf.Clamp(Mathf.RoundToInt(ringWidth * ppu / 4f) * 4, 12, MaxRingPx);
            Sprite[] ringFrames = RingArt(ringPx);
            ring.sprite = ringFrames[Frame(time, RingFps, 0f, RingFrames)];
            float ringScale = Mathf.Max(1f, ringWidth * ppu / ringPx);
            ring.transform.localScale = new Vector3(ringScale, ringScale, 1f);
            ring.transform.position = PixelFx.Snap(center);
            ring.sortingOrder = order;
            ring.color = color;
            ring.enabled = true;
        }

        private void AddOrbiter()
        {
            Sprite[] frames = PixelFx.RandomDebris(out Color tint);
            var o = new Orbiter
            {
                sr = Child("Debris" + orbiters.Count, PixelFx.ActorLayer),
                frames = frames,
                tint = tint,
                angle = Random.value * Mathf.PI * 2f,
                speed = Random.Range(0.8f, 1.3f),
                height = Random.Range(0.05f, 0.95f),
                reach = Random.Range(0.9f, 1.25f),
                phase = Random.value * 4f
            };
            o.sr.sprite = frames[0];
            o.sr.color = tint;
            o.sr.enabled = true;
            orbiters.Add(o);
            PixelFx.Effect("orbiting debris", () => $"dust puffs, rocks and leaves (game's Dust and Rocks when loaded), up to {MaxDebris}, ppu {PixelFx.Ppu}");
        }

        // One band of the funnel, `width` x `height` px (plus room round it for the tails), drawn
        // like the game's spinning gust ring: strokes of uneven length going round in three lanes,
        // spinning a step each frame; light on the near rim and the left, the body tone elsewhere,
        // shaded on the right; across the mouth the dark inner wall shows (and, on the open top
        // band, between the far strokes too); two or three tapered tails flick off tangentially.
        private static Sprite[] BandArt(int width, int height, int shade, bool top, int variant)
        {
            int key = (((width * 32 + height) * 8 + shade * 2 + (top ? 1 : 0)) * BandVariants) + variant;
            if (bandCache.TryGetValue(key, out Sprite[] cached) && PixelFx.Alive(cached))
                return cached;
            int pad = Mathf.Max(2, width / 6);
            int w = width + pad * 2, h = height + 2;
            float halfW = width * 0.5f, halfH = height * 0.5f;
            Sprite[] frames = PixelFx.Strip("band" + key, w, h, BandFrames, Shades[shade], (f, x, y) =>
            {
                float u = (x + 0.5f - w * 0.5f) / halfW;
                float v = (y + 0.5f - h * 0.5f) / halfH;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Atan2(v, u) / (Mathf.PI * 2f);
                return BandPixel(u, v, r, a, f, variant, top);
            }, new Vector2(0.5f, 0.5f));
            bandCache[key] = frames;
            return frames;
        }

        private static int BandPixel(float u, float v, float r, float a, int f, int variant, bool top)
        {
            if (r > BandOuter)
            {
                // Tails: tapered strokes leaving the rim, curving outward as they go.
                for (int k = 0; k < 3; k++)
                {
                    float start = PixelFx.Frac(k / 3f + variant * 0.17f + (float)f / BandFrames);
                    float along = PixelFx.Frac(a - start);
                    if (along > 0.14f)
                        continue;
                    float t = along / 0.14f;
                    if (Mathf.Abs(r - (BandOuter + 0.22f * t)) < 0.07f * (1f - t) + 0.015f)
                        return t < 0.45f ? 4 : 3;
                }
                return 0;
            }
            if (r < BandInner)
                return 0;
            int lane = r < BandLane1 ? 0 : r < BandLane2 ? 1 : 2;
            int strokes = 3 + lane + (variant % 2);
            float pos = a * strokes + lane * 0.37f + variant * 0.21f - (float)f / BandFrames;
            int id = ((Mathf.FloorToInt(pos) % strokes) + strokes) % strokes;
            bool stroke = PixelFx.Frac(pos) < 0.5f + 0.35f * Hash(lane * 97 + id * 13 + variant * 7);
            if (v > 0.05f)
            {
                // The far side: the inner wall in shade, strokes passing over the rest.
                if (lane == 0)
                    return 2;
                if (!stroke)
                    return top ? 2 : 0;
                return u < -0.3f ? 4 : 3;
            }
            if (!stroke)
                return lane == 2 ? 0 : 2;
            if (u > 0.45f && lane >= 1)
                return 3;
            return lane == 2 || u < 0f ? 4 : 3;
        }

        // Drawn stand-in for the game's wind wisps: a curl peeling off to the right (flipped for
        // the left), stretching out, hooking upward, then thinning from the tail.
        private static Sprite[] WispArt()
        {
            if (PixelFx.Alive(wispArt))
                return wispArt;
            float[] length = { 4f, 7f, 9f, 10f, 10.5f };
            float[] curl = { 0.5f, 1.5f, 3f, 4.5f, 5f };
            const int samples = 40;
            var grid = new int[WispFrames, WispWidth, WispHeight];
            for (int f = 0; f < WispFrames; f++)
            {
                for (int i = 0; i <= samples; i++)
                {
                    float s = (float)i / samples;
                    if (f >= 3 && s < (f - 2) * 0.3f)
                        continue;
                    int px = (int)(s * length[f]);
                    int py = (int)(1.5f + curl[f] * s * s * s);
                    if (px >= WispWidth || py >= WispHeight)
                        continue;
                    int c = s > 0.6f ? 4 : s > 0.25f ? 3 : 2;
                    grid[f, px, py] = Mathf.Max(grid[f, px, py], c);
                    if (s < 0.35f && f < 3 && py > 0)
                        grid[f, px, py - 1] = Mathf.Max(grid[f, px, py - 1], 2);
                }
            }
            wispArt = PixelFx.Strip("wisp", WispWidth, WispHeight, WispFrames, PixelFx.Air, (f, x, y) =>
                x >= 0 && y >= 0 && x < WispWidth && y < WispHeight ? grid[f, x, y] : 0, new Vector2(0f, 0.25f));
            return wispArt;
        }

        // Spins off a wisp from a random height on one side.
        private void Wisp(int order, float bottom, float top, float height, float time)
        {
            PixelFx.Effect("funnel wisps", () =>
            {
                float t = Random.Range(0.2f, 1f);
                float side = Random.value < 0.5f ? -1f : 1f;
                Sprite[] game = GameArt.WindWisps();
                // The game's wisps are centred in their tiles, so start them a little further out.
                float reach = WidthAt(t, bottom, top) * 0.42f + (game != null ? 0.5f : 0f);
                var at = center + new Vector2(side * reach + SwayAt(t, time), t * height + 0.2f);
                FxSprite fx = FxSprite.Spawn(game ?? WispArt(), at, WispLife);
                if (fx == null)
                    return null;
                fx.Drift(new Vector2(side * 1.6f, 0.5f), 1.5f).Flip(side < 0f).Fade(0f, 0.12f)
                    .Sort(PixelFx.ActorLayer, order + MaxBands + 1);
                if (progress >= StormAt)
                    fx.Tint(new Color(0.8f, 0.85f, 0.88f, 1f));
                return game != null ? "game's MiscParticleEffects wind wisps, 5 frames" : $"drawn wisp, {WispFrames} frames, {WispWidth}x{WispHeight} px, ppu {PixelFx.Ppu}";
            });
        }

        // A repeatable 0-1 noise value for an integer.
        private static float Hash(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483648f;
        }

        // The dust ring: an ellipse of puffs churning round the base, lighter where they billow.
        private static Sprite[] RingArt(int width)
        {
            if (ringCache.TryGetValue(width, out Sprite[] cached) && PixelFx.Alive(cached))
                return cached;
            int w = width;
            int h = Mathf.Max(6, Mathf.RoundToInt(width * RingFlatten));
            Sprite[] frames = PixelFx.Strip("ring" + width, w, h, RingFrames, PixelFx.Dust, (f, x, y) =>
            {
                float u = (x + 0.5f - w * 0.5f) / (w * 0.5f);
                float v = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                float r = Mathf.Sqrt(u * u + v * v);
                if (r > 1f || r < 0.58f)
                    return 0;
                float q = (r - 0.58f) / 0.42f;
                float along = PixelFx.Frac(Mathf.Atan2(v, u) / (Mathf.PI * 2f) * RingPuffs - (float)f / RingFrames);
                float puff = 1f - Mathf.Abs(along - 0.5f) * 2f;
                if (Mathf.Abs(q - 0.5f) > 0.2f + 0.32f * puff)
                    return 0;
                if (v < 0f && q > 0.75f)
                    return 2;
                return puff > 0.72f ? 4 : puff > 0.4f ? 3 : 2;
            }, new Vector2(0.5f, 0.5f));
            ringCache[width] = frames;
            return frames;
        }

        // Drawn stand-in for the game's gust ring: a ring of wind racing outward, thinning and
        // breaking into arcs, with wisps trailing inside it; light on its leading edge.
        private static Sprite[] Gust(float wantPx, out int px)
        {
            px = Mathf.Clamp(Mathf.RoundToInt(wantPx / GustStepPx) * GustStepPx, GustMinPx, GustMaxPx);
            if (gustCache.TryGetValue(px, out Sprite[] cached) && PixelFx.Alive(cached))
                return cached;
            int w = px, h = Mathf.RoundToInt(px * GustFlatten);
            float[] radius = { 0.25f, 0.42f, 0.56f, 0.68f, 0.78f, 0.86f, 0.93f, 0.98f };
            float[] thick = { 0.16f, 0.14f, 0.12f, 0.1f, 0.08f, 0.065f, 0.05f, 0.04f };
            float[] gaps = { 0f, 0f, 0f, 0f, 0.12f, 0.25f, 0.38f, 0.5f };
            Sprite[] frames = PixelFx.Strip("gust" + px, w, h, GustFrames, PixelFx.Air, (f, x, y) =>
            {
                float u = (x + 0.5f - w * 0.5f) / (w * 0.5f);
                float v = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                float r = Mathf.Sqrt(u * u + v * v);
                float turn = Mathf.Atan2(v, u) / (Mathf.PI * 2f);
                float inner = radius[f] - thick[f] * 0.5f;
                if (r < inner || r > radius[f] + thick[f] * 0.5f)
                {
                    bool wisp = f >= 1 && f <= 5 && r < inner && r > radius[f] - thick[f] * 2.2f &&
                        PixelFx.Frac(turn * 20f + f * 0.1f) < 0.25f;
                    return wisp ? 2 : 0;
                }
                if (PixelFx.Frac(turn * 12f + f * 0.05f) < gaps[f])
                    return 0;
                float d = (r - inner) / thick[f];
                return d > 0.66f ? 4 : d > 0.33f ? 3 : 2;
            }, new Vector2(0.5f, 0.5f));
            gustCache[px] = frames;
            return frames;
        }
    }
}
