using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLChaosDepths
{
    // Task #17: WoL Art's elite markers over each elite's head (elite_marker_<swift|armored|giant>.png,
    // 4 frames of 16x16: dim, normal, bright, normal), looped at 7 fps and shown at true pixel size.
    // The marker isn't parented to the enemy, so the Giant's larger scale and the enemies' turning
    // (a mirrored x scale) don't stretch or flip it; it follows the top of the enemy's picture
    // instead, and goes when the elite dies or is gone.
    public static class EliteMarkers
    {
        private const float Fps = 7f;
        private const int GapPixels = 2;     // between the top of the enemy's picture and the marker
        private const int HeadGapPixels = 6; // between the game's head point and the marker
        private const int SidePixels = 15;   // between two markers' centres (16 px wide, 1 px apart, trimmed edges)
        private const int OverlapXPixels = 13, OverlapYPixels = 12;   // markers of different elites closer than this overlap

        private static ConfigEntry<bool> enabled;
        private static Material material;

        public static void Bind(ConfigFile config)
        {
            enabled = config.Bind("Elites", "Markers", true, "Show a marker for the affix (Swift, Armored, Giant) over each elite's head.");
        }

        // index / count: with two affixes (Ascension 4 and up) the markers stand side by side.
        public static void Attach(Entity entity, string affix, int index = 0, int count = 1)
        {
            if (!enabled.Value || entity == null || entity.spriteRenderer == null)
                return;
            try
            {
                SpriteRenderer body = entity.spriteRenderer;
                float ppu = body.sprite != null && body.sprite.pixelsPerUnit > 0f ? body.sprite.pixelsPerUnit : 16f;
                Sprite[] frames = ItemArt.Frames("elite_marker_" + affix.ToLowerInvariant(), 4, new Vector2(0.5f, 0f), ppu);
                if (frames == null)
                    return;
                var go = new GameObject($"ChaosDepthsEliteMarker {affix}");
                var marker = go.AddComponent<Marker>();
                marker.entity = entity;
                marker.body = body;
                marker.frames = frames;
                marker.ppu = ppu;
                marker.r = go.AddComponent<SpriteRenderer>();
                marker.r.sprite = frames[0];
                if (material == null)
                    material = new Material(Shader.Find("Sprites/Default"));
                marker.r.material = material;
                // So markers in a pack don't pulse together. In the past: up to 0.15.12 it was in the
                // future, and the frame index went negative for up to a second (an IndexOutOfRange).
                marker.born = Time.time - Random.value;
                marker.sideways = (index - (count - 1) * 0.5f) * SidePixels / ppu;
                marker.Follow();
            }
            catch (System.Exception e)
            {
                ChaosDepthsPlugin.LogError($"elite marker: {e.Message}");
            }
        }

        private class Marker : MonoBehaviour
        {
            public Entity entity;
            public SpriteRenderer body;
            public Sprite[] frames;
            public SpriteRenderer r;
            public float ppu, born, sideways;

            // Every marker in the scene, so a pack of elites doesn't pile its markers into one
            // unreadable cluster (#27 retest): where markers of different elites overlap, only the
            // one over the elite nearest a wizard shows.
            private static readonly List<Marker> live = new List<Marker>();
            private float nearest; // this elite's distance to the nearest wizard, this frame

            private void OnEnable() => live.Add(this);
            private void OnDisable() => live.Remove(this);

            private void LateUpdate()
            {
                if (entity == null || body == null || entity.health == null || entity.health.CurrentHealthValue <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
                bool shown = entity.gameObject.activeInHierarchy && body.enabled;
                if (shown)
                {
                    r.sprite = frames[(int)((Time.time - born) * Fps) % frames.Length];
                    Follow();
                    nearest = NearestWizard(transform.position);
                    shown = !Covered();
                }
                r.enabled = shown;
            }

            // Another elite's marker overlaps this one and that elite is nearer a wizard.
            private bool Covered()
            {
                Vector2 at = transform.position;
                float dx = OverlapXPixels / ppu, dy = OverlapYPixels / ppu;
                foreach (Marker other in live)
                {
                    if (other == this || other == null || other.entity == entity || other.entity == null)
                        continue;
                    Vector2 o = other.transform.position;
                    if (Mathf.Abs(o.x - at.x) >= dx || Mathf.Abs(o.y - at.y) >= dy)
                        continue;
                    if (other.nearest < nearest || (other.nearest == nearest && other.GetInstanceID() < GetInstanceID()))
                        return true;
                }
                return false;
            }

            private static float NearestWizard(Vector2 at)
            {
                float best = float.MaxValue;
                Player[] players = GameController.activePlayers;
                if (players != null)
                    foreach (Player p in players)
                        if (p != null)
                            best = Mathf.Min(best, ((Vector2)p.transform.position - at).sqrMagnitude);
                return best;
            }

            // Over the enemy's head, snapped to the pixel grid: the game's own head point (where it
            // puts hats) when the enemy has one, which stays put through the animation; otherwise
            // the top of the sprite's rect, which can sit a little above the drawn head.
            public void Follow()
            {
                Vector3 top;
                if (entity.headPosition != null)
                    top = entity.headPosition.transform.position + new Vector3(0f, HeadGapPixels / ppu, 0f);
                else
                {
                    Bounds b = body.bounds;
                    top = new Vector3(b.center.x, b.max.y + GapPixels / ppu, 0f);
                }
                float x = Mathf.Round((top.x + sideways) * ppu) / ppu;
                float y = Mathf.Round(top.y * ppu) / ppu;
                transform.position = new Vector3(x, y, body.transform.position.z);
                r.sortingLayerID = body.sortingLayerID;
                r.sortingOrder = body.sortingOrder + 10;
            }
        }
    }
}
