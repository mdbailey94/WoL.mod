using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLPricklyBallet
{
    // Cactus Tango (#68, renamed in #77; 0.1.0 was "Prickly Ballet", which is the base game's own
    // cactus arcana): a new Earth dash arcana. The dash is the normal dash; where it started, the
    // base game's cactus (from its Prickly Ballet) pops out of the ground and needles everyone
    // round it. A dash can't be a signature (the spellbook files every dash under the dash slot
    // before it looks for a signature), so there is no signature version. The mod's folder, GUID
    // and skill ID keep their 0.1.0 names so installs, configs and saves carry over.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class PricklyBalletPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.pricklyballet";
        public const string PluginName = "Cactus Tango";
        public const string PluginVersion = "0.4.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.pricklyballet.cfg) ----
        // [Cactus] and [Enhanced] apply the next dash; [Balance] after restarting the game.
        private static ConfigEntry<float> lifetime, every, hitEvery;
        private static ConfigEntry<float> enhancedLifetime, enhancedEvery, followDistance;
        private static ConfigEntry<bool> follow;
        private static ConfigEntry<int> hitDamage;
        private static ConfigEntry<float> hitKnockback, cooldown;

        public static float Lifetime => Get(lifetime, 3f, 0.5f, 10f);
        public static float Every => Get(every, 3f, 0f, 30f);
        public static float EnhancedLifetime => Get(enhancedLifetime, 4f, 0.5f, 10f);
        public static float EnhancedEvery => Get(enhancedEvery, 2f, 0f, 30f);
        public static float HitEvery => Get(hitEvery, 0.3f, 0.1f, 2f);
        public static bool Follow => follow == null || follow.Value;
        public static float FollowDistance => Get(followDistance, 0.9f, 0f, 3f);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Cactus Tango in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            BindSettings();
            // 0.4.0 (Matt, 11-04: 'make the cactus tango very powerful'): 12 a needle (was 4), a
            // needle every 0.3 s (0.4), a cactus every 3 s (6; enhanced 2, was 4.5), knockback 10
            // (6). A config still on the old values moves over once.
            var power = Config.Bind("General", "PowerRevision", 0, "Which version's power defaults this file was last updated to. Leave it alone.");
            if (power.Value < 1)
            {
                if (hitDamage.Value == 4) hitDamage.Value = 12;
                if (Mathf.Approximately(hitEvery.Value, 0.4f)) hitEvery.Value = 0.3f;
                if (Mathf.Approximately(every.Value, 6f)) every.Value = 3f;
                if (Mathf.Approximately(enhancedEvery.Value, 4.5f)) enhancedEvery.Value = 2f;
                if (Mathf.Approximately(hitKnockback.Value, 6f)) hitKnockback.Value = 10f;
                Log("Cactus Tango: moved to 0.4.0's power numbers");
            }
            power.Value = 1;

            Skills.Register(new SkillInfo
            {
                ID = PricklyBalletState.staticID,
                displayName = "Cactus Tango",
                description = "Dash away and leave a cactus behind you, needling everyone close!",
                enhancedDescription = "The cactus stays longer and comes back sooner!",
                icon = LoadIcon("prickly_ballet_icon.png"),
                tier = 2,
                stateType = typeof(PricklyBalletState),
                skillStats = new SkillStats
                {
                    ID = new[] { PricklyBalletState.staticID },
                    elementType = new[] { "Earth" },
                    subElementType = new[] { "Earth" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Level 1: each of the cactus's hits.
                    damage = new[] { Mathf.Clamp(hitDamage.Value, 0, 200) },
                    cooldown = new[] { Mathf.Clamp(cooldown.Value, 0.1f, 5f) },
                    knockbackMultiplier = new[] { Mathf.Clamp(hitKnockback.Value, 0f, 200f) },
                    hitStunDurationModifier = new[] { 0.6f },
                    showDamageNumber = new[] { true },
                    // Shorter than the hit interval, so each hit lands.
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
            // New sections in 0.2.0 ([Dancer] is 0.1.0's), so the new cooldowns (6 s, enhanced
            // 4.5 s) take effect on an existing config too.
            lifetime = Float("Cactus", "Lifetime", 3f, 0.5f, 10f, "Seconds the cactus stays and needles enemies.");
            every = Float("Cactus", "Every", 3f, 0f, 30f, "At most one cactus every this many seconds (dashes in between are plain dashes).");
            hitEvery = Float("Cactus", "HitEvery", 0.3f, 0.1f, 2f, "Seconds between the cactus's hits.");
            follow = Config.Bind("Cactus", "Follow", true, "The cactus follows the wizard for its whole lifetime (0.3.0, Matt). Off: it stays where the dash started.");
            followDistance = Float("Cactus", "FollowDistance", 0.9f, 0f, 3f, "How far behind the wizard (along the way you move) the cactus trails, in units.");
            enhancedLifetime = Float("Cactus Enhanced", "Lifetime", 4f, 0.5f, 10f, "Cactus lifetime when enhanced.");
            enhancedEvery = Float("Cactus Enhanced", "Every", 2f, 0f, 30f, "At most one cactus every this many seconds when enhanced.");

            const string restart = " Applies after restarting the game.";
            hitDamage = Int("Balance", "PrickDamage", 12, 200, "Damage of each of the cactus's hits." + restart);
            hitKnockback = Float("Balance", "PrickKnockback", 10f, 0f, 200f, "Knockback of a hit (small)." + restart);
            cooldown = Float("Balance", "Cooldown", 0.6f, 0.1f, 5f, "Cooldown of the dash itself, in seconds." + restart);
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        private ConfigEntry<int> Int(string section, string key, int value, int max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(0, max)));

        private Sprite LoadIcon(string fileName)
        {
            string resourceName = $"{nameof(WoLPricklyBallet)}.Assets.{fileName}";
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Logger.LogWarning($"Icon resource {resourceName} not found, using the default icon");
                    return null;
                }
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                texture.LoadImage(data);
                return GameIcon.Create(texture, Logger);
            }
        }
    }
}
