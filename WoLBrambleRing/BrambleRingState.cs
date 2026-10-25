using System.Collections;
using System.Collections.Generic;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLBrambleRing
{
    // Slams the ground (Rippling Vines' slam) and knits a thicket of thorny vines around the
    // wizard: the game's own vine traps (VineTrap, the vines Rippling Vines and the Root Shield
    // relic raise) in two rows, each with its own grabbing hit box. The thicket stands where it
    // was cast for a few seconds, and the wizard is free to walk off. Every enemy inside it, or
    // that walks into it, is snared (the game's root status: held in place, no knockback) and
    // pricked by the thorns every half second while it stays; a vine that catches an enemy
    // crossing the ring grabs it and holds it there as well. Damage, the snare and the slow for
    // enemies that can't be snared are in SkillStats in the plugin.
    //
    // Enhanced, the thicket stands half as long again and the vines spread outward into a third,
    // wider row a moment later.
    //
    // Cast from the signature slot with the gauge full (isUltimate, the game's flag for a signature
    // cast; it's set on the state just before the cast and cleared when the state exits, so the
    // thicket remembers it) the thicket has all three rows, stands 6 s, hits harder (skill levels 3
    // and 4) and the vines tighten: every tick drags each snared enemy half a unit toward the centre
    // (moving its body the way Mortal Coil drags the enemies it has hooked; the root only stops an
    // enemy's own movement and knockback, not that). Enhanced and signature are separate flags; the
    // signature's size and time take over when both are set.
    //
    // The signature is played as a set piece, in beats (timings at the constants below):
    // 1. Windup: the slam starts from the top (as Grasping Earth plays it) and the wizard holds it
    //    raised a moment while the ground rumbles and the camera leans in.
    // 2. Slam: the game's hit-stop, a heavy shake, a crater, cracks, rocks and dust (the earth
    //    boss's landing), a punch-in of the camera, a flash of the room's light, then a short
    //    slow-down as the room falls into a green gloom.
    // 3. Eruption: the rows burst up one after another from the centre outward, each with its own
    //    ring of dust, rocks, earth waves, leaves and a shake.
    // 4. Constrict: each tick the vines drag anyone snared inward, leaves bursting off them and the
    //    screen giving a small shake.
    // 5. Final snap: at the end every vine whips shut and the thicket crushes everything inside it
    //    (skill level 5, one closing hit) with a hit-stop, a shake, a punch-in and a burst of thorns
    //    and leaves; then the vines wither and the light comes back.
    // The camera, time and light are handled by BrambleCinematics, which always puts them back.
    public class BrambleRingState : Player.SkillState
    {
        public new static string staticID = "BrambleRing";

        // Rows of vines around the cast point. Vines are about a unit across, so this knits a
        // thicket roughly 0.6 to 2.4 units out (and enemies right up against you are inside it).
        // 0.4.1 drew the standard thicket in from 1.5/2.5 (and a 3-unit reach): it was too big.
        private const float InnerRowRadius = 1.1f;
        private const float OuterRowRadius = 1.9f;
        // Enhanced: the third, wider row, and how long after the outer row is knitted it spreads.
        private const float SpreadRowRadius = 2.9f;
        private const float SpreadRowDelay = 0.3f;
        // The thicket reaches this far past its widest row: everything inside is pricked.
        private const float ThicketMargin = 0.5f;
        // Distance between neighbouring vines along a row (Rippling Vines spaces its 1.4 apart),
        // and the fewest a row gets.
        private const float VineSpacing = 1.4f;
        private const int MinVinesPerRow = 6;
        // How long the thicket stands, normal, enhanced and signature.
        private const float RingDuration = 3f;
        private const float EnhancedRingDuration = 4.5f;
        private const float SignatureRingDuration = 6f;
        // Enemies inside are pricked every TickInterval from the moment it's cast: 6 pricks over
        // 3 s, 9 enhanced, 12 signature. The first prick on an enemy (the catch) is skill level 1,
        // which also carries the slow; every prick after it is level 2. The signature thicket's
        // catch and pricks are levels 3 and 4.
        private const float TickInterval = 0.5f;
        private const int CatchLevel = 1;
        private const int PrickLevel = 2;
        private const int SignatureCatchLevel = 3;
        private const int SignaturePrickLevel = 4;
        // Signature: how far each tick drags a snared enemy toward the centre, how close to the
        // centre the dragging stops, and the clear ground a dragged enemy needs at its new spot
        // (so it's never pulled into a wall or an obstacle).
        private const float SignaturePullPerTick = 0.5f;
        private const float PullStopRadius = 0.5f;
        private const float PullClearance = 0.3f;
        // Vines knit, and later wither, one after another this far apart, so the ring runs round.
        private const float VineStagger = 0.02f;
        // The outer row starts this long after the inner one.
        private const float RowInterval = 0.12f;
        // The slam: dust kicked up around the wizard (Rippling Vines' cast dust) and a shake.
        private const int CastDust = 100;
        private const float CastDustScale = 0.25f;
        private const float CastShake = 0.6f;
        // Leaves thrown up from an enemy at each prick.
        private const int PrickLeaves = 1;
        // The ID the game gives its root status (Health.HandleRootStatusEffect).
        private const string RootStatusID = "RootStatus";

        // ---- Signature beats ----
        // Windup: the slam is played from the very start (normalized time 0, as Grasping Earth
        // plays it) and held at the raised pose (the hold point, 0.2) this many seconds, with a
        // rumble, a little dust and leaves, a light shake and the camera leaning in to this size.
        private const float SignatureAnimStart = 0f;
        private const float SignatureWindup = 0.25f;
        private const float WindupZoom = 0.95f;
        private const int WindupDust = 40;
        private const float WindupDustScale = 0.75f;
        private const float WindupDustSpeed = 4f;
        private const int WindupLeaves = 6;
        private const float WindupShake = 0.3f;
        // Slam: the game's hit-stop for this many frames, then a slow-down from this time scale
        // easing back to full speed over this many real seconds (the eruption ripples out in it).
        private const int SlamFreezeFrames = 6;
        private const float SlamSlowTime = 0.4f;
        private const float SlamSlowScale = 0.35f;
        // A heavy shake (the game's default is 1) and a camera punch-in to this size, in over
        // SlamZoomIn, held SlamZoomHold, back out over SlamZoomOut (real seconds).
        private const float SlamShake = 1.5f;
        private const float SlamZoom = 0.88f;
        private const float SlamZoomIn = 0.04f;
        private const float SlamZoomHold = 0.15f;
        private const float SlamZoomOut = 0.45f;
        // The earth boss's landing dust (count, circle scale, speed) and rocks thrown up at the centre.
        private const int SlamDust = 100;
        private const float SlamDustScale = 1.5f;
        private const float SlamDustSpeed = 25f;
        private const int SlamRocks = 14;
        // Eruption: each row bursts up this long after the one inside it, every vine throwing up
        // rocks, leaves and an earth wave, each row a ring of dust and its own (smaller) shake.
        private const float EruptionRowInterval = 0.08f;
        private const int EruptRocksPerVine = 3;
        private const int EruptLeavesPerVine = 3;
        private const int EruptRowDust = 50;
        private static readonly float[] EruptRowDustScale = { 0.5f, 1f, 1.5f };
        private static readonly float[] EruptRowDustSpeed = { 10f, 14f, 18f };
        private static readonly float[] EruptRowShake = { 0.6f, 0.45f, 0.3f };
        private static readonly float[] EruptRowPitch = { 0.8f, 0.9f, 1f };
        // Constrict: a small shake and leaves off every enemy dragged, each tick anyone is dragged.
        private const float ConstrictShake = 0.25f;
        private const int ConstrictLeaves = 4;
        private const float ConstrictPitch = 0.85f;
        // Final snap: the vines whip shut, SnapSqueeze later the crush lands (skill level 5, once on
        // everything inside), with a hit-stop and slow-down like the slam's but shorter, a shake, a
        // punch-in and a burst of thorns, leaves and rocks; SnapLinger later the vines wither.
        private const int SnapLevel = 5;
        private const float SnapSqueeze = 0.12f;
        private const int SnapFreezeFrames = 7;
        private const float SnapSlowTime = 0.25f;
        private const float SnapSlowScale = 0.4f;
        private const float SnapShake = 1.2f;
        private const float SnapZoom = 0.92f;
        private const float SnapZoomIn = 0.03f;
        private const float SnapZoomHold = 0.12f;
        private const float SnapZoomOut = 0.4f;
        private const int SnapThorns = 30;
        private const int SnapThornsPerEnemy = 10;
        private const int SnapLeaves = 6;
        private const int SnapRocks = 8;
        private const float SnapLinger = 0.15f;
        // The gloom is let go at the latest this many real seconds after the thicket should have
        // withered, whatever happens (BrambleCinematics also lets go if the thicket stops checking in).
        private const float GloomSafety = 4f;

        private static readonly HashSet<string> failedEffects = new HashSet<string>();

        // The signature windup of the cast in progress (this state is reused for every cast).
        private bool signatureCast;
        private float windupStart = -1f;
        private bool windupDone = true;
        private int enterFrame;

        public override string OnEnterAnimStr => parent.GSlamAnimStr;

        public BrambleRingState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            // Lets the arcana into the signature slot: the spellbook's Signature page and the
            // saved loadout both require it (SpellBookUI.LoadEleSkillDict, Player.InitSkills).
            hasSignatureVariant = true;
            isMeleeSkill = true;
            applyStopElementStatus = true;
            // Rippling Vines' timings: the slam lands at 0.3, and you can move again from 0.75.
            SetAnimTimes(
                0.2f,  // start
                0.2f,  // hold
                0.3f,  // execute
                0.75f, // cancel
                0.9f); // run
        }

        public override void OnEnter()
        {
            // isUltimate is set before the state is entered (Player.HandleOverdrive).
            signatureCast = isUltimate;
            windupStart = -1f;
            windupDone = !signatureCast;
            enterFrame = Time.frameCount;
            base.OnEnter();
            if (signatureCast)
                parent.anim.PlayDirectional(OnEnterAnimStr, -1, SignatureAnimStart);
        }

        // Signature: holds the slam raised for the windup before the game's own update plays it
        // down to the execute point. Normal and enhanced casts go straight to the game's.
        public override void Update()
        {
            if (!windupDone && !skillExecuted)
            {
                // Not on the frame the slam was started (the animator may still report the
                // previous animation's time until it updates).
                if (windupStart < 0f && Time.frameCount > enterFrame && parent.anim.AnimPlayed(animHoldTime))
                {
                    windupStart = Time.time;
                    WindupBeat();
                }
                if (windupStart >= 0f)
                {
                    if (Time.time - windupStart < SignatureWindup)
                    {
                        UpdateRepeatLock(string.Empty);
                        parent.anim.PlayDirectional(OnEnterAnimStr, -1, animHoldTime);
                        return;
                    }
                    windupDone = true;
                }
            }
            base.Update();
        }

        public override void OnExit()
        {
            base.OnExit();
            // Knocked out of the cast before the slam: nothing was spawned, only the windup's
            // camera lean to undo.
            if (signatureCast && !skillExecuted)
            {
                Log("signature: cast interrupted before the slam");
                Cine(c => c.ZoomBack("the cast was interrupted"));
            }
            signatureCast = false;
            windupDone = true;
        }

        public override void ExecuteSkill()
        {
            base.ExecuteSkill();

            // The thicket stays where you slammed, even once you've walked off. isUltimate is the
            // game's flag for a signature cast (the signature slot's arcana with the gauge full);
            // it's cleared once this state exits, so the ring keeps its own copy.
            var ring = new Ring((Vector2)parent.transform.position, IsEmpowered, isUltimate);
            Log($"cast ({ring.Variant}) at ({ring.center.x:0.0}, {ring.center.y:0.0})");

            // Run on the wizard, so the thicket outlives this state.
            if (ring.signature)
            {
                ring.levelAtCast = BrambleCinematics.LevelLoads;
                SlamBeat(ring);
                parent.StartCoroutine(Erupt(ring));
            }
            else
                parent.StartCoroutine(Knit(ring));
            parent.StartCoroutine(Stand(ring));
        }

        // One cast's thicket.
        private class Ring
        {
            public readonly Vector2 center;
            public readonly bool enhanced;
            public readonly bool signature;
            public float radius;                  // how far out counts as inside
            public bool ended;
            public BrambleVines vines;
            public readonly HashSet<Entity> caught = new HashSet<Entity>();   // every enemy pricked
            public readonly HashSet<Entity> grabbed = new HashSet<Entity>();  // those a vine hit first
            public readonly List<string> names = new List<string>();
            public int vineHits;                  // hits by the vines' own hit boxes
            public int pricks;                    // pricks dealt by the ticks (the vines' hits aside)
            public int prickDamage;               // their damage before the enemy's armour
            public int pulls;                     // signature: nudges toward the centre
            public float pulled;                  // and how far they added up to
            public int constricts;                // signature: ticks that dragged anyone
            public int levelAtCast;               // signature: BrambleCinematics.LevelLoads at the cast
            public bool aborted;                  // signature: left behind by a level load
            public BrambleCinematics.Hold gloom;  // signature: its hold on the room's light
            private readonly Dictionary<int, AttackInfo> infos = new Dictionary<int, AttackInfo>();

            public Ring(Vector2 center, bool enhanced, bool signature)
            {
                this.center = center;
                this.enhanced = enhanced;
                this.signature = signature;
                // The signature thicket is knitted at its full, enhanced size straight away.
                radius = (signature ? SpreadRowRadius : OuterRowRadius) + ThicketMargin;
            }

            public float Duration => signature ? SignatureRingDuration : enhanced ? EnhancedRingDuration : RingDuration;
            public int CatchLevel => signature ? SignatureCatchLevel : BrambleRingState.CatchLevel;
            public int PrickLevel => signature ? SignaturePrickLevel : BrambleRingState.PrickLevel;

            // Which version of the arcana this cast is, for the log: normal, enhanced (the
            // relic-based empowerment, IsEmpowered), signature (the gauge cast, isUltimate) or both.
            public string Variant => enhanced && signature ? "both: enhanced + signature" : signature ? "signature" : enhanced ? "enhanced" : "normal";

            // The skill's attack at a level, as the game would generate it for a hit box. A
            // signature cast's hits are marked as the game marks its own (isUltimate), so they
            // don't refill the gauge they just spent.
            public AttackInfo Info(Player player, string skillID, int level)
            {
                AttackInfo info;
                if (!infos.TryGetValue(level, out info))
                {
                    info = AttackInfo.GetInfo(player, player.skillCategory, skillID, level, signature);
                    infos[level] = info;
                }
                return info;
            }
        }

        // Normal and enhanced: raises the rows of vines, one vine after another round the ring:
        // the inner row, the outer row a moment later and, enhanced, the wider third row after
        // that. (The signature erupts instead, see Erupt.)
        private IEnumerator Knit(Ring ring)
        {
            ring.vines = NewVines(ring);
            CastEffects(ring.center);

            // A random turn for each cast, so the vines don't always stand in the same places.
            float spin = Random.Range(0f, 360f);
            var positions = new List<Vector2>();
            int blocked = BrambleVines.RowPositions(ring.center, InnerRowRadius, spin, VineSpacing, MinVinesPerRow, false, positions);
            yield return parent.StartCoroutine(RaiseRow(ring, positions));
            yield return new WaitForSeconds(RowInterval);
            positions.Clear();
            blocked += BrambleVines.RowPositions(ring.center, OuterRowRadius, spin, VineSpacing, MinVinesPerRow, true, positions);
            yield return parent.StartCoroutine(RaiseRow(ring, positions));
            Log($"knitted {ring.vines.Count} vines in 2 rows {InnerRowRadius:0.0}-{OuterRowRadius:0.0} out" +
                (blocked > 0 ? $" ({blocked} skipped in walls or pits)" : ""));

            if (!ring.enhanced)
                yield break;
            yield return new WaitForSeconds(SpreadRowDelay);
            if (ring.ended)
                yield break;
            int before = ring.vines.Count;
            positions.Clear();
            blocked = BrambleVines.RowPositions(ring.center, SpreadRowRadius, spin, VineSpacing, MinVinesPerRow, false, positions);
            ring.radius = SpreadRowRadius + ThicketMargin;
            Effect("sound", () => SoundManager.PlayAudioWithDistance("VineWaveEnd", ring.center, null, 24f, -1f, 0.9f));
            yield return parent.StartCoroutine(RaiseRow(ring, positions));
            Log($"vines spread outward: {ring.vines.Count - before} more at {SpreadRowRadius:0.0} out" +
                (blocked > 0 ? $" ({blocked} skipped in walls or pits)" : ""));
        }

        // The thicket's vines; each one that catches an enemy counts it as caught.
        private BrambleVines NewVines(Ring ring)
        {
            return new BrambleVines(parent, skillID, ring.CatchLevel, ring.signature, entity =>
            {
                ring.vineHits++;
                if (!ring.ended && ring.caught.Add(entity))
                {
                    ring.grabbed.Add(entity);
                    Remember(ring, entity);
                }
            });
        }

        // Signature: the three rows burst up as a wave from the centre outward, each row all at
        // once, EruptionRowInterval apart (stretched by the slam's slow-down), every vine throwing
        // up rocks, leaves and an earth wave (Dragon Stomp's), every row its own ring of dust and
        // a shake that weakens as the wave spreads.
        private IEnumerator Erupt(Ring ring)
        {
            ring.vines = NewVines(ring);
            float spin = Random.Range(0f, 360f);
            float[] rows = { InnerRowRadius, OuterRowRadius, SpreadRowRadius };
            var positions = new List<Vector2>();
            int blocked = 0;
            for (int row = 0; row < rows.Length; row++)
            {
                if (ring.ended)
                    yield break;
                positions.Clear();
                // The middle row sits between the others' vines, as the knitted rows do.
                blocked += BrambleVines.RowPositions(ring.center, rows[row], spin, VineSpacing, MinVinesPerRow, row == 1, positions);
                foreach (Vector2 position in positions)
                {
                    if (ring.vines.Raise(position) == null)
                        continue;
                    Vector2 at = position;
                    Effect("erupt rocks", () => PoolManager.GetPoolItem<RockDebrisEmitter>().EmitSingle(EruptRocksPerVine, at,
                        RockDebrisEmitter.SmallCircleExplosionOverride));
                    Effect("erupt wave", () => PoolManager.GetPoolItem<ParticleEffect>("EarthWaveEffect").Emit(1, at));
                    Effect("erupt leaves", () => PoolManager.GetPoolItem<LeafEmitter>().EmitSingle(EruptLeavesPerVine,
                        at + new Vector2(0f, 0.5f), ParticleEffectOverrides.Radius0p5, null, 0f, null, LeafEmitter.LeafColor.Green));
                }
                int r = row;
                Effect("erupt dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(EruptRowDust, EruptRowDustScale[r],
                    EruptRowDustSpeed[r], -1f, ring.center));
                Effect("erupt shake", () => CameraController.ShakeCamera(EruptRowShake[r], true));
                Effect("erupt sound", () => SoundManager.PlayAudioWithDistance("RockForm", ring.center, null, 24f, -1f, EruptRowPitch[r]));
                if (row < rows.Length - 1)
                    yield return new WaitForSeconds(EruptionRowInterval);
            }
            Effect("sound", () => SoundManager.PlayAudioWithDistance("VineWaveEnd", ring.center, null, 24f, -1f, 0.9f));
            Log($"signature: vines erupted outward in {rows.Length} rows {InnerRowRadius:0.0}-{SpreadRowRadius:0.0} out, " +
                $"{EruptionRowInterval * (rows.Length - 1):0.00} s centre to edge: {ring.vines.Count} vines" +
                (blocked > 0 ? $" ({blocked} skipped in walls or pits)" : ""));
        }

        private IEnumerator RaiseRow(Ring ring, List<Vector2> positions)
        {
            foreach (Vector2 position in positions)
            {
                if (ring.ended)
                    yield break;
                ring.vines.Raise(position);
                if (VineStagger > 0f)
                    yield return new WaitForSeconds(VineStagger);
            }
        }

        // The thicket stands for its duration, pricking everything inside every tick, then
        // withers away.
        private IEnumerator Stand(Ring ring)
        {
            float duration = ring.Duration;
            float end = Time.time + duration;
            float nextTick = Time.time;
            while (Time.time < end)
            {
                if (ring.signature)
                {
                    // Tells the gloom the thicket still stands; a level load leaves it behind.
                    ring.gloom?.Beat();
                    if (BrambleCinematics.LevelLoads != ring.levelAtCast)
                    {
                        ring.aborted = true;
                        Log("signature: thicket left behind by a level load, ending it without the final snap");
                        break;
                    }
                }
                if (Time.time >= nextTick)
                {
                    nextTick += TickInterval;
                    Tick(ring);
                }
                yield return null;
            }
            ring.ended = true;
            if (ring.signature && !ring.aborted)
                yield return parent.StartCoroutine(FinalSnap(ring));
            // The light comes back while the vines wither.
            ring.gloom?.Release();
            if (ring.vines != null)
                yield return parent.StartCoroutine(ring.vines.Unravel(VineStagger));
            LogEnd(ring, duration);
        }

        // Pricks every enemy inside the thicket. Newly caught enemies get the catch hit (level 1,
        // with the slow) unless a vine already hit them; enemies the game won't let anything grab
        // (bosses) get the catch hit every time, so the slow keeps up in place of the snare. The
        // signature thicket also drags every snared enemy a step toward the centre.
        private void Tick(Ring ring)
        {
            List<Vector2> dragged = null;
            foreach (Enemy enemy in EnemiesInside(ring.center, ring.radius))
            {
                bool first = ring.caught.Add(enemy);
                bool byVine = ring.grabbed.Contains(enemy);
                bool cannotSnare = enemy.health != null && enemy.health.ignoreGrab;
                int level = (first && !byVine) || cannotSnare ? ring.CatchLevel : ring.PrickLevel;
                if (first)
                {
                    Remember(ring, enemy);
                    // Stop a dash or a charge short, as Grasping Earth does.
                    try
                    {
                        if (enemy.movement != null)
                            enemy.movement.EndMovement();
                    }
                    catch
                    {
                    }
                }
                Prick(ring, enemy, level);
                if (ring.signature && !cannotSnare && Tighten(ring, enemy))
                    (dragged ?? (dragged = new List<Vector2>())).Add(HitPoint(enemy));
                if (first)
                    Log($"caught {enemy.name}{(byVine ? " in a vine" : "")}, {Status(enemy)}");
            }
            if (dragged != null)
                Constrict(ring, dragged);
        }

        // Signature: the vines visibly tighten on a tick that dragged anyone: leaves burst off
        // every dragged enemy, the vines creak (the vine trap's whip, pitched down) and the screen
        // gives a small shake.
        private static void Constrict(Ring ring, List<Vector2> dragged)
        {
            ring.constricts++;
            foreach (Vector2 at in dragged)
            {
                Vector2 spot = at;
                Effect("constrict leaves", () => PoolManager.GetPoolItem<LeafEmitter>().EmitSingle(ConstrictLeaves, spot,
                    ParticleEffectOverrides.Radius0p5, null, 0f, null, LeafEmitter.LeafColor.Green));
            }
            Effect("constrict shake", () => CameraController.ShakeCamera(ConstrictShake));
            Effect("constrict sound", () => SoundManager.PlayAudioWithDistance("VineWhip", ring.center, null, 24f, -1f, ConstrictPitch));
            Log($"signature: constrict pulse {ring.constricts}, dragged {dragged.Count} inward");
        }

        // The vines tighten: the snared enemy's body is moved SignaturePullPerTick toward the
        // centre (its rigidbody, the way Mortal Coil drags the enemies it has hooked: the game's
        // root zeroes an enemy's own movement and ignores knockback, so a shove wouldn't do it),
        // but never into a wall or an obstacle, and not once it's at the centre. Enemies the game
        // won't let anything grab (bosses) aren't dragged either, as they aren't snared.
        // Returns whether it was dragged.
        private static bool Tighten(Ring ring, Enemy enemy)
        {
            Vector2 from = enemy.transform.position;
            Vector2 toCenter = ring.center - from;
            if (toCenter.magnitude <= PullStopRadius)
                return false;
            float step = Mathf.Min(SignaturePullPerTick, toCenter.magnitude - PullStopRadius);
            Vector2 to = from + toCenter.normalized * step;
            try
            {
                if (!Globals.CheckCircle(to, PullClearance, ChaosCollisions.layerUpperWallAndObst))
                    return false;
                if (enemy.rigidbody2D != null)
                    enemy.rigidbody2D.MovePosition(to);
                else
                    enemy.transform.position = to;
                ring.pulls++;
                ring.pulled += step;
                return true;
            }
            catch (System.Exception e)
            {
                LogOnce("pull", $"couldn't drag {enemy.name} toward the centre: {e.Message}");
                return false;
            }
        }

        // One thorn prick: the skill's own hit at the given level, dealt straight to the enemy
        // (the way Grasping Earth deals its hits), so the game applies the snare and the slow.
        private void Prick(Ring ring, Enemy enemy, int level)
        {
            int damage = Strike(ring, enemy, level, "prick");
            if (damage < 0)
                return;
            ring.pricks++;
            ring.prickDamage += damage;
            Vector2 at = HitPoint(enemy);
            Effect("leaves", () => PoolManager.GetPoolItem<LeafEmitter>().EmitSingle(PrickLeaves, at, null, null, 0f, null,
                LeafEmitter.LeafColor.Green));
        }

        // The skill's own hit at a level, dealt straight to the enemy, coming from the thicket's
        // centre with no shove. Returns its damage before armour, or -1 if it couldn't be dealt.
        private int Strike(Ring ring, Enemy enemy, int level, string what)
        {
            if (enemy.health == null)
                return -1;
            AttackInfo info = ring.Info(parent, skillID, level);
            if (info == null)
            {
                LogOnce("stats" + level, $"no stats for level {level}, dealing no {what}");
                return -1;
            }
            try
            {
                // Hurt reactions and hit sparks come from the thicket's centre; no shove.
                info.attackerPosition = ring.center;
                info.knockbackVector = Vector2.zero;
                enemy.health.TakeDamage(info, parent);
                return info.damage;
            }
            catch (System.Exception e)
            {
                LogOnce(what, $"couldn't {what} {enemy.name}: {e.Message}");
                return -1;
            }
        }

        // Where an enemy's body is (its hurt box), for effects on it.
        private static Vector2 HitPoint(Enemy enemy)
        {
            try
            {
                return enemy.hurtBoxTransform != null ? (Vector2)enemy.hurtBoxTransform.position : (Vector2)enemy.transform.position;
            }
            catch
            {
                return Vector2.zero;
            }
        }

        // ---- Signature beats ----

        // Windup: a rumble, dust and leaves stirring round the wizard, a light shake and the camera
        // leaning in while the slam is held raised.
        private void WindupBeat()
        {
            Vector2 at = parent.transform.position;
            Effect("windup sound", () => SoundManager.PlayAudioWithDistance("EarthRumble", at));
            Effect("windup dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(WindupDust, WindupDustScale, WindupDustSpeed, -1f, at));
            Effect("windup leaves", () => PoolManager.GetPoolItem<LeafEmitter>().EmitSingle(WindupLeaves, at + new Vector2(0f, 0.5f),
                ParticleEffectOverrides.RadiusOne, null, 0f, null, LeafEmitter.LeafColor.Green));
            Effect("windup shake", () => CameraController.ShakeCamera(WindupShake));
            Cine(c => c.Zoom(parent, WindupZoom, SignatureWindup, -1f, 0f));
            Log($"signature: windup, holding the slam {SignatureWindup:0.00} s");
        }

        // Slam: the earth boss's landing (a large crater, cracks, rocks, a burst of dust, its
        // explosion and the ground slam sound), the game's hit-stop into a short slow-down, a heavy
        // shake, a camera punch-in, a flash of the room's light and the gloom falling.
        private void SlamBeat(Ring ring)
        {
            Vector2 c = ring.center;
            Effect("slam crater", () => PoolManager.GetPoolItem<ParticleEffect>("FloorCraterLarge").Emit(1, c));
            Effect("slam crack", () => PoolManager.GetPoolItem<FloorCrackEmitter>().Emit(1, c));
            Effect("slam rocks", () => PoolManager.GetPoolItem<RockMedDebrisEmitter>().EmitSingle(SlamRocks, c,
                RockMedDebrisEmitter.FloorExplosionOverride));
            Effect("slam dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(SlamDust, SlamDustScale, SlamDustSpeed, -1f, c));
            Effect("slam sound", () => SoundManager.PlayAudioWithDistance("EarthExplosion", c));
            Effect("slam sound 2", () => SoundManager.PlayAudioWithDistance("GroundSlam", c));
            Effect("slam shake", () => CameraController.ShakeCamera(SlamShake, true));
            Cine(cine =>
            {
                cine.HitStop(SlamFreezeFrames, SlamSlowTime, SlamSlowScale);
                cine.Zoom(parent, SlamZoom, SlamZoomIn, SlamZoomHold, SlamZoomOut);
                ring.gloom = cine.BeginGloom(parent, ring.Duration + GloomSafety);
                cine.Flare();
            });
            Log($"signature: the slam lands, hit-stop {SlamFreezeFrames} frames into a {SlamSlowTime:0.0} s slow-down, " +
                $"shake, camera punch-in to {SlamZoom:0.00}, the earth cracks");
        }

        // Final snap: every vine whips shut, then the thicket crushes everything inside it (level 5,
        // bosses too) with a hit-stop, a shake, a punch-in, a flash and a burst of thorns (the
        // exploding boulder's), leaves and rocks; a moment later the vines wither.
        private IEnumerator FinalSnap(Ring ring)
        {
            if (parent == null || parent.IsDead)
            {
                Log("signature: no final snap, the wizard is down");
                yield break;
            }
            int snapped = ring.vines != null ? ring.vines.SnapShut() : 0;
            Effect("snap sound", () => SoundManager.PlayAudioWithDistance("VineWhip", ring.center, null, 24f, -1f, 0.7f));
            yield return new WaitForSeconds(SnapSqueeze);

            int crushed = 0, damage = 0;
            foreach (Enemy enemy in EnemiesInside(ring.center, ring.radius))
            {
                int dealt = Strike(ring, enemy, SnapLevel, "crush");
                if (dealt < 0)
                    continue;
                crushed++;
                damage += dealt;
                Vector2 at = HitPoint(enemy);
                Effect("snap thorns", () => PoolManager.GetPoolItem<ParticleEffect>("ThornEmitter").Emit(SnapThornsPerEnemy, at,
                    ParticleEffectOverrides.Radius0p25));
            }
            Vector2 c = ring.center;
            Effect("snap thorns", () => PoolManager.GetPoolItem<ParticleEffect>("ThornEmitter").Emit(SnapThorns, c));
            Effect("snap leaves", () => PoolManager.GetPoolItem<LeafEmitter>("FallingLeafEmitter").EmitSingle(SnapLeaves, c + Vector2.up,
                ParticleEffectOverrides.RadiusTwo));
            Effect("snap rocks", () => PoolManager.GetPoolItem<RockDebrisEmitter>().EmitSingle(SnapRocks, c, RockDebrisEmitter.CircleExplosionOverride));
            Effect("snap crack", () => PoolManager.GetPoolItem<FloorCrackEmitter>().Emit(1, c));
            Effect("snap sound 2", () => SoundManager.PlayAudioWithDistance("EarthExplosion", c));
            Effect("snap sound 3", () => SoundManager.PlayWithDistAndSPR("PopSnap", c));
            Effect("snap shake", () => CameraController.ShakeCamera(SnapShake, true));
            Cine(cine =>
            {
                cine.HitStop(SnapFreezeFrames, SnapSlowTime, SnapSlowScale);
                cine.Zoom(parent, SnapZoom, SnapZoomIn, SnapZoomHold, SnapZoomOut);
                cine.Flare();
            });
            Log($"signature: final snap, {snapped} vines whipped shut, crushed {crushed} " +
                $"{(crushed == 1 ? "enemy" : "enemies")} for {damage} damage before armour");
            yield return new WaitForSeconds(SnapLinger);
        }

        // Runs a camera, time or light beat; the cinematics never stop the thicket.
        private static void Cine(System.Action<BrambleCinematics> beat)
        {
            try
            {
                beat(BrambleCinematics.Instance);
            }
            catch (System.Exception e)
            {
                LogOnce("cine", $"signature: cinematics unavailable, skipping them: {e.Message}");
            }
        }

        // Live enemies (not allies) with any collider inside the circle.
        private static List<Enemy> EnemiesInside(Vector2 center, float radius)
        {
            var found = new List<Enemy>();
            foreach (Collider2D col in Physics2D.OverlapCircleAll(center, radius))
            {
                Enemy enemy = col != null ? col.GetComponentInParent<Enemy>() : null;
                if (enemy == null || !enemy.gameObject.activeInHierarchy || found.Contains(enemy))
                    continue;
                if (enemy.health == null || enemy.health.CurrentHealthValue <= 0)
                    continue;
                if (GameController.allies != null && GameController.allies.Contains(enemy.gameObject))
                    continue;
                found.Add(enemy);
            }
            return found;
        }

        private static void Remember(Ring ring, Entity entity)
        {
            if (ring.names.Count < 6)
                ring.names.Add(entity != null ? entity.name : "?");
        }

        // What the catch did to an enemy: snared (the game's root), or only slowed (bosses and
        // anything else that can't be grabbed), or neither (status resistance, or it died).
        private static string Status(Entity entity)
        {
            if (entity.health != null && entity.health.CurrentHealthValue <= 0)
                return "killed";
            if (entity.dotManager != null && entity.dotManager.HasDot(RootStatusID))
                return "snared";
            if (entity.dotManager != null && entity.dotManager.HasDot(SlowDotEffect.slowStatusStr))
                return "slowed (can't be snared)";
            return "hit only";
        }

        // The slam: dust around the wizard, a shake and the vines' sound (Rippling Vines' cast).
        private static void CastEffects(Vector2 center)
        {
            Effect("dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(CastDust, CastDustScale, -1f, -1f, center));
            Effect("camera shake", () => CameraController.ShakeCamera(CastShake));
            Effect("sound", () => SoundManager.PlayAudioWithDistance("VineWaveEnd", center));
        }

        private static void LogEnd(Ring ring, float duration)
        {
            if (ring.caught.Count == 0)
            {
                Log($"thicket withered after {duration:0.0} s, caught nothing");
                return;
            }
            string names = string.Join(", ", ring.names.ToArray()) + (ring.caught.Count > ring.names.Count ? ", ..." : "");
            string pulls = ring.signature ? $", dragged them toward the centre {ring.pulls} times ({ring.pulled:0.0} u in all, " +
                $"{ring.constricts} constrict pulses)" : "";
            Log($"{(ring.signature ? "signature " : "")}thicket withered after {duration:0.0} s: caught {ring.caught.Count} ({names}), " +
                $"{ring.grabbed.Count} of them by a vine first ({ring.vineHits} vine hits in all), " +
                $"{ring.pricks} pricks for {ring.prickDamage} damage before armour{pulls}");
        }

        // Runs a cosmetic effect; if it's unavailable it's skipped (and logged once) instead of
        // stopping the thicket.
        public static void Effect(string name, System.Action effect)
        {
            if (failedEffects.Contains(name))
                return;
            try
            {
                effect();
            }
            catch (System.Exception e)
            {
                failedEffects.Add(name);
                Log($"effect '{name}' unavailable, skipping it: {e.Message}");
            }
        }

        public static void Log(string message) => BrambleRingPlugin.Log("Bramble Ring: " + message);

        public static void LogOnce(string key, string message)
        {
            if (failedEffects.Add("log:" + key))
                Log(message);
        }
    }
}
