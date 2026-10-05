using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using LegendAPI;
using UnityEngine;

namespace WoLSlingshotDash
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class SlingshotDashPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.slingshotdash";
        public const string PluginName = "Slingshot Dash";
        public const string PluginVersion = "0.1.0";

        private static ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Slingshot in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");

            Skills.Register(new SkillInfo
            {
                ID = SlingshotDashState.staticID,
                displayName = "Slingshot",
                description = "Hold to pull back and charge, then release to launch yourself across the room!",
                enhancedDescription = "Launch sends out a bigger burst!",
                icon = LoadIcon("icon.png"),
                tier = 2,
                stateType = typeof(SlingshotDashState),
                skillStats = new SkillStats
                {
                    ID = new[] { SlingshotDashState.staticID },
                    elementType = new[] { "Air" },
                    subElementType = new[] { "Air" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    damage = new[] { 8 },
                    cooldown = new[] { 0.6f },
                    // Positive knockback pushes enemies away from the launch point.
                    knockbackMultiplier = new[] { 30f },
                    hitStunDurationModifier = new[] { 1.2f },
                    sameAttackImmunityTime = new[] { 0.25f }
                },
                priceMultiplier = 3,
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private Sprite LoadIcon(string fileName)
        {
            string resourceName = $"{nameof(WoLSlingshotDash)}.Assets.{fileName}";
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
