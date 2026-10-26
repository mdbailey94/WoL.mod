using System.Collections.Generic;
using UnityEngine;

namespace WoLTidalWall
{
    // How the normal and enhanced wave looks, on top of the game's own water wall and water
    // effects (the signature wave keeps its own cinematic look and doesn't use this). It only
    // draws: nothing here touches the wave's hits, timing or movement.
    //   Rise:  a splash crown bursts up at each column's foot as it rises (the game's WaterHitSmall
    //          frames), with a ripple ring spreading over the floor (painted) and the game's water
    //          ring (Whirlpool's).
    //   Stand and roll: a jagged crest of water rides the top of every column (painted); the
    //          front throws droplets that splash where they land (the game's WaterParticles
    //          frames) and splash crowns (WaterHitSmall), on top of the game's water drops; the
    //          wave leaves a wet trail of puddles that glint and dry up (painted); every enemy it
    //          sweeps is churned in a ring of swirling water (painted), bubbles popping off it (the
    //          game's BubblePop frames, plus its bubble pop effect).
    //   Crash: a big splash crown at each burst (the game's WaterHitEffect frames, larger, with
    //          the game's own splashes), mist hangs over it (the game's Smoke frames tinted pale
    //          blue), foam washes out over the floor (painted) and droplets rain down.
    // Painted ones are in the game's water style (WaveArt.cs). Each visual is logged once the
    // first time it plays ("visual '...' ready", with where its frames come from), and every wave
    // logs what it drew ("look: ...") with its end-of-wave line.
    internal sealed class WaveLook
    {
        // Crests: a column's top when its sprite can't be measured; the crest sits this far below
        // the top so it caps it; neighbours start this far apart in their roll so they don't move
        // in step; and they sort just in front of their column.
        private const float CrestFallbackHeight = 1.6f;
        private const float CrestMinHeight = 0.6f, CrestMaxHeight = 4f;
        private const float CrestSink = 0.2f;
        private const float CrestStagger = 0.07f;
        private const int CrestSortBias = 1;
        private const float CrestSafety = 1f;           // crests end by themselves this long after the wave should have crashed
        // Rise: one surge per column as it rises (WaveFront's RiseDelay and RiseInterval).
        private const float SurgeFirstDelay = 0.05f;
        private const float SurgeStagger = 0.03f;
        private const int SurgeSortBias = 2;             // in front of the column rising behind it
        private const float RingRadiusScale = 0.4f;      // the game's water ring, this much of the wall's width
        private const float RippleFade = 0.3f;           // the ripple fades out over its last this long
        // Spray off the front: droplets per spray tick (WaveFront's SprayInterval), a plume every
        // few, thrown off the wall's face at a height, up and ahead of the wave, falling under
        // gravity to splat on the floor.
        private const int DropletsPerSpray = 2;
        private const int PlumeEvery = 2;
        private const float DropletMinHeight = 0.5f, DropletMaxHeight = 1.4f;
        private const float DropletMinLift = 2f, DropletMaxLift = 5f;
        private const float DropletGravity = -20f;
        private const float DropletMinSpeed = 1f, DropletMaxSpeed = 3f;   // faster than the wave by this
        private const float DropletSpread = 1.2f;                         // sideways, either way
        private const int SpraySortBias = 5;
        // Wet trail: a row of puddles every this far rolled, just behind the wall's middle line,
        // each lying a while (looping) before it dries; at most this many per wave.
        private const float TrailSpacing = 0.45f;
        private const float TrailBehind = 0.5f;
        private const float PuddleMinLife = 1f, PuddleMaxLife = 1.6f;
        private const int MaxPuddles = 56;
        private const int PuddleShapes = 3;
        // Churned enemies: the foam stays this long after the last push; bubbles per push, rising
        // this fast; at most this many enemies churned at once.
        private const float ChurnLinger = 0.6f;
        private const float ChurnSafety = 4f;            // churn foam ends by itself after this
        private const int BubblesPerHit = 2;
        private const float BubbleRise = 1.6f;
        private const int MaxChurned = 12;
        // Crash: each burst's crown starts this far after the last; mist puffs and droplets per
        // burst; the foam lies bubbling this long before it breaks up; mist drifts on and up.
        private const float CrownStagger = 0.04f;
        private const int MistPerBurst = 4;
        private const float CrownScale = 1.5f;          // the game's crown, shown this much larger for the crash
        private const int CrashDropletsPerBurst = 8;
        private const float FoamLoop = 0.5f;
        private const float MistDrift = 0.6f;
        private const float MistRise = 0.5f;
        private const float MistHeight = 0.5f;

        private class Churned
        {
            public Entity entity;
            public FxRef front, back;
            public float until;
        }

        private readonly WaveSettings s;
        private readonly Vector2 side;
        private readonly bool flip;
        private readonly List<FxRef> crests = new List<FxRef>();
        private readonly Dictionary<int, Churned> churned = new Dictionary<int, Churned>();
        private readonly HashSet<int> everChurned = new HashSet<int>();
        private float trailDistance;
        private int sprays;
        private float crestHeight = -1f;
        private bool measured;
        // For the log.
        private int surges, crestCount, droplets, plumes, puddles, bubbles, crowns, mists;

        public WaveLook(WaveSettings settings)
        {
            s = settings;
            side = Globals.GetPerpendicular(s.Direction);
            // The crest is drawn rolling to the right; mirrored when the wave rolls left.
            flip = s.Direction.x < -0.1f;
            PixelArt.MatchPixelSize(s.Caster);
            // Drawn with the material the game's own water wall sprites use.
            try
            {
                if (WaterWall.Prefab != null)
                    PixelArt.UseMaterialOf(WaterWall.Prefab.GetComponentInChildren<SpriteRenderer>(true), "the game's WaterWall");
            }
            catch
            {
            }
        }

        // The wall rising: a surge per column as it comes up, a ripple and the game's water ring.
        public void Rise(IList<Vector2> columnPoints)
        {
            for (int i = 0; i < columnPoints.Count; i++)
            {
                if (PixelFx.Spawn(WaveArt.SmallCrown, columnPoints[i], FxLayer.Actor, SurgeSortBias)?
                    .Delay(SurgeFirstDelay + i * SurgeStagger).Flip(i % 2 == 1) != null)
                    surges++;
            }
            PixelFx.Spawn(WaveArt.Ripple, s.Center, FxLayer.Floor)?.FadeOut(RippleFade);
            Effects.Ring(s.Center, s.Width * RingRadiusScale);
        }

        // The wall has risen: a foam crest on top of each column, riding along with it.
        public void Crests(IList<Transform> columns, bool wallUp, Transform wave)
        {
            float life = s.HoldTime + s.RollTime + CrestSafety;
            if (wallUp)
            {
                int i = 0;
                foreach (Transform column in columns)
                {
                    if (column == null || !column.gameObject.activeSelf)
                        continue;
                    SpriteRenderer body = column.GetComponentInChildren<SpriteRenderer>();
                    PixelArt.UseMaterialOf(body, "a water column");
                    float top = Measure(column, body);
                    PixelFx fx = PixelFx.Spawn(WaveArt.Crest, column.position, body != null ? FxLayer.Actor : FxLayer.Effect, CrestSortBias);
                    if (fx == null)
                        continue;
                    fx.Follow(column, new Vector2(0f, top - CrestSink)).Loop(life).Offset(i * CrestStagger).Flip(flip);
                    if (body != null)
                        fx.SortWith(body, CrestSortBias);
                    crests.Add(fx.Ref);
                    i++;
                }
            }
            else
            {
                // No water wall to cap: the crests ride the wave itself, where its columns would be.
                for (int i = 0; i < s.Columns; i++)
                {
                    Vector2 offset = side * ((i - (s.Columns - 1) * 0.5f) * s.Spacing) + Vector2.up * (CrestFallbackHeight - CrestSink);
                    PixelFx fx = PixelFx.Spawn(WaveArt.Crest, (Vector2)wave.position + offset, FxLayer.Effect, CrestSortBias);
                    if (fx == null)
                        continue;
                    fx.Follow(wave, offset).Loop(life).Offset(i * CrestStagger).Flip(flip);
                    crests.Add(fx.Ref);
                }
                crestHeight = CrestFallbackHeight;
            }
            crestCount = crests.Count;
        }

        // How tall a water column stands (its sprite's top above its foot), once per wave.
        private float Measure(Transform column, SpriteRenderer body)
        {
            if (measured)
                return crestHeight;
            measured = true;
            crestHeight = CrestFallbackHeight;
            try
            {
                if (body != null && body.sprite != null)
                    crestHeight = Mathf.Clamp(body.bounds.max.y - column.position.y, CrestMinHeight, CrestMaxHeight);
            }
            catch
            {
            }
            return crestHeight;
        }

        // The wave rolled this far: drop a row of puddles behind it every TrailSpacing.
        public void Rolled(float step, Vector2 position)
        {
            trailDistance += step;
            while (trailDistance >= TrailSpacing)
            {
                trailDistance -= TrailSpacing;
                int count = Mathf.CeilToInt(s.Width * 0.5f);
                for (int i = 0; i < count && puddles < MaxPuddles; i++)
                {
                    Vector2 at = position - s.Direction * TrailBehind + side * Random.Range(-s.Width * 0.5f, s.Width * 0.5f);
                    if (PixelFx.Spawn(WaveArt.Puddle(Random.Range(0, PuddleShapes)), at, FxLayer.Floor)?
                        .Loop(Random.Range(PuddleMinLife, PuddleMaxLife)).Flip(Random.value < 0.5f) != null)
                        puddles++;
                }
            }
        }

        // One spray tick off the rolling front (at: a spot along its front edge).
        public void Spray(Vector2 at)
        {
            sprays++;
            for (int i = 0; i < DropletsPerSpray; i++)
                Droplet(at + side * Random.Range(-0.3f, 0.3f), s.Direction * (s.Speed + Random.Range(DropletMinSpeed, DropletMaxSpeed))
                    + side * Random.Range(-DropletSpread, DropletSpread), Random.Range(DropletMinHeight, DropletMaxHeight));
            if (sprays % PlumeEvery == 0)
            {
                // The plume rides along with the front that threw it.
                if (PixelFx.Spawn(WaveArt.SmallCrown, at, FxLayer.Effect, SpraySortBias)?.Move(s.Direction * s.Speed).Flip(Random.value < 0.5f) != null)
                    plumes++;
            }
        }

        private void Droplet(Vector2 at, Vector2 velocity, float height)
        {
            // It flies on its first frame and splashes into specks where it lands (the rest).
            PixelFx fx = PixelFx.Spawn(WaveArt.Droplet(Random.Range(0, WaveArt.DropletVariants)), at, FxLayer.Effect, SpraySortBias);
            if (fx == null)
                return;
            fx.Move(velocity).Arc(height, Random.Range(DropletMinLift, DropletMaxLift), DropletGravity, null, -1f, true)
                .Flip(Random.value < 0.5f);
            droplets++;
        }

        // A push (or the crash) landed on something: an enemy is churned in foam while it's carried.
        public void Hit(Entity hurt)
        {
            if (!(hurt is Enemy))
                return;
            int id = hurt.GetInstanceID();
            Vector2 feet = hurt.transform.position;
            Churned churn;
            if (!churned.TryGetValue(id, out churn))
            {
                if (churned.Count >= MaxChurned)
                    return;
                churn = new Churned { entity = hurt };
                SpriteRenderer body = hurt.spriteRenderer;
                churn.back = Half(false, hurt, body);
                churn.front = Half(true, hurt, body);
                churned[id] = churn;
                everChurned.Add(id);
            }
            churn.until = Time.time + ChurnLinger;
            for (int i = 0; i < BubblesPerHit; i++)
            {
                Vector2 at = feet + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(0f, 0.4f));
                if (PixelFx.Spawn(WaveArt.Bubble, at, FxLayer.Effect, SpraySortBias)?.Move(Vector2.up * BubbleRise).Delay(i * 0.08f) != null)
                    bubbles++;
            }
            Effects.Bubbles(feet + Vector2.up * 0.5f);
        }

        private static FxRef Half(bool front, Entity hurt, SpriteRenderer body)
        {
            PixelFx fx = PixelFx.Spawn(WaveArt.Churn(front), hurt.transform.position, FxLayer.Actor, front ? 1 : -1);
            if (fx == null)
                return default(FxRef);
            fx.Follow(hurt.transform, Vector2.zero).Loop(ChurnSafety);
            if (body != null)
                fx.SortWith(body, front ? 1 : -1);
            return fx.Ref;
        }

        // Every frame: the foam lets go of enemies no longer being pushed (or gone).
        public void Tend()
        {
            if (churned.Count == 0)
                return;
            List<int> done = null;
            foreach (KeyValuePair<int, Churned> pair in churned)
            {
                Churned churn = pair.Value;
                bool gone = churn.entity == null || !churn.entity.gameObject.activeInHierarchy
                    || (churn.entity.health != null && churn.entity.health.CurrentHealthValue <= 0);
                if (gone || Time.time >= churn.until || (!churn.front.Alive && !churn.back.Alive))
                {
                    churn.front.Stop();
                    churn.back.Stop();
                    (done ?? (done = new List<int>())).Add(pair.Key);
                }
            }
            if (done != null)
                foreach (int id in done)
                    churned.Remove(id);
        }

        // One burst of the crash (WaveFront throws 3 across the wall).
        public void CrashBurst(Vector2 at, int index)
        {
            if (PixelFx.Spawn(WaveArt.BigCrown, at, FxLayer.Effect, SpraySortBias)?.Scale(CrownScale).Delay(index * CrownStagger)
                .Flip(index % 2 == 1) != null)
                crowns++;
            PixelAnim mist = WaveArt.MistPuff;
            Color mistTint = mist != null && !mist.Fallback ? WaveArt.MistTint : Color.white;
            PixelFx.Spawn(WaveArt.FoamSpread, at + s.Direction * 0.4f, FxLayer.Floor, 1)?.Loop(FoamLoop).Delay(index * CrownStagger);
            for (int i = 0; i < MistPerBurst; i++)
            {
                Vector2 drift = s.Direction * MistDrift + side * Random.Range(-MistDrift, MistDrift);
                if (PixelFx.Spawn(mist, at + side * Random.Range(-0.8f, 0.8f), FxLayer.Effect, SpraySortBias + 1)?.Tint(mistTint)
                    .Move(drift, 1f).Arc(MistHeight, MistRise, 0f).Delay(0.08f + i * 0.1f).Flip(Random.value < 0.5f) != null)
                    mists++;
            }
            for (int i = 0; i < CrashDropletsPerBurst; i++)
            {
                Vector2 dir = (s.Direction + Random.insideUnitCircle * 1.2f).normalized;
                Droplet(at, dir * Random.Range(1.5f, 4f), Random.Range(0.6f, 1.8f));
            }
        }

        // The crash: the crests break apart, the foam lets go of everyone.
        public void Crash()
        {
            foreach (FxRef crest in crests)
                crest.Stop();
            crests.Clear();
            foreach (Churned churn in churned.Values)
            {
                churn.front.Stop();
                churn.back.Stop();
            }
            churned.Clear();
        }

        // The wave's object is going: nothing that follows it is left behind.
        public void End() => Crash();

        public string Summary() =>
            $"Tidal Wall: look: {surges} rising surges, {crestCount} foam crests (columns {crestHeight:0.0} tall), " +
            $"{droplets} droplets, {plumes} sprays, {puddles} puddles, churned {everChurned.Count} enemies ({bubbles} bubbles), " +
            $"crash {crowns} splashes and {mists} mist puffs ({PixelFx.Live} drawn effects still playing)";
    }
}
