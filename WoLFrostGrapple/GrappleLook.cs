using System.Collections.Generic;
using UnityEngine;

namespace WoLFrostGrapple
{
    // The drawn pick, its chain and the landing burst. WoL Art's sheets (Log - Modder 2026-11-04
    // 11:45 spec) are used when they're in Assets; until then simple code-drawn stand-ins:
    //   ice_pickaxe_spin      8 x 16x16, 24 fps loop while flying (the spin is in the frames);
    //   ice_pickaxe_stuck     2 x 16x16, 6 fps glint, on the enemy (or the wall);
    //   ice_link              2 x 4x4, 8 fps shimmer, one every 0.25 u along the chain, never rotated;
    //   frost_grapple_impact  6 x 40x40 (ring r 19 px = 1.2 u), 16 fps once; _enh 6 x 50x50 (r 25 px).
    // The game's ice burst plays its own shards at every hit as well.
    internal class GrappleLook : MonoBehaviour
    {
        private const float LinkSpacing = 0.25f;
        private static Sprite[] spin, stuck, link, impact, impactEnh;
        private static Sprite fallbackPick, fallbackLink;
        private static bool loaded;

        private Player owner;
        private SpriteRenderer pickRenderer;
        private readonly List<SpriteRenderer> links = new List<SpriteRenderer>();
        private bool isStuck;
        private float born;

        public static GrappleLook Make(Player owner)
        {
            Load();
            var go = new GameObject("FrostGrapple Look");
            GrappleLook l = go.AddComponent<GrappleLook>();
            l.owner = owner;
            l.born = Time.time;
            l.pickRenderer = l.Part("Pick", 2);
            return l;
        }

        private static void Load()
        {
            if (loaded)
                return;
            loaded = true;
            spin = Frames("ice_pickaxe_spin.png", 16);
            stuck = Frames("ice_pickaxe_stuck.png", 16);
            link = Frames("ice_link.png", 4);
            impact = Frames("frost_grapple_impact.png", 40);
            impactEnh = Frames("frost_grapple_impact_enh.png", 50);
            fallbackPick = Diamond();
            fallbackLink = Dot();
            FrostGrapplePlugin.Log($"Frost Grapple: art {(spin != null ? "pick" : "-")}/{(stuck != null ? "stuck" : "-")}/{(link != null ? "link" : "-")}/{(impact != null ? "impact" : "-")} (code-drawn stand-ins for the rest)");
        }

        private static Sprite[] Frames(string file, int w)
        {
            Texture2D t = FrostGrapplePlugin.LoadTexture(file);
            if (t == null || t.width < w || t.width % w != 0)
                return null;
            var f = new Sprite[t.width / w];
            for (int i = 0; i < f.Length; i++)
                f[i] = Sprite.Create(t, new Rect(i * w, 0, w, t.height), new Vector2(0.5f, 0.5f), 16f);
            return f;
        }

        private SpriteRenderer Part(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            SpriteRenderer body = owner != null ? owner.spriteRenderer : null;
            if (body != null)
            {
                r.sortingLayerID = body.sortingLayerID;
                r.sortingOrder = body.sortingOrder + order;
            }
            return r;
        }

        public void Flying(Vector2 at)
        {
            isStuck = false;
            pickRenderer.transform.position = at;
            float age = Time.time - born;
            if (spin != null)
            {
                pickRenderer.sprite = spin[(int)(age * 24f) % spin.Length];
                pickRenderer.transform.rotation = Quaternion.identity;
            }
            else
            {
                pickRenderer.sprite = fallbackPick;
                pickRenderer.transform.rotation = Quaternion.Euler(0f, 0f, -age * 900f);
            }
        }

        public void Stuck(Vector2 at)
        {
            isStuck = true;
            pickRenderer.transform.position = at;
            pickRenderer.transform.rotation = Quaternion.identity;
            pickRenderer.sprite = stuck != null ? stuck[0] : spin != null ? spin[0] : fallbackPick;
        }

        private void Update()
        {
            if (isStuck && stuck != null)
                pickRenderer.sprite = stuck[(int)((Time.time - born) * 6f) % stuck.Length];
        }

        public void Chain(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            int count = Mathf.Clamp(Mathf.FloorToInt(d.magnitude / LinkSpacing), 0, 80);
            while (links.Count < count)
            {
                SpriteRenderer r = Part("Link", 1);
                if (link == null)
                    r.color = new Color(0.8f, 0.93f, 1f, 0.9f);
                links.Add(r);
            }
            int frame = (int)((Time.time - born) * 8f);
            for (int i = 0; i < links.Count; i++)
            {
                SpriteRenderer r = links[i];
                r.enabled = i < count;
                if (!r.enabled)
                    continue;
                r.transform.position = from + d * ((i + 1f) / (count + 1f));
                r.sprite = link != null ? link[(frame + i) % link.Length] : fallbackLink;
            }
        }

        // The landing burst, drawn 1:1 when the radius matches Art's sheet.
        public void Impact(Vector2 at, float radius, bool big)
        {
            Sprite[] set = big && impactEnh != null ? impactEnh : impact;
            if (set == null)
                return;
            float native = set == impactEnh ? 25f / 16f : 19f / 16f;
            var go = new GameObject("FrostGrapple Impact");
            go.transform.position = at;
            float s = radius / native;
            go.transform.localScale = new Vector3(s, s, 1f);
            var r = go.AddComponent<SpriteRenderer>();
            SpriteRenderer body = owner != null ? owner.spriteRenderer : null;
            if (body != null)
            {
                r.sortingLayerID = body.sortingLayerID;
                r.sortingOrder = body.sortingOrder + 3;
            }
            go.AddComponent<OneShot>().Play(r, set, 16f);
        }

        public void Finish()
        {
            Destroy(gameObject);
        }

        private class OneShot : MonoBehaviour
        {
            private SpriteRenderer r;
            private Sprite[] frames;
            private float fps, start;

            public void Play(SpriteRenderer r, Sprite[] frames, float fps)
            {
                this.r = r;
                this.frames = frames;
                this.fps = fps;
                start = Time.time;
                r.sprite = frames[0];
            }

            private void Update()
            {
                int f = (int)((Time.time - start) * fps);
                if (f >= frames.Length)
                {
                    Destroy(gameObject);
                    return;
                }
                r.sprite = frames[f];
            }
        }

        // ---- Code-drawn stand-ins ----

        private static Sprite Diamond()
        {
            const int n = 9;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int d = Mathf.Abs(x - 4) + Mathf.Abs(y - 4);
                    Color c = d <= 1 ? Color.white : d <= 3 ? new Color(0.67f, 0.85f, 0.92f) : d == 4 ? new Color(0.29f, 0.51f, 0.61f) : Color.clear;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 16f);
        }

        private static Sprite Dot()
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            t.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
