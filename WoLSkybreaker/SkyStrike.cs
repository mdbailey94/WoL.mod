using System.Collections.Generic;
using UnityEngine;

namespace WoLSkybreaker
{
    // One Skybreaker bolt, from the mark to the strike, living in the scene on its own (a level
    // load ends it, and its cinematic puts everything back as it goes).
    // - standard / enhanced (0.2.0, #64): a target circle the ring's size pulses on the floor for
    //   the delay, then ONE large bolt: the core burst (high damage) and the ring burst round it
    //   (small damage) under the game's lightning strike, thunder and a shake, nothing else;
    // - signature: the stage darkens and the camera closes in while the storm gathers for
    //   GatherTime (the circle, lightning arcing into the spot), then one colossal bolt with a
    //   deep thunder, hit-stop, flash and slow motion (aftershocks are off by default).
    // Every effect is the game's own and is tried on its own; the first failure of each is logged.
    public class SkyStrike : MonoBehaviour
    {
        public enum Kind { Standard, Enhanced, Signature }

        // Skill levels (SkillStats in the plugin).
        private const int CoreLevel = 1, RingLevel = 2, EnhancedCoreLevel = 3;
        private const int SignatureCoreLevel = 4, SignatureRingLevel = 5, AftershockLevel = 6;
        private const float SparkInterval = 0.06f;     // seconds between sparks over the mark
        private const float ArcInterval = 0.1f;        // signature: seconds between arcs into the spot
        private const float ArcReach = 3.5f;           // where they start, this far out
        private const float AftershockBoltSize = 1f;
        private const float Life = 3f;                 // gone by then whatever happened

        private static readonly HashSet<string> failed = new HashSet<string>();
        private static bool loggedBolt;

        private Player owner;
        private string category, skillID;
        private Vector2 spot;
        private Kind kind;
        private float delay, time, nextSpark, nextArc;
        private bool struck, aftershocksDone;
        private SignatureCinematic cinematic;

        public static void Call(Player owner, string skillID, Vector2 spot, Kind kind)
        {
            try
            {
                var strike = new GameObject("Skybreaker Strike").AddComponent<SkyStrike>();
                strike.Init(owner, skillID, spot, kind);
            }
            catch (System.Exception e)
            {
                SkybreakerPlugin.Log($"Skybreaker: couldn't call the bolt down ({e.Message})");
            }
        }

        private void Init(Player player, string id, Vector2 at, Kind which)
        {
            owner = player;
            category = player.skillCategory;
            skillID = id;
            spot = at;
            kind = which;
            transform.position = at;
            delay = kind == Kind.Signature ? SkybreakerPlugin.SignatureGather
                : kind == Kind.Enhanced ? SkybreakerPlugin.EnhancedDelay : SkybreakerPlugin.Delay;
            if (kind == Kind.Signature && SkybreakerPlugin.Cinematic)
            {
                cinematic = SignatureCinematic.Begin();
                if (cinematic != null)
                {
                    cinematic.TakeOff(SkybreakerPlugin.StageDarken);
                    cinematic.Windup(SkybreakerPlugin.CameraZoom);
                }
                Sound("ThunderClap", spot, 0.4f, 0.7f);
            }
            // #64: the target circle on the floor, the ring's size, until the bolt lands.
            ShowTarget();
            Sound("LightningBurst", spot, 0.5f, 1.3f);
            Destroy(gameObject, Life + delay);
            SkybreakerPlugin.Log($"Skybreaker: cast ({kind.ToString().ToLowerInvariant()}), bolt in {delay:0.00} s at " +
                $"({spot.x:0.0}, {spot.y:0.0}), core {CoreSize:0.0} u, ring {RingSize:0.0} u");
        }

        private float CoreSize => kind == Kind.Signature ? SkybreakerPlugin.SignatureCoreSize
            : kind == Kind.Enhanced ? SkybreakerPlugin.EnhancedCoreSize : SkybreakerPlugin.CoreSize;
        private float RingSize => kind == Kind.Signature ? SkybreakerPlugin.SignatureRingSize
            : kind == Kind.Enhanced ? SkybreakerPlugin.EnhancedRingSize : SkybreakerPlugin.RingSize;

        private void Update()
        {
            time += Time.deltaTime;
            if (!struck)
            {
                Gather();
                if (time >= delay)
                    Strike();
                return;
            }
            if (kind == Kind.Signature && !aftershocksDone && time >= delay + SkybreakerPlugin.SignatureAftershockDelay)
                Aftershocks();
        }

        // The target circle pulses faster as the bolt nears (#64: no sparks over the mark any
        // more, the circle is the telegraph); the signature's storm also throws arcs of lightning
        // into the spot.
        private void Gather()
        {
            float near = Mathf.Clamp01(time / Mathf.Max(delay, 0.01f));
            if (target != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * Mathf.Lerp(8f, 30f, near));
                target.color = new Color(TargetColour.r, TargetColour.g, TargetColour.b, Mathf.Lerp(0.45f, 0.95f, pulse));
            }
            if (kind == Kind.Signature && time >= nextArc)
            {
                nextArc = time + ArcInterval;
                Vector2 from = spot + Random.insideUnitCircle.normalized * ArcReach;
                Line(from, spot);
            }
        }

        private void Strike()
        {
            struck = true;
            bool sig = kind == Kind.Signature;
            int coreLevel = sig ? SignatureCoreLevel : kind == Kind.Enhanced ? EnhancedCoreLevel : CoreLevel;
            int ringLevel = sig ? SignatureRingLevel : RingLevel;
            // A burst's hit radius is its scale (ElementalBurst sets the collider radius to it).
            Burst(spot, coreLevel, CoreSize * 0.5f, sig);
            Burst(spot, ringLevel, RingSize * 0.5f, sig);
            // #64: ONE large bolt and nothing else (no extra sparks or scorch marks).
            HideTarget();
            Bolt(spot, sig ? SkybreakerPlugin.SignatureBoltSize : SkybreakerPlugin.BoltSize);
            if (sig)
            {
                // A deeper thunder: the game's clap pitched well down, under a low earth boom.
                Sound("ThunderClap", spot, -1f, 0.5f);
                Sound("EarthExplosion", spot, -1f, 0.55f);
            }
            else
                Sound("ThunderClap", spot, -1f, 1.05f);
            Shake(sig ? 2.4f : 0.6f);
            string did = "";
            if (cinematic != null)
            {
                did = ", " + cinematic.Impact(SkybreakerPlugin.HitStopFrames, SkybreakerPlugin.ImpactFlash, SkybreakerPlugin.ImpactSlowMo);
                if (SkybreakerPlugin.SignatureAftershocks == 0)
                    cinematic.Finish("landed");
            }
            if (!sig)
                aftershocksDone = true;
            SkybreakerPlugin.Log($"Skybreaker: bolt struck{did}");
        }

        private void Aftershocks()
        {
            aftershocksDone = true;
            int count = SkybreakerPlugin.SignatureAftershocks;
            float radius = RingSize * 0.5f + SkybreakerPlugin.SignatureAftershockSize * 0.25f;
            float turn = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                float angle = turn + i * Mathf.PI * 2f / count;
                // The game's 3/4 view: the ring is squashed top to bottom.
                Vector2 at = spot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.7f) * radius;
                Burst(at, AftershockLevel, SkybreakerPlugin.SignatureAftershockSize * 0.5f, true);
                Bolt(at, AftershockBoltSize);
                Effect("BurnMarkXSmallEmitter", 1, at);
            }
            if (count > 0)
            {
                Shake(0.8f);
                SkybreakerPlugin.Log($"Skybreaker: {count} aftershock bolt(s)");
            }
            cinematic?.Finish("landed");
        }

        private void OnDestroy()
        {
            HideTarget();
            if (cinematic != null)
                cinematic.Finish(struck ? "landed" : "cut short");
        }

        // ---- The target circle (#64) ----

        private const float PixelsPerUnit = 16f;
        private static readonly Color TargetColour = new Color32(245, 231, 44, 255);   // the game's lightning yellow
        private static readonly Dictionary<int, Sprite> rings = new Dictionary<int, Sprite>();
        private SpriteRenderer target;

        // A true circle (the burst's hit area is one), RingSize across, on the game's ground
        // telegraph layer, under the wizards and enemies.
        private void ShowTarget()
        {
            try
            {
                int diameter = Mathf.Max(8, Mathf.RoundToInt(RingSize * PixelsPerUnit));
                var go = new GameObject("Skybreaker Target");
                go.transform.position = new Vector3(spot.x, spot.y, 0f);
                go.transform.SetParent(transform, true);
                target = go.AddComponent<SpriteRenderer>();
                target.sprite = Ring(diameter);
                target.color = TargetColour;
                int layer = SortingLayer.NameToID("TargetLineGround");
                if (SortingLayer.IsValid(layer))
                    target.sortingLayerID = layer;
                else if (owner != null && owner.spriteRenderer != null)
                {
                    target.sortingLayerID = owner.spriteRenderer.sortingLayerID;
                    target.sortingOrder = owner.spriteRenderer.sortingOrder - 50;
                }
            }
            catch (System.Exception e)
            {
                Failed("target circle", e);
            }
        }

        private void HideTarget()
        {
            if (target != null)
                Destroy(target.gameObject);
            target = null;
        }

        // A 1-px outline with a second ring just inside it and a faint fill, made once per size.
        private static Sprite Ring(int diameter)
        {
            Sprite sprite;
            if (rings.TryGetValue(diameter, out sprite) && sprite != null)
                return sprite;
            var texture = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[diameter * diameter];
            float c = (diameter - 1) * 0.5f, r = diameter * 0.5f;
            for (int y = 0; y < diameter; y++)
                for (int x = 0; x < diameter; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    byte a = d > r ? (byte)0 : d > r - 1f ? (byte)255 : d > r - 3f && d <= r - 2f ? (byte)140 : (byte)36;
                    pixels[y * diameter + x] = new Color32(255, 255, 255, a);
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            rings[diameter] = sprite;
            return sprite;
        }

        // ---- The game's effects ----

        // A lightning burst; the signature's are marked as the game marks its own (isUltimate), so
        // they don't refill the gauge they just spent.
        private void Burst(Vector2 at, int level, float radius, bool ultimate)
        {
            try
            {
                LightningBurst.CreateBurst(at, category, skillID, level, radius, ultimate);
            }
            catch (System.Exception e)
            {
                Failed("burst", e);
            }
        }

        // The game's lightning bolt from the sky, just its animation.
        private static void Bolt(Vector2 position, float size)
        {
            try
            {
                if (LightningStrike.Prefab == null)
                    return;
                GameObject go = Object.Instantiate(LightningStrike.Prefab, position, Quaternion.identity);
                foreach (MonoBehaviour script in go.GetComponentsInChildren<MonoBehaviour>(true))
                    Object.Destroy(script);
                foreach (Collider2D collider in go.GetComponentsInChildren<Collider2D>(true))
                    Object.Destroy(collider);
                foreach (Animator anim in go.GetComponentsInChildren<Animator>(true))
                    anim.enabled = true;
                go.transform.localScale = new Vector3(size, size, 1f);
                if (!go.activeSelf)
                    go.SetActive(true);
                Object.Destroy(go, 0.7f);
                if (!loggedBolt)
                {
                    loggedBolt = true;
                    SkybreakerPlugin.Log("Skybreaker: using the game's lightning bolts");
                }
            }
            catch (System.Exception e)
            {
                if (!loggedBolt)
                {
                    loggedBolt = true;
                    SkybreakerPlugin.Log($"Skybreaker: the game's lightning bolt is unavailable, bursts only: {e.Message}");
                }
            }
        }

        private static void Effect(string pool, int count, Vector2 position)
        {
            try
            {
                PoolManager.GetPoolItem<ParticleEffect>(pool).Emit(count, (Vector3?)(Vector3)position, null, null, 0f, null);
            }
            catch (System.Exception e)
            {
                Failed(pool, e);
            }
        }

        // The game's lightning line (TeleStrike's) from one spot to another.
        private static void Line(Vector2 from, Vector2 to)
        {
            try
            {
                PoolManager.GetPoolItem<SectionedTrailEmitter>("LightningLine").Emit(from, to);
            }
            catch (System.Exception e)
            {
                Failed("LightningLine", e);
            }
        }

        private static void Sound(string id, Vector2 at, float volume, float pitch)
        {
            try
            {
                SoundManager.PlayAudioWithDistance(id, new Vector2?(at), null, 24f, volume, pitch, false);
            }
            catch (System.Exception e)
            {
                Failed("sound " + id, e);
            }
        }

        private static void Shake(float strength)
        {
            try
            {
                CameraController.ShakeCamera(strength, false);
            }
            catch (System.Exception e)
            {
                Failed("shake", e);
            }
        }

        private static void Failed(string what, System.Exception e)
        {
            if (failed.Add(what))
                SkybreakerPlugin.Log($"Skybreaker: {what} unavailable: {e.Message}");
        }
    }
}
