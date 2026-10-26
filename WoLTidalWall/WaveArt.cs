using UnityEngine;

namespace WoLTidalWall
{
    // Tidal Wall's drawn animations, painted in code in a small water palette (the game's water
    // arcana are deep blue to cyan with white foam), with dark outlines, light from the top and
    // dithered dissolves. Sizes are in the game's pixels (16 to a unit). Each is built the first
    // time it plays (PixelArt), then reused.
    internal static class WaveArt
    {
        // Water: outline, deep, mid, light, foam, white.
        private static readonly Color32[] Water =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x18, 0x34, 0x6e, 0xff),
            new Color32(0x2a, 0x5e, 0xb4, 0xff),
            new Color32(0x3c, 0x92, 0xdc, 0xff),
            new Color32(0x7c, 0xcc, 0xf6, 0xff),
            new Color32(0xd6, 0xf2, 0xff, 0xff),
            new Color32(0xff, 0xff, 0xff, 0xff),
        };
        // The same on the floor, see-through so the floor shows through wet patches.
        private static readonly Color32[] Wet =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x1c, 0x40, 0x80, 0xa0),
            new Color32(0x2a, 0x5e, 0xb4, 0x88),
            new Color32(0x3c, 0x92, 0xdc, 0x98),
            new Color32(0x7c, 0xcc, 0xf6, 0xb8),
            new Color32(0xd6, 0xf2, 0xff, 0xd0),
            new Color32(0xff, 0xff, 0xff, 0xe0),
        };
        // Mist: four pale, see-through blues.
        private static readonly Color32[] Mist =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x9c, 0xc8, 0xe8, 0x70),
            new Color32(0xc4, 0xe4, 0xf8, 0x90),
            new Color32(0xe8, 0xf6, 0xff, 0xb0),
            new Color32(0xff, 0xff, 0xff, 0xc8),
        };
        private const int Outline = 1, Deep = 2, Mid = 3, Light = 4, Foam = 5, White = 6;
        private const float TwoPi = Mathf.PI * 2f;

        // ---- The standing wall ----

        // Foam crest: a whitecap curling along the top of each water column. Six looping frames of
        // two humps rolling forward with curling lips and flicked spray, then five frames of the
        // crest breaking up into spray. 24x12.
        public static PixelAnim Crest => PixelArt.Get("crest", "foam crest", 24, 12, 11, 12f, 0, 5,
            new Vector2(0.5f, 0.2f), Water, (c, f) =>
            {
                bool outro = f >= 6;
                float q = outro ? (f - 5) / 5f : 0f;   // how far it has broken up
                float p = (outro ? 5 : f) / 6f;        // the roll's phase
                for (int x = 0; x < 24; x++)
                {
                    float wave = Mathf.Sin(x / 24f * TwoPi * 2f - p * TwoPi);
                    int top = Mathf.RoundToInt((4f + 1.5f * wave) * (1f - q * 0.8f));
                    for (int y = 0; y <= top; y++)
                        c.Set(x, y, y == 0 ? Deep : y >= top - 1 ? Light : Mid);
                    if (wave > 0.2f && !outro)
                    {
                        c.Set(x, top + 1, Foam);
                        if (wave > 0.7f)
                            c.Set(x, top + 2, Foam);
                    }
                }
                // The curling lips ride the humps' peaks (and wrap round the edge).
                int lipTop = Mathf.RoundToInt(5.5f * (1f - q * 0.8f));
                for (int k = 0; k < 2; k++)
                {
                    float peak = (k * 12f + 3f + p * 12f) % 24f;
                    for (int wrap = -24; wrap <= 0; wrap += 24)
                    {
                        float px = peak + wrap;
                        if (outro)
                            continue;
                        c.Ellipse(px + 1.5f, lipTop + 1f, 2.6f, 1.6f, Foam);
                        c.Set(px + 3f, lipTop + 1f, Foam);
                        c.Set(px + 4f, lipTop, Foam);
                        c.Set(px + 1f, lipTop + 2f, White);
                        c.Set(px, lipTop + 2f, White);
                    }
                }
                c.Outline(Outline);
                // Spray flicked off the lips (after the outline: loose specks aren't outlined).
                for (int k = 0; k < 2; k++)
                {
                    float peak = (k * 12f + 3f + p * 12f) % 24f;
                    for (int wrap = -24; wrap <= 0; wrap += 24)
                    {
                        float px = peak + wrap;
                        if (!outro)
                        {
                            c.Set(px + 2f + f % 2, lipTop + 4f + f % 3, White);
                            c.Set(px - 1f, lipTop + 4f + (f + 1) % 3, Light);
                        }
                        else
                        {
                            // Breaking: the foam flies up and apart.
                            for (int i = 0; i < 4; i++)
                                c.Set(px + (i - 1.5f) * (2f + q * 4f), lipTop + 2f + q * 5f - i % 2, i % 2 == 0 ? White : Foam);
                        }
                    }
                }
                if (outro)
                    c.Dissolve(q * 0.75f);
            });

        // Surge: a jet of water bursting up where each column rises, its crown breaking into
        // droplets, with a splash at its foot. Seven frames, 16x28.
        public static PixelAnim Surge => PixelArt.Get("surge", "rising surge", 16, 28, 7, 16f, -1, -1,
            new Vector2(0.5f, 0f), Water, (c, f) =>
            {
                int[] heights = { 7, 15, 23, 26, 25, 18, 11 };
                int h = heights[f];
                float thin = f < 4 ? 1f : 1f - (f - 3) / 4f;
                int body = f >= 5 ? Mathf.RoundToInt(h * 0.6f) : h;
                for (int y = 0; y < body; y++)
                {
                    float w = (2.6f * (1f - 0.35f * y / h)) * thin + 0.6f;
                    for (int x = 0; x < 16; x++)
                    {
                        float d = x + 0.5f - 8f;
                        if (Mathf.Abs(d) <= w)
                            c.Set(x, y, Mathf.Abs(d) < w * 0.4f ? Foam : d < 0f ? Light : Mid);
                    }
                }
                if (f < 5)
                {
                    c.Ellipse(8f, h - 1.5f, 3.2f - f * 0.2f, 2.2f, Foam);
                    c.Set(7, h - 1, White);
                    c.Set(8, h, White);
                }
                if (f >= 3)
                {
                    // The crown breaks into droplets.
                    for (int i = 0; i < 4; i++)
                        c.Set(8f + (i - 1.5f) * 2.5f * (f - 2), h + 1f - (f - 3) * 2f + i % 2 * 2, i % 2 == 0 ? Foam : Light);
                }
                // The splash at its foot, spreading out both ways.
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        int x = 8 + side * (3 + i + f / 2);
                        int y = Mathf.RoundToInt((1.5f + f * 0.4f) * Mathf.Sin(i / 3f * Mathf.PI));
                        c.Set(x, y, Light);
                        c.Set(x, y + 1, Foam);
                    }
                }
                c.Outline(Outline);
                if (f >= 5)
                    c.Dissolve((f - 4) / 3f * 0.7f);
            });

        // Ripple ring: a ring of water spreading over the floor where the wall rises. Seven
        // frames, 48x20, see-through.
        public static PixelAnim Ripple => PixelArt.Get("ripple", "rising ripple", 48, 20, 7, 14f, -1, -1,
            new Vector2(0.5f, 0.5f), Wet, (c, f) =>
            {
                float rx = 5f + f * 3.2f, ry = rx * 0.42f;
                float th = f < 4 ? 2f : 1.2f;
                c.Ring(24f, 10f, rx, ry, th, Light);
                c.Highlight(Light, Foam);
                if (f > 1)
                    c.Ring(24f, 10f, rx - 4f, ry - 2f, 1f, Mid);
                if (f >= 4)
                    c.Dissolve((f - 3) / 4f * 0.8f);
            });

        // ---- The rolling front ----

        // A droplet flung off the front: a little teardrop with a glint that turns as it flies.
        // Four looping frames, 5x6 (hand drawn).
        public static PixelAnim Droplet => PixelArt.Get("droplet", "spray droplets", 5, 6, 4, 10f, 0, 3,
            new Vector2(0.5f, 0.5f), Water, (c, f) =>
            {
                const string key = ".123456";
                string[][] frames =
                {
                    new[] { "..1..", ".161.", "15431", "14331", "13321", ".111." },
                    new[] { "..1..", ".151.", "16431", "14331", "13321", ".111." },
                    new[] { "..1..", ".141.", ".161.", "14431", "13321", ".111." },
                    new[] { "..1..", ".141.", "14561", "14331", "13321", ".111." },
                };
                c.Rows(frames[f], key);
            });

        // Splat: where a droplet lands, a tiny ring of water spreads and fades. Five frames, 9x5.
        public static PixelAnim Splat => PixelArt.Get("splat", "droplet splats", 9, 5, 5, 14f, -1, -1,
            new Vector2(0.5f, 0.5f), Wet, (c, f) =>
            {
                if (f == 0)
                {
                    c.Ellipse(4.5f, 2.5f, 1.6f, 1.1f, Light);
                    c.Set(4, 2, Foam);
                    return;
                }
                float rx = 1.5f + f, ry = 1f + f * 0.4f;
                c.Ring(4.5f, 2.5f, rx, ry, 1f, Light);
                c.Highlight(Light, Foam);
                if (f == 1)
                {
                    c.Set(2, 4, Foam);
                    c.Set(6, 4, Foam);
                }
                if (f >= 3)
                    c.Dissolve((f - 2) / 3f * 0.8f);
            });

        // Spray plume: a fan of spray thrown up off the front, streaks that break into drops.
        // Six frames, 14x16.
        public static PixelAnim Plume => PixelArt.Get("plume", "front spray", 14, 16, 6, 16f, -1, -1,
            new Vector2(0.5f, 0f), Water, (c, f) =>
            {
                float[] lengths = { 4f, 8f, 12f, 14f, 13f, 11f };
                for (int k = 0; k < 5; k++)
                {
                    float angle = Mathf.Lerp(55f, 125f, k / 4f) * Mathf.Deg2Rad;
                    float len = lengths[f] * (0.7f + 0.15f * (k * 7 % 3));
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    if (f < 3)
                    {
                        Vector2 tip = new Vector2(7f, 0f) + dir * len;
                        c.Line(7f, 0f, tip.x, tip.y, Light);
                        c.Line(7f + 1f, 0f, tip.x + 1f, tip.y - 1f, Mid);
                        c.Set(tip.x, tip.y, White);
                    }
                    else
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            Vector2 dot = new Vector2(7f, 0f) + dir * (len * (0.55f + j * 0.2f));
                            dot.y -= (f - 2) * (1 + j);
                            c.Set(dot.x, dot.y, j == 2 ? White : j == 1 ? Foam : Light);
                        }
                    }
                }
                if (f >= 4)
                    c.Dissolve((f - 3) / 3f * 0.6f);
            });

        // Wet trail: a puddle left on the floor behind the wave: it splashes out, glints and
        // ripples while it lies there, then dries up. Three shapes; 3 frames in, 4 looping, 4 out,
        // 18x9, see-through.
        public static PixelAnim Puddle(int variant) => PixelArt.Get("puddle" + variant, "wet trail", 18, 9, 11, 8f, 3, 6,
            new Vector2(0.5f, 0.5f), Wet, (c, f) =>
            {
                float[] scales = { 0.45f, 0.75f, 1f, 1f, 1f, 1f, 1f, 0.9f, 0.75f, 0.6f, 0.45f };
                float s = scales[f];
                var rng = new System.Random(variant * 31 + 7);
                for (int b = 0; b < 3; b++)
                {
                    float bx = 9f + (float)(rng.NextDouble() * 8.0 - 4.0);
                    float by = 4.5f + (float)(rng.NextDouble() * 2.4 - 1.2);
                    float rx = 3f + (float)rng.NextDouble() * 2f, ry = 1.6f + (float)rng.NextDouble();
                    c.Ellipse(9f + (bx - 9f) * s, 4.5f + (by - 4.5f) * s, rx * s, ry * s, Deep);
                }
                // A lighter rim, lit along its top.
                c.Outline(Mid);
                c.Highlight(Mid, Light);
                if (f == 0)
                {
                    c.Set(2, 7, Light);
                    c.Set(15, 7, Light);
                    c.Set(9, 8, Foam);
                }
                if (f >= 3 && f <= 6)
                {
                    // A glint sliding across and a ripple rising from its middle.
                    int gx = 5 + (f - 3) * 2;
                    if (c.Get(gx, 5) != 0)
                    {
                        c.Set(gx, 5, Foam);
                        c.Set(gx + 1, 5, White);
                    }
                    if (f == 4 || f == 5)
                        c.Ring(9f, 4.5f, f == 4 ? 2f : 3.2f, f == 4 ? 1f : 1.6f, 1f, Light);
                }
                if (f >= 8)
                    c.Dissolve((f - 7) / 4f * 0.7f);
            });

        // Churning foam round a swept enemy's feet: a band of water with foam swirling round it
        // and splashes flicking up at the front. Drawn in two halves, the back half behind the
        // enemy and the front half in front of it. 2 frames in, 6 looping, 3 out, 26x14.
        public static PixelAnim Churn(bool front) => PixelArt.Get(front ? "churn front" : "churn back", "churning foam", 26, 14, 11, 12f, 2, 7,
            new Vector2(0.5f, 0.5f), Water, (c, f) =>
            {
                const float cx = 13f, cy = 7f;
                float grow = f == 0 ? 0.5f : f == 1 ? 0.8f : 1f;
                float q = f >= 8 ? (f - 7) / 3f : 0f;
                float rx = 11f * grow, ry = 4.6f * grow;
                float phase = (f < 2 ? 0 : f - 2) / 6f * TwoPi;
                c.Ring(cx, cy, rx, ry, 2.2f, Mid);
                c.Highlight(Mid, Light);
                for (int k = 0; k < 5; k++)
                {
                    float a = k * TwoPi / 5f + phase;
                    float bx = cx + Mathf.Cos(a) * rx * 0.88f, by = cy + Mathf.Sin(a) * ry * 0.88f;
                    c.Ellipse(bx, by, 1.8f, 1.2f, Foam);
                    c.Set(bx, by + 0.6f, White);
                }
                c.Outline(Outline);
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
                if (q > 0f)
                    c.Dissolve(q * 0.8f);
            });

        // A bubble rising off a churned enemy, then popping. Five frames, 5x5.
        public static PixelAnim Bubble => PixelArt.Get("bubble", "churn bubbles", 5, 5, 5, 10f, -1, -1,
            new Vector2(0.5f, 0.5f), Water, (c, f) =>
            {
                if (f < 3)
                {
                    float r = 1.3f + f * 0.5f;
                    c.Ring(2.5f, 2.5f, r, r, 1f, Light);
                    c.Set(1, 3, White);
                    return;
                }
                // Popped: the skin flies apart in four specks, then two.
                int d = f == 3 ? 1 : 2;
                c.Set(2 - d, 2 + d, Foam);
                c.Set(2 + d, 2 + d, White);
                if (f == 3)
                {
                    c.Set(2 - d, 2 - d, Light);
                    c.Set(2 + d, 2 - d, Foam);
                }
            });

        // ---- The crash ----

        // Crash crown: the wave smashing down, a crown of water fingers thrown up and out around a
        // central spout, foam on every tip, then breaking into falling drops. Eight frames, 40x36.
        public static PixelAnim Crown => PixelArt.Get("crown", "crash splash", 40, 36, 8, 14f, -1, -1,
            new Vector2(0.5f, 0.08f), Water, (c, f) =>
            {
                float[] heights = { 8f, 16f, 24f, 30f, 32f, 28f, 20f, 12f };
                float h = heights[f];
                float open = Mathf.Min(1f, f / 4f);
                c.Ellipse(20f, 3f, 7f + f * 1.3f, 2.5f + f * 0.2f, Mid);
                c.Ellipse(20f, 3.3f, (7f + f * 1.3f) * 0.6f, 1.5f, Light);
                const int fingers = 7;
                for (int k = 0; k < fingers; k++)
                {
                    float u = k / (fingers - 1f) * 2f - 1f;
                    float x0 = 20f + u * 6f;
                    float tipX = 20f + u * (6f + 11f * open);
                    float tipY = h * (1f - 0.35f * u * u);
                    float reach = f <= 4 ? 1f : 1f - (f - 4) / 4f;
                    float endX = Mathf.Lerp(x0, tipX, reach), endY = Mathf.Lerp(3f, tipY, reach);
                    int colour = Mathf.Abs(u) < 0.3f ? Foam : u < 0f ? Light : Mid;
                    c.Line(x0, 3f, endX, endY, colour);
                    c.Line(x0 + 1f, 3f, endX + 1f, endY, colour);
                    if (f <= 4)
                    {
                        c.Ellipse(tipX + 0.5f, tipY, 2f, 1.5f, Foam);
                        c.Set(tipX, tipY + 1f, White);
                    }
                    else
                    {
                        for (int j = 0; j < 3; j++)
                            c.Set(tipX + u * j * 2f, tipY - (f - 4) * 3f - j * 2f, j == 0 ? White : j == 1 ? Foam : Light);
                    }
                }
                if (f <= 4)
                {
                    // The central spout.
                    int top = Mathf.RoundToInt(h * 0.75f);
                    for (int y = 2; y < top; y++)
                    {
                        float w = 2.2f * (1f - (float)y / top) + 0.8f;
                        for (int x = 0; x < 40; x++)
                            if (Mathf.Abs(x + 0.5f - 20f) <= w)
                                c.Set(x, y, Mathf.Abs(x + 0.5f - 20f) < w * 0.5f ? White : Foam);
                    }
                }
                c.Outline(Outline);
                if (f >= 6)
                    c.Dissolve((f - 5) / 3f * 0.7f);
            });

        // Mist: a soft puff of spray hanging in the air after the crash, swelling and thinning
        // away in dithered steps. Seven frames, 22x14, see-through.
        public static PixelAnim MistPuff => PixelArt.Get("mist", "crash mist", 22, 14, 7, 9f, -1, -1,
            new Vector2(0.5f, 0.3f), Mist, (c, f) =>
            {
                float g = 0.55f + f * 0.09f;
                float[,] blobs = { { 8f, 6f, 4.5f }, { 13f, 7.5f, 5f }, { 17f, 6f, 3.5f }, { 11f, 4f, 4f } };
                for (int b = 0; b < 4; b++)
                    c.Ellipse(11f + (blobs[b, 0] - 11f) * g, 6f + (blobs[b, 1] - 6f) * g, blobs[b, 2] * g, blobs[b, 2] * g * 0.8f, 2);
                c.Highlight(2, 3);
                c.Shade(2, 1);
                c.Set(10, 10, 4);
                if (f >= 1)
                    c.Dissolve(f / 7f * 0.85f);
            });

        // Foam spread: the crash's foam washing out over the floor, bubbling, then breaking up.
        // 4 frames in, 2 looping, 4 out, 48x20, see-through.
        public static PixelAnim FoamSpread => PixelArt.Get("foam spread", "crash foam", 48, 20, 10, 10f, 4, 5,
            new Vector2(0.5f, 0.5f), Wet, (c, f) =>
            {
                float[] scales = { 0.4f, 0.6f, 0.8f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
                float s = scales[f];
                float rx = 20f * s, ry = 8f * s;
                c.Ellipse(24f, 10f, rx - 2f, ry - 2f, Mid);
                // A lumpy rim of foam.
                for (int k = 0; k < 64; k++)
                {
                    float a = k / 64f * TwoPi;
                    float lump = 1f + 0.12f * Mathf.Sin(a * 7f + f);
                    c.Ellipse(24f + Mathf.Cos(a) * rx * lump, 10f + Mathf.Sin(a) * ry * lump, 1.6f, 1.2f, Foam);
                }
                var rng = new System.Random(f < 4 ? 3 : f % 2 + 11);
                for (int i = 0; i < 16; i++)
                {
                    float a = (float)(rng.NextDouble() * TwoPi), r = (float)rng.NextDouble() * 0.8f;
                    float bx = 24f + Mathf.Cos(a) * rx * r, by = 10f + Mathf.Sin(a) * ry * r;
                    if (i % 3 == 0)
                        c.Ring(bx, by, 1.6f, 1.6f, 1f, Foam);
                    else
                        c.Set(bx, by, Light);
                }
                if (f >= 6)
                    c.Dissolve((f - 5) / 4f * 0.85f);
            });
    }
}
