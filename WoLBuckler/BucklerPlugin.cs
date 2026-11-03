using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLBuckler
{
    // Buckler (#87, Matt 2026-11-03, the GIANT WEAPON set): a new Earth basic arcana, a small
    // stone shield worn for punching. Three fast, short punches, no charge; the buckler breaks any
    // enemy shot it meets while it's out (Stone Charge's shot-break). Enhanced: a shot it breaks
    // bursts into rock shrapnel. A basic can't be a signature, so there is none.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class BucklerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.buckler";
        public const string PluginName = "Buckler";
        public const string PluginVersion = "0.1.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.buckler.cfg) ----
        // [Buckler] and [Enhanced] apply the next punch; [Balance] after restarting the game.
        private static ConfigEntry<float> punchReach, punchRadius, finisherRadius, guardRadius, guardTime, shrapnelRadius;
        private static ConfigEntry<int> punchDamage, finisherDamage, shrapnelDamage;
        private static ConfigEntry<float> punchKnockback, finisherKnockback;

        public static float PunchReach => Get(punchReach, 1.0f, 0.4f, 3f);
        public static float PunchRadius => Get(punchRadius, 0.75f, 0.3f, 2f);
        public static float FinisherRadius => Get(finisherRadius, 1.0f, 0.3f, 3f);
        public static float GuardRadius => Get(guardRadius, 0.8f, 0.3f, 2f);
        public static float GuardTime => Get(guardTime, 0.3f, 0.1f, 1f);
        public static float ShrapnelRadius => Get(shrapnelRadius, 0.8f, 0.3f, 2f);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Buckler in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            BindSettings();
            BucklerArt.Load(Logger);

            Skills.Register(new SkillInfo
            {
                ID = BucklerState.staticID,
                displayName = "Buckler",
                description = "Punch with a stone buckler: three quick blows, and any shot it meets breaks on the stone!",
                enhancedDescription = "Shots the buckler breaks burst into rock shrapnel!",
                icon = LoadIcon("buckler_icon.png"),
                tier = 2,
                stateType = typeof(BucklerState),
                skillStats = new SkillStats
                {
                    ID = new[] { BucklerState.staticID },
                    elementType = new[] { "Earth" },
                    subElementType = new[] { "Earth" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Levels: 1 a punch (hits 1 and 2), 2 the last punch, 3 the enhanced shrapnel.
                    damage = new[]
                    {
                        Mathf.Clamp(punchDamage.Value, 0, 200), Mathf.Clamp(finisherDamage.Value, 0, 200), Mathf.Clamp(shrapnelDamage.Value, 0, 200)
                    },
                    cooldown = new[] { 0f },
                    knockbackMultiplier = new[] { Mathf.Clamp(punchKnockback.Value, 0f, 100f), Mathf.Clamp(finisherKnockback.Value, 0f, 100f), 5f },
                    hitStunDurationModifier = new[] { 0.8f, 1.2f, 0.5f },
                    showDamageNumber = new[] { true, true, true },
                    sameAttackImmunityTime = new[] { 0.05f },
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
            punchReach = Float("Buckler", "PunchReach", 1.0f, 0.4f, 3f, "How far in front of you a punch lands, in units.");
            punchRadius = Float("Buckler", "PunchRadius", 0.75f, 0.3f, 2f, "Radius of a punch's hit (hits 1 and 2).");
            finisherRadius = Float("Buckler", "FinisherRadius", 1.0f, 0.3f, 3f, "Radius of the last punch's hit.");
            guardRadius = Float("Buckler", "GuardRadius", 0.8f, 0.3f, 2f, "How close to the buckler an enemy shot breaks, in units.");
            guardTime = Float("Buckler", "GuardTime", 0.3f, 0.1f, 1f, "Seconds the buckler stays out (and breaks shots) per punch.");
            shrapnelRadius = Float("Enhanced", "ShrapnelRadius", 0.8f, 0.3f, 2f, "Enhanced: radius of the shrapnel burst where a shot breaks.");

            const string restart = " Applies after restarting the game.";
            punchDamage = Int("Balance", "PunchDamage", 7, 200, "Damage of each punch (hits 1 and 2)." + restart);
            finisherDamage = Int("Balance", "FinisherDamage", 11, 200, "Damage of the last punch." + restart);
            shrapnelDamage = Int("Balance", "ShrapnelDamage", 5, 200, "Damage of the enhanced shrapnel burst." + restart);
            punchKnockback = Float("Balance", "PunchKnockback", 5f, 0f, 100f, "Knockback of a punch (small, so the next one still lands)." + restart);
            finisherKnockback = Float("Balance", "FinisherKnockback", 20f, 0f, 100f, "Knockback of the last punch." + restart);
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
