using System.Collections.Generic;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Storm Slingshot (Lightning): while you hold, a crackling field around you catches enemy
    // projectiles (like Mag Sphere), turns them to your side (the game's own reflect) and spins
    // them around you. The field grows as you hold. Let go and they all fire in a tight fan along
    // your aim while you dash after them, with a small lightning burst where you land.
    public class StormSlingshotState : ChargedDashState
    {
        public new static string staticID = "StormSlingshot";

        private const int MaxCaught = 8;
        private const float MinFieldRadius = 2.5f;
        private const float ExtraFieldRadius = 2f;   // added at full charge
        private const float OrbitRadius = 1.3f;
        private const float OrbitSpeed = 4f;         // radians per second
        private const float LaunchSpeed = 22f;
        private const float FanDegrees = 8f;          // between neighbouring projectiles
        private const float ReflectDamage = 1.5f;     // damage multiplier for caught projectiles
        private const float ScanInterval = 0.1f;

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
            ReleaseAll(null);
            orbitAngle = 0f;
            nextScan = 0f;
            ring = StaticRing.Create(parent);
        }

        protected override void WhileCharging(float holdTime)
        {
            Vector2 center = parent.transform.position;
            float power = Mathf.Clamp01((holdTime - MinChargeTime) / (1f - MinChargeTime));
            float field = MinFieldRadius + ExtraFieldRadius * power;
            ring?.Draw(center, field);

            if (Time.time >= nextScan)
            {
                nextScan = Time.time + ScanInterval;
                nearby = Object.FindObjectsOfType<Projectile>();
            }
            foreach (Projectile p in nearby)
            {
                if (caught.Count >= MaxCaught)
                    break;
                if (p == null || !p.gameObject.activeInHierarchy || p.ignoreReflect || caught.Contains(p))
                    continue;
                if (p.parentEntity is Player)
                    continue;
                Vector2 offset = (Vector2)p.transform.position - center;
                if (offset.magnitude > field)
                    continue;
                try
                {
                    if (!p.OnReflect(new Vector2(-offset.y, offset.x).normalized, ReflectDamage))
                        continue;
                }
                catch
                {
                    continue;
                }
                speeds[p] = p.moveSpeed;
                p.moveSpeed = 0f; // we move it while it orbits
                caught.Add(p);
            }

            // Spin the caught projectiles around you.
            caught.RemoveAll(p => p == null || !p.gameObject.activeInHierarchy);
            orbitAngle += OrbitSpeed * Time.deltaTime;
            for (int i = 0; i < caught.Count; i++)
            {
                float angle = orbitAngle + i * Mathf.PI * 2f / caught.Count;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Place(caught[i], center + offset * OrbitRadius, new Vector2(-offset.y, offset.x));
                try
                {
                    caught[i].RefreshFlightTime(false);
                }
                catch
                {
                }
            }
        }

        protected override void OnLaunch(float charge)
        {
            Vector2 aim = inputVector.sqrMagnitude > 0.01f
                ? inputVector.normalized
                : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            RemoveRing();
            ReleaseAll(aim);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(parent.transform.position), null, 24f, -1f, 1.6f, false);
        }

        protected override void OnLand(float charge)
        {
            LightningBurst.CreateBurst(parent.transform.position, parent.skillCategory, skillID, 1, 1.2f + 0.4f * charge, false);
        }

        // Let go without launching (e.g. hit): fling them outward rather than leave them hanging.
        protected override void OnStateExit()
        {
            RemoveRing();
            ReleaseAll(null);
        }

        // Fire every caught projectile: along the aim in a fan, or outward from you if aim is null.
        private void ReleaseAll(Vector2? aim)
        {
            Vector2 center = parent.transform.position;
            int n = caught.Count;
            for (int i = 0; i < n; i++)
            {
                Projectile p = caught[i];
                if (p == null || !p.gameObject.activeInHierarchy)
                    continue;
                Vector2 direction;
                if (aim.HasValue)
                {
                    float degrees = (i - (n - 1) / 2f) * FanDegrees;
                    direction = Quaternion.Euler(0f, 0f, degrees) * aim.Value;
                    p.transform.position = center + aim.Value * 0.6f + (Vector2)(Quaternion.Euler(0f, 0f, 90f) * aim.Value) * (i - (n - 1) / 2f) * 0.25f;
                }
                else
                {
                    direction = ((Vector2)p.transform.position - center).normalized;
                }
                float speed;
                if (!speeds.TryGetValue(p, out speed))
                    speed = LaunchSpeed;
                p.moveSpeed = aim.HasValue ? Mathf.Max(speed, LaunchSpeed) : speed;
                Place(p, p.transform.position, direction);
                if (p.rigidbody2D != null)
                    p.rigidbody2D.velocity = direction * p.moveSpeed;
                try
                {
                    p.RefreshFlightTime(false);
                }
                catch
                {
                }
            }
            caught.Clear();
            speeds.Clear();
        }

        private static void Place(Projectile p, Vector2 position, Vector2 moveVector)
        {
            p.transform.position = position;
            p.moveVector = moveVector;
            if (p.rigidbody2D != null)
            {
                p.rigidbody2D.position = position;
                p.rigidbody2D.velocity = Vector2.zero;
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
