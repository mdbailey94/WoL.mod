using System;

namespace WoLMomentumSpear
{
    public static class SpearRules
    {
        public static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        public static float Bound(float value, float fallback, float minimum, float maximum)
        {
            return Finite(value) ? Math.Max(minimum, Math.Min(maximum, value)) : fallback;
        }

        // Velocity is sampled before the previous state's OnExit can call EndMovement.
        // Doubles avoid overflow when squaring otherwise finite extreme float velocities.
        public static float MomentumMultiplier(float vx, float vy, float bonusPerUnit, float cap)
        {
            if (!Finite(vx) || !Finite(vy)) return 1f;
            double speed = Math.Sqrt((double)vx * vx + (double)vy * vy);
            double bonus = Bound(bonusPerUnit, 0.1f, 0f, 1f);
            double maximum = Bound(cap, 2f, 1f, 4f);
            return (float)Math.Min(maximum, 1d + speed * bonus);
        }

        public static int Step(int comboCounter) { return Math.Max(1, Math.Min(3, comboCounter)); }
    }
}
