using System.Collections;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Vacuum Fist (Air): a charged launch rushes you forward on a wind that grabs enemies you pass
    // and drags them along with you (level 1, its knockback pointed along the dash: the Drag
    // setting), finishing with a punch that lands in a vacuum, pulling everyone nearby in for a
    // moment (level 2, negative knockback, like Gust Burst). Bigger with more charge. Enhanced, it
    // also leaves a trail of gusts along the dash (level 3) that each blow twice.
    public class SlingshotDashState : ChargedDashState
    {
        public new static string staticID = "SlingshotDash";

        public SlingshotDashState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
        }

        private const float DragInterval = 0.1f;
        private const float DragScale = 2f;        // hit area of each drag along the dash
        private const float LaunchDragScale = 3f;  // and where you launch
        private const int VacuumPulses = 4;
        private const float VacuumPulseInterval = 0.15f;
        private const float PunchAt = 0.55f;       // how far into the dash the punch winds up
        private const float GustInterval = 0.035f;
        private const float TrailInterval = 0.08f;
        private const float TrailScale = 1.1f;
        private const float TrailRepeat = 0.35f;
        private float nextGust;
        private float nextTrail;
        private float nextDrag;
        private Vector2 direction;
        private float launchedAt;
        private bool punchPlayed;

        // Charging: a whirlwind spinning fast round your feet, quicker and wider as it builds.
        protected override float ChargeEffectInterval => 0.035f;
        private float whirl;

        protected override void ChargeEffect(Vector2 position, float charge)
        {
            whirl += 75f * Mathf.Deg2Rad;
            float radius = 0.5f + 0.5f * charge;
            for (int i = 0; i < 2; i++)
            {
                float a = whirl + i * Mathf.PI;
                Effects.AirPuffs(position + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.5f) * radius,
                    1 + Mathf.RoundToInt(2 * charge));
            }
            if (charge >= 0.3f && Random.value < 0.25f)
                Effects.WindSwirl(position, 1);
        }

        protected override void OnLaunch(float charge)
        {
            Vector2 position = parent.transform.position;
            direction = inputVector.sqrMagnitude > 0.01f
                ? inputVector.normalized
                : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            launchedAt = Time.time;
            punchPlayed = false;
            nextGust = 0f;
            nextTrail = 0f;
            nextDrag = 0f;
            // A wide grab where you launch, dragging enemies along with you.
            Drag(position, LaunchDragScale);
            Effects.WindSwirl(position, 2 + Mathf.RoundToInt(3 * charge));
            Effects.AirPuffs(position, 8 + Mathf.RoundToInt(8 * charge));
            Effects.Shake(0.5f + 0.5f * charge);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f,
                1.3f - 0.3f * charge, false);
        }

        protected override void WhileDashing(float charge)
        {
            Vector2 position = parent.transform.position;
            // The rushing wind: grabs and drags enemies you pass along the dash.
            if (Time.time >= nextDrag)
            {
                nextDrag = Time.time + DragInterval;
                Drag(position, DragScale);
            }
            // Enhanced: gusts left along the dash line.
            if (IsEmpowered && Time.time >= nextTrail)
            {
                nextTrail = Time.time + TrailInterval;
                SlingshotDashPlugin.Run(TrailGust(position, parent.skillCategory, skillID));
            }
            // Flair only: wind up the punch for the end of the rush.
            if (!punchPlayed && Time.time - launchedAt >= dashDuration * PunchAt)
            {
                punchPlayed = true;
                parent.anim?.PlayDirectional(parent.ForehandAnimStr, -1, 0f);
            }
            // Flair only: a streaming gust behind you as you fly.
            if (Time.time < nextGust)
                return;
            nextGust = Time.time + GustInterval;
            Effects.AirPuffs(position, 2);
            Effects.Dust(position, 3, 0.25f);
        }

        // The punch lands in a vacuum that keeps pulling everyone nearby in for a moment.
        protected override void OnLand(float charge)
        {
            Vector2 position = parent.transform.position;
            SlingshotDashPlugin.Run(Vacuum(position, 1.75f + 1.25f * charge, parent.skillCategory, skillID));
            Effects.WindSwirl(position, 3);
            Effects.DustSuction(position, 30);
            Effects.Shake(0.5f + 0.4f * charge);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f,
                1.1f - 0.3f * charge, false);
        }

        // An invisible wind burst (level 1) whose knockback points along the dash, so it drags.
        private void Drag(Vector2 position, float scale)
        {
            WindBurst burst = WindBurst.CreateBurst(position, parent.skillCategory, skillID, 1, scale);
            if (burst == null)
                return;
            burst.emitParticles = false;
            if (burst.attack != null)
                burst.attack.knockbackOverwriteVector = direction;
        }

        // Level 2 pulses: negative knockback, pulling toward the centre like Gust Burst.
        private static IEnumerator Vacuum(Vector2 position, float scale, string skillCategory, string id)
        {
            for (int i = 0; i < VacuumPulses; i++)
            {
                WindBurst pulse = WindBurst.CreateBurst(position, skillCategory, id, 2, scale);
                if (pulse != null)
                    pulse.emitParticles = false;
                Effects.WindSwirl(position, 1);
                Effects.Dust(position, 24, scale);
                yield return new WaitForSeconds(VacuumPulseInterval);
            }
        }

        // One gust on the trail: blows now and once more a moment later.
        private static IEnumerator TrailGust(Vector2 position, string skillCategory, string id)
        {
            for (int i = 0; i < 2; i++)
            {
                WindBurst gust = WindBurst.CreateBurst(position, skillCategory, id, 3, TrailScale);
                if (gust != null)
                    gust.emitParticles = false;
                Effects.WindSwirl(position, 1);
                Effects.AirPuffs(position, 3);
                yield return new WaitForSeconds(TrailRepeat);
            }
        }
    }
}
