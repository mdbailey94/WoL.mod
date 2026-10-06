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
        public const string PluginVersion = "0.9.0";

        private static ManualLogSource log;
        private static ConfigEntry<string> chargeAnimation;
        private static ConfigEntry<float> chargePoseFrame;
        private static ConfigEntry<float> hopDistance;
        private static ConfigEntry<float> maxHold;
        private static ConfigEntry<float> cooldownSeconds;
        private ConfigEntry<float> dashCooldownSeconds;
        private ConfigEntry<float> blazingDrag;
        private static ConfigEntry<float> frostFreezeRadius;
        private ConfigEntry<bool> modEnabled;

        private static SlingshotDashPlugin instance;

        public static void Log(string message) => log?.LogInfo(message);

        // Runs effects that outlive the dash state (e.g. Blazing Slingshot's vacuum).
        public static void Run(IEnumerator routine)
        {
            if (instance != null)
                instance.StartCoroutine(routine);
        }

        public static float ChargePoseFrame => Mathf.Clamp01(chargePoseFrame?.Value ?? 0.4f);
        public static float HopDistance => Mathf.Max(0f, hopDistance?.Value ?? 1.5f);
        // 0 = hold as long as you like.
        public static float MaxHoldSeconds => Mathf.Max(0f, maxHold?.Value ?? 0f);
        public static float SlingshotCooldown => Mathf.Max(0.5f, cooldownSeconds?.Value ?? 7f);
        public static float FrostFreezeRadius => Mathf.Max(0.25f, frostFreezeRadius?.Value ?? 1f);

        // The game's own player animation for the chosen name, or null for "None".
        public static string ChargeAnimation(Player player)
        {
            switch ((chargeAnimation?.Value ?? "Jump").Trim().ToLowerInvariant())
            {
                case "none": return null;
                case "charge": return player.ChargeAnimStr;
                case "slide": return player.SlideAnimStr;
                case "hurt": return player.HurtAnimStr;
                case "kick": return player.KickAnimStr;
                case "parry": return player.ParryAnimStr;
                case "slam": return player.GSlamAnimStr;
                case "fall": return player.FallAnimStr;
                default: return player.JumpAnimStr;
            }
        }

        private void Awake()
        {
            log = Logger;
            instance = this;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer the Slingshot arcana (Air, Fire and Water) in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            chargeAnimation = Config.Bind("Charge", "Animation", "Jump",
                new ConfigDescription("Animation for the backward hop and the held pose while charging.",
                    new AcceptableValueList<string>("Jump", "Charge", "Slide", "Hurt", "Kick", "Parry", "Slam", "Fall", "None")));
            chargePoseFrame = Config.Bind("Charge", "PoseFrame", 0.4f,
                new ConfigDescription("How far into the animation the held pose is (0 = first frame, 1 = last).",
                    new AcceptableValueRange<float>(0f, 1f)));
            hopDistance = Config.Bind("Charge", "HopDistance", 1.5f,
                new ConfigDescription("How far the backward hop goes when you start charging (0 = no hop).",
                    new AcceptableValueRange<float>(0f, 4f)));
            maxHold = Config.Bind("Charge", "MaxHoldSeconds", 0f,
                new ConfigDescription("Launch automatically after holding this long (0 = hold as long as you " +
                    "like). The charge is full after 1 second either way.",
                    new AcceptableValueRange<float>(0f, 10f)));
            cooldownSeconds = Config.Bind("Balance", "CooldownSeconds", 7f,
                new ConfigDescription("How long until you can slingshot again (one charge). Until then the " +
                    "dash button is a normal dash.",
                    new AcceptableValueRange<float>(0.5f, 30f)));
            dashCooldownSeconds = Config.Bind("Balance", "DashCooldownSeconds", 0.6f,
                new ConfigDescription("Cooldown of the normal dash these arcana do when not slingshotting. " +
                    "Applies the next time the game starts.",
                    new AcceptableValueRange<float>(0.1f, 5f)));
            // Replaces BlazingPathPull (default 12, too weak to notice) with a stronger default.
            blazingDrag = Config.Bind("Balance", "BlazingDrag", 35f,
                new ConfigDescription("How hard Blazing Slingshot's trail drags enemies along your dash. " +
                    "Make it negative if they get pushed the wrong way. Applies the next time the game starts.",
                    new AcceptableValueRange<float>(-80f, 80f)));
            frostFreezeRadius = Config.Bind("Balance", "FrostFreezeRadius", 1f,
                new ConfigDescription("Radius of Frost Slingshot's freezes at a 0.2 s hold; a full charge adds " +
                    "half again.",
                    new AcceptableValueRange<float>(0.25f, 4f)));

            Skills.Register(new SkillInfo
            {
                ID = SlingshotDashState.staticID,
                displayName = "Slingshot",
                description = "Hold to pull back and charge, then release to launch yourself across the room, blasting enemies away where you leave and land!",
                enhancedDescription = "Launch sends out a bigger burst!",
                icon = LoadIcon("icon.png"),
                tier = 2,
                stateType = typeof(SlingshotDashState),
                skillStats = new SkillStats
                {
                    ID = new[] { SlingshotDashState.staticID },
                    elementType = new[] { "Air" },
                    subElementType = new[] { "Air" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Level 1 is the launch burst, level 2 the landing burst: at most 16 per enemy.
                    damage = new[] { 10, 6 },
                    cooldown = new[] { dashCooldownSeconds.Value },
                    // Positive knockback pushes enemies away; gentler where you land.
                    knockbackMultiplier = new[] { 55f, 22f },
                    hitStunDurationModifier = new[] { 1.2f },
                    sameAttackImmunityTime = new[] { 0.25f }
                },
                priceMultiplier = 3,
                unlockCondition = () => modEnabled.Value
            });

            Skills.Register(new SkillInfo
            {
                ID = BlazingSlingshotState.staticID,
                displayName = "Blazing Slingshot",
                description = "Hold to pull back and charge, then release to blitz across the room in flames, leaving a vacuum that sucks enemies in!",
                enhancedDescription = "A hotter trail and a stronger vacuum!",
                icon = LoadIcon("icon_fire.png"),
                tier = 2,
                stateType = typeof(BlazingSlingshotState),
                skillStats = new SkillStats
                {
                    ID = new[] { BlazingSlingshotState.staticID },
                    elementType = new[] { "Fire" },
                    subElementType = new[] { "Fire" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // Level 1 is the trail, level 2 the vacuum's pulses where you land. About
                    // 3 trail hits x4 + 4 pulses x2 = 20 at most per enemy, plus burn.
                    damage = new[] { 4, 2 },
                    cooldown = new[] { dashCooldownSeconds.Value },
                    // Level 1 (the trail) knocks enemies along the dash direction, set on each burst,
                    // so they get dragged with you; level 2 (the vacuum) pulls toward its centre.
                    knockbackMultiplier = new[] { blazingDrag.Value, -32f },
                    knockbackOverwrite = new[] { true, false },
                    hitStunDurationModifier = new[] { 1.2f },
                    // Short, so the trail and vacuum keep grabbing enemies.
                    sameAttackImmunityTime = new[] { 0.12f }
                },
                priceMultiplier = 3,
                unlockCondition = () => modEnabled.Value
            });

            Skills.Register(new SkillInfo
            {
                ID = FrostSlingshotState.staticID,
                displayName = "Frost Slingshot",
                description = "Hold to pull back and charge, then release to throw an ice feint and swap places with it, freezing enemies where you stood!",
                enhancedDescription = "A longer throw and a colder burst!",
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

            try
            {
                new Harmony(PluginGuid).CreateClassProcessor(typeof(SlingshotHud)).Patch();
            }
            catch (System.Exception e)
            {
                Logger.LogError($"Slingshot HUD hook failed to install: {e.Message}");
            }

            Logger.LogInfo($"{PluginName} {PluginVersion} registered Slingshot, Blazing Slingshot and Frost Slingshot");
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
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }
    }
}
