using UnityEngine;

namespace WoLSlingshotDash
{
    // Blazing Slingshot (Fire): a charged launch drives a long flaming punch ahead of you for the
    // whole dash, and leaves a trail of flame bursts that suck enemies in behind you. Where you
    // land, a bigger burst pulls everyone nearby into a pile. Bigger with more charge.
    //
    // The skill's knockback is negative (a pull, like Gust Burst), so bursts pull toward their
    // centre. The punch's knockback direction is overridden with the dash direction, which with
    // the negative strength throws enemies it hits backward, behind the wizard.
    public class BlazingSlingshotState : ChargedDashState
    {
        public new static string staticID = "BlazingSlingshot";

        // The punch is respawned along the dash so it stays out in front the whole way.
        private const float PunchInterval = 0.06f;
        private const float TrailInterval = 0.09f;

        private Vector2 direction;
        private float nextPunch;
        private float nextTrail;

        public BlazingSlingshotState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
        }

        protected override void OnLaunch(float charge)
        {
            direction = inputVector.sqrMagnitude > 0.01f
                ? inputVector.normalized
                : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            nextPunch = 0f;
            nextTrail = TrailInterval;
            Punch(charge);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(parent.transform.position), null, 24f, -1f,
                0.9f - 0.2f * charge, false);
        }

        protected override void WhileDashing(float charge)
        {
            nextPunch -= Time.deltaTime;
            nextTrail -= Time.deltaTime;
            if (nextPunch <= 0f)
                Punch(charge);
            if (nextTrail <= 0f)
            {
                nextTrail = TrailInterval;
                FlameBurst.CreateBurst(parent.transform.position, parent.skillCategory, skillID, 1, 1.25f + charge, true);
            }
        }

        protected override void OnLand(float charge)
        {
            FlameBurst.CreateBurst(parent.transform.position, parent.skillCategory, skillID, 1, 2f + 1.5f * charge, true);
        }

        // A long fist of flame out in front, pointed along the dash.
        private void Punch(float charge)
        {
            nextPunch = PunchInterval;
            Vector2 origin = parent.attackOriginTrans != null
                ? (Vector2)parent.attackOriginTrans.position
                : (Vector2)parent.transform.position;
            FlamePunch punch = ChaosInst<FlamePunch>(FlamePunch.Prefab, new Vector2?(origin + direction * 0.5f),
                new Quaternion?(Globals.GetRotationQuaternion(direction)), null);
            if (punch == null)
                return;
            punch.SetScale(1.5f + 1.5f * charge);
            if (punch.attack != null)
            {
                punch.attack.SetAttackInfo(parent.skillCategory, skillID, 1, false);
                punch.attack.knockbackOverwriteVector = direction;
            }
        }
    }
}
