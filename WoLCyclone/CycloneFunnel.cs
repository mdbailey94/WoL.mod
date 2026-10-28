using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace WoLCyclone
{
    // Cyclone's twister, in the game's own wind style: one tapered funnel, narrow at the ground and
    // flaring open at the top, its body wound with spiral stripes that wrap round it and scroll
    // (6-frame loop at native pixel size), lit from the upper left with its right side in shade,
    // the dark throat showing in the open mouth; drawn in the game's three flat wind tones, no
    // outline. It grows taller and wider and darkens through three shades, sways like a snaking
    // tornado (the picture is cut into slices that lean with it), with the game's wind wisps
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
        private const float TopWidthEnd = 3.2f;
        private const float BottomWidthStart = 0.35f;
        private const float BottomWidthEnd = 0.8f;
        private const float HeightStart = 0.7f;
        private const float HeightEnd = 4.4f;
        private const float Flare = 1.6f;            // how sharply it widens toward the top
        private const int Slices = 6;                // horizontal slices of the one funnel picture, so it can lean and sway
        private const int FunnelFrames = 6;
        private const int MaxFunnelPx = 128;         // wider or taller is drawn at this and scaled up
        private const int MaxFunnelHeightPx = 112;
        private const int PxStep = 4;                // picture sizes step by this, so it's not redrawn every frame as it grows
        private const float MouthFlatten = 0.28f;    // the open top: an ellipse this tall for its width
        private const float SpiralTurns = 3.2f;      // how many times a stripe winds round from tip to mouth
        private const float StripeFill = 0.55f;      // how much of each winding is the lit stripe
        private const float SpinFpsSlow = 8f;        // the stripes' scroll, small to full
        private const float SpinFpsFast = 18f;
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

        private static readonly Dictionary<string, Sprite[][]> funnelCache = new Dictionary<string, Sprite[][]>();
        private static readonly Dictionary<int, Sprite[]> ringCache = new Dictionary<int, Sprite[]>();
        private static readonly Dictionary<int, Sprite[]> gustCache = new Dictionary<int, Sprite[]>();
        private static Sprite[] wispArt;
        private static bool broken, loggedError;

        private class Band
        {
            public SpriteRenderer sr;
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
                for (int i = 0; i < Slices; i++)
                    funnel.bands.Add(new Band { sr = funnel.Child("Slice" + i, PixelFx.ActorLayer) });
                funnel.ring = funnel.Child("DustRing", PixelFx.GroundLayer);
                funnel.center = position;
                funnel.Draw(0f, 1f, 1f, 1f);
                PixelFx.Effect("funnel", () => CyclonePlugin.DrawnTwister && DrawnSet(1f) >= 0
                    ? $"hand-drawn twister, {DrawnNames.Length} sizes x {drawn[0].Length} frames, in {Slices} leaning slices, ppu {PixelFx.Ppu}"
                    : $"one spiral-striped funnel in {Slices} leaning slices x {FunnelFrames} frames in {Shades.Length} shades of the game's wind tones, ppu {PixelFx.Ppu}");
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
            int shade = p < GaleAt ? 0 : p < StormAt ? 1 : 2;
            int order = PixelFx.SortOrder(center.y);
            float fade = PixelFx.Steps(alpha);
            var color = new Color(1f, 1f, 1f, fade);

            // The funnel: one picture, drawn at (or near) native size and scaled the rest of the
            // way, cut into horizontal slices that each lean with the sway at their height, so the
            // whole column snakes while its stripes run unbroken from tip to mouth.
            Sprite[][] art;
            float scaleX, scaleY;
            int drawnSet = CyclonePlugin.DrawnTwister ? DrawnSet(top * ppu) : -1;
            if (drawnSet >= 0)
            {
                // The hand-drawn twister: the largest of its three sizes that fits under the
                // funnel's width, scaled up the rest of the way; its height follows the picture.
                // It is drawn in the wind teal only, so the gale and storm shades tint it.
                art = drawn[drawnSet];
                float contentW = drawnWidth[drawnSet];
                scaleX = scaleY = top * ppu / contentW;
                height = drawnHeight[drawnSet] * scaleX / ppu;
                color = new Color(DrawnTint[shade].r, DrawnTint[shade].g, DrawnTint[shade].b, fade);
            }
            else
            {
                float pxScale = Mathf.Max(1f, top * ppu / MaxFunnelPx, height * ppu / MaxFunnelHeightPx);
                int topPx = Mathf.Clamp(Mathf.RoundToInt(top * ppu / pxScale / PxStep) * PxStep, 8, MaxFunnelPx);
                int bottomPx = Mathf.Clamp(Mathf.RoundToInt(bottom * ppu / pxScale / 2f) * 2, 4, topPx);
                int heightPx = Mathf.Clamp(Mathf.RoundToInt(height * ppu / pxScale / PxStep) * PxStep, 8, MaxFunnelHeightPx);
                art = FunnelArt(topPx, bottomPx, heightPx, shade);
                scaleX = top * ppu / topPx;
                scaleY = height * ppu / heightPx;
            }
            int frame = Frame(time, Mathf.Lerp(SpinFpsSlow, SpinFpsFast, p), 0f, art.Length);
            Sprite[] slices = art[frame];
            float sliceBottom = 0f;
            for (int i = 0; i < bands.Count; i++)
            {
                Band band = bands[i];
                if (i >= slices.Length || slices[i] == null)
                {
                    band.sr.enabled = false;
                    continue;
                }
                band.sr.sprite = slices[i];
                band.sr.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                float sliceHeight = slices[i].rect.height / ppu * scaleY;
                float tMid = Mathf.Clamp01((sliceBottom + sliceHeight * 0.5f) / Mathf.Max(0.01f, height));
                band.sr.transform.position = PixelFx.Snap(center + new Vector2(SwayAt(tMid, time), sliceBottom));
                band.sr.sortingOrder = order + 1;
                band.sr.color = color;
                band.sr.enabled = true;
                sliceBottom += sliceHeight;
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
                o.sr.sortingOrder = sin > 0f ? order : order + 3;
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

        // ---- The hand-drawn twister (Assets/twister_small|medium|large.png, 6 frames each, drawn
        // for the mod in the game's wind tones) ----
        private static readonly string[] DrawnNames = { "small", "medium", "large" };
        private static readonly Color[] DrawnTint =
        {
            Color.white,
            new Color(0.84f, 0.88f, 0.9f),
            new Color(0.68f, 0.75f, 0.8f),
        };
        private static Sprite[][][] drawn;        // [set][frame][slice]
        private static float[] drawnWidth, drawnHeight; // the picture's content size per set, px
        private static float drawnPpu;
        private static bool drawnFailed;

        // The set to use for a funnel `wantPx` wide: the largest whose picture isn't wider, so
        // it only ever scales up; -1 when the art isn't available.
        private static int DrawnSet(float wantPx)
        {
            if (!LoadDrawn())
                return -1;
            int best = 0;
            for (int i = 1; i < drawn.Length; i++)
                if (drawnWidth[i] <= wantPx * 1.1f)
                    best = i;
            return best;
        }

        private static bool LoadDrawn()
        {
            if (drawnFailed)
                return false;
            if (drawn != null && Mathf.Approximately(drawnPpu, PixelFx.Ppu) && drawn[0] != null && drawn[0][0] != null && drawn[0][0][0] != null && drawn[0][0][0].texture != null)
                return true;
            try
            {
                var sets = new Sprite[DrawnNames.Length][][];
                var widths = new float[DrawnNames.Length];
                var heights = new float[DrawnNames.Length];
                for (int i = 0; i < DrawnNames.Length; i++)
                {
                    Texture2D tex = LoadPng("twister_" + DrawnNames[i]);
                    if (tex == null)
                    {
                        drawnFailed = true;
                        PixelFx.Log($"drawn twister sheet twister_{DrawnNames[i]}.png not found; using the coded funnel");
                        return false;
                    }
                    const int frameCount = 6;
                    int fw = tex.width / frameCount, fh = tex.height;
                    widths[i] = fw - 2;   // a 1-px margin either side of the picture
                    heights[i] = fh;
                    int sliceH = Mathf.CeilToInt((float)fh / Slices);
                    sets[i] = new Sprite[frameCount][];
                    for (int f = 0; f < frameCount; f++)
                    {
                        sets[i][f] = new Sprite[Slices];
                        for (int k = 0; k < Slices; k++)
                        {
                            int y0 = k * sliceH, hh = Mathf.Min(sliceH, fh - y0);
                            if (hh <= 0)
                                continue;
                            sets[i][f][k] = Sprite.Create(tex, new Rect(f * fw, y0, fw, hh), new Vector2(0.5f, 0f), PixelFx.Ppu);
                        }
                    }
                }
                drawn = sets;
                drawnWidth = widths;
                drawnHeight = heights;
                drawnPpu = PixelFx.Ppu;
                return true;
            }
            catch (System.Exception e)
            {
                drawnFailed = true;
                PixelFx.Log($"drawn twister couldn't be loaded ({e.Message}); using the coded funnel");
                return false;
            }
        }

        private static Texture2D LoadPng(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WoLCyclone.Assets." + name + ".png"))
            {
                if (stream == null)
                    return null;
                var data = new byte[stream.Length];
                int read = 0;
                while (read < data.Length)
                {
                    int n = stream.Read(data, read, data.Length - read);
                    if (n <= 0)
                        break;
                    read += n;
                }
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                if (!tex.LoadImage(data))
                    return null;
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                return tex;
            }
        }

        // The funnel picture: `topPx` wide at the mouth, `bottomPx` at the tip, `heightPx` tall
        // (plus the mouth's far rim above), per frame cut into `Slices` strips, bottom first.
        // The body is a cone seen from the game's angle: across each row the front face is
        // mapped round the cone (asin), so the spiral stripes that wind up it bunch at the edges
        // as on a real column and scroll a step each frame. Lit from the upper left: the face in
        // the body tone with light stripes, the right rim in shade with dark stripes; the mouth an
        // open ellipse, its far rim lit, the throat the deepest tone; the silhouette slightly
        // ragged, as a twister's is.
        private static Sprite[][] FunnelArt(int topPx, int bottomPx, int heightPx, int shade)
        {
            string key = topPx + "x" + bottomPx + "x" + heightPx + "s" + shade;
            if (funnelCache.TryGetValue(key, out Sprite[][] cached) && cached.Length > 0 && PixelFx.Alive(cached[0]))
                return cached;
            int pad = 2;
            int mouthHalf = Mathf.Max(2, Mathf.RoundToInt(topPx * MouthFlatten * 0.5f));
            int w = topPx + pad * 2, h = heightPx + mouthHalf + 1;
            float cx = w * 0.5f;
            Sprite[] whole = PixelFx.Strip("funnel" + key, w, h, FunnelFrames, Shades[shade], (f, x, y) =>
            {
                float px = x + 0.5f - cx, py = y + 0.5f;
                if (py >= heightPx)
                {
                    // The open mouth: the far rim, lit, round the dark throat.
                    float mx = px / (topPx * 0.5f), my = (py - heightPx) / mouthHalf;
                    float e = mx * mx + my * my;
                    if (e > 1f)
                        return 0;
                    if (e > 0.7f)
                        return my > 0.35f ? 4 : 3;
                    return 1;
                }
                float t = py / heightPx;
                float half = (bottomPx + (topPx - bottomPx) * Mathf.Pow(t, Flare)) * 0.5f;
                half *= 1f + 0.05f * Mathf.Sin(t * 29f + f * 1.05f) + 0.03f * Mathf.Sin(t * 71f - f * 0.7f);
                float u = px / Mathf.Max(0.5f, half);
                if (Mathf.Abs(u) > 1f)
                {
                    // A few pixels flicking off the rim where a stripe reaches the edge.
                    if (Mathf.Abs(u) < 1.3f && half > 3f)
                    {
                        float edge = PixelFx.Frac(Mathf.Sign(u) * 0.25f + t * SpiralTurns - (float)f / FunnelFrames);
                        if (edge < 0.08f && Hash(x * 31 + y * 17 + f) > 0.45f)
                            return 2;
                    }
                    return 0;
                }
                float theta = Mathf.Asin(Mathf.Clamp(u, -1f, 1f));
                float s = PixelFx.Frac(theta / (Mathf.PI * 2f) + t * SpiralTurns - (float)f / FunnelFrames);
                bool stripe = s < StripeFill;
                if (u > 0.78f)
                    return stripe ? 2 : 1;        // the right rim, in shade
                if (u > 0.3f)
                    return stripe ? 3 : 2;
                if (u < -0.82f)
                    return stripe ? 3 : 2;        // the left rim, turning away
                return stripe ? 4 : 3;            // the lit face
            }, new Vector2(0.5f, 0f));
            // Cut each frame into slices, bottom first, each pivoted at its own bottom centre.
            var frames = new Sprite[FunnelFrames][];
            int sliceH = Mathf.CeilToInt((float)h / Slices);
            for (int f = 0; f < FunnelFrames; f++)
            {
                frames[f] = new Sprite[Slices];
                Rect r = whole[f].rect;
                for (int i = 0; i < Slices; i++)
                {
                    int y0 = i * sliceH;
                    int hh = Mathf.Min(sliceH, h - y0);
                    if (hh <= 0)
                        continue;
                    frames[f][i] = Sprite.Create(whole[f].texture, new Rect(r.x, r.y + y0, r.width, hh), new Vector2(0.5f, 0f), PixelFx.Ppu);
                }
            }
            funnelCache[key] = frames;
            return frames;
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
                    .Sort(PixelFx.ActorLayer, order + 2);
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
