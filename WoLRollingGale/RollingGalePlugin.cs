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
        public const string PluginVersion = "0.5.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<bool> signatureWithArcana;

        public static void Log(string message) => log?.LogInfo(message);

        // The signature's flashes and ease-back run on real time, and its watchdog never leaves
        // the camera or light changed for long (Cinematic.cs).
        private void Update() => Cinematic.Tick();

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Rolling Gale in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            signatureWithArcana = Config.Bind("General", "SignatureUnlocksWithArcana", true,
                "Unlock Rolling Gale's signature version along with the arcana itself, so it can go in the signature slot as soon as " +
                "you have it. Off: its Signature drop has to be found in a run, as for the game's own arcana.");
            // The signature version only shows up in the spellbook's Signature list once it's
            // unlocked; see SignatureUnlock.cs.
            SignatureUnlock.Install(new HarmonyLib.Harmony(PluginGuid), RollingGaleState.staticID,
                () => signatureWithArcana.Value, message => Log("Rolling Gale: " + message));

            Skills.Register(new SkillInfo
            {
                ID = RollingGaleState.staticID,
                displayName = "Rolling Gale",
                description = "Send a line of wind bursts rolling forward, dragging enemies along with them!",
                // The game shows this one text on the card for both the enhanced and the signature.
                enhancedDescription = "Adds a fourth, larger burst! Signature: a sweeping fan of bursts and a huge final gale!",
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
                    // Levels 3 and 4 (0.5.0) are the signature's fan bursts and its final gale: an
                    // enemy dragged through all of it takes up to 6x12 + 5x3 + 45, about 130, about
                    // what the game's air signatures do to one enemy.
                    damage = new[] { 8, 3, 12, 45 },
                    cooldown = new[] { 5f },
                    // Negative knockback pulls targets toward the burst, like Gust Burst.
                    knockbackMultiplier = new[] { -22f, -22f, -26f, -36f },
                    hitStunDurationModifier = new[] { 1.1f, 1.1f, 1.2f, 1.6f },
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
                return GameIcon.Create(texture, Logger);
            }
        }
    }
}
