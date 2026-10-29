using System.Collections.Generic;
using UnityEngine;

namespace WoLSlingshotDash
{
    // The signature's marker on each feint (0.26.1, Matt): a ring of ice on the floor under the
    // feint the size of the burst it will go off in, so the chain's path can be read before it
    // plays. It pulses while the feints stand, flares when the chain begins, and bursts outward
    // as you swap onto it. Drawn in code at the game's pixel size (a 1 px rim with a faint
    // fill, in the game's frost tones) until WoL Art draws one.
    public class FeintMarker : MonoBehaviour
    {
        private const float Ppu = 16f;
        private const float PulseRate = 5f;
        private const float BaseAlpha = 0.45f, PulseAlpha = 0.25f, FlareAlpha = 1f, FlareTime = 0.25f;
        private const float PopTime = 0.18f, PopGrow = 1.35f;
        private static readonly Color Rim = new Color32(0xbf, 0xf2, 0xff, 0xff);
        private static readonly Color Fill = new Color32(0x6f, 0xc8, 0xf0, 0x40);
        private static readonly Dictionary<int, Sprite> rings = new Dictionary<int, Sprite>();
        private static Material material;   // one for every marker

        private SpriteRenderer sr;
        private float born, flaredAt = -99f, poppedAt = -1f;

        public static FeintMarker Place(Vector2 at, float radius, SpriteRenderer sortLike)
        {
            try
            {
                var go = new GameObject("FeintSwapMarker");
                go.transform.position = at;
                var m = go.AddComponent<FeintMarker>();
                m.sr = go.AddComponent<SpriteRenderer>();
                m.sr.sprite = Ring(radius);
                m.sr.material = material != null ? material : (material = new Material(Shader.Find("Sprites/Default")));
                m.sr.sortingLayerName = HasLayer("TargetLineGround") ? "TargetLineGround" : sortLike != null ? sortLike.sortingLayerName : "Default";
                m.sr.sortingOrder = 5;
                m.born = Time.time;
                Effects.Shimmer(at, 3);
                return m;
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"Feint Swap: couldn't place a feint marker: {e.Message}");
                return null;
            }
        }

        // The chain begins: every marker flares.
        public void Flare() => flaredAt = Time.time;

        // Swapped onto: the ring bursts outward and fades.
        public void Pop()
        {
            if (poppedAt < 0f)
                poppedAt = Time.time;
        }

        public static void Remove(FeintMarker m)
        {
            if (m != null)
                Destroy(m.gameObject);
        }

        private void Update()
        {
            if (sr == null)
                return;
            float now = Time.time;
            if (poppedAt >= 0f)
            {
                float t = (now - poppedAt) / PopTime;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                transform.localScale = Vector3.one * Mathf.Lerp(1f, PopGrow, t);
                sr.color = new Color(1f, 1f, 1f, 1f - t);
                return;
            }
            float alpha = BaseAlpha + PulseAlpha * (0.5f + 0.5f * Mathf.Sin((now - born) * PulseRate * Mathf.PI));
            float flare = Mathf.Clamp01(1f - (now - flaredAt) / FlareTime);
            sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(alpha, FlareAlpha, flare));
        }

        // A ring of this radius (units) as a sprite: a 1 px rim, a faint fill, and a second rim a
        // pixel in, cached per size.
        private static Sprite Ring(float radius)
        {
            int r = Mathf.Max(4, Mathf.RoundToInt(radius * Ppu));
            Sprite s;
            if (rings.TryGetValue(r, out s) && s != null)
                return s;
            int size = r * 2 + 3;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    Color col = Color.clear;
                    if (Mathf.Abs(d - r) < 0.6f)
                        col = Rim;
                    else if (Mathf.Abs(d - (r - 2)) < 0.5f && ((x + y) & 1) == 0)
                        col = new Color(Rim.r, Rim.g, Rim.b, 0.55f);
                    else if (d < r)
                        col = Fill;
                    px[y * size + x] = col;
                }
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), Ppu);
            rings[r] = s;
            return s;
        }

        private static bool HasLayer(string name)
        {
            foreach (SortingLayer layer in SortingLayer.layers)
                if (layer.name == name)
                    return true;
            return false;
        }
    }

    // A material made for one object (a feint's own icy copy of the wizard's), freed when the
    // object goes (0.26.3: Unity never frees a material by itself, so one per feint piled up
    // over a long run).
    public class OwnedMaterial : MonoBehaviour
    {
        public Material material;

        public static void Attach(GameObject go, Material m)
        {
            if (go != null && m != null)
                go.AddComponent<OwnedMaterial>().material = m;
        }

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }
    }
}
