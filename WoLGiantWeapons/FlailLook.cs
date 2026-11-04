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
        // #106 (WoL Art, Log - Art 16:30 / 16:36): flail_swoop 6 x 96x96 (ground ring r 45 px,
        // squashed x0.6, rising 10 px behind; frame N's ball at 8 - 60N degrees, clockwise),
        // flail_whirl / _full 6 x 48x24 (pivot centre-bottom = top of the head, ball in the frames,
        // 16 fps), flail_slam_big 6 x 88x88 (hit ring r 42 px = 2.6 u, 14 fps). Each replaces the
        // older piece when it's in Assets.
        private const float SwoopPx = 45f, SlamBigPx = 42f;
        private static Sprite[] head, chain, swing, slam, swoop, whirlArt, whirlFull, slamBig;
        private static bool loaded;

        private static bool Art()
        {
            if (!loaded)
            {
                loaded = true;
                head = Frames("flail_head_big.png", 24, new Vector2(0.5f, 0.5f));
                headBig = head != null;
                if (head == null)
                    head = Frames("flail_head.png", 16, new Vector2(0.5f, 0.5f));
                chain = Frames("flail_chain.png", 8, new Vector2(0f, 0.5f));
                swing = Frames("flail_swing.png", 64, new Vector2(0.5f, 0.5f));
                slam = Frames("flail_slam.png", 48, new Vector2(0.5f, 0.5f));
                swoop = Frames("flail_swoop.png", 96, new Vector2(0.5f, 0.5f));
                whirlArt = Frames("flail_whirl.png", 48, new Vector2(0.5f, 0f));
                whirlFull = Frames("flail_whirl_full.png", 48, new Vector2(0.5f, 0f));
                slamBig = Frames("flail_slam_big.png", 88, new Vector2(0.5f, 0.5f));
                GiantWeaponsPlugin.Log($"Giant Lightning Flail: #106 art {(swoop != null ? "swoop" : "-")}/{(whirlArt != null ? "whirl" : "-")}/{(slamBig != null ? "slam_big" : "-")}/{(headBig ? "head_big" : "-")}");
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
        public static void Reach(Player p, Vector2 spot, float windUp, bool fromApex = false)
        {
            if (p == null || !Art())
                return;
            Fx fx = Fx.Make(p, "GiantFlail Slam");
            fx.spot = spot;
            fx.fromApex = fromApex;
            fx.time = Mathf.Max(0.1f, windUp) + 0.25f;
            fx.reachTime = Mathf.Max(0.1f, windUp);
        }

        // 0.11.0 (#106): the charge's whirl overhead, until Stop().
        public static Fx Whirl(Player p)
        {
            if (p == null || !Art())
                return null;
            Fx fx = Fx.Make(p, "GiantFlail Whirl");
            fx.whirl = true;
            fx.time = 30f;
            return fx;
        }

        // The ball is drawn 1.5x (Matt: "bigger ball") until WoL Art's 24 px flail_head_big lands.
        private static float BallScale => headBig ? 1f : 1.5f;
        private static bool headBig;

        public static void Impact(Player p, Vector2 spot, float radius)
        {
            if (p == null || !Art())
                return;
            Fx fx = Fx.Make(p, "GiantFlail Impact");
            fx.impact = true;
            fx.spot = spot;
            fx.radius = radius;
            fx.time = slamBig != null ? slamBig.Length / 14f : slam.Length / 16f;
        }

        internal class Fx : MonoBehaviour
        {
            public bool ring, impact, clockwise, fromApex, whirl;
            public float charge;   // 0-1 while whirling (#106)
            public float radius, time, reachTime, startDeg;
            public Vector2 spot;
            private Player owner;
            private SpriteRenderer main, ball;
            private float whirlAngle;
            public void Stop() => Destroy(gameObject);
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
                    Sprite[] set = slamBig ?? slam;
                    // 1:1 at the full-charge 2.6 u; the tap slam (2.0 u) at ~0.75, as Art specced.
                    float s = radius * 16f / (slamBig != null ? SlamBigPx : SlamPx);
                    main.transform.localScale = new Vector3(s, s, 1f);
                    main.sprite = set[Mathf.Min(set.Length - 1, (int)(t * set.Length))];
                    return;
                }
                Vector2 hand = Heavy.Hand(owner);
                Vector2 tip;
                if (whirl)
                {
                    // 0.11.0 (#106): charging: the ball circles above the head, faster and brighter
                    // as the charge fills; it stays until the slam takes over (Stop).
                    transform.position = Vector3.zero;
                    Vector2 over = (Vector2)owner.transform.position + new Vector2(0f, GiantWeaponsPlugin.OverheadHeight);
                    // Art's whirl: the ball on its chain circling overhead is in the frames, drawn
                    // 1:1 with its bottom on top of the wizard's head; _full once fully charged.
                    Sprite[] whirlSet = charge >= 1f && whirlFull != null ? whirlFull : whirlArt;
                    if (whirlSet != null)
                    {
                        SpriteRenderer body = owner.spriteRenderer;
                        float headTop = body != null ? body.bounds.max.y : owner.transform.position.y + 1.4f;
                        main.transform.position = new Vector2(owner.transform.position.x, headTop);
                        main.transform.localScale = Vector3.one;
                        main.color = Color.white;
                        main.sprite = whirlSet[(int)(age * (16f + 8f * charge)) % whirlSet.Length];
                        if (body != null)
                            main.sortingOrder = body.sortingOrder + 2;
                        if (ball != null)
                            ball.enabled = false;
                        Chain(hand, hand, age);   // no loose chain: it's in the frames
                        return;
                    }
                    whirlAngle += Time.deltaTime * (7f + 12f * charge);
                    tip = over + new Vector2(Mathf.Cos(whirlAngle) * 0.8f, Mathf.Sin(whirlAngle) * 0.3f);
                    if (ball == null)
                        ball = Part("Ball", 2);
                    ball.transform.position = tip;
                    ball.transform.localScale = Vector3.one * BallScale * (1f + 0.15f * charge);
                    ball.color = Color.Lerp(Color.white, new Color(1f, 1f, 0.6f), charge);
                    SortBall();
                    ball.sprite = head[(int)(age * (12f + 12f * charge)) % head.Length];
                    Chain(hand, tip, age);
                    return;
                }
                if (ring)
                {
                    // Round the wizard, the ring's frames turning with the ball.
                    Vector2 centre = owner.transform.position;
                    transform.position = centre;
                    if (swoop != null)
                    {
                        Swoop(centre, t, age);
                        return;
                    }
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
                    // 0.11.0 (#106): a SWOOPING path: lower and wider in front of the wizard (below
                    // them on screen), rising behind; the hits still cover the full circle.
                    float sy = Mathf.Sin(angle);
                    tip = centre + new Vector2(Mathf.Cos(angle) * radius, sy * radius * (sy < 0f ? 0.85f : 0.65f) - 0.15f);
                    if (ball == null)
                        ball = Part("Ball", 2);
                    ball.transform.position = tip;
                    ball.transform.localScale = Vector3.one * BallScale;
                    SortBall();
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
                    // After a charge the ball is already overhead: straight into the drop.
                    float raise = fromApex ? 0.0001f : GiantWeaponsPlugin.RaiseShare;
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
                        ball.transform.localScale = Vector3.one * BallScale * (u < 1f && !fromApex ? 1f + 0.25f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u / raise)) : 1f);
                    if (ball == null)
                        ball = Part("Ball", 2);
                    ball.transform.position = tip;
                    SortBall();
                    ball.sprite = head[(int)(age * 12f) % head.Length];
                }
                Chain(hand, tip, age);
            }

            // #106: Art's swooping trail, already shaped (squashed in front, rising behind), so never
            // rotated: the frame whose ball sits at the ball's angle is shown (frame N: 8 - 60N
            // degrees, clockwise; mirrored for a counterclockwise swing), 1:1 at a 2.8 u reach.
            // The ball (flail_head_big) rides the same path on top.
            private void Swoop(Vector2 centre, float t, float age)
            {
                float deg = startDeg + (clockwise ? -360f : 360f) * t;
                float k = radius * 16f / SwoopPx;
                main.transform.localRotation = Quaternion.identity;
                main.transform.localScale = new Vector3(clockwise ? k : -k, k, 1f);
                main.color = Color.white;
                float frameAt = clockwise ? (8f - deg) / 60f : (deg - 172f) / 60f;
                int f = ((Mathf.RoundToInt(frameAt) % swoop.Length) + swoop.Length) % swoop.Length;
                main.sprite = swoop[f];
                float a = deg * Mathf.Deg2Rad, sy = Mathf.Sin(a);
                // Art's path: radius 45 px across, x0.6 up and down, plus up to 10 px of rise behind.
                Vector2 tip = centre + new Vector2(Mathf.Cos(a) * radius, sy * radius * 0.6f + (sy > 0f ? sy * radius * (10f / SwoopPx) : 0f));
                if (ball == null)
                    ball = Part("Ball", 2);
                ball.enabled = true;
                ball.transform.position = tip;
                ball.transform.localScale = Vector3.one * BallScale;
                SortBall();
                ball.sprite = head[(int)(age * 8f) % head.Length];
                Chain(Heavy.Hand(owner), tip, age);
            }

            // 0.9.1 (#101 audit B13): the ball is drawn behind the wizard when it's above (behind)
            // them and in front when below, instead of always in front.
            private void SortBall()
            {
                SpriteRenderer body = owner != null ? owner.spriteRenderer : null;
                if (ball == null || body == null)
                    return;
                bool behind = ball.transform.position.y > owner.transform.position.y + 0.15f;
                ball.sortingOrder = body.sortingOrder + (behind ? -2 : 2);
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
