using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WoLCustomPaintings
{
    // Hangs your own pictures in the Chaos Trials: some of the breakable wall paintings show a
    // random PNG from the Paintings folder instead, fitted inside the game's own frame.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class CustomPaintingsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.custompaintings";
        public const string PluginName = "Custom Paintings";
        public const string PluginVersion = "0.1.1";

        private static ManualLogSource log;
        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<float> chance;
        private static ConfigEntry<int> frameInset;
        private static readonly List<Texture2D> pictures = new List<Texture2D>();

        public static int FrameInset => frameInset != null ? frameInset.Value : 0;

        public static void Log(string message) => log?.LogInfo(message);

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Show your pictures on some of the paintings in the trials (also in the title screen Mods menu).");
            // A new key (it was "Chance", 0-1) so the new default replaces an already saved 0.35.
            chance = Config.Bind("General", "ChancePercent", 0.5f,
                new ConfigDescription("Percent of paintings that show one of your pictures (0-100). " +
                    "The default 0.5 makes them a rare find.",
                    new AcceptableValueRange<float>(0f, 100f)));
            frameInset = Config.Bind("General", "FrameInset", 0,
                new ConfigDescription("Pixels of the original painting's frame to keep around your picture " +
                    "(0 = automatic). Raise it if your picture covers the frame, lower it if old canvas shows.",
                    new AcceptableValueRange<int>(0, 32)));

            LoadPictures();
            try
            {
                new Harmony(PluginGuid).CreateClassProcessor(typeof(PaintingPatch)).Patch();
            }
            catch (Exception e)
            {
                Logger.LogError($"Painting hook failed to install: {e.Message}");
            }
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded with {pictures.Count} picture(s)");
        }

        private void LoadPictures()
        {
            string folder = Path.Combine(Path.GetDirectoryName(Info.Location), "Paintings");
            try
            {
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                foreach (string file in Directory.GetFiles(folder, "*.png"))
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                    if (texture.LoadImage(File.ReadAllBytes(file)))
                    {
                        pictures.Add(texture);
                        Logger.LogInfo($"Picture: {Path.GetFileName(file)} ({texture.width}x{texture.height})");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Couldn't read pictures from {folder}: {e.Message}");
            }
            if (pictures.Count == 0)
                Logger.LogInfo($"No pictures yet - put PNG files in {folder}");
        }

        [HarmonyPatch(typeof(Painting), nameof(Painting.Start))]
        private static class PaintingPatch
        {
            private static void Postfix(Painting __instance)
            {
                try
                {
                    if (!modEnabled.Value || pictures.Count == 0 || UnityEngine.Random.value * 100f >= chance.Value)
                        return;
                    Texture2D picture = pictures[UnityEngine.Random.Range(0, pictures.Count)];
                    __instance.gameObject.AddComponent<PaintingSwap>().picture = picture;
                }
                catch (Exception e)
                {
                    log?.LogError(e);
                }
            }
        }
    }
}
