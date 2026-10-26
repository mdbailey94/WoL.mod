using System.Collections.Generic;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLThunderhead
{
    // Thunderhead (Lightning, standard arcana), a leap like Heroic Leap's: after a very short
    // wind-up on the ground (still vulnerable), the wizard leaps high, straight up. While up there
    // they can't be hit, the way the game's jumping arcana work (their hurtbox is switched off, so
    // attacks and projectiles pass through, and they count as airborne), and lightning crashes
    // down around the spot below them in volleys (level 1, can shock). Then they come crashing
    // down in a lightning slam (level 2) that throws enemies back.
    //
    // Three tiers:
    // - standard: they hang still where they took off, with a volley of two strikes every 0.4 s;
    // - enhanced (IsEmpowered): they can steer the hover slowly with the stick, volleys of three
    //   come every 0.25 s, and they stay up longer over a wider circle with a bigger slam;
    // - signature (isUltimate, the game's flag for a signature cast, set on the state just before
    //   the cast and cleared when it exits; the game always empowers the signature slot, so it
    //   builds on the enhanced values): a raging storm, then a cinematic dive. The take-off is a
    //   lightning strike with a flash and a thunderclap, the stage goes dark as the storm cloud
    //   swells and strikes rain down every 0.1 s with sparks, scorch marks and small shakes; at
    //   the top the wizard holds a moment, crackling, as the camera pushes in, then dives; the
    //   impact freezes the game for a few frames under a white flash and a big shake, a ring of
    //   lightning bolts radiates out, the light floods back and time eases back from slow-motion.
    //   Skill levels 4 and 5. The camera, light and time effects are SignatureCinematic's.
    //
    // The hits are the game's lightning bursts; each strike also shows the game's own lightning
    // bolt from the sky (stripped down to its animation, so it can't hit anything itself).
    public class ThunderheadState : Player.SkillState
    {
        public new static string staticID = "Thunderhead";

        // Skill levels (SkillStats in the plugin): the strikes while up, the landing slam, and the
        // signature storm's strikes (and its ring of bolts) and slam (level 3 is the harmless bolt
        // used for the look).
        private const int StrikeLevel = 1;
        private const int SlamLevel = 2;
        private const int SignatureStrikeLevel = 4;
        private const int SignatureSlamLevel = 5;

        // The normal slam's bolt, dust, shake and hit-stop.
        private const float SlamBoltSize = 1.6f;
        private const int SlamDust = 80;
        private const float SlamShake = 1.4f;
        private const int SlamHitStop = 3;

        // The signature storm.
        private const float SignatureDiveSpeed = 0.6f;      // the dive takes this times CrashTime
        private const float SignatureHoldLift = 0.15f;      // rises this much higher (times Height) during the wind-up
        private const float CloudGrowFrom = 0.75f, CloudGrowTo = 1.25f, CloudGrowHold = 1.35f; // the cloud swelling
        private const float TakeOffBoltSize = 2f;
        private const float TakeOffShake = 0.6f;
        private const float StrikeShake = 0.15f;            // each signature volley gives a small shake
        private const int StrikeThunderEvery = 4;           // and every so-many a thunderclap
        private const float CrackleInterval = 0.04f;        // sparks round the wizard during the wind-up
        private const float SignatureSlamBoltSize = 2.6f;
        private const int SignatureSlamDust = 140;
        private const float SignatureSlamShake = 2.2f;
        private const int LightBeams = 6;                   // rays of light at the impact (the ending altar's LightBeam)
        private const float LightBeamScale = 0.75f;
        private const float LightBeamSpeed = 1.6f;
        private const float RingDelay = 0.06f;              // seconds after the impact the ring of bolts strikes
        private const float RingOutside = 0.6f;             // how far past the slam's edge the ring strikes
        private const float RingBurstSize = 1.2f;
        private const float RingShake = 0.8f;

        // Timings and sizes come from the config (ThunderheadPlugin), read as each cast starts.
        private float WindupTime, RiseTime, CrashTime, RecoverTime, Height, HoverSpeedPercent, HoldTime;
        private int VolleySize;
        private bool signature;   // this cast is the signature storm (isUltimate at cast time)
        private bool steering;    // the hover can be steered (enhanced or signature)

        private float time;
        private float nextVolley;
        private int volleyCount;
        private float spiralTurn;
        private float nextCrackle;
        private bool done;
        private bool tookOff;
        private bool windupStarted, diveStarted;
        private bool landed;
        private bool ringFired;
        private bool airborne;
        private bool hurtBoxWasOn;
        private Levitator levitator;
        private ElectricLook electric;
        private StormCloud cloud;
        private SignatureCinematic cinematic;
        // The drawn look of a standard or enhanced cast (glyphs where the bolts will land, scorch
        // marks, arcs, the landing ring and shockwave); null for the signature. The next volley's
        // strike spots are worked out a volley early (the same spread, just sooner) so the glyphs
        // can show where they'll land.
        private ThunderVisuals visuals;
        private readonly List<Vector2> plannedOffsets = new List<Vector2>();
        private readonly List<Vector2> struckSpots = new List<Vector2>();
        private int plannedVolley = -1;
        private Vector2 airVelocity;
        private Vector2 takeOffSpot;
        private Vector2 landingSpot;
        private bool heldAtPitEdge;
        private int strikesFired, strikeHits, slamHits, ringHits, ringFiredCount;
        private bool loggedLanding;
        private static bool loggedBolt;
        private static readonly HashSet<string> failedEffects = new HashSet<string>();
        private const string AirborneModID = "Thunderhead_Airborne";
        // The hover's move-speed mod, like the Charge state's: a multiplier on the move-speed stat
        // (so speed relics count), put on at take-off and always taken off when they land, cancel
        // or die.
        private const string HoverSpeedModID = "Thunderhead_Hover";
        private const float InputDeadZone = 0.04f;  // stick input (squared) below this is "not moving"
        private const float PitEdgeMargin = 0.2f;   // how far ahead of their feet a pit stops the hover

        public ThunderheadState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            // Lets the arcana into the signature slot: the spellbook's Signature page and the
            // saved loadout both require it (SpellBookUI.LoadEleSkillDict, Player.InitSkills).
            hasSignatureVariant = true;
            applyStopElementStatus = true;
            SetAnimTimes(
                0.05f, // start
                0.1f,  // hold
                0.05f, // execute
                0.4f,  // cancel
                0.5f,  // run
                0.6f); // exit
        }

        // The enhanced tier's values when enhanced or signature, the signature multipliers on top
        // for a signature cast.
        private bool EnhancedTier => IsEmpowered || signature;
        private float HoverTimeNow => (EnhancedTier ? ThunderheadPlugin.EnhancedAirTime : ThunderheadPlugin.AirTime)
            * (signature ? ThunderheadPlugin.SignatureAirTimeMultiplier : 1f);
        private float RadiusNow => (EnhancedTier ? ThunderheadPlugin.EnhancedRadius : ThunderheadPlugin.Radius)
            * (signature ? ThunderheadPlugin.SignatureRadiusMultiplier : 1f);
        private float VolleyInterval => (EnhancedTier ? ThunderheadPlugin.EnhancedVolleyInterval : ThunderheadPlugin.VolleyInterval)
            / (signature ? ThunderheadPlugin.SignatureStrikeRateMultiplier : 1f);
        private float SlamScaleNow => (EnhancedTier ? ThunderheadPlugin.EnhancedSlamSize : ThunderheadPlugin.SlamSize)
            * (signature ? ThunderheadPlugin.SignatureSlamSizeMultiplier : 1f);

        // Which version of the arcana this cast is, for the log: standard, enhanced (the relic-based
        // empowerment, IsEmpowered), signature (the gauge cast, isUltimate) or both at once.
        private string Variant()
        {
            if (IsEmpowered && signature)
                return "both: enhanced + signature";
            if (signature)
                return "signature";
            return IsEmpowered ? "enhanced" : "standard";
        }

        public override void OnEnter()
        {
            base.OnEnter();
            // The game sets isUltimate for a signature cast (the signature slot's arcana with the
            // gauge full) and clears it once the state exits, so it's read once here.
            signature = isUltimate;
            steering = EnhancedTier;
            time = 0f;
            nextVolley = 0f;
            nextCrackle = 0f;
            volleyCount = 0;
            spiralTurn = Random.value * 2f * Mathf.PI;
            done = false;
            tookOff = false;
            windupStarted = diveStarted = false;
            landed = false;
            ringFired = false;
            heldAtPitEdge = false;
            strikesFired = strikeHits = slamHits = ringHits = ringFiredCount = 0;
            loggedLanding = false;
            visuals?.Off();
            visuals = null;
            plannedVolley = -1;
            plannedOffsets.Clear();
            WindupTime = ThunderheadPlugin.WindupTime;
            RiseTime = ThunderheadPlugin.RiseTime;
            CrashTime = ThunderheadPlugin.CrashTime * (signature ? SignatureDiveSpeed : 1f);
            RecoverTime = ThunderheadPlugin.RecoverTime;
            Height = ThunderheadPlugin.Height;
            HoverSpeedPercent = ThunderheadPlugin.HoverSpeedPercent;
            HoldTime = signature ? ThunderheadPlugin.SignatureWindupHold : 0f;
            VolleySize = EnhancedTier ? ThunderheadPlugin.EnhancedVolleySize : ThunderheadPlugin.VolleySize;
            levitator = Levitator.On(parent);
        }

        public override void ExecuteSkill()
        {
            if (done)
            {
                base.ExecuteSkill();
                return;
            }
            // Dash-cancelling only before take-off, like committing to a leap.
            if (!tookOff && CancelToDash(false))
                return;

            time += Time.deltaTime;
            float t = time - WindupTime;
            float hoverEnd = RiseTime + HoverTimeNow;
            float holdEnd = hoverEnd + HoldTime;
            float crashEnd = holdEnd + CrashTime;
            // On the ground the wizard holds still; up in the air the standard leap hangs still too,
            // while the enhanced one steers slowly with the stick (and the lightning and the landing
            // follow). The signature commits at the wind-up: no steering in the hold and the dive.
            bool canSteer = steering && tookOff && !landed && (!signature || t < hoverEnd);
            airVelocity = canSteer ? HoverVelocity() : Vector2.zero;
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = airVelocity;
            Vector2 ground = parent.transform.position;

            // A moment's crouch on the ground (still vulnerable)...
            if (t < 0f)
            {
                parent.anim?.PlayDirectional(parent.JumpAnimStr, -1, 0f);
                return;
            }
            // ...then up and away.
            if (!tookOff)
            {
                tookOff = true;
                TakeOff(ground);
            }

            float top = signature ? 1f + SignatureHoldLift : 1f;
            float lift;
            if (t < RiseTime)
            {
                float r = t / RiseTime;
                lift = 1f - (1f - r) * (1f - r);     // fast off the ground, easing to the top
                parent.anim?.PlayDirectional(parent.JumpAnimStr, -1, 0.3f);
            }
            else if (t < hoverEnd)
            {
                lift = 1f;
                parent.anim?.PlayDirectional(parent.ChargeAnimStr, -1, 0.5f);
                if (cloud != null && signature)
                    cloud.Growth = Mathf.Lerp(CloudGrowFrom, CloudGrowTo, (t - RiseTime) / Mathf.Max(HoverTimeNow, 0.01f));
                if (t >= nextVolley)
                {
                    nextVolley = t + VolleyInterval;
                    Volley(ground);
                    if (visuals != null && nextVolley < hoverEnd)
                        PlanVolley(t, nextVolley, ground);
                }
            }
            else if (t < holdEnd)
            {
                // The signature's wind-up: rising a touch higher, crackling, before the dive.
                if (!windupStarted)
                    StartHold(ground);
                float h = (t - hoverEnd) / HoldTime;
                lift = 1f + SignatureHoldLift * (1f - (1f - h) * (1f - h));
                parent.anim?.PlayDirectional(parent.ChargeAnimStr, -1, 0.9f);
                if (cloud != null)
                    cloud.Growth = Mathf.Lerp(CloudGrowTo, CloudGrowHold, h);
                if (t >= nextCrackle)
                {
                    nextCrackle = t + CrackleInterval;
                    Crackle(ground);
                }
            }
            else if (t < crashEnd)
            {
                if (signature && !diveStarted)
                    StartDive(ground);
                float c = (t - holdEnd) / CrashTime;
                lift = top * (1f - c * c);             // slamming down, faster and faster
                parent.anim?.PlayDirectional(parent.GSlamAnimStr, -1, 0.2f);
            }
            else
            {
                lift = 0f;
                if (!landed)
                {
                    landed = true;
                    Slam(ground);
                }
                if (signature && !ringFired && t >= crashEnd + RingDelay)
                    Ring();
                parent.anim?.PlayDirectional(parent.GSlamAnimStr, -1, 0.6f);
            }
            electric?.Update(Mathf.Min(lift, 1f));
            cloud?.Update(Mathf.Min(lift, 1f), levitator);
            visuals?.Air(t, ground, Mathf.Min(lift, 1f), RiseTime, hoverEnd, crashEnd, levitator);
            if (levitator != null)
                levitator.height = Height * lift + (t >= RiseTime && t < hoverEnd ? Mathf.Sin(t * 7f) * 0.06f : 0f);

            if (landed && t >= crashEnd + RecoverTime)
            {
                if (signature && !ringFired)
                    Ring();
                done = true;
                Land();
                base.ExecuteSkill();
            }
        }

        // Keep the hover going through the physics steps too.
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!done && parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = airVelocity;
        }

        public override void OnExit()
        {
            Land();
            base.OnExit();
        }

        // ---- Hovering (enhanced and signature) ----

        // The stick moves the hovering wizard at their move speed with the hover mod on it (a
        // fraction of walking pace), facing the way they go. Walls stop them as usual (their body
        // still collides); a pit's edge stops them too, see KeepOffPits.
        private Vector2 HoverVelocity()
        {
            Vector2 input = GetInputVector(true, false, false);
            if (input.sqrMagnitude < InputDeadZone)
                return Vector2.zero;
            float speed = parent.movement != null ? parent.movement.moveSpeedStat.CurrentValue : 0f;
            return KeepOffPits(input.normalized * speed);
        }

        // Hovering out over a pit would drop them in when they land (the game checks for pits as
        // soon as they're not airborne), so the pit's edge holds them like a wall would: a step
        // that would put their feet over a pit is cut to the axis that stays on the floor, or to
        // nothing. Vanilla Heroic Leap only linecasts its landing spot against walls and takes the
        // fall. Wizards who can walk on air (the relic) hover wherever they like.
        private Vector2 KeepOffPits(Vector2 velocity)
        {
            if (velocity == Vector2.zero || CanWalkOnAir)
                return velocity;
            float step = velocity.magnitude * Mathf.Max(Time.deltaTime, Time.fixedDeltaTime) + PitEdgeMargin;
            Vector2 feet = Feet;
            if (!OverPit(feet + velocity.normalized * step))
                return velocity;
            Vector2 alongX = new Vector2(velocity.x, 0f);
            Vector2 alongY = new Vector2(0f, velocity.y);
            if (alongX != Vector2.zero && !OverPit(feet + alongX.normalized * step))
                velocity = alongX;
            else if (alongY != Vector2.zero && !OverPit(feet + alongY.normalized * step))
                velocity = alongY;
            else
                velocity = Vector2.zero;
            heldAtPitEdge = true;
            return velocity;
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

        // The steering's speed (enhanced and signature), and the cast's line in the log.
        private void StartHover()
        {
            string steer = "steering off (hangs still)";
            if (steering && parent.movement != null)
            {
                try
                {
                    float before = parent.movement.moveSpeedStat.CurrentValue;
                    parent.movement.moveSpeedStat.RemoveMod(HoverSpeedModID);
                    parent.movement.moveSpeedStat.AddMod(new NumVarStatMod(HoverSpeedModID, HoverSpeedPercent / 100f - 1f, 10, VarStatModType.Multiplicative));
                    float after = parent.movement.moveSpeedStat.CurrentValue;
                    steer = $"steering on at {HoverSpeedPercent:0}% of move speed ({before:0.0} -> {after:0.0} u/s)";
                }
                catch (System.Exception e)
                {
                    steer = $"steering on, but couldn't slow it: {e.Message}";
                }
            }
            ThunderheadPlugin.Log($"Thunderhead: cast ({Variant()}), " +
                $"{RiseTime + HoverTimeNow + HoldTime + CrashTime:0.00}s in the air ({RiseTime:0.00} up, {HoverTimeNow:0.00} hovering, " +
                (signature ? $"{HoldTime:0.00} wind-up, " : "") + $"{CrashTime:0.00} down), " +
                $"volleys of {VolleySize} every {VolleyInterval:0.000}s within {RadiusNow:0.0} u, slam size {SlamScaleNow:0.0}, {steer}" +
                (signature ? $", cinematic {(cinematic != null ? "on" : "off")}" : ""));
        }

        private void StopHover()
        {
            try
            {
                parent.movement?.moveSpeedStat.RemoveMod(HoverSpeedModID);
            }
            catch
            {
            }
        }

        // ---- Up and down ----

        // Leaving the ground: airborne, and nothing can touch you (hurtbox off, like a jump).
        private void TakeOff(Vector2 ground)
        {
            airborne = true;
            takeOffSpot = ground;
            try
            {
                parent.airborneStat?.AddMod(new BoolVarStatMod(AirborneModID, true, 10));
            }
            catch
            {
            }
            cinematic?.Finish("a new cast started");
            cinematic = signature && ThunderheadPlugin.Cinematic ? SignatureCinematic.Begin() : null;
            StartHover();
            if (parent.hurtBoxCollider != null)
            {
                hurtBoxWasOn = parent.hurtBoxCollider.enabled;
                parent.hurtBoxCollider.enabled = false;
            }
            if (parent.health != null)
                parent.health.invulnerable = true;
            electric?.Off();
            electric = ElectricLook.On(parent);
            cloud?.Off();
            cloud = StormCloud.On(parent, signature);
            Dust(ground, 30, 0.9f);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(ground), null, 24f, -1f, 0.6f, false);
            if (signature)
                SignatureTakeOff(ground);
            else
            {
                visuals?.Off();
                visuals = ThunderVisuals.On(parent, IsEmpowered, ThunderheadPlugin.StrikeSize, SlamScaleNow);
                visuals?.TakeOff(ground);
                // The first volley comes as they reach the top.
                if (visuals != null && HoverTimeNow > 0f)
                    PlanVolley(0f, RiseTime, ground);
            }
        }

        // The next volley's strike spots, worked out now so their glyphs can show where they'll
        // land (Volley uses these same spots when it fires).
        private void PlanVolley(float from, float until, Vector2 ground)
        {
            plannedOffsets.Clear();
            plannedVolley = volleyCount;
            for (int i = 0; i < VolleySize; i++)
                plannedOffsets.Add(StrikeOffset(i));
            visuals?.Telegraph(plannedOffsets, from, until, ground);
        }

        // The signature launches on a bolt from the sky: a flash, a thunderclap, sparks and a
        // shake, and the stage starts going dark.
        private void SignatureTakeOff(Vector2 ground)
        {
            Bolt(ground, TakeOffBoltSize);
            Effect("LightningHitEffect", 10, ground);
            Effect("BurnMarkMedEmitter", 1, ground);
            Sound("ThunderClap", ground, -1f, 0.8f);
            Shake(TakeOffShake);
            string did = cinematic != null ? cinematic.TakeOff(ThunderheadPlugin.StageDarken) : "cinematic off";
            ThunderheadPlugin.Log($"Thunderhead: signature take-off: bolt, thunder, {did}");
        }

        // The wind-up at the top: the camera pushes in, the stage darkens further, they crackle.
        private void StartHold(Vector2 ground)
        {
            windupStarted = true;
            cinematic?.Windup(ThunderheadPlugin.CameraZoom);
            Sound("ChaosLightningCrackle", ground, -1f, 0.9f);
            ThunderheadPlugin.Log($"Thunderhead: signature wind-up ({HoldTime:0.00}s){(cinematic != null ? $", zoom {ThunderheadPlugin.CameraZoom:0.00}" : "")}");
        }

        private void Crackle(Vector2 ground)
        {
            Vector2 wizard = ground + new Vector2(0f, Height * (1f + SignatureHoldLift) + 0.55f);
            Effect("LightningAuraEffect", 2, wizard + Random.insideUnitCircle * 0.5f, -Height);
            Spark(wizard + Random.insideUnitCircle * 0.7f, HitSparkType.Medium);
        }

        // The dive: a streak of lightning down to the landing spot and the camera pushing in.
        private void StartDive(Vector2 ground)
        {
            diveStarted = true;
            cinematic?.Dive();
            Sound("ElectricSwoop", ground, -1f, 1f);
            Line(ground + new Vector2(0f, Height * (1f + SignatureHoldLift)), ground);
            ThunderheadPlugin.Log($"Thunderhead: signature dive ({CrashTime:0.00}s)");
        }

        private void Touchdown()
        {
            if (!airborne)
                return;
            airborne = false;
            try
            {
                parent.airborneStat?.RemoveMod(AirborneModID);
            }
            catch
            {
            }
            StopHover();
            if (parent.hurtBoxCollider != null && hurtBoxWasOn)
                parent.hurtBoxCollider.enabled = true;
            // Always back to hurtable (restoring "already invulnerable" could leave it on for good).
            if (parent.health != null)
                parent.health.invulnerable = false;
            electric?.Off();
            electric = null;
            cloud?.Off();
            cloud = null;
        }

        // The end of the cast, however it ends (the slam, a dash-cancel, a hit, death): everything
        // back to normal, and one line in the log saying how it went.
        private void Land()
        {
            Touchdown();
            StopHover();
            electric?.Off();
            electric = null;
            cloud?.Off();
            cloud = null;
            visuals?.Off();
            visuals = null;
            if (levitator != null)
            {
                levitator.Off();
                levitator = null;
            }
            // The cinematic eases the light and the camera back by itself and puts everything back
            // when it's done (or at once on a level load).
            if (cinematic != null)
            {
                cinematic.Finish(landed ? "landed" : "cut short");
                cinematic = null;
            }
            if (loggedLanding)
                return;
            loggedLanding = true;
            if (!tookOff)
            {
                ThunderheadPlugin.Log("Thunderhead: cancelled before take-off");
                return;
            }
            float moved = Vector2.Distance(takeOffSpot, parent.transform.position);
            ThunderheadPlugin.Log($"Thunderhead: {(signature ? "signature storm " : "")}{(landed ? "landed" : "cut short in the air")} {moved:0.0} u from take-off, " +
                $"{strikesFired} strikes in {volleyCount} volleys hit {strikeHits} time(s), slam hit {slamHits} time(s)" +
                (signature ? $", ring of {ringFiredCount} bolts hit {ringHits} time(s)" : "") +
                (heldAtPitEdge ? ", held back at a pit's edge" : ""));
        }

        // Counting what the lightning hits (each burst's attack announces every enemy it touches).
        private void CountHits(LightningBurst burst, Attack.EntityCollisionEventHandler counter)
        {
            if (burst == null || burst.attack == null)
                return;
            burst.attack.entityCollisionEventHandlers += counter;
        }

        private void OnStrikeHit(Entity hit) => strikeHits++;

        private void OnSlamHit(Entity hit) => slamHits++;

        private void OnRingHit(Entity hit) => ringHits++;

        // ---- Lightning ----

        // A few strikes at once round the spot below you. Spread evenly (the default): all the
        // strikes of the whole leap are laid out as a sunflower spiral over the circle (even
        // coverage, nothing bunched up), turned a random way each cast, and each volley takes
        // every so-many of them, so every volley strikes near, middle and far all round you.
        // Otherwise each lands at random. The signature's strikes also leave sparks and scorch
        // marks, and each volley shakes the camera a little.
        private void Volley(Vector2 ground)
        {
            int size = VolleySize;
            // A standard or enhanced cast planned this volley's spots a volley early (for its
            // glyphs); otherwise they're worked out now.
            bool planned = plannedVolley == volleyCount && plannedOffsets.Count == size;
            struckSpots.Clear();
            for (int i = 0; i < size; i++)
            {
                Vector2 offset = planned ? plannedOffsets[i] : StrikeOffset(i);
                Vector2 spot = ground + new Vector2(offset.x, offset.y * 0.7f);
                struckSpots.Add(spot);
                // A signature cast's hits are marked as the game marks its own (isUltimate), so
                // they don't refill the gauge they just spent.
                CountHits(LightningBurst.CreateBurst(spot, parent.skillCategory, skillID, signature ? SignatureStrikeLevel : StrikeLevel,
                    ThunderheadPlugin.StrikeSize, signature), OnStrikeHit);
                Bolt(spot, 1f);
                if (signature)
                {
                    Effect("LightningHitEffect", 2, spot);
                    Effect("BurnMarkXSmallEmitter", 1, spot);
                }
                strikesFired++;
            }
            volleyCount++;
            plannedVolley = -1;
            Spark(ground + new Vector2(0f, Height + 0.6f), HitSparkType.Medium);
            if (signature)
            {
                Shake(StrikeShake);
                if (volleyCount % StrikeThunderEvery == 0)
                    Sound("ThunderClap", ground, 0.3f, Random.Range(0.9f, 1.1f));
            }
            else
            {
                visuals?.Struck(struckSpots);
                if (visuals != null)
                    cloud?.Pulse();
            }
        }

        // Where strike i of the current volley lands, from the spot below. Spread evenly (the
        // default) it's the volley's share of the leap's sunflower spiral, nudged a little;
        // otherwise anywhere in the circle.
        private Vector2 StrikeOffset(int i)
        {
            int size = VolleySize;
            float radius = RadiusNow;
            if (!ThunderheadPlugin.EvenSpread)
                return Random.insideUnitCircle * radius;
            int volleys = Mathf.Max(1, Mathf.CeilToInt(HoverTimeNow / VolleyInterval - 0.001f));
            int total = volleys * size;
            int j = (i * volleys + volleyCount) % total;
            float distance = radius * Mathf.Sqrt((j + 0.5f) / total);
            float angle = spiralTurn + j * GoldenAngle;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance
                + Random.insideUnitCircle * 0.08f * radius;
        }

        private const float GoldenAngle = 2.39996f; // radians

        // Crashing down: a big lightning slam that throws enemies back.
        private void Slam(Vector2 ground)
        {
            Touchdown();
            landingSpot = ground;
            float scale = SlamScaleNow;
            CountHits(LightningBurst.CreateBurst(ground, parent.skillCategory, skillID, signature ? SignatureSlamLevel : SlamLevel, scale, signature), OnSlamHit);
            Bolt(ground, signature ? SignatureSlamBoltSize : SlamBoltSize);
            Dust(ground, signature ? SignatureSlamDust : SlamDust, scale);
            // The game's own thunder (Thunder Drop's landing, Player.UseThunderDrop).
            Sound("ThunderClap", ground, -1f, signature ? 0.75f : 1f);
            if (!signature)
            {
                visuals?.Slam(ground);
                Shake(SlamShake);
                HitStop(SlamHitStop);
                return;
            }
            // The signature impact: a crater, scorch, a burst of sparks, rays of light, a big
            // shake, and the cinematic's hit-stop, flash, light and slow-motion.
            Shake(SignatureSlamShake, true);
            Effect("LightningHitEffect", 12, ground);
            Effect("LightningHitEffectLarge", 4, ground);
            Effect("FloorCrater", 1, ground);
            Effect("BurnMarkLargeEmitter", 1, ground);
            Sound("GroundSlam", ground, -1f, 0.85f);
            Beams(ground);
            string did;
            if (cinematic != null)
                did = cinematic.Impact(ThunderheadPlugin.HitStopFrames, ThunderheadPlugin.ImpactFlash, ThunderheadPlugin.ImpactSlowMo);
            else
            {
                HitStop(ThunderheadPlugin.HitStopFrames);
                did = $"hit-stop {ThunderheadPlugin.HitStopFrames} frames (cinematic off)";
            }
            ThunderheadPlugin.Log($"Thunderhead: signature impact: {did}, shake {SignatureSlamShake:0.0}, slam size {scale:0.0}");
        }

        // The signature's ring: bolts radiating out from the landing, striking just past the
        // slam's edge, each a signature strike.
        private void Ring()
        {
            ringFired = true;
            int count = ThunderheadPlugin.RingBolts;
            if (count <= 0)
                return;
            float distance = SlamScaleNow + RingOutside;
            float turn = Random.value * 2f * Mathf.PI;
            for (int i = 0; i < count; i++)
            {
                float angle = turn + i * 2f * Mathf.PI / count;
                // A true circle like the slam's own hit area, so no bolt lands inside it.
                Vector2 spot = landingSpot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                Line(landingSpot, spot);
                CountHits(LightningBurst.CreateBurst(spot, parent.skillCategory, skillID, SignatureStrikeLevel, RingBurstSize, true), OnRingHit);
                Bolt(spot, RingBurstSize);
                Effect("BurnMarkSmallEmitter", 1, spot);
                ringFiredCount++;
            }
            Shake(RingShake, true);
            ThunderheadPlugin.Log($"Thunderhead: signature ring: {count} bolts at {distance:0.0} u");
        }

        // The game's lightning bolt from the sky, just its animation.
        private static void Bolt(Vector2 position, float size)
        {
            try
            {
                if (LightningStrike.Prefab == null)
                    return;
                GameObject go = Object.Instantiate(LightningStrike.Prefab, position, Quaternion.identity);
                foreach (MonoBehaviour script in go.GetComponentsInChildren<MonoBehaviour>(true))
                    Object.Destroy(script);
                foreach (Collider2D collider in go.GetComponentsInChildren<Collider2D>(true))
                    Object.Destroy(collider);
                foreach (Animator anim in go.GetComponentsInChildren<Animator>(true))
                    anim.enabled = true;
                go.transform.localScale = new Vector3(size, size, 1f);
                if (!go.activeSelf)
                    go.SetActive(true);
                Object.Destroy(go, 0.7f);
                if (!loggedBolt)
                {
                    loggedBolt = true;
                    ThunderheadPlugin.Log("Thunderhead: using the game's lightning bolts");
                }
            }
            catch (System.Exception e)
            {
                if (!loggedBolt)
                {
                    loggedBolt = true;
                    ThunderheadPlugin.Log($"Thunderhead: the game's lightning bolt is unavailable, bursts only: {e.Message}");
                }
            }
        }

        // One of the game's pooled particle effects (GameController.CreatePools), sortOffset
        // as the game uses it to draw effects up in the air in front.
        private static void Effect(string pool, int count, Vector2 position, float? sortOffset = null)
        {
            try
            {
                PoolManager.GetPoolItem<ParticleEffect>(pool).Emit(count, (Vector3?)(Vector3)position, null, null, 0f, sortOffset);
            }
            catch (System.Exception e)
            {
                Failed(pool, e);
            }
        }

        // The game's lightning line (TeleStrike's) from one spot to another.
        private static void Line(Vector2 from, Vector2 to)
        {
            try
            {
                PoolManager.GetPoolItem<SectionedTrailEmitter>("LightningLine").Emit(from, to);
            }
            catch (System.Exception e)
            {
                Failed("LightningLine", e);
            }
        }

        // Rays of light from the impact, as the ending altar sends them out.
        private static void Beams(Vector2 position)
        {
            try
            {
                float start = Random.Range(0f, 360f);
                for (int i = 0; i < LightBeams; i++)
                {
                    AnimEffect beam = PoolManager.GetPoolItem<AnimEffect>("LightBeam");
                    if (beam == null)
                        continue;
                    float angle = start + 360f / LightBeams * i + Random.Range(-20f, 20f);
                    beam.Play(position, string.Empty, Random.Range(0f, 0.15f), default(Vector2), LightBeamSpeed, LightBeamScale, new Vector3(0f, 0f, angle));
                }
            }
            catch (System.Exception e)
            {
                Failed("LightBeam", e);
            }
        }

        private static void Sound(string clip, Vector2 position, float volume, float pitch)
        {
            try
            {
                SoundManager.PlayAudioWithDistance(clip, new Vector2?(position), null, 24f, volume, pitch, false);
            }
            catch (System.Exception e)
            {
                Failed("sound " + clip, e);
            }
        }

        private static void Failed(string what, System.Exception e)
        {
            if (failedEffects.Add(what))
                ThunderheadPlugin.Log($"Thunderhead: effect {what} unavailable: {e.Message}");
        }

        private static void Spark(Vector2 position, HitSparkType type)
        {
            try
            {
                PoolManager.GetPoolItem<HitSparkEmitter>().EmitSingle(type, position, position, null, null, 0f);
            }
            catch
            {
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

        private static void Shake(float intensity, bool ignoreShakeCount = false)
        {
            try
            {
                CameraController.ShakeCamera(intensity, ignoreShakeCount);
            }
            catch
            {
            }
        }

        private static void HitStop(int frames)
        {
            if (frames <= 0)
                return;
            try
            {
                TimeScaleController.FreezeForFrames(frames, false);
            }
            catch
            {
            }
        }
    }

    // Lifts the wizard's picture into the air (the wizard themself, their shadow and their hitbox
    // stay on the ground). Applied after the animation each frame and taken back off the next.
    //
    // The game draws things lower on screen in front, and it can judge the wizard by where their
    // picture is: lifted, they'd count as further back and slip behind statues and pillars they
    // stand in front of. So while lifted they keep the draw order of where they are on the
    // ground (following their shadow as they drift), set again just before each frame is drawn.
    public class Levitator : MonoBehaviour
    {
        public float height;
        private Vector3 applied;
        private Vector3 lastSet;
        private bool active;
        private SpriteRenderer body;
        private int groundOrder;
        private int groundLayer;
        private SpriteRenderer shadow;
        private int shadowOffset;
        private static bool loggedSorting;

        // The draw order the wizard keeps while lifted: their shadow's (which stays on the ground
        // under them and is sorted there as they drift), plus the gap the two had on the ground.
        public int DrawOrder => shadow != null ? shadow.sortingOrder + shadowOffset : groundOrder;
        public int DrawLayer => groundLayer;

        public static Levitator On(Player player)
        {
            try
            {
                if (player.spriteRenderer == null)
                    return null;
                GameObject body = player.spriteRenderer.gameObject;
                Levitator levitator = body.GetComponent<Levitator>();
                if (levitator == null)
                    levitator = body.AddComponent<Levitator>();
                levitator.enabled = true;
                levitator.height = 0f;
                levitator.body = player.spriteRenderer;
                levitator.groundOrder = player.spriteRenderer.sortingOrder;
                levitator.groundLayer = player.spriteRenderer.sortingLayerID;
                levitator.shadow = player.shadowSprite;
                if (levitator.shadow != null)
                    levitator.shadowOffset = levitator.groundOrder - levitator.shadow.sortingOrder;
                if (!loggedSorting)
                {
                    loggedSorting = true;
                    foreach (SortGenericLayer sorter in player.GetComponentsInChildren<SortGenericLayer>(true))
                        ThunderheadPlugin.Log($"Thunderhead: sorter on '{sorter.name}', by sprite transform={sorter.sortBySpriteTransform}, " +
                            $"order {player.spriteRenderer.sortingOrder}");
                }
                return levitator;
            }
            catch
            {
                return null;
            }
        }

        private void LateUpdate()
        {
            Vector3 position = transform.localPosition;
            if (active && position == lastSet)
                position -= applied;
            applied = new Vector3(0f, height, 0f);
            lastSet = position + applied;
            transform.localPosition = lastSet;
            active = true;
        }

        // Right before the frame is drawn, after the game's own sorting.
        private void OnWillRenderObject()
        {
            if (!active || body == null || height <= 0.01f)
                return;
            body.sortingLayerID = groundLayer;
            body.sortingOrder = DrawOrder;
        }

        public void Off()
        {
            if (active && transform.localPosition == lastSet)
                transform.localPosition -= applied;
            applied = Vector3.zero;
            height = 0f;
            active = false;
            enabled = false;
        }
    }
}
