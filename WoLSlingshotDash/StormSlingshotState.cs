using System.Collections.Generic;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Storm Slingshot (Lightning): while you hold, a small Mag Sphere forms on you. Every projectile
    // that enters it is turned to your side at once (so it can't hit you or your allies) and spun
    // around you. Let go and they all fire in a fan along your aim while you dash after them, with a
    // small lightning burst where you land.
    //
    // The Mag Sphere is only the look (its colliders are off, so it doesn't do its own thing to the
    // projectiles); the catching is done here, within its radius. Without it, a drawn ring shows.
    public class StormSlingshotState : ChargedDashState
    {
        public new static string staticID = "StormSlingshot";

        private const float SphereScale = 0.6f;       // smaller than the arcana's
        private const float ExtraSphereScale = 0.2f;  // added at full charge
        private const float DefaultSphereRadius = 2f; // if its collider can't be measured
        private const int MaxCaught = 20;
        private const float MinFieldRadius = 2f;
        private const float ExtraFieldRadius = 1.5f;
        private const float OrbitRadius = 1.3f;
        private const float OrbitSpeed = 4f;
        private const float LaunchSpeed = 22f;
        private const float FanDegrees = 8f;
        private const float ReflectDamage = 1.5f;
        private const float ScanInterval = 0.05f;

        private MagSphere sphere;
        private float sphereBaseRadius = DefaultSphereRadius; // its collider's radius at scale 1
        private int seenThisCharge;
        private static bool loggedCatch;
        // Projectiles the sphere has caught and turned to your side.
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
                MagSphere ms = ChaosInst<MagSphere>(MagSphere.Prefab, new Vector2?(parent.transform.position), null, null);
                if (ms == null)
                    return null;
                ms.parentPlayer = parent;
                ms.parentObject = parent.gameObject;
                ms.followTrans = parent.transform;
                ms.skillCategory = parent.skillCategory;
                ms.duration = 60f;
                ms.transform.localScale = Vector3.one;
                Collider2D main = ms.mainCollider != null ? ms.mainCollider : ms.GetComponentInChildren<Collider2D>();
                if (main != null)
                    sphereBaseRadius = Mathf.Max(0.5f, Mathf.Max(main.bounds.extents.x, main.bounds.extents.y));
                // Only its look: we do the catching.
                foreach (Collider2D c in ms.GetComponentsInChildren<Collider2D>(true))
                    c.enabled = false;
                ms.transform.localScale = Vector3.one * SphereScale;
                if (!loggedSphere)
                {
                    loggedSphere = true;
                    SlingshotDashPlugin.Log("Storm: charging with a Mag Sphere");
                }
                return ms;
            }
            catch (System.Exception e)
            {
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
            float power = Mathf.Clamp01((holdTime - MinChargeTime) / (1f - MinChargeTime));
            float field;
            if (sphere != null)
            {
                float scale = SphereScale + ExtraSphereScale * power;
                sphere.transform.position = center;
                sphere.transform.localScale = Vector3.one * scale;
                field = sphereBaseRadius * scale;
            }
            else
            {
                field = MinFieldRadius + ExtraFieldRadius * power;
                ring?.Draw(center, field);
            }
            CatchAndOrbit(center, field);
        }

        protected override void OnLaunch(float charge)
        {
            Vector2 aim = inputVector.sqrMagnitude > 0.01f
                ? inputVector.normalized
                : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            Vector2 center = parent.transform.position;
            RemoveSphere();
            RemoveRing();
            if (!loggedCatch)
            {
                loggedCatch = true;
                SlingshotDashPlugin.Log($"Storm: caught {caught.Count} projectile(s) " +
                    $"({seenThisCharge} entered the field this charge, {nearby.Length} in the room)");
            }
            Fan(caught, center, aim);
            caught.Clear();
            speeds.Clear();
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(center), null, 24f, -1f, 1.6f, false);
        }

        protected override void OnLand(float charge)
        {
            LightningBurst.CreateBurst(parent.transform.position, parent.skillCategory, skillID, 1, 1.2f + 0.4f * charge, false);
        }

        protected override void OnStateExit()
        {
            RemoveSphere();
            RemoveRing();
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
