using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    // Gear affixes for one kind of arcana (Matt, 2026-11-03: "ability type specific dmg CD
    // (projectile, mobility, dash, basic, etc.)"). Every half second each wizard's worn items are
    // summed, and the totals go on that wizard's own arcana stats (state.skillData) by the game's
    // own type flags: isBasic, isDash, isProjectileSkill, isMeleeSkill, isMovementSkill,
    // isSummonSkill. An arcana that's several kinds gets each matching bonus. Stats are rebuilt by
    // the game on level loads, so each one is set again when it's new or the total changed.
    public static class GearTypeStats
    {
        private const string Prefix = "ChaosDepths_GT_";
        private const float Every = 0.1f;   // timed sources (Flurry stacks, sprinting) land within 0.1 s
        private static readonly Dictionary<KeyValuePair<object, string>, float> applied = new Dictionary<KeyValuePair<object, string>, float>();
        private static bool loggedError;

        public static void Install()
        {
            var host = new GameObject("ChaosDepths GearTypeStats");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<Runner>();
        }

        private class Runner : MonoBehaviour
        {
            private float next;

            private void Update()
            {
                if (Time.unscaledTime < next || ChaosDepthsPlugin.Instance == null)
                    return;
                next = Time.unscaledTime + Every;
                try
                {
                    Player[] players = GameController.activePlayers;
                    if (players != null)
                        foreach (Player p in players)
                            if (p != null && p.skillsDict != null)
                                Apply(p, ChaosDepthsPlugin.Instance.SaveOf(p));
                }
                catch (Exception e)
                {
                    if (!loggedError)
                    {
                        loggedError = true;
                        ChaosDepthsPlugin.LogError($"Gear type stats: {e.Message}");
                    }
                }
            }
        }

        // #114 step 2 (0.38.0): one pass for every POINT stat. The totals are StatMods' (gear + stat
        // talents, summed once there; this used to sum the gear a second time) plus the point
        // register (pacts, cloak perks, mechanics, other mods), converted once per stat.
        private static void Apply(Player p, CharacterSave save)
        {
            var totals = new Dictionary<string, float>(StatMods.TotalsOf(p));
            foreach (string stat in Points.PointStats)
            {
                float reg = Points.Of(p, stat);
                if (!Mathf.Approximately(reg, 0f))
                    totals[stat] = Get(totals, stat) + reg;
            }
            Wide(p, totals);
            foreach (Player.SkillState s in p.skillsDict.Values)
            {
                if (s == null || s.skillData == null)
                    continue;
                float damage = 0f, cooldown = 0f;
                if (s.isBasic)
                    damage += Get(totals, "BasicDamage");
                if (s.isDash)
                    cooldown += Get(totals, "DashCooldown");
                if (s.isProjectileSkill)
                {
                    damage += Get(totals, "ProjectileDamage");
                    cooldown += Get(totals, "ProjectileCooldown");
                }
                if (s.isMeleeSkill)
                {
                    damage += Get(totals, "MeleeDamage");
                    cooldown += Get(totals, "MeleeCooldown");
                }
                if (s.isMovementSkill && !s.isDash)
                {
                    damage += Get(totals, "MovementDamage");
                    cooldown += Get(totals, "MovementCooldown");
                }
                if (s.isSummonSkill)
                    damage += Get(totals, "SummonDamage");
                if (s.isHoldSkill)
                {
                    damage += Get(totals, "HoldDamage");
                    cooldown += Get(totals, "HoldCooldown");
                }
                // #114 (0.37.0): ALL of an arcana's cooldown points (generic + type + its element) are
                // summed and applied once: cooldown x 0.99^points, so it never reaches 0 (the 0.36
                // 60% cap is gone). An affix's stored -15% is 15 points.
                string element = null;
                try { element = s.skillData.GetValue<string>(StatData.eleStr); } catch { }
                cooldown += Get(totals, "Cooldown");
                if (!string.IsNullOrEmpty(element) && !s.skillData.GetValue<bool>(StatData.isStatStr))
                    cooldown += Get(totals, element + "Cooldown");
                Set(s.skillData, StatData.damageStr, "Damage", damage);
                Set(s.skillData, StatData.cdStr, "Cooldown", Points.Reduction(cooldown));
            }
        }

        // The wizard-wide point stats: crit chance on all their arcana (1 - 0.99^points), evade
        // (1 - 0.99^points), move speed (soft, 1 - 0.99^points), damage taken (x 0.99^points).
        private static void Wide(Player p, Dictionary<string, float> totals)
        {
            Crit(p, Points.Toward100(Get(totals, "CritChance")));
            Health h = p.health;
            if (h != null)
            {
                Set(h.evadeStat, "Evade", Points.Toward100(Get(totals, "Evade")), VarStatModType.Additive);
                Set(h.damageTakenStat, "DamageTaken", Points.Reduction(Get(totals, "DamageTaken")), VarStatModType.Multiplicative);
            }
            if (p.movement != null)
                Set(p.movement.moveSpeedStat, "MoveSpeed", Points.Toward100(Get(totals, "MoveSpeed")), VarStatModType.Multiplicative);
        }

        private static void Crit(Player p, float value)
        {
            var key = new KeyValuePair<object, string>(p, Prefix + "CritChance");
            float had;
            if (applied.TryGetValue(key, out had) && Mathf.Approximately(had, value))
                return;
            string id = Prefix + "CritChance";
            StatManager.ModifyAllStatData(new NumVarStatMod(id, 0f), p.skillCategory, StatData.critChStr, null, false);
            if (!Mathf.Approximately(value, 0f))
                StatManager.ModifyAllStatData(new NumVarStatMod(id, value, 10, VarStatModType.Additive), p.skillCategory, StatData.critChStr);
            applied[key] = value;
        }

        private static void Set(NumVarStat stat, string node, float value, VarStatModType type)
        {
            if (stat == null)
                return;
            string id = Prefix + node;
            var key = new KeyValuePair<object, string>(stat, id);
            float had;
            if (applied.TryGetValue(key, out had) && Mathf.Approximately(had, value))
                return;
            stat.RemoveMod(id);
            if (!Mathf.Approximately(value, 0f))
                stat.AddMod(new NumVarStatMod(id, value, 10, type, false));
            applied[key] = value;
        }

        private static float Get(Dictionary<string, float> totals, string key)
        {
            float v;
            return totals.TryGetValue(key, out v) ? v : 0f;
        }

        private static void Set(StatData data, string field, string node, float value)
        {
            string id = Prefix + node;
            var key = new KeyValuePair<object, string>(data, id);
            float had;
            if (applied.TryGetValue(key, out had) && Mathf.Approximately(had, value))
                return;
            if (applied.Count > 4096)
                applied.Clear();
            data.RemoveMod(field, id);
            if (!Mathf.Approximately(value, 0f))
                data.AddMod(field, new NumVarStatMod(id, value, 10, VarStatModType.Multiplicative, false));
            applied[key] = value;
        }
    }
}
