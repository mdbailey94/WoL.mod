using System.Collections.Generic;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Charged Leap (Lightning): while you hold, the game's Mag Sphere forms on you and works
    // exactly as the arcana does (its own look and its own handling of projectiles; we don't touch
    // the sphere or the projectiles), only smaller (Balance.StormSphereSize, through a scaled holder
    // so the sphere's own sizing is left alone). Whatever it holds can't hit you or your allies
    // while you charge (StormGuard). Let go and the sphere ends and everything it held fires in a
    // fan along your aim while you dash after it, with a small lightning burst where you land.
    //
    // Without a Mag Sphere, a drawn ring stands in and our own catching spins projectiles round you.
    public class StormSlingshotState : ChargedDashState
    {
        public new static string staticID = "StormSlingshot";

        private const float DefaultSphereRadius = 2.5f; // if its collider can't be measured
        private const int MaxCaught = 20;
        private const float MinFieldRadius = 2f;
        private const float ExtraFieldRadius = 1.5f;
        private const float OrbitRadius = 1.3f;
        private const float OrbitSpeed = 4f;
        private const float LaunchSpeed = 22f;
        private const float FanDegrees = 8f;
        private const float ReflectDamage = 1.5f;
        private const float ScanInterval = 0.05f;
        private const float CrackleInterval = 0.04f;
        private float nextCrackle;
        // Past this share of the sphere's radius, a held projectile heading out is turned back
        // along its orbit (the smaller sphere can't always bend fast ones round by itself).
        private const float KeepInFrom = 0.75f;

        private MagSphere sphere;
        private GameObject sphereHolder;
        private int seenThisCharge;
        private static bool loggedCatch;
        // Projectiles the sphere holds (left as they are; StormGuard keeps them off your side).
        private readonly HashSet<Projectile> held = new HashSet<Projectile>();
        // Enemy projectiles seen coming (so they're still held if the sphere makes them yours first).
        private readonly HashSet<Projectile> incoming = new HashSet<Projectile>();
        // Fallback: projectiles caught and turned to your side.
        private readonly HashSet<Projectile> converted = new HashSet<Projectile>();
        private static bool loggedSphere;

        // Fallback catching (no Mag Sphere).
        private readonly List<Projectile> caught = new List<Projectile>();
        private readonly Dictionary<Projectile, float> speeds = new Dictionary<Projectile, float>();
        private Projectile[] nearby = new Projectile[0];
        private float nextScan;
        private float orbitAngle;
        private StaticRing ring;

        public StormSlingshotState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
        }

        protected override void OnChargeStarted()
        {
            converted.Clear();
            ReleaseHeld();
            caught.Clear();
            seenThisCharge = 0;
            speeds.Clear();
            orbitAngle = 0f;
            nextScan = 0f;
            sphere = SpawnSphere();
            if (sphere == null)
                ring = StaticRing.Create(parent);
        }

        private MagSphere SpawnSphere()
        {
            try
            {
                // The sphere goes inside a scaled holder: that makes it smaller without changing
                // anything on the sphere itself (it can size and animate itself as it likes).
                sphereHolder = new GameObject("StormSlingshotSphere");
                sphereHolder.transform.position = parent.transform.position;
                float size = SlingshotDashPlugin.StormSphereSize;
                sphereHolder.transform.localScale = new Vector3(size, size, 1f);
                MagSphere ms = ChaosInst<MagSphere>(MagSphere.Prefab, new Vector2?(parent.transform.position), null,
                    sphereHolder.transform);
                if (ms == null)
                {
                    RemoveSphere();
                    return null;
                }
                // What the arcana gives its sphere: who it belongs to and what it follows.
                ms.parentPlayer = parent;
                ms.parentObject = parent.gameObject;
                ms.followTrans = parent.transform;
                ms.skillCategory = parent.skillCategory;
                ms.duration = 60f;
                if (ms.damageModifier <= 0f)
                    ms.damageModifier = 1f;
                if (!loggedSphere)
                {
                    loggedSphere = true;
                    SlingshotDashPlugin.Log($"Storm: charging with a Mag Sphere at {size:0.##}x size " +
                        $"(damageModifier {ms.damageModifier}, balanceBias {ms.balanceBias}, " +
                        $"invertedRadius {ms.invertedRadius}, empowered {ms.isEmpowered})");
                }
                return ms;
            }
            catch (System.Exception e)
            {
                RemoveSphere();
                if (!loggedSphere)
                {
                    loggedSphere = true;
                    SlingshotDashPlugin.Log($"Storm: Mag Sphere unavailable, using our own field: {e.Message}");
                }
                return null;
            }
        }

        protected override void WhileCharging(float holdTime)
        {
            Vector2 center = parent.transform.position;
            if (sphere != null)
            {
                // Leave the sphere to do its thing; just keep what it holds off your side.
                HoldInside(sphere.transform.position, SphereRadius());
                return;
            }
            float power = Mathf.Clamp01((holdTime - MinChargeTime) / (1f - MinChargeTime));
            float field = MinFieldRadius + ExtraFieldRadius * power;
            ring?.Draw(center, field);
            CatchAndOrbit(center, field);
        }

        protected override void OnLaunch(float charge)
        {
            Vector2 aim = inputVector.sqrMagnitude > 0.01f
                ? inputVector.normalized
                : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            Vector2 center = parent.transform.position;
            var toFire = new List<Projectile>(caught);
            if (sphere != null)
            {
                Vector2 sphereCenter = sphere.transform.position;
                float radius = SphereRadius();
                foreach (Projectile p in held)
                {
                    if (!toFire.Contains(p))
                        toFire.Add(p);
                }
                foreach (Projectile p in Object.FindObjectsOfType<Projectile>())
                {
                    if (p != null && p.gameObject.activeInHierarchy && !toFire.Contains(p)
                        && Vector2.Distance(p.transform.position, sphereCenter) <= radius)
                        toFire.Add(p);
                }
            }
            RemoveSphere();
            RemoveRing();
            ReleaseHeld();
            if (!loggedCatch)
            {
                loggedCatch = true;
                SlingshotDashPlugin.Log($"Storm: fired {toFire.Count} projectile(s) " +
                    $"({seenThisCharge} caught while charging, {nearby.Length} in the room)");
            }
            Fan(toFire, center, aim);
            // Flair only: a lightning flash along the leap, sparks and a shake.
            Effects.LightningFlash(center, aim);
            Effects.Spark(center, HitSparkType.Medium);
            Effects.Shake(0.6f + 0.4f * charge);
            nextCrackle = 0f;
            caught.Clear();
            converted.Clear();
            held.Clear();
            speeds.Clear();
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(center), null, 24f, -1f, 1.6f, false);
        }

        // Flair only: crackling sparks behind you through the leap.
        protected override void WhileDashing(float charge)
        {
            if (Time.time < nextCrackle)
                return;
            nextCrackle = Time.time + CrackleInterval;
            Effects.Spark(parent.transform.position, HitSparkType.Small);
        }

        protected override void OnLand(float charge)
        {
            Effects.Spark(parent.transform.position, HitSparkType.Large);
            Effects.Shake(0.5f + 0.3f * charge);
            // Enhanced: half as big again.
            float scale = (1.2f + 0.4f * charge) * (IsEmpowered ? 1.5f : 1f);
            LightningBurst.CreateBurst(parent.transform.position, parent.skillCategory, skillID, 1, scale, false);
        }

        protected override void OnStateExit()
        {
            RemoveSphere();
            RemoveRing();
            ReleaseHeld();
            // Let go without launching (e.g. hit): fling the fallback's caught projectiles outward.
            foreach (Projectile p in caught)
            {
                if (p == null || !p.gameObject.activeInHierarchy)
                    continue;
                float speed;
                p.moveSpeed = speeds.TryGetValue(p, out speed) ? speed : LaunchSpeed;
                Send(p, p.transform.position, ((Vector2)p.transform.position - (Vector2)parent.transform.position).normalized);
            }
            caught.Clear();
            speeds.Clear();
        }

        // Anything not yours that comes inside the sphere is held: left exactly as it is for the
        // sphere, but it can't hit you or your allies until you let go.
        private void HoldInside(Vector2 center, float radius)
        {
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + ScanInterval;
                nearby = Object.FindObjectsOfType<Projectile>();
            }
            foreach (Projectile p in nearby)
            {
                if (p == null || !p.gameObject.activeInHierarchy || held.Contains(p))
                    continue;
                float distance = Vector2.Distance(p.transform.position, center);
                if (!(p.parentEntity is Player) && distance <= radius * 2f)
                    incoming.Add(p);
                if (distance > radius || !incoming.Contains(p))
                    continue;
                held.Add(p);
                StormGuard.Hold(p, parent);
                seenThisCharge++;
            }
            held.RemoveWhere(p => p == null || !p.gameObject.activeInHierarchy);
            incoming.RemoveWhere(p => p == null || !p.gameObject.activeInHierarchy);
            foreach (Projectile p in held)
                KeepInOrbit(p, center, radius);
        }

        // A held projectile near the edge and heading out is turned along its orbit and a little
        // inward, the way it's already circling (so it never fights the sphere's own spin).
        private static void KeepInOrbit(Projectile p, Vector2 center, float radius)
        {
            Vector2 offset = (Vector2)p.transform.position - center;
            float distance = offset.magnitude;
            Vector2 move = p.moveVector;
            if (distance < radius * KeepInFrom || distance < 0.01f || move.sqrMagnitude < 0.0001f
                || Vector2.Dot(move, offset) <= 0f)
                return;
            Vector2 outward = offset / distance;
            float spin = outward.x * move.y - outward.y * move.x;
            Vector2 tangent = spin >= 0f ? new Vector2(-outward.y, outward.x) : new Vector2(outward.y, -outward.x);
            float inwardPull = 0.3f + 2f * Mathf.Clamp01((distance / radius - KeepInFrom) / (1f - KeepInFrom));
            Vector2 direction = (tangent - outward * inwardPull).normalized;
            p.moveVector = direction * move.magnitude;
            if (distance > radius)
                p.transform.position = center + outward * radius;
            if (p.rigidbody2D != null)
            {
                if (distance > radius)
                    p.rigidbody2D.position = p.transform.position;
                p.rigidbody2D.velocity = direction * p.rigidbody2D.velocity.magnitude;
            }
        }

        private void ReleaseHeld()
        {
            foreach (Projectile p in held)
                StormGuard.Release(p);
            StormGuard.ReleaseAll(parent);
            held.Clear();
            incoming.Clear();
        }

        private float SphereRadius()
        {
            try
            {
                Collider2D c = sphere.mainCollider != null ? sphere.mainCollider : sphere.GetComponentInChildren<Collider2D>();
                if (c != null)
                    return Mathf.Max(0.5f, Mathf.Max(c.bounds.extents.x, c.bounds.extents.y));
            }
            catch
            {
            }
            return DefaultSphereRadius;
        }

        // The game's reflect (a parry's switch of sides); for projectiles that can't be reflected,
        // their attack is re-aimed at enemies with this arcana's own stats instead.
        private void TurnToYourSide(Projectile p, Vector2 direction)
        {
            bool reflected = false;
            if (!p.ignoreReflect)
            {
                try
                {
                    reflected = p.OnReflect(direction, ReflectDamage);
                }
                catch
                {
                }
            }
            try
            {
                // Can't be reflected: aim its attack at enemies with this arcana's stats.
                if (!reflected && p.attackBox != null)
                    p.attackBox.SetAttackInfo(parent.skillCategory, skillID, 1, false);
                // Either way it's yours now, so it never hits its owner (you).
                p.parentEntity = parent;
                p.parentObject = parent.gameObject;
            }
            catch
            {
            }
        }

        // Every projectile turned to your side and sent along the aim, spread in a fan.
        private void Fan(List<Projectile> projectiles, Vector2 center, Vector2 aim)
        {
            projectiles.RemoveAll(p => p == null || !p.gameObject.activeInHierarchy);
            int n = projectiles.Count;
            Vector2 side = Quaternion.Euler(0f, 0f, 90f) * aim;
            for (int i = 0; i < n; i++)
            {
                Projectile p = projectiles[i];
                float slot = i - (n - 1) / 2f;
                Vector2 direction = Quaternion.Euler(0f, 0f, slot * FanDegrees) * aim;
                if (!(p.parentEntity is Player))
                    TurnToYourSide(p, direction);
                float speed;
                if (!speeds.TryGetValue(p, out speed))
                    speed = p.moveSpeed;
                p.moveSpeed = Mathf.Max(speed, LaunchSpeed);
                Send(p, center + aim * 0.6f + side * slot * 0.25f, direction);
            }
        }

        private static void Send(Projectile p, Vector2 position, Vector2 direction)
        {
            p.transform.position = position;
            p.moveVector = direction;
            if (p.rigidbody2D != null)
            {
                p.rigidbody2D.position = position;
                p.rigidbody2D.velocity = direction * p.moveSpeed;
            }
            try
            {
                p.RefreshFlightTime(false);
            }
            catch
            {
            }
        }

        // Catch every projectile that enters the field (not your own), turn it to your side and
        // spin it around you.
        private void CatchAndOrbit(Vector2 center, float field)
        {
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + ScanInterval;
                nearby = Object.FindObjectsOfType<Projectile>();
            }
            foreach (Projectile p in nearby)
            {
                if (caught.Count >= MaxCaught)
                    break;
                if (p == null || !p.gameObject.activeInHierarchy || caught.Contains(p) || converted.Contains(p))
                    continue;
                // Your own (and ones already yours) aren't caught.
                if (p.parentEntity is Player)
                    continue;
                Vector2 offset = (Vector2)p.transform.position - center;
                if (offset.magnitude > field)
                    continue;
                seenThisCharge++;
                TurnToYourSide(p, new Vector2(-offset.y, offset.x).normalized);
                converted.Add(p);
                speeds[p] = p.moveSpeed;
                p.moveSpeed = 0f;
                caught.Add(p);
            }
            caught.RemoveAll(p => p == null || !p.gameObject.activeInHierarchy);
            orbitAngle += OrbitSpeed * Time.deltaTime;
            for (int i = 0; i < caught.Count; i++)
            {
                float angle = orbitAngle + i * Mathf.PI * 2f / caught.Count;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Projectile p = caught[i];
                p.transform.position = center + offset * OrbitRadius;
                p.moveVector = new Vector2(-offset.y, offset.x);
                if (p.rigidbody2D != null)
                {
                    p.rigidbody2D.position = p.transform.position;
                    p.rigidbody2D.velocity = Vector2.zero;
                }
                try
                {
                    p.RefreshFlightTime(false);
                }
                catch
                {
                }
            }
        }

        private void RemoveSphere()
        {
            if (sphere != null)
            {
                Object.Destroy(sphere.gameObject);
                sphere = null;
            }
            if (sphereHolder != null)
            {
                Object.Destroy(sphereHolder);
                sphereHolder = null;
            }
        }

        private void RemoveRing()
        {
            if (ring != null)
            {
                Object.Destroy(ring.gameObject);
                ring = null;
            }
        }
    }

    // A crackling lightning circle: a jittery line renderer redrawn every frame.
    public class StaticRing : MonoBehaviour
    {
        private const int Points = 28;
        private LineRenderer line;

        public static StaticRing Create(Player player)
        {
            try
            {
                var go = new GameObject("StormField");
                StaticRing ring = go.AddComponent<StaticRing>();
                ring.line = go.AddComponent<LineRenderer>();
                ring.line.material = new Material(Shader.Find("Sprites/Default"));
                ring.line.positionCount = Points + 1;
                ring.line.useWorldSpace = true;
                ring.line.startWidth = ring.line.endWidth = 0.08f;
                SpriteRenderer body = player.GetComponentInChildren<SpriteRenderer>();
                if (body != null)
                {
                    ring.line.sortingLayerID = body.sortingLayerID;
                    ring.line.sortingOrder = body.sortingOrder + 1;
                }
                return ring;
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"Couldn't draw the storm field: {e.Message}");
                return null;
            }
        }

        public void Draw(Vector2 center, float radius)
        {
            float flicker = Random.value;
            var color = Color.Lerp(new Color(1f, 0.93f, 0.45f, 0.55f), new Color(1f, 1f, 1f, 0.9f), flicker);
            line.startColor = line.endColor = color;
            for (int i = 0; i <= Points; i++)
            {
                float a = (i % Points) * Mathf.PI * 2f / Points;
                float r = radius + (i % Points == 0 ? 0f : Random.Range(-0.12f, 0.12f));
                line.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * r, center.y + Mathf.Sin(a) * r * 0.6f, 0f));
            }
        }
    }
}
