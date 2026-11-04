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
        private const float Every = 0.5f;
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

        private static void Apply(Player p, CharacterSave save)
        {
            var totals = new Dictionary<string, float>();
            if (save != null && save.equipped != null)
                foreach (string id in save.equipped)
                {
                    ItemData item = string.IsNullOrEmpty(id) ? null : StatMods.FindItem(save, id);
                    if (item == null)
                        continue;
                    foreach (AffixData a in item.affixes)
                    {
                        float had;
                        totals.TryGetValue(a.key, out had);
                        totals[a.key] = had + a.value;
                    }
                }
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
