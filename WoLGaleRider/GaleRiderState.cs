using System.Collections.Generic;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLGaleRider
{
    // Gale Rider (Air, standard arcana): a ball of wind under the wizard that they ride for a few
    // seconds. They keep steering with the stick, at their move speed with a ride bonus on it
    // (like Aero Ring's +25%), rolling on in the last direction when the stick is let go, and the
    // ball is a rolling attack round them: an enemy it rolls over is shoved away in the direction
    // they're heading and takes a little damage, a few times a second. The ball's SIZE and the
    // SHOVE grow with their speed (their move-speed stat against its base, like Trailblazer reads
    // it: relics, the cloak and speed-boost arcana count, the speed a dash forces for a moment
    // doesn't), from base run speed up to PeakSpeedPercent. Dashing cancels the ride; pressing
    // the button again hops off early. It ends in the game's slide, like Tearing Whirlwind.
    //
    // Built on the same calls as the game's own rides: the ball is an attack circle parented to
    // the wizard (Tearing Whirlwind), its collider pulsing to hit again (Aero Ring), its knockback
    // overridden as it goes (Rushing Typhoon); the speed bonus is a multiplicative move-speed mod
    // put on at the start and always taken off at the end, however the ride ends.
    public class GaleRiderState : Player.SkillState
    {
        public new static string staticID = "GaleRider";

        public const int MaxLevel = 10;                 // levels of the log's speed read, 1 at x1.0, 10 at the peak
        private const string RideSpeedModID = "GaleRider_Ride";
        private const float InputDeadZone = 0.04f;      // stick input (squared) below this is "not steering"
        private const float PitEdgeMargin = 0.2f;       // how far ahead of their feet a pit stops the ball
        private const float TeleportDistance = 3f;      // a step longer than this isn't riding (a new floor, a swap)
        private const float LevelLogInterval = 0.25f;   // at most one "level changed" line this often
        private const float VortexInterval = 0.06f;     // seconds between puffs of the swirling wind
        private const float DustInterval = 0.15f;       // seconds between rings of dust
        private const float BaseSpeedFactor = 1f;       // x1.0 base run speed: smallest ball, weakest shove
        private const float LoopPitch = 1.15f;          // the game's tornado loop, a touch higher (Whirlwind uses 1.2)
        // The drawn look (GaleArt), all cosmetic:
        private const float DrawnVortexInterval = 0.09f; // the game's AirVortex puffs thinned out under the drawn ball
        private const float BallLift = 0.45f;           // the drawn ball's middle above the feet (WindBall.Lift)
        private const float GatherFps = 30f;            // the gather's 6 frames in 0.2 s, over the cast's wind-up
        private const float StreakInterval = 0.11f;     // seconds between wind streaks at level 1...
        private const float StreakPerLevel = 0.007f;    // ...this much sooner each level (0.047 s at 10)
        private const float StreakFps = 20f;
        private const float StreakDrift = 0.6f;         // u/s the streaks drift back, plus StreakDriftPerLevel a level
        private const float StreakDriftPerLevel = 0.12f;
        private const float WakeInterval = 0.1f;        // seconds between puffs of dust thrown back behind
        private const float TrailInterval = 0.45f;      // seconds between the game's wind trails behind, from TrailLevel
        private const int TrailLevel = 4;
        private const float TrailLength = 2.5f;
        private const float PuffInterval = 0.05f;       // at most one drawn gust puff this often (a crowd hit at once)
        private const float PuffFps = 22f;
        private const float UnravelFps = 24f;

        // Numbers come from the config (GaleRiderPlugin), read as each cast starts.
        private float RideTimeNow, SpeedPercent, MinSize, MaxSize, PeakSpeedFactor, HitInterval, Knockback, PeakKnockback;

        private float time;
        private bool riding;
        private bool done;
        private Vector2 direction;
        private Vector2 velocity;
        private Vector2 startSpot;
        private Vector2 lastSpot;
        private float travelled;
        private int hits;
        private float biggestKnockback;
        private float currentKnockback;
        private float currentSize;
        private float currentSpeed = 1f;
        private int speedLevel = 1;
        private float nextPulse;
        private float nextVortex;
        private float nextDust;
        private GameObject ballObj;
        private Attack ball;
        private CircleCollider2D ballCol;
        private AudioSource loopAudio;
        private int castStopwatchID = -1;
        private int lastLoggedLevel = -1;
        private float lastLevelLog = -99f;
        private bool heldAtPitEdge;
        private bool loggedEnd;
        private string endReason;
        private static bool loggedSpeed, loggedLevels;
        // The drawn look.
        private WindBall windBall;
        private float nextStreak, nextWake, nextTrail, lastPuff;
        private int streaks, puffs;
        private float lastBallWidth;
        private static readonly HashSet<string> failed = new HashSet<string>();

        public GaleRiderState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            // A movement skill (can't be cast while rooted) and a melee one, like Tearing Whirlwind.
            isMovementSkill = true;
            isMeleeSkill = true;
            applyStopElementStatus = true;
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
            time = 0f;
            riding = false;
            done = false;
            travelled = 0f;
            hits = 0;
            biggestKnockback = 0f;
            currentKnockback = 0f;
            heldAtPitEdge = false;
            loggedEnd = false;
            endReason = "ride over";
            lastLoggedLevel = -1;
            lastLevelLog = -99f;
            nextPulse = nextVortex = nextDust = 0f;
            RideTimeNow = IsEmpowered ? GaleRiderPlugin.EnhancedRideTime : GaleRiderPlugin.RideTime;
            SpeedPercent = GaleRiderPlugin.RideSpeedPercent;
            MinSize = GaleRiderPlugin.MinSize;
            MaxSize = IsEmpowered ? GaleRiderPlugin.EnhancedMaxSize : GaleRiderPlugin.MaxSize;
            PeakSpeedFactor = Mathf.Max(BaseSpeedFactor + 0.05f, GaleRiderPlugin.PeakSpeedPercent / 100f);
            HitInterval = GaleRiderPlugin.HitInterval;
            Knockback = GaleRiderPlugin.Knockback;
            PeakKnockback = GaleRiderPlugin.PeakKnockback;
            // Set off the way the stick points (or the way they face), with the move stick, not the aim.
            direction = GetInputVector(true, false, true);
            if (direction == Vector2.zero)
                direction = Vector2.right;
            direction.Normalize();
            windBall = null;
            nextStreak = nextWake = nextTrail = 0f;
            lastPuff = -99f;
            streaks = puffs = 0;
            // The wind gathering round the wizard over the wind-up, flashing as the ball forms.
            if (GaleRiderPlugin.DrawnEffects)
            {
                Effect("gather", () =>
                {
                    FxSprite fx = FxSprite.Spawn(GaleArt.Gather, parent.transform.position, "Effect", 0);
                    if (fx == null)
                        return;
                    fx.Follow(parent.transform, new Vector2(0f, BallLift));
                    fx.fps = GatherFps;
                    fx.life = GaleArt.Gather.Length / GatherFps;
                    fx.fadeAt = 1f;
                    fx.owner = this;
                    fx.sortRef = parent.spriteRenderer;
                    fx.sortOffset = 1;
                });
            }
        }

        public override void ExecuteSkill()
        {
            if (done)
            {
                base.ExecuteSkill();
                return;
            }
            if (!riding)
            {
                riding = true;
                Mount();
            }
            // Dashing hops off at any time (OnExit takes everything back off).
            if (CancelToDash(false))
            {
                endReason = "dashed off";
                return;
            }

            time += Time.deltaTime;
            Steer();
            UpdateBall();
            Effects();

            bool hopOff = GaleRiderPlugin.HopOffOnPress && time >= GaleRiderPlugin.MinRideTime && skillSlot >= 0
                && parent.inputDevice != null && parent.inputDevice.GetButtonDown("Skill" + skillSlot);
            if (time >= RideTimeNow || hopOff)
            {
                endReason = hopOff ? "hopped off" : "ride over";
                done = true;
                Finish();
            }
        }

        // Keep the ride going through the physics steps too, and pulse the ball's collider so an
        // enemy still inside it is hit again (a trigger only fires on entering), the way Aero Ring
        // does: off and on again in one step.
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (done || !riding)
                return;
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = velocity;
            if (ballCol != null && time >= nextPulse)
            {
                nextPulse = time + HitInterval;
                ballCol.enabled = false;
                ballCol.enabled = true;
            }
        }

        public override void OnExit()
        {
            Dismount();
            base.OnExit();
        }

        // ---- Riding ----

        // The stick steers; let go and the ball rolls on the way it was going. Speed is the
        // move-speed stat with the ride mod on it. Walls stop them as usual (their body still
        // collides); a pit's edge stops them too, see KeepOffPits.
        private void Steer()
        {
            Vector2 input = GetInputVector(false, false, false);
            if (input.sqrMagnitude >= InputDeadZone)
                direction = input.normalized;
            float speed = parent.movement != null ? parent.movement.moveSpeedStat.CurrentValue : 0f;
            velocity = KeepOffPits(direction * speed);
            if (parent.movement != null)
                parent.movement.moveVector = direction;
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = velocity;
            // Run animation, turning with the ball (the game's Charge does the same).
            parent.PlayAnimOnFaceDirChange("Run", parent.transform.position + (Vector3)direction);

            Vector2 spot = parent.transform.position;
            float step = Vector2.Distance(spot, lastSpot);
            lastSpot = spot;
            if (step <= TeleportDistance)
                travelled += step;
        }

        // Rolling out over a pit would drop them in (the game checks for pits under them as they
        // go), so the pit's edge holds them like a wall would: a step that would put their feet
        // over a pit is cut to the axis that stays on the floor, or to nothing. Wizards who can
        // walk on air (the relic) ride wherever they like.
        private Vector2 KeepOffPits(Vector2 wanted)
        {
            if (wanted == Vector2.zero || CanWalkOnAir)
                return wanted;
            float step = wanted.magnitude * Mathf.Max(Time.deltaTime, Time.fixedDeltaTime) + PitEdgeMargin;
            Vector2 feet = Feet;
            if (!OverPit(feet + wanted.normalized * step))
                return wanted;
            Vector2 alongX = new Vector2(wanted.x, 0f);
            Vector2 alongY = new Vector2(0f, wanted.y);
            if (alongX != Vector2.zero && !OverPit(feet + alongX.normalized * step))
                wanted = alongX;
            else if (alongY != Vector2.zero && !OverPit(feet + alongY.normalized * step))
                wanted = alongY;
            else
                wanted = Vector2.zero;
            heldAtPitEdge = true;
            return wanted;
        }

        // Where the game checks for pits under them.
        private Vector2 Feet => parent.fall != null && parent.fall.fallCol != null
            ? (Vector2)parent.fall.fallCol.bounds.center
            : (Vector2)parent.transform.position;

        private bool CanWalkOnAir => parent.fall != null && parent.fall.ignoreFall != null && parent.fall.ignoreFall.CurrentValue;

        private static bool OverPit(Vector2 spot)
        {
            try
            {
                return ChaosCollisions.layerPit != 0 && Physics2D.OverlapPoint(spot, ChaosCollisions.layerPit) != null;
            }
            catch
            {
                return false;
            }
        }

        // ---- The ball ----

        // Size and shove from the speed read: x1.0 (base run speed) is the smallest ball and the
        // walking-pace knockback, PeakSpeedFactor the biggest and hardest, evenly between. Set on
        // the ball every frame: its collider's radius, its knockback (overriding the stat) and
        // the direction it shoves (the way they're heading).
        private void UpdateBall()
        {
            currentSpeed = SpeedFactor();
            float along = Mathf.Clamp01((currentSpeed - BaseSpeedFactor) / (PeakSpeedFactor - BaseSpeedFactor));
            currentSize = Mathf.Lerp(MinSize, MaxSize, along);
            currentKnockback = Mathf.Lerp(Knockback, PeakKnockback, along);
            speedLevel = Mathf.Clamp(1 + Mathf.FloorToInt(along * (MaxLevel - 1) + 0.001f), 1, MaxLevel);
            if (ballCol != null)
                ballCol.radius = currentSize;
            if (ball != null)
            {
                ball.transform.position = parent.attackOriginTrans.position;
                ball.knockbackMultiplierOverride = currentKnockback;
                ball.knockbackOverwriteVector = direction;
            }
            if (speedLevel != lastLoggedLevel && Time.time - lastLevelLog >= LevelLogInterval)
            {
                lastLoggedLevel = speedLevel;
                lastLevelLog = Time.time;
                GaleRiderPlugin.Log($"Gale Rider: speed x{currentSpeed:0.00} -> level {speedLevel} of {MaxLevel}: " +
                    $"ball radius {currentSize:0.00}, knockback {currentKnockback:0.#}");
            }
        }

        private void Mount()
        {
            startSpot = lastSpot = parent.transform.position;
            StartRide();
            MakeBall();
            if (!loggedLevels)
            {
                loggedLevels = true;
                GaleRiderPlugin.Log($"Gale Rider: level 1 at x{BaseSpeedFactor:0.00} base run speed, level {MaxLevel} at x{PeakSpeedFactor:0.00}; " +
                    $"ball radius {MinSize:0.0} to {MaxSize:0.0}, knockback {Knockback:0.#} to {PeakKnockback:0.#}");
            }
            try
            {
                // The ride's time on the cast bar, like the game's Charge.
                castStopwatchID = ChaosStopwatch.Begin(RideTimeNow);
                parent.hud?.castBar?.StartCast(castStopwatchID, false);
            }
            catch
            {
            }
            parent.anim?.PlayDirectional("Run" + parent.facingDirection);
            Vector2 spot = parent.transform.position;
            Dust(spot, 60, 0.3f);
            try
            {
                PoolManager.GetPoolItem<ParticleEffect>("WindTwirlEffect").Emit(new int?(1), new Vector3?(spot), null,
                    new Vector3?(new Vector3(0f, 0f, 25f)), 0f, null, null);
                PoolManager.GetPoolItem<ParticleEffect>("WindBurstEffect").Emit(new int?(3), new Vector3?(spot), null, null, 0f, null, null);
                CameraController.ShakeCamera(0.3f, false);
            }
            catch
            {
            }
            if (GaleRiderPlugin.DrawnEffects)
            {
                Effect("wind ball", () => windBall = WindBall.Create(parent, this));
                // The game's own air kick-off, as Rushing Typhoon starts its rush: a spinning
                // burst at the feet, a shockwave and a wind trail out the way they set off.
                Vector2 dir = direction;
                Effect("spinning burst", () => PoolManager.GetPoolItem<ParticleEffect>("SpinningAirBurst").Emit(new int?(1), new Vector3?(spot), null, null, 0f, null, null));
                Effect("shockwave", () => PoolManager.GetPoolItem<ParticleEffect>("AirWaveShockwave").Emit(new int?(1), new Vector3?(spot + dir * 0.5f), null,
                    new Vector3?(Globals.GetRotationVector(dir)), 0f, null, null));
                Effect("wind trail", () => PoolManager.GetPoolItem<SectionedTrailEmitter>("WindTrail").Emit(spot, spot + dir * 5f, -1, false, -1f, true, 0.3f));
            }
            SoundManager.PlayAudioWithDistance("TornadoStart", new Vector2?(spot), null, 24f, -1f, LoopPitch, false);
            try
            {
                loopAudio = SoundManager.PlayAudioWithDistance("TornadoLoop", null, parent.transform, 24f, -1f, LoopPitch, false);
            }
            catch
            {
                loopAudio = null;
            }
        }

        // The ball: the game's attack circle, parented to the wizard so it rides with them
        // (Tearing Whirlwind's), hitting with this arcana's level-1 stats. Every enemy it touches
        // is announced, for the count.
        private void MakeBall()
        {
            try
            {
                ballObj = Object.Instantiate(Attack.CirclePrefab, parent.attackOriginTrans.position, Quaternion.identity, parent.attackOriginTrans);
                ball = ballObj.GetComponent<Attack>();
                ball.SetAttackInfo(parent.skillCategory, skillID, 1, isUltimate);
                ballCol = ball.collider as CircleCollider2D ?? ballObj.GetComponent<CircleCollider2D>();
                if (ballCol != null)
                    ballCol.radius = MinSize;
                ball.knockbackOverwriteVector = direction;
                ball.knockbackMultiplierOverride = Knockback;
                ball.entityCollisionEventHandlers += OnHit;
                // Roll through enemies rather than bump into them, like Aero Ring and Whirlwind.
                parent.ToggleEnemyFloorCollisions(false);
            }
            catch (System.Exception e)
            {
                GaleRiderPlugin.Log($"Gale Rider: couldn't make the ball, riding without one: {e.Message}");
                ball = null;
                ballCol = null;
            }
        }

        private void OnHit(Entity hit)
        {
            hits++;
            if (currentKnockback > biggestKnockback)
                biggestKnockback = currentKnockback;
            try
            {
                Vector2 where = hit != null && hit.hurtBoxTransform != null ? (Vector2)hit.hurtBoxTransform.position
                    : hit != null ? (Vector2)hit.transform.position : (Vector2)parent.transform.position;
                PoolManager.GetPoolItem<ParticleEffect>("WindBurstEffect").Emit(new int?(1), new Vector3?(where), null, null, 0f, null, null);
                if (GaleRiderPlugin.DrawnEffects && time - lastPuff >= PuffInterval)
                {
                    lastPuff = time;
                    Effect("gust puff", () => GustPuff(where));
                }
            }
            catch
            {
            }
        }

        // A drawn gust puff on the enemy, swelling along the way it's shoved; bigger at speed.
        private void GustPuff(Vector2 where)
        {
            FxSprite fx = FxSprite.Spawn(GaleArt.Puff, where, "Effect", 0);
            if (fx == null)
                return;
            fx.angle = PixelArt.Angle(direction);
            fx.size = 1f + 0.06f * (speedLevel - 1);
            fx.fps = PuffFps;
            fx.life = GaleArt.Puff.Length / PuffFps;
            fx.fadeAt = 1f;
            fx.velocity = direction * 1.5f;
            fx.drag = 4f;
            puffs++;
        }

        // The ride's move-speed mod, like Aero Ring's: a multiplier on the move-speed stat (so
        // speed relics count), put on at the start and always taken off at the end.
        private void StartRide()
        {
            if (parent.movement == null)
                return;
            try
            {
                float before = parent.movement.moveSpeedStat.CurrentValue;
                parent.movement.moveSpeedStat.RemoveMod(RideSpeedModID);
                parent.movement.moveSpeedStat.AddMod(new NumVarStatMod(RideSpeedModID, SpeedPercent / 100f, 10, VarStatModType.Multiplicative));
                float after = parent.movement.moveSpeedStat.CurrentValue;
                GaleRiderPlugin.Log($"Gale Rider: cast{(IsEmpowered ? " (enhanced)" : "")}, riding {RideTimeNow:0.00}s, " +
                    $"speed +{SpeedPercent:0}% ({before:0.0} -> {after:0.0} u/s)");
            }
            catch (System.Exception e)
            {
                GaleRiderPlugin.Log($"Gale Rider: couldn't speed up the ride: {e.Message}");
            }
        }

        private void StopRide()
        {
            try
            {
                parent.movement?.moveSpeedStat.RemoveMod(RideSpeedModID);
            }
            catch
            {
            }
        }

        // The end of the ride when it runs its course or they hop off: a skid to a stop in the
        // game's slide (Whirlwind and Frost Surf end the same way), which takes over the exit.
        private void Finish()
        {
            try
            {
                if (parent.movement != null)
                    parent.movement.dashVector = direction;
                fsm.ChangeState(Player.SlideState.staticID);
            }
            catch
            {
                // No slide: end as a plain cast does.
                Dismount();
                base.ExecuteSkill();
            }
        }

        // Everything back to normal, however the ride ends (its time up, a hop-off, a dash, a hit,
        // death), and one line in the log saying how it went.
        private void Dismount()
        {
            StopRide();
            // The drawn ball and anything else riding on the wizard go with it.
            float ballWidth = windBall != null ? windBall.Width : 0f;
            if (windBall != null)
            {
                Effect("wind ball", () => windBall.Stop());
                windBall = null;
            }
            Effect("cleanup", () => FxSprite.StopAll(this));
            lastBallWidth = ballWidth;
            try
            {
                parent.ToggleEnemyFloorCollisions(true);
            }
            catch
            {
            }
            if (ball != null)
                ball.entityCollisionEventHandlers -= OnHit;
            if (ballObj != null)
                Object.Destroy(ballObj);
            ballObj = null;
            ball = null;
            ballCol = null;
            try
            {
                parent.hud?.castBar?.EndCast();
            }
            catch
            {
            }
            if (loopAudio != null)
            {
                if (loopAudio.isPlaying)
                    loopAudio.Stop();
                loopAudio = null;
            }
            if (loggedEnd)
                return;
            loggedEnd = true;
            if (!riding)
            {
                GaleRiderPlugin.Log("Gale Rider: cancelled before the ride started");
                return;
            }
            Vector2 spot = parent.transform.position;
            Dust(spot, 30, 0.25f);
            if (GaleRiderPlugin.DrawnEffects)
                Effect("unravel", () => Unravel(spot));
            SoundManager.PlayAudioWithDistance("WindBlastSharp", new Vector2?(spot), null, 24f, -1f, 1f, false);
            if (time < RideTimeNow && endReason == "ride over")
                endReason = "cut short";
            GaleRiderPlugin.Log($"Gale Rider: {endReason} after {time:0.00}s, travelled {travelled:0.0} u, hit {hits} time(s), " +
                $"biggest knockback {biggestKnockback:0.#} (last speed x{currentSpeed:0.00}, level {speedLevel})" +
                (heldAtPitEdge ? ", held back at a pit's edge" : "") +
                (GaleRiderPlugin.DrawnEffects ? $"; drawn: ball {lastBallWidth:0.0} u wide, {streaks} streak(s), {puffs} gust puff(s), {FxSprite.Live} fx sprite(s) live" : ""));
        }

        // The ball coming apart where the ride ends: the gather played backwards, sized to it.
        private void Unravel(Vector2 spot)
        {
            Sprite[] frames = GaleArt.Unravel;
            FxSprite fx = FxSprite.Spawn(frames, spot + new Vector2(0f, BallLift), "Effect", 0);
            if (fx == null)
                return;
            // The gather sheet is 48 px (3 units) wide.
            fx.size = Mathf.Clamp(lastBallWidth / 3f, 0.8f, 2f);
            fx.fps = UnravelFps;
            fx.life = frames.Length / UnravelFps;
            fx.fadeAt = 0.6f;
        }

        // ---- Speed ----

        // The wizard's run speed against its base (1 = running at base speed; more with the ride
        // mod, speed relics, the cloak and speed-boost arcana; less when slowed). Counts the
        // stat's lasting modifiers the way the game does (multiplied, then added, then
        // late-multiplied), but not the Override ones: those are the speeds a dash, a charge or
        // an attack's wind-up force on the wizard for a moment, which would swing the ball about
        // wildly. The same read as Trailblazer's.
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
                        // Already sorted by type (as Recalculate applies them).
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
                        GaleRiderPlugin.Log($"Gale Rider: run speed {speed:0.##} of base {stat.BaseValue:0.##} (x{factor:0.00}; " +
                            $"the stat reads {stat.ModifiedValue:0.##}, {overrides} override mod(s) ignored)");
                    }
                }
            }
            catch
            {
            }
            return Mathf.Clamp(factor, 0.25f, 6f);
        }

        // ---- Look ----

        // The swirling wind round the wizard, sized with the ball: the game's AirVortex puffs,
        // emitted one at a time so each takes the ball's size of the moment, and a ring of dust
        // kicked up as it rolls.
        private void Effects()
        {
            if (GaleRiderPlugin.DrawnEffects)
                DrawnEffects();
            if (time < nextVortex)
                return;
            nextVortex = time + (GaleRiderPlugin.DrawnEffects ? DrawnVortexInterval : VortexInterval);
            Vector2 spot = parent.attackOriginTrans != null ? (Vector2)parent.attackOriginTrans.position : (Vector2)parent.transform.position;
            float look = currentSize * GaleRiderPlugin.VortexSize;
            try
            {
                var vortex = new ParticleSystemOverride
                {
                    // AirVortex at startSize 2.4 covers about a radius-1 burst (Rolling Gale's sizing).
                    startSize = new float?(2.4f * look),
                    startLifetime = new float?(0.5f),
                    sortingLayer = "Trail"
                };
                PoolManager.GetPoolItem<ParticleEffect>("AirVortex").Emit(new int?(1), new Vector3?(spot + Random.insideUnitCircle * 0.15f), vortex,
                    new Vector3?(new Vector3(0f, 0f, Random.Range(0f, 360f))), 0f, null, null);
            }
            catch
            {
            }
            if (GaleRiderPlugin.Dust && time >= nextDust)
            {
                nextDust = time + DustInterval;
                Dust(parent.transform.position, Mathf.RoundToInt(8f + 8f * currentSize), 0.2f * currentSize);
            }
        }

        // The drawn ball kept to the hit's size and the speed level; wind streaks flicking off
        // behind it, dust thrown back from the feet, and from TrailLevel the game's wind trail
        // streaming behind (Rushing Typhoon's).
        private void DrawnEffects()
        {
            if (windBall != null)
            {
                WindBall ball = windBall;
                Effect("wind ball", () => ball.Update(currentSize, speedLevel, GaleRiderPlugin.VortexSize));
            }
            Vector2 feet = parent.transform.position;
            bool moving = velocity.sqrMagnitude > 0.25f;
            if (time >= nextStreak)
            {
                nextStreak = time + Mathf.Max(0.03f, StreakInterval - StreakPerLevel * (speedLevel - 1));
                Effect("wind streak", () => Streak(feet));
            }
            if (moving && GaleRiderPlugin.Dust && time >= nextWake)
            {
                nextWake = time + WakeInterval;
                Vector2 dir = direction;
                int count = 1 + speedLevel / 4;
                Effect("dust wake", () => parent.dustEmitter.EmitDirBurst(count, Globals.GetRotationVector(-dir).z, 4f, -1f, 0.45f, new Vector3?(feet - dir * 0.3f)));
            }
            if (moving && speedLevel >= TrailLevel && time >= nextTrail)
            {
                nextTrail = time + TrailInterval;
                Vector2 from = feet + new Vector2(0f, BallLift) - direction * TrailLength;
                Vector2 to = feet + new Vector2(0f, BallLift);
                Effect("wind trail", () => PoolManager.GetPoolItem<SectionedTrailEmitter>("WindTrail").Emit(from, to, -1, false, -1f, true, 0.3f));
            }
        }

        // A drawn wind streak off the back edge of the ball, pointing the way they ride and
        // drifting back; behind the wizard if it's above their middle, in front if below.
        private void Streak(Vector2 feet)
        {
            float half = windBall != null ? windBall.Width * 0.5f : currentSize;
            Vector2 side = new Vector2(-direction.y, direction.x);
            Vector2 middle = feet + new Vector2(0f, BallLift);
            Vector2 at = middle + side * (Random.Range(-0.85f, 0.85f) * half * 0.6f) - direction * (half * Random.Range(0.3f, 0.7f));
            FxSprite fx = FxSprite.Spawn(GaleArt.Streak, at, "Actor", 0);
            if (fx == null)
                return;
            fx.angle = PixelArt.Angle(direction);
            fx.fps = StreakFps;
            fx.life = GaleArt.Streak.Length / StreakFps;
            fx.fadeAt = 1f;
            fx.velocity = -direction * (StreakDrift + StreakDriftPerLevel * speedLevel);
            fx.drag = 2f;
            fx.sortRef = parent.spriteRenderer;
            fx.sortOffset = at.y > middle.y ? -1 : 1;
            streaks++;
        }

        // Runs a cosmetic effect; if it's unavailable it's skipped (and logged once) instead of
        // stopping the ride.
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
                GaleRiderPlugin.Log($"Gale Rider: effect '{name}' unavailable, skipping it: {e.Message}");
            }
        }

        private static void Dust(Vector2 position, int count, float radius)
        {
            try
            {
                PoolManager.GetPoolItem<DustEmitter>().EmitCircle(count, radius, -6f, -1f, new Vector3?(position), null);
            }
            catch
            {
            }
        }
    }
}
