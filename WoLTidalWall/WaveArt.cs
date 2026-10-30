using UnityEngine;

namespace WoLTidalWall
{
    // Tidal Wall's effect animations, in the game's own water style. Where the game has a fitting
    // sheet its frames are used as they are (cut from the loaded texture): WaterHitSmall and
    // WaterHitEffect (the splash crowns its water hits throw up), WaterParticles (droplets that
    // splash where they land), BubblePop (the bubble projectiles' pop) and Smoke (tinted to mist).
    // Everything else is painted in code the way those sheets are drawn: flat colours sampled from
    // them (pale b3d1e3, mid 8bb4cd, deep 6299bb, dark teal 447d92 and white), no outlines, built
    // from tapering vertical shards and streaks, breaking up into specks at the end and fading by
    // alpha. Each painted animation is also the stand-in if its game sheet isn't loaded. Sizes are
    // in the game's pixels (16 to a unit).
    internal static class WaveArt
    {
        // Sampled from WaterBombExplosion, WaterHitEffect, WaterArc and WaterExplosion.
        private static readonly Color32[] Water =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x44, 0x7d, 0x92, 0xff),   // 1 dark teal
            new Color32(0x62, 0x99, 0xbb, 0xff),   // 2 deep
            new Color32(0x8b, 0xb4, 0xcd, 0xff),   // 3 mid
            new Color32(0xb3, 0xd1, 0xe3, 0xff),   // 4 pale
            new Color32(0xff, 0xff, 0xff, 0xff),   // 5 white
        };
        // The same on the floor, see-through so the floor shows through wet patches.
        private static readonly Color32[] Wet =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x44, 0x7d, 0x92, 0x90),
            new Color32(0x62, 0x99, 0xbb, 0x80),
            new Color32(0x8b, 0xb4, 0xcd, 0x98),
            new Color32(0xb3, 0xd1, 0xe3, 0xb8),
            new Color32(0xff, 0xff, 0xff, 0xc8),
        };
        // Mist (stand-in for the tinted Smoke sheet): the pale tones, see-through.
        private static readonly Color32[] Mist =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x8b, 0xb4, 0xcd, 0x60),
            new Color32(0xb3, 0xd1, 0xe3, 0x90),
            new Color32(0xff, 0xff, 0xff, 0xa8),
        };
        private const int Dark = 1, Deep = 2, Mid = 3, Pale = 4, White = 5;
        private const float TwoPi = Mathf.PI * 2f;
        // The tint that turns the game's white Smoke sheet into pale water mist.
        public static readonly Color MistTint = new Color(0.70f, 0.82f, 0.89f, 0.7f);

        // ---- Game sheets ----

        // Splash crown (WaterHitSmall: five 48 px frames of a small crown of water breaking into
        // specks): each column's rise, and the spray thrown off the rolling front.
        public static PixelAnim SmallCrown => PixelArt.FromSheet("small crown", "water splash crowns", "WaterHitSmall", 48, 48,
            new[] { 0, 1, 2, 3, 4 }, 14f, -1, -1, new Vector2(0.5f, 0.5f), () => Surge);

        // Big crown (WaterHitEffect: five 48 px frames, a wider crown): the crash, shown larger.
        public static PixelAnim BigCrown => PixelArt.FromSheet("big crown", "crash splash", "WaterHitEffect", 48, 48,
            new[] { 0, 1, 2, 3, 4 }, 12f, -1, -1, new Vector2(0.5f, 0.5f), () => Crown);

        // A droplet (WaterParticles: five rows of a drop that splashes into specks, 48 px cells).
        // The first frame holds while it flies; the rest play where it lands.
        public static PixelAnim Droplet(int variant) => PixelArt.FromSheet("droplet" + variant, "spray droplets", "WaterParticles", 48, 48,
            new[] { variant * 5, variant * 5 + 1, variant * 5 + 2, variant * 5 + 3, variant * 5 + 4 }, 14f, 0, 0,
            new Vector2(0.5f, 0.5f), () => PaintedDroplet);

        public const int DropletVariants = 5;

        // A bubble popping (BubblePop: six 32 px frames; the first, a white flash, is skipped).
        public static PixelAnim Bubble => PixelArt.FromSheet("bubble", "churn bubbles", "BubblePop", 32, 32,
            new[] { 1, 2, 3, 4, 5 }, 10f, -1, -1, new Vector2(0.5f, 0.5f), () => PaintedBubble);

        // A puff of mist (Smoke: a white puff shrinking away over four 16 px frames; tinted with
        // MistTint where it's played).
        public static PixelAnim MistPuff => PixelArt.FromSheet("mist", "crash mist", "Smoke", 16, 16,
            new[] { 0, 1, 2, 3 }, 5f, -1, -1, new Vector2(0.5f, 0.5f), () => PaintedMist);

        // ---- Painted in the game's style ----

        // Foam crest: the jagged top of the water riding the wall, shards of water leaning forward
        // and tipped with white, spray flicking off; six looping frames, then five of it breaking
        // into specks. 32x20, wide enough that neighbouring columns' crests overlap into one line
        // (the owner found the 24x14 crests small and not flowing together).
        public const int CrestWidth = 32, CrestHeight = 20;
        public static PixelAnim Crest => PixelArt.Paint("crest", "foam crest", CrestWidth, CrestHeight, 11, 12f, 0, 5,
            new Vector2(0.5f, 0.2f), Water, (c, f) =>
            {
                const int W = CrestWidth;
                const float K = 1.4f;   // the old 24x14 drawing, scaled up
                bool outro = f >= 6;
                float q = outro ? (f - 5) / 5f : 0f;
                float p = (outro ? 5 : f) / 6f;
                for (int x = 0; x < W; x++)
                {
                    // Two humps across the width, rolling forward one hump a loop.
                    float wave = Mathf.Sin(x / (float)W * TwoPi * 2f - p * TwoPi);
                    int top = Mathf.RoundToInt((4f + 1.5f * wave) * K * (1f - q * 0.7f));
                    // Vertical shards of the four tones, the way the game's splashes are drawn.
                    int shard = (x * 7 + 3) % 5;
                    for (int y = 0; y <= top; y++)
                    {
                        int colour = y >= top - 1 ? Pale : shard == 0 ? Dark : shard == 1 || shard == 3 ? Deep : Mid;
                        if (y <= 1 && shard != 0)
                            colour = Deep;
                        c.Set(x, y, colour);
                    }
                }
                // Tongues of water leaning forward off the humps, tipped with white.
                for (int k = 0; k < 5; k++)
                {
                    float at = (k * 6.4f + 2f + p * 16f) % W;
                    float lean = (1.5f + (k % 2)) * K;
                    float h = (k % 2 == 0 ? 5.5f : 4f) * K * (1f - q * 0.7f);
                    float baseY = 4f * K * (1f - q * 0.7f);
                    if (!outro)
                    {
                        c.Stroke(at, baseY, at + lean, baseY + h, 3.5f, Pale);
                        c.Stroke(at - 1f, baseY, at + lean - 1f, baseY + h - 2f, 2.5f, Mid);
                        c.Set(at + lean, baseY + h + 1f, White);
                        c.Set(at + lean + 1f, baseY + h, White);
                    }
                }
                // Spray: loose specks above (the game's splashes end the same way).
                for (int k = 0; k < 4; k++)
                {
                    float at = (k * 8f + 5f + p * 16f) % W;
                    float up = outro ? 8f + q * 5f : 13f + (f + k) % 3;
                    c.Set(at, up, k == 1 ? White : Pale);
                    if (outro)
                        c.Set(at + 2f + q * 4f, up - 2f, Pale);
                }
                if (outro)
                    c.Scatter(1f - q * 0.8f);
            });

        // Surge: a jet of water shards bursting up, its crown breaking into specks (the stand-in
        // for the game's WaterHitSmall crown). Seven frames, 16x28.
        public static PixelAnim Surge => PixelArt.Paint("surge", "water splash crowns", 16, 28, 7, 16f, -1, -1,
            new Vector2(0.5f, 0f), Water, (c, f) =>
            {
                int[] heights = { 7, 15, 23, 26, 25, 18, 11 };
                int h = heights[f];
                float thin = f < 4 ? 1f : 1f - (f - 3) / 4f;
                // Shards side by side, each its own tone, tallest in the middle.
                int[] tones = { Dark, Mid, Pale, White, Pale, Mid, Deep };
                for (int i = 0; i < tones.Length; i++)
                {
                    float u = i / (tones.Length - 1f) * 2f - 1f;
                    float x = 8f + u * 3f * thin;
                    float top = h * (1f - 0.4f * u * u);
                    c.Stroke(x, 0f, x + u * 1.5f, top, 2.4f * thin + 0.6f, tones[i]);
                }
                if (f >= 3)
                    for (int i = 0; i < 5; i++)
                        c.Set(8f + (i - 2f) * 2.2f * (f - 2), h + 1f - (f - 3) * 2f + i % 2 * 2, i % 2 == 0 ? Pale : White);
                for (int side = -1; side <= 1; side += 2)
                    c.Stroke(8f + side * 2f, 0f, 8f + side * (5f + f * 0.6f), 2f + f * 0.4f, 2f, Mid);
                if (f >= 5)
                    c.Scatter(1f - (f - 4) / 3f * 0.7f);
            });

        // Ripple ring: a ring of water spreading over the floor where the wall rises, catching the
        // light on its far side and deeper on its near side. Seven frames, 48x20, see-through (and
        // faded out by alpha where it's played).
        public static PixelAnim Ripple => PixelArt.Paint("ripple", "rising ripple", 48, 20, 7, 14f, -1, -1,
            new Vector2(0.5f, 0.5f), Wet, (c, f) =>
            {
                float rx = 5f + f * 3.2f, ry = rx * 0.42f;
                c.Ring(24f, 10f, rx, ry, f < 4 ? 2f : 1.2f, Pale);
                for (int y = 0; y < 10; y++)
                    for (int x = 0; x < 48; x++)
                        if (c.Get(x, y) == Pale)
                            c.Set(x, y, Mid);
                if (f > 1)
                    c.Ring(24f, 10f, rx - 4f, ry - 2f, 1f, Deep);
                c.Set(24f - rx * 0.5f, 10f + ry * 0.85f, White);
            });

        // The stand-in droplet: a drop that splashes into specks where it lands (first frame flies).
        // Five frames, 12x12.
        public static PixelAnim PaintedDroplet => PixelArt.Paint("painted droplet", "spray droplets", 12, 12, 5, 14f, 0, 0,
            new Vector2(0.5f, 0.5f), Water, (c, f) =>
            {
                if (f == 0)
                {
                    c.Ellipse(6f, 6f, 1.6f, 2.1f, Mid);
                    c.Set(5, 7, Pale);
                    c.Set(6, 5, Deep);
                    return;
                }
                float spread = f * 1.6f;
                for (int i = 0; i < 5; i++)
                {
                    float a = Mathf.PI * (0.15f + i * 0.175f);
                    c.Set(6f + Mathf.Cos(a) * spread, 5f + Mathf.Sin(a) * spread * 0.8f, i % 2 == 0 ? Pale : Mid);
                    if (f < 3)
                        c.Set(6f + Mathf.Cos(a) * (spread - 1f), 5f + Mathf.Sin(a) * (spread - 1f) * 0.8f, Mid);
                }
                if (f < 3)
                    c.Ellipse(6f, 5f, 2f + f * 0.5f, 0.8f, Deep);
            });

        // Wet trail: a puddle left behind the wave: it splashes out, glints and ripples while it
        // lies there, then dries up, shrinking. Three shapes; 3 frames in, 4 looping, 4 out, 18x9,
        // see-through.
        public static PixelAnim Puddle(int variant) => PixelArt.Paint("puddle" + variant, "wet trail", 18, 9, 11, 8f, 3, 6,
            new Vector2(0.5f, 0.5f), Wet, (c, f) =>
            {
                float[] scales = { 0.45f, 0.75f, 1f, 1f, 1f, 1f, 1f, 0.85f, 0.7f, 0.55f, 0.4f };
                float s = scales[f];
                var rng = new System.Random(variant * 31 + 7);
                float[,] blobs = new float[3, 4];
                for (int b = 0; b < 3; b++)
                {
                    blobs[b, 0] = 9f + (float)(rng.NextDouble() * 8.0 - 4.0);
                    blobs[b, 1] = 4.5f + (float)(rng.NextDouble() * 2.4 - 1.2);
                    blobs[b, 2] = 3f + (float)rng.NextDouble() * 2f;
                    blobs[b, 3] = 1.6f + (float)rng.NextDouble();
                }
                // A lighter wet edge (not a dark outline), then the deeper water inside it.
                for (int b = 0; b < 3; b++)
                    c.Ellipse(9f + (blobs[b, 0] - 9f) * s, 4.5f + (blobs[b, 1] - 4.5f) * s, blobs[b, 2] * s + 0.8f, blobs[b, 3] * s + 0.6f, Mid);
                for (int b = 0; b < 3; b++)
                    c.Ellipse(9f + (blobs[b, 0] - 9f) * s, 4.5f + (blobs[b, 1] - 4.5f) * s, blobs[b, 2] * s, blobs[b, 3] * s, Deep);
                c.Highlight(Mid, Pale);
                if (f == 0)
                {
                    c.Set(2, 7, Pale);
                    c.Set(15, 7, Pale);
                    c.Set(9, 8, White);
                }
                if (f >= 3 && f <= 6)
                {
                    int gx = 5 + (f - 3) * 2;
                    if (c.Get(gx, 5) != 0)
                    {
                        c.Set(gx, 5, Pale);
                        c.Set(gx + 1, 5, White);
                    }
                    if (f == 4 || f == 5)
                        c.Ring(9f, 4.5f, f == 4 ? 2f : 3.2f, f == 4 ? 1f : 1.6f, 1f, Mid);
                }
            });

        // Churning foam round a swept enemy's feet: a band of water with pale foam shards swirling
        // round it, splashes flicking up at the front. Two halves, the back drawn behind the enemy
        // and the front in front. 2 frames in, 6 looping, 3 out (shrinking into specks), 26x14.
        public static PixelAnim Churn(bool front) => PixelArt.Paint(front ? "churn front" : "churn back", "churning foam", 26, 14, 11, 12f, 2, 7,
            new Vector2(0.5f, 0.5f), Water, (c, f) =>
            {
                const float cx = 13f, cy = 7f;
                float grow = f == 0 ? 0.5f : f == 1 ? 0.8f : f >= 8 ? 1f - (f - 7) * 0.15f : 1f;
                float rx = 11f * grow, ry = 4.6f * grow;
                float phase = (f < 2 ? 0 : f - 2) / 6f * TwoPi;
                c.Ring(cx, cy, rx, ry, 2.2f, Mid);
                c.Highlight(Mid, Pale);
                c.Shade(Mid, Deep);
                for (int k = 0; k < 5; k++)
                {
                    float a = k * TwoPi / 5f + phase;
                    float bx = cx + Mathf.Cos(a) * rx * 0.88f, by = cy + Mathf.Sin(a) * ry * 0.88f;
                    c.Stroke(bx - 1f, by - 0.5f, bx + 1f, by + 1.5f, 2.2f, Pale);
                    c.Set(bx, by + 1f, White);
                    c.Set(bx - 1f, by - 1f, Dark);
                }
                c.Keep((x, y) => front ? y <= 6 : y >= 7);
                if (front)
                {
                    for (int k = 0; k < 5; k++)
                    {
                        float a = k * TwoPi / 5f + phase;
                        if (Mathf.Sin(a) < -0.5f)
                            c.Set(cx + Mathf.Cos(a) * rx * 0.88f, cy + Mathf.Sin(a) * ry * 0.88f + 2f + f % 2, White);
                    }
                }
                if (f >= 8)
                    c.Scatter(1f - (f - 7) / 3f * 0.7f);
            });

        // The stand-in bubble: a pale ring with a glint, popping into specks. Five frames, 7x7.
        public static PixelAnim PaintedBubble => PixelArt.Paint("painted bubble", "churn bubbles", 7, 7, 5, 10f, -1, -1,
            new Vector2(0.5f, 0.5f), Water, (c, f) =>
            {
                if (f < 3)
                {
                    float r = 1.6f + f * 0.6f;
                    c.Ring(3.5f, 3.5f, r, r, 1f, Pale);
                    c.Set(2, 4, White);
                    return;
                }
                int d = f == 3 ? 2 : 3;
                c.Set(3 - d, 3 + d, Pale);
                c.Set(3 + d, 3 + d, White);
                c.Set(3 - d, 3 - d, Pale);
                c.Set(3 + d, 3 - d, Mid);
            });

        // The stand-in crash crown: a crown of tapering water shards thrown up and out round a
        // central spout, breaking into falling specks. Eight frames, 40x36.
        public static PixelAnim Crown => PixelArt.Paint("crown", "crash splash", 40, 36, 8, 14f, -1, -1,
            new Vector2(0.5f, 0.08f), Water, (c, f) =>
            {
                float[] heights = { 8f, 16f, 24f, 30f, 32f, 28f, 20f, 12f };
                float h = heights[f];
                float open = Mathf.Min(1f, f / 4f);
                c.Ellipse(20f, 3f, 7f + f * 1.3f, 2.5f + f * 0.2f, Deep);
                c.Ellipse(20f, 3.3f, (7f + f * 1.3f) * 0.6f, 1.5f, Mid);
                int[] tones = { Dark, Mid, Pale, White, Pale, Mid, Deep };
                for (int k = 0; k < tones.Length; k++)
                {
                    float u = k / (tones.Length - 1f) * 2f - 1f;
                    float x0 = 20f + u * 6f;
                    float tipX = 20f + u * (6f + 11f * open);
                    float tipY = h * (1f - 0.35f * u * u);
                    if (f <= 4)
                        c.Stroke(x0, 3f, tipX, tipY, 3.5f, tones[k]);
                    else
                    {
                        float reach = 1f - (f - 4) / 4f;
                        c.Stroke(x0, 3f, Mathf.Lerp(x0, tipX, reach), Mathf.Lerp(3f, tipY, reach), 3f, tones[k]);
                        for (int j = 0; j < 3; j++)
                            c.Set(tipX + u * j * 2f, tipY - (f - 4) * 3f - j * 2f, j == 0 ? White : Pale);
                    }
                }
                if (f >= 6)
                    c.Scatter(1f - (f - 5) / 3f * 0.6f);
            });

        // The stand-in mist: a soft puff of spray swelling and thinning. Five frames, 16x12.
        public static PixelAnim PaintedMist => PixelArt.Paint("painted mist", "crash mist", 16, 12, 5, 6f, -1, -1,
            new Vector2(0.5f, 0.5f), Mist, (c, f) =>
            {
                float g = 0.7f + f * 0.08f - (f >= 3 ? (f - 2) * 0.2f : 0f);
                c.Ellipse(6f, 6f, 3.6f * g, 3f * g, 2);
                c.Ellipse(10f, 6.5f, 3.2f * g, 2.8f * g, 2);
                c.Highlight(2, 3);
                c.Shade(2, 1);
            });

        // Foam spread: the crash's foam washing out over the floor in a lumpy pale rim round deeper
        // water, bubbling, then drawing back in. 4 frames in, 2 looping, 4 out, 48x20, see-through.
        public static PixelAnim FoamSpread => PixelArt.Paint("foam spread", "crash foam", 48, 20, 10, 10f, 4, 5,
            new Vector2(0.5f, 0.5f), Wet, (c, f) =>
            {
                float[] scales = { 0.4f, 0.6f, 0.8f, 1f, 1f, 1f, 0.9f, 0.75f, 0.6f, 0.45f };
                float s = scales[f];
                float rx = 20f * s, ry = 8f * s;
                c.Ellipse(24f, 10f, rx - 2f, ry - 2f, Deep);
                for (int k = 0; k < 64; k++)
                {
                    float a = k / 64f * TwoPi;
                    float lump = 1f + 0.12f * Mathf.Sin(a * 7f + f);
                    c.Ellipse(24f + Mathf.Cos(a) * rx * lump, 10f + Mathf.Sin(a) * ry * lump, 1.6f, 1.2f, Pale);
                }
                c.Shade(Pale, Mid);
                var rng = new System.Random(f < 4 ? 3 : f % 2 + 11);
                for (int i = 0; i < 16; i++)
                {
                    float a = (float)(rng.NextDouble() * TwoPi), r = (float)rng.NextDouble() * 0.8f;
                    float bx = 24f + Mathf.Cos(a) * rx * r, by = 10f + Mathf.Sin(a) * ry * r;
                    if (i % 3 == 0)
                        c.Ring(bx, by, 1.6f, 1.6f, 1f, Pale);
                    else
                        c.Set(bx, by, i % 2 == 0 ? Mid : White);
                }
            });
    }
}
