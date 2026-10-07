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
    public class CycloneState : Player.SkillState
    {
        public new static string staticID = "Cyclone";

        private const float GrowTime = 4f;         // from small twister to full hurricane
        private const float EnhancedGrowTime = 3f;
        private const float FullPowerTime = 1f;    // then this long at full power, and it bursts
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

        public CycloneState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            applyStopElementStatus = true;
            SetAnimTimes(
                0.05f, // start
                0.1f,  // hold
                0.05f, // execute
                0.4f,  // cancel
                0.5f,  // run
                0.6f); // exit
        }

        private float GrowTimeNow => IsEmpowered ? EnhancedGrowTime : GrowTime;

        public override void OnEnter()
        {
            base.OnEnter();
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
            look = new CycloneLook(center);
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
                Steer(input.normalized * SteerSpeedAt(progress) * Time.deltaTime);
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
            parent.FaceTarget(center);
            parent.anim?.PlayDirectional(parent.ChargeAnimStr, -1, 0.5f);

            look?.Update(center, progress);
            if (Time.time >= nextHit)
            {
                nextHit = Time.time + Mathf.Lerp(SlowHit, FastHit, progress);
                Hit(progress);
            }
            if (Time.time >= nextSwirl)
            {
                nextSwirl = Time.time + SwirlInterval;
                Swirl(progress);
            }

            if (held >= GrowTimeNow + FullPowerTime)
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
            base.OnExit();
        }

        private void Finish()
        {
            done = true;
            RemoveLook();
            base.ExecuteSkill();
        }

        // Quick while small, heavier and heavier, and barely moving from halfway on.
        private static float SteerSpeedAt(float progress)
        {
            float t = Mathf.Clamp01(progress / HeavyAt);
            return Mathf.Lerp(SteerSpeed, HeavySteerSpeed, 1f - (1f - t) * (1f - t));
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
            float scale = Mathf.Lerp(StartHitScale, EndHitScale, progress);
            WindBurst burst = WindBurst.CreateBurst(center, parent.skillCategory, skillID, level, scale);
            if (burst != null)
                burst.emitParticles = false;
        }

        // The game's air vortex swirling at the twister, growing with it.
        private void Swirl(float progress)
        {
            float scale = Mathf.Lerp(StartHitScale, EndHitScale, progress);
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
            float scale = EndHitScale * (IsEmpowered ? EnhancedBurstScale : BurstScale);
            WindBurst.CreateBurst(center, parent.skillCategory, skillID, 5, scale);
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
