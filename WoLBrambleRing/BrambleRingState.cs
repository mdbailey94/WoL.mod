using System.Collections;
using System.Collections.Generic;
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
    // thicket remembers it) the thicket knits all three rows at once, stands 6 s, hits harder
    // (skill levels 3 and 4) and the vines tighten: every tick drags each snared enemy half a unit
    // toward the centre (moving its body the way Mortal Coil drags the enemies it has hooked; the
    // root only stops an enemy's own movement and knockback, not that). Enhanced and signature are
    // separate flags; the signature's size and time take over when both are set.
    public class BrambleRingState : Player.SkillState
    {
        public new static string staticID = "BrambleRing";

        // Rows of vines around the cast point. Vines are about a unit across, so this knits a
        // thicket roughly 1 to 3 units out (and enemies right up against you are inside it).
        private const float InnerRowRadius = 1.5f;
        private const float OuterRowRadius = 2.5f;
        // Enhanced: the third, wider row, and how long after the outer row is knitted it spreads.
        private const float SpreadRowRadius = 3.5f;
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

        private static readonly HashSet<string> failedEffects = new HashSet<string>();

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

        public override void ExecuteSkill()
        {
            base.ExecuteSkill();

            // The thicket stays where you slammed, even once you've walked off. isUltimate is the
            // game's flag for a signature cast (the signature slot's arcana with the gauge full);
            // it's cleared once this state exits, so the ring keeps its own copy.
            var ring = new Ring((Vector2)parent.transform.position, IsEmpowered, isUltimate);
            Log($"cast ({ring.Variant}) at ({ring.center.x:0.0}, {ring.center.y:0.0})");

            // Run on the wizard, so the thicket outlives this state.
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

        // Raises the rows of vines, one vine after another round the ring: the inner row, the
        // outer row a moment later and, enhanced, the wider third row after that (signature: the
        // third row straight after the second).
        private IEnumerator Knit(Ring ring)
        {
            ring.vines = new BrambleVines(parent, skillID, ring.CatchLevel, ring.signature, entity =>
            {
                ring.vineHits++;
                if (!ring.ended && ring.caught.Add(entity))
                {
                    ring.grabbed.Add(entity);
                    Remember(ring, entity);
                }
            });
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

            if (!ring.enhanced && !ring.signature)
                yield break;
            if (!ring.signature)
                yield return new WaitForSeconds(SpreadRowDelay);
            if (ring.ended)
                yield break;
            int before = ring.vines.Count;
            positions.Clear();
            blocked = BrambleVines.RowPositions(ring.center, SpreadRowRadius, spin, VineSpacing, MinVinesPerRow, false, positions);
            ring.radius = SpreadRowRadius + ThicketMargin;
            Effect("sound", () => SoundManager.PlayAudioWithDistance("VineWaveEnd", ring.center, null, 24f, -1f, 0.9f));
            yield return parent.StartCoroutine(RaiseRow(ring, positions));
            Log($"vines spread outward{(ring.signature ? " at once" : "")}: {ring.vines.Count - before} more at {SpreadRowRadius:0.0} out" +
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
                if (Time.time >= nextTick)
                {
                    nextTick += TickInterval;
                    Tick(ring);
                }
                yield return null;
            }
            ring.ended = true;
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
                if (ring.signature && !cannotSnare)
                    Tighten(ring, enemy);
                if (first)
                    Log($"caught {enemy.name}{(byVine ? " in a vine" : "")}, {Status(enemy)}");
            }
        }

        // The vines tighten: the snared enemy's body is moved SignaturePullPerTick toward the
        // centre (its rigidbody, the way Mortal Coil drags the enemies it has hooked: the game's
        // root zeroes an enemy's own movement and ignores knockback, so a shove wouldn't do it),
        // but never into a wall or an obstacle, and not once it's at the centre. Enemies the game
        // won't let anything grab (bosses) aren't dragged either, as they aren't snared.
        private static void Tighten(Ring ring, Enemy enemy)
        {
            Vector2 from = enemy.transform.position;
            Vector2 toCenter = ring.center - from;
            if (toCenter.magnitude <= PullStopRadius)
                return;
            float step = Mathf.Min(SignaturePullPerTick, toCenter.magnitude - PullStopRadius);
            Vector2 to = from + toCenter.normalized * step;
            try
            {
                if (!Globals.CheckCircle(to, PullClearance, ChaosCollisions.layerUpperWallAndObst))
                    return;
                if (enemy.rigidbody2D != null)
                    enemy.rigidbody2D.MovePosition(to);
                else
                    enemy.transform.position = to;
                ring.pulls++;
                ring.pulled += step;
            }
            catch (System.Exception e)
            {
                LogOnce("pull", $"couldn't drag {enemy.name} toward the centre: {e.Message}");
            }
        }

        // One thorn prick: the skill's own hit at the given level, dealt straight to the enemy
        // (the way Grasping Earth deals its hits), so the game applies the snare and the slow.
        private void Prick(Ring ring, Enemy enemy, int level)
        {
            if (enemy.health == null)
                return;
            AttackInfo info = ring.Info(parent, skillID, level);
            if (info == null)
            {
                LogOnce("stats", $"no stats for level {level}, pricking nothing");
                return;
            }
            try
            {
                // Hurt reactions and hit sparks come from the thicket's centre; no shove.
                info.attackerPosition = ring.center;
                info.knockbackVector = Vector2.zero;
                enemy.health.TakeDamage(info, parent);
                ring.pricks++;
                ring.prickDamage += info.damage;
                Vector2 at = enemy.hurtBoxTransform != null ? (Vector2)enemy.hurtBoxTransform.position : (Vector2)enemy.transform.position;
                Effect("leaves", () => PoolManager.GetPoolItem<LeafEmitter>().EmitSingle(PrickLeaves, at, null, null, 0f, null,
                    LeafEmitter.LeafColor.Green));
            }
            catch (System.Exception e)
            {
                LogOnce("prick", $"couldn't prick {enemy.name}: {e.Message}");
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
            string pulls = ring.signature ? $", dragged them toward the centre {ring.pulls} times ({ring.pulled:0.0} u in all)" : "";
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
