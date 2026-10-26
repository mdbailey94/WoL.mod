using System.Collections.Generic;
using UnityEngine;

namespace WoLRollingGale
{
    // What Rolling Gale looks like, on top of the game's air vortex, wind burst and dust (drawn by
    // the state). Visual only: nothing here touches the hits.
    // - Cast: the game's dash air burst and air shockwave at the wizard, and a drawn gust crescent
    //   rolling off along the aim.
    // - Travel: drawn crescents (and the game's wind trail) rolling from one burst to the next.
    // - Each burst: a drawn inward spiral on the floor where it pulls, with dust, leaves and pebbles
    //   spiralling in to it, and the game's dust suction. Echo pulses get a smaller, quicker spiral.
    // - End: a drawn wind puff breaking apart over the last burst, with the game's vortex-trap
    //   ending (spinning circle and air bursts).
    internal static class GaleLook
    {
        // Cast
        private const float CastCrescentFrom = 0.6f;   // units along the aim from the wizard
        private const float CastCrescentTo = 2.2f;
        private const float CastCrescentLife = 0.22f;
        // Travel: one crescent down the middle and two smaller ones either side.
        private const float RollLife = 0.24f;
        private const float RollSideOffset = 0.7f;      // units to either side of the line
        private const float RollSideScale = 0.75f;
        private const float RollOvershoot = 0.8f;       // units past the burst the crescent carries on
        // Spiral (the pull)
        private const int SpiralFrames = 8;
        private const int SpiralArms = 3;
        private const float SpiralTwist = 1.6f;         // turns from the eye to the rim
        private const float SpiralFill = 0.8f;          // drawn diameter, as a fraction of the burst's pull area
        private const float SpiralFlatten = 0.62f;      // height / width: a circle on the floor, seen at the game's angle
        private const int SpiralMinPx = 24, SpiralMaxPx = 96, SpiralStepPx = 8;
        private const float SpiralLife = 0.45f;
        private const float EchoSpiralLife = 0.32f;
        // Motes sucked in to each burst
        private const int MotesPerBurst = 7;
        private const int MotesPerEcho = 3;
        private const float MoteStartRadius = 0.85f;    // of the burst's pull area
        private const float MoteLife = 0.42f;
        private const float MoteSpin = -5f;             // radians a second, clockwise like the spiral's arms
        private const float MoteFps = 12f;
        // End puff
        private const int PuffWidth = 32, PuffHeight = 24, PuffFrames = 6;
        private const float PuffLife = 0.4f;
        private const float PuffScale = 1.5f;

        private static readonly Dictionary<int, Sprite[]> spirals = new Dictionary<int, Sprite[]>();
        private static Sprite[] puff;

        public static void Cast(Player player, Vector2 origin, Vector2 direction)
        {
            PixelFx.Prepare(player);
            PixelFx.Effect("cast air burst", () =>
            {
                PoolManager.GetPoolItem<DashAirBurst>().Burst(origin, direction, false);
                return "game's DashAirBurst";
            });
            PixelFx.Effect("cast shockwave", () =>
            {
                PoolManager.GetPoolItem<ParticleEffect>("AirWaveShockwave").Emit(new int?(1), new Vector3?(origin + direction),
                    ParticleEffectOverrides.StartLifeTime0p5, new Vector3?(Globals.GetRotationVector(direction)), 0f, null, null);
                return "game's AirWaveShockwave";
            });
            PixelFx.Effect("cast crescent", () =>
            {
                Sprite[] frames = PixelFx.Crescent(direction);
                FxSprite fx = FxSprite.Spawn(frames, origin + direction * CastCrescentFrom, CastCrescentLife);
                if (fx == null)
                    return null;
                fx.Line(origin + direction * CastCrescentFrom, origin + direction * CastCrescentTo).Sort(PixelFx.EffectLayer, 0);
                return $"{frames.Length} frames, 24 px, ppu {PixelFx.Ppu}";
            });
        }

        // The gust rolling on from `from` to the next burst at `to`.
        public static void Roll(Vector2 from, Vector2 to, Vector2 direction)
        {
            PixelFx.Effect("wind trail", () =>
            {
                PoolManager.GetPoolItem<SectionedTrailEmitter>("WindTrail").Emit(from, to, -1, false, -1f, true, 0.3f);
                return "game's WindTrail";
            });
            PixelFx.Effect("rolling crescents", () =>
            {
                Sprite[] frames = PixelFx.Crescent(direction);
                var side = new Vector2(-direction.y, direction.x) * RollSideOffset;
                Vector2 end = to + direction * RollOvershoot;
                int made = 0;
                for (int i = -1; i <= 1; i++)
                {
                    float scale = i == 0 ? 1f : RollSideScale;
                    FxSprite fx = FxSprite.Spawn(frames, from + side * i, RollLife * (i == 0 ? 1f : 1.15f));
                    if (fx == null)
                        continue;
                    fx.Line(from + side * i, end + side * (i * 0.5f)).Scale(scale, scale).Sort(PixelFx.EffectLayer, i == 0 ? 1 : 0);
                    made++;
                }
                return made > 0 ? $"{made} crescents x {frames.Length} frames, ppu {PixelFx.Ppu}" : null;
            });
        }

        // A burst pulling: the spiral on the floor and debris sucked in to it.
        public static void Burst(Vector2 position, float pullRadius, bool echo, Vector2 direction)
        {
            PixelFx.Effect("spiral", () =>
            {
                float wantPx = pullRadius * 2f * SpiralFill * PixelFx.Ppu;
                Sprite[] frames = Spiral(wantPx, out int px);
                FxSprite fx = FxSprite.Spawn(frames, position, echo ? EchoSpiralLife : SpiralLife);
                if (fx == null)
                    return null;
                float scale = wantPx / px;  // only above 1 when the art is capped
                fx.Scale(scale, scale).Fade(0f, 0.12f).Sort(PixelFx.GroundLayer, PixelFx.SortOrder(position.y));
                if (echo)
                    fx.Tint(new Color(1f, 1f, 1f, 0.75f));
                return $"{frames.Length} frames, {px} px, ppu {PixelFx.Ppu}";
            });
            if (!echo)
            {
                PixelFx.Effect("dust suction", () =>
                {
                    PoolManager.GetPoolItem<DustSuctionEffect>().Emit(new int?(10), new Vector3?(position), null, null, 0f, null, null);
                    return "game's DustSuctionEffect";
                });
            }
            PixelFx.Effect("sucked-in debris", () =>
            {
                int count = echo ? MotesPerEcho : MotesPerBurst;
                int made = 0;
                for (int i = 0; i < count; i++)
                {
                    float a = (i + Random.value * 0.6f) / count * Mathf.PI * 2f;
                    float r = pullRadius * MoteStartRadius * Random.Range(0.8f, 1.05f);
                    var start = position + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * SpiralFlatten) * r;
                    FxSprite fx = FxSprite.Spawn(PixelFx.RandomDebris(), start, MoteLife * Random.Range(0.85f, 1.15f), MoteFps);
                    if (fx == null)
                        break;
                    // Depth-sorted by its own height, so enemies being pulled in overlap it naturally.
                    fx.SpiralIn(position, r, a, MoteSpin, SpiralFlatten).Fade(0.06f, 0.1f).Scale(1f, 0.5f).SortWithActors(1);
                    made++;
                }
                return made > 0 ? $"{made} of dust, leaves and pebbles, 4 frames each, ppu {PixelFx.Ppu}" : null;
            });
        }

        // The line is spent: a puff breaking up over the last burst.
        public static void End(Vector2 position)
        {
            PixelFx.Effect("end swirl", () =>
            {
                PoolManager.GetPoolItem<ParticleEffect>("SpinningCircleBurst").Emit(new int?(1), new Vector3?(position), null, null, 0f, null, null);
                PoolManager.GetPoolItem<ParticleEffect>("SpinningAirBurst").Emit(new int?(1), new Vector3?(position + Vector2.up * 0.5f),
                    null, null, 0f, null, null);
                return "game's SpinningCircleBurst and SpinningAirBurst";
            });
            PixelFx.Effect("end puff", () =>
            {
                Sprite[] frames = Puff();
                FxSprite fx = FxSprite.Spawn(frames, position + Vector2.up * 0.4f, PuffLife);
                if (fx == null)
                    return null;
                fx.Drift(Vector2.up * 1.2f, 3f).Scale(PuffScale, PuffScale).Fade(0f, 0.1f).Sort(PixelFx.EffectLayer, 0);
                return $"{frames.Length} frames, {PuffWidth}x{PuffHeight} px, ppu {PixelFx.Ppu}";
            });
        }

        // The pull drawn on the floor: three arms curling in to a bright eye, turning and drawing
        // inward frame by frame; it forms from the rim in, then collapses into the eye.
        private static Sprite[] Spiral(float wantPx, out int px)
        {
            px = Mathf.Clamp(Mathf.RoundToInt(wantPx / SpiralStepPx) * SpiralStepPx, SpiralMinPx, SpiralMaxPx);
            if (spirals.TryGetValue(px, out Sprite[] cached) && PixelFx.Alive(cached))
                return cached;
            int w = px, h = Mathf.Max(8, Mathf.RoundToInt(px * SpiralFlatten));
            // The part of the radius showing in each frame: forming, full, collapsing.
            float[] inner = { 0.55f, 0.3f, 0f, 0f, 0f, 0f, 0f, 0f };
            float[] outer = { 1f, 1f, 1f, 1f, 1f, 0.95f, 0.75f, 0.45f };
            Sprite[] frames = PixelFx.Strip("spiral" + px, w, h, SpiralFrames, PixelFx.Air, (f, x, y) =>
            {
                float u = (x + 0.5f - w * 0.5f) / (w * 0.5f);
                float v = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                float r = Mathf.Sqrt(u * u + v * v);
                if (r > outer[f] || r < inner[f])
                    return 0;
                if (r < 0.1f)
                    return 5;
                if (r < 0.16f)
                    return 4;
                float theta = Mathf.Atan2(v, u);
                // Arms: the phase climbs with angle and radius; adding the frame walks them inward.
                float phase = PixelFx.Frac(SpiralArms * theta / (Mathf.PI * 2f) + SpiralTwist * r + (float)f / SpiralFrames);
                float width = 0.42f * Mathf.Clamp01(r / 0.25f);
                if (phase >= width)
                    return 0;
                // The outer rim breaks up into a dither so the spiral has no hard edge.
                if (r > 0.82f && (x + y) % 2 == 0)
                    return 0;
                bool core = phase < width * 0.4f;
                if (r < 0.4f)
                    return core ? 5 : 4;
                if (r < 0.7f)
                    return core ? 4 : 3;
                return core ? 3 : 2;
            }, new Vector2(0.5f, 0.5f));
            spirals[px] = frames;
            return frames;
        }

        // A wind puff: five lobes pushing out from the middle and thinning away, lit from the top left.
        private static Sprite[] Puff()
        {
            if (PixelFx.Alive(puff))
                return puff;
            float[] spread = { 2f, 4f, 6f, 8f, 9.5f, 10.5f };
            float[] lobe = { 5f, 6f, 6.5f, 5.5f, 4.5f, 3f };
            float[] middle = { 6f, 5f, 3f, 0f, 0f, 0f };
            float cx = PuffWidth * 0.5f, cy = PuffHeight * 0.5f;
            puff = PixelFx.Strip("puff", PuffWidth, PuffHeight, PuffFrames, PixelFx.Air, (f, x, y) =>
            {
                float px = x + 0.5f - cx, py = (y + 0.5f - cy) / 0.75f;
                if (f >= 4 && (x * 3 + y + f) % (f == 4 ? 5 : 3) == 0)
                    return 0;
                int best = 0;
                for (int k = -1; k < 5; k++)
                {
                    float bx = px, by = py, br = middle[f];
                    if (k >= 0)
                    {
                        float a = k * Mathf.PI * 0.4f + f * 0.14f + 0.3f;
                        bx -= Mathf.Cos(a) * spread[f];
                        by -= Mathf.Sin(a) * spread[f];
                        br = lobe[f];
                    }
                    if (br <= 0f || bx * bx + by * by > br * br)
                        continue;
                    // Light from the top left: bright on that side of each lobe, shade opposite.
                    float s = (-bx + by) / br;
                    int c = s > 0.45f ? 5 : s > 0f ? 4 : s > -0.5f ? 3 : 2;
                    best = Mathf.Max(best, c);
                }
                return best;
            }, new Vector2(0.5f, 0.5f));
            return puff;
        }
    }
}
