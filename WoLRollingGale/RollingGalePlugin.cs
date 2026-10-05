using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLRollingGale
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class RollingGalePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.rollinggale";
        public const string PluginName = "Rolling Gale";
        public const string PluginVersion = "0.3.0";

        private ConfigEntry<bool> modEnabled;

        private void Awake()
        {
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Rolling Gale in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");

            Skills.Register(new SkillInfo
            {
                ID = RollingGaleState.staticID,
                displayName = "Rolling Gale",
                description = "Send a line of wind bursts rolling forward, dragging enemies along with them!",
                enhancedDescription = "Adds a fourth, larger burst!",
                icon = LoadIcon("icon.png"),
                tier = 2,
                stateType = typeof(RollingGaleState),
                skillStats = new SkillStats
                {
                    ID = new[] { RollingGaleState.staticID },
                    elementType = new[] { "Air" },
                    subElementType = new[] { "Air" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Level 1 is each burst, level 2 its echo pulse. At most 3x8 + 3x3 = 33 per enemy
                    // (44 enhanced) if dragged through the whole line.
                    damage = new[] { 8, 3 },
                    cooldown = new[] { 5f },
                    // Negative knockback pulls targets toward the burst, like Gust Burst.
                    knockbackMultiplier = new[] { -22f },
                    hitStunDurationModifier = new[] { 1.1f },
                    // Short, so each burst in the line (and its echo) grabs the enemy again.
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
            string resourceName = $"{nameof(WoLRollingGale)}.Assets.{fileName}";
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
