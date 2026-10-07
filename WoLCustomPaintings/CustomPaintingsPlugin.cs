using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WoLCustomPaintings
{
    // Hangs your own pictures in the Chaos Trials: some of the breakable wall paintings show a
    // random PNG from the Paintings folder instead, fitted inside the game's own frame.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class CustomPaintingsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.custompaintings";
        public const string PluginName = "Custom Paintings";
        public const string PluginVersion = "0.3.0";

        private static ManualLogSource log;
        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<float> chance;
        private static ConfigEntry<int> frameInset;
        private static ConfigEntry<int> detail;
        private static readonly List<Texture2D> pictures = new List<Texture2D>();
        // Paintings already given (or not given) a picture in this scene, by instance id.
        private readonly HashSet<int> rolled = new HashSet<int>();
        private int sceneSeen, sceneFramed;
        private string sceneName;
        private float nextScan;

        public static int Detail => Mathf.Clamp(detail?.Value ?? 2, 1, 4);
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
            detail = Config.Bind("General", "Detail", 2,
                new ConfigDescription("How finely your picture is drawn inside the frame: 1 = the game's own pixel size " +
                    "(blockier, matches the game), 2-4 = more detail in the same space.",
                    new AcceptableValueRange<int>(1, 4)));
            frameInset = Config.Bind("General", "FrameInset", 0,
                new ConfigDescription("Only for paintings without a separate artwork layer: pixels of the original painting's frame to keep around your picture " +
                    "(0 = automatic). Raise it if your picture covers the frame, lower it if old canvas shows.",
                    new AcceptableValueRange<int>(0, 32)));

            LoadPictures();
            SceneManager.activeSceneChanged += (from, to) => NewScene();
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded with {pictures.Count} picture(s), " +
                $"{chance.Value}% of paintings");
        }

        // A new floor: re-read the config (so a changed ChancePercent counts without restarting)
        // and start counting again.
        private void NewScene()
        {
            ReportScene();
            // Pictures added while the game is running count from the next floor.
            if (pictures.Count == 0)
            {
                LoadPictures(true);
                if (pictures.Count > 0)
                    Logger.LogInfo($"Found {pictures.Count} new picture(s)");
            }
            rolled.Clear();
            sceneName = SceneManager.GetActiveScene().name;
            sceneSeen = 0;
            sceneFramed = 0;
            try
            {
                Config.Reload();
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Couldn't re-read the config: {e.Message}");
            }
        }

        private void ReportScene()
        {
            if (sceneSeen > 0)
                Logger.LogInfo($"{sceneName}: {sceneFramed} of {sceneSeen} painting(s) showed your pictures");
        }

        // Every half second, any painting not yet seen rolls the chance once.
        private void Update()
        {
            if (Time.unscaledTime < nextScan)
                return;
            nextScan = Time.unscaledTime + 0.5f;
            if (!modEnabled.Value || pictures.Count == 0)
                return;
            Painting[] paintings;
            try
            {
                paintings = FindObjectsOfType<Painting>();
            }
            catch (Exception e)
            {
                Logger.LogError($"Couldn't look for paintings: {e.Message}");
                nextScan = float.MaxValue;
                return;
            }
            foreach (Painting painting in paintings)
            {
                if (painting == null || !rolled.Add(painting.GetInstanceID()))
                    continue;
                sceneSeen++;
                if (painting.destroyed || UnityEngine.Random.value * 100f >= chance.Value)
                    continue;
                if (painting.GetComponent<PaintingSwap>() == null)
                    painting.gameObject.AddComponent<PaintingSwap>().picture = pictures[UnityEngine.Random.Range(0, pictures.Count)];
                sceneFramed++;
            }
        }

        // Reads the pictures in the Paintings folder and any folders inside it. Everything found is
        // logged, including files it can't use and why, so it's clear what the mod sees.
        private void LoadPictures(bool quiet = false)
        {
            string folder = Path.Combine(Path.GetDirectoryName(Info.Location), "Paintings");
            int skipped = 0;
            try
            {
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    string name = file.Substring(folder.Length).TrimStart('\\', '/');
                    string extension = Path.GetExtension(file).ToLowerInvariant();
                    if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
                    {
                        skipped++;
                        if (!quiet)
                            Logger.LogWarning($"Skipping {name}: only PNG and JPG pictures work (save it as one of those)");
                        continue;
                    }
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                    if (texture.LoadImage(File.ReadAllBytes(file)) && texture.width > 2)
                    {
                        pictures.Add(texture);
                        Logger.LogInfo($"Picture: {name} ({texture.width}x{texture.height})");
                    }
                    else
                    {
                        skipped++;
                        Logger.LogWarning($"Skipping {name}: it couldn't be read as a picture " +
                            "(it may be another kind of file with a .png or .jpg name)");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Couldn't read pictures from {folder}: {e.Message}");
            }
            if (pictures.Count == 0 && !quiet)
                Logger.LogWarning($"No pictures yet - put PNG or JPG files in {folder} " +
                    (skipped > 0 ? $"({skipped} other file(s) there were skipped)" : "(the folder is empty)"));
        }
    }
}
