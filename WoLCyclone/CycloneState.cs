using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLCyclone
{
    // Cyclone (Air, standard arcana): hold the button and a tiny twister spins up about three tiles
    // in front of the wizard; steer it slowly with your aim (not through walls, up to 9 away). Over 4 seconds (3 enhanced) it grows into a
    // hurricane: bigger, hitting faster and harder (skill levels 1-4 as it grows). Let go and it dies
    // down; hold on a second more at full power and it bursts, throwing everyone around it away
    // (level 5).
    //
    // The hits are invisible wind bursts at the twister; the look is CycloneLook.
    //
    // 0.5.0, three tiers (Matt):
    // - standard: as before, but no leaves in the wind (dust and rocks only);
    // - enhanced (IsEmpowered): leaves whirl in it, it steers faster and stays steerable longer,
    //   and it charges quicker (full size in 3 s, bursting 0.6 s later);
    // - signature (isUltimate, the game's flag for a signature cast; the signature slot is always
    //   empowered, so it has everything enhanced has): a much wider twister that throws enemies
    //   (not bosses) caught in it up into the air; each takes damage when it lands (Toss).
    public class CycloneState : Player.SkillState
    {
        public new static string staticID = "Cyclone";

        private const float GrowTime = 4f;         // from small twister to full hurricane
        private const float EnhancedGrowTime = 3f;
        private const float FullPowerTime = 1f;    // then this long at full power, and it bursts
        private const float EnhancedFullPowerTime = 0.6f;
        private const float EnhancedSteerSpeed = 5.5f;      // enhanced: quicker to steer...
        private const float EnhancedHeavySteerSpeed = 0.8f; // ...still moving when grown...
        private const float EnhancedHeavyAt = 0.75f;        // ...and heavy only later on
        private const float TossFrom = 0.15f;      // the signature starts throwing this far into the growth
        private const float TossReach = 0.6f;      // throws enemies within this times the hit scale
        // Seconds up and down: 0.9 s for 2.5 units, longer for higher throws (as a fall would).
        private static float TossTime => 0.9f * Mathf.Sqrt(CyclonePlugin.TossHeight / 2.5f);
        private const float TossAgain = 0.5f;      // an enemy can be thrown again this long after landing
        // The signature's cinematic (0.5.2; the camera and light are Cinematic's, always put back).
        private const float SigZoom = 0.25f;       // the camera pulls back this much as it grows
        private const float SigDim = 0.5f;         // the room darkens to this
        private const float BoltFrom = 0.35f;      // lightning flickers in the storm from here on
        private const float BoltFlash = 1.4f;      // the room light jumps to this for a flicker
        private const float TossSlowMoTime = 0.35f, TossSlowMoScale = 0.2f;   // the first throw's peak
        private const int BurstHitStopFrames = 5;
        private const float BurstFlash = 1.8f, BurstShake = 2.4f, BurstSlowMoTime = 0.45f, BurstSlowMoScale = 0.04f;
        private const float BurstEaseBack = 0.9f, LetGoEaseBack = 0.5f;
        private const float MinTime = 0.35f;       // a tap still gives a brief twister
        private const float Distance = 4f;         // starts about three tiles in front of the wizard
        private const float SteerSpeed = 3.5f;     // how fast you can move it when it's small...
        private const float HeavyAt = 0.5f;        // ...slowing until, this far into the growth,
        private const float HeavySteerSpeed = 0.15f; // it barely moves
        private const float MaxRange = 9f;         // how far from the wizard it can be steered
        private const float StartHitScale = 0.9f;  // hit area, small to big
        private const float EndHitScale = 2.4f;
        private const float SlowHit = 0.45f;       // seconds between hits, slow to fast
        private const float FastHit = 0.12f;
        private const float BurstScale = 1.6f;     // the final burst, times the full hit area
        private const float EnhancedBurstScale = 2f;
        private const float SwirlInterval = 0.15f;

        private float held;
        private float nextHit;
        private float nextSwirl;
        private bool done;
        private Vector2 aim;
        private Vector2 center;
        private CycloneLook look;
        private bool signature;
        private int tossed;
        private int cinematic;
        private float nextBolt, firstTossAt;
        private bool tossSlowMoDone;
        private readonly System.Collections.Generic.Dictionary<Entity, float> landedAt = new System.Collections.Generic.Dictionary<Entity, float>();

        public CycloneState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            applyStopElementStatus = true;
            // Lets the arcana into the signature slot (the spellbook's Signature page); see
            // SignatureUnlock.cs for when that's unlocked.
            hasSignatureVariant = true;
            SetAnimTimes(
                0.05f, // start
                0.1f,  // hold
                0.05f, // execute
                0.4f,  // cancel
                0.5f,  // run
                0.6f); // exit
        }

        private bool EnhancedTier => IsEmpowered || signature;
        private float GrowTimeNow => EnhancedTier ? EnhancedGrowTime : GrowTime;
        private float FullPowerNow => EnhancedTier ? EnhancedFullPowerTime : FullPowerTime;
        private float Wide => signature ? CyclonePlugin.SignatureWidth : 1f;
        private float HitScaleAt(float progress) => Mathf.Lerp(StartHitScale, EndHitScale, progress) * Wide;

        public override void OnEnter()
        {
            base.OnEnter();
            signature = isUltimate;
            tossed = 0;
            landedAt.Clear();
            firstTossAt = 0f;
            tossSlowMoDone = false;
            nextBolt = 0f;
            cinematic = signature ? Cinematic.Begin() : 0;
            held = 0f;
            nextHit = 0f;
            nextSwirl = 0f;
            nextRumble = 0f;
            done = false;
            aim = GetInputVector();
            if (aim.sqrMagnitude < 0.01f)
                aim = Entity.GetFacingDirectionVector(parent.facingDirection);
            aim.Normalize();
            float distance = Distance;
            RaycastHit2D wall = Physics2D.Raycast(parent.transform.position, aim, Distance, ChaosCollisions.layerAllWallAndObst);
            if (wall.collider != null)
                distance = Mathf.Max(0.8f, wall.distance - 0.4f);
            center = (Vector2)parent.transform.position + aim * distance;
            look?.Remove();
            look = new CycloneLook(center, parent, EnhancedTier, Wide, signature ? Mathf.Sqrt(Wide) : 1f);
            CyclonePlugin.Log($"Cyclone: cast ({(signature ? IsEmpowered ? "signature + enhanced" : "signature" : IsEmpowered ? "enhanced" : "standard")}" +
                $"{(signature ? $", {Wide:0.#}x wide, throwing enemies" : "")})");
        }

        public override void ExecuteSkill()
        {
            if (done)
            {
                base.ExecuteSkill();
                return;
            }
            if (CancelToDash(false))
                return;

            held += Time.deltaTime;
            float progress = Mathf.Clamp01(held / GrowTimeNow);

            // Steer it slowly with your aim (slower as it grows); the wizard stays put, facing it,
            // in a casting pose.
            // Only while you're actually pushing a direction (not the last aim, which would drift it).
            Vector2 input = GetInputVector(false, false, false);
            if (input.sqrMagnitude > 0.04f)
                Steer(input.normalized * SteerSpeedAt(progress, EnhancedTier) * Time.deltaTime);
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
            parent.FaceTarget(center);
            parent.anim?.PlayDirectional(parent.ChargeAnimStr, -1, 0.5f);

            look?.Update(center, progress);
            if (signature)
                SignatureBeats(progress);
            if (Time.time >= nextHit)
            {
                nextHit = Time.time + Mathf.Lerp(SlowHit, FastHit, progress);
                Hit(progress);
                if (signature && progress >= TossFrom)
                    TossCaught(progress);
            }
            if (Time.time >= nextSwirl)
            {
                nextSwirl = Time.time + SwirlInterval;
                Swirl(progress);
            }

            if (held >= GrowTimeNow + FullPowerNow)
            {
                Burst();
                Finish();
            }
            else if (held >= MinTime && !ButtonHeld())
            {
                DieDown();
                Finish();
            }
        }

        public override void OnExit()
        {
            RemoveLook();
            // Cut short (a dash, a hit, a level load): the camera and light still ease back.
            if (signature)
                Cinematic.EaseOut(cinematic, LetGoEaseBack, "cast cut short");
            base.OnExit();
        }

        // While the signature twister grows: the camera pulls back and the room darkens with it,
        // lightning flickers in the storm, and the first enemy thrown hangs at the top of its arc
        // in a moment of slow motion.
        private void SignatureBeats(float progress)
        {
            float eased = progress * progress * (3f - 2f * progress);
            Cinematic.Set(cinematic, SigZoom * eased, Mathf.Lerp(1f, SigDim, eased));
            if (progress >= BoltFrom && Time.unscaledTime >= nextBolt)
            {
                nextBolt = Time.unscaledTime + Random.Range(0.45f, 1.1f) * (1.2f - 0.5f * progress);
                Cinematic.Flash(cinematic, BoltFlash, 0.06f);
                Try(() => CameraController.ShakeCamera(0.25f + 0.35f * progress, false));
            }
            if (!tossSlowMoDone && firstTossAt > 0f && Time.time >= firstTossAt + TossTime * 0.5f)
            {
                tossSlowMoDone = true;
                Try(() => TimeScaleController.SetTimeScaleDelayOverRealTime(TossSlowMoTime, TossSlowMoScale, true, true));
                CyclonePlugin.Log($"Cyclone: signature beat: the first throw's peak, slow motion {TossSlowMoTime:0.00} s from x{TossSlowMoScale:0.00}");
            }
        }

        private void Finish()
        {
            done = true;
            RemoveLook();
            base.ExecuteSkill();
        }

        // Quick while small, heavier and heavier, and barely moving from halfway on (enhanced:
        // quicker, heavy only from three quarters on, and still moving when grown).
        private static float SteerSpeedAt(float progress, bool enhanced)
        {
            float t = Mathf.Clamp01(progress / (enhanced ? EnhancedHeavyAt : HeavyAt));
            return Mathf.Lerp(enhanced ? EnhancedSteerSpeed : SteerSpeed, enhanced ? EnhancedHeavySteerSpeed : HeavySteerSpeed,
                1f - (1f - t) * (1f - t));
        }

        // The signature throws every enemy in its reach up into the air (not bosses, not one
        // already up or just landed); each lands for TossDamage.
        private void TossCaught(float progress)
        {
            try
            {
                if (GameController.enemies == null)
                    return;
                float reach = HitScaleAt(progress) * TossReach;
                foreach (GameObject go in new System.Collections.Generic.List<GameObject>(GameController.enemies))
                {
                    if (go == null || !go.activeInHierarchy || Vector2.Distance(go.transform.position, center) > reach)
                        continue;
                    Entity e = go.GetComponent<Entity>();
                    if (e == null || e is Boss || e is MiniBoss || e.health == null || e.health.CurrentHealthValue <= 0 || Toss.Airborne(e))
                        continue;
                    float landed;
                    if (landedAt.TryGetValue(e, out landed) && Time.time < landed)
                        continue;
                    landedAt[e] = Time.time + TossTime + TossAgain;
                    Toss.Launch(e, parent, CyclonePlugin.TossHeight, TossTime, CyclonePlugin.TossDamage, skillID);
                    tossed++;
                    if (firstTossAt <= 0f)
                        firstTossAt = Time.time;
                }
            }
            catch (System.Exception ex)
            {
                CyclonePlugin.Log($"Cyclone: couldn't throw enemies: {ex.Message}");
            }
        }

        // Moves the twister, but not through walls or too far from the wizard.
        private void Steer(Vector2 step)
        {
            Vector2 next = center + step;
            if (Vector2.Distance(next, parent.transform.position) > MaxRange)
                return;
            RaycastHit2D wall = Physics2D.Raycast(center, step.normalized, step.magnitude + 0.3f, ChaosCollisions.layerAllWallAndObst);
            if (wall.collider != null)
                return;
            center = next;
        }

        private bool ButtonHeld()
        {
            try
            {
                return parent.inputDevice != null && parent.inputDevice.GetButton("Skill" + skillSlot);
            }
            catch
            {
                return false;
            }
        }

        // Level 1-4 by how far it's grown: each stage hits harder.
        private void Hit(float progress)
        {
            int level = 1 + Mathf.Min(3, Mathf.FloorToInt(progress * 4f));
            float scale = HitScaleAt(progress);
            WindBurst burst = MakeBurst(level, scale);
            if (burst != null)
                burst.emitParticles = false;
        }

        // A signature cast's hits are marked as the game marks its own (isUltimate), so they don't
        // refill the signature gauge: up to 0.5.2 its many hits filled it again almost at once.
        private WindBurst MakeBurst(int level, float scale) => signature
            ? WindBurst.CreateBurst(center, AttackInfo.GetInfo(parent, parent.skillCategory, skillID, level, true), scale)
            : WindBurst.CreateBurst(center, parent.skillCategory, skillID, level, scale);

        // The game's air vortex swirling at the twister, growing with it.
        private void Swirl(float progress)
        {
            float scale = HitScaleAt(progress);
            Try(() =>
            {
                // Layered swirls: more of them, wider and slower, as it builds.
                int layers = 1 + Mathf.FloorToInt(progress * 2.5f);
                for (int i = 0; i < layers; i++)
                {
                    var vortex = new ParticleSystemOverride
                    {
                        startSize = new float?(2.0f * scale * (1f + 0.45f * i)),
                        startLifetime = new float?(0.45f + 0.2f * i)
                    };
                    PoolManager.GetPoolItem<ParticleEffect>("AirVortex").Emit(new int?(1), new Vector3?(center), vortex,
                        new Vector3?(new Vector3(0f, 0f, Random.Range(0f, 360f))), 0f, null, null);
                }
                PoolManager.GetPoolItem<DustEmitter>().EmitCircle(4 + Mathf.RoundToInt(12 * progress), 0.4f + scale * 0.5f,
                    -4f, -1f, new Vector3?(center), null);
            });
            if (progress < 0.3f)
                return;
            // The hurricane's pull: dust streaming in to it, debris and leaves whirling round it.
            Try(() => PoolManager.GetPoolItem<DustSuctionEffect>().Emit(new int?(6 + Mathf.RoundToInt(14 * progress)),
                new Vector3?(center), null, null, 0f, null, null));
            Try(() =>
            {
                for (int i = 0; i < 1 + Mathf.RoundToInt(2 * progress); i++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    var ring = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * scale * 1.1f;
                    PoolManager.GetPoolItem<RockDebrisEmitter>().EmitSingle(new int?(1), new Vector3?(ring), null, null, 0f, null);
                    // Leaves only from the enhanced tier up (0.5.0).
                    if (EnhancedTier)
                        PoolManager.GetPoolItem<LeafEmitter>().EmitSingle(new int?(1), new Vector3?(ring), null, null, 0f, null,
                            LeafEmitter.LeafColor.Green);
                }
            });
            // A rumble that grows into a shake at full power.
            if (progress >= 0.6f && Time.time >= nextRumble)
            {
                nextRumble = Time.time + 0.5f;
                Try(() => CameraController.ShakeCamera(0.15f + 0.45f * progress, false));
            }
            // Gusts bursting out of it near full power.
            if (progress >= 0.8f && Random.value < 0.3f)
                Try(() => PoolManager.GetPoolItem<ParticleEffect>("WindBurstEffect").Emit(new int?(1), new Vector3?(center),
                    null, null, 0f, null, null));
        }

        private float nextRumble;

        // Effects are flair: if one isn't available, skip it (and stop trying it).
        private readonly System.Collections.Generic.HashSet<int> brokenEffects = new System.Collections.Generic.HashSet<int>();

        private void Try(System.Action effect)
        {
            int key = effect.Method.GetHashCode();
            if (brokenEffects.Contains(key))
                return;
            try
            {
                effect();
            }
            catch (System.Exception e)
            {
                brokenEffects.Add(key);
                CyclonePlugin.Log($"Cyclone: an effect is unavailable, skipping it: {e.Message}");
            }
        }

        // Held to the end: the hurricane bursts and throws everyone away.
        private void Burst()
        {
            float scale = EndHitScale * Wide * (EnhancedTier ? EnhancedBurstScale : BurstScale);
            if (signature)
            {
                // The burst: a hit-stop, the room flashing white, a heavy shake and time all but
                // stopping, then the camera and light easing back.
                Try(() => TimeScaleController.FreezeForFrames(BurstHitStopFrames, false));
                Cinematic.Flash(cinematic, BurstFlash, 0.1f);
                Try(() => CameraController.ShakeCamera(BurstShake, true));
                Try(() => TimeScaleController.SetTimeScaleDelayOverRealTime(BurstSlowMoTime, BurstSlowMoScale, true, true));
                Cinematic.EaseOut(cinematic, BurstEaseBack, "burst finished");
                CyclonePlugin.Log($"Cyclone: signature burst after throwing {tossed} enem{(tossed == 1 ? "y" : "ies")}: hit-stop {BurstHitStopFrames} frames, " +
                    $"flash x{BurstFlash:0.0}, shake {BurstShake:0.0}, slow motion {BurstSlowMoTime:0.00} s, easing back over {BurstEaseBack:0.0} s");
            }
            MakeBurst(5, scale);
            // The drawn funnel blows apart in a gust ring the size of the burst (its own try/catch).
            look?.Burst(scale);
            try
            {
                PoolManager.GetPoolItem<ParticleEffect>("WindBurstEffect").Emit(new int?(6), new Vector3?(center), null, null, 0f, null, null);
                // The hurricane's last huge swirl as it blows apart.
                for (int i = 0; i < 3; i++)
                {
                    var vortex = new ParticleSystemOverride
                    {
                        startSize = new float?(2.2f * scale * (1f + 0.3f * i)),
                        startLifetime = new float?(0.5f + 0.15f * i)
                    };
                    PoolManager.GetPoolItem<ParticleEffect>("AirVortex").Emit(new int?(1), new Vector3?(center), vortex,
                        new Vector3?(new Vector3(0f, 0f, Random.Range(0f, 360f))), 0.06f * i, null, null);
                }
                PoolManager.GetPoolItem<DustEmitter>().EmitCircle(120, scale, -8f, -1f, new Vector3?(center), null);
                CameraController.ShakeCamera(1.8f, false);
            }
            catch
            {
            }
            parent.anim?.PlayDirectional(parent.ForehandAnimStr, -1, 0f);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(center), null, 24f, -1f, 0.7f, false);
        }

        // Let go early: it just blows itself out.
        private void DieDown()
        {
            if (signature)
            {
                Cinematic.EaseOut(cinematic, LetGoEaseBack, "let go");
                CyclonePlugin.Log($"Cyclone: signature let go after throwing {tossed} enem{(tossed == 1 ? "y" : "ies")}");
            }
            look?.DieDown();
            try
            {
                PoolManager.GetPoolItem<DustEmitter>().EmitCircle(30, 1f, -5f, -1f, new Vector3?(center), null);
            }
            catch
            {
            }
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(center), null, 24f, -1f, 1.3f, false);
        }

        private void RemoveLook()
        {
            look?.Remove();
            look = null;
        }
    }
}
