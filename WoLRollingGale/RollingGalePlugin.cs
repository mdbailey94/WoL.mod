using System.IO;
using System.Reflection;
using BepInEx;
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
        public const string PluginVersion = "0.1.0";

        private void Awake()
        {
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
                    damage = new[] { 12 },
                    cooldown = new[] { 5f },
                    // Negative knockback pulls targets toward the burst, like Gust Burst.
                    knockbackMultiplier = new[] { -14f },
                    hitStunDurationModifier = new[] { 1.1f },
                    sameAttackImmunityTime = new[] { 0.25f }
                },
                priceMultiplier = 4
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
