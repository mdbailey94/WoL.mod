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
        // #91 v3 "spell look" (Log - Art 2026-11-03 21:38, to Log - Modder 18:55), drawn aiming
        // right: whip_windup 6 x 48x48, pivot (24,32) = the hand; whip_arc 12 x 80x48, pivot
        // (20,32), the tip strikes (68,32) on frame 2; flamingwhip_crack_big 8 x 32x32 (ring = the
        // 1-tile spot) and _enh 8 x 40x40 (the 1.5-tile spot). Any missing: the 0.4.0 look.
        public static Sprite[] WindUp, Arc, CrackBig, CrackBigEnh, CrackHuge, Sigil, SigilEnh;
        public const float StrikeFrame = 2f, StripFps = 25f, ArcDrawnFor = 3f;

        public static void Load(BepInEx.Logging.ManualLogSource log)
        {
            Lash = Frames("whip_lash.png", 64, new Vector2(0f, 0.5f));
            Crack = Frames("whip_crack.png", 48, new Vector2(0.5f, 0.5f));
            FireLine = Frames("whip_fireline.png", 16, new Vector2(0f, 0.5f));
            // Pivots are counted from the bottom in Unity: row 32 of 48 from the top = 16 up.
            WindUp = Frames("whip_windup.png", 48, new Vector2(24f / 48f, 16f / 48f));
            Arc = Frames("whip_arc.png", 80, new Vector2(20f / 80f, 16f / 48f));
            CrackBig = Frames("flamingwhip_crack_big.png", 32, new Vector2(0.5f, 0.5f));
            CrackBigEnh = Frames("flamingwhip_crack_big_enh.png", 40, new Vector2(0.5f, 0.5f));
            // Optional, for the 2-tile enhanced spot (#98): 8 x 48x48, ring radius 16 px.
            CrackHuge = Frames("flamingwhip_crack_huge.png", 48, new Vector2(0.5f, 0.5f));
            // #91 sigil (approved by Matt 09:36): a fire rune ring on the floor at the spot that
            // draws itself over the wind-up; 6 x 20x12 (rx 8 = the 1-tile spot) and 6 x 28x16 (rx 12).
            Sigil = Frames("flamingwhip_sigil.png", 20, new Vector2(0.5f, 0.5f));
            SigilEnh = Frames("flamingwhip_sigil_enh.png", 28, new Vector2(0.5f, 0.5f));
            log?.LogInfo($"Flaming Whip art: lash {Lash.Length}, crack {Crack.Length}, fire line {FireLine.Length}, v3 wind-up {WindUp.Length}, arc {Arc.Length}, big crack {CrackBig.Length}/{CrackBigEnh.Length} frames");
        }

        public static bool HasWindUp => WindUp.Length == 6;
        public static bool HasArc => Arc.Length == 12;

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
        // #81: the press marks the spot, CrackDistance out along the aim from the hand; the crack
        // lands there WindUp seconds later (game time, so a pause doesn't fire it).
        public static void WindUp(Player owner, string skillID, Vector2 hand, Vector2 aim, bool enhanced)
        {
            var go = new GameObject("FlamingWhip WindUp");
            Pending p = go.AddComponent<Pending>();
            p.owner = owner;
            p.skillID = skillID;
            p.hand = hand;
            p.aim = aim;
            p.enhanced = enhanced;
            p.at = Time.time + FlamingWhipPlugin.WindUp;
            Sound("WindSlashBasic", hand, 0.35f, 0.7f);
            // 0.4.0 (Matt: "more spell like"): fire gathers at the hand while it winds up.
            Flames("FlameCircleBurstEmitter", 4, hand);
            // 0.5.0 (#91): Art's v3 strips. The arc starts so its strike frame (2) lands exactly
            // when the crack fires; the wind-up fills the time before that and holds its last frame.
            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            bool left = aim.x < 0f;
            bool behind = aim.y < -0.4f;   // aiming down-screen: the coil sits behind the hood
            float arcStart = Mathf.Max(0f, FlamingWhipPlugin.WindUp - WhipArt.StrikeFrame / WhipArt.StripFps);
            if (WhipArt.HasWindUp && arcStart > 0.02f)
                Anim.Play(WhipArt.WindUp, hand, angle, new Vector3(1f, left ? -1f : 1f, 1f), Mathf.Max(WhipArt.StripFps, 6f / arcStart), owner, behind, arcStart);
            // The sigil marks where the crack will land, drawing itself until the strike.
            float spotRadius = (enhanced ? FlamingWhipPlugin.EnhancedSpotSize : FlamingWhipPlugin.SpotSize) * 0.5f;
            Sprite[] sigil = enhanced && WhipArt.SigilEnh.Length == 6 ? WhipArt.SigilEnh : WhipArt.Sigil;
            if (sigil.Length == 6)
            {
                float native = sigil == WhipArt.SigilEnh ? 0.75f : 0.5f;   // its ring's x radius in units
                float k = Mathf.Max(1f, Mathf.Round(spotRadius / native * 2f) / 2f);   // whole or half steps
                float wind = Mathf.Max(0.05f, FlamingWhipPlugin.WindUp);
                Anim.Play(sigil, hand + aim * FlamingWhipPlugin.CrackDistance, 0f, new Vector3(k, k, 1f), 6f / wind, owner, true, wind);
            }
            if (WhipArt.HasArc)
            {
                float stretch = FlamingWhipPlugin.CrackDistance / WhipArt.ArcDrawnFor;
                Anim.Play(WhipArt.Arc, hand, angle, new Vector3(stretch, left ? -1f : 1f, 1f), WhipArt.StripFps, owner, false, 0f, arcStart);
            }
        }

        private class Pending : MonoBehaviour
        {
            public Player owner;
            public string skillID;
            public Vector2 hand, aim;
            public bool enhanced;
            public float at;

            private void Update()
            {
                if (Time.time < at)
                    return;
                Destroy(gameObject);
                // 0.6.1 (#101): no crack after the wizard died or went down in co-op.
                if (owner != null && owner.health != null && owner.health.CurrentHealthValue > 0)
                    Crack(owner, skillID, hand, aim, enhanced);
            }
        }

        // The crack: the game's fire burst on a spot SpotSize wide (enhanced EnhancedSpotSize)
        // exactly CrackDistance out, and nothing between the wizard and it, so you can be too
        // close or too far. #86: heavy: a camera shake, a crack, and the lash drawn out to the
        // spot with Art's burst on it (drawn so its ring's edge, ~23 px of 24 by the last frame,
        // sits on the hit area's edge). 0.4.0 (Matt: "extend range, reduce sound, more spell
        // like"): 3 tiles out, quieter (no low thud, the crack and flame at about half volume),
        // and flames burst along the lash's path and round the spot, leaving a scorch mark.
        public static void Crack(Player owner, string skillID, Vector2 hand, Vector2 aim, bool enhanced)
        {
            float distance = FlamingWhipPlugin.CrackDistance;
            float radius = (enhanced ? FlamingWhipPlugin.EnhancedSpotSize : FlamingWhipPlugin.SpotSize) * 0.5f;
            Vector2 spot = hand + aim * distance;
            Burst(spot, owner.skillCategory, skillID, 1, radius, true);
            if (!WhipArt.HasArc)
                Anim.Play(WhipArt.Lash, hand, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, new Vector3((distance + radius) / 4f, 1.5f, 1f), 20f, owner, false);
            // 0.5.0 (#91): Art's native big crack (ring = the spot, so 1x at the default sizes).
            // 0.6.0 (#98): the crack strip whose ring matches the spot best (ring radius 0.5 /
            // 0.75 / 1 u), so the default spots draw 1:1: 1.5 tiles uses the _enh strip, 2 tiles
            // the _huge one (until Art draws it, _enh at 1.33x).
            Sprite[] big;
            float native;
            PickCrack(radius, out big, out native);
            if (big.Length == 8)
            {
                float s = radius / native;
                Anim.Play(big, spot, 0f, new Vector3(s, s, 1f), 24f, owner, false);
            }
            else
            {
                float drawn = radius * 16f / 23f;
                Anim.Play(WhipArt.Crack, spot, 0f, new Vector3(drawn, drawn, 1f), 16f, owner, false);
            }
            for (float d = 0.6f; d < distance - 0.3f; d += 0.6f)
                Flames("FlameCircleBurstEmitter", 2, hand + aim * d);
            Flames("FlameCircleBurstEmitter", 10, spot);
            Flames("BurnMarkMedEmitter", 1, spot);
            try
            {
                CameraController.ShakeCamera(FlamingWhipPlugin.Shake);
            }
            catch
            {
            }
            Sound("ThunderClap", spot, 0.35f, 1.9f);
            Sound("FlameSlam", spot, 0.5f, 1f);
            // Enhanced: a short line of fire across the spot.
            if (enhanced)
                FireLine.Lay(owner, skillID, spot - aim * radius, aim, radius * 2f);
            FlamingWhipPlugin.Log($"Flaming Whip: crack {distance:0.0} u out, {radius * 2f:0.0} u spot{(enhanced ? ", fire line" : "")}");
        }

        private static void PickCrack(float radius, out Sprite[] frames, out float native)
        {
            frames = new Sprite[0];
            native = 0.5f;
            float best = float.MaxValue;
            Try(WhipArt.CrackBig, 0.5f, radius, ref frames, ref native, ref best);
            Try(WhipArt.CrackBigEnh, 0.75f, radius, ref frames, ref native, ref best);
            Try(WhipArt.CrackHuge, 1f, radius, ref frames, ref native, ref best);
        }

        private static void Try(Sprite[] set, float ring, float radius, ref Sprite[] frames, ref float native, ref float best)
        {
            if (set == null || set.Length != 8)
                return;
            float off = Mathf.Abs(Mathf.Log(radius / ring));
            if (off < best)
            {
                best = off;
                frames = set;
                native = ring;
            }
        }

        private static void Sound(string name, Vector2 at, float volume, float pitch)
        {
            try
            {
                SoundManager.PlayAudioWithDistance(name, at, null, 24f, volume, pitch);
            }
            catch
            {
            }
        }

        // One of the game's pooled particle effects (its fire bursts and scorch marks).
        private static void Flames(string pool, int count, Vector2 at)
        {
            try
            {
                PoolManager.GetPoolItem<ParticleEffect>(pool).Emit(count, (Vector3?)(Vector3)at);
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
        private float fps, born, holdUntil;
        private SpriteRenderer look;

        // holdUntil > 0: the last frame stays up until that many seconds after the start;
        // delay > 0: it starts that many seconds from now (hidden until then).
        public static void Play(Sprite[] frames, Vector2 at, float angle, Vector3 scale, float fps, Player owner, bool behind, float holdUntil = 0f, float delay = 0f)
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
            a.born = Time.time + delay;
            a.holdUntil = holdUntil;
            a.look = go.AddComponent<SpriteRenderer>();
            a.look.sprite = frames[0];
            a.look.enabled = delay <= 0f;
            Whip.Sort(a.look, owner, behind ? -1 : 1);
        }

        private void Update()
        {
            float age = Time.time - born;
            if (age < 0f)
                return;
            look.enabled = true;
            int f = (int)(age * fps);
            if (f >= frames.Length)
            {
                if (age < holdUntil)
                {
                    look.sprite = frames[frames.Length - 1];
                    return;
                }
                Destroy(gameObject);
                return;
            }
            look.sprite = frames[f];
        }
    }

    // Enhanced: a line of fire on the floor where a lash struck, hitting enemies on it every
    // FireLineTick for FireLineTime (level 2, no burst effect: the tiles are its look).
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
                Whip.Burst(enemy.transform.position, owner.skillCategory, skillID, 2, 0.3f, false);
        }
    }
}
