using System.Collections.Generic;
using UnityEngine;

namespace WoLBrambleRing
{
    // The mod's own drawn effects: small pixel-art animations painted in code (PixelCanvas), cut
    // into frames once (PixelArt) and played on pooled sprite objects (PixelFx). Used where the
    // game has no fitting effect (sprouts, creeping roots, thorns, vine coils, withered leaves...);
    // the game's own pooled emitters (leaves, dust, vine arcs) still do the rest.

    // One drawn animation: its frames, speed and (optionally) a looping middle section. Without a
    // loop it plays once; with one it plays the intro, loops until stopped, then plays the outro.
    internal sealed class PixelAnim
    {
        public readonly string Name;      // the visual's name in the log
        public readonly Sprite[] Frames;
        public readonly float Fps;
        public readonly int LoopStart;    // -1: no loop, it plays once
        public readonly int LoopEnd;
        public readonly int Width, Height, Colours;

        public PixelAnim(string name, Sprite[] frames, float fps, int loopStart, int loopEnd, int width, int height, int colours)
        {
            Name = name;
            Frames = frames;
            Fps = fps;
            LoopStart = loopStart;
            LoopEnd = loopEnd;
            Width = width;
            Height = height;
            Colours = colours;
        }

        public bool HasLoop => LoopStart >= 0;
        public float OnceTime => Frames.Length / Fps;
        public bool Valid => Frames != null && Frames.Length > 0 && Frames[0] != null;
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

        // Every clear pixel touching a painted one (not already this colour) becomes c: the dark
        // outline the game's sprites have.
        public void Outline(int c)
        {
            System.Array.Copy(px, scratch, px.Length);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (scratch[y * W + x] != 0)
                        continue;
                    if (Painted(x - 1, y, c) || Painted(x + 1, y, c) || Painted(x, y - 1, c) || Painted(x, y + 1, c))
                        px[y * W + x] = (byte)c;
                }
            }
        }

        private bool Painted(int x, int y, int outline)
        {
            if (x < 0 || y < 0 || x >= W || y >= H)
                return false;
            byte v = scratch[y * W + x];
            return v != 0 && v != outline;
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

        public void Recolor(int from, int to)
        {
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i] == from)
                    px[i] = (byte)to;
            }
        }

        // Breaks it up pixel by pixel: amount 0 keeps all, 1 clears all. Each pixel has a fixed
        // scattered threshold (a hash of its spot), so frames of rising amount crumble away
        // steadily, in chunky pixels and without a regular checkerboard.
        public void Dissolve(float amount)
        {
            if (amount <= 0f)
                return;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (Threshold(x, y) < amount * 16f)
                        px[y * W + x] = 0;
                }
            }
        }

        private static int Threshold(int x, int y)
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

    // Builds and caches the drawn animations.
    internal static class PixelArt
    {
        // The game's sprites are 16 pixels to a unit; the wizard's sprite is read to be sure.
        private const float FallbackPixelsPerUnit = 16f;
        private static float pixelsPerUnit;
        private static readonly Dictionary<string, PixelAnim> cache = new Dictionary<string, PixelAnim>();

        public delegate void Painter(PixelCanvas canvas, int frame);

        public static float PixelsPerUnit => pixelsPerUnit > 0f ? pixelsPerUnit : FallbackPixelsPerUnit;

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

        // The animation called key, painted frame by frame the first time it's asked for. name is
        // the visual it belongs to (for the log); loopStart -1 plays it once.
        public static PixelAnim Get(string key, string name, int w, int h, int frames, float fps, int loopStart, int loopEnd,
            Vector2 pivot, Color32[] palette, Painter paint)
        {
            PixelAnim anim;
            if (cache.TryGetValue(key, out anim) && anim.Valid)
                return anim;
            var texture = new Texture2D(w * frames, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "BrambleRing " + key,
                // Kept across level loads (and out of the game's asset clean-ups).
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[w * frames * h];
            var canvas = new PixelCanvas(w, h);
            var used = new HashSet<int>();
            for (int f = 0; f < frames; f++)
            {
                canvas.Clear();
                paint(canvas, f);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        used.Add(canvas.Get(x, y));
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
            used.Remove(0);
            anim = new PixelAnim(name, sprites, fps, loopStart, loopEnd, w, h, used.Count);
            cache[key] = anim;
            return anim;
        }
    }

    // Where a drawn effect sorts: on the floor under everyone (the layer the game's own floor
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

    // One drawn effect playing: a pooled sprite object that runs its animation and moves by itself
    // (drift, an arc with gravity, following something), then goes back to the pool. Every one ends
    // on its own: a looping effect has a time limit, a follower stops when what it follows goes,
    // and a level load takes the objects with it (the pool skips them).
    internal sealed class PixelFx : MonoBehaviour
    {
        private const int MaxLive = 260;          // effects playing at once; more are skipped
        private const int MaxPooled = 260;        // idle objects kept for reuse
        private const float DefaultLoopTime = 2f; // a looping effect stops after this unless told otherwise
        private const int FadeSteps = 3;          // fades go down in this many steps, not smoothly
        private const string FloorLayer = "TargetLineGround";
        private const string ActorLayer = "Actor";
        private const string EffectLayer = "Effect";
        // SortGenericLayer.GetSortingOrder: 1024 - 16 per unit up the screen.
        private const int SortBase = 1024;
        private const float SortPerUnit = 16f;

        private static readonly Stack<PixelFx> pool = new Stack<PixelFx>();
        private static readonly HashSet<string> played = new HashSet<string>();
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
        private bool paused;
        private int frame;
        private Vector2 ground;
        private float height;
        private Vector2 velocity;
        private float drag;
        private float lift;
        private float gravity;
        private bool landed;
        private System.Action<Vector2> onLand;
        private bool recycleOnLand;
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
                    BrambleRingState.Log($"{MaxLive} drawn effects already playing, skipping extras (logged once)");
                }
                return null;
            }
            PixelFx fx = null;
            while (fx == null && pool.Count > 0)
                fx = pool.Pop();   // a level load destroys idle ones; those come back null
            if (fx == null)
            {
                var go = new GameObject("BrambleRingFx");
                fx = go.AddComponent<PixelFx>();
                fx.sr = go.AddComponent<SpriteRenderer>();
            }
            fx.Begin(anim, at, layer, bias);
            if (played.Add(anim.Name))
                BrambleRingState.Log($"visual '{anim.Name}' ready ({anim.Frames.Length} frames of {anim.Width}x{anim.Height} px, " +
                    $"{anim.Colours} colours, ppu {PixelArt.PixelsPerUnit:0}, layer {fx.sr.sortingLayerName})");
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
            stopping = paused = landed = following = recycleOnLand = false;
            frame = 0;
            ground = at;
            height = lift = gravity = drag = landLinger = 0f;
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
        // back and lands: then landed(spot) is called and it goes (or lingers that long and fades).
        public PixelFx Arc(float startHeight, float upSpeed, float fall, System.Action<Vector2> landed = null, float linger = -1f)
        {
            height = startHeight;
            lift = upSpeed;
            gravity = fall;
            onLand = landed;
            recycleOnLand = linger < 0f;
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
        public PixelFx Rotate(float degrees) { transform.rotation = Quaternion.Euler(0f, 0f, degrees); return this; }
        public PixelFx Flip(bool x, bool y = false) { sr.flipX = x; sr.flipY = y; return this; }
        public PixelFx Sway(float amount, float speed) { swayAmount = amount; swaySpeed = speed; swayPhase = Random.value * 6.3f; return this; }
        // Fades out (in steps) over its last this many seconds.
        public PixelFx FadeOut(float seconds) { fadeTime = seconds; return this; }
        public PixelFx Tint(Color color) { tint = color; sr.color = color; return this; }

        // Ends a looping effect (its outro plays); anything else just goes.
        public void Stop()
        {
            if (!InUse || stopping)
                return;
            if (anim.HasLoop && anim.LoopEnd < anim.Frames.Length - 1 && delay <= 0f)
            {
                stopping = true;
                clock = 0f;
            }
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
                    paused = true;
                    stopping = false;
                    stopAt = age + landLinger;
                    if (fadeTime <= 0f)
                        fadeTime = landLinger;
                }
            }

            if (!Animate(dt))
                return;
            Place();
        }

        // Steps the frames; false once it's finished (and recycled).
        private bool Animate(float dt)
        {
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
                if (step >= count)
                {
                    Recycle();
                    return false;
                }
                frame = step;
                remaining = (count - clock * anim.Fps) / anim.Fps;
                if (stopAt >= 0f && age >= stopAt)
                {
                    Recycle();
                    return false;
                }
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
                a = Mathf.Ceil(a * FadeSteps) / FadeSteps;
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
