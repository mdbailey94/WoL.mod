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
        private static readonly Color AfterimageTint = new Color(0.45f, 0.9f, 1f, 0.6f);
        private static readonly HashSet<string> missing = new HashSet<string>();
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
                    Spawn(player, key, false);
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
            string name = "fx_ks_" + key;
            Texture2D tex = ItemArt.LoadPng(name);
            if (tex == null)
            {
                Missing(key, "no " + name + ".png yet");
                return null;
            }
            int count = Mathf.Max(1, tex.width / Mathf.Max(1, tex.height));
            SpriteRenderer body = player.spriteRenderer;
            float ppu = body.sprite != null && body.sprite.pixelsPerUnit > 0f ? body.sprite.pixelsPerUnit : 16f;
            Sprite[] frames = ItemArt.Frames(name, count, new Vector2(0.5f, 0.25f), ppu);
            if (frames == null || frames.Length == 0)
            {
                Missing(key, "couldn't cut " + name + ".png into " + count + " frames");
                return null;
            }
            var go = new GameObject("ChaosDepths Keystone " + key);
            Fx fx = go.AddComponent<Fx>();
            fx.Init(player, body, frames, ppu, loop, Color.white);
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
            private float ppu, born;
            private bool loop;

            public void Init(Player owner, SpriteRenderer ownerBody, Sprite[] strip, float pixels, bool loops, Color colour)
            {
                player = owner;
                body = ownerBody;
                frames = strip;
                ppu = pixels;
                loop = loops;
                born = Time.time;
                r = gameObject.AddComponent<SpriteRenderer>();
                r.material = Material();
                r.color = colour;
                r.sprite = frames[0];
                Follow();
            }

            private void LateUpdate()
            {
                int frame = (int)((Time.time - born) * Fps);
                if (player == null || body == null || (!loop && frame >= frames.Length))
                {
                    Destroy(gameObject);
                    return;
                }
                r.sprite = frames[loop ? frame % frames.Length : frame];
                Follow();
            }

            private void Follow()
            {
                Vector3 feet = player.transform.position;
                transform.position = new Vector3(Mathf.Round(feet.x * ppu) / ppu, Mathf.Round(feet.y * ppu) / ppu, feet.z);
                r.sortingLayerID = body.sortingLayerID;
                r.sortingOrder = body.sortingOrder + 20;
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
