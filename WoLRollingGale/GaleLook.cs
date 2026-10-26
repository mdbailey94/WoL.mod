using System.Collections.Generic;
using UnityEngine;

namespace WoLRollingGale
{
    // What Rolling Gale looks like, on top of the game's air vortex, wind burst and dust (drawn by
    // the state). Visual only: nothing here touches the hits. The game's own sprites are used
    // wherever they're loaded (drawn stand-ins in the game's style otherwise):
    // - Cast: the game's dash air burst and air shockwave at the wizard, and its large wind slash
    //   rolling off along the aim.
    // - Travel: the game's wind slashes (and wind trail) rolling from one burst to the next.
    // - Each burst: a spiral on the floor where it pulls, painted in the game's wind tones; the
    //   game's air-spin curl winding up at its eye; dust puffs, rocks, leaves and wind wisps
    //   spiralling in; and the game's dust suction. Echo pulses get a smaller, quicker spiral.
    // - End: the game's wind twirl and a wind puff breaking into curls over the last burst, with
    //   the game's vortex-trap ending (spinning circle and air bursts).
    internal static class GaleLook
    {
        // Cast
        private const float CastCrescentFrom = 0.6f;   // units along the aim from the wizard
        private const float CastCrescentTo = 2.2f;
        private const float CastCrescentLife = 0.26f;
        // Travel: one slash down the middle and two smaller ones either side.
        private const float RollLife = 0.26f;
        private const float RollSideOffset = 0.7f;      // units to either side of the line
        private const float RollSideScale = 0.75f;      // drawn stand-ins only (the game has small slashes)
        private const float RollOvershoot = 0.8f;       // units past the burst the slash carries on
        // Spiral (the pull)
        private const int SpiralFrames = 8;
        private const int SpiralArms = 3;
        private const float SpiralTwist = 1.6f;         // turns from the eye to the rim
        private const float SpiralFill = 0.8f;          // drawn diameter, as a fraction of the burst's pull area
        private const float SpiralFlatten = 0.62f;      // height / width: a circle on the floor, seen at the game's angle
        private const int SpiralMinPx = 24, SpiralMaxPx = 96, SpiralStepPx = 8;
        private const float SpiralLife = 0.45f;
        private const float EchoSpiralLife = 0.32f;
        private const float EyeLife = 0.42f;            // the game's air-spin curl at the eye
        // Debris sucked in to each burst
        private const int MotesPerBurst = 7;
        private const int MotesPerEcho = 3;
        private const int WispsPerBurst = 2;
        private const float MoteStartRadius = 0.85f;    // of the burst's pull area
        private const float MoteLife = 0.42f;
        private const float MoteSpin = -5f;             // radians a second, clockwise like the spiral's arms
        private const float MoteFps = 12f;
        // End puff
        private const int PuffWidth = 32, PuffHeight = 24, PuffFrames = 6;
        private const float PuffLife = 0.4f;
        private const float PuffScale = 1.5f;           // drawn stand-in only

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
                FxSprite fx = Slash("Large", 1f, origin + direction * CastCrescentFrom, CastCrescentLife, direction, out string what);
                if (fx == null)
                    return null;
                fx.Line(origin + direction * CastCrescentFrom, origin + direction * CastCrescentTo).Sort(PixelFx.EffectLayer, 0);
                return what;
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
                var side = new Vector2(-direction.y, direction.x) * RollSideOffset;
                Vector2 end = to + direction * RollOvershoot;
                int made = 0;
                string what = null;
                for (int i = -1; i <= 1; i++)
                {
                    FxSprite fx = Slash(i == 0 ? "Med" : "Small", i == 0 ? 1f : RollSideScale, from + side * i,
                        RollLife * (i == 0 ? 1f : 1.15f), direction, out string kind);
                    if (fx == null)
                        continue;
                    fx.Line(from + side * i, end + side * (i * 0.5f)).Sort(PixelFx.EffectLayer, i == 0 ? 1 : 0);
                    what = kind;
                    made++;
                }
                return made > 0 ? $"{made} x {what}" : null;
            });
        }

        // One of the game's wind slashes turned to face `direction`, or the drawn crescent.
        private static FxSprite Slash(string size, float drawnScale, Vector2 at, float life, Vector2 direction, out string what)
        {
            Sprite[] game = PixelFx.GameSlash(size);
            if (game != null)
            {
                what = $"game's WindSlash{size}0-6";
                FxSprite fx = FxSprite.Spawn(game, at, life);
                return fx?.Rotate(PixelFx.SlashAngle(direction)).Fade(0f, life * 0.3f);
            }
            Sprite[] drawn = PixelFx.Crescent(direction);
            what = $"drawn crescent, {drawn.Length} frames, ppu {PixelFx.Ppu}";
            return FxSprite.Spawn(drawn, at, life)?.Scale(drawnScale, drawnScale);
        }

        // A burst pulling: the spiral on the floor, the curl at its eye, and debris sucked in to it.
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
                fx.Scale(scale, scale * 0.85f).Fade(0f, 0.15f).Sort(PixelFx.GroundLayer, PixelFx.SortOrder(position.y));
                if (echo)
                    fx.Tint(new Color(1f, 1f, 1f, 0.7f));
                return $"{frames.Length} frames, {px} px, game wind tones, ppu {PixelFx.Ppu}";
            });
            if (!echo)
            {
                PixelFx.Effect("spiral eye", () =>
                {
                    Sprite[] spin = GameArt.AirSpin();
                    if (spin == null)
                        return null;  // the floor spiral already has its own eye
                    FxSprite fx = FxSprite.Spawn(spin, position + Vector2.up * 0.25f, EyeLife);
                    if (fx == null)
                        return null;
                    fx.Fade(0.05f, 0.12f).Sort(PixelFx.EffectLayer, 0);
                    return "game's AirSpin0-3 + AirSpinAlt0-2";
                });
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
                    Sprite[] frames = PixelFx.RandomDebris(out Color tint);
                    if (SpiralIn(frames, tint, position, pullRadius, (i + Random.value * 0.6f) / count) == null)
                        break;
                    made++;
                }
                return made > 0 ? $"{made} of dust puffs, rocks and leaves (game's Dust and Rocks when loaded), ppu {PixelFx.Ppu}" : null;
            });
            if (!echo)
            {
                PixelFx.Effect("sucked-in wisps", () =>
                {
                    Sprite[] wisps = GameArt.WindWisps();
                    if (wisps == null)
                        return null;
                    int made = 0;
                    for (int i = 0; i < WispsPerBurst; i++)
                    {
                        if (SpiralIn(wisps, Color.white, position, pullRadius, Random.value) != null)
                            made++;
                    }
                    return made > 0 ? $"{made} x game's MiscParticleEffects wind wisps" : null;
                });
            }
        }

        // One thing spiralling in to the burst from round its rim (`turn` 0-1 sets where).
        private static FxSprite SpiralIn(Sprite[] frames, Color tint, Vector2 position, float pullRadius, float turn)
        {
            float a = turn * Mathf.PI * 2f;
            float r = pullRadius * MoteStartRadius * Random.Range(0.8f, 1.05f);
            var start = position + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * SpiralFlatten) * r;
            FxSprite fx = FxSprite.Spawn(frames, start, MoteLife * Random.Range(0.85f, 1.15f), MoteFps);
            // Depth-sorted by its own height, so enemies being pulled in overlap it naturally.
            return fx?.SpiralIn(position, r, a, MoteSpin, SpiralFlatten).Fade(0.06f, 0.12f).Scale(1f, 0.5f).Tint(tint).SortWithActors(1);
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
            PixelFx.Effect("end twirl", () =>
            {
                // As the game's twister relic does it: flipped, at a random tilt.
                PoolManager.GetPoolItem<ParticleEffect>("WindTwirlEffect").Emit(new int?(1), new Vector3?(position), ParticleEffectOverrides.FlipX,
                    new Vector3?(new Vector3(0f, 0f, Random.Range(-30f, 30f) + (Random.value < 0.5f ? 180f : 0f))), 0f, null, null);
                return "game's WindTwirlEffect";
            });
            PixelFx.Effect("end puff", () =>
            {
                Sprite[] game = GameArt.Breakup();
                Sprite[] frames = game ?? Puff();
                FxSprite fx = FxSprite.Spawn(frames, position + Vector2.up * 0.4f, PuffLife);
                if (fx == null)
                    return null;
                float scale = game != null ? 1f : PuffScale;
                fx.Drift(Vector2.up * 1.2f, 3f).Scale(scale, scale).Fade(0f, 0.12f).Sort(PixelFx.EffectLayer, 0);
                return game != null ? "game's WindParticles breakup, 6 frames" : $"drawn puff, {frames.Length} frames, {PuffWidth}x{PuffHeight} px, ppu {PixelFx.Ppu}";
            });
        }

        // The pull drawn on the floor in the game's wind tones: three brushed arms curling in to the
        // eye (light leading edge, body, darker trailing edge), turning and drawing inward frame by
        // frame; it forms from the rim in, then collapses into the eye. Tapered, no outline.
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
                if (r < 0.12f)
                    return 4;
                float theta = Mathf.Atan2(v, u);
                // Arms: the phase climbs with angle and radius; adding the frame walks them inward.
                float phase = PixelFx.Frac(SpiralArms * theta / (Mathf.PI * 2f) + SpiralTwist * r + (float)f / SpiralFrames);
                // Brushed: widest a third of the way out, tapering to points at the eye and the rim.
                float width = 0.4f * Mathf.Clamp01(r / 0.3f) * Mathf.Clamp01((1.05f - r) / 0.45f);
                if (phase >= width)
                    return 0;
                float across = phase / Mathf.Max(0.001f, width);
                return across < 0.35f ? 4 : across < 0.75f ? 3 : 2;
            }, new Vector2(0.5f, 0.5f));
            spirals[px] = frames;
            return frames;
        }

        // Drawn stand-in for the game's breakup: five lobes pushing out from the middle and
        // shrinking away, lit from the top left.
        private static Sprite[] Puff()
        {
            if (PixelFx.Alive(puff))
                return puff;
            float[] spread = { 2f, 4f, 6f, 8f, 9.5f, 10.5f };
            float[] lobe = { 5f, 6f, 6.5f, 5.5f, 4f, 2.5f };
            float[] middle = { 6f, 5f, 3f, 0f, 0f, 0f };
            float cx = PuffWidth * 0.5f, cy = PuffHeight * 0.5f;
            puff = PixelFx.Strip("puff", PuffWidth, PuffHeight, PuffFrames, PixelFx.Air, (f, x, y) =>
            {
                float px = x + 0.5f - cx, py = (y + 0.5f - cy) / 0.75f;
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
                    // Light from the top left: light on that side of each lobe, shade opposite.
                    float s = (-bx + by) / br;
                    int c = s > 0.2f ? 4 : s > -0.45f ? 3 : 2;
                    best = Mathf.Max(best, c);
                }
                return best;
            }, new Vector2(0.5f, 0.5f));
            return puff;
        }
    }
}
