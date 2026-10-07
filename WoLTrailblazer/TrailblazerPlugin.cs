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
        public const string PluginVersion = "0.2.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.trailblazer.cfg) ----
        // [Trail] applies straight away; [Balance] after restarting the game.
        private static ConfigEntry<float> duration, enhancedDuration;
        private static ConfigEntry<float> minSpeed, flameInterval, flameAmount, trailLinger, hitInterval, hitSize, speedPerLevel;
        private ConfigEntry<int> damage;
        private ConfigEntry<float> knockback, burnChance, cooldown;

        public static float Duration => Get(duration, 6f, 1f, 30f);
        public static float EnhancedDuration => Get(enhancedDuration, 9f, 1f, 30f);
        public static float MinSpeed => Get(minSpeed, 1.5f, 0.1f, 10f);
        public static float FlameInterval => Get(flameInterval, 0.04f, 0.01f, 0.5f);
        public static float FlameAmount => Get(flameAmount, 2f, 0.5f, 10f);
        public static float TrailLinger => Get(trailLinger, 0.5f, 0.1f, 3f);
        public static float HitInterval => Get(hitInterval, 0.1f, 0.03f, 1f);
        public static float HitSize => Get(hitSize, 0.8f, 0.2f, 4f);
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
                description = "Set your feet ablaze! For a few seconds, running, dashing and movement arcana leave a trail of fire, scorching and shoving aside any enemy you run into. The faster you are, the bigger it burns!",
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
            minSpeed = Float("Trail", "MinSpeed", 1.5f, 0.1f, 10f, "How fast the wizard must be moving (units a second) to leave fire.");
            flameInterval = Float("Trail", "FlameInterval", 0.04f, 0.01f, 0.5f, "Seconds between puffs of flame on the trail.");
            flameAmount = Float("Trail", "FlameAmount", 2f, 0.5f, 10f, "Flames in each puff at normal speed (more when faster).");
            trailLinger = Float("Trail", "TrailLinger", 0.5f, 0.1f, 3f, "Roughly how long the trail's flames last, in seconds.");
            hitInterval = Float("Trail", "HitInterval", 0.1f, 0.03f, 1f, "Seconds between scorching hits while moving.");
            hitSize = Float("Trail", "HitSize", 0.8f, 0.2f, 4f, "Size of the scorching hit at normal speed (bigger when faster).");
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
