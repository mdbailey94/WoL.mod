using System.Collections.Generic;
using UnityEngine;

namespace WoLFlamingWhip
{
    // WoL Art's frames, all 16 px per unit, Point filter: the lash (#78 redraw, Log - Art
    // 2026-11-03 11:24: 8 x 64x12, handle at the left on row 6; an S-wave runs down it over frames
    // 0-5, then 6-7 snap straight with a spark at the tip), the crack burst (#78: 6 x 48x48,
    // centre 24,24) and the fire line floor tile (4 x 16x16, tiles along x).
    internal static class WhipArt
    {
        public static Sprite[] Lash, Crack, FireLine;

        public static void Load(BepInEx.Logging.ManualLogSource log)
        {
            Lash = Frames("whip_lash.png", 64, new Vector2(0f, 0.5f));
            Crack = Frames("whip_crack.png", 48, new Vector2(0.5f, 0.5f));
            FireLine = Frames("whip_fireline.png", 16, new Vector2(0f, 0.5f));
            log?.LogInfo($"Flaming Whip art: lash {Lash.Length}, crack {Crack.Length}, fire line {FireLine.Length} frames");
        }

        private static Sprite[] Frames(string file, int width, Vector2 pivot)
        {
            Texture2D texture = FlamingWhipPlugin.LoadTexture(file);
            if (texture == null || texture.width < width)
                return new Sprite[0];
            var frames = new Sprite[texture.width / width];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(texture, new Rect(i * width, 0, width, texture.height), pivot, 16f);
            return frames;
        }
    }

    internal static class Whip
    {
        // Hits 1 and 2: the game's fire burst on every enemy along the lash (a 0.3-unit burst on
        // each, so one burst per enemy reached).
        public static void Lash(Player owner, string skillID, Vector2 hand, Vector2 aim, bool enhanced)
        {
            float length = FlamingWhipPlugin.LashLength;
            int hits = 0;
            foreach (GameObject enemy in EnemiesAlong(hand, aim, length, FlamingWhipPlugin.LashWidth * 0.5f))
            {
                Burst(enemy.transform.position, owner.skillCategory, skillID, 1, 0.3f, true);
                hits++;
            }
            Anim.Play(WhipArt.Lash, hand, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, new Vector3(length / 4f, 1f, 1f), 16f, owner, false);
            if (enhanced)
                FireLine.Lay(owner, skillID, hand + aim * 0.5f, aim, length - 0.5f);
            FlamingWhipPlugin.Log($"Flaming Whip: lash ({length:0.0} u{(enhanced ? ", fire line" : "")}), {hits} hit");
        }

        // Hit 3: an overhead crack, the game's fire burst in a ring round the wizard. #78: a smaller
        // ring (1.8 u), with Art's bigger, brighter burst drawn on it and a sharp crack sound. The
        // burst's ring reaches ~23 px of its 24 by the last frame, so it's drawn so its outer edge
        // sits on the hit area's edge (Art's suggestion: 1.25x for 1.8 u).
        public static void Crack(Player owner, string skillID, Vector2 at)
        {
            float radius = FlamingWhipPlugin.CrackRadius;
            Burst(at, owner.skillCategory, skillID, 2, radius, true);
            float drawn = radius * 16f / 23f;
            Anim.Play(WhipArt.Crack, at, 0f, new Vector3(drawn, drawn, 1f), 16f, owner, false);
            Sound("ThunderClap", at, 1.9f);
            Sound("FlameSlam", at, 1.2f);
            FlamingWhipPlugin.Log($"Flaming Whip: crack ({radius:0.0} u)");
        }

        private static void Sound(string name, Vector2 at, float pitch)
        {
            try
            {
                SoundManager.PlayAudioWithDistance(name, at, null, 24f, -1f, pitch);
            }
            catch
            {
            }
        }

        // A burst's hit radius is its scale (ElementalBurst sets the collider radius to it).
        internal static void Burst(Vector2 at, string category, string skillID, int level, float radius, bool effects)
        {
            try
            {
                FlameBurst.CreateBurst(at, category, skillID, level, radius, effects);
            }
            catch (System.Exception e)
            {
                FlamingWhipPlugin.Log($"Flaming Whip: a burst failed: {e.Message}");
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

        public static void Play(Sprite[] frames, Vector2 at, float angle, Vector3 scale, float fps, Player owner, bool behind)
        {
            if (frames == null || frames.Length == 0)
                return;
            var go = new GameObject("FlamingWhip Fx");
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = scale;
            Anim a = go.AddComponent<Anim>();
            a.frames = frames;
            a.fps = fps;
            a.born = Time.time;
            a.look = go.AddComponent<SpriteRenderer>();
            a.look.sprite = frames[0];
            Whip.Sort(a.look, owner, behind ? -1 : 1);
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

    // Enhanced: a line of fire on the floor where a lash struck, hitting enemies on it every
    // FireLineTick for FireLineTime (level 3, no burst effect: the tiles are its look).
    internal class FireLine : MonoBehaviour
    {
        private Player owner;
        private string skillID;
        private Vector2 from, dir;
        private float length, born, nextTick;
        private readonly List<SpriteRenderer> tiles = new List<SpriteRenderer>();

        public static void Lay(Player owner, string skillID, Vector2 from, Vector2 dir, float length)
        {
            if (length <= 0.2f)
                return;
            var go = new GameObject("FlamingWhip FireLine");
            go.transform.position = from;
            FireLine line = go.AddComponent<FireLine>();
            line.owner = owner;
            line.skillID = skillID;
            line.from = from;
            line.dir = dir;
            line.length = length;
            line.born = Time.time;
            line.nextTick = Time.time + 0.1f;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            int count = Mathf.Max(1, Mathf.CeilToInt(length));
            for (int i = 0; i < count; i++)
            {
                var tile = new GameObject("Tile");
                tile.transform.SetParent(go.transform, false);
                tile.transform.position = from + dir * i;
                tile.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                SpriteRenderer r = tile.AddComponent<SpriteRenderer>();
                Whip.Sort(r, owner, -1);
                line.tiles.Add(r);
            }
            Destroy(go, FlamingWhipPlugin.FireLineTime);
        }

        private void Update()
        {
            if (WhipArt.FireLine.Length > 0)
            {
                int f = (int)((Time.time - born) * 10f) % WhipArt.FireLine.Length;
                for (int i = 0; i < tiles.Count; i++)
                    if (tiles[i] != null)
                        tiles[i].sprite = WhipArt.FireLine[(f + i) % WhipArt.FireLine.Length];
            }
            if (Time.time < nextTick || owner == null)
                return;
            nextTick = Time.time + FlamingWhipPlugin.FireLineTick;
            foreach (GameObject enemy in Whip.EnemiesAlong(from, dir, length, 0.3f))
                Whip.Burst(enemy.transform.position, owner.skillCategory, skillID, 3, 0.3f, false);
        }
    }
}
