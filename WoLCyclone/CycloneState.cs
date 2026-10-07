using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLCyclone
{
    // Cyclone (Air, standard arcana): hold the button and a small tornado spins up in front of the
    // wizard, following your aim. The longer you hold, the bigger it gets, the faster it hits and the
    // harder (skill levels 1-4 as it grows). Let go and it dies down; hold the full 5 seconds (3.5
    // enhanced) and it bursts, throwing everyone around it away (level 5).
    //
    // The hits are invisible wind bursts at the tornado; the look is the game's own tornado (the one
    // the Whirlwind ultimate summons, its attack set harmless at level 6) plus the game's air
    // vortex swirls, which also stand in if the tornado can't be made.
    public class CycloneState : Player.SkillState
    {
        public new static string staticID = "Cyclone";

        private const float FullTime = 5f;
        private const float EnhancedFullTime = 3.5f;
        private const float MinTime = 0.35f;       // a tap still gives a brief tornado
        private const float Distance = 1.7f;       // tornado in front of the wizard
        private const float StartHitScale = 0.9f;  // hit area, small to big
        private const float EndHitScale = 2.4f;
        private const float SlowHit = 0.45f;       // seconds between hits, slow to fast
        private const float FastHit = 0.12f;
        private const float StartLook = 0.45f;     // the game tornado's size, small to big
        private const float EndLook = 1.1f;
        private const float BurstScale = 1.6f;     // the final burst, times the full hit area
        private const float EnhancedBurstScale = 2f;
        private const float SwirlInterval = 0.15f;
        private const int HarmlessLevel = 6;

        private float held;
        private float nextHit;
        private float nextSwirl;
        private bool done;
        private Vector2 aim;
        private Vector2 center;
        private Tornado tornado;
        private static bool loggedTornado;

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

        private float FullTimeNow => IsEmpowered ? EnhancedFullTime : FullTime;

        public override void OnEnter()
        {
            base.OnEnter();
            held = 0f;
            nextHit = 0f;
            nextSwirl = 0f;
            done = false;
            aim = GetInputVector();
            if (aim.sqrMagnitude < 0.01f)
                aim = Entity.GetFacingDirectionVector(parent.facingDirection);
            aim.Normalize();
            center = (Vector2)parent.transform.position + aim * Distance;
            SpawnTornado();
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
            float progress = Mathf.Clamp01(held / FullTimeNow);

            // Follow the aim; the wizard stays put, facing the tornado, in a casting pose.
            Vector2 input = GetInputVector(false, true, true);
            if (input.sqrMagnitude > 0.01f)
                aim = input.normalized;
            center = (Vector2)parent.transform.position + aim * Distance;
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
            parent.FaceTarget(center);
            parent.anim?.PlayDirectional(parent.ChargeAnimStr, -1, 0.5f);

            UpdateTornado(progress);
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

            if (progress >= 1f)
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
            RemoveTornado();
            base.OnExit();
        }

        private void Finish()
        {
            done = true;
            RemoveTornado();
            base.ExecuteSkill();
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

        // The game's air vortex swirling at the tornado, growing with it.
        private void Swirl(float progress)
        {
            try
            {
                float scale = Mathf.Lerp(StartHitScale, EndHitScale, progress);
                var vortex = new ParticleSystemOverride
                {
                    startSize = new float?(2.0f * scale),
                    startLifetime = new float?(0.45f)
                };
                PoolManager.GetPoolItem<ParticleEffect>("AirVortex").Emit(new int?(1), new Vector3?(center), vortex,
                    new Vector3?(new Vector3(0f, 0f, Random.Range(0f, 360f))), 0f, null, null);
                PoolManager.GetPoolItem<DustEmitter>().EmitCircle(4 + Mathf.RoundToInt(8 * progress), 0.4f + scale * 0.4f,
                    -4f, -1f, new Vector3?(center), null);
            }
            catch
            {
            }
        }

        // Held to the end: the tornado bursts and throws everyone away.
        private void Burst()
        {
            float scale = EndHitScale * (IsEmpowered ? EnhancedBurstScale : BurstScale);
            WindBurst.CreateBurst(center, parent.skillCategory, skillID, 5, scale);
            try
            {
                PoolManager.GetPoolItem<ParticleEffect>("WindBurstEffect").Emit(new int?(4), new Vector3?(center), null, null, 0f, null, null);
                PoolManager.GetPoolItem<DustEmitter>().EmitCircle(120, scale, -8f, -1f, new Vector3?(center), null);
                CameraController.ShakeCamera(1.3f, false);
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

        // ---- The game's tornado, for the look ----

        private void SpawnTornado()
        {
            try
            {
                if (Tornado.Prefab == null)
                    return;
                GameObject go = Object.Instantiate(Tornado.Prefab, center, Quaternion.identity);
                tornado = go.GetComponent<Tornado>();
                if (tornado == null)
                {
                    Object.Destroy(go);
                    return;
                }
                // Only the look: its own attack does nothing, and it never runs out of hits.
                tornado.attackBox?.SetAttackInfo(parent.skillCategory, skillID, HarmlessLevel, false);
                tornado.maxHitCount = 100000;
                tornado.useCamShake = false;
                tornado.transform.localScale = Vector3.one * StartLook;
                tornado.PlayAudio(true);
                if (!loggedTornado)
                {
                    loggedTornado = true;
                    CyclonePlugin.Log("Cyclone: using the game's tornado");
                }
            }
            catch (System.Exception e)
            {
                if (!loggedTornado)
                {
                    loggedTornado = true;
                    CyclonePlugin.Log($"Cyclone: the game's tornado is unavailable, swirls only: {e.Message}");
                }
                RemoveTornado();
            }
        }

        private void UpdateTornado(float progress)
        {
            if (tornado == null)
                return;
            try
            {
                tornado.transform.position = center;
                if (tornado.rigidbody2D != null)
                {
                    tornado.rigidbody2D.position = center;
                    tornado.rigidbody2D.velocity = Vector2.zero;
                }
                tornado.transform.localScale = Vector3.one * Mathf.Lerp(StartLook, EndLook, progress);
                tornado.RefreshTornado(100000);
            }
            catch
            {
            }
        }

        private void RemoveTornado()
        {
            if (tornado == null)
                return;
            try
            {
                tornado.PlayAudio(false);
                tornado.StopTornado();
            }
            catch
            {
            }
            Object.Destroy(tornado.gameObject, 0.4f);
            tornado = null;
        }
    }
}
