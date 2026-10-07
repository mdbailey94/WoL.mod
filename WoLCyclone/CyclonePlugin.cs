using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLCyclone
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class CyclonePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.cyclone";
        public const string PluginName = "Cyclone";
        public const string PluginVersion = "0.3.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Cyclone in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");

            Skills.Register(new SkillInfo
            {
                ID = CycloneState.staticID,
                displayName = "Cyclone",
                description = "Hold to whip up a twister you can steer, growing into a raging hurricane that bursts, blowing everyone away!",
                enhancedDescription = "Reaches full strength sooner and bursts wider!",
                icon = LoadIcon("icon.png"),
                tier = 2,
                stateType = typeof(CycloneState),
                skillStats = new SkillStats
                {
                    ID = new[] { CycloneState.staticID },
                    elementType = new[] { "Air" },
                    subElementType = new[] { "Air" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Levels 1-4 are the twister's hits as it grows over 4 s (stronger and more
                    // often each stage), level 5 the final burst after a second at full power, level
                    // 6 the game's twister and storm used only for the look (harmless). Held to the
                    // burst: roughly 130 to an enemy caught the whole time.
                    damage = new[] { 2, 3, 4, 6, 14, 0 },
                    cooldown = new[] { 5f },
                    // The tornado tugs enemies gently in to keep them inside; the burst throws hard.
                    knockbackMultiplier = new[] { -6f, -6f, -7f, -8f, 70f, 0f },
                    hitStunDurationModifier = new[] { 1f, 1f, 1f, 1f, 1.4f, 0f },
                    showDamageNumber = new[] { true, true, true, true, true, false },
                    // Shorter than the fastest hit rate, so every hit can land.
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
            string resourceName = $"{nameof(WoLCyclone)}.Assets.{fileName}";
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
