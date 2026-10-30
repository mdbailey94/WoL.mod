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
        public const string PluginVersion = "0.5.0";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<bool> signatureWithArcana;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.tidalwall.cfg) ----
        // [Cast], [Enhanced] and [Signature] apply the next time you cast it; [Balance] after
        // restarting the game. Every default is the 0.4.4 value, so nothing changes until edited.
        private static ConfigEntry<int> columns, enhancedExtraColumns, sigColumns;
        private static ConfigEntry<float> riseDistance, columnSpacing, wallDepth, hitAreaMargin, maxRiseTime, columnRiseInterval,
            holdTime, rollSpeed, rollTime, pushInterval, crashDepth, crashHitTime, enhancedRollTime;
        private static ConfigEntry<float> sigColumnSpacing, sigColumnScale, sigDepth, sigHoldTime, sigSpeed, sigRollTime,
            sigEnhancedRollTime, sigPushInterval, gatherTime;
        private static ConfigEntry<int> riseHitStop, pushHitStop;
        private static ConfigEntry<float> riseShake, pushShake, crashShake, crashSlowMoTime, crashSlowMoSpeed, crashFlash,
            cameraPullBack, roomDim, easeBackTime;
        private static ConfigEntry<int> pushDamage, crashDamage, sigPushDamage, sigCrashDamage;
        private static ConfigEntry<float> cooldown, pushKnockback, crashKnockback, sigPushKnockback, sigCrashKnockback,
            pushHitStun, crashHitStun, sigPushHitStun, sigCrashHitStun, sigFreezeChance, sigFreezeDuration,
            pushImmunity, sigPushImmunity;

        public static float RiseDistance => Get(riseDistance, 2f, 0.5f, 6f);
        public static int Columns => Get(columns, 5, 1, 15);
        public static float ColumnSpacing => Get(columnSpacing, 0.7f, 0.3f, 2f);
        public static float WallDepth => Get(wallDepth, 1.4f, 0.3f, 5f);
        public static float HitAreaMargin => Get(hitAreaMargin, 0.6f, 0f, 3f);
        public static float MaxRiseTime => Get(maxRiseTime, 0.5f, 0.05f, 2f);
        public static float ColumnRiseInterval => Get(columnRiseInterval, 0.03f, 0f, 0.3f);
        public static float HoldTime => Get(holdTime, 0.2f, 0f, 2f);
        public static float RollSpeed => Get(rollSpeed, 6f, 1f, 30f);
        public static float RollTime => Get(rollTime, 0.8f, 0.1f, 5f);
        public static float PushInterval => Get(pushInterval, 0.35f, 0.05f, 2f);
        public static float CrashDepth => Get(crashDepth, 1.6f, 0.5f, 4f);
        public static float CrashHitTime => Get(crashHitTime, 0.1f, 0.02f, 1f);
        public static int EnhancedExtraColumns => Get(enhancedExtraColumns, 2, 0, 10);
        public static float EnhancedRollTime => Get(enhancedRollTime, 1.05f, 0.1f, 5f);
        public static int SignatureColumns => Get(sigColumns, 8, 1, 25);
        public static float SignatureColumnSpacing => Get(sigColumnSpacing, 1f, 0.3f, 3f);
        public static float SignatureColumnScale => Get(sigColumnScale, 1.7f, 0.5f, 4f);
        public static float SignatureDepth => Get(sigDepth, 2.4f, 0.3f, 8f);
        public static float SignatureHoldTime => Get(sigHoldTime, 0.1f, 0f, 2f);
        public static float SignatureSpeed => Get(sigSpeed, 12f, 1f, 40f);
        public static float SignatureRollTime => Get(sigRollTime, 1f, 0.1f, 5f);
        public static float EnhancedSignatureRollTime => Get(sigEnhancedRollTime, 1.2f, 0.1f, 5f);
        public static float SignaturePushInterval => Get(sigPushInterval, 0.25f, 0.05f, 2f);
        public static float GatherTime => Get(gatherTime, 0.6f, 0.1f, 3f);
        public static int RiseHitStopFrames => Get(riseHitStop, 4, 0, 30);
        public static int PushHitStopFrames => Get(pushHitStop, 2, 0, 30);
        public static float RiseShake => Get(riseShake, 1.2f, 0f, 5f);
        public static float PushShake => Get(pushShake, 0.3f, 0f, 5f);
        public static float CrashShake => Get(crashShake, 1.6f, 0f, 5f);
        public static float CrashSlowMoTime => Get(crashSlowMoTime, 0.45f, 0f, 3f);
        public static float CrashSlowMoSpeed => Get(crashSlowMoSpeed, 0.03f, 0.01f, 1f);
        public static float CrashFlash => Get(crashFlash, 1.5f, 1f, 3f);
        public static float CameraPullBack => Get(cameraPullBack, 0.18f, 0f, 0.6f);
        public static float RoomDim => Get(roomDim, 0.55f, 0.1f, 1f);
        public static float EaseBackTime => Get(easeBackTime, 0.6f, 0.05f, 3f);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private static int Get(ConfigEntry<int> entry, int fallback, int min, int max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

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
            BindSettings();
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
                    damage = new[]
                    {
                        Mathf.Clamp(pushDamage.Value, 0, 100),
                        Mathf.Clamp(crashDamage.Value, 0, 100),
                        Mathf.Clamp(sigPushDamage.Value, 0, 200),
                        Mathf.Clamp(sigCrashDamage.Value, 0, 300)
                    },
                    cooldown = new[] { Mathf.Clamp(cooldown.Value, 1f, 60f) },
                    // Knockback is always straight along your aim (overwrite), so the wall shoves
                    // everything the same way; the pushes are light so enemies stay in front of it
                    // and get carried, the crash throws them. Tidal Blast's blast is 50, the
                    // ice wave's (SummonIceWave) 25.
                    // 0.4.4 halved them all (Matt: it pushed too far; was 20, 40, 30, 60).
                    knockbackMultiplier = new[]
                    {
                        Mathf.Clamp(pushKnockback.Value, 0f, 100f),
                        Mathf.Clamp(crashKnockback.Value, 0f, 200f),
                        Mathf.Clamp(sigPushKnockback.Value, 0f, 100f),
                        Mathf.Clamp(sigCrashKnockback.Value, 0f, 200f)
                    },
                    knockbackOverwrite = new[] { true },
                    hitStunDurationModifier = new[]
                    {
                        Mathf.Clamp(pushHitStun.Value, 0f, 5f),
                        Mathf.Clamp(crashHitStun.Value, 0f, 5f),
                        Mathf.Clamp(sigPushHitStun.Value, 0f, 5f),
                        Mathf.Clamp(sigCrashHitStun.Value, 0f, 5f)
                    },
                    // Water's status in this game is Freeze (Air's is Slow). The wall doesn't
                    // freeze at all (it's already control); the signature's crash freezes
                    // everything it carried (level 4). Its pushes don't, so the frozen don't stop
                    // being carried.
                    freezeChance = new[] { 0f, 0f, 0f, Mathf.Clamp01(sigFreezeChance.Value) },
                    freezeDuration = new[] { 1.5f, 1.5f, 1.5f, Mathf.Clamp(sigFreezeDuration.Value, 0f, 10f) },
                    // One hit per push: the wall's hit area is switched off and on every 0.35 s
                    // (0.25 s for the signature), and this stops it hitting again in between as
                    // enemies get shoved out and caught up with.
                    sameAttackImmunityTime = new[]
                    {
                        Mathf.Clamp(pushImmunity.Value, 0f, 2f),
                        0.3f,
                        Mathf.Clamp(sigPushImmunity.Value, 0f, 2f),
                        0.3f
                    }
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

        private void BindSettings()
        {
            const string next = " Applies to the next cast.";
            riseDistance = Float("Cast", "RiseDistance", 2f, 0.5f, 6f,
                "How far in front of you the wall rises, in units (it stops short of a level wall in the way)." + next);
            columns = Int("Cast", "Columns", 5, 1, 15,
                "Water columns side by side in the wall. Its width is Columns x ColumnSpacing (3.5 units)." + next);
            columnSpacing = Float("Cast", "ColumnSpacing", 0.7f, 0.3f, 2f,
                "Units between neighbouring columns, which sets how wide the wall is." + next);
            wallDepth = Float("Cast", "WallDepth", 1.4f, 0.3f, 5f, "Thickness of the wall's hit area along your aim, in units." + next);
            hitAreaMargin = Float("Cast", "HitAreaMargin", 0.6f, 0f, 3f,
                "Units the hit area reaches past the columns, in total across both ends (twice this for the crash; signature too)." + next);
            maxRiseTime = Float("Cast", "MaxRiseTime", 0.5f, 0.05f, 2f,
                "Most seconds the wall spends rising before it stands, even if columns are still coming up (signature too)." + next);
            columnRiseInterval = Float("Cast", "ColumnRiseInterval", 0.03f, 0f, 0.3f,
                "Seconds between one column rising and the next (signature too)." + next);
            holdTime = Float("Cast", "HoldTime", 0.2f, 0f, 2f, "Seconds the risen wall stands before it rolls." + next);
            rollSpeed = Float("Cast", "RollSpeed", 6f, 1f, 30f, "How fast the wall rolls forward, in units per second." + next);
            rollTime = Float("Cast", "RollTime", 0.8f, 0.1f, 5f,
                "Seconds the wall rolls before it crashes. It travels RollSpeed x RollTime units (4.8)." + next);
            pushInterval = Float("Cast", "PushInterval", 0.35f, 0.05f, 2f,
                "Seconds between pushes while the wall rolls; each push hits everything in the wall once." + next);
            crashDepth = Float("Cast", "CrashDepthMultiplier", 1.6f, 0.5f, 4f,
                "The crash's hit area is this many times as deep as the wall's (signature too)." + next);
            crashHitTime = Float("Cast", "CrashHitTime", 0.1f, 0.02f, 1f,
                "Seconds the crash's hit area stays on (signature too)." + next);

            enhancedExtraColumns = Int("Enhanced", "ExtraColumns", 2, 0, 10,
                "Columns the enhanced wall has on top of Columns (and on top of the signature's Columns)." + next);
            enhancedRollTime = Float("Enhanced", "RollTime", 1.05f, 0.1f, 5f,
                "Seconds the enhanced wall rolls, in place of RollTime (6.3 units at the default speed)." + next);

            sigColumns = Int("Signature", "Columns", 8, 1, 25,
                "Water columns in the signature wave (plus the enhanced ExtraColumns). Width is Columns x ColumnSpacing (8 units)." + next);
            sigColumnSpacing = Float("Signature", "ColumnSpacing", 1f, 0.3f, 3f, "Units between the signature wave's columns." + next);
            sigColumnScale = Float("Signature", "ColumnScale", 1.7f, 0.5f, 4f, "Size of the signature's columns, times the normal ones." + next);
            sigDepth = Float("Signature", "Depth", 2.4f, 0.3f, 8f, "Thickness of the signature wave's hit area along your aim, in units." + next);
            sigHoldTime = Float("Signature", "HoldTime", 0.1f, 0f, 2f, "Seconds the risen signature wave stands before it rushes." + next);
            sigSpeed = Float("Signature", "Speed", 12f, 1f, 40f, "How fast the signature wave rushes, in units per second." + next);
            sigRollTime = Float("Signature", "RollTime", 1f, 0.1f, 5f,
                "Seconds the signature wave rushes before it crashes. It travels Speed x RollTime units (12)." + next);
            sigEnhancedRollTime = Float("Signature", "EnhancedRollTime", 1.2f, 0.1f, 5f,
                "RollTime when the signature is also enhanced (14.4 units at the default speed)." + next);
            sigPushInterval = Float("Signature", "PushInterval", 0.25f, 0.05f, 2f, "Seconds between the signature wave's pushes." + next);
            gatherTime = Float("Signature", "GatherTime", 0.6f, 0.1f, 3f,
                "Seconds you spend gathering the water before the signature wave rises." + next);
            riseHitStop = Int("Signature", "RiseHitStopFrames", 4, 0, 30, "Frames of hit-stop as the signature wave erupts (0: none)." + next);
            pushHitStop = Int("Signature", "PushHitStopFrames", 2, 0, 30, "Frames of hit-stop on each signature push that lands (0: none)." + next);
            riseShake = Float("Signature", "RiseShake", 1.2f, 0f, 5f, "Screen shake strength as the signature wave erupts." + next);
            pushShake = Float("Signature", "PushShake", 0.3f, 0f, 5f, "Screen shake strength on each signature push that lands." + next);
            crashShake = Float("Signature", "CrashShake", 1.6f, 0f, 5f, "Screen shake strength of the signature's crash." + next);
            crashSlowMoTime = Float("Signature", "CrashSlowMotionTime", 0.45f, 0f, 3f,
                "Seconds (real time) the crash's slow motion takes to ease back to normal speed (0: none)." + next);
            crashSlowMoSpeed = Float("Signature", "CrashSlowMotionSpeed", 0.03f, 0.01f, 1f,
                "Game speed the crash's slow motion starts from, as a multiplier (1: no slow-down)." + next);
            crashFlash = Float("Signature", "CrashFlash", 1.5f, 1f, 3f, "Room light at the crash, as a multiplier of normal (1: no flash)." + next);
            cameraPullBack = Float("Signature", "CameraPullBack", 0.18f, 0f, 0.6f,
                "How far the camera pulls back during the signature, as a fraction of its size (0.18 = 18%)." + next);
            roomDim = Float("Signature", "RoomDim", 0.55f, 0.1f, 1f,
                "Room light during the gather and rush, as a multiplier of normal (1: no darkening)." + next);
            easeBackTime = Float("Signature", "EaseBackTime", 0.6f, 0.05f, 3f,
                "Seconds (real time) the camera and light take to ease back after the crash." + next);

            const string restart = " Applies after restarting the game.";
            pushDamage = Int("Balance", "PushDamage", 4, 0, 100, "Damage of each push of the rolling wall." + restart);
            crashDamage = Int("Balance", "CrashDamage", 6, 0, 100, "Damage of the wall's crash." + restart);
            sigPushDamage = Int("Balance", "SignaturePushDamage", 10, 0, 200, "Damage of each push of the signature wave." + restart);
            sigCrashDamage = Int("Balance", "SignatureCrashDamage", 60, 0, 300, "Damage of the signature wave's crash." + restart);
            cooldown = Float("Balance", "WallCooldown", 6f, 1f, 60f, "Cooldown in seconds." + restart);
            pushKnockback = Float("Balance", "PushKnockback", 10f, 0f, 100f,
                "Knockback of each push, always straight along your aim. Low keeps enemies in front of the wall so they get carried." + restart);
            crashKnockback = Float("Balance", "CrashKnockback", 20f, 0f, 200f, "Knockback of the crash, along your aim." + restart);
            sigPushKnockback = Float("Balance", "SignaturePushKnockback", 15f, 0f, 100f, "Knockback of each signature push." + restart);
            sigCrashKnockback = Float("Balance", "SignatureCrashKnockback", 30f, 0f, 200f, "Knockback of the signature's crash." + restart);
            pushHitStun = Float("Balance", "PushHitStun", 1.5f, 0f, 5f, "Hit stun of each push, as a multiplier of the game's normal hit stun." + restart);
            crashHitStun = Float("Balance", "CrashHitStun", 1.5f, 0f, 5f, "Hit stun of the crash, as a multiplier." + restart);
            sigPushHitStun = Float("Balance", "SignaturePushHitStun", 2f, 0f, 5f, "Hit stun of each signature push, as a multiplier." + restart);
            sigCrashHitStun = Float("Balance", "SignatureCrashHitStun", 2f, 0f, 5f, "Hit stun of the signature's crash, as a multiplier." + restart);
            sigFreezeChance = Float("Balance", "SignatureCrashFreezeChance", 1f, 0f, 1f,
                "Chance (0 to 1) that the signature's crash freezes what it hits. The normal wall never freezes." + restart);
            sigFreezeDuration = Float("Balance", "SignatureCrashFreezeDuration", 2.5f, 0f, 10f,
                "Seconds the signature's crash keeps enemies frozen." + restart);
            pushImmunity = Float("Balance", "PushHitImmunity", 0.3f, 0f, 2f,
                "Seconds after a push before the same enemy can be pushed again. Keep it below PushInterval, or pushes get skipped." + restart);
            sigPushImmunity = Float("Balance", "SignaturePushHitImmunity", 0.2f, 0f, 2f,
                "The same for the signature's pushes; keep it below its PushInterval." + restart);
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        private ConfigEntry<int> Int(string section, string key, int value, int min, int max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(min, max)));

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
