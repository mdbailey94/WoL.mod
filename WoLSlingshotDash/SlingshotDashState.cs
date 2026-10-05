using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLSlingshotDash
{
    // A dash you can charge: hold the dash button to hop backward and hold that pose while you pull
// back, release to launch.
    // The longer the hold (up to MaxCharge), the faster and longer the dash, and the launch
    // spot gets a wind burst that knocks enemies away. A tap is a normal dash.
    //
    // The actual dash is the game's own BaseDashState; charging only delays base.OnEnter and
    // boosts the dash speed/duration stats for this one dash.
    public class SlingshotDashState : Player.BaseDashState
    {
        public new static string staticID = "SlingshotDash";

        private const float MaxCharge = 1f;
        // A press shorter than this counts as a tap: dash immediately at no extra charge.
        private const float TapWindow = 0.12f;
        // Bonus at full charge. Mod values are the change: 0.6 = +60%.
        private const float MaxSpeedBonus = 0.6f;
        private const float MaxDurationBonus = 0.4f;
        private const float DustInterval = 0.1f;
        // The backward hop at the start of a charge.
        private const float HopTime = 0.18f;
        private const string SpeedModID = "SlingshotDash_Speed";
        private const string DurationModID = "SlingshotDash_Duration";

        private bool charging;
        private float chargeTime;
        private float nextDust;
        private bool boosted;
        private Vector2 hopVelocity;
        private bool hopStarted;
        // Charge of the dash in flight (0 for a tap), for the burst where it lands.
        private float launchCharge;

        public SlingshotDashState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
            applyStopElementStatus = true;
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
            // A charged launch that finished (not interrupted by a hit) bursts where it lands too,
            // so the far end of the slingshot hits as well as the start.
            if (!charging && launchCharge > 0f && cooldownReady
                && !fsm.nextStateName.Contains("Hurt") && !fsm.nextStateName.Contains("Dead"))
                LaunchBurst(launchCharge, true);
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
            {
                ApplyBoost(charge);
                LaunchBurst(charge, false);
            }
            // Start the game's own dash now, aimed wherever the player is holding.
            base.OnEnter();
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

        // Pushes enemies away from where you launched (and where you land); bigger with more
        // charge. The hit area is the burst scale, and the dust ring is drawn to match it.
        private void LaunchBurst(float charge, bool landing)
        {
            Vector3 position = parent.transform.position;
            float scale = (landing ? 1.75f : 1.5f) + 1.75f * charge;
            WindBurst burst = WindBurst.CreateBurst(position, parent.skillCategory, skillID, 1, scale);
            burst.emitParticles = false;
            PoolManager.GetPoolItem<ParticleEffect>("WindBurstEffect").Emit(new int?(2 + Mathf.RoundToInt(3 * charge)),
                new Vector3?(position), null, null, 0f, null, null);
            PoolManager.GetPoolItem<DustEmitter>().EmitCircle(60 + Mathf.RoundToInt(90 * charge), scale, -8f, -1f,
                new Vector3?(position), null);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f,
                (landing ? 1.1f : 1.3f) - 0.3f * charge, false);
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
