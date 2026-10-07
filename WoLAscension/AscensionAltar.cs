using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace WoLAscension
{
    // The Ascension altar: a stone pedestal with a floating chaos crystal, standing beside the Chaos
    // Trials portal in the plaza. Walk up and the game's own button prompt appears over it; interact
    // to raise the level by one (after 10 it goes back to off). The crystal burns brighter the
    // higher the level, and the game's notice banner says what the level adds. Runs then start at
    // that level.
    public class AscensionAltar : MonoBehaviour
    {
        private const float Range = 2.4f;         // from the middle of the altar
        private const string InteractAction = "Interact";
        private const float BobSpeed = 2.2f;
        private const float BobHeight = 0.08f;

        public static Vector2 Offset = new Vector2(-2.5f, -2.5f);

        private static readonly Dictionary<int, Sprite> crystalSprites = new Dictionary<int, Sprite>();
        private static Sprite pedestalSprite;
        private static float pixelsPerUnit = 16f;

        private ConfigEntry<int> level;
        private ManualLogSource log;
        private SpriteRenderer pedestal;
        private SpriteRenderer crystal;
        private Vector3 crystalHome;
        private OverheadPrompt prompt;
        private bool promptShown;
        private bool greeted;
        private bool loggedNear;

        // Finds the portal into the trials in this scene and puts an altar beside it (null if this
        // scene has no such portal).
        // Where the portal into the trials was last seen (scene name and position), remembered when
        // a run starts through it, for scenes where it can't be found by looking.
        public static ConfigEntry<string> RememberedPortal;
        private static readonly HashSet<string> loggedScenes = new HashSet<string>();

        public static AscensionAltar SpawnBesideTrialsPortal(ConfigEntry<int> level, ManualLogSource log)
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool firstLook = loggedScenes.Add(scene);
            Vector2? spot = null;
            string why = null;
            NextLevelLoader blank = null;

            // Every level loader in the scene, hidden ones too (the trials portal may be switched
            // off until something opens it, or get its destination later).
            foreach (NextLevelLoader loader in Resources.FindObjectsOfTypeAll<NextLevelLoader>())
            {
                if (loader == null || loader.gameObject.scene.name != scene)
                    continue;
                if (firstLook)
                    log.LogInfo($"Altar: level loader '{loader.name}' to '{loader.nextLevelName}' at {loader.transform.position} " +
                        $"(active: {loader.gameObject.activeInHierarchy})");
                if (spot == null && IsTrialScene(loader.nextLevelName))
                {
                    spot = loader.transform.position;
                    why = $"beside the portal to '{loader.nextLevelName}'";
                }
                // The plaza's trials portal has no destination until you step in (the game picks the
                // floor then): the active loader with none.
                else if (blank == null && string.IsNullOrEmpty(loader.nextLevelName) && loader.gameObject.activeInHierarchy)
                    blank = loader;
            }
            if (spot == null && blank != null)
            {
                spot = blank.transform.position;
                why = "beside the trials portal";
            }
            // Otherwise where a run was last started from, if it was this scene.
            if (spot == null && RememberedPortal != null && !string.IsNullOrEmpty(RememberedPortal.Value))
            {
                string[] parts = RememberedPortal.Value.Split('|');
                float x, y;
                if (parts.Length == 3 && parts[0] == scene && float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out x)
                    && float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out y))
                {
                    spot = new Vector2(x, y);
                    why = "where the last run's portal was";
                }
            }
            if (spot == null)
            {
                if (firstLook)
                    log.LogInfo($"Altar: no trials portal found in '{scene}'");
                return null;
            }
            try
            {
                ReadPixelSize();
                var go = new GameObject("AscensionAltar");
                go.transform.position = spot.Value + Offset;
                AscensionAltar altar = go.AddComponent<AscensionAltar>();
                altar.level = level;
                altar.log = log;
                altar.Build();
                log.LogInfo($"Ascension altar placed in '{scene}' at {go.transform.position}, {why} ({pixelsPerUnit} px per unit)");
                return altar;
            }
            catch (Exception e)
            {
                log.LogError($"Couldn't place the Ascension altar: {e.Message}");
                return null;
            }
        }

        // Called when a run starts through a portal: remember where it was.
        public static void RememberPortal(NextLevelLoader portal)
        {
            if (RememberedPortal == null || portal == null)
                return;
            Vector3 p = portal.transform.position;
            RememberedPortal.Value = portal.gameObject.scene.name + "|"
                + p.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|"
                + p.y.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool IsTrialScene(string name)
        {
            try
            {
                return !string.IsNullOrEmpty(name) && (NextLevelLoader.InTierScene(name) || NextLevelLoader.InBossScene(name));
            }
            catch
            {
                return false;
            }
        }

        // Pixel size of the game's sprites, from the wizard's, so the altar matches.
        private static void ReadPixelSize()
        {
            try
            {
                Player[] players = GameController.activePlayers;
                if (players != null && players.Length > 0 && players[0] != null && players[0].spriteRenderer != null
                    && players[0].spriteRenderer.sprite != null)
                    pixelsPerUnit = players[0].spriteRenderer.sprite.pixelsPerUnit;
            }
            catch
            {
            }
        }

        private void Build()
        {
            pedestal = gameObject.AddComponent<SpriteRenderer>();
            pedestal.sprite = PedestalSprite();
            var top = new GameObject("Crystal");
            top.transform.SetParent(transform, false);
            crystalHome = new Vector3(0f, 14f / pixelsPerUnit, 0f);
            top.transform.localPosition = crystalHome;
            crystal = top.AddComponent<SpriteRenderer>();
            crystal.sprite = CrystalSprite(Level);
        }

        private int Level => Mathf.Clamp(level.Value, 0, AscensionLevels.Max);

        private void Update()
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                log?.LogError($"Ascension altar: {e.Message}");
                enabled = false;
            }
        }

        private void Tick()
        {
            crystal.transform.localPosition = crystalHome + new Vector3(0f, Mathf.Sin(Time.time * BobSpeed) * BobHeight, 0f);

            Player near = NearestPlayer();
            SortAgainst(near);
            if (near == null)
            {
                HidePrompt();
                greeted = false;
                return;
            }
            ShowPrompt(near);
            if (!greeted)
            {
                greeted = true;
                Announce("Interact to raise");
                if (!loggedNear)
                {
                    loggedNear = true;
                    log?.LogInfo("Ascension altar: a wizard is in reach");
                }
            }
            if (InteractPressed(near))
            {
                level.Value = Level >= AscensionLevels.Max ? 0 : Level + 1;
                crystal.sprite = CrystalSprite(Level);
                log?.LogInfo($"Ascension altar: level set to {Level}");
                Announce(null);
                SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(transform.position), null, 24f, -1f,
                    0.8f + 0.06f * Level, false);
            }
        }

        private void Announce(string extra)
        {
            int lvl = Level;
            string header = lvl > 0 ? $"ASCENSION {lvl}" : "ASCENSION OFF";
            string info = AscensionLevels.Summary(lvl);
            if (!string.IsNullOrEmpty(extra))
                info = extra + " - " + info;
            GameBanner.Show(header, info, CrystalSprite(lvl));
        }

        // The game's Interact button, read two ways (the game's own input, then Rewired's).
        private static bool InteractPressed(Player p)
        {
            ChaosInputDevice device = p.inputDevice;
            if (device == null)
                return false;
            try
            {
                if (device.GetButtonDown(InteractAction))
                    return true;
            }
            catch
            {
            }
            try
            {
                return device.rewiredPlayer != null && device.rewiredPlayer.GetButtonDown(InteractAction);
            }
            catch
            {
                return false;
            }
        }

        private Player NearestPlayer()
        {
            Player[] players = GameController.activePlayers;
            if (players == null)
                return null;
            Player best = null;
            float bestDistance = Range;
            foreach (Player p in players)
            {
                if (p == null || !p.gameObject.activeInHierarchy)
                    continue;
                float d = Vector2.Distance(p.transform.position, (Vector2)transform.position + new Vector2(0f, 0.5f));
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }
            return best;
        }

        // In front of a wizard standing behind it, behind one standing in front.
        private void SortAgainst(Player p)
        {
            Player any = p;
            if (any == null)
            {
                Player[] players = GameController.activePlayers;
                any = players != null && players.Length > 0 ? players[0] : null;
            }
            if (any == null || any.spriteRenderer == null)
                return;
            int order = any.spriteRenderer.sortingOrder + (any.transform.position.y > transform.position.y ? 1 : -1);
            pedestal.sortingLayerID = crystal.sortingLayerID = any.spriteRenderer.sortingLayerID;
            pedestal.sortingOrder = order;
            crystal.sortingOrder = order + 1;
        }

        // The game's own "press this button" prompt over the altar.
        private void ShowPrompt(Player p)
        {
            if (promptShown)
                return;
            try
            {
                if (prompt == null)
                {
                    GameObject go = Instantiate(OverheadPrompt.Prefab);
                    prompt = go != null ? go.GetComponent<OverheadPrompt>() : null;
                    if (prompt == null)
                        return;
                    prompt.Initialize(transform, p.inputDevice.inputScheme, 30f / pixelsPerUnit, 0f);
                }
                prompt.DisplayPrompt(InteractAction, false, false);
                promptShown = true;
            }
            catch (Exception e)
            {
                log?.LogInfo($"Ascension altar: no button prompt ({e.Message})");
                promptShown = true;
            }
        }

        private void HidePrompt()
        {
            if (!promptShown)
                return;
            promptShown = false;
            try
            {
                prompt?.HidePrompt("");
            }
            catch
            {
            }
        }

        private void OnDestroy()
        {
            if (prompt != null)
                Destroy(prompt.gameObject);
        }

        // ---- Art ----

        // Soft-shaded like the game's own props: no dark outline, just light on top and shade
        // below.
        private static readonly Dictionary<char, Color32> Stone = new Dictionary<char, Color32>
        {
            { 'D', new Color32(0x5a, 0x55, 0x70, 0xff) },
            { 'M', new Color32(0x7c, 0x77, 0x94, 0xff) },
            { 'L', new Color32(0xa2, 0x9d, 0xb8, 0xff) },
            { 's', new Color32(0xc8, 0xc4, 0xd8, 0xff) },
            { 'h', new Color32(0x45, 0x41, 0x58, 0x90) }, // soft ground shadow
        };

        private static readonly string[] PedestalRows =
        {
            "..ssssssssssss..",
            ".sLLLLLLLLLLLLs.",
            ".MMMMMMMMMMMMMM.",
            "...LMMMMMMMMD...",
            "...LMMMLMMMMD...",
            "...LMMMLMMMMD...",
            "...LMMLMMMMMD...",
            "...LMMLMMMMDD...",
            "...LMMMLMMMDD...",
            "..sLLLLLLLLLLM..",
            ".LMMMMMMMMMMMMD.",
            ".DDDDDDDDDDDDDD.",
            "hhhhhhhhhhhhhhhh",
        };

        private static readonly string[] CrystalRows =
        {
            "...WL...",
            "..WWLM..",
            ".WWWLMD.",
            ".WWLLMD.",
            "WWWLLMMD",
            "WWLLMMDD",
            ".WLLMMD.",
            ".LLMMDD.",
            "..LMDD..",
            "...MD...",
        };

        private static Sprite PedestalSprite()
        {
            if (pedestalSprite == null)
                pedestalSprite = Make(PedestalRows, c => Stone.ContainsKey(c) ? (Color32?)Stone[c] : null, new Vector2(0.5f, 0f));
            return pedestalSprite;
        }

        // The crystal, from a dim grey-violet when off to the Ascension orange at level 10.
        public static Sprite CrystalSprite(int lvl)
        {
            Sprite sprite;
            if (crystalSprites.TryGetValue(lvl, out sprite) && sprite != null)
                return sprite;
            float t = Mathf.Clamp01(lvl / (float)AscensionLevels.Max);
            Color low = new Color32(0x7a, 0x70, 0xa8, 0xff), high = new Color32(0xff, 0x7a, 0x30, 0xff);
            Color tint = Color.Lerp(low, high, t);
            sprite = Make(CrystalRows, c =>
            {
                switch (c)
                {
                    case 'W': return (Color32?)Color.Lerp(tint, Color.white, 0.75f);
                    case 'L': return Color.Lerp(tint, Color.white, 0.35f);
                    case 'M': return tint;
                    case 'D': return Color.Lerp(tint, Color.black, 0.25f);
                    default: return null;
                }
            }, new Vector2(0.5f, 0f));
            crystalSprites[lvl] = sprite;
            return sprite;
        }

        private static Sprite Make(string[] rows, Func<char, Color32?> colour, Vector2 pivot)
        {
            int w = rows[0].Length, h = rows.Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color32? c = colour(rows[h - 1 - y][x]);
                    tex.SetPixel(x, y, c.HasValue ? (Color)c.Value : new Color(0f, 0f, 0f, 0f));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, pixelsPerUnit);
        }
    }
}
