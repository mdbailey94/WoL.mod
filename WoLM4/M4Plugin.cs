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
        public const string PluginVersion = "0.1.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;
        private static ConfigEntry<int> burst, finalBurst;
        private static ConfigEntry<float> bulletSpeed, spread;
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
                new ConfigDescription("Random spread either side of the aim, in degrees (the last pull sprays 1.6 times wider).", new AcceptableValueRange<float>(0f, 20f)));

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
                    damage = new[] { 5, 5, 6 },
                    cooldown = new[] { 0f },
                    knockbackMultiplier = new[] { 1f, 1f, 2f },
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
