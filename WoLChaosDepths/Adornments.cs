using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLChaosDepths
{
    // M5, second slice: equipped items show on the wizard, drawn in the game's pixel style.
    //   Amulet  a pendant on a short chain at the chest
    //   Ring    a glint on the leading hand that twinkles now and then
    //   Charm   stones (one per rarity tier) floating in a circle round the wizard, bobbing
    // Each is in its item's rarity colour (a unique in its power's colour, with the game's own
    // hit sparks drifting off it). They follow the wizard's facing: in front of the body facing
    // down, left or right, and hidden behind it facing up.
    public static class Adornments
    {
        private static ConfigEntry<bool> enabled, uniqueSparks;
        private static ConfigEntry<float> legendaryCharmScale, charmOrbitHeight;
        private static readonly Dictionary<int, Adorn> adorns = new Dictionary<int, Adorn>();
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private static Material material;

        public static void Bind(ConfigFile config)
        {
            enabled = config.Bind("Look", "ShowEquipped", true, "Equipped items show on the wizard: a pendant, a ring glint and a belt charm.");
            uniqueSparks = config.Bind("Look", "UniqueSparks", true, "Unique legendaries worn give off small sparks.");
            charmOrbitHeight = config.Bind("Look", "CharmOrbitHeight", 0.45f,
                new ConfigDescription("How high the charm's stones circle, as a fraction of the wizard's height: 0.6 was chest height (0.11.9 and before), 0.45 the waist, 0.3 the hips.", new AcceptableValueRange<float>(0.1f, 0.9f)));
            legendaryCharmScale = config.Bind("Look", "LegendaryCharmScale", 0.6f,
                new ConfigDescription("Size of the four stones a Legendary (or unique) charm floats round the wizard, as a fraction of the drawn 7x7 stone. 1 = full size.", new AcceptableValueRange<float>(0.4f, 1f)));
        }

        // Twice a second from the plugin, for every active player, in and out of the trials.
        public static void Tick(Player player, CharacterSave save)
        {
            if (player == null)
                return;
            int id = player.GetInstanceID();
            Adorn adorn;
            if (!enabled.Value)
            {
                if (adorns.TryGetValue(id, out adorn) && adorn != null)
                    adorn.Vanish();
                adorns.Remove(id);
                return;
            }
            if (!adorns.TryGetValue(id, out adorn) || adorn == null)
            {
                var go = new GameObject("ChaosDepthsAdornments");
                adorn = go.AddComponent<Adorn>();
                adorn.player = player;
                adorns[id] = adorn;
            }
            adorn.Refresh(save);
        }

        public static void Clear()
        {
            foreach (Adorn a in adorns.Values)
                if (a != null)
                    a.Vanish();
            adorns.Clear();
            BodyPixels.Prune();
        }

        // ---- Colours ----

        private static Color ColourOf(ItemData item)
        {
            if (item == null)
                return Color.clear;
            return ItemArt.Stone(item); // a unique in its power's colour, else the rarity's
        }

        // ---- Pixel sprites, drawn in code (1 px = 1/ppu world units) ----

        private static Material Mat()
        {
            if (material == null)
                material = new Material(Shader.Find("Sprites/Default"));
            return material;
        }

        private static Sprite Make(string key, int w, int h, Func<int, int, Color> pixel, Vector2 pivot, float ppu)
        {
            string k = key + ppu;
            Sprite s;
            if (sprites.TryGetValue(k, out s))
                return s;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel(x, y);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu);
            sprites[k] = s;
            return s;
        }

        // ---- Drawn designs ----

        // Each unique's own worn picture (worn_<part>_<name>), by its key.
        private static readonly Dictionary<string, string> UniqueArt = new Dictionary<string, string>
        {
            { "Pyre", "cinderheart" }, { "Gale", "galeheart" },
            { "Chain", "signet" }, { "Echo", "twinfold" },
            { "Leech", "bloodwell" }, { "Thorn", "thornwick" },
            // The themed Legendary orbs, kept for these since 0.15.12 (their trails come with them).
            { "Ember", "legendary_offense" }, { "Moss", "legendary_defense" }, { "Void", "legendary_utility" }
        };

        // A non-unique Legendary's theme, from the strongest stat its name is taken from (#8):
        // offense (Might, Precision, Ruin), defense (Vigor, Warding, Mending), utility (Haste,
        // the Wind, Shadows).
        private static readonly Dictionary<string, string> ThemeOfAffix = new Dictionary<string, string>
        {
            { "Damage", "offense" }, { "CritChance", "offense" }, { "CritDamage", "offense" },
            { "Health", "defense" }, { "DamageTaken", "defense" }, { "Healing", "defense" },
            { "Cooldown", "utility" }, { "MoveSpeed", "utility" }, { "Evade", "utility" }
        };

        private static string Theme(ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.name) || LootTable.Current == null)
                return null;
            int of = item.name.IndexOf(" of ", StringComparison.Ordinal);
            if (of < 0)
                return null;
            string suffix = item.name.Substring(of + 4);
            foreach (LootTable.AffixDef def in LootTable.Current.affixes)
            {
                string theme;
                if (def.suffix == suffix && ThemeOfAffix.TryGetValue(def.key, out theme))
                    return theme;
            }
            return null;
        }

        // The drawn pictures to try for an item, best first: its unique's own, then a
        // Legendary's theme, then its rarity (worn_pendant_cinderheart, worn_pendant_legendary_
        // offense, worn_pendant_legendary).
        private static string[] Designs(string part, ItemData item, int tier)
        {
            var list = new List<string>();
            if (item == null)
                return list.ToArray();
            string name;
            if (!string.IsNullOrEmpty(item.unique) && UniqueArt.TryGetValue(item.unique, out name))
                list.Add("worn_" + part + "_" + name);
            // A Legendary charm keeps the plain orb whatever its theme (Matt, 0.15.12): the theme
            // shows in its trail (CharmTrail), and the themed orbs are kept for uniques.
            string theme = tier >= 3 && string.IsNullOrEmpty(item.unique) && part != "charm" ? Theme(item) : null;
            if (theme != null)
                list.Add("worn_" + part + "_legendary_" + theme);
            list.Add("worn_" + part + "_" + ItemArt.RarityName(tier));
            return list.ToArray();
        }

        private static readonly Vector2 TopCentre = new Vector2(0.5f, 1f), Centre = new Vector2(0.5f, 0.5f);

        // The drawn pendant for this view ("down", "side" or "up"), from the first design that
        // has it: its 4-frame flicker loop when one is asked for and drawn (Cinderheart, standing),
        // its 2-frame swing while running, else its rest / glint pair. Then the older single
        // picture for every view (worn_pendant_<rarity>.png). Null when nothing is drawn.
        private static Sprite DrawnPendant(string[] designs, string view, int glint, int swing, int flicker, float ppu)
        {
            foreach (string d in designs)
            {
                Sprite[] rest = ItemArt.Frames(d + "_" + view, 2, TopCentre, ppu);
                if (rest == null)
                    continue;
                if (flicker >= 0)
                {
                    Sprite[] f = ItemArt.Frames(d + "_" + view + "_flicker", 4, TopCentre, ppu);
                    if (f != null)
                        return f[flicker & 3];
                }
                if (swing >= 0)
                {
                    Sprite[] s = ItemArt.Frames(d + "_" + view + "_swing", 2, TopCentre, ppu);
                    if (s != null)
                        return s[swing & 1];
                }
                return rest[Mathf.Clamp(glint, 0, 1)];
            }
            foreach (string d in designs)
            {
                Sprite[] single = ItemArt.Frames(d, 2, TopCentre, ppu);
                if (single != null)
                    return single[Mathf.Clamp(glint, 0, 1)];
            }
            return null;
        }

        // The eyes facing sideways: one eye, the left 3 columns of each of the strip's 4 frames
        // (its eye pixel in the middle), centred like the full frames.
        private static readonly Dictionary<string, Sprite[]> oneEye = new Dictionary<string, Sprite[]>();

        private static Sprite[] OneEye(string name, float ppu)
        {
            string key = name + "|" + ppu;
            Sprite[] frames;
            if (oneEye.TryGetValue(key, out frames))
                return frames;
            frames = null;
            Texture2D tex = ItemArt.LoadPng(name);
            if (tex != null && tex.width % 4 == 0 && tex.width / 4 >= 3)
            {
                int w = tex.width / 4;
                frames = new Sprite[4];
                for (int i = 0; i < 4; i++)
                    frames[i] = Sprite.Create(tex, new Rect(i * w, 0, 3, tex.height), new Vector2(0.5f, 0.5f), ppu);
            }
            oneEye[key] = frames;
            return frames;
        }

        private static bool HasPendantView(string[] designs, string view)
        {
            foreach (string d in designs)
                if (ItemArt.LoadPng(d + "_" + view) != null)
                    return true;
            return false;
        }

        // The first design with a picture of its own (2 frames: rest, glint), for rings and charms.
        // A charm's trail puffs: a unique's own; a plain Legendary's in its theme's colour (red
        // offense, green defense, violet utility) when drawn, else the plain orange one.
        private static string CharmTrail(ItemData charm, string design)
        {
            string theme = charm != null && string.IsNullOrEmpty(charm.unique) ? Theme(charm) : null;
            if (theme != null && ItemArt.LoadPng("worn_charm_legendary_" + theme + "_trail") != null)
                return "worn_charm_legendary_" + theme + "_trail";
            return design + "_trail";
        }

        private static string FirstDrawn(string[] designs)
        {
            foreach (string d in designs)
                if (ItemArt.LoadPng(d) != null)
                    return d;
            return null;
        }

        // Rarity tier: 0 Common, 1 Magic, 2 Rare, 3 Legendary (a unique counts as Legendary).
        // Each part grows and shines more with it.
        private static int Tier(ItemData item)
        {
            if (item == null)
                return 0;
            if (!string.IsNullOrEmpty(item.unique))
                return 3;
            return Mathf.Clamp(LootTable.RarityIndex(item.rarity), 0, 3);
        }

        // A pendant on a short chain: a 3x3 gem (Common), 5x5 (Magic), 7x7 (Rare), 7x7 in a gold
        // setting (Legendary), with a dark rim and a lit facet. When it glints the facet goes
        // white, and on a Legendary the setting's four points too. Pivot top.
        // The code-drawn fallback when no picture is drawn (see DrawnPendant).
        private static Sprite Pendant(Color c, int tier, int frame, float ppu)
        {
            Color chain = new Color32(0xc8, 0xb0, 0x60, 0xff), setting = new Color32(0xf0, 0xd0, 0x70, 0xff);
            Color light = Color.Lerp(c, Color.white, 0.6f), dark = Color.Lerp(c, Color.black, 0.45f);
            int r = tier == 0 ? 1 : tier == 1 ? 2 : 3;
            int outer = tier >= 3 ? r + 1 : r;
            int size = outer * 2 + 1, links = tier == 0 ? 2 : 3;
            return Make("pendant" + tier + frame + ColorUtility.ToHtmlStringRGB(c), size, size + links, (x, y) =>
            {
                if (y >= size)
                    return x == outer ? chain : Color.clear;      // the chain, top rows
                int dx = Mathf.Abs(x - outer), dy = Mathf.Abs(y - outer), d = dx + dy;
                if (d > outer)
                    return Color.clear;
                if (d > r)                                        // the gold setting
                    return frame == 1 && (dx == 0 || dy == 0) ? Color.white : setting;
                if (d == r)
                    return dark;                                  // the rim
                bool facet = d >= 1 && x < outer && y >= outer;   // the upper-left facet
                if (facet)
                    return frame == 1 ? Color.white : light;
                return c;
            }, new Vector2(0.5f, 1f), ppu);
        }

        // A ring on the hand: a plain 3x2 band (Common), a silver band with a 1-px stone
        // (Magic), a 4-wide gold band with a 2-px stone (Rare), a 5-wide gold band with a 3x2
        // stone (Legendary). The stone is in the item's colour; a glint lights its corner (all
        // of its top row on a Legendary). Pivot on the band.
        // design: the drawn picture to use (see Designs), or null for the code-drawn one.
        private static Sprite Ring(string design, Color c, int tier, int frame, float ppu)
        {
            // Drawn ring: pivot on the band (the bottom row), as the code-drawn one.
            Texture2D ringArt = design != null ? ItemArt.LoadPng(design) : null;
            Sprite[] drawn = ringArt != null ? ItemArt.Frames(design, 2, new Vector2(0.5f, 1f / ringArt.height), ppu) : null;
            if (drawn != null)
                return drawn[Mathf.Clamp(frame, 0, 1)];
            Color metal = tier >= 2 ? (Color)new Color32(0xe8, 0xc4, 0x5c, 0xff) : tier == 1 ? (Color)new Color32(0xc0, 0xc4, 0xd0, 0xff) : c;
            Color metalDark = Color.Lerp(metal, Color.black, 0.35f), light = Color.Lerp(c, Color.white, 0.7f);
            int w = tier <= 1 ? 3 : tier == 2 ? 4 : 5;
            int sw = tier == 0 ? 0 : tier == 1 ? 1 : tier == 2 ? 2 : 3;
            int sh = tier == 3 ? 2 : sw > 0 ? 1 : 0;
            int sx = (w - sw) / 2, h = 2 + sh;
            return Make("ring" + tier + frame + ColorUtility.ToHtmlStringRGB(c), w, h, (x, y) =>
            {
                if (y == 0)
                    return metalDark;
                if (y == 1)
                    return metal;
                if (x < sx || x >= sx + sw)
                    return Color.clear;                           // the stone, above the band
                bool corner = x == sx && y == h - 1;
                if (frame == 1 && (corner || (tier >= 3 && y == h - 1)))
                    return Color.white;
                return corner ? light : c;
            }, new Vector2(0.5f, 1f / h), ppu);
        }

        // A charm stone that floats round the wizard: a 3x3 gem (Common); a 5x5 gem with a lit
        // facet (Magic); the same with a white spark on its point (Rare); a 5x5 gem in a gold
        // setting with four points (Legendary, 7x7). A glint lights the facet (and the setting's
        // points on a Legendary). Pivot centre. design: the drawn orb (see Designs), or null.
        private static Sprite Charmstone(string design, Color c, int tier, int frame, float ppu)
        {
            Sprite[] drawn = design != null ? ItemArt.Frames(design, 2, Centre, ppu) : null;
            if (drawn != null)
                return drawn[Mathf.Clamp(frame, 0, 1)];
            // The setting is a darker gold at rest so four Legendary stones don't read as a
            // cloud of white sparks (the 0.9.7 check); it only goes white on a glint.
            Color setting = new Color32(0xc8, 0xa0, 0x40, 0xff);
            Color light = Color.Lerp(c, Color.white, 0.6f), dark = Color.Lerp(c, Color.black, 0.45f);
            int r = tier == 0 ? 1 : 2;
            int outer = tier >= 3 ? r + 1 : r;
            int size = outer * 2 + 1;
            return Make("charm" + tier + frame + ColorUtility.ToHtmlStringRGB(c), size, size, (x, y) =>
            {
                int dx = Mathf.Abs(x - outer), dy = Mathf.Abs(y - outer), d = dx + dy;
                if (d > outer)
                    return Color.clear;
                if (d > r)                                        // the gold setting's points
                    return (dx == 0 || dy == 0) ? (frame == 1 ? Color.white : setting) : Color.clear;
                if (d == r)
                {
                    // Rare and up: the top point is lit (white only while glinting).
                    if (tier >= 2 && dx == 0 && y > outer)
                        return frame == 1 ? Color.white : light;
                    return dark;                                  // the rim
                }
                bool facet = tier >= 1 && d >= 1 && x < outer && y >= outer;
                if (facet)
                    return frame == 1 ? Color.white : light;
                return c;
            }, new Vector2(0.5f, 0.5f), ppu);
        }

        // A 1-px mote that drifts up a few pixels and fades over half a second.
        private class Mote : MonoBehaviour
        {
            private SpriteRenderer r;
            private float born, life = 0.5f;
            private Vector3 start, drift;

            // A pixel left where a floating stone was, fading in 0.3 s (the stone's trail).
            public static void Trail(SpriteRenderer from, Color c, float ppu)
            {
                try
                {
                    var go = new GameObject("trail");
                    var m = go.AddComponent<Mote>();
                    m.r = go.AddComponent<SpriteRenderer>();
                    m.r.sprite = Make("mote", 1, 1, (x, y) => Color.white, new Vector2(0.5f, 0.5f), ppu);
                    m.r.material = Mat();
                    m.r.color = Color.Lerp(c, Color.white, 0.25f);
                    m.r.sortingLayerID = from.sortingLayerID;
                    m.r.sortingOrder = from.sortingOrder - 1;
                    m.born = Time.time;
                    m.life = 0.3f;
                    m.start = from.transform.position;
                    m.drift = Vector3.zero;
                    go.transform.position = m.start;
                }
                catch
                {
                }
            }

            public static void Shed(SpriteRenderer from, Color c, SpriteRenderer body, float ppu)
            {
                try
                {
                    var go = new GameObject("mote");
                    var m = go.AddComponent<Mote>();
                    m.r = go.AddComponent<SpriteRenderer>();
                    m.r.sprite = Make("mote", 1, 1, (x, y) => Color.white, new Vector2(0.5f, 0.5f), ppu);
                    m.r.material = Mat();
                    m.r.color = Color.Lerp(c, Color.white, 0.5f);
                    m.r.sortingLayerID = from.sortingLayerID;
                    m.r.sortingOrder = from.sortingOrder + 1;
                    m.born = Time.time;
                    m.start = from.transform.position + (Vector3)(UnityEngine.Random.insideUnitCircle * (1.5f / ppu));
                    m.drift = new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(2f, 3.5f), 0f) / ppu;
                    go.transform.position = m.start;
                }
                catch
                {
                }
            }

            private void Update()
            {
                float t = (Time.time - born) / life;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                // Snapped to the pixel grid so it reads as part of the picture.
                Vector3 p = start + drift * t;
                float ppu = r.sprite.pixelsPerUnit;
                transform.position = new Vector3(Mathf.Round(p.x * ppu) / ppu, Mathf.Round(p.y * ppu) / ppu, p.z);
                Color c = r.color;
                c.a = 1f - t * t;
                r.color = c;
            }
        }

        // A drawn trail puff (worn_charm_<design>_trail.png, 4 frames) left where a Legendary
        // charm's orb was: it stays put in the world, plays its frames at 10 fps and goes.
        private class Puff : MonoBehaviour
        {
            private SpriteRenderer r;
            private Sprite[] frames;
            private float born;

            public static void Leave(SpriteRenderer from, Sprite[] frames)
            {
                try
                {
                    var go = new GameObject("charm trail");
                    var p = go.AddComponent<Puff>();
                    p.frames = frames;
                    p.r = go.AddComponent<SpriteRenderer>();
                    p.r.sprite = frames[0];
                    p.r.material = Mat();
                    p.r.sortingLayerID = from.sortingLayerID;
                    p.r.sortingOrder = from.sortingOrder - 1;
                    p.born = Time.time;
                    go.transform.position = from.transform.position;
                }
                catch
                {
                }
            }

            private void Update()
            {
                int i = (int)((Time.time - born) * 10f);
                if (i >= frames.Length)
                {
                    Destroy(gameObject);
                    return;
                }
                r.sprite = frames[i];
            }
        }

        // ---- Per-player component ----

        private class Adorn : MonoBehaviour
        {
            public Player player;
            private string signature = "";
            private ItemData amulet, ring, charm;
            private int amuletTier, ringTier, charmTier;
            // The drawn pictures to try for each part, best first (see Designs); the ring's and
            // the charm's picked, or null when only the code-drawn one exists; the charm's trail.
            private string[] amuletDesigns = new string[0];
            private string ringDesign, charmDesign;
            private Sprite[] charmTrail;
            // Running: the swing frame advances every second change of the body's picture, so
            // the pendant swings back and forward once per stride.
            private Sprite lastBodySprite;
            private int bodyFrames;
            private SpriteRenderer pendant, glint;
            // A worn unique's foot aura (WoL Art's unique_aura_<name>.png, #5): 6 frames of 24x10
            // looping at 8 fps behind the wizard, centred under the feet.
            private SpriteRenderer aura;
            private Sprite[] auraFrames;
            // Its glowing eyes (unique_eyes_<name>.png, #5): 4 frames of 5x3 looping at 6 fps over
            // the face, found in every frame from the picture; one eye facing sideways, none
            // facing up.
            private SpriteRenderer eyes;
            private Sprite[] eyeFrames, eyeSide;
            private const int EyeRowsDown = 7;   // the eye row below the top of the picture (WoL Art)
            // The charm's stones floating round the wizard (one per rarity tier above Common).
            private readonly List<SpriteRenderer> orbs = new List<SpriteRenderer>();
            private float orbitPhase, nextTrail;
            // The hand's last spot as an offset from the pivot (so it moves with the wizard), and
            // how many frames in a row a jump was refused.
            private Vector2 lastHand;
            private bool handTracked;
            private int handRefused;
            private float lastHandSide;
            private float ppu = 16f, nextSpark, stillFor;
            private readonly float[] nextTwinkle = new float[3], twinkleStart = { -10f, -10f, -10f };
            private Vector3 lastPos;
            private Vector2 lastSize;

            // Twinkle every Min-Max seconds by tier (Common never twinkles).
            private static readonly float[] TwinkleMin = { 0f, 3f, 2f, 1.2f }, TwinkleMax = { 0f, 5f, 3.5f, 2.2f };

            // Where the gear sits, as offsets from the body sprite's pivot (its feet), measured
            // from the wizard standing still in each facing (down, up, side; the side's x is
            // in the facing direction). The run and dash frames flare the cloak and change the
            // picture's bounds, so measuring every frame threw the gear off the body.
            private class Anchors
            {
                public Vector2 chest, hand, belt;
                public float top;      // the top of the picture, to follow the run's bob
                public bool settled;   // measured standing still (not a guess from a run frame)
            }
            private readonly Anchors[] anchors = new Anchors[3];

            public void Refresh(CharacterSave save)
            {
                if (player == null || player.gameObject == null)
                {
                    Vanish();
                    return;
                }
                ItemData a = Worn(save, "Amulet"), r = Worn(save, "Ring"), c = Worn(save, "Charm");
                string sig = (a != null ? a.id : "") + "|" + (r != null ? r.id : "") + "|" + (c != null ? c.id : "");
                if (sig == signature)
                    return;
                signature = sig;
                amulet = a;
                ring = r;
                charm = c;
                Rebuild();
            }

            private static ItemData Worn(CharacterSave save, string slot)
            {
                if (save == null || save.equipped == null)
                    return null;
                int i = Array.IndexOf(LootTable.Slots, slot);
                return i >= 0 && i < save.equipped.Count ? StatMods.FindItem(save, save.equipped[i]) : null;
            }

            private void Rebuild()
            {
                try
                {
                    SpriteRenderer body = player.spriteRenderer;
                    if (body != null && body.sprite != null && body.sprite.pixelsPerUnit > 0f)
                        ppu = body.sprite.pixelsPerUnit;
                    amuletTier = Tier(amulet);
                    ringTier = Tier(ring);
                    charmTier = Tier(charm);
                    amuletDesigns = Designs("pendant", amulet, amuletTier);
                    ringDesign = FirstDrawn(Designs("ring", ring, ringTier));
                    charmDesign = FirstDrawn(Designs("charm", charm, charmTier));
                    charmTrail = charmDesign != null && charmTier >= 3 ? ItemArt.Frames(CharmTrail(charm, charmDesign), 4, Centre, ppu) : null;
                    pendant = Part(pendant, amulet, amulet != null ? PendantSprite("down", 0, -1, -1) : null);
                    glint = Part(glint, ring, ring != null ? Ring(ringDesign, ColourOf(ring), ringTier, 0, ppu) : null);
                    // The aura of the first unique worn (amulet, then ring, then charm).
                    ItemData auraOf = null;
                    auraFrames = null;
                    foreach (ItemData worn in new[] { amulet, ring, charm })
                    {
                        string art;
                        if (worn == null || string.IsNullOrEmpty(worn.unique) || !UniqueArt.TryGetValue(worn.unique, out art))
                            continue;
                        auraFrames = ItemArt.Frames("unique_aura_" + art, 6, Centre, ppu);
                        if (auraFrames != null)
                        {
                            auraOf = worn;
                            break;
                        }
                    }
                    aura = Part(aura, auraOf, auraFrames != null ? auraFrames[0] : null);
                    // The eyes of the first unique worn that has them.
                    ItemData eyesOf = null;
                    eyeFrames = eyeSide = null;
                    foreach (ItemData worn in new[] { amulet, ring, charm })
                    {
                        string art;
                        if (worn == null || string.IsNullOrEmpty(worn.unique) || !UniqueArt.TryGetValue(worn.unique, out art))
                            continue;
                        eyeFrames = ItemArt.Frames("unique_eyes_" + art, 4, Centre, ppu);
                        if (eyeFrames != null)
                        {
                            eyeSide = OneEye("unique_eyes_" + art, ppu);
                            eyesOf = worn;
                            break;
                        }
                    }
                    eyes = Part(eyes, eyesOf, eyeFrames != null ? eyeFrames[0] : null);
                    // A drawn charm is one orb, its size set by its rarity; the code-drawn
                    // stones are one per rarity tier.
                    int stones = charm == null ? 0 : charmDesign != null ? 1 : charmTier + 1;
                    while (orbs.Count > stones)
                    {
                        if (orbs[orbs.Count - 1] != null)
                            Destroy(orbs[orbs.Count - 1].gameObject);
                        orbs.RemoveAt(orbs.Count - 1);
                    }
                    while (orbs.Count < stones)
                        orbs.Add(null);
                    for (int i = 0; i < stones; i++)
                    {
                        orbs[i] = Part(orbs[i], charm, Charmstone(charmDesign, ColourOf(charm), charmTier, 0, ppu));
                        // The code-drawn Legendary stones are 7x7, which is a lot of orange round
                        // a 37-px wizard four times over; they float a step smaller. A drawn orb
                        // is shown at its true pixel size.
                        if (orbs[i] != null)
                            orbs[i].transform.localScale = Vector3.one * (charmTier >= 3 && charmDesign == null ? legendaryCharmScale.Value : 1f);
                    }
                    // Stagger the first twinkles so the three parts don't flash together.
                    for (int i = 0; i < 3; i++)
                        nextTwinkle[i] = Time.time + UnityEngine.Random.Range(0.5f, 2f);
                }
                catch (Exception e)
                {
                    ChaosDepthsPlugin.LogError($"adornments: {e.Message}");
                }
            }

            private Sprite PendantSprite(string view, int glint, int swing, int flicker)
            {
                return DrawnPendant(amuletDesigns, view, glint, swing, flicker, ppu) ?? Pendant(ColourOf(amulet), amuletTier, glint, ppu);
            }

            private SpriteRenderer Part(SpriteRenderer existing, ItemData item, Sprite sprite)
            {
                if (item == null || sprite == null)
                {
                    if (existing != null)
                        Destroy(existing.gameObject);
                    return null;
                }
                if (existing == null)
                {
                    var go = new GameObject("part");
                    go.transform.SetParent(transform, false);
                    existing = go.AddComponent<SpriteRenderer>();
                    existing.material = Mat();
                }
                existing.sprite = sprite;
                return existing;
            }

            private void LateUpdate()
            {
                if (player == null || player.gameObject == null || !player.gameObject.activeInHierarchy)
                {
                    bool gone = false;
                    try
                    {
                        gone = player == null || player.gameObject == null;
                    }
                    catch
                    {
                        gone = true;
                    }
                    if (gone)
                        Vanish();
                    else
                        Show(false);
                    return;
                }
                SpriteRenderer body = player.spriteRenderer;
                if (body == null || !body.enabled)
                {
                    Show(false);
                    return;
                }
                // Lifted off the ground (riding Gale Rider's ball, up on Thunderhead's cloud): the
                // gear would float in the ball or cloud, so it hides until the wizard is back down.
                if (body.transform.localPosition.y > 0.45f)
                {
                    Show(false);
                    return;
                }
                // A part goes with the body it hangs on; if that body was replaced, make it again.
                bool orbGone = false;
                foreach (SpriteRenderer o in orbs)
                    if (o == null)
                        orbGone = true;
                if ((amulet != null && pendant == null) || (ring != null && glint == null) || (charm != null && (orbs.Count == 0 || orbGone))
                    || (auraFrames != null && aura == null) || (eyeFrames != null && eyes == null))
                    Rebuild();
                Show(true);
                Vector3 feet = player.transform.position;
                FacingDirection facing = player.facingDirection;
                bool up = facing == FacingDirection.Up, down = facing == FacingDirection.Down;
                bool right = facing == FacingDirection.Right || (facing != FacingDirection.Left && player.isFacingRight);
                float side = right ? 1f : -1f;
                int front = body.sortingOrder + 1, back = body.sortingOrder - 1;
                if (aura != null && auraFrames != null)
                {
                    aura.sprite = auraFrames[(int)(Time.time * 8f) % auraFrames.Length];
                    aura.transform.position = new Vector3(Mathf.Round(feet.x * ppu) / ppu, Mathf.Round(feet.y * ppu) / ppu, feet.z);
                    aura.sortingLayerID = body.sortingLayerID;
                    aura.sortingOrder = body.sortingOrder - 2;
                }
                int key = down ? 0 : up ? 1 : 2;
                float fwd = key == 2 ? side : 1f;

                // Measure where the gear goes while the wizard stands still (same spot, same
                // picture size for 0.3 s), then keep those offsets from the pivot through the
                // run, dash and cast frames. A run frame only gives a guess until then. Measured
                // again every time he stands still, so a held cast pose (Thunderhead's landing)
                // that got measured is put right by the next real stand.
                Vector3 pivot = body.transform.position;
                Bounds bb = body.bounds;
                bool still = (feet - lastPos).sqrMagnitude < 1e-8f
                    && Mathf.Abs(bb.size.x - lastSize.x) < 0.5f / ppu && Mathf.Abs(bb.size.y - lastSize.y) < 0.5f / ppu;
                stillFor = still ? stillFor + Time.deltaTime : 0f;
                lastSize = bb.size;
                Anchors a = anchors[key];
                if (a == null || stillFor >= 0.3f)
                    anchors[key] = a = Measure(bb, pivot, up, down, side, fwd, stillFor >= 0.3f);

                // Follow the run's bob (the top of the picture), at most 2 px either way.
                float bob = Mathf.Clamp(Mathf.Round((bb.max.y - pivot.y - a.top) * ppu), -2f, 2f) / ppu;

                bool moving = (feet - lastPos).sqrMagnitude > 1e-8f;
                if (body.sprite != lastBodySprite)
                {
                    lastBodySprite = body.sprite;
                    bodyFrames++;
                }

                if (pendant != null)
                {
                    // One picture for every view unless the art has a view for this facing;
                    // facing up it shows only when there is an "up" picture (the chain at the
                    // back of the neck), behind the body. Running, the swing frames; standing,
                    // a flicker loop where one is drawn (Cinderheart's flames, 8 fps), else the
                    // rest / glint pair.
                    string view = down ? "down" : up ? "up" : "side";
                    int swing = moving ? (bodyFrames / 2) & 1 : -1;
                    int flicker = moving ? -1 : (int)(Time.time * 8f) & 3;
                    Place(pendant, body, pivot, a.chest, fwd, bob);
                    // Across the body the pendant follows the chest in every frame, read from
                    // the picture's outline at chest height (the run leans the torso forward,
                    // which left it at the back of the neck when it kept its standing spot).
                    Vector2 chestWorld;
                    if (ChestOnFrame(body, bb, down || up, side, out chestWorld))
                        pendant.transform.position = new Vector3(chestWorld.x, chestWorld.y, pivot.z);
                    Sort(pendant, body, up ? back : front);
                    pendant.enabled = !up || HasPendantView(amuletDesigns, "up");
                    pendant.flipX = view == "side" && side < 0f && HasPendantView(amuletDesigns, "side");
                    pendant.sprite = PendantSprite(view, Twinkle(0, amuletTier), swing, flicker);
                }
                if (glint != null)
                {
                    // On the hand in every frame if the picture's outline gives it away: the
                    // hand is the outline's furthest point in the facing direction at hand
                    // height. Otherwise the standing-still anchor.
                    Vector2 handWorld;
                    if (!up && HandOnFrame(body, bb, down ? 1f : side, out handWorld))
                    {
                        glint.transform.SetParent(body.transform, false);
                        glint.transform.position = new Vector3(handWorld.x, handWorld.y, pivot.z);
                        handTracked = true;
                    }
                    else
                    {
                        Place(glint, body, pivot, a.hand, fwd, bob);
                        handTracked = false;
                    }
                    Sort(glint, body, front);
                    glint.enabled = !up; // the hand is behind the body facing up
                    glint.sprite = Ring(ringDesign, ColourOf(ring), ringTier, Twinkle(1, ringTier), ppu);
                }
                if (eyes != null && eyeFrames != null)
                {
                    Vector2 eyesWorld = Vector2.zero;
                    bool shown = !up && EyesOnFrame(body, down, side, out eyesWorld);
                    eyes.enabled = shown;
                    if (shown)
                    {
                        int f = (int)(Time.time * 6f) & 3;   // dim, normal, bright, normal
                        eyes.sprite = down || eyeSide == null ? eyeFrames[f] : eyeSide[f];
                        eyes.transform.position = new Vector3(eyesWorld.x, eyesWorld.y, pivot.z);
                        Sort(eyes, body, front + 1);
                    }
                }
                if (orbs.Count > 0)
                {
                    // The charm's stones circle the wizard at chest height: a flat ellipse (seen
                    // from the game's angle), each stone a share of the way round, drifting up and
                    // down a little as they go, in front of the body on the near half and behind
                    // it on the far half. More stones, and a quicker, wider circle, with rarity.
                    orbitPhase += Time.deltaTime * (1.3f + 0.15f * charmTier);
                    float rx = 0.55f + 0.05f * charmTier, ry = rx * 0.35f;
                    float centreY = pivot.y + a.top * charmOrbitHeight.Value + bob;
                    for (int i = 0; i < orbs.Count; i++)
                    {
                        SpriteRenderer o = orbs[i];
                        if (o == null)
                            continue;
                        float ang = orbitPhase + i * Mathf.PI * 2f / orbs.Count;
                        float undulate = 0.06f * Mathf.Sin(Time.time * 2.4f + i * 1.7f);
                        float px = Mathf.Round((pivot.x + Mathf.Cos(ang) * rx) * ppu) / ppu;
                        float py = Mathf.Round((centreY + Mathf.Sin(ang) * ry + undulate) * ppu) / ppu;
                        if (o.transform.parent != body.transform)
                            o.transform.SetParent(body.transform, false);
                        o.transform.position = new Vector3(px, py, pivot.z);
                        Sort(o, body, Mathf.Sin(ang) < 0f ? front : back);
                        o.sprite = Charmstone(charmDesign, ColourOf(charm), charmTier, Twinkle(2, charmTier), ppu);
                        // Mossguard: the orb swells while its ward is up.
                        if (charm != null && charm.unique == "Moss")
                            o.transform.localScale = Vector3.one * (Uniques.WardUp(player) ? 1.3f : 1f);
                    }
                    // Rare and up leave a trail behind each stone: a Legendary's drawn puffs
                    // (every 0.08 s) when it has them, else a pixel that fades.
                    if (charmTier >= 2 && Time.time >= nextTrail && Time.timeScale > 0f)
                    {
                        nextTrail = Time.time + (charmTrail != null ? 0.08f : 0.09f);
                        foreach (SpriteRenderer o in orbs)
                            if (o != null && o.enabled)
                            {
                                if (charmTrail != null)
                                    Puff.Leave(o, charmTrail);
                                else
                                    Mote.Trail(o, ColourOf(charm), ppu);
                            }
                    }
                }
                lastPos = feet;

                // Uniques shed a single pixel-sized mote every 1.5-2.5 s, other Legendaries every
                // 3-4.5 s, drifting up and fading.
                if (uniqueSparks.Value && Time.time >= nextSpark && Time.timeScale > 0f)
                {
                    var parts = new List<KeyValuePair<SpriteRenderer, ItemData>>();
                    bool anyUnique = false;
                    Shedder(parts, pendant, amulet, ref anyUnique);
                    Shedder(parts, glint, ring, ref anyUnique);
                    foreach (SpriteRenderer o in orbs)
                        Shedder(parts, o, charm, ref anyUnique);
                    nextSpark = Time.time + (anyUnique ? UnityEngine.Random.Range(1.5f, 2.5f) : UnityEngine.Random.Range(3f, 4.5f));
                    if (parts.Count > 0)
                    {
                        var pick = parts[UnityEngine.Random.Range(0, parts.Count)];
                        Mote.Shed(pick.Key, ColourOf(pick.Value), body, ppu);
                    }
                }
            }

            // Where the eyes go in this frame: on the row EyeRowsDown below the top of the drawn
            // picture, its middle facing down, two pixels in from the front edge facing sideways.
            // A body pixel's own centre, so the eye pixels land on the wizard's pixel grid.
            private bool EyesOnFrame(SpriteRenderer body, bool down, float side, out Vector2 world)
            {
                world = Vector2.zero;
                try
                {
                    BodyPixels.Shape sh = BodyPixels.Of(body.sprite);
                    if (sh == null || body.flipY || sh.top - sh.bottom < 16)
                        return false;
                    int y = sh.top - EyeRowsDown, l, r;
                    if (!sh.Row(y, out l, out r) || r - l < 4)
                        return false;
                    int px;
                    if (down)
                        px = l + (r - l) / 2;
                    else
                    {
                        bool rightIsFront = World(body, sh, r, y).x * side >= World(body, sh, l, y).x * side;
                        px = rightIsFront ? r - 2 : l + 2;
                    }
                    world = World(body, sh, px, y);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            // ---- Reading this frame's picture (BodyPixels) ----

            // A pixel of the body's picture in the world (the renderer's flips applied; the
            // transform's own scale is in TransformPoint).
            private static Vector2 World(SpriteRenderer body, BodyPixels.Shape sh, int x, int y)
            {
                Vector2 local = sh.Local(x, y);
                if (body.flipX)
                    local.x = -local.x;
                if (body.flipY)
                    local.y = -local.y;
                return body.transform.TransformPoint(local);
            }

            // The drawn pixels between two fractions of the picture's height (from its lowest to
            // its highest opaque row): the leftmost and rightmost in the world, and the one
            // furthest along `side`. False when the picture can't be read.
            private static bool PixelBand(SpriteRenderer body, float loFrac, float hiFrac, float side,
                out float minX, out float maxX, out Vector2 furthest, out float heightAt70)
            {
                minX = float.PositiveInfinity;
                maxX = float.NegativeInfinity;
                furthest = Vector2.zero;
                heightAt70 = 0f;
                BodyPixels.Shape sh = BodyPixels.Of(body.sprite);
                if (sh == null || body.flipY)
                    return false;
                int rows = sh.top - sh.bottom + 1;
                if (rows < 8)
                    return false;
                int lo = sh.bottom + Mathf.FloorToInt(rows * loFrac), hi = sh.bottom + Mathf.CeilToInt(rows * hiFrac);
                float best = float.NegativeInfinity;
                for (int y = lo; y <= hi; y++)
                {
                    int l, r;
                    if (!sh.Row(y, out l, out r))
                        continue;
                    Vector2 a = World(body, sh, l, y), b = World(body, sh, r, y);
                    minX = Mathf.Min(minX, Mathf.Min(a.x, b.x));
                    maxX = Mathf.Max(maxX, Mathf.Max(a.x, b.x));
                    if (a.x * side > best) { best = a.x * side; furthest = a; }
                    if (b.x * side > best) { best = b.x * side; furthest = b; }
                }
                if (best == float.NegativeInfinity)
                    return false;
                heightAt70 = World(body, sh, 0, sh.bottom + Mathf.RoundToInt(rows * 0.70f)).y;
                return true;
            }

            // The chest's spot across the body in this frame, at chest height (62-78% of the
            // picture): its middle facing down or up, a pixel inside its front edge facing
            // sideways, 70% up the picture. Read from the drawn pixels, else from the sprite's
            // outline vertices. A jump of more than half a unit (the outline caught a sleeve or
            // a spell) is refused for a few frames, as for the hand.
            private Vector2 lastChest;
            private int chestRefused;
            private float lastChestSide = 99f;
            private bool ChestOnFrame(SpriteRenderer body, Bounds bb, bool centred, float side, out Vector2 world)
            {
                world = Vector2.zero;
                try
                {
                    Sprite sprite = body.sprite;
                    if (sprite == null)
                        return false;
                    float px0, px1, pxY;
                    Vector2 unused;
                    if (PixelBand(body, 0.62f, 0.78f, side, out px0, out px1, out unused, out pxY) && px1 - px0 >= 2f / ppu)
                    {
                        float pxX = centred ? (px0 + px1) * 0.5f : (side > 0f ? px1 : px0) - side * 1f / ppu;
                        return ChestTail(body, centred, side, pxX, pxY, out world);
                    }
                    Vector2[] verts = sprite.vertices;
                    if (verts == null || verts.Length < 3)
                        return false;
                    float h = Mathf.Max(0.6f, bb.size.y);
                    float lo = bb.min.y + h * 0.62f, hi = bb.min.y + h * 0.78f;
                    // Whether the outline needs mirroring to match the picture as drawn: the game
                    // may face left by flipX or by a negative scale, so both readings are tried and
                    // the one whose overall width lines up with the renderer's bounds is used (the
                    // 0.12.1 pendant sat at the back of the neck facing left).
                    bool mirror = MirrorFits(verts, body, bb);
                    float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
                    int n = 0;
                    foreach (Vector2 v in verts)
                    {
                        Vector2 local = mirror ? new Vector2(-v.x, v.y) : v;
                        Vector2 w = body.transform.TransformPoint(local);
                        if (w.y < lo || w.y > hi)
                            continue;
                        minX = Mathf.Min(minX, w.x);
                        maxX = Mathf.Max(maxX, w.x);
                        n++;
                    }
                    if (n < 2 || maxX - minX < 2f / ppu)
                        return false;
                    float x = centred ? (minX + maxX) * 0.5f : (side > 0f ? maxX : minX) - side * 1.5f / ppu;
                    // Height from this frame's own picture (70% up it), so a lower run frame
                    // carries the pendant down with it instead of leaving it on the hood.
                    float y = bb.min.y + h * 0.70f;
                    return ChestTail(body, centred, side, x, y, out world);
                }
                catch
                {
                    return false;
                }
            }

            private bool ChestTail(SpriteRenderer body, bool centred, float side, float x, float y, out Vector2 world)
            {
                Vector2 pivot = body.transform.position;
                var offset = new Vector2(Mathf.Round((x - pivot.x) * ppu) / ppu, Mathf.Round((y - pivot.y) * ppu) / ppu);
                float key = centred ? 0f : side;
                if (key == lastChestSide && Mathf.Abs(offset.x - lastChest.x) > 0.5f && chestRefused < 6)
                {
                    chestRefused++;
                    offset = lastChest;
                }
                else
                    chestRefused = 0;
                lastChest = offset;
                lastChestSide = key;
                world = pivot + offset;
                return true;
            }

            // True when the sprite's outline, mirrored, lines up with the renderer's bounds better
            // than as stored (see ChestOnFrame).
            private static bool MirrorFits(Vector2[] verts, SpriteRenderer body, Bounds bb)
            {
                float aMin = float.PositiveInfinity, aMax = float.NegativeInfinity, bMin = float.PositiveInfinity, bMax = float.NegativeInfinity;
                foreach (Vector2 v in verts)
                {
                    float a = body.transform.TransformPoint(v).x;
                    float b = body.transform.TransformPoint(new Vector2(-v.x, v.y)).x;
                    aMin = Mathf.Min(aMin, a); aMax = Mathf.Max(aMax, a);
                    bMin = Mathf.Min(bMin, b); bMax = Mathf.Max(bMax, b);
                }
                float errA = Mathf.Abs(aMin - bb.min.x) + Mathf.Abs(aMax - bb.max.x);
                float errB = Mathf.Abs(bMin - bb.min.x) + Mathf.Abs(bMax - bb.max.x);
                return errB + 0.01f < errA;
            }

            // The hand on this frame of the picture: the drawn pixel furthest along `side` within
            // the hand's height band (33-52% of the picture), a pixel inside it; the cloak's hem
            // is below that band and the shoulders above it. When the picture can't be read, the
            // outline vertex furthest that way, which can lie past the picture (#24: a side run
            // put the ring out in front of the face). A jump is refused for a few frames.
            private bool HandOnFrame(SpriteRenderer body, Bounds bb, float side, out Vector2 world)
            {
                world = Vector2.zero;
                try
                {
                    Sprite sprite = body.sprite;
                    if (sprite == null)
                        return false;
                    float minX, maxX, unusedY;
                    Vector2 edge;
                    if (PixelBand(body, 0.33f, 0.52f, side, out minX, out maxX, out edge, out unusedY))
                    {
                        edge.x -= side * 1f / ppu;
                        return HandTail(body, side, edge, out world);
                    }
                    Vector2[] verts = sprite.vertices;
                    if (verts == null || verts.Length < 3)
                        return false;
                    float h = Mathf.Max(0.6f, bb.size.y);
                    float lo = bb.min.y + h * 0.33f, hi = bb.min.y + h * 0.52f;
                    bool mirror = MirrorFits(verts, body, bb);
                    bool found = false;
                    float best = float.NegativeInfinity;
                    Vector2 pick = Vector2.zero;
                    foreach (Vector2 v in verts)
                    {
                        Vector2 local = mirror ? new Vector2(-v.x, v.y) : v;
                        Vector2 w = body.transform.TransformPoint(local);
                        if (w.y < lo || w.y > hi)
                            continue;
                        float along = w.x * side;
                        if (along > best)
                        {
                            best = along;
                            pick = w;
                            found = true;
                        }
                    }
                    if (!found)
                        return false;
                    // A pixel inside the outline, so the band sits on the hand, not beside it.
                    pick.x -= side * 1.5f / ppu;
                    return HandTail(body, side, pick, out world);
                }
                catch
                {
                    return false;
                }
            }

            private bool HandTail(SpriteRenderer body, float side, Vector2 pick, out Vector2 world)
            {
                // Compared as offsets from the pivot: a world spot went stale as soon as the
                // wizard moved (a leap like Thunderhead's then locked the ring off the body).
                Vector2 pivot = body.transform.position;
                Vector2 offset = new Vector2(Mathf.Round((pick.x - pivot.x) * ppu) / ppu, Mathf.Round((pick.y - pivot.y) * ppu) / ppu);
                // Refuse a jump (the outline caught a sleeve or a spell) for a few frames,
                // then take the new spot: a long run of jumps means the hand really moved.
                if (handTracked && side == lastHandSide && Vector2.Distance(offset, lastHand) > 0.45f && handRefused < 6)
                {
                    handRefused++;
                    offset = lastHand;
                }
                else
                    handRefused = 0;
                lastHand = offset;
                lastHandSide = side;
                world = pivot + offset;
                return true;
            }

            private Anchors Measure(Bounds bb, Vector3 pivot, bool up, bool down, float side, float fwd, bool settled)
            {
                float w = Mathf.Max(0.3f, bb.size.x), h = Mathf.Max(0.6f, bb.size.y);
                // The wizard is about 37 px tall: chest at 70% of the height, hands at 42%, the
                // belt at 54% (the 0.9.2 screenshots had the pendant on the belt buckle).
                Vector2 chest = new Vector2(bb.center.x + (up ? 0f : side * w * 0.06f), bb.min.y + h * 0.70f);
                Vector2 hand = new Vector2(bb.center.x + side * w * 0.42f, bb.min.y + h * 0.42f);
                if (down)
                    hand.x = bb.center.x + w * 0.36f;
                // The charm hangs from the belt: on the trailing side facing left or right, at
                // the left hip facing down, in the middle of the back facing up.
                Vector2 belt = new Vector2(bb.center.x - side * w * 0.3f, bb.min.y + h * 0.54f);
                if (down)
                    belt.x = bb.center.x - w * 0.34f;
                else if (up)
                    belt.x = bb.center.x + w * 0.08f;
                return new Anchors
                {
                    chest = Offset(chest, pivot, fwd),
                    hand = Offset(hand, pivot, fwd),
                    belt = Offset(belt, pivot, fwd),
                    top = bb.max.y - pivot.y,
                    settled = settled
                };
            }

            // A point as whole-pixel offsets from the pivot, x in the facing direction.
            private Vector2 Offset(Vector2 p, Vector3 pivot, float fwd)
            {
                return new Vector2(Mathf.Round((p.x - pivot.x) * fwd * ppu) / ppu, Mathf.Round((p.y - pivot.y) * ppu) / ppu);
            }

            // Hang the part on the body (so it moves with it whatever else moves the body this
            // frame) at the offset from the pivot.
            private static void Place(SpriteRenderer part, SpriteRenderer body, Vector3 pivot, Vector2 offset, float fwd, float bob)
            {
                if (part.transform.parent != body.transform)
                    part.transform.SetParent(body.transform, false);
                part.transform.position = new Vector3(pivot.x + offset.x * fwd, pivot.y + offset.y + bob, pivot.z);
            }

            // 1 while a part is glinting, else 0: every 3-5 s on Magic, 2-3.5 s on Rare,
            // 1.2-2.2 s on Legendary, for 0.15 s (0.2 s on Legendary); never on Common.
            private int Twinkle(int part, int tier)
            {
                if (tier <= 0)
                    return 0;
                float t = Time.time;
                if (t >= nextTwinkle[part])
                {
                    twinkleStart[part] = t;
                    nextTwinkle[part] = t + UnityEngine.Random.Range(TwinkleMin[tier], TwinkleMax[tier]);
                }
                return t - twinkleStart[part] < (tier >= 3 ? 0.2f : 0.15f) ? 1 : 0;
            }

            private static void Shedder(List<KeyValuePair<SpriteRenderer, ItemData>> parts, SpriteRenderer r, ItemData item, ref bool anyUnique)
            {
                if (r == null || !r.enabled || Tier(item) < 3)
                    return;
                parts.Add(new KeyValuePair<SpriteRenderer, ItemData>(r, item));
                if (IsUnique(item))
                    anyUnique = true;
            }

            private static bool IsUnique(ItemData item) => item != null && !string.IsNullOrEmpty(item.unique);

            private static void Sort(SpriteRenderer r, SpriteRenderer body, int order)
            {
                r.sortingLayerID = body.sortingLayerID;
                r.sortingOrder = order;
            }

            private void Show(bool on)
            {
                if (pendant != null) pendant.enabled = on;
                if (glint != null) glint.enabled = on;
                if (aura != null) aura.enabled = on;
                if (eyes != null) eyes.enabled = on;
                foreach (SpriteRenderer o in orbs)
                    if (o != null) o.enabled = on;
            }

            // The parts hang on the body, not on this object, so they go separately.
            private void OnDestroy()
            {
                if (pendant != null) Destroy(pendant.gameObject);
                if (glint != null) Destroy(glint.gameObject);
                if (aura != null) Destroy(aura.gameObject);
                if (eyes != null) Destroy(eyes.gameObject);
                foreach (SpriteRenderer o in orbs)
                    if (o != null) Destroy(o.gameObject);
                orbs.Clear();
            }

            public void Vanish()
            {
                try
                {
                    Destroy(gameObject);
                }
                catch
                {
                }
            }
        }
    }
}
