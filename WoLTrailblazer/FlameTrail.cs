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
    // Everything grows with the wizard's speed, over ten levels. The speed is their move speed
    // stat against its base, counting the lasting bonuses (sprinting, speed relics, the cloak,
    // speed-boost arcana) but not the speeds a dash or attack forces for a moment. Running at base
    // speed is level 1 and PeakSpeedPercent of base speed is level 10, the levels between spread
    // evenly (with the default 230%: one level every +14.4% speed, so a plain sprint is level 3).
    // The level sets the hits' damage, knockback and burn chance, and the size of the fire.
    // Enhanced counts as one level more.
    //
    // On top of the game's flames, the drawn fire (BlazeLook): flames licking up round the wizard,
    // embers, the ring of fire on the floor, flames and char marks on the trail, and a flare-up as
    // it burns out.
    public class FlameTrail : MonoBehaviour
    {
        private const int MaxPatches = 40;

        // Ten levels by speed. The hits' numbers are the arcana's level-1 stats (the Balance
        // settings, with the wizard's own damage bonuses) scaled by the level: damage up to
        // PeakDamageMultiplier times, knockback and burn chance up to these, all spread evenly.
        public const int MaxLevel = 10;
        private const float PeakKnockbackMultiplier = 2.5f; // knockback at level 10, against level 1
        private const float PeakBurnBonus = 0.5f;           // burn chance added at level 10 (0.5 -> 1.0)

        private class Patch
        {
            public Vector2 position;
            public float until;
            public float nextHit;
            public float nextFlame;
            public int level;
            public GameObject column; // Searing Rush's fire, if available
            public BlazeLook.PatchLook look; // its drawn flames (the char mark plays out by itself)
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
        // The drawn fire, and what this frame's Update worked out for it (drawn in LateUpdate,
        // once the wizard has moved).
        private BlazeLook look;
        private bool lookLit;
        private int lookLevel = 1;
        private float lookRing;
        private static bool loggedSpeed, loggedPatch, loggedLevels;
        private static string lastBurstError;

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
            // The drawn fire, if it's on (checked each cast).
            if (!TrailblazerPlugin.Drawn)
            {
                trail.look?.Off();
                trail.look = null;
            }
            else if (trail.look == null)
                trail.look = BlazeLook.For(player);
            trail.look?.Ignite(player.transform.position);
            TrailblazerPlugin.Log($"Trailblazer: lit for {duration:0.#} s");
            if (!loggedLevels)
            {
                loggedLevels = true;
                TrailblazerPlugin.Log($"Trailblazer: level 1 at x1.00 base run speed, one level every +{LevelStep() * 100f:0.0}%, " +
                    $"level {MaxLevel} at x{TrailblazerPlugin.PeakSpeedPercent / 100f:0.00}; damage x1 to x{TrailblazerPlugin.PeakDamageMultiplier:0.#}");
            }
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
                    // Burnt out: a last puff of flame, and the drawn flare-up.
                    Flames(player.transform.position, 6, 0.8f);
                    litUntil = 0f;
                    look?.BurntOut(player.transform.position, lookLevel);
                }
                lookLit = false;
                if (patches.Count == 0)
                    enabled = false;
                return;
            }

            Vector2 position = player.transform.position;
            float speed = SpeedFactor();
            bool empowered = skill != null && skill.IsEmpowered;
            int level = Level(speed, empowered);
            float size = SizeOf(level);
            lookLit = true;
            lookLevel = level;
            lookRing = TrailblazerPlugin.AuraSize * HitScale(size); // the ring's hit radius (a burst's scale is its radius)

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
                look?.PatchOut(patches[0].look);
                patches.RemoveAt(0);
            }
            var patch = new Patch
            {
                position = position,
                until = now + TrailblazerPlugin.TrailLinger * (0.8f + 0.2f * size),
                nextHit = now + TrailblazerPlugin.TrailHitInterval,
                nextFlame = now,
                level = level,
                column = TrailblazerPlugin.SearingFire
                    ? SearingLook.Light(position, ColumnScale(level), player != null ? player.spriteRenderer : null)
                    : null
            };
            patch.look = look?.Patch(position, level, patch.until - now, patch.column != null);
            patches.Add(patch);
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
                    look?.PatchOut(patch.look);
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

        // How much faster (as a fraction of base run speed) each level needs than the one before:
        // the levels go evenly from x1 (level 1) to PeakSpeedPercent (level 10).
        private static float LevelStep() => (TrailblazerPlugin.PeakSpeedPercent / 100f - 1f) / (MaxLevel - 1);

        // Level 1 running at base speed, up to level 10 at the peak speed. Enhanced counts one
        // level more.
        private int Level(float speed, bool empowered)
        {
            int level = 1 + Mathf.FloorToInt((speed - 1f) / LevelStep() + 0.001f);
            level = Mathf.Clamp(level + (empowered ? 1 : 0), 1, MaxLevel);
            lastSpeed = speed;
            return level;
        }

        private float lastSpeed = 1f;
        private int lastLoggedLevel = -1;

        // How far along the ten levels (0 at level 1, 1 at level 10).
        private static float Along(int level) => Mathf.Clamp01((level - 1) / (float)(MaxLevel - 1));

        private static float DamageScale(int level) => 1f + (TrailblazerPlugin.PeakDamageMultiplier - 1f) * Along(level);
        private static float KnockbackScale(int level) => 1f + (PeakKnockbackMultiplier - 1f) * Along(level);
        private static float BurnBonus(int level) => PeakBurnBonus * Along(level);

        private static float SizeOf(int level) => level <= 1 ? 0.5f : 1f + 0.1f * (level - 2);

        // The hits don't shrink as far as the look, so a slow wizard's fire still reaches enemies
        // touching them.
        private static float HitScale(float size) => 0.5f + 0.5f * size;

        // The flames at the feet and on the patches, against Blazing Blitz's own size.
        private static float FlameScale(int level) => TrailblazerPlugin.FlameSize * 0.9f * SizeOf(level);

        // Searing Rush's fire columns, against the game's own size (full size when sprinting).
        private static float ColumnScale(int level) => TrailblazerPlugin.FlameSize * SizeOf(level);

        // The drawn fire on the wizard, after they've moved this frame.
        private void LateUpdate()
        {
            if (!lookLit || look == null || player == null || Time.time >= litUntil)
                return;
            look.Lit(lookLevel, lookRing);
        }

        private void OnDisable()
        {
            look?.Off();
        }

        private void OnDestroy()
        {
            foreach (Patch patch in patches)
            {
                SearingLook.PutOut(patch.column);
                look?.PatchOut(patch.look, true);
            }
            patches.Clear();
            look?.Off();
        }

        // A hit of the fire at a spot. The game only keeps SkillStats.maxSkillLevel (5) levels of
        // stats for an arcana, so the ten speed levels can't be stat levels (levels 6-10 used to
        // index past the stats and the hit silently got no attack). Instead every hit is made at
        // stat level 1 and its damage, knockback and burn chance are scaled by the level here,
        // before the hit's collider gets to touch anything. All hits share the level-1 attack key,
        // so the stats' sameTargetImmunityTime limits how often one enemy is hit by any of them.
        private void Burst(Vector2 position, int level, float scale, bool effects)
        {
            try
            {
                FlameBurst burst = FlameBurst.CreateBurst(position, player != null ? player.skillCategory : "",
                    TrailblazerState.staticID, 1, scale, effects);
                AttackInfo info = burst != null && burst.attack != null ? burst.attack.atkInfo : null;
                if (info == null)
                    throw new System.Exception("the burst has no attack info");
                info.damage = Mathf.Max(1, Mathf.RoundToInt(info.damage * DamageScale(level)));
                info.knockbackMultiplier *= KnockbackScale(level);
                info.burnChance = Mathf.Clamp01(info.burnChance + BurnBonus(level));
                if (level != lastLoggedLevel)
                {
                    lastLoggedLevel = level;
                    TrailblazerPlugin.Log($"Trailblazer: speed x{lastSpeed:0.00} -> level {level} of {MaxLevel}: " +
                        $"{info.damage} damage a hit, knockback {info.knockbackMultiplier:0.#}, burn {info.burnChance * 100f:0}%, size {SizeOf(level):0.0}");
                }
            }
            catch (System.Exception e)
            {
                // Each different failure is logged once, so a level that stops hitting shows up.
                if (e.Message != lastBurstError)
                {
                    lastBurstError = e.Message;
                    TrailblazerPlugin.Log($"Trailblazer: couldn't make a fire burst at level {level}: {e.Message}");
                }
            }
        }

        // The wizard's run speed against its base (1 = running at base speed, 1.4 sprinting,
        // more with speed relics, the cloak and speed-boost arcana). Counts the stat's lasting
        // modifiers the way the game does (multiplied, then added, then late-multiplied), but not
        // the Override ones: those are the speeds a dash, a charge or an attack's wind-up force on
        // the wizard for a moment, which would otherwise swing the level about wildly.
        private float SpeedFactor()
        {
            float factor = 1f;
            try
            {
                NumVarStat stat = player.movement != null ? player.movement.moveSpeedStat : null;
                if (stat != null && stat.BaseValue > 0.01f)
                {
                    float speed = stat.BaseValue;
                    int overrides = 0;
                    if (stat.modifiers != null)
                    {
                        // Already sorted by type (as Recalculate applies them).
                        foreach (VarStatMod<float> mod in stat.modifiers)
                        {
                            NumVarStatMod numMod = mod as NumVarStatMod;
                            if (numMod == null)
                                continue;
                            switch (numMod.modType)
                            {
                                case VarStatModType.Additive:
                                    speed += numMod.modValue;
                                    break;
                                case VarStatModType.Multiplicative:
                                case VarStatModType.LateMultiplicative:
                                    speed *= 1f + numMod.modValue;
                                    break;
                                case VarStatModType.Override:
                                case VarStatModType.OverrideWithMods:
                                    overrides++;
                                    break;
                            }
                        }
                    }
                    factor = speed / stat.BaseValue;
                    if (!loggedSpeed)
                    {
                        loggedSpeed = true;
                        TrailblazerPlugin.Log($"Trailblazer: run speed {speed:0.##} of base {stat.BaseValue:0.##} (x{factor:0.00}; " +
                            $"the stat reads {stat.ModifiedValue:0.##}, {overrides} override mod(s) ignored)");
                    }
                }
            }
            catch
            {
            }
            return Mathf.Clamp(factor, 0.5f, 6f);
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
