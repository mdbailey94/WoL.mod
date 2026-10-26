using UnityEngine;

namespace WoLBrambleRing
{
    // Bramble Ring's drawn animations, painted in code: a living-plant palette (the game's leaf
    // green, LeafEmitter.LeafColor.Green, with darker and lighter greens), bone-pale thorns with
    // red tips, brown soil, and the browns and yellows of withering. Dark outlines, light from the
    // top, dithered dissolves; sizes in the game's pixels (16 to a unit). Each is built the first
    // time it plays (PixelArt), then reused.
    internal static class ThicketArt
    {
        private static readonly Color32[] Plant =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x1c, 0x2a, 0x14, 0xff),   // 1 outline
            new Color32(0x2c, 0x58, 0x26, 0xff),   // 2 dark green
            new Color32(0x4c, 0x88, 0x34, 0xff),   // 3 mid green
            new Color32(0x7b, 0xc1, 0x52, 0xff),   // 4 leaf green (the game's)
            new Color32(0xbc, 0xe4, 0x7c, 0xff),   // 5 highlight
            new Color32(0xe6, 0xd8, 0xa4, 0xff),   // 6 thorn
            new Color32(0xb8, 0x34, 0x34, 0xff),   // 7 thorn tip
            new Color32(0x3e, 0x28, 0x18, 0xff),   // 8 soil dark
            new Color32(0x6a, 0x48, 0x2a, 0xff),   // 9 soil
            new Color32(0x98, 0x72, 0x46, 0xff),   // 10 soil light
            new Color32(0x76, 0x58, 0x28, 0xff),   // 11 withered brown
            new Color32(0xc0, 0xa0, 0x48, 0xff),   // 12 withered yellow
            new Color32(0x5a, 0x40, 0x22, 0xff),   // 13 bark
            new Color32(0xff, 0xff, 0xf0, 0xff),   // 14 white glint
        };
        // Dust: four see-through earth tones.
        private static readonly Color32[] Dust =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x6a, 0x50, 0x34, 0x90),
            new Color32(0x98, 0x7a, 0x54, 0xa8),
            new Color32(0xc0, 0xa6, 0x7e, 0xb8),
            new Color32(0xde, 0xcc, 0xa4, 0xc0),
        };
        private const int Outline = 1, Dark = 2, Mid = 3, Light = 4, Highlight = 5, Thorn = 6, Tip = 7;
        private const int SoilDark = 8, Soil = 9, SoilLight = 10, Brown = 11, Yellow = 12, Bark = 13, White = 14;
        // For hand-drawn rows: a character's position here is its colour.
        private const string Key = ".odmlhtrSMLbykw";
        private const float TwoPi = Mathf.PI * 2f;

        // ---- Sprouting ----

        // Soil burst: the ground under a vine bulges, cracks and bursts open, clods thrown out,
        // then lies churned up (rootlets poking out of the hole) while the thicket stands, and
        // crumbles away. 5 frames in, 1 held, 4 out, 24x12.
        public static PixelAnim SoilBurst => PixelArt.Get("soil", "soil burst", 24, 12, 10, 14f, 5, 5,
            new Vector2(0.5f, 0.5f), Plant, (c, f) =>
            {
                const float cx = 12f, cy = 6f;
                if (f == 0)
                {
                    c.Ellipse(cx, cy, 4f, 2.2f, Soil);
                    c.Highlight(Soil, SoilLight);
                    c.Outline(SoilDark);
                    return;
                }
                if (f == 1)
                {
                    c.Ellipse(cx, cy + 0.5f, 5.5f, 3f, Soil);
                    c.Highlight(Soil, SoilLight);
                    c.Line(9, 7, 15, 6, SoilDark);
                    c.Outline(SoilDark);
                    return;
                }
                float open = f >= 5 ? 1f : (f - 1) / 3f;
                float rx = 6.5f + open * 1.5f, ry = 3.1f + open * 0.5f;
                for (int k = 0; k < 48; k++)
                {
                    if (f >= 5 && (k * 7 + 3) % 5 == 0)
                        continue;   // settled: the heaped earth is broken in places
                    float a = k / 48f * TwoPi;
                    float r = (1f + 0.1f * Mathf.Sin(a * 5f + 1.3f)) * 0.85f;
                    c.Ellipse(cx + Mathf.Cos(a) * rx * r, cy + Mathf.Sin(a) * ry * r, 1.6f, 1.1f, Soil);
                }
                c.Ellipse(cx, cy, rx * 0.55f, ry * 0.5f, SoilDark);
                c.Highlight(Soil, SoilLight);
                // Clods flying out, then lying round it.
                for (int k = 0; k < 6; k++)
                {
                    float a = k * TwoPi / 6f + 0.4f;
                    float d = f >= 5 ? 10f : 6f + (f - 1) * 1.6f;
                    float up = f == 3 ? 1.5f : f == 2 || f == 4 ? 0.8f : 0f;
                    float x = cx + Mathf.Cos(a) * d, y = cy + Mathf.Sin(a) * d * 0.45f + up;
                    c.Set(x, y, SoilLight);
                    c.Set(x + 1f, y, Soil);
                    c.Set(x, y - 1f, Soil);
                }
                if (f >= 4)
                {
                    // Rootlets poking up out of the hole.
                    c.Set(cx - 2f, cy + 0.5f, Mid);
                    c.Set(cx - 2f, cy + 1.5f, Light);
                    c.Set(cx + 2f, cy - 0.5f, Mid);
                    c.Set(cx + 3f, cy + 0.5f, Light);
                }
                c.Outline(SoilDark);
                if (f >= 6)
                    c.Dissolve((f - 5) / 4f * 0.85f);
            });

        // Undergrowth: a thorny shoot pushing up out of the soil between the vines, unfurling two
        // leaves; it sways while the thicket stands, then wilts: it browns, droops, drops its
        // leaves and crumbles. 6 frames in, 8 looping, 5 out, 14x18.
        public static PixelAnim Sprout => PixelArt.Get("sprout", "thorny undergrowth", 14, 18, 19, 10f, 6, 13,
            new Vector2(0.5f, 0f), Plant, (c, f) =>
            {
                float[] grow = { 0.12f, 0.3f, 0.5f, 0.7f, 0.85f, 1f };
                int[] sways = { 0, 0, 1, 1, 0, 0, -1, -1 };
                float g = f < 6 ? grow[f] : 1f;
                float sway = f >= 6 && f <= 13 ? sways[f - 6] : 0f;
                float w = f >= 14 ? (f - 13) / 5f : 0f;
                int stemLight = w > 0.6f ? Yellow : w > 0.2f ? Brown : Mid;
                int stemDark = w > 0.2f ? Bark : Dark;
                int leaf = w > 0.6f ? Yellow : w > 0.2f ? Brown : Light;

                // The soil heaped at its foot.
                c.Ellipse(7f, 0.5f, 3f, 1.2f, Soil);
                c.Highlight(Soil, SoilLight);
                int stemH = Mathf.RoundToInt(2f + 12f * g);
                int topX = 7, topY = 0;
                for (int y = 1; y <= stemH; y++)
                {
                    float t = (float)y / stemH;
                    float bend = sway * t * t * 1.6f + 0.8f * Mathf.Sin(y * 0.5f) + w * 3.5f * t * t;
                    int x = Mathf.RoundToInt(6.5f + bend);
                    int yy = Mathf.RoundToInt(y * (1f - 0.3f * w));
                    c.Set(x, yy, stemLight);
                    c.Set(x + 1, yy, stemDark);
                    topX = x;
                    topY = yy;
                    if (g >= 0.5f && y == Mathf.RoundToInt(stemH * 0.35f))
                    {
                        c.Set(x - 1, yy, Thorn);
                        c.Set(x - 2, yy + 1, Tip);
                    }
                    if (g >= 0.5f && y == Mathf.RoundToInt(stemH * 0.65f))
                    {
                        c.Set(x + 2, yy, Thorn);
                        c.Set(x + 3, yy + 1, Tip);
                    }
                }
                if (g >= 0.6f)
                {
                    // Two pointed leaves opening up and out from the top (unfurling as it grows,
                    // drooping and falling away as it wilts).
                    float size = Mathf.Clamp01((g - 0.6f) / 0.4f);
                    float drop = w > 0.5f ? (w - 0.5f) * 12f : 0f;
                    float len = 2.5f + 3f * size;
                    int vein = w > 0.2f ? Bark : Mid;
                    Leaf(c, topX, topY - 1f - drop, 150f + 70f * w + sway * 8f, len, leaf, vein);
                    Leaf(c, topX + 1f, topY - 2f - drop * 0.6f, 30f - 70f * w + sway * 8f, len, leaf, vein);
                    if (w <= 0.2f)
                        c.Highlight(Light, Highlight);
                }
                c.Set(topX, topY + 1, w > 0.2f ? Brown : Light);
                if (w <= 0.2f && g >= 0.3f)
                    c.Set(topX, topY + 2, Highlight);
                c.Outline(Outline);
                if (w > 0.6f)
                    c.Dissolve((w - 0.6f) * 2f);
            });

        // A pointed leaf len pixels long from (x, y), heading at degrees, widest in its middle,
        // with a midrib down it.
        private static void Leaf(PixelCanvas c, float x, float y, float degrees, float len, int colour, int vein)
        {
            float r = degrees * Mathf.Deg2Rad;
            float dx = Mathf.Cos(r), dy = Mathf.Sin(r);
            for (float t = 0f; t <= len; t += 0.5f)
            {
                float half = Mathf.Sin(t / len * Mathf.PI) * len * 0.3f;
                for (float s = -half; s <= half; s += 0.5f)
                    c.Set(x + dx * t - dy * s, y + dy * t + dx * s, colour);
            }
            for (float t = 1f; t < len - 1f; t += 1f)
                c.Set(x + dx * t, y + dy * t, vein);
        }

        // Creeping root: a thorny tendril snaking over the floor between the rows, growing out to
        // its tip with knots, barbs and a leaf; sap glints along it while the thicket stands, then
        // it dries brown, breaks into pieces and crumbles. 7 frames in, 4 looping, 4 out, 22x12,
        // drawn pointing right from its root end (rotated into place).
        public static PixelAnim Root => PixelArt.Get("root", "creeping roots", 22, 12, 15, 12f, 7, 10,
            new Vector2(0f, 0.5f), Plant, (c, f) =>
            {
                int[] reach = { 3, 6, 9, 12, 15, 18, 20 };
                int length = f < 7 ? reach[f] : 20;
                int pulse = f >= 7 && f <= 10 ? f - 7 : -1;
                float w = f >= 11 ? (f - 10) / 4f : 0f;
                int top = w > 0.3f ? Brown : Mid, under = w > 0.3f ? Bark : Dark;
                for (int x = 0; x <= length; x++)
                {
                    if (w > 0.5f && x % 6 == 3)
                        continue;   // dried out, it breaks apart
                    int y = RootPath(x);
                    c.Set(x, y, top);
                    if (x < length - 2)
                        c.Set(x, y - 1, under);
                }
                for (int node = 5; node <= 17; node += 6)
                {
                    if (node > length - 1)
                        break;
                    int y = RootPath(node);
                    c.Set(node, y + 1, w > 0.3f ? Yellow : Light);
                    if ((node / 6) % 2 == 0)
                    {
                        c.Set(node + 1, y + 2, Thorn);
                        c.Set(node + 2, y + 3, Tip);
                    }
                    else
                    {
                        c.Set(node + 1, y - 2, Thorn);
                        c.Set(node + 2, y - 3, Tip);
                    }
                    if (node == 11)
                    {
                        c.Ellipse(node - 1.5f, y + 2.5f, 1.7f, 1f, w > 0.3f ? Yellow : Light);
                        if (w <= 0.3f)
                            c.Set(node - 2, y + 3, Highlight);
                    }
                }
                if (f < 7)
                    c.Set(length + 1, RootPath(length), Light);   // the growing tip
                if (pulse >= 0)
                {
                    // Sap glinting along it, root to tip.
                    int from = 2 + pulse * 5;
                    for (int i = 0; i < 3; i++)
                        if (c.Get(from + i, RootPath(from + i)) != 0)
                            c.Set(from + i, RootPath(from + i), Highlight);
                }
                c.Outline(Outline);
                if (w > 0.5f)
                    c.Dissolve((w - 0.5f) * 1.6f);
            });

        private static int RootPath(int x) => 6 + Mathf.RoundToInt(1.7f * Mathf.Sin(x * 0.42f + 0.6f));

        // A clod of earth thrown up as a vine bursts out, tumbling (its lit corner turns). Four
        // looping frames, 4x4 (hand drawn).
        public static PixelAnim Clod => PixelArt.Get("clod", "flying clods", 4, 4, 4, 12f, 0, 3,
            new Vector2(0.5f, 0.5f), Plant, (c, f) =>
            {
                string[][] frames =
                {
                    new[] { ".SS.", "SLMS", "SMMS", ".SS." },
                    new[] { ".SS.", "SMLS", "SMMS", ".SS." },
                    new[] { ".SS.", "SMMS", "SMLS", ".SS." },
                    new[] { ".SS.", "SMMS", "SLMS", ".SS." },
                };
                c.Rows(frames[f], Key);
            });

        // ---- Pricking and snaring ----

        // Thorn jab: a hooked thorn on a green stem stabbing in, its tip glinting and beading red,
        // then pulling back out. Six frames, 13x5, drawn pointing right with its tip as the pivot.
        public static PixelAnim ThornJab => PixelArt.Get("thorn", "thorn jabs", 13, 5, 6, 20f, -1, -1,
            new Vector2(11.5f / 13f, 0.5f), Plant, (c, f) =>
            {
                int[] shift = { -7, -3, 0, 0, -2, -6 };
                int tip = 11 + shift[f];
                for (int x = tip - 11; x <= tip - 8; x++)
                {
                    c.Set(x, 2, Mid);
                    c.Set(x, 1, Dark);
                }
                for (int i = 0; i <= 7; i++)
                {
                    int x = tip - i;
                    if (i <= 2)
                        c.Set(x, 2, i == 0 && (f == 2 || f == 3) ? White : Thorn);
                    else if (i <= 4)
                    {
                        c.Set(x, 2, Thorn);
                        c.Set(x, 1, Bark);
                    }
                    else
                    {
                        c.Set(x, 3, Thorn);
                        c.Set(x, 2, Thorn);
                        c.Set(x, 1, Bark);
                    }
                }
                if (f == 3)
                {
                    c.Set(tip + 1, 2, Tip);
                    c.Set(tip + 1, 3, Tip);
                }
                c.Outline(Outline);
                if (f == 5)
                    c.Dissolve(0.5f);
            });

        // Prick spark: a little red-and-white burst where the thorns land. Four frames, 7x7.
        public static PixelAnim Spark => PixelArt.Get("spark", "prick sparks", 7, 7, 4, 18f, -1, -1,
            new Vector2(0.5f, 0.5f), Plant, (c, f) =>
            {
                string[][] frames =
                {
                    new[] { ".......", ".......", "...r...", "..rwr..", "...r...", ".......", "......." },
                    new[] { ".......", "...t...", "...r...", ".trwrt.", "...r...", "...t...", "......." },
                    new[] { "...t...", ".r...r.", ".......", "t..t..t", ".......", ".r...r.", "...t..." },
                    new[] { ".......", "r.....r", ".......", "t.....t", ".......", "r.....r", "......." },
                };
                c.Rows(frames[f], Key);
            });

        // Vine coil: two thorny vines spiralling up round a snared enemy's legs from the ground,
        // squeezing while it's held, then loosening, browning and sliding off. Drawn in two
        // halves: the turns behind the enemy and the turns in front. 7 frames in, 4 looping,
        // 5 out, 24x26.
        public static PixelAnim Coil(bool front) => PixelArt.Get(front ? "coil front" : "coil back", "vine coil", 24, 26, 16, 12f, 7, 10,
            new Vector2(0.5f, 3f / 26f), Plant, (c, f) =>
            {
                float[] climb = { 0.12f, 0.25f, 0.4f, 0.55f, 0.7f, 0.85f, 1f };
                float[] squeeze = { 0f, 0.6f, 1f, 0.4f };
                float g = f < 7 ? climb[f] : 1f;
                float sq = f >= 7 && f <= 10 ? squeeze[f - 7] : 0f;
                int k5 = f >= 11 ? f - 10 : 0;
                float w = k5 / 5f;
                float slide = k5 * 1.5f;
                DrawCoil(c, front, g, sq, w, slide);
            });

        private static void DrawCoil(PixelCanvas c, bool front, float g, float squeeze, float w, float slide)
        {
            const float cx = 12f, foot = 3f, tall = 18f;
            const int steps = 90;
            const int Strands = 2;       // vines winding round
            const float Turns = 1.5f;    // times each winds round on its way up
            float rx = 7f - squeeze * 1.3f, ry = 2.6f;
            int light = w > 0.6f ? Yellow : w > 0.2f ? Brown : Mid;
            int dark = w > 0.2f ? Bark : Dark;
            int leaf = w > 0.6f ? Yellow : w > 0.2f ? Brown : Light;
            for (int k = 0; k < Strands; k++)
            {
                float lastX = 0f, lastY = 0f;
                bool drewTip = false;
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    if (t > g)
                        break;
                    float a = k * TwoPi / Strands + t * Turns * TwoPi;
                    float sin = Mathf.Sin(a), cos = Mathf.Cos(a);
                    if ((sin < 0f) != front)
                        continue;
                    float taper = 1f - 0.3f * t;
                    float x = cx + cos * rx * taper;
                    float y = foot + t * tall - slide + sin * ry * taper;
                    c.Set(x, y, light);
                    c.Set(x, y - 1f, dark);
                    int dx = cos >= 0f ? 1 : -1;
                    if (s % 18 == 9)
                    {
                        c.Set(x + dx, y + 1f, Thorn);
                        c.Set(x + 2 * dx, y + 2f, Tip);
                    }
                    if (s == 45 + k * 25)
                        Leaf(c, x + dx, y, dx > 0 ? 25f : 155f, 4f, leaf, w > 0.2f ? Bark : Mid);
                    lastX = x;
                    lastY = y;
                    drewTip = s == Mathf.FloorToInt(g * steps);
                }
                if (drewTip && w <= 0.2f)
                    c.Set(lastX, lastY + 1f, Highlight);   // the climbing tip
            }
            c.Outline(Outline);
            if (w > 0.4f)
                c.Dissolve((w - 0.4f) * 1.4f);
        }

        // ---- Withering ----

        // A withered leaf falling, flipping over as it flutters (full, tilted, edge on, the other
        // side). Four looping frames, 6x5 (hand drawn).
        public static PixelAnim WitheredLeaf => PixelArt.Get("leaf", "withered leaves", 6, 5, 4, 8f, 0, 3,
            new Vector2(0.5f, 0.5f), Plant, (c, f) =>
            {
                string[][] frames =
                {
                    new[] { "..ooo.", ".oyyyo", "oyybbo", ".obbo.", "ko.o.." },
                    new[] { "...oo.", "..oyyo", ".oybo.", "oybo..", "ko...." },
                    new[] { "......", "....oo", "..oyo.", ".oyo..", "ko...." },
                    new[] { "......", ".oo...", "obbyo.", ".obbyo", "..oook" },
                };
                c.Rows(frames[f], Key);
            });

        // Dust puff: a puff of dry earth as the thicket crumbles, swelling and thinning away.
        // Six frames, 14x12, see-through.
        public static PixelAnim DustPuff => PixelArt.Get("dust", "withering dust", 14, 12, 6, 12f, -1, -1,
            new Vector2(0.5f, 0.3f), Dust, (c, f) =>
            {
                float g = 0.5f + f * 0.12f;
                float[,] blobs = { { 5f, 5f, 3.2f }, { 8.5f, 6f, 3.8f }, { 10.5f, 4.5f, 2.8f } };
                for (int b = 0; b < 3; b++)
                    c.Ellipse(7f + (blobs[b, 0] - 7f) * g, 5f + (blobs[b, 1] - 5f) * g, blobs[b, 2] * g, blobs[b, 2] * g * 0.85f, 2);
                c.Highlight(2, 3);
                c.Shade(2, 1);
                c.Set(7, 9, 4);
                if (f >= 2)
                    c.Dissolve((f - 1) / 5f * 0.9f);
            });
    }
}
