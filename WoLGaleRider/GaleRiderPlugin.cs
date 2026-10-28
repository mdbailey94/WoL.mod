using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLGaleRider
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class GaleRiderPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.galerider";
        public const string PluginName = "Gale Rider";
        public const string PluginVersion = "0.3.7";

        private static BepInEx.Logging.ManualLogSource log;
        private ConfigEntry<bool> modEnabled;

        public static void Log(string message) => log?.LogInfo(message);

        // ---- Settings (BepInEx\config\mdbailey94.wol.galerider.cfg) ----
        // [Ride], [Ball] and [Look] apply the next time you cast it; [Balance] after restarting the game.
        private static ConfigEntry<float> rideTime, enhancedRideTime, rideSpeedPercent, minRideTime;
        private static ConfigEntry<bool> hopOffOnPress;
        private static ConfigEntry<float> minSize, maxSize, enhancedMaxSize, peakSpeedPercent;
        private static ConfigEntry<float> vortexSize;
        private static ConfigEntry<bool> dust, drawnEffects, rideOnTop;
        private static ConfigEntry<string> ridePose, ballArt;
        public static string BallArt => ballArt?.Value ?? "Drawn";
        private static ConfigEntry<float> ridePoseFrame, rideSeat;
        private static ConfigEntry<int> damage;
        private static ConfigEntry<bool> hittable;
        private static ConfigEntry<float> knockback, peakKnockback, hitInterval, cooldown;

        public static float RideTime => Get(rideTime, 3f, 0.5f, 10f);
        public static float EnhancedRideTime => Get(enhancedRideTime, 4.5f, 0.5f, 12f);
        public static float RideSpeedPercent => Get(rideSpeedPercent, 40f, 0f, 150f);
        public static float MinRideTime => Get(minRideTime, 0.4f, 0f, 5f);
        public static bool HopOffOnPress => hopOffOnPress?.Value ?? true;
        public static float MinSize => Get(minSize, 0.8f, 0.3f, 5f);
        public static float MaxSize => Get(maxSize, 1.6f, 0.3f, 6f);
        public static float EnhancedMaxSize => Get(enhancedMaxSize, 2f, 0.3f, 6f);
        public static bool HittableWhileRiding => hittable?.Value ?? true;
        public static float PeakSpeedPercent => Get(peakSpeedPercent, 200f, 110f, 600f);
        public static float VortexSize => Get(vortexSize, 1f, 0.3f, 3f);
        public static bool Dust => dust?.Value ?? true;
        public static bool DrawnEffects => drawnEffects?.Value ?? true;
        public static bool RideOnTop => rideOnTop?.Value ?? true;
        public static string RidePose => ridePose?.Value ?? "Slide";
        public static float RidePoseFrame => ridePoseFrame?.Value ?? 0.5f;
        public static float RideSeat => rideSeat?.Value ?? 0.9f;
        // Balance numbers are read again by the state for the knockback it sets on the ball each
        // frame; the stats registered below are what the game keeps (restart to change those).
        public static float Knockback => Get(knockback, 15f, 0f, 200f);
        public static float PeakKnockback => Get(peakKnockback, 70f, 0f, 300f);
        public static float HitInterval => Get(hitInterval, 0.33f, 0.1f, 2f);

        private static float Get(ConfigEntry<float> entry, float fallback, float min, float max) =>
            Mathf.Clamp(entry?.Value ?? fallback, min, max);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer Gale Rider in the arcana shop (also in the title screen Mods menu). " +
                "Turning it off doesn't remove it from a run where you already have it.");
            BindSettings();

            Skills.Register(new SkillInfo
            {
                ID = GaleRiderState.staticID,
                displayName = "Gale Rider",
                description = "Summon a ball of wind and ride it! Steer with the stick at a boosted pace while the ball rolls over enemies, " +
                    "shoving them away in the direction you're heading. The faster you go, the bigger the ball and the harder the shove!",
                enhancedDescription = "Ride longer and the ball grows even larger!",
                icon = LoadIcon("icon.png"),
                // Between Tearing Whirlwind (tier 2) and Aero Ring / Rushing Typhoon (tier 4), the
                // vanilla Air rides it's built on.
                tier = 3,
                stateType = typeof(GaleRiderState),
                skillStats = new SkillStats
                {
                    ID = new[] { GaleRiderState.staticID },
                    elementType = new[] { "Air" },
                    subElementType = new[] { "Air" },
                    targetNames = new[] { "EnemyHurtBox", "DestructibleHurtBox" },
                    // One stat level: the ball's hit. Small damage (it's displacement and mobility),
                    // at most one hit every HitInterval per enemy, about 12 a second at the default.
                    damage = new[] { Mathf.Clamp(damage.Value, 0, 100) },
                    cooldown = new[] { Mathf.Clamp(cooldown.Value, 0.5f, 60f) },
                    // The walking-pace knockback; the state raises it with your speed, up to
                    // PeakKnockback, by overriding it on the ball every frame.
                    knockbackMultiplier = new[] { Mathf.Clamp(knockback.Value, 0f, 200f) },
                    // Shove in the direction you're riding (the state sets the ball's vector), not
                    // outward from the ball's centre.
                    knockbackOverwrite = new[] { true },
                    hitStunDurationModifier = new[] { 1f },
                    // Same-target: the ball is one attack object whose collider pulses to hit again,
                    // so this is what caps how often one enemy is hit.
                    sameTargetImmunityTime = new[] { Mathf.Clamp(hitInterval.Value, 0.1f, 2f) }
                },
                // Aero Ring and Rushing Typhoon are 6, Tearing Whirlwind 7.
                priceMultiplier = 6,
                // LegendAPI rerolls a shop offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });

            // 0.3.5 shrank the ball's default radii, but a config file written by an older
            // version keeps its stored values, so the old defaults are moved to the new ones
            // once; values the player changed themselves are left alone.
            if (Mathf.Approximately(minSize.Value, 1.2f) && Mathf.Approximately(maxSize.Value, 2.4f) && Mathf.Approximately(enhancedMaxSize.Value, 3f))
            {
                minSize.Value = 0.8f;
                maxSize.Value = 1.6f;
                enhancedMaxSize.Value = 2f;
                Logger.LogInfo("Gale Rider: [Ball] sizes were the pre-0.3.5 defaults (1.2 / 2.4 / 3); set to the new defaults 0.8 / 1.6 / 2");
            }
            Logger.LogInfo($"{PluginName} {PluginVersion} registered");
        }

        private void BindSettings()
        {
            rideTime = Float("Ride", "RideTime", 3f, 0.5f, 10f, "Seconds you ride the ball.");
            enhancedRideTime = Float("Ride", "EnhancedRideTime", 4.5f, 0.5f, 12f, "RideTime when enhanced.");
            rideSpeedPercent = Float("Ride", "RideSpeedPercent", 40f, 0f, 150f,
                "Move-speed bonus while riding, as a percentage (a multiplier on your move-speed stat, so speed relics " +
                "count). 40 matches a sprint, so you're never slower on the ball than running.");
            hopOffOnPress = Config.Bind("Ride", "HopOffOnPress", true,
                "Press the arcana's button again (after MinRideTime) to hop off early. Dashing always cancels the ride.");
            minRideTime = Float("Ride", "MinRideTime", 0.4f, 0f, 5f, "Seconds before you can hop off by pressing again.");

            // 0.3.5 shrank the ball by a third (1.2/2.4/3 before): the drawn ball is sized from
            // this radius, so the picture and the hit shrink together (task sheet #9/#10).
            minSize = Float("Ball", "MinSize", 0.8f, 0.3f, 5f, "Radius of the ball's hit at base run speed (x1.0). The drawn ball is sized to it.");
            maxSize = Float("Ball", "MaxSize", 1.6f, 0.3f, 6f, "Radius of the ball's hit at the peak speed (see PeakSpeedPercent).");
            enhancedMaxSize = Float("Ball", "EnhancedMaxSize", 2f, 0.3f, 6f, "MaxSize when enhanced.");
            peakSpeedPercent = Float("Ball", "PeakSpeedPercent", 200f, 110f, 600f,
                "Speed, as a percentage of base run speed, at which the ball is biggest and shoves hardest. Base run speed " +
                "(100) is the smallest and weakest; it grows evenly between. Riding alone is 100 + RideSpeedPercent; " +
                "speed relics, the cloak and speed-boost arcana take you further.");

            vortexSize = Float("Look", "VortexSize", 1f, 0.3f, 3f, "Size of the swirling wind (the game's air vortex) drawn round you, relative to the ball. The ball's own picture always matches its hit area.");
            ballArt = Config.Bind("Look", "Ball", "Drawn",
                new ConfigDescription("The ball's picture: Drawn = the hand-drawn wind ball made for the mod (8 frames, near and far strokes round the wizard); Game = the game's own WindBall sprite when its sheet is loaded; Coded = the ball drawn in code.",
                    new AcceptableValueList<string>("Drawn", "Game", "Coded")));
            rideOnTop = Config.Bind("Look", "RideOnTop", true,
                "Crouch on top of the wind ball as it rolls under you: a crouched pose, held still, your feet on its top (RidePose, RidePoseFrame, RideSeat). Off: ride inside it as before.");
            // No AcceptableValueList here: BepInEx resets a value that isn't listed exactly (a
            // different case included) to the default on load, which looks like the edit vanishing.
            // The name is matched ignoring case in the state; an unknown one falls back to Slide.
            ridePose = Config.Bind("Look", "RidePose", "Slide",
                "The wizard's pose while riding, one of the game's own animations held on one frame: Slide (crouched low), GSlam (crouched, fist down), Jump (legs tucked), Fall, Charge, Dash, or Run (not held). Case doesn't matter; an unknown name means Slide.");
            ridePoseFrame = Float("Look", "RidePoseFrame", 0.5f, 0f, 1f,
                "Which moment of the pose's animation is held, 0 = its first frame, 1 = its last. Try a few: 0.5 of Slide is the full crouch.");
            rideSeat = Float("Look", "RideSeat", 0.9f, 0f, 1f,
                "Where your feet sit on the ball, as a fraction of its height: 1 = right on top of it, 0.9 = crouched on top with your feet just into its surface, 0.4 = your legs inside its top, 0 = on the ground inside it.");
            dust = Config.Bind("Look", "Dust", true, "Kick up a ring of dust as the ball rolls.");
            drawnEffects = Config.Bind("Look", "DrawnEffects", true,
                "Add the wind ball, floor ring, streaks, gust puffs and gather/unravel on top of the game's wind effects (0.2.0; " +
                "0.2.1 plays the game's own wind sprites for them where loaded). Off = the 0.1.0 look. Cosmetic only.");

            const string restart = " Applies after restarting the game.";
            hittable = Config.Bind("Balance", "HittableWhileRiding", true,
                "Enemies can hurt you while you ride. The mod never made you invulnerable itself; with this on it also clears any invulnerability the game puts on a riding wizard, and logs once if it found any.");
            damage = Config.Bind("Balance", "Damage", 4,
                new ConfigDescription("Damage of each hit of the ball." + restart, new AcceptableValueRange<int>(0, 100)));
            knockback = Float("Balance", "Knockback", 15f, 0f, 200f, "Knockback at base run speed (Aero Ring is 25).");
            peakKnockback = Float("Balance", "PeakKnockback", 70f, 0f, 300f, "Knockback at the peak speed (Rushing Typhoon's final shove is 70).");
            hitInterval = Float("Balance", "HitInterval", 0.33f, 0.1f, 2f,
                "Seconds between hits on the same enemy (0.33 = about three a second)." + restart);
            // 7 s: three seconds of boosted steering and a crowd-shoving ball; Aero Ring is 6, Tearing
            // Whirlwind 6.5, Rushing Typhoon 5.5.
            cooldown = Float("Balance", "Cooldown", 7f, 0.5f, 60f, "Cooldown in seconds (counting from the cast)." + restart);
        }

        private ConfigEntry<float> Float(string section, string key, float value, float min, float max, string description) =>
            Config.Bind(section, key, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

        private Sprite LoadIcon(string fileName)
        {
            string resourceName = $"{nameof(WoLGaleRider)}.Assets.{fileName}";
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
