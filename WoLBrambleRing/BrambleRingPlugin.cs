using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
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
        public const string PluginVersion = "0.4.2";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<bool> signatureWithArcana;

        public static void Log(string message) => log?.LogInfo(message);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Bramble Ring in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            signatureWithArcana = Config.Bind("General", "SignatureUnlocksWithArcana", true,
                "Unlock Bramble Ring's signature version along with the arcana itself, so it can go in the " +
                "signature slot as soon as you have it. Off: its Signature drop has to be found in a run, " +
                "like the game's own arcana.");
            // The signature version only shows up in the spellbook's Signature list once it's
            // unlocked; see SignatureUnlock.cs.
            SignatureUnlock.Install(new Harmony(PluginGuid), BrambleRingState.staticID,
                () => signatureWithArcana.Value, message => Log("Bramble Ring: " + message));
            // The arcana (BrambleRingState) logs each cast, each enemy caught and the thicket's
            // end, so a run can be checked from BepInEx\LogOutput.log.

            // The signature's camera, time and light beats run from their own object; made now so
            // it counts level loads from the start.
            try
            {
                BrambleCinematics instance = BrambleCinematics.Instance;
            }
            catch (System.Exception e)
            {
                Logger.LogWarning($"Bramble Ring: signature cinematics unavailable: {e.Message}");
            }

            Skills.Register(new SkillInfo
            {
                ID = BrambleRingState.staticID,
                displayName = "Bramble Ring",
                description = "Knit a thicket of thorny vines around you that snares the enemies inside and pricks them while it stands!",
                // The game shows this one text for both the enhanced and the signature version.
                enhancedDescription = "The thicket stands longer and spreads wider! Signature: it erupts, drags foes inward, then crushes them!",
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
                    // An enemy that stays the whole 3 s takes the catch and 5 pricks (20);
                    // enhanced, the catch and 8 (29). Kept low on purpose: the thicket is a hard
                    // snare for its whole stand (and a second past it), so it sits at the low end
                    // of its tier for damage. Embracing Vines does 25 a wave with a 2.5 s root;
                    // Terra Ring 15 a ring, cascading, with no hold. (0.2.1: pricks were 5, 30 a
                    // full stand.)
                    // Levels 3 and 4 are the signature thicket's catch and pricks: the catch and
                    // 11 pricks over its 6 s, 76 for an enemy held the whole way, for the whole
                    // gauge. Vanilla signature casts land higher in a burst (Heroic Leap's 40 + 50,
                    // Tidal Blast's 12 blasts of 50-60); this one is spread over a 6 s hard snare
                    // that also drags everything to the centre, so it stays under them.
                    // Level 5 is the signature's final snap: one crushing hit of 12 on everything
                    // still inside as the thicket closes (bosses too), the payoff for holding them
                    // there; 88 in all for an enemy held the whole way. (0.4.0: new.)
                    damage = new[] { 5, 3, 10, 6, 12 },
                    cooldown = new[] { 6f },
                    // The thicket holds enemies rather than shoving them.
                    knockbackMultiplier = new[] { 0f },
                    // The catch and the final snap flinch; the pricks don't, so a snared enemy can
                    // still fight back from inside the thicket.
                    canHitStun = new[] { true, false, true, false, true },
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
                    // On the catch only (levels 1 and 3): the game's slow (level 2 is 50% move
                    // speed for 2 s), so enemies that can't be snared are at least slowed while inside.
                    slowChance = new[] { 1f, 0f, 1f, 0f, 0f },
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
