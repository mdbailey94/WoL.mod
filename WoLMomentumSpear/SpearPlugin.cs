using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using HarmonyLib;
using UnityEngine;

namespace WoLMomentumSpear
{
    [BepInPlugin(PluginGuid, "Momentum Spear", "0.1.1")]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public sealed class SpearPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.momentumspear";
        private static ConfigEntry<float> bonus, cap, reach, width, lunge, finisherLunge;
        private static ConfigEntry<bool> logThrusts;
        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> enabledSetting;
        private Texture2D iconTexture;
        private Sprite iconSprite;
        private Harmony harmony;
        internal static Material Material;
        public static float BonusPerSpeedUnit { get { return SpearRules.Bound(bonus != null ? bonus.Value : 0.1f, 0.1f, 0f, 1f); } }
        public static float MomentumCap { get { return SpearRules.Bound(cap != null ? cap.Value : 2f, 2f, 1f, 4f); } }
        public static float ReachScale { get { return SpearRules.Bound(reach != null ? reach.Value : 1.8f, 1.8f, 0.5f, 3f); } }
        public static float WidthScale { get { return SpearRules.Bound(width != null ? width.Value : 0.65f, 0.65f, 0.3f, 1.5f); } }
        public static float Lunge { get { return SpearRules.Bound(lunge != null ? lunge.Value : 4f, 4f, 0f, 20f); } }
        public static float FinisherLunge { get { return SpearRules.Bound(finisherLunge != null ? finisherLunge.Value : 8f, 8f, 0f, 20f); } }

        internal static void Trace(string message)
        {
            if (logThrusts != null && logThrusts.Value && log != null) log.LogInfo(message);
        }

        private void Awake()
        {
            enabledSetting = Config.Bind("General", "Enabled", true, "Offer Momentum Spear in the arcana shop. Existing equipped copies remain usable.");
            bonus = Config.Bind("Momentum", "BonusPerSpeedUnit", 0.1f, "Opening-hit bonus per unit of actual velocity, clamped to 0–1. 0.1 adds 10% per unit/second. Captured before attack movement is stopped.");
            cap = Config.Bind("Momentum", "MaximumMultiplier", 2f, "Opening-hit total multiplier cap, 1–4. Default 2 is at most double damage. Other hits are not momentum-scaled.");
            reach = Config.Bind("Spear", "ReachScale", 1.8f, "Native thrust hitbox length multiplier, 0.5–3. Enhanced adds 15%; finisher adds 25%. Applies to future attacks.");
            width = Config.Bind("Spear", "WidthScale", 0.65f, "Native thrust width multiplier, 0.3–1.5. Applies to future attacks.");
            lunge = Config.Bind("Spear", "Lunge", 4f, "Speed of the forward lunge on the first two thrusts, units a second, 0–20 (the game's melee basics default to 4). Applies after restarting the game.");
            finisherLunge = Config.Bind("Spear", "FinisherLunge", 8f, "Speed of the finisher's lunge, 0–20 (the game's melee basics default to 8). Applies after restarting the game.");
            logThrusts = Config.Bind("Debug", "LogThrusts", false, "Log every thrust that fires (its combo step, level and opening multiplier), to tell a thrust that missed from one that never fired.");
            log = Logger;
            int first = Damage("OpeningDamage", 8), second = Damage("SecondDamage", 10), final = Damage("FinisherDamage", 16);
            float firstPush = Push("OpeningKnockback", 2f), secondPush = Push("SecondKnockback", 2f), finalPush = Push("FinisherKnockback", 12f);
            try { iconSprite = CreateIcon(); }
            catch (System.Exception e) { Logger.LogWarning("Using default icon: " + e.Message); }
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) Material = new Material(shader);
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(SpearPlugin).Assembly);
            Skills.Register(new SkillInfo
            {
                ID = SpearState.staticID,
                displayName = "Momentum Spear",
                description = "Chain three spear thrusts! Build momentum before the opening thrust to strike harder, then finish with an extended piercing thrust!",
                enhancedDescription = "Longer reach on every thrust!",
                icon = iconSprite,
                tier = 1,
                stateType = typeof(SpearState),
                skillStats = new SkillStats
                {
                    ID = new[] { SpearState.staticID },
                    elementType = new[] { "Air" }, subElementType = new[] { "Air" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    damage = new[] { first, second, final },
                    cooldown = new[] { 0f },
                    knockbackMultiplier = new[] { firstPush, secondPush, finalPush },
                    sameAttackImmunityTime = new[] { 0.3f },
                    shakeCamera = new[] { false }
                },
                priceMultiplier = 2,
                unlockCondition = () => enabledSetting.Value
            });
            Logger.LogInfo("Momentum Spear registered: three-hit basic, actual-velocity opening bonus.");
        }

        private int Damage(string key, int value)
        {
            return Mathf.Clamp(Config.Bind("Balance", key, value, "Base damage, clamped to 1–100. Requires restart.").Value, 1, 100);
        }

        private float Push(string key, float value)
        {
            return SpearRules.Bound(Config.Bind("Balance", key, value, "Knockback, clamped to 0–60. Too much on the first two hits knocks the target out of the next thrust's reach. Requires restart.").Value, value, 0f, 60f);
        }

        private Sprite CreateIcon()
        {
            const int n = 32;
            iconTexture = new Texture2D(n, n) { filterMode = FilterMode.Point };
            Color[] pixels = new Color[n * n];
            for (int y = 2; y < 30; y++)
                for (int x = 2; x < 30; x++)
                {
                    bool shaft = x >= 15 && x <= 16 && y < 23;
                    bool head = y >= 20 && System.Math.Abs(x - 15.5f) <= (29 - y) * 0.5f;
                    pixels[y * n + x] = head ? new Color(0.5f, 0.95f, 1f, 1f) : shaft ? new Color(0.8f, 0.55f, 0.3f, 1f) : Color.clear;
                }
            iconTexture.SetPixels(pixels); iconTexture.Apply();
            return Sprite.Create(iconTexture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / (18f / 16f));
        }

        private void OnDestroy()
        {
            if (harmony != null) harmony.UnpatchSelf();
            if (Material != null) Destroy(Material);
            Material = null;
            if (iconSprite != null) Destroy(iconSprite);
            if (iconTexture != null) Destroy(iconTexture);
        }
    }
}
