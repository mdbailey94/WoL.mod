using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLM4
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class M4Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.m4";
        public const string PluginName = "M4";
        public const string PluginVersion = "0.2.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;
        private static ConfigEntry<int> burst, finalBurst;
        private static ConfigEntry<float> bulletSpeed, spread;
        private static ConfigEntry<float> roundGap, enhancedRoundGap, finalSpread;
        private static ConfigEntry<int> enhancedExtraRounds;
        private static ConfigEntry<int> damage, finalDamage;
        private static ConfigEntry<float> knockback, finalKnockback;
        // Animation time between the rounds of a burst (Air Dart's darts are 0.08 apart).
        public static float RoundGap => Mathf.Clamp(roundGap?.Value ?? 0.055f, 0.02f, 0.2f);
        public static float EnhancedRoundGap => Mathf.Clamp(enhancedRoundGap?.Value ?? 0.04f, 0.02f, 0.2f);
        public static int EnhancedExtraRounds => Mathf.Clamp(enhancedExtraRounds?.Value ?? 1, 0, 6);
        public static float FinalSpread => Mathf.Clamp(finalSpread?.Value ?? 1.6f, 1f, 5f);
        public static int Burst => Mathf.Clamp(burst?.Value ?? 3, 1, 8);
        public static int FinalBurst => Mathf.Clamp(finalBurst?.Value ?? 5, 1, 12);
        public static float BulletSpeed => Mathf.Clamp(bulletSpeed?.Value ?? 1.6f, 0.5f, 4f);
        public static float Spread => Mathf.Clamp(spread?.Value ?? 3f, 0f, 20f);

        public static void Log(string message) => log?.LogInfo(message);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer the M4 in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            burst = Config.Bind("Gun", "Burst", 3,
                new ConfigDescription("Rounds in each of the first two trigger pulls.", new AcceptableValueRange<int>(1, 8)));
            finalBurst = Config.Bind("Gun", "FinalBurst", 5,
                new ConfigDescription("Rounds in the third pull.", new AcceptableValueRange<int>(1, 12)));
            bulletSpeed = Config.Bind("Gun", "BulletSpeed", 1.6f,
                new ConfigDescription("Bullet speed, times the game's Air Dart.", new AcceptableValueRange<float>(0.5f, 4f)));
            spread = Config.Bind("Gun", "Spread", 3f,
                new ConfigDescription("Random spread either side of the aim, in degrees (the last pull sprays FinalSpread times wider).", new AcceptableValueRange<float>(0f, 20f)));
            finalSpread = Config.Bind("Gun", "FinalSpread", 1.6f,
                new ConfigDescription("How much wider the third pull sprays than Spread.", new AcceptableValueRange<float>(1f, 5f)));
            roundGap = Config.Bind("Gun", "RoundGap", 0.055f,
                new ConfigDescription("Time between the rounds of a burst, as a share of the attack animation (smaller = faster fire).", new AcceptableValueRange<float>(0.02f, 0.2f)));
            enhancedRoundGap = Config.Bind("Enhanced", "RoundGap", 0.04f,
                new ConfigDescription("RoundGap when enhanced.", new AcceptableValueRange<float>(0.02f, 0.2f)));
            enhancedExtraRounds = Config.Bind("Enhanced", "ExtraRounds", 1,
                new ConfigDescription("Rounds added to every burst when enhanced.", new AcceptableValueRange<int>(0, 6)));
            const string restart = " Applies after restarting the game.";
            damage = Config.Bind("Balance", "Damage", 5,
                new ConfigDescription("Damage of each bullet of the first two pulls." + restart, new AcceptableValueRange<int>(0, 100)));
            finalDamage = Config.Bind("Balance", "FinalDamage", 6,
                new ConfigDescription("Damage of each bullet of the third pull's spray." + restart, new AcceptableValueRange<int>(0, 100)));
            knockback = Config.Bind("Balance", "Knockback", 1f,
                new ConfigDescription("Knockback of each bullet of the first two pulls." + restart, new AcceptableValueRange<float>(0f, 50f)));
            finalKnockback = Config.Bind("Balance", "FinalKnockback", 2f,
                new ConfigDescription("Knockback of each bullet of the spray." + restart, new AcceptableValueRange<float>(0f, 50f)));

            Skills.Register(new SkillInfo
            {
                ID = M4State.staticID,
                displayName = "M4",
                description = "Squeeze off bursts of rifle fire! Three pulls of the trigger, the last one a long spray!",
                enhancedDescription = "One more round in every burst, and they come faster!",
                icon = LoadIcon("icon.png"),
                tier = 1,
                stateType = typeof(M4State),
                skillStats = new SkillStats
                {
                    ID = new[] { M4State.staticID },
                    elementType = new[] { "Air" },
                    subElementType = new[] { "Air" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Per bullet, by trigger pull: about 15 for each of the first two bursts and
                    // 30 for the spray.
                    damage = new[] { damage.Value, damage.Value, finalDamage.Value },
                    cooldown = new[] { 0f },
                    knockbackMultiplier = new[] { knockback.Value, knockback.Value, finalKnockback.Value },
                    // Bullets land a few hundredths of a second apart; every one counts.
                    sameAttackImmunityTime = new[] { 0f }
                },
                priceMultiplier = 2,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private Sprite LoadIcon(string fileName)
        {
            string resourceName = $"{nameof(WoLM4)}.Assets.{fileName}";
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
