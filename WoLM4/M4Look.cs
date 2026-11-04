using System.Collections.Generic;
using UnityEngine;

namespace WoLM4
{
    // 0.3.0 (Matt, 11-04): the M4 is drawn IN THE WIZARD'S HANDS while firing, and the bullets
    // are rifle tracers, not tinted Air Darts. WoL Art's sprites are used when they're in the
    // Assets folder (spec in Log - Player 11:55):
    //   m4_held_8dir.png  8 frames side by side, one per facing (right, down-right, down,
    //                     down-left, left, up-left, up, up-right), centre pivot = the grip
    //   m4_bullet.png     the tracer, pointing RIGHT, centre pivot (turned to the flight)
    //   m4_muzzle.png     muzzle flash strip (square frames, played once), pivot left-centre
    // Until then these are drawn in code at the game's 16 px per unit, in gunmetal greys.
    internal static class M4Look
    {
        public static Sprite[] Held = new Sprite[0];
        public static Sprite Gun, Bullet;
        public static Sprite[] Muzzle = new Sprite[0];
        private static readonly Dictionary<int, GunHolder> holders = new Dictionary<int, GunHolder>();

        public static void Load(BepInEx.Logging.ManualLogSource log)
        {
            Held = Strip(M4Plugin.LoadTexture("m4_held_8dir.png"), new Vector2(0.5f, 0.5f));
            if (Held.Length != 8)
                Held = new Sprite[0];
            Texture2D bullet = M4Plugin.LoadTexture("m4_bullet.png");
            Bullet = bullet != null ? Sprite.Create(bullet, new Rect(0, 0, bullet.width, bullet.height), new Vector2(0.5f, 0.5f), 16f) : Tracer();
            Muzzle = Strip(M4Plugin.LoadTexture("m4_muzzle.png"), new Vector2(0f, 0.5f));
            if (Muzzle.Length == 0)
                Muzzle = new[] { Flash() };
            Gun = Rifle();
            log?.LogInfo($"M4 look: held gun {(Held.Length == 8 ? "WoL Art's 8 facings" : "drawn in code, turned to the aim")}, " +
                $"bullet {(bullet != null ? "WoL Art's" : "drawn tracer")}, muzzle flash {Muzzle.Length} frame(s)");
        }

        private static Sprite[] Strip(Texture2D tex, Vector2 pivot)
        {
            if (tex == null || tex.height <= 0)
                return new Sprite[0];
            int size = tex.height;
            int count = Mathf.Max(1, tex.width / size);
            // Frames are square unless the strip is exactly 8 wide frames (the held gun may be wider than tall).
            if (tex.width % 8 == 0 && tex.width / 8 > tex.height)
            {
                size = tex.width / 8;
                count = 8;
            }
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
                frames[i] = Sprite.Create(tex, new Rect(i * size, 0, size, tex.height), pivot, 16f);
            return frames;
        }

        public static int Facing(Vector2 aim)
        {
            float degrees = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            return ((Mathf.RoundToInt(-degrees / 45f) % 8) + 8) % 8;
        }

        // ---- Code-drawn stand-ins (game palette greys, light from the top, no outlines) ----

        private static Texture2D Canvas(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color32[w * h];
            t.SetPixels32(clear);
            return t;
        }

        private static void Fill(Texture2D t, int x0, int y0, int x1, int y1, Color c)
        {
            // y counted from the TOP, as in an image editor.
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    t.SetPixel(x, t.height - 1 - y, c);
        }

        // A 24x8 rifle pointing right; the grip (pivot) at x 7, y 4 from the top.
        private static Sprite Rifle()
        {
            Color dark = new Color32(0x1b, 0x1c, 0x22, 255), body = new Color32(0x34, 0x37, 0x40, 255),
                light = new Color32(0x5a, 0x5f, 0x6b, 255), shine = new Color32(0x8a, 0x90, 0x9c, 255);
            Texture2D t = Canvas(24, 8);
            Fill(t, 0, 3, 4, 5, body);      // stock
            Fill(t, 0, 3, 4, 3, light);
            Fill(t, 0, 5, 1, 5, dark);
            Fill(t, 5, 2, 15, 4, body);     // receiver + handguard
            Fill(t, 5, 2, 15, 2, light);
            Fill(t, 9, 1, 12, 1, dark);     // carry handle / sight rail
            Fill(t, 10, 0, 10, 0, dark);
            Fill(t, 16, 3, 23, 3, dark);    // barrel
            Fill(t, 16, 3, 20, 3, light);
            Fill(t, 22, 2, 22, 2, dark);    // front sight
            Fill(t, 10, 5, 12, 7, dark);    // magazine
            Fill(t, 10, 5, 11, 5, body);
            Fill(t, 6, 5, 7, 7, dark);      // pistol grip
            Fill(t, 13, 2, 14, 2, shine);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 24, 8), new Vector2(7f / 24f, 3.5f / 8f), 16f);
        }

        // A 10x3 tracer pointing right: white-hot core, gold edges, a fading tail.
        private static Sprite Tracer()
        {
            Texture2D t = Canvas(10, 3);
            Color gold = new Color32(0xf9, 0xd2, 0x3c, 255), core = new Color32(0xff, 0xfb, 0xe8, 255), tail = new Color32(0xe1, 0x4f, 0x00, 255);
            Fill(t, 0, 1, 2, 1, tail);
            Fill(t, 3, 0, 8, 2, gold);
            Fill(t, 3, 1, 9, 1, core);
            Fill(t, 3, 0, 3, 0, new Color(0, 0, 0, 0));
            Fill(t, 3, 2, 3, 2, new Color(0, 0, 0, 0));
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 10, 3), new Vector2(0.5f, 0.5f), 16f);
        }

        // A 7x7 muzzle flash, left edge at the muzzle.
        private static Sprite Flash()
        {
            Texture2D t = Canvas(7, 7);
            Color gold = new Color32(0xf9, 0xd2, 0x3c, 255), core = new Color32(0xff, 0xfb, 0xe8, 255), orange = new Color32(0xf5, 0x90, 0x00, 255);
            Fill(t, 0, 3, 6, 3, gold);
            Fill(t, 2, 1, 2, 5, orange);
            Fill(t, 1, 2, 4, 4, gold);
            Fill(t, 0, 3, 3, 3, core);
            Fill(t, 4, 1, 4, 1, orange);
            Fill(t, 4, 5, 4, 5, orange);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 7, 7), new Vector2(0f, 0.5f), 16f);
        }

        // ---- The gun in the wizard's hands ----

        public static void Show(Player owner, Vector2 aim)
        {
            if (owner == null)
                return;
            GunHolder holder;
            if (!holders.TryGetValue(owner.playerID, out holder) || holder == null)
            {
                var go = new GameObject("M4 Gun");
                holder = go.AddComponent<GunHolder>();
                holder.owner = owner;
                holder.look = go.AddComponent<SpriteRenderer>();
                holders[owner.playerID] = holder;
            }
            holder.Aim(aim);
        }

        public static void Kick(Player owner, Vector2 aim)
        {
            GunHolder holder;
            if (owner != null && holders.TryGetValue(owner.playerID, out holder) && holder != null)
                holder.Fire(aim);
        }

        public static void Hide(Player owner, float after)
        {
            GunHolder holder;
            if (owner != null && holders.TryGetValue(owner.playerID, out holder) && holder != null)
                holder.hideAt = Time.time + after;
        }

        public static void Sort(SpriteRenderer r, Player owner, int offset)
        {
            SpriteRenderer wizard = owner != null ? owner.GetComponentInChildren<SpriteRenderer>() : null;
            if (wizard != null)
            {
                r.sortingLayerID = wizard.sortingLayerID;
                r.sortingOrder = wizard.sortingOrder + offset;
            }
        }

        internal class GunHolder : MonoBehaviour
        {
            private const float HandOut = 0.3f, KickTime = 0.05f, KickBack = 0.1f;
            public Player owner;
            public SpriteRenderer look;
            public float hideAt = -1f;
            private Vector2 aim = Vector2.right;
            private float kickUntil;
            private float flashUntil;
            private SpriteRenderer flash;

            public void Aim(Vector2 a)
            {
                if (a != Vector2.zero)
                    aim = a.normalized;
                hideAt = -1f;
                look.enabled = true;
            }

            public void Fire(Vector2 a)
            {
                Aim(a);
                kickUntil = Time.time + KickTime;
                if (flash == null)
                {
                    var go = new GameObject("M4 Muzzle");
                    go.transform.SetParent(transform, false);
                    flash = go.AddComponent<SpriteRenderer>();
                }
                flashUntil = Time.time + 0.05f;
            }

            private void LateUpdate()
            {
                if (owner == null || owner.attackOriginTrans == null)
                {
                    Destroy(gameObject);
                    return;
                }
                if (hideAt > 0f && Time.time >= hideAt)
                {
                    look.enabled = false;
                    if (flash != null)
                        flash.enabled = false;
                    return;
                }
                if (!look.enabled)
                    return;
                int facing = Facing(aim);
                bool up = aim.y > 0.35f;
                float kick = Time.time < kickUntil ? KickBack : 0f;
                Vector2 hand = (Vector2)owner.attackOriginTrans.position + aim * (HandOut - kick);
                transform.position = hand;
                if (Held.Length == 8)
                {
                    // Art's frames carry the turn; drawn upright.
                    look.sprite = Held[facing];
                    look.flipY = false;
                    transform.rotation = Quaternion.identity;
                }
                else
                {
                    look.sprite = Gun;
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
                    look.flipY = aim.x < -0.01f;   // keep the magazine down when aiming left
                }
                Sort(look, owner, up ? -1 : 2);
                if (flash != null)
                {
                    flash.enabled = Time.time < flashUntil;
                    if (flash.enabled)
                    {
                        flash.sprite = Muzzle[0];
                        flash.transform.position = hand + aim * 1.0f;
                        flash.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
                        Sort(flash, owner, up ? -1 : 3);
                    }
                }
            }
        }
    }
}
