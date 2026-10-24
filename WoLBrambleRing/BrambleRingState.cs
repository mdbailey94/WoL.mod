using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WoLBrambleRing
{
    // Slams the ground (Terra Ring's ground slam) and bursts a ring of thorns up around the wizard:
    // the game's own earth spikes (EarthFloorSpike, the spikes of Earth Wave and the earth enemies'
    // spike waves), each with its own hit box, raised in two staggered rows that ripple outward.
    // Each ring hits an enemy once, shoves it outward a little and slows it (SkillStats in the
    // plugin). Enhanced, a second, wider ring bursts out from the same spot a moment later.
    public class BrambleRingState : Player.SkillState
    {
        public new static string staticID = "BrambleRing";

        // The first ring: two rows of thorns, the inner one first. Thorns reach about half a unit
        // either side, so this covers roughly 1 to 3 units out (enemies right up against you too).
        private const float InnerRowRadius = 1.4f;
        private const float OuterRowRadius = 2.4f;
        // Enhanced: the second, wider ring (roughly 3 to 5 units out), and how long after the
        // first it bursts.
        private const float EnhancedInnerRowRadius = 3.6f;
        private const float EnhancedOuterRowRadius = 4.6f;
        private const float EnhancedRingDelay = 0.35f;
        // Distance between neighbouring thorns along a row (the earth boss's spike waves space
        // them 1 apart), and the fewest a row gets.
        private const float ThornSpacing = 1f;
        private const int MinThornsPerRow = 6;
        // The outer row bursts this long after the inner one, so the ring ripples outward.
        private const float RowInterval = 0.05f;
        // Thorns aren't raised inside walls, obstacles or pits (the check Terra Ring uses).
        private const float WallCheckRadius = 0.25f;
        // How long a thorn can hit for (the spike's own animation normally ends it after 0.1 s),
        // and when it's cleared away if the game hasn't already (it normally breaks after 0.8 s).
        private const float HitWindow = 0.3f;
        private const float ThornLifetime = 2f;
        // Rock bits each thorn throws up as it bursts out and as it crumbles.
        private const int ThornRockBits = 2;
        private const int ThornBreakRockBits = 1;
        // Leaves each thorn throws up, for the bramble look.
        private const int LeavesPerThorn = 2;
        private const float RingShake = 0.5f;
        // The enhanced ring's slam is a little deeper.
        private const float EnhancedRingPitch = 0.85f;

        private static readonly HashSet<string> failedEffects = new HashSet<string>();
        private static bool loggedHitBox;

        public override string OnEnterAnimStr => parent.GSlamAnimStr;

        public BrambleRingState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            isMeleeSkill = true;
            applyStopElementStatus = true;
            // Terra Ring's timings: the slam lands at 0.3.
            SetAnimTimes(
                0.15f, // start
                0.2f,  // hold
                0.3f,  // execute
                0.8f,  // cancel
                0.9f); // run
        }

        public override void ExecuteSkill()
        {
            base.ExecuteSkill();

            // Both rings burst around where you slammed, even if you move off before the second.
            Vector2 center = parent.transform.position;
            bool enhanced = IsEmpowered;
            BrambleRingPlugin.Log($"Bramble Ring cast at ({center.x:0.0}, {center.y:0.0}){(enhanced ? ", enhanced" : "")}");

            // Run on the wizard, so the rings finish even after this state has ended.
            parent.StartCoroutine(Ring(center, 1, InnerRowRadius, OuterRowRadius, 0f));
            if (enhanced)
                parent.StartCoroutine(Ring(center, 2, EnhancedInnerRowRadius, EnhancedOuterRowRadius, EnhancedRingDelay));
        }

        // One ring of thorns. Ring 1 hits with skill level 1, the enhanced ring 2 with level 2.
        private IEnumerator Ring(Vector2 center, int ring, float innerRadius, float outerRadius, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            var hits = new HashSet<Entity>();
            var thorns = new List<EarthFloorSpike>();
            // A random turn for each ring, so the thorns don't always stand in the same places.
            float spin = Random.Range(0f, 360f);

            RingEffects(center, ring);
            int blocked = RaiseRow(center, innerRadius, spin, false, ring, hits, thorns);
            yield return new WaitForSeconds(RowInterval);
            blocked += RaiseRow(center, outerRadius, spin, true, ring, hits, thorns);
            BrambleRingPlugin.Log($"Bramble Ring: ring {ring} raised {thorns.Count} thorns {innerRadius:0.0}-{outerRadius:0.0} out" +
                (blocked > 0 ? $" ({blocked} skipped in walls or pits)" : ""));

            yield return new WaitForSeconds(HitWindow);

            // Make sure no thorn keeps hitting (the spike's animation normally switches it off).
            int stillOn = 0;
            foreach (EarthFloorSpike thorn in thorns)
            {
                if (thorn == null || thorn.attack == null || thorn.attack.collider == null || !thorn.attack.collider.enabled)
                    continue;
                thorn.attack.collider.enabled = false;
                stillOn++;
            }
            if (stillOn > 0)
                BrambleRingPlugin.Log($"Bramble Ring: switched off {stillOn} thorn hit boxes the game left on");

            LogHits(ring, hits);
        }

        // A row of thorns around the center; the outer row sits between the inner row's thorns.
        private int RaiseRow(Vector2 center, float radius, float spin, bool offset, int level,
            HashSet<Entity> hits, List<EarthFloorSpike> thorns)
        {
            int count = Mathf.Max(MinThornsPerRow, Mathf.RoundToInt(2f * Mathf.PI * radius / ThornSpacing));
            float step = 360f / count;
            int blocked = 0;
            for (int i = 0; i < count; i++)
            {
                float angle = (spin + step * (i + (offset ? 0.5f : 0f))) * Mathf.Deg2Rad;
                var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 position = center + outward * radius;
                if (!Globals.CheckCircle(position, WallCheckRadius, ChaosCollisions.layerAll))
                {
                    blocked++;
                    continue;
                }
                EarthFloorSpike thorn = RaiseThorn(position, outward, level, hits);
                if (thorn != null)
                    thorns.Add(thorn);
            }
            return blocked;
        }

        private EarthFloorSpike RaiseThorn(Vector2 position, Vector2 outward, int level, HashSet<Entity> hits)
        {
            try
            {
                GameObject go = Object.Instantiate(EarthFloorSpike.Prefab, position, Quaternion.identity);
                Object.Destroy(go, ThornLifetime);
                EarthFloorSpike thorn = go.GetComponent<EarthFloorSpike>();
                if (thorn == null || thorn.attack == null)
                {
                    LogOnce("thorn", "the game's earth spike has no attack");
                    return null;
                }

                // One camera shake for the whole ring instead of one per thorn.
                thorn.shakeOnSpawn = false;
                thorn.spawnParticleCount = ThornRockBits;
                thorn.breakPrimaryParticleCount = ThornBreakRockBits;
                thorn.breakSeconaryParticleCount = ThornBreakRockBits;

                thorn.attack.SetAttackInfo(parent.skillCategory, skillID, level, isUltimate);
                // Knockback is overwritten to this direction (SkillStats), so enemies are shoved
                // away from the wizard rather than away from whichever thorn hit them.
                thorn.attack.knockbackOverwriteVector = outward;
                thorn.attack.entityCollisionEventHandlers += entity =>
                {
                    if (entity != null)
                        hits.Add(entity);
                };
                // Hit box on; the floor collider (which blocks movement) off, as Earth Wave does.
                thorn.Activate(true, false);
                LogHitBox(thorn);

                Effect("leaves", () => PoolManager.GetPoolItem<LeafEmitter>().EmitSingle(LeavesPerThorn,
                    position + new Vector2(0f, 0.5f), null, null, 0f, null, LeafEmitter.LeafColor.Green));
                return thorn;
            }
            catch (System.Exception e)
            {
                LogOnce("thorn", $"couldn't raise a thorn: {e.Message}");
                return null;
            }
        }

        // The slam: a crack and dust at the center for the first ring, and a thud and a shake for each.
        private static void RingEffects(Vector2 center, int ring)
        {
            if (ring == 1)
            {
                Effect("floor crack", () => PoolManager.GetPoolItem<FloorCrackEmitter>().Emit(1, center));
                Effect("dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(50, 1f, 6f, -1f, center));
            }
            Effect("sound", () => SoundManager.PlayWithDistAndSPR("GroundSlam", center, ring == 1 ? 1f : EnhancedRingPitch));
            Effect("camera shake", () => CameraController.ShakeCamera(RingShake));
        }

        // What each ring caught, and how many of those are slowed now (enemies that died, and Air
        // enemies, which can't be slowed, aren't).
        private static void LogHits(int ring, HashSet<Entity> hits)
        {
            if (hits.Count == 0)
            {
                BrambleRingPlugin.Log($"Bramble Ring: ring {ring} hit no enemies");
                return;
            }
            int slowed = 0;
            var names = new List<string>();
            foreach (Entity entity in hits)
            {
                if (entity == null)
                    continue;
                if (entity.dotManager != null && entity.dotManager.HasDot(SlowDotEffect.slowStatusStr))
                    slowed++;
                if (names.Count < 5)
                    names.Add(entity.name);
            }
            BrambleRingPlugin.Log($"Bramble Ring: ring {ring} hit {hits.Count} enemies ({string.Join(", ", names.ToArray())}), {slowed} slowed");
        }

        // The size of a thorn's hit box, once, for tuning the row spacing.
        private static void LogHitBox(EarthFloorSpike thorn)
        {
            if (loggedHitBox)
                return;
            loggedHitBox = true;
            Collider2D box = thorn.attack.collider;
            if (box == null)
            {
                BrambleRingPlugin.Log("Bramble Ring: thorn has no hit box");
                return;
            }
            Vector3 scale = box.transform.lossyScale;
            string size;
            if (box is CircleCollider2D circle)
                size = $"circle, radius {circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)):0.00}";
            else if (box is BoxCollider2D rect)
                size = $"box, {rect.size.x * Mathf.Abs(scale.x):0.00} x {rect.size.y * Mathf.Abs(scale.y):0.00}";
            else
                size = $"{box.GetType().Name}, {box.bounds.size.x:0.00} x {box.bounds.size.y:0.00}";
            BrambleRingPlugin.Log($"Bramble Ring: thorn hit box is a {size} (row spacing {OuterRowRadius - InnerRowRadius:0.0}, thorn spacing {ThornSpacing:0.0})");
        }

        // Runs a cosmetic effect; if it's unavailable it's skipped (and logged once) instead of
        // stopping the ring.
        private static void Effect(string name, System.Action effect)
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
                BrambleRingPlugin.Log($"Bramble Ring: effect '{name}' unavailable, skipping it: {e.Message}");
            }
        }

        private static void LogOnce(string key, string message)
        {
            if (failedEffects.Add("log:" + key))
                BrambleRingPlugin.Log($"Bramble Ring: {message}");
        }
    }
}
