using System.Collections;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Feint Swap (Water, the game's frost element), a standard arcana: hold the button and ice
    // crystals gather round the wizard's feet; let go to throw an ice feint (the game's IceDecoy,
    // which enemies go after) out along your aim, hitting enemies along its path (ice bursts,
    // level 2). It hovers there while you move about; when the hover ends, or as soon as you
    // press the button again, you swap places with it and it explodes round you in a Frost Nova
    // that hurts and freezes (level 1). Enhanced, a new feint is left where you swapped from,
    // drawing enemies to it, and explodes the same way 2 seconds later.
    //
    // Holding up to 1 s scales the throw (a bit over a plain dash's length, up to MaxDistance)
    // and the hover (none for a tap, up to 2 s); enhanced, both are full at 0.5 s. Held 2 s, it
    // throws by itself. The cooldown starts with the throw. Dash to cancel the charge.
    public class FeintSwapState : Player.SkillState
    {
        public new static string staticID = "FeintSwap";

        private const float FullCharge = 1f;
        private const float MinRangeScale = 1.15f; // the shortest throw, times a plain dash's length
        private const float MaxDistance = 10.5f;   // at full charge
        private const float LeftBehindFuse = 2f;   // enhanced: the feint left in your place explodes after this
        private const float PathHitInterval = 0.05f;
        private const float PathHitScale = 1.1f;
        private const float SettleTime = 0.12f;
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

            // Hold time 0 -> 0, 1 s -> 1; enhanced, full range in half the charge-up (0.5 s).
            float span = FullCharge * (IsEmpowered ? 0.5f : 1f);
            float power = Mathf.Clamp01(held / span);
            Vector2 direction = GetInputVector();
            if (direction.sqrMagnitude < 0.01f)
                direction = Entity.GetFacingDirectionVector(parent.facingDirection);
            direction.Normalize();
            Vector2 start = parent.transform.position;
            float shortest = SlingshotDashPlugin.DashLength(parent) * MinRangeScale;
            float distance = Mathf.Lerp(shortest, Mathf.Max(shortest, MaxDistance), power);
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
            bool empowered = IsEmpowered;
            float freezeRadius = SlingshotDashPlugin.FrostFreezeRadius * (1f + 0.5f * power);
            SlingshotDashPlugin.Run(Fly(this, feint, SpawnDecoy(start), start, start + direction * distance, freezeRadius, empowered));
            SlingshotDashPlugin.Run(WatchForSwap(parent, skillSlot, feint));
        }

        // The feint's flight (hitting along the way), hover, the swap and the explosions. Runs on
        // the plugin, since this state ends long before the swap.
        private static IEnumerator Fly(FeintSwapState state, FrostSlingshotState.Feint feint, GameObject decoy,
            Vector2 start, Vector2 end, float freezeRadius, bool empowered)
        {
            Player player = state.parent;
            string category = player.skillCategory;
            string id = state.skillID;

            // Fly out, bursting with ice along the way.
            float t = 0f;
            float nextHit = 0f;
            while (t < FrostSlingshotState.FlightTime)
            {
                t += Time.deltaTime;
                Vector2 position = Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / FrostSlingshotState.FlightTime));
                FrostSlingshotState.MoveDecoy(decoy, position);
                if (t >= nextHit)
                {
                    nextHit = t + PathHitInterval;
                    try
                    {
                        IceBurst.CreateBurst(position, category, id, 2, PathHitScale);
                    }
                    catch
                    {
                    }
                    Effects.Shimmer(position, 2);
                }
                yield return null;
            }

            // Hover with a slight bob until the time's up or you press again (a moment at least,
            // so the explosion isn't swallowed by the hit immunity of the last burst on the way).
            t = 0f;
            while (t < SettleTime || (t < feint.hoverTime && !feint.swapRequested))
            {
                t += Time.deltaTime;
                FrostSlingshotState.MoveDecoy(decoy, end + new Vector2(0f, Mathf.Sin(t * 6f) * 0.08f));
                yield return null;
            }

            feint.swapped = true;
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                FrostSlingshotState.Shatter(decoy);
                yield break;
            }

            // Swap: you to the feint, and it explodes round you.
            Vector2 here = player.transform.position;
            player.transform.position = end;
            if (player.rigidbody2D != null)
            {
                player.rigidbody2D.position = end;
                player.rigidbody2D.velocity = Vector2.zero;
            }
            FrostSlingshotState.Shatter(decoy);
            Explode(end, freezeRadius, category, id);
            // Flair only: a frosty streak along the swap.
            for (int i = 1; i < 6; i++)
                Effects.Shimmer(Vector2.Lerp(here, end, i / 6f), 2);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(end), null, 24f, -1f, 1.7f, false);

            if (!empowered)
                yield break;

            // Enhanced: a new feint where you swapped from, drawing enemies in, then exploding.
            GameObject left = null;
            try
            {
                left = state.SpawnDecoy(here, LeftBehindFuse + 0.5f);
            }
            catch
            {
            }
            Effects.Splash(here, 0.8f, 2);
            float fuse = 0f;
            while (fuse < LeftBehindFuse)
            {
                fuse += Time.deltaTime;
                // A quickening glint as it's about to go.
                if (Random.value < 0.05f + 0.25f * (fuse / LeftBehindFuse))
                    Effects.Shimmer(here + Random.insideUnitCircle * 0.4f, 1);
                yield return null;
            }
            FrostSlingshotState.Shatter(left);
            Explode(here, freezeRadius, category, id);
        }

        // A feint exploding: a Frost Nova that hurts and freezes, with a splash, glints and a shake.
        private static void Explode(Vector2 position, float radius, string category, string id)
        {
            try
            {
                FrostNova.CreateFrostNova(position, radius, category, id, 1);
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"Feint Swap: couldn't make the explosion: {e.Message}");
            }
            PoolManager.GetPoolItem<DustEmitter>().EmitCircle(40, 1.2f, -6f, -1f, new Vector3?(position), null);
            Effects.Splash(position, radius, 3);
            Effects.Shimmer(position, 6);
            Effects.Spark(position, HitSparkType.Medium);
            Effects.Shake(0.7f);
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

        private GameObject SpawnDecoy(Vector2 position, float duration = -1f)
        {
            try
            {
                IceDecoy decoy = ChaosInst<IceDecoy>(IceDecoy.Prefab, new Vector2?(position), null, null);
                if (decoy == null)
                    return null;
                decoy.parentEnt = parent;
                // Long enough for the flight and the longest hover (it's ended at the swap anyway).
                decoy.duration = duration > 0f ? duration : FrostSlingshotState.FlightTime + FrostSlingshotState.MaxHoverTime + 0.5f;
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
