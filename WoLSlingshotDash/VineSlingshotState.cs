using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Vine Slingshot (Earth): hold dash and twin vines shoot out along your aim. The first enemy
    // they touch is grabbed and takes small, stunning hits while you keep holding (up to
    // MaxTether); if they touch no enemy they latch onto the wall or the ground at their reach.
    // Let go (or run out of time) and you pull yourself to the target and kick it back hard.
    public class VineSlingshotState : ChargedDashState
    {
        public new static string staticID = "VineSlingshot";

        private const float Range = 8f;
        private const float CatchRadius = 0.45f;
        private const float WallMargin = 0.6f;
        private const float MaxTether = 2f;
        private const float TickInterval = 0.4f;
        private const float ArriveDistance = 0.9f;
        private const float MinPullSpeed = 18f;

        private Enemy target;          // grabbed enemy, or null when latched to wall/ground
        private Vector2 anchor;        // where the vines hold
        private Vector2 launchFrom;
        private float nextTick;
        private bool kicked;
        private float pullSpeed;
        private VineLines vines;        // drawn vines (fallback) and the coils
        private GameVines gameVines;    // the game's own vine, when it works

        public VineSlingshotState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
        }

        // You pull yourself in; the dash's own speed boost doesn't apply.
        protected override bool BoostsDash => false;
        protected override float MaxHoldTime => MinChargeTime + MaxTether;

        protected override void OnChargeStarted()
        {
            Vector2 start = parent.transform.position;
            Vector2 direction = GetInputVector(true, true, true);
            if (direction.sqrMagnitude < 0.01f)
                direction = Entity.GetFacingDirectionVector(parent.facingDirection);
            direction.Normalize();

            float reach = Range;
            RaycastHit2D wall = Physics2D.Raycast(start, direction, Range, ChaosCollisions.layerAllWallAndObst);
            if (wall.collider != null)
                reach = Mathf.Max(0f, wall.distance - WallMargin);

            target = FirstEnemy(start, direction, reach);
            anchor = target != null ? (Vector2)target.transform.position : start + direction * reach;
            nextTick = 0f;
            kicked = false;

            vines = VineLines.Create(parent);
            gameVines = GameVines.Create(parent, parent.skillCategory, skillID);
            if (gameVines != null)
                gameVines.Hold(target != null ? target.transform : null, anchor);
            if (gameVines == null || gameVines.Broken)
                vines?.Shoot(start, anchor);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(start), null, 24f, -1f, 0.8f, false);
        }

        protected override void WhileCharging(float holdTime)
        {
            FollowTarget();
            DrawVines();
            // Hold the grabbed enemy: small hits that keep it stunned.
            if (target != null && holdTime - MinChargeTime >= nextTick)
            {
                nextTick += TickInterval;
                EarthBurst.CreateBurst(anchor, parent.skillCategory, skillID, 2, 0.7f);
            }
        }

        protected override void OnLaunch(float charge)
        {
            launchFrom = parent.transform.position;
            FollowTarget();
            float distance = Vector2.Distance(launchFrom, anchor);
            float duration = dashDuration > 0.05f ? dashDuration : 0.25f;
            pullSpeed = Mathf.Max(MinPullSpeed, distance / duration);
            parent.FaceTarget(anchor);
            parent.anim?.PlayDirectional(parent.KickAnimStr, -1, 0f);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(launchFrom), null, 24f, -1f, 1.1f, false);
        }

        protected override void WhileDashing(float charge)
        {
            if (kicked)
                return;
            FollowTarget();
            DrawVines();
        }

        // Pull yourself along the vines, and kick on arrival.
        protected override void DashFixedUpdate(float charge)
        {
            if (kicked || parent.rigidbody2D == null)
                return;
            Vector2 toTarget = anchor - (Vector2)parent.transform.position;
            if (toTarget.magnitude <= ArriveDistance)
            {
                parent.rigidbody2D.velocity = Vector2.zero;
                Kick();
                return;
            }
            parent.rigidbody2D.velocity = toTarget.normalized * pullSpeed;
        }

        // If the dash ran out before you got there (something in the way), kick where you are.
        protected override void OnLand(float charge)
        {
            if (!kicked)
                Kick();
        }

        protected override void OnStateExit()
        {
            RemoveVines();
            target = null;
        }

        private void Kick()
        {
            kicked = true;
            RemoveVines();
            Vector2 position = parent.transform.position;
            Vector2 direction = anchor - launchFrom;
            if (direction.sqrMagnitude < 0.01f)
                direction = Entity.GetFacingDirectionVector(parent.facingDirection);
            direction.Normalize();
            parent.anim?.PlayDirectional(parent.KickAnimStr, -1, 0.5f);
            EarthBurst burst = EarthBurst.CreateBurst(position + direction * 0.6f, parent.skillCategory, skillID, 1, 1.3f);
            // Knock the target on, away from you.
            if (burst != null && burst.attack != null)
                burst.attack.knockbackOverwriteVector = direction;
            PoolManager.GetPoolItem<DustEmitter>().EmitCircle(50, 1.2f, -6f, -1f, new Vector3?(position), null);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f, 0.7f, false);
            target = null;
        }

        private void FollowTarget()
        {
            if (target == null)
                return;
            if (!target.gameObject.activeInHierarchy)
            {
                // Gone (e.g. killed): the vines stay where it was.
                target = null;
                return;
            }
            anchor = target.transform.position;
        }

        // The game's vine if it's working, otherwise our drawn ones; coils on a grabbed enemy.
        private void DrawVines()
        {
            if (gameVines != null && !gameVines.Broken)
                gameVines.Hold(target != null ? target.transform : null, anchor);
            if (gameVines == null || gameVines.Broken)
                vines?.Hold(parent.transform.position, anchor);
            if (target != null)
                vines?.Ensnare(anchor);
        }

        private void RemoveVines()
        {
            gameVines?.Remove();
            gameVines = null;
            if (vines != null)
            {
                Object.Destroy(vines.gameObject);
                vines = null;
            }
        }

        // The nearest enemy along the vines' path, if any.
        private static Enemy FirstEnemy(Vector2 start, Vector2 direction, float reach)
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(start, CatchRadius, direction, reach);
            Enemy best = null;
            float bestDistance = float.MaxValue;
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null)
                    continue;
                Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
                if (enemy == null || !enemy.gameObject.activeInHierarchy)
                    continue;
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = enemy;
                }
            }
            return best;
        }
    }
}
