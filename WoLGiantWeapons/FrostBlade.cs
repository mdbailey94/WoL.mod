using System;
using UnityEngine;

namespace WoLGiantWeapons
{
    // The Giant Ice Sword's look (Matt: "reusing the ice boss's sword would be good"): the game's
    // own IceSword prefab, the ice boss's greatsword, which the game also lends a player arcana. It
    // spawns on a rotator at the hand ("IceSwordRotator", so its EndSwing takes the rotator with
    // it), aimed as the game aims it, then swings and shatters into ice. Its own hit box is
    // switched off: the arcana's hits stay IceSwordState's (the numbers Matt approved).
    internal static class FrostBlade
    {
        // 0.4.0 (#93): the blade turned by hand instead of the boss's own swing animation, so the
        // look matches the hit box: it starts `fromDeg` off the aim (+ = counterclockwise) and
        // turns `sweepDeg` (- = clockwise) over `time`, then shatters.
        public static void Sweep(Player p, string skillID, Vector2 aim, float fromDeg, float sweepDeg, float time) =>
            Show(p, skillID, aim, true, time, fromDeg, sweepDeg);

        // A swing along `aim` (or, spin = true, a turn of `sweepDeg` round the wizard over `spinTime`).
        public static void Show(Player p, string skillID, Vector2 aim, bool spin, float spinTime, float fromDeg = 0f, float sweepDeg = 720f)
        {
            try
            {
                GameObject prefab = IceSword.Prefab;
                if (prefab == null || p == null)
                    return;
                var rotator = new GameObject("IceSwordRotator").transform;
                rotator.position = Heavy.Hand(p);
                rotator.localEulerAngles = Globals.GetRotationVector(-aim);
                float scale = GiantWeaponsPlugin.SwordScale;
                // As the game does: mirrored unless the wizard faces left.
                rotator.localScale = new Vector3(p.facingDirection != FacingDirection.Left ? -scale : scale, scale, 1f);
                GameObject go = UnityEngine.Object.Instantiate(prefab, rotator, false);
                IceSword sword = go.GetComponent<IceSword>();
                if (sword == null)
                {
                    UnityEngine.Object.Destroy(rotator.gameObject);
                    return;
                }
                // Its Start reads the attack's category (Player: the player's Spawn animation).
                sword.attack.SetAttackInfo(p.skillCategory, skillID, 1);
                sword.attack.gameObject.SetActive(false);
                sword.initAnimTime = 0.5f;   // straight into the end of its appearing
                Driver d = rotator.gameObject.AddComponent<Driver>();
                d.sword = sword;
                d.spin = spin;
                d.spinTime = Mathf.Max(0.1f, spinTime);
                d.sweep = sweepDeg;
                d.aimDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
                d.fromDeg = fromDeg;
                d.turnFrom = rotator.localEulerAngles.z + fromDeg + GiantWeaponsPlugin.SwordAngleOffset;
                rotator.localEulerAngles = new Vector3(0f, 0f, d.turnFrom);
            }
            catch (Exception e)
            {
                GiantWeaponsPlugin.Log($"Giant Ice Sword: the ice boss's sword couldn't be shown ({e.Message})");
            }
        }

        // Plays the swing once it has appeared (or spins it), then shatters it; never lingers past
        // a few seconds.
        // 0.6.0 (#95, Matt: "swings the wrong directions"): the blade is steered by where it
        // actually IS, not by the rotator's angle. Each frame the blade's direction is measured
        // (hand to the middle of its picture) and the rotator is turned by the difference to the
        // wanted angle, so the prefab's own mirroring and animation can't flip the sweep: hit 1
        // goes clockwise and hit 2 counterclockwise for every aim. HEAVY: the angle follows
        // t^Momentum (slow start, gaining speed to the end), the same curve the hits use.
        private class Driver : MonoBehaviour
        {
            public IceSword sword;
            public bool spin;
            public float spinTime, turnFrom, sweep = 720f;
            public float aimDeg, fromDeg;
            private float born = -1f;
            private bool swinging;
            private SpriteRenderer blade;

            private void Update()
            {
                if (born < 0f)
                    born = Time.time;
                float age = Time.time - born;
                if (sword == null || age > 3f)
                {
                    if (sword != null)
                        sword.EndSwing();
                    else
                        Destroy(gameObject);
                    return;
                }
                if (spin)
                {
                    float t = Mathf.Clamp01(age / spinTime);
                    float want = aimDeg + fromDeg + sweep * GiantWeaponsPlugin.Ease(t) + GiantWeaponsPlugin.SwordAngleOffset;
                    Steer(want);
                    if (t >= 1f)
                        sword.EndSwing();
                    return;
                }
                if (!swinging)
                {
                    if (age < 0.06f)
                        return;
                    swinging = true;
                    sword.anim.speed = GiantWeaponsPlugin.SwordSwingSpeed;
                    sword.anim.Play("Swing", -1, 0f);
                    return;
                }
                if (sword.anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
                    sword.EndSwing();
            }

            // Turns the rotator so the blade points at `want` degrees (world, counterclockwise from
            // +x). Before the blade has a picture, the rotator's own angle is used as a guess.
            private void Steer(float want)
            {
                if (blade == null)
                {
                    float biggest = 0f;
                    foreach (SpriteRenderer r in sword.GetComponentsInChildren<SpriteRenderer>())
                    {
                        float size = r.bounds.size.sqrMagnitude;
                        if (r.enabled && r.sprite != null && size > biggest)
                        {
                            biggest = size;
                            blade = r;
                        }
                    }
                }
                Vector2 d = blade != null ? (Vector2)(blade.bounds.center - transform.position) : Vector2.zero;
                var e = transform.localEulerAngles;
                if (d.sqrMagnitude < 0.04f)
                {
                    transform.localEulerAngles = new Vector3(e.x, e.y, turnFrom + (want - aimDeg - fromDeg));
                    return;
                }
                float now = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                transform.localEulerAngles = new Vector3(e.x, e.y, e.z + Mathf.DeltaAngle(now, want));
            }
        }
    }
}
