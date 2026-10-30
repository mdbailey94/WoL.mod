using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace WoLDamageMeter
{
    public class SkillTotals
    {
        public int Damage;
        public int Hits;
        public int Crits;
    }

    // One burst of damage: starts on the first hit, ends after the idle gap (see the plugin).
    // Times are Time.time, so pausing the game doesn't count towards the session.
    public class MeterSession
    {
        // DPS divides by at least this many seconds, so a single hit doesn't read as infinite DPS.
        public const float MinDpsSeconds = 1f;
        // Arcana listed in the overlay's breakdown (the log lists them all).
        private const int TopSkills = 4;
        // Skill ID used when the hit doesn't say which skill it came from.
        private const string UnknownSkill = "?";

        private const string Dim = "#9aa";
        private const string Accent = "#fd5";

        public readonly float StartTime;
        public float LastHitTime;
        public int Damage;
        public int Hits;
        public int Crits;
        public int BiggestHit;
        public bool BiggestWasCrit;
        public string BiggestSkill;
        public readonly Dictionary<string, SkillTotals> BySkill = new Dictionary<string, SkillTotals>();
        private readonly HashSet<int> targets = new HashSet<int>();

        public MeterSession(float now)
        {
            StartTime = now;
            LastHitTime = now;
        }

        public void Add(int damage, bool crit, string skillID, int targetID, float now)
        {
            if (string.IsNullOrEmpty(skillID))
                skillID = UnknownSkill;

            SkillTotals skill;
            if (!BySkill.TryGetValue(skillID, out skill))
                BySkill[skillID] = skill = new SkillTotals();

            LastHitTime = now;
            targets.Add(targetID);
            Damage += damage;
            Hits++;
            skill.Damage += damage;
            skill.Hits++;
            if (crit)
            {
                Crits++;
                skill.Crits++;
            }
            if (damage > BiggestHit)
            {
                BiggestHit = damage;
                BiggestWasCrit = crit;
                BiggestSkill = skillID;
            }
        }

        // First hit to last hit, so the idle gap that ends the session isn't counted.
        public float Duration => LastHitTime - StartTime;

        public float Dps => DpsOf(Damage);

        private float DpsOf(int damage) => damage / Mathf.Max(Duration, MinDpsSeconds);

        private List<KeyValuePair<string, SkillTotals>> Ranked() =>
            BySkill.OrderByDescending(pair => pair.Value.Damage).ToList();

        // The overlay text. `ended` switches the header from live to final.
        public string Overlay(bool ended)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<b>DAMAGE METER</b>  {Color(ended ? "(done)" : "(live)", ended ? Dim : Accent)}");
            Row(sb, "DPS", $"{Dps:0}");
            Row(sb, "Damage", Damage.ToString());
            Row(sb, "Hits", $"{Hits}  {Color($"({Crits} crits)", Dim)}");
            string crit = BiggestWasCrit ? " " + Color("crit", Accent) : string.Empty;
            Row(sb, "Biggest hit", $"{BiggestHit}{crit}  {Color($"({SkillName(BiggestSkill)})", Dim)}");
            Row(sb, "Time", $"{Duration:0.0} s");
            if (targets.Count > 1)
                Row(sb, "Targets", targets.Count.ToString());

            List<KeyValuePair<string, SkillTotals>> ranked = Ranked();
            if (ranked.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<b>BY ARCANA</b>");
                foreach (var pair in ranked.Take(TopSkills))
                    Row(sb, SkillName(pair.Key),
                        $"{pair.Value.Damage}  {Color($"({Percent(pair.Value.Damage)}, {pair.Value.Hits} hits)", Dim)}");
                if (ranked.Count > TopSkills)
                    sb.AppendLine(Color($"+{ranked.Count - TopSkills} more (see log)", Dim));
            }
            return sb.ToString().TrimEnd();
        }

        // Log lines for the end of the session: the summary, then one line per skill.
        public List<string> LogLines()
        {
            string biggest = BiggestWasCrit ? $"{BiggestHit} (crit, {SkillName(BiggestSkill)})" : $"{BiggestHit} ({SkillName(BiggestSkill)})";
            var lines = new List<string>
            {
                $"Session: {Duration:0.0} s, {Damage} dmg, {Dps:0} DPS, {Hits} hits, {Crits} crits, " +
                $"biggest {biggest}, {targets.Count} target{(targets.Count == 1 ? "" : "s")}"
            };
            foreach (var pair in Ranked())
            {
                SkillTotals skill = pair.Value;
                lines.Add($"  {SkillName(pair.Key)} [{pair.Key}]: {skill.Damage} dmg ({Percent(skill.Damage)}), " +
                          $"{DpsOf(skill.Damage):0} DPS, {skill.Hits} hits, {skill.Crits} crits");
            }
            return lines;
        }

        private string Percent(int part) => Damage > 0 ? $"{100f * part / Damage:0}%" : "0%";

        // Same lookup ExtendedStats uses: the game's display name, else the raw ID.
        public static string SkillName(string skillID)
        {
            if (string.IsNullOrEmpty(skillID) || skillID == UnknownSkill)
                return "Other";
            TextManager.SkillInfo info;
            if (TextManager.skillInfoDict != null && TextManager.skillInfoDict.TryGetValue(skillID, out info)
                && !string.IsNullOrEmpty(info.displayName))
                return info.displayName;
            return skillID;
        }

        private static string Color(string text, string color) => $"<color={color}>{text}</color>";

        private static void Row(StringBuilder sb, string label, string value) =>
            sb.AppendLine($"{label}: <b>{value}</b>");
    }
}
