using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WoLSlingshotDash
{
    // The mod's own drawn effects for the dashes (only for show: nothing here hits anything). Each
    // effect is a short pixel-art animation generated in code (PixelArt.cs) into one small texture
    // per animation, then played by pooled sprites (FxSprite) that follow, drift, hop or spin.
    //
    // Palette indices of the drawn art: every element palette (PixelArt.cs) uses the same slots,
    // so one drawing can be coloured for several elements.
    internal static class Px
    {
        public const byte Clear = 0;
        public const byte Outline = 1;
        public const byte Dark = 2;
        public const byte Mid = 3;
        public const byte Light = 4;
        public const byte High = 5;
        public const byte AccentA = 6;
        public const byte AccentB = 7;
        public const int Count = 8;
    }

    // One frame being drawn: palette indices on a small grid (y up, as in a texture), with the
    // shapes the art needs and the finishing passes (outline, dithered dissolve).
    internal sealed class PixelCanvas
    {
        public readonly int W, H;
        private readonly byte[] px;
        private byte[] scratch;

        // Ordered 4x4 dither, so dissolving frames break up in the chunky checker the game's
        // own effects use rather than fading smoothly.
        private static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        public PixelCanvas(int w, int h)
        {
            W = w;
            H = h;
            px = new byte[w * h];
        }

        public void Clear() => Array.Clear(px, 0, px.Length);

        public byte Get(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? Px.Clear : px[y * W + x];

        public void Set(int x, int y, byte c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H)
                return;
            px[y * W + x] = c;
        }

        // Like Set, except that where two shades meet the brighter one wins (so overlapping
        // strokes keep their bright heads); accents simply overwrite.
        public void Paint(int x, int y, byte c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H)
                return;
            int i = y * W + x;
            byte cur = px[i];
            if (cur != Px.Clear && cur <= Px.High && c <= Px.High && cur > c)
                return;
            px[i] = c;
        }

        public void Ellipse(float cx, float cy, float rx, float ry, byte c)
        {
            if (rx <= 0f || ry <= 0f)
                return;
            int x0 = Mathf.FloorToInt(cx - rx - 1f), x1 = Mathf.CeilToInt(cx + rx + 1f);
            int y0 = Mathf.FloorToInt(cy - ry - 1f), y1 = Mathf.CeilToInt(cy + ry + 1f);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f)
                        Paint(x, y, c);
                }
            }
        }

        // An elliptical ring: everything between `inner` (a fraction of the radii) and the edge.
        public void Band(float cx, float cy, float rx, float ry, float inner, byte c)
        {
            if (rx <= 0f || ry <= 0f)
                return;
            int x0 = Mathf.FloorToInt(cx - rx - 1f), x1 = Mathf.CeilToInt(cx + rx + 1f);
            int y0 = Mathf.FloorToInt(cy - ry - 1f), y1 = Mathf.CeilToInt(cy + ry + 1f);
            float inner2 = inner * inner;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = dx * dx + dy * dy;
                    if (d <= 1f && d >= inner2)
                        Paint(x, y, c);
                }
            }
        }

        // A dot: one pixel when thin, a small disc when thicker.
        public void Dot(float x, float y, float r, byte c)
        {
            if (r < 0.75f)
                Paint(Mathf.FloorToInt(x), Mathf.FloorToInt(y), c);
            else
                Ellipse(x, y, r, r, c);
        }

        public void Line(float x0, float y0, float x1, float y1, float r, byte c)
        {
            float length = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            int steps = Mathf.CeilToInt(length * 1.5f) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Dot(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), r, c);
            }
        }

        public void Line(Vector2 a, Vector2 b, float r, byte c) => Line(a.x, a.y, b.x, b.y, r, c);

        public void HLine(int x0, int x1, int y, byte c)
        {
            for (int x = x0; x <= x1; x++)
                Paint(x, y, c);
        }

        public void Triangle(Vector2 a, Vector2 b, Vector2 c, byte col)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), y1 = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d1 = Side(p, a, b), d2 = Side(p, b, c), d3 = Side(p, c, a);
                    bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
                    if (!(neg && pos))
                        Paint(x, y, col);
                }
            }
        }

        private static float Side(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        // A four-pointed impact star (an astroid: sqrt|x|/a + sqrt|y|/b <= 1), bright inside
        // `innerFrac` and `outer` beyond; `diagonal` turns it 45 degrees.
        public void Astroid(float cx, float cy, float a, float b, byte inner, byte outer, float innerFrac, bool diagonal = false)
        {
            float r = Mathf.Max(a, b) + 1f;
            for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
            {
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (diagonal)
                    {
                        float u = (dx + dy) * 0.7071f, v = (dy - dx) * 0.7071f;
                        dx = u;
                        dy = v;
                    }
                    float s = Mathf.Sqrt(Mathf.Abs(dx) / a) + Mathf.Sqrt(Mathf.Abs(dy) / b);
                    if (s <= 1f)
                        Paint(x, y, s < innerFrac ? inner : outer);
                }
            }
        }

        // One pixel of `color` round everything drawn (4-way, so corners stay chunky like the
        // game's outlines). Drawn from a copy, so it never grows on itself.
        public void Outline(byte color = Px.Outline)
        {
            if (scratch == null)
                scratch = new byte[px.Length];
            Array.Copy(px, scratch, px.Length);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (scratch[i] != Px.Clear)
                        continue;
                    if ((x > 0 && scratch[i - 1] != Px.Clear) || (x < W - 1 && scratch[i + 1] != Px.Clear)
                        || (y > 0 && scratch[i - W] != Px.Clear) || (y < H - 1 && scratch[i + W] != Px.Clear))
                        px[i] = color;
                }
            }
        }

        // Knocks out `amount` (0..1) of the pixels in an ordered dither.
        public void Dissolve(float amount)
        {
            if (amount <= 0f)
                return;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if ((Bayer[(x & 3) + ((y & 3) << 2)] + 0.5f) / 16f < amount)
                        px[y * W + x] = Px.Clear;
                }
            }
        }

        // Keep only the rows from `y` up (the far side of a ring), or below `y` (the near side).
        public void KeepAbove(int y)
        {
            for (int row = 0; row < Mathf.Min(y, H); row++)
                Array.Clear(px, row * W, W);
        }

        public void KeepBelow(int y)
        {
            for (int row = Mathf.Max(0, y); row < H; row++)
                Array.Clear(px, row * W, W);
        }

        public void WriteTo(Color32[] atlas, int atlasWidth, int xOffset, Color32[] palette, bool[] used)
        {
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    byte c = px[y * W + x];
                    if (c == Px.Clear || c >= palette.Length)
                        continue;
                    atlas[y * atlasWidth + xOffset + x] = palette[c];
                    used[c] = true;
                }
            }
        }
    }

    // A drawn animation: its frames side by side in one point-filtered texture, one sprite each,
    // at the wizard's pixels per unit so a drawn pixel is a game pixel.
    internal sealed class FxAnim
    {
        private const int Padding = 1; // clear pixels between frames, so none bleeds into the next

        public readonly string Name;
        public readonly Sprite[] Frames;
        public readonly float FrameTime;
        public readonly bool Loop;

        private FxAnim(string name, Sprite[] frames, float frameTime, bool loop)
        {
            Name = name;
            Frames = frames;
            FrameTime = frameTime;
            Loop = loop;
        }

        public float Length => Frames.Length * FrameTime;

        // Draws every frame with `draw(canvas, frame, frameCount)` and logs once that it's ready.
        public static FxAnim Build(string owner, string name, int w, int h, int frames, float frameTime, bool loop,
            Vector2 pivot, Color32[] palette, Action<PixelCanvas, int, int> draw)
        {
            float ppu = PixelFx.PixelsPerUnit;
            int stride = w + Padding * 2, atlasWidth = stride * frames;
            var pixels = new Color32[atlasWidth * h];
            var canvas = new PixelCanvas(w, h);
            var used = new bool[Px.Count];
            for (int f = 0; f < frames; f++)
            {
                canvas.Clear();
                draw(canvas, f, frames);
                canvas.WriteTo(pixels, atlasWidth, f * stride + Padding, palette, used);
            }
            var texture = new Texture2D(atlasWidth, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Slingshot " + name
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprites = new Sprite[frames];
            for (int f = 0; f < frames; f++)
            {
                sprites[f] = Sprite.Create(texture, new Rect(f * stride + Padding, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
                sprites[f].name = name + " " + f;
            }
            int colours = 0;
            for (int i = 1; i < used.Length; i++)
                colours += used[i] ? 1 : 0;
            SlingshotDashPlugin.Log($"{owner}: visual '{name}' ready ({frames} frames, {w}x{h} px, {colours} colours, ppu {ppu:0.#})");
            return new FxAnim(name, sprites, frameTime, loop);
        }
    }

    // Where a drawn sprite sorts: among the actors (by height on screen, like the wizard), on the
    // effects layer above them, or flat on the ground under them.
    internal enum FxLayer
    {
        Actor,
        Effect,
        Ground
    }

    // How a drawn sprite moves and sorts (all optional; a struct, so spawning allocates nothing).
    internal struct FxOpts
    {
        public float rotation;          // degrees
        public float spin;              // degrees a second
        public Vector2 velocity;        // drift along the ground, units a second
        public float drag;              // share of that drift lost each second
        public float scale;             // 0 = 1
        public float lift;              // drawn this far above its ground point (sorting stays at the ground point)
        public float hop;               // upward speed of a hopping sprite (pebbles), units a second
        public float gravity;           // pulls a hopping sprite back down
        public FxLayer layer;
        public float sortOffset;        // added to the height used for sorting (+ = further back)
        public Transform follow;        // stays on this, plus `offset`; ends when it's gone
        public Vector2 offset;
        public SpriteRenderer orderFrom; // sorts just behind/in front of this (e.g. the wizard) instead
        public int orderDelta;
        public bool snapshotOrder;      // take orderFrom's order once, at the start
        public float loopFor;           // a looping animation ends after this long (0 = when stopped)
        public bool flipX;
        public bool reverse;            // play the frames backwards (a ring closing in)
        public float speed;             // frame rate multiplier (0 = 1)
        public Color tint;              // alpha 0 = no tint
    }

    // A drawn sprite in flight, so the state that started it can stop or resize it. Safe to use
    // after it has ended (and its sprite went on to play something else).
    internal struct FxHandle
    {
        private readonly FxSprite sprite;
        private readonly int generation;

        internal FxHandle(FxSprite sprite)
        {
            this.sprite = sprite;
            generation = sprite.generation;
        }

        public bool Alive => sprite != null && sprite.inUse && sprite.generation == generation;

        public void Stop()
        {
            if (Alive)
                sprite.Release();
        }

        public void SetScale(float scale)
        {
            if (Alive)
                sprite.scale = scale;
        }
    }

    // A pooled sprite playing one drawn animation (or holding one picture, for afterimages).
    // Runs on game time, so the game's hit-stops and pause freeze it with everything else. All
    // of them are put away when a level loads; looping ones are stopped by the state that
    // started them when it ends (and end by themselves when what they follow is gone).
    internal sealed class FxSprite : MonoBehaviour
    {
        private const int MaxLive = 160; // never more on screen at once, whatever is asked for

        private static readonly Stack<FxSprite> free = new Stack<FxSprite>();
        private static readonly List<FxSprite> live = new List<FxSprite>();
        private static GameObject root;
        private static bool hooked;
        private static bool loggedCap;

        internal int generation;
        internal bool inUse;
        internal float scale = 1f;
        private int liveIndex;
        private SpriteRenderer sr;
        private Material defaultMaterial;
        private FxAnim anim;
        private float stillLife;
        private float stillAlpha;
        private FxOpts o;
        private bool following;
        private float age;      // frame clock (scaled by speed)
        private float elapsed;  // game time since it started
        private float height, upSpeed, angle;
        private Vector3 baseScale = Vector3.one;
        private Vector2 ground;
        private bool sorted;

        public static FxHandle Play(FxAnim anim, Vector2 position, FxOpts opts)
        {
            if (anim == null || anim.Frames.Length == 0)
                return default(FxHandle);
            FxSprite s = Take();
            if (s == null)
                return default(FxHandle);
            s.anim = anim;
            s.Begin(position, opts, Vector3.one);
            s.sr.sprite = anim.Frames[opts.reverse ? anim.Frames.Length - 1 : 0];
            return new FxHandle(s);
        }

        // One picture held for `life` seconds, fading out in three steps (afterimages).
        public static FxHandle Still(Sprite sprite, Material material, Vector2 position, Vector3 scale, float life, FxOpts opts)
        {
            if (sprite == null)
                return default(FxHandle);
            FxSprite s = Take();
            if (s == null)
                return default(FxHandle);
            s.anim = null;
            s.stillLife = Mathf.Max(0.02f, life);
            s.stillAlpha = opts.tint.a > 0f ? opts.tint.a : 1f;
            s.Begin(position, opts, scale);
            s.sr.sprite = sprite;
            if (material != null)
                s.sr.sharedMaterial = material;
            return new FxHandle(s);
        }

        private static FxSprite Take()
        {
            if (root == null)
            {
                root = new GameObject("SlingshotFx");
                DontDestroyOnLoad(root);
                free.Clear();
                live.Clear();
            }
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += (scene, mode) => ReleaseAll();
            }
            if (live.Count >= MaxLive)
            {
                if (!loggedCap)
                {
                    loggedCap = true;
                    SlingshotDashPlugin.Log($"Slingshot visuals: {MaxLive} drawn sprites on screen, skipping more until some finish");
                }
                return null;
            }
            FxSprite s = null;
            while (s == null && free.Count > 0)
                s = free.Pop();
            if (s == null)
            {
                var go = new GameObject("SlingshotFxSprite");
                go.transform.SetParent(root.transform, false);
                s = go.AddComponent<FxSprite>();
                s.sr = go.AddComponent<SpriteRenderer>();
                s.defaultMaterial = s.sr.sharedMaterial;
            }
            s.liveIndex = live.Count;
            live.Add(s);
            s.inUse = true;
            return s;
        }

        private void Begin(Vector2 position, FxOpts opts, Vector3 pictureScale)
        {
            o = opts;
            following = opts.follow != null;
            ground = following ? (Vector2)opts.follow.position + opts.offset : position;
            age = 0f;
            elapsed = 0f;
            height = 0f;
            upSpeed = opts.hop;
            angle = opts.rotation;
            scale = opts.scale > 0f ? opts.scale : 1f;
            baseScale = pictureScale;
            sorted = false;
            sr.flipX = opts.flipX;
            sr.color = opts.tint.a > 0f ? opts.tint : Color.white;
            gameObject.SetActive(true);
            Place();
        }

        private void Update()
        {
            if (!inUse)
                return;
            float dt = Time.deltaTime;
            elapsed += dt;
            age += dt * (o.speed > 0f ? o.speed : 1f);
            if (anim != null)
            {
                int n = anim.Frames.Length;
                int f = (int)(age / anim.FrameTime);
                if (f >= n)
                {
                    if (!anim.Loop || (o.loopFor > 0f && elapsed >= o.loopFor))
                    {
                        Release();
                        return;
                    }
                    f %= n;
                }
                sr.sprite = anim.Frames[o.reverse ? n - 1 - f : f];
            }
            else
            {
                if (elapsed >= stillLife)
                {
                    Release();
                    return;
                }
                // Stepped, like the game's own flickers, rather than a smooth fade.
                float step = Mathf.Ceil((1f - elapsed / stillLife) * 3f) / 3f;
                Color c = sr.color;
                c.a = stillAlpha * step;
                sr.color = c;
            }

            if (following)
            {
                if (o.follow == null || !o.follow.gameObject.activeInHierarchy)
                {
                    Release();
                    return;
                }
                ground = (Vector2)o.follow.position + o.offset;
            }
            else if (o.velocity.sqrMagnitude > 0f)
            {
                ground += o.velocity * dt;
                o.velocity *= Mathf.Max(0f, 1f - o.drag * dt);
            }
            if (o.gravity > 0f)
            {
                upSpeed -= o.gravity * dt;
                height += upSpeed * dt;
                if (height < 0f)
                {
                    // A little bounce, then it settles.
                    height = 0f;
                    upSpeed = Mathf.Abs(upSpeed) > 2f ? -upSpeed * 0.35f : 0f;
                }
            }
            angle += o.spin * dt;
            Place();
        }

        private void Place()
        {
            transform.position = new Vector3(ground.x, ground.y + o.lift + height, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            transform.localScale = new Vector3(baseScale.x * scale, baseScale.y * scale, 1f);
            if (sorted && o.snapshotOrder)
                return;
            sorted = true;
            if (o.orderFrom != null)
            {
                sr.sortingLayerID = o.orderFrom.sortingLayerID;
                sr.sortingOrder = o.orderFrom.sortingOrder + o.orderDelta;
                return;
            }
            sr.sortingLayerID = PixelFx.LayerId(o.layer);
            sr.sortingOrder = PixelFx.SortOrder(ground.y + o.sortOffset + PixelFx.LayerBias(o.layer));
        }

        internal void Release()
        {
            if (!inUse)
                return;
            inUse = false;
            generation++;
            int last = live.Count - 1;
            if (liveIndex >= 0 && liveIndex <= last && live[liveIndex] == this)
            {
                live[liveIndex] = live[last];
                live[liveIndex].liveIndex = liveIndex;
                live.RemoveAt(last);
            }
            else
            {
                live.Remove(this);
            }
            o = default(FxOpts);
            anim = null;
            following = false;
            if (sr != null)
            {
                sr.sprite = null;
                sr.sharedMaterial = defaultMaterial;
                sr.color = Color.white;
                sr.flipX = false;
            }
            gameObject.SetActive(false);
            free.Push(this);
        }

        // A level is loading: put everything away.
        public static void ReleaseAll()
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (i >= live.Count)
                    continue;
                FxSprite s = live[i];
                if (s == null)
                    live.RemoveAt(i);
                else
                    s.Release();
            }
        }
    }

    // Shared setup for the drawn effects: the wizard's pixels per unit, the game's sorting
    // layers, one-time log lines and the wizard's afterimages.
    internal static class PixelFx
    {
        // The game's sprites are 16 px per unit (CameraController.pixelsPerUnit); the wizard's own
        // sprite is checked at runtime and wins.
        internal static float PixelsPerUnit = 16f;

        // The game's layer names: actors (the wizard, enemies), effects drawn over them, and the
        // ground layer target lines and floor rings use.
        private const string EffectLayerName = "Effect";
        private const string GroundLayerName = "TargetLineGround";
        // If a layer is missing, its sprites go on the actors' layer, nudged back or forward.
        private const float MissingGroundBias = 3f;
        private const float MissingEffectBias = -3f;

        private static bool ready;
        private static int actorLayer, effectLayer, groundLayer;
        private static bool effectMissing, groundMissing;
        private static readonly HashSet<string> said = new HashSet<string>();

        // Afterimages: the wizard's sprite drawn solid in one colour with the game's own flash
        // (the material the wizard is drawn with, as the game's Ice Dash decoy copies it).
        private static readonly Dictionary<int, Material> flashMaterials = new Dictionary<int, Material>();
        private static bool loggedGhost;

        public static void Setup(Player player)
        {
            if (ready)
                return;
            SpriteRenderer body = player != null ? player.spriteRenderer : null;
            if (body == null)
                return;
            ready = true;
            float ppu = 0f;
            try
            {
                if (body.sprite != null)
                    ppu = body.sprite.pixelsPerUnit;
                if (ppu < 4f || ppu > 200f)
                    ppu = CameraController.pixelsPerUnit;
            }
            catch
            {
                ppu = 0f;
            }
            PixelsPerUnit = ppu >= 4f && ppu <= 200f ? ppu : 16f;
            actorLayer = body.sortingLayerID;
            effectLayer = FindLayer(EffectLayerName, out effectMissing);
            groundLayer = FindLayer(GroundLayerName, out groundMissing);
            SlingshotDashPlugin.Log($"Slingshot visuals: drawn at {PixelsPerUnit:0.#} px per unit (the wizard's sprite), " +
                $"sorting with the wizard on '{body.sortingLayerName}', effects on '{(effectMissing ? body.sortingLayerName : EffectLayerName)}', " +
                $"ground rings on '{(groundMissing ? body.sortingLayerName : GroundLayerName)}'");
        }

        private static int FindLayer(string name, out bool missing)
        {
            foreach (SortingLayer layer in SortingLayer.layers)
            {
                if (layer.name == name)
                {
                    missing = false;
                    return layer.id;
                }
            }
            missing = true;
            return actorLayer;
        }

        internal static int LayerId(FxLayer layer)
        {
            switch (layer)
            {
                case FxLayer.Effect: return effectLayer;
                case FxLayer.Ground: return groundLayer;
                default: return actorLayer;
            }
        }

        internal static float LayerBias(FxLayer layer)
        {
            if (layer == FxLayer.Ground && groundMissing)
                return MissingGroundBias;
            if (layer == FxLayer.Effect && effectMissing)
                return MissingEffectBias;
            return 0f;
        }

        // The game's own y-sorting (SortGenericLayer): lower on screen draws in front.
        internal static int SortOrder(float y) => Mathf.Clamp((int)(1024f - 16f * y), -32000, 32000);

        // Logs `message` the first time `key` comes up.
        public static void Once(string key, string message)
        {
            if (said.Add(key))
                SlingshotDashPlugin.Log(message);
        }

        // A solid-coloured copy of the wizard's current pose left behind, blinking out over `life`.
        public static void Afterimage(Player player, Color color, float life, string owner)
        {
            SpriteRenderer body = player != null ? player.spriteRenderer : null;
            if (body == null || body.sprite == null)
                return;
            Material material = FlashMaterial(body, color, owner);
            var opts = new FxOpts
            {
                orderFrom = body,
                orderDelta = -1,
                snapshotOrder = true,
                flipX = body.flipX,
                tint = material != null ? new Color(1f, 1f, 1f, color.a) : color
            };
            FxSprite.Still(body.sprite, material, body.transform.position, body.transform.lossyScale, life, opts);
        }

        private static Material FlashMaterial(SpriteRenderer body, Color color, string owner)
        {
            int key = ((int)(color.r * 255f) << 16) | ((int)(color.g * 255f) << 8) | (int)(color.b * 255f);
            Material material;
            if (flashMaterials.TryGetValue(key, out material))
                return material;
            material = null;
            Material source = body.sharedMaterial;
            if (source != null && source.HasProperty("_FlashAmount") && source.HasProperty("_FlashColor"))
            {
                material = new Material(source);
                material.SetColor("_FlashColor", new Color(color.r, color.g, color.b, 1f));
                material.SetFloat("_FlashAmount", 1f);
                if (material.HasProperty("_OutlineColor"))
                    material.SetColor("_OutlineColor", new Color(0f, 0f, 0f, 0f));
            }
            flashMaterials[key] = material;
            if (!loggedGhost)
            {
                loggedGhost = true;
                SlingshotDashPlugin.Log($"{owner}: afterimages " + (material != null
                    ? $"drawn with the game's flash on the wizard's material ('{source.shader.name}')"
                    : "tinted copies of the wizard's sprite (the wizard's material has no flash)"));
            }
            return material;
        }
    }
}
