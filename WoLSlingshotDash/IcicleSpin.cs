using UnityEngine;

namespace WoLSlingshotDash
{
    // Feint Swap's signature spin, for show only (the hit is the Frost Nova that goes off as it
    // bursts, FeintSwapSignature.cs): a ring of icicles that spins outward from where you swapped
    // in, each pointing out, then bursts into the game's icicle shards (its "IcicleBreakEffect",
    // what its ice shards and icicles break into).
    //
    // The icicles are drawn with the sprite of the game's ice shard arrow (IceShard.ArrowPrefab,
    // the shards the Ice boss's signature fires in a ring), read from the prefab without making
    // one, so nothing of the boss's projectile (its attack, its pool) is involved. If that sprite
    // can't be had, the mod's own frost crystal stands in (the log says which).
    //
    // Runs on game time, so the hit-stops freeze it with everything else.
    public class IcicleSpin : MonoBehaviour
    {
        private const float StartRadius = 0.3f;   // the ring starts this far out...
        private const float SpinSpeed = 720f;     // ...turning this many degrees a second...
        private const float StartScale = 0.55f;   // ...the icicles growing from this to full size as it spreads
        private const float YSquash = 0.7f;       // the ring is flattened, as the floor is seen at an angle
        private const int BreakShards = 5;        // the game's icicle shards each icicle bursts into
        private const int OrderAbove = 2;         // drawn just above the wizard

        private static bool looked;
        private static Sprite gameSprite;
        private static Material gameMaterial;
        private static Vector3 gameScale = Vector3.one;
        private static int gameLayer, gameOrder;
        private static bool loggedSource;
        private static bool loggedBreak;

        private Transform[] icicles;
        private Vector2 centre;
        private float radius, life, born, turn, sense, size = 1f;

        // A ring of `count` icicles spreading to `ringRadius` over `time` seconds, then bursting.
        public static void Create(Vector2 centre, int count, float ringRadius, float time, Player player, bool clockwise)
        {
            try
            {
                LookUpGameSprite();
                var go = new GameObject("FeintSwapIcicleSpin");
                go.transform.position = centre;
                IcicleSpin spin = go.AddComponent<IcicleSpin>();
                spin.centre = centre;
                spin.radius = ringRadius;
                spin.life = Mathf.Max(0.05f, time);
                spin.born = Time.time;
                spin.turn = Random.value * 360f;
                spin.sense = clockwise ? -1f : 1f;
                spin.icicles = new Transform[count];

                Sprite sprite = gameSprite;
                Material material = gameMaterial;
                int layer = gameLayer, order = gameOrder;
                Vector3 scale = gameScale;
                SpriteRenderer body = player != null ? player.spriteRenderer : null;
                if (sprite == null)
                {
                    sprite = IceShard.ShardSprite(player);
                    material = null;
                    scale = Vector3.one;
                    spin.size = 1.3f;
                    if (body != null)
                    {
                        layer = body.sortingLayerID;
                        order = body.sortingOrder + OrderAbove;
                    }
                }
                for (int i = 0; i < count; i++)
                {
                    var icicle = new GameObject("Icicle" + i);
                    icicle.transform.SetParent(go.transform, false);
                    SpriteRenderer sr = icicle.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    if (material != null)
                        sr.sharedMaterial = material;
                    sr.sortingLayerID = layer;
                    sr.sortingOrder = order;
                    icicle.transform.localScale = scale * (StartScale * spin.size);
                    spin.icicles[i] = icicle.transform;
                }
                spin.Place(0f);
                if (!loggedSource)
                {
                    loggedSource = true;
                    SlingshotDashPlugin.Log("Feint Swap: signature icicles drawn with " + (gameSprite != null
                        ? $"the game's ice shard sprite ('{gameSprite.name}')"
                        : "the mod's frost crystal (the game's ice shard sprite wasn't available)"));
                }
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"Feint Swap: icicle spin unavailable: {e.Message}");
            }
        }

        // The ice shard arrow's sprite, material and sorting, straight from its prefab.
        private static void LookUpGameSprite()
        {
            if (looked)
                return;
            looked = true;
            try
            {
                GameObject prefab = global::IceShard.ArrowPrefab;
                Transform sprite = prefab != null ? prefab.transform.Find("ProjectileSprite") : null;
                SpriteRenderer sr = sprite != null ? sprite.GetComponent<SpriteRenderer>() : null;
                if (sr == null && prefab != null)
                    sr = prefab.GetComponentInChildren<SpriteRenderer>(true);
                if (sr == null || sr.sprite == null)
                    return;
                gameSprite = sr.sprite;
                gameMaterial = sr.sharedMaterial;
                gameLayer = sr.sortingLayerID;
                gameOrder = sr.sortingOrder;
                gameScale = sr.transform.localScale;
            }
            catch (System.Exception e)
            {
                gameSprite = null;
                SlingshotDashPlugin.Log($"Feint Swap: the game's ice shard sprite unavailable: {e.Message}");
            }
        }

        private void Update()
        {
            float age = Time.time - born;
            if (age >= life)
            {
                Burst();
                Destroy(gameObject);
                return;
            }
            Place(age / life);
        }

        // Round the ring, spinning, each icicle pointing out from the centre.
        private void Place(float k)
        {
            float ease = 1f - (1f - k) * (1f - k);
            float r = Mathf.Lerp(StartRadius, radius, ease);
            float spin = turn + sense * SpinSpeed * k * life;
            float grow = Mathf.Lerp(StartScale, 1f, ease) * size;
            for (int i = 0; i < icicles.Length; i++)
            {
                if (icicles[i] == null)
                    continue;
                float a = (spin + 360f * i / icicles.Length) * Mathf.Deg2Rad;
                Vector2 outward = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * YSquash);
                icicles[i].position = centre + outward * r;
                // The sprites point up; turn each to point along its way out.
                icicles[i].rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg - 90f);
                icicles[i].localScale = (gameSprite != null ? gameScale : Vector3.one) * grow;
            }
        }

        // Each icicle breaks into the game's icicle shards.
        private void Burst()
        {
            try
            {
                ParticleEffect shards = PoolManager.GetPoolItem<ParticleEffect>("IcicleBreakEffect");
                for (int i = 0; i < icicles.Length; i++)
                {
                    if (icicles[i] != null)
                        shards.Emit(BreakShards, icicles[i].position);
                }
            }
            catch (System.Exception e)
            {
                if (!loggedBreak)
                {
                    loggedBreak = true;
                    SlingshotDashPlugin.Log($"Feint Swap: icicle break effect unavailable: {e.Message}");
                }
            }
        }
    }
}
