using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using LegendAPI;
using UnityEngine;

namespace WoLThunderhead
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class ThunderheadPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.thunderhead";
        public const string PluginName = "Thunderhead";
        public const string PluginVersion = "0.10.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<bool> signatureWithArcana;
        private ConfigEntry<int> settingsRevision;
        // Bumped when a default changes: an older config file still holding the old default gets
        // the new one (values you changed yourself are kept). 8 = 0.8.0.
        private const int SettingsRevision = 8;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.thunderhead.cfg) ----
        // [Leap], [Lightning] and [Signature] apply the next time you cast it; [Balance] after restarting the game.
        private static ConfigEntry<float> height, windupTime, riseTime, airTime, enhancedAirTime, crashTime, recoverTime;
        private static ConfigEntry<float> radius, enhancedRadius, volleyInterval, enhancedVolleyInterval, auraSize, strikeSize, slamSize, enhancedSlamSize;
        private static ConfigEntry<int> volleySize, enhancedVolleySize;
        private static ConfigEntry<bool> evenSpread, electricLook, stormCloud, drawn;
        private static ConfigEntry<float> hoverSpeedPercent, cloudSize;
        private static ConfigEntry<float> signatureAirTime, signatureStrikeRate, signatureRadius, signatureSlamSize, signatureHold;
        private static ConfigEntry<bool> cinematic;
        private static ConfigEntry<float> cameraZoom, stageDarken, impactFlash, impactSlowMo;
        private static ConfigEntry<int> hitStopFrames, ringBolts;
        private static ConfigEntry<int> strikeDamage, slamDamage, signatureStrikeDamage, signatureSlamDamage;
        private static ConfigEntry<float> strikeKnockback, slamKnockback, strikeShockChance, slamShockChance, cooldown;

        public static float Height => Get(height, 4f, 0.5f, 12f);
        public static float WindupTime => Get(windupTime, 0.12f, 0f, 1f);
        public static float RiseTime => Get(riseTime, 0.25f, 0.05f, 1.5f);
        public static float AirTime => Get(airTime, 1f, 0f, 5f);
        public static float EnhancedAirTime => Get(enhancedAirTime, 1.5f, 0f, 6f);
        public static float CrashTime => Get(crashTime, 0.12f, 0.03f, 1f);
        public static float RecoverTime => Get(recoverTime, 0.15f, 0f, 1f);
        public static float Radius => Get(radius, 2.2f, 0.5f, 8f);
        public static float EnhancedRadius => Get(enhancedRadius, 2.8f, 0.5f, 8f);
        // Standard: volleys of 2 every 0.4 s (6 strikes in the 1 s up). Enhanced: 3 every 0.25 s.
        public static float VolleyInterval => Get(volleyInterval, 0.4f, 0.05f, 2f);
        public static float EnhancedVolleyInterval => Get(enhancedVolleyInterval, 0.25f, 0.05f, 2f);
        public static float HoverSpeedPercent => Get(hoverSpeedPercent, 40f, 0f, 100f);
        public static bool Cloud => stormCloud?.Value ?? true;
        public static float CloudSize => Get(cloudSize, 1f, 0.3f, 3f);
        public static bool ElectricLook => electricLook?.Value ?? true;
        public static bool Drawn => drawn?.Value ?? true;
        public static float AuraSize => Get(auraSize, 1f, 0.2f, 4f);
        public static int VolleySize => Mathf.Clamp(volleySize?.Value ?? 2, 1, 12);
        public static int EnhancedVolleySize => Mathf.Clamp(enhancedVolleySize?.Value ?? 3, 1, 12);
        public static bool EvenSpread => evenSpread?.Value ?? true;
        public static float StrikeSize => Get(strikeSize, 1f, 0.3f, 4f);
        public static float SlamSize => Get(slamSize, 2.4f, 0.5f, 6f);
        public static float EnhancedSlamSize => Get(enhancedSlamSize, 3f, 0.5f, 6f);
        // The signature storm's multipliers, on top of the enhanced values.
        public static float SignatureAirTimeMultiplier => Get(signatureAirTime, 1.6f, 1f, 4f);
        public static float SignatureStrikeRateMultiplier => Get(signatureStrikeRate, 2.5f, 1f, 4f);
        public static float SignatureRadiusMultiplier => Get(signatureRadius, 1.5f, 1f, 3f);
        public static float SignatureSlamSizeMultiplier => Get(signatureSlamSize, 1.5f, 1f, 3f);
        // The signature's cinematic beats.
        public static float SignatureWindupHold => Get(signatureHold, 0.25f, 0f, 1f);
        public static bool Cinematic => cinematic?.Value ?? true;
        public static float CameraZoom => Get(cameraZoom, 0.9f, 0.7f, 1f);
        public static float StageDarken => Get(stageDarken, 0.45f, 0f, 1f);
        public static int HitStopFrames => Mathf.Clamp(hitStopFrames?.Value ?? 4, 0, 10);
        public static float ImpactFlash => Get(impactFlash, 0.85f, 0f, 1f);
        public static float ImpactSlowMo => Get(impactSlowMo, 0.3f, 0f, 1f);
        public static int RingBolts => Mathf.Clamp(ringBolts?.Value ?? 8, 0, 16);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            // Before anything is bound: is there a config file from an earlier version?
            bool hadConfig = File.Exists(Config.ConfigFilePath);
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Thunderhead in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            signatureWithArcana = Config.Bind("General", "SignatureUnlocksWithArcana", true,
                "Unlock Thunderhead's signature version along with the arcana itself, so it can go in the " +
                "signature slot as soon as you have it. Off: its Signature drop has to be found in a run, " +
                "like the game's own arcana.");
            settingsRevision = Config.Bind("General", "SettingsRevision", 0,
                "Which version's defaults this file was last updated to. Leave it alone.");
            BindSettings();
            AdvancedTuning.Bind(Config);
            UpgradeSettings(hadConfig);
            // The signature version only shows up in the spellbook's Signature list once it's
            // unlocked; see SignatureUnlock.cs.
            SignatureUnlock.Install(new Harmony(PluginGuid), ThunderheadState.staticID,
                () => signatureWithArcana.Value, message => Log("Thunderhead: " + message));

            Skills.Register(new SkillInfo
            {
                ID = ThunderheadState.staticID,
                displayName = "Thunderhead",
                description = "Leap high into the air, out of reach, as lightning rains down around you, then crash down in a thunderous slam!",
                // The game shows this one text on the card for both the enhanced and the signature
                // version, so it stays short (longer text runs off the spellbook card).
                enhancedDescription = "Steer through the air as lightning strikes faster! Signature: a raging storm and a devastating dive!",
                icon = LoadIcon("icon.png"),
                tier = 2,
                stateType = typeof(ThunderheadState),
                skillStats = new SkillStats
                {
                    ID = new[] { ThunderheadState.staticID },
                    elementType = new[] { "Lightning" },
                    subElementType = new[] { "Lightning" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Level 1 is the strikes that rain down while you're up, level 2 the slam as you
                    // crash down, level 3 the game's lightning bolts used only for the look
                    // (harmless). All from [Balance] in the config. Each strike's hit circle is about
                    // a tile across its radius, so an enemy standing near the spot below takes about
                    // a third of the strikes:
                    // - standard (hanging still, volleys of 2 every 0.4 s for 1 s: 6 strikes):
                    //   2 strikes and the slam, about 24 (0.7.0: 12 strikes and steering, about 34);
                    // - enhanced (steering, volleys of 3 every 0.25 s for 1.5 s over a wider circle:
                    //   18 strikes): 4 strikes and the slam, about 34, more when you steer onto them.
                    // Levels 4 and 5 are the signature storm's strikes (also its ring of bolts round
                    // the landing) and slam: 2.4 s of volleys every 0.1 s (72 strikes within 4.2
                    // tiles). A strike's circle plus the enemy's own hurtbox reaches 1.2-1.5 tiles,
                    // so an enemy kept under the storm takes about 8-13 strikes and the slam: about
                    // 110 (90-125 by the enemy's size), for the whole gauge. Vanilla lightning
                    // signatures do about 110-130 to one enemy: Shock Nova's 11 pulses of 10,
                    // Thunder Drop's 30 plus 9-10 of its 10-damage javelins. (0.8.0: strikes 8,
                    // about 125.)
                    damage = new[] { Mathf.Clamp(strikeDamage.Value, 0, 200), Mathf.Clamp(slamDamage.Value, 0, 500), 0,
                        Mathf.Clamp(signatureStrikeDamage.Value, 0, 200), Mathf.Clamp(signatureSlamDamage.Value, 0, 500) },
                    cooldown = new[] { Mathf.Clamp(cooldown.Value, 0.5f, 60f) },
                    knockbackMultiplier = new[] { Mathf.Clamp(strikeKnockback.Value, 0f, 200f), Mathf.Clamp(slamKnockback.Value, 0f, 200f), 0f,
                        Mathf.Clamp(strikeKnockback.Value, 0f, 200f), Mathf.Clamp(slamKnockback.Value, 0f, 200f) },
                    hitStunDurationModifier = new[] { 1f, 1.3f, 0f, 1f, 1.5f },
                    showDamageNumber = new[] { true, true, false, true, true },
                    // The signature slam (level 5) always shocks; its strikes shock like the normal ones.
                    shockChance = new[] { Mathf.Clamp01(strikeShockChance.Value), Mathf.Clamp01(slamShockChance.Value), 0f,
                        Mathf.Clamp01(strikeShockChance.Value), 1f },
                    shockLevel = new[] { 1 },
                    sameAttackImmunityTime = new[] { 0.1f }
                },
                priceMultiplier = 4,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private void BindSettings()
        {
            height = Float("Leap", "Height", 4f, 0.5f, 12f, "How high the wizard leaps (in tiles, roughly).");
            windupTime = Float("Leap", "WindupTime", 0.12f, 0f, 1f, "Seconds crouching on the ground before take-off (you can still be hit, or dash to cancel).");
            riseTime = Float("Leap", "RiseTime", 0.25f, 0.05f, 1.5f, "Seconds to rise to the top.");
            airTime = Float("Leap", "AirTime", 1f, 0f, 5f, "Seconds hanging at the top while lightning falls.");
            enhancedAirTime = Float("Leap", "EnhancedAirTime", 1.5f, 0f, 6f, "AirTime when enhanced.");
            crashTime = Float("Leap", "CrashTime", 0.12f, 0.03f, 1f, "Seconds to crash back down.");
            hoverSpeedPercent = Float("Leap", "HoverSpeedPercent", 40f, 0f, 100f,
                "Enhanced and signature only (the standard leap hangs still): how fast you can steer the hover " +
                "with the stick while up in the air, as a percentage of your move speed (speed relics count; " +
                "0 = hang still, 100 = walking pace).");
            recoverTime = Float("Leap", "RecoverTime", 0.15f, 0f, 1f, "Seconds on the ground after the slam before you can act.");

            radius = Float("Lightning", "Radius", 2.2f, 0.5f, 8f, "How far from the spot below the strikes can land.");
            enhancedRadius = Float("Lightning", "EnhancedRadius", 2.8f, 0.5f, 8f, "Radius when enhanced.");
            volleyInterval = Float("Lightning", "VolleyInterval", 0.4f, 0.05f, 2f, "Seconds between volleys of strikes while up.");
            enhancedVolleyInterval = Float("Lightning", "EnhancedVolleyInterval", 0.25f, 0.05f, 2f,
                "VolleyInterval when enhanced (lower = more often).");
            volleySize = Config.Bind("Lightning", "VolleySize", 2,
                new ConfigDescription("Strikes in each volley.", new AcceptableValueRange<int>(1, 12)));
            enhancedVolleySize = Config.Bind("Lightning", "EnhancedVolleySize", 3,
                new ConfigDescription("VolleySize when enhanced (and in the signature storm).", new AcceptableValueRange<int>(1, 12)));
            evenSpread = Config.Bind("Lightning", "EvenSpread", true,
                "Spread the strikes evenly over the whole area through the leap, each volley striking near, " +
                "middle and far all round you. Off: each strike lands at a random spot.");
            strikeSize = Float("Lightning", "StrikeSize", 1f, 0.3f, 4f, "Size of each strike's hit area.");
            slamSize = Float("Lightning", "SlamSize", 2.4f, 0.5f, 6f, "Size of the landing slam's hit area.");
            enhancedSlamSize = Float("Lightning", "EnhancedSlamSize", 3f, 0.5f, 6f, "SlamSize when enhanced.");

            signatureAirTime = Float("Signature", "AirTimeMultiplier", 1.6f, 1f, 4f,
                "The signature cast hangs in the air this many times as long (times EnhancedAirTime).");
            signatureStrikeRate = Float("Signature", "StrikeRateMultiplier", 2.5f, 1f, 4f,
                "Volleys come this many times as often in the signature cast (EnhancedVolleyInterval divided by this).");
            signatureRadius = Float("Signature", "RadiusMultiplier", 1.5f, 1f, 3f,
                "The signature cast's strikes land over a circle this many times as wide (times EnhancedRadius).");
            signatureSlamSize = Float("Signature", "SlamSizeMultiplier", 1.5f, 1f, 3f,
                "The signature slam's hit area, times EnhancedSlamSize.");
            signatureHold = Float("Signature", "WindupHold", 0.25f, 0f, 1f,
                "Seconds the wizard holds at the top, crackling, before the dive.");
            ringBolts = Config.Bind("Signature", "RingBolts", 8,
                new ConfigDescription("Lightning bolts radiating round the signature landing (they strike just outside the slam).",
                    new AcceptableValueRange<int>(0, 16)));
            cinematic = Config.Bind("Signature", "Cinematic", true,
                "The signature's camera, light and time effects: the stage darkening, zoom, flash, hit-stop and slow-motion below.");
            stageDarken = Float("Signature", "StageDarken", 0.45f, 0f, 1f,
                "How much of the stage light is left while the storm rages (1 = no darkening).");
            cameraZoom = Float("Signature", "CameraZoom", 0.9f, 0.7f, 1f,
                "Camera size during the wind-up and dive (0.9 = zoom in 10%; 1 = no zoom).");
            hitStopFrames = Config.Bind("Signature", "HitStopFrames", 4,
                new ConfigDescription("Frames the game freezes at the signature impact (the normal slam: 3).", new AcceptableValueRange<int>(0, 10)));
            impactFlash = Float("Signature", "ImpactFlash", 0.85f, 0f, 1f, "Strength of the white flash at the impact (0 = none).");
            impactSlowMo = Float("Signature", "ImpactSlowMo", 0.3f, 0f, 1f,
                "Seconds of slow-motion easing back to normal speed after the impact's hit-stop (0 = none).");

            electricLook = Config.Bind("Look", "Electric", true,
                "Crackle with electricity while up in the air (the game's electric aura, sparks and a flickering glow).");
            stormCloud = Config.Bind("Look", "Cloud", true, "Ride a dark storm cloud while up in the air.");
            drawn = Config.Bind("Look", "Drawn", true,
                "The standard and enhanced leap's drawn effects: glyphs where the bolts will land, scorch marks, arcs round the wizard, " +
                "lightning inside the cloud, the landing ring, the slam's shockwave and crater (the signature storm has its own look).");
            cloudSize = Float("Look", "CloudSize", 1f, 0.3f, 3f, "Size of the storm cloud.");
            auraSize = Float("Look", "AuraSize", 1f, 0.2f, 4f, "Size of the electric aura round the wizard in the air.");

            const string restart = " Applies after restarting the game.";
            strikeDamage = Config.Bind("Balance", "StrikeDamage", 5,
                new ConfigDescription("Damage of each lightning strike." + restart, new AcceptableValueRange<int>(0, 200)));
            slamDamage = Config.Bind("Balance", "SlamDamage", 14,
                new ConfigDescription("Damage of the landing slam." + restart, new AcceptableValueRange<int>(0, 500)));
            strikeKnockback = Float("Balance", "StrikeKnockback", 6f, 0f, 200f, "Knockback of each strike." + restart);
            slamKnockback = Float("Balance", "SlamKnockback", 45f, 0f, 200f, "Knockback of the slam." + restart);
            strikeShockChance = Float("Balance", "StrikeShockChance", 0.2f, 0f, 1f, "Chance a strike shocks (0 to 1)." + restart);
            slamShockChance = Float("Balance", "SlamShockChance", 0.5f, 0f, 1f, "Chance the slam shocks (0 to 1)." + restart);
            signatureStrikeDamage = Config.Bind("Balance", "SignatureStrikeDamage", 7,
                new ConfigDescription("Damage of each lightning strike in the signature cast." + restart, new AcceptableValueRange<int>(0, 200)));
            signatureSlamDamage = Config.Bind("Balance", "SignatureSlamDamage", 35,
                new ConfigDescription("Damage of the signature cast's landing slam (it always shocks)." + restart, new AcceptableValueRange<int>(0, 500)));
            // 7 s (6 before 0.6.0, when steering came in). Since 0.8.0 only the enhanced leap steers
            // and the standard one deals less, but it's still 1.4 s out of reach, so it stays 7.
            cooldown = Float("Balance", "Cooldown", 7f, 0.5f, 60f, "Cooldown in seconds." + restart);
        }

        // A config file from before 0.8.0 still holds the old defaults for what 0.8.0 rebalanced;
        // those (and only those, a value you set yourself is kept) move to the new defaults.
        private void UpgradeSettings(bool hadConfig)
        {
            if (!hadConfig || settingsRevision.Value >= SettingsRevision)
            {
                settingsRevision.Value = SettingsRevision;
                return;
            }
            var changed = new System.Collections.Generic.List<string>();
            if (settingsRevision.Value < 8)
            {
                Upgrade(volleyInterval, 0.25f, changed);
                Upgrade(volleySize, 3, changed);
                Upgrade(signatureAirTime, 2f, changed);
                Upgrade(signatureStrikeRate, 2f, changed);
            }
            settingsRevision.Value = SettingsRevision;
            Logger.LogInfo(changed.Count > 0
                ? "Thunderhead: settings moved to the 0.8.0 defaults: " + string.Join(", ", changed.ToArray())
                : "Thunderhead: settings kept as you set them (none were on the old defaults)");
        }

        private static void Upgrade(ConfigEntry<float> entry, float oldDefault, System.Collections.Generic.List<string> changed)
        {
            float now = (float)entry.DefaultValue;
            if (Mathf.Abs(entry.Value - oldDefault) > 0.0001f || Mathf.Abs(now - oldDefault) < 0.0001f)
                return;
            changed.Add($"{entry.Definition.Key} {entry.Value:0.###} -> {now:0.###}");
            entry.Value = now;
        }

        private static void Upgrade(ConfigEntry<int> entry, int oldDefault, System.Collections.Generic.List<string> changed)
        {
            int now = (int)entry.DefaultValue;
            if (entry.Value != oldDefault || now == oldDefault)
                return;
            changed.Add($"{entry.Definition.Key} {entry.Value} -> {now}");
            entry.Value = now;
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        private Sprite LoadIcon(string fileName)
        {
            string resourceName = $"{nameof(WoLThunderhead)}.Assets.{fileName}";
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
