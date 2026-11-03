using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLGiantSpear
{
    // Giant Air Spear (#85, Matt 2026-11-03, the GIANT WEAPON set): a new Air basic arcana, a huge
    // spear of wind wielded slowly. Two long, heavy stabs along the aim, then a throw: the spear
    // flies out, piercing everything on its way, and blows away into wind at the end. Enhanced: a
    // longer reach, and a gust where the thrown spear ends. 0.2.0 (Matt's play-test): drawn as wind
    // (see-through cyan with wind ghosts), slower and heavier, and the throw no longer returns.
    // Drawn with WoL Art's spear (#44 for the stabs, 'Giant weapons (2026-11-03)' for the throw and
    // the icon) at giant scale. A basic
    // can't be a signature, so there is none.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class GiantSpearPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.giantspear";
        public const string PluginName = "Giant Air Spear";
        public const string PluginVersion = "0.2.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> returnToHand;

        public static void Log(string message) => log?.LogInfo(message);
        public static bool ReturnToHand => returnToHand != null && returnToHand.Value;

        // ---- Settings (BepInEx\config\mdbailey94.wol.giantspear.cfg) ----
        // [Spear] and [Enhanced] apply the next attack; [Balance] after restarting the game.
        private static ConfigEntry<float> stabReach, stabWidth, throwRange, throwSpeed, returnSpeed, throwWidth, enhancedReach, gustRadius;
        private static ConfigEntry<int> stabDamage, throwDamage, gustDamage;
        private static ConfigEntry<float> stabKnockback, throwKnockback, gustKnockback;

        public static float StabReach => Get(stabReach, 3.4f, 1f, 8f);
        public static float StabWidth => Get(stabWidth, 0.9f, 0.2f, 3f);
        public static float ThrowRange => Get(throwRange, 6.5f, 2f, 15f);
        public static float ThrowSpeed => Get(throwSpeed, 16f, 4f, 40f);
        public static float ReturnSpeed => Get(returnSpeed, 20f, 4f, 40f);
        public static float ThrowWidth => Get(throwWidth, 1.0f, 0.2f, 3f);
        public static float EnhancedReach => Get(enhancedReach, 1.25f, 1f, 2f);
        public static float GustRadius => Get(gustRadius, 1.6f, 0.5f, 4f);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            bool hadConfig = File.Exists(Config.ConfigFilePath);
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Giant Air Spear in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            BindSettings();
            // 0.2.0: heavier stabs; a 0.1 config still holds the lighter knockback.
            var revision = Config.Bind("General", "SettingsRevision", 0, "Which version's defaults this file was last updated to. Leave it alone.");
            if (hadConfig && revision.Value < 1)
            {
                stabKnockback.Value = (float)stabKnockback.DefaultValue;
                Logger.LogInfo("Giant Air Spear: settings moved to 0.2.0's heavier stabs (StabKnockback 12)");
            }
            revision.Value = 1;
            SpearArt.Load(Logger);

            Skills.Register(new SkillInfo
            {
                ID = GiantSpearState.staticID,
                displayName = "Giant Air Spear",
                description = "Heave a giant spear of wind: two crushing stabs, then hurl it through your foes until it blows away!",
                enhancedDescription = "Longer reach, and the thrown spear bursts into a gust where it ends!",
                icon = LoadIcon("giant_spear_icon.png"),
                tier = 2,
                stateType = typeof(GiantSpearState),
                skillStats = new SkillStats
                {
                    ID = new[] { GiantSpearState.staticID },
                    elementType = new[] { "Air" },
                    subElementType = new[] { "Air" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Levels: 1 a stab (hits 1 and 2), 2 the thrown spear (out and back), 3 the
                    // enhanced gust.
                    damage = new[]
                    {
                        Mathf.Clamp(stabDamage.Value, 0, 200), Mathf.Clamp(throwDamage.Value, 0, 200), Mathf.Clamp(gustDamage.Value, 0, 200)
                    },
                    cooldown = new[] { 0f },
                    knockbackMultiplier = new[]
                    {
                        Mathf.Clamp(stabKnockback.Value, 0f, 100f), Mathf.Clamp(throwKnockback.Value, 0f, 100f), Mathf.Clamp(gustKnockback.Value, 0f, 100f)
                    },
                    hitStunDurationModifier = new[] { 1.3f, 1.3f, 1f },
                    showDamageNumber = new[] { true, true, true },
                    sameAttackImmunityTime = new[] { 0.1f },
                    shakeCamera = new[] { true, true, true }
                },
                priceMultiplier = 3,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private void BindSettings()
        {
            stabReach = Float("Spear", "StabReach", 3.4f, 1f, 8f, "How far each stab reaches along your aim, in units.");
            stabWidth = Float("Spear", "StabWidth", 0.9f, 0.2f, 3f, "How wide a stab's hit is, in units.");
            throwRange = Float("Spear", "ThrowRange", 6.5f, 2f, 15f, "How far the thrown spear flies before it comes back, in units.");
            throwSpeed = Float("Spear", "ThrowSpeed", 16f, 4f, 40f, "How fast the thrown spear flies out, units a second.");
            returnToHand = Config.Bind("Spear", "ReturnToHand", false, "The thrown spear flies back to you, hitting again on the way (0.1's throw). Off: it blows away at the end of its flight.");
            returnSpeed = Float("Spear", "ReturnSpeed", 20f, 4f, 40f, "With ReturnToHand: how fast it flies back to you, units a second.");
            throwWidth = Float("Spear", "ThrowWidth", 1.0f, 0.2f, 3f, "How wide the thrown spear's hit is, in units.");
            enhancedReach = Float("Enhanced", "ReachMultiplier", 1.25f, 1f, 2f, "Enhanced: stab reach and throw range times this.");
            gustRadius = Float("Enhanced", "GustRadius", 1.6f, 0.5f, 4f, "Enhanced: radius of the gust where the thrown spear turns.");

            const string restart = " Applies after restarting the game.";
            stabDamage = Int("Balance", "StabDamage", 12, 200, "Damage of each stab (hits 1 and 2)." + restart);
            throwDamage = Int("Balance", "ThrowDamage", 16, 200, "Damage of the thrown spear (and again on the way back with ReturnToHand)." + restart);
            gustDamage = Int("Balance", "GustDamage", 10, 200, "Damage of the enhanced gust." + restart);
            stabKnockback = Float("Balance", "StabKnockback", 12f, 0f, 100f, "Knockback of a stab." + restart);
            throwKnockback = Float("Balance", "ThrowKnockback", 20f, 0f, 100f, "Knockback of the thrown spear." + restart);
            gustKnockback = Float("Balance", "GustKnockback", 30f, 0f, 100f, "Knockback of the enhanced gust." + restart);
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        private ConfigEntry<int> Int(string section, string key, int value, int max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(0, max)));

        internal static Texture2D LoadTexture(string fileName)
        {
            string resourceName = $"{nameof(WoLGiantSpear)}.Assets.{fileName}";
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    log?.LogWarning($"Resource {resourceName} not found");
                    return null;
                }
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                texture.LoadImage(data);
                return texture;
            }
        }

        private Sprite LoadIcon(string fileName)
        {
            Texture2D texture = LoadTexture(fileName);
            if (texture == null)
            {
                Logger.LogWarning("Icon not found, using the default icon");
                return null;
            }
            return GameIcon.Create(texture, Logger);
        }
    }
}
