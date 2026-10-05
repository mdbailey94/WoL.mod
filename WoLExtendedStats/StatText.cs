using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WoLExtendedStats
{
    // Builds the text for the menu info boxes, the F2 overlay and the end-of-run summary.
    // `rich` controls whether Unity rich-text colour tags are used (uGUI Text may have them off).
    public static class StatText
    {
        private const string Dim = "#9aa";
        private const string Accent = "#fd5";

        // Shown under the game's description when an arcana is highlighted in the equip menu.
        public static string Arcana(Player player, Player.SkillState skill, bool rich)
        {
            var sb = new StringBuilder();
            StatData data = skill.skillData;
            if (data != null)
            {
                float damage = Stat(data, StatData.damageStr, true);
                float critChance = Stat(data, StatData.critChStr, false);
                float critDamage = Stat(data, StatData.critDmgStr, false);
                float cooldown = Cooldown(player, skill, data);
                sb.Append($"Damage {damage:0}  ·  Crit {Percent(critChance)} ({critDamage:0.##}x)  ·  Cooldown {cooldown:0.##}s");
            }

            SkillRunStats run;
            if (RunTracker.For(player).BySkill.TryGetValue(skill.skillID, out run) && run.Hits > 0)
            {
                sb.Append('\n');
                sb.Append(Color($"This run: {run.Damage} dmg · {run.Hits} hits · {run.Crits} crits", Dim, rich));
            }
            return sb.ToString();
        }

        // Shown under the cloak's description when the outfit is highlighted.
        public static string Wizard(Player player, bool rich)
        {
            var lines = new List<string>();
            Health health = player.health;
            if (health != null)
            {
                string shield = health.CurrentShieldValue > 0 ? $"  ·  Shield {health.CurrentShieldValue}" : string.Empty;
                lines.Add($"Health {health.CurrentHealthValue}/{Value(health.healthStat):0}{shield}");
                lines.Add($"Armor {Percent(Value(health.armorStat))}  ·  Evade {Percent(Value(health.evadeStat))}");
                lines.Add($"Damage taken {Multiplier(Value(health.damageTakenStat))}  ·  Healing {Multiplier(Value(health.healModifierStat))}");
            }

            Movement movement = player.movement;
            if (movement != null)
                lines.Add($"Move speed {Value(movement.moveSpeedStat):0.#}  ·  Dash speed {Value(movement.dashSpeedStat):0.#}");

            if (Player.goldWallet != null && Player.platWallet != null)
                lines.Add(Color($"Gold {Player.goldWallet.balance}  ·  Platinum {Player.platWallet.balance}", Dim, rich));

            return string.Join("\n", lines.ToArray());
        }

        // The F2 overlay: everything at once.
        public static string Overlay(Player player, string header)
        {
            var sb = new StringBuilder(header);
            sb.AppendLine("<b>WIZARD</b>");
            sb.AppendLine(Wizard(player, true));

            if (player.assignedSkills != null)
            {
                sb.AppendLine();
                sb.AppendLine("<b>ARCANA</b>");
                foreach (Player.SkillState skill in player.assignedSkills)
                {
                    if (skill == null || string.IsNullOrEmpty(skill.skillID))
                        continue;
                    string enhanced = skill.IsEmpowered ? Color(" (enhanced)", Accent, true) : string.Empty;
                    sb.AppendLine($"<b>{SkillName(skill.skillID)}</b>{enhanced}");
                    sb.AppendLine(Arcana(player, skill, true));
                }
            }

            sb.AppendLine();
            sb.Append(RunSummary(RunTracker.For(player), "THIS RUN SO FAR"));
            return sb.ToString().TrimEnd();
        }

        public static string RunSummary(RunStats run, string title)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<b>{title}</b>");
            float critRate = run.Hits > 0 ? (float)run.Crits / run.Hits : 0f;

            Row(sb, "Damage dealt", run.DamageDealt.ToString());
            Row(sb, "Hits landed", run.Hits.ToString());
            Row(sb, "Critical hits", $"{run.Crits}  {Color($"({Percent(critRate)} of hits)", Dim, true)}");
            Row(sb, "Biggest crit", Biggest(run.BiggestCrit, run.BiggestCritSkill));
            Row(sb, "Biggest hit", Biggest(run.BiggestHit, run.BiggestHitSkill));
            Row(sb, "Enemies defeated", run.Kills.ToString());
            Row(sb, "Knocked into pits", run.PitKnockoffs.ToString());
            Row(sb, "Damage taken", run.DamageTaken.ToString());
            Row(sb, "Attacks evaded", run.Evades.ToString());
            Row(sb, "Dashes", run.Dashes.ToString());
            Row(sb, "Top speed", $"{run.TopSpeed:0.#}");
            Row(sb, "Distance travelled", $"{run.Distance:0}");

            var top = run.BySkill
                .Where(pair => pair.Key != "?" && pair.Value.Damage > 0)
                .OrderByDescending(pair => pair.Value.Damage)
                .Take(4)
                .ToList();
            if (top.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<b>TOP ARCANA</b>");
                foreach (var pair in top)
                    Row(sb, SkillName(pair.Key), $"{pair.Value.Damage}  {Color($"({pair.Value.Crits} crits)", Dim, true)}");
            }
            return sb.ToString().TrimEnd();
        }

        private static string Biggest(int amount, string skillID) =>
            amount > 0 ? $"{amount}  {Color($"({SkillName(skillID)})", Dim, true)}" : "-";

        // Reads a skill stat with relic/outfit modifiers applied. Each field is stored as a list
        // of ints or floats, so a failed read on one field just shows 0 instead of hiding the text.
        private static float Stat(StatData data, string field, bool isInt)
        {
            try
            {
                return isInt
                    ? data.ApplyVarStatMod(field, data.GetValue<int>(field))
                    : data.ApplyVarStatMod(field, data.GetValue<float>(field));
            }
            catch
            {
                return 0f;
            }
        }

        private static float Cooldown(Player player, Player.SkillState skill, StatData data)
        {
            try
            {
                if (player.cooldownManager != null && player.cooldownManager.cooldowns != null
                    && player.cooldownManager.cooldowns.ContainsKey(skill.skillID))
                    return player.cooldownManager.GetMaxTime(skill.skillID);
            }
            catch
            {
                // Fall back to the stat value below.
            }
            return Stat(data, StatData.cdStr, false);
        }

        public static string SkillName(string skillID)
        {
            if (string.IsNullOrEmpty(skillID))
                return "?";
            TextManager.SkillInfo info;
            if (TextManager.skillInfoDict != null && TextManager.skillInfoDict.TryGetValue(skillID, out info)
                && !string.IsNullOrEmpty(info.displayName))
                return info.displayName;
            return skillID;
        }

        private static float Value(NumVarStat stat) => stat != null ? stat.CurrentValue : 0f;

        private static string Percent(float fraction) => $"{fraction * 100f:0.#}%";

        private static string Multiplier(float value) => $"{value:0.##}x";

        private static string Color(string text, string color, bool rich) =>
            rich ? $"<color={color}>{text}</color>" : text;

        private static void Row(StringBuilder sb, string label, string value) =>
            sb.AppendLine($"{label}: <b>{value}</b>");
    }
}
