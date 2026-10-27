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

        // A pendant: a 1-px chain 3 px long, then a 3x3 diamond gem. 3 wide, 6 tall, pivot top.
        private static Sprite Pendant(Color c, float ppu)
        {
            Color chain = new Color32(0xc8, 0xb0, 0x60, 0xff);
            Color light = Color.Lerp(c, Color.white, 0.6f), dark = Color.Lerp(c, Color.black, 0.45f);
            return Make("pendant" + ColorUtility.ToHtmlStringRGB(c), 3, 6, (x, y) =>
            {
                if (y >= 3)
                    return x == 1 ? chain : Color.clear;           // the chain, top three rows
                int dx = Mathf.Abs(x - 1), dy = Mathf.Abs(y - 1);  // the gem, bottom three rows
                if (dx + dy > 1)
                    return Color.clear;
                return dx + dy == 0 ? light : (y == 0 ? dark : c);
            }, new Vector2(0.5f, 1f), ppu);
        }

        // A ring glint: frame 0 a dot, 1 a small plus, 2 a 5-px cross with a bright heart. 5x5.
        private static Sprite Glint(Color c, int frame, float ppu)
        {
            Color light = Color.Lerp(c, Color.white, 0.7f);
            return Make("glint" + frame + ColorUtility.ToHtmlStringRGB(c), 5, 5, (x, y) =>
            {
                int dx = Mathf.Abs(x - 2), dy = Mathf.Abs(y - 2);
                if (dx + dy == 0)
                    return frame == 0 ? c : light;
                if (frame >= 1 && dx + dy == 1)
                    return frame == 1 ? light : c;
                if (frame == 2 && dx + dy == 2 && (dx == 0 || dy == 0))
                    return new Color(light.r, light.g, light.b, 0.7f);
                return Color.clear;
            }, new Vector2(0.5f, 0.5f), ppu);
        }

        // A belt charm: a 1-px cord, a 3x3 body with a darker bottom row, a 1-px tassel. 3x6, pivot top.
        private static Sprite Tassel(Color c, float ppu)
        {
            Color cord = new Color32(0x6a, 0x4e, 0x32, 0xff);
            Color dark = Color.Lerp(c, Color.black, 0.4f), light = Color.Lerp(c, Color.white, 0.4f);
            return Make("tassel" + ColorUtility.ToHtmlStringRGB(c), 3, 6, (x, y) =>
            {
                if (y == 5)
                    return x == 1 ? cord : Color.clear;
                if (y >= 2)
                    return y == 4 && x == 0 ? light : (y == 2 ? dark : c);
                if (y == 1)
                    return x == 1 ? dark : Color.clear;
                return x == 1 ? cord : Color.clear;
            }, new Vector2(0.5f, 1f), ppu);
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

                // The wizard's own picture bobs as they run; follow its vertical offset.
                float bodyLift = body.transform.position.y - feet.y;

                if (pendant != null)
                {
                    Vector2 at = Chest + new Vector2(up ? 0f : side * 0.05f, bodyLift);
                    pendant.transform.position = feet + (Vector3)at;
                    Sort(pendant, body, up ? back : front);
                    pendant.enabled = !up;
                }
                if (glint != null)
                {
                    Vector2 at = new Vector2(Hand.x * side, Hand.y + bodyLift);
                    if (facing == FacingDirection.Down)
                        at.x = Hand.x * 0.85f;
                    glint.transform.position = feet + (Vector3)at;
                    Sort(glint, body, up ? back : front);
                    // A twinkle every 1.5-3 s: dot, plus, cross, plus, dot over 0.3 s.
                    float t = Time.time;
                    if (t >= nextTwinkle)
                    {
                        twinkleStart = t;
                        nextTwinkle = t + UnityEngine.Random.Range(1.5f, 3f);
                    }
                    float age = t - twinkleStart;
                    int frame = age < 0.06f ? 1 : age < 0.18f ? 2 : age < 0.24f ? 1 : 0;
                    glint.sprite = Glint(ColourOf(ring), frame, ppu);
                }
                if (tassel != null)
                {
                    // Trails the movement a little, so it swings.
                    Vector3 moved = feet - lastPos;
                    swing = Mathf.Lerp(swing, Mathf.Clamp(-moved.x * 6f, -0.08f, 0.08f), 0.2f);
                    Vector2 at = new Vector2(-Belt.x * side + swing, Belt.y + bodyLift);
                    if (up || facing == FacingDirection.Down)
                        at.x = (facing == FacingDirection.Down ? -1f : 1f) * Belt.x * 0.8f + swing;
                    tassel.transform.position = feet + (Vector3)at;
                    Sort(tassel, body, up ? front : back); // on the far side of the body
                }
                lastPos = feet;

                // Sparks off worn uniques, in the game's own style.
                if (uniqueSparks.Value && Time.time >= nextSpark && Time.timeScale > 0f)
                {
                    nextSpark = Time.time + 0.8f;
                    SpriteRenderer part = IsUnique(amulet) ? pendant : IsUnique(ring) ? glint : IsUnique(charm) ? tassel : null;
                    if (IsUnique(amulet) && IsUnique(ring) && UnityEngine.Random.value < 0.5f)
                        part = glint;
                    if (part != null && part.enabled)
                    {
                        try
                        {
                            Vector2 spot = (Vector2)part.transform.position + UnityEngine.Random.insideUnitCircle * 0.08f;
                            PoolManager.GetPoolItem<HitSparkEmitter>().EmitSingle(HitSparkType.Small, spot, spot, null, null, 0f);
                        }
                        catch
                        {
                            nextSpark = Time.time + 30f; // the game's spark pool isn't available here
                        }
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
