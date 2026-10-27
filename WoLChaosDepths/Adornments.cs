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

        // A pendant: a 1-px chain 3 px long, then a 5x5 diamond gem with a facet and a dark rim.
        // 5 wide, 8 tall, pivot top.
        private static Sprite Pendant(Color c, float ppu)
        {
            Color chain = new Color32(0xc8, 0xb0, 0x60, 0xff);
            Color light = Color.Lerp(c, Color.white, 0.6f), dark = Color.Lerp(c, Color.black, 0.45f);
            return Make("pendant" + ColorUtility.ToHtmlStringRGB(c), 5, 8, (x, y) =>
            {
                if (y >= 5)
                    return x == 2 ? chain : Color.clear;           // the chain, top three rows
                int dx = Mathf.Abs(x - 2), dy = Mathf.Abs(y - 2);  // the gem, bottom five rows
                if (dx + dy > 2)
                    return Color.clear;
                if (dx + dy == 2)
                    return dark;                                   // the rim
                return x == 1 && y == 3 ? light : c;               // one facet catching the light
            }, new Vector2(0.5f, 1f), ppu);
        }

        // A ring: a 3x2 band on the hand (dark lower edge); when it glints, a single pixel goes
        // bright. Never bigger than the band.
        private static Sprite Glint(Color c, int frame, float ppu)
        {
            Color light = Color.Lerp(c, Color.white, 0.8f), dark = Color.Lerp(c, Color.black, 0.35f);
            return Make("ring" + frame + ColorUtility.ToHtmlStringRGB(c), 3, 2, (x, y) =>
            {
                if (frame == 1 && x == 1 && y == 1)
                    return light;
                return y == 0 ? dark : c;
            }, new Vector2(0.5f, 0.5f), ppu);
        }

        // A belt charm: a 1-px cord, a 3x3 body with a dark bottom row and a light corner, a
        // 1-px tassel. 3 wide, 6 tall, pivot top.
        private static Sprite Tassel(Color c, float ppu)
        {
            Color cord = new Color32(0x6a, 0x4e, 0x32, 0xff);
            Color dark = Color.Lerp(c, Color.black, 0.4f), light = Color.Lerp(c, Color.white, 0.4f);
            return Make("tassel" + ColorUtility.ToHtmlStringRGB(c), 3, 6, (x, y) =>
            {
                if (y == 5)
                    return x == 1 ? cord : Color.clear;
                if (y == 0)
                    return x == 1 ? dark : Color.clear;
                if (y == 1)
                    return dark;
                return y == 3 && x == 0 ? light : c;
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
            private SpriteRenderer pendant, glint, tassel;
            private float ppu = 16f, nextTwinkle, twinkleStart = -10f, nextSpark, swing;
            private Vector3 lastPos;

            private static readonly Vector2 Chest = new Vector2(0f, 0.62f);
            private static readonly Vector2 Hand = new Vector2(0.3f, 0.4f);
            private static readonly Vector2 Belt = new Vector2(0.22f, 0.38f);

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
                    pendant = Part(pendant, amulet, amulet != null ? Pendant(ColourOf(amulet), ppu) : null);
                    glint = Part(glint, ring, ring != null ? Glint(ColourOf(ring), 0, ppu) : null);
                    tassel = Part(tassel, charm, charm != null ? Tassel(ColourOf(charm), ppu) : null);
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
                Show(true);
                Vector3 feet = player.transform.position;
                FacingDirection facing = player.facingDirection;
                bool up = facing == FacingDirection.Up;
                bool right = facing == FacingDirection.Right || (facing != FacingDirection.Left && player.isFacingRight);
                float side = right ? 1f : -1f;
                int front = body.sortingOrder + 1, back = body.sortingOrder - 1;

                // Places on the wizard's picture as drawn this frame (its bounds move with the
                // animation: running, dashing, casting), so the gear moves with the model.
                Bounds bb = body.bounds;
                float w = Mathf.Max(0.3f, bb.size.x), h = Mathf.Max(0.6f, bb.size.y);
                Vector2 chest = new Vector2(bb.center.x + (up ? 0f : side * w * 0.06f), bb.min.y + h * 0.56f);
                Vector2 hand = new Vector2(bb.center.x + side * w * 0.42f, bb.min.y + h * 0.36f);
                if (facing == FacingDirection.Down)
                    hand.x = bb.center.x + w * 0.36f;
                Vector2 belt = new Vector2(bb.center.x - side * w * 0.3f, bb.min.y + h * 0.4f);
                if (up || facing == FacingDirection.Down)
                    belt.x = bb.center.x + (facing == FacingDirection.Down ? -1f : 1f) * w * 0.26f;

                if (pendant != null)
                {
                    pendant.transform.position = new Vector3(chest.x, chest.y, feet.z);
                    Sort(pendant, body, up ? back : front);
                    pendant.enabled = !up;
                }
                if (glint != null)
                {
                    glint.transform.position = new Vector3(hand.x, hand.y, feet.z);
                    Sort(glint, body, up ? back : front);
                    // A glint every 2-4 s: one pixel bright for 0.15 s.
                    float t = Time.time;
                    if (t >= nextTwinkle)
                    {
                        twinkleStart = t;
                        nextTwinkle = t + UnityEngine.Random.Range(2f, 4f);
                    }
                    int frame = t - twinkleStart < 0.15f ? 1 : 0;
                    glint.sprite = Glint(ColourOf(ring), frame, ppu);
                }
                if (tassel != null)
                {
                    // Trails the movement a little, so it swings.
                    Vector3 moved = feet - lastPos;
                    swing = Mathf.Lerp(swing, Mathf.Clamp(-moved.x * 4f, -0.05f, 0.05f), 0.2f);
                    tassel.transform.position = new Vector3(belt.x + swing, belt.y, feet.z);
                    Sort(tassel, body, up ? front : back); // on the far side of the body
                }
                lastPos = feet;

                // A worn unique sheds a single pixel-sized mote now and then, drifting up and fading.
                if (uniqueSparks.Value && Time.time >= nextSpark && Time.timeScale > 0f)
                {
                    nextSpark = Time.time + UnityEngine.Random.Range(1.5f, 2.5f);
                    var parts = new List<KeyValuePair<SpriteRenderer, ItemData>>();
                    if (IsUnique(amulet) && pendant != null && pendant.enabled) parts.Add(new KeyValuePair<SpriteRenderer, ItemData>(pendant, amulet));
                    if (IsUnique(ring) && glint != null && glint.enabled) parts.Add(new KeyValuePair<SpriteRenderer, ItemData>(glint, ring));
                    if (IsUnique(charm) && tassel != null && tassel.enabled) parts.Add(new KeyValuePair<SpriteRenderer, ItemData>(tassel, charm));
                    if (parts.Count > 0)
                    {
                        var pick = parts[UnityEngine.Random.Range(0, parts.Count)];
                        Mote.Shed(pick.Key, ColourOf(pick.Value), body, ppu);
                    }
                }
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
