using UnityEngine;

namespace WoLBrambleRing
{
    // Bramble Ring's effect animations, in the game's own plant-and-earth style. Where the game has
    // a fitting sheet its frames are used as they are (cut from the loaded texture): VineArc (the
    // thorny tendrils Churning Earth sprouts, which arch up and sink back into the ground), Plants
    // (the dark ground roots spreading under the game's vine trap), Rocks (rock debris, for the
    // clods) and Smoke (tinted to dry dust). Everything else is painted in code the way those
    // sheets are drawn: flat colours sampled from them (olive vines 4c4b3a / 717546 / 8f8c59 /
    // 9e9b67, bone thorns c6bfac / b2ac9b, withered browns 59402c / 815d36 / 8b724b from the
    // Plants sheet's dying vines, earth 4d3720 / 745432 / 95784c / 9c8159 from EarthWave and
    // Rocks), no outlines (a darker tone shades the underside instead), thin tapering strokes,
    // fading by alpha and shrinking. Each painted animation is also the stand-in if its game sheet
    // isn't loaded. Sizes are in the game's pixels (16 to a unit).
    internal static class ThicketArt
    {
        private static readonly Color32[] Plant =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x41, 0x41, 0x26, 0xff),   // 1 deepest olive (shade)
            new Color32(0x4c, 0x4b, 0x3a, 0xff),   // 2 dark olive
            new Color32(0x71, 0x75, 0x46, 0xff),   // 3 olive
            new Color32(0x8f, 0x8c, 0x59, 0xff),   // 4 light olive
            new Color32(0x9e, 0x9b, 0x67, 0xff),   // 5 khaki highlight
            new Color32(0xc6, 0xbf, 0xac, 0xff),   // 6 thorn
            new Color32(0xb2, 0xac, 0x9b, 0xff),   // 7 thorn shade
            new Color32(0x59, 0x40, 0x2c, 0xff),   // 8 withered dark
            new Color32(0x81, 0x5d, 0x36, 0xff),   // 9 withered
            new Color32(0x8b, 0x72, 0x4b, 0xff),   // 10 withered light
            new Color32(0x4d, 0x37, 0x20, 0xff),   // 11 earth dark
            new Color32(0x74, 0x54, 0x32, 0xff),   // 12 earth
            new Color32(0x95, 0x78, 0x4c, 0xff),   // 13 earth light
            new Color32(0x9c, 0x81, 0x59, 0xff),   // 14 earth highlight
            new Color32(0xff, 0xff, 0xff, 0xff),   // 15 white glint
        };
        // Dust (stand-in for the tinted Smoke sheet): see-through earth tones.
        private static readonly Color32[] Dust =
        {
            new Color32(0, 0, 0, 0),
            new Color32(0x74, 0x54, 0x32, 0x70),
            new Color32(0x95, 0x78, 0x4c, 0x98),
            new Color32(0xc6, 0xbf, 0xac, 0xa8),
        };
        private const int Shade = 1, DarkOlive = 2, Olive = 3, LightOlive = 4, Khaki = 5, Thorn = 6, ThornShade = 7;
        private const int DeadDark = 8, Dead = 9, DeadLight = 10, EarthDark = 11, Earth = 12, EarthLight = 13, EarthTop = 14, White = 15;
        // For hand-drawn rows: a character's position here is its colour.
        private const string Key = ".zdolktsbBLeEHiw";
        private const float TwoPi = Mathf.PI * 2f;
        // The tint that turns the game's white Smoke sheet into dry earth dust.
        public static readonly Color DustTint = new Color(0.76f, 0.68f, 0.54f, 0.75f);

        // ---- Game sheets ----

        // Thorny undergrowth (VineArc: eight variants, each a thorny tendril arching up out of a
        // hole in the ground over four 32 px frames and sinking back over four). It holds its
        // tallest frame while the thicket stands. The pivots put each variant's root on the spot.
        public const int SproutVariants = 8;
        private static readonly Vector2[] SproutRoots =
        {
            new Vector2(13.5f, 8f), new Vector2(11.5f, 8f), new Vector2(10.5f, 13f), new Vector2(20.5f, 9f),
            new Vector2(13.5f, 8f), new Vector2(21.5f, 5f), new Vector2(13.5f, 4f), new Vector2(22.5f, 12f),
        };

        public static PixelAnim Sprout(int variant)
        {
            int v = Mathf.Clamp(variant, 0, SproutVariants - 1);
            var cells = new int[8];
            for (int i = 0; i < 8; i++)
                cells[i] = v * 8 + i;
            return PixelArt.FromSheet("sprout" + v, "thorny undergrowth", "VineArc", 32, 32, cells, 12f, 3, 3,
                new Vector2(SproutRoots[v].x / 32f, SproutRoots[v].y / 32f), () => PaintedSprout);
        }

        // Creeping roots (Plants: the dark roots that spread over the ground under the game's vine
        // trap, seven frames growing out from their left end). They hold their last frame, then
        // fade and shrink. Drawn pointing right from their root end, rotated into place.
        public static PixelAnim Root => PixelArt.FromSheet("root", "creeping roots", "Plants", RootRects, 14f, 6, 6,
            new Vector2(6f / 32f, 0.5f), () => PaintedRoot);

        private static readonly Rect[] RootRects =
        {
            new Rect(0, 84, 32, 26), new Rect(32, 84, 32, 26), new Rect(64, 84, 32, 26), new Rect(96, 84, 32, 26),
            new Rect(128, 84, 32, 26), new Rect(160, 84, 32, 26), new Rect(192, 84, 32, 26),
        };

        // A clod of earth (Rocks: four rows of a rock breaking down over four 16 px frames). It
        // flies (spinning) on the first frame and crumbles through the rest where it lands.
        public const int ClodVariants = 4;
        public static PixelAnim Clod(int variant) => PixelArt.FromSheet("clod" + variant, "flying clods", "Rocks", 16, 16,
            new[] { variant * 4, variant * 4 + 1, variant * 4 + 2, variant * 4 + 3 }, 10f, 0, 0,
            new Vector2(0.5f, 0.5f), () => PaintedClod);

        // A puff of dry dust (Smoke: a white puff shrinking away over four 16 px frames; tinted
        // with DustTint where it's played).
        public static PixelAnim DustPuff => PixelArt.FromSheet("dust", "withering dust", "Smoke", 16, 16,
            new[] { 0, 1, 2, 3 }, 6f, -1, -1, new Vector2(0.5f, 0.5f), () => PaintedDust);

        // ---- Painted in the game's style ----

        // Soil burst: the ground under a vine heaves up, cracks and opens, earth heaped round the
        // hole in lit, faceted clumps (the way the game's rocks are shaded), then lies churned up
        // while the thicket stands (and fades and shrinks away at the end). 5 frames in, 1 held,
        // 24x12.
        public static PixelAnim SoilBurst => PixelArt.Paint("soil", "soil burst", 24, 12, 6, 14f, 5, 5,
            new Vector2(0.5f, 0.5f), Plant, (c, f) =>
            {
                const float cx = 12f, cy = 6f;
                if (f == 0)
                {
                    c.Ellipse(cx, cy, 4f, 2.2f, Earth);
                    c.Highlight(Earth, EarthLight);
                    c.Shade(Earth, EarthDark);
                    return;
                }
                if (f == 1)
                {
                    c.Ellipse(cx, cy + 0.5f, 5.5f, 3f, Earth);
                    c.Highlight(Earth, EarthLight);
                    c.Shade(Earth, EarthDark);
                    c.Line(9, 7, 15, 6, EarthDark);
                    return;
                }
                float open = f >= 4 ? 1f : (f - 1) / 3f;
                float rx = 6.5f + open * 1.5f, ry = 3.1f + open * 0.5f;
                // Heaped clumps round the hole, each lit on top and shaded underneath.
                for (int k = 0; k < 12; k++)
                {
                    float a = k / 12f * TwoPi + 0.2f;
                    float r = 0.82f + 0.08f * ((k * 5) % 3);
                    c.Ellipse(cx + Mathf.Cos(a) * rx * r, cy + Mathf.Sin(a) * ry * r, 2.2f, 1.5f, Earth);
                }
                c.Ellipse(cx, cy, rx * 0.55f, ry * 0.5f, EarthDark);
                c.Highlight(Earth, EarthLight);
                c.Shade(Earth, EarthDark);
                for (int k = 0; k < 4; k++)
                {
                    float a = k / 4f * TwoPi + 0.9f;
                    c.Set(cx + Mathf.Cos(a) * rx * 0.85f, cy + Mathf.Sin(a) * ry * 0.85f + 1f, EarthTop);
                }
                // Clumps thrown out, then lying round it.
                for (int k = 0; k < 6; k++)
                {
                    float a = k * TwoPi / 6f + 0.4f;
                    float d = f >= 4 ? 10f : 6f + (f - 1) * 1.6f;
                    float up = f == 3 ? 1.5f : f == 2 ? 0.8f : 0f;
                    float x = cx + Mathf.Cos(a) * d, y = cy + Mathf.Sin(a) * d * 0.45f + up;
                    c.Set(x, y, EarthLight);
                    c.Set(x + 1f, y, Earth);
                    c.Set(x, y - 1f, EarthDark);
                }
                if (f >= 4)
                {
                    // Root tips poking up out of the hole.
                    c.Set(cx - 2f, cy + 0.5f, Olive);
                    c.Set(cx - 2f, cy + 1.5f, LightOlive);
                    c.Set(cx + 2f, cy - 0.5f, Olive);
                    c.Set(cx + 3f, cy + 0.5f, LightOlive);
                }
            });

        // Thorn jab: a pale thorn on an olive stem stabbing in, its tip glinting, then pulling back.
        // Six frames, 13x5, drawn pointing right with its tip as the pivot.
        public static PixelAnim ThornJab => PixelArt.Paint("thorn", "thorn jabs", 13, 5, 6, 20f, -1, -1,
            new Vector2(11.5f / 13f, 0.5f), Plant, (c, f) =>
            {
                int[] shift = { -7, -3, 0, 0, -2, -6 };
                int tip = 11 + shift[f];
                for (int x = tip - 11; x <= tip - 7; x++)
                {
                    c.Set(x, 2, LightOlive);
                    c.Set(x, 1, DarkOlive);
                }
                for (int i = 0; i <= 6; i++)
                {
                    int x = tip - i;
                    c.Set(x, 2, i == 0 && (f == 2 || f == 3) ? White : Thorn);
                    if (i >= 3)
                        c.Set(x, 1, ThornShade);
                    if (i >= 5)
                        c.Set(x, 3, Thorn);
                }
            });

        // Prick spark: a little pale star where the thorns land. Four frames, 7x7.
        public static PixelAnim Spark => PixelArt.Paint("spark", "prick sparks", 7, 7, 4, 18f, -1, -1,
            new Vector2(0.5f, 0.5f), Plant, (c, f) =>
            {
                string[][] frames =
                {
                    new[] { ".......", ".......", "...t...", "..twt..", "...t...", ".......", "......." },
                    new[] { ".......", "...s...", "...t...", ".stwts.", "...t...", "...s...", "......." },
                    new[] { "...s...", ".t...t.", ".......", "s..t..s", ".......", ".t...t.", "...s..." },
                    new[] { ".......", "s.....s", ".......", ".......", ".......", "s.....s", "......." },
                };
                c.Rows(frames[f], Key);
            });

        // Vine coil: two thorny olive vines spiralling up round a snared enemy's legs from the
        // ground (thin strokes lit on top, shaded underneath, pale thorns, as the game's vines),
        // squeezing while it's held, then loosening, withering brown and sliding off. Drawn in two
        // halves: the turns behind the enemy and the turns in front. 7 frames in, 4 looping, 5 out,
        // 24x26.
        public static PixelAnim Coil(bool front) => PixelArt.Paint(front ? "coil front" : "coil back", "vine coil", 24, 26, 16, 12f, 7, 10,
            new Vector2(0.5f, 3f / 26f), Plant, (c, f) =>
            {
                float[] climb = { 0.12f, 0.25f, 0.4f, 0.55f, 0.7f, 0.85f, 1f };
                float[] squeeze = { 0f, 0.6f, 1f, 0.4f };
                float g = f < 7 ? climb[f] : 1f;
                float sq = f >= 7 && f <= 10 ? squeeze[f - 7] : 0f;
                int k5 = f >= 11 ? f - 10 : 0;
                DrawCoil(c, front, g, sq, k5 / 5f, k5 * 1.5f);
                if (k5 >= 3)
                    c.Scatter(1f - (k5 - 2) * 0.25f);
            });

        private static void DrawCoil(PixelCanvas c, bool front, float g, float squeeze, float w, float slide)
        {
            const float cx = 12f, foot = 3f, tall = 18f;
            const int steps = 90;
            const int Strands = 2;       // vines winding round
            const float Turns = 1.5f;    // times each winds round on its way up
            float rx = 7f - squeeze * 1.3f, ry = 2.6f;
            int light = w > 0.2f ? DeadLight : LightOlive;
            int mid = w > 0.2f ? Dead : Olive;
            int dark = w > 0.2f ? DeadDark : DarkOlive;
            for (int k = 0; k < Strands; k++)
            {
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
                    c.Set(x, y + 1f, light);
                    c.Set(x, y, mid);
                    c.Set(x, y - 1f, dark);
                    int dx = cos >= 0f ? 1 : -1;
                    if (s % 18 == 9)
                    {
                        c.Set(x + dx, y + 1f, Thorn);
                        c.Set(x + 2 * dx, y + 2f, Thorn);
                    }
                }
            }
        }

        // A withered leaf falling, flipping as it flutters (full, tilted, edge on, the other side),
        // in the browns of the game's dying vines. Four looping frames, 6x5 (hand drawn).
        public static PixelAnim WitheredLeaf => PixelArt.Paint("leaf", "withered leaves", 6, 5, 4, 8f, 0, 3,
            new Vector2(0.5f, 0.5f), Plant, (c, f) =>
            {
                string[][] frames =
                {
                    new[] { "..LL..", ".LLBB.", "LLBBb.", ".BBb..", "b....." },
                    new[] { "...LL.", "..LBB.", ".LBb..", "LBb...", "b....." },
                    new[] { "......", "....LB", "..LB..", ".Bb...", "b....." },
                    new[] { "......", ".BB...", "bBBL..", ".bBBL.", "...BLb" },
                };
                c.Rows(frames[f], Key);
            });

        // ---- Stand-ins for game sheets that aren't loaded ----

        // A thorny tendril arching up out of a dark hole and sinking back (VineArc's stand-in):
        // 4 frames up, 1 held, 3 down, 16x20.
        public static PixelAnim PaintedSprout => PixelArt.Paint("painted sprout", "thorny undergrowth", 16, 20, 8, 12f, 3, 3,
            new Vector2(0.5f, 0.1f), Plant, (c, f) =>
            {
                float[] growth = { 0.25f, 0.5f, 0.8f, 1f, 0.75f, 0.5f, 0.25f, 0f };
                float g = growth[f];
                c.Ellipse(8f, 2f, 2.2f, 1f, Shade);
                if (g <= 0f)
                    return;
                float top = 2f + 14f * g;
                float curl = f >= 3 ? 3f * g : 0f;
                c.Stroke(8f, 2f, 8f + curl * 0.5f, top, 2.6f, Olive);
                c.Stroke(7.5f, 2f, 7.5f + curl * 0.5f, top - 1f, 1.2f, LightOlive);
                if (curl > 0f)
                    c.Stroke(8f + curl * 0.5f, top, 8f + curl * 1.5f, top - 2f, 1.6f, Olive);
                for (int i = 1; i <= 3; i++)
                {
                    float y = 2f + (top - 2f) * i / 4f;
                    c.Set(8f + curl * 0.5f * i / 4f + (i % 2 == 0 ? 2f : -1f), y, Thorn);
                }
            });

        // Creeping roots' stand-in: a dark olive root forking over the floor as it grows. 7 frames
        // up, then held, 22x12, drawn pointing right from its root end.
        public static PixelAnim PaintedRoot => PixelArt.Paint("painted root", "creeping roots", 22, 12, 7, 14f, 6, 6,
            new Vector2(0f, 0.5f), Plant, (c, f) =>
            {
                int length = 3 + f * 3;
                for (int x = 0; x <= length && x < 22; x++)
                {
                    int y = 6 + Mathf.RoundToInt(1.7f * Mathf.Sin(x * 0.42f + 0.6f));
                    c.Set(x, y, DarkOlive);
                    if (x < length - 2)
                        c.Set(x, y - 1, Shade);
                    if (x == 8 || x == 15)
                        c.Stroke(x, y, x + 3f, y + (x == 8 ? 3f : -3f), 1.5f, DarkOlive);
                }
            });

        // A clod's stand-in: a lit lump of earth (the first frame flies, the rest crumble). Four
        // frames, 6x6.
        public static PixelAnim PaintedClod => PixelArt.Paint("painted clod", "flying clods", 6, 6, 4, 10f, 0, 0,
            new Vector2(0.5f, 0.5f), Plant, (c, f) =>
            {
                float r = 2.4f - f * 0.5f;
                c.Ellipse(3f, 3f, r, r * 0.9f, Earth);
                c.Highlight(Earth, EarthLight);
                c.Shade(Earth, EarthDark);
            });

        // Dry dust's stand-in: a puff of earth-coloured dust shrinking away. Four frames, 14x12.
        public static PixelAnim PaintedDust => PixelArt.Paint("painted dust", "withering dust", 14, 12, 4, 6f, -1, -1,
            new Vector2(0.5f, 0.5f), Dust, (c, f) =>
            {
                float g = 1f - f * 0.2f;
                c.Ellipse(5f, 5f, 3.2f * g, 2.8f * g, 2);
                c.Ellipse(8.5f, 6f, 3.8f * g, 3.2f * g, 2);
                c.Highlight(2, 3);
                c.Shade(2, 1);
            });
    }
}
