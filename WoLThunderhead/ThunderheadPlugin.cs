using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
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
        public const string PluginVersion = "0.2.2";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.thunderhead.cfg) ----
        // [Leap] and [Lightning] apply the next time you cast it; [Balance] after restarting the game.
        private static ConfigEntry<float> height, windupTime, riseTime, airTime, enhancedAirTime, crashTime, recoverTime;
        private static ConfigEntry<float> radius, enhancedRadius, volleyInterval, strikeSize, slamSize, enhancedSlamSize;
        private static ConfigEntry<int> volleySize;
        private static ConfigEntry<int> strikeDamage, slamDamage;
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
        public static float VolleyInterval => Get(volleyInterval, 0.25f, 0.05f, 2f);
        public static int VolleySize => Mathf.Clamp(volleySize?.Value ?? 3, 1, 12);
        public static float StrikeSize => Get(strikeSize, 1f, 0.3f, 4f);
        public static float SlamSize => Get(slamSize, 2.4f, 0.5f, 6f);
        public static float EnhancedSlamSize => Get(enhancedSlamSize, 3f, 0.5f, 6f);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Thunderhead in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            BindSettings();

            Skills.Register(new SkillInfo
            {
                ID = ThunderheadState.staticID,
                displayName = "Thunderhead",
                description = "Leap high into the air, out of reach, as lightning rains down around you, then crash down in a thunderous slam!",
                enhancedDescription = "Stay up longer, strike wider and slam harder!",
                icon = LoadIcon("icon.png"),
                tier = 2,
                stateType = typeof(ThunderheadState),
                skillStats = new SkillStats
                {
                    ID = new[] { ThunderheadState.staticID },
                    elementType = new[] { "Lightning" },
                    subElementType = new[] { "Lightning" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Level 1 is the strikes that rain down while you're up (volleys of three every
                    // 0.25 s for 1 s round the spot below), level 2 the slam as you crash down, level
                    // 3 the game's lightning bolts used only for the look (harmless). An enemy close
                    // by: a few strikes and the slam, about 30. All from [Balance] in the config.
                    damage = new[] { Mathf.Clamp(strikeDamage.Value, 0, 200), Mathf.Clamp(slamDamage.Value, 0, 500), 0 },
                    cooldown = new[] { Mathf.Clamp(cooldown.Value, 0.5f, 60f) },
                    knockbackMultiplier = new[] { Mathf.Clamp(strikeKnockback.Value, 0f, 200f), Mathf.Clamp(slamKnockback.Value, 0f, 200f), 0f },
                    hitStunDurationModifier = new[] { 1f, 1.3f, 0f },
                    showDamageNumber = new[] { true, true, false },
                    shockChance = new[] { Mathf.Clamp01(strikeShockChance.Value), Mathf.Clamp01(slamShockChance.Value), 0f },
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
            recoverTime = Float("Leap", "RecoverTime", 0.15f, 0f, 1f, "Seconds on the ground after the slam before you can act.");

            radius = Float("Lightning", "Radius", 2.2f, 0.5f, 8f, "How far from the spot below the strikes can land.");
            enhancedRadius = Float("Lightning", "EnhancedRadius", 2.8f, 0.5f, 8f, "Radius when enhanced.");
            volleyInterval = Float("Lightning", "VolleyInterval", 0.25f, 0.05f, 2f, "Seconds between volleys of strikes while up.");
            volleySize = Config.Bind("Lightning", "VolleySize", 3,
                new ConfigDescription("Strikes in each volley.", new AcceptableValueRange<int>(1, 12)));
            strikeSize = Float("Lightning", "StrikeSize", 1f, 0.3f, 4f, "Size of each strike's hit area.");
            slamSize = Float("Lightning", "SlamSize", 2.4f, 0.5f, 6f, "Size of the landing slam's hit area.");
            enhancedSlamSize = Float("Lightning", "EnhancedSlamSize", 3f, 0.5f, 6f, "SlamSize when enhanced.");

            const string restart = " Applies after restarting the game.";
            strikeDamage = Config.Bind("Balance", "StrikeDamage", 5,
                new ConfigDescription("Damage of each lightning strike." + restart, new AcceptableValueRange<int>(0, 200)));
            slamDamage = Config.Bind("Balance", "SlamDamage", 14,
                new ConfigDescription("Damage of the landing slam." + restart, new AcceptableValueRange<int>(0, 500)));
            strikeKnockback = Float("Balance", "StrikeKnockback", 6f, 0f, 200f, "Knockback of each strike." + restart);
            slamKnockback = Float("Balance", "SlamKnockback", 45f, 0f, 200f, "Knockback of the slam." + restart);
            strikeShockChance = Float("Balance", "StrikeShockChance", 0.2f, 0f, 1f, "Chance a strike shocks (0 to 1)." + restart);
            slamShockChance = Float("Balance", "SlamShockChance", 0.5f, 0f, 1f, "Chance the slam shocks (0 to 1)." + restart);
            cooldown = Float("Balance", "Cooldown", 6f, 0.5f, 60f, "Cooldown in seconds." + restart);
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
