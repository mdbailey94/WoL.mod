using Chaos.AnimatorExtensions;
using System.Collections.Generic;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Blazing Kick (Fire), a standard arcana since 0.26.0 (Matt; up to 0.25.6 it was a dash,
    // BlazingSlingshotState, kept hidden so saves that have it still load). Hold the button and
    // flames ring your feet, hotter as it builds (full at 1 s; at 2 s it goes by itself); let go
    // to rush forward wrapped in Blazing Blitz's flame trail, dropping flame bursts that shove
    // enemies out of your path (level 1), and kick into a fiery blast that throws everyone nearby
    // away (level 2). A full charge rushes 2.2x a plain dash's length. Enhanced, levels 3 and 4
    // stand in for them and every hit burns. Dash to cancel the charge; the cooldown starts with
    // the rush.
    //
    // The signature (isUltimate), the Inferno Kick, in four beats, the camera and light always put
    // back (Cinematic):
    // 1. Ignite: the room plunges into firelit dark and the camera closes in while every flame
    //    round your feet roars up, with a heartbeat of shakes.
    // 2. Blitz: you streak at up to three enemies in turn, through each one, each impact a
    //    burning blast (level 4) with a hit-stop and a flash.
    // 3. Rise: you spring up into the air, and time all but stops at the top as the camera
    //    closes in further.
    // 4. Axe kick: you come down in a huge blast (level 5) with a ring of fire bursting out round
    //    it (level 3), a long hit-stop, a white flash, the heaviest shake and near-frozen time,
    //    then the camera and light ease back.
    public class BlazingKickState : Player.SkillState
    {
        public new static string staticID = "BlazingKick";

        // [Blazing Kick] and [Blazing Kick Signature] settings (0.27.0; the defaults are the values
        // these had in code).
        private static float FullCharge => SlingshotDashPlugin.KickFullCharge;
        private static float MaxHold => SlingshotDashPlugin.KickMaxHold;
        private static float RushSpeedBonus => SlingshotDashPlugin.KickRushSpeedBonus;   // at full charge
        private static float RushTimeBonus => SlingshotDashPlugin.KickRushTimeBonus;
        private const float DefaultDashSpeed = 30f, DefaultDashTime = 0.125f;
        private static float TrailInterval => SlingshotDashPlugin.KickTrailInterval;
        private static float TrailHitScale => SlingshotDashPlugin.KickTrailHitSize;
        private static float LaunchHitScale => SlingshotDashPlugin.KickLaunchHitSize;
        private const float FlameInterval = 0.03f;
        private static float BlastScale => SlingshotDashPlugin.KickBlastSize;
        private static float BlastChargeScale => SlingshotDashPlugin.KickBlastChargeSize;
        private const float KickAt = 0.55f;
        private static float RecoverTime => SlingshotDashPlugin.KickRecoverTime;
        // The signature.
        private static float IgniteTime => SlingshotDashPlugin.InfernoIgniteTime;
        private static int BlitzTargets => SlingshotDashPlugin.InfernoBlitzTargets;
        private static float BlitzRange => SlingshotDashPlugin.InfernoBlitzRange;
        private static float BlitzSpeed => SlingshotDashPlugin.InfernoBlitzSpeed;
        private static float BlitzOvershoot => SlingshotDashPlugin.InfernoBlitzOvershoot;
        private static float BlitzPause => SlingshotDashPlugin.InfernoBlitzPause;
        private static float BlitzMaxTime => SlingshotDashPlugin.InfernoBlitzMaxTime;
        private static float ImpactScale => SlingshotDashPlugin.InfernoImpactSize;
        private static float RiseTime => SlingshotDashPlugin.InfernoRiseTime;
        private static float RiseHeight => SlingshotDashPlugin.InfernoRiseHeight;
        private static float HangSlowMoTime => SlingshotDashPlugin.InfernoHangSlowMoTime;
        private static float HangSlowMoScale => SlingshotDashPlugin.InfernoHangSlowMoScale;
        private static float FallTime => SlingshotDashPlugin.InfernoFallTime;
        private static float FinaleScale => SlingshotDashPlugin.InfernoFinaleSize;
        private static float RingRadius => SlingshotDashPlugin.InfernoRingRadius;
        private static float RingBurstScale => SlingshotDashPlugin.InfernoRingBurstSize;
        private static int RingBursts => SlingshotDashPlugin.InfernoRingBursts;
        // The camera closes in (a negative pull-back) and the room darkens.
        private static float IgniteZoom => -SlingshotDashPlugin.InfernoIgniteZoomIn;
        private static float HangZoom => -SlingshotDashPlugin.InfernoHangZoomIn;
        private static float IgniteDim => SlingshotDashPlugin.InfernoIgniteLight;
        private static float HangDim => SlingshotDashPlugin.InfernoHangLight;
        private static int ImpactHitStopFrames => SlingshotDashPlugin.InfernoImpactHitStop;
        private static int FinaleHitStopFrames => SlingshotDashPlugin.InfernoFinaleHitStop;
        private static float FinaleFlash => SlingshotDashPlugin.InfernoFinaleFlash;
        private static float FinaleShake => SlingshotDashPlugin.InfernoFinaleShake;
        private static float FinaleSlowMoTime => SlingshotDashPlugin.InfernoFinaleSlowMoTime;
        private static float FinaleSlowMoScale => SlingshotDashPlugin.InfernoFinaleSlowMoScale;
        private static float EaseBack => SlingshotDashPlugin.InfernoEaseBack;

        private enum Phase { Charging, Rushing, Recovering, Igniting, Blitzing, BlitzPause, Rising, Falling, Done }

        private Phase phase;
        private float held, phaseTime;
        private float charge;
        private Vector2 aim, rushVelocity;
        private float rushTime, nextTrail, nextFlame, nextEffect;
        private bool kicked, signature;
        private int cinematic;
        private readonly List<Entity> blitzed = new List<Entity>();
        private Entity blitzTarget;
        private int blitzCount;
        private Vector2 blitzEnd;
        private Vector3 liftApplied;
        private float nextHeartbeat;
        private int LevelOffset => IsEmpowered ? 2 : 0;

        // The look (the dash version's, BlazingSlingshotState).
        private const int Tongues = 5;
        private const float TongueRadius = 0.65f, TongueSquash = 0.45f;
        private const int TonguesAtTap = 2;
        private const float GameTongueMin = 0.5f, GameTongueMax = 0.75f, DrawnTongueMin = 0.55f, DrawnTongueMax = 1.05f;
        private const float WispInterval = 0.04f, WispSpread = 0.35f, ScorchInterval = 0.14f, SmokeInterval = 0.2f, EmberRise = 2.2f;
        private const int WispVariants = 4, BlastEmbers = 10;
        private static readonly Color FireOrange = new Color32(0xf5, 0x90, 0x00, 0xff);
        private static readonly Color FireYellow = new Color32(0xf9, 0xd2, 0x3c, 0xff);
        private static readonly Color FlashColour = new Color32(0xf6, 0xed, 0xa4, 0xff);
        private FxHandle[] tongues;
        private bool tonguesFromGame, fullCue;
        private float nextWisp, nextScorch, nextSmoke;

        public BlazingKickState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            // Lets the arcana into the signature slot (the spellbook's Signature page); see
            // SignatureUnlock.cs for when that's unlocked.
            hasSignatureVariant = true;
            applyStopElementStatus = true;
            isMovementSkill = true;
            isMeleeSkill = true;
            // The cooldown starts with the rush, not the press.
            disableStartCooldown = true;
            startCooldownOnEnter = false;
            SetAnimTimes(0.05f, 0.1f, 0.05f, 0.4f, 0.5f, 0.6f);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            held = phaseTime = 0f;
            charge = 0f;
            kicked = false;
            nextEffect = 0f;
            liftApplied = Vector3.zero;
            blitzed.Clear();
            blitzTarget = null;
            blitzCount = 0;
            signature = isUltimate;
            aim = inputVector.sqrMagnitude > 0.01f ? inputVector.normalized : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            if (aim == Vector2.zero)
                aim = Vector2.right;
            PixelFx.Setup(parent);
            StartTongues();
            if (signature)
            {
                phase = Phase.Igniting;
                cinematic = Cinematic.Begin();
                nextHeartbeat = 0f;
                SlingshotDashPlugin.Log($"Blazing Kick: signature (Inferno Kick{(IsEmpowered ? ", enhanced" : "")}): ignite");
            }
            else
            {
                phase = Phase.Charging;
                cinematic = 0;
                SlingshotDashPlugin.Log($"Blazing Kick: cast{(IsEmpowered ? " (enhanced)" : "")}");
            }
        }

        public override void OnExit()
        {
            Lift(0f);
            StopTongues();
            if (signature)
                Cinematic.EaseOut(cinematic, 0.5f, phase == Phase.Done ? "cast over" : "cut short");
            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (parent.rigidbody2D == null)
                return;
            parent.rigidbody2D.velocity = phase == Phase.Rushing || phase == Phase.Blitzing ? rushVelocity : Vector2.zero;
        }

        public override void ExecuteSkill()
        {
            float dt = Time.deltaTime;
            phaseTime += dt;
            switch (phase)
            {
                case Phase.Charging: Charging(dt); break;
                case Phase.Rushing: Rushing(dt); break;
                case Phase.Recovering:
                    if (phaseTime >= RecoverTime)
                        Done();
                    break;
                case Phase.Igniting: Igniting(); break;
                case Phase.Blitzing: Blitzing(dt); break;
                case Phase.BlitzPause:
                    if (phaseTime >= BlitzPause)
                        NextBlitz();
                    break;
                case Phase.Rising: Rising(); break;
                case Phase.Falling: Falling(); break;
                default: base.ExecuteSkill(); break;
            }
        }

        private void Done()
        {
            phase = Phase.Done;
            base.ExecuteSkill();
        }

        private void Go(Phase next)
        {
            phase = next;
            phaseTime = 0f;
        }

        // ---- The standard kick ----

        private void Charging(float dt)
        {
            if (CancelToDash(false))
            {
                SlingshotDashPlugin.Log($"Blazing Kick: charge cancelled by a dash after {held:0.00} s");
                return;
            }
            held += dt;
            charge = Mathf.Clamp01(held / FullCharge);
            Vector2 input = GetInputVector(false, false, false);
            if (input.sqrMagnitude > 0.04f)
                aim = input.normalized;
            parent.FaceTarget(parent.transform.position + (Vector3)aim);
            parent.anim?.PlayDirectional(parent.ChargeAnimStr, -1, 0.5f);
            if (held >= nextEffect)
            {
                nextEffect = held + 0.1f;
                ChargeEffect(parent.transform.position, charge);
            }
            GrowTongues(charge);
            if (ButtonHeld() && held < MaxHold)
                return;
            Launch();
        }

        private void Launch()
        {
            StartCooldownNow();
            float speed = DashSpeed() * (1f + RushSpeedBonus * charge);
            rushTime = DashTime() * (1f + RushTimeBonus * charge);
            rushVelocity = aim * speed;
            nextTrail = nextFlame = 0f;
            kicked = false;
            Go(Phase.Rushing);
            Effects.FireBlast(parent.transform.position, 6 + Mathf.RoundToInt(6 * charge));
            Effects.Shake(0.5f + 0.4f * charge);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(parent.transform.position), null, 24f, -1f, 0.9f - 0.2f * charge, false);
            FlameBurst.CreateBurst(parent.transform.position, parent.skillCategory, skillID, 1 + LevelOffset, LaunchHitScale, false);
            LaunchLook(parent.transform.position, charge);
            parent.anim?.PlayDirectional(parent.DashAnimStr, -1, 0f);
            SlingshotDashPlugin.Log($"Blazing Kick: rush at {charge * 100f:0}% charge: {speed * rushTime:0.0} u in {rushTime:0.00} s");
        }

        private void Rushing(float dt)
        {
            Vector2 here = parent.transform.position;
            // The edge of a pit stops the rush (the dash version flew over them; this one runs).
            if (PitAhead(here, rushVelocity * dt * 2f))
                phaseTime = rushTime;
            if (!kicked && phaseTime >= rushTime * KickAt)
            {
                kicked = true;
                parent.anim?.PlayDirectional(parent.KickAnimStr, -1, 0f);
            }
            TrailTick(here, LevelOffset);
            if (phaseTime >= rushTime)
            {
                rushVelocity = Vector2.zero;
                Land(here, charge, 2 + LevelOffset);
                Go(Phase.Recovering);
            }
        }

        private void TrailTick(Vector2 here, int offset)
        {
            nextFlame -= Time.deltaTime;
            if (nextFlame <= 0f)
            {
                nextFlame = FlameInterval;
                EmitBlitzFlames(here, 2 + Mathf.RoundToInt(3 * charge));
            }
            RushLook(here);
            nextTrail -= Time.deltaTime;
            if (nextTrail <= 0f)
            {
                nextTrail = TrailInterval;
                FlameBurst.CreateBurst(here, parent.skillCategory, skillID, 1 + offset, TrailHitScale, false);
                FlameBurst.CreateBurst(here, parent.skillCategory, skillID, 1 + offset, 0.8f + 0.4f * charge, true);
            }
        }

        // The kick lands in a fiery blast that throws everyone nearby away.
        private void Land(Vector2 position, float power, int level)
        {
            float scale = BlastScale + BlastChargeScale * power;
            FlameBurst.CreateBurst(position, parent.skillCategory, skillID, level, scale, true);
            EmitBlitzFlames(position, 14);
            Effects.FireBlast(position, 12 + Mathf.RoundToInt(8 * power));
            Effects.Dust(position, 60, scale);
            Effects.Shake(0.9f + 0.4f * power);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f, 0.7f - 0.1f * power, false);
            LandLook(position, power);
        }

        // ---- The Inferno Kick (signature) ----

        private void Igniting()
        {
            float t = Mathf.Clamp01(phaseTime / IgniteTime);
            float eased = t * t * (3f - 2f * t);
            Cinematic.Set(cinematic, IgniteZoom * eased, Mathf.Lerp(1f, IgniteDim, eased));
            charge = 1f;
            GrowTongues(Mathf.Lerp(0.5f, 1f, eased));
            parent.anim?.PlayDirectional(parent.ChargeAnimStr, -1, 0.5f);
            if (phaseTime >= nextEffect)
            {
                nextEffect = phaseTime + 0.06f;
                ChargeEffect(parent.transform.position, 1f);
                EmitBlitzFlames(parent.transform.position, 6);
            }
            if (phaseTime >= nextHeartbeat)
            {
                nextHeartbeat = phaseTime + IgniteTime / 3f;
                Effects.Shake(0.35f + 0.35f * eased);
                Effects.Game("FlameCircleBurstEmitter", 8 + Mathf.RoundToInt(10 * eased), parent.transform.position, ParticleEffectOverrides.Radius0p75);
                SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(parent.transform.position), null, 24f, -1f, 0.5f + 0.2f * eased, false);
            }
            if (phaseTime >= IgniteTime)
            {
                StartCooldownNow();
                StopTongues();
                LaunchLook(parent.transform.position, 1f);
                NextBlitz();
            }
        }

        // The next streak: at the nearest enemy not yet struck, through it and a little past.
        private void NextBlitz()
        {
            if (blitzCount >= BlitzTargets)
            {
                Rise();
                return;
            }
            Vector2 here = parent.transform.position;
            blitzTarget = NearestFoe(here);
            Vector2 dir = blitzTarget != null ? ((Vector2)blitzTarget.transform.position - here).normalized : aim;
            if (dir == Vector2.zero)
                dir = aim;
            if (blitzTarget == null && blitzCount > 0)
            {
                // Nobody left to strike: straight on to the finale.
                Rise();
                return;
            }
            aim = dir;
            float distance = blitzTarget != null ? Vector2.Distance(here, blitzTarget.transform.position) + BlitzOvershoot : DashSpeed() * DashTime() * 2f;
            blitzEnd = here + dir * distance;
            rushVelocity = dir * DashSpeed() * BlitzSpeed;
            blitzCount++;
            kicked = false;
            nextTrail = nextFlame = 0f;
            parent.FaceTarget(parent.transform.position + (Vector3)dir);
            parent.anim?.PlayDirectional(parent.DashAnimStr, -1, 0f);
            Effects.FireBlast(here, 8);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(here), null, 24f, -1f, 0.9f + 0.1f * blitzCount, false);
            Go(Phase.Blitzing);
        }

        private void Blitzing(float dt)
        {
            Vector2 here = parent.transform.position;
            TrailTick(here, 2);   // the burning trail (level 3)
            if (!kicked && phaseTime >= BlitzMaxTime * 0.4f)
            {
                kicked = true;
                parent.anim?.PlayDirectional(parent.KickAnimStr, -1, 0f);
            }
            bool past = Vector2.Dot(blitzEnd - here, aim) <= 0f;
            if (!past && phaseTime < BlitzMaxTime && !PitAhead(here, rushVelocity * dt * 2f))
                return;
            rushVelocity = Vector2.zero;
            // The impact: a burning blast (level 4) with a hit-stop and a flash.
            Vector2 at = blitzTarget != null && blitzTarget.health != null && blitzTarget.health.CurrentHealthValue > 0
                ? (Vector2)blitzTarget.transform.position : here;
            if (blitzTarget != null)
                blitzed.Add(blitzTarget);
            FlameBurst.CreateBurst(at, parent.skillCategory, skillID, 4, ImpactScale, true);
            Effects.FireBlast(at, 14);
            Effects.Game("FlameCircleBurstEmitter", 16, at, ParticleEffectOverrides.Radius0p75);
            Effects.Visual("hit-stop", () => TimeScaleController.FreezeForFrames(ImpactHitStopFrames, false));
            Cinematic.Flash(cinematic, 0.9f, 0.05f);
            Effects.Shake(0.8f);
            SlingshotDashPlugin.Log($"Blazing Kick: signature blitz {blitzCount} of {BlitzTargets}{(blitzTarget != null ? " into " + blitzTarget.GetType().Name : " (no enemy in reach)")}");
            Go(Phase.BlitzPause);
        }

        private Entity NearestFoe(Vector2 from)
        {
            Entity best = null;
            float bestDist = BlitzRange;
            if (GameController.enemies == null)
                return null;
            foreach (GameObject go in new List<GameObject>(GameController.enemies))
            {
                if (go == null || !go.activeInHierarchy)
                    continue;
                Entity e = go.GetComponent<Entity>();
                if (e == null || blitzed.Contains(e) || e.health == null || e.health.CurrentHealthValue <= 0)
                    continue;
                float d = Vector2.Distance(from, go.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = e;
                }
            }
            return best;
        }

        // Up into the air, and time all but stops at the top.
        private void Rise()
        {
            rushVelocity = Vector2.zero;
            Go(Phase.Rising);
            parent.anim?.PlayDirectional(parent.JumpAnimStr, -1, 0.5f);
            Effects.FireBlast(parent.transform.position, 10);
            Effects.Dust(parent.transform.position, 40, 1f);
            SlingshotDashPlugin.Log("Blazing Kick: signature rise");
        }

        private void Rising()
        {
            float t = Mathf.Clamp01(phaseTime / RiseTime);
            Lift(RiseHeight * (1f - (1f - t) * (1f - t)));
            Cinematic.Set(cinematic, Mathf.Lerp(IgniteZoom, HangZoom, t), Mathf.Lerp(IgniteDim, HangDim, t));
            if (phaseTime >= nextEffect)
            {
                nextEffect = phaseTime + 0.03f;
                EmitBlitzFlames((Vector2)parent.transform.position + Vector2.up * RiseHeight * t, 4);
            }
            if (t >= 1f)
            {
                Effects.Visual("slow motion", () => TimeScaleController.SetTimeScaleDelayOverRealTime(HangSlowMoTime, HangSlowMoScale, true, true));
                parent.anim?.PlayDirectional(parent.KickAnimStr, -1, 0f);
                Go(Phase.Falling);
            }
        }

        private void Falling()
        {
            float t = Mathf.Clamp01(phaseTime / FallTime);
            Lift(RiseHeight * (1f - t * t));
            if (t < 1f)
                return;
            Lift(0f);
            Finale();
            Go(Phase.Recovering);
        }

        // The axe kick: a huge blast with a ring of fire bursting out round it.
        private void Finale()
        {
            Vector2 at = parent.transform.position;
            FlameBurst.CreateBurst(at, parent.skillCategory, skillID, 5, FinaleScale, true);
            for (int i = 0; i < RingBursts; i++)
            {
                float a = i * Mathf.PI * 2f / RingBursts;
                Vector2 spot = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * RingRadius;
                FlameBurst.CreateBurst(spot, parent.skillCategory, skillID, 3, RingBurstScale, true);
                EmitBlitzFlames(spot, 6);
                Effects.Game("BurnMarkMedEmitter", 1, spot, ParticleEffectOverrides.StartLifeTimeTwo);
            }
            LandLook(at, 1f);
            Effects.FireBlast(at, 30);
            Effects.Dust(at, 140, FinaleScale * 0.5f);
            Effects.Visual("hit-stop", () => TimeScaleController.FreezeForFrames(FinaleHitStopFrames, false));
            Cinematic.Flash(cinematic, FinaleFlash, 0.12f);
            Effects.Visual("shake", () => CameraController.ShakeCamera(FinaleShake, true));
            Effects.Visual("slow motion", () => TimeScaleController.SetTimeScaleDelayOverRealTime(FinaleSlowMoTime, FinaleSlowMoScale, true, true));
            Cinematic.EaseOut(cinematic, EaseBack, "axe kick finished");
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(at), null, 24f, -1f, 0.5f, false);
            SlingshotDashPlugin.Log($"Blazing Kick: signature axe kick: blast x{FinaleScale:0.#}, ring of {RingBursts} fire bursts at {RingRadius:0.#}, " +
                $"hit-stop {FinaleHitStopFrames} frames, flash x{FinaleFlash:0.0}, shake {FinaleShake:0.0}, slow motion {FinaleSlowMoTime:0.00} s");
        }

        // Raises the wizard's picture (its own child) off the ground, the body staying put.
        private void Lift(float height)
        {
            try
            {
                SpriteRenderer body = parent.spriteRenderer;
                if (body == null || body.transform == parent.transform)
                    return;
                body.transform.localPosition -= liftApplied;
                liftApplied = new Vector3(0f, Mathf.Round(height * 16f) / 16f, 0f);
                body.transform.localPosition += liftApplied;
            }
            catch
            {
                liftApplied = Vector3.zero;
            }
        }

        // ---- Helpers ----

        private void StartCooldownNow()
        {
            try
            {
                StartCooldownTimer();
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"Blazing Kick: couldn't start the cooldown: {e.Message}");
            }
        }

        private float DashSpeed()
        {
            try
            {
                if (parent.movement != null && parent.movement.dashSpeedStat != null)
                    return parent.movement.dashSpeedStat.ModifiedValue;
            }
            catch
            {
            }
            return DefaultDashSpeed;
        }

        private float DashTime()
        {
            try
            {
                if (parent.movement != null && parent.movement.dashDurationStat != null && parent.movement.dashDurationStat.ModifiedValue > 0f)
                    return parent.movement.dashDurationStat.ModifiedValue;
            }
            catch
            {
            }
            return DefaultDashTime;
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

        private static bool PitAhead(Vector2 here, Vector2 step)
        {
            try
            {
                return ChaosCollisions.layerPit != 0 && Physics2D.OverlapPoint(here + step, ChaosCollisions.layerPit) != null;
            }
            catch
            {
                return false;
            }
        }

        private static void EmitBlitzFlames(Vector3 position, int count)
        {
            try
            {
                FireBurst fire = PoolManager.GetPoolItem<FireBurst>();
                if (fire != null)
                    fire.EmitSingle(new int?(count), new Vector3?(position));
            }
            catch
            {
            }
        }

        // ---- The look (BlazingSlingshotState's) ----

        private void ChargeEffect(Vector2 position, float power)
        {
            EmitBlitzFlames(position, 2 + Mathf.RoundToInt(5 * power));
            for (int i = 0; i < 1 + Mathf.RoundToInt(power); i++)
            {
                Vector2 at = position + new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(-0.2f, 0.2f));
                Effects.Visual("ember", () => FxSprite.Play(FireArt.Ember.Get(), at, new FxOpts
                {
                    lift = Random.Range(0.2f, 0.8f),
                    velocity = new Vector2(Random.Range(-0.4f, 0.4f), EmberRise * Random.Range(0.7f, 1.2f)),
                    drag = 0.8f,
                    layer = FxLayer.Effect
                }));
            }
        }

        private void StartTongues()
        {
            fullCue = false;
            StopTongues();
            Effects.Visual("flame tongues", () =>
            {
                if (tongues == null)
                    tongues = new FxHandle[Tongues];
                FxAnim tongue = FireArt.Tongue.Get();
                tonguesFromGame = tongue != null && tongue.FromGame;
                SpriteRenderer body = parent.spriteRenderer;
                for (int i = 0; i < Tongues; i++)
                {
                    float a = (i + 0.5f) * Mathf.PI * 2f / Tongues;
                    var offset = new Vector2(Mathf.Cos(a) * TongueRadius, Mathf.Sin(a) * TongueRadius * TongueSquash);
                    tongues[i] = FxSprite.Play(tongue, parent.transform.position, new FxOpts
                    {
                        follow = parent.transform,
                        offset = offset,
                        orderFrom = body,
                        orderDelta = offset.y > 0f ? -1 : 1,
                        flipX = i % 2 == 1,
                        speed = 0.85f + 0.3f * PixelArt.Hash(i, 3),
                        scale = i < TonguesAtTap ? TongueScale(0f) : 0.01f
                    });
                }
            });
        }

        private float TongueScale(float power) => tonguesFromGame
            ? Mathf.Lerp(GameTongueMin, GameTongueMax, power)
            : Mathf.Lerp(DrawnTongueMin, DrawnTongueMax, power);

        private void GrowTongues(float power)
        {
            if (tongues == null)
                return;
            int lit = TonguesAtTap + Mathf.RoundToInt((Tongues - TonguesAtTap) * power);
            float scale = TongueScale(power);
            for (int i = 0; i < tongues.Length; i++)
                tongues[i].SetScale(i < lit ? scale * (0.9f + 0.2f * PixelArt.Hash(i, 5)) : 0.01f);
            if (!fullCue && power >= 1f)
            {
                fullCue = true;
                Effects.Game("FlameCircleBurstEmitter", 12, parent.transform.position, ParticleEffectOverrides.Radius0p5);
            }
        }

        private void StopTongues()
        {
            if (tongues == null)
                return;
            for (int i = 0; i < tongues.Length; i++)
            {
                tongues[i].Stop();
                tongues[i] = default(FxHandle);
            }
        }

        private void LaunchLook(Vector2 position, float power)
        {
            StopTongues();
            nextWisp = nextScorch = nextSmoke = 0f;
            Effects.Visual("launch flare", () =>
            {
                FxAnim puff = FireArt.Puff.Get();
                FxSprite.Play(puff, position, new FxOpts
                {
                    lift = 0.5f,
                    layer = FxLayer.Effect,
                    scale = (puff != null && puff.FromGame ? 1f : 0.45f) + 0.2f * power,
                    speed = 1.2f
                });
            });
            Effects.Game("FlameCircleBurstEmitter", 10 + Mathf.RoundToInt(10 * power), position, ParticleEffectOverrides.Radius0p75);
            Effects.Game("BurnMarkMedEmitter", 1, position, ParticleEffectOverrides.StartLifeTimeTwo);
        }

        private void RushLook(Vector2 position)
        {
            if (Time.time >= nextWisp)
            {
                nextWisp = Time.time + WispInterval;
                Vector2 at = position + new Vector2(-aim.y, aim.x) * Random.Range(-WispSpread, WispSpread);
                bool yellow = Random.value < 0.4f;
                Effects.Visual("flame wisps", () => FxSprite.Play(FireArt.Wisp.Get(), at, new FxOpts
                {
                    variant = Random.Range(0, WispVariants),
                    flipX = Random.value < 0.5f,
                    lift = Random.Range(0.1f, 0.6f),
                    velocity = Vector2.up * Random.Range(0.6f, 1.4f) - aim * 0.8f,
                    colour = yellow ? FireYellow : FireOrange,
                    shrinkTo = 0.5f,
                    speed = Random.Range(0.9f, 1.15f)
                }));
                Effects.Visual("ember", () => FxSprite.Play(FireArt.Ember.Get(), at, new FxOpts
                {
                    lift = Random.Range(0.3f, 1f),
                    velocity = -aim * Random.Range(1f, 2.5f) + Vector2.up * EmberRise,
                    drag = 1.5f,
                    layer = FxLayer.Effect
                }));
            }
            if (Time.time >= nextScorch)
            {
                nextScorch = Time.time + ScorchInterval;
                Effects.Game("BurnMarkSmallEmitter", 1, position, ParticleEffectOverrides.StartLifeTimeOne);
            }
            if (Time.time >= nextSmoke)
            {
                nextSmoke = Time.time + SmokeInterval;
                Effects.Game("SmokeEmitter", 2, position + Vector2.up, SmokeEmitter.SmokeSmallOverride);
            }
        }

        private void LandLook(Vector2 position, float power)
        {
            float size = 0.85f + 0.4f * power;
            Effects.Visual("explosion", () =>
            {
                FxAnim blast = FireArt.Blast.Get();
                float s = (blast != null && blast.FromGame ? 0.8f : 1f) * size;
                FxSprite.Play(blast, position, new FxOpts { lift = 0.6f, layer = FxLayer.Effect, scale = s });
            });
            Effects.Visual("blast ring", () =>
            {
                FxAnim ring = FireArt.Ring.Get();
                bool game = ring != null && ring.FromGame;
                FxSprite.Play(ring, position, new FxOpts
                {
                    lift = game ? 0.6f : 0f,
                    layer = game ? FxLayer.Effect : FxLayer.Ground,
                    scale = (game ? 1.6f : 1f) * size,
                    colour = FireYellow
                });
            });
            Effects.Visual("burning fissure", () => FxSprite.Play(FireArt.Fissure.Get(), position,
                new FxOpts { layer = FxLayer.Ground, scale = 1.25f }));
            Effects.Visual("kick flash", () =>
            {
                FxAnim flash = FireArt.Flash.Get();
                FxSprite.Play(flash, position + aim * 0.6f, new FxOpts
                {
                    rotation = flash != null && flash.FromGame ? 0f : Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg,
                    lift = 0.7f,
                    layer = FxLayer.Effect,
                    scale = flash != null && flash.FromGame ? 0.45f + 0.15f * power : 0.8f + 0.3f * power,
                    colour = FlashColour
                });
            });
            for (int i = 0; i < BlastEmbers; i++)
            {
                float a = (i + Random.value * 0.5f) * Mathf.PI * 2f / BlastEmbers;
                var fling = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * Random.Range(3f, 5.5f) * size;
                Effects.Visual("ember", () => FxSprite.Play(FireArt.Ember.Get(), position, new FxOpts
                {
                    lift = 0.5f,
                    velocity = fling + Vector2.up * 1.5f,
                    drag = 3f,
                    speed = 0.7f,
                    layer = FxLayer.Effect
                }));
            }
            Effects.Game("FlameCircleBurstEmitter", 24 + Mathf.RoundToInt(16 * power), position, ParticleEffectOverrides.RadiusTwo);
            Effects.Game("BurnMarkLargeEmitter", 1, position, ParticleEffectOverrides.StartLifeTimeTwo);
            Effects.Game("SmokeEmitter", 8, position + Vector2.up, SmokeEmitter.SmokeMedOverride, float.NaN, 0.1f);
        }
    }
}
