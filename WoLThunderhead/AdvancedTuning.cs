using BepInEx.Configuration;

namespace WoLThunderhead
{
    // Bound once at startup. Numeric reads allocate nothing; defaults preserve prior behavior.
    internal static class AdvancedTuning
    {
        private static ConfigEntry<int> entryAimedPerVolley;
        public static int AimedPerVolley => entryAimedPerVolley?.Value ?? 1;
        private static ConfigEntry<float> entryAimFollow;
        public static float AimFollow => entryAimFollow?.Value ?? 1.2f;
        private static ConfigEntry<float> entryAimReachPastRadius;
        public static float AimReachPastRadius => entryAimReachPastRadius?.Value ?? 0.5f;
        private static ConfigEntry<float> entryRingShake;
        public static float RingShake => entryRingShake?.Value ?? 0.8f;
        private static ConfigEntry<float> entrySignatureSlamShake;
        public static float SignatureSlamShake => entrySignatureSlamShake?.Value ?? 2.2f;
        private static ConfigEntry<int> entrySlamHitStop;
        public static int SlamHitStop => entrySlamHitStop?.Value ?? 3;
        private static ConfigEntry<float> entrySlamShake;
        public static float SlamShake => entrySlamShake?.Value ?? 1.4f;
        private static ConfigEntry<float> entryStrikeShake;
        public static float StrikeShake => entryStrikeShake?.Value ?? 0.15f;
        private static ConfigEntry<float> entryTakeOffShake;
        public static float TakeOffShake => entryTakeOffShake?.Value ?? 0.6f;
        private static ConfigEntry<float> entrySignatureSlamBoltSize;
        public static float SignatureSlamBoltSize => entrySignatureSlamBoltSize?.Value ?? 2.6f;
        private static ConfigEntry<float> entrySlamBoltSize;
        public static float SlamBoltSize => entrySlamBoltSize?.Value ?? 1.6f;
        private static ConfigEntry<float> entryRingBurstSize;
        public static float RingBurstSize => entryRingBurstSize?.Value ?? 1.2f;
        private static ConfigEntry<float> entryRingDelay;
        public static float RingDelay => entryRingDelay?.Value ?? 0.06f;
        private static ConfigEntry<float> entryRingOutside;
        public static float RingOutside => entryRingOutside?.Value ?? 0.6f;
        private static ConfigEntry<float> entrySignatureDiveSpeed;
        public static float SignatureDiveSpeed => entrySignatureDiveSpeed?.Value ?? 0.6f;
        private static ConfigEntry<float> entrySignatureHoldLift;
        public static float SignatureHoldLift => entrySignatureHoldLift?.Value ?? 0.15f;

        public static void Bind(ConfigFile config)
        {
            entryAimedPerVolley = config.Bind("Aim Advanced", "AimedPerVolley", 1,
                new ConfigDescription("Number of bolts per standard or enhanced volley that seek enemies; capped by volley size. Restart the game after editing this file.", new AcceptableValueRange<int>(0, 8)));
            entryAimFollow = config.Bind("Aim Advanced", "AimFollow", 1.2f,
                new ConfigDescription("World units a targeted bolt follows an enemy beyond its drawn target marker. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 5f)));
            entryAimReachPastRadius = config.Bind("Aim Advanced", "AimReachPastRadius", 0.5f,
                new ConfigDescription("Extra world units past the strike circle that can be targeted. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 5f)));
            entryRingShake = config.Bind("Cinema Advanced", "RingShake", 0.8f,
                new ConfigDescription("Camera-shake strength from the lightning ring. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 5f)));
            entrySignatureSlamShake = config.Bind("Cinema Advanced", "SignatureSlamShake", 2.2f,
                new ConfigDescription("Signature landing camera-shake strength. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 5f)));
            entrySlamHitStop = config.Bind("Cinema Advanced", "SlamHitStop", 3,
                new ConfigDescription("Standard landing hit-stop frames; 0 disables it. Restart the game after editing this file.", new AcceptableValueRange<int>(0, 15)));
            entrySlamShake = config.Bind("Cinema Advanced", "SlamShake", 1.4f,
                new ConfigDescription("Standard landing camera-shake strength; 0 disables it. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 5f)));
            entryStrikeShake = config.Bind("Cinema Advanced", "StrikeShake", 0.15f,
                new ConfigDescription("Camera-shake strength for each signature volley. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 2f)));
            entryTakeOffShake = config.Bind("Cinema Advanced", "TakeOffShake", 0.6f,
                new ConfigDescription("Signature take-off camera-shake strength. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 5f)));
            entrySignatureSlamBoltSize = config.Bind("Effects Advanced", "SignatureSlamBoltSize", 2.6f,
                new ConfigDescription("Signature landing lightning-bolt visual size. Restart the game after editing this file.", new AcceptableValueRange<float>(0.25f, 6f)));
            entrySlamBoltSize = config.Bind("Effects Advanced", "SlamBoltSize", 1.6f,
                new ConfigDescription("Standard landing lightning-bolt visual size. Restart the game after editing this file.", new AcceptableValueRange<float>(0.25f, 5f)));
            entryRingBurstSize = config.Bind("Signature Advanced", "RingBurstSize", 1.2f,
                new ConfigDescription("Hit size of each lightning-ring burst in world units. Restart the game after editing this file.", new AcceptableValueRange<float>(0.25f, 4f)));
            entryRingDelay = config.Bind("Signature Advanced", "RingDelay", 0.06f,
                new ConfigDescription("Seconds after impact before the surrounding lightning ring strikes, capped by [Leap] RecoverTime. Restart the game after editing this file.", new AcceptableValueRange<float>(0.01f, 1f)));
            entryRingOutside = config.Bind("Signature Advanced", "RingOutside", 0.6f,
                new ConfigDescription("World units added beyond slam radius for the surrounding lightning ring. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 5f)));
            entrySignatureDiveSpeed = config.Bind("Signature Advanced", "SignatureDiveSpeed", 0.6f,
                new ConfigDescription("Signature descent duration multiplied by the configured CrashTime; smaller is faster. Restart the game after editing this file.", new AcceptableValueRange<float>(0.1f, 2f)));
            entrySignatureHoldLift = config.Bind("Signature Advanced", "SignatureHoldLift", 0.15f,
                new ConfigDescription("Additional signature wind-up height as a fraction of configured Height. Restart the game after editing this file.", new AcceptableValueRange<float>(0f, 1f)));
        }
    }
}