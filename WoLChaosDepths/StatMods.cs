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
            // Phase 1a: the worn cloak's talents count like gear (nothing while [Talents] is off).
            TalentEffects.AddStats(player, totals);
            // #66 (Matt): the signature is harder to earn, for every wizard; it adds to the same
            // stat cloaks and talents change (the hit's signature charge multiplier).
            float gain = SignatureGain;
            if (!Mathf.Approximately(gain, 0f))
            {
                float current;
                totals.TryGetValue("SignatureCharge", out current);
                totals["SignatureCharge"] = current + gain;
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
                Skill(category, StatData.cdStr, "Cooldown", Mathf.Max(Get(totals, "Cooldown"), -ChaosDepthsPlugin.GearCooldownCap), VarStatModType.Multiplicative);
                Skill(category, StatData.kbMultiStr, "Knockback", Get(totals, "Knockback"), VarStatModType.Multiplicative);
                // The fields the game's Fury cloak uses (OutfitModType.ODRate / ODDamage).
                Skill(category, StatData.odProgStr, "SignatureCharge", Get(totals, "SignatureCharge"), VarStatModType.Multiplicative);
                Skill(category, StatData.odDmgStr, "SignatureDamage", Get(totals, "SignatureDamage"), VarStatModType.Multiplicative);
                // Elemental talents (Phase 1a): only that element's arcana, by the game's own test
                // (status effects such as burns excluded, as the game's element relics do).
                foreach (string element in Elements)
                {
                    string ele = element;
                    StatManager.ModApplyConditional only = sd => !sd.GetValue<bool>(StatData.isStatStr) && sd.GetValue<string>(StatData.eleStr) == ele;
                    Skill(category, StatData.damageStr, ele + "Damage", Get(totals, ele + "Damage"), VarStatModType.Multiplicative, only);
                    Skill(category, StatData.cdStr, ele + "Cooldown", Get(totals, ele + "Cooldown"), VarStatModType.Multiplicative, only);
                }
                Health health = player.health;
                if (health != null)
                {
                    Stat(health.healthStat, "Health", Get(totals, "Health") + levelHealth, VarStatModType.Multiplicative, wasFull);
                    if (!wasFull && health.healthStat != null && hpBefore > 0)
                        health.CurrentHealthValue = Mathf.Min(hpBefore, (int)health.healthStat.ModifiedValue);
                    Stat(health.healModifierStat, "Healing", Get(totals, "Healing"), VarStatModType.Multiplicative, false);
                    Stat(health.damageTakenStat, "DamageTaken", Mathf.Max(Get(totals, "DamageTaken"), -ChaosDepthsPlugin.GearDamageTakenCap), VarStatModType.Multiplicative, false);
                    Stat(health.evadeStat, "Evade", Mathf.Min(Get(totals, "Evade"), ChaosDepthsPlugin.GearEvadeCap), VarStatModType.Additive, false);
                    Stat(health.critHealModStat, "HealCrit", Get(totals, "HealCrit"), VarStatModType.Additive, false);
                }
                if (player.movement != null)
                    Stat(player.movement.moveSpeedStat, "MoveSpeed", Get(totals, "MoveSpeed"), VarStatModType.Multiplicative, false);
                Activation(player, Get(totals, "ActivationSpeed"));
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
                StatManager.ModifyAllStatData(new NumVarStatMod(Prefix + "Knockback", 0f), category, StatData.kbMultiStr, null, false);
                StatManager.ModifyAllStatData(new NumVarStatMod(Prefix + "SignatureCharge", 0f), category, StatData.odProgStr, null, false);
                StatManager.ModifyAllStatData(new NumVarStatMod(Prefix + "SignatureDamage", 0f), category, StatData.odDmgStr, null, false);
                foreach (string element in Elements)
                {
                    StatManager.ModifyAllStatData(new NumVarStatMod(Prefix + element + "Damage", 0f), category, StatData.damageStr, null, false);
                    StatManager.ModifyAllStatData(new NumVarStatMod(Prefix + element + "Cooldown", 0f), category, StatData.cdStr, null, false);
                }
                Health health = player.health;
                if (health != null)
                {
                    health.healthStat?.RemoveMod(Prefix + "Health");
                    health.healModifierStat?.RemoveMod(Prefix + "Healing");
                    health.damageTakenStat?.RemoveMod(Prefix + "DamageTaken");
                    health.evadeStat?.RemoveMod(Prefix + "Evade");
                    health.critHealModStat?.RemoveMod(Prefix + "HealCrit");
                }
                player.movement?.moveSpeedStat?.RemoveMod(Prefix + "MoveSpeed");
                Activation(player, 0f);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Couldn't remove stats: {e.Message}");
            }
            applied.Remove(player.GetInstanceID());
        }

        private static readonly string[] Elements = { "Fire", "Air", "Earth", "Lightning", "Water" };

        // Activation speed (gear affix "ActivationSpeed", Matt 2026-11-03): the wizard's animation
        // speed handler, the way the game's own attack-speed relic speeds casts. Off, then on at the
        // new value.
        private static void Activation(Player player, float value)
        {
            if (player == null || player.animSpeedHandler == null)
                return;
            string id = Prefix + "ActivationSpeed";
            player.animSpeedHandler.Modify(new NumVarStatMod(id, 0f, 10, VarStatModType.Multiplicative, false), false);
            if (!Mathf.Approximately(value, 0f))
                player.animSpeedHandler.Modify(new NumVarStatMod(id, value, 10, VarStatModType.Multiplicative, false), true);
        }

        // [Signature] ChargeGain (#66), bound on first use in Chaos Depths' own config.
        private static BepInEx.Configuration.ConfigEntry<float> signatureGain;
        public static float SignatureGain
        {
            get
            {
                if (signatureGain == null && ChaosDepthsPlugin.Instance != null)
                    signatureGain = ChaosDepthsPlugin.Instance.Config.Bind("Signature", "ChargeGain", -0.25f,
                        new BepInEx.Configuration.ConfigDescription(
                            "How fast every wizard's signature bar fills from hits, against the game's own rate: -0.25 = 25% slower " +
                            "(Matt, #66), 0 = the game's rate. Cloak, talent and perk bonuses add on top.",
                            new BepInEx.Configuration.AcceptableValueRange<float>(-0.9f, 1f)));
                return signatureGain != null ? Mathf.Clamp(signatureGain.Value, -0.9f, 1f) : -0.25f;
            }
        }

        private static void Skill(string category, string field, string name, float value, VarStatModType type,
            StatManager.ModApplyConditional only = null)
        {
            if (Mathf.Approximately(value, 0f) || string.IsNullOrEmpty(field))
                return;
            StatManager.ModifyAllStatData(new NumVarStatMod(Prefix + name, value, 10, type), category, field, only);
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
