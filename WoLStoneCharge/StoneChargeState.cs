using System.Collections.Generic;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLStoneCharge
{
    // Stone Charge (Earth, standard arcana): a shield crash you have to run up to.
    //
    // Cast: a quick slam raises a stone shield (Aegis Charge's) a unit in front of you, shoving
    // whatever's there (level 1). Then, for up to ChargeTime, you're free to run about behind it
    // (the game's run speed, working up to a sprint after SprintDelay), and every moment on the
    // move stores force in the shield in proportion to your run speed against its base (sprint,
    // speed relics, the cloak and speed buffs count, the speeds a dash forces for a moment don't):
    // standing still stores nothing. The shield turns to face the way you run, hits from the
    // front do less (FrontArmour) and you aren't staggered (Brace), but you still take the damage,
    // and you can't cast anything else. A dash keeps the shield and the force (0.3.0): it's the
    // game's own dash (BaseDashState's speed and duration stats, so dash relics count, its look
    // and sound, airborne over pits, then the 0.25 s slide) played inside this state, turning the
    // shield with it; it stores no force, but dashing the shield into an enemy crashes at once.
    //
    // Press the button again, run the shield into an enemy, or let the time run out to unload,
    // with whatever force is stored by then: a short rush forward
    // (RushDistance) bashing with the shield (level 2) and a crash where it ends (level 3), both
    // shoving along your way. Their damage and knockback scale with the stored force, 1x at none up
    // to FullMultiplier at full, and the crash stuns longer from StunEnergy up. An instant crash
    // is weak; a crash after a long sprint is not.
    //
    // Signature (0.5.0; isUltimate, the game's flag for a signature cast): a phalanx (0.5.1,
    // Matt). The shield goes up at full force and a line of shields forms out to either side of
    // it, pair by pair from the middle, PhalanxPairs a side, each pair landing with a shake: a
    // wall of stone about ten units wide that moves and turns with you. Running it into enemies
    // doesn't crash: every shield grabs what it touches (Carrier), carried along in front of the
    // line, up to 8, until the crash. Then every shield crashes at once (level 4, full force),
    // with a hit-stop, a flash, a heavy shake and slowed time, and a shockwave rolls on ahead of
    // the whole line (level 5) with rock spikes. The camera pulls back to take the line in and
    // the room darkens while you charge (Cinematic, always put back).
    public class StoneChargeState : Player.SkillState
    {
        public new static string staticID = "StoneCharge";
        // The signature.
        private const int SigCrashLevel = 4, AftershockLevel = 5;
        private const int PhalanxPairs = 3;           // shields either side of the middle one: 7 in all
        private const float PhalanxSpacing = 1.4f;    // between shields in the line
        private const float PhalanxStep = 0.07f;      // seconds between pairs forming
        private const float PhalanxCrashRadius = 1.9f; // each shield's crash
        private const float ShockAhead = 2.2f, AftershockRadius = 1.8f, AftershockAt = 0.3f, SigRecoverTime = 0.55f;
        private const float CarryKeep = 2.6f;         // a carried enemy further than this from the shield is let go
        private const float SigZoom = 0.25f, SigDim = 0.65f;
        private readonly List<StoneShield> phalanx = new List<StoneShield>();
        private readonly List<Vector2> crashSpots = new List<Vector2>();
        private int pairsRaised;
        private const int SigHitStopFrames = 6;
        private const float SigFlash = 1.6f, SigShake = 2.6f, SigSlowMoTime = 0.5f, SigSlowMoScale = 0.05f, SigEaseBack = 1f;
        private bool signature, aftershockDone;
        private int cinematic;
        private Vector2 crashAt;
        private float crashRadius;
        private readonly Carrier carrier = new Carrier();

        // Stat levels of the three hits (see the plugin's skillStats).
        private const int RaiseLevel = 1;
        private const int BashLevel = 2;
        private const int CrashLevel = 3;

        // Where the hits land, ahead of the wizard along the rush.
        private const float BashOffset = 1f;    // the shield's spot
        private const float CrashOffset = 1f;
        // Seconds on the ground after the crash before you can act.
        private const float RecoverTime = 0.2f;
        // The game's run speed-up (+40%), given back after SprintDelay on the move.
        private const float SprintBonus = 0.4f;
        private const string SprintModID = "StoneCharge_Sprint";
        private const string ArmourModID = "StoneCharge_Armour";
        // A hit counts as "from the front" within 60 degrees of the way you're facing.
        private const float FrontCos = 0.5f;
        // Energy is logged (and the shield shows it) at these steps.
        private const float MilestoneStep = 25f;
        // Energy look: rock debris round the wizard every so often, floor spikes behind them when
        // moving at half force or more (the Charge arcana's), a rock aura from three quarters.
        private const float EffectInterval = 0.12f;
        private const float SpikeInterval = 0.125f;
        private const float InputDeadZone = 0.0016f;
        // Running the shield into an enemy crashes at once: anything this close to the shield's
        // spot while you're on the move counts.
        private const float ContactRadius = 0.9f;
        // The dash behind the shield: the game's dash and slide, copied from BaseDashState and
        // SlideState (Player.cs) so the shield needn't be dropped for the real dash state.
        // BaseDashState.dashDuration when the dash arcana isn't a BaseDashState (the stat's mods,
        // dash relics, still apply on top, as they do to the real dash).
        private const float DefaultDashDuration = 0.125f;
        // SlideState: 0.25 s gliding on along the dash, from half its slideSpeed up to it, before
        // you can dash again; this is all the spacing the game puts between dashes (the dash
        // arcana's cooldown only gates its attack, which this dash doesn't make).
        private const float SlideTime = 0.25f;
        private const float SlideSpeed = 5f;
        // SlideState.maxIgnoreCount: with the skip-the-slide relic, dashes chain this many times.
        private const int MaxDashChain = 2;
        private const string DashModID = "StoneCharge_Dash";
        // The drawn look (StoneArt, ShieldRunes), all cosmetic:
        private const float WakeInterval = 0.09f;      // seconds between puffs of dust thrown back while running
        private const float CrackInterval = 0.2f;      // seconds between drawn floor cracks behind you from half force
        private const float CrackFps = 24f;
        private const float CrackLife = 0.9f;          // the crack forms, holds, then steps out
        private const float EmberInterval = 0.08f;    // seconds between embers rising off you at full force
        private const float EmberFps = 12f;
        private const float GameCrackFps = 12f;        // the game's floor-crack frames, a split, a web (and on to the hole for the crater)
        private const float GameCrackSize = 0.7f;      // the game's crack behind you, a bit smaller than its own (about 1.3 units)
        private const float DustRingRadius = 0.5f;     // the game's dust ring at each force step: this radius, DustRingPerStep more each step
        private const float DustRingPerStep = 0.15f;
        private const float CraterFps = 18f;
        private const float CraterLife = 1.1f;
        private const float CraterWidth = 3f;          // the crater sheet's width in units (48 px)
        private const float CraterFill = 0.8f;         // drawn crater width against the crash's hit diameter
        private const int MinShards = 4;               // rock shards flung by the crash, MinShards + ShardsPerForce * force
        private const int ShardsPerForce = 6;
        private const float ShardFps = 12f;            // drawn tumble frames a second, or quarter turns of the game's stones
        private const int MinSpikes = 5;               // the game's rock spikes round the crash, MinSpikes + SpikesPerForce * force
        private const int SpikesPerForce = 3;
        private const float SpikeReach = 0.8f;         // of the crash radius, where they rise
        private const float FloorSquashY = 0.6f;       // the ring's depth in the game's 3/4 view
        private const float SpikeStagger = 0.035f;     // seconds between spikes round the ring
        private const float SpikeFps = 16f;            // 8 frames: half a second up and crumbled

        private enum Phase { Raising, Charging, Rushing, Recovering, Done }
        private enum DashPhase { None, Dashing, Sliding }

        private Phase phase;
        private float time;             // seconds since the shield went up
        private float energy;           // 0-100
        private float moveTime;         // seconds on the move without stopping
        private float lastSpeed = 1f;   // the latest speed read (x base run speed)
        private float peakSpeed;
        private int milestones;
        private bool wasMoving;
        private bool sprinting;
        private float nextEffect, nextSpike;
        private Vector2 aim;            // where the shield points and the rush goes
        private StoneShield shield;
        private bool braced, armourOn, listening, floorToggled;
        private int frontHits, otherHits;
        private bool unloaded;
        private bool loggedHits;
        // The rush.
        private Vector2 rushDir;
        private float rushSpeed, rushTime, recoverTime;
        private bool bashedMid;
        private float force;            // energy / 100 at the unload
        // What the hits caught.
        private int bashHits, crashHits;
        private int crashDamage;
        private readonly List<string> hitNames = new List<string>();
        // The dash behind the shield.
        private DashPhase dashPhase;
        private Vector2 dashDir;        // the game's dashVector (raw input, as the dash takes it)
        private float slideTime;
        private int dashes, dashChain;
        private Vector2 lastHere;       // last frame's position: a fast dash is checked in between too
        private bool replayAnim;        // the run/idle animation is due again after a slide
        private bool dashDurationOn, dashEvadeOn, dashAirborne;
        private readonly NumVarStatMod dashDurationMod = new NumVarStatMod(DashModID, DefaultDashDuration, 10, VarStatModType.OverrideWithMods);
        private readonly NumVarStatMod dashEvadeMod = new NumVarStatMod(DashModID + "Evade", 1f, 10, VarStatModType.Override);
        private readonly BoolVarStatMod dashAirborneMod = new BoolVarStatMod(DashModID, newModValue: true);
        // The drawn look.
        private ShieldRunes runes;
        private StoneOrbit orbit;
        private float nextWake, nextCrack, nextEmber;
        private int drawnCracks, drawnEmbers, drawnShards, drawnSpikes, flung;
        private static readonly HashSet<string> failed = new HashSet<string>();

        public override string OnEnterAnimStr => parent.GSlamAnimStr;

        public StoneChargeState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            isMovementSkill = true;
            isMeleeSkill = true;
            applyStopElementStatus = true;
            // The cooldown counts from the end (the crash or a cancel), like the Charge arcana's.
            startCooldownOnEnter = false;
            // Lets the arcana into the signature slot (the spellbook's Signature page); see
            // SignatureUnlock.cs for when that's unlocked.
            hasSignatureVariant = true;
            SetAnimTimes(
                0.1f,  // start
                0.15f, // hold
                0.3f,  // execute: the slam lands and the shield rises
                0.5f,  // cancel
                0.6f,  // run
                0.7f); // exit
        }

        public override void OnEnter()
        {
            base.OnEnter();
            phase = Phase.Raising;
            time = 0f;
            energy = 0f;
            moveTime = 0f;
            lastSpeed = peakSpeed = 1f;
            milestones = 0;
            wasMoving = false;
            sprinting = false;
            nextEffect = nextSpike = 0f;
            shield = null;
            braced = armourOn = listening = floorToggled = false;
            frontHits = otherHits = 0;
            unloaded = loggedHits = false;
            bashedMid = false;
            force = 0f;
            bashHits = crashHits = crashDamage = 0;
            hitNames.Clear();
            dashPhase = DashPhase.None;
            slideTime = 0f;
            dashes = dashChain = 0;
            replayAnim = false;
            lastHere = parent.transform.position;
            runes = null;
            orbit = null;
            nextWake = nextCrack = nextEmber = 0f;
            drawnCracks = drawnEmbers = drawnShards = drawnSpikes = flung = 0;
            // The shield goes up the way you're aiming (or facing).
            aim = inputVector != Vector2.zero ? inputVector.normalized : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            if (aim == Vector2.zero)
                aim = Vector2.right;
            parent.movement.EndMovement();
            signature = isUltimate;
            aftershockDone = false;
            carrier.Clear();
            cinematic = 0;
            DropPhalanx();
            pairsRaised = 0;
            crashSpots.Clear();
            if (signature)
            {
                energy = 100f;
                cinematic = Cinematic.Begin();
            }
        }

        // The base Update runs ExecuteSkill once the animation passes its execute time; while the
        // shield is up the run/idle animations loop, so the phases are driven from here instead.
        public override void Update()
        {
            UpdateRepeatLock(string.Empty);
            if (skillExecuted)
            {
                ConcludeSkill();
                return;
            }
            if (phase == Phase.Raising && !parent.anim.AnimPlayed(animExecTime))
            {
                // The wind-up can be dashed out of, like any slam.
                CancelToDash(false);
                return;
            }
            ExecuteSkill();
        }

        public override void ExecuteSkill()
        {
            switch (phase)
            {
                case Phase.Raising:
                    RaiseShield();
                    phase = Phase.Charging;
                    Charge();
                    break;
                case Phase.Charging:
                    Charge();
                    break;
                case Phase.Rushing:
                    Rush();
                    break;
                case Phase.Recovering:
                    Recover();
                    break;
                default:
                    base.ExecuteSkill();
                    break;
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (parent.rigidbody2D == null)
                return;
            if (phase == Phase.Charging && dashPhase != DashPhase.None)
                DashMove();
            else if (phase == Phase.Rushing)
                parent.rigidbody2D.velocity = rushDir * rushSpeed;
            else if (phase == Phase.Recovering)
                parent.rigidbody2D.velocity = Vector2.zero;
        }

        public override void OnExit()
        {
            if (signature)
                Cinematic.EaseOut(cinematic, 0.5f, unloaded ? "cast over" : "shield dropped before the crash");
            carrier.Clear();
            DropPhalanx();
            EndDash();
            StopSprint();
            SetArmour(false);
            StopListening();
            if (braced)
            {
                braced = false;
                try { parent.Endure(); } catch { }
            }
            if (floorToggled)
            {
                floorToggled = false;
                try { parent.ToggleEnemyFloorCollisions(); } catch { }
            }
            // The drawn chunks circling you and anything riding on you go too (the shield's
            // cracks are part of the shield and go with it).
            if (orbit != null)
            {
                StoneOrbit circling = orbit;
                Effect("orbit", () => circling.Stop());
                orbit = null;
            }
            Effect("cleanup", () => FxSprite.StopAll(this));
            if (StoneChargePlugin.DrawnEffects && phase != Phase.Raising)
                Log($"drawn: shield cracks at stage {(runes != null ? runes.Stage : 0)} of {ShieldRunes.Stages}{(runes == null ? " (no overlay)" : "")}, " +
                    $"{drawnCracks} floor crack(s), {drawnEmbers} ember(s), {flung} chunk(s) flung, {drawnShards} shard(s), {drawnSpikes} spike(s); {FxSprite.Live} fx sprite(s) live");
            runes = null;
            if (shield != null)
            {
                shield.Drop();
                shield = null;
            }
            if (!unloaded)
            {
                string next = fsm.nextStateName ?? "";
                string why = next.Contains("Dash") ? "dashed" : next.Contains("Dead") ? "died" : next.Contains("Fall") ? "fell" : $"left for {next}";
                if (phase == Phase.Raising)
                    Log($"cancelled ({why}) before the shield went up");
                else
                    Log($"shield dropped ({why}) after {time:0.0} s with {energy:0}% force lost{HitsTaken()}");
            }
            else if (!loggedHits)
                LogHits();
            base.OnExit();
        }

        // ---- The shield ----

        private void RaiseShield()
        {
            Vector2 at = parent.attackOriginTrans.position;
            shield = StoneShield.Raise(parent, skillID, aim);
            // Its Start() fires the level-1 shove burst at the shield's spot a frame later; count
            // what it hits through the game's attack history instead: cheap and good enough.
            if (StoneChargePlugin.Brace)
            {
                try
                {
                    parent.Endure(ignoreKnockback: true, ignoreHurtState: true, faceHurtDirection: false);
                    braced = true;
                }
                catch (System.Exception e)
                {
                    LogOnce("brace", $"couldn't brace against hits: {e.Message}");
                }
            }
            if (StoneChargePlugin.FrontArmourPercent > 0f && parent.health != null)
            {
                parent.health.takeDamageEnterHandlers += OnHurt;
                listening = true;
            }
            Effect("dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(20, 0.75f, -1f, -1f, new Vector3?(parent.transform.position), null));
            if (StoneChargePlugin.DrawnEffects)
            {
                // The ground splitting under the slab (the game's floor crack, as Earth Axe and
                // Boulder Drop make) and the shield's crack overlay.
                Vector2 spot = at + aim * BashOffset;
                Effect("floor crack", () => PoolManager.GetPoolItem<FloorCrackEmitter>().Emit(new int?(1), new Vector3?(spot), null, null, 0f, null, null));
                Effect("shield cracks", () => runes = ShieldRunes.Attach(shield != null ? shield.Renderer : null));
                orbit = new StoneOrbit();
            }
            Effect("shake", () => CameraController.ShakeCamera(0.25f, false));
            Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthExplosion", new Vector2?(at), null, 24f, -1f, 1.1f, false));

            float rate = StoneChargePlugin.EnergyPerSecond * (IsEmpowered ? 1f + StoneChargePlugin.EnhancedEnergyBonus / 100f : 1f);
            Log($"cast{(IsEmpowered ? " (enhanced)" : "")}: shield up for {StoneChargePlugin.ChargeTime:0.#} s, " +
                $"force builds {rate:0}%/s at base run speed (full in {100f / rate:0.0} s at x1.00, {100f / (rate * (1f + SprintBonus)):0.0} s sprinting); " +
                $"run into a foe or press again to crash{(StoneChargePlugin.FrontArmourPercent > 0f ? $", {StoneChargePlugin.FrontArmourPercent:0}% less from hits in front" : "")}" +
                (braced ? ", braced" : ""));
        }

        // Every frame the shield is up: run about, store force, watch for the second press.
        private void Charge()
        {
            float dt = Time.deltaTime;
            time += dt;
            Vector3 here = parent.transform.position;

            // A dash press dashes behind the shield instead of leaving for the dash state.
            TryDash();
            bool dashing = dashPhase != DashPhase.None;
            bool moving;
            if (dashing)
            {
                // The dash and its slide move the wizard (FixedUpdate). They count as being on the
                // move for the sprint and the contact crash, but store no force: their speed is
                // forced, like the Override speed mods SpeedFactor leaves out.
                moving = true;
                moveTime += dt;
            }
            else
            {
                Vector2 input = GetInputVector(false, false, false);
                moving = input.sqrMagnitude > InputDeadZone;
                if (moving)
                {
                    parent.movement.moveVector = input;
                    parent.movement.MoveToMoveVector();
                    if (!wasMoving || replayAnim)
                    {
                        parent.FaceTarget(here + (Vector3)input);
                        parent.anim.PlayDirectional("Run" + parent.facingDirection);
                    }
                    else
                        parent.PlayAnimOnFaceDirChange("Run", here + (Vector3)input);
                    aim = input.normalized;
                    moveTime += dt;
                    if (!sprinting && moveTime >= StoneChargePlugin.SprintDelay)
                        StartSprint();
                    lastSpeed = SpeedFactor();
                    peakSpeed = Mathf.Max(peakSpeed, lastSpeed);
                    float rate = StoneChargePlugin.EnergyPerSecond * (IsEmpowered ? 1f + StoneChargePlugin.EnhancedEnergyBonus / 100f : 1f);
                    energy = signature ? 100f : Mathf.Min(100f, energy + dt * rate * lastSpeed);
                }
                else
                {
                    parent.movement.EndMovement();
                    if (wasMoving || time <= dt || replayAnim)
                        parent.anim.PlayDirectional("Idle" + parent.facingDirection);
                    moveTime = 0f;
                    StopSprint();
                }
                replayAnim = false;
            }
            wasMoving = moving;
            shield?.Aim(aim);
            Milestones();
            ChargeEffects(moving);

            // The signature: the phalanx forms, and every shield in it grabs what it runs into,
            // carried along in front of the line.
            if (signature)
            {
                float settle = Mathf.Clamp01(time / 0.5f);
                float eased = settle * settle * (3f - 2f * settle);
                Cinematic.Set(cinematic, SigZoom * eased, Mathf.Lerp(1f, SigDim, eased));
                FormPhalanx();
                foreach (StoneShield s in phalanx)
                    s.Aim(aim);
                Vector2 shieldSpot = (Vector2)here + aim * BashOffset;
                if (moving)
                {
                    GrabAt(shieldSpot);
                    foreach (StoneShield s in phalanx)
                        if (s.Alive)
                            GrabAt(s.Position(parent));
                }
                carrier.Move(here, shieldSpot, PhalanxPairs * PhalanxSpacing + CarryKeep);
            }

            // A dash covers most of a unit a frame: check halfway from last frame's spot as well.
            Enemy struck;
            if (time >= StoneChargePlugin.MinPressTime && skillSlot >= 0 && parent.inputDevice.GetButtonDown("Skill" + skillSlot))
                Unload("press");
            else if (!signature && moving && time >= StoneChargePlugin.MinPressTime &&
                (ShieldTouches(here, out struck) || (dashing && ShieldTouches(((Vector2)here + lastHere) * 0.5f, out struck))))
                Unload((dashing ? "dashed into " : "ran into ") + struck.name);
            else if (time >= StoneChargePlugin.ChargeTime)
                Unload("timeout");
            lastHere = here;
        }

        // ---- The dash behind the shield ----

        // The dash button while the shield is up: a fresh press when not dashing (as the game's
        // TransitionToSkill reads it), or the button held during the slide with the skip-the-slide
        // relic (SlideState.Update), up to MaxDashChain dashes in a row.
        private void TryDash()
        {
            if (dashPhase == DashPhase.Dashing || parent.inputDevice == null || !parent.MovementEnabled)
                return;
            Player.SkillState dashSkill = parent.GetDashSkill();
            if (dashSkill == null)
                return;
            int slot = dashSkill.SetSkillSlot();
            if (slot < 0)
                return;
            if (dashPhase == DashPhase.None)
            {
                if (!parent.inputDevice.GetButtonDown("Skill" + slot))
                    return;
                dashChain = 0;
            }
            else
            {
                if (!(parent.ignoreDashSlide.CurrentValue && dashChain < MaxDashChain && parent.inputDevice.GetButton("Skill" + slot)))
                    return;
                dashChain++;
            }
            StartDash(dashSkill);
        }

        // BaseDashState.OnEnter, minus leaving this state and the dash arcana's own attack.
        private void StartDash(Player.SkillState dashSkill)
        {
            Player.BaseDashState baseDash = dashSkill as Player.BaseDashState;
            float duration = baseDash != null ? baseDash.dashDuration : DefaultDashDuration;
            // The way BaseDashState takes it: the aim for a mouse dash, else the move input (or the
            // aim with a locked controller aim), else the way you face; it turns the wizard too.
            Vector2 dir;
            if (parent.inputDevice.IsMouseDash)
                dir = GetInputVector();
            else
                dir = GetInputVector(true, parent.inputDevice.inputScheme == ChaosInputDevice.InputScheme.Gamepad && ChaosInputDevice.lockControllerAim);
            if (dir == Vector2.zero)
                dir = aim;
            bool chained = dashPhase == DashPhase.Sliding;
            dashPhase = DashPhase.Dashing;
            dashes++;
            dashDir = dir;
            aim = dir.normalized;
            // Drop the run's velocity (this also resets Movement's dash timer for DashToTarget).
            parent.movement.EndMovement();

            try
            {
                // The dash's duration as BaseDashState sets it (an override the stat's other mods,
                // dash relics, still apply to); DashToTarget then reads the stats each step.
                dashDurationMod.modValue = duration;
                parent.movement.dashDurationStat.AddMod(dashDurationMod);
                dashDurationOn = true;
                parent.movement.dashTimer = duration;
                parent.movement.dashVector = dir;
                if (baseDash != null && baseDash.forceEvadeStat.CurrentValue && parent.health != null)
                {
                    parent.health.evadeStat.AddMod(dashEvadeMod);
                    dashEvadeOn = true;
                }
            }
            catch (System.Exception e)
            {
                LogOnce("dash", $"couldn't set up the dash's stats: {e.Message}");
            }
            // Over pits like the game's dash: airborne, the pit-edge collider off, and through enemies.
            Airborne(true);
            if (!floorToggled)
            {
                try
                {
                    parent.ToggleEnemyFloorCollisions(false);
                    floorToggled = true;
                }
                catch
                {
                }
            }
            parent.anim.PlayDirectional(parent.DashAnimStr);
            Vector2 from = parent.attackOriginTrans.position;
            Effect("dashburst", () => PoolManager.GetPoolItem<DashAirBurst>().Burst(from, dir));
            Effect("dashdust", () => parent.dustEmitter.EmitDirBurst(15, Globals.GetRotationVector(-dir).z));
            Effect("dashtrails", () => parent.dashTrails.SetActive(true));
            Effect("dashsound", () => SoundManager.PlayWithDistAndSPR("StandardDash", parent.transform.position));
            shield?.Aim(aim);

            float speed = parent.movement.dashSpeedStat.CurrentValue;
            float length = parent.movement.dashDurationStat.CurrentValue;
            Log($"dash {dashes} behind the shield{(chained ? " (chained, slide skipped)" : "")}: toward ({aim.x:0.00}, {aim.y:0.00}) " +
                $"at {speed:0.#} u/s for {length:0.###} s (~{speed * length:0.#} u, {dashSkill.skillID}'s dash), " +
                $"{energy:0}% force kept, {StoneChargePlugin.ChargeTime - time:0.0} s of shield left{(dashEvadeOn ? ", evading" : "")}");
        }

        // FixedUpdate during the dash: BaseDashState.FixedUpdate (DashToTarget: dashVector at the
        // dash speed stat until the dash duration stat runs out), then SlideState.FixedUpdate.
        private void DashMove()
        {
            if (dashPhase == DashPhase.Dashing)
            {
                if (parent.movement.DashToTarget())
                    FinishDash();
                return;
            }
            slideTime += Time.fixedDeltaTime;
            if (slideTime >= SlideTime)
            {
                EndSlide();
                return;
            }
            parent.rigidbody2D.velocity = dashDir * Mathf.Lerp(SlideSpeed * 0.5f, SlideSpeed, slideTime / SlideTime);
            Vector2 dir = dashDir;
            Effect("slidedust", () => parent.dustEmitter.EmitDirBurst(1, -Globals.GetRotationVector(dir).z, 10f, -1f, 0.75f));
        }

        // BaseDashState.DashFinished, then SlideState.OnEnter: land, then glide on for SlideTime.
        private void FinishDash()
        {
            RemoveDashStats();
            Effect("dashtrails", () => parent.dashTrails.SetActive(false));
            Airborne(false);
            if (floorToggled)
            {
                floorToggled = false;
                try { parent.ToggleEnemyFloorCollisions(); } catch { }
            }
            parent.movement.EndMovement();
            dashPhase = DashPhase.Sliding;
            slideTime = 0f;
            parent.anim.PlayDirectional(parent.SlideAnimStr);
            // With the skip-the-slide relic the slide stays airborne, so a chained dash crosses on.
            if (parent.ignoreDashSlide.CurrentValue && dashChain < MaxDashChain)
                Airborne(true);
        }

        private void EndSlide()
        {
            Airborne(false);
            parent.movement.EndMovement();
            dashPhase = DashPhase.None;
            replayAnim = true;
            Log($"dash {dashes} over without a contact crash: {energy:0}% force kept, {StoneChargePlugin.ChargeTime - time:0.0} s of shield left");
        }

        // Ends a dash or slide at once (the unload, or leaving the state): the dash's stats and
        // airborne status come off. Enemy floor collisions are left to the caller (the rush wants
        // them off; OnExit puts them back).
        private void EndDash()
        {
            if (dashPhase == DashPhase.None && !dashDurationOn && !dashEvadeOn && !dashAirborne)
                return;
            RemoveDashStats();
            if (dashPhase == DashPhase.Dashing)
                Effect("dashtrails", () => parent.dashTrails.SetActive(false));
            Airborne(false);
            dashPhase = DashPhase.None;
        }

        private void RemoveDashStats()
        {
            if (dashDurationOn)
            {
                dashDurationOn = false;
                try { parent.movement.dashDurationStat.RemoveMod(dashDurationMod.ID); } catch { }
            }
            if (dashEvadeOn)
            {
                dashEvadeOn = false;
                try { parent.health.evadeStat.RemoveMod(dashEvadeMod); } catch { }
            }
        }

        // The dash's pit handling (BaseDashState.OnEnter / DashFinished): airborne, so the Fall
        // component lets you over pits, and the pit-edge collider off. Landing puts the collider
        // back only off a pit (TogglePreventFallCollider), so landing on one drops you in, as the
        // game's dash does.
        private void Airborne(bool on)
        {
            if (on == dashAirborne)
                return;
            dashAirborne = on;
            try
            {
                if (on)
                    parent.airborneStat.AddMod(dashAirborneMod);
                else
                    parent.airborneStat.RemoveMod(dashAirborneMod);
                parent.TogglePreventFallCollider(!on);
            }
            catch (System.Exception e)
            {
                LogOnce("airborne", $"couldn't set the dash's airborne status: {e.Message}");
            }
        }

        // The phalanx forming: a pair of shields either side of the middle one every PhalanxStep,
        // from the middle outward, each pair landing with a shake; a hit-stop and a flash when the
        // line is complete.
        private void FormPhalanx()
        {
            while (pairsRaised < PhalanxPairs && time >= PhalanxStep * (pairsRaised + 1))
            {
                pairsRaised++;
                float side = pairsRaised * PhalanxSpacing;
                foreach (float s in new[] { side, -side })
                {
                    StoneShield shield = StoneShield.Raise(parent, skillID, aim, s);
                    if (shield == null)
                        continue;
                    phalanx.Add(shield);
                    Vector2 spot = shield.Position(parent);
                    Effect("dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(16, 0.6f, -2f, -1f, new Vector3?(spot), null));
                    if (StoneChargePlugin.DrawnEffects)
                        Effect("floor crack", () => PoolManager.GetPoolItem<FloorCrackEmitter>().Emit(new int?(1), new Vector3?(spot), null, null, 0f, null, null));
                }
                int pair = pairsRaised;
                Effect("shake", () => CameraController.ShakeCamera(0.3f + 0.1f * pair, false));
                Vector2 at = parent.transform.position;
                Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthExplosion", new Vector2?(at), null, 24f, -1f, 1f + 0.12f * pair, false));
                if (pairsRaised == PhalanxPairs)
                {
                    Effect("hit-stop", () => TimeScaleController.FreezeForFrames(3, false));
                    Cinematic.Flash(cinematic, 1.3f, 0.06f);
                    Log($"signature phalanx formed: {phalanx.Count + 1} shields, {2f * PhalanxPairs * PhalanxSpacing + 1.5f:0.#} u wide");
                }
            }
        }

        private void DropPhalanx()
        {
            foreach (StoneShield s in phalanx)
                s?.Drop();
            phalanx.Clear();
        }

        // The signature: every live enemy (not an ally) at the shield is grabbed to be carried.
        private void GrabAt(Vector2 spot)
        {
            try
            {
                int before = carrier.Grabbed;
                foreach (Collider2D col in Physics2D.OverlapCircleAll(spot, ContactRadius))
                {
                    Enemy enemy = col != null ? col.GetComponentInParent<Enemy>() : null;
                    if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.health == null || enemy.health.CurrentHealthValue <= 0)
                        continue;
                    if (GameController.allies != null && GameController.allies.Contains(enemy.gameObject))
                        continue;
                    carrier.Grab(enemy);
                }
                if (carrier.Grabbed > before)
                    Effect("grab dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(12, 0.5f, -3f, -1f, new Vector3?(spot), null));
            }
            catch (System.Exception e)
            {
                LogOnce("grab", $"couldn't grab enemies at the shield: {e.Message}");
            }
        }

        // A live enemy (not an ally) touching the shield's spot ahead of the wizard.
        private bool ShieldTouches(Vector2 here, out Enemy struck)
        {
            struck = null;
            try
            {
                foreach (Collider2D col in Physics2D.OverlapCircleAll(here + aim * BashOffset, ContactRadius))
                {
                    Enemy enemy = col != null ? col.GetComponentInParent<Enemy>() : null;
                    if (enemy == null || !enemy.gameObject.activeInHierarchy)
                        continue;
                    if (enemy.health == null || enemy.health.CurrentHealthValue <= 0)
                        continue;
                    if (GameController.allies != null && GameController.allies.Contains(enemy.gameObject))
                        continue;
                    struck = enemy;
                    return true;
                }
            }
            catch (System.Exception e)
            {
                LogOnce("contact", $"couldn't check for enemies at the shield: {e.Message}");
            }
            return false;
        }

        private void Milestones()
        {
            int reached = Mathf.FloorToInt(energy / MilestoneStep + 0.0001f);
            while (milestones < reached)
            {
                milestones++;
                float percent = milestones * MilestoneStep;
                Log($"force {percent:0}% after {time:0.0} s (speed x{lastSpeed:0.00}{(sprinting ? ", sprinting" : "")}" +
                    $"{(IsEmpowered ? ", enhanced" : "")})");
                Vector3 at = parent.transform.position;
                int step = milestones;
                Effect("form", () => PoolManager.GetPoolItem<ParticleEffect>("RockFormEffect").Emit(new int?(4 + 3 * step), new Vector3?(at), RockFormEffect.SmallSettings, null, 0f, null, null));
                Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthPebble", new Vector2?(at), null, 24f, -1f, 0.8f + 0.15f * step, false));
                if (StoneChargePlugin.DrawnEffects)
                {
                    // The shield's cracks spread a step, and the game's dust rings out over the
                    // floor, wider each step.
                    runes?.SetStage(step);
                    Effect("dust ring", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(10 + 6 * step, DustRingRadius + DustRingPerStep * step, -4f, -1f, new Vector3?(at), null));
                }
                if (percent >= 100f)
                {
                    Effect("shake", () => CameraController.ShakeCamera(0.15f, false));
                    Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthDust", new Vector2?(at), null, 24f, -1f, 1f, false));
                }
            }
        }

        // The look of the force building: rock debris round the wizard, more and wider as it
        // builds; from half force the ground cracks and spikes behind them as they run (the Charge
        // arcana's spikes, no hit); at full a steady rock aura.
        private void ChargeEffects(bool moving)
        {
            if (StoneChargePlugin.DrawnEffects)
                DrawnCharge(moving);
            if (time >= nextEffect)
            {
                nextEffect = time + EffectInterval;
                Vector3 at = parent.transform.position;
                if (energy >= MilestoneStep)
                {
                    int count = 1 + Mathf.FloorToInt(energy / MilestoneStep);
                    float spread = 0.2f + 0.3f * energy / 100f;
                    Effect("debris", () => PoolManager.GetPoolItem<RockDebrisEmitter>().EmitSingle(RockDebrisType.Default, new int?(count),
                        new Vector3?(at + (Vector3)(Random.insideUnitCircle * spread)), RockDebrisEmitter.SmallCircleSlowExplosionOverride, null, 0f, null));
                }
                if (energy >= 100f)
                    Effect("form", () => PoolManager.GetPoolItem<ParticleEffect>("RockFormEffect").Emit(new int?(2), new Vector3?(at), RockFormEffect.SmallSettings, null, 0f, null, null));
            }
            if (moving && energy >= 2f * MilestoneStep && time >= nextSpike)
            {
                nextSpike = time + SpikeInterval * (energy >= 3f * MilestoneStep ? 0.7f : 1f);
                SpawnSpike();
            }
        }

        // The drawn side of the force building: stone chunks circling you from half force,
        // dust thrown back from your feet as you run (the game's dash dust, a puff at a time),
        // drawn cracks spreading in the floor behind you from half force (ember-lit at full) and
        // embers rising off you at full.
        private void DrawnCharge(bool moving)
        {
            int step = Mathf.FloorToInt(energy / MilestoneStep + 0.0001f);
            if (orbit != null)
            {
                StoneOrbit circling = orbit;
                Effect("orbit", () => circling.Update(parent, step, this));
            }
            Vector2 feet = parent.transform.position;
            bool running = moving && dashPhase == DashPhase.None;
            if (running && time >= nextWake)
            {
                nextWake = time + WakeInterval;
                Vector2 dir = aim;
                int count = 1 + step / 2;
                Effect("dust wake", () => parent.dustEmitter.EmitDirBurst(count, Globals.GetRotationVector(-dir).z, 3f, -1f, 0.4f, new Vector3?(feet - dir * 0.3f)));
            }
            if (running && step >= 2 && time >= nextCrack)
            {
                nextCrack = time + CrackInterval;
                Vector2 behind = feet - aim * 0.6f + Random.insideUnitCircle * 0.2f;
                bool glow = step >= 4;
                Effect("floor crack", () => FloorCrack(behind, glow, 1f));
            }
            if (step >= 4 && time >= nextEmber)
            {
                nextEmber = time + EmberInterval;
                // Every other one shed off a circling chunk, the rest off the wizard.
                Vector2 chunk;
                if (orbit != null && Random.value < 0.5f && orbit.AnyChunk(out chunk))
                    Effect("ember", () => RiseEmber(chunk - new Vector2(0f, 0.2f), 0.15f));
                else
                    Effect("ember", () => RiseEmber(feet));
            }
        }

        // A crack opening in the floor: the game's (its floor-crack sheet's first two frames, a
        // split then a web, held and faded; at full force an ember rises out of it) or the drawn
        // one (ember-lit at full force).
        private void FloorCrack(Vector2 at, bool glow, float size)
        {
            Sprite[] game = StoneArt.GameCrack;
            FxSprite fx = FxSprite.Spawn(game ?? StoneArt.Crack(glow), at, "TargetLineGround", 0);
            if (fx == null)
                return;
            fx.fps = game != null ? GameCrackFps : CrackFps;
            fx.life = CrackLife;
            fx.fadeAt = 0.6f;
            fx.size = game != null ? size * GameCrackSize : size;
            fx.sr.flipX = Random.value < 0.5f;
            drawnCracks++;
            if (game != null && glow)
                RiseEmber(at - new Vector2(0f, 0.2f));
        }

        private void RiseEmber(Vector2 feet, float spread = 1f)
        {
            Vector2 offset = Random.insideUnitCircle * spread;
            offset.x *= 0.7f;
            offset.y *= 0.35f;
            Vector2 at = feet + offset + new Vector2(0f, 0.2f);
            FxSprite fx = FxSprite.Spawn(StoneArt.Ember, at, "Actor", 0);
            if (fx == null)
                return;
            fx.velocity = new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(1f, 1.6f));
            fx.fps = EmberFps;
            fx.life = StoneArt.Ember.Length / EmberFps;
            fx.fadeAt = 1f;
            fx.sortRef = parent.spriteRenderer;
            fx.sortOffset = offset.y > 0f ? -1 : 1;
            drawnEmbers++;
        }

        // The Charge arcana's floor spikes, as it spawns them: a cosmetic spike behind the wizard.
        private void SpawnSpike()
        {
            Effect("spike", () =>
            {
                Vector3 behind = parent.transform.position + (Vector3)(Random.insideUnitCircle * 0.25f) - (Vector3)aim * 0.5f;
                EarthFloorSpike spike = Object.Instantiate(EarthFloorSpike.Prefab, behind, Quaternion.identity).GetComponent<EarthFloorSpike>();
                spike.breakPrimaryParticleCount = 3;
                spike.breakSeconaryParticleCount = 2;
                spike.spawnParticleCount = 3;
                spike.useFloorCollider = false;
                spike.shakeOnSpawn = false;
                spike.Activate(useAttackBox: false, useFloorCollider: false);
            });
        }

        // ---- Speed ----

        // The game's run speed-up, which its Run state drops the moment you cast: given back after
        // SprintDelay on the move behind the shield, and lost again when you stop.
        private void StartSprint()
        {
            try
            {
                parent.movement.moveSpeedStat.RemoveMod(SprintModID);
                parent.movement.moveSpeedStat.AddMod(new NumVarStatMod(SprintModID, SprintBonus, 10, VarStatModType.LateMultiplicative));
                sprinting = true;
                Effect("sound", () => SoundManager.PlayWithDistAndSPR("RunSpeed", parent.transform.position));
                Log($"sprinting (+{SprintBonus * 100f:0}% run speed) after {moveTime:0.0} s on the move; run speed now {parent.movement.moveSpeedStat.CurrentValue:0.0} u/s");
            }
            catch (System.Exception e)
            {
                LogOnce("sprint", $"couldn't apply the sprint: {e.Message}");
            }
        }

        private void StopSprint()
        {
            if (!sprinting)
                return;
            sprinting = false;
            try { parent.movement?.moveSpeedStat?.RemoveMod(SprintModID); } catch { }
        }

        // The wizard's run speed against its base (1 = base, 1.4 sprinting, more with speed relics,
        // the cloak and speed buffs). Counts the stat's lasting modifiers the way the game does
        // (multiplied, then added, then late-multiplied) but not the Override ones: those are the
        // speeds a dash or an attack forces on the wizard for a moment. (Trailblazer's read.)
        private static bool loggedSpeed;

        private float SpeedFactor()
        {
            float factor = 1f;
            try
            {
                NumVarStat stat = parent.movement != null ? parent.movement.moveSpeedStat : null;
                if (stat != null && stat.BaseValue > 0.01f)
                {
                    float speed = stat.BaseValue;
                    int overrides = 0;
                    if (stat.modifiers != null)
                    {
                        foreach (VarStatMod<float> mod in stat.modifiers)
                        {
                            NumVarStatMod numMod = mod as NumVarStatMod;
                            if (numMod == null)
                                continue;
                            switch (numMod.modType)
                            {
                                case VarStatModType.Additive:
                                    speed += numMod.modValue;
                                    break;
                                case VarStatModType.Multiplicative:
                                case VarStatModType.LateMultiplicative:
                                    speed *= 1f + numMod.modValue;
                                    break;
                                case VarStatModType.Override:
                                case VarStatModType.OverrideWithMods:
                                    overrides++;
                                    break;
                            }
                        }
                    }
                    factor = speed / stat.BaseValue;
                    if (!loggedSpeed)
                    {
                        loggedSpeed = true;
                        Log($"run speed {speed:0.##} of base {stat.BaseValue:0.##} (x{factor:0.00}; the stat reads {stat.ModifiedValue:0.##}, {overrides} override mod(s) ignored)");
                    }
                }
            }
            catch
            {
            }
            return Mathf.Clamp(factor, 0.5f, 6f);
        }

        // ---- Taking hits behind the shield ----

        // Just before the game works out a hit on the wizard: hits from in front get the armour,
        // the rest don't. (The armour stat is read after this is announced.)
        private void OnHurt(AttackInfo info, Entity attacker)
        {
            try
            {
                if (info == null || info.isStatusEffect)
                    return;
                Vector2 from = info.attackerPosition - (Vector2)parent.transform.position;
                bool front = from.sqrMagnitude > 0.0001f && Vector2.Dot(from.normalized, aim) >= FrontCos;
                SetArmour(front);
                if (front)
                {
                    frontHits++;
                    if (frontHits == 1)
                        Log($"blocked a hit from the front with the shield ({StoneChargePlugin.FrontArmourPercent:0}% less damage)");
                }
                else
                    otherHits++;
            }
            catch
            {
            }
        }

        private void SetArmour(bool on)
        {
            if (on == armourOn || parent.health == null || parent.health.armorStat == null)
                return;
            try
            {
                if (on)
                    parent.health.armorStat.AddMod(new NumVarStatMod(ArmourModID, StoneChargePlugin.FrontArmourPercent / 100f, 10, VarStatModType.Additive));
                else
                    parent.health.armorStat.RemoveMod(ArmourModID);
                armourOn = on;
            }
            catch (System.Exception e)
            {
                LogOnce("armour", $"couldn't set the shield's armour: {e.Message}");
            }
        }

        private void StopListening()
        {
            if (!listening)
                return;
            listening = false;
            try { if (parent.health != null) parent.health.takeDamageEnterHandlers -= OnHurt; } catch { }
        }

        private string HitsTaken() =>
            frontHits + otherHits > 0 ? $"; took {frontHits} hit(s) on the shield, {otherHits} from the sides or behind" : "";

        // ---- The crash ----

        private int ScaledDamage(int baseDamage) =>
            Mathf.Max(baseDamage > 0 ? 1 : 0, Mathf.RoundToInt(baseDamage * (1f + (StoneChargePlugin.FullMultiplier - 1f) * force)));

        private float ScaledKnockback => Mathf.Lerp(StoneChargePlugin.Knockback, StoneChargePlugin.FullKnockback, force);

        private bool Stuns => force * 100f >= StoneChargePlugin.StunEnergyPercent - 0.001f;

        // The second press or the timeout: commit to the rush.
        private void Unload(string trigger)
        {
            if (dashPhase != DashPhase.None)
            {
                // Mid-dash or mid-slide: the rush takes over from here, along the dash.
                Log(trigger.StartsWith("dashed into")
                    ? $"dash {dashes} ended in a contact crash: {trigger.Substring("dashed into ".Length)} at {energy:0}% force"
                    : $"dash {dashes} cut short by the unload ({trigger})");
                EndDash();
            }
            phase = Phase.Rushing;
            unloaded = true;
            force = Mathf.Clamp01(energy / 100f);
            rushDir = aim;
            rushTime = 0f;
            bashedMid = false;
            StopSprint();
            parent.FaceTarget(parent.transform.position + (Vector3)rushDir);
            parent.movement.EndMovement();
            parent.movement.moveVector = rushDir;
            rushSpeed = StoneChargePlugin.RushDistance / StoneChargePlugin.RushTime;
            try
            {
                // Rush through enemies rather than getting stuck on them (Aegis Charge does this).
                parent.ToggleEnemyFloorCollisions(false);
                floorToggled = true;
            }
            catch
            {
            }
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = rushDir * rushSpeed;
            parent.anim.PlayDirectional(parent.SlideAnimStr, -1, 0.25f);

            AttackInfo bashInfo = AttackInfo.GetInfo(parent, parent.skillCategory, skillID, BashLevel, isUltimate);
            AttackInfo crashInfo = AttackInfo.GetInfo(parent, parent.skillCategory, skillID, CrashLevel, isUltimate);
            int bash = bashInfo != null ? ScaledDamage(bashInfo.damage) : -1;
            int crash = crashInfo != null ? ScaledDamage(crashInfo.damage) : -1;
            Log($"unload ({trigger}) at {energy:0}% force after {time:0.0} s (speed x{lastSpeed:0.00}, peak x{peakSpeed:0.00}): " +
                $"rush {StoneChargePlugin.RushDistance:0.#} u, bash {bash} + crash {crash} damage (x{1f + (StoneChargePlugin.FullMultiplier - 1f) * force:0.0}), " +
                $"knockback {ScaledKnockback:0}{(Stuns ? $", stun x{StoneChargePlugin.StunMultiplier:0.#}" : "")}, " +
                $"crash radius {(IsEmpowered ? StoneChargePlugin.EnhancedCrashRadius : StoneChargePlugin.CrashRadius):0.0}{HitsTaken()}");

            Vector2 at = parent.transform.position;
            Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthCrumble", new Vector2?(at), null, 24f, -1f, 1.1f, false));
            Effect("debris", () => PoolManager.GetPoolItem<RockBurstDirectionalEffect>("RockBurstDirectionalEffect").EmitSingle(RockDebrisType.Default,
                new int?(3 + Mathf.RoundToInt(5f * force)), new Vector3?(at + rushDir * BashOffset), null, new Vector3?(Globals.GetRotationVector(rushDir)), 0f, null));
            if (StoneChargePlugin.DrawnEffects)
            {
                // The circling chunks thrown ahead along the rush, a crack torn where you push
                // off and dust blasted back.
                if (orbit != null)
                {
                    StoneOrbit circling = orbit;
                    flung += circling.Count;
                    Vector2 dir = rushDir;
                    Effect("orbit", () => circling.Fling(dir));
                    orbit = null;
                }
                bool glow = force >= 0.999f;
                Effect("floor crack", () => FloorCrack(at, glow, 1.1f));
                Vector2 back = -rushDir;
                Effect("dust wake", () => parent.dustEmitter.EmitDirBurst(8 + Mathf.RoundToInt(8f * force), Globals.GetRotationVector(back).z, -1f, -1f, -1f, new Vector3?(at)));
            }
            Bash();
        }

        private void Rush()
        {
            rushTime += Time.deltaTime;
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = rushDir * rushSpeed;
            if (signature)
                carrier.Move(parent.transform.position, (Vector2)parent.transform.position + rushDir * BashOffset, PhalanxPairs * PhalanxSpacing + CarryKeep + 1f);
            if (!bashedMid && rushTime >= StoneChargePlugin.RushTime * 0.5f)
            {
                bashedMid = true;
                Bash();
                if (StoneChargePlugin.DrawnEffects)
                {
                    // A second crack furrowed halfway along the rush.
                    Vector2 mid = parent.transform.position;
                    bool glow = force >= 0.999f;
                    Effect("floor crack", () => FloorCrack(mid, glow, 1.1f));
                }
            }
            if (rushTime >= StoneChargePlugin.RushTime)
                Crash();
        }

        // The shield bash (level 2) at the shield's spot: a quiet burst (the shield is the look).
        private void Bash()
        {
            Vector2 at = (Vector2)parent.attackOriginTrans.position + rushDir * BashOffset;
            try
            {
                EarthBurst burst = EarthBurst.CreateBurst(at, parent.skillCategory, skillID, BashLevel, StoneChargePlugin.BashRadius);
                burst.skipParticleEmit = true;
                Scale(burst.attack, false);
                burst.attack.entityCollisionEventHandlers += OnBashHit;
            }
            catch (System.Exception e)
            {
                LogOnce("bash", $"couldn't make the bash burst: {e.Message}");
            }
        }

        // The crash (level 3) where the rush ends, a unit ahead.
        private void Crash()
        {
            phase = Phase.Recovering;
            recoverTime = 0f;
            parent.movement.EndMovement();
            float radius = IsEmpowered ? StoneChargePlugin.EnhancedCrashRadius : StoneChargePlugin.CrashRadius;
            if (signature)
                radius = PhalanxCrashRadius * StoneChargePlugin.SignatureCrashScale / 2f;
            Vector2 at = (Vector2)parent.attackOriginTrans.position + rushDir * CrashOffset;
            crashAt = at;
            crashRadius = radius;
            crashSpots.Clear();
            crashSpots.Add(at);
            if (signature)
            {
                // Every shield of the line crashes where it stands, at once.
                foreach (StoneShield s in phalanx)
                {
                    if (!s.Alive)
                        continue;
                    Vector2 spot = s.Position(parent) + rushDir * (CrashOffset - 1f);
                    crashSpots.Add(spot);
                    try
                    {
                        EarthBurst side = EarthBurst.CreateBurst(spot, parent.skillCategory, skillID, SigCrashLevel, radius);
                        Scale(side.attack, true);
                        side.attack.entityCollisionEventHandlers += OnCrashHit;
                    }
                    catch (System.Exception e)
                    {
                        LogOnce("phalanx crash", $"couldn't make a phalanx shield's crash: {e.Message}");
                    }
                    Effect("game crater", () => PoolManager.GetPoolItem<ParticleEffect>("FloorCraterLarge").Emit(new int?(1), new Vector3?(spot), null, null, 0f, null, null));
                    Effect("dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(30, radius * 0.5f, -6f, -1f, new Vector3?(spot), null));
                    Effect("debris", () => PoolManager.GetPoolItem<RockDebrisEmitter>().EmitSingle(RockDebrisType.Default, new int?(8),
                        new Vector3?(spot), RockDebrisEmitter.FloorExplosionOverrideR1p5, null, 0f, null));
                }
                DropPhalanx();
                Earthquake(carrier.Grabbed);
            }
            carrier.Clear();
            try
            {
                EarthBurst burst = EarthBurst.CreateBurst(at, parent.skillCategory, skillID, signature ? SigCrashLevel : CrashLevel, radius);
                Scale(burst.attack, true);
                crashDamage = burst.attack.atkInfo != null ? burst.attack.atkInfo.damage : 0;
                burst.attack.entityCollisionEventHandlers += OnCrashHit;
            }
            catch (System.Exception e)
            {
                LogOnce("crash", $"couldn't make the crash burst: {e.Message}");
            }
            if (shield != null)
            {
                shield.Drop();
                shield = null;
            }
            if (floorToggled)
            {
                floorToggled = false;
                try { parent.ToggleEnemyFloorCollisions(); } catch { }
            }
            // A dash that crashed over a pit landed with the pit-edge collider off (it stays off
            // over a pit); the rush may have carried you off it, so check again where you stop.
            if (dashes > 0)
            {
                try { parent.TogglePreventFallCollider(true); } catch { }
            }
            Effect("dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(30 + Mathf.RoundToInt(40f * force), radius * 0.5f, -6f, -1f, new Vector3?(at), null));
            Effect("debris", () => PoolManager.GetPoolItem<RockDebrisEmitter>().EmitSingle(RockDebrisType.Default, new int?(6 + Mathf.RoundToInt(10f * force)),
                new Vector3?(at), RockDebrisEmitter.FloorExplosionOverrideR1p5, null, 0f, null));
            if (StoneChargePlugin.DrawnEffects)
            {
                // The game's crater (Earth Axe's: the large one from half force) and its floor
                // crack spread to the crash's size, a ring of the game's rock spikes erupting at
                // the crash's edge, and its stones flung mostly ahead.
                bool big = force >= 0.5f;
                bool glow = Stuns;
                Effect("game crater", () => PoolManager.GetPoolItem<ParticleEffect>(big ? "FloorCraterLarge" : "FloorCrater").Emit(new int?(1), new Vector3?(at), null, null, 0f, null, null));
                Effect("crater", () => Crater(at, radius, glow));
                Effect("rock spikes", () => SpikeRing(at, radius));
                Effect("rock shard", () => Shards(at));
            }
            Effect("shake", () => CameraController.ShakeCamera(0.4f + 0.8f * force, false));
            if (force >= 0.999f)
            {
                try { TimeScaleController.FreezeForFrames(3, false); } catch { }
            }
            Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthExplosionShort", new Vector2?(at), null, 24f, -1f, 1.1f - 0.3f * force, false));
            Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthPebble", new Vector2?(at), null, 24f, -1f, 1f, false));
            parent.anim.PlayDirectional(parent.GSlamAnimStr, -1, 0.3f);
        }

        // The crater spreading under the crash, sized to its hit area: the game's floor crack
        // played out to its hole, or the drawn one (ember-lit from StunEnergy).
        private void Crater(Vector2 at, float radius, bool glow)
        {
            Sprite[] game = StoneArt.GameCrater;
            FxSprite fx = FxSprite.Spawn(game ?? StoneArt.Crater(glow), at, "TargetLineGround", 0);
            if (fx == null)
                return;
            fx.fps = game != null ? GameCrackFps : CraterFps;
            fx.life = CraterLife;
            fx.fadeAt = 0.65f;
            fx.size = radius * 2f * CraterFill / (game != null ? StoneArt.CrackWidth : CraterWidth);
        }

        // The game's rock spikes (its EarthWave sheet) shooting up and crumbling in a ring at the
        // crash's edge, one after another round from the side facing the rush; more at higher
        // force. In front of the wizard below their feet, behind above. Skipped if the sheet
        // isn't loaded (the crater and stones still show).
        private void SpikeRing(Vector2 at, float radius)
        {
            if (StoneArt.GameSpike(0) == null)
                return;
            int count = MinSpikes + Mathf.RoundToInt(SpikesPerForce * force);
            float ahead = Mathf.Atan2(rushDir.y, rushDir.x) * Mathf.Rad2Deg;
            float feetY = parent.transform.position.y;
            for (int i = 0; i < count; i++)
            {
                float angle = (ahead + i * 360f / count) * Mathf.Deg2Rad;
                Vector2 spot = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * FloorSquashY) * radius * SpikeReach
                    + Random.insideUnitCircle * 0.1f;
                Sprite[] frames = StoneArt.GameSpike(i);
                FxSprite fx = FxSprite.Spawn(frames, spot, "Actor", 0);
                if (fx == null)
                    return;
                // Round the ring from the front, a beat apart.
                int step = i == 0 ? 0 : (i + 1) / 2;
                fx.delay = step * SpikeStagger;
                fx.fps = SpikeFps;
                fx.life = fx.delay + frames.Length / SpikeFps;
                fx.fadeAt = 0.85f;
                fx.sr.flipX = Random.value < 0.5f;
                fx.sortRef = parent.spriteRenderer;
                fx.sortOffset = spot.y > feetY ? -1 : 1;
                drawnSpikes++;
            }
        }

        // Stones flung out of the crash, most of them on ahead along the rush, a couple back;
        // each hops in an arc, tumbling, lands and fades. The game's (the bigger stones of its
        // Rocks debris sheet, tumbling by quarter turns) or the drawn ones. More at higher force.
        private void Shards(Vector2 at)
        {
            int count = Mathf.Min(MinShards + ShardsPerForce, MinShards + Mathf.RoundToInt(ShardsPerForce * force));
            float ahead = Mathf.Atan2(rushDir.y, rushDir.x) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                float angle = (i < 2 ? ahead + 180f : ahead) + Random.Range(-75f, 75f);
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Sprite[] game = StoneArt.GameShard(i);
                FxSprite fx = FxSprite.Spawn(game ?? StoneArt.Shard(i), at + dir * 0.3f, "Effect", 0);
                if (fx == null)
                    return;
                fx.velocity = dir * Random.Range(3f, 6.5f) * (0.8f + 0.4f * force);
                fx.drag = 2.5f;
                fx.life = Random.Range(0.55f, 0.75f);
                fx.hop = Random.Range(0.5f, 1.2f);
                fx.hopTime = fx.life * 0.8f;
                fx.loop = true;
                fx.fps = ShardFps;
                if (game != null)
                {
                    fx.angle = 90f * i;
                    fx.spin = (i % 2 == 0 ? 90f : -90f) * ShardFps;
                    fx.spinStep = 90f;
                }
                else
                    fx.Skip(i);
                fx.fadeAt = 0.75f;
                drawnShards++;
            }
        }

        // The signature crash's beats: time all but stops on a hit-stop, the room flashes, the
        // ground heaves.
        private void Earthquake(int carried)
        {
            Effect("hit-stop", () => TimeScaleController.FreezeForFrames(SigHitStopFrames, false));
            Cinematic.Flash(cinematic, SigFlash, 0.1f);
            Effect("quake", () => CameraController.ShakeCamera(SigShake, true));
            Effect("slow motion", () => TimeScaleController.SetTimeScaleDelayOverRealTime(SigSlowMoTime, SigSlowMoScale, true, true));
            Log($"signature phalanx crash: {crashSpots.Count} shields at radius {crashRadius:0.0}, carrying {carried} enem{(carried == 1 ? "y" : "ies")}, hit-stop {SigHitStopFrames} frames, " +
                $"flash x{SigFlash:0.0}, shake {SigShake:0.0}, slow motion {SigSlowMoTime:0.00} s; the shockwave rolls on {AftershockAt:0.0} s later");
        }

        // The shockwave: the ground erupts again a little ahead of every shield of the line, with
        // rock spikes, throwing enemies outward.
        private void Aftershock()
        {
            aftershockDone = true;
            foreach (Vector2 spot in crashSpots)
            {
                Vector2 at = spot + rushDir * ShockAhead;
                try
                {
                    EarthBurst burst = EarthBurst.CreateBurst(at, parent.skillCategory, skillID, AftershockLevel, AftershockRadius);
                    burst.skipParticleEmit = true;
                }
                catch (System.Exception e)
                {
                    LogOnce("aftershock", $"couldn't make the shockwave: {e.Message}");
                }
                if (StoneChargePlugin.DrawnEffects)
                    Effect("rock spikes", () => SpikeRing(at, AftershockRadius));
                Effect("dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(30, AftershockRadius * 0.6f, -6f, -1f, new Vector3?(at), null));
            }
            Effect("shake", () => CameraController.ShakeCamera(1.3f, true));
            Vector2 middle = crashAt + rushDir * ShockAhead;
            Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthExplosion", new Vector2?(middle), null, 24f, -1f, 0.8f, false));
            Cinematic.EaseOut(cinematic, SigEaseBack, "shockwave finished");
        }

        private void Recover()
        {
            recoverTime += Time.deltaTime;
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
            if (signature && !aftershockDone && recoverTime >= AftershockAt)
                Aftershock();
            if (recoverTime < (signature ? SigRecoverTime : RecoverTime))
                return;
            phase = Phase.Done;
            LogHits();
            // Past the run and exit thresholds, so ExitToSkillOrRunOrIdle lets you go at once.
            parent.anim.PlayDirectional(parent.GSlamAnimStr, -1, 0.65f);
            base.ExecuteSkill();
        }

        // Scales a bash or crash hit by the stored force, before its collider touches anything:
        // damage 1x to FullMultiplier, knockback Knockback to FullKnockback along the rush, and the
        // crash's hitstun from StunEnergy up. (The game's stats hold the base numbers.)
        private void Scale(Attack attack, bool crash)
        {
            attack.knockbackOverwriteVector = rushDir;
            AttackInfo info = attack.atkInfo;
            if (info == null)
                throw new System.Exception("the burst has no attack info");
            info.damage = ScaledDamage(info.damage);
            info.knockbackMultiplier = ScaledKnockback;
            if (crash && Stuns)
                info.hitStunDurationModifier *= StoneChargePlugin.StunMultiplier;
        }

        private void OnBashHit(Entity hit)
        {
            bashHits++;
            Remember(hit);
        }

        private void OnCrashHit(Entity hit)
        {
            crashHits++;
            Remember(hit);
        }

        private void Remember(Entity hit)
        {
            if (hitNames.Count < 6)
                hitNames.Add(hit != null ? hit.name : "?");
        }

        private void LogHits()
        {
            if (loggedHits)
                return;
            loggedHits = true;
            if (bashHits + crashHits == 0)
            {
                Log($"crash hit nothing ({energy:0}% force, {crashDamage} damage waiting)");
                return;
            }
            string names = string.Join(", ", hitNames.ToArray()) + (bashHits + crashHits > hitNames.Count ? ", ..." : "");
            Log($"crash: bash hit {bashHits}, crash hit {crashHits} for {crashDamage} damage each before armour ({names})");
        }

        // ---- Helpers ----

        // Runs a cosmetic effect; if it's unavailable it's skipped (and logged once) instead of
        // stopping the cast.
        private static void Effect(string name, System.Action effect)
        {
            if (failed.Contains(name))
                return;
            try
            {
                effect();
            }
            catch (System.Exception e)
            {
                failed.Add(name);
                Log($"effect '{name}' unavailable, skipping it: {e.Message}");
            }
        }

        public static void Log(string message) => StoneChargePlugin.Log("Stone Charge: " + message);

        public static void LogOnce(string key, string message)
        {
            if (failed.Add("log:" + key))
                Log(message);
        }
    }
}
