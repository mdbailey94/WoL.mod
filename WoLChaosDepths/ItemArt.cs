using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace WoLChaosDepths
{
    // The items' artwork, drawn in code in the game's pixel style: one picture per item type
    // (amulet, ring, charm), 14x14, that gains colour and glamour with rarity, and a design of
    // its own for each unique legendary. Used on the floor (Pickups) and in the inventory panel.
    //
    //   Common     the item in dull metal and a grey stone
    //   Magic      the stone in the item's colour (blue), a lit facet
    //   Rare       gold metal, the stone gold, a white spark
    //   Legendary  the stone orange with a gold setting and a halo of sparks
    //   Unique     its own picture, in its power's colour
    public static class ItemArt
    {
        public const int Size = 14;

        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        // Palette (sampled from the game's relic icons' feel: a dark outline-less shading).
        private static readonly Color Iron = new Color32(0x8a, 0x8f, 0x9a, 0xff), IronDark = new Color32(0x55, 0x59, 0x66, 0xff), IronLight = new Color32(0xc4, 0xc8, 0xd2, 0xff);
        private static readonly Color Gold = new Color32(0xe8, 0xc4, 0x5c, 0xff), GoldDark = new Color32(0xa0, 0x7c, 0x28, 0xff), GoldLight = new Color32(0xff, 0xee, 0xa8, 0xff);
        private static readonly Color Leather = new Color32(0x8c, 0x5e, 0x3a, 0xff), LeatherDark = new Color32(0x5a, 0x3a, 0x22, 0xff), LeatherLight = new Color32(0xb8, 0x88, 0x5c, 0xff);
        private static readonly Color Cord = new Color32(0x6a, 0x4e, 0x32, 0xff);

        private static string Key(ItemData item) => (item.unique ?? "") + "|" + item.slot + "|" + item.rarity;

        public static int Tier(ItemData item)
        {
            if (item == null)
                return 0;
            return Mathf.Clamp(LootTable.RarityIndex(item.rarity), 0, 3);
        }

        // The stone's colour by rarity, or the unique's power colour.
        public static Color Stone(ItemData item)
        {
            switch (item != null ? item.unique : null)
            {
                case "Chain": return new Color32(0x7f, 0xdc, 0xff, 0xff);
                case "Pyre": return new Color32(0xff, 0x7a, 0x2a, 0xff);
                case "Leech": return new Color32(0xe0, 0x30, 0x48, 0xff);
                case "Gale": return new Color32(0xa8, 0xf0, 0xd0, 0xff);
                case "Echo": return new Color32(0xd8, 0xb0, 0xff, 0xff);
                case "Thorn": return new Color32(0x8c, 0xd0, 0x50, 0xff);
                // The 0.16.0 charms: the main colour of WoL Art's red, green and violet orbs.
                case "Ember": return new Color32(0xff, 0x5a, 0x3a, 0xff);
                case "Moss": return new Color32(0x4c, 0xc0, 0x70, 0xff);
                case "Void": return new Color32(0xb3, 0x8a, 0xe6, 0xff);
            }
            return Pickups.Colour(item);
        }

        public static Texture2D Texture(ItemData item)
        {
            Sprite(item, 16f);
            Texture2D t;
            return textures.TryGetValue(Key(item), out t) ? t : null;
        }

        public static Sprite Sprite(ItemData item, float ppu)
        {
            string key = Key(item) + "|" + ppu;
            Sprite s;
            if (sprites.TryGetValue(key, out s) && s != null)
                return s;
            Texture2D tex;
            if (!textures.TryGetValue(Key(item), out tex) || tex == null)
            {
                tex = Paint(item);
                textures[Key(item)] = tex;
            }
            s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
            sprites[key] = s;
            return s;
        }

        // ---- Drawn artwork ----
        // The item pictures are PNGs (16x16, drawn in the game's pixel style): item_<slot>_<rarity>
        // for the three item types at each rarity and unique_<key> for the uniques. A file in an
        // "Art" folder beside the plugin DLL wins; otherwise the copy embedded in the DLL is used;
        // with neither, the picture is drawn in code (Paint below).
        private static string artFolder;
        private static readonly Dictionary<string, bool> missing = new Dictionary<string, bool>();

        public static string ArtFolder
        {
            get
            {
                if (artFolder == null)
                {
                    try { artFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Art"); }
                    catch { artFolder = ""; }
                }
                return artFolder;
            }
        }

        private static string ArtName(ItemData item)
        {
            if (!string.IsNullOrEmpty(item.unique))
                return "unique_" + item.unique.ToLowerInvariant();
            return "item_" + (item.slot ?? "amulet").ToLowerInvariant() + "_" + (item.rarity ?? "common").ToLowerInvariant();
        }

        // A sprite sheet by name (frames side by side, all the same width), as sprites with the
        // given pivot; null when there is no such PNG. Cached per name, pivot and ppu.
        // The worn gear asks for its frames every frame, so the key is a struct, not a string
        // built (with the pivot and ppu formatted as text) on every call (0.13.4, #26).
        private struct SheetKey : IEquatable<SheetKey>
        {
            public string name;
            public int frameCount;
            public Vector2 pivot;
            public float ppu;

            public bool Equals(SheetKey o) => frameCount == o.frameCount && pivot == o.pivot && ppu == o.ppu && name == o.name;
            public override bool Equals(object obj) => obj is SheetKey && Equals((SheetKey)obj);
            public override int GetHashCode() => ((name != null ? name.GetHashCode() : 0) * 31 + frameCount) * 31 + pivot.GetHashCode() ^ ppu.GetHashCode();
        }

        private static readonly Dictionary<SheetKey, Sprite[]> sheets = new Dictionary<SheetKey, Sprite[]>();

        public static Sprite[] Frames(string name, int frameCount, Vector2 pivot, float ppu)
        {
            var key = new SheetKey { name = name, frameCount = frameCount, pivot = pivot, ppu = ppu };
            Sprite[] result;
            if (sheets.TryGetValue(key, out result))
                return result;
            Texture2D tex = LoadPng(name);
            if (tex != null && frameCount > 0 && tex.width % frameCount == 0)
            {
                int w = tex.width / frameCount;
                result = new Sprite[frameCount];
                for (int i = 0; i < frameCount; i++)
                    result[i] = UnityEngine.Sprite.Create(tex, new Rect(i * w, 0, w, tex.height), pivot, ppu);
            }
            else
            {
                if (tex != null)
                    ChaosDepthsPlugin.LogWarning($"art: {name}.png is {tex.width} px wide, not a multiple of {frameCount} frames; using the code-drawn one");
                result = null;
            }
            sheets[key] = result;
            return result;
        }

        public static string RarityName(int tier) => tier <= 0 ? "common" : tier == 1 ? "magic" : tier == 2 ? "rare" : "legendary";

        // A PNG by name, from the Art folder or the DLL; null when there is none.
        private static readonly Dictionary<string, Texture2D> loaded = new Dictionary<string, Texture2D>();

        public static Texture2D LoadPng(string name)
        {
            if (missing.ContainsKey(name))
                return null;
            Texture2D cached;
            if (loaded.TryGetValue(name, out cached) && cached != null)
                return cached;
            byte[] data = null;
            try
            {
                string file = Path.Combine(ArtFolder, name + ".png");
                if (ArtFolder != "" && File.Exists(file))
                    data = File.ReadAllBytes(file);
            }
            catch { }
            if (data == null)
            {
                try
                {
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WoLChaosDepths.Art." + name + ".png"))
                    {
                        if (stream != null)
                        {
                            data = new byte[stream.Length];
                            int read = 0;
                            while (read < data.Length)
                            {
                                int n = stream.Read(data, read, data.Length - read);
                                if (n <= 0) break;
                                read += n;
                            }
                        }
                    }
                }
                catch { }
            }
            if (data == null)
            {
                missing[name] = true;
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            if (!tex.LoadImage(data))
            {
                missing[name] = true;
                return null;
            }
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            loaded[name] = tex;
            return tex;
        }

        // ---- Painting ----

        private class Canvas
        {
            public readonly Color[] px = new Color[Size * Size];
            public void Set(int x, int y, Color c)
            {
                if (x >= 0 && x < Size && y >= 0 && y < Size)
                    px[y * Size + x] = c;
            }
            public Color Get(int x, int y) => x >= 0 && x < Size && y >= 0 && y < Size ? px[y * Size + x] : Color.clear;
            public void Line(int x0, int y0, int x1, int y1, Color c)
            {
                int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
                for (int n = 0; n < 64; n++)
                {
                    Set(x0, y0, c);
                    if (x0 == x1 && y0 == y1)
                        break;
                    int e2 = 2 * err;
                    if (e2 >= dy) { err += dy; x0 += sx; }
                    if (e2 <= dx) { err += dx; y0 += sy; }
                }
            }
            // A diamond-cut stone centred at (cx, cy) with radius r: rim dark, body c, one lit facet.
            public void Gem(int cx, int cy, int r, Color c, Color light, Color dark)
            {
                for (int y = -r; y <= r; y++)
                    for (int x = -r; x <= r; x++)
                    {
                        int d = Mathf.Abs(x) + Mathf.Abs(y);
                        if (d > r)
                            continue;
                        Color col = d == r ? dark : (x < 0 && y > 0 && d >= 1 ? light : c);
                        Set(cx + x, cy + y, col);
                    }
            }
            // The gold setting round a stone: four points past its tips.
            public void Setting(int cx, int cy, int r, Color gold, Color goldDark)
            {
                Set(cx, cy + r + 1, gold); Set(cx, cy - r - 1, goldDark);
                Set(cx + r + 1, cy, gold); Set(cx - r - 1, cy, goldDark);
            }
            // Sparks in a halo round a point.
            public void Halo(int cx, int cy, int r, Color c)
            {
                Set(cx - r, cy + r, c); Set(cx + r, cy + r, c);
                Set(cx - r, cy - r, c); Set(cx + r, cy - r, c);
            }
        }

        private static Texture2D Paint(ItemData item)
        {
            Texture2D drawn = LoadPng(ArtName(item));
            if (drawn != null)
                return drawn;
            var c = new Canvas();
            int tier = Tier(item);
            bool unique = !string.IsNullOrEmpty(item.unique);
            Color stone = Stone(item);
            // Common: a dull grey stone; otherwise the rarity colour (or the unique's).
            if (tier == 0 && !unique)
                stone = new Color32(0xb0, 0xb4, 0xbc, 0xff);
            Color stoneLight = Color.Lerp(stone, Color.white, 0.6f), stoneDark = Color.Lerp(stone, Color.black, 0.45f);
            // Metal: iron up to Magic, gold from Rare.
            Color metal = tier >= 2 || unique ? Gold : Iron, metalDark = tier >= 2 || unique ? GoldDark : IronDark, metalLight = tier >= 2 || unique ? GoldLight : IronLight;

            if (unique)
                PaintUnique(c, item.unique, stone, stoneLight, stoneDark);
            else
                switch (item.slot)
                {
                    case "Ring": PaintRing(c, tier, stone, stoneLight, stoneDark, metal, metalDark, metalLight); break;
                    case "Charm": PaintCharm(c, tier, stone, stoneLight, stoneDark, metal, metalDark); break;
                    default: PaintAmulet(c, tier, stone, stoneLight, stoneDark, metal, metalDark, metalLight); break;
                }

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(c.px);
            tex.Apply();
            return tex;
        }

        // An amulet: a chain arching over the top, a stone hanging from it. The stone grows with
        // rarity (r 2, 2, 3, 3), gets a lit facet from Magic, a spark from Rare, a setting and a
        // halo on Legendary.
        private static void PaintAmulet(Canvas c, int tier, Color stone, Color light, Color dark, Color metal, Color metalDark, Color metalLight)
        {
            // The chain: an arch from (2,7) up to (6,12) and down to (11,7), in links.
            int[,] chain = { { 2, 6 }, { 2, 8 }, { 3, 10 }, { 4, 11 }, { 6, 12 }, { 8, 12 }, { 10, 11 }, { 11, 10 }, { 12, 8 }, { 12, 6 } };
            for (int i = 0; i < chain.GetLength(0); i++)
                c.Set(chain[i, 0], chain[i, 1], i % 2 == 0 ? metal : metalDark);
            // The bail (the loop the stone hangs from).
            c.Set(7, 9, metalLight); c.Set(7, 8, metalDark);
            int r = tier >= 2 ? 3 : 2;
            c.Gem(7, 4, r, stone, tier >= 1 ? light : stone, dark);
            if (tier >= 2)
                c.Set(6, 5, Color.white);                 // a spark
            if (tier >= 3)
            {
                c.Setting(7, 4, r, metal, metalDark);
                c.Halo(7, 4, r + 2, light);
            }
        }

        // A ring seen from a little above: an oval band with a stone on top. The band is iron
        // up to Magic and gold from Rare; the stone grows and a Legendary gets a setting and halo.
        private static void PaintRing(Canvas c, int tier, Color stone, Color light, Color dark, Color metal, Color metalDark, Color metalLight)
        {
            // The band: an ellipse 9 wide, 5 tall, centred (7,5); the far side darker.
            for (int y = 3; y <= 7; y++)
                for (int x = 3; x <= 11; x++)
                {
                    float ex = (x - 7f) / 4.5f, ey = (y - 5f) / 2.5f;
                    float d = ex * ex + ey * ey;
                    if (d <= 1.05f && d >= 0.45f)
                        c.Set(x, y, y >= 6 ? metalDark : y == 3 ? metalLight : metal);
                }
            int r = tier == 0 ? 1 : 2;
            c.Gem(7, 8 + (r - 1), r, stone, tier >= 1 ? light : stone, dark);
            if (tier >= 2)
                c.Set(6, 9 + (r - 1), Color.white);
            if (tier >= 3)
            {
                c.Setting(7, 8 + (r - 1), r, metal, metalDark);
                c.Halo(7, 9, 4, light);
            }
        }

        // A charm: a small leather pouch on a cord, with a stone sewn on its face. The stone
        // takes the rarity; from Rare the stitching is gold; a Legendary gets a halo.
        private static void PaintCharm(Canvas c, int tier, Color stone, Color light, Color dark, Color metal, Color metalDark)
        {
            // The cord loop at the top.
            c.Line(5, 12, 7, 13, Cord); c.Line(7, 13, 9, 12, Cord); c.Set(7, 11, Cord);
            // The pouch: 7 wide, 8 tall, rounded, gathered at the neck.
            for (int y = 2; y <= 10; y++)
                for (int x = 3; x <= 11; x++)
                {
                    int w = y >= 9 ? 2 : y >= 8 ? 3 : y <= 2 ? 3 : 4;
                    if (Mathf.Abs(x - 7) > w)
                        continue;
                    bool edge = Mathf.Abs(x - 7) == w || y == 2;
                    c.Set(x, y, edge ? LeatherDark : (x < 7 && y > 4 ? LeatherLight : Leather));
                }
            // Stitching along the neck.
            for (int x = 5; x <= 9; x++)
                c.Set(x, 9, tier >= 2 ? metal : LeatherDark);
            int r = tier == 0 ? 1 : 2;
            c.Gem(7, 5, r, stone, tier >= 1 ? light : stone, dark);
            if (tier >= 2)
                c.Set(6, 6, Color.white);
            if (tier >= 3)
                c.Halo(7, 5, 4, light);
        }

        // ---- The uniques, each its own picture ----

        private static void PaintUnique(Canvas c, string key, Color stone, Color light, Color dark)
        {
            switch (key)
            {
                case "Chain":   // Stormlinked Signet: a gold signet ring whose face is a lightning bolt.
                    for (int y = 2; y <= 6; y++)
                        for (int x = 3; x <= 11; x++)
                        {
                            float ex = (x - 7f) / 4.5f, ey = (y - 4f) / 2.5f;
                            float d = ex * ex + ey * ey;
                            if (d <= 1.05f && d >= 0.45f)
                                c.Set(x, y, y >= 5 ? GoldDark : y == 2 ? GoldLight : Gold);
                        }
                    // The face: a 5x5 plate with a cyan bolt.
                    for (int y = 6; y <= 10; y++)
                        for (int x = 5; x <= 9; x++)
                            c.Set(x, y, (x == 5 || x == 9 || y == 6 || y == 10) ? GoldDark : Gold);
                    c.Set(8, 10, stone); c.Set(7, 9, stone); c.Set(8, 8, light); c.Set(7, 8, stone); c.Set(6, 7, stone); c.Set(6, 6, light);
                    c.Set(8, 12, light); c.Set(10, 11, light); c.Set(4, 11, light);     // sparks
                    break;
                case "Pyre":    // Cinderheart: a heart-shaped ember on a dark chain, a flame licking off its top.
                    for (int i = 0; i < 5; i++)
                    {
                        c.Set(2 + i, 9 + Mathf.Min(i, 2), i % 2 == 0 ? IronDark : Iron);
                        c.Set(12 - i, 9 + Mathf.Min(i, 2), i % 2 == 0 ? IronDark : Iron);
                    }
                    c.Set(7, 10, Iron);
                    // The heart: two lobes and a point.
                    int[,] heart = { { 5, 8 }, { 6, 8 }, { 8, 8 }, { 9, 8 }, { 4, 7 }, { 5, 7 }, { 6, 7 }, { 7, 7 }, { 8, 7 }, { 9, 7 }, { 10, 7 },
                                     { 4, 6 }, { 5, 6 }, { 6, 6 }, { 7, 6 }, { 8, 6 }, { 9, 6 }, { 10, 6 }, { 5, 5 }, { 6, 5 }, { 7, 5 }, { 8, 5 }, { 9, 5 },
                                     { 6, 4 }, { 7, 4 }, { 8, 4 }, { 7, 3 } };
                    for (int i = 0; i < heart.GetLength(0); i++)
                    {
                        int x = heart[i, 0], y = heart[i, 1];
                        bool edge = x == 4 || x == 10 || y == 3 || (y == 8) || (y == 4 && (x == 6 || x == 8)) || (y == 5 && (x == 5 || x == 9));
                        c.Set(x, y, edge ? dark : (x <= 6 && y >= 6 ? light : stone));
                    }
                    c.Set(7, 6, GoldLight);                                           // the ember's white heart
                    c.Set(6, 9, stone); c.Set(6, 10, light); c.Set(7, 11, GoldLight); // the flame
                    break;
                case "Leech":   // Bloodwell Charm: a glass vial of blood, corked, on a cord.
                    c.Line(5, 13, 9, 13, Cord); c.Set(7, 12, Cord);
                    for (int x = 5; x <= 9; x++) c.Set(x, 11, Leather);               // the cork
                    for (int x = 6; x <= 8; x++) c.Set(x, 10, LeatherDark);           // its neck
                    for (int y = 2; y <= 9; y++)
                        for (int x = 4; x <= 10; x++)
                        {
                            bool edge = x == 4 || x == 10 || y == 2 || y == 9;
                            if (y == 9 && (x == 4 || x == 10)) continue;
                            if (y == 2 && (x == 4 || x == 10)) continue;
                            Color glass = new Color32(0xc8, 0xe0, 0xf0, 0xff);
                            if (edge)
                                c.Set(x, y, glass);
                            else
                                c.Set(x, y, y <= 6 ? (x == 5 ? light : stone) : (Color)new Color32(0x30, 0x40, 0x50, 0xff));
                        }
                    c.Set(5, 8, Color.white);                                          // a glint on the glass
                    c.Set(9, 5, dark); c.Set(8, 7, dark);                              // a bubble
                    break;
                case "Gale":    // Galeheart: an amulet with a swirl of wind in a pale green stone.
                    PaintAmulet(c, 3, stone, light, dark, Gold, GoldDark, GoldLight);
                    c.Set(7, 4, Color.white); c.Set(6, 5, Color.white); c.Set(8, 3, Color.white); c.Set(6, 3, light);
                    break;
                case "Echo":    // Twinfold Band: two interlocked rings, violet stones on both.
                    for (int k = 0; k < 2; k++)
                        for (int y = 3; y <= 7; y++)
                            for (int x = 1; x <= 12; x++)
                            {
                                float ex = (x - (5f + 3.5f * k)) / 3.5f, ey = (y - 5f) / 2.5f;
                                float d = ex * ex + ey * ey;
                                if (d <= 1.05f && d >= 0.4f)
                                    c.Set(x, y, y >= 6 ? GoldDark : y == 3 ? GoldLight : Gold);
                            }
                    c.Gem(4, 9, 1, stone, light, dark); c.Gem(9, 9, 1, stone, light, dark);
                    c.Set(6, 11, light); c.Set(7, 12, Color.white);
                    break;
                case "Thorn":   // Thornwick Charm: a wooden token wound with a thorny vine.
                    c.Line(5, 13, 9, 13, Cord); c.Set(7, 12, Cord);
                    for (int y = 2; y <= 11; y++)
                        for (int x = 4; x <= 10; x++)
                        {
                            bool edge = x == 4 || x == 10 || y == 2 || y == 11;
                            c.Set(x, y, edge ? LeatherDark : (x <= 6 ? LeatherLight : Leather));
                        }
                    c.Line(4, 3, 10, 10, stone); c.Line(4, 10, 10, 3, dark);          // the vine, crossed
                    c.Set(3, 5, light); c.Set(11, 8, light); c.Set(6, 12, light);     // thorns
                    c.Set(7, 6, Color.white);
                    break;
                default:
                    PaintAmulet(c, 3, stone, light, dark, Gold, GoldDark, GoldLight);
                    break;
            }
        }
    }
}
