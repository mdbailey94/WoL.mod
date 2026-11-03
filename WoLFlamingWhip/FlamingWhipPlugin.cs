using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLFlamingWhip
{
    // Flaming Whip (#69, Matt's request): a new Fire basic arcana with long reach. A 3-hit combo:
    // two lashes along the aim, then an overhead crack in a ring round you. Enhanced: each lash
    // leaves a short line of fire on the floor. A basic can't be a signature (the spellbook files
    // every basic under the basic slot before it looks for a signature), so there is no signature.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class FlamingWhipPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.flamingwhip";
        public const string PluginName = "Flaming Whip";
        public const string PluginVersion = "0.2.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.flamingwhip.cfg) ----
        // [Whip] and [Enhanced] apply the next attack; [Balance] after restarting the game.
        private static ConfigEntry<float> lashLength, lashWidth, crackRadius, fireLineTime, fireLineTick;
        private static ConfigEntry<int> lashDamage, crackDamage, fireLineDamage;
        private static ConfigEntry<float> lashBurn, crackBurn, lashKnockback, crackKnockback;

        public static float LashLength => Get(lashLength, 3.5f, 1f, 8f);
        public static float LashWidth => Get(lashWidth, 0.6f, 0.2f, 2f);
        public static float CrackRadius => Get(crackRadius, 1.8f, 0.5f, 6f);
        public static float FireLineTime => Get(fireLineTime, 1.5f, 0.2f, 6f);
        public static float FireLineTick => Get(fireLineTick, 0.4f, 0.1f, 2f);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            bool hadConfig = File.Exists(Config.ConfigFilePath);
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Flaming Whip in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            BindSettings();
            // 0.2.0 (#78): Matt asked for a smaller crack; a 0.1.0 config still holds 2.5 u.
            var revision = Config.Bind("General", "SettingsRevision", 0, "Which version's defaults this file was last updated to. Leave it alone.");
            if (hadConfig && revision.Value < 2)
            {
                crackRadius.Value = (float)crackRadius.DefaultValue;
                Logger.LogInfo("Flaming Whip: settings moved to 0.2.0's smaller crack (CrackRadius 1.8)");
            }
            revision.Value = 2;
            WhipArt.Load(Logger);

            Skills.Register(new SkillInfo
            {
                ID = WhipState.staticID,
                displayName = "Flaming Whip",
                description = "Lash out with a blazing whip that reaches far, then crack it overhead to scorch everything around you!",
                enhancedDescription = "Every lash leaves a line of fire on the floor!",
                icon = LoadIcon("flaming_whip_icon.png"),
                tier = 2,
                stateType = typeof(WhipState),
                skillStats = new SkillStats
                {
                    ID = new[] { WhipState.staticID },
                    elementType = new[] { "Fire" },
                    subElementType = new[] { "Fire" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Levels: 1 a lash (hits 1 and 2), 2 the crack (hit 3), 3 the enhanced fire line.
                    damage = new[]
                    {
                        Mathf.Clamp(lashDamage.Value, 0, 200), Mathf.Clamp(crackDamage.Value, 0, 200), Mathf.Clamp(fireLineDamage.Value, 0, 100)
                    },
                    cooldown = new[] { 0f },
                    knockbackMultiplier = new[] { Mathf.Clamp(lashKnockback.Value, 0f, 100f), Mathf.Clamp(crackKnockback.Value, 0f, 100f), 0f },
                    hitStunDurationModifier = new[] { 1f, 1.2f, 0.3f },
                    showDamageNumber = new[] { true, true, true },
                    burnChance = new[] { Mathf.Clamp01(lashBurn.Value), Mathf.Clamp01(crackBurn.Value), 0f },
                    burnLevel = new[] { 1 },
                    sameAttackImmunityTime = new[] { 0.1f },
                    shakeCamera = new[] { false, true, false }
                },
                priceMultiplier = 3,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private void BindSettings()
        {
            lashLength = Float("Whip", "LashLength", 3.5f, 1f, 8f, "How far each lash reaches along your aim, in units.");
            lashWidth = Float("Whip", "LashWidth", 0.6f, 0.2f, 2f, "How wide the lash's hit is, in units.");
            crackRadius = Float("Whip", "CrackRadius", 1.8f, 0.5f, 6f, "Radius of the third hit's overhead crack round you.");
            fireLineTime = Float("Enhanced", "FireLineTime", 1.5f, 0.2f, 6f, "Seconds each lash's line of fire burns on the floor.");
            fireLineTick = Float("Enhanced", "FireLineTick", 0.4f, 0.1f, 2f, "Seconds between the fire line's hits.");

            const string restart = " Applies after restarting the game.";
            lashDamage = Int("Balance", "LashDamage", 7, 200, "Damage of each lash (hits 1 and 2)." + restart);
            crackDamage = Int("Balance", "CrackDamage", 12, 200, "Damage of the crack (hit 3)." + restart);
            fireLineDamage = Int("Balance", "FireLineDamage", 2, 100, "Damage of each fire line tick (enhanced)." + restart);
            lashBurn = Float("Balance", "LashBurnChance", 0.25f, 0f, 1f, "Chance a lash burns, 0 to 1." + restart);
            crackBurn = Float("Balance", "CrackBurnChance", 0.5f, 0f, 1f, "Chance the crack burns, 0 to 1." + restart);
            lashKnockback = Float("Balance", "LashKnockback", 3f, 0f, 100f, "Knockback of a lash (small, so the next lash still reaches)." + restart);
            crackKnockback = Float("Balance", "CrackKnockback", 25f, 0f, 100f, "Knockback of the crack." + restart);
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        private ConfigEntry<int> Int(string section, string key, int value, int max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(0, max)));

        internal static Texture2D LoadTexture(string fileName)
        {
            string resourceName = $"{nameof(WoLFlamingWhip)}.Assets.{fileName}";
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
