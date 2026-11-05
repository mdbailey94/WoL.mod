using System.Collections.Generic;
using UnityEngine;

namespace WoLGiantSpear
{
    // WoL Art's spear, 16 px per unit, Point filter, pointing right: the thrust (#44, 4 x 48x15)
    // and the giant thrown spear (Log - Art 13:48, 4 x 48x15: frame 0 the release burst, 1-3 wind
    // streaks and a sliding wind ring). The shaft runs from x 2 (the butt) to x 33 (the tip) on
    // row 7. Drawn at giant scale: big enough that the tip lands on the stab's reach.
    internal static class SpearArt
    {
        public const float ButtX = 2f, TipX = 33f, FrameWidth = 48f;
        public static Sprite[] Thrust, Throw;

        // #112 (0.3.0): WoL Art's air spear (the game's 3 air tones), drawn in 8 FACINGS so nothing
        // is rotated: [facing][frame], facing 0 right, 1 down-right, 2 down, 3 down-left, 4 left,
        // 5 up-left, 6 up, 7 up-right. Pivot = the front fist (Log - Art 13:48). Empty = fallback
        // to the old right-facing strip turned to the aim.
        public static readonly string[] FacingNames = { "right", "down_right", "down", "down_left", "left", "up_left", "up", "up_right" };
        public static Sprite[][] Thrust8 = new Sprite[8][], Gold8 = new Sprite[8][], Throw8 = new Sprite[8][];
        // Fist to tip of the thrust art, and the thrown lance's length, in pixels (for the scale).
        public const float ThrustLength = 25f, ThrowLength = 40f;

        public static void Load(BepInEx.Logging.ManualLogSource log)
        {
            Thrust = Frames("spear_thrust.png", 48, new Vector2(ButtX / FrameWidth, 0.5f));
            // The thrown spear turns round its middle.
            Throw = Frames("giant_spear_throw.png", 48, new Vector2((ButtX + TipX) / 2f / FrameWidth, 0.5f));
            int loaded = 0;
            for (int f = 0; f < 8; f++)
            {
                Thrust8[f] = Facing("spear_thrust_" + FacingNames[f] + ".png", f);
                Gold8[f] = Facing("spear_thrust_gold_" + FacingNames[f] + ".png", f);
                if (Thrust8[f].Length > 0)
                    loaded++;
            }
            Texture2D rows = GiantSpearPlugin.LoadTexture("giant_spear_throw_8dir.png");
            for (int f = 0; f < 8; f++)
            {
                Throw8[f] = new Sprite[0];
                if (rows == null || rows.width < 48 || rows.height < 8 * 48)
                    continue;
                int count = rows.width / 48;
                Throw8[f] = new Sprite[count];
                // Row f from the TOP (textures count rows from the bottom).
                float y = rows.height - (f + 1) * 48;
                for (int i = 0; i < count; i++)
                    Throw8[f][i] = Sprite.Create(rows, new Rect(i * 48, y, 48, 48), new Vector2(0.5f, 0.5f), 16f);
            }
            log?.LogInfo($"Giant Air Spear art: {loaded}/8 thrust facings, throw {(Throw8[0].Length > 0 ? "8 rows" : Throw.Length + " frames (turned)")}");
        }

        // The aim's facing, 45 degrees a sector (0 right, then clockwise on screen).
        public static int FacingOf(Vector2 aim)
        {
            float degrees = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            return ((Mathf.RoundToInt(-degrees / 45f) % 8) + 8) % 8;
        }

        // Up, up-left and up-right draw behind the wizard (Art's draw order).
        public static bool Behind(int facing) => facing >= 5;

        private static Sprite[] Facing(string file, int facing)
        {
            Texture2D texture = GiantSpearPlugin.LoadTexture(file);
            if (texture == null)
                return new Sprite[0];
            // Side strips 48x15, up/down 15x48, diagonals 48x48; the fist per Art's spec.
            int w, h;
            Vector2 fist;
            switch (facing)
            {
                case 0: w = 48; h = 15; fist = new Vector2(20, 7); break;
                case 4: w = 48; h = 15; fist = new Vector2(27, 7); break;
                case 2: w = 15; h = 48; fist = new Vector2(7, 20); break;
                case 6: w = 15; h = 48; fist = new Vector2(7, 27); break;
                default: w = 48; h = 48; fist = new Vector2(23.5f, 23.5f); break;
            }
            if (texture.width < w || texture.height != h)
                return new Sprite[0];
            var pivot = new Vector2((fist.x + 0.5f) / w, 1f - (fist.y + 0.5f) / h);
            var frames = new Sprite[texture.width / w];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(texture, new Rect(i * w, 0, w, h), pivot, 16f);
            return frames;
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
        // 0.2.0 (Matt: 'look more wind spell'): the spear is drawn as wind, a pale see-through cyan,
        // and leaves fading wind ghosts of itself behind as it moves.
        public static readonly Color WindTint = new Color(0.72f, 1f, 1f, 0.8f);
        private const float StabFps = 10f;

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
            int facing = SpearArt.FacingOf(aim);
            Sprite[] set = enhanced && SpearArt.Gold8[facing].Length > 0 ? SpearArt.Gold8[facing] : SpearArt.Thrust8[facing];
            float scale, angle;
            int order = SpearArt.Behind(facing) ? -1 : 1;
            if (set.Length > 0)
            {
                // #112: Art's facing, unturned; the fist on the hand, the tip at the reach.
                scale = reach * 16f / SpearArt.ThrustLength;
                angle = 0f;
            }
            else
            {
                set = SpearArt.Thrust;
                scale = SpearArt.ScaleFor(reach);
                angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
                order = 1;
            }
            Anim.Play(set, hand, angle, new Vector3(scale, scale, 1f), StabFps, owner, order);
            // Two wind ghosts trailing the thrust.
            if (set.Length > 0)
                for (int i = 1; i <= 2; i++)
                    Ghost.Leave(set[0], hand - aim * (0.45f * i), angle, new Vector3(scale, scale, 1f), owner, 0.5f / i, 0.3f);
            Trail(hand, hand + aim * reach);
            Sound("WindBlastSharp", hand, 0.75f);
            GiantSpearPlugin.Log($"Giant Air Spear: stab ({reach:0.0} u), {hits} hit");
        }

        // The game's own wind streaks (the "WindTrail" emitter its air arcana and the Air boss
        // use) along a line.
        internal static void Trail(Vector2 from, Vector2 to)
        {
            try
            {
                PoolManager.GetPoolItem<SectionedTrailEmitter>("WindTrail").Emit(from, to, -1, false, -1f, true, 0.3f);
            }
            catch
            {
            }
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

        public static void Play(Sprite[] frames, Vector2 at, float angle, Vector3 scale, float fps, Player owner, int order = 1)
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
            // #112: Art's air tones are drawn as they are (the old wooden spear was tinted to wind).
            a.look.color = SpearArt.Thrust8[0].Length > 0 ? Color.white : Spear.WindTint;
            Spear.Sort(a.look, owner, order);
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

    // A fading copy of a wind sprite left behind (the spear's wind ghosts).
    internal class Ghost : MonoBehaviour
    {
        private SpriteRenderer look;
        private float born, life, alpha;

        public static void Leave(Sprite sprite, Vector2 at, float angle, Vector3 scale, Player owner, float alpha, float life)
        {
            if (sprite == null)
                return;
            var go = new GameObject("GiantSpear Ghost");
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = scale;
            Ghost g = go.AddComponent<Ghost>();
            g.look = go.AddComponent<SpriteRenderer>();
            g.look.sprite = sprite;
            g.born = Time.time;
            g.life = life;
            g.alpha = alpha;
            Spear.Sort(g.look, owner, 0);
            g.Update();
        }

        private void Update()
        {
            float t = (Time.time - born) / life;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Color c = SpearArt.Thrust8[0].Length > 0 ? Color.white : Spear.WindTint;
            c.a = alpha * (1f - t);
            look.color = c;
        }
    }

    // Hit 3: the spear flies out along the aim, piercing (a wind burst on each enemy it passes),
    // and at its range blows away into wind (enhanced: a gust there). 0.2.0: it no longer comes
    // back (Matt); [Spear] ReturnToHand brings back 0.1's return, hitting again on the way.
    internal class ThrownSpear : MonoBehaviour
    {
        private const float MaxLife = 4f, CatchDistance = 0.6f, ReleaseTime = 0.08f, GhostEvery = 0.04f, FadeTime = 0.2f, TrailEvery = 0.1f;
        private Player owner;
        private string skillID;
        private Vector2 dir;
        private float range, born, distance, nextGhost, nextTrail, fadeFrom = -1f;
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
            bool rows = SpearArt.Throw8[0].Length >= 4;
            float scale = rows ? GiantSpearPlugin.StabReach * 16f / SpearArt.ThrowLength : SpearArt.ScaleFor(GiantSpearPlugin.StabReach);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            spear.look = go.AddComponent<SpriteRenderer>();
            if (SpearArt.Throw.Length > 0)
                spear.look.sprite = SpearArt.Throw[0];
            spear.look.color = rows ? Color.white : Spear.WindTint;
            Spear.Sort(spear.look, owner, 1);
            spear.Face(aim);
            Spear.Sound("WindBoomerang", hand, 0.8f);
            GiantSpearPlugin.Log($"Giant Air Spear: thrown ({spear.range:0.0} u{(enhanced ? ", gust" : "")})");
        }

        private int facing = -1;

        // #112: with Art's 8-row throw the row is picked by the flight's angle (no rotation);
        // the old single strip is turned to it.
        private void Face(Vector2 towards)
        {
            if (SpearArt.Throw8[0].Length >= 4)
            {
                facing = SpearArt.FacingOf(towards);
                transform.rotation = Quaternion.identity;
                if (look != null)
                    Spear.Sort(look, owner, SpearArt.Behind(facing) ? -1 : 1);
                return;
            }
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(towards.y, towards.x) * Mathf.Rad2Deg);
        }

        private Sprite[] Flight => facing >= 0 && SpearArt.Throw8[facing].Length >= 4 ? SpearArt.Throw8[facing] : SpearArt.Throw;

        private void Update()
        {
            if (owner == null || Time.time - born > MaxLife)
            {
                Destroy(gameObject);
                return;
            }
            // The release burst first, then the wind-streak frames 1-3 loop in flight.
            float age = Time.time - born;
            Sprite[] flight = Flight;
            if (flight.Length >= 4)
                look.sprite = age < ReleaseTime ? flight[0] : flight[1 + (int)((age - ReleaseTime) * 14f) % 3];
            if (Time.time >= nextGhost)
            {
                nextGhost = Time.time + GhostEvery;
                Ghost.Leave(look.sprite, transform.position, transform.eulerAngles.z, transform.localScale, owner, 0.45f, 0.22f);
            }
            if (Time.time >= nextTrail && fadeFrom < 0f)
            {
                nextTrail = Time.time + TrailEvery;
                Vector2 now = transform.position;
                Spear.Trail(now - (returning ? ((Vector2)owner.transform.position - now).normalized : dir) * 1.5f, now);
            }

            // Blowing away at the end of its flight: it drifts on a little, fading.
            if (fadeFrom >= 0f)
            {
                float t = (Time.time - fadeFrom) / FadeTime;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                Color c = SpearArt.Throw8[0].Length >= 4 ? Color.white : Spear.WindTint;
                c.a *= 1f - t;
                look.color = c;
                transform.position = (Vector2)transform.position + dir * GiantSpearPlugin.ThrowSpeed * 0.3f * Time.deltaTime;
                return;
            }

            Vector2 from = transform.position;
            Vector2 to;
            if (!returning)
            {
                float step = GiantSpearPlugin.ThrowSpeed * Time.deltaTime;
                to = from + dir * step;
                distance += step;
                if (distance >= range)
                {
                    if (enhanced)
                    {
                        Spear.Burst(to, owner.skillCategory, skillID, 3, GiantSpearPlugin.GustRadius);
                        Spear.Sound("WindBlastDeep", to, 1f);
                    }
                    if (GiantSpearPlugin.ReturnToHand)
                        returning = true;
                    else
                    {
                        fadeFrom = Time.time;
                        Spear.Sound("WindHowlShort", to, 1.2f);
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
