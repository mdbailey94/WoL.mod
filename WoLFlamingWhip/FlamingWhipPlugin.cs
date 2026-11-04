using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLFlamingWhip
{
    // Flaming Whip (#69, redesigned in #81/#86, Matt 2026-11-03): a Fire basic arcana from the
    // giant weapon set. ONE big crack per press: a slow, heavy wind-up, then the whip cracks onto a
    // 1-tile spot exactly 2 tiles out along the aim, and nothing between you and it is hit, so you
    // can be too close or too far. Enhanced: a 1.5-tile spot that leaves a line of fire. A basic
    // can't be a signature (the spellbook files every basic under the basic slot before it looks
    // for a signature), so there is no signature.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class FlamingWhipPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.flamingwhip";
        public const string PluginName = "Flaming Whip";
        public const string PluginVersion = "0.6.2";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.flamingwhip.cfg) ----
        // [Crack] and [Enhanced] apply the next press; [Balance] after restarting the game. 0.3.0's
        // keys are new, so 0.1/0.2's ([Whip], LashDamage...) are simply left unused.
        private static ConfigEntry<float> crackDistance, spotSize, enhancedSpotSize, windUp, windUpPace, shake;
        private static ConfigEntry<float> fireLineTime, fireLineTick;
        private static ConfigEntry<int> crackDamage, fireLineDamage;
        private static ConfigEntry<float> crackBurn, crackKnockback;

        public static float CrackDistance => Get(crackDistance, 3f, 0.5f, 6f);
        public static float SpotSize => Get(spotSize, 1.5f, 0.3f, 4f);
        public static float EnhancedSpotSize => Get(enhancedSpotSize, 2f, 0.3f, 4f);
        public static float WindUp => Get(windUp, 0.3f, 0f, 1.5f);
        public static float WindUpPace => Get(windUpPace, 0.7f, 0.3f, 1f);
        public static float Shake => Get(shake, 1.2f, 0f, 3f);
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
            // 0.4.0 (Matt: "extend range"): the crack lands 3 tiles out; a 0.3.0 config holds 2.
            var revision = Config.Bind("General", "SettingsRevision", 0, "Which version's defaults this file was last updated to. Leave it alone.");
            if (hadConfig && revision.Value < 3)
            {
                crackDistance.Value = (float)crackDistance.DefaultValue;
                Logger.LogInfo("Flaming Whip: settings moved to 0.4.0's longer reach (Distance 3)");
            }
            revision.Value = 3;
            WhipArt.Load(Logger);

            Skills.Register(new SkillInfo
            {
                ID = WhipState.staticID,
                displayName = "Flaming Whip",
                description = "Conjure a giant blazing whip and crack it down on the spot three steps ahead: too close or too far and it misses!",
                enhancedDescription = "A wider crack that leaves a line of fire on the floor!",
                icon = LoadIcon("flaming_whip_icon.png"),
                tier = 2,
                stateType = typeof(WhipState),
                skillStats = new SkillStats
                {
                    ID = new[] { WhipState.staticID },
                    elementType = new[] { "Fire" },
                    subElementType = new[] { "Fire" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Levels: 1 the crack, 2 the enhanced fire line.
                    damage = new[] { Mathf.Clamp(crackDamage.Value, 0, 300), Mathf.Clamp(fireLineDamage.Value, 0, 100) },
                    cooldown = new[] { 0f },
                    knockbackMultiplier = new[] { Mathf.Clamp(crackKnockback.Value, 0f, 100f), 0f },
                    hitStunDurationModifier = new[] { 1.4f, 0.3f },
                    showDamageNumber = new[] { true, true },
                    burnChance = new[] { Mathf.Clamp01(crackBurn.Value), 0f },
                    burnLevel = new[] { 1 },
                    sameAttackImmunityTime = new[] { 0.1f },
                    shakeCamera = new[] { true, false }
                },
                priceMultiplier = 3,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private void BindSettings()
        {
            crackDistance = Float("Crack", "Distance", 3f, 0.5f, 6f, "How far out along your aim the crack lands, in units (tiles), from the hand.");
            spotSize = Float("Crack", "HitSpot", 1.5f, 0.3f, 4f, "Width of the spot the crack hits, in units (tiles). 0.6.0 (#98): bigger, 1.5 (was 1); the old SpotSize key is unused.");
            windUp = Float("Crack", "WindUp", 0.3f, 0f, 1.5f, "Seconds from the press to the crack (the cast time).");
            windUpPace = Float("Crack", "WindUpPace", 0.7f, 0.3f, 1f, "How fast the wizard's swing plays (0.7 = 30% slower, for a heavy feel; 1 = normal).");
            shake = Float("Crack", "Shake", 1.2f, 0f, 3f, "Strength of the camera shake when it cracks (0 = none).");
            enhancedSpotSize = Float("Enhanced", "HitSpot", 2f, 0.3f, 4f, "Width of the spot when enhanced. 0.6.0 (#98): 2 (was 1.5); the old SpotSize key is unused.");
            fireLineTime = Float("Enhanced", "FireLineTime", 1.5f, 0.2f, 6f, "Seconds the line of fire burns on the floor.");
            fireLineTick = Float("Enhanced", "FireLineTick", 0.4f, 0.1f, 2f, "Seconds between the fire line's hits.");

            const string restart = " Applies after restarting the game.";
            crackDamage = Int("Balance", "BigCrackDamage", 18, 300, "Damage of the crack." + restart);
            fireLineDamage = Int("Balance", "FireLineDamage", 2, 100, "Damage of each fire line tick (enhanced)." + restart);
            crackBurn = Float("Balance", "BigCrackBurnChance", 0.5f, 0f, 1f, "Chance the crack burns, 0 to 1." + restart);
            crackKnockback = Float("Balance", "BigCrackKnockback", 20f, 0f, 100f, "Knockback of the crack." + restart);
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
