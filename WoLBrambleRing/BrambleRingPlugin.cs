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
        public const string PluginVersion = "0.6.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<bool> signatureWithArcana;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.bramblering.cfg) ----
        // [Cast], [Enhanced] and [Signature] apply to the next cast; [Balance] after restarting
        // the game. Their defaults are the values the arcana had before 0.6.0.
        private static ConfigEntry<float> duration, innerRow, outerRow, reachPastRows, prickInterval, vineSpacing, rowDelay;
        private static ConfigEntry<float> enhancedDuration, spreadRow, spreadRowDelay;
        private static ConfigEntry<float> sigDuration, sigInnerRow, sigOuterRow, sigSpreadRow, pullPerTick, pullStopRadius;
        private static ConfigEntry<float> windupTime, eruptionRowDelay, snapDelay;
        private static ConfigEntry<float> windupZoom, windupShake, slamSlowTime, slamSlowScale, slamShake, slamZoom;
        private static ConfigEntry<float> snapSlowTime, snapSlowScale, snapShake, snapZoom;
        private static ConfigEntry<int> slamFreezeFrames, snapFreezeFrames;
        private static ConfigEntry<int> catchDamage, prickDamage, sigCatchDamage, sigPrickDamage, snapDamage;
        private static ConfigEntry<float> cooldown, knockback, hitStun, rootChance, rootDuration, slowChance;

        public static float Duration => Get(duration, 3f, 0.5f, 20f);
        public static float InnerRowRadius => Get(innerRow, 0.8f, 0.3f, 6f);
        public static float OuterRowRadius => Get(outerRow, 1.5f, 0.3f, 8f);
        public static float ReachPastRows => Get(reachPastRows, 0.5f, 0f, 3f);
        public static float PrickInterval => Get(prickInterval, 0.5f, 0.1f, 5f);
        public static float VineSpacing => Get(vineSpacing, 1.4f, 0.5f, 5f);
        public static float RowDelay => Get(rowDelay, 0.12f, 0f, 2f);
        public static float EnhancedDuration => Get(enhancedDuration, 4.5f, 0.5f, 20f);
        public static float SpreadRowRadius => Get(spreadRow, 2.9f, 0.3f, 10f);
        public static float SpreadRowDelay => Get(spreadRowDelay, 0.3f, 0f, 3f);
        public static float SignatureDuration => Get(sigDuration, 6f, 0.5f, 20f);
        public static float SignatureInnerRowRadius => Get(sigInnerRow, 1.4f, 0.3f, 8f);
        public static float SignatureOuterRowRadius => Get(sigOuterRow, 2.4f, 0.3f, 10f);
        public static float SignatureSpreadRowRadius => Get(sigSpreadRow, 3.5f, 0.3f, 12f);
        public static float PullPerTick => Get(pullPerTick, 0.5f, 0f, 3f);
        public static float PullStopRadius => Get(pullStopRadius, 0.5f, 0f, 5f);
        public static float WindupTime => Get(windupTime, 0.25f, 0f, 2f);
        public static float EruptionRowDelay => Get(eruptionRowDelay, 0.08f, 0f, 1f);
        public static float SnapDelay => Get(snapDelay, 0.12f, 0f, 2f);
        public static float WindupZoom => Get(windupZoom, 0.95f, 0.5f, 1f);
        public static float WindupShake => Get(windupShake, 0.3f, 0f, 5f);
        public static int SlamFreezeFrames => Mathf.Clamp(slamFreezeFrames?.Value ?? 6, 0, 30);
        public static float SlamSlowTime => Get(slamSlowTime, 0.4f, 0f, 3f);
        public static float SlamSlowScale => Get(slamSlowScale, 0.35f, 0.05f, 1f);
        public static float SlamShake => Get(slamShake, 1.5f, 0f, 5f);
        public static float SlamZoom => Get(slamZoom, 0.88f, 0.5f, 1f);
        public static int SnapFreezeFrames => Mathf.Clamp(snapFreezeFrames?.Value ?? 7, 0, 30);
        public static float SnapSlowTime => Get(snapSlowTime, 0.25f, 0f, 3f);
        public static float SnapSlowScale => Get(snapSlowScale, 0.4f, 0.05f, 1f);
        public static float SnapShake => Get(snapShake, 1.2f, 0f, 5f);
        public static float SnapZoom => Get(snapZoom, 0.92f, 0.5f, 1f);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

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
            BindSettings();
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
                    damage = new[]
                    {
                        Mathf.Clamp(catchDamage.Value, 0, 100),
                        Mathf.Clamp(prickDamage.Value, 0, 100),
                        Mathf.Clamp(sigCatchDamage.Value, 0, 200),
                        Mathf.Clamp(sigPrickDamage.Value, 0, 200),
                        Mathf.Clamp(snapDamage.Value, 0, 300)
                    },
                    cooldown = new[] { Mathf.Clamp(cooldown.Value, 1f, 60f) },
                    // The thicket holds enemies rather than shoving them.
                    knockbackMultiplier = new[] { Mathf.Clamp(knockback.Value, 0f, 100f) },
                    // The catch and the final snap flinch; the pricks don't, so a snared enemy can
                    // still fight back from inside the thicket.
                    canHitStun = new[] { true, false, true, false, true },
                    hitStunDurationModifier = new[] { Mathf.Clamp(hitStun.Value, 0f, 5f) },
                    // No shake on every prick (the cast shakes once).
                    shakeCamera = new[] { false },
                    // So an enemy crossing several vines at once is only grabbed by one.
                    sameTargetImmunityTime = new[] { 0.5f },
                    // The snare: the game's root status (what Grasping Earth and the vine arcana
                    // use), which holds an enemy in place and ignores knockback for rootDuration.
                    // Every prick renews it, so it lasts a second past the last prick. Bosses and
                    // anything else the game won't let a grab hold aren't snared.
                    rootChance = new[] { Mathf.Clamp(rootChance.Value, 0f, 1f) },
                    rootDuration = new[] { Mathf.Clamp(rootDuration.Value, 0f, 10f) },
                    // On the catch only (levels 1 and 3): the game's slow (level 2 is 50% move
                    // speed for 2 s), so enemies that can't be snared are at least slowed while inside.
                    slowChance = new[] { Mathf.Clamp(slowChance.Value, 0f, 1f), 0f, Mathf.Clamp(slowChance.Value, 0f, 1f), 0f, 0f },
                    slowLevel = new[] { 2 }
                },
                priceMultiplier = 6,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private void BindSettings()
        {
            const string next = " Applies to the next cast.";
            duration = Float("Cast", "Duration", 3f, 0.5f, 20f, "Seconds the thicket stands." + next);
            innerRow = Float("Cast", "InnerRowRadius", 0.8f, 0.3f, 6f, "Distance in units from the centre to the inner row of vines." + next);
            outerRow = Float("Cast", "OuterRowRadius", 1.5f, 0.3f, 8f, "Distance in units from the centre to the outer row of vines." + next);
            reachPastRows = Float("Cast", "ReachPastRows", 0.5f, 0f, 3f,
                "How many units past its widest row the thicket reaches: every enemy within that is pricked and snared " +
                "(all versions)." + next);
            prickInterval = Float("Cast", "PrickInterval", 0.5f, 0.1f, 5f,
                "Seconds between thorn pricks on every enemy inside (all versions; the signature's pull happens on each one too)." + next);
            vineSpacing = Float("Cast", "VineSpacing", 1.4f, 0.5f, 5f,
                "Distance in units between neighbouring vines along a row (smaller: more vines, more vine hit boxes; " +
                "all versions)." + next);
            rowDelay = Float("Cast", "RowDelay", 0.12f, 0f, 2f, "Seconds between the inner row being knitted and the outer row starting." + next);

            enhancedDuration = Float("Enhanced", "Duration", 4.5f, 0.5f, 20f, "Seconds the enhanced thicket stands." + next);
            spreadRow = Float("Enhanced", "SpreadRowRadius", 2.9f, 0.3f, 10f,
                "Distance in units from the centre to the enhanced thicket's third, wider row." + next);
            spreadRowDelay = Float("Enhanced", "SpreadRowDelay", 0.3f, 0f, 3f,
                "Seconds after the outer row is knitted before the third row spreads out." + next);

            sigDuration = Float("Signature", "Duration", 6f, 0.5f, 20f, "Seconds the signature thicket stands before its final snap." + next);
            sigInnerRow = Float("Signature", "InnerRowRadius", 1.4f, 0.3f, 8f, "Distance in units from the centre to the signature's inner row." + next);
            sigOuterRow = Float("Signature", "OuterRowRadius", 2.4f, 0.3f, 10f, "Distance in units from the centre to the signature's middle row." + next);
            sigSpreadRow = Float("Signature", "SpreadRowRadius", 3.5f, 0.3f, 12f, "Distance in units from the centre to the signature's outer row." + next);
            pullPerTick = Float("Signature", "PullPerTick", 0.5f, 0f, 3f,
                "Units each snared enemy is dragged toward the centre at every prick (0: no pull)." + next);
            pullStopRadius = Float("Signature", "PullStopRadius", 0.5f, 0f, 5f,
                "The pull stops once an enemy is this many units from the centre." + next);
            windupTime = Float("Signature", "WindupTime", 0.25f, 0f, 2f, "Seconds the wizard holds the slam raised before it lands." + next);
            eruptionRowDelay = Float("Signature", "EruptionRowDelay", 0.08f, 0f, 1f, "Seconds between each row bursting up, centre outward." + next);
            snapDelay = Float("Signature", "SnapDelay", 0.12f, 0f, 2f,
                "Seconds between the vines whipping shut and the final crush landing." + next);
            windupZoom = Float("Signature", "WindupZoom", 0.95f, 0.5f, 1f,
                "Camera size during the windup, times the game's own (smaller: closer; 1: no zoom)." + next);
            windupShake = Float("Signature", "WindupShake", 0.3f, 0f, 5f, "Screen shake strength of the windup (the game's default shake is 1)." + next);
            slamFreezeFrames = Config.Bind("Signature", "SlamFreezeFrames", 6,
                new ConfigDescription("Frames of hit-stop when the slam lands (0: none)." + next, new AcceptableValueRange<int>(0, 30)));
            slamSlowTime = Float("Signature", "SlamSlowTime", 0.4f, 0f, 3f,
                "Real seconds of slow-down after the slam's hit-stop, easing back to full speed (0: none)." + next);
            slamSlowScale = Float("Signature", "SlamSlowScale", 0.35f, 0.05f, 1f,
                "Game speed the slam's slow-down starts at, as a multiplier (1: no slow-down)." + next);
            slamShake = Float("Signature", "SlamShake", 1.5f, 0f, 5f, "Screen shake strength of the slam (the game's default shake is 1)." + next);
            slamZoom = Float("Signature", "SlamZoom", 0.88f, 0.5f, 1f,
                "Camera size of the slam's punch-in, times the game's own (smaller: closer; 1: no zoom)." + next);
            snapFreezeFrames = Config.Bind("Signature", "SnapFreezeFrames", 7,
                new ConfigDescription("Frames of hit-stop when the final crush lands (0: none)." + next, new AcceptableValueRange<int>(0, 30)));
            snapSlowTime = Float("Signature", "SnapSlowTime", 0.25f, 0f, 3f,
                "Real seconds of slow-down after the final crush's hit-stop (0: none)." + next);
            snapSlowScale = Float("Signature", "SnapSlowScale", 0.4f, 0.05f, 1f,
                "Game speed the final crush's slow-down starts at, as a multiplier (1: no slow-down)." + next);
            snapShake = Float("Signature", "SnapShake", 1.2f, 0f, 5f, "Screen shake strength of the final crush (the game's default shake is 1)." + next);
            snapZoom = Float("Signature", "SnapZoom", 0.92f, 0.5f, 1f,
                "Camera size of the final crush's punch-in, times the game's own (smaller: closer; 1: no zoom)." + next);

            const string restart = " Applies after restarting the game.";
            catchDamage = Config.Bind("Balance", "CatchDamage", 5,
                new ConfigDescription("Damage of the catch: a vine's grab, or the first prick on an enemy inside. It also flinches and slows." + restart,
                    new AcceptableValueRange<int>(0, 100)));
            prickDamage = Config.Bind("Balance", "PrickDamage", 3,
                new ConfigDescription("Damage of every thorn prick after the catch." + restart, new AcceptableValueRange<int>(0, 100)));
            sigCatchDamage = Config.Bind("Balance", "SignatureCatchDamage", 10,
                new ConfigDescription("Damage of the signature thicket's catch." + restart, new AcceptableValueRange<int>(0, 200)));
            sigPrickDamage = Config.Bind("Balance", "SignaturePrickDamage", 6,
                new ConfigDescription("Damage of every signature prick after the catch." + restart, new AcceptableValueRange<int>(0, 200)));
            snapDamage = Config.Bind("Balance", "SignatureSnapDamage", 12,
                new ConfigDescription("Damage of the signature's final crush, once on everything still inside (bosses too)." + restart,
                    new AcceptableValueRange<int>(0, 300)));
            cooldown = Float("Balance", "Cooldown", 6f, 1f, 60f, "Cooldown in seconds." + restart);
            knockback = Float("Balance", "Knockback", 0f, 0f, 100f,
                "Knockback multiplier of the vines' hits (0: they hold rather than shove; snared enemies ignore knockback, and the pricks never shove)." + restart);
            hitStun = Float("Balance", "HitStun", 1f, 0f, 5f,
                "How long the catch and the final crush make an enemy flinch, times the game's normal (the pricks don't flinch)." + restart);
            rootChance = Float("Balance", "SnareChance", 1f, 0f, 1f,
                "Chance from 0 to 1 that a hit snares the enemy (the game's root: held in place, ignoring knockback)." + restart);
            rootDuration = Float("Balance", "SnareDuration", 1f, 0f, 10f,
                "Seconds the snare holds after each hit; every prick renews it, so it lasts this long past the last one." + restart);
            slowChance = Float("Balance", "CatchSlowChance", 1f, 0f, 1f,
                "Chance from 0 to 1 that the catch also slows the enemy (50% move speed for 2 s), so bosses that can't be snared are slowed." + restart);
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

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
