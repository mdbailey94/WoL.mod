using System.Collections.Generic;
using UnityEngine;

namespace WoLThunderhead
{
    // The dark storm cloud the wizard rides while up in the air: a solid pixel-art cloud drawn in
    // the game's style (shaded, no dark outline), one puff in front of their feet and one behind,
    // carried up with their lifted picture. It billows gently, flickers with lightning now and
    // then, and fades in as they rise and out as they land. The signature storm's cloud is bigger,
    // flickers much more often and swells as the storm goes on (Growth, set by the state).
    // The standard and enhanced clouds also show the lightning inside: a drawn bolt crawling
    // across the cloud's belly and forking down out of it with each flicker and each volley
    // (Pulse, from the state).
    public class StormCloud
    {
        private const float StormySize = 1.4f;       // the signature cloud's size, times CloudSize
        private const float FlashGapMin = 0.35f;     // seconds between flickers
        private const float FlashGapMax = 0.9f;
        private const float StormyFlashGapMin = 0.1f;
        private const float StormyFlashGapMax = 0.35f;

        private static readonly Color32 Dark = new Color32(46, 50, 66, 255);
        private static readonly Color32 Mid = new Color32(72, 78, 98, 255);
        private static readonly Color32 Light = new Color32(108, 114, 138, 255);
        private static readonly Color32 Rim = new Color32(146, 152, 176, 255);
        private static readonly Color Flash = new Color(1f, 0.96f, 0.7f, 1f);
        private static Sprite front, back;

        private readonly SpriteRenderer body;
        private readonly GameObject root;
        private readonly SpriteRenderer frontPuff, backPuff;
        private readonly bool stormy;
        private readonly float size;
        private float flashUntil;
        private float nextFlash;
        // The drawn lightning inside (not the signature's cloud): a child of the front puff, so it
        // billows with it and goes when the cloud does.
        private readonly SpriteRenderer innerBolt;
        private Sprite[] boltFrames;
        private float boltStart = -1f;
        private const float FlashTime = 0.06f;        // seconds the cloud stays lit by a flicker
        private const float InnerBoltHeight = 1f;     // pixels above the front puff's pivot

        // A multiplier on the cloud's size, for the signature storm's cloud growing as it rages.
        public float Growth = 1f;

        private StormCloud(Player player, bool stormy)
        {
            this.stormy = stormy;
            body = player.spriteRenderer;
            float ppu = body.sprite != null ? body.sprite.pixelsPerUnit : 16f;
            if (front == null)
            {
                front = Make(40, 14, ppu, new[] { new Vector3(7, 6, 6), new Vector3(15, 8, 7), new Vector3(24, 8, 7),
                    new Vector3(32, 6, 6), new Vector3(20, 5, 6), new Vector3(11, 4, 5), new Vector3(29, 4, 5) });
                back = Make(34, 12, ppu, new[] { new Vector3(7, 6, 6), new Vector3(16, 8, 7), new Vector3(26, 6, 6),
                    new Vector3(19, 4, 5) });
            }
            root = new GameObject("Thunderhead Cloud");
            root.transform.SetParent(body.transform, false);
            size = ThunderheadPlugin.CloudSize * (stormy ? StormySize : 1f);
            root.transform.localScale = new Vector3(size, size, 1f);
            frontPuff = Puff("Front", front, new Vector3(0f, 1f / ppu, 0f));
            backPuff = Puff("Back", back, new Vector3(2f / ppu, 6f / ppu, 0f));
            if (!stormy && ThunderheadPlugin.Drawn)
            {
                try
                {
                    ThunderArt.Use(ppu);
                    var go = new GameObject("Inner Bolt");
                    go.transform.SetParent(frontPuff.transform, false);
                    go.transform.localPosition = new Vector3(0f, InnerBoltHeight / ppu, 0f);
                    innerBolt = go.AddComponent<SpriteRenderer>();
                }
                catch (System.Exception e)
                {
                    Fx.Fail("cloud lightning", e);
                    innerBolt = null;
                }
            }
            Update(0f, null);
        }

        // A volley struck: the cloud lights up and lightning crawls through it (standard and
        // enhanced clouds only).
        public void Pulse()
        {
            if (stormy || root == null)
                return;
            flashUntil = Time.time + FlashTime;
            StartBolt(Time.time);
        }

        private void StartBolt(float now)
        {
            if (innerBolt == null)
                return;
            try
            {
                Sprite[][] variants = ThunderArt.CloudBolts();
                boltFrames = variants[Random.Range(0, variants.Length)];
                boltStart = now;
                Fx.Ready("cloud lightning", ThunderArt.Count(variants), ThunderArt.Ppu, body);
            }
            catch (System.Exception e)
            {
                Fx.Fail("cloud lightning", e);
                boltStart = -1f;
            }
        }

        // The inner bolt's frames, as bright as the cloud is solid, just in front of the front puff.
        private void UpdateBolt(float now, float alpha)
        {
            if (innerBolt == null)
                return;
            int frame = boltStart >= 0f && boltFrames != null ? Mathf.FloorToInt((now - boltStart) / ThunderArt.CloudBoltFrameTime) : -1;
            if (frame < 0 || frame >= boltFrames.Length)
            {
                boltStart = -1f;
                innerBolt.sprite = null;
                return;
            }
            innerBolt.sprite = boltFrames[frame];
            innerBolt.color = new Color(1f, 1f, 1f, alpha);
            innerBolt.sortingLayerID = frontPuff.sortingLayerID;
            innerBolt.sortingOrder = frontPuff.sortingOrder + 1;
        }

        // stormy: the signature storm's cloud.
        public static StormCloud On(Player player, bool stormy = false)
        {
            try
            {
                return player != null && player.spriteRenderer != null && ThunderheadPlugin.Cloud ? new StormCloud(player, stormy) : null;
            }
            catch (System.Exception e)
            {
                ThunderheadPlugin.Log($"Thunderhead: storm cloud unavailable: {e.Message}");
                return null;
            }
        }

        // Every frame in the air; lift is 0 on the ground to 1 at the top.
        public void Update(float lift, Levitator levitator)
        {
            if (root == null || body == null)
                return;
            float now = Time.time;
            float grown = size * Growth;
            root.transform.localScale = new Vector3(grown, grown, 1f);
            // Billowing: the puffs drift a little against each other.
            float ppu = front.pixelsPerUnit;
            frontPuff.transform.localPosition = new Vector3(Mathf.Sin(now * 2.1f) * 1.5f / ppu, (1f + Mathf.Sin(now * 3.3f)) / ppu, 0f);
            backPuff.transform.localPosition = new Vector3((2f + Mathf.Sin(now * 1.7f + 1f) * 1.5f) / ppu, (6f + Mathf.Sin(now * 2.6f + 2f)) / ppu, 0f);
            // Lightning flickering inside now and then.
            if (now >= nextFlash)
            {
                nextFlash = now + (stormy ? Random.Range(StormyFlashGapMin, StormyFlashGapMax) : Random.Range(FlashGapMin, FlashGapMax));
                flashUntil = now + FlashTime;
                StartBolt(now);
            }
            Color tint = now < flashUntil ? Flash : Color.white;
            // Solid at the top, fading in and out with the rise and the landing.
            float alpha = Mathf.Clamp01(lift * 1.4f);
            frontPuff.color = new Color(tint.r, tint.g, tint.b, alpha);
            backPuff.color = new Color(tint.r * 0.85f, tint.g * 0.85f, tint.b * 0.85f, alpha);
            // In front of the wizard's feet, and behind them, wherever the wizard is drawn.
            int layer = levitator != null ? levitator.DrawLayer : body.sortingLayerID;
            int order = levitator != null ? levitator.DrawOrder : body.sortingOrder;
            frontPuff.sortingLayerID = layer;
            frontPuff.sortingOrder = order + 2;
            backPuff.sortingLayerID = layer;
            backPuff.sortingOrder = order - 1;
            try
            {
                UpdateBolt(now, alpha);
            }
            catch (System.Exception e)
            {
                Fx.Fail("cloud lightning", e);
                if (innerBolt != null)
                    innerBolt.sprite = null;
                boltStart = -1f;
            }
        }

        public void Off()
        {
            if (root != null)
                Object.Destroy(root);
        }

        private SpriteRenderer Puff(string name, Sprite sprite, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return renderer;
        }

        // A cloud from round blobs (x, y, radius in pixels), shaded in bands by depth below its
        // top edge in each column: a pale rim, a lit band, mid grey, and dark underneath.
        private static Sprite Make(int w, int h, float ppu, Vector3[] blobs)
        {
            var pixels = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                int top = -1;
                for (int y = h - 1; y >= 0 && top < 0; y--)
                    if (Covered(x, y, blobs))
                        top = y;
                if (top < 0)
                    continue;
                for (int y = 0; y <= top; y++)
                {
                    if (!Covered(x, y, blobs))
                        continue;
                    int depth = top - y;
                    Color32 c = depth == 0 ? Rim : depth <= 2 ? Light : y <= 2 ? Dark : depth <= 5 ? Mid : Dark;
                    pixels[y * w + x] = c;
                }
            }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.35f), ppu);
        }

        private static bool Covered(int x, int y, IEnumerable<Vector3> blobs)
        {
            foreach (Vector3 b in blobs)
            {
                float dx = x + 0.5f - b.x, dy = y + 0.5f - b.y;
                if (dx * dx + dy * dy * 1.6f <= b.z * b.z)
                    return true;
            }
            return false;
        }
    }
}
