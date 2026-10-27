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
    /// </summary>
    public static class ProfileSave
    {
        private const string Folder = "ChaosDepths";
        private static readonly string[] Files = { "GameData.gd", "GameDataChaos.gd", "GameSettings.gs", "GameVars.gv" };

        private static ConfigEntry<bool> separate;
        private static ConfigEntry<bool> seed;
        private static ManualLogSource log;
        private static string baseRoot;   // the game's own save root, with trailing slash
        private static string profileRoot;
        private static bool seeded;

        public static bool Active { get; private set; }

        public static void Bind(ConfigFile config)
        {
            separate = config.Bind("Profile", "SeparateSave", true,
                "In the profile, keep the game's own save (unlocks, loadout, gold, gems, settings) in its own folder, so a profile session never changes the base game's save. The folder is ChaosDepths beside the game's save folder.");
            seed = config.Bind("Profile", "SeedFromBaseSave", true,
                "The first time the profile runs, copy the base game's save into the profile's folder as its starting point.");
        }

        /// <summary>Called from Awake, before the game touches its save. Only acts in the profile.</summary>
        public static void Install(Harmony harmony, ManualLogSource logger)
        {
            log = logger;
            if (!ChaosDepthsPlugin.InProfile || !separate.Value) return;
            try
            {
                baseRoot = Application.persistentDataPath + "/";
                profileRoot = baseRoot + Folder + "/";
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
                Active = true;
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

        private static void BeforeLoad()
        {
            if (seeded || !Active) return;
            seeded = true;
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
