using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLTrailblazer
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class TrailblazerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.trailblazer";
        public const string PluginName = "Trailblazer";
        public const string PluginVersion = "0.9.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.trailblazer.cfg) ----
        // [Trail] and [Look] apply straight away; [Balance] after restarting the game.
        private static ConfigEntry<float> duration, enhancedDuration;
        private static ConfigEntry<bool> searingFire, drawn, ringOfFire;
        private static ConfigEntry<float> auraSize, auraInterval, patchSpacing, trailLinger, trailHitSize, trailHitInterval, flameAmount, flameSize, peakSpeedPercent, peakDamageMultiplier;
        private ConfigEntry<int> damage;
        private ConfigEntry<float> knockback, burnChance, cooldown;

        // One enemy is hit by the fire (ring and trail together) at most this often, in seconds.
        private const float HitImmunity = 0.3f;

        public static float Duration => Get(duration, 6f, 1f, 30f);
        public static float EnhancedDuration => Get(enhancedDuration, 9f, 1f, 30f);
        public static float AuraSize => Get(auraSize, 2f, 0.5f, 6f);
        public static float AuraInterval => Get(auraInterval, 0.15f, 0.05f, 1f);
        public static float PatchSpacing => Get(patchSpacing, 0.6f, 0.2f, 4f);
        public static float TrailLinger => Get(trailLinger, 1.5f, 0.2f, 6f);
        public static float TrailHitSize => Get(trailHitSize, 1.6f, 0.3f, 5f);
        public static float TrailHitInterval => Get(trailHitInterval, 0.3f, 0.1f, 2f);
        public static bool SearingFire => searingFire?.Value ?? true;
        public static bool Drawn => drawn?.Value ?? true;
        public static bool RingOfFire => ringOfFire?.Value ?? false;
        public static float FlameSize => Get(flameSize, 1f, 0.2f, 4f);
        public static float FlameAmount => Get(flameAmount, 2f, 0.5f, 10f);
        public static float PeakSpeedPercent => Get(peakSpeedPercent, 230f, 110f, 600f);
        public static float PeakDamageMultiplier => Get(peakDamageMultiplier, 4f, 1f, 10f);
        public const float TeleportDistance = 3f;

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Trailblazer in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            BindSettings();

            // One stat level only: these are the level-1 numbers (running at base speed), and
            // FlameTrail scales each hit by its speed level (1-10) as it's made. The game keeps at
            // most SkillStats.maxSkillLevel (5) stat levels per arcana and quietly cuts longer
            // arrays down, so the ten levels can't live here.
            Skills.Register(new SkillInfo
            {
                ID = TrailblazerState.staticID,
                displayName = "Trailblazer",
                description = "Set yourself ablaze! For a few seconds a ring of fire scorches any enemy you touch, and running, dashing and movement arcana leave a burning trail behind you. The faster you are, the bigger it burns!",
                enhancedDescription = "Lasts longer and burns bigger and hotter!",
                icon = LoadIcon("icon.png"),
                tier = 2,
                stateType = typeof(TrailblazerState),
                skillStats = new SkillStats
                {
                    ID = new[] { TrailblazerState.staticID },
                    elementType = new[] { "Fire" },
                    subElementType = new[] { "Fire" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    damage = new[] { Mathf.Clamp(damage.Value, 0, 100) },
                    cooldown = new[] { Mathf.Clamp(cooldown.Value, 1f, 60f) },
                    knockbackMultiplier = new[] { Mathf.Clamp(knockback.Value, 0f, 100f) },
                    hitStunDurationModifier = new[] { 0.6f },
                    burnChance = new[] { Mathf.Clamp01(burnChance.Value) },
                    burnLevel = new[] { 1 },
                    // The same enemy is scorched at most about three times a second, by the ring
                    // and the trail together. (Same-target: every burst is its own attack object,
                    // so the same-attack immunity never applied between them.)
                    sameTargetImmunityTime = new[] { HitImmunity }
                },
                priceMultiplier = 3,
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private void BindSettings()
        {
            duration = Float("Trail", "Duration", 6f, 1f, 30f, "Seconds your feet stay ablaze after casting it.");
            enhancedDuration = Float("Trail", "EnhancedDuration", 9f, 1f, 30f, "Duration when enhanced.");
            auraSize = Float("Trail", "AuraSize", 2f, 0.5f, 6f, "Size of the ring of fire round the wizard that hurts enemies who touch it.");
            auraInterval = Float("Trail", "AuraInterval", 0.15f, 0.05f, 1f, "Seconds between the ring's hits.");
            patchSpacing = Float("Trail", "PatchSpacing", 0.6f, 0.2f, 4f, "Distance moved between patches of fire on the trail.");
            trailLinger = Float("Trail", "TrailLinger", 1.5f, 0.2f, 6f, "Seconds each patch of the trail keeps burning.");
            trailHitSize = Float("Trail", "TrailHitSize", 1.6f, 0.3f, 5f, "Size of each patch's hit.");
            trailHitInterval = Float("Trail", "TrailHitInterval", 0.3f, 0.1f, 2f, "Seconds between each patch's hits.");
            flameAmount = Float("Trail", "FlameAmount", 2f, 0.5f, 10f, "How many flames flicker on each patch (more the harder it hits).");
            searingFire = Config.Bind("Trail", "SearingRushFire", true,
                "The trail burns with Searing Rush's fire columns. Off: plain flickering flames.");
            flameSize = Float("Trail", "FlameSize", 1f, 0.2f, 4f,
                "Size of all the flames (feet and trail): half size at level 1, full size at level 2, +10% a level from there.");
            peakSpeedPercent = Float("Trail", "PeakSpeedPercent", 230f, 110f, 600f,
                "Speed, as a percentage of base run speed, at which the fire peaks (level 10). Running at base speed (100) " +
                "is level 1 and the levels between are spread evenly: with 230, one level every +14.4% speed. " +
                "230 is about a sprint (+40%) with a speed cloak (+15%), a speed relic (+15%) and a speed-boost arcana (+25%), " +
                "all at once; a plain sprint is level 3.");
            peakDamageMultiplier = Float("Trail", "PeakDamageMultiplier", 4f, 1f, 10f,
                "Damage of each hit at level 10, as a multiple of Damage (the levels between are spread evenly: " +
                "with Damage 3 and 4, the levels hit for 3, 4, 5 ... 12).");

            ringOfFire = Config.Bind("Look", "RingOfFire", false,
                "Draw the ring of fire on the floor round the wizard at the size of its hit circle (the hits happen either way).");
            drawn = Config.Bind("Look", "Drawn", true,
                "The drawn fire on top of the game's flames: flames licking up from the wizard's feet, embers, " +
                "flames and char marks on the trail, the burst as you cast it and the flare-up as it burns out (from the next cast).");

            const string restart = " Applies after restarting the game.";
            damage = Config.Bind("Balance", "Damage", 3,
                new ConfigDescription("Damage of each hit at level 1 (running at base speed); see PeakDamageMultiplier." + restart,
                    new AcceptableValueRange<int>(0, 100)));
            knockback = Float("Balance", "Knockback", 8f, 0f, 100f, "Knockback at level 1 (2.5 times that at level 10)." + restart);
            burnChance = Float("Balance", "BurnChance", 0.5f, 0f, 1f, "Chance to burn at level 1, 0 to 1 (+0.5 by level 10)." + restart);
            cooldown = Float("Balance", "Cooldown", 12f, 1f, 60f, "Cooldown in seconds (counting from the cast)." + restart);
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        private Sprite LoadIcon(string fileName)
        {
            string resourceName = $"{nameof(WoLTrailblazer)}.Assets.{fileName}";
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
