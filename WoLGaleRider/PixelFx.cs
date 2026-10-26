using System.Collections.Generic;
using UnityEngine;

namespace WoLGaleRider
{
    // A small pixel-art kit for this mod's drawn effects. A picture is drawn into a Grid of
    // palette indices (0 = clear), outlined, and turned into a point-filtered texture at the
    // game's 16 pixels per unit, cut into animation frames; FxSprite plays those frames.
    public static class PixelArt
    {
        // CameraController.pixelsPerUnit: the game draws every sprite at 16 pixels to a unit.
        public const float Ppu = 16f;

        // A picture being drawn: palette indices, row 0 at the bottom (as textures are).
        public class Grid
        {
            public readonly int W, H;
            public readonly byte[] Cells;

            public Grid(int w, int h)
            {
                W = w;
                H = h;
                Cells = new byte[w * h];
            }

            public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;

            public int Get(int x, int y) => In(x, y) ? Cells[y * W + x] : 0;

            public void Set(int x, int y, int index)
            {
                if (In(x, y))
                    Cells[y * W + x] = (byte)index;
            }

            // Paints only over lower palette indices: the palettes run dark to bright, so the
            // brighter stroke wins where two cross.
            public void Plot(int x, int y, int index)
            {
                if (In(x, y) && Cells[y * W + x] < index)
                    Cells[y * W + x] = (byte)index;
            }

            public void PlotF(float x, float y, int index) => Plot(Mathf.FloorToInt(x), Mathf.FloorToInt(y), index);

            // A filled disc (squash < 1 flattens it into the game's 3/4-view ellipse).
            public void Disc(float cx, float cy, float r, int index, float squash = 1f)
            {
                int x0 = Mathf.FloorToInt(cx - r), x1 = Mathf.CeilToInt(cx + r);
                int y0 = Mathf.FloorToInt(cy - r * squash), y1 = Mathf.CeilToInt(cy + r * squash);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - cx, dy = (y + 0.5f - cy) / Mathf.Max(0.01f, squash);
                        if (dx * dx + dy * dy <= r * r)
                            Plot(x, y, index);
                    }
                }
            }

            // A one-pixel outline in index outline on the clear pixels touching (4 ways) any
            // pixel of at least minIndex.
            public void Outline(int outline, int minIndex)
            {
                var add = new List<int>();
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        if (Cells[y * W + x] != 0)
                            continue;
                        if (Get(x + 1, y) >= minIndex || Get(x - 1, y) >= minIndex || Get(x, y + 1) >= minIndex || Get(x, y - 1) >= minIndex)
                            add.Add(y * W + x);
                    }
                }
                foreach (int i in add)
                    Cells[i] = (byte)outline;
            }

            // Hand-drawn rows (top row first, as you'd read them), each character's index in key
            // being its palette index ('.' or any character not in key = leave as is).
            public void Stamp(string[] rows, string key, int ox, int oy)
            {
                for (int r = 0; r < rows.Length; r++)
                {
                    int y = oy + rows.Length - 1 - r;
                    for (int x = 0; x < rows[r].Length; x++)
                    {
                        int index = key.IndexOf(rows[r][x]);
                        if (index > 0)
                            Plot(ox + x, y, index);
                    }
                }
            }
        }

        // Frames side by side on one texture, each its own sprite (all the same size and pivot),
        // at the game's pixels per unit unless told otherwise (an overlay matching a game sprite).
        public static Sprite[] Sheet(Grid[] frames, Color32[] palette, Vector2 pivot, float ppu = Ppu)
        {
            int w = frames[0].W, h = frames[0].H, n = frames.Length;
            var tex = new Texture2D(w * n, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                // Made in code and kept for the whole session: never unloaded with a level.
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[w * n * h];
            for (int f = 0; f < n; f++)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int index = frames[f].Cells[y * w + x];
                        pixels[y * w * n + f * w + x] = index < palette.Length ? palette[index] : palette[0];
                    }
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            var sprites = new Sprite[n];
            for (int f = 0; f < n; f++)
            {
                sprites[f] = Sprite.Create(tex, new Rect(f * w, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
                sprites[f].hideFlags = HideFlags.HideAndDontSave;
            }
            return sprites;
        }

        // A cached sheet is still good (Unity objects can be destroyed under a C# reference).
        public static bool Alive(Sprite[] sheet) => sheet != null && sheet.Length > 0 && sheet[0] != null;

        // Degrees to turn a picture drawn pointing +x so it points along direction.
        public static float Angle(Vector2 direction) => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    // A sprite kept by a controller across frames: Get is null once that sprite has stopped (and
    // maybe been handed out again from the pool), so the controller never moves or stops another
    // effect's sprite.
    public struct FxHandle
    {
        private readonly FxSprite fx;
        private readonly int serial;

        public FxHandle(FxSprite sprite)
        {
            fx = sprite;
            serial = sprite != null ? sprite.Serial : -1;
        }

        public FxSprite Get => fx != null && fx.Serial == serial && fx.gameObject.activeSelf ? fx : null;
    }

    // A pooled sprite that plays a sheet of frames once (holding the last) or on a loop, while
    // it follows a transform or drifts and slows, turns, grows, hops (an arc up and down) and
    // steps out (alpha 2/3, 1/3, gone: chunky, like the game's own blinks) near the end of its
    // life. Its sorting can track a reference renderer (the wizard) every frame. Spawned objects
    // are plain scene objects, so a level load clears them with the scene; the pool skips any
    // that went that way.
    public class FxSprite : MonoBehaviour
    {
        // Most drawn sprites alive at once (all effects of this mod together): past this, new
        // ones are skipped rather than piling up.
        public const int MaxLive = 80;

        private static readonly Stack<FxSprite> pool = new Stack<FxSprite>();
        private static readonly List<FxSprite> live = new List<FxSprite>();

        public SpriteRenderer sr;
        public Sprite[] frames;
        public float fps = 12f;
        public bool loop;
        public float life;              // seconds; 0 = until Stop() (loopers)
        public Vector2 velocity;        // units a second, when not following
        public float drag;              // fraction of the velocity lost per second
        public float angle;             // degrees
        public float spin;              // degrees a second
        public float size = 1f;
        public float growTo = 1f;       // size multiplier reached at the end of its life
        public float hop;               // peak height of an arc over hopTime (flung debris)
        public float hopTime;
        public float fadeAt = 0.7f;     // fraction of its life after which it steps out
        public float alpha = 1f;
        public Color tint = Color.white;
        public Transform follow;
        public Vector2 followOffset;
        public SpriteRenderer sortRef;
        public int sortOffset;
        public object owner;

        // Counts spawns, so a handle can tell its sprite from a later reuse of the same object.
        private static int spawns;
        public int Serial { get; private set; }

        private bool following;
        private float age;
        private float frameClock;
        private Vector2 ground;

        public static int Live
        {
            get
            {
                live.RemoveAll(f => f == null);
                return live.Count;
            }
        }

        // A sprite at position on the given layer and order, playing frames; null if the cap is
        // reached or it can't be made (the caller's Effect wrapper logs that).
        public static FxSprite Spawn(Sprite[] frames, Vector2 position, string layer, int order)
        {
            if (!PixelArt.Alive(frames))
                return null;
            if (live.Count >= MaxLive && Live >= MaxLive)
                return null;
            FxSprite fx = null;
            while (pool.Count > 0 && fx == null)
                fx = pool.Pop();
            if (fx == null)
            {
                var go = new GameObject("GaleRiderFx");
                fx = go.AddComponent<FxSprite>();
                fx.sr = go.AddComponent<SpriteRenderer>();
            }
            fx.Reset(frames, position);
            fx.Serial = ++spawns;
            fx.sr.sortingLayerName = layer;
            fx.sr.sortingOrder = order;
            fx.gameObject.SetActive(true);
            live.Add(fx);
            return fx;
        }

        private void Reset(Sprite[] newFrames, Vector2 position)
        {
            frames = newFrames;
            fps = 12f;
            loop = false;
            life = 0f;
            velocity = Vector2.zero;
            drag = 0f;
            angle = spin = 0f;
            size = growTo = 1f;
            hop = hopTime = 0f;
            fadeAt = 0.7f;
            alpha = 1f;
            tint = Color.white;
            follow = null;
            following = false;
            followOffset = Vector2.zero;
            sortRef = null;
            sortOffset = 0;
            owner = null;
            age = frameClock = 0f;
            ground = position;
            transform.position = new Vector3(position.x, position.y, 0f);
            transform.localEulerAngles = Vector3.zero;
            transform.localScale = Vector3.one;
            sr.sprite = frames[0];
            sr.color = Color.white;
            sr.flipX = false;
            sr.enabled = true;
        }

        // Rides along with target (at offset), until the target goes.
        public FxSprite Follow(Transform target, Vector2 offset)
        {
            follow = target;
            followOffset = offset;
            following = target != null;
            if (following)
                ground = (Vector2)target.position + offset;
            return this;
        }

        // Moves it (for sprites a controller places by hand each frame).
        public void Place(Vector2 position) => ground = position;

        // Swaps the sheet, keeping its place in the animation.
        public void SetFrames(Sprite[] newFrames)
        {
            if (PixelArt.Alive(newFrames))
                frames = newFrames;
        }

        // Starts it this many frames into its animation.
        public void Skip(int frameCount) => frameClock += frameCount;

        public void Stop()
        {
            if (this == null)
                return;
            live.Remove(this);
            gameObject.SetActive(false);
            pool.Push(this);
        }

        // Stops every sprite spawned for owner (the ones riding on or circling the wizard: the
        // backstop when a state ends, even if its controller was switched off by an error); loose
        // debris is spawned without an owner and finishes its own short life.
        public static void StopAll(object owner)
        {
            foreach (FxSprite fx in live.ToArray())
            {
                if (fx != null && owner != null && fx.owner == owner)
                    fx.Stop();
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if ((life > 0f && age >= life) || (following && follow == null) || !PixelArt.Alive(frames))
            {
                Stop();
                return;
            }
            if (following)
                ground = (Vector2)follow.position + followOffset;
            else if (velocity != Vector2.zero)
            {
                ground += velocity * dt;
                if (drag > 0f)
                    velocity *= Mathf.Max(0f, 1f - drag * dt);
            }
            float height = 0f;
            if (hop > 0f && hopTime > 0f && age < hopTime)
            {
                float t = age / hopTime;
                height = 4f * hop * t * (1f - t);
            }
            transform.position = new Vector3(ground.x, ground.y + height, 0f);
            transform.localEulerAngles = new Vector3(0f, 0f, angle + spin * age);
            float lived = life > 0f ? Mathf.Clamp01(age / life) : 0f;
            float scale = size * Mathf.Lerp(1f, growTo, lived);
            transform.localScale = new Vector3(scale, scale, 1f);

            frameClock += dt * fps;
            int frame = Mathf.FloorToInt(frameClock);
            frame = loop ? frame % frames.Length : Mathf.Min(frame, frames.Length - 1);
            sr.sprite = frames[frame];

            float a = alpha;
            if (life > 0f && lived > fadeAt && fadeAt < 1f)
                a *= Mathf.Ceil((1f - lived) / (1f - fadeAt) * 3f) / 3f;
            Color c = tint;
            c.a *= a;
            sr.color = c;
        }

        private void LateUpdate()
        {
            if (sortRef == null)
                return;
            sr.sortingLayerID = sortRef.sortingLayerID;
            sr.sortingOrder = sortRef.sortingOrder + sortOffset;
        }
    }
}
