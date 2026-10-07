using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLThunderhead
{
    // Thunderhead (Lightning, standard arcana): after a very short wind-up on the ground (still
    // vulnerable), the wizard rises into the air, crackling, for about 1.5 seconds. Up there they're
    // airborne and can't be hurt, like the game's jumping arcana, while lightning rains down on a
    // small area beneath them for 1.2 seconds (1.7 enhanced): scattered strikes all over it (level
    // 1) and a big strike right below every half second (level 2), which can shock. Then they drift
    // back down.
    //
    // The hits are the game's lightning bursts; each strike also shows the game's own lightning
    // bolt from the sky (stripped down to its animation, so it can't hit anything itself).
    public class ThunderheadState : Player.SkillState
    {
        public new static string staticID = "Thunderhead";

        private const float WindupTime = 0.12f;      // on the ground, still vulnerable
        private const float RiseTime = 0.15f;
        private const float StormTime = 1.2f;        // about 1.5 s in the air in all
        private const float EnhancedStormTime = 1.7f;
        private const float FallTime = 0.15f;
        private const float Height = 0.9f;
        private const float Radius = 1.8f;          // the area beneath you
        private const float EnhancedRadius = 2.4f;
        private const float StrikeInterval = 0.12f; // scattered strikes
        private const float EnhancedStrikeInterval = 0.09f;
        private const float BigStrikeInterval = 0.5f;
        private const float StrikeScale = 1f;
        private const float BigStrikeScale = 1.6f;
        private const float SparkInterval = 0.08f;

        private float time;
        private float nextStrike;
        private float nextBigStrike;
        private float nextSpark;
        private bool done;
        private Levitator levitator;
        private bool airborne;
        private bool tookOff;
        private const string AirborneModID = "Thunderhead_Airborne";
        private static bool loggedBolt;

        public ThunderheadState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
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

        private float StormTimeNow => IsEmpowered ? EnhancedStormTime : StormTime;
        private float RadiusNow => IsEmpowered ? EnhancedRadius : Radius;

        public override void OnEnter()
        {
            base.OnEnter();
            time = 0f;
            nextStrike = RiseTime;
            nextBigStrike = RiseTime;
            nextSpark = 0f;
            done = false;
            levitator = Levitator.On(parent);
            tookOff = false;
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(parent.transform.position), null, 24f, -1f, 0.7f, false);
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

            time += Time.deltaTime;

            // A moment's wind-up on the ground (still vulnerable), then up into the air.
            if (time < WindupTime)
            {
                if (parent.rigidbody2D != null)
                    parent.rigidbody2D.velocity = Vector2.zero;
                parent.anim?.PlayDirectional(parent.ChargeAnimStr, -1, 0.15f);
                return;
            }
            if (!tookOff)
            {
                tookOff = true;
                TakeOff();
            }
            float t = time - WindupTime;
            float stormEnd = RiseTime + StormTimeNow;
            float end = stormEnd + FallTime;

            // Hovering in place, arms raised.
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
            parent.anim?.PlayDirectional(parent.ChargeAnimStr, -1, 0.5f);
            float lift = t < RiseTime ? t / RiseTime
                : t < stormEnd ? 1f
                : 1f - Mathf.Clamp01((t - stormEnd) / FallTime);
            if (levitator != null)
                levitator.height = Height * Mathf.SmoothStep(0f, 1f, lift) + (lift >= 1f ? Mathf.Sin(t * 9f) * 0.04f : 0f);

            Vector2 ground = parent.transform.position;
            if (t >= RiseTime && t < stormEnd)
            {
                if (t >= nextStrike)
                {
                    nextStrike = t + (IsEmpowered ? EnhancedStrikeInterval : StrikeInterval);
                    Vector2 offset = Random.insideUnitCircle * RadiusNow;
                    Strike(ground + new Vector2(offset.x, offset.y * 0.7f), 1, StrikeScale);
                }
                if (t >= nextBigStrike)
                {
                    nextBigStrike = t + BigStrikeInterval;
                    Strike(ground, 2, BigStrikeScale);
                    Shake(0.5f);
                }
                if (t >= nextSpark)
                {
                    nextSpark = t + SparkInterval;
                    Spark(ground + new Vector2(0f, Height + 0.5f) + Random.insideUnitCircle * 0.5f);
                }
            }

            // Back on the ground (with the fall nearly done): can be hurt again.
            if (airborne && t >= stormEnd + FallTime * 0.7f)
                Touchdown();

            if (t >= end)
            {
                done = true;
                Land();
                base.ExecuteSkill();
            }
        }

        public override void OnExit()
        {
            Land();
            base.OnExit();
        }

        // Up in the air: airborne (like a jump) and not to be hurt.
        private void TakeOff()
        {
            if (airborne || parent.health == null)
                return;
            airborne = true;
            parent.health.invulnerable = true;
            try
            {
                parent.airborneStat?.AddMod(new BoolVarStatMod(AirborneModID, true, 10));
            }
            catch
            {
            }
        }

        private void Touchdown()
        {
            if (!airborne)
                return;
            airborne = false;
            // Always back to hurtable: restoring an "already invulnerable" from, say, a dash's
            // moment of invulnerability that ended meanwhile would leave you invulnerable for good.
            if (parent.health != null)
                parent.health.invulnerable = false;
            try
            {
                parent.airborneStat?.RemoveMod(AirborneModID);
            }
            catch
            {
            }
        }

        private void Land()
        {
            Touchdown();
            if (levitator != null)
            {
                levitator.Off();
                levitator = null;
            }
        }

        private void Strike(Vector2 position, int level, float scale)
        {
            LightningBurst.CreateBurst(position, parent.skillCategory, skillID, level, scale, false);
            Bolt(position, level == 2 ? 1.25f : 1f);
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
                    ThunderheadPlugin.Log("Thunderhead: using the game's lightning bolts");
                }
            }
            catch (System.Exception e)
            {
                if (!loggedBolt)
                {
                    loggedBolt = true;
                    ThunderheadPlugin.Log($"Thunderhead: the game's lightning bolt is unavailable, bursts only: {e.Message}");
                }
            }
        }

        private static void Spark(Vector2 position)
        {
            try
            {
                PoolManager.GetPoolItem<HitSparkEmitter>().EmitSingle(HitSparkType.Small, position, position, null, null, 0f);
            }
            catch
            {
            }
        }

        private static void Shake(float intensity)
        {
            try
            {
                CameraController.ShakeCamera(intensity, false);
            }
            catch
            {
            }
        }
    }

    // Lifts the wizard's picture into the air (the wizard themself, their shadow and their hitbox
    // stay on the ground). Applied after the animation each frame and taken back off the next.
    public class Levitator : MonoBehaviour
    {
        public float height;
        private Vector3 applied;
        private Vector3 lastSet;
        private bool active;

        public static Levitator On(Player player)
        {
            try
            {
                if (player.spriteRenderer == null)
                    return null;
                GameObject body = player.spriteRenderer.gameObject;
                Levitator levitator = body.GetComponent<Levitator>();
                if (levitator == null)
                    levitator = body.AddComponent<Levitator>();
                levitator.enabled = true;
                levitator.height = 0f;
                return levitator;
            }
            catch
            {
                return null;
            }
        }

        private void LateUpdate()
        {
            Vector3 position = transform.localPosition;
            if (active && position == lastSet)
                position -= applied;
            applied = new Vector3(0f, height, 0f);
            lastSet = position + applied;
            transform.localPosition = lastSet;
            active = true;
        }

        public void Off()
        {
            if (active && transform.localPosition == lastSet)
                transform.localPosition -= applied;
            applied = Vector3.zero;
            height = 0f;
            active = false;
            enabled = false;
        }
    }
}
