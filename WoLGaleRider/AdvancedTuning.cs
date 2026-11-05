using BepInEx.Configuration;

namespace WoLGaleRider
{
    // Bound once at startup. Numeric reads allocate nothing; defaults preserve prior behavior.
    internal static class AdvancedTuning
    {
        private static ConfigEntry<float> entrySigDim;
        public static float SigDim => entrySigDim?.Value ?? 0.65f;
        private static ConfigEntry<float> entrySigSettleTime;
        public static float SigSettleTime => entrySigSettleTime?.Value ?? 0.6f;
        private static ConfigEntry<float> entrySigZoom;
        public static float SigZoom => entrySigZoom?.Value ?? 0.2f;
        private static ConfigEntry<float> entrySlamEaseBack;
        public static float SlamEaseBack => entrySlamEaseBack?.Value ?? 0.8f;
        private static ConfigEntry<float> entrySlamFlash;
        public static float SlamFlash => entrySlamFlash?.Value ?? 1.8f;
        private static ConfigEntry<int> entrySlamHitStopFrames;
        public static int SlamHitStopFrames => entrySlamHitStopFrames?.Value ?? 5;
        private static ConfigEntry<float> entrySlamShake;
        public static float SlamShake => entrySlamShake?.Value ?? 2.2f;
        private static ConfigEntry<float> entrySlamSlowMoScale;
        public static float SlamSlowMoScale => entrySlamSlowMoScale?.Value ?? 0.05f;
        private static ConfigEntry<float> entrySlamSlowMoTime;
        public static float SlamSlowMoTime => entrySlamSlowMoTime?.Value ?? 0.3f;
        private static ConfigEntry<float> entryTopSlowMoScale;
        public static float TopSlowMoScale => entryTopSlowMoScale?.Value ?? 0.15f;
        private static ConfigEntry<float> entryTopSlowMoTime;
        public static float TopSlowMoTime => entryTopSlowMoTime?.Value ?? 0.35f;
        private static ConfigEntry<float> entryCarryMargin;
        public static float CarryMargin => entryCarryMargin?.Value ?? 1.2f;
        private static ConfigEntry<float> entryLeapCarry;
        public static float LeapCarry => entryLeapCarry?.Value ?? 0.35f;
        private static ConfigEntry<float> entryLeapDownTime;
        public static float LeapDownTime => entryLeapDownTime?.Value ?? 0.2f;
        private static ConfigEntry<float> entryLeapUpTime;
        public static float LeapUpTime => entryLeapUpTime?.Value ?? 0.35f;
        private static ConfigEntry<float> entrySigKnockbackScale;
        public static float SigKnockbackScale => entrySigKnockbackScale?.Value ?? 1.5f;
        private static ConfigEntry<float> entrySlamScale;
        public static float SlamScale => entrySlamScale?.Value ?? 4.5f;

        public static void Bind(ConfigFile config)
        {
            entrySigDim = config.Bind("Cinema Advanced", "SigDim", 0.65f,
                new ConfigDescription("Stage light multiplier during the signature; 1 leaves brightness unchanged. Restart the game after editing this file.", new AcceptableValueRange<float>(0.1f, 1f)));
            entrySigSettleTime = config.Bind("Cinema Advanced", "SigSettleTime", 0.6f,
                new ConfigDescription("Seconds for the signature camera and light to settle. Restart the game after editing this file.", new AcceptableValueRange<float>(0.05f, 3f)));
            entrySigZoom = config.Bind("Cinema Advanced", "SigZoom", 0.2f,
                new ConfigDescription("Camera pullback fraction; 0 disables this zoom. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 0.7f)));
            entrySlamEaseBack = config.Bind("Cinema Advanced", "SlamEaseBack", 0.8f,
                new ConfigDescription("Seconds for camera and light to return after landing. Restart the game after editing this file.", new AcceptableValueRange<float>(0.05f, 3f)));
            entrySlamFlash = config.Bind("Cinema Advanced", "SlamFlash", 1.8f,
                new ConfigDescription("Landing flash brightness multiplier. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 3f)));
            entrySlamHitStopFrames = config.Bind("Cinema Advanced", "SlamHitStopFrames", 5,
                new ConfigDescription("Frames of hit-stop at landing; 0 disables it. Restart the game after editing this file.", new AcceptableValueRange<int>(0, 15)));
            entrySlamShake = config.Bind("Cinema Advanced", "SlamShake", 2.2f,
                new ConfigDescription("Landing camera-shake strength; 0 disables it. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 5f)));
            entrySlamSlowMoScale = config.Bind("Cinema Advanced", "SlamSlowMoScale", 0.05f,
                new ConfigDescription("Landing time scale; 1 disables slowdown. Restart the game after editing this file.", new AcceptableValueRange<float>(0.01f, 1f)));
            entrySlamSlowMoTime = config.Bind("Cinema Advanced", "SlamSlowMoTime", 0.3f,
                new ConfigDescription("Real seconds of landing slow motion. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 2f)));
            entryTopSlowMoScale = config.Bind("Cinema Advanced", "TopSlowMoScale", 0.15f,
                new ConfigDescription("Time scale at the leap peak; 1 disables slowdown. Restart the game after editing this file.", new AcceptableValueRange<float>(0.01f, 1f)));
            entryTopSlowMoTime = config.Bind("Cinema Advanced", "TopSlowMoTime", 0.35f,
                new ConfigDescription("Real seconds of slow motion at the top of the leap. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 2f)));
            entryCarryMargin = config.Bind("Signature Advanced", "CarryMargin", 1.2f,
                new ConfigDescription("Extra world units beyond the ball radius before a carried enemy is released. Restart the game after editing this file.", new AcceptableValueRange<float>(0.1f, 4f)));
            entryLeapCarry = config.Bind("Signature Advanced", "LeapCarry", 0.35f,
                new ConfigDescription("Fraction of riding speed retained horizontally during the leap. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 1.5f)));
            entryLeapDownTime = config.Bind("Signature Advanced", "LeapDownTime", 0.2f,
                new ConfigDescription("Seconds to descend during the final leap. Restart the game after editing this file.", new AcceptableValueRange<float>(0.05f, 2f)));
            entryLeapUpTime = config.Bind("Signature Advanced", "LeapUpTime", 0.35f,
                new ConfigDescription("Seconds to rise during the final leap. Restart the game after editing this file.", new AcceptableValueRange<float>(0.05f, 2f)));
            entrySigKnockbackScale = config.Bind("Signature Advanced", "SigKnockbackScale", 1.5f,
                new ConfigDescription("Signature rolling-hit knockback multiplier. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 4f)));
            entrySlamScale = config.Bind("Signature Advanced", "SlamScale", 4.5f,
                new ConfigDescription("Landing wind-blast size in world units. Restart the game after editing this file.", new AcceptableValueRange<float>(0.5f, 12f)));
        }
    }
}