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
        public const string PluginVersion = "0.3.1";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;
        private static ConfigEntry<int> burst, finalBurst;
        private static ConfigEntry<float> bulletSpeed, spread, range;
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
        public static float BulletSpeed => Mathf.Clamp(bulletSpeed?.Value ?? 3f, 0.5f, 6f);
        // 0.3.0 (Matt: 'powerful, fast and long range'): how far a bullet flies, in units.
        public static float Range => Mathf.Clamp(range?.Value ?? 14f, 3f, 30f);
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
            bulletSpeed = Config.Bind("Gun", "BulletSpeed", 3f,
                new ConfigDescription("Bullet speed, times the game's Air Dart.", new AcceptableValueRange<float>(0.5f, 6f)));
            range = Config.Bind("Gun", "Range", 14f,
                new ConfigDescription("How far a bullet flies before it's gone, in units.", new AcceptableValueRange<float>(3f, 30f)));
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
            damage = Config.Bind("Balance", "Damage", 8,
                new ConfigDescription("Damage of each bullet of the first two pulls." + restart, new AcceptableValueRange<int>(0, 100)));
            finalDamage = Config.Bind("Balance", "FinalDamage", 9,
                new ConfigDescription("Damage of each bullet of the third pull's spray." + restart, new AcceptableValueRange<int>(0, 100)));
            knockback = Config.Bind("Balance", "Knockback", 3f,
                new ConfigDescription("Knockback of each bullet of the first two pulls." + restart, new AcceptableValueRange<float>(0f, 50f)));
            finalKnockback = Config.Bind("Balance", "FinalKnockback", 5f,
                new ConfigDescription("Knockback of each bullet of the spray." + restart, new AcceptableValueRange<float>(0f, 50f)));

            // 0.3.0 (Matt, 11-04: bring the M4 back, powerful, fast and long range): a config
            // still on 0.2.0's defaults moves to the new ones once.
            var revision = Config.Bind("General", "SettingsRevision", 0, "Which version's defaults this file was last updated to. Leave it alone.");
            if (revision.Value < 1)
            {
                if (Mathf.Approximately(bulletSpeed.Value, 1.6f)) bulletSpeed.Value = 3f;
                if (damage.Value == 5) damage.Value = 8;
                if (finalDamage.Value == 6) finalDamage.Value = 9;
                if (Mathf.Approximately(knockback.Value, 1f)) knockback.Value = 3f;
                if (Mathf.Approximately(finalKnockback.Value, 2f)) finalKnockback.Value = 5f;
            }
            revision.Value = 1;
            M4Look.Load(Logger);

            Skills.Register(new SkillInfo
            {
                ID = M4State.staticID,
                displayName = "M4",
                description = "Shoulder the rifle and fire hard-hitting bursts down range! Three pulls of the trigger, the last one a long spray!",
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

        internal static Texture2D LoadTexture(string fileName)
        {
            string resourceName = $"{nameof(WoLM4)}.Assets.{fileName}";
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return null;
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                texture.LoadImage(data);
                return texture;
            }
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
