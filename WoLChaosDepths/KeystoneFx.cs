using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    // Phase 1a: one visual effect per keystone talent (Matt 2026-11-02, Log - Player 17:10). The
    // talent effects (TalentEffects.cs, WoL Modder) call Fire when a keystone acts and SetActive for
    // the ones that last while they apply. Each plays WoL Art's strip fx_ks_<Key>.png (frames side by
    // side, square unless the width says otherwise) on the wizard's feet, in front of it, at 12 fps:
    // once for Fire, looping while active. Afterimage is drawn in code: a fading cyan copy of the
    // wizard left where the dash began. A strip that isn't there yet is skipped (logged once), so
    // the hooks can go in before the art. Only while [Talents] Enabled.
    public static class KeystoneFx
    {
        private const float Fps = 12f;
        private const float AfterimageLife = 1.5f;
        private static readonly Color AfterimageTint = new Color32(0x6f, 0xe0, 0xf0, 0x80);   // WoL Art's #6fe0f0 at 50%
        private static readonly HashSet<string> missing = new HashSet<string>();

        // WoL Art's strips (Log - Art 17:14): frame size, frame count, fps, the anchor point in
        // the frame (from its top left) that goes on the wizard's feet, how far above the feet
        // (units), and whether it's drawn behind the wizard. 'only' picks single frames from the
        // strip (Archmage's ring is state frames: 0 = idle, 4 = the free cast's flare).
        private class Spec
        {
            public string file;
            public int w, h, n;
            public float fps, ax, ay, lift;
            public bool behind;
            public int[] only;
        }

        private static readonly Dictionary<string, Spec> specs = new Dictionary<string, Spec>
        {
            { "Graduate", new Spec { w = 32, h = 32, n = 6, fps = 12, ax = 16, ay = 18 } },
            { "Survivor", new Spec { w = 32, h = 40, n = 5, fps = 12, ax = 16, ay = 36 } },
            { "Juggernaut", new Spec { w = 40, h = 16, n = 5, fps = 12, ax = 20, ay = 9 } },
            { "LastStand", new Spec { w = 40, h = 20, n = 6, fps = 10, ax = 20, ay = 12, behind = true } },
            { "Assassin", new Spec { w = 24, h = 24, n = 4, fps = 12, ax = 12, ay = 12, lift = 0.6f } },
            { "Flurry", new Spec { w = 8, h = 8, n = 4, fps = 12, ax = 4, ay = 4, lift = 0.8f } },
            { "Archmage", new Spec { w = 32, h = 16, n = 5, fps = 1, ax = 16, ay = 8, behind = true, only = new[] { 0 } } },
            { "Archmage!", new Spec { file = "fx_ks_Archmage", w = 32, h = 16, n = 5, fps = 4, ax = 16, ay = 8, behind = true, only = new[] { 4 } } },
            { "Cataclysm", new Spec { w = 64, h = 32, n = 6, fps = 12, ax = 32, ay = 16 } },
            { "Velocity", new Spec { w = 24, h = 24, n = 4, fps = 16, ax = 12, ay = 12, lift = 0.6f, behind = true } },
            { "Sanctuary", new Spec { w = 64, h = 32, n = 6, fps = 10, ax = 32, ay = 16 } },
            { "Lifebloom", new Spec { w = 16, h = 8, n = 4, fps = 12, ax = 8, ay = 4, lift = 0.6f } },
            { "AllIn", new Spec { w = 32, h = 40, n = 6, fps = 10, ax = 16, ay = 35, behind = true } },
            { "DoubleOrNothing", new Spec { w = 12, h = 12, n = 6, fps = 10, ax = 6, ay = 6, lift = 1.9f } },
            { "DoubleOrNothing!", new Spec { file = "fx_ks_DoubleOrNothing_shatter", w = 16, h = 16, n = 4, fps = 12, ax = 8, ay = 8, lift = 1.9f } },
        };
        // The looping effects showing now, per wizard and keystone.
        private static readonly Dictionary<string, Fx> looping = new Dictionary<string, Fx>();
        private static Material material;

        // A keystone just acted: its effect plays once.
        public static void Fire(Player player, string key)
        {
            if (!Talents.Enabled || player == null || player.spriteRenderer == null)
                return;
            try
            {
                if (key == "Afterimage")
                    Afterimage(player);
                else
                    // A one-shot of a lasting keystone has its own strip where Art drew one
                    // (Archmage's flare, Double or Nothing's shatter).
                    Spawn(player, specs.ContainsKey(key + "!") ? key + "!" : key, false);
            }
            catch (System.Exception e)
            {
                Missing(key, e.Message);
            }
        }

        // A keystone's effect left on the floor at a spot, looping for this long (Sanctuary's circle).
        public static void FireAt(Player player, Vector2 spot, string key, float seconds)
        {
            if (!Talents.Enabled || player == null || player.spriteRenderer == null)
                return;
            try
            {
                Fx fx = Spawn(player, key, true);
                if (fx != null)
                    fx.Pin(spot, seconds);
            }
            catch (System.Exception e)
            {
                Missing(key, e.Message);
            }
        }

        // A lasting keystone turned on or off (Last Stand under 30% health, Flurry's stacks, ...).
        // Calling it every frame with the same value is fine.
        public static void SetActive(Player player, string key, bool on)
        {
            if (player == null)
                return;
            string id = player.playerID + "/" + key;
            Fx fx;
            looping.TryGetValue(id, out fx);
            if (fx != null && (!on || !Talents.Enabled))
            {
                Object.Destroy(fx.gameObject);
                looping.Remove(id);
            }
            else if (fx == null && on && Talents.Enabled && player.spriteRenderer != null)
            {
                try
                {
                    fx = Spawn(player, key, true);
                    if (fx != null)
                        looping[id] = fx;
                }
                catch (System.Exception e)
                {
                    Missing(key, e.Message);
                }
            }
        }

        // Every looping effect off (a level load, talents switched off).
        public static void Clear()
        {
            foreach (Fx fx in looping.Values)
                if (fx != null)
                    Object.Destroy(fx.gameObject);
            looping.Clear();
        }

        private static Fx Spawn(Player player, string key, bool loop)
        {
            Spec spec;
            specs.TryGetValue(key, out spec);
            string name = spec != null && spec.file != null ? spec.file : "fx_ks_" + key;
            Texture2D tex = ItemArt.LoadPng(name);
            if (tex == null)
            {
                Missing(key, "no " + name + ".png yet");
                return null;
            }
            // Without a spec: square frames, anchored at the bottom middle.
            int count = spec != null ? spec.n : Mathf.Max(1, tex.width / Mathf.Max(1, tex.height));
            Vector2 pivot = spec != null ? new Vector2(spec.ax / spec.w, (spec.h - spec.ay) / spec.h) : new Vector2(0.5f, 0.25f);
            SpriteRenderer body = player.spriteRenderer;
            float ppu = body.sprite != null && body.sprite.pixelsPerUnit > 0f ? body.sprite.pixelsPerUnit : 16f;
            Sprite[] frames = ItemArt.Frames(name, count, pivot, ppu);
            if (frames == null || frames.Length == 0)
            {
                Missing(key, "couldn't cut " + name + ".png into " + count + " frames");
                return null;
            }
            if (spec != null && spec.only != null)
            {
                var picked = new List<Sprite>();
                foreach (int i in spec.only)
                    if (i >= 0 && i < frames.Length)
                        picked.Add(frames[i]);
                frames = picked.ToArray();
            }
            var go = new GameObject("ChaosDepths Keystone " + key);
            Fx fx = go.AddComponent<Fx>();
            fx.Init(player, body, frames, ppu, loop, Color.white, spec != null ? spec.fps : Fps,
                spec != null ? spec.lift : 0f, spec != null && spec.behind);
            ChaosDepthsPlugin.Log($"Keystone: {key} {(loop ? "on" : "fired")} for player {player.playerID + 1}");
            return fx;
        }

        // Afterimage: the wizard's current picture, tinted cyan, left where it stood and fading.
        private static void Afterimage(Player player)
        {
            SpriteRenderer body = player.spriteRenderer;
            if (body.sprite == null)
                return;
            var go = new GameObject("ChaosDepths Keystone Afterimage");
            go.transform.position = body.transform.position;
            go.transform.localScale = body.transform.lossyScale;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = body.sprite;
            r.flipX = body.flipX;
            r.material = Material();
            r.color = AfterimageTint;
            r.sortingLayerID = body.sortingLayerID;
            r.sortingOrder = body.sortingOrder - 1;
            go.AddComponent<Fade>().life = AfterimageLife;
            ChaosDepthsPlugin.Log($"Keystone: Afterimage fired for player {player.playerID + 1}");
        }

        private static Material Material()
        {
            if (material == null)
                material = new Material(Shader.Find("Sprites/Default"));
            return material;
        }

        private static void Missing(string key, string why)
        {
            if (missing.Add(key))
                ChaosDepthsPlugin.Log($"Keystone: no effect for {key} ({why})");
        }

        // A strip on the wizard's feet, in front of it, not parented (so its turning doesn't flip it).
        private class Fx : MonoBehaviour
        {
            private Player player;
            private SpriteRenderer body, r;
            private Sprite[] frames;
            private float ppu, born, until = -1f;
            private bool loop, pinned;
            private Vector2 spot;

            // Stays at a spot (under the wizards, on the floor) until the time is up.
            public void Pin(Vector2 at, float seconds)
            {
                pinned = true;
                spot = at;
                until = Time.time + seconds;
                Follow();
                r.sortingOrder = body.sortingOrder - 5;
            }

            private float fps, lift;
            private bool behind;

            public void Init(Player owner, SpriteRenderer ownerBody, Sprite[] strip, float pixels, bool loops, Color colour,
                float framesPerSecond, float above, bool drawBehind)
            {
                player = owner;
                body = ownerBody;
                frames = strip;
                ppu = pixels;
                loop = loops;
                fps = framesPerSecond;
                lift = above;
                behind = drawBehind;
                born = Time.time;
                r = gameObject.AddComponent<SpriteRenderer>();
                r.material = Material();
                r.color = colour;
                r.sprite = frames[0];
                Follow();
            }

            private void LateUpdate()
            {
                int frame = (int)((Time.time - born) * fps);
                if (player == null || body == null || (!loop && frame >= frames.Length) || (until >= 0f && Time.time >= until))
                {
                    Destroy(gameObject);
                    return;
                }
                r.sprite = frames[loop ? frame % frames.Length : frame];
                Follow();
            }

            private void Follow()
            {
                Vector3 feet = pinned ? (Vector3)spot : player.transform.position + new Vector3(0f, lift, 0f);
                transform.position = new Vector3(Mathf.Round(feet.x * ppu) / ppu, Mathf.Round(feet.y * ppu) / ppu, feet.z);
                r.sortingLayerID = body.sortingLayerID;
                if (!pinned)
                    r.sortingOrder = body.sortingOrder + (behind ? -1 : 20);
            }
        }

        private class Fade : MonoBehaviour
        {
            public float life;
            private float born = -1f;
            private SpriteRenderer r;
            private Color start;

            private void LateUpdate()
            {
                if (born < 0f)
                {
                    born = Time.time;
                    r = GetComponent<SpriteRenderer>();
                    start = r.color;
                }
                float t = (Time.time - born) / Mathf.Max(0.01f, life);
                if (t >= 1f || r == null)
                {
                    Destroy(gameObject);
                    return;
                }
                r.color = new Color(start.r, start.g, start.b, start.a * (1f - t));
            }
        }
    }
}
