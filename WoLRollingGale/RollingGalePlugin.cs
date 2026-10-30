using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLRollingGale
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class RollingGalePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.rollinggale";
        public const string PluginName = "Rolling Gale";
        public const string PluginVersion = "0.6.1";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<bool> signatureWithArcana;

        public static void Log(string message) => log?.LogInfo(message);

        // The signature's flashes and ease-back run on real time, and its watchdog never leaves
        // the camera or light changed for long (Cinematic.cs).
        private void Update() => Cinematic.Tick();

        // ---- Settings (BepInEx\config\mdbailey94.wol.rollinggale.cfg) ----
        // [Cast], [Enhanced] and [Signature] apply the next time you cast it; [Balance] after
        // restarting the game. The defaults (0.6.0) are the values the arcana always had.
        private static ConfigEntry<float> firstBurstDistance, burstSpacing, firstBurstScale, burstScaleGrowth, burstInterval, echoScale;
        private static ConfigEntry<int> burstCount, enhancedBurstCount;
        private static ConfigEntry<int> fanBursts, fanHitStopFrames, finalHitStopFrames;
        private static ConfigEntry<float> fanSpread, fanDelay, fanInterval, fanFirstDistance, fanDistanceStep, fanFirstScale, fanScaleStep;
        private static ConfigEntry<float> gatherTime, finalDistance, finalScale, finalSlowMoTime, finalSlowMoScale;
        private static ConfigEntry<int> burstDamage, echoDamage, fanBurstDamage, finalGaleDamage;
        private static ConfigEntry<float> cooldown, burstPull, fanPull, finalPull, burstHitStun, fanHitStun, finalHitStun, regrabTime;

        public static float FirstBurstDistance => Get(firstBurstDistance, 2.5f, 0.5f, 10f);
        public static float BurstSpacing => Get(burstSpacing, 3f, 0.5f, 8f);
        public static float FirstBurstScale => Get(firstBurstScale, 2.5f, 0.5f, 8f);
        public static float BurstScaleGrowth => Get(burstScaleGrowth, 0.5f, 0f, 3f);
        public static float BurstInterval => Get(burstInterval, 0.12f, 0.03f, 1f);
        public static float EchoScale => Get(echoScale, 0.85f, 0.1f, 2f);
        public static int BurstCount => Get(burstCount, 3, 1, 10);
        public static int EnhancedBurstCount => Get(enhancedBurstCount, 4, 1, 10);

        public static int FanBursts => Get(fanBursts, 6, 1, 12);
        public static float FanSpread => Get(fanSpread, 40f, 0f, 90f);
        public static float FanDelay => Get(fanDelay, 0.1f, 0f, 1f);
        public static float FanInterval => Get(fanInterval, 0.13f, 0.03f, 1f);
        public static float FanFirstDistance => Get(fanFirstDistance, 2.5f, 0.5f, 10f);
        public static float FanDistanceStep => Get(fanDistanceStep, 1f, 0f, 4f);
        public static float FanFirstScale => Get(fanFirstScale, 2.6f, 0.5f, 8f);
        public static float FanScaleStep => Get(fanScaleStep, 0.35f, 0f, 2f);
        public static int FanHitStopFrames => Get(fanHitStopFrames, 2, 0, 10);
        public static float GatherTime => Get(gatherTime, 0.3f, 0f, 2f);
        public static float FinalDistance => Get(finalDistance, 5.5f, 0f, 12f);
        public static float FinalScale => Get(finalScale, 7f, 1f, 15f);
        public static int FinalHitStopFrames => Get(finalHitStopFrames, 5, 0, 20);
        public static float FinalSlowMoTime => Get(finalSlowMoTime, 0.35f, 0f, 2f);
        public static float FinalSlowMoScale => Get(finalSlowMoScale, 0.05f, 0.01f, 1f);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private static int Get(ConfigEntry<int> entry, int fallback, int min, int max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Rolling Gale in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            signatureWithArcana = Config.Bind("General", "SignatureUnlocksWithArcana", true,
                "Unlock Rolling Gale's signature version along with the arcana itself, so it can go in the signature slot as soon as " +
                "you have it. Off: its Signature drop has to be found in a run, as for the game's own arcana.");
            BindSettings();
            // The signature version only shows up in the spellbook's Signature list once it's
            // unlocked; see SignatureUnlock.cs.
            SignatureUnlock.Install(new HarmonyLib.Harmony(PluginGuid), RollingGaleState.staticID,
                () => signatureWithArcana.Value, message => Log("Rolling Gale: " + message));

            Skills.Register(new SkillInfo
            {
                ID = RollingGaleState.staticID,
                displayName = "Rolling Gale",
                description = "Send a line of wind bursts rolling forward, dragging enemies along with them!",
                // The game shows this one text on the card for both the enhanced and the signature.
                enhancedDescription = "Adds a fourth, larger burst! Signature: a sweeping fan of bursts and a huge final gale!",
                icon = LoadIcon("icon.png"),
                tier = 2,
                stateType = typeof(RollingGaleState),
                skillStats = new SkillStats
                {
                    ID = new[] { RollingGaleState.staticID },
                    elementType = new[] { "Air" },
                    subElementType = new[] { "Air" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Level 1 is each burst, level 2 its echo pulse. At most 3x8 + 3x3 = 33 per enemy
                    // (44 enhanced) if dragged through the whole line.
                    // Levels 3 and 4 (0.5.0) are the signature's fan bursts and its final gale: an
                    // enemy dragged through all of it takes up to 6x12 + 5x3 + 45, about 130, about
                    // what the game's air signatures do to one enemy.
                    damage = new[]
                    {
                        Mathf.Clamp(burstDamage.Value, 0, 100),
                        Mathf.Clamp(echoDamage.Value, 0, 100),
                        Mathf.Clamp(fanBurstDamage.Value, 0, 100),
                        Mathf.Clamp(finalGaleDamage.Value, 0, 300)
                    },
                    cooldown = new[] { Mathf.Clamp(cooldown.Value, 1f, 60f) },
                    // Negative knockback pulls targets toward the burst, like Gust Burst. The
                    // settings are the pull's strength, so they're negated here.
                    knockbackMultiplier = new[]
                    {
                        -Mathf.Clamp(burstPull.Value, 0f, 100f),
                        -Mathf.Clamp(burstPull.Value, 0f, 100f),
                        -Mathf.Clamp(fanPull.Value, 0f, 100f),
                        -Mathf.Clamp(finalPull.Value, 0f, 150f)
                    },
                    hitStunDurationModifier = new[]
                    {
                        Mathf.Clamp(burstHitStun.Value, 0f, 5f),
                        Mathf.Clamp(burstHitStun.Value, 0f, 5f),
                        Mathf.Clamp(fanHitStun.Value, 0f, 5f),
                        Mathf.Clamp(finalHitStun.Value, 0f, 5f)
                    },
                    // Short, so each burst in the line (and its echo) grabs the enemy again.
                    sameAttackImmunityTime = new[] { Mathf.Clamp(regrabTime.Value, 0.02f, 2f) }
                },
                priceMultiplier = 4,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private void BindSettings()
        {
            const string next = " Applies to the next cast.";
            firstBurstDistance = Float("Cast", "FirstBurstDistance", 2.5f, 0.5f, 10f, "How far in front of the wizard the first burst lands, in units." + next);
            burstSpacing = Float("Cast", "BurstSpacing", 3f, 0.5f, 8f, "Distance between one burst and the next along the aim, in units." + next);
            firstBurstScale = Float("Cast", "FirstBurstScale", 2.5f, 0.5f, 8f, "Size of the first burst's pull area (the game's wind burst scale)." + next);
            burstScaleGrowth = Float("Cast", "BurstScaleGrowth", 0.5f, 0f, 3f, "How much bigger each burst is than the one before (added to its scale)." + next);
            burstInterval = Float("Cast", "BurstInterval", 0.12f, 0.03f, 1f,
                "Seconds between bursts; each burst's echo pulse also comes this long after it." + next);
            echoScale = Float("Cast", "EchoScale", 0.85f, 0.1f, 2f, "Size of each echo pulse, times the burst it echoes." + next);
            burstCount = Int("Cast", "BurstCount", 3, 1, 10, "Number of bursts in the line." + next);

            enhancedBurstCount = Int("Enhanced", "BurstCount", 4, 1, 10, "Number of bursts in the line when enhanced." + next);

            fanBursts = Int("Signature", "FanBursts", 6, 1, 12, "Number of bursts in the signature's fan." + next);
            fanSpread = Float("Signature", "FanSpread", 40f, 0f, 90f, "How far the fan sweeps to either side of the aim, in degrees (40 is an 80-degree fan)." + next);
            fanDelay = Float("Signature", "FanDelay", 0.1f, 0f, 1f, "Seconds from the cast to the fan's first burst." + next);
            fanInterval = Float("Signature", "FanInterval", 0.13f, 0.03f, 1f, "Seconds between the fan's bursts." + next);
            fanFirstDistance = Float("Signature", "FanFirstDistance", 2.5f, 0.5f, 10f, "How far from the wizard the fan's first burst lands, in units." + next);
            fanDistanceStep = Float("Signature", "FanDistanceStep", 1f, 0f, 4f, "How much further out each fan burst lands than the one before, in units." + next);
            fanFirstScale = Float("Signature", "FanFirstScale", 2.6f, 0.5f, 8f, "Size of the fan's first burst (the game's wind burst scale)." + next);
            fanScaleStep = Float("Signature", "FanScaleStep", 0.35f, 0f, 2f, "How much bigger each fan burst is than the one before (added to its scale)." + next);
            fanHitStopFrames = Int("Signature", "FanHitStopFrames", 2, 0, 10, "Frames the game freezes as each fan burst lands (0: none)." + next);
            gatherTime = Float("Signature", "GatherTime", 0.3f, 0f, 2f, "Seconds between the fan's last burst and the final gale." + next);
            finalDistance = Float("Signature", "FinalDistance", 5.5f, 0f, 12f, "How far along the aim the final gale lands, in units." + next);
            finalScale = Float("Signature", "FinalScale", 7f, 1f, 15f, "Size of the final gale's pull area (the game's wind burst scale)." + next);
            finalHitStopFrames = Int("Signature", "FinalHitStopFrames", 5, 0, 20, "Frames the game freezes as the final gale lands (0: none)." + next);
            finalSlowMoTime = Float("Signature", "FinalSlowMoTime", 0.35f, 0f, 2f, "Seconds of slow motion after the final gale (0: none)." + next);
            finalSlowMoScale = Float("Signature", "FinalSlowMoSpeed", 0.05f, 0.01f, 1f,
                "Game speed during that slow motion, as a multiplier (0.05 is near-frozen, 1 is normal speed)." + next);

            const string restart = " Applies after restarting the game.";
            burstDamage = Config.Bind("Balance", "BurstDamage", 8,
                new ConfigDescription("Damage of each burst in the line." + restart, new AcceptableValueRange<int>(0, 100)));
            echoDamage = Config.Bind("Balance", "EchoDamage", 3,
                new ConfigDescription("Damage of each burst's echo pulse (and the signature fan's echoes)." + restart, new AcceptableValueRange<int>(0, 100)));
            fanBurstDamage = Config.Bind("Balance", "FanBurstDamage", 12,
                new ConfigDescription("Damage of each burst in the signature's fan." + restart, new AcceptableValueRange<int>(0, 100)));
            finalGaleDamage = Config.Bind("Balance", "FinalGaleDamage", 45,
                new ConfigDescription("Damage of the signature's final gale." + restart, new AcceptableValueRange<int>(0, 300)));
            cooldown = Float("Balance", "GaleCooldown", 5f, 1f, 60f, "Cooldown in seconds." + restart);
            burstPull = Float("Balance", "BurstPull", 22f, 0f, 100f,
                "How hard each burst and its echo drag enemies in toward the burst's middle (the game's knockback, pulling inward)." + restart);
            fanPull = Float("Balance", "FanPull", 26f, 0f, 100f, "How hard each signature fan burst drags enemies in." + restart);
            finalPull = Float("Balance", "FinalPull", 36f, 0f, 150f, "How hard the signature's final gale drags enemies in." + restart);
            burstHitStun = Float("Balance", "BurstHitStun", 1.1f, 0f, 5f, "How long enemies are stunned by a burst or its echo, times the game's usual hit stun." + restart);
            fanHitStun = Float("Balance", "FanHitStun", 1.2f, 0f, 5f, "Hit stun of each signature fan burst, times the game's usual hit stun." + restart);
            finalHitStun = Float("Balance", "FinalHitStun", 1.6f, 0f, 5f, "Hit stun of the signature's final gale, times the game's usual hit stun." + restart);
            regrabTime = Float("Balance", "RegrabTime", 0.1f, 0.02f, 2f,
                "Seconds before another of its bursts can hit the same enemy; short, so each burst in the line grabs it again." + restart);
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        private ConfigEntry<int> Int(string section, string key, int value, int min, int max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(min, max)));

        private Sprite LoadIcon(string fileName)
        {
            string resourceName = $"{nameof(WoLRollingGale)}.Assets.{fileName}";
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
