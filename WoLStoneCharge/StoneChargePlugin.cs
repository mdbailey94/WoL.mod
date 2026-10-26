using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLStoneCharge
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class StoneChargePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.stonecharge";
        public const string PluginName = "Stone Charge";
        public const string PluginVersion = "0.4.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.stonecharge.cfg) ----
        // [Charge] and [Crash] apply the next time you cast it; [Balance] after restarting the game.
        private static ConfigEntry<float> chargeTime, minPressTime, energyPerSecond, enhancedEnergyBonus, sprintDelay, frontArmour;
        private static ConfigEntry<bool> brace, drawnEffects;
        private static ConfigEntry<float> rushDistance, rushTime, bashRadius, crashRadius, enhancedCrashRadius;
        private static ConfigEntry<float> fullMultiplier, knockback, fullKnockback, stunEnergy, stunMultiplier;
        private static ConfigEntry<int> raiseDamage, bashDamage, crashDamage;
        private static ConfigEntry<float> raiseKnockback, cooldown;

        // How long the shield stays up before it crashes by itself.
        public static float ChargeTime => Get(chargeTime, 4f, 1f, 10f);
        // The cast's own press mustn't count as the "press again": ignore the button this long.
        public static float MinPressTime => Get(minPressTime, 0.2f, 0f, 2f);
        // Percent of full force stored per second of running at base run speed.
        public static float EnergyPerSecond => Get(energyPerSecond, 25f, 5f, 100f);
        public static float EnhancedEnergyBonus => Get(enhancedEnergyBonus, 35f, 0f, 200f);
        public static float SprintDelay => Get(sprintDelay, 1f, 0f, 5f);
        public static float FrontArmourPercent => Get(frontArmour, 30f, 0f, 90f);
        public static bool Brace => brace?.Value ?? true;
        public static bool DrawnEffects => drawnEffects?.Value ?? true;
        public static float RushDistance => Get(rushDistance, 2.5f, 0.5f, 6f);
        public static float RushTime => Get(rushTime, 0.12f, 0.05f, 0.5f);
        public static float BashRadius => Get(bashRadius, 1.2f, 0.3f, 3f);
        public static float CrashRadius => Get(crashRadius, 2f, 0.5f, 5f);
        public static float EnhancedCrashRadius => Get(enhancedCrashRadius, 2.5f, 0.5f, 5f);
        public static float FullMultiplier => Get(fullMultiplier, 6f, 1f, 15f);
        public static float Knockback => Get(knockback, 10f, 0f, 200f);
        public static float FullKnockback => Get(fullKnockback, 70f, 0f, 300f);
        public static float StunEnergyPercent => Get(stunEnergy, 75f, 0f, 100f);
        public static float StunMultiplier => Get(stunMultiplier, 2.5f, 1f, 6f);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Stone Charge in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            BindSettings();

            Skills.Register(new SkillInfo
            {
                ID = StoneChargeState.staticID,
                displayName = "Stone Charge",
                description = "Raise a stone bulwark and keep running: the faster you move, the more force it stores, " +
                    "and dashing keeps it up. Run or dash into a foe or press again to crash forward and unload all of it!",
                enhancedDescription = "Stores force faster and crashes wider!",
                icon = LoadIcon("icon.png"),
                // Same tier and price as Aegis Charge and Terra Ring, the vanilla Earth arcana it's
                // closest to (Aegis Charge: a shielded rush, 5.75 s cooldown, price 6).
                tier = 4,
                stateType = typeof(StoneChargeState),
                skillStats = new SkillStats
                {
                    ID = new[] { StoneChargeState.staticID },
                    elementType = new[] { "Earth" },
                    subElementType = new[] { "Earth" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Level 1 is the shield forming (a shove, no force behind it yet). Level 2 is
                    // the shield bash during the rush and level 3 the crash at its end: these are
                    // the base numbers at no stored force, and StoneChargeState scales them by the
                    // force as each hit is made, up to FullMultiplier (6x) at full. An enemy caught
                    // by both takes 6 at no force and 36 at full; one standing right in front of
                    // you when you cast takes the shove (3) as well. Full force needs 4 s of
                    // running at base speed (less the faster you are), and the crash is telegraphed
                    // by a 2.5 u rush, so the big number has a real cost. All from [Balance].
                    damage = new[]
                    {
                        Mathf.Clamp(raiseDamage.Value, 0, 100),
                        Mathf.Clamp(bashDamage.Value, 0, 100),
                        Mathf.Clamp(crashDamage.Value, 0, 200)
                    },
                    cooldown = new[] { Mathf.Clamp(cooldown.Value, 1f, 60f) },
                    // Every hit shoves along the way you're facing (knockbackOverwrite), not away
                    // from the burst's centre. The bash and crash knockback is set by the force as
                    // each hit is made ([Crash] Knockback -> FullKnockback); these are placeholders.
                    knockbackMultiplier = new[] { Mathf.Clamp(raiseKnockback.Value, 0f, 100f), Knockback, Knockback },
                    knockbackOverwrite = new[] { true, true, true },
                    // The crash stuns longer at high force (StunMultiplier), set as the hit is made.
                    hitStunDurationModifier = new[] { 1f, 1f, 1f },
                    // Two bash bursts overlap during the rush: one bash per enemy.
                    sameTargetImmunityTime = new[] { 0.5f, 0.5f, 0.5f },
                    shakeCamera = new[] { false, false, true }
                },
                priceMultiplier = 6,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private void BindSettings()
        {
            chargeTime = Float("Charge", "ChargeTime", 4f, 1f, 10f,
                "Seconds the shield stays up before it crashes by itself (press the button again to crash sooner).");
            minPressTime = Float("Charge", "MinPressTime", 0.2f, 0f, 2f,
                "Seconds after the cast before pressing the button again crashes (so the cast's own press doesn't).");
            energyPerSecond = Float("Charge", "EnergyPerSecond", 25f, 5f, 100f,
                "Percent of full force stored per second of running at base run speed. Faster stores more " +
                "(a sprint is +40%, speed relics +15% each), standing still stores nothing. 25 = full force after " +
                "4 s at base speed, about 2.9 s sprinting.");
            enhancedEnergyBonus = Float("Charge", "EnhancedEnergyBonus", 35f, 0f, 200f,
                "Percent faster the force builds when enhanced.");
            sprintDelay = Float("Charge", "SprintDelay", 1f, 0f, 5f,
                "Seconds on the move behind the shield before you work up to a sprint (+40% run speed, the game's own " +
                "run speed-up, which it drops when you cast). Stopping loses it.");
            frontArmour = Float("Charge", "FrontArmour", 30f, 0f, 90f,
                "Percent less damage from hits coming from in front of you while the shield is up (0 = off). " +
                "Hits from behind or the sides land in full.");
            brace = Config.Bind("Charge", "Brace", true,
                "Hits don't stagger you or knock you back while the shield is up (you still take the damage).");

            rushDistance = Float("Crash", "RushDistance", 2.5f, 0.5f, 6f, "How far the crash rushes you forward.");
            rushTime = Float("Crash", "RushTime", 0.12f, 0.05f, 0.5f, "Seconds the rush takes.");
            bashRadius = Float("Crash", "BashRadius", 1.2f, 0.3f, 3f,
                "Hit area of the shield bash during the rush (at the shield, a unit ahead of you).");
            crashRadius = Float("Crash", "CrashRadius", 2f, 0.5f, 5f, "Hit area of the crash at the end of the rush.");
            enhancedCrashRadius = Float("Crash", "EnhancedCrashRadius", 2.5f, 0.5f, 5f, "CrashRadius when enhanced.");
            fullMultiplier = Float("Crash", "FullMultiplier", 6f, 1f, 15f,
                "Damage of the bash and the crash at full force, as a multiple of their base damage ([Balance]). " +
                "Scales evenly from 1x at no force.");
            knockback = Float("Crash", "Knockback", 10f, 0f, 200f, "Knockback of the bash and the crash at no force.");
            fullKnockback = Float("Crash", "FullKnockback", 70f, 0f, 300f, "Knockback of the bash and the crash at full force.");
            stunEnergy = Float("Crash", "StunEnergy", 75f, 0f, 100f,
                "Percent of full force from which the crash stuns longer (see StunMultiplier).");
            stunMultiplier = Float("Crash", "StunMultiplier", 2.5f, 1f, 6f,
                "How much longer the crash's hitstun lasts at StunEnergy and above (1 = no longer).");

            drawnEffects = Config.Bind("Look", "DrawnEffects", true,
                "Draw the mod's own pixel-art effects on top of the game's (0.4.0): glowing cracks and a rune in the shield, " +
                "stone chunks circling you, floor cracks, embers, a crater, pulse rings and flung shards, plus the game's " +
                "floor crack and crater. Off = the 0.3.0 look. Cosmetic only; applies on the next cast.");

            const string restart = " Applies after restarting the game.";
            raiseDamage = Config.Bind("Balance", "RaiseDamage", 3,
                new ConfigDescription("Damage of the shove as the shield forms in front of you." + restart, new AcceptableValueRange<int>(0, 100)));
            raiseKnockback = Float("Balance", "RaiseKnockback", 8f, 0f, 100f, "Knockback of that shove." + restart);
            bashDamage = Config.Bind("Balance", "BashDamage", 2,
                new ConfigDescription("Base damage of the shield bash during the rush (x FullMultiplier at full force)." + restart,
                    new AcceptableValueRange<int>(0, 100)));
            crashDamage = Config.Bind("Balance", "CrashDamage", 4,
                new ConfigDescription("Base damage of the crash at the end of the rush (x FullMultiplier at full force)." + restart,
                    new AcceptableValueRange<int>(0, 200)));
            // Counting from the end of the cast (the crash or the cancel), not the start: the shield
            // can be up for 4 s, and Aegis Charge's 5.75 counts from a cast that's over in a moment.
            cooldown = Float("Balance", "Cooldown", 6f, 1f, 60f, "Cooldown in seconds, counting from the crash (or the cancel)." + restart);
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        private Sprite LoadIcon(string fileName)
        {
            string resourceName = $"{nameof(WoLStoneCharge)}.Assets.{fileName}";
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
