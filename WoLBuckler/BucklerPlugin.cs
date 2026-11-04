using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLBuckler
{
    // Buckler (#87, Matt 2026-11-03, the GIANT WEAPON set): a new Earth basic arcana, a small
    // stone shield worn for punching. 0.4.0 (Matt: 'hit hold, hit hold, toss disk'): a punch, then
    // the buckler held up in front through a long pause; a second punch and hold; then the buckler
    // tossed like a disc, spinning out and back. Held or flying, it breaks enemy shots (Stone
    // Charge's shot-break; held, only at its face). ENHANCED: the larger, swing-sized buckler (its
    // hits and guard with it) and high knockback, and a shot it breaks bursts into rock shrapnel.
    // A basic can't be a signature, so there is none. Earlier: 0.2.0 at hand height and
    // directional; 0.3.0 the longer pause.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class BucklerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.buckler";
        public const string PluginName = "Buckler";
        public const string PluginVersion = "0.7.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.buckler.cfg) ----
        // [Buckler], [Disc] and [Enhanced] apply the next hit; [Balance] after restarting the game.
        private static ConfigEntry<float> punchReach, punchRadius, guardRadius, holdTime;
        private static ConfigEntry<float> discRange, discSpeed, discReturnSpeed, discRadius;
        private static ConfigEntry<float> enhancedSize, shrapnelRadius;
        private static ConfigEntry<int> punchDamage, discDamage, shrapnelDamage;
        private static ConfigEntry<float> punchKnockback, discKnockback, enhancedPunchKnockback, enhancedDiscKnockback;

        public static float PunchReach => Get(punchReach, 1.0f, 0.4f, 3f);
        public static float PunchRadius => Get(punchRadius, 0.75f, 0.3f, 2f);
        public static float GuardRadius => Get(guardRadius, 0.8f, 0.3f, 2f);
        public static float HoldTime => Get(holdTime, 0.55f, 0.15f, 1.5f);
        public static float DiscRange => Get(discRange, 5.5f, 1f, 12f);
        public static float DiscSpeed => Get(discSpeed, 14f, 4f, 40f);
        public static float DiscReturnSpeed => Get(discReturnSpeed, 16f, 4f, 40f);
        public static float DiscRadius => Get(discRadius, 0.6f, 0.2f, 2f);
        public static float EnhancedSize => Get(enhancedSize, 1.75f, 1f, 3f);
        public static float ShrapnelRadius => Get(shrapnelRadius, 0.8f, 0.3f, 2f);

        private static ConfigEntry<bool> heldArt;
        // #94: Art's 8-facing held buckler and disc spin strips; off = 0.4.0's turned punch frame and spinning buckler.
        public static bool HeldArt => heldArt == null || heldArt.Value;

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            bool hadConfig = File.Exists(Config.ConfigFilePath);
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Buckler in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            BindSettings();
            heldArt = Config.Bind("Look", "DirectionalArt", true,
                "WoL Art's held buckler in 8 facings and the disc's spin strips (#94). Off: the 0.4.0 look (punch frame turned to the aim, spinning buckler).");
            // 0.4.0: the big buckler and high knockback moved to the enhanced version; an older
            // config still holds 0.3's (or 0.1's) numbers.
            var revision = Config.Bind("General", "SettingsRevision", 0, "Which version's defaults this file was last updated to. Leave it alone.");
            if (hadConfig && revision.Value < 2)
            {
                foreach (ConfigEntry<float> entry in new[] { punchReach, punchRadius, guardRadius, punchKnockback })
                    entry.Value = (float)entry.DefaultValue;
                Logger.LogInfo("Buckler: settings moved to 0.4.0's numbers (hit, hold, hit, hold, disc)");
            }
            revision.Value = 2;
            BucklerArt.Load(Logger);

            int punch = Mathf.Clamp(punchDamage.Value, 0, 200), disc = Mathf.Clamp(discDamage.Value, 0, 200);
            Skills.Register(new SkillInfo
            {
                ID = BucklerState.staticID,
                displayName = "Buckler",
                description = "Punch and hold up a stone buckler that breaks any shot it meets head-on, twice, then toss it like a disc!",
                enhancedDescription = "A bigger buckler with heavy knockback, and broken shots burst into rock shrapnel!",
                icon = LoadIcon("buckler_icon.png"),
                tier = 2,
                stateType = typeof(BucklerState),
                skillStats = new SkillStats
                {
                    ID = new[] { BucklerState.staticID },
                    elementType = new[] { "Earth" },
                    subElementType = new[] { "Earth" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Levels: 1 a punch, 2 the disc, 3 the enhanced shrapnel, 4 an enhanced punch
                    // and 5 the enhanced disc (the same damage, high knockback).
                    damage = new[] { punch, disc, Mathf.Clamp(shrapnelDamage.Value, 0, 200), punch, disc },
                    cooldown = new[] { 0f },
                    knockbackMultiplier = new[]
                    {
                        Mathf.Clamp(punchKnockback.Value, 0f, 100f), Mathf.Clamp(discKnockback.Value, 0f, 100f), 5f,
                        Mathf.Clamp(enhancedPunchKnockback.Value, 0f, 100f), Mathf.Clamp(enhancedDiscKnockback.Value, 0f, 100f)
                    },
                    hitStunDurationModifier = new[] { 1f, 1.2f, 0.5f, 1.3f, 1.4f },
                    showDamageNumber = new[] { true, true, true, true, true },
                    sameAttackImmunityTime = new[] { 0.05f },
                    shakeCamera = new[] { false, false, false, true, true }
                },
                priceMultiplier = 3,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private void BindSettings()
        {
            punchReach = Float("Buckler", "PunchReach", 1.0f, 0.4f, 3f, "How far in front of your hands a punch lands, in units.");
            punchRadius = Float("Buckler", "PunchRadius", 0.75f, 0.3f, 2f, "Radius of a punch's hit.");
            guardRadius = Float("Buckler", "GuardRadius", 0.8f, 0.3f, 2f, "How close to the held buckler an enemy shot breaks, in units.");
            holdTime = Float("Buckler", "HoldTime", 0.55f, 0.15f, 1.5f, "Seconds the buckler stays held up (breaking shots) after each punch.");
            discRange = Float("Disc", "Range", 5.5f, 1f, 12f, "How far the tossed buckler flies before it comes back, in units.");
            discSpeed = Float("Disc", "Speed", 14f, 4f, 40f, "How fast it flies out, units a second.");
            discReturnSpeed = Float("Disc", "ReturnSpeed", 16f, 4f, 40f, "How fast it flies back to you, units a second.");
            discRadius = Float("Disc", "Radius", 0.6f, 0.2f, 2f, "How close to the disc an enemy is hit (or a shot breaks), in units.");
            enhancedSize = Float("Enhanced", "SizeMultiplier", 1.75f, 1f, 3f, "Enhanced: the buckler, its hits, guard and disc this much bigger.");
            shrapnelRadius = Float("Enhanced", "ShrapnelRadius", 0.8f, 0.3f, 2f, "Enhanced: radius of the shrapnel burst where a shot breaks.");

            const string restart = " Applies after restarting the game.";
            punchDamage = Int("Balance", "PunchDamage", 7, 200, "Damage of each punch (hits 1 and 2)." + restart);
            discDamage = Int("Balance", "DiscDamage", 10, 200, "Damage of the tossed disc, on the way out and again on the way back." + restart);
            shrapnelDamage = Int("Balance", "ShrapnelDamage", 5, 200, "Damage of the enhanced shrapnel burst." + restart);
            punchKnockback = Float("Balance", "PunchKnockback", 6f, 0f, 100f, "Knockback of a punch." + restart);
            discKnockback = Float("Balance", "DiscKnockback", 12f, 0f, 100f, "Knockback of the disc." + restart);
            enhancedPunchKnockback = Float("Balance", "EnhancedPunchKnockback", 30f, 0f, 100f, "Enhanced: knockback of a punch." + restart);
            enhancedDiscKnockback = Float("Balance", "EnhancedDiscKnockback", 45f, 0f, 100f, "Enhanced: knockback of the disc." + restart);
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        private ConfigEntry<int> Int(string section, string key, int value, int max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(0, max)));

        internal static Texture2D LoadTexture(string fileName)
        {
            string resourceName = $"{nameof(WoLBuckler)}.Assets.{fileName}";
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
