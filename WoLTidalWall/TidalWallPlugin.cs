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
        public const string PluginVersion = "0.4.2";

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
                description = "Raise a wall of water that blocks projectiles, then rolls forward sweeping enemies along until it crashes!",
                // The game shows this one text for both the enhanced and the signature version.
                enhancedDescription = "A wider wall that rolls further. Signature: a towering tidal wave whose crash freezes all it carries!",
                icon = LoadIcon("icon.png"),
                // Tier, price and cooldown of the game's water control arcana (Tidal Blast 6 s,
                // Whirlpool 7 s).
                tier = 2,
                stateType = typeof(TidalWallState),
                skillStats = new SkillStats
                {
                    ID = new[] { TidalWallState.staticID },
                    elementType = new[] { "Water" },
                    subElementType = new[] { "Water" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Level 1 is each push of the rolling wall, level 2 its crash. The wall is
                    // control (it pushes, it blocks projectiles), so it hits lightly: an enemy
                    // carried the whole way takes 3 pushes and the crash, 3x4 + 6 = 18 (4x4 + 6 = 22
                    // enhanced, which rolls further). 0.2.0's rows of Tidal Blast orbs did up to 44
                    // and could freeze; Tidal Blast itself is 15 + 50.
                    // Levels 3 and 4 are the signature wave's pushes and crash: 5x10 + 60 = 110 for
                    // an enemy carried all the way (6x10 + 60 = 120 when also enhanced), for the
                    // whole gauge. Vanilla water signatures do about 110-125 to one enemy (Glacial
                    // Cross 30 + 30 + 50, Tidal Blast's pull and two or three of its 50-60 blasts);
                    // this one also carries and freezes everything it sweeps, so it sits at that,
                    // not above. (0.3.0: crash 70, 130; 0.2.0: 124 / 148.)
                    damage = new[] { 4, 6, 10, 60 },
                    cooldown = new[] { 6f },
                    // Knockback is always straight along your aim (overwrite), so the wall shoves
                    // everything the same way; the pushes are light so enemies stay in front of it
                    // and get carried, the crash throws them. Tidal Blast's blast is 50, the
                    // ice wave's (SummonIceWave) 25.
                    knockbackMultiplier = new[] { 20f, 40f, 30f, 60f },
                    knockbackOverwrite = new[] { true },
                    hitStunDurationModifier = new[] { 1.5f, 1.5f, 2f, 2f },
                    // Water's status in this game is Freeze (Air's is Slow). The wall doesn't
                    // freeze at all (it's already control); the signature's crash freezes
                    // everything it carried (level 4). Its pushes don't, so the frozen don't stop
                    // being carried.
                    freezeChance = new[] { 0f, 0f, 0f, 1f },
                    freezeDuration = new[] { 1.5f, 1.5f, 1.5f, 2.5f },
                    // One hit per push: the wall's hit area is switched off and on every 0.35 s
                    // (0.25 s for the signature), and this stops it hitting again in between as
                    // enemies get shoved out and caught up with.
                    sameAttackImmunityTime = new[] { 0.3f, 0.3f, 0.2f, 0.3f }
                },
                priceMultiplier = 6,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        // Last line of defence for the signature's camera and light changes (Cinematic.cs): put
        // them back if a cast somehow never did.
        private void Update() => Cinematic.Watchdog();

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
