using System.Collections.Generic;
using UnityEngine;

namespace WoLGiantSpear
{
    // WoL Art's spear (#44), 16 px per unit, Point filter, pointing right: the thrust (4 x 48x15)
    // and the finisher (5 x 48x15). The shaft runs from x 2 (the butt) to x 33 (the tip) on row 7.
    // Drawn at giant scale: big enough that the tip lands on the stab's reach.
    internal static class SpearArt
    {
        public const float ButtX = 2f, TipX = 33f, FrameWidth = 48f;
        public static Sprite[] Thrust, Throw;

        public static void Load(BepInEx.Logging.ManualLogSource log)
        {
            Thrust = Frames("spear_thrust.png", 48, new Vector2(ButtX / FrameWidth, 0.5f));
            // The thrown spear turns round its middle.
            Throw = Frames("spear_throw.png", 48, new Vector2((ButtX + TipX) / 2f / FrameWidth, 0.5f));
            log?.LogInfo($"Giant Air Spear art: thrust {Thrust.Length}, throw {Throw.Length} frames");
        }

        // How much to scale the art so the spear (butt to tip) is this long.
        public static float ScaleFor(float length) => length * 16f / (TipX - ButtX);

        private static Sprite[] Frames(string file, int width, Vector2 pivot)
        {
            Texture2D texture = GiantSpearPlugin.LoadTexture(file);
            if (texture == null || texture.width < width)
                return new Sprite[0];
            var frames = new Sprite[texture.width / width];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(texture, new Rect(i * width, 0, width, texture.height), pivot, 16f);
            return frames;
        }
    }

    internal static class Spear
    {
        // Hits 1 and 2: the game's wind burst on every enemy along the spear.
        public static void Stab(Player owner, string skillID, Vector2 hand, Vector2 aim, bool enhanced)
        {
            float reach = GiantSpearPlugin.StabReach * (enhanced ? GiantSpearPlugin.EnhancedReach : 1f);
            int hits = 0;
            foreach (GameObject enemy in EnemiesAlong(hand, aim, reach, GiantSpearPlugin.StabWidth * 0.5f))
            {
                Burst(enemy.transform.position, owner.skillCategory, skillID, 1, 0.3f);
                hits++;
            }
            float scale = SpearArt.ScaleFor(reach);
            Anim.Play(SpearArt.Thrust, hand, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, new Vector3(scale, scale, 1f), 16f, owner);
            GiantSpearPlugin.Log($"Giant Air Spear: stab ({reach:0.0} u), {hits} hit");
        }

        internal static void Burst(Vector2 at, string category, string skillID, int level, float radius)
        {
            try
            {
                WindBurst.CreateBurst(at, category, skillID, level, radius);
            }
            catch (System.Exception e)
            {
                GiantSpearPlugin.Log($"Giant Air Spear: a burst failed: {e.Message}");
            }
        }

        internal static void Sound(string name, Vector2 at, float pitch)
        {
            try
            {
                SoundManager.PlayAudioWithDistance(name, at, null, 24f, -1f, pitch);
            }
            catch
            {
            }
        }

        // Enemies whose centre is within halfWidth (+ a little for their body) of the segment.
        internal static List<GameObject> EnemiesAlong(Vector2 from, Vector2 dir, float length, float halfWidth)
        {
            var found = new List<GameObject>();
            if (GameController.enemies == null)
                return found;
            float reach = halfWidth + 0.3f;
            foreach (GameObject go in GameController.enemies)
            {
                if (go == null || !go.activeInHierarchy)
                    continue;
                Vector2 rel = (Vector2)go.transform.position - from;
                float along = Vector2.Dot(rel, dir);
                if (along < -0.2f || along > length + 0.3f)
                    continue;
                if (Mathf.Abs(rel.x * dir.y - rel.y * dir.x) <= reach)
                    found.Add(go);
            }
            return found;
        }

        internal static void Sort(SpriteRenderer r, Player owner, int offset)
        {
            SpriteRenderer wizard = owner != null ? owner.GetComponentInChildren<SpriteRenderer>() : null;
            if (wizard != null)
            {
                r.sortingLayerID = wizard.sortingLayerID;
                r.sortingOrder = wizard.sortingOrder + offset;
            }
        }
    }

    // A one-shot sprite animation in the world.
    internal class Anim : MonoBehaviour
    {
        private Sprite[] frames;
        private float fps, born;
        private SpriteRenderer look;

        public static void Play(Sprite[] frames, Vector2 at, float angle, Vector3 scale, float fps, Player owner)
        {
            if (frames == null || frames.Length == 0)
                return;
            var go = new GameObject("GiantSpear Fx");
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = scale;
            Anim a = go.AddComponent<Anim>();
            a.frames = frames;
            a.fps = fps;
            a.born = Time.time;
            a.look = go.AddComponent<SpriteRenderer>();
            a.look.sprite = frames[0];
            Spear.Sort(a.look, owner, 1);
        }

        private void Update()
        {
            int f = (int)((Time.time - born) * fps);
            if (f >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }
            look.sprite = frames[f];
        }
    }

    // Hit 3: the spear flies out along the aim, piercing (a wind burst on each enemy it passes,
    // once each way), turns at its range (enhanced: a gust there) and flies back to the wizard,
    // who catches it.
    internal class ThrownSpear : MonoBehaviour
    {
        private const float MaxLife = 4f, CatchDistance = 0.6f;
        private Player owner;
        private string skillID;
        private Vector2 dir;
        private float range, born, distance;
        private bool returning, enhanced;
        private SpriteRenderer look;
        private readonly HashSet<int> hitOut = new HashSet<int>(), hitBack = new HashSet<int>();

        public static void Throw(Player owner, string skillID, Vector2 hand, Vector2 aim, bool enhanced)
        {
            var go = new GameObject("GiantSpear Thrown");
            go.transform.position = hand;
            ThrownSpear spear = go.AddComponent<ThrownSpear>();
            spear.owner = owner;
            spear.skillID = skillID;
            spear.dir = aim;
            spear.enhanced = enhanced;
            spear.range = GiantSpearPlugin.ThrowRange * (enhanced ? GiantSpearPlugin.EnhancedReach : 1f);
            spear.born = Time.time;
            float scale = SpearArt.ScaleFor(GiantSpearPlugin.StabReach);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            spear.look = go.AddComponent<SpriteRenderer>();
            if (SpearArt.Throw.Length > 0)
                spear.look.sprite = SpearArt.Throw[0];
            Spear.Sort(spear.look, owner, 1);
            spear.Face(aim);
            Spear.Sound("WindBoomerang", hand, 0.8f);
            GiantSpearPlugin.Log($"Giant Air Spear: thrown ({spear.range:0.0} u{(enhanced ? ", gust" : "")})");
        }

        private void Face(Vector2 towards)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(towards.y, towards.x) * Mathf.Rad2Deg);
        }

        private void Update()
        {
            if (owner == null || Time.time - born > MaxLife)
            {
                Destroy(gameObject);
                return;
            }
            // Frames 1-3 of the finisher carry the wind streaks: they loop in flight.
            if (SpearArt.Throw.Length >= 4)
                look.sprite = SpearArt.Throw[1 + (int)((Time.time - born) * 14f) % 3];

            Vector2 from = transform.position;
            Vector2 to;
            if (!returning)
            {
                float step = GiantSpearPlugin.ThrowSpeed * Time.deltaTime;
                to = from + dir * step;
                distance += step;
                if (distance >= range)
                {
                    returning = true;
                    if (enhanced)
                    {
                        Spear.Burst(to, owner.skillCategory, skillID, 3, GiantSpearPlugin.GustRadius);
                        Spear.Sound("WindBlastDeep", to, 1f);
                    }
                }
            }
            else
            {
                Vector2 home = owner.transform.position;
                Vector2 back = home - from;
                float step = GiantSpearPlugin.ReturnSpeed * Time.deltaTime;
                if (back.magnitude <= Mathf.Max(CatchDistance, step))
                {
                    Spear.Sound("WindSlashBasic", home, 1.3f);
                    Destroy(gameObject);
                    return;
                }
                to = from + back.normalized * step;
                Face(back);
            }
            transform.position = to;
            Strike(from, to, returning ? hitBack : hitOut);
        }

        // A wind burst on each enemy the spear swept past this frame, once a leg.
        private void Strike(Vector2 from, Vector2 to, HashSet<int> hit)
        {
            Vector2 move = to - from;
            float length = move.magnitude;
            Vector2 way = length > 0.0001f ? move / length : dir;
            foreach (GameObject enemy in Spear.EnemiesAlong(from, way, length, GiantSpearPlugin.ThrowWidth * 0.5f))
                if (hit.Add(enemy.GetInstanceID()))
                    Spear.Burst(enemy.transform.position, owner.skillCategory, skillID, 2, 0.3f);
        }
    }
}
