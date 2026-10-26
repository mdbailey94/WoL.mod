using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WoLTrailblazer
{
    // The toolkit behind the drawn effects (FireArt, BlazeLook):
    // - PixelCanvas: a small canvas to draw pixel-art frames on in code, turned into point-filtered
    //   sprites at the game's pixel size;
    // - GameSprites: the game's own effect sprites, found by name or cut from a loaded sheet;
    // - Fx: a pool of sprite objects the effects borrow and give back (capped per effect and in all,
    //   the oldest reused past the cap, everything put away on a level load), the sorting layers
    //   they draw on, and the log lines (each visual once when it first plays, each failure once);
    // - FxSprite: one pooled sprite playing its frames (looping, one after another, or each for its
    //   own time), drifting, and fading out in steps (chunky, like the art).
    internal struct Pixel
    {
        public readonly int X, Y;

        public Pixel(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    internal sealed class PixelCanvas
    {
        public const float Tau = 2f * Mathf.PI;

        public readonly int Width, Height;
        public readonly float CenterX, CenterY;
        private readonly Color32[] pixels;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            CenterX = (width - 1) / 2f;
            CenterY = (height - 1) / 2f;
            pixels = new Color32[width * height];
        }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool Has(int x, int y) => Inside(x, y) && pixels[y * Width + x].a > 0;

        public void Set(int x, int y, Color32 color)
        {
            if (Inside(x, y))
                pixels[y * Width + x] = color;
        }

        // Only where nothing is drawn yet.
        public void Under(int x, int y, Color32 color)
        {
            if (Inside(x, y) && pixels[y * Width + x].a == 0)
                pixels[y * Width + x] = color;
        }

        // A pixel's distance from the canvas centre, and its angle (0 to Tau, anticlockwise from
        // the right; y goes up).
        public float Distance(int x, int y)
        {
            float dx = x - CenterX, dy = y - CenterY;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public float Angle(int x, int y)
        {
            float a = Mathf.Atan2(y - CenterY, x - CenterX);
            return a < 0f ? a + Tau : a;
        }

        public void Line(int x0, int y0, int x1, int y1, Color32 color)
        {
            var points = new List<Pixel>();
            Trace(x0, y0, x1, y1, points);
            foreach (Pixel p in points)
                Set(p.X, p.Y, color);
        }

        public void Draw(List<Pixel> points, Color32 color)
        {
            foreach (Pixel p in points)
                Set(p.X, p.Y, color);
        }

        // The pixels of a straight line (Bresenham), in order, added to a path (a point already at
        // the end of the path isn't repeated).
        public static void Trace(int x0, int y0, int x1, int y1, List<Pixel> path)
        {
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                if (path.Count == 0 || path[path.Count - 1].X != x0 || path[path.Count - 1].Y != y0)
                    path.Add(new Pixel(x0, y0));
                if (x0 == x1 && y0 == y1)
                    return;
                int e2 = 2 * error;
                if (e2 >= dy)
                {
                    error += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        // A one-pixel border (the four sides) round everything drawn so far.
        public void Outline(Color32 color)
        {
            var edge = new List<int>();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (pixels[y * Width + x].a > 0)
                        continue;
                    if (Has(x - 1, y) || Has(x + 1, y) || Has(x, y - 1) || Has(x, y + 1))
                        edge.Add(y * Width + x);
                }
            }
            foreach (int i in edge)
                pixels[i] = color;
        }

        // A soft glow round everything drawn so far, the way the game's lightning glows: one ring of
        // `near` touching the drawing, then `far` thinning out (its alpha falling with distance) to
        // `reach` pixels away. Fills only empty pixels.
        public void Glow(Color32 near, Color32 far, float reach)
        {
            var core = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                core[i] = pixels[i].a > 0;
            int span = Mathf.CeilToInt(reach);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (core[y * Width + x])
                        continue;
                    float nearest = float.MaxValue;
                    for (int dy = -span; dy <= span; dy++)
                    {
                        for (int dx = -span; dx <= span; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (!Inside(nx, ny) || !core[ny * Width + nx])
                                continue;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (d < nearest)
                                nearest = d;
                        }
                    }
                    if (nearest <= 1.5f)
                        pixels[y * Width + x] = near;
                    else if (nearest <= reach)
                    {
                        float fade = 1f - (nearest - 1.5f) / Mathf.Max(reach - 1.5f, 0.01f);
                        pixels[y * Width + x] = new Color32(far.r, far.g, far.b, (byte)Mathf.Max(1, Mathf.RoundToInt(far.a * (0.35f + 0.65f * fade))));
                    }
                }
            }
        }

        public Sprite ToSprite(float ppu, Vector2 pivot)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                // Kept through the game's asset clean-ups (the sprites are made once and reused).
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Width, Height), pivot, ppu);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        // Repeatable noise for a pixel: 0 to 1.
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / (float)0x1000000;
            }
        }

        public static float Frac(float value) => value - Mathf.Floor(value);

        // Still usable (a sprite cut from a game texture dies with that texture).
        public static bool Alive(Sprite[] frames) => frames != null && frames.Length > 0 && frames[0] != null && frames[0].texture != null;
    }

    // The game's own effect sprites, found among what's loaded: by name (the sprite sheets' named
    // frames) or cut from a texture by rectangle (particle sheets, whose frames have no names).
    // Looked up once and kept; when something isn't loaded (an atlas only some scenes or arcana
    // load), the effects draw their own frames instead and it's looked for again later.
    internal static class GameSprites
    {
        private const float RescanGap = 20f;   // seconds before looking again for something missing

        private static Dictionary<string, Sprite> byName;
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite[]> sets = new Dictionary<string, Sprite[]>();
        private static readonly Dictionary<string, float> missingSince = new Dictionary<string, float>();
        private static float scannedAt = -1000f;

        // prefix0 .. prefix(count-1), or null if any is missing.
        public static Sprite[] Named(string prefix, int count)
        {
            string key = "name:" + prefix + count;
            Sprite[] frames;
            if (sets.TryGetValue(key, out frames) && PixelCanvas.Alive(frames))
                return frames;
            if (StillMissing(key))
                return null;
            if (byName == null || Time.realtimeSinceStartup - scannedAt > RescanGap)
                Scan();
            frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                Sprite sprite;
                if (!byName.TryGetValue(prefix + i, out sprite) || sprite == null || sprite.texture == null)
                    return Missing(key);
                frames[i] = sprite;
            }
            sets[key] = frames;
            return frames;
        }

        // Frames cut from a loaded texture: rectangles in its pixels (from the bottom left, as
        // Unity counts), all with the same pivot.
        public static Sprite[] Cut(string texture, Rect[] rects, Vector2 pivot, float ppu)
        {
            string key = "cut:" + texture + rects.Length + rects[0].x + "," + rects[0].y;
            Sprite[] frames;
            if (sets.TryGetValue(key, out frames) && PixelCanvas.Alive(frames))
                return frames;
            if (StillMissing(key))
                return null;
            Texture2D source;
            if (!textures.TryGetValue(texture, out source) || source == null)
            {
                source = null;
                foreach (Texture2D t in Resources.FindObjectsOfTypeAll<Texture2D>())
                {
                    if (t != null && t.name == texture)
                    {
                        source = t;
                        break;
                    }
                }
                textures[texture] = source;
            }
            if (source == null)
                return Missing(key);
            frames = new Sprite[rects.Length];
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].xMax > source.width || rects[i].yMax > source.height)
                    return Missing(key);
                frames[i] = Sprite.Create(source, rects[i], pivot, ppu);
            }
            sets[key] = frames;
            return frames;
        }

        private static void Scan()
        {
            scannedAt = Time.realtimeSinceStartup;
            byName = new Dictionary<string, Sprite>();
            foreach (Sprite sprite in Resources.FindObjectsOfTypeAll<Sprite>())
                if (sprite != null && !byName.ContainsKey(sprite.name))
                    byName[sprite.name] = sprite;
        }

        private static bool StillMissing(string key)
        {
            float since;
            return missingSince.TryGetValue(key, out since) && Time.realtimeSinceStartup - since < RescanGap;
        }

        private static Sprite[] Missing(string key)
        {
            missingSince[key] = Time.realtimeSinceStartup;
            return null;
        }
    }

    internal static class Fx
    {
        private const string Prefix = "Trailblazer";
        private const int MaxSprites = 160;      // drawn sprites out at once, all effects together
        private const string FloorLayer = "Shadow";              // char marks and the ring of fire (the layer the game draws ground lines and shadows on)
        private const string GroundLayer = "TargetLineGround";  // bursts on the ground (the game's ground telegraphs' layer)
        private const int FloorOrder = -32000;   // under everything else on the floor layer (the actors' shadows)
        private const int FallbackOrder = -32000; // no such layer: drawn on the wizard's layer under everything

        private static readonly List<FxSprite> live = new List<FxSprite>();
        private static readonly Stack<FxSprite> spare = new Stack<FxSprite>();
        private static readonly HashSet<string> logged = new HashSet<string>();
        private static readonly Dictionary<string, int> layers = new Dictionary<string, int>();
        private static bool hooked, loggedLayers;

        // The game's pixel size (pixels per world unit): the wizard's own sprite's, or the camera's.
        public static float Ppu(SpriteRenderer body)
        {
            try
            {
                if (body != null && body.sprite != null && body.sprite.pixelsPerUnit > 0f)
                    return body.sprite.pixelsPerUnit;
                if (CameraController.pixelsPerUnit > 0)
                    return CameraController.pixelsPerUnit;
            }
            catch
            {
            }
            return 16f;
        }

        // A pooled sprite for an effect. Past the effect's own cap, or the overall one, the oldest
        // goes back first and is reused.
        public static FxSprite Get(string kind, int cap)
        {
            Hook();
            live.RemoveAll(s => s == null);
            FxSprite oldest = null;
            int count = 0;
            foreach (FxSprite s in live)
            {
                if (s.Kind != kind)
                    continue;
                if (oldest == null)
                    oldest = s;
                count++;
            }
            if (count >= cap && oldest != null)
                Release(oldest);
            else if (live.Count >= MaxSprites)
                Release(live[0]);
            FxSprite sprite = null;
            while (sprite == null && spare.Count > 0)
                sprite = spare.Pop();
            if (sprite == null)
            {
                var go = new GameObject(Prefix + " FX");
                sprite = go.AddComponent<FxSprite>();
                sprite.Init();
            }
            sprite.Begin(kind);
            live.Add(sprite);
            return sprite;
        }

        public static void Release(FxSprite sprite)
        {
            if (sprite == null || !sprite.Live)
                return;
            live.Remove(sprite);
            sprite.End();
            spare.Push(sprite);
        }

        public static void ReleaseAll()
        {
            foreach (FxSprite s in live.ToArray())
            {
                if (s != null)
                {
                    s.End();
                    spare.Push(s);
                }
            }
            live.Clear();
        }

        // A level load puts everything away (sprites left in the old level are gone with it).
        private static void Hook()
        {
            if (hooked)
                return;
            hooked = true;
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                try
                {
                    ReleaseAll();
                }
                catch (System.Exception e)
                {
                    Fail("level load clean-up", e);
                }
            };
        }

        // ---- Sorting ----

        // On the floor, under the characters and their shadows (char marks, the ring of fire).
        public static void OnFloor(SpriteRenderer renderer, int slot, SpriteRenderer like) =>
            Place(renderer, FloorLayer, FloorOrder + slot, like, FallbackOrder + slot);

        // On the ground, above the floor marks and under the characters (bursts).
        public static void OnGround(SpriteRenderer renderer, int slot, SpriteRenderer like) =>
            Place(renderer, GroundLayer, slot, like, FallbackOrder + GroundFallbackGap + slot);

        // Next to the wizard on their own layer: in front (delta > 0) or behind.
        public static void NearBody(SpriteRenderer renderer, int delta, SpriteRenderer body)
        {
            if (body == null)
                return;
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = body.sortingOrder + delta;
        }

        private const int GroundFallbackGap = 100; // without the layers, ground bursts still sort above floor marks

        private static void Place(SpriteRenderer renderer, string layerName, int order, SpriteRenderer like, int fallbackOrder)
        {
            int id = LayerID(layerName);
            if (id != int.MinValue)
            {
                renderer.sortingLayerID = id;
                renderer.sortingOrder = order;
            }
            else if (like != null)
            {
                renderer.sortingLayerID = like.sortingLayerID;
                renderer.sortingOrder = fallbackOrder;
            }
        }

        private static int LayerID(string name)
        {
            int id;
            if (layers.TryGetValue(name, out id))
                return id;
            id = int.MinValue;
            try
            {
                int found = SortingLayer.NameToID(name);
                if (SortingLayer.IsValid(found))
                    id = found;
            }
            catch
            {
            }
            layers[name] = id;
            return id;
        }

        // ---- The game's own pooled particle effects ----

        // False if it couldn't be had (logged once).
        public static bool Effect(string pool, int count, Vector2 position, ParticleSystemOverride overrides = null)
        {
            try
            {
                PoolManager.GetPoolItem<ParticleEffect>(pool).Emit(count, (Vector3?)(Vector3)position, overrides);
                return true;
            }
            catch (System.Exception e)
            {
                Fail(pool, e);
                return false;
            }
        }

        // ---- The log ----

        // The first time a visual plays: what it's made of, and (once) where the drawn effects sort.
        public static void Ready(string visual, int sprites, float ppu, SpriteRenderer body)
        {
            if (!logged.Add("ready " + visual))
                return;
            TrailblazerPlugin.Log($"{Prefix}: visual '{visual}' ready ({sprites} sprites, ppu {ppu:0.#})");
            if (loggedLayers)
                return;
            loggedLayers = true;
            TrailblazerPlugin.Log($"{Prefix}: drawn effects sort on {Describe(FloorLayer)} (floor marks) and {Describe(GroundLayer)} " +
                $"(ground bursts), the wizard on '{(body != null ? SafeName(body.sortingLayerID) : "?")}' " +
                $"(#{(body != null ? SafeValue(body.sortingLayerID) : 0)})");
        }

        // Where a visual's frames come from (the game's own sprites, or drawn), once each way.
        public static void Source(string visual, Sprite[] game, string gameName)
        {
            string key = "source " + visual + (game != null ? " game" : " drawn");
            if (!logged.Add(key))
                return;
            TrailblazerPlugin.Log(game != null
                ? $"{Prefix}: visual '{visual}' uses the game's sprites {gameName}"
                : $"{Prefix}: visual '{visual}': the game's {gameName} isn't loaded here, drawing it instead");
        }

        public static void Fail(string what, System.Exception e)
        {
            if (logged.Add("failed " + what))
                TrailblazerPlugin.Log($"{Prefix}: visual '{what}' skipped: {e.Message}");
        }

        private static string Describe(string layer)
        {
            int id = LayerID(layer);
            return id == int.MinValue ? $"'{layer}' (missing: the wizard's layer instead)" : $"'{layer}' (#{SafeValue(id)})";
        }

        private static string SafeName(int id)
        {
            try
            {
                return SortingLayer.IDToName(id);
            }
            catch
            {
                return "?";
            }
        }

        private static int SafeValue(int id)
        {
            try
            {
                return SortingLayer.GetLayerValueFromID(id);
            }
            catch
            {
                return 0;
            }
        }
    }

    // A handle on a pooled sprite that goes stale once the sprite is put away (and maybe reused by
    // another effect), so an effect never moves someone else's sprite.
    internal struct FxHandle
    {
        private readonly FxSprite sprite;
        private readonly int generation;

        public FxHandle(FxSprite sprite)
        {
            this.sprite = sprite;
            generation = sprite != null ? sprite.Generation : 0;
        }

        public FxSprite Sprite => sprite != null && sprite.Live && sprite.Generation == generation ? sprite : null;

        public void Release()
        {
            FxSprite s = Sprite;
            if (s != null)
                Fx.Release(s);
        }
    }

    internal class FxSprite : MonoBehaviour
    {
        private const int FadeSteps = 4;   // a fade-out goes in this many steps, not smoothly

        internal SpriteRenderer Renderer;
        internal string Kind;
        internal int Generation;
        internal bool Live;
        internal Vector2 Velocity, Accel;
        internal float Drag;

        private Sprite[] frames;
        private float[] durations;   // each frame's own time (null: frameTime each)
        private float frameTime;
        private bool loop;
        private bool manual;         // the owner picks the frame (Show)
        private float life;          // seconds until it's put away (0: until released)
        private float fadeTime;
        private float baseAlpha;
        private float born;
        private int shown;

        internal void Init()
        {
            Renderer = gameObject.AddComponent<SpriteRenderer>();
        }

        internal void Begin(string kind)
        {
            Kind = kind;
            Generation++;
            Live = true;
            frames = null;
            durations = null;
            frameTime = 0.05f;
            loop = manual = false;
            life = fadeTime = 0f;
            baseAlpha = 1f;
            born = Time.time;
            shown = -1;
            Velocity = Accel = Vector2.zero;
            Drag = 0f;
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
            Renderer.sprite = null;
            Renderer.flipX = false;
            Renderer.color = Color.white;
            gameObject.SetActive(true);
        }

        internal void End()
        {
            Live = false;
            Generation++;
            if (this == null)
                return;
            Renderer.sprite = null;
            if (transform.parent != null)
                transform.SetParent(null, false);
            gameObject.SetActive(false);
        }

        // Frames one after another, frameTime each; looping round until life is up (0: until
        // released), or holding the last.
        internal FxSprite Play(Sprite[] frames, float frameTime, bool loop, float life)
        {
            this.frames = frames;
            this.frameTime = Mathf.Max(frameTime, 0.01f);
            this.loop = loop;
            this.life = life;
            Show(0);
            return this;
        }

        // Each frame for its own time, the last held until life is up.
        internal FxSprite Timeline(Sprite[] frames, float[] durations, float life)
        {
            this.frames = frames;
            this.durations = durations;
            this.life = life;
            Show(0);
            return this;
        }

        // Frames the owner picks with Show.
        internal FxSprite Hold(Sprite[] frames)
        {
            this.frames = frames;
            manual = true;
            Show(0);
            return this;
        }

        // New frames (a bigger size, say), keeping the timing.
        internal void Swap(Sprite[] frames)
        {
            this.frames = frames;
            int frame = shown;
            shown = -1;
            Show(Mathf.Max(frame, 0));
        }

        internal FxSprite Fade(float seconds)
        {
            fadeTime = seconds;
            return this;
        }

        internal FxSprite Alpha(float alpha)
        {
            baseAlpha = alpha;
            SetAlpha(alpha);
            return this;
        }

        // Starts this far into its frames (so flames side by side don't flicker in step).
        internal FxSprite Phase(float seconds)
        {
            born -= seconds;
            return this;
        }

        internal FxSprite At(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, 0f);
            return this;
        }

        // Carried along by a transform (the wizard's picture), starting at a world position.
        internal FxSprite On(Transform parent, Vector2 position)
        {
            At(position);
            if (parent != null)
                transform.SetParent(parent, true);
            return this;
        }

        internal void Show(int frame)
        {
            if (frames == null || frames.Length == 0)
                return;
            frame = Mathf.Clamp(frame, 0, frames.Length - 1);
            if (frame == shown)
                return;
            shown = frame;
            Renderer.sprite = frames[frame];
        }

        internal void SetAlpha(float alpha)
        {
            if (!Mathf.Approximately(Renderer.color.a, alpha))
                Renderer.color = new Color(1f, 1f, 1f, alpha);
        }

        private void Update()
        {
            if (!Live || manual)
                return;
            try
            {
                float age = Time.time - born;
                if (life > 0f && age >= life)
                {
                    Fx.Release(this);
                    return;
                }
                Show(FrameAt(age));
                if (Velocity != Vector2.zero || Accel != Vector2.zero)
                {
                    float dt = Time.deltaTime;
                    transform.position += (Vector3)(Velocity * dt);
                    Velocity += Accel * dt;
                    if (Drag > 0f)
                        Velocity *= Mathf.Max(0f, 1f - Drag * dt);
                }
                float alpha = baseAlpha;
                if (fadeTime > 0f && life > 0f)
                    alpha *= Mathf.Ceil(Mathf.Clamp01((life - age) / fadeTime) * FadeSteps) / FadeSteps;
                SetAlpha(alpha);
            }
            catch (System.Exception e)
            {
                Fx.Fail(Kind, e);
                Fx.Release(this);
            }
        }

        private int FrameAt(float age)
        {
            int count = frames != null ? frames.Length : 0;
            if (count == 0)
                return 0;
            if (durations != null)
            {
                float t = 0f;
                for (int i = 0; i < count && i < durations.Length; i++)
                {
                    t += durations[i];
                    if (age < t)
                        return i;
                }
                return count - 1;
            }
            int frame = Mathf.FloorToInt(Mathf.Max(age, 0f) / frameTime);
            return loop ? frame % count : Mathf.Min(frame, count - 1);
        }
    }
}
