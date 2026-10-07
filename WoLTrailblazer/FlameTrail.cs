using System.Collections.Generic;
using UnityEngine;

namespace WoLTrailblazer
{
    // The fire on a wizard for a few seconds after casting Trailblazer:
    // - a ring of fire round them that burns and shoves aside any enemy who touches it, all the
    //   time it's lit (moving or not);
    // - a trail: as they move (running, dashing, or carried by any movement arcana) they leave
    //   patches of fire behind them every so often, which burn on the ground for a while and hurt
    //   enemies who walk into them.
    //
    // Everything grows with the wizard's run speed after relics and the like (their move speed
    // stat against its base): the ring, the patches, and the hits' level (damage, knockback and
    // burn chance, levels 1-5). Enhanced counts as one level more and a bigger fire.
    public class FlameTrail : MonoBehaviour
    {
        private const int MaxPatches = 40;

        private class Patch
        {
            public Vector2 position;
            public float until;
            public float nextHit;
            public float nextFlame;
        }

        private Player player;
        private Player.SkillState skill;
        private float litUntil;
        private Vector2 last;
        private bool hasLast;
        private float travelled;
        private float nextAura;
        private float nextAuraFlame;
        private readonly List<Patch> patches = new List<Patch>();
        private static bool loggedSpeed, loggedHit, loggedPatch;

        // Sets (or re-sets) the wizard alight for this long.
        public static void Light(Player player, Player.SkillState skill, float duration)
        {
            if (player == null)
                return;
            FlameTrail trail = player.GetComponent<FlameTrail>();
            if (trail == null)
                trail = player.gameObject.AddComponent<FlameTrail>();
            trail.player = player;
            trail.skill = skill;
            trail.litUntil = Time.time + duration;
            trail.hasLast = false;
            trail.travelled = 0f;
            trail.nextAura = 0f;
            trail.enabled = true;
            TrailblazerPlugin.Log($"Trailblazer: lit for {duration:0.#} s");
        }

        private void Update()
        {
            float now = Time.time;
            bool lit = player != null && now < litUntil;
            UpdatePatches(now);
            if (!lit)
            {
                if (player != null && litUntil > 0f)
                {
                    // Burnt out: a last puff of flame.
                    Flames(player.transform.position, 8);
                    litUntil = 0f;
                }
                if (patches.Count == 0)
                    enabled = false;
                return;
            }

            Vector2 position = player.transform.position;
            float speed = SpeedFactor();
            bool empowered = skill != null && skill.IsEmpowered;
            float size = speed * (empowered ? 1.25f : 1f);
            int level = Level(speed, empowered);

            // The ring round the wizard.
            if (now >= nextAura)
            {
                nextAura = now + TrailblazerPlugin.AuraInterval;
                Burst(position, level, TrailblazerPlugin.AuraSize * size, false);
            }
            if (now >= nextAuraFlame)
            {
                nextAuraFlame = now + 0.06f;
                float radius = 0.45f * TrailblazerPlugin.AuraSize * size;
                for (int i = 0; i < 2; i++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    Flames(position + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f) * radius, 1);
                }
            }

            // The trail: by distance covered, so it doesn't matter how the wizard moves or how the
            // frames fall.
            if (!hasLast)
            {
                last = position;
                hasLast = true;
                return;
            }
            float step = Vector2.Distance(position, last);
            last = position;
            if (step > TrailblazerPlugin.TeleportDistance)
                return; // a teleport (a new floor, a swap), not a run
            travelled += step;
            if (travelled >= TrailblazerPlugin.PatchSpacing)
            {
                travelled = 0f;
                DropPatch(position, now, level, size);
            }
        }

        private void DropPatch(Vector2 position, float now, int level, float size)
        {
            if (patches.Count >= MaxPatches)
                patches.RemoveAt(0);
            patches.Add(new Patch
            {
                position = position,
                until = now + TrailblazerPlugin.TrailLinger * (0.8f + 0.2f * size),
                nextHit = now + TrailblazerPlugin.TrailHitInterval,
                nextFlame = now
            });
            // A visible flare as it catches.
            Burst(position, level, 0.8f + 0.3f * size, true);
            if (!loggedPatch)
            {
                loggedPatch = true;
                TrailblazerPlugin.Log("Trailblazer: leaving a trail");
            }
        }

        // The patches burn on the ground, flickering and hurting whoever walks into them.
        private void UpdatePatches(float now)
        {
            if (patches.Count == 0)
                return;
            float speed = player != null ? SpeedFactor() : 1f;
            bool empowered = skill != null && skill.IsEmpowered;
            float size = speed * (empowered ? 1.25f : 1f);
            int level = Level(speed, empowered);
            for (int i = patches.Count - 1; i >= 0; i--)
            {
                Patch patch = patches[i];
                if (now >= patch.until)
                {
                    patches.RemoveAt(i);
                    continue;
                }
                if (now >= patch.nextFlame)
                {
                    patch.nextFlame = now + 0.1f;
                    Flames(patch.position + Random.insideUnitCircle * 0.25f * size,
                        Mathf.Max(1, Mathf.RoundToInt(TrailblazerPlugin.FlameAmount * size)));
                }
                if (now >= patch.nextHit)
                {
                    patch.nextHit = now + TrailblazerPlugin.TrailHitInterval;
                    Burst(patch.position, level, TrailblazerPlugin.TrailHitSize * size, false);
                }
            }
        }

        private static int Level(float speed, bool empowered) =>
            Mathf.Clamp(1 + Mathf.RoundToInt((speed - 1f) / TrailblazerPlugin.SpeedPerLevel) + (empowered ? 1 : 0), 1, 5);

        private void Burst(Vector2 position, int level, float scale, bool effects)
        {
            try
            {
                FlameBurst.CreateBurst(position, player != null ? player.skillCategory : "", TrailblazerState.staticID,
                    level, scale, effects);
                if (!loggedHit)
                {
                    loggedHit = true;
                    TrailblazerPlugin.Log($"Trailblazer: fire bursts working (level {level}, size {scale:0.##})");
                }
            }
            catch (System.Exception e)
            {
                if (!loggedHit)
                {
                    loggedHit = true;
                    TrailblazerPlugin.Log($"Trailblazer: couldn't make a fire burst: {e.Message}");
                }
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

        // Blazing Blitz's flame particles.
        private static void Flames(Vector2 position, int count)
        {
            try
            {
                PoolManager.GetPoolItem<FireBurst>()?.EmitSingle(new int?(count), new Vector3?(position));
            }
            catch
            {
            }
        }
    }
}
