using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLGiantWeapons
{
    // The giant weapon set's basics built by WoL Modder (Matt, 2026-11-03): Giant Ice Sword (#83,
    // Water) and Giant Lightning Flail (#84, Lightning). Slow, heavy 3-hit combos. WoL Player's
    // Giant Air Spear and Buckler are their own mods (WoLGiantSpear, WoLBuckler). The hits are the
    // game's own elemental bursts with each arcana's stats; WoL Art's sprites are hooked when
    // drawn. A basic can't be a signature (the spellbook files every basic under the basic slot
    // before it looks for a signature), so neither has one.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class GiantWeaponsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.giantweapons";
        public const string PluginName = "Giant Weapons";
        public const string PluginVersion = "0.5.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> swordEnabled, flailEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.giantweapons.cfg) ----
        // [Ice Sword] and [Flail] apply the next attack; [Balance] after restarting the game.
        private static ConfigEntry<float> swordPace, swingReach, swingArc, chopReach, spinMin, spinFull, spinRadius, fullSpinRadius,
            spinTime, enhancedSpinWidth, frostRingTime;
        private static ConfigEntry<int> spinHits;
        private static ConfigEntry<float> swordScale, swordSwingSpeed, swingTurnTime, swordAngleOffset;
        private static ConfigEntry<float> flailPace, circleTime, blurAlpha, circleRadius, slamDistance, slamRadius, slamWindUp, arcReach;
        private static ConfigEntry<int> arcTargets, circleSlices;
        private static ConfigEntry<int> swingDamage, chopDamage, spinDamage, fullSpinDamage, circleDamage, slamDamage, arcDamage;

        public static float SwordPace => Get(swordPace, 0.65f, 0.3f, 1f);
        public static float SwingReach => Get(swingReach, 2.2f, 0.5f, 6f);
        public static float SwingArc => Get(swingArc, 45f, 10f, 360f);
        public static float ChopReach => Get(chopReach, 2.5f, 0.5f, 6f);
        public static float SpinMinCharge => Get(spinMin, 0.4f, 0f, 3f);
        public static float SpinFullCharge => Get(spinFull, 1f, 0.1f, 4f);
        public static float SpinRadius => Get(spinRadius, 2.5f, 0.5f, 8f);
        public static float FullSpinRadius => Get(fullSpinRadius, 3f, 0.5f, 8f);
        public static int SpinHits => Mathf.Clamp(spinHits?.Value ?? 4, 1, 12);
        public static float SpinTime => Get(spinTime, 0.5f, 0.1f, 3f);
        public static float EnhancedSpinWidth => Get(enhancedSpinWidth, 1.3f, 1f, 3f);
        public static float FrostRingTime => Get(frostRingTime, 1.5f, 0.2f, 6f);
        public static float SwordScale => Get(swordScale, 0.7f, 0.2f, 2f);
        public static float SwordSwingSpeed => Get(swordSwingSpeed, 1f, 0.2f, 4f);
        public static float SwingTurnTime => Get(swingTurnTime, 0.22f, 0.05f, 1f);
        public static float SwordAngleOffset => Get(swordAngleOffset, 0f, -180f, 180f);
        public static float FlailPace => Get(flailPace, 0.63f, 0.3f, 1f);
        public static float CircleTime => Get(circleTime, 0.45f, 0.15f, 2f);
        public static float FlailBlurAlpha => Get(blurAlpha, 0.45f, 0f, 1f);
        public static int CircleSlices => Mathf.Clamp(circleSlices?.Value ?? 4, 1, 12);
        public static float CircleRadius => Get(circleRadius, 2f, 0.5f, 6f);
        public static float SlamDistance => Get(slamDistance, 2.5f, 0.5f, 6f);
        public static float SlamRadius => Get(slamRadius, 1.5f, 0.3f, 5f);
        public static float SlamWindUp => Get(slamWindUp, 0.4f, 0f, 1.5f);
        public static float ArcReach => Get(arcReach, 4f, 0.5f, 10f);
        public static int ArcTargets => Mathf.Clamp(arcTargets?.Value ?? 3, 0, 12);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            swordEnabled = Config.Bind("General", "IceSwordEnabled", true,
                "Offer the Giant Ice Sword in the arcana shop. Turning it off doesn't remove it from a run where you already have it.");
            flailEnabled = Config.Bind("General", "FlailEnabled", true,
                "Offer the Giant Lightning Flail in the arcana shop. Turning it off doesn't remove it from a run where you already have it.");
            BindSettings();

            Skills.Register(new SkillInfo
            {
                ID = IceSwordState.staticID,
                displayName = "Giant Ice Sword",
                description = "Heave a huge frozen greatsword: a swing, a swing back, then a full spin! Hold before the spin to charge it!",
                enhancedDescription = "A wider spin that leaves a ring of frost!",
                icon = Icon("giant_ice_sword_icon.png"),
                tier = 2,
                stateType = typeof(IceSwordState),
                skillStats = new SkillStats
                {
                    ID = new[] { IceSwordState.staticID },
                    elementType = new[] { "Water" },
                    subElementType = new[] { "Water" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Levels: 1 a swing, 2 a tapped spin, 3 a charged spin, 4 a full-charge spin,
                    // 5 the enhanced frost ring (no damage, slows).
                    damage = new[] { Clamp(swingDamage, 0, 200), Clamp(chopDamage, 0, 300), Clamp(spinDamage, 0, 200), Clamp(fullSpinDamage, 0, 200), 0 },
                    cooldown = new[] { 0f },
                    knockbackMultiplier = new[] { 6f, 14f, 18f, 24f, 0f },
                    hitStunDurationModifier = new[] { 1f, 1.4f, 0.8f, 1f, 0f },
                    showDamageNumber = new[] { true, true, true, true, false },
                    slowChance = new[] { 0.2f, 0f, 0f, 0f, 1f },
                    slowLevel = new[] { 1 },
                    freezeChance = new[] { 0f, 0.3f, 0.4f, 0.5f, 0f },
                    sameAttackImmunityTime = new[] { 0.1f },
                    shakeCamera = new[] { false, false, true, true, false }
                },
                priceMultiplier = 3,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => swordEnabled.Value
            });

            Skills.Register(new SkillInfo
            {
                ID = FlailState.staticID,
                displayName = "Giant Lightning Flail",
                description = "Whirl a huge crackling flail round you, then slam it down in front with a thunderous crash!",
                enhancedDescription = "The slam arcs lightning to nearby foes!",
                icon = Icon("flail_icon.png"),
                tier = 2,
                stateType = typeof(FlailState),
                skillStats = new SkillStats
                {
                    ID = new[] { FlailState.staticID },
                    elementType = new[] { "Lightning" },
                    subElementType = new[] { "Lightning" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Levels: 1 a circle swing, 2 the slam, 3 an enhanced arc.
                    damage = new[] { Clamp(circleDamage, 0, 200), Clamp(slamDamage, 0, 300), Clamp(arcDamage, 0, 200) },
                    cooldown = new[] { 0f },
                    knockbackMultiplier = new[] { 8f, 30f, 4f },
                    hitStunDurationModifier = new[] { 1f, 1.5f, 0.8f },
                    showDamageNumber = new[] { true, true, true },
                    shockChance = new[] { 0.25f, 1f, 0.5f },
                    shockLevel = new[] { 1 },
                    sameAttackImmunityTime = new[] { 0.1f },
                    shakeCamera = new[] { false, true, false }
                },
                priceMultiplier = 3,
                unlockCondition = () => flailEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered (Giant Ice Sword, Giant Lightning Flail)");
        }

        private static int Clamp(ConfigEntry<int> entry, int min, int max) => Mathf.Clamp(entry.Value, min, max);

        // WoL Art's 18x18 arcana icons, sized like the game's own in the shop (GameIcon).
        private Sprite Icon(string file)
        {
            Texture2D tex = FlailLook.Load(file);
            if (tex == null)
            {
                Logger.LogWarning($"Icon {file} not found, using the default icon");
                return null;
            }
            return GameIcon.Create(tex, Logger);
        }

        private void BindSettings()
        {
            swordPace = Float("Ice Sword", "Pace", 0.65f, 0.3f, 1f, "How fast the wizard's swings play (0.65 = 35% slower, the heavy feel; 1 = normal).");
            swingReach = Float("Ice Sword", "SwingReach", 2.2f, 0.5f, 6f, "Reach of swings 1 and 2, in units.");
            swingArc = Float("Ice Sword", "SwingAngle", 45f, 10f, 360f, "How far swings 1 and 2 turn, in degrees; the hit box is the same angle in front (0.4.0, #93; the old SwingArc key is unused).");
            chopReach = Float("Ice Sword", "ChopReach", 2.5f, 0.5f, 6f, "Unused since 0.4.0 (the third hit is always a spin).");
            spinMin = Float("Ice Sword", "SpinMinCharge", 0.4f, 0f, 3f, "Seconds held before the third hit's spin counts as charged (shorter = a tapped spin).");
            spinFull = Float("Ice Sword", "SpinFullCharge", 1f, 0.1f, 4f, "Seconds held for a full-charge spin.");
            spinRadius = Float("Ice Sword", "SpinRadius", 2.5f, 0.5f, 8f, "Radius of a tapped spin (and a charged one at the least charge).");
            fullSpinRadius = Float("Ice Sword", "FullSpinRadius", 3f, 0.5f, 8f, "Radius of a full-charge spin.");
            spinHits = Config.Bind("Ice Sword", "SpinSlices", 4, new ConfigDescription("The spin's one turn is cut into this many slices; an enemy is hit once, as the blade passes its slice (0.4.0).", new AcceptableValueRange<int>(1, 12)));
            spinTime = Float("Ice Sword", "SpinTurnTime", 0.5f, 0.1f, 3f, "Seconds for the spin's one 360-degree turn (0.4.0).");
            enhancedSpinWidth = Float("Ice Sword", "EnhancedSpinWidth", 1.3f, 1f, 3f, "How much wider the spin is when enhanced.");
            frostRingTime = Float("Ice Sword", "FrostRingTime", 1.5f, 0.2f, 6f, "Seconds the enhanced frost ring stays on the floor, slowing enemies.");
            swordScale = Float("Ice Sword", "SwordScale", 0.7f, 0.2f, 2f, "Size of the drawn sword (the game's ice boss greatsword; 1 = the boss's own size). Looks only; the hits are the reaches above.");
            swordSwingSpeed = Float("Ice Sword", "SwordSwingSpeed", 1f, 0.2f, 4f, "Unused since 0.4.0 (the blade is turned by the mod).");
            swingTurnTime = Float("Ice Sword", "SwingTurnTime", 0.22f, 0.05f, 1f, "Seconds the drawn blade takes to turn through a 45-degree swing.");
            swordAngleOffset = Float("Ice Sword", "SwordAngleOffset", 0f, -180f, 180f, "Turns the drawn blade's starting angle, if it doesn't line up with the aim (looks only).");

            flailPace = Float("Flail", "SwingPace", 0.63f, 0.3f, 1f, "How fast the wizard's swings play (0.63 = 37% slower; 1 = normal). 0.5.0 (#92): a new key, 10% slower than the old Pace 0.7, which is unused now.");
            circleTime = Float("Flail", "CircleTime", 0.45f, 0.15f, 2f, "Seconds for the ball to go once round on hits 1-2 (was 0.35).");
            circleSlices = Config.Bind("Flail", "CircleSlices", 4, new ConfigDescription("The circle is cut into this many slices; an enemy is hit once, as the ball passes its slice.", new AcceptableValueRange<int>(1, 12)));
            blurAlpha = Float("Flail", "BlurAlpha", 0.45f, 0f, 1f, "How strongly the motion-blur ring shows behind the ball (0 = only the ball and chain).");
            circleRadius = Float("Flail", "CircleRadius", 2f, 0.5f, 6f, "Radius round the wizard of hits 1 and 2 (the circle swings).");
            slamDistance = Float("Flail", "SlamDistance", 2.5f, 0.5f, 6f, "How far ahead the slam lands, in units.");
            slamRadius = Float("Flail", "SlamRadius", 1.5f, 0.3f, 5f, "Radius of the slam.");
            slamWindUp = Float("Flail", "SlamWindUp", 0.4f, 0f, 1.5f, "Seconds from the third press to the slam.");
            arcReach = Float("Flail", "ArcReach", 4f, 0.5f, 10f, "How far the enhanced slam's lightning arcs reach.");
            arcTargets = Config.Bind("Flail", "ArcTargets", 3, new ConfigDescription("Enemies the enhanced slam arcs to.", new AcceptableValueRange<int>(0, 12)));

            const string restart = " Applies after restarting the game.";
            swingDamage = Int("Balance", "SwordSwingDamage", 12, "Ice Sword: damage of swings 1 and 2." + restart);
            chopDamage = Int("Balance", "SwordTapSpinDamage", 18, "Ice Sword: damage of a tapped spin (hit 3)." + restart);
            spinDamage = Int("Balance", "SwordChargedSpinDamage", 22, "Ice Sword: damage of a charged spin." + restart);
            fullSpinDamage = Int("Balance", "SwordFullSpin360Damage", 28, "Ice Sword: damage of a full-charge spin." + restart);
            circleDamage = Int("Balance", "FlailCircleDamage", 10, "Flail: damage of the circle swings." + restart);
            slamDamage = Int("Balance", "FlailSlamDamage", 20, "Flail: damage of the slam." + restart);
            arcDamage = Int("Balance", "FlailArcDamage", 6, "Flail: damage of each enhanced arc." + restart);
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        private ConfigEntry<int> Int(string section, string key, int value, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(0, 300)));
    }
}
