using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Recolours each arcana's icon in the palette of the game's own icons for similar spells
    // (same element; for Feint Swap the frost arcana, for Vine Slingshot the vine arcana), so they
    // sit with the rest. The game's colours are read from its icons, sorted dark to light and cut
    // into a few shades; our icon's element colours are mapped onto those shades by brightness, so
    // outlines stay darkest and highlights lightest. Colours marked as neutral in our PNGs (alpha
    // 254: the boot, the wooden fork) are left alone. The reference icons are also saved to
    // BepInEx/config/SlingshotDash_IconRefs so they can be checked.
    public static class IconPalette
    {
        private const int Shades = 6;
        private const string RefFolder = "SlingshotDash_IconRefs";

        private struct Target
        {
            public string id;
            public string element;
            public string[] nameHints; // preferred reference skills, by part of their ID
        }

        private static readonly Target[] Targets =
        {
            new Target { id = SlingshotDashState.staticID, element = "Air", nameHints = new string[0] },
            new Target { id = BlazingSlingshotState.staticID, element = "Fire", nameHints = new string[0] },
            new Target { id = FrostSlingshotState.staticID, element = "Water",
                nameHints = new[] { "Ice", "Frost", "Freeze", "Snow", "Cold", "Blizzard", "Shard" } },
            new Target { id = VineSlingshotState.staticID, element = "Earth",
                nameHints = new[] { "Vine", "Ivy", "Leaf", "Thorn", "Root" } },
            new Target { id = StormSlingshotState.staticID, element = "Lightning", nameHints = new string[0] },
        };

        private static readonly Dictionary<string, Sprite> recoloured = new Dictionary<string, Sprite>();
        private static bool done;
        private static bool exported;

        // Called about once a second by the plugin; nothing in the game is hooked. Waits until the
        // game has loaded its icons and skill stats, recolours once, and from then on just puts our
        // recoloured icons back whenever LegendAPI restores the originals.
        public static void Tick()
        {
            try
            {
                if (!SlingshotDashPlugin.MatchIconPalette)
                    return;
                // The field, not the property: the property would make the game load its icons early.
                Dictionary<string, Sprite> icons = IconManager.skillIcons;
                if (icons == null)
                    return;
                if (!done)
                {
                    if (!Targets.All(t => icons.ContainsKey(t.id)) || StatManager.globalSkillData == null
                        || StatManager.globalSkillData.Count < 10)
                        return;
                    done = true;
                    Recolour(icons);
                }
                foreach (KeyValuePair<string, Sprite> entry in recoloured)
                {
                    Sprite current;
                    if (!icons.TryGetValue(entry.Key, out current) || current != entry.Value)
                        icons[entry.Key] = entry.Value;
                }
            }
            catch (Exception e)
            {
                done = true;
                recoloured.Clear();
                SlingshotDashPlugin.Log($"Icons: couldn't match the game's colours, keeping ours: {e.Message}");
            }
        }

        private static void Recolour(Dictionary<string, Sprite> icons)
        {
            foreach (Target target in Targets)
            {
                Sprite ours;
                if (!icons.TryGetValue(target.id, out ours) || ours == null)
                    continue;
                List<string> refs = References(icons, target);
                Color32[] ramp = Ramp(icons, refs);
                if (ramp == null)
                {
                    SlingshotDashPlugin.Log($"Icons: no game icons found for {target.id}, keeping ours");
                    continue;
                }
                Sprite sprite = Recolour(ours, ramp);
                if (sprite != null)
                    recoloured[target.id] = sprite;
                SlingshotDashPlugin.Log($"Icons: {target.id} recoloured from {string.Join(", ", refs.ToArray())}");
                Export(icons, refs, target.id, sprite);
            }
        }

        // The game's own skills of the same element, preferring those whose IDs match the hints.
        private static List<string> References(Dictionary<string, Sprite> icons, Target target)
        {
            var sameElement = new List<string>();
            foreach (string id in icons.Keys.ToList())
            {
                if (id == IconManager.unavailableSkillName || SlingshotHud.IsSlingshot(id))
                    continue;
                if (Element(id) == target.element)
                    sameElement.Add(id);
            }
            var hinted = sameElement.Where(id => target.nameHints.Any(h => id.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            if (hinted.Count == 0)
            {
                // A hint may also name skills of another element (vines are sometimes Earth, sometimes not).
                hinted = icons.Keys.Where(id => !SlingshotHud.IsSlingshot(id) && id != IconManager.unavailableSkillName
                    && target.nameHints.Any(h => id.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            }
            return hinted.Count > 0 ? hinted : sameElement;
        }

        private static string Element(string id)
        {
            try
            {
                StatData data;
                if (StatManager.globalSkillData == null || !StatManager.globalSkillData.TryGetValue(id, out data) || data == null)
                    return null;
                string element = null;
                try
                {
                    element = data.GetValue<string>(StatData.eleStr, 1);
                }
                catch
                {
                }
                return string.IsNullOrEmpty(element) ? data.GetValue<string>(StatData.eleStr, 0) : element;
            }
            catch
            {
                return null;
            }
        }

        // The reference icons' colours, dark to light, cut into Shades steps (the median of each).
        private static Color32[] Ramp(Dictionary<string, Sprite> icons, List<string> refs)
        {
            var pixels = new List<Color32>();
            foreach (string id in refs)
            {
                Sprite sprite = icons[id];
                Color32[] read = SpriteReader.Read(sprite);
                if (read == null)
                    continue;
                // If the icon has a solid backdrop, its corner colours are that; leave them out.
                int w = Mathf.RoundToInt(sprite.textureRect.width), h = Mathf.RoundToInt(sprite.textureRect.height);
                var backdrop = new HashSet<int>();
                foreach (int i in new[] { 0, w - 1, (h - 1) * w, h * w - 1 })
                {
                    if (i >= 0 && i < read.Length && read[i].a > 200)
                        backdrop.Add(Key(read[i]));
                }
                pixels.AddRange(read.Where(c => c.a > 200 && !backdrop.Contains(Key(c))));
            }
            if (pixels.Count < Shades * 4)
                return null;
            pixels.Sort((a, b) => Luma(a).CompareTo(Luma(b)));
            var ramp = new Color32[Shades];
            for (int i = 0; i < Shades; i++)
            {
                int from = pixels.Count * i / Shades, to = pixels.Count * (i + 1) / Shades;
                ramp[i] = pixels[(from + to) / 2];
            }
            return ramp;
        }

        private static Sprite Recolour(Sprite ours, Color32[] ramp)
        {
            Color32[] pixels = SpriteReader.Read(ours);
            if (pixels == null)
                return null;
            Rect rect = ours.textureRect;
            int w = Mathf.RoundToInt(rect.width), h = Mathf.RoundToInt(rect.height);
            // Our element colours (not neutral, not clear), each placed by its brightness among
            // our pixels: weighted by how many pixels use it.
            var element = pixels.Where(IsElement).ToList();
            if (element.Count == 0)
                return null;
            element.Sort((a, b) => Luma(a).CompareTo(Luma(b)));
            var shadeOf = new Dictionary<int, int>();
            for (int i = 0; i < element.Count; i++)
            {
                int key = Key(element[i]);
                if (!shadeOf.ContainsKey(key))
                    shadeOf[key] = Mathf.Min(Shades - 1, (i + element.Count(c => Key(c) == key) / 2) * Shades / element.Count);
            }
            var output = new Color32[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                if (IsElement(c))
                {
                    Color32 r = ramp[shadeOf[Key(c)]];
                    output[i] = new Color32(r.r, r.g, r.b, 255);
                }
                else
                    output[i] = new Color32(c.r, c.g, c.b, IsNeutral(c) ? (byte)255 : c.a);
            }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texture.SetPixels32(output);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ours.pixelsPerUnit);
        }

        // Saves the reference icons and our recoloured one, once, for checking by eye.
        private static void Export(Dictionary<string, Sprite> icons, List<string> refs, string ourID, Sprite ours)
        {
            try
            {
                string folder = Path.Combine(BepInEx.Paths.ConfigPath, RefFolder);
                Directory.CreateDirectory(folder);
                foreach (string id in refs)
                    Save(icons[id], Path.Combine(folder, id + ".png"));
                if (ours != null)
                    Save(ours, Path.Combine(folder, "_" + ourID + ".png"));
                if (!exported)
                {
                    exported = true;
                    SlingshotDashPlugin.Log($"Icons: saved the game's reference icons to {folder}");
                }
            }
            catch (Exception e)
            {
                SlingshotDashPlugin.Log($"Icons: couldn't save the reference icons: {e.Message}");
            }
        }

        private static void Save(Sprite sprite, string path)
        {
            Color32[] pixels = SpriteReader.Read(sprite);
            if (pixels == null)
                return;
            Rect rect = sprite.textureRect;
            var texture = new Texture2D(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
        }

        // Our PNGs mark neutral colours with alpha 254 (allowing a little for the read-back).
        private static bool IsNeutral(Color32 c) => c.a >= 248 && c.a < 255;
        private static bool IsElement(Color32 c) => c.a == 255;

        private static float Luma(Color32 c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        private static int Key(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;
    }
}
