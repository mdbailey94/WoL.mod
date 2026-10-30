using System.Collections;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Feint Swap (Water, the game's frost element), a standard arcana: hold the button and ice
    // crystals gather round the wizard's feet; let go to throw an ice feint (the game's IceDecoy,
    // an icy copy of you that enemies go after) out along your aim, hitting enemies along its path
    // (ice bursts, level 2). It then stands there for up to HoverTime: press the button again (or
    // cast again once the cooldown is up) to swap places with it sooner, or it swaps by itself when
    // the time is up; either way it explodes round you in a Frost Nova that hurts and freezes
    // (level 1). If enemies break it first, it just shatters and there is no swap. Enhanced, a new
    // feint is left where you swapped from, drawing enemies to it, and explodes the same way
    // 2 seconds later.
    //
    // Holding up to 1 s scales the throw (a bit over a plain dash's length, up to MaxDistance);
    // enhanced, full at 0.5 s. Held 2 s, it throws by itself. The cooldown starts with the
    // throw. Dash to cancel the charge.
    //
    // Cast from the signature slot with the gauge full (isUltimate), the release throws six
    // feints in a fan instead, and when they've stood their time (or on a re-press / recast) you
    // swap onto each in turn, 0.2 s apart, every one going off in a spinning ring of icicles: see
    // FeintSwapSignature.cs. Enhanced and signature are separate flags and stack (the game
    // empowers whatever sits in the signature slot, so a signature cast is usually both).
    public partial class FeintSwapState : Player.SkillState
    {
        public new static string staticID = "FeintSwap";

        // [Feint Swap] settings (0.27.0; the defaults are the values these had in code).
        private static float FullCharge => SlingshotDashPlugin.FeintFullCharge;
        private static float EnhancedChargeShare => SlingshotDashPlugin.FeintEnhancedChargeShare; // enhanced: full range this much sooner
        private static float MinRangeScale => SlingshotDashPlugin.FeintShortestThrow; // the shortest throw, times a plain dash's length
        private static float MaxDistance => SlingshotDashPlugin.FeintMaxDistance;     // at full charge
        private static float FlightTime => SlingshotDashPlugin.FeintFlightTime;       // (0.26.3 and before: FrostSlingshotState's)
        private static float LeftBehindFuse => SlingshotDashPlugin.FeintLeftBehindFuse; // enhanced: the feint left in your place explodes after this
        private static float PathHitInterval => SlingshotDashPlugin.FeintPathHitInterval;
        private static float PathHitScale => SlingshotDashPlugin.FeintPathHitSize;
        // The explosion grows by this share of FrostFreezeRadius at a full charge.
        private static float ChargeRadiusBonus => SlingshotDashPlugin.FeintChargeRadiusBonus;
        private const float SettleTime = 0.12f;
        private static float HoverTime => SlingshotDashPlugin.FeintHoverTime; // the feint stands this long after landing, then swaps by itself
        // The game ends a decoy by itself when its duration runs out (with an ice burst), so the
        // feint gets one it will never reach: it stays until the swap or until enemies break it.
        private const float DecoyLifetime = 3600f;
        private static float MaxHold => SlingshotDashPlugin.FeintMaxHold;       // throws by itself
        private static float ThrowTime => SlingshotDashPlugin.FeintThrowTime;   // the throwing pose, after letting go
        // A press this soon after the throw can't be the swap (the game's own Bolt Claymore waits as
        // long before reading the same button for its detonation). Shorter than the flight, so it
        // never delays a real re-press.
        private const float RepressGrace = 0.25f;
        // The game's frost palette for an ice copy of the wizard (Ice Dash's decoy uses the same).
        private const float IcePalette = 19f;

        private float held;
        private float sinceThrow;
        private float nextEffect;
        private bool thrown;
        private bool done;
        private bool signature;   // this cast is the signature (isUltimate at cast time)

        // The feint that is out right now (per wizard would be cleaner, but each wizard's state
        // object is its own instance of this class, so one per state).
        private FrostSlingshotState.Feint outFeint;
        // Enhanced (0.26.2, Matt): the feint left where you swapped from, while its fuse burns. A
        // third press (or a recast) swaps you back onto it.
        private FrostSlingshotState.Feint leftFeint;
        private const float SwapBackGrace = 0.15f;   // a press this soon after the swap isn't the third

        public FeintSwapState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            // Lets the arcana into the signature slot: the spellbook's Signature page and the
            // saved loadout both require it (SpellBookUI.LoadEleSkillDict, Player.InitSkills).
            hasSignatureVariant = true;
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
            // The game sets isUltimate for a signature cast (the signature slot's arcana with the
            // gauge full) and clears it once the state exits (Player.HandleOverdrive), so it's
            // read once here.
            signature = isUltimate;
            // Enhanced: cast again while the feint left behind is burning: that's the swap back.
            if (!signature && leftFeint != null && !leftFeint.swapped)
            {
                leftFeint.swapRequested = true;
                done = true;
                SlingshotDashPlugin.Log("Feint Swap: cast again with the feint left behind out, swap back requested");
                return;
            }
            // Cast again while a feint is out (the cooldown has run its course): that's the swap,
            // not a second feint.
            if (outFeint != null && !outFeint.swapped)
            {
                outFeint.swapRequested = true;
                if (!signature)
                {
                    done = true;
                    SlingshotDashPlugin.Log("Feint Swap: cast again with the feint out, swap requested");
                    return;
                }
                // With the gauge full, any press of the button is the signature cast (the game
                // spends the gauge on entering), so don't waste it on the swap: that feint swaps
                // now and the signature charges as usual.
                SlingshotDashPlugin.Log("Feint Swap: signature cast with a feint out: that feint swaps now, and the signature charges");
            }
            SlingshotDashPlugin.Log($"Feint Swap: cast ({Variant()})");
        }

        // Which version of the arcana this cast is, for the log: normal, enhanced (the relic-based
        // empowerment, IsEmpowered), signature (the gauge cast, isUltimate) or both at once.
        private string Variant()
        {
            if (IsEmpowered && signature)
                return "both: enhanced + signature";
            if (signature)
                return "signature";
            return IsEmpowered ? "enhanced" : "normal";
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
            float span = FullCharge * (IsEmpowered ? EnhancedChargeShare : 1f);
            float power = Mathf.Clamp01(held / span);
            Vector2 direction = GetInputVector();
            if (direction.sqrMagnitude < 0.01f)
                direction = Entity.GetFacingDirectionVector(parent.facingDirection);
            direction.Normalize();
            Vector2 start = parent.transform.position;
            float shortest = SlingshotDashPlugin.DashLength(parent) * MinRangeScale;
            float distance = Mathf.Lerp(shortest, Mathf.Max(shortest, MaxDistance), power);
            if (signature)
            {
                // The signature: six feints in a fan (FeintSwapSignature.cs).
                ThrowFan(start, direction, distance, power);
                return;
            }
            // Stop short of walls so you never swap into one.
            RaycastHit2D hit = Physics2D.Raycast(start, direction, distance, ChaosCollisions.layerAllWallAndObst);
            if (hit.collider != null)
                distance = Mathf.Max(0f, hit.distance - FrostSlingshotState.WallMargin);

            parent.anim?.PlayDirectional(parent.ForehandAnimStr, -1, 0f);
            Effects.Splash(start, 0.8f, 2);
            Effects.Spark(start + direction * 0.5f, HitSparkType.Small);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(start), null, 24f, -1f, 1.5f, false);

            var feint = new FrostSlingshotState.Feint { thrownAt = Time.time };
            outFeint = feint;
            // The swap happens long after this state ends, so it runs on the plugin.
            bool empowered = IsEmpowered;
            float freezeRadius = SlingshotDashPlugin.FrostFreezeRadius * (1f + ChargeRadiusBonus * power);
            SlingshotDashPlugin.Log($"Feint Swap: feint thrown after a {held:0.00} s hold (power {power:0.00}, "
                + $"{distance:0.0} units{(empowered ? ", enhanced" : "")}, button {(ButtonHeld() ? "still held" : "released")}); "
                + $"it swaps on a re-press, a recast, or by itself after {HoverTime:0} s, unless enemies break it first");
            SlingshotDashPlugin.Run(Fly(this, feint, SpawnDecoy(start), start, start + direction * distance, freezeRadius, empowered));
            SlingshotDashPlugin.Run(WatchForSwap(parent, skillSlot, feint));
        }

        // The feint's flight (hitting along the way), its wait, the swap and the explosions. Runs
        // on the plugin, since this state ends long before the swap.
        private static IEnumerator Fly(FeintSwapState state, FrostSlingshotState.Feint feint, GameObject decoy,
            Vector2 start, Vector2 end, float freezeRadius, bool empowered)
        {
            Player player = state.parent;
            string category = player.skillCategory;
            string id = state.skillID;

            // Fly out, bursting with ice along the way (the feint can't be hurt until it lands).
            float t = 0f;
            float nextHit = 0f;
            float flightTime = FlightTime;
            while (t < flightTime)
            {
                t += Time.deltaTime;
                Vector2 position = Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / flightTime));
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
            SetVulnerable(decoy, true);

            // Stand there with a slight bob until you press again or the time is up (a moment at
            // least, so the explosion isn't swallowed by the hit immunity of the last burst on the
            // way). If enemies break the feint first, that's the end of it.
            t = 0f;
            while (t < SettleTime || (!feint.swapRequested && t < HoverTime))
            {
                t += Time.deltaTime;
                if (Broken(decoy))
                {
                    feint.swapped = true;
                    state.ClearFeint(feint);
                    SlingshotDashPlugin.Log($"Feint Swap: the feint was broken {Time.time - feint.thrownAt:0.00} s after the throw, no swap or explosion");
                    yield break;
                }
                FrostSlingshotState.MoveDecoy(decoy, end + new Vector2(0f, Mathf.Sin(t * 6f) * 0.08f));
                yield return null;
            }

            bool forced = !feint.swapRequested;
            feint.swapped = true;
            state.ClearFeint(feint);
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                FrostSlingshotState.Shatter(decoy);
                SlingshotDashPlugin.Log("Feint Swap: wizard gone (cancel), the feint shattered without a swap or explosion");
                yield break;
            }

            // Swap: you to the feint, and it explodes round you.
            SlingshotDashPlugin.Log($"Feint Swap: swap and explosion ({(forced ? "time's up" : "on your press")}), "
                + $"{Time.time - feint.thrownAt:0.00} s after the throw");
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

            // Enhanced: a new feint where you swapped from, drawing enemies in, then exploding. A
            // third press (or a recast) before its fuse runs out swaps you back onto it, and it
            // goes off round you as you land (0.26.2).
            GameObject left = null;
            try
            {
                left = state.SpawnDecoy(here);
                SetVulnerable(left, true);
            }
            catch
            {
            }
            var back = new FrostSlingshotState.Feint { thrownAt = Time.time };
            state.leftFeint = back;
            string button = "Skill" + state.skillSlot;
            bool wasHeld = true;   // the press that made the swap, until it's seen released
            // No water splash or glints round it while it waits (the owner found them odd): it
            // just stands there until its fuse runs out.
            float fuse = 0f;
            while (fuse < LeftBehindFuse && !Broken(left) && !back.swapRequested)
            {
                fuse += Time.deltaTime;
                bool held = false;
                try
                {
                    held = player != null && player.inputDevice != null && player.inputDevice.GetButton(button);
                }
                catch
                {
                }
                if (held && !wasHeld && fuse >= SwapBackGrace)
                    back.swapRequested = true;
                wasHeld = held;
                yield return null;
            }
            back.swapped = true;
            if (state.leftFeint == back)
                state.leftFeint = null;
            if (Broken(left))
            {
                SlingshotDashPlugin.Log("Feint Swap: the feint left behind (enhanced) was broken before its fuse ran out");
                yield break;
            }
            if (back.swapRequested && player != null && player.gameObject.activeInHierarchy)
            {
                // The swap back: you to the feint left behind, and it goes off round you.
                Vector2 from = player.transform.position;
                player.transform.position = here;
                if (player.rigidbody2D != null)
                {
                    player.rigidbody2D.position = here;
                    player.rigidbody2D.velocity = Vector2.zero;
                }
                FrostSlingshotState.Shatter(left);
                Explode(here, freezeRadius, category, id);
                for (int i = 1; i < 6; i++)
                    Effects.Shimmer(Vector2.Lerp(from, here, i / 6f), 2);
                SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(here), null, 24f, -1f, 1.8f, false);
                SlingshotDashPlugin.Log($"Feint Swap: swapped back onto the feint left behind (enhanced) {fuse:0.00} s after the swap; it exploded round you");
                yield break;
            }
            FrostSlingshotState.Shatter(left);
            Explode(here, freezeRadius, category, id);
            SlingshotDashPlugin.Log($"Feint Swap: the feint left behind (enhanced) exploded {LeftBehindFuse:0} s after the swap");
        }

        private void ClearFeint(FrostSlingshotState.Feint feint)
        {
            if (outFeint == feint)
                outFeint = null;
        }

        // Gone, or in the game's dead state (enemies broke it, or it fell into a pit).
        private static bool Broken(GameObject decoy)
        {
            if (decoy == null || !decoy.activeInHierarchy)
                return true;
            try
            {
                IceDecoy ice = decoy.GetComponent<IceDecoy>();
                if (ice == null)
                    return true;
                if (ice.fsm != null && ice.fsm.currentStateName == "Dead")
                    return true;
                return ice.health != null && ice.health.CurrentHealthValue <= 0;
            }
            catch
            {
                return false;
            }
        }

        private static void SetVulnerable(GameObject decoy, bool vulnerable)
        {
            try
            {
                IceDecoy ice = decoy != null ? decoy.GetComponent<IceDecoy>() : null;
                if (ice != null && ice.health != null)
                    ice.health.invulnerable = !vulnerable;
            }
            catch
            {
            }
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

        // The feint: the game's ice decoy set up the way its own Ice Dash sets one up (an icy copy
        // of the wizard, on the allies' side so enemies go after it and your own attacks pass it
        // by), kept out of harm's way while it flies. Its own attack (what it does if it's broken)
        // is at the given level, and marked as a signature cast's when ultimate.
        private GameObject SpawnDecoy(Vector2 position, int level = 1, bool ultimate = false)
        {
            try
            {
                IceDecoy decoy = ChaosInst<IceDecoy>(IceDecoy.Prefab, new Vector2?(position), null, null);
                if (decoy == null)
                    return null;
                decoy.gameObject.name = skillID + parent.skillCategory;
                decoy.parentEnt = parent;
                decoy.parentGroup = GameController.allies;
                decoy.attack.SetAttackInfo(parent.skillCategory, skillID, level, ultimate);
                decoy.duration = DecoyLifetime;
                decoy.facingDirection = parent.facingDirection;
                decoy.isFacingRight = parent.isFacingRight;
                decoy.spriteRenderer.sprite = parent.spriteRenderer.sprite;
                decoy.spriteRenderer.material = new Material(parent.spriteRenderer.material);
                OwnedMaterial.Attach(decoy.gameObject, decoy.spriteRenderer.material);
                decoy.spriteRenderer.material.SetFloat("_PaletteIndex", IcePalette);
                decoy.spriteRenderer.material.SetColor("_OutlineColor", Globals.transparentColor);
                decoy.spriteRenderer.material.SetColor("_FlashColor", Color.white);
                decoy.spriteRenderer.material.SetFloat("_FlashAmount", 0f);
                decoy.shadowTrans.localPosition = parent.shadowSprite.transform.localPosition;
                decoy.shadowTrans.localScale = parent.shadowSprite.transform.localScale;
                // Not a target for your own arcana (the bursts along its flight hit whatever is
                // called EnemyHurtBox, which was shattering it as it landed).
                decoy.hurtBoxTransform.name = "AllyHurtBox";
                decoy.floorContact.name = "AllyFloorContact";
                decoy.floorContact.layer = LayerMask.NameToLayer("AllyFloorContact");
                if (decoy.health != null)
                    decoy.health.invulnerable = true;
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
