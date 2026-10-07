using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLTrailblazer
{
    // Trailblazer (Fire, standard arcana), an active effect: a quick stamp sets the wizard
    // ablaze with a fire burst, and for the next few seconds (FlameTrail) a ring of fire round them
    // hurts enemies who touch it, and running, dashing and every movement arcana leave a burning
    // trail behind them.
    public class TrailblazerState : Player.SkillState
    {
        public new static string staticID = "Trailblazer";

        private const float CastTime = 0.2f;   // the stamp; dash to cancel it (the fire stays lit)

        private float time;
        private bool lit;
        private bool done;

        public TrailblazerState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
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

        public override void OnEnter()
        {
            base.OnEnter();
            time = 0f;
            lit = false;
            done = false;
        }

        public override void ExecuteSkill()
        {
            if (done)
            {
                base.ExecuteSkill();
                return;
            }
            if (!lit)
            {
                lit = true;
                Ignite();
            }
            if (CancelToDash(false))
                return;

            time += Time.deltaTime;
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
            parent.anim?.PlayDirectional(parent.GSlamAnimStr, -1, 0.5f);
            if (time >= CastTime)
            {
                done = true;
                base.ExecuteSkill();
            }
        }

        private void Ignite()
        {
            Vector2 position = parent.transform.position;
            float duration = IsEmpowered ? TrailblazerPlugin.EnhancedDuration : TrailblazerPlugin.Duration;
            FlameTrail.Light(parent, this, duration);
            try
            {
                // The hit is wide, but its visible burst small, so it doesn't blind.
                FlameBurst.CreateBurst(position, parent.skillCategory, skillID, 1, 2.5f, false);
                FlameBurst.CreateBurst(position, parent.skillCategory, skillID, 1, 0.9f, true);
                PoolManager.GetPoolItem<FireBurst>()?.EmitSingle(new int?(8), new Vector3?(position));
                CameraController.ShakeCamera(0.4f, false);
            }
            catch
            {
            }
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f, 0.8f, false);
        }
    }
}
