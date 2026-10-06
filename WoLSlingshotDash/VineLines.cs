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

        // A 16x4 vine: dark stem, a lighter highlight, and a leaf every few pixels.
        private static Material VineMaterial()
        {
            if (material != null)
                return material;
            var texture = new Texture2D(16, 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            var clear = new Color32(0, 0, 0, 0);
            var dark = new Color32(36, 74, 30, 255);
            var stem = new Color32(70, 128, 44, 255);
            var light = new Color32(128, 190, 78, 255);
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
    }
}
