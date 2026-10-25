using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLThunderhead
{
    // Thunderhead (Lightning, standard arcana), a leap like Heroic Leap's: after a very short
    // wind-up on the ground (still vulnerable), the wizard leaps high, straight up. While up there
    // they can't be hit, the way the game's jumping arcana work (their hurtbox is switched off, so
    // attacks and projectiles pass through, and they count as airborne), they hover, steering
    // slowly with the stick, and lightning crashes down around the spot below them in volleys
    // (level 1, can shock). Then they come crashing down in a lightning slam (level 2) that throws
    // enemies back.
    //
    // The hits are the game's lightning bursts; each strike also shows the game's own lightning
    // bolt from the sky (stripped down to its animation, so it can't hit anything itself).
    //
    // Cast from the signature slot with the gauge full (isUltimate, the game's flag for a signature
    // cast, set on the state just before the cast and cleared when it exits) it's a true
    // thunderstorm: twice the air time, strikes raining at twice the rate over a wider circle, and
    // a bigger landing slam that always shocks (skill levels 4 and 5). The multipliers are in the
    // config's [Signature] section and apply on top of the normal or enhanced values, so a
    // signature cast of the enhanced arcana (the usual case, the game empowers whatever sits in the
    // signature slot) gets both. The hover speed is the same in every version.
    public class ThunderheadState : Player.SkillState
    {
        public new static string staticID = "Thunderhead";

        // Skill levels (SkillStats in the plugin): the strikes while up, the landing slam, and the
        // signature storm's strikes and slam (level 3 is the harmless bolt used for the look).
        private const int StrikeLevel = 1;
        private const int SlamLevel = 2;
        private const int SignatureStrikeLevel = 4;
        private const int SignatureSlamLevel = 5;
        // The signature slam's bolt, dust and shake, against the normal slam's 1.6, 80 and 1.4.
        private const float SignatureSlamBoltSize = 2.2f;
        private const int SignatureSlamDust = 120;
        private const float SignatureSlamShake = 2f;

        // Timings and sizes come from the config (ThunderheadPlugin), read as each cast starts.
        private float WindupTime, RiseTime, CrashTime, RecoverTime, Height, HoverSpeedPercent;
        private int VolleySize;
        private bool signature;   // this cast is the signature storm (isUltimate at cast time)

        private float time;
        private float nextVolley;
        private int volleyCount;
        private float spiralTurn;
        private bool done;
        private bool tookOff;
        private bool landed;
        private bool airborne;
        private bool hurtBoxWasOn;
        private Levitator levitator;
        private ElectricLook electric;
        private StormCloud cloud;
        private Vector2 airVelocity;
        private Vector2 takeOffSpot;
        private bool heldAtPitEdge;
        private int strikesFired, strikeHits, slamHits;
        private bool loggedLanding;
        private static bool loggedBolt;
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

        // The enhanced values when enhanced, and the signature multipliers on top for a signature cast.
        private float HoverTimeNow => (IsEmpowered ? ThunderheadPlugin.EnhancedAirTime : ThunderheadPlugin.AirTime)
            * (signature ? ThunderheadPlugin.SignatureAirTimeMultiplier : 1f);
        private float RadiusNow => (IsEmpowered ? ThunderheadPlugin.EnhancedRadius : ThunderheadPlugin.Radius)
            * (signature ? ThunderheadPlugin.SignatureRadiusMultiplier : 1f);
        private float VolleyInterval => (IsEmpowered ? ThunderheadPlugin.EnhancedVolleyInterval : ThunderheadPlugin.VolleyInterval)
            / (signature ? ThunderheadPlugin.SignatureStrikeRateMultiplier : 1f);
        private float SlamScaleNow => (IsEmpowered ? ThunderheadPlugin.EnhancedSlamSize : ThunderheadPlugin.SlamSize)
            * (signature ? ThunderheadPlugin.SignatureSlamSizeMultiplier : 1f);

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

        public override void OnEnter()
        {
            base.OnEnter();
            // The game sets isUltimate for a signature cast (the signature slot's arcana with the
            // gauge full) and clears it once the state exits, so it's read once here.
            signature = isUltimate;
            time = 0f;
            nextVolley = 0f;
            volleyCount = 0;
            spiralTurn = Random.value * 2f * Mathf.PI;
            done = false;
            tookOff = false;
            landed = false;
            heldAtPitEdge = false;
            strikesFired = strikeHits = slamHits = 0;
            loggedLanding = false;
            WindupTime = ThunderheadPlugin.WindupTime;
            RiseTime = ThunderheadPlugin.RiseTime;
            CrashTime = ThunderheadPlugin.CrashTime;
            RecoverTime = ThunderheadPlugin.RecoverTime;
            Height = ThunderheadPlugin.Height;
            HoverSpeedPercent = ThunderheadPlugin.HoverSpeedPercent;
            VolleySize = ThunderheadPlugin.VolleySize;
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
            // On the ground the wizard holds still; up in the air they hover, steering slowly with
            // the stick (and the lightning and the landing follow them).
            airVelocity = tookOff && !landed ? HoverVelocity() : Vector2.zero;
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = airVelocity;
            Vector2 ground = parent.transform.position;

            // A moment's crouch on the ground (still vulnerable)...
            if (time < WindupTime)
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
            float t = time - WindupTime;
            float hoverEnd = RiseTime + HoverTimeNow;
            float crashEnd = hoverEnd + CrashTime;

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
                if (t >= nextVolley)
                {
                    nextVolley = t + VolleyInterval;
                    Volley(ground);
                }
            }
            else if (t < crashEnd)
            {
                float c = (t - hoverEnd) / CrashTime;
                lift = 1f - c * c;                     // slamming down, faster and faster
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
                parent.anim?.PlayDirectional(parent.GSlamAnimStr, -1, 0.6f);
            }
            electric?.Update(lift);
            cloud?.Update(lift, levitator);
            if (levitator != null)
                levitator.height = Height * lift + (t >= RiseTime && t < hoverEnd ? Mathf.Sin(t * 7f) * 0.06f : 0f);

            if (landed && t >= crashEnd + RecoverTime)
            {
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

        // ---- Hovering ----

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

        private void StartHover()
        {
            if (parent.movement == null)
                return;
            try
            {
                float before = parent.movement.moveSpeedStat.CurrentValue;
                parent.movement.moveSpeedStat.RemoveMod(HoverSpeedModID);
                parent.movement.moveSpeedStat.AddMod(new NumVarStatMod(HoverSpeedModID, HoverSpeedPercent / 100f - 1f, 10, VarStatModType.Multiplicative));
                float after = parent.movement.moveSpeedStat.CurrentValue;
                ThunderheadPlugin.Log($"Thunderhead: cast ({Variant()}), " +
                    $"{RiseTime + HoverTimeNow + CrashTime:0.00}s in the air ({RiseTime:0.00} up, {HoverTimeNow:0.00} hovering, {CrashTime:0.00} down), " +
                    $"volleys of {VolleySize} every {VolleyInterval:0.000}s within {RadiusNow:0.0} u, slam size {SlamScaleNow:0.0}, " +
                    $"hover speed {HoverSpeedPercent:0}% of move speed ({before:0.0} -> {after:0.0} u/s)");
            }
            catch (System.Exception e)
            {
                ThunderheadPlugin.Log($"Thunderhead: couldn't slow the hover: {e.Message}");
            }
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
            if (levitator != null)
            {
                levitator.Off();
                levitator = null;
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
                (heldAtPitEdge ? ", held back at a pit's edge" : ""));
        }

        // Counting what the lightning hits (each burst's attack announces every enemy it touches).
        private void CountHits(LightningBurst burst, bool slam)
        {
            if (burst == null || burst.attack == null)
                return;
            if (slam)
                burst.attack.entityCollisionEventHandlers += OnSlamHit;
            else
                burst.attack.entityCollisionEventHandlers += OnStrikeHit;
        }

        private void OnStrikeHit(Entity hit) => strikeHits++;

        private void OnSlamHit(Entity hit) => slamHits++;

        // ---- Lightning ----

        // A few strikes at once round the spot below you. Spread evenly (the default): all the
        // strikes of the whole leap are laid out as a sunflower spiral over the circle (even
        // coverage, nothing bunched up), turned a random way each cast, and each volley takes
        // every so-many of them, so every volley strikes near, middle and far all round you.
        // Otherwise each lands at random.
        private void Volley(Vector2 ground)
        {
            int size = VolleySize;
            float radius = RadiusNow;
            bool even = ThunderheadPlugin.EvenSpread;
            int volleys = Mathf.Max(1, Mathf.CeilToInt(HoverTimeNow / VolleyInterval - 0.001f));
            int total = volleys * size;
            for (int i = 0; i < size; i++)
            {
                Vector2 offset;
                if (even)
                {
                    int j = (i * volleys + volleyCount) % total;
                    float distance = radius * Mathf.Sqrt((j + 0.5f) / total);
                    float angle = spiralTurn + j * GoldenAngle;
                    offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance
                        + Random.insideUnitCircle * 0.08f * radius;
                }
                else
                    offset = Random.insideUnitCircle * radius;
                Vector2 spot = ground + new Vector2(offset.x, offset.y * 0.7f);
                // A signature cast's hits are marked as the game marks its own (isUltimate), so
                // they don't refill the gauge they just spent.
                CountHits(LightningBurst.CreateBurst(spot, parent.skillCategory, skillID, signature ? SignatureStrikeLevel : StrikeLevel,
                    ThunderheadPlugin.StrikeSize, signature), false);
                Bolt(spot, 1f);
                strikesFired++;
            }
            volleyCount++;
            Spark(ground + new Vector2(0f, Height + 0.6f));
        }

        private const float GoldenAngle = 2.39996f; // radians

        // Crashing down: a big lightning slam that throws enemies back.
        private void Slam(Vector2 ground)
        {
            Touchdown();
            float scale = SlamScaleNow;
            CountHits(LightningBurst.CreateBurst(ground, parent.skillCategory, skillID, signature ? SignatureSlamLevel : SlamLevel, scale, signature), true);
            Bolt(ground, signature ? SignatureSlamBoltSize : 1.6f);
            Dust(ground, signature ? SignatureSlamDust : 80, scale);
            Shake(signature ? SignatureSlamShake : 1.4f);
            try
            {
                TimeScaleController.FreezeForFrames(3, false);
            }
            catch
            {
            }
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(ground), null, 24f, -1f, 0.5f, false);
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

        private static void Spark(Vector2 position)
        {
            try
            {
                PoolManager.GetPoolItem<HitSparkEmitter>().EmitSingle(HitSparkType.Medium, position, position, null, null, 0f);
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

        private static void Shake(float intensity)
        {
            try
            {
                CameraController.ShakeCamera(intensity, false);
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
