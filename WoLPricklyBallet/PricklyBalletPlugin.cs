using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLPricklyBallet
{
    // Prickly Ballet (#68, Matt's request): a new Earth dash arcana. The dash is the normal dash;
    // where it started, a cactus dancer pops out of the ground and pirouettes, pricking enemies
    // round it, then bursts into a ring of needles. A dash can't be a signature (the spellbook
    // files every dash under the dash slot before it looks for a signature), so there is no
    // signature version.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class PricklyBalletPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.pricklyballet";
        public const string PluginName = "Prickly Ballet";
        public const string PluginVersion = "0.1.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.pricklyballet.cfg) ----
        // [Dancer] and [Enhanced] apply the next dash; [Balance] after restarting the game.
        private static ConfigEntry<float> lifetime, every, reach, prickEvery, needleRange, needleSpeed;
        private static ConfigEntry<float> enhancedLifetime, enhancedEvery;
        private static ConfigEntry<int> needles;
        private static ConfigEntry<int> prickDamage, needleDamage;
        private static ConfigEntry<float> prickKnockback, needleKnockback, cooldown;

        public static float Lifetime => Get(lifetime, 3f, 0.5f, 10f);
        public static float Every => Get(every, 4f, 0f, 30f);
        public static float EnhancedLifetime => Get(enhancedLifetime, 4f, 0.5f, 10f);
        public static float EnhancedEvery => Get(enhancedEvery, 3f, 0f, 30f);
        public static float Reach => Get(reach, 1.2f, 0.3f, 4f);
        public static float PrickEvery => Get(prickEvery, 0.4f, 0.1f, 2f);
        public static int Needles => Mathf.Clamp(needles?.Value ?? 8, 0, 32);
        public static float NeedleRange => Get(needleRange, 4f, 0.5f, 12f);
        public static float NeedleSpeed => Get(needleSpeed, 14f, 2f, 40f);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Prickly Ballet in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            BindSettings();
            DancerArt.Load(Logger);

            Skills.Register(new SkillInfo
            {
                ID = PricklyBalletState.staticID,
                displayName = "Prickly Ballet",
                description = "Dash away and leave a cactus dancer pirouetting behind you, pricking everyone close, then bursting into needles!",
                enhancedDescription = "The dancer spins longer and comes back sooner!",
                icon = LoadIcon("prickly_ballet_icon.png"),
                tier = 2,
                stateType = typeof(PricklyBalletState),
                skillStats = new SkillStats
                {
                    ID = new[] { PricklyBalletState.staticID },
                    elementType = new[] { "Earth" },
                    subElementType = new[] { "Earth" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Levels (Dancer): 1 a prick, 2 a needle.
                    damage = new[] { Mathf.Clamp(prickDamage.Value, 0, 200), Mathf.Clamp(needleDamage.Value, 0, 200) },
                    cooldown = new[] { Mathf.Clamp(cooldown.Value, 0.1f, 5f) },
                    knockbackMultiplier = new[] { Mathf.Clamp(prickKnockback.Value, 0f, 200f), Mathf.Clamp(needleKnockback.Value, 0f, 200f) },
                    hitStunDurationModifier = new[] { 0.6f, 1f },
                    showDamageNumber = new[] { true, true },
                    // Shorter than a prick's interval, so each prick lands.
                    sameAttackImmunityTime = new[] { 0.1f }
                },
                priceMultiplier = 3,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private void BindSettings()
        {
            lifetime = Float("Dancer", "Lifetime", 3f, 0.5f, 10f, "Seconds the cactus dancer pirouettes before it bursts.");
            every = Float("Dancer", "Every", 4f, 0f, 30f, "At most one dancer every this many seconds (dashes in between are plain dashes).");
            reach = Float("Dancer", "Reach", 1.2f, 0.3f, 4f, "Radius in units round the dancer that its pricks hit.");
            prickEvery = Float("Dancer", "PrickEvery", 0.4f, 0.1f, 2f, "Seconds between pricks.");
            needles = Config.Bind("Dancer", "Needles", 8,
                new ConfigDescription("Needles it bursts into at the end (0 = none).", new AcceptableValueRange<int>(0, 32)));
            needleRange = Float("Dancer", "NeedleRange", 4f, 0.5f, 12f, "How far the needles fly, in units.");
            needleSpeed = Float("Dancer", "NeedleSpeed", 14f, 2f, 40f, "Needle speed, units per second.");
            enhancedLifetime = Float("Enhanced", "Lifetime", 4f, 0.5f, 10f, "Dancer lifetime when enhanced.");
            enhancedEvery = Float("Enhanced", "Every", 3f, 0f, 30f, "At most one dancer every this many seconds when enhanced.");

            const string restart = " Applies after restarting the game.";
            prickDamage = Int("Balance", "PrickDamage", 4, 200, "Damage of each prick." + restart);
            needleDamage = Int("Balance", "NeedleDamage", 6, 200, "Damage of each needle." + restart);
            prickKnockback = Float("Balance", "PrickKnockback", 6f, 0f, 200f, "Knockback of a prick (small)." + restart);
            needleKnockback = Float("Balance", "NeedleKnockback", 10f, 0f, 200f, "Knockback of a needle." + restart);
            cooldown = Float("Balance", "Cooldown", 0.6f, 0.1f, 5f, "Cooldown of the dash itself, in seconds." + restart);
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        private ConfigEntry<int> Int(string section, string key, int value, int max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(0, max)));

        internal static Texture2D LoadTexture(string fileName)
        {
            string resourceName = $"{nameof(WoLPricklyBallet)}.Assets.{fileName}";
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
