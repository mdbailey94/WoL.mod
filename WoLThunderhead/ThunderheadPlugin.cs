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
        public const string PluginVersion = "0.2.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Thunderhead in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");

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
                    // by: a few strikes and the slam, about 30.
                    damage = new[] { 5, 14, 0 },
                    cooldown = new[] { 6f },
                    knockbackMultiplier = new[] { 6f, 45f, 0f },
                    hitStunDurationModifier = new[] { 1f, 1.3f, 0f },
                    showDamageNumber = new[] { true, true, false },
                    shockChance = new[] { 0.2f, 0.5f, 0f },
                    shockLevel = new[] { 1 },
                    sameAttackImmunityTime = new[] { 0.1f }
                },
                priceMultiplier = 4,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

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
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }
    }
}
