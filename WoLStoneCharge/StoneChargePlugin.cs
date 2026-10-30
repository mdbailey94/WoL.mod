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
        public const string PluginVersion = "0.7.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<bool> signatureWithArcana;

        public static void Log(string message) => log?.LogInfo(message);

        // The signature's flashes and ease-back run on real time, and its watchdog never leaves
        // the camera or light changed for long (Cinematic.cs).
        private void Update() => Cinematic.Tick();

        // ---- Settings (BepInEx\config\mdbailey94.wol.stonecharge.cfg) ----
        // [Throw], [Signature] and [Look] apply the next time you cast it; [Balance] after
        // restarting the game. 0.6.0's are new names: the charge's old ones are left unused.
        private static ConfigEntry<float> distance, flightTime, slamDelay, shieldSize, slamRadius, enhancedSlamRadius;
        private static ConfigEntry<float> sigDistance, sigSlamDelay, sigShieldSize, sigSlamRadius;
        private static ConfigEntry<bool> drawnEffects;
        private static ConfigEntry<int> raiseDamage, slamDamage, enhancedSlamDamage, sigSlamDamage, shockwaveDamage;
        private static ConfigEntry<float> raiseKnockback, slamKnockback, cooldown;

        public static float Distance => Get(distance, 5f, 1f, 10f);
        public static float FlightTime => Get(flightTime, 0.4f, 0.1f, 1.5f);
        public static float SlamDelay => Get(slamDelay, 0.45f, 0f, 2f);
        public static float ShieldSize => Get(shieldSize, 1.6f, 0.5f, 4f);
        public static float SlamRadius => Get(slamRadius, 2.6f, 0.5f, 6f);
        public static float EnhancedSlamRadius => Get(enhancedSlamRadius, 3.2f, 0.5f, 6f);
        public static float SignatureDistance => Get(sigDistance, 6.5f, 1f, 12f);
        public static float SignatureSlamDelay => Get(sigSlamDelay, 0.8f, 0f, 3f);
        public static float SignatureShieldSize => Get(sigShieldSize, 4.5f, 1f, 8f);
        public static float SignatureSlamRadius => Get(sigSlamRadius, 7f, 1f, 12f);
        public static bool DrawnEffects => drawnEffects?.Value ?? true;

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Stone Charge in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            BindSettings();
            AdvancedTuning.Bind(Config);
            // The signature version only shows up in the spellbook's Signature list once it's
            // unlocked; see SignatureUnlock.cs.
            SignatureUnlock.Install(new HarmonyLib.Harmony(PluginGuid), StoneChargeState.staticID,
                () => signatureWithArcana.Value, message => Log("Stone Charge: " + message));

            Skills.Register(new SkillInfo
            {
                ID = StoneChargeState.staticID,
                displayName = "Stone Charge",
                description = "Hurl a great stone shield forward: it hangs in the air for a moment, then slams down, " +
                    "blasting away every foe around it!",
                // The game shows this one text on the card for both the enhanced and the signature.
                enhancedDescription = "A bigger shield that drops sooner and blasts wider! Signature: a colossal shield and an earthquake!",
                icon = LoadIcon("icon.png"),
                // Same tier and price as Aegis Charge and Terra Ring, the vanilla Earth arcana it's
                // closest to.
                tier = 4,
                stateType = typeof(StoneChargeState),
                skillStats = new SkillStats
                {
                    ID = new[] { StoneChargeState.staticID },
                    elementType = new[] { "Earth" },
                    subElementType = new[] { "Earth" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Level 1: the shove as the shield leaves the wizard's hands (the game's shield
                    // makes it). 2-4: the slam (standard, enhanced, signature). 5: the signature's
                    // shockwave ring further out. An enemy under the signature takes about 80.
                    damage = new[]
                    {
                        Mathf.Clamp(raiseDamage.Value, 0, 100),
                        Mathf.Clamp(slamDamage.Value, 0, 200),
                        Mathf.Clamp(enhancedSlamDamage.Value, 0, 200),
                        Mathf.Clamp(sigSlamDamage.Value, 0, 300),
                        Mathf.Clamp(shockwaveDamage.Value, 0, 200)
                    },
                    cooldown = new[] { Mathf.Clamp(cooldown.Value, 1f, 60f) },
                    // The shove pushes the way it's thrown; the slams and the shockwave throw
                    // enemies outward from where the shield lands.
                    knockbackMultiplier = new[]
                    {
                        Mathf.Clamp(raiseKnockback.Value, 0f, 100f),
                        Mathf.Clamp(slamKnockback.Value, 0f, 200f),
                        Mathf.Clamp(slamKnockback.Value * 1.15f, 0f, 200f),
                        Mathf.Clamp(slamKnockback.Value * 1.7f, 0f, 300f),
                        45f
                    },
                    knockbackOverwrite = new[] { true, false, false, false, false },
                    hitStunDurationModifier = new[] { 1f, 1.3f, 1.3f, 1.6f, 1.5f },
                    sameTargetImmunityTime = new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f },
                    shakeCamera = new[] { false, true, true, true, true }
                },
                priceMultiplier = 6,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private void BindSettings()
        {
            distance = Float("Throw", "Distance", 5f, 1f, 10f, "How far the shield is thrown (it stops short of a wall in the way).");
            flightTime = Float("Throw", "FlightTime", 0.4f, 0.1f, 1.5f, "Seconds the shield flies.");
            slamDelay = Float("Throw", "SlamDelay", 0.45f, 0f, 2f,
                "Seconds the shield hangs in the air over where it lands before it slams down (enhanced: three quarters of it).");
            shieldSize = Float("Throw", "ShieldSize", 1.6f, 0.5f, 4f, "The shield's size, times the game's Aegis Charge shield (enhanced: 1.25 times this).");
            slamRadius = Float("Throw", "SlamRadius", 2.6f, 0.5f, 6f, "Hit area of the slam.");
            enhancedSlamRadius = Float("Throw", "EnhancedSlamRadius", 3.2f, 0.5f, 6f, "SlamRadius when enhanced.");

            sigDistance = Float("Signature", "ThrowDistance", 6.5f, 1f, 12f, "How far the signature's shield is thrown.");
            sigSlamDelay = Float("Signature", "SlamDelay", 0.8f, 0f, 3f, "Seconds the signature's shield hangs in the air before it slams.");
            sigShieldSize = Float("Signature", "ShieldSize", 4.5f, 1f, 8f, "The signature's shield, times the game's Aegis Charge shield.");
            sigSlamRadius = Float("Signature", "SlamRadius", 7f, 1f, 12f, "Hit area of the signature's slam; its shockwave reaches 1.6 times this.");

            drawnEffects = Config.Bind("Look", "DrawnEffects", true,
                "Add effects on top of the game's own: glowing cracks in the shield, a crater, rock spikes and flung stones. " +
                "Cosmetic only; applies on the next cast.");

            signatureWithArcana = Config.Bind("General", "SignatureUnlocksWithArcana", true,
                "Unlock Stone Charge's signature version along with the arcana itself, so it can go in the signature slot as soon as " +
                "you have it. Off: its Signature drop has to be found in a run, as for the game's own arcana.");

            const string restart = " Applies after restarting the game.";
            raiseDamage = Config.Bind("Balance", "RaiseDamage", 3,
                new ConfigDescription("Damage of the shove as the shield leaves the wizard's hands." + restart, new AcceptableValueRange<int>(0, 100)));
            raiseKnockback = Float("Balance", "RaiseKnockback", 8f, 0f, 100f, "Knockback of that shove." + restart);
            slamDamage = Config.Bind("Balance", "SlamDamage", 22,
                new ConfigDescription("Damage of the slam." + restart, new AcceptableValueRange<int>(0, 200)));
            enhancedSlamDamage = Config.Bind("Balance", "EnhancedSlamDamage", 28,
                new ConfigDescription("Damage of the enhanced slam." + restart, new AcceptableValueRange<int>(0, 200)));
            sigSlamDamage = Config.Bind("Balance", "SignatureSlamDamage", 60,
                new ConfigDescription("Damage of the signature's slam." + restart, new AcceptableValueRange<int>(0, 300)));
            shockwaveDamage = Config.Bind("Balance", "SignatureShockwaveDamage", 20,
                new ConfigDescription("Damage of the signature's shockwave ring." + restart, new AcceptableValueRange<int>(0, 200)));
            slamKnockback = Float("Balance", "SlamKnockback", 35f, 0f, 200f,
                "Knockback of the slam, outward (enhanced 1.15x, signature 1.7x)." + restart);
            cooldown = Float("Balance", "ThrowCooldown", 6f, 1f, 60f, "Cooldown in seconds." + restart);
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
