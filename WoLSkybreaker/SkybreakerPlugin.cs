using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using LegendAPI;
using UnityEngine;

namespace WoLSkybreaker
{
    // Skybreaker (#52, Matt's request): a new Lightning standard arcana. Point and cast: after a
    // brief delay one large bolt strikes the spot ahead, high damage in a 1-unit core and a little
    // in a 2-unit ring round it. The signature is a colossal, cinematic version.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class SkybreakerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.skybreaker";
        public const string PluginName = "Skybreaker";
        public const string PluginVersion = "0.2.1";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<bool> signatureWithArcana;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.skybreaker.cfg) ----
        // [Bolt], [Enhanced] and [Signature] apply the next time you cast it; [Balance] after restarting the game.
        private static ConfigEntry<float> range, delay, coreSize, ringSize, boltSize;
        private static ConfigEntry<float> enhancedDelay, enhancedCoreSize, enhancedRingSize;
        private static ConfigEntry<float> sigGather, sigCoreSize, sigRingSize, sigBoltSize, sigAftershockDelay, sigAftershockSize;
        private static ConfigEntry<int> sigAftershocks;
        private static ConfigEntry<bool> cinematic;
        private static ConfigEntry<float> stageDarken, cameraZoom, impactFlash, impactSlowMo;
        private static ConfigEntry<int> hitStopFrames;
        private static ConfigEntry<int> coreDamage, ringDamage, enhancedCoreDamage, sigCoreDamage, sigRingDamage, aftershockDamage;
        private static ConfigEntry<float> coreKnockback, ringKnockback, ringShockChance, cooldown;

        public static float Range => Get(range, 4f, 1f, 10f);
        public static float Delay => Get(delay, 0.45f, 0.05f, 2f);
        // Diameters in units (a burst's hit radius is half of it).
        public static float CoreSize => Get(coreSize, 1f, 0.3f, 5f);
        public static float RingSize => Get(ringSize, 2f, 0.5f, 8f);
        public static float BoltSize => Get(boltSize, 2.2f, 0.5f, 4f);
        public static float EnhancedDelay => Get(enhancedDelay, 0.3f, 0.05f, 2f);
        public static float EnhancedCoreSize => Get(enhancedCoreSize, 1.3f, 0.3f, 5f);
        public static float EnhancedRingSize => Get(enhancedRingSize, 2.6f, 0.5f, 8f);
        public static float SignatureGather => Get(sigGather, 0.8f, 0.1f, 3f);
        public static float SignatureCoreSize => Get(sigCoreSize, 2f, 0.3f, 6f);
        public static float SignatureRingSize => Get(sigRingSize, 4f, 0.5f, 10f);
        public static float SignatureBoltSize => Get(sigBoltSize, 4.5f, 0.5f, 8f);
        public static int SignatureAftershocks => Mathf.Clamp(sigAftershocks?.Value ?? 0, 0, 16);
        public static float SignatureAftershockDelay => Get(sigAftershockDelay, 0.15f, 0f, 1f);
        public static float SignatureAftershockSize => Get(sigAftershockSize, 1f, 0.3f, 4f);
        public static bool Cinematic => cinematic?.Value ?? true;
        public static float StageDarken => Get(stageDarken, 0.5f, 0f, 1f);
        public static float CameraZoom => Get(cameraZoom, 0.9f, 0.7f, 1f);
        public static int HitStopFrames => Mathf.Clamp(hitStopFrames?.Value ?? 4, 0, 10);
        public static float ImpactFlash => Get(impactFlash, 0.85f, 0f, 1f);
        public static float ImpactSlowMo => Get(impactSlowMo, 0.3f, 0f, 1f);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            bool hadConfig = File.Exists(Config.ConfigFilePath);
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Skybreaker in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            signatureWithArcana = Config.Bind("General", "SignatureUnlocksWithArcana", true,
                "Unlock Skybreaker's signature version along with the arcana itself (not in Chaos Depths' fresh saves, " +
                "where the signature is its own shop card). Off: its Signature drop has to be found in a run.");
            BindSettings();
            // 0.2.0 (#64): one big bolt, and one huge signature bolt with no aftershocks. A config
            // from 0.1.0 still holds the old sizes and 6 aftershocks; Matt asked for these outright,
            // so they move to the new values.
            var revision = Config.Bind("General", "SettingsRevision", 0, "Which version's defaults this file was last updated to. Leave it alone.");
            if (hadConfig && revision.Value < 2)
            {
                boltSize.Value = (float)boltSize.DefaultValue;
                sigBoltSize.Value = (float)sigBoltSize.DefaultValue;
                sigAftershocks.Value = (int)sigAftershocks.DefaultValue;
                Logger.LogInfo("Skybreaker: settings moved to 0.2.0's single bolt (BoltSize 2.2, signature BoltSize 4.5, no aftershocks)");
            }
            revision.Value = 2;
            SignatureUnlock.Install(new Harmony(PluginGuid), SkybreakerState.staticID,
                () => signatureWithArcana.Value, message => Log("Skybreaker: " + message));

            Skills.Register(new SkillInfo
            {
                ID = SkybreakerState.staticID,
                displayName = "Skybreaker",
                description = "Call down a single mighty bolt of lightning on the spot ahead: devastating at its heart, crackling around it!",
                // One text on the card for both the enhanced and the signature version.
                enhancedDescription = "Strikes sooner and wider! Signature: the sky splits open!",
                icon = LoadIcon("icon.png"),
                tier = 3,
                stateType = typeof(SkybreakerState),
                skillStats = new SkillStats
                {
                    ID = new[] { SkybreakerState.staticID },
                    elementType = new[] { "Lightning" },
                    subElementType = new[] { "Lightning" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Levels (SkyStrike): 1 core, 2 ring (standard and enhanced), 3 enhanced core,
                    // 4 signature core, 5 signature ring, 6 signature aftershocks. Core and ring are
                    // separate bursts, so an enemy in the core takes both: 28 + 6 = 34 standard,
                    // 32 + 6 enhanced, 70 + 20 signature (plus an aftershock or two, 8 each).
                    damage = new[]
                    {
                        Mathf.Clamp(coreDamage.Value, 0, 300), Mathf.Clamp(ringDamage.Value, 0, 200),
                        Mathf.Clamp(enhancedCoreDamage.Value, 0, 300), Mathf.Clamp(sigCoreDamage.Value, 0, 500),
                        Mathf.Clamp(sigRingDamage.Value, 0, 300), Mathf.Clamp(aftershockDamage.Value, 0, 200)
                    },
                    cooldown = new[] { Mathf.Clamp(cooldown.Value, 0.5f, 60f) },
                    knockbackMultiplier = new[]
                    {
                        Mathf.Clamp(coreKnockback.Value, 0f, 200f), Mathf.Clamp(ringKnockback.Value, 0f, 200f),
                        Mathf.Clamp(coreKnockback.Value, 0f, 200f), Mathf.Clamp(coreKnockback.Value * 1.5f, 0f, 300f),
                        Mathf.Clamp(ringKnockback.Value * 1.5f, 0f, 300f), Mathf.Clamp(ringKnockback.Value, 0f, 200f)
                    },
                    hitStunDurationModifier = new[] { 1.3f, 1f, 1.3f, 1.6f, 1.2f, 1f },
                    showDamageNumber = new[] { true, true, true, true, true, true },
                    // The cores always shock; the rings and aftershocks sometimes.
                    shockChance = new[]
                    {
                        1f, Mathf.Clamp01(ringShockChance.Value), 1f, 1f, Mathf.Clamp01(ringShockChance.Value), Mathf.Clamp01(ringShockChance.Value)
                    },
                    shockLevel = new[] { 1 },
                    shakeCamera = new[] { true, false, true, true, false, false },
                    sameAttackImmunityTime = new[] { 0.1f }
                },
                priceMultiplier = 5,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private void BindSettings()
        {
            range = Float("Bolt", "Range", 4f, 1f, 10f, "How far ahead along your aim the bolt strikes.");
            delay = Float("Bolt", "Delay", 0.45f, 0.05f, 2f, "Seconds between the cast marking the spot and the bolt striking it.");
            coreSize = Float("Bolt", "CoreSize", 1f, 0.3f, 5f, "Diameter in units of the bolt's core (the high damage).");
            ringSize = Float("Bolt", "RingSize", 2f, 0.5f, 8f, "Diameter in units of the ring round it (the small damage).");
            boltSize = Float("Bolt", "BoltSize", 2.2f, 0.5f, 4f, "Size of the drawn bolt (the game's lightning strike).");
            enhancedDelay = Float("Enhanced", "Delay", 0.3f, 0.05f, 2f, "Delay when enhanced.");
            enhancedCoreSize = Float("Enhanced", "CoreSize", 1.3f, 0.3f, 5f, "Core diameter when enhanced.");
            enhancedRingSize = Float("Enhanced", "RingSize", 2.6f, 0.5f, 8f, "Ring diameter when enhanced.");

            sigGather = Float("Signature", "GatherTime", 0.8f, 0.1f, 3f, "Seconds the storm gathers over the spot before the bolt.");
            sigCoreSize = Float("Signature", "CoreSize", 2f, 0.3f, 6f, "Core diameter of the signature bolt.");
            sigRingSize = Float("Signature", "RingSize", 4f, 0.5f, 10f, "Ring diameter of the signature bolt.");
            sigBoltSize = Float("Signature", "BoltSize", 4.5f, 0.5f, 8f, "Size of the drawn signature bolt.");
            sigAftershocks = Config.Bind("Signature", "Aftershocks", 0,
                new ConfigDescription("Smaller bolts striking round the ring's edge just after the signature bolt (0 = one single bolt, as Matt asked in #64).",
                    new AcceptableValueRange<int>(0, 16)));
            sigAftershockDelay = Float("Signature", "AftershockDelay", 0.15f, 0f, 1f, "Seconds after the signature bolt before the aftershocks.");
            sigAftershockSize = Float("Signature", "AftershockSize", 1f, 0.3f, 4f, "Hit diameter of each aftershock.");
            cinematic = Config.Bind("Signature", "Cinematic", true,
                "The signature's camera, light and time effects: the stage darkening, zoom, flash, hit-stop and slow-motion below.");
            stageDarken = Float("Signature", "StageDarken", 0.5f, 0f, 1f, "How much of the stage light is left while the storm gathers (1 = no darkening).");
            cameraZoom = Float("Signature", "CameraZoom", 0.9f, 0.7f, 1f, "Camera size while the storm gathers (0.9 = zoom in 10%; 1 = no zoom).");
            hitStopFrames = Config.Bind("Signature", "HitStopFrames", 4,
                new ConfigDescription("Frames the game freezes when the signature bolt lands.", new AcceptableValueRange<int>(0, 10)));
            impactFlash = Float("Signature", "ImpactFlash", 0.85f, 0f, 1f, "Strength of the white flash when it lands (0 = none).");
            impactSlowMo = Float("Signature", "ImpactSlowMo", 0.3f, 0f, 1f, "Seconds of slow-motion easing back after the hit-stop (0 = none).");

            const string restart = " Applies after restarting the game.";
            coreDamage = Int("Balance", "CoreDamage", 28, 300, "Damage in the bolt's core." + restart);
            ringDamage = Int("Balance", "RingDamage", 6, 200, "Damage in the ring round it (an enemy in the core takes this too)." + restart);
            enhancedCoreDamage = Int("Balance", "EnhancedCoreDamage", 32, 300, "Core damage when enhanced." + restart);
            sigCoreDamage = Int("Balance", "SignatureCoreDamage", 70, 500, "Core damage of the signature bolt." + restart);
            sigRingDamage = Int("Balance", "SignatureRingDamage", 20, 300, "Ring damage of the signature bolt." + restart);
            aftershockDamage = Int("Balance", "AftershockDamage", 8, 200, "Damage of each signature aftershock." + restart);
            coreKnockback = Float("Balance", "CoreKnockback", 20f, 0f, 200f, "Knockback in the core (signature 1.5x)." + restart);
            ringKnockback = Float("Balance", "RingKnockback", 10f, 0f, 200f, "Knockback in the ring (signature 1.5x)." + restart);
            ringShockChance = Float("Balance", "RingShockChance", 0.3f, 0f, 1f, "Chance the ring and the aftershocks shock (the core always does)." + restart);
            cooldown = Float("Balance", "Cooldown", 4.5f, 0.5f, 60f, "Cooldown in seconds." + restart);
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        private ConfigEntry<int> Int(string section, string key, int value, int max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(0, max)));

        private Sprite LoadIcon(string fileName)
        {
            string resourceName = $"{nameof(WoLSkybreaker)}.Assets.{fileName}";
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Logger.LogWarning($"Icon resource {resourceName} not found, using the default icon");
                    return null;
                }
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                var texture = new Texture2D(2, 2) { filterMode = FilterMode.Point };
                texture.LoadImage(data);
                return GameIcon.Create(texture, Logger);
            }
        }
    }
}
