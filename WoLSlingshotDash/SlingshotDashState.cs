using UnityEngine;

namespace WoLSlingshotDash
{
    // A dash you can charge: hold the dash button to plant and pull back, release to launch.
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
        private const string SpeedModID = "SlingshotDash_Speed";
        private const string DurationModID = "SlingshotDash_Duration";

        private bool charging;
        private float chargeTime;
        private float nextDust;
        private bool boosted;

        public SlingshotDashState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
            applyStopElementStatus = true;
        }

        public override void OnEnter()
        {
            // Without a way to read the button we can't charge; behave like a normal dash.
            if (!DashButton.Available(parent))
            {
                base.OnEnter();
                return;
            }

            charging = true;
            chargeTime = 0f;
            nextDust = 0f;
            StopMoving();
        }

        public override void Update()
        {
            if (!charging)
            {
                base.Update();
                return;
            }

            chargeTime += Time.deltaTime;
            StopMoving();
            EmitChargeDust();

            bool held = DashButton.Held(parent);
            if (!held || chargeTime >= MaxCharge)
                Launch(chargeTime < TapWindow ? 0f : Mathf.Clamp01(chargeTime / MaxCharge));
        }

        public override void FixedUpdate()
        {
            if (charging)
            {
                StopMoving();
                return;
            }
            base.FixedUpdate();
        }

        public override void OnExit()
        {
            charging = false;
            RemoveBoost();
            base.OnExit();
        }

        private void Launch(float charge)
        {
            charging = false;
            if (charge > 0f)
            {
                ApplyBoost(charge);
                LaunchBurst(charge);
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

        // Pushes enemies away from where you launched; bigger with more charge.
        private void LaunchBurst(float charge)
        {
            Vector3 position = parent.transform.position;
            float scale = 1.5f + 1.5f * charge;
            WindBurst burst = WindBurst.CreateBurst(position, parent.skillCategory, skillID, 1, scale);
            burst.emitParticles = false;
            PoolManager.GetPoolItem<ParticleEffect>("WindBurstEffect").Emit(new int?(2 + Mathf.RoundToInt(3 * charge)),
                new Vector3?(position), null, null, 0f, null, null);
            PoolManager.GetPoolItem<DustEmitter>().EmitCircle(60 + Mathf.RoundToInt(90 * charge), 1.5f + charge, -8f, -1f,
                new Vector3?(position), null);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f,
                1.3f - 0.3f * charge, false);
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

        private void StopMoving()
        {
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
        }
    }
}
