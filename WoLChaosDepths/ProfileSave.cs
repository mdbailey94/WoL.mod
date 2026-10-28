using System;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WoLChaosDepths
{
    /// <summary>
    /// Gives the Chaos Depths profile its own copy of the game's save (unlocks, loadout, gold,
    /// gems, settings), so a profile session never rewrites the base game's loadout and the base
    /// game's mod arcana never go missing from a profile save.
    ///
    /// The game builds every save path from GameDataManager.basePathStr, a private field it only
    /// fills (with Application.persistentDataPath) when it is still empty. Filling it first with
    /// "<persistentDataPath>/ChaosDepths/" moves every file the game reads or writes, without
    /// patching any of the save code. The first time the profile loads, the base save is copied
    /// in as its starting point, so nothing has to be unlocked twice.
    ///
    /// Two saves (#31), picked on the title screen: "Fresh", a new game save (only the settings
    /// file is carried over) with its own Chaos Depths characters, where every arcana, mod ones
    /// too, is bought in the shop as on any new save; and "Debugging", the save the profile used
    /// up to 0.13.6 (folder ChaosDepths, characters in BepInEx\config\ChaosDepths), kept as it is
    /// for testing.
    /// </summary>
    public static class ProfileSave
    {
        private const string DebugFolder = "ChaosDepths", FreshFolder = "ChaosDepthsFresh";
        private const string SettingsFile = "GameSettings.gs";
        // Read by Unlock Mod Content, which leaves a fresh save's mod arcana locked.
        public const string FreshSaveVariable = "CHAOSDEPTHS_FRESH_SAVE";
        private static readonly string[] Files = { "GameData.gd", "GameDataChaos.gd", "GameSettings.gs", "GameVars.gv" };

        private static ConfigEntry<bool> separate;
        private static ConfigEntry<bool> seed;
        private static ConfigEntry<string> slot;
        private static ManualLogSource log;
        private static FieldInfo basePath;
        private static string baseRoot;   // the game's own save root, with trailing slash
        private static string profileRoot;
        private static bool seeded;

        public static bool Active { get; private set; }

        // The fresh save is in use (only ever in the profile).
        public static bool Fresh => Active && IsFresh(slot.Value);

        public static string SaveName => Fresh ? "FRESH SAVE" : "DEBUGGING SAVE";

        private static bool IsFresh(string value) => !string.Equals((value ?? "").Trim(), "Debugging", StringComparison.OrdinalIgnoreCase);

        private static string RootFor(bool fresh) => baseRoot + (fresh ? FreshFolder : DebugFolder) + "/";

        public static void Bind(ConfigFile config)
        {
            separate = config.Bind("Profile", "SeparateSave", true,
                "In the profile, keep the game's own save (unlocks, loadout, gold, gems, settings) in its own folder, so a profile session never changes the base game's save. The folder is ChaosDepths beside the game's save folder.");
            seed = config.Bind("Profile", "SeedFromBaseSave", true,
                "The first time the profile runs, copy the base game's save into the profile's folder as its starting point (the Debugging save only).");
            slot = config.Bind("Profile", "Save", "Fresh",
                "Which save the profile plays: Fresh (a new save, arcana bought in the shop, its own characters) or Debugging (the save used for testing). Switched on the title screen with Tab or LB; remembered here.");
        }

        /// <summary>Called from Awake, before the game touches its save. Only acts in the profile.</summary>
        public static void Install(Harmony harmony, ManualLogSource logger)
        {
            log = logger;
            if (!ChaosDepthsPlugin.InProfile || !separate.Value) return;
            try
            {
                baseRoot = Application.persistentDataPath + "/";
                profileRoot = RootFor(IsFresh(slot.Value));
                var field = AccessTools.Field(typeof(GameDataManager), "basePathStr");
                if (field == null)
                {
                    log.LogWarning("Profile save: GameDataManager.basePathStr not found; the game save stays shared");
                    return;
                }
                var current = field.GetValue(null) as string;
                if (!string.IsNullOrEmpty(current) && current != baseRoot)
                {
                    log.LogWarning($"Profile save: the game already uses {current}; leaving it");
                    return;
                }
                Directory.CreateDirectory(profileRoot);
                field.SetValue(null, profileRoot);
                basePath = field;
                Active = true;
                MarkFresh();
                // Copy the base save in once, when the game first loads it (by then the Steam ID
                // folder name is known).
                foreach (var name in new[] { "LoadInitial", "Load", "LoadChaosData", "LoadSettings", "LoadGameVars" })
                {
                    var m = AccessTools.Method(typeof(GameDataManager), name);
                    if (m != null) harmony.Patch(m, new HarmonyMethod(typeof(ProfileSave), nameof(BeforeLoad)));
                }
                log.LogInfo($"Profile save: the game's save files live in {profileRoot}");
            }
            catch (Exception e)
            {
                log.LogError($"Profile save failed to install ({e.Message}); the game save stays shared");
            }
        }

        // Tells Unlock Mod Content (through the process's environment) whether the fresh save is on.
        private static void MarkFresh()
        {
            try
            {
                Environment.SetEnvironmentVariable(FreshSaveVariable, Fresh ? "1" : null);
            }
            catch
            {
            }
        }

        /// <summary>
        /// On the title screen: switch to the other save. The game reads every save file through
        /// basePathStr, so pointing it at the other folder and running the game's own first load
        /// again loads that save, as if the game had started with it.
        /// </summary>
        public static bool Switch()
        {
            if (!Active || basePath == null)
                return false;
            try
            {
                bool toFresh = !Fresh;
                profileRoot = RootFor(toFresh);
                Directory.CreateDirectory(profileRoot);
                if (toFresh)
                    PrepareFresh(GameDataManager.steamIDStr ?? "");
                basePath.SetValue(null, profileRoot);
                slot.Value = toFresh ? "Fresh" : "Debugging";
                MarkFresh();
                GameDataManager.LoadInitial(true);
                log.LogInfo($"Profile save: switched to the {SaveName.ToLowerInvariant()} ({profileRoot})");
                return true;
            }
            catch (Exception e)
            {
                log.LogError($"Profile save: couldn't switch saves ({e.Message})");
                return false;
            }
        }

        // A new fresh save takes only the settings (controls, screen, sound) from the Debugging
        // save, or the base game's; everything else starts as on a new game.
        private static void PrepareFresh(string id)
        {
            var dst = RootFor(true) + id;
            if (File.Exists(dst + SettingsFile))
                return;
            foreach (var src in new[] { RootFor(false) + id, baseRoot + id })
            {
                if (!File.Exists(src + SettingsFile))
                    continue;
                Directory.CreateDirectory(dst);
                File.Copy(src + SettingsFile, dst + SettingsFile, false);
                log.LogInfo($"Profile save: the fresh save takes its settings from {src}");
                return;
            }
        }

        private static void BeforeLoad()
        {
            if (seeded || !Active) return;
            seeded = true;
            if (Fresh)
            {
                try
                {
                    PrepareFresh(GameDataManager.steamIDStr ?? "");
                    log.LogInfo($"Profile save: playing the fresh save ({profileRoot})");
                }
                catch (Exception e)
                {
                    log.LogWarning($"Profile save: couldn't carry the settings over ({e.Message})");
                }
                return;
            }
            if (!seed.Value) return;
            try
            {
                var id = GameDataManager.steamIDStr ?? "";   // "<accountid>/" once Steam is up
                var src = baseRoot + id;
                var dst = profileRoot + id;
                if (File.Exists(dst + "GameData.gd"))
                {
                    log.LogInfo($"Profile save: using {dst}");
                    return;
                }
                if (!File.Exists(src + "GameData.gd"))
                {
                    log.LogInfo($"Profile save: no base save at {src} to copy; the profile starts fresh");
                    return;
                }
                Directory.CreateDirectory(dst);
                int copied = 0;
                foreach (var f in Files)
                {
                    if (!File.Exists(src + f)) continue;
                    File.Copy(src + f, dst + f, false);
                    copied++;
                }
                log.LogInfo($"Profile save: copied {copied} file(s) of the base save into {dst} as the profile's starting point");
            }
            catch (Exception e)
            {
                log.LogWarning($"Profile save: couldn't copy the base save ({e.Message}); the profile starts fresh");
            }
        }
    }
}
