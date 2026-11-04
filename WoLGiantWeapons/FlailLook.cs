using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace WoLGiantWeapons
{
    // WoL Art's Giant Lightning Flail (#84, Log - Art 2026-11-03 15:31; Matt: "a black ball with
    // white spikes on an electric chain"), 16 px per unit, Point filter:
    //   flail_head   4 x 16x16, centre (8,8): the spiked ball, a spark crawling round it;
    //   flail_chain  4 x 8x4, tiles along x: the lightning chain, its crackle travelling;
    //   flail_swing  6 x 64x64, centre (32,32): the circle swing, ring at r 28-31 px, frame N's
    //                head at N x 60 degrees;
    //   flail_slam   6 x 48x48, centre (24,24): the slam's impact.
    // The circle swings play the ring round the wizard with the chain out to the ball; the slam
    // reaches the ball and chain out to the spot over the wind-up, then the impact plays there.
    internal static class FlailLook
    {
        private const float RingPx = 30f, SlamPx = 23f;
        private static Sprite[] head, chain, swing, slam;
        private static bool loaded;

        private static bool Art()
        {
            if (!loaded)
            {
                loaded = true;
                head = Frames("flail_head.png", 16, new Vector2(0.5f, 0.5f));
                chain = Frames("flail_chain.png", 8, new Vector2(0f, 0.5f));
                swing = Frames("flail_swing.png", 64, new Vector2(0.5f, 0.5f));
                slam = Frames("flail_slam.png", 48, new Vector2(0.5f, 0.5f));
                GiantWeaponsPlugin.Log($"Giant Lightning Flail: art {(head != null && chain != null && swing != null && slam != null ? "loaded" : "missing")}");
            }
            return head != null && chain != null && swing != null && slam != null;
        }

        internal static Texture2D Load(string file)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{nameof(WoLGiantWeapons)}.Assets.{file}"))
            {
                if (s == null)
                    return null;
                byte[] data = new byte[s.Length];
                s.Read(data, 0, data.Length);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                tex.LoadImage(data);
                return tex;
            }
        }

        private static Sprite[] Frames(string file, int w, Vector2 pivot)
        {
            Texture2D tex = Load(file);
            if (tex == null || tex.width < w)
                return null;
            var frames = new Sprite[tex.width / w];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(tex, new Rect(i * w, 0, w, tex.height), pivot, 16f);
            return frames;
        }

        // Hits 1-2: the ring round the wizard over `time`, its radius drawn to `radius`.
        public static void Circle(Player p, float radius, float time, float startDeg = 0f, bool clockwise = false)
        {
            if (p == null || !Art())
                return;
            Fx fx = Fx.Make(p, "GiantFlail Swing");
            fx.ring = true;
            fx.radius = radius;
            fx.time = Mathf.Max(0.15f, time);
            fx.startDeg = startDeg;
            fx.clockwise = clockwise;
        }

        // Hit 3: the ball and chain reach from the hand to the spot over `windUp`; Impact plays
        // the slam there.
        public static void Reach(Player p, Vector2 spot, float windUp)
        {
            if (p == null || !Art())
                return;
            Fx fx = Fx.Make(p, "GiantFlail Slam");
            fx.spot = spot;
            fx.time = Mathf.Max(0.1f, windUp) + 0.25f;
            fx.reachTime = Mathf.Max(0.1f, windUp);
        }

        public static void Impact(Player p, Vector2 spot, float radius)
        {
            if (p == null || !Art())
                return;
            Fx fx = Fx.Make(p, "GiantFlail Impact");
            fx.impact = true;
            fx.spot = spot;
            fx.radius = radius;
            fx.time = slam.Length / 16f;
        }

        private class Fx : MonoBehaviour
        {
            public bool ring, impact, clockwise;
            public float radius, time, reachTime, startDeg;
            public Vector2 spot;
            private Player owner;
            private SpriteRenderer main, ball;
            private SpriteRenderer[] links = new SpriteRenderer[0];
            private float born;

            public static Fx Make(Player p, string name)
            {
                var go = new GameObject(name);
                Fx fx = go.AddComponent<Fx>();
                fx.owner = p;
                fx.born = Time.time;
                fx.main = fx.Part("Main", 1);
                return fx;
            }

            private SpriteRenderer Part(string name, int order)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                SpriteRenderer r = go.AddComponent<SpriteRenderer>();
                SpriteRenderer body = owner != null ? owner.spriteRenderer : null;
                if (body != null)
                {
                    r.sortingLayerID = body.sortingLayerID;
                    r.sortingOrder = body.sortingOrder + order;
                }
                return r;
            }

            private void Update()
            {
                float age = Time.time - born;
                if (owner == null || age >= time)
                {
                    Destroy(gameObject);
                    return;
                }
                float t = age / time;
                if (impact)
                {
                    transform.position = spot;
                    float s = radius * 16f / SlamPx;
                    main.transform.localScale = new Vector3(s, s, 1f);
                    main.sprite = slam[Mathf.Min(slam.Length - 1, (int)(t * slam.Length))];
                    return;
                }
                Vector2 hand = Heavy.Hand(owner);
                Vector2 tip;
                if (ring)
                {
                    // Round the wizard, the ring's frames turning with the ball.
                    Vector2 centre = owner.transform.position;
                    transform.position = centre;
                    float s = radius * 16f / RingPx;
                    main.transform.localScale = new Vector3(s, s, 1f);
                    // 0.5.0 (#92): the ball itself is drawn on the ring, turning smoothly once
                    // round from the aim (the ring's frames behind it as a faint motion blur).
                    int f = Mathf.Min(swing.Length - 1, (int)(t * swing.Length));
                    main.sprite = swing[f];
                    main.color = new Color(1f, 1f, 1f, GiantWeaponsPlugin.FlailBlurAlpha);
                    float deg = startDeg + (clockwise ? -360f : 360f) * t;
                    float angle = deg * Mathf.Deg2Rad;
                    // Frame f's bright head is drawn at f x 60 degrees counterclockwise; turn the
                    // ring so that head sits on the ball (mirrored for a clockwise swing, so its
                    // tail trails behind).
                    main.transform.localScale = new Vector3(s, clockwise ? -s : s, 1f);
                    main.transform.localRotation = Quaternion.Euler(0f, 0f, clockwise ? deg + f * 60f : deg - f * 60f);
                    tip = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (ball == null)
                        ball = Part("Ball", 2);
                    ball.transform.position = tip;
                    ball.sprite = head[(int)(age * 12f) % head.Length];
                }
                else
                {
                    // Out to the spot over the wind-up, then held there for the impact.
                    transform.position = Vector3.zero;
                    // 0.6.0 (#97): an OVER-THE-TOP swing. For the first RaiseShare of the wind-up the
                    // ball rises slowly (smoothstep) from the hand to high over the wizard's head;
                    // then it comes down fast (t^3) on an arc over the top onto the spot.
                    float u = Mathf.Clamp01(age / reachTime);
                    float raise = GiantWeaponsPlugin.RaiseShare;
                    Vector2 apex = (Vector2)owner.transform.position + new Vector2(0f, GiantWeaponsPlugin.OverheadHeight)
                        - (spot - hand).normalized * 0.4f;   // a little behind the head, so it comes over
                    if (u < raise)
                    {
                        float k = u / raise;
                        k = k * k * (3f - 2f * k);
                        tip = Vector2.Lerp(hand, apex, k);
                    }
                    else
                    {
                        float k = (u - raise) / (1f - raise);
                        k = k * k * k;
                        // A bowed path: the midpoint lifted so it reads as coming over the top.
                        Vector2 mid = (apex + spot) * 0.5f + new Vector2(0f, 0.6f);
                        tip = Vector2.Lerp(Vector2.Lerp(apex, mid, k), Vector2.Lerp(mid, spot, k), k);
                    }
                    if (ball != null)
                        ball.transform.localScale = Vector3.one * (u < 1f ? 1f + 0.25f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u / raise)) : 1f);
                    if (ball == null)
                        ball = Part("Ball", 2);
                    ball.transform.position = tip;
                    ball.sprite = head[(int)(age * 12f) % head.Length];
                }
                Chain(hand, tip, age);
            }

            // The lightning chain from the hand to the ball, tiled in 0.5 u links.
            private void Chain(Vector2 from, Vector2 to, float age)
            {
                Vector2 d = to - from;
                float length = d.magnitude;
                int count = Mathf.Clamp(Mathf.CeilToInt(length / 0.5f), 0, 24);
                if (links.Length < count)
                {
                    var more = new SpriteRenderer[count];
                    Array.Copy(links, more, links.Length);
                    for (int i = links.Length; i < count; i++)
                        more[i] = Part("Link", 0);
                    links = more;
                }
                float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                int frame = (int)(age * 14f);
                for (int i = 0; i < links.Length; i++)
                {
                    SpriteRenderer r = links[i];
                    r.enabled = i < count;
                    if (!r.enabled)
                        continue;
                    r.transform.position = from + d.normalized * (i * 0.5f);
                    r.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                    r.sprite = chain[(frame + i) % chain.Length];
                }
            }
        }
    }
}
