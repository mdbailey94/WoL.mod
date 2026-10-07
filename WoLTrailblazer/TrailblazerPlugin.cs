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
        public const string PluginVersion = "0.5.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.trailblazer.cfg) ----
        // [Trail] applies straight away; [Balance] after restarting the game.
        private static ConfigEntry<float> duration, enhancedDuration;
        private static ConfigEntry<bool> searingFire;
        private static ConfigEntry<float> auraSize, auraInterval, patchSpacing, trailLinger, trailHitSize, trailHitInterval, flameAmount, flameSize, speedPerLevel;
        private ConfigEntry<int> damage;
        private ConfigEntry<float> knockback, burnChance, cooldown;

        public static float Duration => Get(duration, 6f, 1f, 30f);
        public static float EnhancedDuration => Get(enhancedDuration, 9f, 1f, 30f);
        public static float AuraSize => Get(auraSize, 2f, 0.5f, 6f);
        public static float AuraInterval => Get(auraInterval, 0.15f, 0.05f, 1f);
        public static float PatchSpacing => Get(patchSpacing, 0.6f, 0.2f, 4f);
        public static float TrailLinger => Get(trailLinger, 1.5f, 0.2f, 6f);
        public static float TrailHitSize => Get(trailHitSize, 1.6f, 0.3f, 5f);
        public static float TrailHitInterval => Get(trailHitInterval, 0.3f, 0.1f, 2f);
        public static bool SearingFire => searingFire?.Value ?? true;
        public static float FlameSize => Get(flameSize, 1f, 0.2f, 4f);
        public static float FlameAmount => Get(flameAmount, 2f, 0.5f, 10f);
        public static float SpeedPerLevel => Get(speedPerLevel, 0.15f, 0.02f, 1f);
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

            // Levels 1-5 by run speed: each one hits a little harder, shoves a little further and
            // burns more surely. Level 1 is a normal-speed wizard, about 3 damage per hit.
            int[] damages = new int[5];
            float[] knockbacks = new float[5];
            float[] burns = new float[5];
            for (int i = 0; i < 5; i++)
            {
                damages[i] = Mathf.Max(0, Mathf.RoundToInt(Mathf.Clamp(damage.Value, 0, 100) * (1f + 0.25f * i)));
                knockbacks[i] = Mathf.Clamp(knockback.Value, 0f, 100f) * (1f + 0.3f * i);
                burns[i] = Mathf.Clamp01(Mathf.Clamp01(burnChance.Value) + 0.125f * i);
            }

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
                    damage = damages,
                    cooldown = new[] { Mathf.Clamp(cooldown.Value, 1f, 60f) },
                    knockbackMultiplier = knockbacks,
                    hitStunDurationModifier = new[] { 0.6f },
                    burnChance = burns,
                    burnLevel = new[] { 1 },
                    // The same enemy is scorched at most about three times a second.
                    sameAttackImmunityTime = new[] { 0.3f }
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
                "Size of all the flames (feet and trail). They start small at normal speed and grow with each damage level.");
            speedPerLevel = Float("Trail", "SpeedPerLevel", 0.15f, 0.02f, 1f,
                "Extra run speed for each stronger hit level (0.15 = every +15% speed, up to level 5).");

            const string restart = " Applies after restarting the game.";
            damage = Config.Bind("Balance", "Damage", 3,
                new ConfigDescription("Damage of each hit at normal speed (+25% for each level above)." + restart,
                    new AcceptableValueRange<int>(0, 100)));
            knockback = Float("Balance", "Knockback", 8f, 0f, 100f, "Knockback at normal speed (+30% for each level above)." + restart);
            burnChance = Float("Balance", "BurnChance", 0.5f, 0f, 1f, "Chance to burn at normal speed, 0 to 1 (+0.125 for each level above)." + restart);
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
