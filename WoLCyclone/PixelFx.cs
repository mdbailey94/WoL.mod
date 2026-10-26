using System.Collections.Generic;
using UnityEngine;

namespace WoLCyclone
{
    // Drawn pixel-art effects, painted in code: frame strips (one Texture2D per animation, point
    // filtered, at the wizard sprite's pixels per unit), a small pool of sprite objects that play
    // them, and the shared Air palette and debris (leaves, pebbles, dust).
    //
    // Every effect goes through Effect(name, ...): if one throws it logs once and is skipped from
    // then on, and the first time each one plays it logs "visual '<name>' ready (...)".
    internal static class PixelFx
    {
        private const float FallbackPpu = 16f;  // used if the wizard's sprite can't be read
        private const int FrameGap = 2;          // empty pixels between frames in a strip, so point filtering never bleeds a neighbour in
        private const int AlphaSteps = 4;        // fades step through this many levels (pixel-art style) rather than smoothly
        // The game's depth sort for actors (SortGenericLayer.GetSortingOrder): 1024, minus 16 per unit up.
        private const int SortBase = 1024;
        private const float SortPerUnit = 16f;

        public static float Ppu = FallbackPpu;
        public static int ActorLayer, EffectLayer, GroundLayer;
        public static string LayerNote = "layers not read";
        private static bool prepared;
        private static readonly HashSet<string> failed = new HashSet<string>();
        private static readonly HashSet<string> announced = new HashSet<string>();

        // Palettes: index 0 is clear and index 1 the outline everywhere, then dark to light.
        public static readonly Color32[] Air =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x3d, 0x6a, 0x8e, 0xc8), // outline: deep sky slate, a little see-through
            new Color32(0x74, 0xa9, 0xc9, 0xe6), // shade
            new Color32(0xa9, 0xdc, 0xea, 0xf2), // mid
            new Color32(0xdd, 0xf6, 0xf7, 0xff), // light
            new Color32(0xff, 0xff, 0xff, 0xff), // highlight
        };
        public static readonly Color32[] Dust =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x5e, 0x52, 0x47, 0xd0),
            new Color32(0x8f, 0x80, 0x6e, 0xe6),
            new Color32(0xb9, 0xab, 0x96, 0xf0),
            new Color32(0xe0, 0xd6, 0xc4, 0xff),
            new Color32(0xf4, 0xfb, 0xfb, 0xff), // a fleck of wind white
        };
        public static readonly Color32[] Leaf =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x26, 0x4f, 0x2e, 0xff),
            new Color32(0x3e, 0x7d, 0x3b, 0xff),
            new Color32(0x6c, 0xab, 0x48, 0xff),
            new Color32(0xb3, 0xdb, 0x70, 0xff),
            new Color32(0xe2, 0xf4, 0xa8, 0xff),
        };
        public static readonly Color32[] Pebble =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x34, 0x35, 0x3e, 0xff),
            new Color32(0x5f, 0x61, 0x6b, 0xff),
            new Color32(0x92, 0x94, 0x9e, 0xff),
            new Color32(0xcc, 0xcf, 0xd5, 0xff),
            new Color32(0xf0, 0xf2, 0xf5, 0xff),
        };

        // Row art keys: '.' clear, 'o' outline, 'd' dark, 'm' mid, 'l' light, 'w' highlight.
        private const string RowKey = ".odmlw";

        public static void Log(string message) => CyclonePlugin.Log("Cyclone: " + message);

        // Reads the game's pixel size and sorting layers once (from the wizard's own sprite).
        public static void Prepare(Player player)
        {
            if (prepared)
                return;
            prepared = true;
            try
            {
                SpriteRenderer body = player != null ? player.spriteRenderer : null;
                if (body != null && body.sprite != null && body.sprite.pixelsPerUnit > 1f)
                    Ppu = body.sprite.pixelsPerUnit;
                ActorLayer = body != null ? body.sortingLayerID : Layer("Actor", 0);
                EffectLayer = Layer("Effect", ActorLayer);
                GroundLayer = Layer("TargetLineGround", ActorLayer);
                LayerNote = $"wizard on '{SortingLayer.IDToName(ActorLayer)}' order {(body != null ? body.sortingOrder : 0)}" +
                    $" (depth formula says {SortOrder(player != null ? player.transform.position.y : 0f)}), " +
                    $"effects on '{SortingLayer.IDToName(EffectLayer)}', floor marks on '{SortingLayer.IDToName(GroundLayer)}'";
                Log($"drawn effects at {Ppu} px per unit; {LayerNote}");
            }
            catch (System.Exception e)
            {
                Log($"couldn't read the game's pixel size, drawing at {Ppu} px per unit: {e.Message}");
            }
        }

        private static int Layer(string name, int fallback)
        {
            foreach (SortingLayer layer in SortingLayer.layers)
            {
                if (layer.name == name)
                    return layer.id;
            }
            return fallback;
        }

        public static int SortOrder(float y) => (int)(SortBase - SortPerUnit * y);

        // Positions land on the game's pixel grid so the art stays crisp.
        public static Vector2 Snap(Vector2 v) => new Vector2(Mathf.Round(v.x * Ppu) / Ppu, Mathf.Round(v.y * Ppu) / Ppu);

        public static float Steps(float alpha) => Mathf.Ceil(Mathf.Clamp01(alpha) * AlphaSteps) / AlphaSteps;

        public static float Frac(float v) => v - Mathf.Floor(v);

        // Runs one effect; returns what it showed (for the one-time "ready" line), or null if nothing.
        public static void Effect(string name, System.Func<string> effect)
        {
            if (failed.Contains(name))
                return;
            try
            {
                string detail = effect();
                if (detail != null && announced.Add(name))
                    Log($"visual '{name}' ready ({detail})");
            }
            catch (System.Exception e)
            {
                failed.Add(name);
                Log($"visual '{name}' unavailable, skipping it: {e.Message}");
            }
        }

        public static void LogOnce(string key, string message)
        {
            if (announced.Add("log:" + key))
                Log(message);
        }

        public delegate int Painter(int frame, int x, int y);

        // Paints a strip of frames (palette indices, 0 clear) and cuts it into sprites. With an
        // outline index, every clear pixel touching a painted one (index 2 and up) takes it, so each
        // frame gets a clean one-pixel outline.
        // Outlined art gets a one-pixel margin all round (the painter sees x from -1 to w and y
        // from -1 to h), so a shape touching the frame's edge still gets its outline.
        public static Sprite[] Strip(string name, int artWidth, int artHeight, int frames, Color32[] palette, Painter paint, Vector2 pivot, int outline = 1)
        {
            int margin = outline > 0 ? 1 : 0;
            int w = artWidth + 2 * margin, h = artHeight + 2 * margin;
            int stride = w + FrameGap;
            int texWidth = stride * frames;
            var tex = new Texture2D(texWidth, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Cyclone_" + name,
                // Kept across level loads (the strips are cached for the whole session).
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            var pixels = new Color32[texWidth * h];
            var cell = new int[w * h];
            var copy = new int[w * h];
            var sprites = new Sprite[frames];
            for (int f = 0; f < frames; f++)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        cell[y * w + x] = paint(f, x - margin, y - margin);
                if (outline > 0)
                {
                    System.Array.Copy(cell, copy, cell.Length);
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            if (copy[y * w + x] != 0)
                                continue;
                            if ((x > 0 && copy[y * w + x - 1] >= 2) || (x < w - 1 && copy[y * w + x + 1] >= 2) ||
                                (y > 0 && copy[(y - 1) * w + x] >= 2) || (y < h - 1 && copy[(y + 1) * w + x] >= 2))
                                cell[y * w + x] = outline;
                        }
                    }
                }
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int c = cell[y * w + x];
                        if (c > 0 && c < palette.Length)
                            pixels[y * texWidth + f * stride + x] = palette[c];
                    }
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            for (int f = 0; f < frames; f++)
            {
                sprites[f] = Sprite.Create(tex, new Rect(f * stride, 0, w, h), pivot, Ppu, 0, SpriteMeshType.FullRect);
                sprites[f].hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            return sprites;
        }

        // Hand-drawn frames from rows of RowKey letters (top row first), already outlined.
        public static Sprite[] FromRows(string name, string[][] frames, Color32[] palette)
        {
            int h = frames[0].Length;
            int w = frames[0][0].Length;
            return Strip(name, w, h, frames.Length, palette, (f, x, y) =>
            {
                string row = frames[f][h - 1 - y];
                int c = x < row.Length ? RowKey.IndexOf(row[x]) : 0;
                return c < 0 ? 0 : c;
            }, new Vector2(0.5f, 0.5f), 0);
        }

        public static bool Alive(Sprite[] frames) => frames != null && frames.Length > 0 && frames[0] != null;

        // ---- Shared art -------------------------------------------------------------------------

        private const int CrescentSize = 24;     // px square
        public const int CrescentFrames = 6;
        private const int CrescentDirections = 16;
        private static readonly Sprite[][] crescents = new Sprite[CrescentDirections][];
        // Per frame: outer radius, thickness at the front, and how far the upper horn curls (px).
        private static readonly float[] CrescentRadius = { 5f, 7f, 8.5f, 9.5f, 10f, 10.5f };
        private static readonly float[] CrescentThick = { 2f, 3f, 4f, 3.5f, 2.5f, 1.5f };
        private static readonly float[] CrescentCurl = { 0f, 0.6f, 1.2f, 1.8f, 2.4f, 3f };
        private static readonly float[] CrescentTrail = { 2f, 4f, 6f, 7f, 6f, 4f };

        // A gust crescent curling toward `direction`: it forms, swells, thins and breaks up
        // (6 frames), with wind lines trailing behind. Painted for the exact heading (16 of them)
        // so it never needs rotating (rotated pixel art goes jagged).
        public static Sprite[] Crescent(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x);
            int index = ((Mathf.RoundToInt(angle / (Mathf.PI * 2f) * CrescentDirections) % CrescentDirections) + CrescentDirections) % CrescentDirections;
            if (Alive(crescents[index]))
                return crescents[index];
            float a = index * Mathf.PI * 2f / CrescentDirections;
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            float c = CrescentSize * 0.5f;
            crescents[index] = Strip("crescent" + index, CrescentSize, CrescentSize, CrescentFrames, Air, (f, x, y) =>
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float lx = dx * cos + dy * sin;   // along the heading
                float ly = -dx * sin + dy * cos;  // across it
                float r = CrescentRadius[f], t = CrescentThick[f];
                // Late frames break up into chunky gaps as the gust dissipates.
                if (f >= 4 && (x + y * 2 + f) % (f == 4 ? 4 : 3) == 0)
                    return 0;
                float outer = Mathf.Sqrt(lx * lx + ly * ly);
                float ix = lx + t, iy = ly - CrescentCurl[f];
                bool inOuter = outer <= r;
                bool inInner = ix * ix + iy * iy <= r * r;
                if (inOuter && !inInner && lx > -r * 0.6f)
                {
                    float rim = r - outer;
                    if (Mathf.Abs(ly) > r * 0.78f)
                        return 2;            // horn tips in shade
                    if (rim < 1.2f && lx > 0f)
                        return 5;            // the leading edge catches the light
                    return rim < 2.4f ? 4 : 3;
                }
                // Two wind lines trailing behind the crescent's horns.
                float trail = CrescentTrail[f];
                float start = -t * 0.5f - 1f;
                for (int line = -1; line <= 1; line += 2)
                {
                    if (Mathf.Abs(ly - line * r * 0.5f) < 0.55f && lx < start && lx > start - trail)
                        return lx > start - trail * 0.4f ? 3 : 2;
                }
                return 0;
            }, new Vector2(0.5f, 0.5f));
            return crescents[index];
        }

        private static Sprite[] leaf, pebble, dust;

        // A green leaf tumbling end over end (7x7, 4 frames).
        public static Sprite[] LeafFrames()
        {
            if (!Alive(leaf))
                leaf = FromRows("leaf", new[]
                {
                    new[] { ".......", "....oo.", "...olmo", "..olmdo", ".olmdo.", ".oddo..", "o.oo..." },
                    new[] { ".......", ".....o.", "....olo", "...olmo", "..omdo.", ".oddo..", "o.o...." },
                    new[] { ".......", ".......", ".....oo", "...oomo", ".oodo..", "oo.....", "......." },
                    new[] { ".......", ".oo....", "odmo...", "odmdo..", ".oddmo.", "..oddo.", "...oo.o" },
                }, Leaf);
            return leaf;
        }

        // A pebble turning over, lit from the top left (5x5, 4 frames).
        public static Sprite[] PebbleFrames()
        {
            if (!Alive(pebble))
                pebble = FromRows("pebble", new[]
                {
                    new[] { ".ooo.", "olmmo", "ommdo", ".oddo", "..oo." },
                    new[] { "..oo.", ".olmo", "olmdo", "oddo.", ".oo.." },
                    new[] { ".ooo.", "olmmo", "omddo", "odddo", ".ooo." },
                    new[] { ".oo..", "olmo.", "ommdo", ".oddo", "..oo." },
                }, Pebble);
            return pebble;
        }

        // A dust clump billowing and squashing (6x6, 4 frames).
        public static Sprite[] DustFrames()
        {
            if (!Alive(dust))
                dust = FromRows("dust", new[]
                {
                    new[] { "......", "..oo..", ".olmo.", "omllmo", "odmmdo", ".oooo." },
                    new[] { ".oo...", "olmoo.", "omlwmo", "omlmdo", "odmddo", ".oooo." },
                    new[] { "......", "...oo.", ".oomlo", "omllmo", "odmmdo", ".oooo." },
                    new[] { "......", "......", ".oooo.", "omllmo", "odmmdo", ".oooo." },
                }, Dust);
            return dust;
        }

        // A random bit of debris: mostly dust, some leaves, the odd pebble.
        public static Sprite[] RandomDebris()
        {
            float roll = Random.value;
            return roll < 0.5f ? DustFrames() : roll < 0.8f ? LeafFrames() : PebbleFrames();
        }
    }

    // One drawn sprite playing an animation, then going back to the pool. It can drift (velocity
    // and drag), travel along a line, or spiral in to a point, and it scales and fades in steps.
    internal class FxSprite : MonoBehaviour
    {
        private const int MaxActive = 90;  // cap on drawn sprites alive at once (extra ones are skipped)
        private const int MaxIdle = 60;    // spare objects kept for reuse
        private static readonly Stack<FxSprite> idle = new Stack<FxSprite>();
        private static int active;
        private static bool loggedError;

        private SpriteRenderer sr;
        private Sprite[] frames;
        private float fps, life, age;
        private bool loop, counted;
        private int mode;  // 0 drift, 1 line, 2 spiral in
        private Vector2 pos, vel, from, to, center;
        private float drag, radius, angle, spin, squash;
        private float scale0, scale1, fadeIn, fadeOut;
        private Color tint;
        private bool sortByY;
        private int order;

        // Plays `frames` once over `life` seconds (or loops at `loopFps`), at `position`.
        public static FxSprite Spawn(Sprite[] frames, Vector2 position, float life, float loopFps = 0f)
        {
            if (!PixelFx.Alive(frames) || active >= MaxActive)
                return null;
            FxSprite fx = null;
            while (fx == null && idle.Count > 0)
                fx = idle.Pop();  // Unity-null if a level load destroyed it
            if (fx == null)
            {
                var go = new GameObject("CycloneFx");
                fx = go.AddComponent<FxSprite>();
                fx.sr = go.AddComponent<SpriteRenderer>();
                go.SetActive(false);
            }
            fx.frames = frames;
            fx.life = Mathf.Max(0.05f, life);
            fx.loop = loopFps > 0f;
            fx.fps = fx.loop ? loopFps : frames.Length / fx.life;
            fx.age = 0f;
            fx.mode = 0;
            fx.pos = position;
            fx.vel = Vector2.zero;
            fx.drag = 0f;
            fx.scale0 = fx.scale1 = 1f;
            fx.fadeIn = 0f;
            fx.fadeOut = 0f;
            fx.tint = Color.white;
            fx.sortByY = false;
            fx.order = 0;
            fx.sr.sprite = frames[0];
            fx.sr.sortingLayerID = PixelFx.EffectLayer;
            fx.sr.sortingOrder = 0;
            fx.sr.color = Color.white;
            fx.sr.flipX = false;
            fx.transform.position = PixelFx.Snap(position);
            fx.transform.localScale = Vector3.one;
            fx.counted = true;
            active++;
            fx.gameObject.SetActive(true);
            return fx;
        }

        public FxSprite Drift(Vector2 velocity, float drag)
        {
            mode = 0;
            vel = velocity;
            this.drag = drag;
            return this;
        }

        // Eases out from one point to another over its life.
        public FxSprite Line(Vector2 start, Vector2 end)
        {
            mode = 1;
            from = start;
            to = end;
            return this;
        }

        // Spirals in to `middle` from `startRadius` (squashed vertically for the floor's
        // perspective), speeding up as it closes, like something sucked into a vortex.
        public FxSprite SpiralIn(Vector2 middle, float startRadius, float startAngle, float angularSpeed, float flatten)
        {
            mode = 2;
            center = middle;
            radius = startRadius;
            angle = startAngle;
            spin = angularSpeed;
            squash = flatten;
            return this;
        }

        public FxSprite Scale(float start, float end)
        {
            scale0 = start;
            scale1 = end;
            return this;
        }

        public FxSprite Fade(float inTime, float outTime)
        {
            fadeIn = inTime;
            fadeOut = outTime;
            return this;
        }

        // Mirrored left to right (around its pivot).
        public FxSprite Flip(bool flip)
        {
            sr.flipX = flip;
            return this;
        }

        public FxSprite Tint(Color color)
        {
            tint = color;
            return this;
        }

        // Fixed layer and order...
        public FxSprite Sort(int layer, int sortingOrder)
        {
            sr.sortingLayerID = layer;
            sortByY = false;
            order = sortingOrder;
            sr.sortingOrder = sortingOrder;
            return this;
        }

        // ...or depth-sorted with the actors by its height on screen, like the game's own sprites.
        public FxSprite SortWithActors(int offset = 0)
        {
            sr.sortingLayerID = PixelFx.ActorLayer;
            sortByY = true;
            order = offset;
            return this;
        }

        private void Update()
        {
            try
            {
                float dt = Time.deltaTime;
                age += dt;
                if (age >= life)
                {
                    Release();
                    return;
                }
                float t = age / life;
                Vector2 p;
                switch (mode)
                {
                    case 1:
                        p = Vector2.Lerp(from, to, 1f - (1f - t) * (1f - t));
                        break;
                    case 2:
                        angle += spin * (1f + 2f * t) * dt;
                        float r = radius * (1f - t * t);
                        p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * squash) * r;
                        break;
                    default:
                        vel /= 1f + drag * dt;
                        pos += vel * dt;
                        p = pos;
                        break;
                }
                transform.position = PixelFx.Snap(p);
                int frame = (int)(age * fps);
                sr.sprite = frames[loop ? frame % frames.Length : Mathf.Min(frames.Length - 1, frame)];
                float s = Mathf.Lerp(scale0, scale1, t);
                transform.localScale = new Vector3(s, s, 1f);
                float alpha = 1f;
                if (fadeIn > 0f && age < fadeIn)
                    alpha = age / fadeIn;
                if (fadeOut > 0f && life - age < fadeOut)
                    alpha = Mathf.Min(alpha, (life - age) / fadeOut);
                sr.color = new Color(tint.r, tint.g, tint.b, tint.a * PixelFx.Steps(alpha));
                if (sortByY)
                    sr.sortingOrder = PixelFx.SortOrder(p.y) + order;
            }
            catch (System.Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    PixelFx.Log($"a drawn effect failed, hiding it: {e.Message}");
                }
                Release();
            }
        }

        private void Release()
        {
            gameObject.SetActive(false);
            if (idle.Count < MaxIdle)
                idle.Push(this);
            else
                Destroy(gameObject);
        }

        // Also runs when a level load destroys it, so the count stays right.
        private void OnDisable()
        {
            if (!counted)
                return;
            counted = false;
            active--;
        }
    }
}
