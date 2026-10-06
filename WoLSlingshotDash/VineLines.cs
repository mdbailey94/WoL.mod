using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Twin pixel-art vines from the wizard to a point: two wobbling line renderers with a small
    // generated vine texture, drawn in the wizard's sorting layer. They shoot out quickly, then
    // sway gently while held.
    public class VineLines : MonoBehaviour
    {
        private const float ShootTime = 0.12f;
        private const int Segments = 10;
        private const float Spread = 0.22f;   // gap between the two vines at your end
        private const float Width = 0.28f;

        private static Material material;

        private LineRenderer[] lines;
        private LineRenderer[] coils;
        private int sortingLayer;
        private int sortingOrder;
        private float shotAt;
        private float snaredAt = -1f;

        public static VineLines Create(Player player)
        {
            try
            {
                var root = new GameObject("SlingshotVines");
                VineLines vines = root.AddComponent<VineLines>();
                SpriteRenderer body = player.GetComponentInChildren<SpriteRenderer>();
                if (body != null)
                {
                    vines.sortingLayer = body.sortingLayerID;
                    vines.sortingOrder = body.sortingOrder + 1;
                }
                vines.lines = new LineRenderer[2];
                for (int i = 0; i < 2; i++)
                    vines.lines[i] = vines.NewLine("Vine" + i, Segments + 1, Width, Width * 0.75f, false);
                return vines;
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"Couldn't draw vines: {e.Message}");
                return null;
            }
        }

        public void Shoot(Vector2 from, Vector2 to)
        {
            shotAt = Time.time;
            Hold(from, to);
        }

        public void Hold(Vector2 from, Vector2 to)
        {
            float reach = Mathf.Clamp01((Time.time - shotAt) / ShootTime);
            Vector2 tip = Vector2.Lerp(from, to, reach);
            Vector2 along = tip - from;
            Vector2 side = new Vector2(-along.y, along.x).normalized;
            for (int v = 0; v < lines.Length; v++)
            {
                float sign = v == 0 ? 1f : -1f;
                for (int i = 0; i <= Segments; i++)
                {
                    float f = (float)i / Segments;
                    // Apart at your end, meeting at the target, with a gentle sway in between.
                    float sway = Mathf.Sin(Time.time * 10f + f * 9f + v * 2f) * 0.07f * Mathf.Sin(f * Mathf.PI);
                    Vector2 point = from + along * f + side * (sign * Spread * (1f - f) + sway);
                    lines[v].SetPosition(i, new Vector3(point.x, point.y, 0f));
                }
            }
        }

        // Vine coils tightening around a grabbed enemy's feet, then gently writhing. Purely a
        // look: it shows on any enemy, including bosses the grip can't hold still.
        public void Ensnare(Vector2 center)
        {
            if (coils == null)
            {
                coils = new LineRenderer[2];
                for (int i = 0; i < 2; i++)
                    coils[i] = NewLine("Coil" + i, CoilPoints + 1, Width * 0.8f, Width * 0.8f, true);
                snaredAt = Time.time;
            }
            float tighten = Mathf.Clamp01((Time.time - snaredAt) / 0.15f);
            for (int c = 0; c < coils.Length; c++)
            {
                // Wide, then snug; the two coils sit at slightly different heights.
                float rx = Mathf.Lerp(1.0f, 0.55f, tighten) + Mathf.Sin(Time.time * 8f + c * 2f) * 0.03f;
                float ry = rx * 0.45f;
                float lift = 0.1f + c * 0.22f;
                for (int i = 0; i <= CoilPoints; i++)
                {
                    float a = (float)i / CoilPoints * Mathf.PI * 2f + c * 0.9f;
                    coils[c].SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * rx, center.y + lift + Mathf.Sin(a) * ry, 0f));
                }
            }
        }

        private const int CoilPoints = 16;

        private LineRenderer NewLine(string name, int points, float startWidth, float endWidth, bool loop)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.material = VineMaterial();
            line.textureMode = LineTextureMode.Tile;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.positionCount = points;
            line.useWorldSpace = true;
            line.sortingLayerID = sortingLayer;
            line.sortingOrder = sortingOrder;
            return line;
        }

        // A 16x4 vine: dark stem, a lighter highlight, and a leaf every few pixels, in the greens
        // of the game's own vines (Rippling Vines' VineTrap/VineWave sprites) when they can be
        // read, otherwise a close match.
        private static Material VineMaterial()
        {
            if (material != null)
                return material;
            Color32 dark = new Color32(36, 74, 30, 255);
            Color32 stem = new Color32(70, 128, 44, 255);
            Color32 light = new Color32(128, 190, 78, 255);
            GamePalette(ref dark, ref stem, ref light);

            var texture = new Texture2D(16, 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[16 * 4];
            for (int x = 0; x < 16; x++)
            {
                pixels[0 * 16 + x] = clear;
                pixels[1 * 16 + x] = x % 5 == 0 ? light : stem;
                pixels[2 * 16 + x] = dark;
                pixels[3 * 16 + x] = clear;
            }
            // Leaves.
            pixels[3 * 16 + 3] = light;
            pixels[3 * 16 + 4] = stem;
            pixels[0 * 16 + 10] = stem;
            pixels[0 * 16 + 11] = dark;
            texture.SetPixels32(pixels);
            texture.Apply();
            material = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture };
            return material;
        }

        // Picks a dark, mid and light green for the vines: from the config if set there, else
        // from the green pixels of the game's own vine art (the vine whips, traps and waves of
        // Soaring Ivy, Rippling Vines and co., including their particle textures).
        private static void GamePalette(ref Color32 dark, ref Color32 stem, ref Color32 light)
        {
            Color32 configured;
            bool fromConfig = false;
            if (TryParse(SlingshotDashPlugin.VineDarkColor, out configured)) { dark = configured; fromConfig = true; }
            if (TryParse(SlingshotDashPlugin.VineMidColor, out configured)) { stem = configured; fromConfig = true; }
            if (TryParse(SlingshotDashPlugin.VineLightColor, out configured)) { light = configured; fromConfig = true; }
            if (fromConfig)
            {
                SlingshotDashPlugin.Log($"Vines: using the colours from the config {Hex(dark)} {Hex(stem)} {Hex(light)}");
                return;
            }

            var greens = new List<Color32>();
            var report = new List<string>();
            var sources = new Dictionary<string, Func<GameObject>>
            {
                { "VineJumpWhip", () => VineJumpWhip.Prefab },
                { "VineWhipObject", () => VineWhipObject.Prefab },
                { "VineTrap", () => VineTrap.Prefab },
                { "VineWave", () => VineWave.Prefab },
                { "VinePull", () => VinePull.Prefab },
                { "VinePullProjectile", () => VinePullProjectile.Prefab },
            };
            foreach (KeyValuePair<string, Func<GameObject>> source in sources)
            {
                GameObject prefab = SafePrefab(source.Value);
                if (prefab == null)
                {
                    report.Add(source.Key + ": no prefab");
                    continue;
                }
                int before = greens.Count, images = 0;
                foreach (SpriteRenderer renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    images++;
                    AddGreens(SpriteReader.Read(renderer.sprite), greens);
                }
                foreach (ParticleSystemRenderer renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    Material m = renderer.sharedMaterial;
                    if (m != null && m.mainTexture != null)
                    {
                        images++;
                        AddGreens(SpriteReader.ReadTexture(m.mainTexture), greens);
                    }
                }
                report.Add($"{source.Key}: {images} image(s), {greens.Count - before} green px");
            }
            SlingshotDashPlugin.Log("Vines: " + string.Join("; ", report.ToArray()));
            if (greens.Count < 12)
            {
                SlingshotDashPlugin.Log("Vines: couldn't read the game's vine greens, using the built-in ones " +
                    "(set them yourself under [Vines] in the config)");
                return;
            }
            greens.Sort((x, y) => (x.r + x.g + x.b).CompareTo(y.r + y.g + y.b));
            dark = greens[greens.Count * 15 / 100];
            stem = greens[greens.Count / 2];
            light = greens[greens.Count * 85 / 100];
            SlingshotDashPlugin.Log($"Vines: using the game's greens {Hex(dark)} {Hex(stem)} {Hex(light)}");
        }

        // Opaque, clearly coloured pixels with a green-ish hue (yellow-green to teal).
        private static void AddGreens(Color32[] pixels, List<Color32> greens)
        {
            if (pixels == null)
                return;
            foreach (Color32 c in pixels)
            {
                if (c.a < 200)
                    continue;
                float h, sat, v;
                Color.RGBToHSV(c, out h, out sat, out v);
                if (h >= 0.18f && h <= 0.48f && sat >= 0.25f && v >= 0.15f)
                    greens.Add(c);
            }
        }

        private static bool TryParse(string hex, out Color32 color)
        {
            color = new Color32();
            if (string.IsNullOrEmpty(hex))
                return false;
            Color parsed;
            if (!ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out parsed))
                return false;
            color = parsed;
            return true;
        }

        private static string Hex(Color32 c) => "#" + c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2");

        private static GameObject SafePrefab(Func<GameObject> get)
        {
            try
            {
                return get();
            }
            catch
            {
                return null;
            }
        }
    }
}
