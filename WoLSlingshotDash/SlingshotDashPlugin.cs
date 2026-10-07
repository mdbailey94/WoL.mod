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
        public const string PluginVersion = "0.22.2";

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

        private static SlingshotDashPlugin instance;

        public static void Log(string message) => log?.LogInfo(message);

        // Runs effects that outlive the dash state (e.g. Blazing Kick's vacuum).
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
                    damage = new[] { 4, 2, 3 },
                    cooldown = new[] { dashCooldownSeconds.Value },
                    // Level 1 knocks enemies along the dash (set on each burst), so they get
                    // dragged with you; level 2 pulls toward its centre; level 3 pushes away.
                    knockbackMultiplier = new[] { drag.Value, -32f, 18f },
                    knockbackOverwrite = new[] { true, false, false },
                    hitStunDurationModifier = new[] { 1.2f },
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
                    damage = new[] { 4, 10, 4, 10 },
                    cooldown = new[] { dashCooldownSeconds.Value },
                    // Positive knockback: the trail shoves enemies out of your way, the blast
                    // throws them hard.
                    knockbackMultiplier = new[] { 30f, 60f, 30f, 60f },
                    hitStunDurationModifier = new[] { 1.2f },
                    burnChance = new[] { 0f, 0f, 1f, 1f },
                    burnLevel = new[] { 1 },
                    // Short, so the trail keeps catching enemies and the blast still lands.
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
                    damage = new[] { 14, 3, 0, 2 },
                    cooldown = new[] { dashCooldownSeconds.Value },
                    // The kick knocks along your pull (set on the burst); the grip holds still.
                    knockbackMultiplier = new[] { 45f, 0f, 0f, 0f },
                    knockbackOverwrite = new[] { true, false, false, false },
                    hitStunDurationModifier = new[] { 1.2f, 1.5f, 0f, 1f },
                    showDamageNumber = new[] { true, true, false, true },
                    rootChance = new[] { 0f, 0f, 0f, 1f },
                    rootDuration = new[] { 0f, 0f, 0f, 0.6f },
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
                    damage = new[] { 10 },
                    cooldown = new[] { dashCooldownSeconds.Value },
                    knockbackMultiplier = new[] { 25f },
                    hitStunDurationModifier = new[] { 1.2f },
                    sameAttackImmunityTime = new[] { 0.3f }
                },
                priceMultiplier = 3,
                unlockCondition = () => modEnabled.Value
            });

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
