using System.Collections;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Feint Swap (Water, the game's frost element), a standard arcana: hold the button and ice
    // crystals gather round the wizard's feet; let go to throw an ice feint (the game's IceDecoy,
    // which enemies go after) out along your aim, hitting enemies along its path (ice bursts,
    // level 2). It hovers there while you move about, and only when you press the button again do
    // you swap places with it and it explodes round you in a Frost Nova that hurts and freezes
    // (level 1); left alone for MaxWait it just melts away. Enhanced, a new feint is left where
    // you swapped from, drawing enemies to it, and explodes the same way 2 seconds later.
    //
    // Holding up to 1 s scales the throw (a bit over a plain dash's length, up to MaxDistance);
    // enhanced, full at 0.5 s. Held 2 s, it throws by itself. The cooldown starts with the
    // throw. Dash to cancel the charge.
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
        private const float MaxWait = 5f;          // how long the feint waits for the swap
        private const float MaxHold = 2f;       // throws by itself
        private const float ThrowTime = 0.2f;   // the throwing pose, after letting go
        // A press this soon after the throw can't be the swap (the game's own Bolt Claymore waits as
        // long before reading the same button for its detonation). Shorter than the flight, so it
        // never delays a real re-press.
        private const float RepressGrace = 0.25f;

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
            {
                SlingshotDashPlugin.Log($"Feint Swap: charge cancelled by a dash after {held:0.00} s, nothing thrown");
                return;
            }
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

            var feint = new FrostSlingshotState.Feint { thrownAt = Time.time, hoverTime = MaxWait };
            // The swap happens long after this state ends, so it runs on the plugin.
            bool empowered = IsEmpowered;
            float freezeRadius = SlingshotDashPlugin.FrostFreezeRadius * (1f + 0.5f * power);
            SlingshotDashPlugin.Log($"Feint Swap: feint thrown after a {held:0.00} s hold (power {power:0.00}, "
                + $"{distance:0.0} units{(empowered ? ", enhanced" : "")}, button {(ButtonHeld() ? "still held" : "released")}); "
                + $"waiting up to {MaxWait:0} s for a re-press");
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

            // Hover with a slight bob until you press again (a moment at least, so the explosion
            // isn't swallowed by the hit immunity of the last burst on the way). Nothing swaps or
            // explodes on its own: left alone for MaxWait, the feint just melts away.
            t = 0f;
            while (t < SettleTime || !feint.swapRequested)
            {
                t += Time.deltaTime;
                if (t >= MaxWait)
                {
                    feint.swapped = true;
                    FrostSlingshotState.Shatter(decoy);
                    SlingshotDashPlugin.Log($"Feint Swap: no re-press within {MaxWait:0} s (timeout), the feint melted away without a swap or explosion");
                    yield break;
                }
                FrostSlingshotState.MoveDecoy(decoy, end + new Vector2(0f, Mathf.Sin(t * 6f) * 0.08f));
                yield return null;
            }

            feint.swapped = true;
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                FrostSlingshotState.Shatter(decoy);
                SlingshotDashPlugin.Log("Feint Swap: wizard gone (cancel), the feint shattered without a swap or explosion");
                yield break;
            }

            // Swap: you to the feint, and it explodes round you.
            SlingshotDashPlugin.Log($"Feint Swap: swap and explosion triggered by a re-press, {Time.time - feint.thrownAt:0.00} s after the throw");
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
            SlingshotDashPlugin.Log($"Feint Swap: the feint left behind (enhanced) exploded {LeftBehindFuse:0} s after the swap");
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
        //
        // The press that cast the skill must not count. The game keeps reporting it through
        // GetButtonDown for a short while after the press (its own Bolt Claymore and Gravity Vortex
        // wait before reading the same button for their detonation), and after a tap the throw comes
        // within a frame or two of that press, so reading GetButtonDown here swapped and exploded the
        // feint straight away. So: the button counts as held from the throw until it's actually seen
        // up, and only its going down again after that (GetButton's rising edge, which no buffering
        // touches) is the re-press, and not within RepressGrace of the throw either. A button still
        // held when the feint throws itself (MaxHold) likewise has to be let go first.
        private static IEnumerator WatchForSwap(Player player, int slot, FrostSlingshotState.Feint feint)
        {
            string button = "Skill" + slot;
            bool wasHeld = true; // the cast press, until it's seen released
            bool seenReleased = false;
            float sinceThrow = 0f;
            while (!feint.swapped)
            {
                yield return null;
                sinceThrow += Time.deltaTime;
                if (player == null)
                    yield break;
                bool held;
                try
                {
                    held = player.inputDevice != null && player.inputDevice.GetButton(button);
                }
                catch
                {
                    yield break;
                }
                bool pressed = held && !wasHeld;
                wasHeld = held;
                if (!held && !seenReleased)
                {
                    seenReleased = true;
                    SlingshotDashPlugin.Log($"Feint Swap: button released {sinceThrow:0.00} s after the throw, a new press now swaps");
                }
                if (!pressed || feint.swapRequested)
                    continue;
                if (sinceThrow < RepressGrace)
                {
                    SlingshotDashPlugin.Log($"Feint Swap: press {sinceThrow:0.00} s after the throw ignored (within the {RepressGrace:0.00} s grace)");
                    continue;
                }
                feint.swapRequested = true;
                SlingshotDashPlugin.Log($"Feint Swap: re-press seen {sinceThrow:0.00} s after the throw, swap requested");
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
                // Long enough for the flight and the longest wait (it's ended at the swap anyway).
                decoy.duration = duration > 0f ? duration : FrostSlingshotState.FlightTime + MaxWait + 0.5f;
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
