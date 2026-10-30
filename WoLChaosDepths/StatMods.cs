using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    // Turns a character's level and equipped items into the game's own stat modifiers on one
    // player, and takes them off again. Everything goes through named mods, so re-applying first
    // removes the old ones: nothing compounds.
    public static class StatMods
    {
        private const string Prefix = "ChaosDepths_";
        private static readonly Dictionary<int, string> applied = new Dictionary<int, string>();

        // Health per level above 1, as a fraction (0.02 = +2%).
        public static float HealthPerLevel => ChaosDepthsPlugin.HealthPerLevel;

        public static void Apply(Player player, CharacterSave save)
        {
            if (player == null || save == null)
                return;
            var totals = new Dictionary<string, float>();
            foreach (string id in save.equipped)
            {
                ItemData item = FindItem(save, id);
                if (item == null)
                    continue;
                foreach (AffixData affix in item.affixes)
                {
                    float current;
                    totals.TryGetValue(affix.key, out current);
                    totals[affix.key] = current + affix.value;
                }
            }
            float levelHealth = HealthPerLevel * (save.level - 1);

            // Only touch the game when something changed.
            string signature = levelHealth.ToString("0.####") + "|" + Join(totals);
            string before;
            int key = player.GetInstanceID();
            if (applied.TryGetValue(key, out before) && before == signature)
                return;
            // A new level or new gear doesn't heal (Matt, 0.15.2): a wizard at full health stays
            // full under the new maximum (a run's start), anyone else keeps the health they had.
            // Up to 0.15.1 the health mod filled to the new maximum on every change.
            Health h = player.health;
            int hpBefore = h != null && h.healthStat != null ? h.CurrentHealthValue : 0;
            bool wasFull = h == null || h.healthStat == null || hpBefore >= (int)h.healthStat.ModifiedValue;
            Remove(player);

            try
            {
                string category = player.skillCategory;
                Skill(category, StatData.damageStr, "Damage", Get(totals, "Damage"), VarStatModType.Multiplicative);
                Skill(category, StatData.critChStr, "CritChance", Get(totals, "CritChance"), VarStatModType.Additive);
                Skill(category, StatData.critDmgStr, "CritDamage", Get(totals, "CritDamage"), VarStatModType.Additive);
                Skill(category, StatData.cdStr, "Cooldown", Get(totals, "Cooldown"), VarStatModType.Multiplicative);
                Health health = player.health;
                if (health != null)
                {
                    Stat(health.healthStat, "Health", Get(totals, "Health") + levelHealth, VarStatModType.Multiplicative, wasFull);
                    if (!wasFull && health.healthStat != null && hpBefore > 0)
                        health.CurrentHealthValue = Mathf.Min(hpBefore, (int)health.healthStat.ModifiedValue);
                    Stat(health.healModifierStat, "Healing", Get(totals, "Healing"), VarStatModType.Multiplicative, false);
                    Stat(health.damageTakenStat, "DamageTaken", Get(totals, "DamageTaken"), VarStatModType.Multiplicative, false);
                    Stat(health.evadeStat, "Evade", Get(totals, "Evade"), VarStatModType.Additive, false);
                }
                if (player.movement != null)
                    Stat(player.movement.moveSpeedStat, "MoveSpeed", Get(totals, "MoveSpeed"), VarStatModType.Multiplicative, false);
                applied[key] = signature;
                ChaosDepthsPlugin.Log($"Stats applied to player {player.playerID + 1}: level {save.level} (+{levelHealth * 100f:0}% health), items: " +
                    (totals.Count == 0 ? "none" : Join(totals)));
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Couldn't apply stats: {e.Message}");
            }
        }

        public static void Remove(Player player)
        {
            if (player == null)
                return;
            try
            {
                string category = player.skillCategory;
                foreach (string field in new[] { StatData.damageStr, StatData.critChStr, StatData.critDmgStr, StatData.cdStr })
                {
                    foreach (string name in new[] { "Damage", "CritChance", "CritDamage", "Cooldown" })
                        StatManager.ModifyAllStatData(new NumVarStatMod(Prefix + name, 0f), category, field, null, false);
                }
                Health health = player.health;
                if (health != null)
                {
                    health.healthStat?.RemoveMod(Prefix + "Health");
                    health.healModifierStat?.RemoveMod(Prefix + "Healing");
                    health.damageTakenStat?.RemoveMod(Prefix + "DamageTaken");
                    health.evadeStat?.RemoveMod(Prefix + "Evade");
                }
                player.movement?.moveSpeedStat?.RemoveMod(Prefix + "MoveSpeed");
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Couldn't remove stats: {e.Message}");
            }
            applied.Remove(player.GetInstanceID());
        }

        private static void Skill(string category, string field, string name, float value, VarStatModType type)
        {
            if (Mathf.Approximately(value, 0f) || string.IsNullOrEmpty(field))
                return;
            StatManager.ModifyAllStatData(new NumVarStatMod(Prefix + name, value, 10, type), category, field);
        }

        private static void Stat(NumVarStat stat, string name, float value, VarStatModType type, bool fill)
        {
            if (stat == null || Mathf.Approximately(value, 0f))
                return;
            stat.AddMod(new NumVarStatMod(Prefix + name, value, 10, type, fill));
        }

        private static float Get(Dictionary<string, float> totals, string key)
        {
            float value;
            return totals.TryGetValue(key, out value) ? value : 0f;
        }

        public static ItemData FindItem(CharacterSave save, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (ItemData item in save.inventory)
                if (item.id == id)
                    return item;
            return null;
        }

        private static string Join(Dictionary<string, float> totals)
        {
            var parts = new List<string>();
            foreach (KeyValuePair<string, float> pair in totals)
                parts.Add($"{pair.Key} {pair.Value * 100f:+0;-0}%");
            parts.Sort();
            return string.Join(", ", parts.ToArray());
        }
    }
}
