using System.Collections.Generic;
using UnityEngine;

namespace WoLBrambleRing
{
    // How the normal and enhanced thicket looks, on top of the game's own vine traps and effects
    // (the signature keeps its own cinematic eruption and doesn't use this). The aim is a living
    // plant growing round you, not a ring of rock: it only draws, nothing here touches the
    // thicket's hits, snare, timing or reach.
    //   Cast:   dark roots creep out over the floor from your feet to the first row (the game's
    //           vine-trap ground roots, Plants sheet).
    //   Knit:   under every vine the earth heaves and opens (painted), clods of earth tumble out
    //           and crumble (the game's Rocks frames) with a little dust and the game's vine arcs
    //           (Churning Earth's); thorny tendrils arch up out of the ground between the vines of
    //           each row and stand while it stands (the game's VineArc frames), and roots creep
    //           across the floor from row to row.
    //   Prick:  pale thorns stab in at the enemy from round it and pull back, with a little spark
    //           (painted) and the game's cut grass flying.
    //   Snare:  two thorny olive vines spiral up round a snared enemy's legs and squeeze (painted,
    //           half behind and half in front of it), until it's free or gone.
    //   Wither: the tendrils sink back into the ground, roots and earth fade, coils brown and slide
    //           off, withered leaves flutter down (painted, and the game's falling leaves) and puffs
    //           of dry dust rise (the game's Smoke frames tinted earth).
    // Painted ones are in the game's plant-and-earth style (ThicketArt.cs). Each visual is logged
    // once the first time it plays ("visual '...' ready", with where its frames come from), and
    // every thicket logs what it drew ("look: ...") with its end line.
    internal sealed class ThicketLook
    {
        // Everything standing ends by itself this long after the thicket should have withered
        // (in case the thicket's own end never comes, e.g. the wizard's object goes).
        private const float StandSafety = 2f;
        // Cast: roots from the wizard's feet, starting this far out, one after another.
        private const int CastRoots = 6;
        private const float CastRootStart = 0.15f;
        private const float CastRootStagger = 0.03f;
        // Floor sorting: soil under the roots.
        private const int SoilSortBias = 1;
        private const int RootSortBias = 2;
        // Roots between rows start this far inside the previous row and lean this many degrees
        // off straight out, so they creep rather than spoke.
        private const float RootInset = 0.2f;
        private const float RootLean = 18f;
        // Per vine: clods thrown up (lift, sideways speed, gravity, tumbling speed), the game's
        // dust (count, scale) and vine arcs.
        private const int ClodsPerVine = 3;
        private const float ClodMinLift = 3f, ClodMaxLift = 5f;
        private const float ClodMinSpeed = 0.8f, ClodMaxSpeed = 2.2f;
        private const float ClodGravity = -22f;
        private const float ClodSpin = 540f;
        private const int VineDust = 8;
        private const float VineDustScale = 0.2f;
        private const int VineArcs = 2;
        // Undergrowth between neighbouring vines, nudged in or out of the row up to this far.
        private const float SproutJitter = 0.15f;
        private const int SproutSortBias = 1;
        // Pricks: thorns from round the enemy, their tips this far short of its middle, a few
        // hundredths apart; drawn above everything; the game's cut grass.
        private const int ThornsPerPrick = 3;
        private const float ThornReach = 0.12f;
        private const float ThornStagger = 0.03f;
        private const float ThornJitter = 20f;
        private const int ThornSortBias = 6;
        private const int GrassPerPrick = 2;
        // Coils: let go this long after the enemy stops being snared; at most this many at once.
        private const float CoilUnrootGrace = 0.3f;
        private const int MaxCoils = 12;
        // Wither: everything goes within this many seconds (random, so it doesn't all go at once);
        // drawn leaves per vine fall from a height, gently, swaying, and lie a moment; the game's
        // falling leaves; a dust puff every few vines; at most this many vines' worth.
        private const float WitherSpread = 0.4f;
        private const int LeavesPerVine = 2;
        private const float LeafMinHeight = 0.8f, LeafMaxHeight = 1.5f;
        private const float LeafGravity = -3f;
        private const float LeafSway = 0.25f, LeafSwaySpeed = 6f;
        private const float LeafLinger = 0.6f;
        private const int GameLeavesPerVine = 1;
        private const int DustPuffEvery = 2;
        private const int MaxWitherVines = 40;

        private class Coil
        {
            public Enemy enemy;
            public FxRef front, back;
            public float unrootedSince = -1f;
        }

        private readonly Vector2 center;
        private readonly float endsAt;
        private readonly List<FxRef> standing = new List<FxRef>();
        private readonly List<Vector2> vines = new List<Vector2>();
        private readonly Dictionary<int, Coil> coils = new Dictionary<int, Coil>();
        private float lastRowRadius = -1f;
        private bool withered;
        // For the log.
        private int roots, soils, sprouts, clods, thorns, coiled, leaves, puffs;

        public ThicketLook(Player wizard, Vector2 center, float duration)
        {
            this.center = center;
            endsAt = Time.time + duration + StandSafety;
            PixelArt.MatchPixelSize(wizard != null ? wizard.gameObject : null);
            // Drawn with the material the game's own vine trap sprites use.
            try
            {
                if (VineTrap.Prefab != null)
                    PixelArt.UseMaterialOf(VineTrap.Prefab.GetComponentInChildren<SpriteRenderer>(true), "the game's vine trap");
            }
            catch
            {
            }
        }

        private float Remaining => Mathf.Max(0.1f, endsAt - Time.time);

        // Roots creeping out from the wizard's feet to the first row.
        public void Cast()
        {
            float spin = Random.Range(0f, 360f);
            for (int i = 0; i < CastRoots; i++)
            {
                float angle = spin + i * 360f / CastRoots + Random.Range(-15f, 15f);
                Vector2 dir = Dir(angle);
                Root(center + dir * CastRootStart, angle, i * CastRootStagger);
            }
        }

        // A vine burst up here: the soil under it and the clods it throws.
        public void Vine(Vector2 at)
        {
            vines.Add(at);
            if (PixelFx.Spawn(ThicketArt.SoilBurst, at, FxLayer.Floor, SoilSortBias)?.Loop(Remaining).Flip(Random.value < 0.5f) is PixelFx soil)
            {
                standing.Add(soil.Ref);
                soils++;
            }
            for (int i = 0; i < ClodsPerVine; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                // It tumbles through the air on its first frame and crumbles where it lands.
                if (PixelFx.Spawn(ThicketArt.Clod(Random.Range(0, ThicketArt.ClodVariants)), at, FxLayer.Actor)?
                    .Move(dir * Random.Range(ClodMinSpeed, ClodMaxSpeed)).Spin(Random.value < 0.5f ? ClodSpin : -ClodSpin)
                    .Arc(0.2f, Random.Range(ClodMinLift, ClodMaxLift), ClodGravity, null, -1f, true) != null)
                    clods++;
            }
            BrambleRingState.Effect("vine dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(VineDust, VineDustScale, -1f, -1f, at));
            BrambleRingState.Effect("vine arcs", () => PoolManager.GetPoolItem<ParticleEffect>("VineArc").Emit(VineArcs, at,
                ParticleEffectOverrides.StartLifeTime0p5));
        }

        // A row of vines is up: shoots between its vines, and roots creeping to it from the row
        // inside (the first row's come from the cast).
        public void Row(IList<Vector2> row, float radius)
        {
            for (int i = 0; i < row.Count; i++)
            {
                Vector2 a = row[i], b = row[(i + 1) % row.Count];
                if (row.Count < 2 || (a - b).sqrMagnitude > 9f)
                    continue;   // neighbours either side of a gap (a wall): nothing between them
                Vector2 mid = (a + b) * 0.5f;
                Vector2 outward = (mid - center).sqrMagnitude > 0.0001f ? (mid - center).normalized : Vector2.up;
                Vector2 spot = mid + outward * Random.Range(-SproutJitter, SproutJitter);
                if (PixelFx.Spawn(ThicketArt.Sprout(Random.Range(0, ThicketArt.SproutVariants)), spot, FxLayer.Actor, SproutSortBias)?.Loop(Remaining)
                    .Delay(Random.Range(0f, 0.15f)).Offset(Random.Range(0f, 0.2f)).Flip(Random.value < 0.5f) is PixelFx sprout)
                {
                    standing.Add(sprout.Ref);
                    sprouts++;
                }
            }
            if (lastRowRadius > 0f)
            {
                for (int i = 0; i < row.Count; i++)
                {
                    Vector2 toVine = row[i] - center;
                    float angle = Mathf.Atan2(toVine.y, toVine.x) * Mathf.Rad2Deg;
                    float lean = (i % 2 == 0 ? 1f : -1f) * RootLean;
                    Vector2 start = center + Dir(angle - lean) * Mathf.Max(0.1f, lastRowRadius - RootInset);
                    Vector2 along = row[i] - start;
                    Root(start, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg, i * CastRootStagger * 0.5f);
                }
            }
            lastRowRadius = radius;
        }

        private void Root(Vector2 from, float angle, float delay)
        {
            // Pointing left it's mirrored top to bottom, so its barbs and leaf keep their light side up.
            bool left = Mathf.Abs(Mathf.DeltaAngle(0f, angle)) > 90f;
            if (PixelFx.Spawn(ThicketArt.Root, from, FxLayer.Floor, RootSortBias)?.Rotate(angle).Flip(false, left)
                .Loop(Remaining).Delay(delay) is PixelFx root)
            {
                standing.Add(root.Ref);
                roots++;
            }
        }

        // A thorn prick on an enemy (at: its body): thorns stab in from round it.
        public void Prick(Enemy enemy, Vector2 at)
        {
            float spin = Random.Range(0f, 360f);
            for (int i = 0; i < ThornsPerPrick; i++)
            {
                float angle = spin + i * 360f / ThornsPerPrick + Random.Range(-ThornJitter, ThornJitter);
                Vector2 outward = Dir(angle);
                float pointing = angle + 180f;   // in, at the enemy
                bool left = Mathf.Abs(Mathf.DeltaAngle(0f, pointing)) > 90f;
                if (PixelFx.Spawn(ThicketArt.ThornJab, at + outward * ThornReach, FxLayer.Effect, ThornSortBias)?
                    .Rotate(pointing).Flip(false, left).Delay(i * ThornStagger) != null)
                    thorns++;
            }
            PixelFx.Spawn(ThicketArt.Spark, at, FxLayer.Effect, ThornSortBias + 1)?.Delay(ThornStagger);
            BrambleRingState.Effect("prick grass", () => PoolManager.GetPoolItem<ParticleEffect>("TallGrassEmitter").Emit(GrassPerPrick, at));
        }

        // An enemy inside the thicket: once it's snared, vines coil up round it.
        public void Snare(Enemy enemy)
        {
            if (withered || enemy == null || coils.Count >= MaxCoils)
                return;
            int id = enemy.GetInstanceID();
            if (coils.ContainsKey(id) || !Rooted(enemy))
                return;
            var coil = new Coil { enemy = enemy };
            coil.back = Half(false, enemy);
            coil.front = Half(true, enemy);
            coils[id] = coil;
            coiled++;
        }

        private FxRef Half(bool front, Enemy enemy)
        {
            PixelFx fx = PixelFx.Spawn(ThicketArt.Coil(front), enemy.transform.position, FxLayer.Actor, front ? 1 : -1);
            if (fx == null)
                return default(FxRef);
            fx.Follow(enemy.transform, Vector2.zero).Loop(Remaining);
            if (enemy.spriteRenderer != null)
                fx.SortWith(enemy.spriteRenderer, front ? 1 : -1);
            return fx.Ref;
        }

        private static bool Rooted(Enemy enemy)
        {
            try
            {
                return enemy.dotManager != null && enemy.dotManager.HasDot(BrambleRingState.RootStatusID);
            }
            catch
            {
                return false;
            }
        }

        // Every frame while it stands: coils let go of enemies that are free, dead or gone.
        public void Tend()
        {
            if (coils.Count == 0)
                return;
            List<int> done = null;
            foreach (KeyValuePair<int, Coil> pair in coils)
            {
                Coil coil = pair.Value;
                Enemy enemy = coil.enemy;
                bool gone = enemy == null || !enemy.gameObject.activeInHierarchy
                    || (enemy.health != null && enemy.health.CurrentHealthValue <= 0) || (!coil.front.Alive && !coil.back.Alive);
                bool free = false;
                if (!gone)
                {
                    if (Rooted(enemy))
                        coil.unrootedSince = -1f;
                    else if (coil.unrootedSince < 0f)
                        coil.unrootedSince = Time.time;
                    else
                        free = Time.time - coil.unrootedSince >= CoilUnrootGrace;
                }
                if (gone || free)
                {
                    coil.front.Stop();
                    coil.back.Stop();
                    (done ?? (done = new List<int>())).Add(pair.Key);
                }
            }
            if (done != null)
                foreach (int id in done)
                    coils.Remove(id);
        }

        // The thicket's time is up: everything withers and falls (just before the game's vines
        // wither themselves).
        public void Wither()
        {
            if (withered)
                return;
            withered = true;
            foreach (FxRef fx in standing)
                fx.StopAfter(Random.Range(0f, WitherSpread));
            standing.Clear();
            foreach (Coil coil in coils.Values)
            {
                coil.front.Stop();
                coil.back.Stop();
            }
            coils.Clear();
            PixelAnim dust = ThicketArt.DustPuff;
            Color dustTint = dust != null && !dust.Fallback ? ThicketArt.DustTint : Color.white;
            for (int i = 0; i < vines.Count && i < MaxWitherVines; i++)
            {
                Vector2 at = vines[i];
                for (int j = 0; j < LeavesPerVine; j++)
                {
                    Vector2 spot = at + new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-0.2f, 0.2f));
                    if (PixelFx.Spawn(ThicketArt.WitheredLeaf, spot, FxLayer.Actor, 3)?
                        .Arc(Random.Range(LeafMinHeight, LeafMaxHeight), 0f, LeafGravity, null, LeafLinger)
                        .Sway(LeafSway, LeafSwaySpeed).Offset(Random.value).Flip(Random.value < 0.5f)
                        .Delay(Random.Range(0f, WitherSpread)) != null)
                        leaves++;
                }
                BrambleRingState.Effect("wither leaves", () => PoolManager.GetPoolItem<LeafEmitter>("FallingLeafEmitter").EmitSingle(GameLeavesPerVine,
                    at + Vector2.up, ParticleEffectOverrides.Radius0p5, null, 0f, null, LeafEmitter.LeafColor.YellowGreen));
                if (i % DustPuffEvery == 0 && PixelFx.Spawn(dust, at, FxLayer.Effect, 2)?.Tint(dustTint)
                    .Arc(0.2f, 0.4f, 0f).Delay(Random.Range(0.1f, WitherSpread + 0.1f)).Flip(Random.value < 0.5f) != null)
                    puffs++;
            }
        }

        private static Vector2 Dir(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        public string Summary() =>
            $"look: {soils} soil bursts ({clods} clods), {sprouts} thorny shoots, {roots} creeping roots, {thorns} thorn jabs, " +
            $"{coiled} vine coils, {leaves} withered leaves and {puffs} dust puffs ({PixelFx.Live} drawn effects still playing)";
    }
}
