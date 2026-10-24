using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLBrambleRing
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class BrambleRingPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.bramblering";
        public const string PluginName = "Bramble Ring";
        public const string PluginVersion = "0.1.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Bramble Ring in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");

            Skills.Register(new SkillInfo
            {
                ID = BrambleRingState.staticID,
                displayName = "Bramble Ring",
                description = "Burst a ring of thorns out of the ground around you, slowing the enemies they catch!",
                enhancedDescription = "A second, wider ring bursts out a moment later!",
                icon = LoadIcon("icon.png"),
                // Same tier and price as Terra Ring, the vanilla Earth arcana it's closest to.
                tier = 4,
                stateType = typeof(BrambleRingState),
                skillStats = new SkillStats
                {
                    ID = new[] { BrambleRingState.staticID },
                    elementType = new[] { "Earth" },
                    subElementType = new[] { "Earth" },
                    // Like Terra Ring: ground spikes hit enemies' feet (and crates, pots...).
                    targetNames = new[] { "EnemyFloorContact", "DestructibleHurtBox" },
                    // Level 1 is the first ring, level 2 the enhanced outer ring. Each ring hits an
                    // enemy once, so 25 a cast (up to 45 enhanced, for an enemy both rings reach).
                    // Terra Ring does 15 a ring, cascading up to 4 times, without the slow.
                    damage = new[] { 25, 20 },
                    cooldown = new[] { 5f },
                    // A small shove outward, away from you (the direction is set per thorn).
                    knockbackMultiplier = new[] { 6f },
                    knockbackOverwrite = new[] { true },
                    hitStunDurationModifier = new[] { 1f },
                    // Long enough that the thorns of one ring (two rows) only hit an enemy once.
                    sameTargetImmunityTime = new[] { 0.5f },
                    // The game's slow (Air's status, which any element can carry): level 2 is
                    // 50% move speed for 2 seconds.
                    slowChance = new[] { 1f },
                    slowLevel = new[] { 2 }
                },
                priceMultiplier = 6,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private Sprite LoadIcon(string fileName)
        {
            string resourceName = $"{nameof(WoLBrambleRing)}.Assets.{fileName}";
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
