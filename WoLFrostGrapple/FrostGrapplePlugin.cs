using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using LegendAPI;
using UnityEngine;

namespace WoLFrostGrapple
{
    // Frost Grapple (Matt, 2026-11-04 11:31, approved 11:32 with "make signature cinematic as
    // well"): a Water standard arcana. Throw an ice pickaxe along your aim; if it bites an enemy you
    // are pulled to them and land in an ice burst; a wall pulls you to the wall; a miss costs half
    // the cooldown. The signature chains the pick through up to 3 enemies, cinematic.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class FrostGrapplePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.frostgrapple";
        public const string PluginName = "Frost Grapple";
        public const string PluginVersion = "0.1.1";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled, signatureWithArcana;

        public static void Log(string message) => log?.LogInfo(message);

        // [Throw], [Pull], [Enhanced], [Signature] apply the next cast; [Balance] after a restart.
        private static ConfigEntry<float> throwSpeed, range, pullSpeed, maxPull, landGap, burstRadius, enhancedRadius, missRefund;
        private static ConfigEntry<int> sigTargets;
        private static ConfigEntry<float> sigSearch, sigThrowSpeed, sigFinaleRadius;
        private static ConfigEntry<bool> cinematic;
        private static ConfigEntry<float> stageDarken, cameraZoom, impactFlash, impactSlowMo;
        private static ConfigEntry<int> hitStopFrames, stopHitStop;
        private static ConfigEntry<int> pickDamage, burstDamage, sigStopDamage, sigFinaleDamage;
        private static ConfigEntry<float> chill, freeze, sigFreeze, burstKnockback, cooldown;

        public static float ThrowSpeed => Get(throwSpeed, 20f, 4f, 60f);
        public static float Range => Get(range, 7f, 1f, 15f);
        public static float PullSpeed => Get(pullSpeed, 24f, 4f, 60f);
        public static float MaxPull => Get(maxPull, 0.5f, 0.1f, 2f);
        public static float LandGap => Get(landGap, 0.6f, 0f, 2f);
        public static float BurstRadius => Get(burstRadius, 1.2f, 0.3f, 5f);
        public static float EnhancedRadius => Get(enhancedRadius, 1.56f, 0.3f, 5f);
        public static float MissRefund => Get(missRefund, 0.5f, 0f, 1f);
        public static float Cooldown => Get(cooldown, 5f, 0.5f, 60f);
        public static int SignatureTargets => Mathf.Clamp(sigTargets?.Value ?? 3, 1, 8);
        public static float SignatureSearch => Get(sigSearch, 6f, 1f, 15f);
        public static float SignatureThrowSpeed => Get(sigThrowSpeed, 30f, 4f, 80f);
        public static float SignatureFinaleRadius => Get(sigFinaleRadius, 1.8f, 0.3f, 6f);
        public static bool Cinematic => cinematic?.Value ?? true;
        public static float StageDarken => Get(stageDarken, 0.55f, 0f, 1f);
        public static float CameraZoom => Get(cameraZoom, 0.9f, 0.7f, 1f);
        public static int HitStopFrames => Mathf.Clamp(hitStopFrames?.Value ?? 5, 0, 12);
        public static int StopHitStop => Mathf.Clamp(stopHitStop?.Value ?? 2, 0, 8);
        public static float ImpactFlash => Get(impactFlash, 0.8f, 0f, 1f);
        public static float ImpactSlowMo => Get(impactSlowMo, 0.35f, 0f, 1f);

        private static float Get(ConfigEntry<float> e, float fallback, float min, float max) => Mathf.Clamp(e?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Frost Grapple in the arcana shop (also in the title screen Mods menu). Turning it off doesn't remove it from a run where you already have it.");
            signatureWithArcana = Config.Bind("General", "SignatureUnlocksWithArcana", true,
                "Unlock the signature version along with the arcana (not in Chaos Depths' fresh saves, where the signature is its own shop card).");
            BindSettings();
            SignatureUnlock.Install(new Harmony(PluginGuid), FrostGrappleState.staticID, () => signatureWithArcana.Value, m => Log("Frost Grapple: " + m));

            Skills.Register(new SkillInfo
            {
                ID = FrostGrappleState.staticID,
                displayName = "Frost Grapple",
                description = "Hurl an ice pickaxe! It bites the first foe and drags you to them in a burst of frost. Hits a wall? Off you go!",
                enhancedDescription = "A wider frost burst! Signature: the pick leaps from foe to foe and you with it!",
                icon = LoadIcon("frost_grapple_icon.png"),
                tier = 2,
                stateType = typeof(FrostGrappleState),
                skillStats = new SkillStats
                {
                    ID = new[] { FrostGrappleState.staticID },
                    elementType = new[] { "Water" },
                    subElementType = new[] { "Water" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Levels: 1 the pick biting, 2 the arrival burst (standard and enhanced; the
                    // enhanced one is only wider), 3 a signature stop, 4 the signature finale.
                    damage = new[] { Clamp(pickDamage, 300), Clamp(burstDamage, 300), Clamp(sigStopDamage, 300), Clamp(sigFinaleDamage, 500) },
                    cooldown = new[] { Cooldown },
                    knockbackMultiplier = new[] { 2f, Mathf.Clamp(burstKnockback.Value, 0f, 200f), Mathf.Clamp(burstKnockback.Value, 0f, 200f), Mathf.Clamp(burstKnockback.Value * 1.5f, 0f, 300f) },
                    hitStunDurationModifier = new[] { 0.8f, 1.2f, 1.1f, 1.5f },
                    showDamageNumber = new[] { true, true, true, true },
                    slowChance = new[] { Mathf.Clamp01(chill.Value), 0f, 0f, 0f },
                    slowLevel = new[] { 1 },
                    freezeChance = new[] { 0f, Mathf.Clamp01(freeze.Value), Mathf.Clamp01(freeze.Value), Mathf.Clamp01(sigFreeze.Value) },
                    shakeCamera = new[] { false, true, true, true },
                    sameAttackImmunityTime = new[] { 0.1f }
                },
                priceMultiplier = 3,
                unlockCondition = () => modEnabled.Value
            });
            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private void BindSettings()
        {
            throwSpeed = Float("Throw", "Speed", 20f, 4f, 60f, "How fast the pickaxe flies, in units a second.");
            range = Float("Throw", "Range", 7f, 1f, 15f, "How far the pickaxe flies before it comes back.");
            missRefund = Float("Throw", "MissCooldownShare", 0.5f, 0f, 1f, "Share of the cooldown spent when the pick hits nothing (0.5 = half).");
            pullSpeed = Float("Pull", "Speed", 24f, 4f, 60f, "How fast you're pulled along the chain, units a second.");
            maxPull = Float("Pull", "MaxSeconds", 0.5f, 0.1f, 2f, "The longest a pull lasts (a fleeing enemy can't drag you across the room).");
            landGap = Float("Pull", "LandGap", 0.6f, 0f, 2f, "You stop this far short of the enemy's body.");
            burstRadius = Float("Pull", "BurstRadius", 1.2f, 0.3f, 5f, "Radius of the ice burst where you land.");
            enhancedRadius = Float("Enhanced", "BurstRadius", 1.56f, 0.3f, 5f, "Radius of the landing burst when enhanced (30% wider: the one upgrade).");

            sigTargets = Config.Bind("Signature", "Targets", 3, new ConfigDescription("Enemies the signature pick leaps through.", new AcceptableValueRange<int>(1, 8)));
            sigSearch = Float("Signature", "LeapRange", 6f, 1f, 15f, "How far the pick looks for the next enemy from each stop.");
            sigThrowSpeed = Float("Signature", "ThrowSpeed", 30f, 4f, 80f, "How fast the signature pick flies between enemies.");
            sigFinaleRadius = Float("Signature", "FinaleRadius", 1.8f, 0.3f, 6f, "Radius of the finale burst at the last enemy.");
            cinematic = Config.Bind("Signature", "Cinematic", true, "The signature's camera, light and time effects below.");
            stageDarken = Float("Signature", "StageDarken", 0.55f, 0f, 1f, "Stage light left while the chain runs (1 = no darkening).");
            cameraZoom = Float("Signature", "CameraZoom", 0.9f, 0.7f, 1f, "Camera size during the chain (0.9 = 10% in).");
            stopHitStop = Config.Bind("Signature", "StopHitStopFrames", 2, new ConfigDescription("Frames frozen at each stop on the way.", new AcceptableValueRange<int>(0, 8)));
            hitStopFrames = Config.Bind("Signature", "FinaleHitStopFrames", 5, new ConfigDescription("Frames frozen at the finale.", new AcceptableValueRange<int>(0, 12)));
            impactFlash = Float("Signature", "FinaleFlash", 0.8f, 0f, 1f, "White flash at the finale (0 = none).");
            impactSlowMo = Float("Signature", "FinaleSlowMo", 0.35f, 0f, 1f, "Seconds of slow-motion after the finale (0 = none).");

            const string r = " Applies after restarting the game.";
            pickDamage = Int("Balance", "PickDamage", 8, "Damage when the pick bites." + r);
            burstDamage = Int("Balance", "BurstDamage", 16, "Damage of the landing burst." + r);
            sigStopDamage = Int("Balance", "SignatureStopDamage", 12, "Damage of each signature stop's burst." + r);
            sigFinaleDamage = Int("Balance", "SignatureFinaleDamage", 24, "Damage of the signature finale burst." + r);
            chill = Float("Balance", "PickChill", 0.3f, 0f, 1f, "Chance the pick chills (slows)." + r);
            freeze = Float("Balance", "BurstFreeze", 0.4f, 0f, 1f, "Chance the landing burst freezes." + r);
            sigFreeze = Float("Balance", "FinaleFreeze", 0.6f, 0f, 1f, "Chance the finale freezes." + r);
            burstKnockback = Float("Balance", "BurstKnockback", 14f, 0f, 200f, "Knockback of the landing burst (finale 1.5x)." + r);
            cooldown = Float("Balance", "Cooldown", 5f, 0.5f, 60f, "Cooldown in seconds." + r);
        }

        private static int Clamp(ConfigEntry<int> e, int max) => Mathf.Clamp(e.Value, 0, max);

        private ConfigEntry<float> Float(string s, string k, float v, float min, float max, string d) =>
            Config.Bind(s, k, v, new ConfigDescription(d, new AcceptableValueRange<float>(min, max)));

        private ConfigEntry<int> Int(string s, string k, int v, string d) =>
            Config.Bind(s, k, v, new ConfigDescription(d, new AcceptableValueRange<int>(0, 500)));

        internal static Texture2D LoadTexture(string fileName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{nameof(WoLFrostGrapple)}.Assets.{fileName}"))
            {
                if (stream == null)
                    return null;
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                t.LoadImage(data);
                return t;
            }
        }

        private Sprite LoadIcon(string file)
        {
            Texture2D t = LoadTexture(file);
            if (t == null)
            {
                Logger.LogInfo("Frost Grapple: no icon yet (WoL Art's frost_grapple_icon.png), using the default");
                return null;
            }
            return GameIcon.Create(t, Logger);
        }
    }
}
