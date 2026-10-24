using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLBrambleRing
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class BrambleRingPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.bramblering";
        public const string PluginName = "Bramble Ring";
        public const string PluginVersion = "0.2.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Bramble Ring in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            // The arcana (BrambleRingState) logs each cast, each enemy caught and the thicket's
            // end, so a run can be checked from BepInEx\LogOutput.log.

            Skills.Register(new SkillInfo
            {
                ID = BrambleRingState.staticID,
                displayName = "Bramble Ring",
                description = "Knit a thicket of thorny vines around you that snares the enemies inside and pricks them while it stands!",
                enhancedDescription = "The thicket stands longer and spreads outward into a wider ring!",
                icon = LoadIcon("icon.png"),
                // Same tier and price as Rippling Vines and Terra Ring, the vanilla Earth arcana
                // it's closest to.
                tier = 4,
                stateType = typeof(BrambleRingState),
                skillStats = new SkillStats
                {
                    ID = new[] { BrambleRingState.staticID },
                    elementType = new[] { "Earth" },
                    subElementType = new[] { "Earth" },
                    // Like the game's vines: the hit boxes catch enemies' feet (and crates, pots...).
                    targetNames = new[] { "EnemyFloorContact", "DestructibleHurtBox" },
                    // Level 1 is the catch: a vine's grab, or the first prick on an enemy inside
                    // the thicket. Level 2 is every thorn prick after it, one every half second.
                    // An enemy that stays the whole 3 s takes 6 pricks (30); enhanced, 9 (45).
                    // Rippling Vines does 20 a vine with its grab; Terra Ring 15 a ring, cascading.
                    damage = new[] { 5, 5 },
                    cooldown = new[] { 6f },
                    // The thicket holds enemies rather than shoving them.
                    knockbackMultiplier = new[] { 0f },
                    // The catch flinches; the pricks after it don't, so a snared enemy can still
                    // fight back from inside the thicket.
                    canHitStun = new[] { true, false },
                    hitStunDurationModifier = new[] { 1f },
                    // No shake on every prick (the cast shakes once).
                    shakeCamera = new[] { false },
                    // So an enemy crossing several vines at once is only grabbed by one.
                    sameTargetImmunityTime = new[] { 0.5f },
                    // The snare: the game's root status (what Grasping Earth and the vine arcana
                    // use), which holds an enemy in place and ignores knockback for rootDuration.
                    // Every prick renews it, so it lasts a second past the last prick. Bosses and
                    // anything else the game won't let a grab hold aren't snared.
                    rootChance = new[] { 1f },
                    rootDuration = new[] { 1f },
                    // On the catch only: the game's slow (level 2 is 50% move speed for 2 s), so
                    // enemies that can't be snared are at least slowed while inside.
                    slowChance = new[] { 1f, 0f },
                    slowLevel = new[] { 2 }
                },
                priceMultiplier = 6,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private Sprite LoadIcon(string fileName)
        {
            string resourceName = $"{nameof(WoLBrambleRing)}.Assets.{fileName}";
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
