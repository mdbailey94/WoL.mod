using BepInEx.Configuration;

namespace WoLTrailblazer
{
    // Bound once at startup. Numeric reads allocate nothing; defaults preserve prior behavior.
    internal static class AdvancedTuning
    {
        private static ConfigEntry<float> entryCometDim;
        public static float CometDim => entryCometDim?.Value ?? 0.5f;
        private static ConfigEntry<float> entryCometSettle;
        public static float CometSettle => entryCometSettle?.Value ?? 0.6f;
        private static ConfigEntry<float> entryCometZoom;
        public static float CometZoom => entryCometZoom?.Value ?? 0.15f;
        private static ConfigEntry<float> entryEaseBack;
        public static float EaseBack => entryEaseBack?.Value ?? 1f;
        private static ConfigEntry<float> entryFinalFlash;
        public static float FinalFlash => entryFinalFlash?.Value ?? 1.9f;
        private static ConfigEntry<int> entryFinalHitStopFrames;
        public static int FinalHitStopFrames => entryFinalHitStopFrames?.Value ?? 5;
        private static ConfigEntry<float> entryFinalShake;
        public static float FinalShake => entryFinalShake?.Value ?? 2.2f;
        private static ConfigEntry<float> entryFinalSlowMoScale;
        public static float FinalSlowMoScale => entryFinalSlowMoScale?.Value ?? 0.05f;
        private static ConfigEntry<float> entryFinalSlowMoTime;
        public static float FinalSlowMoTime => entryFinalSlowMoTime?.Value ?? 0.35f;
        private static ConfigEntry<float> entryPillarLife;
        public static float PillarLife => entryPillarLife?.Value ?? 0.6f;
        private static ConfigEntry<float> entryPillarScale;
        public static float PillarScale => entryPillarScale?.Value ?? 2.2f;
        private static ConfigEntry<float> entryPeakBurnBonus;
        public static float PeakBurnBonus => entryPeakBurnBonus?.Value ?? 0.5f;
        private static ConfigEntry<float> entryPeakKnockbackMultiplier;
        public static float PeakKnockbackMultiplier => entryPeakKnockbackMultiplier?.Value ?? 2.5f;
        private static ConfigEntry<float> entryCometFire;
        public static float CometFire => entryCometFire?.Value ?? 1.6f;
        private static ConfigEntry<float> entryCometFuse;
        public static float CometFuse => entryCometFuse?.Value ?? 0.8f;
        private static ConfigEntry<float> entryCometHitWide;
        public static float CometHitWide => entryCometHitWide?.Value ?? 2f;
        private static ConfigEntry<int> entryCometLevels;
        public static int CometLevels => entryCometLevels?.Value ?? 2;
        private static ConfigEntry<float> entryCometRing;
        public static float CometRing => entryCometRing?.Value ?? 1.5f;
        private static ConfigEntry<float> entryCometSpeed;
        public static float CometSpeed => entryCometSpeed?.Value ?? 0.25f;
        private static ConfigEntry<float> entryFinalDelay;
        public static float FinalDelay => entryFinalDelay?.Value ?? 0.25f;
        private static ConfigEntry<float> entryFinalRadius;
        public static float FinalRadius => entryFinalRadius?.Value ?? 3f;
        private static ConfigEntry<float> entryPillarStep;
        public static float PillarStep => entryPillarStep?.Value ?? 0.04f;

        public static void Bind(ConfigFile config)
        {
            entryCometDim = config.Bind("Cinema Advanced", "CometDim", 0.5f,
                new ConfigDescription("Signature stage-light multiplier; 1 keeps normal brightness. Restart the game after editing this file.", new AcceptableValueRange<float>(0.1f, 1f)));
            entryCometSettle = config.Bind("Cinema Advanced", "CometSettle", 0.6f,
                new ConfigDescription("Seconds for signature camera and light to settle. Restart the game after editing this file.", new AcceptableValueRange<float>(0.05f, 3f)));
            entryCometZoom = config.Bind("Cinema Advanced", "CometZoom", 0.15f,
                new ConfigDescription("Camera pullback fraction; 0 disables this zoom. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 0.7f)));
            entryEaseBack = config.Bind("Cinema Advanced", "EaseBack", 1f,
                new ConfigDescription("Seconds to restore camera and lighting. Restart the game after editing this file.", new AcceptableValueRange<float>(0.05f, 3f)));
            entryFinalFlash = config.Bind("Cinema Advanced", "FinalFlash", 1.9f,
                new ConfigDescription("Final blast flash brightness multiplier. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 3f)));
            entryFinalHitStopFrames = config.Bind("Cinema Advanced", "FinalHitStopFrames", 5,
                new ConfigDescription("Final blast hit-stop frames; 0 disables it. Restart the game after editing this file.", new AcceptableValueRange<int>(0, 15)));
            entryFinalShake = config.Bind("Cinema Advanced", "FinalShake", 2.2f,
                new ConfigDescription("Final blast camera-shake strength; 0 disables it. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 5f)));
            entryFinalSlowMoScale = config.Bind("Cinema Advanced", "FinalSlowMoScale", 0.05f,
                new ConfigDescription("Final blast time scale; 1 disables slowdown. Restart the game after editing this file.", new AcceptableValueRange<float>(0.01f, 1f)));
            entryFinalSlowMoTime = config.Bind("Cinema Advanced", "FinalSlowMoTime", 0.35f,
                new ConfigDescription("Real seconds of slow motion after the final blast. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 2f)));
            entryPillarLife = config.Bind("Effects Advanced", "PillarLife", 0.6f,
                new ConfigDescription("Seconds each eruption fire-column visual remains. Restart the game after editing this file.", new AcceptableValueRange<float>(0.1f, 3f)));
            entryPillarScale = config.Bind("Effects Advanced", "PillarScale", 2.2f,
                new ConfigDescription("Eruption pillar visual-size multiplier. Restart the game after editing this file.", new AcceptableValueRange<float>(0.25f, 5f)));
            entryPeakBurnBonus = config.Bind("Scaling Advanced", "PeakBurnBonus", 0.5f,
                new ConfigDescription("Additional burn chance at speed level 10; 0.5 adds 50 percentage points, capped at 100 percent. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 1f)));
            entryPeakKnockbackMultiplier = config.Bind("Scaling Advanced", "PeakKnockbackMultiplier", 2.5f,
                new ConfigDescription("Knockback multiplier at speed level 10; interpolated from 1 at level 1. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 5f)));
            entryCometFire = config.Bind("Signature Advanced", "CometFire", 1.6f,
                new ConfigDescription("Signature flame visual-size multiplier. Restart the game after editing this file.", new AcceptableValueRange<float>(0.25f, 4f)));
            entryCometFuse = config.Bind("Signature Advanced", "CometFuse", 0.8f,
                new ConfigDescription("Game seconds from dropping a signature patch to its eruption. Restart the game after editing this file.", new AcceptableValueRange<float>(0.05f, 5f)));
            entryCometHitWide = config.Bind("Signature Advanced", "CometHitWide", 2f,
                new ConfigDescription("Signature trail hit-width multiplier. Restart the game after editing this file.", new AcceptableValueRange<float>(0.25f, 4f)));
            entryCometLevels = config.Bind("Signature Advanced", "CometLevels", 2,
                new ConfigDescription("Extra fire speed-levels added on top of enhancement; final level is capped at 10. Restart the game after editing this file.", new AcceptableValueRange<int>(0, 8)));
            entryCometRing = config.Bind("Signature Advanced", "CometRing", 1.5f,
                new ConfigDescription("Signature aura hit-size multiplier. Restart the game after editing this file.", new AcceptableValueRange<float>(0.25f, 4f)));
            entryCometSpeed = config.Bind("Signature Advanced", "CometSpeed", 0.25f,
                new ConfigDescription("Added movement speed fraction during the signature; 0.25 means +25 percent. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 1.5f)));
            entryFinalDelay = config.Bind("Signature Advanced", "FinalDelay", 0.25f,
                new ConfigDescription("Seconds after the last eruption before the final blast. Restart the game after editing this file.", new AcceptableValueRange<float>(0.01f, 2f)));
            entryFinalRadius = config.Bind("Signature Advanced", "FinalRadius", 3f,
                new ConfigDescription("Final blast hit radius in world units. Restart the game after editing this file.", new AcceptableValueRange<float>(0.5f, 10f)));
            entryPillarStep = config.Bind("Signature Advanced", "PillarStep", 0.04f,
                new ConfigDescription("Seconds between remaining patch eruptions when the signature ends. Restart the game after editing this file.", new AcceptableValueRange<float>(0.01f, 0.15f)));
        }
    }
}