using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    // The unique legendaries' powers, drawn in the game's pixel style (code-drawn sprites, no
    // outlines, fading by alpha), in place of the plain lines and rings they had:
    //   Arc     a jagged bolt between two enemies, white core with a coloured edge, 3 frames
    //   Burst   a ring of fire breaking into dashes as it spreads, 4 frames
    //   Motes   pixels flying from a corpse to the wizard (the Bloodwell's draw)
    //   Gust    white streaks whipping off the wizard (Galeheart's speed)
    //   Echo    a violet flash on the enemy hit again (Twinfold Band)
    //   Thorns  a ring of thorn pixels snapping out from the wizard (Thornwick)
    public static class PowerFx
    {
        private static readonly Dictionary<string, Sprite[]> frames = new Dictionary<string, Sprite[]>();
        private static Material material;

        private static Material Mat()
        {
            if (material == null)
                material = new Material(Shader.Find("Sprites/Default"));
            return material;
        }

        private static float Ppu(Player near)
        {
            try
            {
                SpriteRenderer sr = near != null ? near.spriteRenderer : null;
                if (sr != null && sr.sprite != null && sr.sprite.pixelsPerUnit > 0f)
                    return sr.sprite.pixelsPerUnit;
            }
            catch
            {
            }
            return 16f;
        }

        private static Sprite Make(int w, int h, Func<int, int, Color> pixel, Vector2 pivot, float ppu)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel(x, y);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu);
        }

        private static Sprite[] Cached(string key, Func<Sprite[]> build)
        {
            Sprite[] f;
            if (frames.TryGetValue(key, out f) && f != null && f.Length > 0 && f[0] != null)
                return f;
            f = build();
            frames[key] = f;
            return f;
        }

        // ---- Sprites ----

        // A bolt 24 px long: a jagged white core with a coloured edge; three jags, so it crackles.
        private static Sprite[] Bolt(Color edge, float ppu)
        {
            return Cached("bolt" + ColorUtility.ToHtmlStringRGB(edge) + ppu, () =>
            {
                const int W = 24, H = 7;
                var result = new Sprite[3];
                for (int v = 0; v < 3; v++)
                {
                    var rng = new System.Random(40 + v);
                    var core = new HashSet<int>();
                    int y = 3, x = 0;
                    while (x < W)
                    {
                        int len = 3 + rng.Next(0, 4);
                        int ny = Mathf.Clamp(y + rng.Next(-2, 3), 1, 5);
                        for (int i = 0; i < len && x < W; i++, x++)
                        {
                            int yy = Mathf.RoundToInt(Mathf.Lerp(y, ny, (i + 1f) / len));
                            core.Add(yy * W + x);
                            if (yy != y && i == 0)
                                core.Add(y * W + x);
                        }
                        y = ny;
                    }
                    result[v] = Make(W, H, (px, py) =>
                    {
                        if (core.Contains(py * W + px))
                            return Color.white;
                        bool near = core.Contains((py + 1) * W + px) || core.Contains((py - 1) * W + px) || core.Contains(py * W + px + 1) || core.Contains(py * W + px - 1);
                        return near ? edge : Color.clear;
                    }, new Vector2(0f, 0.5f), ppu);
                }
                return result;
            });
        }

        // A ring 32 px across: solid, then thinner, then dashes, then sparks.
        private static Sprite[] Ring(Color c, float ppu)
        {
            return Cached("ring" + ColorUtility.ToHtmlStringRGB(c) + ppu, () =>
            {
                const int S = 32;
                Color light = Color.Lerp(c, Color.white, 0.5f);
                var result = new Sprite[4];
                for (int f = 0; f < 4; f++)
                {
                    int frame = f;
                    result[f] = Make(S, S, (x, y) =>
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S / 2f, S / 2f));
                        float r = 14.5f;
                        float thick = frame == 0 ? 2.5f : frame == 1 ? 1.6f : 1.1f;
                        if (Mathf.Abs(d - r) > thick)
                            return Color.clear;
                        float ang = Mathf.Atan2(y - S / 2f, x - S / 2f);
                        int seg = Mathf.FloorToInt((ang + Mathf.PI) / (Mathf.PI * 2f) * 16f);
                        if (frame == 2 && seg % 2 == 1)
                            return Color.clear;
                        if (frame == 3 && seg % 4 != 0)
                            return Color.clear;
                        return frame == 0 && Mathf.Abs(d - r) < 1f ? light : c;
                    }, new Vector2(0.5f, 0.5f), ppu);
                }
                return result;
            });
        }

        private static Sprite Dot(float ppu)
        {
            return Cached("dot" + ppu, () => new[] { Make(1, 1, (x, y) => Color.white, new Vector2(0.5f, 0.5f), ppu) })[0];
        }

        // A violet 5x5 flash: a plus, then a diamond, then gone.
        private static Sprite[] Plus(Color c, float ppu)
        {
            return Cached("plus" + ColorUtility.ToHtmlStringRGB(c) + ppu, () =>
            {
                var result = new Sprite[2];
                for (int f = 0; f < 2; f++)
                {
                    int frame = f;
                    result[f] = Make(5, 5, (x, y) =>
                    {
                        int dx = Mathf.Abs(x - 2), dy = Mathf.Abs(y - 2);
                        if (frame == 0)
                            return dx == 0 || dy == 0 ? (dx + dy == 0 ? Color.white : c) : Color.clear;
                        return dx + dy == 2 ? c : Color.clear;
                    }, new Vector2(0.5f, 0.5f), ppu);
                }
                return result;
            });
        }

        // ---- The effects ----

        public static void Arc(Vector3 from, Vector3 to, Color colour, Player near)
        {
            try
            {
                float ppu = Ppu(near);
                Sprite[] bolt = Bolt(colour, ppu);
                Vector3 d = to - from;
                float length = d.magnitude;
                if (length < 0.05f)
                    return;
                var go = new GameObject("ChaosDepthsArc");
                go.transform.position = from;
                go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                go.transform.localScale = new Vector3(length / (24f / ppu), 1f, 1f);
                var fx = go.AddComponent<Anim>();
                fx.Setup(bolt, 0.05f, 0.1f, near, 40);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"power fx arc: {e.Message}");
            }
        }

        public static void Burst(Vector3 at, float radius, Color colour, Player near)
        {
            try
            {
                float ppu = Ppu(near);
                Sprite[] ring = Ring(colour, ppu);
                var go = new GameObject("ChaosDepthsBurst");
                go.transform.position = at;
                go.transform.localScale = Vector3.one * (radius * 2f / (32f / ppu));
                var fx = go.AddComponent<Anim>();
                fx.Setup(ring, 0.06f, 0.08f, near, -5);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"power fx burst: {e.Message}");
            }
        }

        // Pixels flying from a point to the wizard over 0.35 s.
        public static void Motes(Vector3 from, Player to, Color colour, int count)
        {
            try
            {
                float ppu = Ppu(to);
                for (int i = 0; i < count; i++)
                {
                    var go = new GameObject("ChaosDepthsMote");
                    var m = go.AddComponent<Flyer>();
                    m.Setup(Dot(ppu), from + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.4f), to, Color.Lerp(colour, Color.white, 0.3f), 0.35f + i * 0.05f, ppu);
                }
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"power fx motes: {e.Message}");
            }
        }

        // Three white streaks a few pixels long, whipping off behind the wizard.
        public static void Gust(Player player, Vector2 direction, Color colour)
        {
            try
            {
                float ppu = Ppu(player);
                if (direction.sqrMagnitude < 0.01f)
                    direction = Vector2.right;
                direction.Normalize();
                Vector3 at = player.transform.position + new Vector3(0f, 0.5f, 0f);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 start = at - (Vector3)direction * (0.3f + 0.25f * i) + new Vector3(0f, (i - 1) * 0.2f, 0f);
                    var go = new GameObject("ChaosDepthsGust");
                    go.transform.position = start;
                    go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                    var r = go.AddComponent<SpriteRenderer>();
                    r.sprite = Streak(colour, ppu);
                    r.material = Mat();
                    SpriteRenderer sr = player.spriteRenderer;
                    if (sr != null)
                    {
                        r.sortingLayerID = sr.sortingLayerID;
                        r.sortingOrder = sr.sortingOrder - 1;
                    }
                    var f = go.AddComponent<Fade>();
                    f.life = 0.25f;
                    f.velocity = -(Vector3)direction * 2.5f;
                }
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"power fx gust: {e.Message}");
            }
        }

        private static Sprite Streak(Color c, float ppu)
        {
            return Cached("streak" + ColorUtility.ToHtmlStringRGB(c) + ppu, () => new[] { Make(6, 1, (x, y) => x < 2 ? Color.white : c, new Vector2(1f, 0.5f), ppu) })[0];
        }

        public static void Echo(Vector3 at, Color colour, Player near)
        {
            try
            {
                var go = new GameObject("ChaosDepthsEcho");
                go.transform.position = at + new Vector3(0f, 0.5f, 0f);
                var fx = go.AddComponent<Anim>();
                fx.Setup(Plus(colour, Ppu(near)), 0.07f, 0.06f, near, 40);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"power fx echo: {e.Message}");
            }
        }

        // Thorns: eight pixels shooting out from the wizard to the radius and vanishing.
        public static void Thorns(Player player, float radius, Color colour)
        {
            try
            {
                float ppu = Ppu(player);
                Vector3 at = player.transform.position + new Vector3(0f, 0.4f, 0f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 2f / 8f + 0.2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.6f, 0f);
                    var go = new GameObject("ChaosDepthsThorn");
                    go.transform.position = at + dir * 0.3f;
                    go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                    var r = go.AddComponent<SpriteRenderer>();
                    r.sprite = Streak(colour, ppu);
                    r.material = Mat();
                    SpriteRenderer sr = player.spriteRenderer;
                    if (sr != null)
                    {
                        r.sortingLayerID = sr.sortingLayerID;
                        r.sortingOrder = sr.sortingOrder + 1;
                    }
                    var f = go.AddComponent<Fade>();
                    f.life = 0.2f;
                    f.velocity = dir * (radius / 0.2f);
                }
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"power fx thorns: {e.Message}");
            }
        }

        // ---- Behaviours ----

        private class Anim : MonoBehaviour
        {
            private SpriteRenderer r;
            private Sprite[] frames;
            private float frameTime, fadeTime, born;

            public void Setup(Sprite[] frames, float frameTime, float fadeTime, Player near, int orderBias)
            {
                this.frames = frames;
                this.frameTime = frameTime;
                this.fadeTime = fadeTime;
                born = Time.time;
                r = gameObject.AddComponent<SpriteRenderer>();
                r.sprite = frames[0];
                r.material = Mat();
                SpriteRenderer sr = near != null ? near.spriteRenderer : null;
                if (sr != null)
                {
                    r.sortingLayerID = sr.sortingLayerID;
                    r.sortingOrder = sr.sortingOrder + orderBias;
                }
            }

            private void Update()
            {
                float t = Time.time - born;
                int i = Mathf.FloorToInt(t / frameTime);
                if (i < frames.Length)
                {
                    r.sprite = frames[i];
                    return;
                }
                float over = t - frames.Length * frameTime;
                if (over >= fadeTime)
                {
                    Destroy(gameObject);
                    return;
                }
                Color c = r.color;
                c.a = 1f - over / fadeTime;
                r.color = c;
            }
        }

        private class Flyer : MonoBehaviour
        {
            private SpriteRenderer r;
            private Player to;
            private Vector3 from;
            private float life, born, ppu;

            public void Setup(Sprite sprite, Vector3 from, Player to, Color c, float life, float ppu)
            {
                this.from = from;
                this.to = to;
                this.life = life;
                this.ppu = ppu;
                born = Time.time;
                r = gameObject.AddComponent<SpriteRenderer>();
                r.sprite = sprite;
                r.material = Mat();
                r.color = c;
                SpriteRenderer sr = to != null ? to.spriteRenderer : null;
                if (sr != null)
                {
                    r.sortingLayerID = sr.sortingLayerID;
                    r.sortingOrder = sr.sortingOrder + 2;
                }
                transform.position = from;
            }

            private void Update()
            {
                float t = (Time.time - born) / life;
                if (t >= 1f || to == null)
                {
                    Destroy(gameObject);
                    return;
                }
                Vector3 target = to.transform.position + new Vector3(0f, 0.6f, 0f);
                Vector3 p = Vector3.Lerp(from, target, t * t);
                p.y += Mathf.Sin(t * Mathf.PI) * 0.4f;
                transform.position = new Vector3(Mathf.Round(p.x * ppu) / ppu, Mathf.Round(p.y * ppu) / ppu, p.z);
            }
        }

        private class Fade : MonoBehaviour
        {
            public float life = 0.25f;
            public Vector3 velocity;
            private float born = -1f;
            private SpriteRenderer r;

            private void Update()
            {
                if (born < 0f)
                {
                    born = Time.time;
                    r = GetComponent<SpriteRenderer>();
                }
                float t = (Time.time - born) / life;
                if (t >= 1f || r == null)
                {
                    Destroy(gameObject);
                    return;
                }
                transform.position += velocity * Time.deltaTime;
                Color c = r.color;
                c.a = 1f - t;
                r.color = c;
            }
        }
    }
}
