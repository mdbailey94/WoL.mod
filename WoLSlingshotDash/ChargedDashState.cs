using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLSlingshotDash
{
    // A dash you can charge: hold the dash button to hop backward and hold that pose while you pull
    // back, release to launch. The longer the hold (up to MaxCharge), the faster and longer the
    // dash. A tap is a normal dash. Each arcana adds its own attacks through OnLaunch,
    // WhileDashing and OnLand, which only run for a charged launch.
    //
    // The actual dash is the game's own BaseDashState; charging only delays base.OnEnter and
    // boosts the dash speed/duration stats for this one dash.
    public abstract class ChargedDashState : Player.BaseDashState
    {
        private const float MaxCharge = 1f;
        // A press shorter than this counts as a tap: dash immediately at no extra charge.
        private const float TapWindow = 0.12f;
        // Bonus at full charge. Mod values are the change: 0.6 = +60%.
        private const float MaxSpeedBonus = 0.6f;
        private const float MaxDurationBonus = 0.4f;
        private const float DustInterval = 0.1f;
        // The backward hop at the start of a charge.
        private const float HopTime = 0.18f;
        private const string SpeedModID = "ChargedDash_Speed";
        private const string DurationModID = "ChargedDash_Duration";

        private bool charging;
        private float chargeTime;
        private float nextDust;
        private bool boosted;
        private Vector2 hopVelocity;
        private bool hopStarted;
        // Charge of the dash in flight (0 for a tap).
        private float launchCharge;

        protected ChargedDashState(string skillID, FSM fsm, Player parentPlayer) : base(skillID, fsm, parentPlayer)
        {
            applyStopElementStatus = true;
            // One dash charge, so the long cooldown means one slingshot at a time.
            InitChargeSkillSettings(1, 0f, skillData, this);
        }

        // Right after a charged launch starts, still at the launch spot; inputVector is the aim.
        protected abstract void OnLaunch(float charge);

        // Every frame of a charged dash.
        protected virtual void WhileDashing(float charge)
        {
        }

        // When a charged dash ends normally (not cut short by a hit or death).
        protected virtual void OnLand(float charge)
        {
        }

        public override void OnEnter()
        {
            // Without a way to read the button we can't charge; behave like a normal dash.
            if (!DashButton.Available(parent, skillSlot))
            {
                base.OnEnter();
                return;
            }

            charging = true;
            launchCharge = 0f;
            chargeTime = 0f;
            nextDust = 0f;
            hopStarted = false;
            ApplyChargeVelocity();
        }

        public override void Update()
        {
            if (!charging)
            {
                if (launchCharge > 0f && cooldownReady && !finishedDashing)
                    WhileDashing(launchCharge);
                base.Update();
                return;
            }

            chargeTime += Time.deltaTime;
            if (chargeTime >= TapWindow)
            {
                // Held past a tap: hop backward, then keep turning to face the aim.
                if (!hopStarted)
                    StartHop();
                else if (!Hopping)
                    parent.SetFacingDirectionBasedOnInput();
                PlayChargePose();
                EmitChargeDust();
            }
            ApplyChargeVelocity();

            bool held = DashButton.Held(parent, skillSlot);
            if (!held || chargeTime >= MaxCharge)
                Launch(chargeTime < TapWindow ? 0f : Mathf.Clamp01(chargeTime / MaxCharge));
        }

        public override void FixedUpdate()
        {
            if (charging)
            {
                ApplyChargeVelocity();
                return;
            }
            base.FixedUpdate();
        }

        public override void OnExit()
        {
            if (!charging && launchCharge > 0f && cooldownReady
                && !fsm.nextStateName.Contains("Hurt") && !fsm.nextStateName.Contains("Dead"))
                OnLand(launchCharge);
            charging = false;
            launchCharge = 0f;
            RemoveBoost();
            base.OnExit();
        }

        private void Launch(float charge)
        {
            charging = false;
            launchCharge = charge;
            if (charge > 0f)
                ApplyBoost(charge);
            // Start the game's own dash now, aimed wherever the player is holding.
            base.OnEnter();
            // Like the game's own dash arcana, no attacks when the dash has no charge left.
            if (charge > 0f && cooldownReady)
                OnLaunch(charge);
        }

        private void ApplyBoost(float charge)
        {
            Movement movement = parent.movement;
            if (movement == null)
                return;
            movement.dashSpeedStat?.AddMod(new NumVarStatMod(SpeedModID, MaxSpeedBonus * charge, 10, VarStatModType.Multiplicative));
            movement.dashDurationStat?.AddMod(new NumVarStatMod(DurationModID, MaxDurationBonus * charge, 10, VarStatModType.Multiplicative));
            boosted = true;
        }

        private void RemoveBoost()
        {
            if (!boosted)
                return;
            Movement movement = parent.movement;
            if (movement != null)
            {
                movement.dashSpeedStat?.RemoveMod(SpeedModID);
                movement.dashDurationStat?.RemoveMod(DurationModID);
            }
            boosted = false;
        }

        // Little dust puffs at your feet that get bigger as the charge builds.
        private void EmitChargeDust()
        {
            if (chargeTime < nextDust)
                return;
            nextDust = chargeTime + DustInterval;
            float charge = Mathf.Clamp01(chargeTime / MaxCharge);
            PoolManager.GetPoolItem<DustEmitter>().EmitCircle(6 + Mathf.RoundToInt(14 * charge), 0.4f + 0.8f * charge,
                -3f, -1f, new Vector3?(parent.transform.position), null);
        }

        private float HopProgress => Mathf.Clamp01((chargeTime - TapWindow) / HopTime);
        private bool Hopping => hopStarted && HopProgress < 1f;

        // Face where you're aiming and hop away from it, like pulling back a slingshot.
        private void StartHop()
        {
            hopStarted = true;
            parent.SetFacingDirectionBasedOnInput();
            Vector2 facing = Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            hopVelocity = -facing * (SlingshotDashPlugin.HopDistance / HopTime);
        }

        // Planted, except while hopping backward.
        private void ApplyChargeVelocity()
        {
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Hopping ? hopVelocity : Vector2.zero;
        }

        // Plays the chosen animation through the hop up to the pose frame, then holds that frame
        // (the game holds a pose the same way: replaying the animation at a fixed time each frame).
        private void PlayChargePose()
        {
            if (parent.anim == null)
                return;
            string anim = SlingshotDashPlugin.ChargeAnimation(parent);
            if (string.IsNullOrEmpty(anim))
                return;
            float pose = SlingshotDashPlugin.ChargePoseFrame;
            float time = pose * HopProgress;
            parent.anim.PlayDirectional(anim, -1, time);
        }
    }
}
