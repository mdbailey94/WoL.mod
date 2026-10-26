using UnityEngine;

namespace WoLSlingshotDash
{
    // Element palettes for the drawn effects, in the slots of Px: outline, dark, mid, light,
    // highlight, then two accents. Picked to sit with the game's own element colours.
    internal static class Palettes
    {
        // Air: the game's pale aqua-white wind.
        public static readonly Color32[] Air = Make(0x1f4652, 0x3e8a92, 0x7fcdc4, 0xc6f2e6, 0xffffff, 0xa6e3d6, 0xe8fbf6);
        // Fire: deep red to white-hot yellow; the accents are smoke.
        public static readonly Color32[] Fire = Make(0x4e140e, 0xa82c16, 0xe8601c, 0xffaa30, 0xfff0a0, 0x3a2a28, 0x6a5450);
        // Earth: packed dirt to sunlit sandstone; the accents are leaf greens.
        public static readonly Color32[] Earth = Make(0x2a1a10, 0x5a3a22, 0x8a6038, 0xbb935c, 0xe6cf9a, 0x4f8a32, 0x8cc450);
        // Lightning: white-hot core, the game's yellow, and a violet glow and outline.
        public static readonly Color32[] Storm = Make(0x2e1a5c, 0x6a46d0, 0xf2c232, 0xfff07a, 0xffffff, 0xa98cff, 0xe0d4ff);

        private static Color32[] Make(params int[] rgb)
        {
            var palette = new Color32[Px.Count];
            for (int i = 0; i < rgb.Length && i + 1 < palette.Length; i++)
                palette[i + 1] = new Color32((byte)(rgb[i] >> 16), (byte)(rgb[i] >> 8), (byte)rgb[i], 255);
            return palette;
        }
    }

    // The drawings. Each takes the canvas, the frame number and the frame count, and draws that
    // one frame: shapes in palette slots (bright cores, mid bodies, dark edges), then the outline,
    // then a dithered dissolve on the last frames. Everything is deterministic, so a frame looks
    // the same every time it's built.
    internal static class PixelArt
    {
        private const float Tau = Mathf.PI * 2f;

        // A repeatable 0..1 "random" number from a few integers.
        internal static float Hash(int a, int b = 0, int c = 0)
        {
            unchecked
            {
                int h = a * 73856093 ^ b * 19349663 ^ c * 83492791;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        // ---- Air ----

        // Three wind crescents chasing each other round a flattened ring (the floor seen at an
        // angle), each with a white head and a thinning tail curling in, a smaller ring turning
        // the other way and twinkling motes. Three-fold, so 8 frames turning a third of a circle
        // loop seamlessly. half: 0 = all, 1 = only the far (upper) half, 2 = only the near half.
        public static void WindRing(PixelCanvas c, int f, int n, int half)
        {
            float cx = c.W / 2f, cy = c.H / 2f, rx = c.W / 2f - 2.5f, ry = c.H / 2f - 2.5f;
            float turn = (float)f / n * Tau / 3f;
            for (int k = 0; k < 3; k++)
            {
                float head = turn + k * Tau / 3f;
                for (int s = 0; s <= 48; s++)
                {
                    float t = s / 48f; // 0 at the head, 1 at the tail
                    float a = head - t * 1.9f;
                    float r = 1f - 0.14f * t;
                    byte col = t < 0.1f ? Px.High : t < 0.38f ? Px.Light : t < 0.7f ? Px.Mid : Px.Dark;
                    c.Dot(cx + Mathf.Cos(a) * rx * r, cy + Mathf.Sin(a) * ry * r, t < 0.18f ? 1.25f : t < 0.55f ? 0.9f : 0.5f, col);
                }
            }
            for (int k = 0; k < 3; k++)
            {
                float head = -turn * 2f + k * Tau / 3f + 0.9f;
                for (int s = 0; s <= 24; s++)
                {
                    float t = s / 24f;
                    float a = head + t * 1.1f;
                    byte col = t < 0.3f ? Px.Light : t < 0.7f ? Px.Mid : Px.Dark;
                    c.Dot(cx + Mathf.Cos(a) * rx * 0.6f, cy + Mathf.Sin(a) * ry * 0.6f, 0.5f, col);
                }
            }
            if (f % 2 == 0)
            {
                for (int k = 0; k < 4; k++)
                {
                    float a = turn * 1.5f + k * Tau / 4f + 0.4f;
                    c.Set(Mathf.FloorToInt(cx + Mathf.Cos(a) * rx * 0.85f), Mathf.FloorToInt(cy + Mathf.Sin(a) * ry * 0.85f), Px.High);
                }
            }
            c.Outline();
            Halve(c, half);
        }

        private static void Halve(PixelCanvas c, int half)
        {
            int middle = c.H / 2;
            if (half == 1)
                c.KeepAbove(middle);
            else if (half == 2)
                c.KeepBelow(middle);
        }

        // A speed line left in the air: a bright stroke with its head curling up, stretching out,
        // breaking into dashes and blowing away. Head to the right (+x), the way you're going.
        public static void GustStreak(PixelCanvas c, int f, int n)
        {
            const int y = 4;
            switch (f)
            {
                case 0:
                    c.HLine(13, 20, y, Px.Light);
                    c.Dot(21.5f, 4.5f, 1.1f, Px.High);
                    break;
                case 1:
                    c.HLine(5, 9, y, Px.Dark);
                    c.HLine(10, 15, y, Px.Mid);
                    c.HLine(16, 22, y, Px.Light);
                    Curl(c, 23, y, Px.High, Px.Light);
                    break;
                case 2:
                    c.HLine(2, 7, y, Px.Dark);
                    c.HLine(10, 17, y, Px.Mid);
                    c.HLine(20, 24, y, Px.Light);
                    Curl(c, 25, y, Px.Light, Px.Mid);
                    break;
                case 3:
                    c.HLine(4, 6, y, Px.Dark);
                    c.HLine(12, 15, y, Px.Dark);
                    c.HLine(21, 23, y, Px.Mid);
                    c.Set(25, 6, Px.Dark);
                    break;
                default:
                    c.Set(6, y, Px.Dark);
                    c.Set(14, y, Px.Dark);
                    c.Set(22, y, Px.Dark);
                    break;
            }
            c.Outline();
        }

        private static void Curl(PixelCanvas c, int x, int y, byte head, byte body)
        {
            c.Set(x, y, body);
            c.Set(x + 1, y + 1, body);
            c.Set(x + 1, y + 2, head);
            c.Set(x, y + 3, head);
            c.Set(x - 1, y + 2, body);
        }

        // The wind bursting out behind you at the launch: five curved speed lines fanning out
        // from the left edge with a puff where they start, flying off and breaking up.
        public static void GustCone(PixelCanvas c, int f, int n)
        {
            const float ox = 4f, oy = 16f;
            float tip = Mathf.Min(35f, 9f + f * 6f), tail = Mathf.Max(1.5f, (f - 1) * 6.5f);
            for (int k = -2; k <= 2; k++)
            {
                float baseAngle = k * 16f * Mathf.Deg2Rad;
                float reach = k == 0 ? 1f : (k == 1 || k == -1) ? 0.9f : 0.75f;
                float r1 = tip * reach, r0 = Mathf.Min(r1 - 2f, tail * reach);
                for (int s = 0; s <= 32; s++)
                {
                    float t = s / 32f;
                    float r = Mathf.Lerp(r0, r1, t);
                    float a = baseAngle * (1f + 0.25f * t);
                    byte col = t > 0.85f ? Px.High : t > 0.55f ? Px.Light : t > 0.25f ? Px.Mid : Px.Dark;
                    c.Dot(ox + Mathf.Cos(a) * r, oy + Mathf.Sin(a) * r * 0.85f, f <= 1 ? 0.9f : 0.5f, col);
                }
            }
            if (f <= 1)
            {
                float puff = 4f + f * 2f;
                c.Band(ox + 1f, oy, puff, puff * 0.9f, 0.55f, f == 0 ? Px.Light : Px.Mid);
            }
            c.Outline();
            if (f >= 4)
                c.Dissolve((f - 3) * 0.35f);
        }

        // The vacuum: six spiral arms of wind closing in (flattened, on the floor), a core
        // brightening as they arrive, then a pop of rays and a ring that blows away.
        public static void Implode(PixelCanvas c, int f, int n)
        {
            const float cx = 24f, cy = 24f, squash = 0.7f;
            if (f <= 4)
            {
                float reach = 22f * (1f - f / 5.5f), turn = f * 0.55f;
                for (int k = 0; k < 6; k++)
                {
                    float start = k * Tau / 6f + turn;
                    for (int s = 0; s <= 40; s++)
                    {
                        float t = s / 40f; // 0 outer tail, 1 inner head
                        float r = reach * (1f - 0.5f * t);
                        float a = start + t * 1.4f;
                        byte col = t > 0.8f ? Px.High : t > 0.5f ? Px.Light : t > 0.2f ? Px.Mid : Px.Dark;
                        c.Dot(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r * squash, t > 0.6f ? 1.1f : 0.6f, col);
                    }
                }
                if (f >= 2)
                {
                    float core = 1f + f * 0.7f;
                    c.Ellipse(cx, cy, core, core, Px.Light);
                    c.Ellipse(cx, cy, core * 0.5f, core * 0.5f, Px.High);
                }
            }
            else if (f == 5)
            {
                c.Ellipse(cx, cy, 4.5f, 4.5f, Px.Light);
                c.Ellipse(cx, cy, 2.5f, 2.5f, Px.High);
                c.Band(cx, cy, 7f, 7f * squash, 0.8f, Px.Mid);
                for (int k = 0; k < 4; k++)
                {
                    float a = Tau / 8f + k * Tau / 4f;
                    c.Line(cx + Mathf.Cos(a) * 5f, cy + Mathf.Sin(a) * 5f, cx + Mathf.Cos(a) * 9f, cy + Mathf.Sin(a) * 9f, 0.5f, Px.High);
                }
            }
            else
            {
                bool first = f == 6;
                float r1 = first ? 14f : 18f, r0 = first ? 5f : 11f;
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Tau / 8f;
                    float end = k % 2 == 0 ? r1 : r1 * 0.7f;
                    float x1 = cx + Mathf.Cos(a) * end, y1 = cy + Mathf.Sin(a) * end;
                    c.Line(cx + Mathf.Cos(a) * r0, cy + Mathf.Sin(a) * r0, x1, y1, 0.5f, first ? Px.Light : Px.Mid);
                    if (first)
                        c.Set(Mathf.FloorToInt(x1), Mathf.FloorToInt(y1), Px.High);
                }
                c.Band(cx, cy, r1 * 0.7f, r1 * 0.7f * 0.8f, 0.85f, first ? Px.Mid : Px.Dark);
            }
            c.Outline();
            if (f == 7)
                c.Dissolve(0.5f);
        }

        // ---- Shared ----

        // An impact: a four-pointed flash, then a burst of eight rays flying out and breaking up.
        // Long axis along x, so it can be turned to point along a punch or kick.
        public static void ImpactStar(PixelCanvas c, int f, int n)
        {
            float cx = c.W / 2f, cy = c.H / 2f;
            switch (f)
            {
                case 0:
                    c.Astroid(cx, cy, 12f, 8f, Px.High, Px.Light, 0.75f);
                    break;
                case 1:
                    c.Astroid(cx, cy, 6.5f, 6.5f, Px.Mid, Px.Mid, 0f, true);
                    c.Astroid(cx, cy, 14.5f, 10f, Px.High, Px.Light, 0.6f);
                    break;
                default:
                    float r0 = f == 2 ? 5f : 10f;
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Tau / 8f;
                        bool axis = k % 2 == 0;
                        float r1 = axis ? 15f : 11.5f;
                        float dx = Mathf.Cos(a), dy = Mathf.Sin(a) * (axis ? 1f : 0.8f);
                        if (f == 4)
                        {
                            c.Set(Mathf.FloorToInt(cx + dx * (r1 - 0.5f)), Mathf.FloorToInt(cy + dy * (r1 - 0.5f)), Px.Dark);
                            continue;
                        }
                        c.Line(cx + dx * r0, cy + dy * r0, cx + dx * r1, cy + dy * r1, 0.5f, f == 2 ? Px.Light : Px.Mid);
                        c.Set(Mathf.FloorToInt(cx + dx * r1), Mathf.FloorToInt(cy + dy * r1), f == 2 ? Px.High : Px.Light);
                    }
                    if (f == 2)
                        c.Band(cx, cy, 4.5f, 4.5f, 0.6f, Px.Mid);
                    break;
            }
            c.Outline();
            if (f == 4)
                c.Dissolve(0.25f);
        }

        // A shockwave flat on the floor: a thick bright ellipse that spreads, thins, darkens,
        // tears into arcs and dissolves (played backwards, it closes in).
        public static void GroundRing(PixelCanvas c, int f, int n)
        {
            float cx = c.W / 2f, cy = c.H / 2f;
            float reach = 7f + f * (21f / Mathf.Max(1, n - 1));
            float thick = Mathf.Max(1.2f, 3.6f - f * 0.5f);
            for (int y = 0; y < c.H; y++)
            {
                for (int x = 0; x < c.W; x++)
                {
                    float dx = x + 0.5f - cx, dy = (y + 0.5f - cy) / 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > reach || dist < reach - thick)
                        continue;
                    float a = Mathf.Atan2(dy, dx);
                    if (f >= 2 && Mathf.Sin(a * 7f + f * 1.3f) > 0.72f)
                        continue;
                    float edge = (dist - (reach - thick)) / thick; // 0 inside .. 1 outside
                    byte col = f <= 1 ? (edge > 0.5f ? Px.High : Px.Light)
                        : f <= 3 ? (edge > 0.5f ? Px.Light : Px.Mid)
                        : (edge > 0.5f ? Px.Mid : Px.Dark);
                    c.Paint(x, y, col);
                }
            }
            c.Outline();
            if (f >= 4)
                c.Dissolve((f - 3) * 0.3f);
        }

        // ---- Fire ----

        // A flame: a round base tapering to a swaying tip, banded white-hot core, yellow, orange,
        // red edge, with a drop of flame sometimes breaking off the tip. `shift` darkens it (a
        // dying flame), `tipH` is its height in pixels and `rb` the base's radius.
        private static void Flame(PixelCanvas c, float phase, float tipH, float rb, int shift, bool droplet)
        {
            float cx = c.W / 2f, cy = rb * 0.9f + 0.6f;
            for (int y = 0; y < c.H; y++)
            {
                for (int x = 0; x < c.W; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f, q, h;
                    if (py <= cy)
                    {
                        float dx = (px - cx) / rb, dy = (py - cy) / (cy - 0.2f);
                        float d = dx * dx + dy * dy;
                        if (d > 1f)
                            continue;
                        q = Mathf.Sqrt(d);
                        h = 0f;
                    }
                    else
                    {
                        h = (py - cy) / (tipH - cy);
                        if (h > 1f)
                            continue;
                        float w = rb * Mathf.Pow(1f - h, 0.85f);
                        float middle = cx + 1.6f * h * h * Mathf.Sin(phase + h * 3.2f);
                        float dx = Mathf.Abs(px - middle);
                        if (dx > w)
                            continue;
                        q = w > 0.01f ? dx / w : 1f;
                    }
                    byte col = q < 0.38f && h < 0.4f ? Px.High
                        : q < 0.62f && h < 0.62f ? Px.Light
                        : q < 0.86f && h < 0.86f ? Px.Mid
                        : Px.Dark;
                    c.Paint(x, y, Shift(col, shift));
                }
            }
            // The drop floats clear of the tip (a gap of clear pixels), room allowing.
            if (droplet && tipH + 3.5f <= c.H - 1.5f)
                c.Ellipse(cx + 1.6f * Mathf.Sin(phase + 3.2f), tipH + 2.4f, 0.9f, 1.1f, Shift(Px.Light, shift));
            c.Outline();
        }

        private static byte Shift(byte col, int shift) => (byte)Mathf.Max(Px.Dark, col - shift);

        // A flame licking up, looping: its tip sways and stretches, and now and then a drop of
        // flame breaks off.
        public static void FlameTongue(PixelCanvas c, int f, int n)
        {
            float phase = (float)f / n * Tau;
            Flame(c, phase, 14.5f + 2f * Mathf.Sin(phase + 0.6f), 4.2f + 0.3f * Mathf.Sin(phase * 2f), 0,
                Mathf.Sin(phase) > 0.35f);
        }

        // A flame left behind: it shrinks, reddens and gutters out.
        public static void FlameWisp(PixelCanvas c, int f, int n)
        {
            float k = (float)f / (n - 1);
            int shift = f >= 4 ? 2 : f >= 2 ? 1 : 0;
            Flame(c, f * 1.3f, Mathf.Lerp(17f, 6f, k), Mathf.Lerp(4.2f, 2f, k), shift, f == 1);
            if (f >= 4)
                c.Dissolve((f - 3) * 0.3f);
        }

        // A spark of fire: a bright cross that cools to a red dot.
        public static void Ember(PixelCanvas c, int f, int n)
        {
            const int x = 2, y = 2;
            switch (f)
            {
                case 0:
                    c.Set(x, y, Px.High);
                    c.Set(x - 1, y, Px.Light);
                    c.Set(x + 1, y, Px.Light);
                    c.Set(x, y - 1, Px.Light);
                    c.Set(x, y + 1, Px.Light);
                    break;
                case 1:
                    c.Set(x, y, Px.Light);
                    c.Set(x, y + 1, Px.Mid);
                    break;
                case 2:
                    c.Set(x, y, Px.Mid);
                    break;
                default:
                    c.Set(x, y, Px.Dark);
                    return;
            }
            c.Outline(Px.Dark);
        }

        // Smoke curling inside a fire blast's ring: dark below, lighter on top.
        private static void SmokeBlob(PixelCanvas c, float bx, float by, float r)
        {
            for (int y = Mathf.FloorToInt(by - r - 1f); y <= Mathf.CeilToInt(by + r + 1f); y++)
            {
                for (int x = Mathf.FloorToInt(bx - r - 1f); x <= Mathf.CeilToInt(bx + r + 1f); x++)
                {
                    float dx = (x + 0.5f - bx) / r, dy = (y + 0.5f - by) / (r * 0.85f);
                    if (dx * dx + dy * dy <= 1f)
                        c.Set(x, y, dy > 0.2f ? Px.AccentB : Px.AccentA);
                }
            }
        }

        private static readonly float[] BlastReach = { 9f, 15f, 21f, 25f, 28f, 30f, 31f };

        // The kick's blast: a white-hot flash swelling into a lumpy fireball with flames bursting
        // off its rim, then a hollow ring of fire (smoke rising in the middle) that tears apart and
        // dissolves. Flattened a little, as the floor is seen at an angle.
        public static void FireBlast(PixelCanvas c, int f, int n)
        {
            float cx = c.W / 2f, cy = c.H / 2f;
            const float squash = 0.74f;
            float reach = BlastReach[Mathf.Clamp(f, 0, BlastReach.Length - 1)];
            if (f >= 3)
            {
                for (int k = 0; k < 4; k++)
                    SmokeBlob(c, cx + (k - 1.5f) * 4.5f, cy + 1f + f * 1.5f + (k % 2) * 2f, 2.5f + f * 0.5f);
            }
            for (int y = 0; y < c.H; y++)
            {
                for (int x = 0; x < c.W; x++)
                {
                    float dx = x + 0.5f - cx, dy = (y + 0.5f - cy) / squash;
                    float a = Mathf.Atan2(dy, dx);
                    float lump = 1f + 0.1f * Mathf.Sin(5f * a + f * 1.7f) + 0.07f * Mathf.Sin(9f * a - f * 2.3f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / (reach * lump);
                    if (d > 1f)
                        continue;
                    byte col;
                    switch (f)
                    {
                        case 0:
                            col = d < 0.55f ? Px.High : Px.Light;
                            break;
                        case 1:
                            col = d < 0.35f ? Px.High : d < 0.65f ? Px.Light : d < 0.9f ? Px.Mid : Px.Dark;
                            break;
                        case 2:
                            col = d < 0.2f ? Px.High : d < 0.5f ? Px.Light : d < 0.82f ? Px.Mid : Px.Dark;
                            break;
                        case 3:
                            if (d < 0.45f)
                                continue;
                            col = d < 0.58f ? Px.Dark : d < 0.78f ? Px.Mid : d < 0.93f ? Px.Light : Px.Mid;
                            break;
                        case 4:
                            if (d < 0.62f || Mathf.Sin(7f * a + f) > 0.65f)
                                continue;
                            col = d < 0.75f ? Px.Dark : d < 0.9f ? Px.Mid : Px.Light;
                            break;
                        default:
                            if (d < 0.8f || Mathf.Sin(7f * a + f) > 0.4f)
                                continue;
                            col = d < 0.9f ? Px.Dark : Px.Mid;
                            break;
                    }
                    c.Paint(x, y, col);
                }
            }
            if (f == 2 || f == 3)
            {
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Tau / 12f + f * 0.2f;
                    float r0 = reach * 0.92f, r1 = reach + 3f + 2f * Hash(k, f);
                    float x1 = cx + Mathf.Cos(a) * r1, y1 = cy + Mathf.Sin(a) * r1 * squash;
                    c.Line(cx + Mathf.Cos(a) * r0, cy + Mathf.Sin(a) * r0 * squash, x1, y1, 0.9f, f == 2 ? Px.Light : Px.Mid);
                    if (f == 2)
                        c.Set(Mathf.FloorToInt(x1), Mathf.FloorToInt(y1), Px.High);
                }
            }
            c.Outline();
            if (f == 5)
                c.Dissolve(0.35f);
            else if (f >= 6)
                c.Dissolve(0.7f);
        }

        // ---- Earth ----

        // A puff of dust: three lumps, lit from above, swelling and dissolving.
        public static void DustPuff(PixelCanvas c, int f, int n)
        {
            float g = 2.2f + f * 0.6f;
            DustLump(c, 5f, 3.6f + f * 0.3f, g);
            DustLump(c, 10.5f, 4f + f * 0.35f, g * 0.9f);
            DustLump(c, 7.6f, 5.5f + f * 0.4f, g * 0.85f);
            c.Outline(Px.Dark);
            if (f >= 3)
                c.Dissolve((f - 2) * 0.33f);
        }

        private static void DustLump(PixelCanvas c, float bx, float by, float r)
        {
            float ry = r * 0.8f;
            for (int y = Mathf.FloorToInt(by - ry - 1f); y <= Mathf.CeilToInt(by + ry + 1f); y++)
            {
                for (int x = Mathf.FloorToInt(bx - r - 1f); x <= Mathf.CeilToInt(bx + r + 1f); x++)
                {
                    float dx = (x + 0.5f - bx) / r, dy = (y + 0.5f - by) / ry;
                    if (dx * dx + dy * dy > 1f)
                        continue;
                    byte col = dy > 0.6f && dx < 0f ? Px.High : dy > 0.3f ? Px.Light : dy > -0.4f ? Px.Mid : Px.Dark;
                    c.Paint(x, y, col);
                }
            }
        }

        private static readonly float[] RockRadii = { 3.3f, 2.6f, 3.5f, 2.9f, 3.1f };
        private static readonly Vector2 RockLight = new Vector2(-0.6f, 0.8f);

        // A chunk of rock tumbling: an uneven five-sided stone lit from the top left, turning a
        // quarter each frame (so 4 frames loop).
        public static void RockChunk(PixelCanvas c, int f, int n)
        {
            var centre = new Vector2(c.W / 2f, c.H / 2f);
            float turn = f * Tau / n;
            var corners = new Vector2[RockRadii.Length];
            for (int k = 0; k < corners.Length; k++)
            {
                float a = turn + k * Tau / corners.Length;
                corners[k] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RockRadii[k];
            }
            for (int k = 0; k < corners.Length; k++)
            {
                // Each wedge from the centre to one side, shaded by which way it faces.
                Vector2 a = corners[k], b = corners[(k + 1) % corners.Length];
                Vector2 facing = ((a + b) * 0.5f - centre).normalized;
                float lit = Vector2.Dot(facing, RockLight);
                byte col = lit > 0.45f ? Px.Light : lit > -0.3f ? Px.Mid : Px.Dark;
                c.Triangle(centre, a, b, col);
            }
            // A glint on the lit face.
            Vector2 glint = centre + RockLight * 1.6f;
            c.Set(Mathf.FloorToInt(glint.x), Mathf.FloorToInt(glint.y), Px.High);
            c.Outline();
        }

        private static readonly float[] ShardAngles = { -46f, -30f, -14f, 0f, 14f, 30f, 46f, 160f, -160f };
        private static readonly float[] ShardSpeed = { 0.7f, 0.95f, 1.1f, 1.2f, 1.1f, 0.95f, 0.7f, 0.25f, 0.25f };

        // The kick's impact on stone: a flash, then rock shards bursting forward along the kick
        // (+x) in a cone, arcing down as they fly, out of a cloud of dust lumps thrown up round
        // the impact and drifting forward.
        public static void EarthImpact(PixelCanvas c, int f, int n)
        {
            const float ox = 12f, oy = 20f;
            if (f == 0)
            {
                c.Astroid(ox + 2f, oy, 10f, 7f, Px.High, Px.Light, 0.7f);
                c.Outline();
                return;
            }
            if (f <= 5)
            {
                float spread = 2.5f + f * 1.8f, lump = 2.6f + f * 0.35f;
                for (int k = 0; k < 5; k++)
                {
                    float a = (k / 5f - 0.5f) * 2.4f; // a fan opening forward
                    DustLump(c, ox + 1f + Mathf.Cos(a) * spread + f * 0.8f, oy - 1.5f + Mathf.Sin(a) * spread * 0.6f, lump * (k % 2 == 0 ? 1f : 0.8f));
                }
            }
            float fall = (f - 1) * (f - 1) * 0.3f;
            for (int k = 0; k < ShardAngles.Length; k++)
            {
                float a = ShardAngles[k] * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.65f);
                float travel = (5f + f * 5.2f) * ShardSpeed[k];
                Vector2 baseCentre = new Vector2(ox, oy - fall) + dir * travel;
                Vector2 tip = baseCentre + dir.normalized * 5f;
                Vector2 side = new Vector2(-dir.y, dir.x).normalized * (k % 2 == 0 ? 2.3f : 1.7f);
                byte top = f >= 5 ? Px.Mid : Px.Light, bottom = f >= 5 ? Px.Dark : Px.Mid;
                c.Triangle(tip, baseCentre + side, baseCentre, top);
                c.Triangle(tip, baseCentre, baseCentre - side, bottom);
                if (f <= 2)
                    c.Set(Mathf.FloorToInt(tip.x), Mathf.FloorToInt(tip.y), Px.High);
            }
            c.Outline();
            if (f >= 5)
                c.Dissolve((f - 4) * 0.35f);
        }

        // ---- Lightning ----

        // A spark: a white cross that flares into a star, breaks into points and winks out.
        public static void Zap(PixelCanvas c, int f, int n)
        {
            const int x = 4, y = 4;
            switch (f)
            {
                case 0:
                    c.Set(x, y, Px.High);
                    for (int i = 1; i <= 3; i++)
                    {
                        byte col = i == 3 ? Px.Mid : Px.Light;
                        c.Set(x + i, y, col);
                        c.Set(x - i, y, col);
                        c.Set(x, y + i, col);
                        c.Set(x, y - i, col);
                    }
                    c.Outline();
                    break;
                case 1:
                    c.Set(x, y, Px.High);
                    for (int i = 1; i <= 2; i++)
                    {
                        c.Set(x + i, y + i, Px.Light);
                        c.Set(x - i, y + i, Px.Light);
                        c.Set(x + i, y - i, Px.Light);
                        c.Set(x - i, y - i, Px.Light);
                    }
                    for (int i = 1; i <= 3; i++)
                    {
                        c.Set(x + i, y, Px.Mid);
                        c.Set(x - i, y, Px.Mid);
                        c.Set(x, y + i, Px.Mid);
                        c.Set(x, y - i, Px.Mid);
                    }
                    c.Outline();
                    break;
                case 2:
                    c.Set(x, y, Px.Mid);
                    c.Set(x + 3, y + 1, Px.Light);
                    c.Set(x - 3, y - 1, Px.Light);
                    c.Set(x + 1, y - 3, Px.Mid);
                    c.Set(x - 1, y + 3, Px.Mid);
                    c.Outline(Px.Dark);
                    break;
                default:
                    c.Set(x + 3, y + 2, Px.Dark);
                    c.Set(x - 3, y - 2, Px.Dark);
                    c.Set(x - 2, y + 3, Px.Dark);
                    break;
            }
        }

        // An electric ring round your feet: a jagged, broken ellipse of arcs redrawn every frame
        // (so it flickers), sparks jumping off it, glowing violet.
        public static void StaticRing(PixelCanvas c, int f, int n, int half)
        {
            float cx = c.W / 2f, cy = c.H / 2f, rx = c.W / 2f - 5f, ry = c.H / 2f - 3.5f;
            const int points = 18;
            Vector2 prev = Vector2.zero;
            for (int k = 0; k <= points; k++)
            {
                int kk = k % points;
                float a = kk * Tau / points + f * 0.21f;
                float r = 1f + (Hash(f, kk) - 0.5f) * 0.22f;
                var p = new Vector2(cx + Mathf.Cos(a) * rx * r, cy + Mathf.Sin(a) * ry * r);
                if (k > 0 && Hash(f, kk, 7) > 0.28f)
                    c.Line(prev, p, 0.5f, Hash(f, kk, 3) > 0.5f ? Px.High : Px.Light);
                prev = p;
            }
            for (int s = 0; s < 3; s++)
            {
                float a = Hash(f, s, 11) * Tau;
                var outward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var start = new Vector2(cx + outward.x * rx, cy + outward.y * ry);
                Vector2 end = start + new Vector2(outward.x, outward.y * 0.6f) * (2f + 2f * Hash(f, s, 13));
                c.Line(start, end, 0.5f, Px.Mid);
            }
            c.Outline(Px.Dark);
            Halve(c, half);
        }

        // A bolt along the leap: a jagged white line with branches, glowing yellow then violet,
        // re-forking as it flickers, then breaking up. From the left edge (x = 0) to the right.
        public static void Bolt(PixelCanvas c, int f, int n)
        {
            int seed = f < 2 ? 1 : f + 1;
            const int joints = 8;
            float mid = c.H / 2f;
            var pts = new Vector2[joints + 1];
            for (int k = 0; k <= joints; k++)
            {
                float y = k == 0 || k == joints ? mid : mid + (Hash(seed, k) - 0.5f) * 8f;
                pts[k] = new Vector2(k * (c.W - 1f) / joints, y);
            }
            byte core = f <= 1 ? Px.High : f == 2 ? Px.Light : f == 3 ? Px.Mid : Px.Dark;
            for (int k = 0; k < joints; k++)
            {
                if (f >= 3 && Hash(seed, k, 5) < 0.4f)
                    continue;
                c.Line(pts[k], pts[k + 1], 0.5f, core);
            }
            if (f <= 2)
            {
                for (int b = 3; b <= 5; b += 2)
                {
                    var dir = new Vector2(1f, Hash(seed, b, 9) > 0.5f ? 1f : -1f).normalized;
                    c.Line(pts[b], pts[b] + dir * 3.5f, 0.5f, f == 2 ? Px.Mid : Px.Light);
                }
            }
            if (f == 0)
            {
                c.Outline(Px.Light);
                c.Outline(Px.Dark);
            }
            else if (f == 1)
            {
                c.Outline(Px.Mid);
                c.Outline(Px.Outline);
            }
            else
            {
                c.Outline(f == 2 ? Px.Dark : Px.Outline);
            }
            if (f >= 4)
                c.Dissolve(0.45f);
        }

        // The landing: a white flash with four long jagged rays, then eight, a ring spreading,
        // tearing apart and dissolving.
        public static void ThunderBurst(PixelCanvas c, int f, int n)
        {
            float cx = c.W / 2f, cy = c.H / 2f;
            switch (f)
            {
                case 0:
                    c.Ellipse(cx, cy, 4.5f, 4.5f, Px.High);
                    JaggedRays(c, cx, cy, 4, 4f, 19f, f, Px.High, 0f);
                    c.Outline(Px.Light);
                    c.Outline(Px.Dark);
                    break;
                case 1:
                    c.Ellipse(cx, cy, 7f, 7f, Px.Light);
                    c.Ellipse(cx, cy, 3.5f, 3.5f, Px.High);
                    JaggedRays(c, cx, cy, 8, 5f, 21f, f, Px.Light, 0.39f);
                    c.Outline(Px.Dark);
                    c.Outline(Px.Outline);
                    break;
                case 2:
                    c.Band(cx, cy, 12f, 10f, 0.8f, Px.Light);
                    JaggedRays(c, cx, cy, 8, 11f, 22f, f, Px.Mid, 0.39f);
                    c.Outline(Px.Dark);
                    break;
                case 3:
                    BrokenRing(c, cx, cy, 15f, 12.5f, f, Px.Light);
                    JaggedRays(c, cx, cy, 8, 17f, 22f, f, Px.Dark, 0.39f);
                    c.Outline();
                    break;
                case 4:
                    BrokenRing(c, cx, cy, 18f, 15f, f, Px.Mid);
                    for (int s = 0; s < 6; s++)
                    {
                        float a = Hash(f, s, 17) * Tau;
                        c.Set(Mathf.FloorToInt(cx + Mathf.Cos(a) * 21f), Mathf.FloorToInt(cy + Mathf.Sin(a) * 18f), Px.Light);
                    }
                    c.Outline();
                    break;
                default:
                    BrokenRing(c, cx, cy, 21f, 17.5f, f, Px.Dark);
                    c.Outline();
                    c.Dissolve(f == 5 ? 0.4f : 0.75f);
                    break;
            }
        }

        private static void JaggedRays(PixelCanvas c, float cx, float cy, int count, float r0, float r1, int seed, byte col, float turn)
        {
            for (int k = 0; k < count; k++)
            {
                float a = turn + k * Tau / count;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var side = new Vector2(-dir.y, dir.x);
                var centre = new Vector2(cx, cy);
                Vector2 prev = centre + dir * r0;
                for (int j = 1; j <= 3; j++)
                {
                    float r = Mathf.Lerp(r0, r1, j / 3f);
                    float jitter = j < 3 ? (Hash(seed, k, j) - 0.5f) * 4f : 0f;
                    Vector2 p = centre + dir * r + side * jitter;
                    c.Line(prev, p, 0.5f, col);
                    prev = p;
                }
            }
        }

        private static void BrokenRing(PixelCanvas c, float cx, float cy, float rx, float ry, int seed, byte col)
        {
            const int steps = 128;
            for (int s = 0; s < steps; s++)
            {
                if (Hash(seed, s / 8) < 0.3f)
                    continue;
                float a = s * Tau / steps;
                c.Set(Mathf.FloorToInt(cx + Mathf.Cos(a) * rx), Mathf.FloorToInt(cy + Mathf.Sin(a) * ry), col);
            }
        }
    }

    // Each dash's drawn animations, built the first time each one plays (the log line
    // "<dash>: visual '<name>' ready (...)" says so) and kept for the session.
    internal static class AirArt
    {
        private const string Owner = "Vacuum Fist";
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static FxAnim ringBack, ringFront, streak, cone, implode, star, ring;

        public static FxAnim RingBack => ringBack ?? (ringBack = FxAnim.Build(Owner, "wind ring (far half)", 48, 24, 8, 0.045f, true,
            Centre, Palettes.Air, (c, f, n) => PixelArt.WindRing(c, f, n, 1)));
        public static FxAnim RingFront => ringFront ?? (ringFront = FxAnim.Build(Owner, "wind ring (near half)", 48, 24, 8, 0.045f, true,
            Centre, Palettes.Air, (c, f, n) => PixelArt.WindRing(c, f, n, 2)));
        public static FxAnim Streak => streak ?? (streak = FxAnim.Build(Owner, "gust streak", 28, 9, 5, 0.04f, false,
            Centre, Palettes.Air, PixelArt.GustStreak));
        public static FxAnim Cone => cone ?? (cone = FxAnim.Build(Owner, "launch gust", 40, 32, 6, 0.045f, false,
            new Vector2(0.1f, 0.5f), Palettes.Air, PixelArt.GustCone));
        public static FxAnim Implode => implode ?? (implode = FxAnim.Build(Owner, "vacuum implosion", 48, 48, 8, 0.05f, false,
            Centre, Palettes.Air, PixelArt.Implode));
        public static FxAnim Star => star ?? (star = FxAnim.Build(Owner, "punch impact", 32, 32, 5, 0.04f, false,
            Centre, Palettes.Air, PixelArt.ImpactStar));
        public static FxAnim Ring => ring ?? (ring = FxAnim.Build(Owner, "vacuum ring", 64, 32, 6, 0.05f, false,
            Centre, Palettes.Air, PixelArt.GroundRing));
    }

    internal static class FireArt
    {
        private const string Owner = "Blazing Kick";
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 Foot = new Vector2(0.5f, 0.05f);
        private static FxAnim tongue, wisp, ember, blast, star, ring;

        public static FxAnim Tongue => tongue ?? (tongue = FxAnim.Build(Owner, "flame tongue", 12, 22, 6, 0.06f, true,
            Foot, Palettes.Fire, PixelArt.FlameTongue));
        public static FxAnim Wisp => wisp ?? (wisp = FxAnim.Build(Owner, "flame wisp", 12, 22, 6, 0.055f, false,
            Foot, Palettes.Fire, PixelArt.FlameWisp));
        public static FxAnim Ember => ember ?? (ember = FxAnim.Build(Owner, "ember", 5, 5, 4, 0.07f, false,
            Centre, Palettes.Fire, PixelArt.Ember));
        public static FxAnim Blast => blast ?? (blast = FxAnim.Build(Owner, "fire blast", 64, 48, 7, 0.05f, false,
            Centre, Palettes.Fire, PixelArt.FireBlast));
        public static FxAnim Star => star ?? (star = FxAnim.Build(Owner, "kick impact", 32, 32, 5, 0.04f, false,
            Centre, Palettes.Fire, PixelArt.ImpactStar));
        public static FxAnim Ring => ring ?? (ring = FxAnim.Build(Owner, "scorch ring", 64, 32, 6, 0.05f, false,
            Centre, Palettes.Fire, PixelArt.GroundRing));
    }

    internal static class EarthArt
    {
        private const string Owner = "Vine Slingshot";
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static FxAnim dust, rock, impact, star, ring;

        public static FxAnim Dust => dust ?? (dust = FxAnim.Build(Owner, "dust puff", 16, 12, 5, 0.06f, false,
            new Vector2(0.5f, 0.15f), Palettes.Earth, PixelArt.DustPuff));
        public static FxAnim Rock => rock ?? (rock = FxAnim.Build(Owner, "tumbling rock", 9, 9, 4, 0.07f, true,
            Centre, Palettes.Earth, PixelArt.RockChunk));
        public static FxAnim Impact => impact ?? (impact = FxAnim.Build(Owner, "rock burst", 64, 40, 7, 0.05f, false,
            new Vector2(12f / 64f, 0.5f), Palettes.Earth, PixelArt.EarthImpact));
        public static FxAnim Star => star ?? (star = FxAnim.Build(Owner, "kick impact", 32, 32, 5, 0.04f, false,
            Centre, Palettes.Earth, PixelArt.ImpactStar));
        public static FxAnim Ring => ring ?? (ring = FxAnim.Build(Owner, "ground shockwave", 64, 32, 6, 0.05f, false,
            Centre, Palettes.Earth, PixelArt.GroundRing));
    }

    internal static class StormArt
    {
        private const string Owner = "Charged Leap";
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static FxAnim ringBack, ringFront, zap, bolt, burst, ring;

        public static FxAnim RingBack => ringBack ?? (ringBack = FxAnim.Build(Owner, "static ring (far half)", 48, 24, 6, 0.04f, true,
            Centre, Palettes.Storm, (c, f, n) => PixelArt.StaticRing(c, f, n, 1)));
        public static FxAnim RingFront => ringFront ?? (ringFront = FxAnim.Build(Owner, "static ring (near half)", 48, 24, 6, 0.04f, true,
            Centre, Palettes.Storm, (c, f, n) => PixelArt.StaticRing(c, f, n, 2)));
        public static FxAnim Zap => zap ?? (zap = FxAnim.Build(Owner, "spark", 9, 9, 4, 0.04f, false,
            Centre, Palettes.Storm, PixelArt.Zap));
        public static FxAnim Bolt => bolt ?? (bolt = FxAnim.Build(Owner, "bolt", 64, 16, 5, 0.045f, false,
            new Vector2(0f, 0.5f), Palettes.Storm, PixelArt.Bolt));
        public static FxAnim Burst => burst ?? (burst = FxAnim.Build(Owner, "thunder burst", 48, 48, 7, 0.045f, false,
            Centre, Palettes.Storm, PixelArt.ThunderBurst));
        public static FxAnim Ring => ring ?? (ring = FxAnim.Build(Owner, "shock ring", 64, 32, 6, 0.05f, false,
            Centre, Palettes.Storm, PixelArt.GroundRing));
    }
}
