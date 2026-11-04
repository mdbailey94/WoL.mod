using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLChaosDepths
{
    // Task #29: WoL Art's "Chaos Surge" look for a run at Ascension 10.
    //   a10_banner.png      4 frames of 200x44: 'CHAOS SURGE' with a light sweeping across, shown
    //                       top centre for about 3 s when an A10 run enters the trials
    //   a10_vignette.png    320x180 violet screen edges, stretched to the screen, its opacity
    //                       pulsing slowly, over a faint violet room tint
    //   a10_mote.png        4 frames of 8x8, a few chaos motes drifting up the screen
    //   a10_enemy_aura.png  6 frames of 24x24 behind every enemy's feet
    // Everything is drawn at whole-pixel scales. [Chaos Surge] Preview shows it at any level.
    public static class ChaosSurge
    {
        private const float BannerSeconds = 3f, BannerFade = 0.5f, BannerFps = 10f;
        private const float AuraFps = 8f, MoteFps = 6f, PulseSeconds = 3f;
        private const int Motes = 7;

        private static ConfigEntry<bool> enabled, preview, auras;
        private static ConfigEntry<float> vignette, tint;
        private const float LightVignette = 0.3f, LightTint = 0.03f;   // was 1.0 and 0.08 (too distracting, Matt)

        private static bool active, wasInTrials, logged;
        private static float bannerAt = -100f;
        private static readonly HashSet<int> aured = new HashSet<int>();
        private static Material material;

        public static void Bind(ConfigFile config)
        {
            enabled = config.Bind("Chaos Surge", "Enabled", true, "At Ascension 10, show WoL Art's Chaos Surge look in the trials: the banner, a violet vignette and tint, drifting motes and an aura behind every enemy (#29).");
            preview = config.Bind("Chaos Surge", "Preview", false, "Show the Chaos Surge look at any Ascension level, to check it.");
            auras = config.Bind("Chaos Surge", "EnemyAuras", true, "Draw the violet aura behind every enemy's feet.");
            vignette = config.Bind("Chaos Surge", "Vignette", LightVignette, new ConfigDescription("Opacity of the violet screen edges (0 = off).", new AcceptableValueRange<float>(0f, 1f)));
            tint = config.Bind("Chaos Surge", "RoomTint", LightTint, new ConfigDescription("Opacity of the faint violet tint over the room (0 = off).", new AcceptableValueRange<float>(0f, 0.5f)));
            // Matt (11-04 15:00): "make the visual border much lighter, it's too distracting".
            // Configs still on the old defaults (edges 1.0, tint 0.08) move to the light ones once.
            var revision = config.Bind("Chaos Surge", "LookRevision", 0, "Internal: which default look this config was last moved to. Leave it.");
            if (revision.Value < 1)
            {
                if (Mathf.Approximately(vignette.Value, 1f))
                    vignette.Value = LightVignette;
                if (Mathf.Approximately(tint.Value, 0.08f))
                    tint.Value = LightTint;
                revision.Value = 1;
            }
        }

        // Twice a second from the plugin: whether the look is on, the banner when an A10 run
        // reaches the trials, and auras on enemies not yet given one.
        public static void Tick(bool inTrials)
        {
            if (!inTrials)
            {
                aured.Clear();
                wasInTrials = false;
                active = false;
                return;
            }
            active = enabled.Value && (preview.Value || AscensionLink.Level >= 10);
            if (!wasInTrials && active)
            {
                bannerAt = Time.unscaledTime;
                if (!logged)
                {
                    logged = true;
                    ChaosDepthsPlugin.Log($"Chaos Surge: the Ascension 10 look is on ({(preview.Value ? "preview" : "level " + AscensionLink.Level)})");
                }
            }
            wasInTrials = true;
            if (active && auras.Value)
                AttachAuras();
        }

        // ---- Screen overlay (from the plugin's OnGUI, in the trials) ----

        public static void Draw()
        {
            if (!active)
                return;
            try
            {
                float t = Time.unscaledTime;
                int s = Mathf.Max(1, Mathf.FloorToInt(Screen.height / 360f));
                if (tint.Value > 0f)
                    PixelUI.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.78f, 0.63f, 1f, tint.Value));
                if (vignette.Value > 0f)
                {
                    Texture2D v = ItemArt.LoadPng("a10_vignette");
                    if (v != null)
                    {
                        float pulse = 0.95f + 0.05f * Mathf.Sin(t * 2f * Mathf.PI / PulseSeconds);
                        Color previous = GUI.color;
                        GUI.color = new Color(1f, 1f, 1f, vignette.Value * pulse);
                        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), v, ScaleMode.StretchToFill);
                        GUI.color = previous;
                    }
                }
                DrawMotes(t, s);
                float since = t - bannerAt;
                if (since >= 0f && since < BannerSeconds)
                {
                    float alpha = since > BannerSeconds - BannerFade ? (BannerSeconds - since) / BannerFade : 1f;
                    int frame = Mathf.Min(3, (int)(since * BannerFps));
                    float x = Mathf.Floor((Screen.width - 200 * s) / 2f);
                    PixelUI.Sheet("a10_banner", 4, frame, x, 24 * s, s, new Color(1f, 1f, 1f, alpha));
                }
            }
            catch
            {
            }
        }

        // A few motes rising slowly, each on its own column, speed and phase, wrapping at the top.
        private static void DrawMotes(float t, int s)
        {
            for (int i = 0; i < Motes; i++)
            {
                float column = Hash(i, 1), speed = 14f + 10f * Hash(i, 2), phase = Hash(i, 3);
                float span = Screen.height + 8 * s;
                float y = Screen.height - ((t * speed * s + phase * span) % span);
                float x = Mathf.Floor(column * (Screen.width - 8 * s) + Mathf.Sin(t * 0.7f + i) * 3 * s);
                PixelUI.Sheet("a10_mote", 4, (int)(t * MoteFps) + i, x, Mathf.Floor(y), s, new Color(1f, 1f, 1f, 0.85f));
            }
        }

        private static float Hash(int i, int salt)
        {
            unchecked
            {
                uint h = (uint)(i * 374761393 + salt * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65536f;
            }
        }

        // ---- Enemy auras ----

        private static void AttachAuras()
        {
            if (GameController.enemies == null)
                return;
            foreach (GameObject go in new List<GameObject>(GameController.enemies))
            {
                if (go == null || !go.activeInHierarchy)
                    continue;
                Entity entity = go.GetComponent<Entity>();
                if (entity == null || entity is Player || entity.spriteRenderer == null || entity.health == null || entity.health.CurrentHealthValue <= 0f)
                    continue;
                if (!aured.Add(entity.GetInstanceID()))
                    continue;
                SpriteRenderer body = entity.spriteRenderer;
                float ppu = body.sprite != null && body.sprite.pixelsPerUnit > 0f ? body.sprite.pixelsPerUnit : 16f;
                // The ellipse's centre (x 12, y 20 from the top of 24) on the feet.
                Sprite[] frames = ItemArt.Frames("a10_enemy_aura", 6, new Vector2(0.5f, 4f / 24f), ppu);
                if (frames == null)
                    return;
                var aura = new GameObject("ChaosDepthsSurgeAura").AddComponent<Aura>();
                aura.entity = entity;
                aura.body = body;
                aura.frames = frames;
                aura.ppu = ppu;
                aura.r = aura.gameObject.AddComponent<SpriteRenderer>();
                if (material == null)
                    material = new Material(Shader.Find("Sprites/Default"));
                aura.r.material = material;
                aura.r.sprite = frames[0];
                aura.born = Time.time - Random.value;
                // Scaled up in whole steps for big enemies (a 24-px aura is about a grunt's width):
                // by the enemy's own scale (a Giant elite's), 2 for bosses. 0.19.0 went by the
                // picture's width, which counts its empty margin, so bunched knights at 2-3x
                // merged into one wide band (#29 retest).
                aura.scale = entity is Boss || entity is MiniBoss ? 2
                    : Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(entity.transform.lossyScale.y)), 1, 3);
                aura.transform.localScale = new Vector3(aura.scale, aura.scale, 1f);
                aura.Follow();
            }
        }

        // Not parented to the enemy, so its turning (a mirrored scale) doesn't flip the aura.
        private class Aura : MonoBehaviour
        {
            public Entity entity;
            public SpriteRenderer body, r;
            public Sprite[] frames;
            public float ppu, born;
            public int scale;

            private void LateUpdate()
            {
                if (entity == null || body == null || entity.health == null || entity.health.CurrentHealthValue <= 0f || !active)
                {
                    Destroy(gameObject);
                    return;
                }
                bool shown = entity.gameObject.activeInHierarchy && body.enabled;
                if (shown)
                {
                    r.sprite = frames[(int)((Time.time - born) * AuraFps) % frames.Length];
                    Follow();
                }
                r.enabled = shown;
            }

            public void Follow()
            {
                Bounds b = body.bounds;
                float x = Mathf.Round(b.center.x * ppu) / ppu, y = Mathf.Round(b.min.y * ppu) / ppu;
                transform.position = new Vector3(x, y, body.transform.position.z);
                r.sortingLayerID = body.sortingLayerID;
                r.sortingOrder = body.sortingOrder - 1;
            }
        }
    }
}
