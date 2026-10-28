using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLCyclone
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class CyclonePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.cyclone";
        public const string PluginName = "Cyclone";
        public const string PluginVersion = "0.5.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;
        private static ConfigEntry<string> twister;
        public static bool DrawnTwister => twister == null || twister.Value != "Coded";
        private ConfigEntry<bool> signatureWithArcana;
        private static ConfigEntry<float> signatureWidth, tossHeight;
        private static ConfigEntry<int> tossDamage;
        public static float SignatureWidth => Mathf.Clamp(signatureWidth?.Value ?? 1.8f, 1f, 3f);
        public static float TossHeight => Mathf.Clamp(tossHeight?.Value ?? 2.5f, 0.5f, 6f);
        public static int TossDamage => Mathf.Clamp(tossDamage?.Value ?? 12, 0, 200);

        public static void Log(string message) => log?.LogInfo(message);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Cyclone in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");

            twister = Config.Bind("Look", "Twister", "Drawn",
                new ConfigDescription("Drawn: the hand-drawn twister (three sizes, six frames each, scaled to the funnel's width). Coded: the funnel drawn in code (0.4.3's spiral column). Applies to the next cast.",
                    new AcceptableValueList<string>("Drawn", "Coded")));
            signatureWithArcana = Config.Bind("General", "SignatureUnlocksWithArcana", true,
                "Unlock Cyclone's signature version along with the arcana itself, so it can go in the signature slot as soon as " +
                "you have it. Off: its Signature drop has to be found in a run, as for the game's own arcana.");
            signatureWidth = Config.Bind("Signature", "Width", 1.8f,
                new ConfigDescription("The signature twister's hit area and look, times the enhanced one's. Applies to the next cast.", new AcceptableValueRange<float>(1f, 3f)));
            tossHeight = Config.Bind("Signature", "TossHeight", 2.5f,
                new ConfigDescription("How high the signature throws enemies (units). Applies to the next cast.", new AcceptableValueRange<float>(0.5f, 6f)));
            tossDamage = Config.Bind("Signature", "TossDamage", 12,
                new ConfigDescription("Damage a thrown enemy takes when it lands. Applies to the next cast.", new AcceptableValueRange<int>(0, 200)));
            SignatureUnlock.Install(new HarmonyLib.Harmony(PluginGuid), CycloneState.staticID,
                () => signatureWithArcana.Value, message => Log("Cyclone: " + message));

            Skills.Register(new SkillInfo
            {
                ID = CycloneState.staticID,
                displayName = "Cyclone",
                description = "Hold to whip up a twister you can steer, growing into a raging hurricane that bursts, blowing everyone away!",
                // The game shows this one text on the card for both the enhanced and the signature.
                enhancedDescription = "Charges faster, steers easier and whirls leaves! Signature: a huge twister that throws enemies into the air!",
                icon = LoadIcon("icon.png"),
                tier = 2,
                stateType = typeof(CycloneState),
                skillStats = new SkillStats
                {
                    ID = new[] { CycloneState.staticID },
                    elementType = new[] { "Air" },
                    subElementType = new[] { "Air" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Levels 1-4 are the twister's hits as it grows over 4 s (stronger and more
                    // often each stage), level 5 the final burst after a second at full power, level
                    // 6 the game's twister and storm used only for the look (harmless). Held to the
                    // burst: roughly 130 to an enemy caught the whole time.
                    damage = new[] { 2, 3, 4, 6, 14, 0 },
                    cooldown = new[] { 5f },
                    // The tornado tugs enemies gently in to keep them inside; the burst throws hard.
                    knockbackMultiplier = new[] { -6f, -6f, -7f, -8f, 70f, 0f },
                    hitStunDurationModifier = new[] { 1f, 1f, 1f, 1f, 1.4f, 0f },
                    showDamageNumber = new[] { true, true, true, true, true, false },
                    // Shorter than the fastest hit rate, so every hit can land.
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
            string resourceName = $"{nameof(WoLCyclone)}.Assets.{fileName}";
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
