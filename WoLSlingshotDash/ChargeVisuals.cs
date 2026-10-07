using UnityEngine;

namespace WoLSlingshotDash
{
    // Makes the wizard's sprite tremble (only the picture: the wizard doesn't move). Applied after
    // the animation each frame and taken back off the next, so it never drifts.
    public class SpriteShaker : MonoBehaviour
    {
        public float amount;
        private Vector3 applied;
        private Vector3 lastSet;
        private bool active;

        private void LateUpdate()
        {
            Vector3 position = transform.localPosition;
            // Nothing else moved it since our last nudge: take that nudge back first.
            if (active && position == lastSet)
                position -= applied;
            applied = amount > 0f
                ? new Vector3(Random.Range(-1, 2) * amount, Random.Range(-1, 2) * amount * 0.5f, 0f)
                : Vector3.zero;
            lastSet = position + applied;
            transform.localPosition = lastSet;
            active = true;
        }

        public void Stop()
        {
            if (active && transform.localPosition == lastSet)
                transform.localPosition -= applied;
            applied = Vector3.zero;
            amount = 0f;
            active = false;
        }
    }

    // Ice crystals forming round the wizard's feet: little pixel shards that grow, glint and fade.
    public class IceShard : MonoBehaviour
    {
        private const float GrowTime = 0.12f;
        private const float Life = 0.55f;
        private static Sprite sprite;
        private SpriteRenderer sr;
        private float born;
        private float size;

        public static void Spawn(Vector2 position, Player player, float size)
        {
            try
            {
                var go = new GameObject("SlingshotIceShard");
                go.transform.position = position;
                IceShard shard = go.AddComponent<IceShard>();
                shard.size = size;
                shard.born = Time.time;
                shard.sr = go.AddComponent<SpriteRenderer>();
                shard.sr.sprite = ShardSprite(player);
                SpriteRenderer body = player != null ? player.spriteRenderer : null;
                if (body != null)
                {
                    shard.sr.sortingLayerID = body.sortingLayerID;
                    // Behind the wizard when above their feet, in front when below.
                    shard.sr.sortingOrder = body.sortingOrder + (position.y > player.transform.position.y ? -1 : 1);
                }
                shard.transform.localScale = Vector3.zero;
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"Ice shards unavailable: {e.Message}");
            }
        }

        private void Update()
        {
            float age = Time.time - born;
            if (age >= Life)
            {
                Destroy(gameObject);
                return;
            }
            float grow = Mathf.Clamp01(age / GrowTime);
            transform.localScale = new Vector3(size * grow, size * grow, 1f);
            float fade = Mathf.Clamp01((Life - age) / 0.2f);
            sr.color = new Color(1f, 1f, 1f, fade);
        }

        // A 5x9 crystal in the game's frost blues, at the wizard sprite's pixel size.
        private static Sprite ShardSprite(Player player)
        {
            if (sprite != null)
                return sprite;
            string[] rows =
            {
                "..o..",
                ".olo.",
                ".olo.",
                "owlmo",
                "owlmo",
                "olmdo",
                "olmdo",
                ".odo.",
                "..o..",
            };
            var colors = new System.Collections.Generic.Dictionary<char, Color32>
            {
                { 'o', new Color32(0x2a, 0x4e, 0x8c, 0xff) },
                { 'd', new Color32(0x4f, 0x8f, 0xd0, 0xff) },
                { 'm', new Color32(0x8c, 0xc8, 0xf0, 0xff) },
                { 'l', new Color32(0xd2, 0xf0, 0xff, 0xff) },
                { 'w', new Color32(0xff, 0xff, 0xff, 0xff) },
            };
            var tex = new Texture2D(5, 9, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    char c = rows[8 - y][x];
                    tex.SetPixel(x, y, colors.ContainsKey(c) ? (Color)colors[c] : new Color(0f, 0f, 0f, 0f));
                }
            }
            tex.Apply();
            float ppu = 16f;
            try
            {
                if (player != null && player.spriteRenderer != null && player.spriteRenderer.sprite != null)
                    ppu = player.spriteRenderer.sprite.pixelsPerUnit;
            }
            catch
            {
            }
            sprite = Sprite.Create(tex, new Rect(0, 0, 5, 9), new Vector2(0.5f, 0f), ppu);
            return sprite;
        }
    }

    // Electric arcs crackling round the wizard: short jagged bolts redrawn at random, flickering.
    public class Crackle : MonoBehaviour
    {
        private const int Bolts = 4;
        private const int Points = 6;
        private const float Redraw = 0.04f;
        private LineRenderer[] lines;
        private float nextDraw;

        public static Crackle Create(Player player)
        {
            try
            {
                var go = new GameObject("SlingshotCrackle");
                Crackle crackle = go.AddComponent<Crackle>();
                crackle.lines = new LineRenderer[Bolts];
                Material material = new Material(Shader.Find("Sprites/Default"));
                SpriteRenderer body = player.spriteRenderer;
                for (int i = 0; i < Bolts; i++)
                {
                    var child = new GameObject("Bolt" + i);
                    child.transform.SetParent(go.transform, false);
                    LineRenderer line = child.AddComponent<LineRenderer>();
                    line.material = material;
                    line.positionCount = Points;
                    line.useWorldSpace = true;
                    if (body != null)
                    {
                        line.sortingLayerID = body.sortingLayerID;
                        line.sortingOrder = body.sortingOrder + 1;
                    }
                    crackle.lines[i] = line;
                }
                return crackle;
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"Lightning crackle unavailable: {e.Message}");
                return null;
            }
        }

        // Bolts leap out from round the wizard; more, longer and brighter as the charge builds.
        public void Draw(Vector2 center, float charge)
        {
            if (Time.time < nextDraw)
                return;
            nextDraw = Time.time + Redraw;
            for (int i = 0; i < lines.Length; i++)
            {
                LineRenderer line = lines[i];
                bool on = Random.value < 0.35f + 0.55f * charge && i < 2 + Mathf.RoundToInt(2 * charge);
                line.enabled = on;
                if (!on)
                    continue;
                float angle = Random.value * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.7f);
                Vector2 start = center + new Vector2(0f, 0.5f) + dir * 0.25f;
                float length = 0.5f + (0.6f + 0.6f * charge) * Random.value;
                var side = new Vector2(-dir.y, dir.x);
                for (int p = 0; p < Points; p++)
                {
                    float f = (float)p / (Points - 1);
                    float jag = p == 0 || p == Points - 1 ? 0f : Random.Range(-0.18f, 0.18f);
                    Vector2 point = start + dir * (length * f) + side * jag;
                    line.SetPosition(p, new Vector3(point.x, point.y, 0f));
                }
                Color core = Random.value < 0.5f ? new Color(1f, 1f, 0.85f, 1f) : new Color(0.8f, 0.9f, 1f, 1f);
                Color tip = new Color(0.65f, 0.45f, 1f, 0.8f);
                line.startColor = core;
                line.endColor = tip;
                line.startWidth = 0.09f + 0.05f * charge;
                line.endWidth = 0.03f;
            }
        }
    }
}
