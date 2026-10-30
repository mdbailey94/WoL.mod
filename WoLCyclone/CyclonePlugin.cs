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
        public const string PluginVersion = "0.7.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;
        private static ConfigEntry<string> twister;
        public static bool DrawnTwister => twister == null || twister.Value != "Coded";
        private ConfigEntry<bool> signatureWithArcana;
        private static ConfigEntry<float> signatureWidth, tossHeight;
        private static ConfigEntry<int> tossDamage;
        public static float SignatureWidth => Mathf.Clamp(signatureWidth?.Value ?? 2.5f, 1f, 4f);
        public static float TossHeight => Mathf.Clamp(tossHeight?.Value ?? 11f, 0.5f, 16f);
        public static int TossDamage => Mathf.Clamp(tossDamage?.Value ?? 25, 0, 200);

        // ---- 0.7.0 settings (BepInEx\config\mdbailey94.wol.cyclone.cfg), defaults as before ----
        // [Cast], [Enhanced] and [Signature] apply the next time you cast it; [Balance] after
        // restarting the game.
        private static ConfigEntry<float> growTime, fullPowerTime, minHoldTime, startDistance, steerSpeed, heavySteerSpeed, heavyAt,
            steerRange, startHitSize, fullHitSize, slowHitInterval, fastHitInterval, burstSize;
        private static ConfigEntry<float> enhGrowTime, enhFullPowerTime, enhSteerSpeed, enhHeavySteerSpeed, enhHeavyAt, enhBurstSize;
        private static ConfigEntry<float> tossFrom, tossReach, tossAgainAfter, cameraPullBack, roomDim, lightningFrom, lightningFlash,
            throwSlowMoTime, throwSlowMoSpeed, burstFlash, burstShake, burstSlowMoTime, burstSlowMoSpeed, burstEaseBack, letGoEaseBack;
        private static ConfigEntry<int> burstHitStopFrames;
        private ConfigEntry<int> stage1Damage, stage2Damage, stage3Damage, stage4Damage, burstDamage;
        private ConfigEntry<float> stage1Knockback, stage2Knockback, stage3Knockback, stage4Knockback, burstKnockback,
            twisterHitStun, burstHitStun, sameAttackImmunity, cooldown;

        public static float GrowTime => Get(growTime, 4f, 0.5f, 15f);
        public static float FullPowerTime => Get(fullPowerTime, 1f, 0f, 5f);
        public static float MinHoldTime => Get(minHoldTime, 0.35f, 0f, 2f);
        public static float StartDistance => Get(startDistance, 4f, 0.5f, 10f);
        public static float SteerSpeed => Get(steerSpeed, 3.5f, 0f, 15f);
        public static float HeavySteerSpeed => Get(heavySteerSpeed, 0.15f, 0f, 15f);
        public static float HeavyAt => Get(heavyAt, 0.5f, 0.05f, 1f);
        public static float SteerRange => Get(steerRange, 9f, 1f, 20f);
        public static float StartHitSize => Get(startHitSize, 0.9f, 0.2f, 6f);
        public static float FullHitSize => Get(fullHitSize, 2.4f, 0.2f, 8f);
        public static float SlowHitInterval => Get(slowHitInterval, 0.45f, 0.05f, 2f);
        public static float FastHitInterval => Get(fastHitInterval, 0.12f, 0.05f, 2f);
        public static float BurstSize => Get(burstSize, 1.6f, 0.5f, 5f);
        public static float EnhancedGrowTime => Get(enhGrowTime, 3f, 0.5f, 15f);
        public static float EnhancedFullPowerTime => Get(enhFullPowerTime, 0.6f, 0f, 5f);
        public static float EnhancedSteerSpeed => Get(enhSteerSpeed, 5.5f, 0f, 15f);
        public static float EnhancedHeavySteerSpeed => Get(enhHeavySteerSpeed, 0.8f, 0f, 15f);
        public static float EnhancedHeavyAt => Get(enhHeavyAt, 0.75f, 0.05f, 1f);
        public static float EnhancedBurstSize => Get(enhBurstSize, 2f, 0.5f, 5f);
        public static float TossFrom => Get(tossFrom, 0.15f, 0f, 1f);
        public static float TossReach => Get(tossReach, 0.6f, 0.1f, 2f);
        public static float TossAgainAfter => Get(tossAgainAfter, 0.5f, 0f, 5f);
        public static float CameraPullBack => Get(cameraPullBack, 0.25f, 0f, 0.6f);
        public static float RoomDim => Get(roomDim, 0.5f, 0.1f, 1f);
        public static float LightningFrom => Get(lightningFrom, 0.35f, 0f, 1f);
        public static float LightningFlash => Get(lightningFlash, 1.4f, 1f, 3f);
        public static float ThrowSlowMoTime => Get(throwSlowMoTime, 0.35f, 0f, 2f);
        public static float ThrowSlowMoSpeed => Get(throwSlowMoSpeed, 0.2f, 0.01f, 1f);
        public static int BurstHitStopFrames => Mathf.Clamp(burstHitStopFrames?.Value ?? 5, 0, 30);
        public static float BurstFlash => Get(burstFlash, 1.8f, 1f, 3f);
        public static float BurstShake => Get(burstShake, 2.4f, 0f, 6f);
        public static float BurstSlowMoTime => Get(burstSlowMoTime, 0.45f, 0f, 2f);
        public static float BurstSlowMoSpeed => Get(burstSlowMoSpeed, 0.04f, 0.01f, 1f);
        public static float BurstEaseBack => Get(burstEaseBack, 0.9f, 0.1f, 3f);
        public static float LetGoEaseBack => Get(letGoEaseBack, 0.5f, 0.1f, 3f);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        public static void Log(string message) => log?.LogInfo(message);

        // The signature's flashes and ease-back run on real time here, and its watchdog
        // (Cinematic.cs) never leaves the camera or light changed for long.
        private void Update() => Cinematic.Tick();

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
            // 0.5.1: 2.5 (Matt: wider; was 1.8). An old 1.8 left in the config file is moved up once.
            signatureWidth = Config.Bind("Signature", "Width", 2.5f,
                new ConfigDescription("The signature twister's hit area and look, times the enhanced one's. Applies to the next cast.", new AcceptableValueRange<float>(1f, 4f)));
            if (Mathf.Approximately(signatureWidth.Value, 1.8f))
                signatureWidth.Value = 2.5f;
            // 0.6.0 (Matt: 10+ units high, hurt on landing): 11 high, 25 on landing (were 2.5 and
            // 12); the old values left in a config file are moved up once.
            tossHeight = Config.Bind("Signature", "TossHeight", 11f,
                new ConfigDescription("How high the signature throws enemies (units); higher throws stay up longer. Applies to the next cast.", new AcceptableValueRange<float>(0.5f, 16f)));
            if (Mathf.Approximately(tossHeight.Value, 2.5f))
                tossHeight.Value = 11f;
            tossDamage = Config.Bind("Signature", "TossDamage", 25,
                new ConfigDescription("Damage a thrown enemy takes when it lands. Applies to the next cast.", new AcceptableValueRange<int>(0, 200)));
            if (tossDamage.Value == 12)
                tossDamage.Value = 25;
            BindSettings();
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
                    damage = new[]
                    {
                        Mathf.Clamp(stage1Damage.Value, 0, 100),
                        Mathf.Clamp(stage2Damage.Value, 0, 100),
                        Mathf.Clamp(stage3Damage.Value, 0, 100),
                        Mathf.Clamp(stage4Damage.Value, 0, 100),
                        Mathf.Clamp(burstDamage.Value, 0, 300),
                        0
                    },
                    cooldown = new[] { Mathf.Clamp(cooldown.Value, 0.5f, 60f) },
                    // The tornado tugs enemies gently in to keep them inside; the burst throws hard.
                    knockbackMultiplier = new[]
                    {
                        Mathf.Clamp(stage1Knockback.Value, -50f, 50f),
                        Mathf.Clamp(stage2Knockback.Value, -50f, 50f),
                        Mathf.Clamp(stage3Knockback.Value, -50f, 50f),
                        Mathf.Clamp(stage4Knockback.Value, -50f, 50f),
                        Mathf.Clamp(burstKnockback.Value, 0f, 300f),
                        0f
                    },
                    hitStunDurationModifier = new[]
                    {
                        Mathf.Clamp(twisterHitStun.Value, 0f, 5f),
                        Mathf.Clamp(twisterHitStun.Value, 0f, 5f),
                        Mathf.Clamp(twisterHitStun.Value, 0f, 5f),
                        Mathf.Clamp(twisterHitStun.Value, 0f, 5f),
                        Mathf.Clamp(burstHitStun.Value, 0f, 5f),
                        0f
                    },
                    showDamageNumber = new[] { true, true, true, true, true, false },
                    // Shorter than the fastest hit rate, so every hit can land.
                    sameAttackImmunityTime = new[] { Mathf.Clamp(sameAttackImmunity.Value, 0f, 2f) }
                },
                priceMultiplier = 4,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        // 0.7.0 (Matt: more to balance with). All new names, defaulting to what was hardcoded.
        private void BindSettings()
        {
            const string next = " Applies to the next cast.";
            growTime = Float("Cast", "GrowTime", 4f, 0.5f, 15f, "Seconds of holding for the twister to grow from small to full size." + next);
            fullPowerTime = Float("Cast", "FullPowerTime", 1f, 0f, 5f, "Seconds it then stays at full size before it bursts." + next);
            minHoldTime = Float("Cast", "MinHoldTime", 0.35f, 0f, 2f, "Seconds a tap still keeps the twister up before it dies down." + next);
            startDistance = Float("Cast", "StartDistance", 4f, 0.5f, 10f,
                "Units in front of the wizard the twister starts (it stops short of a wall in the way)." + next);
            steerSpeed = Float("Cast", "SteerSpeed", 3.5f, 0f, 15f, "Units a second you can steer the twister while it's small." + next);
            heavySteerSpeed = Float("Cast", "HeavySteerSpeed", 0.15f, 0f, 15f, "Units a second you can steer it once it's heavy." + next);
            heavyAt = Float("Cast", "HeavyAt", 0.5f, 0.05f, 1f,
                "How far into the growth (0 to 1) the steering has slowed to HeavySteerSpeed." + next);
            steerRange = Float("Cast", "SteerRange", 9f, 1f, 20f, "Units from the wizard the twister can be steered at most." + next);
            startHitSize = Float("Cast", "StartHitSize", 0.9f, 0.2f, 6f, "Size of the twister's hit area when it starts (the game's wind burst size)." + next);
            fullHitSize = Float("Cast", "FullHitSize", 2.4f, 0.2f, 8f, "Size of its hit area at full size." + next);
            slowHitInterval = Float("Cast", "SlowHitInterval", 0.45f, 0.05f, 2f, "Seconds between the twister's hits when it starts." + next);
            fastHitInterval = Float("Cast", "FastHitInterval", 0.12f, 0.05f, 2f, "Seconds between its hits at full size." + next);
            burstSize = Float("Cast", "BurstSize", 1.6f, 0.5f, 5f, "Hit area of the final burst, times FullHitSize." + next);

            enhGrowTime = Float("Enhanced", "GrowTime", 3f, 0.5f, 15f, "Seconds for the enhanced twister to grow to full size." + next);
            enhFullPowerTime = Float("Enhanced", "FullPowerTime", 0.6f, 0f, 5f, "Seconds the enhanced twister stays at full size before it bursts." + next);
            enhSteerSpeed = Float("Enhanced", "SteerSpeed", 5.5f, 0f, 15f, "Units a second you can steer the enhanced twister while it's small." + next);
            enhHeavySteerSpeed = Float("Enhanced", "HeavySteerSpeed", 0.8f, 0f, 15f, "Units a second you can steer it once it's heavy." + next);
            enhHeavyAt = Float("Enhanced", "HeavyAt", 0.75f, 0.05f, 1f,
                "How far into the growth (0 to 1) its steering has slowed to HeavySteerSpeed." + next);
            enhBurstSize = Float("Enhanced", "BurstSize", 2f, 0.5f, 5f, "Hit area of the enhanced burst, times FullHitSize." + next);

            // The signature has everything enhanced has; these are its own.
            tossFrom = Float("Signature", "TossFrom", 0.15f, 0f, 1f, "How far into the growth (0 to 1) the signature starts throwing enemies." + next);
            tossReach = Float("Signature", "TossReach", 0.6f, 0.1f, 2f, "Enemies within this times the twister's hit area are thrown." + next);
            tossAgainAfter = Float("Signature", "TossAgainAfter", 0.5f, 0f, 5f, "Seconds after landing before an enemy can be thrown again." + next);
            cameraPullBack = Float("Signature", "CameraPullBack", 0.25f, 0f, 0.6f,
                "How far the camera pulls back as it grows, as a fraction of the normal view (0.25 = 25% more)." + next);
            roomDim = Float("Signature", "RoomDim", 0.5f, 0.1f, 1f, "The room's light at full size, times normal (1 = no darkening)." + next);
            lightningFrom = Float("Signature", "LightningFrom", 0.35f, 0f, 1f, "How far into the growth (0 to 1) lightning starts flickering." + next);
            lightningFlash = Float("Signature", "LightningFlash", 1.4f, 1f, 3f, "The room's light during a lightning flicker, times normal." + next);
            throwSlowMoTime = Float("Signature", "ThrowSlowMoTime", 0.35f, 0f, 2f,
                "Seconds of slow motion as the first thrown enemy reaches the top (0 = none)." + next);
            throwSlowMoSpeed = Float("Signature", "ThrowSlowMoSpeed", 0.2f, 0.01f, 1f, "Game speed that slow motion starts from (1 = normal speed)." + next);
            burstHitStopFrames = Config.Bind("Signature", "BurstHitStopFrames", 5,
                new ConfigDescription("Frames the game freezes for when the signature bursts (0 = none)." + next, new AcceptableValueRange<int>(0, 30)));
            burstFlash = Float("Signature", "BurstFlash", 1.8f, 1f, 3f, "The room's light in the burst's flash, times normal." + next);
            burstShake = Float("Signature", "BurstShake", 2.4f, 0f, 6f, "Strength of the burst's camera shake (the game's shake amount)." + next);
            burstSlowMoTime = Float("Signature", "BurstSlowMoTime", 0.45f, 0f, 2f, "Seconds of slow motion after the burst (0 = none)." + next);
            burstSlowMoSpeed = Float("Signature", "BurstSlowMoSpeed", 0.04f, 0.01f, 1f, "Game speed that slow motion starts from (1 = normal speed)." + next);
            burstEaseBack = Float("Signature", "BurstEaseBack", 0.9f, 0.1f, 3f, "Seconds the camera and light take to ease back after the burst." + next);
            letGoEaseBack = Float("Signature", "LetGoEaseBack", 0.5f, 0.1f, 3f,
                "Seconds the camera and light take to ease back when it's let go early or cut short." + next);

            const string restart = " Applies after restarting the game.";
            stage1Damage = Int("Balance", "Stage1Damage", 2, 0, 100, "Damage of each twister hit in the first quarter of its growth." + restart);
            stage2Damage = Int("Balance", "Stage2Damage", 3, 0, 100, "Damage of each hit in the second quarter." + restart);
            stage3Damage = Int("Balance", "Stage3Damage", 4, 0, 100, "Damage of each hit in the third quarter." + restart);
            stage4Damage = Int("Balance", "Stage4Damage", 6, 0, 100, "Damage of each hit in the last quarter and at full size." + restart);
            burstDamage = Int("Balance", "BurstDamage", 14, 0, 300, "Damage of the final burst." + restart);
            stage1Knockback = Float("Balance", "Stage1Knockback", -6f, -50f, 50f,
                "Knockback of the first quarter's hits; negative pulls enemies in toward the twister." + restart);
            stage2Knockback = Float("Balance", "Stage2Knockback", -6f, -50f, 50f, "Knockback of the second quarter's hits (negative pulls in)." + restart);
            stage3Knockback = Float("Balance", "Stage3Knockback", -7f, -50f, 50f, "Knockback of the third quarter's hits (negative pulls in)." + restart);
            stage4Knockback = Float("Balance", "Stage4Knockback", -8f, -50f, 50f, "Knockback of the last quarter's hits (negative pulls in)." + restart);
            burstKnockback = Float("Balance", "BurstKnockback", 70f, 0f, 300f, "Knockback of the final burst, outward." + restart);
            twisterHitStun = Float("Balance", "TwisterHitStun", 1f, 0f, 5f, "Hit stun of the twister's hits, times the game's normal." + restart);
            burstHitStun = Float("Balance", "BurstHitStun", 1.4f, 0f, 5f, "Hit stun of the final burst, times the game's normal." + restart);
            sameAttackImmunity = Float("Balance", "SameAttackImmunity", 0.1f, 0f, 2f,
                "Seconds an enemy can't be hit again by the same hit; keep it under FastHitInterval so every hit lands." + restart);
            cooldown = Float("Balance", "Cooldown", 5f, 0.5f, 60f, "Cooldown in seconds." + restart);
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        private ConfigEntry<int> Int(string section, string key, int value, int min, int max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(min, max)));

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
