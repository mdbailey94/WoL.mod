using System.Collections;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Vine Slingshot (Earth): hold dash and twin vines shoot out along your aim. The first enemy
    // they touch is grabbed and takes small, stunning hits while you keep holding (up to the
    // slingshots' max hold); if they touch no enemy they latch onto the wall or the ground at their reach.
    // Let go (or run out of time) and you pull yourself to the target and kick it back hard.
    //
    // Enhanced, vines also spread from the grab to nearby enemies and snare them, lingering for
    // 3 s after the kick (VineSpread).
    //
    // While holding, the wizard stays planted punching the ground and can't turn (the trade-off
    // for a longer reach); the pull trails dust and pebbles, and the kick lands with a hit-stop,
    // a camera shake, a floor crack and a spray of rock, and the target skids away trailing dust.
    public class VineSlingshotState : ChargedDashState
    {
        public new static string staticID = "VineSlingshot";

        // [Vine Slingshot] settings (0.27.0; the defaults are the values these had in code).
        private static float Range => SlingshotDashPlugin.VineRange; // 8, +15% for not being able to turn, then +10%
        private const float TapRange = 4.5f;  // a tap's reach if the normal dash length can't be read
        // The kick also hits a little way behind the target, along the kick.
        private static float BehindDistance => SlingshotDashPlugin.VineBehindDistance;
        private static float BehindScale => SlingshotDashPlugin.VineBehindSize;
        private static float KickScale => SlingshotDashPlugin.VineKickSize;
        private static float GripScale => SlingshotDashPlugin.VineGripSize;
        private const float TrailInterval = 0.035f;
        private const float SkidTime = 0.45f;
        private const float SkidInterval = 0.05f;
        private static float CatchRadius => SlingshotDashPlugin.VineCatchWidth;
        private const float WallMargin = 0.6f;
        private static float TickInterval => SlingshotDashPlugin.VineGripInterval;
        private const float ArriveDistance = 0.9f;
        private static float MinPullSpeed => SlingshotDashPlugin.VineMinPullSpeed;

        private Enemy target;          // grabbed enemy, or null when latched to wall/ground
        private Vector2 anchor;        // where the vines hold
        private Vector2 launchFrom;
        private float nextTick;
        private bool kicked;
        private float pullSpeed;
        private float nextTrail;
        private VineLines vines;        // drawn vines (fallback) and the coils
        private GameVines gameVines;    // the game's own vine, when it works
        private VineSpread spread;      // enhanced: vines spread to nearby enemies

        public VineSlingshotState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
        }

        // You pull yourself in; the dash's own speed boost doesn't apply.
        protected override bool BoostsDash => false;

        // Planted, punching the ground, facing where the vines went.
        protected override bool HopsBack => false;
        protected override bool LocksFacing => true;
        protected override string ChargeAnimation => SlingshotDashPlugin.VineHoldAnimation(parent);
        protected override float ChargePoseFrame => SlingshotDashPlugin.VineHoldPoseFrame;

        protected override void OnChargeStarted() => Grab(Range);

        // A tap: the vines only reach as far as a normal dash goes.
        protected override void OnTap() => Grab(NormalDashLength());

        private float NormalDashLength()
        {
            try
            {
                float speed = parent.movement != null && parent.movement.dashSpeedStat != null
                    ? parent.movement.dashSpeedStat.ModifiedValue : 0f;
                float length = speed * dashDuration;
                if (length > 1f)
                    return Mathf.Min(length, Range);
            }
            catch
            {
            }
            return TapRange;
        }

        private void Grab(float range)
        {
            Vector2 start = parent.transform.position;
            Vector2 direction = GetInputVector(true, true, true);
            if (direction.sqrMagnitude < 0.01f)
                direction = Entity.GetFacingDirectionVector(parent.facingDirection);
            direction.Normalize();

            float reach = range;
            RaycastHit2D wall = Physics2D.Raycast(start, direction, range, ChaosCollisions.layerAllWallAndObst);
            if (wall.collider != null)
                reach = Mathf.Max(0f, wall.distance - WallMargin);

            target = FirstEnemy(start, direction, reach);
            anchor = target != null ? (Vector2)target.transform.position : start + direction * reach;
            nextTick = 0f;
            nextTrail = 0f;
            kicked = false;

            vines = VineLines.Create(parent);
            gameVines = GameVines.Create(parent, parent.skillCategory, skillID);
            if (gameVines != null)
                gameVines.Hold(target != null ? target.transform : null, anchor);
            if (gameVines == null || gameVines.Broken)
                vines?.Shoot(start, anchor);
            if (IsEmpowered)
                spread = VineSpread.Begin(parent, parent.skillCategory, skillID, target, anchor);
            // The fist hits the ground; leaves fly where the vines take hold.
            Effects.Crack(start);
            Effects.Leaves(anchor, 6);
            Effects.Dust(start, 14, 0.5f);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(start), null, 24f, -1f, 0.8f, false);
            GrabLook(start, direction);
        }

        // ---- The look (only for show) ----
        private const string Name = "Vine Slingshot";
        private const float RockGravity = 22f;          // tumbling rocks fall back this fast...
        private const float RockHopMin = 2.5f, RockHopMax = 4.5f; // ...after hopping up this fast
        private const int RockKinds = 16;              // the game's rock debris sheet has 16 rocks
        private const float PullDustInterval = 0.04f;  // dust kicked up along the pull...
        private const float PullRockInterval = 0.09f;  // ...and rocks
        private const float PullDustDrift = 1.5f;
        private const int KickRocks = 6;               // rocks thrown along the kick
        private const float KickRockCone = 40f;        // degrees either side of the kick
        // The game's earth wave spikes burst out of the floor along the kick, one after another.
        private static readonly float[] SpikeAt = { 0.5f, 1.3f, 2.1f };
        private const float SpikeDelay = 0.05f;
        private const float SpikeJitter = 0.3f;
        // Its spike sheet's rows: which lean right (flipped for a kick to the left) and which stand up.
        private static readonly int[] LeaningSpikes = { 0, 3, 4, 7 };
        private static readonly int[] UprightSpikes = { 2, 6 };
        // Dust and the white rings and flash tinted the game's dusty earth browns.
        private static readonly Color DustColour = new Color(0.75f, 0.66f, 0.52f, 0.9f);
        private static readonly Color RingColour = new Color(0.85f, 0.75f, 0.6f, 1f);
        private static readonly Color FlashColour = new Color(0.92f, 0.85f, 0.68f, 1f);
        private float nextPullDust, nextPullRock;

        private static void Rock(Vector2 at, Vector2 throwVelocity, float hop) =>
            Effects.Visual("rock debris", () =>
            {
                FxAnim rock = EarthArt.Rock.Get();
                float airTime = hop * 2f / RockGravity + 0.25f;
                FxSprite.Play(rock, at, new FxOpts
                {
                    velocity = throwVelocity,
                    drag = 1.2f,
                    hop = hop,
                    gravity = RockGravity,
                    spin = Random.Range(-240f, 240f),
                    loopFor = airTime,
                    // One of the game's 16 rocks, held while it tumbles; the drawn rock animates.
                    still = rock != null && rock.FromGame ? Random.Range(1, RockKinds + 1) : 0,
                    fadeFrom = 0.8f,
                    shrinkTo = 0.6f,
                    speed = Random.Range(0.8f, 1.3f)
                });
            });

        private static void DustPuff(Vector2 at, Vector2 drift, float scale) =>
            Effects.Visual("dust puffs", () => FxSprite.Play(EarthArt.Dust.Get(), at, new FxOpts
            {
                velocity = drift,
                drag = 2f,
                scale = scale,
                variant = Random.Range(0, 4),
                flipX = Random.value < 0.5f,
                colour = DustColour,
                sortOffset = -0.2f
            }));

        private static void DustRing(Vector2 at, float scale) =>
            Effects.Visual("dust ring", () =>
            {
                FxAnim ring = EarthArt.Ring.Get();
                // The game's meteor ring is 93 to 145 px across; the drawn one 64.
                float s = (ring != null && ring.FromGame ? 0.55f : 1f) * scale;
                FxSprite.Play(ring, at, new FxOpts { layer = FxLayer.Ground, scale = s, colour = RingColour });
            });

        // Flair only: a dust ring spreading from the fist with rocks and dust thrown up, and where
        // the vines take hold, the game's thorns on an enemy (rocks and dust on a wall or floor).
        private void GrabLook(Vector2 start, Vector2 direction)
        {
            PixelFx.Setup(parent);
            DustRing(start, 0.6f);
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f + Random.Range(-30f, 30f)) * Mathf.Deg2Rad;
                var outward = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f);
                Rock(start + outward * 0.3f, outward * Random.Range(1f, 2f), Random.Range(RockHopMin, RockHopMax));
                DustPuff(start + outward * 0.4f, outward * PullDustDrift, 0.9f);
            }
            if (target != null)
                Effects.Game("ThornEmitter", 10, anchor);
            else
            {
                Rock(anchor, -direction * 1.5f, RockHopMin);
                DustPuff(anchor, Vector2.zero, 1f);
            }
            PixelFx.Once("earth grab", $"{Name}: look 'grab' played (game meteor dust ring, rock debris and smoke puffs, " +
                "game ThornEmitter on a grabbed enemy)");
        }

        // Flair only: dust puffs and rocks kicked up behind you as you're pulled in.
        private void PullLook(Vector2 position, Vector2 direction)
        {
            if (Time.time >= nextPullDust)
            {
                nextPullDust = Time.time + PullDustInterval;
                DustPuff(position + Random.insideUnitCircle * 0.2f, -direction * PullDustDrift, Random.Range(0.8f, 1.15f));
            }
            if (Time.time >= nextPullRock)
            {
                nextPullRock = Time.time + PullRockInterval;
                Rock(position, -direction * Random.Range(1.5f, 3f) + Random.insideUnitCircle, Random.Range(RockHopMin * 0.6f, RockHopMin));
            }
        }

        // Flair only: the kick: the game's earth spikes bursting out of the floor one after another
        // along it, its dust ring and a sandy star flash, rocks thrown on ahead, and its floor crater.
        private void KickLook(Vector2 impact, Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Effects.Visual("earth spikes", () =>
            {
                FxAnim spike = EarthArt.Spike.Get();
                if (spike == null)
                    return;
                if (!spike.FromGame)
                {
                    // The drawn stand-in is one burst of shards pointing along the kick.
                    FxSprite.Play(spike, impact, new FxOpts { rotation = angle, lift = 0.3f, layer = FxLayer.Effect });
                    return;
                }
                bool upright = Mathf.Abs(direction.x) < 0.4f;
                Vector2 side = new Vector2(-direction.y, direction.x);
                for (int i = 0; i < SpikeAt.Length; i++)
                {
                    int[] rows = upright ? UprightSpikes : LeaningSpikes;
                    FxSprite.Play(spike, impact + direction * SpikeAt[i] + side * Random.Range(-SpikeJitter, SpikeJitter), new FxOpts
                    {
                        variant = rows[Random.Range(0, rows.Length)],
                        flipX = !upright && direction.x < 0f,
                        delay = i * SpikeDelay
                    });
                }
            });
            DustRing(impact, 1.15f);
            Effects.Visual("earth kick flash", () =>
            {
                FxAnim flash = EarthArt.Flash.Get();
                FxSprite.Play(flash, impact, new FxOpts
                {
                    rotation = flash != null && flash.FromGame ? 0f : angle,
                    lift = 0.6f,
                    layer = FxLayer.Effect,
                    scale = flash != null && flash.FromGame ? 0.4f : 1f,
                    colour = FlashColour
                });
            });
            for (int i = 0; i < KickRocks; i++)
            {
                float a = angle + Random.Range(-KickRockCone, KickRockCone);
                var fling = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                Rock(impact, fling * Random.Range(4f, 7f), Random.Range(RockHopMin, RockHopMax + 1f));
            }
            Effects.Game("FloorCraterMed", 1, impact);
            PixelFx.Once("earth kick", $"{Name}: look 'kick' played (game earth spikes, meteor dust ring, star flash and rock debris, game FloorCraterMed)");
        }

        protected override void WhileCharging(float holdTime)
        {
            FollowTarget();
            DrawVines();
            // Hold the grabbed enemy: small hits that keep it stunned.
            if (target != null && holdTime - MinChargeTime >= nextTick)
            {
                nextTick += TickInterval;
                EarthBurst.CreateBurst(anchor, parent.skillCategory, skillID, 2, GripScale);
            }
        }

        // Charging: pebbles and leaves kicked up by the fist in the ground.
        protected override void ChargeEffect(Vector2 position, float charge)
        {
            Effects.Pebbles(position, 1 + Mathf.RoundToInt(3 * charge));
            if (Random.value < 0.3f + 0.4f * charge)
                Effects.Leaves(position, 1);
            // Flair only: now and then a stone hops up out of the cracked floor.
            if (Random.value < 0.35f + 0.35f * charge)
            {
                Vector2 jiggle = Random.insideUnitCircle * 0.5f;
                Rock(position + jiggle, jiggle * 1.5f, Random.Range(RockHopMin * 0.7f, RockHopMax));
            }
        }

        protected override void OnLaunch(float charge)
        {
            launchFrom = parent.transform.position;
            FollowTarget();
            float distance = Vector2.Distance(launchFrom, anchor);
            float duration = dashDuration > 0.05f ? dashDuration : 0.25f;
            pullSpeed = Mathf.Max(MinPullSpeed, distance / duration);
            parent.FaceTarget(anchor);
            parent.anim?.PlayDirectional(parent.KickAnimStr, -1, 0f);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(launchFrom), null, 24f, -1f, 1.1f, false);
            // Flair only: you tear free of the floor in a burst of dust.
            nextPullDust = 0f;
            nextPullRock = 0f;
            Vector2 away = (launchFrom - anchor).sqrMagnitude > 0.01f ? (launchFrom - anchor).normalized : Vector2.down;
            for (int i = 0; i < 3; i++)
                DustPuff(launchFrom + Random.insideUnitCircle * 0.3f, (away + Random.insideUnitCircle * 0.6f) * PullDustDrift, 1.1f);
        }

        protected override void WhileDashing(float charge)
        {
            if (kicked)
                return;
            FollowTarget();
            DrawVines();
            // A trail of dust and pebbles behind you as you're pulled in.
            if (Time.time >= nextTrail)
            {
                nextTrail = Time.time + TrailInterval;
                Vector2 position = parent.transform.position;
                Effects.Dust(position, 4, 0.3f);
                Effects.Pebbles(position, 2);
            }
            PullLook(parent.transform.position, (anchor - launchFrom).sqrMagnitude > 0.01f ? (anchor - launchFrom).normalized : Vector2.right);
        }

        // Pull yourself along the vines, and kick on arrival.
        protected override void DashFixedUpdate(float charge)
        {
            if (kicked || parent.rigidbody2D == null)
                return;
            Vector2 toTarget = anchor - (Vector2)parent.transform.position;
            if (toTarget.magnitude <= ArriveDistance)
            {
                parent.rigidbody2D.velocity = Vector2.zero;
                Kick();
                return;
            }
            parent.rigidbody2D.velocity = toTarget.normalized * pullSpeed;
        }

        // If the dash ran out before you got there (something in the way), kick where you are.
        protected override void OnLand(float charge)
        {
            if (!kicked)
                Kick();
        }

        protected override void OnStateExit()
        {
            spread?.Linger();
            spread = null;
            RemoveVines();
            target = null;
        }

        private void Kick()
        {
            kicked = true;
            RemoveVines();
            spread?.Linger();
            spread = null;
            Enemy kickedEnemy = target;
            Vector2 position = parent.transform.position;
            Vector2 direction = anchor - launchFrom;
            if (direction.sqrMagnitude < 0.01f)
                direction = Entity.GetFacingDirectionVector(parent.facingDirection);
            direction.Normalize();
            parent.anim?.PlayDirectional(parent.KickAnimStr, -1, 0.5f);
            Vector2 impact = position + direction * 0.6f;
            EarthBurst burst = EarthBurst.CreateBurst(impact, parent.skillCategory, skillID, 1, KickScale);
            // Knock the target on, away from you.
            if (burst != null && burst.attack != null)
                burst.attack.knockbackOverwriteVector = direction;
            // And whoever's just behind it (same attack, so nobody is hit twice).
            Vector2 behind = position + direction * BehindDistance;
            EarthBurst behindBurst = EarthBurst.CreateBurst(behind, parent.skillCategory, skillID, 1, BehindScale);
            if (behindBurst != null && behindBurst.attack != null)
                behindBurst.attack.knockbackOverwriteVector = direction;
            Effects.Dust(behind, 20, 0.8f);

            // The impact: a short hit-stop, a shake, a crack and a spray of rock along the kick.
            Effects.HitStop(4);
            Effects.Shake(1.6f);
            Effects.Crack(impact);
            Effects.RockSpray(impact, direction, 14);
            Effects.Leaves(impact, 8);
            Effects.Dust(position, 50, 1.2f);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f, 0.7f, false);
            KickLook(impact, direction);
            if (kickedEnemy != null)
                SlingshotDashPlugin.Run(Skid(kickedEnemy.transform, direction));
            target = null;
        }

        // The kicked enemy skids away trailing dust and pebbles, with a crack where it stops.
        private static IEnumerator Skid(Transform enemy, Vector2 direction)
        {
            float end = Time.time + SkidTime;
            Vector2 last = enemy != null ? (Vector2)enemy.position : Vector2.zero;
            while (Time.time < end && enemy != null && enemy.gameObject.activeInHierarchy)
            {
                last = enemy.position;
                Effects.Dust(last, 6, 0.35f);
                Effects.Pebbles(last, 2);
                // Flair only: a drawn dust puff kicked up behind the skid.
                DustPuff(last, -direction * 1f, 0.9f);
                yield return new WaitForSeconds(SkidInterval);
            }
            Effects.Crack(last + direction * 0.3f);
            Effects.Dust(last, 16, 0.6f);
            DustPuff(last, Vector2.zero, 1.2f);
        }

        private void FollowTarget()
        {
            if (target == null)
                return;
            if (!target.gameObject.activeInHierarchy)
            {
                // Gone (e.g. killed): the vines stay where it was.
                target = null;
                return;
            }
            anchor = target.transform.position;
        }

        // The game's vine if it's working, otherwise our drawn ones; coils on a grabbed enemy.
        private void DrawVines()
        {
            if (gameVines != null && !gameVines.Broken)
                gameVines.Hold(target != null ? target.transform : null, anchor);
            if (gameVines == null || gameVines.Broken)
                vines?.Hold(parent.transform.position, anchor);
            if (target != null)
                vines?.Ensnare(anchor);
        }

        private void RemoveVines()
        {
            gameVines?.Remove();
            gameVines = null;
            if (vines != null)
            {
                Object.Destroy(vines.gameObject);
                vines = null;
            }
        }

        // The nearest enemy along the vines' path, if any.
        private static Enemy FirstEnemy(Vector2 start, Vector2 direction, float reach)
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(start, CatchRadius, direction, reach);
            Enemy best = null;
            float bestDistance = float.MaxValue;
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null)
                    continue;
                Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
                if (enemy == null || !enemy.gameObject.activeInHierarchy)
                    continue;
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = enemy;
                }
            }
            return best;
        }
    }
}
