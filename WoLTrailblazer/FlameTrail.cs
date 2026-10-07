using UnityEngine;

namespace WoLTrailblazer
{
    // The fire that follows a wizard who has Trailblazer equipped. Whenever they're moving
    // (running, dashing, or carried by any movement arcana) it leaves flames on the ground behind
    // them and scorches enemies they run into: a small fire burst at their feet that burns and
    // knocks back a little.
    //
    // Everything grows with the wizard's run speed after relics and the like (their move speed
    // stat against its base): the flames, the hit area, and the hit's level (damage, knockback
    // and burn chance, levels 1-5). Enhanced counts as one level more and a bigger trail.
    public class FlameTrail : MonoBehaviour
    {
        public Player player;
        public Player.SkillState skill;

        private Vector2 last;
        private bool hasLast;
        private float nextFlame;
        private float nextHit;
        private float moving; // seconds the wizard has been moving
        private static bool loggedSpeed;

        private void OnEnable()
        {
            hasLast = false;
            moving = 0f;
        }

        private void LateUpdate()
        {
            if (player == null)
            {
                enabled = false;
                return;
            }
            float dt = Time.deltaTime;
            Vector2 position = player.transform.position;
            if (!hasLast || dt <= 0f)
            {
                last = position;
                hasLast = true;
                return;
            }
            float step = Vector2.Distance(position, last);
            last = position;
            // A jump of several tiles in one frame is a teleport (a new floor, a swap), not a run.
            if (step > TrailblazerPlugin.TeleportDistance || step / dt < TrailblazerPlugin.MinSpeed)
            {
                moving = 0f;
                return;
            }
            moving += dt;

            float speed = SpeedFactor();
            bool empowered = skill != null && skill.IsEmpowered;
            float size = speed * (empowered ? 1.25f : 1f);

            nextFlame -= dt;
            if (nextFlame <= 0f)
            {
                nextFlame = TrailblazerPlugin.FlameInterval;
                Flames(position, size);
            }
            // A moment's movement first, so a twitch of the stick doesn't set anything alight.
            if (moving < 0.05f || Time.time < nextHit)
                return;
            nextHit = Time.time + TrailblazerPlugin.HitInterval;
            int level = Mathf.Clamp(1 + Mathf.RoundToInt((speed - 1f) / TrailblazerPlugin.SpeedPerLevel) + (empowered ? 1 : 0), 1, 5);
            try
            {
                FlameBurst.CreateBurst(position, player.skillCategory, TrailblazerState.staticID, level,
                    TrailblazerPlugin.HitSize * size, false);
            }
            catch (System.Exception e)
            {
                TrailblazerPlugin.Log($"Trailblazer: couldn't make a fire burst: {e.Message}");
                nextHit = float.MaxValue;
            }
        }

        // The wizard's run speed stat against its base (1 = normal, more with speed relics).
        private float SpeedFactor()
        {
            float factor = 1f;
            try
            {
                NumVarStat stat = player.movement != null ? player.movement.moveSpeedStat : null;
                if (stat != null && stat.BaseValue > 0.01f)
                    factor = stat.ModifiedValue / stat.BaseValue;
                if (!loggedSpeed)
                {
                    loggedSpeed = true;
                    TrailblazerPlugin.Log($"Trailblazer: run speed {(stat != null ? stat.ModifiedValue.ToString("0.##") : "?")} " +
                        $"of base {(stat != null ? stat.BaseValue.ToString("0.##") : "?")}");
                }
            }
            catch
            {
            }
            return Mathf.Clamp(factor, 0.75f, 2.5f);
        }

        // Blazing Blitz's flame particles, more of them and lingering longer the faster you are.
        private static void Flames(Vector2 position, float size)
        {
            try
            {
                FireBurst fire = PoolManager.GetPoolItem<FireBurst>();
                if (fire == null)
                    return;
                var linger = new ParticleSystemOverride
                {
                    startLifetime = new float?(TrailblazerPlugin.TrailLinger * (0.8f + 0.4f * size))
                };
                fire.EmitSingle(new int?(Mathf.Max(1, Mathf.RoundToInt(TrailblazerPlugin.FlameAmount * size))),
                    new Vector3?(position), linger);
            }
            catch
            {
            }
        }
    }
}
