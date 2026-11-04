using System.Collections.Generic;
using UnityEngine;

namespace WoLBuckler
{
    // WoL Art's buckler (Log - Art 13:48, 'Giant weapons (2026-11-03)'), 16 px per unit, Point
    // filter, centre pivot: the buckler (14x14) and the punch (4 x 14x14: pull back, the smash with
    // sparks on its right edge, hold, recover; turned to the punch).
    internal static class BucklerArt
    {
        public static Sprite Shield;
        public static Sprite[] Punch = new Sprite[0];
        // #94 (Log - Art 07:52 / 05:14): the held buckler in 8 facings (16x16, clockwise from right:
        // right, down-right, down, down-left, left, up-left, up, up-right; 5-7 drawn behind the
        // wizard) and the tossed disc's spin (16x16 base, 26x26 enhanced, 24 fps, never rotated).
        public static Sprite[] Held = new Sprite[0];
        public static Sprite[] Disc = new Sprite[0];
        public static Sprite[] DiscBig = new Sprite[0];
        public const float ArtPixels = 16f;      // the new strips' native buckler size
        public const float DiscFps = 24f;

        public static void Load(BepInEx.Logging.ManualLogSource log)
        {
            Texture2D texture = BucklerPlugin.LoadTexture("buckler.png");
            if (texture != null)
                Shield = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 16f);
            Texture2D strip = BucklerPlugin.LoadTexture("buckler_punch.png");
            if (strip != null && strip.height > 0 && strip.width >= strip.height)
            {
                Punch = new Sprite[strip.width / strip.height];
                for (int i = 0; i < Punch.Length; i++)
                    Punch[i] = Sprite.Create(strip, new Rect(i * strip.height, 0, strip.height, strip.height), new Vector2(0.5f, 0.5f), 16f);
            }
            Held = Strip("buckler_held_8dir.png");
            Disc = Strip("buckler_disc.png");
            DiscBig = Strip("buckler_disc_big.png");
            log?.LogInfo($"Buckler art: buckler {(Shield != null ? "loaded" : "missing")}, punch {Punch.Length} frames, " +
                $"held {Held.Length} facings, disc {Disc.Length} + big {DiscBig.Length} frames");
        }

        // Square frames side by side, centre pivot, 16 px per unit.
        private static Sprite[] Strip(string file)
        {
            Texture2D strip = BucklerPlugin.LoadTexture(file);
            if (strip == null || strip.height <= 0 || strip.width < strip.height)
                return new Sprite[0];
            var frames = new Sprite[strip.width / strip.height];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(strip, new Rect(i * strip.height, 0, strip.height, strip.height), new Vector2(0.5f, 0.5f), 16f);
            return frames;
        }

        // The facing index for an aim: 0 right, then clockwise in screen space (1 down-right ... 7 up-right).
        public static int Facing(Vector2 aim)
        {
            float degrees = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            return ((Mathf.RoundToInt(-degrees / 45f) % 8) + 8) % 8;
        }

        public static bool HeldBehind(int facing) => facing >= 5;

        // A punch frame (0 pull back, 1 smash, 2 hold, 3 recover), or the plain buckler.
        public static Sprite PunchFrame(int i) => Punch.Length >= 4 ? Punch[i] : Shield;
    }

    internal static class Buckler
    {
        // The base buckler is drawn 1.15x (14 px, about 1 u across); enhanced, SizeMultiplier
        // times that, its hits and guard with it.
        public const float BaseScale = 1.15f;

        public static float Size(bool enhanced) => enhanced ? BucklerPlugin.EnhancedSize : 1f;

        // Where the wizard's hands are (the game's attack origin); the feet were too low (Matt,
        // 0.2.0).
        public static Vector2 Hand(Player owner) =>
            owner.attackOriginTrans != null ? (Vector2)owner.attackOriginTrans.position : (Vector2)owner.transform.position;

        // Hits 1 and 2: the game's earth burst where the buckler lands, then the buckler is held
        // up in front (breaking shots at its face) through the pause before the next hit.
        public static void Punch(Player owner, string skillID, Vector2 aim, bool enhanced)
        {
            Vector2 at = Hand(owner) + aim * BucklerPlugin.PunchReach * Size(enhanced);
            Burst(owner, skillID, at, enhanced ? 4 : 1, BucklerPlugin.PunchRadius * Size(enhanced));
            Sound("ImpactEarthAlt", at, enhanced ? 0.9f : 1.2f);
            BucklerGuard.Raise(owner, skillID, aim, enhanced);
        }

        internal static void Burst(Player owner, string skillID, Vector2 at, int level, float radius)
        {
            try
            {
                EarthBurst.CreateBurst(at, owner.skillCategory, skillID, level, radius);
            }
            catch (System.Exception e)
            {
                BucklerPlugin.Log($"Buckler: a burst failed: {e.Message}");
            }
        }

        internal static void Sound(string name, Vector2 at, float pitch)
        {
            try
            {
                SoundManager.PlayAudioWithDistance(name, at, null, 24f, -1f, pitch);
            }
            catch
            {
            }
        }

        // ---- Enemy shots breaking on the buckler ----

        private const float ShotScanInterval = 0.1f;
        private static Projectile[] shots = new Projectile[0];
        private static float nextShotScan;

        // Enemy shots within reach of the buckler break, with the checks of Stone Charge's shield
        // (WoLStoneCharge NegateShots): not one that ignores negation, is already returning, or is
        // a wizard's own; a boomerang is sent back instead. With a facing, only a shot in front of
        // the hands breaks (the held buckler is directional, 0.2.0). Enhanced: shrapnel.
        public static void BreakShots(Player owner, string skillID, Vector2 at, float reach, Vector2? facing, bool enhanced)
        {
            if (Time.time >= nextShotScan)
            {
                nextShotScan = Time.time + ShotScanInterval;
                shots = Object.FindObjectsOfType<Projectile>();
            }
            foreach (Projectile p in shots)
            {
                if (p == null || !p.gameObject.activeInHierarchy || p.ignoreNegate || p.forcedReturn
                    || p.parentObject == owner.gameObject || p.attackBox == null || p.attackBox.atkInfo == null)
                    continue;
                string category = p.attackBox.atkInfo.skillCategory;
                if (category != null && category.Contains("Player"))
                    continue;
                Vector2 spot = p.transform.position;
                if (Vector2.Distance(spot, at) > reach + p.thickness)
                    continue;
                if (facing.HasValue && Vector2.Dot(spot - Hand(owner), facing.Value) < 0.1f)
                    continue;
                try
                {
                    PoolManager.GetPoolItem<HitSparkEmitter>().EmitSingle(HitSparkType.Large, spot, spot);
                }
                catch
                {
                }
                Sound("ImpactNegate", spot, 1f);
                try
                {
                    if (p.boomerang)
                        p.ForceProjectileToReturn();
                    else
                        p.ResetProjectile();
                }
                catch (System.Exception e)
                {
                    BucklerPlugin.Log($"Buckler: couldn't break a shot ({e.Message})");
                    continue;
                }
                BucklerPlugin.Log("Buckler: broke an enemy shot" + (enhanced ? ", shrapnel" : ""));
                if (enhanced)
                    Burst(owner, skillID, spot, 3, BucklerPlugin.ShrapnelRadius);
            }
        }

        // Enemies within reach of a swept segment.
        internal static List<GameObject> EnemiesNear(Vector2 from, Vector2 to, float reach)
        {
            var found = new List<GameObject>();
            if (GameController.enemies == null)
                return found;
            Vector2 seg = to - from;
            float len2 = Mathf.Max(seg.sqrMagnitude, 0.0001f);
            foreach (GameObject go in GameController.enemies)
            {
                if (go == null || !go.activeInHierarchy)
                    continue;
                Vector2 p = go.transform.position;
                float t = Mathf.Clamp01(Vector2.Dot(p - from, seg) / len2);
                if (Vector2.Distance(p, from + seg * t) <= reach + 0.3f)
                    found.Add(go);
            }
            return found;
        }

        internal static void Sort(SpriteRenderer r, Player owner, int offset)
        {
            SpriteRenderer wizard = owner.GetComponentInChildren<SpriteRenderer>();
            if (wizard != null)
            {
                r.sortingLayerID = wizard.sortingLayerID;
                r.sortingOrder = wizard.sortingOrder + offset;
            }
        }
    }

    // The buckler after a punch: it smashes out to the punch's reach, then is held up in front of
    // the hands, turned to the aim, for HoldTime (the pause before the next hit), breaking enemy
    // shots at its face. A wizard holds one at a time: the next punch or the toss puts it away.
    internal class BucklerGuard : MonoBehaviour
    {
        private const float SmashTime = 0.12f, RecoverTime = 0.1f, HoldAt = 0.65f;
        private static readonly Dictionary<int, BucklerGuard> held = new Dictionary<int, BucklerGuard>();
        private Player owner;
        private string skillID;
        private Vector2 aim;
        private bool enhanced;
        private float born;
        private SpriteRenderer look;

        public static void PutAway(Player owner)
        {
            BucklerGuard old;
            if (owner != null && held.TryGetValue(owner.playerID, out old) && old != null)
                Destroy(old.gameObject);
        }

        public static void Raise(Player owner, string skillID, Vector2 aim, bool enhanced)
        {
            PutAway(owner);
            var go = new GameObject("Buckler Guard");
            BucklerGuard guard = go.AddComponent<BucklerGuard>();
            held[owner.playerID] = guard;
            guard.owner = owner;
            guard.skillID = skillID;
            guard.aim = aim;
            guard.enhanced = enhanced;
            guard.born = Time.time;
            float scale = Buckler.BaseScale * Buckler.Size(enhanced);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            guard.look = go.AddComponent<SpriteRenderer>();
            guard.look.sprite = BucklerArt.PunchFrame(1);
            // The smash's sparks are drawn on the right edge: turned to the aim, and mirrored
            // top to bottom when punching left so its light stays on top.
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            guard.look.flipY = aim.x < -0.01f;
            Buckler.Sort(guard.look, owner, aim.y > 0.3f ? -1 : 1);
            guard.Place();
        }

        private float Reach => BucklerPlugin.PunchReach * Buckler.Size(enhanced);

        // Out to the punch's reach during the smash, then back to where it's held.
        private Vector2 Place()
        {
            float age = Time.time - born;
            float d = age < SmashTime ? Mathf.Lerp(0.3f, Reach, age / SmashTime)
                : Mathf.Lerp(Reach, Reach * HoldAt, Mathf.Clamp01((age - SmashTime) / 0.1f));
            Vector2 at = Buckler.Hand(owner) + aim * d;
            transform.position = at;
            return at;
        }

        private void Update()
        {
            float age = Time.time - born;
            if (owner == null || age >= BucklerPlugin.HoldTime)
            {
                Destroy(gameObject);
                return;
            }
            Vector2 at = Place();
            if (age >= SmashTime && BucklerArt.Held.Length >= 8 && BucklerPlugin.HeldArt)
                ShowHeld();
            else
                look.sprite = BucklerArt.PunchFrame(age < SmashTime ? 1 : age > BucklerPlugin.HoldTime - RecoverTime ? 3 : 2);
            Buckler.BreakShots(owner, skillID, at, BucklerPlugin.GuardRadius * Buckler.Size(enhanced), aim, enhanced);
        }

        // #94: Art's held buckler for the aim's facing, upright (the turn is in the frames), at the
        // native 16 px (enhanced: times its size), in front of the wizard or behind for the up facings.
        private bool heldShown;
        private void ShowHeld()
        {
            if (heldShown)
                return;
            heldShown = true;
            int facing = BucklerArt.Facing(aim);
            look.sprite = BucklerArt.Held[facing];
            look.flipY = false;
            transform.rotation = Quaternion.identity;
            float scale = Buckler.Size(enhanced);
            transform.localScale = new Vector3(scale, scale, 1f);
            Buckler.Sort(look, owner, BucklerArt.HeldBehind(facing) ? -1 : 1);
        }

        private void OnDestroy()
        {
            BucklerGuard current;
            if (owner != null && held.TryGetValue(owner.playerID, out current) && current == this)
                held.Remove(owner.playerID);
        }
    }

    // Hit 3: the buckler tossed like a disc. It spins out along the aim, hitting each enemy it
    // passes (an earth burst, once each way) and breaking every enemy shot it meets, then comes
    // back to the hands.
    internal class BucklerDisc : MonoBehaviour
    {
        private const float MaxLife = 3f, CatchDistance = 0.6f, SpinSpeed = -900f;
        private Player owner;
        private string skillID;
        private Vector2 dir;
        private float born, distance;
        private bool returning, enhanced;
        private readonly HashSet<int> hitOut = new HashSet<int>(), hitBack = new HashSet<int>();
        private SpriteRenderer look;
        private Sprite[] frames = new Sprite[0];

        public static void Toss(Player owner, string skillID, Vector2 aim, bool enhanced)
        {
            BucklerGuard.PutAway(owner);
            Vector2 hand = Buckler.Hand(owner);
            var go = new GameObject("Buckler Disc");
            go.transform.position = hand;
            float scale = Buckler.BaseScale * Buckler.Size(enhanced);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            BucklerDisc disc = go.AddComponent<BucklerDisc>();
            disc.owner = owner;
            disc.skillID = skillID;
            disc.dir = aim;
            disc.enhanced = enhanced;
            disc.born = Time.time;
            SpriteRenderer look = go.AddComponent<SpriteRenderer>();
            look.sprite = BucklerArt.Shield;
            disc.look = look;
            // #94: Art's spin strip (the native 26 px one when enhanced, so it isn't scaled up),
            // sized to the same width the buckler had: base 16 px, enhanced 16 x EnhancedSize.
            disc.frames = enhanced && BucklerArt.DiscBig.Length > 0 ? BucklerArt.DiscBig : BucklerArt.Disc;
            if (disc.frames.Length > 0 && BucklerPlugin.HeldArt)
            {
                float px = disc.frames[0].rect.width;
                float s = BucklerArt.ArtPixels * Buckler.Size(enhanced) / px;
                go.transform.localScale = new Vector3(s, s, 1f);
                look.sprite = disc.frames[0];
            }
            else
                disc.frames = new Sprite[0];
            Buckler.Sort(look, owner, 1);
            Buckler.Sound("EarthPebble", hand, 0.8f);
            BucklerPlugin.Log($"Buckler: disc tossed ({BucklerPlugin.DiscRange:0.0} u{(enhanced ? ", enhanced" : "")})");
        }

        private float Reach => BucklerPlugin.DiscRadius * Buckler.Size(enhanced);

        private void Update()
        {
            if (owner == null || Time.time - born > MaxLife)
            {
                Destroy(gameObject);
                return;
            }
            if (frames.Length > 0)
                look.sprite = frames[(int)((Time.time - born) * BucklerArt.DiscFps) % frames.Length];   // the spin is in the frames
            else
                transform.Rotate(0f, 0f, SpinSpeed * Time.deltaTime);
            Vector2 from = transform.position;
            Vector2 to;
            if (!returning)
            {
                float step = BucklerPlugin.DiscSpeed * Time.deltaTime;
                to = from + dir * step;
                distance += step;
                returning = distance >= BucklerPlugin.DiscRange;
            }
            else
            {
                Vector2 back = Buckler.Hand(owner) - from;
                float step = BucklerPlugin.DiscReturnSpeed * Time.deltaTime;
                if (back.magnitude <= Mathf.Max(CatchDistance, step))
                {
                    Buckler.Sound("EarthPebble", from, 1.3f);
                    Destroy(gameObject);
                    return;
                }
                to = from + back.normalized * step;
            }
            transform.position = to;
            HashSet<int> hit = returning ? hitBack : hitOut;
            foreach (GameObject enemy in Buckler.EnemiesNear(from, to, Reach))
                if (hit.Add(enemy.GetInstanceID()))
                    Buckler.Burst(owner, skillID, enemy.transform.position, enhanced ? 5 : 2, 0.3f);
            Buckler.BreakShots(owner, skillID, to, Reach, null, enhanced);
        }
    }
}
