using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLSlingshotDash
{
    // A dash you can charge: hold the dash button to hop backward and hold that pose while you pull
    // back, release to launch. You can hold as long as you like (see MaxHoldSeconds); the charge
    // is full after MaxCharge. The longer the hold (up to MaxCharge), the faster and longer the
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
            // Nothing else here: the game builds every dash state while the wizard spawns, and
            // anything that throws in a constructor stops the wizard spawning at all.
        }

        // The slingshot itself has its own cooldown (one charge): until it's ready again, the
        // dash button is just a normal dash. Game time, so it doesn't run down while paused.
        private float slingshotReadyAt;
        // A dash used by the arcana for something else (e.g. Frost's early swap): stand still.
        private bool standStill;

        private float slingshotCooldownTotal = 1f;

        protected bool SlingshotReady => Time.time >= slingshotReadyAt;

        // For the HUD: seconds until the slingshot is ready, and the full cooldown.
        public float SlingshotRemaining => Mathf.Max(0f, slingshotReadyAt - Time.time);
        public float SlingshotCooldownTotal => slingshotCooldownTotal;

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

        // A charged launch normally boosts the dash; an arcana with its own launch can opt out.
        protected virtual bool BoostsDash => true;

        // Keep the wizard planted during a charged dash (the dash still runs for its cooldown,
        // invulnerability and timing, but doesn't move you).
        protected virtual bool HoldsStill => false;

        // Holding shorter than this is a tap: a normal dash, no hop or slingshot. The charge passed
        // to OnLaunch etc. is still hold time / MaxCharge (1 s), so at least this much.
        protected virtual float MinChargeTime => TapWindow;

        // Called first when the dash button starts a dash. Return true to use this press for
        // something else (the wizard then stands still for the dash instead of moving).
        protected virtual bool InterceptDash() => false;

        public override void OnEnter()
        {
            standStill = false;
            launchCharge = 0f;
            if (InterceptDash())
            {
                standStill = true;
                base.OnEnter();
                return;
            }
            // Slingshot still recharging, or no way to read the button: a normal dash.
            if (!SlingshotReady || !DashButton.Available(parent, skillSlot))
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
            if (chargeTime >= MinChargeTime)
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
            float maxHold = SlingshotDashPlugin.MaxHoldSeconds;
            if (!held || (maxHold > 0f && chargeTime >= maxHold))
                Launch(chargeTime < MinChargeTime ? 0f : Mathf.Clamp01(chargeTime / MaxCharge));
        }

        public override void FixedUpdate()
        {
            if (charging)
            {
                ApplyChargeVelocity();
                return;
            }
            base.FixedUpdate();
            if ((standStill || (launchCharge > 0f && HoldsStill)) && parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
        }

        public override void OnExit()
        {
            if (!charging && launchCharge > 0f && cooldownReady
                && !fsm.nextStateName.Contains("Hurt") && !fsm.nextStateName.Contains("Dead"))
                OnLand(launchCharge);
            charging = false;
            launchCharge = 0f;
            standStill = false;
            RemoveBoost();
            base.OnExit();
        }

        private void Launch(float charge)
        {
            charging = false;
            launchCharge = charge;
            if (charge > 0f && BoostsDash)
                ApplyBoost(charge);
            // Start the game's own dash now, aimed wherever the player is holding.
            base.OnEnter();
            // Like the game's own dash arcana, no attacks when the dash itself is recharging.
            if (charge > 0f && cooldownReady)
            {
                float cooldown = SlingshotDashPlugin.SlingshotCooldown;
                slingshotReadyAt = Time.time + cooldown;
                slingshotCooldownTotal = cooldown;
                SlingshotDashPlugin.Run(ReadyCue(parent, cooldown));
                OnLaunch(charge);
            }
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

        private float HopProgress => Mathf.Clamp01((chargeTime - MinChargeTime) / HopTime);
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

        // A small puff and a high swish when the slingshot is ready again, since the HUD only
        // shows the normal dash's cooldown.
        private static System.Collections.IEnumerator ReadyCue(Player player, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (player == null || !player.gameObject.activeInHierarchy)
                yield break;
            Vector3 position = player.transform.position;
            PoolManager.GetPoolItem<DustEmitter>().EmitCircle(16, 0.6f, -3f, -1f, new Vector3?(position), null);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f, 1.8f, false);
        }
    }
}
