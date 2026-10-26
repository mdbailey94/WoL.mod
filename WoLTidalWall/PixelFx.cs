using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WoLTidalWall
{
    // The mod's own effect animations, played on pooled sprite objects (PixelFx). Wherever the game
    // has a fitting sheet, the frames are cut straight from the game's own loaded texture
    // (PixelArt.FromSheet: its splashes, droplets, bubbles, smoke), so they are the game's art;
    // the rest are painted in code in the game's effect style (PixelCanvas: the colours sampled
    // from the game's water sheets, no outlines, brush-like shapes, alpha fades). A sheet that
    // isn't loaded falls back to a painted version (logged once). The game's own pooled emitters
    // still do the rest (Effects.cs).

    // One animation: its frames, speed and (optionally) a looping middle section. Without a loop it
    // plays once; with one it plays the intro, loops until stopped, then plays the outro (or, with
    // no outro frames, fades out and shrinks).
    internal sealed class PixelAnim
    {
        public readonly string Name;      // the visual's name in the log
        public readonly Sprite[] Frames;
        public readonly float Fps;
        public readonly int LoopStart;    // -1: no loop, it plays once
        public readonly int LoopEnd;
        public readonly int Width, Height;
        public readonly string Source;    // where its frames come from, for the log
        public readonly bool Fallback;    // painted in place of a game sheet that wasn't loaded
        public readonly string Scene;     // the level it was made in (a fallback is retried in the next)

        public PixelAnim(string name, Sprite[] frames, float fps, int loopStart, int loopEnd, int width, int height,
            string source, bool fallback = false)
        {
            Name = name;
            Frames = frames;
            Fps = fps;
            LoopStart = loopStart;
            LoopEnd = loopEnd;
            Width = width;
            Height = height;
            Source = source;
            Fallback = fallback;
            Scene = PixelArt.SceneName;
        }

        public bool HasLoop => LoopStart >= 0;
        public bool HasOutro => HasLoop && LoopEnd < Frames.Length - 1;
        public bool Valid => Frames != null && Frames.Length > 0 && Frames[0] != null && Frames[0].texture != null;
    }

    // A small grid of palette indices (0 = clear) to paint one frame on, y up from the bottom row.
    internal sealed class PixelCanvas
    {
        public readonly int W, H;
        private readonly byte[] px;
        private readonly byte[] scratch;

        public PixelCanvas(int w, int h)
        {
            W = w;
            H = h;
            px = new byte[w * h];
            scratch = new byte[w * h];
        }

        public void Clear() => System.Array.Clear(px, 0, px.Length);

        public int Get(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? 0 : px[y * W + x];

        public void Set(int x, int y, int c)
        {
            if (x >= 0 && y >= 0 && x < W && y < H)
                px[y * W + x] = (byte)c;
        }

        public void Set(float x, float y, int c) => Set(Mathf.RoundToInt(x), Mathf.RoundToInt(y), c);

        // A filled ellipse centred on (cx, cy) in pixel units (pixel centres at +0.5).
        public void Ellipse(float cx, float cy, float rx, float ry, int c)
        {
            if (rx <= 0f || ry <= 0f)
                return;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f)
                        px[y * W + x] = (byte)c;
                }
            }
        }

        // An elliptical band thickness pixels wide, inside the given radii.
        public void Ring(float cx, float cy, float rx, float ry, float thickness, int c)
        {
            float ix = rx - thickness, iy = ry - thickness;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f - cx), dy = (y + 0.5f - cy);
                    float outer = (dx / rx) * (dx / rx) + (dy / ry) * (dy / ry);
                    bool inside = ix > 0f && iy > 0f && (dx / ix) * (dx / ix) + (dy / iy) * (dy / iy) < 1f;
                    if (outer <= 1f && !inside)
                        px[y * W + x] = (byte)c;
                }
            }
        }

        // A one-pixel line (Bresenham).
        public void Line(int x0, int y0, int x1, int y1, int c)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            for (int guard = 0; guard < 256; guard++)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1)
                    return;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public void Line(float x0, float y0, float x1, float y1, int c) =>
            Line(Mathf.RoundToInt(x0), Mathf.RoundToInt(y0), Mathf.RoundToInt(x1), Mathf.RoundToInt(y1), c);

        // A brush stroke: a tapering streak from (x0, y0) to (x1, y1), width pixels wide at its
        // root narrowing to one at its tip (the shape the game's water and vine effects are made of).
        public void Stroke(float x0, float y0, float x1, float y1, float width, int c)
        {
            float len = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            int steps = Mathf.Max(1, Mathf.RoundToInt(len * 2f));
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float x = x0 + (x1 - x0) * t, y = y0 + (y1 - y0) * t;
                float r = Mathf.Max(0.5f, width * 0.5f * (1f - t));
                if (r <= 0.6f)
                    Set(x, y, c);
                else
                    Ellipse(x + 0.5f, y + 0.5f, r, r, c);
            }
        }

        // Hand-drawn rows (top row first); each character's position in key is its palette index,
        // '.' or a space is left clear.
        public void Rows(string[] rows, string key, int ox = 0, int oy = 0)
        {
            for (int r = 0; r < rows.Length; r++)
            {
                string row = rows[r];
                int y = oy + rows.Length - 1 - r;
                for (int x = 0; x < row.Length; x++)
                {
                    int c = key.IndexOf(row[x]);
                    if (row[x] != '.' && row[x] != ' ' && c > 0)
                        Set(ox + x, y, c);
                }
            }
        }

        // Pixels of colour from with nothing painted above them become to (light from the top).
        public void Highlight(int from, int to) => EdgeRecolor(from, to, 1);

        // Pixels of colour from with nothing painted below them become to (shade at the bottom).
        public void Shade(int from, int to) => EdgeRecolor(from, to, -1);

        private void EdgeRecolor(int from, int to, int dy)
        {
            System.Array.Copy(px, scratch, px.Length);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int ny = y + dy;
                    bool open = ny < 0 || ny >= H || scratch[ny * W + x] == 0;
                    if (scratch[y * W + x] == from && open)
                        px[y * W + x] = (byte)to;
                }
            }
        }

        // Breaks it into specks: each pixel survives with chance keep (a fixed hash of its spot, so
        // rising amounts crumble steadily), the way the game's splashes scatter apart at the end.
        public void Scatter(float keep)
        {
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (Hash(x, y) >= keep * 16f)
                        px[y * W + x] = 0;
                }
            }
        }

        private static int Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (int)((h >> 9) & 15u);
            }
        }

        // Keeps only the pixels where keep(x, y) is true.
        public void Keep(System.Func<int, int, bool> keep)
        {
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (!keep(x, y))
                        px[y * W + x] = 0;
                }
            }
        }

        public void CopyTo(Color32[] target, int offsetX, int stride, Color32[] palette)
        {
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int c = px[y * W + x];
                    target[y * stride + offsetX + x] = c > 0 && c < palette.Length ? palette[c] : clear;
                }
            }
        }
    }

    // Builds and caches the animations: cut from the game's sheets, or painted.
    internal static class PixelArt
    {
        // The game's sprites are 16 pixels to a unit; the wizard's sprite is read to be sure.
        private const float FallbackPixelsPerUnit = 16f;
        private const string LogPrefix = "Tidal Wall: ";
        private static float pixelsPerUnit;
        private static readonly Dictionary<string, PixelAnim> cache = new Dictionary<string, PixelAnim>();
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> missingLogged = new HashSet<string>();
        private static string scannedScene;
        private static Material material;
        private static bool materialTried;

        public delegate void Painter(PixelCanvas canvas, int frame);

        public static float PixelsPerUnit => pixelsPerUnit > 0f ? pixelsPerUnit : FallbackPixelsPerUnit;

        public static string SceneName
        {
            get
            {
                try
                {
                    return SceneManager.GetActiveScene().name;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        // The material the game's own effect sprites are drawn with, if one has been found (else
        // Unity's default sprite material, which is what they use too).
        public static Material Material => material;

        // Takes the material from one of the game's effect sprites, once.
        public static void UseMaterialOf(SpriteRenderer gameSprite, string from)
        {
            if (materialTried || gameSprite == null || gameSprite.sharedMaterial == null)
                return;
            materialTried = true;
            material = gameSprite.sharedMaterial;
            TidalWallPlugin.Log($"{LogPrefix}effects drawn with the game's sprite material '{material.name}' (from {from})");
        }

        // Matches the drawn pixels to the game's (the wizard's sprite), once.
        public static void MatchPixelSize(GameObject wizard)
        {
            if (pixelsPerUnit > 0f || wizard == null)
                return;
            try
            {
                Entity entity = wizard.GetComponent<Entity>();
                Sprite sprite = entity != null && entity.spriteRenderer != null ? entity.spriteRenderer.sprite : null;
                // 100 is Unity's default for an imported image, not a pixel-art sprite.
                if (sprite != null && sprite.pixelsPerUnit > 1f && !Mathf.Approximately(sprite.pixelsPerUnit, 100f))
                    pixelsPerUnit = sprite.pixelsPerUnit;
            }
            catch
            {
            }
            if (pixelsPerUnit <= 0f)
                pixelsPerUnit = FallbackPixelsPerUnit;
        }

        // The game's loaded texture called name, or null. All loaded textures are listed once per
        // level (the lookup is slow) and remembered.
        private static Texture2D FindTexture(string name)
        {
            Texture2D texture;
            if (textures.TryGetValue(name, out texture) && texture != null)
                return texture;
            string scene = SceneName;
            if (scannedScene == scene)
                return null;
            scannedScene = scene;
            textures.Clear();
            foreach (Texture2D t in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                if (t != null && !string.IsNullOrEmpty(t.name) && !textures.ContainsKey(t.name))
                    textures[t.name] = t;
            }
            return textures.TryGetValue(name, out texture) ? texture : null;
        }

        // Frames cut from one of the game's own sheets (cells of a regular grid, numbered left to
        // right from the top row), or, if that sheet isn't loaded, the painted fallback.
        public static PixelAnim FromSheet(string key, string name, string sheet, int cellW, int cellH, int[] cells, float fps,
            int loopStart, int loopEnd, Vector2 pivot, System.Func<PixelAnim> fallback)
        {
            return FromSheet(key, name, sheet, texture =>
            {
                int columns = Mathf.Max(1, texture.width / cellW);
                var rects = new Rect[cells.Length];
                for (int i = 0; i < cells.Length; i++)
                {
                    int col = cells[i] % columns, row = cells[i] / columns;
                    rects[i] = new Rect(col * cellW, texture.height - (row + 1) * cellH, cellW, cellH);
                }
                return rects;
            }, fps, loopStart, loopEnd, pivot, fallback);
        }

        // Frames cut from one of the game's own sheets at the given rects (in the texture's own
        // coordinates, y up from its bottom, as its sprites are), or the painted fallback.
        public static PixelAnim FromSheet(string key, string name, string sheet, Rect[] rects, float fps,
            int loopStart, int loopEnd, Vector2 pivot, System.Func<PixelAnim> fallback) =>
            FromSheet(key, name, sheet, texture => rects, fps, loopStart, loopEnd, pivot, fallback);

        private static PixelAnim FromSheet(string key, string name, string sheet, System.Func<Texture2D, Rect[]> cut, float fps,
            int loopStart, int loopEnd, Vector2 pivot, System.Func<PixelAnim> fallback)
        {
            PixelAnim anim;
            if (cache.TryGetValue(key, out anim) && anim.Valid && !(anim.Fallback && anim.Scene != SceneName))
                return anim;
            Texture2D texture = null;
            Rect[] rects = null;
            try
            {
                texture = FindTexture(sheet);
                if (texture != null)
                {
                    rects = cut(texture);
                    foreach (Rect rect in rects)
                        if (rect.xMin < 0f || rect.yMin < 0f || rect.xMax > texture.width || rect.yMax > texture.height)
                            rects = null;
                }
            }
            catch (System.Exception e)
            {
                TidalWallPlugin.Log($"{LogPrefix}couldn't look up the game's sheets: {e.Message}");
            }
            if (texture != null && rects != null && rects.Length > 0)
            {
                var sprites = new Sprite[rects.Length];
                for (int i = 0; i < rects.Length; i++)
                {
                    sprites[i] = Sprite.Create(texture, rects[i], pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprites[i].hideFlags = HideFlags.HideAndDontSave;
                }
                anim = new PixelAnim(name, sprites, fps, loopStart, loopEnd, (int)rects[0].width, (int)rects[0].height,
                    $"the game's '{sheet}' sheet");
            }
            else
            {
                if (missingLogged.Add(sheet))
                    TidalWallPlugin.Log($"{LogPrefix}the game's '{sheet}' sheet isn't loaded here, '{name}' uses its painted stand-in");
                PixelAnim painted = fallback();
                anim = new PixelAnim(name, painted.Frames, painted.Fps, painted.LoopStart, painted.LoopEnd, painted.Width, painted.Height,
                    "painted stand-in", true);
            }
            cache[key] = anim;
            return anim;
        }

        // An animation painted in code frame by frame the first time it's asked for. name is the
        // visual it belongs to (for the log); loopStart -1 plays it once.
        public static PixelAnim Paint(string key, string name, int w, int h, int frames, float fps, int loopStart, int loopEnd,
            Vector2 pivot, Color32[] palette, Painter paint)
        {
            PixelAnim anim;
            if (cache.TryGetValue(key, out anim) && anim.Valid)
                return anim;
            var texture = new Texture2D(w * frames, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "TidalWall " + key,
                // Kept across level loads (and out of the game's asset clean-ups).
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[w * frames * h];
            var canvas = new PixelCanvas(w, h);
            for (int f = 0; f < frames; f++)
            {
                canvas.Clear();
                paint(canvas, f);
                canvas.CopyTo(pixels, f * w, w * frames, palette);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprites = new Sprite[frames];
            for (int f = 0; f < frames; f++)
            {
                sprites[f] = Sprite.Create(texture, new Rect(f * w, 0, w, h), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprites[f].hideFlags = HideFlags.HideAndDontSave;
            }
            anim = new PixelAnim(name, sprites, fps, loopStart, loopEnd, w, h, $"painted, {palette.Length - 1} colours");
            cache[key] = anim;
            return anim;
        }
    }

    // Where an effect sorts: on the floor under everyone (the layer the game's own floor
    // whirlpool uses, AquaVortex), among the characters by height on screen (the game's Actor
    // layer, sorted as SortGenericLayer does), or above everything (the game's Effect layer).
    internal enum FxLayer { Floor, Actor, Effect }

    // A safe hold on a playing effect: once the effect is finished and its object reused for
    // another, Stop does nothing.
    internal struct FxRef
    {
        private readonly PixelFx fx;
        private readonly int generation;

        public FxRef(PixelFx fx)
        {
            this.fx = fx;
            generation = fx != null ? fx.Generation : -1;
        }

        public bool Alive => fx != null && fx.InUse && fx.Generation == generation;

        public void Stop()
        {
            if (Alive)
                fx.Stop();
        }

        public void StopAfter(float seconds)
        {
            if (Alive)
                fx.StopAfter(seconds);
        }
    }

    // One effect playing: a pooled sprite object that runs its animation and moves by itself
    // (drift, an arc with gravity, a spin, following something), then goes back to the pool.
    // Every one ends on its own: a looping effect has a time limit, a follower stops when what it
    // follows goes, and a level load takes the objects with it (the pool skips them).
    internal sealed class PixelFx : MonoBehaviour
    {
        private const int MaxLive = 260;          // effects playing at once; more are skipped
        private const int MaxPooled = 260;        // idle objects kept for reuse
        private const float DefaultLoopTime = 2f; // a looping effect stops after this unless told otherwise
        private const float StopFadeTime = 0.25f; // stopped with no outro frames: fades out over this
        private const float StopShrink = 0.35f;   // and shrinks by this much as it fades
        private const string FloorLayer = "TargetLineGround";
        private const string ActorLayer = "Actor";
        private const string EffectLayer = "Effect";
        // SortGenericLayer.GetSortingOrder: 1024 - 16 per unit up the screen.
        private const int SortBase = 1024;
        private const float SortPerUnit = 16f;

        private static readonly Stack<PixelFx> pool = new Stack<PixelFx>();
        private static readonly HashSet<string> played = new HashSet<string>();
        private static Material defaultMaterial;
        private static int live;
        private static int nextGeneration;
        private static bool loggedCap;

        private SpriteRenderer sr;
        private PixelAnim anim;
        private FxLayer layer;
        private int bias;
        private SpriteRenderer sortWith;
        private float clock;
        private float age;
        private float delay;
        private float stopAt;
        private bool stopping;
        private float fadeClock = -1f;  // fading out after a stop with no outro frames
        private bool paused;
        private int frame;
        private Vector2 ground;
        private float height;
        private Vector2 velocity;
        private float drag;
        private float lift;
        private float gravity;
        private float spin;
        private float angle;
        private float scale;
        private bool landed;
        private System.Action<Vector2> onLand;
        private bool recycleOnLand;
        private bool outroOnLand;
        private float landLinger;
        private Transform follow;
        private Vector2 followOffset;
        private bool following;
        private float swayAmount, swaySpeed, swayPhase;
        private float fadeTime;
        private Color tint;

        public int Generation { get; private set; }
        public bool InUse { get; private set; }
        public static int Live => live;

        // Starts anim at a spot on the ground. Null when it can't play (no frames, or too many
        // effects already playing), so callers chain with ?.
        public static PixelFx Spawn(PixelAnim anim, Vector2 at, FxLayer layer, int bias = 0)
        {
            if (anim == null || !anim.Valid)
                return null;
            if (live >= MaxLive)
            {
                if (!loggedCap)
                {
                    loggedCap = true;
                    TidalWallPlugin.Log($"Tidal Wall: {MaxLive} drawn effects already playing, skipping extras (logged once)");
                }
                return null;
            }
            PixelFx fx = null;
            while (fx == null && pool.Count > 0)
                fx = pool.Pop();   // a level load destroys idle ones; those come back null
            if (fx == null)
            {
                var go = new GameObject("TidalWallFx");
                fx = go.AddComponent<PixelFx>();
                fx.sr = go.AddComponent<SpriteRenderer>();
                if (defaultMaterial == null)
                    defaultMaterial = fx.sr.sharedMaterial;
            }
            fx.Begin(anim, at, layer, bias);
            if (played.Add(anim.Name))
                TidalWallPlugin.Log($"Tidal Wall: visual '{anim.Name}' ready ({anim.Frames.Length} frames of {anim.Width}x{anim.Height} px from " +
                    $"{anim.Source}, ppu {PixelArt.PixelsPerUnit:0}, layer {fx.sr.sortingLayerName})");
            return fx;
        }

        private void Begin(PixelAnim newAnim, Vector2 at, FxLayer newLayer, int newBias)
        {
            anim = newAnim;
            layer = newLayer;
            bias = newBias;
            sortWith = null;
            clock = age = delay = 0f;
            stopAt = anim.HasLoop ? DefaultLoopTime : -1f;
            stopping = paused = landed = following = recycleOnLand = outroOnLand = false;
            fadeClock = -1f;
            frame = 0;
            ground = at;
            height = lift = gravity = drag = landLinger = spin = angle = 0f;
            scale = 1f;
            velocity = Vector2.zero;
            onLand = null;
            follow = null;
            followOffset = Vector2.zero;
            swayAmount = swaySpeed = swayPhase = 0f;
            fadeTime = 0f;
            tint = Color.white;
            Generation = ++nextGeneration;
            InUse = true;
            live++;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            Material game = PixelArt.Material;
            sr.sharedMaterial = game != null ? game : defaultMaterial;
            sr.flipX = sr.flipY = false;
            sr.color = tint;
            sr.sprite = anim.Frames[0];
            sr.enabled = true;
            sr.sortingLayerName = layer == FxLayer.Floor ? FloorLayer : layer == FxLayer.Actor ? ActorLayer : EffectLayer;
            gameObject.SetActive(true);
            Place();
        }

        public FxRef Ref => new FxRef(this);

        // A looping effect keeps looping this long, then plays its outro.
        public PixelFx Loop(float seconds) { stopAt = age + Mathf.Max(0f, seconds); return this; }
        // Starts this many seconds into the animation (so neighbours don't move in step).
        public PixelFx Offset(float seconds) { clock = Mathf.Max(0f, seconds); return this; }
        // Hidden (and still) for this long first.
        public PixelFx Delay(float seconds) { delay = seconds; sr.enabled = seconds <= 0f; return this; }
        public PixelFx Move(Vector2 perSecond, float slowing = 0f) { velocity = perSecond; drag = slowing; return this; }
        // Up off the ground (drawn higher, still sorted by its ground spot); with gravity it falls
        // back and lands: then landed(spot) is called and it goes, or plays its outro (outro), or
        // lies there that long (linger) and fades.
        public PixelFx Arc(float startHeight, float upSpeed, float fall, System.Action<Vector2> landed = null, float linger = -1f,
            bool outro = false)
        {
            height = startHeight;
            lift = upSpeed;
            gravity = fall;
            onLand = landed;
            outroOnLand = outro;
            recycleOnLand = !outro && linger < 0f;
            landLinger = Mathf.Max(0f, linger);
            return this;
        }
        public PixelFx Follow(Transform target, Vector2 offset)
        {
            follow = target;
            followOffset = offset;
            following = target != null;
            Place();
            return this;
        }
        // Sorts just in front of (bias > 0) or behind (bias < 0) another sprite.
        public PixelFx SortWith(SpriteRenderer target, int sortBias) { sortWith = target; bias = sortBias; Place(); return this; }
        public PixelFx Rotate(float degrees) { angle = degrees; transform.rotation = Quaternion.Euler(0f, 0f, angle); return this; }
        // Turns this many degrees a second (tumbling debris).
        public PixelFx Spin(float degreesPerSecond) { spin = degreesPerSecond; return this; }
        public PixelFx Scale(float size) { scale = size; transform.localScale = new Vector3(size, size, 1f); return this; }
        public PixelFx Flip(bool x, bool y = false) { sr.flipX = x; sr.flipY = y; return this; }
        public PixelFx Sway(float amount, float speed) { swayAmount = amount; swaySpeed = speed; swayPhase = Random.value * 6.3f; return this; }
        // Fades out over its last this many seconds.
        public PixelFx FadeOut(float seconds) { fadeTime = seconds; return this; }
        public PixelFx Tint(Color color) { tint = color; sr.color = color; return this; }

        // Ends a looping effect: its outro plays, or with none it fades out and shrinks; anything
        // else just goes.
        public void Stop()
        {
            if (!InUse || stopping || fadeClock >= 0f)
                return;
            if (delay > 0f)
                Recycle();
            else if (anim.HasOutro)
            {
                stopping = true;
                paused = false;
                clock = 0f;
            }
            else if (anim.HasLoop || paused)
                fadeClock = 0f;
            else
                Recycle();
        }

        public void StopAfter(float seconds)
        {
            if (InUse && !stopping)
                stopAt = age + Mathf.Max(0f, seconds);
        }

        private void Update()
        {
            if (!InUse)
                return;
            float dt = Time.deltaTime;
            if (delay > 0f)
            {
                delay -= dt;
                if (delay > 0f)
                    return;
                sr.enabled = true;
            }
            age += dt;

            if (following)
            {
                if (follow == null || !follow.gameObject.activeInHierarchy)
                {
                    following = false;
                    Stop();
                    if (!InUse)
                        return;
                }
                else
                    ground = (Vector2)follow.position + followOffset;
            }
            if (velocity != Vector2.zero)
            {
                ground += velocity * dt;
                if (drag > 0f)
                    velocity *= Mathf.Max(0f, 1f - drag * dt);
            }
            if (spin != 0f && !landed)
            {
                angle += spin * dt;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Round(angle / 15f) * 15f);
            }
            if (!landed)
            {
                lift += gravity * dt;
                height += lift * dt;
                if (gravity < 0f && height <= 0f && lift < 0f)
                {
                    height = 0f;
                    landed = true;
                    velocity = Vector2.zero;
                    try
                    {
                        onLand?.Invoke(ground);
                    }
                    catch
                    {
                    }
                    if (recycleOnLand)
                    {
                        Recycle();
                        return;
                    }
                    if (outroOnLand)
                    {
                        if (anim.HasOutro)
                        {
                            stopping = true;
                            clock = 0f;
                        }
                        else
                            fadeClock = 0f;
                    }
                    else
                    {
                        paused = true;
                        stopAt = age + landLinger;
                        if (fadeTime <= 0f)
                            fadeTime = landLinger;
                    }
                }
            }

            if (!Animate(dt))
                return;
            Place();
        }

        // Steps the frames; false once it's finished (and recycled).
        private bool Animate(float dt)
        {
            if (fadeClock >= 0f)
            {
                // Stopped with no outro: fade out and shrink on the frame it stopped at.
                fadeClock += dt;
                float t = fadeClock / StopFadeTime;
                if (t >= 1f)
                {
                    Recycle();
                    return false;
                }
                float s = scale * (1f - StopShrink * t);
                transform.localScale = new Vector3(s, s, 1f);
                sr.color = new Color(tint.r, tint.g, tint.b, tint.a * (1f - t));
                return true;
            }
            if (!paused)
                clock += dt;
            int step = (int)(clock * anim.Fps);
            int count = anim.Frames.Length;
            float remaining;
            if (paused)
            {
                remaining = stopAt - age;
                if (remaining <= 0f)
                {
                    Recycle();
                    return false;
                }
            }
            else if (!anim.HasLoop)
            {
                if (step >= count || (stopAt >= 0f && age >= stopAt))
                {
                    Recycle();
                    return false;
                }
                frame = step;
                remaining = (count - clock * anim.Fps) / anim.Fps;
            }
            else if (!stopping)
            {
                int span = anim.LoopEnd - anim.LoopStart + 1;
                frame = step < anim.LoopStart ? step : anim.LoopStart + (step - anim.LoopStart) % span;
                remaining = stopAt - age;
                if (stopAt >= 0f && age >= stopAt)
                {
                    Stop();
                    if (!InUse)
                        return false;
                }
            }
            else
            {
                frame = anim.LoopEnd + 1 + step;
                if (frame >= count)
                {
                    Recycle();
                    return false;
                }
                remaining = (count - frame) / anim.Fps;
            }
            sr.sprite = anim.Frames[Mathf.Clamp(frame, 0, count - 1)];
            if (fadeTime > 0f)
            {
                float a = Mathf.Clamp01(remaining / fadeTime);
                sr.color = new Color(tint.r, tint.g, tint.b, tint.a * a);
            }
            return true;
        }

        // Draws it at its ground spot (plus its height and sway), on the game's pixel grid, sorted.
        private void Place()
        {
            float ppu = PixelArt.PixelsPerUnit;
            float x = ground.x + (swayAmount > 0f ? Mathf.Sin(age * swaySpeed + swayPhase) * swayAmount : 0f);
            float y = ground.y + height;
            transform.position = new Vector3(Mathf.Round(x * ppu) / ppu, Mathf.Round(y * ppu) / ppu, 0f);
            if (sortWith != null)
            {
                sr.sortingLayerID = sortWith.sortingLayerID;
                sr.sortingOrder = sortWith.sortingOrder + bias;
            }
            else if (layer == FxLayer.Actor)
                sr.sortingOrder = SortBase - (int)(SortPerUnit * ground.y) + bias;
            else
                sr.sortingOrder = bias;
        }

        private void Recycle()
        {
            if (!InUse)
                return;
            InUse = false;
            live--;
            follow = null;
            sortWith = null;
            onLand = null;
            sr.sprite = null;
            if (pool.Count >= MaxPooled)
            {
                Destroy(gameObject);
                return;
            }
            gameObject.SetActive(false);
            pool.Push(this);
        }

        private void OnDestroy()
        {
            if (InUse)
            {
                InUse = false;
                live--;
            }
        }
    }
}
