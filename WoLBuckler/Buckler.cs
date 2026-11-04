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
        // Log - Art 09:05: the punch in 8 facings, 8 ROWS (same facing order as Held, row 0 at the
        // top) x 4 frames (pull back, SMASH, hold, recover) of 40x40; pivot (20,20) = the shoulder.
        public static Sprite[][] Punch8 = new Sprite[0][];
        // #100 (Log - Art 09:24): the LEFT hand, drawn (not mirrored): held 8 facings + punch 8 x 4
        // from the left shoulder. #99 (09:15): the thrown disc seen as a tilted flat disc, 8 throw
        // directions x 4 spin frames, 16x16 and a native 26x26 for the enhanced buckler.
        public static Sprite[] HeldLeft = new Sprite[0];
        public static Sprite[][] PunchLeft8 = new Sprite[0][];
        public static Sprite[][] Disc8 = new Sprite[0][];
        public static Sprite[][] DiscBig8 = new Sprite[0][];
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
            Punch8 = Sheet("buckler_punch_8dir.png", 40, 8, 4);
            HeldLeft = Strip("buckler_held_left_8dir.png");
            PunchLeft8 = Sheet("buckler_punch_left_8dir.png", 40, 8, 4);
            Disc8 = Sheet("buckler_disc_8dir.png", 16, 8, 4);
            DiscBig8 = Sheet("buckler_disc_big_8dir.png", 26, 8, 4);
            Disc = Strip("buckler_disc.png");
            DiscBig = Strip("buckler_disc_big.png");
            log?.LogInfo($"Buckler art: buckler {(Shield != null ? "loaded" : "missing")}, punch {Punch.Length} frames, " +
                $"held {Held.Length}/{HeldLeft.Length} facings, punch {Punch8.Length}/{PunchLeft8.Length} rows, disc 8-dir {Disc8.Length}/{DiscBig8.Length} rows");
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

        // rows x cols of size x size frames; row 0 is the TOP of the image (Unity's rects count from the bottom).
        private static Sprite[][] Sheet(string file, int size, int rows, int cols)
        {
            Texture2D tex = BucklerPlugin.LoadTexture(file);
            if (tex == null || tex.width < size * cols || tex.height < size * rows)
                return new Sprite[0][];
            var sheet = new Sprite[rows][];
            for (int r = 0; r < rows; r++)
            {
                sheet[r] = new Sprite[cols];
                for (int c = 0; c < cols; c++)
                    sheet[r][c] = Sprite.Create(tex, new Rect(c * size, tex.height - (r + 1) * size, size, size), new Vector2(0.5f, 0.5f), 16f);
            }
            return sheet;
        }

        public static bool HasPunch8 => Punch8.Length == 8 && Punch8[0].Length == 4;
        public static bool HasDualWield => HasPunch8 && PunchLeft8.Length == 8 && PunchLeft8[0].Length == 4 && Held.Length >= 8 && HeldLeft.Length >= 8;
        public static bool HasDisc8 => Disc8.Length == 8 && Disc8[0].Length == 4;

        // Log - Art 09:24: each shoulder's offset from the wizard's chest point (the attack origin),
        // in px, x right / y DOWN, per facing (right, down-right, down, down-left, left, up-left, up,
        // up-right); the left shoulder is the mirror point.
        private static readonly int[,] RightShoulderPx = { { 0, 2 }, { -3, 2 }, { -4, 0 }, { -3, -2 }, { 0, -2 }, { 3, -2 }, { 4, 0 }, { 3, 2 } };

        public static Vector2 Shoulder(Player owner, int facing, bool left)
        {
            float sign = left ? -1f : 1f;
            return Buckler.Hand(owner) + new Vector2(sign * RightShoulderPx[facing, 0], -sign * RightShoulderPx[facing, 1]) / 16f;
        }

        // The held shield: its shoulder plus 6 px along the facing (the vertical part x0.6).
        public static Vector2 HeldSpot(Player owner, int facing, bool left)
        {
            float a = -facing * 45f * Mathf.Deg2Rad;
            return Shoulder(owner, facing, left) + new Vector2(Mathf.Cos(a) * 6f, Mathf.Sin(a) * 6f * 0.6f) / 16f;
        }

        // Draw order (09:24): right: R front, L behind | down-right, down, down-left: both front |
        // left: R behind, L front | up-left, up, up-right: both behind.
        public static bool Behind(int facing, bool left)
        {
            if (facing == 0) return left;
            if (facing == 4) return !left;
            return facing >= 5;
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
        public static void Punch(Player owner, string skillID, Vector2 aim, bool enhanced, bool left = false)
        {
            Vector2 at = Hand(owner) + aim * BucklerPlugin.PunchReach * Size(enhanced);
            Burst(owner, skillID, at, enhanced ? 4 : 1, BucklerPlugin.PunchRadius * Size(enhanced));
            Sound("ImpactEarthAlt", at, enhanced ? 0.9f : 1.2f);
            BucklerGuard.Raise(owner, skillID, aim, enhanced, left);
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

    // The bucklers on the wizard during the combo. 0.7.0 (#100, Matt: dual-wield): hit 1 punches
    // with the RIGHT buckler while the left is held up, hit 2 with the LEFT while the right is
    // held, hit 3 throws the right one (the left stays up until it's caught). Each punch: the
    // pull-back frame during the wind-up, then smash (the hit), hold, recover at ~16 fps from
    // that hand's shoulder, then that buckler held up in front through the pause, breaking enemy
    // shots at its face as before. Without Art's dual-wield sheets: the 0.6.0 single buckler.
    internal class BucklerGuard : MonoBehaviour
    {
        private const float SmashTime = 0.12f, HoldAt = 0.65f, PunchFrameTime = 1f / 16f, PullMaxTime = 0.35f, TossHoldMax = 3f;
        private static readonly Dictionary<int, BucklerGuard> held = new Dictionary<int, BucklerGuard>();
        private enum Mode { Pull, Punch, Toss }
        private Player owner;
        private string skillID;
        private Vector2 aim;
        private bool enhanced, left;
        private float born;
        private int facing;
        private Mode mode;
        private SpriteRenderer main, off;   // the punching (or thrown-from) hand, and the other hand

        private static bool Dual => BucklerArt.HasDualWield && BucklerPlugin.HeldArt;
        private static bool Single => !Dual && BucklerArt.HasPunch8 && BucklerPlugin.HeldArt;

        public static void PutAway(Player owner)
        {
            BucklerGuard old;
            if (owner != null && held.TryGetValue(owner.playerID, out old) && old != null)
                Destroy(old.gameObject);
        }

        // The wind-up of hits 1-2: the pull-back frame (f0) on the punching hand.
        public static void Pull(Player owner, Vector2 aim, bool enhanced, bool left)
        {
            if ((!Dual && !Single) || owner == null)
                return;
            Make(owner, null, aim, enhanced, Dual && left, Mode.Pull).Draw(0f);
        }

        public static void Raise(Player owner, string skillID, Vector2 aim, bool enhanced, bool left)
        {
            BucklerGuard guard = Make(owner, skillID, aim, enhanced, Dual && left, Mode.Punch);
            if (Dual || Single)
            {
                guard.Draw(0f);
                return;
            }
            // No 8-facing art: the 0.4.0 look (the punch strip turned to the aim).
            float scale = Buckler.BaseScale * Buckler.Size(enhanced);
            guard.main.transform.localScale = new Vector3(scale, scale, 1f);
            guard.main.sprite = BucklerArt.PunchFrame(1);
            guard.main.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            guard.main.flipY = aim.x < -0.01f;
            Buckler.Sort(guard.main, owner, aim.y > 0.3f ? -1 : 1);
            guard.main.transform.position = guard.BucklerAt();
        }

        // Hit 3: the right buckler is in the air; the left stays held up until it's caught.
        public static void HoldOffHand(Player owner, Vector2 aim, bool enhanced)
        {
            if (!Dual || owner == null)
                return;
            Make(owner, null, aim, enhanced, false, Mode.Toss).Draw(0f);
        }

        private static BucklerGuard Make(Player owner, string skillID, Vector2 aim, bool enhanced, bool left, Mode mode)
        {
            PutAway(owner);
            var go = new GameObject("Buckler Guard");
            BucklerGuard guard = go.AddComponent<BucklerGuard>();
            held[owner.playerID] = guard;
            guard.owner = owner;
            guard.skillID = skillID;
            guard.aim = aim;
            guard.enhanced = enhanced;
            guard.left = left;
            guard.mode = mode;
            guard.born = Time.time;
            guard.facing = BucklerArt.Facing(aim);
            guard.main = Part(go, "main");
            if (Dual)
                guard.off = Part(go, "off");
            return guard;
        }

        private static SpriteRenderer Part(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<SpriteRenderer>();
        }

        private float Reach => BucklerPlugin.PunchReach * Buckler.Size(enhanced);

        // Where the buckler's guard is for shot-breaking: out to the punch's reach during the
        // smash, then back to where it's held (unchanged from 0.4.0).
        private Vector2 BucklerAt()
        {
            float age = Time.time - born;
            float d = age < SmashTime ? Mathf.Lerp(0.3f, Reach, age / SmashTime)
                : Mathf.Lerp(Reach, Reach * HoldAt, Mathf.Clamp01((age - SmashTime) / 0.1f));
            return Buckler.Hand(owner) + aim * d;
        }

        private void Update()
        {
            float age = Time.time - born;
            float life = mode == Mode.Pull ? PullMaxTime : mode == Mode.Toss ? TossHoldMax : BucklerPlugin.HoldTime;
            if (owner == null || age >= life)
            {
                Destroy(gameObject);
                return;
            }
            if (Dual || Single)
                Draw(age);
            else
                main.sprite = BucklerArt.PunchFrame(age < SmashTime ? 1 : age > BucklerPlugin.HoldTime - 0.1f ? 3 : 2);
            if (mode == Mode.Punch)
            {
                Vector2 at = BucklerAt();
                if (!Dual && !Single)
                    main.transform.position = at;
                Buckler.BreakShots(owner, skillID, at, BucklerPlugin.GuardRadius * Buckler.Size(enhanced), aim, enhanced);
            }
        }

        // Both hands for this moment, upright, native size (x enhanced), in Art's draw order.
        private void Draw(float age)
        {
            float scale = Buckler.Size(enhanced);
            // The punching / throwing hand.
            if (mode == Mode.Toss)
                main.enabled = false;
            else
            {
                int frame = mode == Mode.Pull ? 0 : age < SmashTime ? 1 : age < SmashTime + PunchFrameTime ? 2 : age < SmashTime + 2f * PunchFrameTime ? 3 : -1;
                if (frame >= 0)
                    Put(main, (left ? BucklerArt.PunchLeft8 : BucklerArt.Punch8)[facing][frame],
                        Dual ? BucklerArt.Shoulder(owner, facing, left) : Buckler.Hand(owner), left, scale);
                else
                    Put(main, (left ? BucklerArt.HeldLeft : BucklerArt.Held)[facing],
                        Dual ? BucklerArt.HeldSpot(owner, facing, left) : BucklerAt(), left, scale);
            }
            // The other hand: held up the whole time.
            if (off != null)
            {
                bool offLeft = mode == Mode.Toss || !left;
                Put(off, (offLeft ? BucklerArt.HeldLeft : BucklerArt.Held)[facing], BucklerArt.HeldSpot(owner, facing, offLeft), offLeft, scale);
            }
        }

        private void Put(SpriteRenderer r, Sprite sprite, Vector2 at, bool leftHand, float scale)
        {
            r.enabled = true;
            r.sprite = sprite;
            r.flipY = false;
            r.transform.rotation = Quaternion.identity;
            r.transform.localScale = new Vector3(scale, scale, 1f);
            r.transform.position = at;
            Buckler.Sort(r, owner, (Dual ? BucklerArt.Behind(facing, leftHand) : BucklerArt.HeldBehind(facing)) ? -1 : 1);
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
        private Sprite[][] sheet;
        private Vector2 lastMove;

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
            // #99: the 8-direction flat disc, at native size (16 px; the enhanced one is drawn at
            // 26 px, so scale 1.0, per Art 09:15 and the Modder's review), row by flight direction.
            disc.sheet = enhanced ? BucklerArt.DiscBig8 : BucklerArt.Disc8;
            if (BucklerArt.HasDisc8 && disc.sheet.Length == 8 && BucklerPlugin.HeldArt)
            {
                go.transform.localScale = Vector3.one;
                look.sprite = disc.sheet[BucklerArt.Facing(aim)][0];
                disc.frames = new Sprite[0];
            }
            else
            {
                disc.sheet = null;
                disc.frames = enhanced && BucklerArt.DiscBig.Length > 0 ? BucklerArt.DiscBig : BucklerArt.Disc;
            }
            if (disc.sheet == null && disc.frames.Length > 0 && BucklerPlugin.HeldArt)
            {
                float px = disc.frames[0].rect.width;
                float s = BucklerArt.ArtPixels * Buckler.Size(enhanced) / px;
                go.transform.localScale = new Vector3(s, s, 1f);
                look.sprite = disc.frames[0];
            }
            else
                disc.frames = new Sprite[0];
            Buckler.Sort(look, owner, 1);
            BucklerGuard.HoldOffHand(owner, aim, enhanced);   // #100: the left buckler stays up while the right one flies
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
            int spin = (int)((Time.time - born) * BucklerArt.DiscFps);
            if (sheet != null)
                look.sprite = sheet[BucklerArt.Facing(lastMove != Vector2.zero ? lastMove : dir)][spin % 4];   // row = flight direction
            else if (frames.Length > 0)
                look.sprite = frames[spin % frames.Length];   // the spin is in the frames
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
                    BucklerGuard.PutAway(owner);   // caught: the held left buckler goes with the combo's end
                    Destroy(gameObject);
                    return;
                }
                to = from + back.normalized * step;
            }
            if (to != from)
                lastMove = (to - from).normalized;
            transform.position = to;
            HashSet<int> hit = returning ? hitBack : hitOut;
            foreach (GameObject enemy in Buckler.EnemiesNear(from, to, Reach))
                if (hit.Add(enemy.GetInstanceID()))
                    Buckler.Burst(owner, skillID, enemy.transform.position, enhanced ? 5 : 2, 0.3f);
            Buckler.BreakShots(owner, skillID, to, Reach, null, enhanced);
        }
    }
}
