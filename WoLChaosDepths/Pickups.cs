using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLChaosDepths
{
    // M5, first slice: items are physical drops.
    //
    // A drop lands on the floor where the enemy died as a small gem in its rarity's colour (a
    // star for a unique), bobbing over a soft glow, and shows its name when a wizard is near.
    // Walk over it to pick it up. Whatever is still lying around when the floor is left is
    // collected for its owner, so nothing is lost ([Loot] CollectLeftovers).
    public static class Pickups
    {
        private static ConfigEntry<bool> physical, collectLeftovers;
        private static ConfigEntry<float> pickupRadius, nameRadius;

        private static readonly List<Pickup> live = new List<Pickup>();
        private static readonly Dictionary<string, Sprite> gems = new Dictionary<string, Sprite>();
        private static Sprite glow;
        private static Material material;

        // Called by the plugin when a wizard collects an item (or leftovers are collected).
        public static Action<Player, ItemData, bool> Collected;

        public static bool Physical => physical.Value;

        public static void Bind(ConfigFile config)
        {
            physical = config.Bind("Loot", "PhysicalDrops", true,
                "Items drop on the floor where the enemy died and are picked up by walking over them (off: straight into the inventory).");
            collectLeftovers = config.Bind("Loot", "CollectLeftovers", true,
                "Items still on the floor when you leave it go to their owner's inventory anyway.");
            pickupRadius = config.Bind("Loot", "PickupRadius", 1.3f, "How close a wizard must come to pick an item up, in game units.");
            nameRadius = config.Bind("Loot", "NameRadius", 3.5f, "From how far an item's name shows above it, in game units.");
        }

        // ---- Spawning ----

        public static void Spawn(Player owner, ItemData item, Vector3 at)
        {
            try
            {
                var go = new GameObject($"ChaosDepthsPickup {item.rarity} {item.name}");
                go.transform.position = new Vector3(at.x, at.y, at.z);
                var pickup = go.AddComponent<Pickup>();
                pickup.owner = owner;
                pickup.item = item;
                pickup.Build(owner != null ? owner.spriteRenderer : null);
                live.Add(pickup);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"pickup spawn: {e.Message}");
                Collected?.Invoke(owner, item, true); // never lose the item
            }
        }

        // The floor is being left: hand leftovers to their owners.
        public static void CollectLeftovers()
        {
            int count = 0;
            foreach (Pickup p in new List<Pickup>(live))
            {
                if (p == null)
                    continue;
                if (collectLeftovers.Value && p.item != null)
                {
                    Collected?.Invoke(p.owner, p.item, true);
                    count++;
                }
                p.Vanish();
            }
            live.Clear();
            if (count > 0)
                ChaosDepthsPlugin.Log($"Pickups: {count} item(s) left on the floor collected for their owners");
        }

        public static void Clear()
        {
            foreach (Pickup p in live)
                if (p != null)
                    p.Vanish();
            live.Clear();
        }

        internal static void Remove(Pickup p) => live.Remove(p);

        // ---- Names over nearby items (IMGUI, from the plugin's OnGUI) ----

        public static void Draw()
        {
            if (live.Count == 0)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            int u = Mathf.Max(2, Mathf.FloorToInt(Screen.height / 240f));
            int font = Mathf.Max(10, 3 * u);
            foreach (Pickup p in live)
            {
                if (p == null || p.item == null)
                    continue;
                float nearest = float.MaxValue;
                foreach (Player pl in players)
                    if (pl != null)
                        nearest = Mathf.Min(nearest, Vector2.Distance(pl.transform.position, p.transform.position));
                if (nearest > nameRadius.Value)
                    continue;
                Vector3 screen = cam.WorldToScreenPoint(p.transform.position + new Vector3(0f, 0.9f, 0f));
                if (screen.z < 0f)
                    continue;
                string text = (string.IsNullOrEmpty(p.item.unique) ? "" : "* ") + p.item.name;
                GUIStyle style = PixelUI.Style(font, Colour(p.item), TextAnchor.MiddleCenter);
                var size = style.CalcSize(new GUIContent(text));
                var rect = new Rect(screen.x - size.x / 2f - u, Screen.height - screen.y - size.y / 2f, size.x + 2 * u, size.y);
                PixelUI.Fill(rect, new Color(0f, 0f, 0f, 0.55f));
                GUI.Label(rect, text, style);
            }
        }

        public static Color Colour(ItemData item)
        {
            switch (item != null ? item.rarity : "")
            {
                case "Magic": return new Color32(0x6f, 0xa8, 0xff, 0xff);
                case "Rare": return new Color32(0xff, 0xe0, 0x66, 0xff);
                case "Legendary": return new Color32(0xff, 0x8a, 0x3d, 0xff);
                default: return new Color32(0xd8, 0xd8, 0xd8, 0xff);
            }
        }

        // ---- Sprites, drawn in code ----

        private static Material Mat()
        {
            if (material == null)
                material = new Material(Shader.Find("Sprites/Default"));
            return material;
        }

        // A 9x9 gem (diamond) or, for a unique, a 4-point star, in the rarity colour with a
        // lighter facet and a darker rim.
        // A gem (diamond) or, for a unique, a 4-point star, in the rarity colour with a lighter
        // facet and a darker rim. Legendaries get a white outline so they read against lava.
        private static Sprite Gem(ItemData item, float ppu)
        {
            string key = (string.IsNullOrEmpty(item.unique) ? "gem" : "star") + item.rarity + ppu;
            Sprite sprite;
            if (gems.TryGetValue(key, out sprite))
                return sprite;
            bool outline = item.rarity == "Legendary";
            const int S = 11; // the 9x9 shape centred, with a 1-px border for the outline
            Color c = Colour(item);
            Color light = Color.Lerp(c, Color.white, 0.55f);
            Color dark = Color.Lerp(c, Color.black, 0.45f);
            bool star = !string.IsNullOrEmpty(item.unique);
            var px = new Color32[S * S];
            Func<int, int, bool> inside = (x, y) =>
            {
                int dx = Mathf.Abs(x - 5), dy = Mathf.Abs(y - 5);
                return star ? (dx == 0 || dy == 0 || (dx <= 1 && dy <= 1)) && dx + dy <= 4 : dx + dy <= 4;
            };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int dx = Mathf.Abs(x - 5), dy = Mathf.Abs(y - 5);
                    Color col = Color.clear;
                    if (inside(x, y))
                    {
                        bool rim = star ? (dx + dy == 4 || (dx == 1 && dy == 1)) : dx + dy == 4;
                        col = rim ? dark : (x <= 4 && y >= 6 ? light : c);
                    }
                    else if (outline)
                    {
                        // Touching the shape: the outline.
                        for (int oy = -1; oy <= 1 && col == Color.clear; oy++)
                            for (int ox = -1; ox <= 1; ox++)
                                if ((ox != 0 || oy != 0) && x + ox >= 0 && x + ox < S && y + oy >= 0 && y + oy < S && inside(x + ox, y + oy))
                                {
                                    col = Color.white;
                                    break;
                                }
                    }
                    px[y * S + x] = col;
                }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 1f / S), ppu);
            gems[key] = sprite;
            return sprite;
        }

        // A soft 17x17 round glow (alpha falls off with distance), tinted per pickup.
        private static Sprite Glow(float ppu)
        {
            if (glow != null)
                return glow;
            const int S = 17;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(8f, 8f)) / 8f;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * 0.6f;
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            glow = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), ppu);
            return glow;
        }

        private static float Ppu(SpriteRenderer like)
        {
            try
            {
                if (like != null && like.sprite != null && like.sprite.pixelsPerUnit > 0f)
                    return like.sprite.pixelsPerUnit;
                if (CameraController.pixelsPerUnit > 0)
                    return CameraController.pixelsPerUnit;
            }
            catch
            {
            }
            return 16f;
        }

        // ---- The pickup object ----

        public class Pickup : MonoBehaviour
        {
            public Player owner;
            public ItemData item;
            private SpriteRenderer gem, halo;
            private float born, phase;
            private bool taken;

            public void Build(SpriteRenderer like)
            {
                float ppu = Ppu(like);
                born = Time.time;
                phase = UnityEngine.Random.value * Mathf.PI * 2f;

                var haloGo = new GameObject("glow");
                haloGo.transform.SetParent(transform, false);
                halo = haloGo.AddComponent<SpriteRenderer>();
                halo.sprite = Glow(ppu);
                halo.material = Mat();
                Color c = Colour(item);
                halo.color = new Color(c.r, c.g, c.b, 0.7f);

                var gemGo = new GameObject("gem");
                gemGo.transform.SetParent(transform, false);
                gem = gemGo.AddComponent<SpriteRenderer>();
                gem.sprite = Gem(item, ppu);
                gem.material = Mat();

                if (like != null)
                {
                    halo.sortingLayerID = like.sortingLayerID;
                    halo.sortingOrder = like.sortingOrder - 2;
                    gem.sortingLayerID = like.sortingLayerID;
                    gem.sortingOrder = like.sortingOrder - 1;
                }
                // Legendaries and uniques are a bit bigger.
                float scale = item.rarity == "Legendary" ? 1.35f : item.rarity == "Rare" ? 1.15f : 1f;
                gemGo.transform.localScale = Vector3.one * scale;
                haloGo.transform.localScale = Vector3.one * scale;
            }

            private void Update()
            {
                if (taken || item == null)
                    return;
                float t = Time.time - born;
                // A little hop out of the corpse, then a gentle bob.
                float hop = t < 0.35f ? Mathf.Sin(t / 0.35f * Mathf.PI) * 0.6f : 0f;
                float bob = 0.08f + 0.06f * Mathf.Sin(t * 3f + phase);
                if (gem != null)
                    gem.transform.localPosition = new Vector3(0f, hop + bob, 0f);
                if (halo != null)
                {
                    Color c = halo.color;
                    bool legendary = item.rarity == "Legendary";
                    c.a = legendary ? 0.6f + 0.35f * Mathf.Sin(t * 4f + phase) : 0.45f + 0.25f * Mathf.Sin(t * 2.2f + phase);
                    halo.color = c;
                    if (legendary)
                        halo.transform.localScale = Vector3.one * (1.35f + 0.25f * Mathf.Sin(t * 4f + phase));
                }
                if (t < 0.4f)
                    return; // can't be grabbed mid-hop
                Player[] players = GameController.activePlayers;
                if (players == null)
                    return;
                // The owner first; anyone else can also pick it up for them.
                Player taker = null;
                if (owner != null && owner.gameObject.activeInHierarchy && Vector2.Distance(owner.transform.position, transform.position) <= pickupRadius.Value)
                    taker = owner;
                else
                    foreach (Player p in players)
                        if (p != null && p.gameObject.activeInHierarchy && Vector2.Distance(p.transform.position, transform.position) <= pickupRadius.Value)
                        {
                            taker = p;
                            break;
                        }
                if (taker == null)
                    return;
                taken = true;
                Pickups.Remove(this);
                try
                {
                    Collected?.Invoke(owner ?? taker, item, false);
                }
                catch (Exception e)
                {
                    ChaosDepthsPlugin.LogError($"pickup: {e.Message}");
                }
                Vanish();
            }

            public void Vanish()
            {
                taken = true;
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
