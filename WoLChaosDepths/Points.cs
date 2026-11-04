using UnityEngine;

namespace WoLChaosDepths
{
    // #114 (Matt, 2026-11-04): percentage stats become POINTS. A source's old fraction is read as
    // points (+0.12 crit = 12 points, -0.15 cooldown = 15 points); a stat's points are summed, then
    // converted once, so stacking can never reach 100%:
    //   toward 100% (crit chance, evade, move speed): bonus = 1 - 0.99^points;
    //   reductions (cooldown, damage taken): multiplier change = 0.99^points - 1;
    //   damage %, crit damage, healing stay linear (1 point = 1%).
    public static class Points
    {
        public const float Base = 0.99f;

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
    }
}
