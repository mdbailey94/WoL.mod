using System.Collections.Generic;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLSlingshotDash
{
    // A dash you can charge: hold the dash button to hop backward and hold that pose while you pull
    // back, release to launch. A hold launches by itself after MaxHold (2 s by default); the charge
    // is full after MaxCharge. The longer the hold (up to MaxCharge), the faster and longer the
    // dash. A tap is the arcana's dash at normal length. Each arcana adds its own attacks through
    // OnLaunch, WhileDashing and OnLand, which run for a tap or a charged launch while the
    // slingshot is ready; while it's recharging, the dash is a plain one.
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
        private SpriteShaker shaker;
        // Charge of the dash in flight (0 for a tap), and whether it's the arcana's own dash (with
        // its attacks) rather than a plain one.
        private float launchCharge;
        private bool slingshotting;

        protected ChargedDashState(string skillID, FSM fsm, Player parentPlayer) : base(skillID, fsm, parentPlayer)
        {
            applyStopElementStatus = true;
            // Nothing else here: the game builds every dash state while the wizard spawns, and
            // anything that throws in a constructor stops the wizard spawning at all.
        }

        // The slingshot itself has its own cooldown (one charge): until it's ready again, the
        // dash button is just a normal dash. Game time, so it doesn't run down while paused.
        // Kept per player and arcana (not on this object) so the HUD can look it up whichever
        // state object it holds.
        private static readonly Dictionary<string, KeyValuePair<float, float>> cooldowns =
            new Dictionary<string, KeyValuePair<float, float>>(); // key -> (ready at, total)

        // A dash used by the arcana for something else (e.g. Frost's early swap): stand still.
        private bool standStill;

        private static string CooldownKey(Player player, string id) =>
            (player != null ? player.GetInstanceID() : 0) + "/" + id;

        // For the HUD: seconds until this player's slingshot of this arcana is ready, and the full
        // cooldown. False when it's ready.
        public static bool TryGetCooldown(Player player, string id, out float remaining, out float total)
        {
            KeyValuePair<float, float> entry;
            remaining = 0f;
            total = 1f;
            if (string.IsNullOrEmpty(id) || !cooldowns.TryGetValue(CooldownKey(player, id), out entry))
                return false;
            remaining = entry.Key - Time.time;
            total = entry.Value;
            return remaining > 0f;
        }

        public static bool AnyCooldownRunning()
        {
            foreach (KeyValuePair<float, float> entry in cooldowns.Values)
            {
                if (entry.Key > Time.time)
                    return true;
            }
            return false;
        }

        protected bool SlingshotReady
        {
            get
            {
                float remaining, total;
                return !TryGetCooldown(parent, skillID, out remaining, out total);
            }
        }

        // Right after the arcana's dash starts (charge 0 for a tap), still at the launch spot;
        // inputVector is the aim.
        protected abstract void OnLaunch(float charge);

        // A tap skips the charge-up: an arcana that sets something up while charging does it here.
        protected virtual void OnTap()
        {
        }

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

        // When a hold first passes MinChargeTime (the hop starts), and every frame of the hold
        // after that. holdTime counts from the start of the press.
        protected virtual void OnChargeStarted()
        {
        }

        protected virtual void WhileCharging(float holdTime)
        {
        }

        // The animation for the hop and the held pose, and how far into it the pose is.
        protected virtual string ChargeAnimation => SlingshotDashPlugin.ChargeAnimation(parent);
        protected virtual float ChargePoseFrame => SlingshotDashPlugin.ChargePoseFrame;

        // A tap is just a plain dash (no slingshot, and it doesn't use the slingshot's charge),
        // for an arcana whose move only makes sense held.
        protected virtual bool TapIsPlainDash => false;

        // Hop backward at the start of a charge (otherwise the pose plays where you stand).
        protected virtual bool HopsBack => true;

        // Keep facing the way the charge started instead of turning with the aim.
        protected virtual bool LocksFacing => false;

        // Launch automatically after holding this long (0 = no limit).
        protected virtual float MaxHoldTime => SlingshotDashPlugin.MaxHoldSeconds;

        // Every physics step of a charged dash, after the game's own movement: set the velocity
        // here to steer the dash yourself.
        protected virtual void DashFixedUpdate(float charge)
        {
        }

        // Whenever the state ends, however it ends (clean up anything left over).
        protected virtual void OnStateExit()
        {
        }

        // How far a plain dash carries this wizard: the game's dash speed x dash duration (with
        // relics), or 3 if that can't be read or looks wrong. Never throws.
        private static bool loggedDash;

        protected float PlainDashDistance()
        {
            float computed = 0f;
            try
            {
                Movement movement = parent != null ? parent.movement : null;
                if (movement != null && movement.dashSpeedStat != null && movement.dashDurationStat != null)
                    computed = movement.dashSpeedStat.ModifiedValue * movement.dashDurationStat.ModifiedValue;
            }
            catch
            {
                computed = 0f;
            }
            bool usable = computed >= 1f && computed <= 12f;
            if (!loggedDash)
            {
                loggedDash = true;
                SlingshotDashPlugin.Log($"Dash length from speed x duration: {computed:0.##}" + (usable ? "" : " (using 3)"));
            }
            return usable ? computed : 3f;
        }

        public override void OnEnter()
        {
            standStill = false;
            launchCharge = 0f;
            slingshotting = false;
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
                if (slingshotting && cooldownReady && !finishedDashing)
                    WhileDashing(launchCharge);
                base.Update();
                return;
            }

            chargeTime += Time.deltaTime;
            if (chargeTime >= MinChargeTime)
            {
                // Held past a tap: hop backward, then keep turning to face the aim.
                if (!hopStarted)
                {
                    StartHop();
                    OnChargeStarted();
                }
                else if (!Hopping && !LocksFacing)
                    parent.SetFacingDirectionBasedOnInput();
                PlayChargePose();
                EmitChargeDust();
                WhileCharging(chargeTime);
            }
            ApplyChargeVelocity();

            bool held = DashButton.Held(parent, skillSlot);
            float maxHold = MaxHoldTime;
            if (!held || (maxHold > 0f && chargeTime >= maxHold))
                Launch(chargeTime < MinChargeTime ? 0f : Mathf.Clamp01(chargeTime / MaxCharge), chargeTime < MinChargeTime);
        }

        public override void FixedUpdate()
        {
            if (charging)
            {
                ApplyChargeVelocity();
                return;
            }
            base.FixedUpdate();
            if ((standStill || (slingshotting && HoldsStill)) && parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
            if (slingshotting && cooldownReady && !standStill)
                DashFixedUpdate(launchCharge);
        }

        public override void OnExit()
        {
            StopShaking();
            if (!charging && slingshotting && cooldownReady
                && !fsm.nextStateName.Contains("Hurt") && !fsm.nextStateName.Contains("Dead"))
                OnLand(launchCharge);
            charging = false;
            launchCharge = 0f;
            slingshotting = false;
            standStill = false;
            RemoveBoost();
            try
            {
                OnStateExit();
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"{skillID} cleanup failed: {e.Message}");
            }
            base.OnExit();
        }

        // A tap is the arcana's own dash at normal length (charge 0, no boost); a hold is charged.
        // Either way it uses the slingshot's charge.
        private void Launch(float charge, bool tap)
        {
            charging = false;
            StopShaking();
            launchCharge = charge;
            if (tap && TapIsPlainDash)
            {
                base.OnEnter();
                return;
            }
            if (charge > 0f && BoostsDash)
                ApplyBoost(charge);
            // Start the game's own dash now, aimed wherever the player is holding.
            base.OnEnter();
            // Like the game's own dash arcana, no attacks when the dash itself is recharging.
            slingshotting = cooldownReady;
            if (slingshotting)
            {
                float cooldown = SlingshotDashPlugin.SlingshotCooldown;
                cooldowns[CooldownKey(parent, skillID)] = new KeyValuePair<float, float>(Time.time + cooldown, cooldown);
                SlingshotDashPlugin.Run(ReadyCue(parent, cooldown));
                if (tap)
                    OnTap();
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

        // The charge-up effect at your feet, every ChargeEffectInterval, growing as the charge
        // builds; and the wizard trembling, for an arcana that shakes.
        private void EmitChargeDust()
        {
            float charge = Mathf.Clamp01(chargeTime / MaxCharge);
            float shake = ChargeShake(charge);
            if (shake > 0f)
            {
                if (shaker == null && parent.spriteRenderer != null)
                {
                    GameObject body = parent.spriteRenderer.gameObject;
                    shaker = body.GetComponent<SpriteShaker>();
                    if (shaker == null)
                        shaker = body.AddComponent<SpriteShaker>();
                }
                if (shaker != null)
                    shaker.amount = shake;
            }
            if (chargeTime < nextDust)
                return;
            nextDust = chargeTime + ChargeEffectInterval;
            ChargeEffect(parent.transform.position, Mathf.Clamp01(chargeTime / MaxCharge));
        }

        protected virtual float ChargeEffectInterval => DustInterval;

        // How far the wizard trembles while charging (0 = not at all).
        protected virtual float ChargeShake(float charge) => 0f;

        private void StopShaking()
        {
            if (shaker != null)
                shaker.Stop();
        }

        // Each arcana shows its own element here; plain dust otherwise.
        protected virtual void ChargeEffect(Vector2 position, float charge)
        {
            Effects.Dust(position, 6 + Mathf.RoundToInt(14 * charge), 0.4f + 0.8f * charge);
        }

        private float HopProgress => Mathf.Clamp01((chargeTime - MinChargeTime) / HopTime);
        private bool Hopping => hopStarted && HopProgress < 1f;

        // Face where you're aiming and hop away from it, like pulling back a slingshot.
        private void StartHop()
        {
            hopStarted = true;
            parent.SetFacingDirectionBasedOnInput();
            Vector2 facing = Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            hopVelocity = HopsBack ? -facing * (SlingshotDashPlugin.HopDistance / HopTime) : Vector2.zero;
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
            string anim = ChargeAnimation;
            if (string.IsNullOrEmpty(anim))
                return;
            float pose = ChargePoseFrame;
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
