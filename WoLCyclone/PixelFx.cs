using System.Collections.Generic;
using UnityEngine;

namespace WoLCyclone
{
    // Pixel-art effects in the game's own style: wherever the game has a fitting sprite it is used
    // (GameArt finds them at runtime), and anything drawn here in code is painted like the game's
    // effects: its three flat wind tones (sampled from WindEffects / WindSlash / WindParticles), no
    // outlines, brushed shapes, fading by alpha and shrinking. Also a small pool of sprite objects
    // that play them, and the shared debris (dust, pebbles, leaves).
    //
    // Every effect goes through Effect(name, ...): if one throws it logs once and is skipped from
    // then on, and the first time each one plays it logs "visual '<name>' ready (...)".
    internal static class PixelFx
    {
        private const float FallbackPpu = 16f;  // used if the wizard's sprite can't be read
        private const int FrameGap = 2;          // empty pixels between frames in a strip, so point filtering never bleeds a neighbour in
        // The game's depth sort for actors (SortGenericLayer.GetSortingOrder): 1024, minus 16 per unit up.
        private const int SortBase = 1024;
        private const float SortPerUnit = 16f;

        public static float Ppu = FallbackPpu;
        public static int ActorLayer, EffectLayer, GroundLayer;
        public static string LayerNote = "layers not read";
        private static bool prepared;
        private static readonly HashSet<string> failed = new HashSet<string>();
        private static readonly HashSet<string> announced = new HashSet<string>();

        // Palettes: index 0 clear, then darkest to lightest. The wind tones are the game's own
        // (its wind sheets use just these: 8b9d9d, b6d5d5, cfebeb, and 697a7a sparingly).
        public static readonly Color32[] Air =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x69, 0x7a, 0x7a, 0xff), // deepest shade, sparingly
            new Color32(0x8b, 0x9d, 0x9d, 0xff), // shade
            new Color32(0xb6, 0xd5, 0xd5, 0xff), // body
            new Color32(0xcf, 0xeb, 0xeb, 0xff), // light edge
            new Color32(0xcf, 0xeb, 0xeb, 0xff), // (the game's wind has no white: same as light)
        };
        // Dust, as the game's pale dust puffs look on the floor.
        public static readonly Color32[] Dust =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x7d, 0x72, 0x66, 0xb0),
            new Color32(0x9c, 0x90, 0x80, 0xc8),
            new Color32(0xbd, 0xb2, 0xa0, 0xd8),
            new Color32(0xdb, 0xd2, 0xc2, 0xe8),
            new Color32(0xdb, 0xd2, 0xc2, 0xe8),
        };
        // Leaves, from the game's green leaf colour (LeafEmitter: 7bc152) with its shade and light.
        public static readonly Color32[] Leaf =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x3b, 0x5e, 0x28, 0xff),
            new Color32(0x4f, 0x7d, 0x35, 0xff),
            new Color32(0x7b, 0xc1, 0x52, 0xff),
            new Color32(0xa4, 0xdd, 0x7e, 0xff),
            new Color32(0xa4, 0xdd, 0x7e, 0xff),
        };
        // Pebbles, from the game's rock debris (Rocks sheet: 5d442b, 73502a, 9a7c4c).
        public static readonly Color32[] Pebble =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x4d, 0x37, 0x20, 0xff),
            new Color32(0x5d, 0x44, 0x2b, 0xff),
            new Color32(0x73, 0x50, 0x2a, 0xff),
            new Color32(0x9a, 0x7c, 0x4c, 0xff),
            new Color32(0x9a, 0x7c, 0x4c, 0xff),
        };
        // How the game's white dust puffs are tinted here (a pale warm grey, a little see-through).
        public static readonly Color DustTint = new Color(0.86f, 0.83f, 0.78f, 0.85f);

        // Row art keys: '.' clear, then 'o' deepest, 'd' dark, 'm' mid, 'l' light.
        private const string RowKey = ".odml";

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

        // Fades are smooth alpha, like the game's own effects.
        public static float Steps(float alpha) => Mathf.Clamp01(alpha);

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

        // Paints a strip of frames (palette indices, 0 clear) and cuts it into sprites. No outline
        // by default (the game's effects have none); with an outline index, every clear pixel
        // touching a painted one takes it, inside a one-pixel margin (the painter then sees x from
        // -1 to w and y from -1 to h).
        public static Sprite[] Strip(string name, int artWidth, int artHeight, int frames, Color32[] palette, Painter paint, Vector2 pivot, int outline = 0)
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

        // Hand-drawn frames from rows of RowKey letters (top row first).
        public static Sprite[] FromRows(string name, string[][] frames, Color32[] palette)
        {
            int h = frames[0].Length;
            int w = frames[0][0].Length;
            return Strip(name, w, h, frames.Length, palette, (f, x, y) =>
            {
                string row = frames[f][h - 1 - y];
                int c = x < row.Length ? RowKey.IndexOf(row[x]) : 0;
                return c < 0 ? 0 : c;
            }, new Vector2(0.5f, 0.5f));
        }

        // A sprite set is usable while its sprites and their texture are still loaded.
        public static bool Alive(Sprite[] frames) => frames != null && frames.Length > 0 && frames[0] != null && frames[0].texture != null;

        // ---- Shared art -------------------------------------------------------------------------

        // The game's wind slash (a crescent, convex side up, 7 frames thinning away), by size.
        public static Sprite[] GameSlash(string size)
        {
            var names = new string[7];
            for (int i = 0; i < names.Length; i++)
                names[i] = "WindSlash" + size + i;
            return GameArt.Named("slash" + size, names);
        }

        // Rotation (degrees) that turns the game's slash (convex side up) to face `direction`.
        public static float SlashAngle(Vector2 direction) => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        private const int CrescentSize = 24;     // px square
        public const int CrescentFrames = 6;
        private const int CrescentDirections = 16;
        private static readonly Sprite[][] crescents = new Sprite[CrescentDirections][];
        // Per frame: outer radius, thickness at the front, and how far the upper horn curls (px).
        private static readonly float[] CrescentRadius = { 5f, 7f, 8.5f, 9.5f, 10f, 10.5f };
        private static readonly float[] CrescentThick = { 2f, 3f, 4f, 3.5f, 2.5f, 1.5f };
        private static readonly float[] CrescentCurl = { 0f, 0.6f, 1.2f, 1.8f, 2.4f, 3f };
        private static readonly float[] CrescentTrail = { 2f, 4f, 6f, 7f, 6f, 4f };

        // Drawn stand-in for the game's slash: a gust crescent curling toward `direction` (it forms,
        // swells and thins away over 6 frames) with two wind lines trailing, painted like the
        // game's (light leading edge, body, darker inner edge). Painted for the exact heading (16 of
        // them).
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
                float outer = Mathf.Sqrt(lx * lx + ly * ly);
                float ix = lx + t, iy = ly - CrescentCurl[f];
                bool inOuter = outer <= r;
                bool inInner = ix * ix + iy * iy <= r * r;
                if (inOuter && !inInner && lx > -r * 0.6f)
                {
                    float rim = r - outer;
                    if (Mathf.Abs(ly) > r * 0.78f)
                        return 3;            // the horns taper off in the body tone
                    if (rim < 1.2f)
                        return 4;            // the leading edge catches the light
                    return rim < 2.6f ? 3 : 2;
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

        // A green leaf tumbling end over end (7x7, 4 frames; the game has no leaf sprite loaded to
        // borrow, so it's drawn in the game's leaf green, flat and unoutlined).
        public static Sprite[] LeafFrames()
        {
            if (!Alive(leaf))
                leaf = FromRows("leaf", new[]
                {
                    new[] { ".......", ".....l.", "....lm.", "...lmd.", "..lmd..", ".mdd...", "d......" },
                    new[] { ".......", ".....m.", "....lm.", "...lm..", "..md...", ".dd....", "d......" },
                    new[] { ".......", ".......", ".......", "....mll", "..mdd..", "dd.....", "......." },
                    new[] { ".......", ".d.....", ".md....", ".mmd...", "..mmd..", "...dd..", "......d" },
                }, Leaf);
            return leaf;
        }

        // Drawn stand-in for the game's rock debris: a pebble turning over (5x5, 4 frames).
        public static Sprite[] PebbleFrames()
        {
            if (!Alive(pebble))
                pebble = FromRows("pebble", new[]
                {
                    new[] { ".mm..", "mllm.", "mlmdd", ".mdd.", "..d.." },
                    new[] { "..m..", ".mlm.", "mlmd.", "mdd..", ".d..." },
                    new[] { ".mm..", "mlmm.", "mmddd", ".dddo", "..oo." },
                    new[] { ".m...", "mlm..", "mmdd.", ".mdd.", "..d.." },
                }, Pebble);
            return pebble;
        }

        // Drawn stand-in for the game's dust puffs: a clump billowing and squashing (6x6, 4 frames).
        public static Sprite[] DustFrames()
        {
            if (!Alive(dust))
                dust = FromRows("dust", new[]
                {
                    new[] { "......", "..mm..", ".mllm.", "mlllmd", "dmmmdd", ".dddd." },
                    new[] { ".mm...", "mllmm.", "mlllmd", "mlmmdd", "dmmddd", ".dddd." },
                    new[] { "......", "...mm.", ".mmllm", "mlllmd", "dmmmdd", ".dddd." },
                    new[] { "......", "......", ".mmmm.", "mlllmd", "dmmmdd", ".dddd." },
                }, Dust);
            return dust;
        }

        // A random bit of debris: mostly dust, some leaves, the odd pebble; the game's own dust
        // puffs and rocks when they're loaded, tinted as they'd be.
        public static Sprite[] RandomDebris(out Color tint)
        {
            float roll = Random.value;
            tint = Color.white;
            if (roll < 0.5f)
            {
                Sprite[] game = GameArt.DustPuffs();
                if (game != null)
                {
                    tint = DustTint;
                    return game;
                }
                return DustFrames();
            }
            if (roll < 0.8f)
                return LeafFrames();
            return GameArt.Rocks() ?? PebbleFrames();
        }
    }

    // The game's own sprites, found at runtime: named sprites from its atlases, and tiles cut from
    // its particle sheets (which have no named sprites). Looked up once and cached; if something
    // isn't loaded the caller falls back to drawn art (logged once per set), and the lookup is
    // retried now and then in case it loads later.
    internal static class GameArt
    {
        private const float RescanSeconds = 10f;  // look again for missing art at most this often
        private const float GamePpu = 16f;        // the game's effect sheets are 16 px per unit
        private static Dictionary<string, Sprite> sprites;
        private static Dictionary<string, Texture2D> textures;
        private static float lastScan = -1000f;
        private static readonly Dictionary<string, Sprite[]> sets = new Dictionary<string, Sprite[]>();
        private static readonly HashSet<string> logged = new HashSet<string>();

        // Wind wisps: small arcs flicking away (MiscParticleEffects, row 4, 5 frames of 32 px).
        public static Sprite[] WindWisps() => Tiles("wisps", "MiscParticleEffects", 256, 256, 32, 32, 24, 5);
        // A wind puff breaking into curls (WindParticles, top row, 6 frames of 48 px).
        public static Sprite[] Breakup() => Tiles("breakup", "WindParticles", 288, 288, 48, 48, 0, 6);
        // The spinning gust ring blowing outward (AirSpinnerBurst, 8 frames of 128 px).
        public static Sprite[] GustRing() => Tiles("gust ring", "AirSpinnerBurst", 1024, 128, 128, 128, 0, 8);
        // Dust puffs (the white Dust sheet, tinted by the caller; 4 of its 16 px puffs in turn).
        public static Sprite[] DustPuffs() => Tiles("dust", "Dust", 16, 176, 16, 16, 0, 4);
        // Rock debris: the four large rocks of the Rocks sheet, shown in turn as a tumble.
        public static Sprite[] Rocks() => Named("rocks", "Rocks_0", "Rocks_4", "Rocks_8", "Rocks_12");
        // A curl winding into a pinwheel (AirSpin, then AirSpinAlt: 7 frames of 32 px).
        public static Sprite[] AirSpin() => Named("air spin", "AirSpin0", "AirSpin1", "AirSpin2", "AirSpin3", "AirSpinAlt0", "AirSpinAlt1", "AirSpinAlt2");

        // The game's sprites by name, in order; null if any isn't loaded.
        public static Sprite[] Named(string key, params string[] names)
        {
            if (sets.TryGetValue(key, out Sprite[] cached) && PixelFx.Alive(cached))
                return cached;
            Sprite[] found = FindSprites(names);
            if (found == null && Rescan())
                found = FindSprites(names);
            return Result(key, found, string.Join(", ", names));
        }

        // `count` tiles of `tileW` x `tileH` cut from the game's texture `texture` (identified by its
        // name and size), counted left to right and top to bottom from tile `first`.
        public static Sprite[] Tiles(string key, string texture, int texW, int texH, int tileW, int tileH, int first, int count)
        {
            if (sets.TryGetValue(key, out Sprite[] cached) && PixelFx.Alive(cached))
                return cached;
            Texture2D tex = FindTexture(texture, texW, texH);
            if (tex == null && Rescan())
                tex = FindTexture(texture, texW, texH);
            Sprite[] cut = null;
            if (tex != null)
            {
                int columns = Mathf.Max(1, texW / tileW);
                cut = new Sprite[count];
                for (int i = 0; i < count; i++)
                {
                    int col = (first + i) % columns, row = (first + i) / columns;
                    var rect = new Rect(col * tileW, texH - (row + 1) * tileH, tileW, tileH);
                    cut[i] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), GamePpu, 0, SpriteMeshType.FullRect);
                    cut[i].name = $"Cyclone_{texture}_{first + i}";
                }
            }
            return Result(key, cut, $"{texture} tiles {first}-{first + count - 1}");
        }

        private static Sprite[] Result(string key, Sprite[] found, string what)
        {
            if (found != null)
            {
                sets[key] = found;
                if (logged.Add("found:" + key))
                    PixelFx.Log($"using the game's {what} for '{key}'");
            }
            else if (logged.Add("missing:" + key))
            {
                PixelFx.Log($"the game's {what} isn't loaded, drawing '{key}' instead");
            }
            return found;
        }

        private static Sprite[] FindSprites(string[] names)
        {
            if (sprites == null)
                Rescan();
            var found = new Sprite[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                if (!sprites.TryGetValue(names[i], out Sprite s) || s == null || s.texture == null)
                    return null;
                found[i] = s;
            }
            return found;
        }

        private static Texture2D FindTexture(string name, int w, int h)
        {
            if (textures == null)
                Rescan();
            return textures.TryGetValue($"{name}_{w}x{h}", out Texture2D t) && t != null ? t : null;
        }

        // Indexes every loaded sprite and texture (slow, so at most every RescanSeconds).
        private static bool Rescan()
        {
            if (sprites != null && Time.realtimeSinceStartup - lastScan < RescanSeconds)
                return false;
            lastScan = Time.realtimeSinceStartup;
            sprites = new Dictionary<string, Sprite>();
            foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (s != null && !sprites.ContainsKey(s.name))
                    sprites[s.name] = s;
            }
            textures = new Dictionary<string, Texture2D>();
            foreach (Texture2D t in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                if (t == null)
                    continue;
                string key = $"{t.name}_{t.width}x{t.height}";
                if (!textures.ContainsKey(key))
                    textures[key] = t;
            }
            return true;
        }
    }

    // One sprite playing an animation, then going back to the pool. It can drift (velocity and
    // drag), travel along a line, or spiral in to a point, and it scales and fades.
    internal class FxSprite : MonoBehaviour
    {
        private const int MaxActive = 90;  // cap on effect sprites alive at once (extra ones are skipped)
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
            fx.transform.rotation = Quaternion.identity;
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

        public FxSprite Tint(Color color)
        {
            tint = color;
            return this;
        }

        // Turned by `degrees` (the game rotates its own slashes and gusts the same way).
        public FxSprite Rotate(float degrees)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, degrees);
            return this;
        }

        // Mirrored left to right (around its pivot).
        public FxSprite Flip(bool flip)
        {
            sr.flipX = flip;
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
