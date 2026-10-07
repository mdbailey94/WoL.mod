using System.Collections.Generic;
using UnityEngine;

namespace WoLTrailblazer
{
    // The fire on a wizard for a few seconds after casting Trailblazer:
    // - a ring of fire round them that burns and shoves aside any enemy who touches it, all the
    //   time it's lit (moving or not), shown as flames licking up from their feet;
    // - a trail: as they move (running, dashing, or carried by any movement arcana) they leave
    //   patches of fire behind them every so often, which burn on the ground for a while and hurt
    //   enemies who walk into them.
    //
    // Everything grows with the wizard's speed (their move speed stat against its base, so relics
    // and sprinting count), over ten levels: running at base speed is level 1 (size 0.5),
    // sprinting at base speed level 2 (size 1), and every 5% faster than that one more level and
    // +10% size, up to level 10. The level sets the hits' damage, knockback and burn chance, and
    // the size the fire. Enhanced counts as one level more.
    public class FlameTrail : MonoBehaviour
    {
        private const int MaxPatches = 40;

        private class Patch
        {
            public Vector2 position;
            public float until;
            public float nextHit;
            public float nextFlame;
            public int level;
            public GameObject column; // Searing Rush's fire, if available
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
                    Flames(player.transform.position, 6, 0.8f);
                    litUntil = 0f;
                }
                if (patches.Count == 0)
                    enabled = false;
                return;
            }

            Vector2 position = player.transform.position;
            float speed = SpeedFactor();
            bool empowered = skill != null && skill.IsEmpowered;
            int level = Level(speed, empowered);
            float size = SizeOf(level);

            // The ring round the wizard.
            if (now >= nextAura)
            {
                nextAura = now + TrailblazerPlugin.AuraInterval;
                Burst(position, level, TrailblazerPlugin.AuraSize * HitScale(size), false);
            }
            // Burning feet: small flames licking up right at the wizard's feet (the ring's hit has
            // no look of its own).
            if (now >= nextAuraFlame)
            {
                nextAuraFlame = now + 0.035f;
                // Kept visible even at half size, so the burning feet always show.
                Flames(position + new Vector2(Random.Range(-0.18f, 0.18f), Random.Range(-0.06f, 0.06f)), 1,
                    TrailblazerPlugin.FlameSize * (0.6f + 0.4f * size));
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
            {
                SearingLook.PutOut(patches[0].column);
                patches.RemoveAt(0);
            }
            patches.Add(new Patch
            {
                position = position,
                until = now + TrailblazerPlugin.TrailLinger * (0.8f + 0.2f * size),
                nextHit = now + TrailblazerPlugin.TrailHitInterval,
                nextFlame = now,
                level = level,
                column = TrailblazerPlugin.SearingFire
                    ? SearingLook.Light(position, ColumnScale(level), player != null ? player.spriteRenderer : null)
                    : null
            });
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
            int level = Level(speed, empowered);
            float size = SizeOf(level);
            for (int i = patches.Count - 1; i >= 0; i--)
            {
                Patch patch = patches[i];
                if (now >= patch.until)
                {
                    SearingLook.PutOut(patch.column);
                    patches.RemoveAt(i);
                    continue;
                }
                // Small flames at normal speed, bigger (and more of them) the harder the patch hits
                // (only when there's no Searing Rush fire column on it).
                if (patch.column == null && now >= patch.nextFlame)
                {
                    patch.nextFlame = now + 0.1f;
                    float spread = 0.08f + 0.05f * patch.level;
                    int count = Mathf.Max(1, Mathf.RoundToInt(TrailblazerPlugin.FlameAmount * (0.4f + 0.15f * patch.level)));
                    Flames(patch.position + Random.insideUnitCircle * spread, count, FlameScale(patch.level));
                }
                if (now >= patch.nextHit)
                {
                    patch.nextHit = now + TrailblazerPlugin.TrailHitInterval;
                    Burst(patch.position, level, TrailblazerPlugin.TrailHitSize * HitScale(size), false);
                }
            }
        }

        // Ten levels by speed: 1 running at base speed (size 0.5), 2 sprinting at base speed
        // (size 1), then one more for every SpeedStep (5%) faster than that, each +10% size, up to
        // level 10 (size 1.8). Enhanced counts one level more.
        public const int MaxLevel = 10;

        private int Level(float speed, bool empowered)
        {
            float sprint = SprintFactor();
            int level = speed < sprint - 0.01f
                ? 1
                : 2 + Mathf.FloorToInt((speed - sprint) / TrailblazerPlugin.SpeedStep + 0.001f);
            level = Mathf.Clamp(level + (empowered ? 1 : 0), 1, MaxLevel);
            // The first few level changes go in the log, to check the speeds line up.
            if (level != lastLevel && levelLogs < 20)
            {
                levelLogs++;
                TrailblazerPlugin.Log($"Trailblazer: speed x{speed:0.00} (sprint x{sprint:0.00}) -> level {level}, size {SizeOf(level):0.0}");
            }
            lastLevel = level;
            return level;
        }

        private int lastLevel = -1;
        private static int levelLogs;

        private static float SizeOf(int level) => level <= 1 ? 0.5f : 1f + 0.1f * (level - 2);

        // The hits don't shrink as far as the look, so a slow wizard's fire still reaches enemies
        // touching them.
        private static float HitScale(float size) => 0.5f + 0.5f * size;

        // The flames at the feet and on the patches, against Blazing Blitz's own size.
        private static float FlameScale(int level) => TrailblazerPlugin.FlameSize * 0.9f * SizeOf(level);

        // Searing Rush's fire columns, against the game's own size (full size when sprinting).
        private static float ColumnScale(int level) => TrailblazerPlugin.FlameSize * SizeOf(level);

        private void OnDestroy()
        {
            foreach (Patch patch in patches)
                SearingLook.PutOut(patch.column);
            patches.Clear();
        }


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

        // How much faster a sprint is than a run at base speed (1.25 = +25%): the game's own sprint
        // bonus if it can be read, or SprintBonus.
        private float sprintFactor = -1f;
        private static bool loggedSprint;

        private float SprintFactor()
        {
            if (sprintFactor > 0f)
                return sprintFactor;
            float bonus = TrailblazerPlugin.SprintBonus;
            string from = "the SprintBonus setting";
            if (bonus <= 0f)
            {
                bonus = 0.25f;
                from = "a default";
                try
                {
                    SyncedNumVarStat stat = player != null ? player.runSpeedupStat : null;
                    if (stat != null)
                    {
                        float v = stat.ModifiedValue;
                        if (v > 0.01f && v < 1f)
                        {
                            bonus = v;
                            from = "the game";
                        }
                        else if (v > 1.01f && v <= 3f)
                        {
                            bonus = v - 1f;
                            from = "the game";
                        }
                    }
                }
                catch
                {
                }
            }
            sprintFactor = 1f + bonus;
            if (!loggedSprint)
            {
                loggedSprint = true;
                TrailblazerPlugin.Log($"Trailblazer: a sprint is +{bonus * 100f:0}% speed (from {from})");
            }
            return sprintFactor;
        }

        // The wizard's run speed stat against its base (1 = normal, more with speed relics and
        // while sprinting).
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
            return Mathf.Clamp(factor, 0.5f, 4f);
        }

        // Blazing Blitz's flame particles, scaled (1 = their own size).
        private static float baseFlameSize = -1f;

        private static void Flames(Vector2 position, int count, float scale)
        {
            try
            {
                FireBurst fire = PoolManager.GetPoolItem<FireBurst>();
                if (fire == null)
                    return;
                if (baseFlameSize < 0f)
                {
                    baseFlameSize = 0f;
                    ParticleSystem system = fire.effectEmitter != null ? fire.effectEmitter : fire.GetComponentInChildren<ParticleSystem>();
                    if (system != null)
                        baseFlameSize = system.main.startSizeMultiplier;
                    TrailblazerPlugin.Log($"Trailblazer: flame size {baseFlameSize:0.###}");
                }
                ParticleSystemOverride sized = baseFlameSize > 0f
                    ? new ParticleSystemOverride { startSize = new float?(baseFlameSize * scale) }
                    : null;
                fire.EmitSingle(new int?(count), new Vector3?(position), sized);
            }
            catch
            {
            }
        }
    }
}
