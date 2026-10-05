using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoLExtendedStats
{
    // Everything we count for one player over one run.
    public class RunStats
    {
        public int Hits;
        public int Crits;
        public int DamageDealt;
        public int BiggestHit;
        public string BiggestHitSkill;
        public int BiggestCrit;
        public string BiggestCritSkill;
        public int Kills;
        public int PitKnockoffs;
        public int DamageTaken;
        public int Evades;
        public int Dashes;
        public float TopSpeed;
        public float Distance;
        public Dictionary<string, SkillRunStats> BySkill = new Dictionary<string, SkillRunStats>();

        public SkillRunStats Skill(string skillID)
        {
            SkillRunStats stats;
            if (!BySkill.TryGetValue(skillID, out stats))
                BySkill[skillID] = stats = new SkillRunStats();
            return stats;
        }

        // Snapshot for the end-of-run screen, so late events can't change what's shown.
        public RunStats Clone()
        {
            var copy = (RunStats)MemberwiseClone();
            copy.BySkill = new Dictionary<string, SkillRunStats>();
            foreach (var pair in BySkill)
                copy.BySkill[pair.Key] = pair.Value.Clone();
            return copy;
        }
    }

    public class SkillRunStats
    {
        public int Damage;
        public int Hits;
        public int Crits;

        public SkillRunStats Clone() => (SkillRunStats)MemberwiseClone();
    }

    // Collects RunStats per player from the game's own events plus a few Harmony hooks.
    // Stats are keyed by the player's PlayerEndStats object, which the game resets per run.
    public static class RunTracker
    {
        // Ignore per-frame jumps bigger than this (teleports, level transitions) for distance.
        private const float MaxStepDistance = 3f;
        // An enemy that "dies" twice within this window (e.g. fall + damage) only counts once.
        private const float KillDedupeSeconds = 2f;

        private static readonly Dictionary<PlayerEndStats, RunStats> statsByRun = new Dictionary<PlayerEndStats, RunStats>();
        private static readonly Dictionary<int, Player> lastHitBy = new Dictionary<int, Player>();
        private static readonly Dictionary<int, float> lastKillTime = new Dictionary<int, float>();
        private static readonly Dictionary<int, Vector2> lastPosition = new Dictionary<int, Vector2>();
        private static readonly HashSet<int> evadeHooked = new HashSet<int>();
        private static readonly HashSet<int> deathHooked = new HashSet<int>();
        private static bool damageHooked;

        public static RunStats For(Player player)
        {
            PlayerEndStats key = player.stats;
            if (key == null)
                return new RunStats();

            RunStats stats;
            if (!statsByRun.TryGetValue(key, out stats))
                statsByRun[key] = stats = new RunStats();
            return stats;
        }

        public static void ResetRun(PlayerEndStats key)
        {
            if (key != null)
                statsByRun[key] = new RunStats();
            lastHitBy.Clear();
            lastKillTime.Clear();
        }

        public static void EnsureHooked(List<Player> players)
        {
            if (!damageHooked)
            {
                Health.globalTakeDamageActualHandlers += OnAnyDamage;
                damageHooked = true;
            }

            foreach (Player player in players)
            {
                Health health = player.health;
                if (health == null || !evadeHooked.Add(health.GetInstanceID()))
                    continue;
                Player captured = player;
                health.evadeEventHandlers += () => For(captured).Evades++;
            }
        }

        // Called every frame for each active player: speed and distance.
        public static void TrackMovement(Player player)
        {
            RunStats stats = For(player);
            Rigidbody2D body = player.rigidbody2D;
            if (body != null && body.velocity.magnitude > stats.TopSpeed)
                stats.TopSpeed = body.velocity.magnitude;

            int id = player.GetInstanceID();
            Vector2 position = player.transform.position;
            Vector2 previous;
            if (lastPosition.TryGetValue(id, out previous))
            {
                float step = Vector2.Distance(previous, position);
                if (step < MaxStepDistance)
                    stats.Distance += step;
            }
            lastPosition[id] = position;
        }

        public static void OnDash(Player player)
        {
            if (player != null)
                For(player).Dashes++;
        }

        public static void OnEntityFall(Entity entity)
        {
            if (!(entity is Enemy))
                return;

            Player player;
            if (!lastHitBy.TryGetValue(entity.GetInstanceID(), out player) || player == null)
                return;

            RunStats stats = For(player);
            stats.PitKnockoffs++;
            if (MarkKilled(entity))
                stats.Kills++;
        }

        private static void OnAnyDamage(AttackInfo attack, Entity attacker, Entity hurt)
        {
            if (attack == null || hurt == null)
                return;

            var hurtPlayer = hurt as Player;
            if (hurtPlayer != null)
            {
                For(hurtPlayer).DamageTaken += attack.damage;
                return;
            }

            if (!(hurt is Enemy))
                return;

            Player player = FindAttacker(attack, attacker);
            if (player == null)
                return;

            lastHitBy[hurt.GetInstanceID()] = player;
            HookDeath(hurt);

            RunStats stats = For(player);
            string skillID = string.IsNullOrEmpty(attack.skillID) ? "?" : attack.skillID;
            SkillRunStats skill = stats.Skill(skillID);

            stats.Hits++;
            skill.Hits++;
            stats.DamageDealt += attack.damage;
            skill.Damage += attack.damage;

            if (attack.damage > stats.BiggestHit)
            {
                stats.BiggestHit = attack.damage;
                stats.BiggestHitSkill = skillID;
            }

            if (attack.isCritical)
            {
                stats.Crits++;
                skill.Crits++;
                if (attack.damage > stats.BiggestCrit)
                {
                    stats.BiggestCrit = attack.damage;
                    stats.BiggestCritSkill = skillID;
                }
            }
        }

        // Credit a kill to whoever hit the enemy last. Enemies are pooled, so hook each once.
        private static void HookDeath(Entity enemy)
        {
            Health health = enemy.health;
            if (health == null || !deathHooked.Add(health.GetInstanceID()))
                return;
            health.deathEventHandlers += OnEnemyDeath;
        }

        private static void OnEnemyDeath(Entity entity)
        {
            try
            {
                Player player;
                if (entity != null && lastHitBy.TryGetValue(entity.GetInstanceID(), out player)
                    && player != null && MarkKilled(entity))
                    For(player).Kills++;
            }
            catch (Exception e)
            {
                ExtendedStatsPlugin.LogError("death", e);
            }
        }

        private static Player FindAttacker(AttackInfo attack, Entity attacker)
        {
            var player = attacker as Player ?? attack.entity as Player;
            if (player != null)
                return player;

            // Projectiles and summons may not carry the entity; fall back to the skill category.
            Player[] players = GameController.activePlayers;
            if (players == null || string.IsNullOrEmpty(attack.skillCategory))
                return null;
            foreach (Player candidate in players)
            {
                if (candidate != null && candidate.skillCategory == attack.skillCategory)
                    return candidate;
            }
            return null;
        }

        private static bool MarkKilled(Entity entity)
        {
            int id = entity.GetInstanceID();
            float last;
            if (lastKillTime.TryGetValue(id, out last) && Time.time - last < KillDedupeSeconds)
                return false;
            lastKillTime[id] = Time.time;
            return true;
        }
    }
}
