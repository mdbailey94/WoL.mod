using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LegendAPI;
using UnityEngine;

namespace WoLSlingshotDash
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class SlingshotDashPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.slingshotdash";
        public const string PluginName = "Slingshot Dash";
        public const string PluginVersion = "0.27.1";

        private static ManualLogSource log;
        private static ConfigEntry<string> chargeAnimation;
        private static ConfigEntry<float> chargePoseFrame;
        private static ConfigEntry<float> hopDistance;
        private static ConfigEntry<float> maxHold;
        private static ConfigEntry<float> cooldownSeconds;
        private ConfigEntry<float> dashCooldownSeconds;
        private ConfigEntry<float> drag;
        private static ConfigEntry<float> frostFreezeRadius;
        private static ConfigEntry<float> stormSphereSize;
        private static ConfigEntry<bool> matchIconPalette;
        private static ConfigEntry<float> vineHoldFrame;
        private static ConfigEntry<string> vineHoldAnimation;
        private static ConfigEntry<float> vineHoldPoseFrame;
        private static ConfigEntry<string> vineDark, vineMid, vineLight;
        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<bool> signatureWithArcana;

        private static SlingshotDashPlugin instance;

        public static void Log(string message) => log?.LogInfo(message);

        // Runs effects that outlive the dash state (e.g. Blazing Kick's vacuum).
        // How far a plain dash carries this wizard: the game's dash speed x dash duration (with
        // relics), or 3 if that can't be read or looks wrong. Never throws.
        private static bool loggedDash;

        public static float DashLength(Player player)
        {
            float computed = 0f;
            try
            {
                Movement movement = player != null ? player.movement : null;
                if (movement != null && movement.dashSpeedStat != null && movement.dashDurationStat != null)
                    computed = movement.dashSpeedStat.ModifiedValue * movement.dashDurationStat.ModifiedValue;
            }
            catch
            {
                computed = 0f;
            }
            bool usable = computed >= 1f && computed <= 12f;
            if (!loggedDash)
            {
                loggedDash = true;
                Log($"Dash length from speed x duration: {computed:0.##}" + (usable ? "" : " (using 3)"));
            }
            return usable ? computed : 3f;
        }

        public static void Run(IEnumerator routine)
        {
            if (instance != null)
                instance.StartCoroutine(routine);
        }

        public static float ChargePoseFrame => Mathf.Clamp01(chargePoseFrame?.Value ?? 0.4f);
        public static float HopDistance => Mathf.Max(0f, hopDistance?.Value ?? 1.5f);
        // 0 = hold as long as you like.
        public static float MaxHoldSeconds => Mathf.Max(0f, maxHold?.Value ?? 2f);
        public static float SlingshotCooldown => Mathf.Max(0.5f, cooldownSeconds?.Value ?? 7f);
        public static string VineDarkColor => vineDark?.Value;
        public static string VineMidColor => vineMid?.Value;
        public static string VineLightColor => vineLight?.Value;
        public static float FrostFreezeRadius => Mathf.Max(0.25f, frostFreezeRadius?.Value ?? 1f);
        public static float VineHoldFrame => Mathf.Clamp01(vineHoldFrame?.Value ?? 0.95f);
        public static bool MatchIconPalette => matchIconPalette?.Value ?? true;
        public static float StormSphereSize => Mathf.Clamp(stormSphereSize?.Value ?? 0.6f, 0.2f, 1.5f);

        // ---- Each arcana's numbers (0.27.0, Matt: more to balance with) ----
        // [<Arcana>] and [<Arcana> Signature] apply the next time you cast it; [<Arcana> Balance]
        // (the skill stats) after restarting the game. Every default is the value it had in code.
        private static ConfigEntry<float> chargeFull, tapWindow, chargeSpeedBonus, chargeTimeBonus, hopTime;
        private static ConfigEntry<float> vfDragInterval, vfDragSize, vfLaunchDragSize, vfPulseInterval, vfVacuumSize,
            vfVacuumChargeSize, vfGustInterval, vfGustSize, vfGustRepeat;
        private static ConfigEntry<int> vfPulses;
        private static ConfigEntry<float> bdTrailInterval, bdTrailHitSize, bdLaunchHitSize, bdBlastSize, bdBlastChargeSize;
        private static ConfigEntry<float> bkFullCharge, bkMaxHold, bkSpeedBonus, bkTimeBonus, bkTrailInterval, bkTrailHitSize,
            bkLaunchHitSize, bkBlastSize, bkBlastChargeSize, bkRecoverTime;
        private static ConfigEntry<float> ikIgniteTime, ikBlitzRange, ikBlitzSpeed, ikBlitzOvershoot, ikBlitzPause, ikBlitzMaxTime,
            ikImpactSize, ikRiseTime, ikRiseHeight, ikFallTime, ikHangSlowMoTime, ikHangSlowMoScale, ikFinaleSize, ikRingRadius,
            ikRingBurstSize, ikIgniteZoomIn, ikHangZoomIn, ikIgniteLight, ikHangLight, ikFinaleFlash, ikFinaleShake,
            ikFinaleSlowMoTime, ikFinaleSlowMoScale, ikEaseBack;
        private static ConfigEntry<int> ikBlitzTargets, ikRingBursts, ikImpactHitStop, ikFinaleHitStop;
        private static ConfigEntry<float> fsFullCharge, fsEnhancedShare, fsMaxHold, fsShortestThrow, fsMaxDistance, fsFlightTime,
            fsPathHitInterval, fsPathHitSize, fsHoverTime, fsChargeRadiusBonus, fsLeftBehindFuse, fsThrowTime;
        private static ConfigEntry<float> sgFanSpread, sgFanDistance, sgPathHitInterval, sgPathHitSize, sgSwapInterval, sgSpinTime,
            sgSpinRadius, sgFinaleRadius, sgThrowFlash, sgStandLight, sgStandZoom, sgSpinShake, sgSpinShakeStep, sgSpinFlash,
            sgSpinFlashStep, sgChainStartFlash, sgChainStartShake, sgFinaleShake, sgFinaleFlash, sgFinaleSlowMoTime,
            sgFinaleSlowMoScale, sgFinaleLight;
        private static ConfigEntry<int> sgFanCount, sgChainStartHitStop, sgSpinHitStop, sgFinaleHitStop;
        private static ConfigEntry<float> vsRange, vsCatchWidth, vsGripInterval, vsGripSize, vsKickSize, vsBehindDistance, vsBehindSize,
            vsMinPullSpeed, vsSpreadRadius, vsSpreadLinger, vsSnareInterval, vsSnareSize;
        private static ConfigEntry<int> vsSpreadMax;
        private static ConfigEntry<float> clLaunchSpeed, clFanDegrees, clReflectDamage, clBurstSize, clBurstChargeSize,
            clEnhancedBurst, clFieldRadius, clFieldChargeRadius;
        private static ConfigEntry<int> clMaxCaught;
        // [<Arcana> Balance]: read once, when the arcana are registered.
        private ConfigEntry<int> vfWindDamage, vfVacuumDamage, vfGustDamage;
        private ConfigEntry<float> vfVacuumPull, vfGustKnockback, vfHitStun;
        private ConfigEntry<int> bdTrailDamage, bdBlastDamage;
        private ConfigEntry<float> bdTrailKnockback, bdBlastKnockback, bdHitStun, bdBurnChance;
        private ConfigEntry<int> bkTrailDamage, bkBlastDamage, bkEnhancedTrailDamage, bkEnhancedBlastDamage, bkKickDamage;
        private ConfigEntry<float> bkCooldown, bkTrailKnockback, bkBlastKnockback, bkKickKnockback, bkHitStun, bkKickHitStun, bkBurnChance;
        private ConfigEntry<int> fsExplosionDamage, fsPathDamage, fsSigPathDamage, fsSpinDamage, fsFinaleDamage;
        private ConfigEntry<float> fsPathKnockback, fsHitStun, fsFreezeDuration, fsFinaleFreezeDuration;
        private ConfigEntry<int> vsKickDamage, vsGripDamage, vsSnareDamage;
        private ConfigEntry<float> vsKickKnockback, vsKickHitStun, vsGripHitStun, vsSnareHitStun, vsSnareRoot;
        private ConfigEntry<int> clBurstDamage;
        private ConfigEntry<float> clBurstKnockback, clHitStun;

        // The four dashes' charge (ChargedDashState).
        public static float FullChargeTime => Get(chargeFull, 1f, 0.2f, 3f);
        public static float TapWindow => Get(tapWindow, 0.12f, 0.02f, 0.5f);
        public static float FullChargeSpeedBonus => Get(chargeSpeedBonus, 0.6f, 0f, 3f);
        public static float FullChargeDurationBonus => Get(chargeTimeBonus, 0.4f, 0f, 3f);
        public static float HopTime => Get(hopTime, 0.18f, 0.05f, 1f);

        // Vacuum Fist.
        public static float VacuumDragInterval => Get(vfDragInterval, 0.1f, 0.02f, 1f);
        public static float VacuumDragSize => Get(vfDragSize, 2f, 0.5f, 6f);
        public static float VacuumLaunchDragSize => Get(vfLaunchDragSize, 3f, 0.5f, 8f);
        public static int VacuumPulses => GetInt(vfPulses, 4, 1, 12);
        public static float VacuumPulseInterval => Get(vfPulseInterval, 0.15f, 0.03f, 1f);
        public static float VacuumSize => Get(vfVacuumSize, 1.75f, 0.5f, 6f);
        public static float VacuumChargeSize => Get(vfVacuumChargeSize, 1.25f, 0f, 6f);
        public static float VacuumGustInterval => Get(vfGustInterval, 0.08f, 0.02f, 1f);
        public static float VacuumGustSize => Get(vfGustSize, 1.1f, 0.3f, 4f);
        public static float VacuumGustRepeat => Get(vfGustRepeat, 0.35f, 0.05f, 2f);

        // Blazing Kick's dash version (hidden, BlazingSlingshotState).
        public static float BlazingDashTrailInterval => Get(bdTrailInterval, 0.1f, 0.02f, 1f);
        public static float BlazingDashTrailHitSize => Get(bdTrailHitSize, 2f, 0.5f, 6f);
        public static float BlazingDashLaunchHitSize => Get(bdLaunchHitSize, 3f, 0.5f, 8f);
        public static float BlazingDashBlastSize => Get(bdBlastSize, 2f, 0.5f, 8f);
        public static float BlazingDashBlastChargeSize => Get(bdBlastChargeSize, 1.25f, 0f, 6f);

        // Blazing Kick (BlazingKickState) and its signature, the Inferno Kick.
        public static float KickFullCharge => Get(bkFullCharge, 1f, 0.2f, 3f);
        public static float KickMaxHold => Get(bkMaxHold, 2f, 0.2f, 10f);
        public static float KickRushSpeedBonus => Get(bkSpeedBonus, 0.6f, 0f, 3f);
        public static float KickRushTimeBonus => Get(bkTimeBonus, 0.4f, 0f, 3f);
        public static float KickTrailInterval => Get(bkTrailInterval, 0.1f, 0.02f, 1f);
        public static float KickTrailHitSize => Get(bkTrailHitSize, 2f, 0.5f, 6f);
        public static float KickLaunchHitSize => Get(bkLaunchHitSize, 3f, 0.5f, 8f);
        public static float KickBlastSize => Get(bkBlastSize, 2f, 0.5f, 8f);
        public static float KickBlastChargeSize => Get(bkBlastChargeSize, 1.25f, 0f, 6f);
        public static float KickRecoverTime => Get(bkRecoverTime, 0.15f, 0f, 1f);
        public static float InfernoIgniteTime => Get(ikIgniteTime, 0.6f, 0.1f, 2f);
        public static int InfernoBlitzTargets => GetInt(ikBlitzTargets, 3, 1, 8);
        public static float InfernoBlitzRange => Get(ikBlitzRange, 9f, 2f, 20f);
        public static float InfernoBlitzSpeed => Get(ikBlitzSpeed, 2.5f, 0.5f, 6f);
        public static float InfernoBlitzOvershoot => Get(ikBlitzOvershoot, 1.2f, 0f, 4f);
        public static float InfernoBlitzPause => Get(ikBlitzPause, 0.08f, 0f, 0.5f);
        public static float InfernoBlitzMaxTime => Get(ikBlitzMaxTime, 0.35f, 0.1f, 1f);
        public static float InfernoImpactSize => Get(ikImpactSize, 2f, 0.5f, 8f);
        public static float InfernoRiseTime => Get(ikRiseTime, 0.3f, 0.05f, 1.5f);
        public static float InfernoRiseHeight => Get(ikRiseHeight, 3f, 0f, 8f);
        public static float InfernoFallTime => Get(ikFallTime, 0.12f, 0.03f, 1f);
        public static float InfernoHangSlowMoTime => Get(ikHangSlowMoTime, 0.45f, 0f, 2f);
        public static float InfernoHangSlowMoScale => Get(ikHangSlowMoScale, 0.12f, 0.01f, 1f);
        public static float InfernoFinaleSize => Get(ikFinaleSize, 5f, 1f, 12f);
        public static int InfernoRingBursts => GetInt(ikRingBursts, 10, 0, 24);
        public static float InfernoRingRadius => Get(ikRingRadius, 3.5f, 0.5f, 10f);
        public static float InfernoRingBurstSize => Get(ikRingBurstSize, 1.4f, 0.3f, 5f);
        public static float InfernoIgniteZoomIn => Get(ikIgniteZoomIn, 0.1f, 0f, 0.5f);
        public static float InfernoHangZoomIn => Get(ikHangZoomIn, 0.18f, 0f, 0.5f);
        public static float InfernoIgniteLight => Get(ikIgniteLight, 0.3f, 0f, 1f);
        public static float InfernoHangLight => Get(ikHangLight, 0.15f, 0f, 1f);
        public static int InfernoImpactHitStop => GetInt(ikImpactHitStop, 3, 0, 20);
        public static int InfernoFinaleHitStop => GetInt(ikFinaleHitStop, 7, 0, 30);
        public static float InfernoFinaleFlash => Get(ikFinaleFlash, 2.2f, 0f, 5f);
        public static float InfernoFinaleShake => Get(ikFinaleShake, 3f, 0f, 10f);
        public static float InfernoFinaleSlowMoTime => Get(ikFinaleSlowMoTime, 0.6f, 0f, 3f);
        public static float InfernoFinaleSlowMoScale => Get(ikFinaleSlowMoScale, 0.04f, 0.01f, 1f);
        public static float InfernoEaseBack => Get(ikEaseBack, 1.2f, 0.1f, 3f);

        // Feint Swap (FeintSwapState) and its signature.
        public static float FeintFullCharge => Get(fsFullCharge, 1f, 0.2f, 3f);
        public static float FeintEnhancedChargeShare => Get(fsEnhancedShare, 0.5f, 0.1f, 1f);
        public static float FeintMaxHold => Get(fsMaxHold, 2f, 0.2f, 10f);
        public static float FeintShortestThrow => Get(fsShortestThrow, 1.15f, 0.2f, 4f);
        public static float FeintMaxDistance => Get(fsMaxDistance, 10.5f, 1f, 20f);
        public static float FeintFlightTime => Get(fsFlightTime, 0.3f, 0.05f, 1.5f);
        public static float FeintPathHitInterval => Get(fsPathHitInterval, 0.05f, 0.02f, 0.5f);
        public static float FeintPathHitSize => Get(fsPathHitSize, 1.1f, 0.3f, 4f);
        public static float FeintHoverTime => Get(fsHoverTime, 2f, 0.2f, 6f);
        public static float FeintChargeRadiusBonus => Get(fsChargeRadiusBonus, 0.5f, 0f, 3f);
        public static float FeintLeftBehindFuse => Get(fsLeftBehindFuse, 2f, 0.2f, 6f);
        public static float FeintThrowTime => Get(fsThrowTime, 0.2f, 0.05f, 1f);
        public static int FeintFanCount => GetInt(sgFanCount, 9, 2, 15);
        public static float FeintFanSpread => Get(sgFanSpread, 72f, 0f, 180f);
        public static float FeintFanDistanceScale => Get(sgFanDistance, 1f, 0.2f, 2f);
        public static float FeintSigPathHitInterval => Get(sgPathHitInterval, 0.075f, 0.02f, 0.5f);
        public static float FeintSigPathHitSize => Get(sgPathHitSize, 1f, 0.3f, 4f);
        public static float FeintSwapInterval => Get(sgSwapInterval, 0.08f, 0f, 0.5f);
        public static float FeintSpinTime => Get(sgSpinTime, 0.12f, 0.03f, 0.5f);
        public static float FeintSpinRadiusScale => Get(sgSpinRadius, 3f, 0.5f, 8f);
        public static float FeintFinaleRadiusScale => Get(sgFinaleRadius, 4.2f, 0.5f, 10f);
        public static int FeintChainStartHitStop => GetInt(sgChainStartHitStop, 4, 0, 20);
        public static float FeintThrowFlash => Get(sgThrowFlash, 0.35f, 0f, 1f);
        public static float FeintStandLight => Get(sgStandLight, 0.55f, 0f, 1f);
        public static float FeintStandZoom => Get(sgStandZoom, 1.2f, 1f, 2f);
        public static int FeintSpinHitStop => GetInt(sgSpinHitStop, 3, 0, 20);
        public static float FeintSpinShake => Get(sgSpinShake, 0.7f, 0f, 5f);
        public static float FeintSpinShakeStep => Get(sgSpinShakeStep, 0.15f, 0f, 1f);
        public static float FeintSpinFlash => Get(sgSpinFlash, 0.2f, 0f, 1f);
        public static float FeintSpinFlashStep => Get(sgSpinFlashStep, 0.05f, 0f, 0.3f);
        public static float FeintChainStartFlash => Get(sgChainStartFlash, 0.3f, 0f, 1f);
        public static float FeintChainStartShake => Get(sgChainStartShake, 0.6f, 0f, 5f);
        public static int FeintFinaleHitStop => GetInt(sgFinaleHitStop, 8, 0, 30);
        public static float FeintFinaleShake => Get(sgFinaleShake, 2.6f, 0f, 10f);
        public static float FeintFinaleFlash => Get(sgFinaleFlash, 0.8f, 0f, 1f);
        public static float FeintFinaleSlowMoTime => Get(sgFinaleSlowMoTime, 0.45f, 0f, 3f);
        public static float FeintFinaleSlowMoScale => Get(sgFinaleSlowMoScale, 0.05f, 0.01f, 1f);
        public static float FeintFinaleLight => Get(sgFinaleLight, 1.45f, 1f, 3f);

        // Vine Slingshot (VineSlingshotState) and its enhanced spread (VineSpread).
        public static float VineRange => Get(vsRange, 10.1f, 2f, 20f);
        public static float VineCatchWidth => Get(vsCatchWidth, 0.45f, 0.1f, 2f);
        public static float VineGripInterval => Get(vsGripInterval, 0.4f, 0.1f, 2f);
        public static float VineGripSize => Get(vsGripSize, 0.7f, 0.2f, 3f);
        public static float VineKickSize => Get(vsKickSize, 1.3f, 0.3f, 4f);
        public static float VineBehindDistance => Get(vsBehindDistance, 2.4f, 0f, 6f);
        public static float VineBehindSize => Get(vsBehindSize, 1.1f, 0.3f, 4f);
        public static float VineMinPullSpeed => Get(vsMinPullSpeed, 18f, 5f, 60f);
        public static float VineSpreadRadius => Get(vsSpreadRadius, 4f, 0.5f, 10f);
        public static int VineSpreadMaxEnemies => GetInt(vsSpreadMax, 5, 0, 12);
        public static float VineSpreadLinger => Get(vsSpreadLinger, 3f, 0f, 10f);
        public static float VineSnareInterval => Get(vsSnareInterval, 0.5f, 0.1f, 3f);
        public static float VineSnareSize => Get(vsSnareSize, 0.6f, 0.2f, 3f);

        // Charged Leap (StormSlingshotState).
        public static float LeapLaunchSpeed => Get(clLaunchSpeed, 22f, 5f, 60f);
        public static float LeapFanDegrees => Get(clFanDegrees, 8f, 0f, 45f);
        public static float LeapReflectDamage => Get(clReflectDamage, 1.5f, 0.1f, 5f);
        public static float LeapBurstSize => Get(clBurstSize, 1.2f, 0.3f, 5f);
        public static float LeapBurstChargeSize => Get(clBurstChargeSize, 0.4f, 0f, 4f);
        public static float LeapEnhancedBurstScale => Get(clEnhancedBurst, 1.5f, 1f, 4f);
        public static float LeapFieldRadius => Get(clFieldRadius, 2f, 0.5f, 6f);
        public static float LeapFieldChargeRadius => Get(clFieldChargeRadius, 1.5f, 0f, 6f);
        public static int LeapMaxCaught => GetInt(clMaxCaught, 20, 1, 60);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private static int GetInt(ConfigEntry<int> entry, int fallback, int min, int max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        // The game's own player animation for the chosen name, or null for "None".
        public static string ChargeAnimation(Player player) => AnimationByName(player, chargeAnimation?.Value ?? "Jump");

        // Vine Slingshot's hold: the wizard punching the ground.
        public static string VineHoldAnimation(Player player) => AnimationByName(player, vineHoldAnimation?.Value ?? "PBAoE");
        public static float VineHoldPoseFrame => Mathf.Clamp01(vineHoldPoseFrame?.Value ?? 0.5f);

        private static string AnimationByName(Player player, string name)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "none": return null;
                case "charge": return player.ChargeAnimStr;
                case "slide": return player.SlideAnimStr;
                case "hurt": return player.HurtAnimStr;
                case "kick": return player.KickAnimStr;
                case "parry": return player.ParryAnimStr;
                case "slam": return player.GSlamAnimStr;
                case "pbaoe": return player.PBAoEAnimStr;
                case "fall": return player.FallAnimStr;
                default: return player.JumpAnimStr;
            }
        }

        private float nextIconCheck;

        // The icon recolouring checks in now and then (it hooks nothing in the game).
        private void Update()
        {
            // The Inferno Kick's flashes and ease-back run on real time, and its watchdog never
            // leaves the camera or light changed for long (Cinematic.cs).
            Cinematic.Tick();
            if (Time.unscaledTime < nextIconCheck)
                return;
            nextIconCheck = Time.unscaledTime + 1f;
            IconPalette.Tick();
        }

        private void Awake()
        {
            log = Logger;
            instance = this;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer the Slingshot arcana (Air, Fire, Water, Earth and Lightning) in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            signatureWithArcana = Config.Bind("General", "SignatureUnlocksWithArcana", true,
                "Unlock Feint Swap's signature version along with the arcana itself, so it can go in the " +
                "signature slot as soon as you have it. Off: its Signature drop has to be found in a run, " +
                "like the game's own arcana.");
            chargeAnimation = Config.Bind("Charge", "Animation", "Jump",
                new ConfigDescription("Animation for the backward hop and the held pose while charging.",
                    new AcceptableValueList<string>("Jump", "Charge", "Slide", "Hurt", "Kick", "Parry", "Slam", "PBAoE", "Fall", "None")));
            chargePoseFrame = Config.Bind("Charge", "PoseFrame", 0.4f,
                new ConfigDescription("How far into the animation the held pose is (0 = first frame, 1 = last).",
                    new AcceptableValueRange<float>(0f, 1f)));
            hopDistance = Config.Bind("Charge", "HopDistance", 1.5f,
                new ConfigDescription("How far the backward hop goes when you start charging (0 = no hop).",
                    new AcceptableValueRange<float>(0f, 4f)));
            // Replaces MaxHoldSeconds (default 0 = no limit) so the 2 s default applies.
            maxHold = Config.Bind("Charge", "MaxHold", 2f,
                new ConfigDescription("Launch automatically after holding this long, for every slingshot " +
                    "(0 = hold as long as you like).",
                    new AcceptableValueRange<float>(0f, 10f)));
            cooldownSeconds = Config.Bind("Balance", "CooldownSeconds", 7f,
                new ConfigDescription("How long until you can slingshot again (one charge). Until then the " +
                    "dash button is a normal dash.",
                    new AcceptableValueRange<float>(0.5f, 30f)));
            dashCooldownSeconds = Config.Bind("Balance", "DashCooldownSeconds", 0.6f,
                new ConfigDescription("Cooldown of the normal dash these arcana do when not slingshotting. " +
                    "Applies the next time the game starts.",
                    new AcceptableValueRange<float>(0.1f, 5f)));
            // The drag moved from the fire arcana (BlazingDrag) to the wind one.
            drag = Config.Bind("Balance", "Drag", 35f,
                new ConfigDescription("How hard Vacuum Fist's wind drags enemies along your dash. " +
                    "Make it negative if they get pushed the wrong way. Applies the next time the game starts.",
                    new AcceptableValueRange<float>(-80f, 80f)));
            vineDark = Config.Bind("Vines", "DarkColor", "",
                "Vine Slingshot's darkest green as a hex colour like #1E4A2A (empty = taken from the game's vines).");
            vineMid = Config.Bind("Vines", "MidColor", "", "Vine Slingshot's main green (empty = from the game).");
            vineLight = Config.Bind("Vines", "LightColor", "", "Vine Slingshot's highlight green (empty = from the game).");
            vineHoldAnimation = Config.Bind("Vines", "HoldAnimation", "PBAoE",
                new ConfigDescription("The wizard's animation while holding Vine Slingshot (PBAoE = punching the ground).",
                    new AcceptableValueList<string>("PBAoE", "Slam", "Jump", "Charge", "Slide", "Hurt", "Kick", "Parry", "Fall", "None")));
            vineHoldPoseFrame = Config.Bind("Vines", "HoldPoseFrame", 0.5f,
                new ConfigDescription("How far into that animation the held pose is (0 = first frame, 1 = last).",
                    new AcceptableValueRange<float>(0f, 1f)));
            vineHoldFrame = Config.Bind("Vines", "HoldFrame", 0.95f,
                new ConfigDescription("Which moment of the game's vine animation Vine Slingshot holds for the whole " +
                    "grab (0 = its start, 1 = its end).",
                    new AcceptableValueRange<float>(0f, 1f)));
            frostFreezeRadius = Config.Bind("Balance", "FrostFreezeRadius", 1f,
                new ConfigDescription("Radius of Feint Swap's freezes at a 0.2 s hold; a full charge adds " +
                    "half again.",
                    new AcceptableValueRange<float>(0.25f, 4f)));
            matchIconPalette = Config.Bind("Icons", "MatchGamePalette", true,
                "Recolour Vine Slingshot's icon in the colours of the game's own vine arcana icons. " +
                "Turn off to use the mod's own colours.");
            stormSphereSize = Config.Bind("Balance", "StormSphereSize", 0.6f,
                new ConfigDescription("Size of Charged Leap's Mag Sphere compared with the Mag Sphere arcana's " +
                    "(1 = the same size).",
                    new AcceptableValueRange<float>(0.2f, 1.5f)));
            BindArcanaSettings();

            Skills.Register(new SkillInfo
            {
                ID = SlingshotDashState.staticID,
                displayName = "Vacuum Fist",
                description = "Hold to charge, then release to rush across the room on a howling wind that drags enemies along, and finish with a punch that pulls them all in!",
                enhancedDescription = "Leaves a trail of gusts along your path!",
                icon = LoadIcon("icon.png"),
                tier = 2,
                stateType = typeof(SlingshotDashState),
                skillStats = new SkillStats
                {
                    ID = new[] { SlingshotDashState.staticID },
                    elementType = new[] { "Air" },
                    subElementType = new[] { "Air" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Level 1 is the dragging wind (launch and dash), level 2 the vacuum's pulses
                    // where you land: about 3 x4 + 4 x2 = 20 at most per enemy. Level 3 is the
                    // enhanced trail's gusts (each blows twice).
                    damage = new[]
                    {
                        GetInt(vfWindDamage, 4, 0, 100),
                        GetInt(vfVacuumDamage, 2, 0, 100),
                        GetInt(vfGustDamage, 3, 0, 100)
                    },
                    cooldown = new[] { dashCooldownSeconds.Value },
                    // Level 1 knocks enemies along the dash (set on each burst), so they get
                    // dragged with you; level 2 pulls toward its centre; level 3 pushes away.
                    knockbackMultiplier = new[] { drag.Value, -Get(vfVacuumPull, 32f, 0f, 120f), Get(vfGustKnockback, 18f, 0f, 120f) },
                    knockbackOverwrite = new[] { true, false, false },
                    hitStunDurationModifier = new[] { Get(vfHitStun, 1.2f, 0f, 5f) },
                    // Short, so the wind and vacuum keep grabbing enemies.
                    sameAttackImmunityTime = new[] { 0.12f }
                },
                priceMultiplier = 3,
                unlockCondition = () => modEnabled.Value
            });

            Skills.Register(new SkillInfo
            {
                ID = BlazingSlingshotState.staticID,
                displayName = "Blazing Kick",
                description = "Hold to charge, then release to blitz through enemies in a trail of flame, shoving them aside, and finish with a kick that blasts them away!",
                enhancedDescription = "Sets every enemy it touches on fire!",
                icon = LoadIcon("icon_fire.png"),
                tier = 2,
                stateType = typeof(BlazingSlingshotState),
                skillStats = new SkillStats
                {
                    ID = new[] { BlazingSlingshotState.staticID },
                    elementType = new[] { "Fire" },
                    subElementType = new[] { "Fire" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Level 1 is the launch and trail, level 2 the blast where you land: about
                    // 2 trail hits x4 + 10 = 18 at most per enemy. Levels 3 and 4 are the same for
                    // the enhanced version, plus a sure burn.
                    damage = new[]
                    {
                        GetInt(bdTrailDamage, 4, 0, 100),
                        GetInt(bdBlastDamage, 10, 0, 200),
                        GetInt(bdTrailDamage, 4, 0, 100),
                        GetInt(bdBlastDamage, 10, 0, 200)
                    },
                    cooldown = new[] { dashCooldownSeconds.Value },
                    // Positive knockback: the trail shoves enemies out of your way, the blast
                    // throws them hard.
                    knockbackMultiplier = new[]
                    {
                        Get(bdTrailKnockback, 30f, 0f, 120f),
                        Get(bdBlastKnockback, 60f, 0f, 200f),
                        Get(bdTrailKnockback, 30f, 0f, 120f),
                        Get(bdBlastKnockback, 60f, 0f, 200f)
                    },
                    hitStunDurationModifier = new[] { Get(bdHitStun, 1.2f, 0f, 5f) },
                    burnChance = new[] { 0f, 0f, Get(bdBurnChance, 1f, 0f, 1f), Get(bdBurnChance, 1f, 0f, 1f) },
                    burnLevel = new[] { 1 },
                    // Short, so the trail keeps catching enemies and the blast still lands.
                    sameAttackImmunityTime = new[] { 0.12f }
                },
                priceMultiplier = 3,
                // Replaced by the standard arcana below (0.26.0): kept only so saves that have it
                // still load.
                hidden = true,
                unlockCondition = () => false
            });

            // Blazing Kick as a standard arcana (0.26.0, Matt): hold its button to charge, let go
            // to rush and kick; with a signature, the Inferno Kick (BlazingKickState.cs).
            Skills.Register(new SkillInfo
            {
                ID = BlazingKickState.staticID,
                displayName = "Blazing Kick",
                description = "Hold to charge, then release to blitz through enemies in a trail of flame, shoving them aside, and finish with a kick that blasts them away!",
                // The game shows this one text for both the enhanced and the signature version.
                enhancedDescription = "Sets every enemy it touches on fire! Signature: blitz through your foes, leap and crash down in an inferno!",
                icon = LoadIcon("icon_fire.png"),
                tier = 2,
                stateType = typeof(BlazingKickState),
                skillStats = new SkillStats
                {
                    ID = new[] { BlazingKickState.staticID },
                    elementType = new[] { "Fire" },
                    subElementType = new[] { "Fire" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // As the dash's: level 1 the launch and trail, level 2 the blast (about 18 at
                    // most per enemy), 3 and 4 the same burning (enhanced). Level 5 is the Inferno
                    // Kick's axe kick (the game keeps 5 levels at most): with its blitz impacts
                    // (level 4, 10 and a burn), the burning trail and the ring of fire, about 110
                    // to an enemy caught in all of it, about what the game's fire signatures do.
                    damage = new[]
                    {
                        GetInt(bkTrailDamage, 4, 0, 100),
                        GetInt(bkBlastDamage, 10, 0, 200),
                        GetInt(bkEnhancedTrailDamage, 4, 0, 100),
                        GetInt(bkEnhancedBlastDamage, 10, 0, 200),
                        GetInt(bkKickDamage, 45, 0, 300)
                    },
                    cooldown = new[] { Get(bkCooldown, 4f, 0.5f, 60f) },
                    knockbackMultiplier = new[]
                    {
                        Get(bkTrailKnockback, 30f, 0f, 120f),
                        Get(bkBlastKnockback, 60f, 0f, 200f),
                        Get(bkTrailKnockback, 30f, 0f, 120f),
                        Get(bkBlastKnockback, 60f, 0f, 200f),
                        Get(bkKickKnockback, 80f, 0f, 300f)
                    },
                    hitStunDurationModifier = new[]
                    {
                        Get(bkHitStun, 1.2f, 0f, 5f), Get(bkHitStun, 1.2f, 0f, 5f), Get(bkHitStun, 1.2f, 0f, 5f),
                        Get(bkHitStun, 1.2f, 0f, 5f), Get(bkKickHitStun, 1.8f, 0f, 5f)
                    },
                    burnChance = new[]
                    {
                        0f, 0f, Get(bkBurnChance, 1f, 0f, 1f), Get(bkBurnChance, 1f, 0f, 1f), Get(bkBurnChance, 1f, 0f, 1f)
                    },
                    burnLevel = new[] { 1 },
                    sameAttackImmunityTime = new[] { 0.12f }
                },
                priceMultiplier = 3,
                unlockCondition = () => modEnabled.Value
            });

            Skills.Register(new SkillInfo
            {
                ID = FrostSlingshotState.staticID,
                displayName = "Feint Swap",
                description = "Hold to charge, then release to throw an ice feint and swap places with it, freezing enemies at both ends!",
                enhancedDescription = "Reaches full throwing range in half the charge time!",
                icon = LoadIcon("icon_ice.png"),
                tier = 2,
                stateType = typeof(FrostSlingshotState),
                skillStats = new SkillStats
                {
                    ID = new[] { FrostSlingshotState.staticID },
                    // The game's frost arcana are Water: "Ice" has no spellbook tab, and using it
                    // broke the wizard's setup when loading into the house.
                    elementType = new[] { "Water" },
                    subElementType = new[] { "Water" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // One Frost Nova where you stood: 12 damage and a guaranteed freeze.
                    damage = new[] { 12 },
                    cooldown = new[] { dashCooldownSeconds.Value },
                    knockbackMultiplier = new[] { 0f },
                    hitStunDurationModifier = new[] { 1f },
                    sameAttackImmunityTime = new[] { 0.5f },
                    freezeChance = new[] { 1f },
                    freezeDuration = new[] { 1.5f }
                },
                priceMultiplier = 3,
                // Replaced by the standard arcana below: kept only so saves that have it still load.
                hidden = true,
                unlockCondition = () => false
            });

            Skills.Register(new SkillInfo
            {
                ID = FeintSwapState.staticID,
                displayName = "Feint Swap",
                description = "Hold to charge, then release to hurl an ice feint through your foes; press again to swap places with it, and it explodes in a freezing blast!",
                // The game shows this one text for both the enhanced and the signature version.
                enhancedDescription = "Faster charge; leaves an exploding feint behind you can swap back to. Signature: hurl 9 feints, then chain-swap through them all!",
                icon = LoadIcon("icon_ice.png"),
                tier = 2,
                stateType = typeof(FeintSwapState),
                skillStats = new SkillStats
                {
                    ID = new[] { FeintSwapState.staticID },
                    // The game's frost arcana are Water: "Ice" has no spellbook tab, and using it
                    // broke the wizard's setup when loading into the house.
                    elementType = new[] { "Water" },
                    subElementType = new[] { "Water" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Level 1 is the feint's explosion (a Frost Nova): 15 damage and a sure freeze.
                    // Level 2 is the ice bursts along its flight: 4 damage and a little knockback
                    // (short immunity, so an enemy it passes can still be caught by the explosion).
                    // An enemy on the path takes one or two bursts, so about 23 at most with the
                    // explosion: a sure freeze plus a teleport and a decoy keeps the damage low
                    // (Frost Feint, the dash, is 12 with its freeze; Frost Fan 25). (0.23.7: the
                    // path bursts were 6.)
                    //
                    // Levels 3 to 5 are the signature (FeintSwapSignature.cs), so the levels above
                    // stay as they are: 3 the ice bursts along the six feints' flights, 4 each
                    // icicle spin as you swap onto a feint, 5 the bigger last one. One spin on an
                    // enemy is 17 (the plain explosion's 15 and a bit), with a burst or two on the
                    // way, about 20-23. Where everything lands on one spot (all six feints stopped
                    // by a wall right in front of you), 5 x 17 + 22 + 3 bursts x 3 = 116, and 131
                    // with the enhanced feint left behind (15), against the game's own signatures'
                    // ~120 on one enemy (85-175). In open ground the feints land 1.5 to 4 apart, so
                    // an enemy is usually in one spin, two or three at the shortest throw.
                    // sameTargetImmunityTime on these levels: the six feints' bursts can't stack on
                    // one enemy (at most one every 0.15 s), and nothing from one spin hits twice,
                    // while the next spin, 0.2 s later, still can.
                    damage = new[]
                    {
                        GetInt(fsExplosionDamage, 15, 0, 200),
                        GetInt(fsPathDamage, 4, 0, 100),
                        GetInt(fsSigPathDamage, 3, 0, 100),
                        GetInt(fsSpinDamage, 17, 0, 200),
                        GetInt(fsFinaleDamage, 22, 0, 300)
                    },
                    cooldown = new[] { Mathf.Max(0.5f, cooldownSeconds.Value) },
                    knockbackMultiplier = new[] { 0f, Get(fsPathKnockback, 10f, 0f, 120f), Get(fsPathKnockback, 10f, 0f, 120f), 0f, 0f },
                    hitStunDurationModifier = new[] { Get(fsHitStun, 1f, 0f, 5f) },
                    sameAttackImmunityTime = new[] { 0.5f, 0.1f, 0.1f, 0.5f, 0.5f },
                    sameTargetImmunityTime = new[] { 0f, 0f, 0.15f, 0.15f, 0.15f },
                    freezeChance = new[] { 1f, 0f, 0f, 1f, 1f },
                    freezeDuration = new[]
                    {
                        Get(fsFreezeDuration, 1.5f, 0f, 6f), Get(fsFreezeDuration, 1.5f, 0f, 6f), Get(fsFreezeDuration, 1.5f, 0f, 6f),
                        Get(fsFreezeDuration, 1.5f, 0f, 6f), Get(fsFinaleFreezeDuration, 2f, 0f, 6f)
                    }
                },
                priceMultiplier = 3,
                unlockCondition = () => modEnabled.Value
            });

            Skills.Register(new SkillInfo
            {
                ID = VineSlingshotState.staticID,
                displayName = "Vine Slingshot",
                description = "Hold to lash out twin vines that grab the first foe they touch, then release to pull yourself in and kick it away!",
                enhancedDescription = "Vines spread to nearby foes and keep them snared for 3 seconds after the kick!",
                icon = LoadIcon("icon_earth.png"),
                tier = 2,
                stateType = typeof(VineSlingshotState),
                skillStats = new SkillStats
                {
                    ID = new[] { VineSlingshotState.staticID },
                    elementType = new[] { "Earth" },
                    subElementType = new[] { "Earth" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Level 1 is the kick, level 2 the grip's ticks (every 0.4 s, up to 2 s), level 3
                    // the game's vine used for the look (harmless), level 4 the enhanced spread's
                    // snares (every 0.5 s, rooting).
                    damage = new[]
                    {
                        GetInt(vsKickDamage, 14, 0, 200),
                        GetInt(vsGripDamage, 3, 0, 100),
                        0,
                        GetInt(vsSnareDamage, 2, 0, 100)
                    },
                    cooldown = new[] { dashCooldownSeconds.Value },
                    // The kick knocks along your pull (set on the burst); the grip holds still.
                    knockbackMultiplier = new[] { Get(vsKickKnockback, 45f, 0f, 200f), 0f, 0f, 0f },
                    knockbackOverwrite = new[] { true, false, false, false },
                    hitStunDurationModifier = new[]
                    {
                        Get(vsKickHitStun, 1.2f, 0f, 5f), Get(vsGripHitStun, 1.5f, 0f, 5f), 0f, Get(vsSnareHitStun, 1f, 0f, 5f)
                    },
                    showDamageNumber = new[] { true, true, false, true },
                    rootChance = new[] { 0f, 0f, 0f, 1f },
                    rootDuration = new[] { 0f, 0f, 0f, Get(vsSnareRoot, 0.6f, 0f, 5f) },
                    // Short, so the kick still lands right after the last grip tick.
                    sameAttackImmunityTime = new[] { 0.1f }
                },
                priceMultiplier = 3,
                unlockCondition = () => modEnabled.Value
            });

            Skills.Register(new SkillInfo
            {
                ID = StormSlingshotState.staticID,
                displayName = "Charged Leap",
                description = "Hold to pull enemy projectiles into orbit around you, then release to fling them at the foe and leap in after them!",
                enhancedDescription = "A bigger lightning burst where you land!",
                icon = LoadIcon("icon_lightning.png"),
                tier = 2,
                stateType = typeof(StormSlingshotState),
                skillStats = new SkillStats
                {
                    ID = new[] { StormSlingshotState.staticID },
                    elementType = new[] { "Lightning" },
                    subElementType = new[] { "Lightning" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // The landing burst; caught projectiles keep their own damage (x1.5).
                    damage = new[] { GetInt(clBurstDamage, 10, 0, 200) },
                    cooldown = new[] { dashCooldownSeconds.Value },
                    knockbackMultiplier = new[] { Get(clBurstKnockback, 25f, 0f, 200f) },
                    hitStunDurationModifier = new[] { Get(clHitStun, 1.2f, 0f, 5f) },
                    sameAttackImmunityTime = new[] { 0.3f }
                },
                priceMultiplier = 3,
                unlockCondition = () => modEnabled.Value
            });

            // Feint Swap's signature version only shows up in the spellbook's Signature list once
            // it's unlocked; see SignatureUnlock.cs.
            try
            {
                SignatureUnlock.Install(new Harmony(PluginGuid + ".signature"), FeintSwapState.staticID,
                    () => signatureWithArcana.Value, message => Log("Slingshot: " + message));
                SignatureUnlock.Install(new Harmony(PluginGuid + ".signature"), BlazingKickState.staticID,
                    () => signatureWithArcana.Value, message => Log("Slingshot: " + message));
            }
            catch (System.Exception e)
            {
                Logger.LogError($"Feint Swap signature unlock hook failed to install: {e.Message}");
            }
            try
            {
                SlingshotHud.Install(new Harmony(PluginGuid));
            }
            catch (System.Exception e)
            {
                Logger.LogError($"Slingshot HUD hook failed to install: {e.Message}");
            }
            try
            {
                StormGuard.Install(new Harmony(PluginGuid + ".storm"));
            }
            catch (System.Exception e)
            {
                Logger.LogError($"Charged Leap hit guard failed to install: {e.Message}");
            }

            Logger.LogInfo($"{PluginName} {PluginVersion} registered Vacuum Fist, Blazing Kick, Feint Swap, Vine Slingshot and Charged Leap");
        }

        // 0.27.0: every arcana's numbers, each with the value it had in code as its default.
        private void BindArcanaSettings()
        {
            const string next = " Applies to the next cast.";
            const string restart = " Applies after restarting the game.";

            // The four dashes' charge (Vacuum Fist, Blazing Kick's dash version, Vine Slingshot, Charged Leap).
            chargeFull = Float("Charge", "FullChargeTime", 1f, 0.2f, 3f,
                "Seconds of holding the dash for a full charge, for the slingshot dashes." + next);
            tapWindow = Float("Charge", "TapWindow", 0.12f, 0.02f, 0.5f,
                "A press shorter than this (seconds) is a tap: the arcana's dash at normal length, with no hop." + next);
            chargeSpeedBonus = Float("Charge", "FullChargeSpeedBonus", 0.6f, 0f, 3f,
                "Extra dash speed at a full charge, as a share (0.6 = +60%)." + next);
            chargeTimeBonus = Float("Charge", "FullChargeDurationBonus", 0.4f, 0f, 3f,
                "Extra dash time at a full charge, as a share (0.4 = +40%)." + next);
            hopTime = Float("Charge", "HopTime", 0.18f, 0.05f, 1f, "Seconds the backward hop at the start of a charge takes." + next);

            // Vacuum Fist.
            const string vf = "Vacuum Fist";
            vfDragInterval = Float(vf, "DragInterval", 0.1f, 0.02f, 1f, "Seconds between the wind's grabs along the dash." + next);
            vfDragSize = Float(vf, "DragSize", 2f, 0.5f, 6f, "Size of each grab along the dash (the game's wind burst scale)." + next);
            vfLaunchDragSize = Float(vf, "LaunchDragSize", 3f, 0.5f, 8f, "Size of the wide grab where you launch." + next);
            vfPulses = Int(vf, "VacuumPulses", 4, 1, 12, "How many times the vacuum where you land pulls enemies in." + next);
            vfPulseInterval = Float(vf, "VacuumPulseInterval", 0.15f, 0.03f, 1f, "Seconds between the vacuum's pulls." + next);
            vfVacuumSize = Float(vf, "VacuumSize", 1.75f, 0.5f, 6f, "Size of the vacuum after a tap." + next);
            vfVacuumChargeSize = Float(vf, "VacuumChargeSize", 1.25f, 0f, 6f, "Size the vacuum gains at a full charge, on top of VacuumSize." + next);
            vfGustInterval = Float(vf, "GustInterval", 0.08f, 0.02f, 1f, "Enhanced: seconds between the gusts left along your path." + next);
            vfGustSize = Float(vf, "GustSize", 1.1f, 0.3f, 4f, "Enhanced: size of each gust." + next);
            vfGustRepeat = Float(vf, "GustRepeatDelay", 0.35f, 0.05f, 2f, "Enhanced: seconds before each gust blows its second time." + next);
            const string vfb = "Vacuum Fist Balance";
            vfWindDamage = Int(vfb, "WindDamage", 4, 0, 100, "Damage of each wind grab (the launch and along the dash)." + restart);
            vfVacuumDamage = Int(vfb, "VacuumDamage", 2, 0, 100, "Damage of each vacuum pull where you land." + restart);
            vfGustDamage = Int(vfb, "GustDamage", 3, 0, 100, "Enhanced: damage of each blow of a trail gust." + restart);
            vfVacuumPull = Float(vfb, "VacuumPull", 32f, 0f, 120f, "How hard each vacuum pull draws enemies in (knockback toward its centre)." + restart);
            vfGustKnockback = Float(vfb, "GustKnockback", 18f, 0f, 120f, "Enhanced: how hard the trail gusts push enemies away." + restart);
            vfHitStun = Float(vfb, "HitStun", 1.2f, 0f, 5f, "Hit stun of every hit, times the game's normal." + restart);

            // Blazing Kick's dash version (hidden from the shop since 0.26.0; still in saves that have it).
            const string bd = "Blazing Kick Dash";
            bdTrailInterval = Float(bd, "TrailInterval", 0.1f, 0.02f, 1f, "Dash version: seconds between the flame bursts along the dash." + next);
            bdTrailHitSize = Float(bd, "TrailHitSize", 2f, 0.5f, 6f, "Dash version: hit size of each flame burst along the dash." + next);
            bdLaunchHitSize = Float(bd, "LaunchHitSize", 3f, 0.5f, 8f, "Dash version: hit size of the burst where you launch." + next);
            bdBlastSize = Float(bd, "BlastSize", 2f, 0.5f, 8f, "Dash version: size of the kick's blast after a tap." + next);
            bdBlastChargeSize = Float(bd, "BlastChargeSize", 1.25f, 0f, 6f, "Dash version: size the blast gains at a full charge." + next);
            const string bdb = "Blazing Kick Dash Balance";
            bdTrailDamage = Int(bdb, "TrailDamage", 4, 0, 100, "Dash version: damage of the launch burst and each trail burst (enhanced too)." + restart);
            bdBlastDamage = Int(bdb, "BlastDamage", 10, 0, 200, "Dash version: damage of the kick's blast (enhanced too)." + restart);
            bdTrailKnockback = Float(bdb, "TrailKnockback", 30f, 0f, 120f, "Dash version: how hard the trail shoves enemies aside." + restart);
            bdBlastKnockback = Float(bdb, "BlastKnockback", 60f, 0f, 200f, "Dash version: how hard the blast throws enemies away." + restart);
            bdHitStun = Float(bdb, "HitStun", 1.2f, 0f, 5f, "Dash version: hit stun of every hit, times the game's normal." + restart);
            bdBurnChance = Float(bdb, "EnhancedBurnChance", 1f, 0f, 1f, "Dash version: chance (0 to 1) that an enhanced hit sets an enemy on fire." + restart);

            // Blazing Kick.
            const string bk = "Blazing Kick";
            bkFullCharge = Float(bk, "FullChargeTime", 1f, 0.2f, 3f, "Seconds of holding for a full charge." + next);
            bkMaxHold = Float(bk, "MaxHold", 2f, 0.2f, 10f, "Seconds of holding after which the rush goes by itself." + next);
            bkSpeedBonus = Float(bk, "RushSpeedBonus", 0.6f, 0f, 3f,
                "Extra rush speed at a full charge, as a share of a plain dash's speed (0.6 = +60%)." + next);
            bkTimeBonus = Float(bk, "RushTimeBonus", 0.4f, 0f, 3f,
                "Extra rush time at a full charge, as a share of a plain dash's time (0.4 = +40%)." + next);
            bkTrailInterval = Float(bk, "TrailInterval", 0.1f, 0.02f, 1f, "Seconds between the flame bursts along the rush." + next);
            bkTrailHitSize = Float(bk, "TrailHitSize", 2f, 0.5f, 6f, "Hit size of each flame burst along the rush." + next);
            bkLaunchHitSize = Float(bk, "LaunchHitSize", 3f, 0.5f, 8f, "Hit size of the burst where you start the rush." + next);
            bkBlastSize = Float(bk, "BlastSize", 2f, 0.5f, 8f, "Size of the kick's blast with no charge." + next);
            bkBlastChargeSize = Float(bk, "BlastChargeSize", 1.25f, 0f, 6f, "Size the blast gains at a full charge, on top of BlastSize." + next);
            bkRecoverTime = Float(bk, "RecoverTime", 0.15f, 0f, 1f, "Seconds after the kick (or the axe kick) before you can act again." + next);
            const string ik = "Blazing Kick Signature";
            ikIgniteTime = Float(ik, "IgniteTime", 0.6f, 0.1f, 2f, "Seconds the Inferno Kick's opening (the room going dark, flames roaring up) lasts." + next);
            ikBlitzTargets = Int(ik, "BlitzTargets", 3, 1, 8, "How many enemies you streak through, one after another." + next);
            ikBlitzRange = Float(ik, "BlitzRange", 9f, 2f, 20f, "How far away (units) an enemy can be to be streaked at." + next);
            ikBlitzSpeed = Float(ik, "BlitzSpeed", 2.5f, 0.5f, 6f, "Speed of each streak, times the wizard's dash speed." + next);
            ikBlitzOvershoot = Float(ik, "BlitzOvershoot", 1.2f, 0f, 4f, "How far (units) each streak carries on past the enemy." + next);
            ikBlitzPause = Float(ik, "BlitzPause", 0.08f, 0f, 0.5f, "Seconds between one streak's impact and the next streak." + next);
            ikBlitzMaxTime = Float(ik, "BlitzMaxTime", 0.35f, 0.1f, 1f, "Longest a streak lasts (seconds) before it stops anyway." + next);
            ikImpactSize = Float(ik, "ImpactSize", 2f, 0.5f, 8f, "Size of the burning blast at each streak's impact." + next);
            ikRiseTime = Float(ik, "RiseTime", 0.3f, 0.05f, 1.5f, "Seconds the spring up into the air takes." + next);
            ikRiseHeight = Float(ik, "RiseHeight", 3f, 0f, 8f, "How high (units) the wizard springs." + next);
            ikFallTime = Float(ik, "FallTime", 0.12f, 0.03f, 1f, "Seconds the axe kick's drop takes." + next);
            ikHangSlowMoTime = Float(ik, "HangSlowMoTime", 0.45f, 0f, 2f, "Seconds of slow motion at the top of the spring." + next);
            ikHangSlowMoScale = Float(ik, "HangSlowMoScale", 0.12f, 0.01f, 1f, "Game speed during that slow motion (1 = normal)." + next);
            ikFinaleSize = Float(ik, "FinaleSize", 5f, 1f, 12f, "Size of the axe kick's blast." + next);
            ikRingBursts = Int(ik, "RingBursts", 10, 0, 24, "How many fire bursts ring the axe kick's blast." + next);
            ikRingRadius = Float(ik, "RingRadius", 3.5f, 0.5f, 10f, "How far out (units) the ring of fire bursts sits." + next);
            ikRingBurstSize = Float(ik, "RingBurstSize", 1.4f, 0.3f, 5f, "Size of each fire burst in the ring." + next);
            ikIgniteZoomIn = Float(ik, "IgniteZoomIn", 0.1f, 0f, 0.5f, "How far the camera closes in during the opening (0.1 = 10%)." + next);
            ikHangZoomIn = Float(ik, "HangZoomIn", 0.18f, 0f, 0.5f, "How far the camera closes in at the top of the spring (0.18 = 18%)." + next);
            ikIgniteLight = Float(ik, "IgniteLight", 0.3f, 0f, 1f, "The room's light during the opening, times its normal (1 = no darkening)." + next);
            ikHangLight = Float(ik, "HangLight", 0.15f, 0f, 1f, "The room's light at the top of the spring, times its normal." + next);
            ikImpactHitStop = Int(ik, "ImpactHitStop", 3, 0, 20, "Frames of hit-stop at each streak's impact." + next);
            ikFinaleHitStop = Int(ik, "FinaleHitStop", 7, 0, 30, "Frames of hit-stop at the axe kick." + next);
            ikFinaleFlash = Float(ik, "FinaleFlash", 2.2f, 0f, 5f, "The axe kick's flash: the room's light for a moment, times its normal." + next);
            ikFinaleShake = Float(ik, "FinaleShake", 3f, 0f, 10f, "Strength of the axe kick's camera shake." + next);
            ikFinaleSlowMoTime = Float(ik, "FinaleSlowMoTime", 0.6f, 0f, 3f, "Seconds of slow motion after the axe kick." + next);
            ikFinaleSlowMoScale = Float(ik, "FinaleSlowMoScale", 0.04f, 0.01f, 1f, "Game speed during that slow motion (1 = normal)." + next);
            ikEaseBack = Float(ik, "EaseBack", 1.2f, 0.1f, 3f, "Seconds the camera and light take to ease back after the axe kick." + next);
            const string bkb = "Blazing Kick Balance";
            bkTrailDamage = Int(bkb, "TrailDamage", 4, 0, 100, "Damage of the launch burst and each trail burst." + restart);
            bkBlastDamage = Int(bkb, "BlastDamage", 10, 0, 200, "Damage of the kick's blast." + restart);
            bkEnhancedTrailDamage = Int(bkb, "EnhancedTrailDamage", 4, 0, 100,
                "Enhanced: damage of the launch and trail bursts; also the signature's burning trail and ring of fire." + restart);
            bkEnhancedBlastDamage = Int(bkb, "EnhancedBlastDamage", 10, 0, 200,
                "Enhanced: damage of the kick's blast; also each of the signature's streak impacts." + restart);
            bkKickDamage = Int(bkb, "SignatureKickDamage", 45, 0, 300, "Damage of the signature's axe kick blast." + restart);
            bkCooldown = Float(bkb, "Cooldown", 4f, 0.5f, 60f, "Cooldown in seconds, from the rush." + restart);
            bkTrailKnockback = Float(bkb, "TrailKnockback", 30f, 0f, 120f, "How hard the trail shoves enemies aside (enhanced too)." + restart);
            bkBlastKnockback = Float(bkb, "BlastKnockback", 60f, 0f, 200f, "How hard the blast throws enemies away (enhanced too)." + restart);
            bkKickKnockback = Float(bkb, "SignatureKickKnockback", 80f, 0f, 300f, "How hard the axe kick throws enemies away." + restart);
            bkHitStun = Float(bkb, "HitStun", 1.2f, 0f, 5f, "Hit stun of the trail and blast hits, times the game's normal." + restart);
            bkKickHitStun = Float(bkb, "SignatureKickHitStun", 1.8f, 0f, 5f, "Hit stun of the axe kick, times the game's normal." + restart);
            bkBurnChance = Float(bkb, "EnhancedBurnChance", 1f, 0f, 1f,
                "Chance (0 to 1) that an enhanced or signature hit sets an enemy on fire." + restart);

            // Feint Swap.
            const string fs = "Feint Swap";
            fsFullCharge = Float(fs, "FullChargeTime", 1f, 0.2f, 3f, "Seconds of holding for the longest throw." + next);
            fsEnhancedShare = Float(fs, "EnhancedChargeShare", 0.5f, 0.1f, 1f,
                "Enhanced: the share of FullChargeTime that gives the longest throw (0.5 = in half the time)." + next);
            fsMaxHold = Float(fs, "MaxHold", 2f, 0.2f, 10f, "Seconds of holding after which the feint throws by itself." + next);
            fsShortestThrow = Float(fs, "ShortestThrow", 1.15f, 0.2f, 4f, "The shortest throw (a tap), times a plain dash's length." + next);
            fsMaxDistance = Float(fs, "MaxDistance", 10.5f, 1f, 20f, "The longest throw (units), at a full charge." + next);
            fsFlightTime = Float(fs, "FlightTime", 0.3f, 0.05f, 1.5f, "Seconds the feint flies (the signature's feints too)." + next);
            fsPathHitInterval = Float(fs, "PathHitInterval", 0.05f, 0.02f, 0.5f, "Seconds between the ice bursts along the feint's flight." + next);
            fsPathHitSize = Float(fs, "PathHitSize", 1.1f, 0.3f, 4f, "Size of each ice burst along the flight." + next);
            fsHoverTime = Float(fs, "HoverTime", 2f, 0.2f, 6f,
                "Seconds the feint stands after landing before it swaps by itself (the signature's feints too)." + next);
            fsChargeRadiusBonus = Float(fs, "ChargeRadiusBonus", 0.5f, 0f, 3f,
                "Size the explosion gains at a full charge, as a share of [Balance] FrostFreezeRadius (0.5 = half again)." + next);
            fsLeftBehindFuse = Float(fs, "LeftBehindFuse", 2f, 0.2f, 6f,
                "Enhanced: seconds before the feint left where you swapped from explodes." + next);
            fsThrowTime = Float(fs, "ThrowTime", 0.2f, 0.05f, 1f, "Seconds the throwing pose lasts after letting go, before you can act again." + next);
            const string sg = "Feint Swap Signature";
            sgFanCount = Int(sg, "FanCount", 9, 2, 15, "How many feints the signature throws in a fan." + next);
            sgFanSpread = Float(sg, "FanSpread", 72f, 0f, 180f, "Degrees across the fan, centred on your aim." + next);
            sgFanDistance = Float(sg, "FanDistanceScale", 1f, 0.2f, 2f, "How far the fan's feints fly, times the normal throw at that charge." + next);
            sgPathHitInterval = Float(sg, "PathHitInterval", 0.075f, 0.02f, 0.5f, "Seconds between each fan feint's ice bursts in flight." + next);
            sgPathHitSize = Float(sg, "PathHitSize", 1f, 0.3f, 4f, "Size of each of those ice bursts." + next);
            sgSwapInterval = Float(sg, "SwapInterval", 0.08f, 0f, 0.5f, "Seconds between one swap of the chain and the next." + next);
            sgSpinTime = Float(sg, "SpinTime", 0.12f, 0.03f, 0.5f, "Seconds each icicle ring spins out before it bursts." + next);
            sgSpinRadius = Float(sg, "SpinRadiusScale", 3f, 0.5f, 8f, "Radius of each icicle spin's burst, times [Balance] FrostFreezeRadius." + next);
            sgFinaleRadius = Float(sg, "FinaleRadiusScale", 4.2f, 0.5f, 10f, "Radius of the last spin's burst, times [Balance] FrostFreezeRadius." + next);
            sgChainStartHitStop = Int(sg, "ChainStartHitStop", 4, 0, 20, "Frames held as the chain of swaps begins." + next);
            sgThrowFlash = Float(sg, "ThrowFlash", 0.35f, 0f, 1f, "Strength of the frosty flash as the fan is thrown (0 to 1)." + next);
            sgStandLight = Float(sg, "StandLight", 0.55f, 0f, 1f, "The stage light while the feints stand, times its normal (1 = no dimming)." + next);
            sgStandZoom = Float(sg, "StandZoom", 1.2f, 1f, 2f, "The camera pulls back to this times its size while the fan is out (1 = no pull-back)." + next);
            sgSpinHitStop = Int(sg, "SpinHitStop", 3, 0, 20, "Frames of hit-stop at each icicle spin." + next);
            sgSpinShake = Float(sg, "SpinShake", 0.7f, 0f, 5f, "Strength of the first spin's camera shake." + next);
            sgSpinShakeStep = Float(sg, "SpinShakeStep", 0.15f, 0f, 1f, "How much stronger each later spin's shake gets." + next);
            sgSpinFlash = Float(sg, "SpinFlash", 0.2f, 0f, 1f, "Strength of the first spin's flash (0 to 1)." + next);
            sgSpinFlashStep = Float(sg, "SpinFlashStep", 0.05f, 0f, 0.3f, "How much stronger each later spin's flash gets." + next);
            sgChainStartFlash = Float(sg, "ChainStartFlash", 0.3f, 0f, 1f, "Strength of the flash as the chain begins (0 to 1)." + next);
            sgChainStartShake = Float(sg, "ChainStartShake", 0.6f, 0f, 5f, "Strength of the camera shake as the chain begins." + next);
            sgFinaleHitStop = Int(sg, "FinaleHitStop", 8, 0, 30, "Frames of hit-stop at the last spin." + next);
            sgFinaleShake = Float(sg, "FinaleShake", 2.6f, 0f, 10f, "Strength of the last spin's camera shake." + next);
            sgFinaleFlash = Float(sg, "FinaleFlash", 0.8f, 0f, 1f, "Strength of the last spin's flash (0 to 1)." + next);
            sgFinaleSlowMoTime = Float(sg, "FinaleSlowMoTime", 0.45f, 0f, 3f, "Seconds of slow motion after the last spin." + next);
            sgFinaleSlowMoScale = Float(sg, "FinaleSlowMoScale", 0.05f, 0.01f, 1f, "Game speed during that slow motion (1 = normal)." + next);
            sgFinaleLight = Float(sg, "FinaleLight", 1.45f, 1f, 3f, "The stage lights up to this times its normal at the last spin." + next);
            const string fsb = "Feint Swap Balance";
            fsExplosionDamage = Int(fsb, "ExplosionDamage", 15, 0, 200,
                "Damage of the feint's explosion (and the enhanced feint left behind's)." + restart);
            fsPathDamage = Int(fsb, "PathDamage", 4, 0, 100, "Damage of each ice burst along the feint's flight." + restart);
            fsSigPathDamage = Int(fsb, "SignaturePathDamage", 3, 0, 100, "Damage of each ice burst along the signature feints' flights." + restart);
            fsSpinDamage = Int(fsb, "SignatureSpinDamage", 17, 0, 200, "Damage of each icicle spin as you swap onto a signature feint." + restart);
            fsFinaleDamage = Int(fsb, "SignatureFinaleDamage", 22, 0, 300, "Damage of the signature's last, bigger icicle spin." + restart);
            fsPathKnockback = Float(fsb, "PathKnockback", 10f, 0f, 120f, "Knockback of the ice bursts along the flights." + restart);
            fsHitStun = Float(fsb, "HitStun", 1f, 0f, 5f, "Hit stun of every hit, times the game's normal." + restart);
            fsFreezeDuration = Float(fsb, "FreezeDuration", 1.5f, 0f, 6f, "Seconds an enemy stays frozen by the explosion or a spin." + restart);
            fsFinaleFreezeDuration = Float(fsb, "SignatureFinaleFreezeDuration", 2f, 0f, 6f,
                "Seconds an enemy stays frozen by the signature's last spin." + restart);

            // Vine Slingshot.
            const string vs = "Vine Slingshot";
            vsRange = Float(vs, "Range", 10.1f, 2f, 20f, "How far (units) the vines reach when held (a tap reaches a plain dash's length)." + next);
            vsCatchWidth = Float(vs, "CatchWidth", 0.45f, 0.1f, 2f, "Radius (units) of the vines' path for catching an enemy." + next);
            vsGripInterval = Float(vs, "GripInterval", 0.4f, 0.1f, 2f, "Seconds between the grip's hits on a grabbed enemy while you hold." + next);
            vsGripSize = Float(vs, "GripSize", 0.7f, 0.2f, 3f, "Size of each grip hit." + next);
            vsKickSize = Float(vs, "KickSize", 1.3f, 0.3f, 4f, "Size of the kick's hit." + next);
            vsBehindDistance = Float(vs, "BehindDistance", 2.4f, 0f, 6f, "How far (units) past you the kick's second hit lands, along the kick." + next);
            vsBehindSize = Float(vs, "BehindSize", 1.1f, 0.3f, 4f, "Size of that second hit." + next);
            vsMinPullSpeed = Float(vs, "MinPullSpeed", 18f, 5f, 60f, "Slowest you're pulled along the vines (units a second)." + next);
            vsSpreadRadius = Float(vs, "SpreadRadius", 4f, 0.5f, 10f, "Enhanced: how far (units) from the grab the vines spread to other enemies." + next);
            vsSpreadMax = Int(vs, "SpreadMaxEnemies", 5, 0, 12, "Enhanced: most enemies the vines spread to." + next);
            vsSpreadLinger = Float(vs, "SpreadLinger", 3f, 0f, 10f, "Enhanced: seconds the spread vines keep enemies snared after the kick." + next);
            vsSnareInterval = Float(vs, "SnareInterval", 0.5f, 0.1f, 3f, "Enhanced: seconds between the snares' hits." + next);
            vsSnareSize = Float(vs, "SnareSize", 0.6f, 0.2f, 3f, "Enhanced: size of each snare hit." + next);
            const string vsb = "Vine Slingshot Balance";
            vsKickDamage = Int(vsb, "KickDamage", 14, 0, 200, "Damage of the kick." + restart);
            vsGripDamage = Int(vsb, "GripDamage", 3, 0, 100, "Damage of each grip hit while you hold." + restart);
            vsSnareDamage = Int(vsb, "SnareDamage", 2, 0, 100, "Enhanced: damage of each snare hit." + restart);
            vsKickKnockback = Float(vsb, "KickKnockback", 45f, 0f, 200f, "How hard the kick knocks enemies away." + restart);
            vsKickHitStun = Float(vsb, "KickHitStun", 1.2f, 0f, 5f, "Hit stun of the kick, times the game's normal." + restart);
            vsGripHitStun = Float(vsb, "GripHitStun", 1.5f, 0f, 5f, "Hit stun of each grip hit, times the game's normal." + restart);
            vsSnareHitStun = Float(vsb, "SnareHitStun", 1f, 0f, 5f, "Enhanced: hit stun of each snare hit, times the game's normal." + restart);
            vsSnareRoot = Float(vsb, "SnareRootDuration", 0.6f, 0f, 5f, "Enhanced: seconds each snare hit roots an enemy." + restart);

            // Charged Leap.
            const string cl = "Charged Leap";
            clLaunchSpeed = Float(cl, "LaunchSpeed", 22f, 5f, 60f, "Slowest the caught projectiles fly when flung (units a second)." + next);
            clFanDegrees = Float(cl, "FanDegrees", 8f, 0f, 45f, "Degrees between the flung projectiles in their fan." + next);
            clReflectDamage = Float(cl, "ReflectDamage", 1.5f, 0.1f, 5f, "Damage of the flung projectiles, times their own." + next);
            clBurstSize = Float(cl, "BurstSize", 1.2f, 0.3f, 5f, "Size of the lightning burst where you land after a tap." + next);
            clBurstChargeSize = Float(cl, "BurstChargeSize", 0.4f, 0f, 4f, "Size the landing burst gains at a full charge, on top of BurstSize." + next);
            clEnhancedBurst = Float(cl, "EnhancedBurstScale", 1.5f, 1f, 4f, "Enhanced: the landing burst's size, times the normal one." + next);
            clFieldRadius = Float(cl, "FieldRadius", 2f, 0.5f, 6f,
                "Only without the game's Mag Sphere: radius (units) of the stand-in field that catches projectiles." + next);
            clFieldChargeRadius = Float(cl, "FieldChargeRadius", 1.5f, 0f, 6f,
                "Only without the Mag Sphere: radius the stand-in field gains at a full charge." + next);
            clMaxCaught = Int(cl, "MaxCaught", 20, 1, 60, "Only without the Mag Sphere: most projectiles the stand-in field holds." + next);
            const string clb = "Charged Leap Balance";
            clBurstDamage = Int(clb, "BurstDamage", 10, 0, 200, "Damage of the lightning burst where you land (also of a caught projectile the game can't reflect)." + restart);
            clBurstKnockback = Float(clb, "BurstKnockback", 25f, 0f, 200f, "Knockback of the landing burst." + restart);
            clHitStun = Float(clb, "HitStun", 1.2f, 0f, 5f, "Hit stun of the landing burst, times the game's normal." + restart);
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        private ConfigEntry<int> Int(string section, string key, int value, int min, int max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<int>(min, max)));

        private Sprite LoadIcon(string fileName)
        {
            string resourceName = $"{nameof(WoLSlingshotDash)}.Assets.{fileName}";
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
