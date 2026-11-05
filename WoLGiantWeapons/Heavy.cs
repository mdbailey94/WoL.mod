using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoLGiantWeapons
{
    // What the giant weapons share: the heavy pace (the wizard's animation slowed while the weapon
    // swings), the game's elemental bursts as hits (with the arcana's stats, so gear, crits,
    // statuses and talents apply), finding enemies in an arc or a line, sounds and delays.
    internal static class Heavy
    {
        // The wizard's animation runs at `pace` while on (1 = normal). Its own ID per weapon.
        public static void Pace(Player p, ref NumVarStatMod mod, string id, float pace, ref bool on, bool want)
        {
            try
            {
                if (p == null || p.animSpeedHandler == null || on == want)
                    return;
                if (mod == null)
                    mod = new NumVarStatMod(id, pace - 1f, 10, VarStatModType.Multiplicative);
                // 0.8.0 (#101 cleanup): the setting is read again each attack (it was cached, so a
                // changed Pace only took effect after a restart).
                if (want)
                    mod.modValue = pace - 1f;
                p.animSpeedHandler.Modify(mod, want);
                on = want;
            }
            catch (Exception e)
            {
                GiantWeaponsPlugin.Log($"Giant weapons: the heavy pace failed: {e.Message}");
            }
        }

        public static Vector2 Aim(Player.SkillState s, Player p)
        {
            Vector2 aim = s.inputVector != Vector2.zero ? s.inputVector.normalized : Entity.GetFacingDirectionVector(p.facingDirection).normalized;
            return aim == Vector2.zero ? Vector2.right : aim;
        }

        public static Vector2 Hand(Player p) =>
            p.attackOriginTrans != null ? (Vector2)p.attackOriginTrans.position : (Vector2)p.transform.position;

        public static void Ice(Vector2 at, Player p, string id, int level, float radius)
        {
            try { IceBurst.CreateBurst(at, p.skillCategory, id, level, radius); }
            catch (Exception e) { GiantWeaponsPlugin.Log($"Giant weapons: an ice hit failed: {e.Message}"); }
        }

        public static void Bolt(Vector2 at, Player p, string id, int level, float radius)
        {
            try { LightningBurst.CreateBurst(at, p.skillCategory, id, level, radius); }
            catch (Exception e) { GiantWeaponsPlugin.Log($"Giant weapons: a lightning hit failed: {e.Message}"); }
        }

        // Live enemies within `reach` of `from` and within halfAngle degrees of `dir`.
        // 0.7.0 (#101): with BodyPad for the enemy's body, on the reach and on the arc's edges, so
        // a big enemy the blade visibly crosses isn't missed because its centre is just outside.
        public const float BodyPad = 0.3f;

        public static List<GameObject> InArc(Vector2 from, Vector2 dir, float reach, float halfAngle)
        {
            var found = new List<GameObject>();
            float r = reach + BodyPad;
            foreach (GameObject go in Enemies())
            {
                Vector2 rel = Body(go) - from;
                float d2 = rel.sqrMagnitude;
                if (d2 > r * r)
                    continue;
                float off = Vector2.Angle(dir, rel) - halfAngle;
                if (d2 < 0.01f || off <= 0f || (off < 90f && Mathf.Sqrt(d2) * Mathf.Sin(off * Mathf.Deg2Rad) <= BodyPad))
                    found.Add(go);
            }
            return found;
        }

        // 0.9.0 (Matt: "the flail was not hitting enemies as expected"): where an enemy's body
        // really is. transform.position is the FEET pivot, while the hurt box sits up on the body,
        // so a small burst placed at the feet often missed it (the log showed 2 flail hits in 4
        // swings among 5 enemies). This is the centre of the enemy's hurt-box collider (its
        // 'EnemyHurtBox' layer), else the middle of its picture, else its pivot. Cached per enemy.
        private static readonly Dictionary<int, Collider2D> hurtBoxes = new Dictionary<int, Collider2D>();
        private static int hurtLayer = -2;

        public static Vector2 Body(GameObject go)
        {
            if (go == null)
                return Vector2.zero;
            int id = go.GetInstanceID();
            Collider2D box;
            if (!hurtBoxes.TryGetValue(id, out box) || (box == null && hurtBoxes.Count > 512))
            {
                if (hurtBoxes.Count > 512)
                    hurtBoxes.Clear();
                if (hurtLayer == -2)
                    hurtLayer = LayerMask.NameToLayer("EnemyHurtBox");
                box = null;
                foreach (Collider2D c in go.GetComponentsInChildren<Collider2D>())
                    if (c != null && (hurtLayer < 0 || c.gameObject.layer == hurtLayer))
                    {
                        box = c;
                        if (hurtLayer >= 0)
                            break;
                    }
                hurtBoxes[id] = box;
            }
            if (box != null && box.enabled)
                return box.bounds.center;
            var e = go.GetComponent<Entity>();
            if (e != null && e.spriteRenderer != null)
                return e.spriteRenderer.bounds.center;
            return go.transform.position;
        }

        // The burst on each enemy found: on its body, wide enough to touch the hurt box.
        public const float OnBodyRadius = 0.35f;

        // A delayed hit only lands while its wizard is alive (not after a death or a co-op down).
        public static bool Alive(Player p) => p != null && p.health != null && p.health.CurrentHealthValue > 0;

        // Live enemies within halfWidth (+ a little for their body) of the segment.
        public static List<GameObject> AlongLine(Vector2 from, Vector2 dir, float length, float halfWidth)
        {
            var found = new List<GameObject>();
            foreach (GameObject go in Enemies())
            {
                Vector2 rel = (Vector2)go.transform.position - from;
                float along = Vector2.Dot(rel, dir);
                if (along < -0.2f || along > length + 0.3f)
                    continue;
                if (Mathf.Abs(rel.x * dir.y - rel.y * dir.x) <= halfWidth + 0.3f)
                    found.Add(go);
            }
            return found;
        }

        public static List<GameObject> Near(Vector2 at, float reach) => InArc(at, Vector2.right, reach, 180f);

        private static IEnumerable<GameObject> Enemies()
        {
            // 0.14.0: bosses too, in case one isn't on the enemy list (each enemy once).
            if (GameController.enemies != null)
                foreach (GameObject go in GameController.enemies)
                    if (go != null && go.activeInHierarchy)
                        yield return go;
            if (GameController.bosses != null)
                foreach (GameObject go in GameController.bosses)
                    if (go != null && go.activeInHierarchy && (GameController.enemies == null || !GameController.enemies.Contains(go)))
                        yield return go;
        }

        public static void Sound(string name, Vector2 at, float pitch)
        {
            try { SoundManager.PlayAudioWithDistance(name, at, null, 24f, -1f, pitch); }
            catch { }
        }

        public static void Shake(float strength)
        {
            try { CameraController.ShakeCamera(strength); }
            catch { }
        }

        public static void Dust(Vector2 at, int count, float radius)
        {
            try { PoolManager.GetPoolItem<DustEmitter>().EmitCircle(count, radius, -3f, -1f, new Vector3?(at), null); }
            catch { }
        }

        // Runs `act` after `seconds` of game time (a pause holds it). 0.8.0 (#101 cleanup): one
        // runner per scene holds every pending hit (it used to be a new GameObject per hit, 4-5 a
        // spin); it lives in the scene, so pending hits are dropped on a scene change as before.
        public static void After(float seconds, Action act)
        {
            if (runner == null)
                runner = new GameObject("GiantWeapons Delays").AddComponent<Delays>();
            runner.Add(Time.time + seconds, act);
        }

        private static Delays runner;

        private class Delays : MonoBehaviour
        {
            private readonly List<float> at = new List<float>();
            private readonly List<Action> acts = new List<Action>();
            private readonly List<Action> due = new List<Action>();

            public void Add(float when, Action act)
            {
                at.Add(when);
                acts.Add(act);
            }

            private void Update()
            {
                if (at.Count == 0)
                    return;
                float now = Time.time;
                due.Clear();
                for (int i = at.Count - 1; i >= 0; i--)
                    if (now >= at[i])
                    {
                        due.Add(acts[i]);
                        at.RemoveAt(i);
                        acts.RemoveAt(i);
                    }
                for (int i = due.Count - 1; i >= 0; i--)
                {
                    try { due[i]?.Invoke(); }
                    catch (Exception e) { GiantWeaponsPlugin.Log($"Giant weapons: a delayed hit failed: {e.Message}"); }
                }
            }
        }

        // Crates, pots, plants and the like (the game's Destructible) inside the same arc, by their
        // colliders (0.8.0, #101): the hits used to search enemies only, so these broke only when a
        // burst on an enemy happened to reach them. Each is returned once, at its collider's centre.
        private static readonly HashSet<int> seenProps = new HashSet<int>();

        public static List<Vector2> PropsInArc(Vector2 from, Vector2 dir, float reach, float halfAngle)
        {
            var found = new List<Vector2>();
            seenProps.Clear();
            Collider2D[] hits;
            try { hits = Physics2D.OverlapCircleAll(from, reach + BodyPad); }
            catch { return found; }
            foreach (Collider2D c in hits)
            {
                if (c == null)
                    continue;
                Destructible d = c.GetComponentInParent<Destructible>();
                if (d == null || d.destroyed || !seenProps.Add(d.GetInstanceID()))
                    continue;
                Vector2 at = c.bounds.center;
                Vector2 rel = at - from;
                float off = Vector2.Angle(dir, rel) - halfAngle;
                if (rel.sqrMagnitude < 0.01f || off <= 0f || (off < 90f && rel.magnitude * Mathf.Sin(off * Mathf.Deg2Rad) <= BodyPad))
                    found.Add(at);
            }
            return found;
        }
    }
}
