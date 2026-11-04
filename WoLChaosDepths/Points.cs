using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    // #114 (Matt, 2026-11-04): percentage stats become POINTS. A source's old fraction is read as
    // points (+0.12 crit = 12 points, -0.15 cooldown = 15 points); a stat's points are summed, then
    // converted once, so stacking can never reach 100%:
    //   toward 100% (crit chance, evade, move speed): bonus = 1 - 0.99^points;
    //   reductions (cooldown, damage taken): multiplier change = 0.99^points - 1;
    //   damage %, crit damage, healing stay linear (1 point = 1%).
    //
    // Step 2 (0.38.0): the shared REGISTER. Gear and stat talents are summed by StatMods; every other
    // source (pacts, cloak perks, archetype/talent mechanics, other mods) calls
    //     Points.Set(player, stat, source, points)
    // with the old percentage as signed points (crit +10% -> 10, a 30% cooldown cut -> -30), and 0
    // to take it off. GearTypeStats adds the register to the gear totals and applies each point
    // stat once. Stat keys are the gear affix keys:
    //   CritChance, Evade, MoveSpeed, DamageTaken, Cooldown, DashCooldown, ProjectileCooldown,
    //   MeleeCooldown, MovementCooldown, HoldCooldown, FireCooldown, AirCooldown, EarthCooldown,
    //   LightningCooldown, WaterCooldown.
    // Other mods can call it without a reference by reflection:
    //   Type.GetType("WoLChaosDepths.Points, WoLChaosDepths")
    //       .GetMethod("Set").Invoke(null, new object[] { player, "CritChance", "MyMod.Source", 10f });
    public static class Points
    {
        public const float Base = 0.99f;

        // The stats the register converts (anything else set here is ignored by the appliers).
        public static readonly string[] PointStats =
        {
            "CritChance", "Evade", "MoveSpeed", "DamageTaken", "Cooldown", "DashCooldown",
            "ProjectileCooldown", "MeleeCooldown", "MovementCooldown", "HoldCooldown",
            "FireCooldown", "AirCooldown", "EarthCooldown", "LightningCooldown", "WaterCooldown"
        };

        // A summed fraction (e.g. 0.38) -> the bonus to add (0.317). Negatives stay as they are.
        public static float Toward100(float fraction)
        {
            if (fraction <= 0f)
                return fraction;
            return 1f - Mathf.Pow(Base, fraction * 100f);
        }

        // A summed NEGATIVE fraction (a cut, e.g. -0.38) -> the Multiplicative mod value
        // (0.99^38 - 1 = -0.317). Positives (a penalty) stay linear.
        public static float Reduction(float fraction)
        {
            if (fraction >= 0f)
                return fraction;
            return Mathf.Pow(Base, -fraction * 100f) - 1f;
        }

        public static string Show(float fraction, bool reduction) =>
            $"{Mathf.Abs(fraction) * 100f:0} pts ({(reduction ? Reduction(fraction) : Toward100(fraction)) * 100f:+0;-0}%)";

        // ---- The register: player -> stat -> source -> fraction ----

        private static readonly Dictionary<int, Dictionary<string, Dictionary<string, float>>> register =
            new Dictionary<int, Dictionary<string, Dictionary<string, float>>>();

        /// <summary>Puts one source's points on a stat for this wizard (signed like the old
        /// percentage: +10 crit, -30 cooldown); 0 takes the source off. Cheap to call every frame:
        /// nothing changes unless the value does.</summary>
        public static void Set(Player player, string stat, string source, float points)
        {
            if (player == null || string.IsNullOrEmpty(stat) || string.IsNullOrEmpty(source))
                return;
            int id = player.GetInstanceID();
            Dictionary<string, Dictionary<string, float>> stats;
            if (!register.TryGetValue(id, out stats))
            {
                if (Mathf.Approximately(points, 0f))
                    return;
                if (register.Count > 16)
                    register.Clear();   // players from old sessions
                register[id] = stats = new Dictionary<string, Dictionary<string, float>>();
            }
            Dictionary<string, float> sources;
            if (!stats.TryGetValue(stat, out sources))
            {
                if (Mathf.Approximately(points, 0f))
                    return;
                stats[stat] = sources = new Dictionary<string, float>();
            }
            if (Mathf.Approximately(points, 0f))
                sources.Remove(source);
            else
                sources[source] = points / 100f;
        }

        /// <summary>Takes a source off every stat of this wizard.</summary>
        public static void Clear(Player player, string source)
        {
            Dictionary<string, Dictionary<string, float>> stats;
            if (player == null || !register.TryGetValue(player.GetInstanceID(), out stats))
                return;
            foreach (Dictionary<string, float> sources in stats.Values)
                sources.Remove(source);
        }

        /// <summary>The registered sum on a stat, as a fraction (0.12 = 12 points).</summary>
        public static float Of(Player player, string stat)
        {
            Dictionary<string, Dictionary<string, float>> stats;
            Dictionary<string, float> sources;
            if (player == null || !register.TryGetValue(player.GetInstanceID(), out stats) || !stats.TryGetValue(stat, out sources))
                return 0f;
            float sum = 0f;
            foreach (KeyValuePair<string, float> pair in sources)
                if (Counts(pair.Key))
                    sum += pair.Value;
            return sum;
        }

        // Sources from the talent systems (named "Pact.", "Perk.", "Talent.") stop counting the moment
        // [Talents] is turned off, even though those systems stop ticking and can't take them off.
        private static bool Counts(string source) =>
            Talents.Enabled || !(source.StartsWith("Pact.") || source.StartsWith("Perk.") || source.StartsWith("Talent."));

        /// <summary>For the stat sheet: "name +12" per registered source on a stat.</summary>
        public static IEnumerable<KeyValuePair<string, float>> Sources(Player player, string stat)
        {
            Dictionary<string, Dictionary<string, float>> stats;
            Dictionary<string, float> sources;
            if (player == null || !register.TryGetValue(player.GetInstanceID(), out stats) || !stats.TryGetValue(stat, out sources))
                yield break;
            foreach (KeyValuePair<string, float> pair in sources)
                yield return pair;
        }
    }
}
