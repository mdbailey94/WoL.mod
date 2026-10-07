using System.Collections;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Feint Swap (Water, the game's frost element), a standard arcana: hold the button and ice
    // crystals gather round the wizard's feet; let go to throw an ice feint (the game's IceDecoy,
    // which enemies go after) out along your aim. It hovers there while you move about; when the
    // hover ends, or as soon as you press the button again, you swap places with it. Both spots
    // burst with a small Frost Nova that freezes enemies, and the feint lingers where you were to
    // keep drawing them.
    //
    // Holding 0.2 s to 1 s scales the throw (a plain dash's length up to MaxDistance) and the
    // hover (none up to 2 s); enhanced, both are full at 0.6 s. Let go before 0.2 s and nothing
    // is thrown and the arcana isn't used up (its cooldown only starts with a throw). Held 2 s,
    // it throws by itself. Dash to cancel the charge.
    public class FeintSwapState : Player.SkillState
    {
        public new static string staticID = "FeintSwap";

        private const float MinHold = 0.2f;     // shorter throws nothing
        private const float FullCharge = 1f;
        private const float MaxHold = 2f;       // throws by itself
        private const float ThrowTime = 0.2f;   // the throwing pose, after letting go

        private float held;
        private float sinceThrow;
        private float nextEffect;
        private bool thrown;
        private bool done;

        public FeintSwapState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            applyStopElementStatus = true;
            // The cooldown starts with the throw (see Throw), not with the press.
            disableStartCooldown = true;
            startCooldownOnEnter = false;
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
            held = 0f;
            sinceThrow = 0f;
            nextEffect = 0f;
            thrown = false;
            done = false;
        }

        public override void ExecuteSkill()
        {
            if (done)
            {
                base.ExecuteSkill();
                return;
            }
            if (!thrown && CancelToDash(false))
                return;
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;

            if (thrown)
            {
                sinceThrow += Time.deltaTime;
                if (sinceThrow >= ThrowTime)
                    Finish();
                return;
            }

            // Charging: face the aim, crystals forming round your feet.
            held += Time.deltaTime;
            try
            {
                parent.SetFacingDirectionBasedOnInput();
            }
            catch
            {
            }
            parent.anim?.PlayDirectional(parent.ChargeAnimStr, -1, 0.5f);
            if (held >= nextEffect)
            {
                nextEffect = held + 0.1f;
                ChargeEffect(parent.transform.position, Mathf.Clamp01(held / FullCharge));
            }

            if (ButtonHeld() && held < MaxHold)
                return;
            if (held < MinHold)
            {
                Finish(); // a tap: nothing thrown, nothing used up
                return;
            }
            Throw();
        }

        private void Finish()
        {
            done = true;
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

        // Ice crystals in a ring round your feet, closing in and growing as the charge builds.
        private void ChargeEffect(Vector2 position, float charge)
        {
            float radius = 1.1f - 0.5f * charge;
            int count = 1 + Mathf.RoundToInt(2 * charge);
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                IceShard.Spawn(position + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.5f) * radius, parent,
                    0.7f + 0.6f * charge);
            }
            if (Random.value < 0.3f)
                Effects.Shimmer(position, 1);
        }

        private void Throw()
        {
            thrown = true;
            try
            {
                StartCooldownTimer();
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"Feint Swap: couldn't start the cooldown: {e.Message}");
            }

            // Hold time 0.2 s -> 0, 1 s -> 1; enhanced, full range in half the charge-up (0.6 s).
            float span = (FullCharge - MinHold) * (IsEmpowered ? 0.5f : 1f);
            float power = Mathf.Clamp01((held - MinHold) / span);
            Vector2 direction = GetInputVector();
            if (direction.sqrMagnitude < 0.01f)
                direction = Entity.GetFacingDirectionVector(parent.facingDirection);
            direction.Normalize();
            Vector2 start = parent.transform.position;
            float shortest = SlingshotDashPlugin.DashLength(parent);
            float distance = Mathf.Lerp(shortest, Mathf.Max(shortest, FrostSlingshotState.MaxDistance), power);
            // Stop short of walls so you never swap into one.
            RaycastHit2D hit = Physics2D.Raycast(start, direction, distance, ChaosCollisions.layerAllWallAndObst);
            if (hit.collider != null)
                distance = Mathf.Max(0f, hit.distance - FrostSlingshotState.WallMargin);

            parent.anim?.PlayDirectional(parent.ForehandAnimStr, -1, 0f);
            Effects.Splash(start, 0.8f, 2);
            Effects.Spark(start + direction * 0.5f, HitSparkType.Small);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(start), null, 24f, -1f, 1.5f, false);

            var feint = new FrostSlingshotState.Feint { thrownAt = Time.time, hoverTime = FrostSlingshotState.MaxHoverTime * power };
            // The swap happens long after this state ends, so it runs on the plugin.
            SlingshotDashPlugin.Run(FrostSlingshotState.Throw(parent, feint, SpawnDecoy(start), start, start + direction * distance,
                SlingshotDashPlugin.FrostFreezeRadius * (1f + 0.5f * power), parent.skillCategory, skillID));
            SlingshotDashPlugin.Run(WatchForSwap(parent, skillSlot, feint));
        }

        // Pressing the button again while the feint is out swaps you with it right away.
        private static IEnumerator WatchForSwap(Player player, int slot, FrostSlingshotState.Feint feint)
        {
            yield return null; // not the frame you let go
            while (!feint.swapped)
            {
                if (player == null)
                    yield break;
                bool pressed = false;
                try
                {
                    pressed = player.inputDevice != null && player.inputDevice.GetButtonDown("Skill" + slot);
                }
                catch
                {
                    yield break;
                }
                if (pressed)
                    feint.swapRequested = true;
                yield return null;
            }
        }

        private GameObject SpawnDecoy(Vector2 position)
        {
            try
            {
                IceDecoy decoy = ChaosInst<IceDecoy>(IceDecoy.Prefab, new Vector2?(position), null, null);
                if (decoy == null)
                    return null;
                decoy.parentEnt = parent;
                decoy.duration = FrostSlingshotState.FlightTime + FrostSlingshotState.MaxHoverTime + FrostSlingshotState.LingerTime;
                return decoy.gameObject;
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"Ice feint unavailable: {e.Message}");
                return null;
            }
        }
    }
}
