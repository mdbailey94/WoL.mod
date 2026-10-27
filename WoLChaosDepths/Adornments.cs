using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLChaosDepths
{
    // M5, second slice: equipped items show on the wizard, drawn in the game's pixel style.
    //   Amulet  a pendant on a short chain at the chest
    //   Ring    a glint on the leading hand that twinkles now and then
    //   Charm   a small tassel hanging from the belt on the trailing side
    // Each is in its item's rarity colour (a unique in its power's colour, with the game's own
    // hit sparks drifting off it). They follow the wizard's facing: in front of the body facing
    // down, left or right, and hidden behind it facing up.
    public static class Adornments
    {
        private static ConfigEntry<bool> enabled, uniqueSparks;
        private static readonly Dictionary<int, Adorn> adorns = new Dictionary<int, Adorn>();
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private static Material material;

        public static void Bind(ConfigFile config)
        {
            enabled = config.Bind("Look", "ShowEquipped", true, "Equipped items show on the wizard: a pendant, a ring glint and a belt charm.");
            uniqueSparks = config.Bind("Look", "UniqueSparks", true, "Unique legendaries worn give off small sparks.");
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
        }

        // ---- Colours ----

        private static Color ColourOf(ItemData item)
        {
            if (item == null)
                return Color.clear;
            switch (item.unique)
            {
                case "Chain": return new Color32(0x7f, 0xdc, 0xff, 0xff);  // storm cyan
                case "Pyre": return new Color32(0xff, 0x7a, 0x2a, 0xff);   // ember orange
                case "Leech": return new Color32(0xe0, 0x30, 0x48, 0xff);  // blood red
            }
            return Pickups.Colour(item);
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
        private static Sprite Ring(Color c, int tier, int frame, float ppu)
        {
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

        // A belt charm: a 1-px cord, a body 3x2 (Common), 3x3 (Magic), 4x3 (Rare) or 5x4
        // (Legendary) with a dark bottom row and a lit corner, and a fringe 1 or 2 px long.
        // Pivot top.
        private static Sprite Tassel(Color c, int tier, int frame, float ppu)
        {
            Color cord = new Color32(0x6a, 0x4e, 0x32, 0xff);
            Color dark = Color.Lerp(c, Color.black, 0.4f), light = Color.Lerp(c, Color.white, 0.4f);
            int w = tier <= 1 ? 3 : tier == 2 ? 4 : 5;
            int bh = tier == 0 ? 2 : tier == 3 ? 4 : 3;
            int tail = tier >= 2 ? 2 : 1;
            int h = 1 + bh + tail;
            return Make("tassel" + tier + frame + ColorUtility.ToHtmlStringRGB(c), w, h, (x, y) =>
            {
                if (y == h - 1)
                    return x == w / 2 ? cord : Color.clear;
                if (y < tail)                                     // the fringe
                    return (w == 3 ? x == 1 : w == 4 ? x == 1 || x == 2 : x == 1 || x == 3) ? dark : Color.clear;
                if (y == tail)
                    return dark;
                if (x == 0 && y == h - 2)                         // the lit corner
                    return frame == 1 ? Color.white : light;
                if (frame == 1 && tier >= 3 && x == 1 && y == h - 3)
                    return Color.white;
                return c;
            }, new Vector2(0.5f, 1f), ppu);
        }

        // A 1-px mote that drifts up a few pixels and fades over half a second.
        private class Mote : MonoBehaviour
        {
            private SpriteRenderer r;
            private float born;
            private Vector3 start, drift;

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
                float t = (Time.time - born) / 0.5f;
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

        // ---- Per-player component ----

        private class Adorn : MonoBehaviour
        {
            public Player player;
            private string signature = "";
            private ItemData amulet, ring, charm;
            private int amuletTier, ringTier, charmTier;
            private SpriteRenderer pendant, glint, tassel;
            private float ppu = 16f, nextSpark, swing, stillFor;
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
                    pendant = Part(pendant, amulet, amulet != null ? Pendant(ColourOf(amulet), amuletTier, 0, ppu) : null);
                    glint = Part(glint, ring, ring != null ? Ring(ColourOf(ring), ringTier, 0, ppu) : null);
                    tassel = Part(tassel, charm, charm != null ? Tassel(ColourOf(charm), charmTier, 0, ppu) : null);
                    // Stagger the first twinkles so the three parts don't flash together.
                    for (int i = 0; i < 3; i++)
                        nextTwinkle[i] = Time.time + UnityEngine.Random.Range(0.5f, 2f);
                }
                catch (Exception e)
                {
                    ChaosDepthsPlugin.LogError($"adornments: {e.Message}");
                }
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
                // A part goes with the body it hangs on; if that body was replaced, make it again.
                if ((amulet != null && pendant == null) || (ring != null && glint == null) || (charm != null && tassel == null))
                    Rebuild();
                Show(true);
                Vector3 feet = player.transform.position;
                FacingDirection facing = player.facingDirection;
                bool up = facing == FacingDirection.Up, down = facing == FacingDirection.Down;
                bool right = facing == FacingDirection.Right || (facing != FacingDirection.Left && player.isFacingRight);
                float side = right ? 1f : -1f;
                int front = body.sortingOrder + 1, back = body.sortingOrder - 1;
                int key = down ? 0 : up ? 1 : 2;
                float fwd = key == 2 ? side : 1f;

                // Measure where the gear goes while the wizard stands still (same spot, same
                // picture size for 0.3 s), then keep those offsets from the pivot through the
                // run, dash and cast frames. A run frame only gives a guess until then.
                Vector3 pivot = body.transform.position;
                Bounds bb = body.bounds;
                bool still = (feet - lastPos).sqrMagnitude < 1e-8f
                    && Mathf.Abs(bb.size.x - lastSize.x) < 0.5f / ppu && Mathf.Abs(bb.size.y - lastSize.y) < 0.5f / ppu;
                stillFor = still ? stillFor + Time.deltaTime : 0f;
                lastSize = bb.size;
                Anchors a = anchors[key];
                if (a == null || (!a.settled && stillFor >= 0.3f))
                    anchors[key] = a = Measure(bb, pivot, up, down, side, fwd, stillFor >= 0.3f);

                // Follow the run's bob (the top of the picture), at most 2 px either way.
                float bob = Mathf.Clamp(Mathf.Round((bb.max.y - pivot.y - a.top) * ppu), -2f, 2f) / ppu;

                if (pendant != null)
                {
                    Place(pendant, body, pivot, a.chest, fwd, bob);
                    Sort(pendant, body, front);
                    pendant.enabled = !up;
                    pendant.sprite = Pendant(ColourOf(amulet), amuletTier, Twinkle(0, amuletTier), ppu);
                }
                if (glint != null)
                {
                    Place(glint, body, pivot, a.hand, fwd, bob);
                    Sort(glint, body, front);
                    glint.enabled = !up; // the hand is behind the body facing up
                    glint.sprite = Ring(ColourOf(ring), ringTier, Twinkle(1, ringTier), ppu);
                }
                if (tassel != null)
                {
                    // Trails the movement a little, so it swings (whole pixels only).
                    Vector3 moved = feet - lastPos;
                    swing = Mathf.Lerp(swing, Mathf.Clamp(-moved.x * 4f, -0.05f, 0.05f), 0.2f);
                    Place(tassel, body, pivot, a.belt, fwd, bob);
                    tassel.transform.position += new Vector3(Mathf.Round(swing * ppu) / ppu, 0f, 0f);
                    // In front at the hip (down) and on the back (up); behind the body on the
                    // trailing side otherwise.
                    Sort(tassel, body, up || down ? front : back);
                    tassel.sprite = Tassel(ColourOf(charm), charmTier, Twinkle(2, charmTier), ppu);
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
                    Shedder(parts, tassel, charm, ref anyUnique);
                    nextSpark = Time.time + (anyUnique ? UnityEngine.Random.Range(1.5f, 2.5f) : UnityEngine.Random.Range(3f, 4.5f));
                    if (parts.Count > 0)
                    {
                        var pick = parts[UnityEngine.Random.Range(0, parts.Count)];
                        Mote.Shed(pick.Key, ColourOf(pick.Value), body, ppu);
                    }
                }
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
                if (tassel != null) tassel.enabled = on;
            }

            // The parts hang on the body, not on this object, so they go separately.
            private void OnDestroy()
            {
                if (pendant != null) Destroy(pendant.gameObject);
                if (glint != null) Destroy(glint.gameObject);
                if (tassel != null) Destroy(tassel.gameObject);
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
