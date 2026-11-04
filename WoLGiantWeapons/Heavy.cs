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
                Vector2 rel = (Vector2)go.transform.position - from;
                float d2 = rel.sqrMagnitude;
                if (d2 > r * r)
                    continue;
                float off = Vector2.Angle(dir, rel) - halfAngle;
                if (d2 < 0.01f || off <= 0f || (off < 90f && Mathf.Sqrt(d2) * Mathf.Sin(off * Mathf.Deg2Rad) <= BodyPad))
                    found.Add(go);
            }
            return found;
        }

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
            if (GameController.enemies == null)
                yield break;
            foreach (GameObject go in GameController.enemies)
                if (go != null && go.activeInHierarchy)
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

        // Runs `act` after `seconds` of game time (a pause holds it).
        public static void After(float seconds, Action act)
        {
            var go = new GameObject("GiantWeapons Delay");
            Delay d = go.AddComponent<Delay>();
            d.at = Time.time + seconds;
            d.act = act;
        }

        private class Delay : MonoBehaviour
        {
            public float at;
            public Action act;

            private void Update()
            {
                if (Time.time < at)
                    return;
                Destroy(gameObject);
                try { act?.Invoke(); }
                catch (Exception e) { GiantWeaponsPlugin.Log($"Giant weapons: a delayed hit failed: {e.Message}"); }
            }
        }
    }
}
