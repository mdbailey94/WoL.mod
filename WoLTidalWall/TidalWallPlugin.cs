using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using LegendAPI;
using UnityEngine;

namespace WoLTidalWall
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class TidalWallPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.tidalwall";
        public const string PluginName = "Tidal Wall";
        public const string PluginVersion = "0.2.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<bool> signatureWithArcana;

        public static void Log(string message) => log?.LogInfo(message);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Tidal Wall in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            signatureWithArcana = Config.Bind("General", "SignatureUnlocksWithArcana", true,
                "Unlock Tidal Wall's signature version along with the arcana itself, so it can go in the " +
                "signature slot as soon as you have it. Off: its Signature drop has to be found in a run, " +
                "like the game's own arcana.");
            // The signature version only shows up in the spellbook's Signature list once it's
            // unlocked; see SignatureUnlock.cs.
            SignatureUnlock.Install(new Harmony(PluginGuid), TidalWallState.staticID,
                () => signatureWithArcana.Value, message => Log("Tidal Wall: " + message));

            Skills.Register(new SkillInfo
            {
                ID = TidalWallState.staticID,
                displayName = "Tidal Wall",
                description = "Raise a wall of water that surges forward, washing enemies away and sometimes freezing them!",
                // The game shows this one text for both the enhanced and the signature version.
                enhancedDescription = "The wave is wider and surges further! Signature: a tidal wave twice as long " +
                    "that freezes all it catches!",
                icon = LoadIcon("icon.png"),
                // Same tier, price and cooldown as Tidal Blast, whose water blasts it's built from.
                tier = 2,
                stateType = typeof(TidalWallState),
                skillStats = new SkillStats
                {
                    ID = new[] { TidalWallState.staticID },
                    elementType = new[] { "Water" },
                    subElementType = new[] { "Water" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Level 1 is each blast in the surging rows, level 2 the bigger crest at the end.
                    // An enemy carried the whole way takes a hit per row: up to 3x8 + 20 = 44
                    // (52 enhanced), against Tidal Blast's 15 + 50 with no freeze. Under Tidal
                    // Blast on purpose: the wave shoves everything it catches the whole way and
                    // can freeze, so the rows are kept light and the crest is the payoff. (0.1.1:
                    // the rows were 10, 50 in all.)
                    // Levels 3 and 4 are the signature wave's rows and crest: 7x12 + 40 = 124 for
                    // an enemy carried all 8 rows (9x12 + 40 = 148 over the enhanced cast's 10),
                    // for the whole gauge. Vanilla signature casts land about there: Tidal Blast's
                    // is 12 blasts of 50-60, Heroic Leap's 40 + 50.
                    damage = new[] { 8, 20, 12, 40 },
                    cooldown = new[] { 6f },
                    // Knockback is always straight along your aim (overwrite), so the wave shoves
                    // everything the same way. Tidal Blast's own blast is 50.
                    knockbackMultiplier = new[] { 30f, 50f, 35f, 60f },
                    knockbackOverwrite = new[] { true },
                    hitStunDurationModifier = new[] { 1.5f },
                    // Water's status in this game is Freeze (Air's is Slow). Plain water arcana
                    // like Tidal Blast don't freeze at all and ice ones always do, so a chance;
                    // the signature wave (levels 3, 4) freezes everything it catches, a little longer.
                    freezeChance = new[] { 0.15f, 0.5f, 1f, 1f },
                    freezeDuration = new[] { 1.5f, 1.5f, 2f, 2f },
                    // Each blast hits once; this only stops one blast hitting the same enemy twice.
                    sameAttackImmunityTime = new[] { 0.25f }
                },
                priceMultiplier = 6,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private Sprite LoadIcon(string fileName)
        {
            string resourceName = $"{nameof(WoLTidalWall)}.Assets.{fileName}";
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
