using BepInEx.Configuration;

namespace WoLStoneCharge
{
    // Bound once at startup. Numeric reads allocate nothing; defaults preserve prior behavior.
    internal static class AdvancedTuning
    {
        private static ConfigEntry<float> entrySigDim;
        public static float SigDim => entrySigDim?.Value ?? 0.6f;
        private static ConfigEntry<float> entrySigEaseBack;
        public static float SigEaseBack => entrySigEaseBack?.Value ?? 1f;
        private static ConfigEntry<float> entrySigFlash;
        public static float SigFlash => entrySigFlash?.Value ?? 1.6f;
        private static ConfigEntry<int> entrySigHitStopFrames;
        public static int SigHitStopFrames => entrySigHitStopFrames?.Value ?? 6;
        private static ConfigEntry<float> entrySigShake;
        public static float SigShake => entrySigShake?.Value ?? 2.6f;
        private static ConfigEntry<float> entrySigSlowMoScale;
        public static float SigSlowMoScale => entrySigSlowMoScale?.Value ?? 0.05f;
        private static ConfigEntry<float> entrySigSlowMoTime;
        public static float SigSlowMoTime => entrySigSlowMoTime?.Value ?? 0.5f;
        private static ConfigEntry<float> entrySigZoom;
        public static float SigZoom => entrySigZoom?.Value ?? 0.3f;
        private static ConfigEntry<float> entryShockwaveAfter;
        public static float ShockwaveAfter => entryShockwaveAfter?.Value ?? 0.55f;
        private static ConfigEntry<float> entryShockwaveScale;
        public static float ShockwaveScale => entryShockwaveScale?.Value ?? 1.6f;
        private static ConfigEntry<float> entrySigArcHeight;
        public static float SigArcHeight => entrySigArcHeight?.Value ?? 2f;
        private static ConfigEntry<float> entrySigHangHeight;
        public static float SigHangHeight => entrySigHangHeight?.Value ?? 3.5f;
        private static ConfigEntry<float> entrySignatureFlightTime;
        public static float SignatureFlightTime => entrySignatureFlightTime?.Value ?? 0.6f;
        private static ConfigEntry<float> entrySignatureSlamTime;
        public static float SignatureSlamTime => entrySignatureSlamTime?.Value ?? 0.18f;
        private static ConfigEntry<float> entrySigRiseHeight;
        public static float SigRiseHeight => entrySigRiseHeight?.Value ?? 6f;
        private static ConfigEntry<float> entryArcHeight;
        public static float ArcHeight => entryArcHeight?.Value ?? 1f;
        private static ConfigEntry<float> entryHangHeight;
        public static float HangHeight => entryHangHeight?.Value ?? 1.6f;
        private static ConfigEntry<float> entryRiseHeight;
        public static float RiseHeight => entryRiseHeight?.Value ?? 2.6f;
        private static ConfigEntry<float> entrySlamTime;
        public static float SlamTime => entrySlamTime?.Value ?? 0.12f;
        private static ConfigEntry<float> entryStartHeight;
        public static float StartHeight => entryStartHeight?.Value ?? 0.5f;

        public static void Bind(ConfigFile config)
        {
            entrySigDim = config.Bind("Cinema Advanced", "SigDim", 0.6f,
                new ConfigDescription("Signature stage-light multiplier; 1 keeps normal brightness. Restart the game after editing this file.", new AcceptableValueRange<float>(0.1f, 1f)));
            entrySigEaseBack = config.Bind("Cinema Advanced", "SigEaseBack", 1f,
                new ConfigDescription("Seconds for camera and light to return after the slam. Restart the game after editing this file.", new AcceptableValueRange<float>(0.05f, 3f)));
            entrySigFlash = config.Bind("Cinema Advanced", "SigFlash", 1.6f,
                new ConfigDescription("Signature slam flash brightness multiplier. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 3f)));
            entrySigHitStopFrames = config.Bind("Cinema Advanced", "SigHitStopFrames", 6,
                new ConfigDescription("Signature slam hit-stop frames; 0 disables it. Restart the game after editing this file.", new AcceptableValueRange<int>(0, 15)));
            entrySigShake = config.Bind("Cinema Advanced", "SigShake", 2.6f,
                new ConfigDescription("Signature slam camera-shake strength; 0 disables it. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 5f)));
            entrySigSlowMoScale = config.Bind("Cinema Advanced", "SigSlowMoScale", 0.05f,
                new ConfigDescription("Signature slam time scale; 1 disables slowdown. Restart the game after editing this file.", new AcceptableValueRange<float>(0.01f, 1f)));
            entrySigSlowMoTime = config.Bind("Cinema Advanced", "SigSlowMoTime", 0.5f,
                new ConfigDescription("Real seconds of signature slam slow motion. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 2f)));
            entrySigZoom = config.Bind("Cinema Advanced", "SigZoom", 0.3f,
                new ConfigDescription("Signature camera pullback fraction; 0 disables this zoom. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 0.7f)));
            entryShockwaveAfter = config.Bind("Signature Advanced", "ShockwaveAfter", 0.55f,
                new ConfigDescription("Real seconds between signature slam and aftershock. Restart the game after editing this file.", new AcceptableValueRange<float>(0.05f, 2f)));
            entryShockwaveScale = config.Bind("Signature Advanced", "ShockwaveScale", 1.6f,
                new ConfigDescription("Aftershock radius as a multiplier of signature slam radius. Restart the game after editing this file.", new AcceptableValueRange<float>(0.25f, 3f)));
            entrySigArcHeight = config.Bind("Signature Advanced", "SigArcHeight", 2f,
                new ConfigDescription("Added signature flight-arc height in world units. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 8f)));
            entrySigHangHeight = config.Bind("Signature Advanced", "SigHangHeight", 3.5f,
                new ConfigDescription("Signature shield hover height in world units. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 10f)));
            entrySignatureFlightTime = config.Bind("Signature Advanced", "SignatureFlightTime", 0.6f,
                new ConfigDescription("Seconds the signature shield flies before hovering. Restart the game after editing this file.", new AcceptableValueRange<float>(0.1f, 2f)));
            entrySignatureSlamTime = config.Bind("Signature Advanced", "SignatureSlamTime", 0.18f,
                new ConfigDescription("Seconds for the signature shield to drop to the ground. Restart the game after editing this file.", new AcceptableValueRange<float>(0.04f, 1f)));
            entrySigRiseHeight = config.Bind("Signature Advanced", "SigRiseHeight", 6f,
                new ConfigDescription("Signature shield height at the end of the hover in world units. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 15f)));
            entryArcHeight = config.Bind("Throw Advanced", "ArcHeight", 1f,
                new ConfigDescription("Added standard shield flight-arc height in world units. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 5f)));
            entryHangHeight = config.Bind("Throw Advanced", "HangHeight", 1.6f,
                new ConfigDescription("Standard shield hover height in world units. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 6f)));
            entryRiseHeight = config.Bind("Throw Advanced", "RiseHeight", 2.6f,
                new ConfigDescription("Standard shield height at the end of the hover in world units. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 10f)));
            entrySlamTime = config.Bind("Throw Advanced", "SlamTime", 0.12f,
                new ConfigDescription("Seconds for a standard or enhanced shield to drop to the ground. Restart the game after editing this file.", new AcceptableValueRange<float>(0.04f, 1f)));
            entryStartHeight = config.Bind("Throw Advanced", "StartHeight", 0.5f,
                new ConfigDescription("Shield height above its ground position at launch in world units. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 3f)));
        }
    }
}