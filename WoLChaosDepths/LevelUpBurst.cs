using BepInEx.Configuration;
using UnityEngine;

namespace WoLChaosDepths
{
    // Task #37: WoL Art's level-up burst on the wizard who levelled (FUTURE.md). 8 frames of 40x56,
    // played once at 16 fps (0.5 s) in front of the wizard, the feet point (x 20, y 50 from the top)
    // on the wizard's feet. Gold alone (levelup_burst.png); in co-op the white strip
    // (levelup_burst_tint.png) multiplied by that wizard's co-op marker colour.
    public static class LevelUpBurst
    {
        private const float Fps = 16f;
        private const int Frames = 8;

        private static ConfigEntry<bool> enabled;
        private static Material material;

        public static void Bind(ConfigFile config)
        {
            enabled = config.Bind("Levels", "LevelUpBurst", true, "Play WoL Art's burst on a wizard who gains a level (in co-op, in that player's colour).");
        }

        public static void Play(Player player)
        {
            if (enabled == null || !enabled.Value || player == null || player.spriteRenderer == null)
                return;
            try
            {
                SpriteRenderer body = player.spriteRenderer;
                float ppu = body.sprite != null && body.sprite.pixelsPerUnit > 0f ? body.sprite.pixelsPerUnit : 16f;
                bool coop = Players() > 1;
                Color colour = Color.white;
                Sprite[] frames = null;
                if (coop && player.playerMarker != null)
                {
                    frames = ItemArt.Frames("levelup_burst_tint", Frames, new Vector2(0.5f, 6f / 56f), ppu);
                    colour = player.playerMarker.coopColor;
                    colour.a = 1f;
                }
                if (frames == null)
                {
                    frames = ItemArt.Frames("levelup_burst", Frames, new Vector2(0.5f, 6f / 56f), ppu);
                    colour = Color.white;
                }
                if (frames == null)
                    return;
                var go = new GameObject("ChaosDepthsLevelUpBurst");
                var burst = go.AddComponent<Burst>();
                burst.player = player;
                burst.body = body;
                burst.frames = frames;
                burst.ppu = ppu;
                burst.born = Time.time;
                burst.r = go.AddComponent<SpriteRenderer>();
                if (material == null)
                    material = new Material(Shader.Find("Sprites/Default"));
                burst.r.material = material;
                burst.r.color = colour;
                burst.r.sprite = frames[0];
                burst.Follow();
            }
            catch (System.Exception e)
            {
                ChaosDepthsPlugin.LogError($"level-up burst: {e.Message}");
            }
        }

        private static int Players()
        {
            int n = 0;
            Player[] players = GameController.activePlayers;
            if (players != null)
                foreach (Player p in players)
                    if (p != null)
                        n++;
            return n;
        }

        // Not parented to the wizard, so its turning (a mirrored scale) doesn't flip the burst.
        private class Burst : MonoBehaviour
        {
            public Player player;
            public SpriteRenderer body, r;
            public Sprite[] frames;
            public float ppu, born;

            private void LateUpdate()
            {
                int frame = (int)((Time.time - born) * Fps);
                if (player == null || body == null || frame >= frames.Length)
                {
                    Destroy(gameObject);
                    return;
                }
                r.sprite = frames[frame];
                Follow();
            }

            public void Follow()
            {
                Vector3 feet = player.transform.position;
                transform.position = new Vector3(Mathf.Round(feet.x * ppu) / ppu, Mathf.Round(feet.y * ppu) / ppu, feet.z);
                r.sortingLayerID = body.sortingLayerID;
                r.sortingOrder = body.sortingOrder + 20;
            }
        }
    }
}
