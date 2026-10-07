using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace WoLAscension
{
    // The Ascension altar: a stone pedestal with a flame on top and the level on its face, standing
    // beside the Chaos
    // Trials portal in the plaza. Walk up and the game's own button prompt appears over it; interact
    // to raise the level by one (after 10 it goes back to off). The flame grows from a small cold
    // blue one to a big white-hot one with the level, and the number on the front shows it; the
    // higher the level, and the game's on-screen notice says what the level adds. Runs then start at
    // that level.
    public class AscensionAltar : MonoBehaviour
    {
        private const float Range = 2.4f;         // from the middle of the altar
        private const string InteractAction = "Interact";

        public static Vector2 Offset = new Vector2(-3.5f, 0f);

        private static readonly Dictionary<int, Sprite> flameSprites = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> numberSprites = new Dictionary<int, Sprite>();
        private const int FlameFrames = 3;
        private const float FlameFps = 9f;
        private static Sprite pedestalSprite;
        private static float pixelsPerUnit = 16f;

        private ConfigEntry<int> level;
        private ManualLogSource log;
        private SpriteRenderer pedestal;
        private SpriteRenderer crystal;   // the flame
        private SpriteRenderer number;
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
            var top = new GameObject("Flame");
            top.transform.SetParent(transform, false);
            crystalHome = new Vector3(0f, 12f / pixelsPerUnit, 0f);
            top.transform.localPosition = crystalHome;
            crystal = top.AddComponent<SpriteRenderer>();
            var face = new GameObject("Number");
            face.transform.SetParent(transform, false);
            face.transform.localPosition = new Vector3(0f, 6.5f / pixelsPerUnit, 0f);
            number = face.AddComponent<SpriteRenderer>();
            ShowLevel();
            AddCollider();
        }

        // The flame's size and heat, and the number, for the current level.
        private void ShowLevel()
        {
            int lvl = Level;
            float size = lvl <= 0 ? 0.45f : 0.6f + 0.1f * lvl;
            crystal.transform.localScale = new Vector3(size, size, 1f);
            number.sprite = NumberSprite(lvl);
        }

        // Solid to walk into, like the game's own props: a box over the pedestal's footprint, on the
        // layer the game uses for obstacles.
        private void AddCollider()
        {
            try
            {
                var go = new GameObject("Base");
                go.transform.SetParent(transform, false);
                go.layer = ObstacleLayer();
                BoxCollider2D box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(13f / pixelsPerUnit, 5f / pixelsPerUnit);
                box.offset = new Vector2(0f, 3f / pixelsPerUnit);
                log?.LogInfo($"Ascension altar: solid on layer '{LayerMask.LayerToName(go.layer)}'");
            }
            catch (Exception e)
            {
                log?.LogInfo($"Ascension altar: couldn't make it solid ({e.Message})");
            }
        }

        private static int ObstacleLayer()
        {
            int named = LayerMask.NameToLayer("Obstacle");
            if (named >= 0)
                return named;
            int obstacles = ChaosCollisions.layerAllWallAndObst & ~ChaosCollisions.layerAllWall;
            int mask = obstacles != 0 ? obstacles : ChaosCollisions.layerAllWallAndObst;
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                    return i;
            }
            return 0;
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
            // The flame flickers through its frames.
            crystal.sprite = FlameSprite(Level, Mathf.FloorToInt(Time.time * FlameFps) % FlameFrames);

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
                ShowLevel();
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
            GameBanner.Show(header, info, Icon(lvl));
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
            pedestal.sortingLayerID = crystal.sortingLayerID = number.sortingLayerID = any.spriteRenderer.sortingLayerID;
            pedestal.sortingOrder = order;
            crystal.sortingOrder = order + 1;
            number.sortingOrder = order + 1;
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
            "...LMDDDDDDMD...",
            "...LMDDDDDDMD...",
            "...LMDDDDDDMD...",
            "...LMDDDDDDDD...",
            "...LMMMMMMMDD...",
            "..sLLLLLLLLLLM..",
            ".LMMMMMMMMMMMMD.",
            ".DDDDDDDDDDDDDD.",
            "hhhhhhhhhhhhhhhh",
        };

        private static Sprite PedestalSprite()
        {
            if (pedestalSprite == null)
                pedestalSprite = Make(PedestalRows, c => Stone.ContainsKey(c) ? (Color32?)Stone[c] : null, new Vector2(0.5f, 0f));
            return pedestalSprite;
        }

        // Three flicker frames of a flame, in outer, middle and core colours (no outline).
        private static readonly string[][] FlameRows =
        {
            new[]
            {
                "....o.....",
                "....oo....",
                "...ooo..o.",
                "...omo..o.",
                "..oomoo.o.",
                "..ommmooo.",
                ".oomcmmoo.",
                ".ommccmmo.",
                ".omccccmo.",
                "oommcccmoo",
                "ommcccccmo",
                "ommcccccmo",
                ".ommcccmo.",
                "..ommmmo..",
            },
            new[]
            {
                ".....o....",
                "....oo....",
                ".o..ooo...",
                ".o..omo...",
                ".o.oomoo..",
                ".ooommmo..",
                ".oommcmoo.",
                ".ommccmmo.",
                ".omccccmo.",
                "oommcccmoo",
                "ommcccccmo",
                "ommcccccmo",
                ".ommcccmo.",
                "..ommmmo..",
            },
            new[]
            {
                "..........",
                ".....o....",
                "....ooo...",
                "...oomo...",
                "...ommoo..",
                "..oommmo..",
                "..ommcmoo.",
                ".oomccmmo.",
                ".omccccmo.",
                "oommcccmoo",
                "ommcccccmo",
                "ommcccccmo",
                ".ommcccmo.",
                "..ommmmo..",
            },
        };

        // Outer, middle and core colours at levels 1, 4, 7 and 10.
        private static readonly Color[][] Heat =
        {
            new Color[] { new Color32(0x2c, 0x4c, 0xc8, 0xff), new Color32(0x5c, 0x9c, 0xff, 0xff), new Color32(0xcc, 0xec, 0xff, 0xff) },
            new Color[] { new Color32(0x7a, 0x34, 0xc8, 0xff), new Color32(0xb0, 0x70, 0xff, 0xff), new Color32(0xf0, 0xdc, 0xff, 0xff) },
            new Color[] { new Color32(0xd0, 0x2a, 0x2a, 0xff), new Color32(0xff, 0x6a, 0x3a, 0xff), new Color32(0xff, 0xd8, 0xb0, 0xff) },
            new Color[] { new Color32(0xf0, 0x40, 0x10, 0xff), new Color32(0xff, 0xa0, 0x20, 0xff), new Color32(0xff, 0xf8, 0xd0, 0xff) },
        };

        // Cold blue when low, through to a white-hot orange at 10 (a grey ember when off).
        private static void FlameColours(int lvl, out Color outer, out Color mid, out Color core)
        {
            if (lvl <= 0)
            {
                outer = new Color32(0x5a, 0x58, 0x6e, 0xff);
                mid = new Color32(0x80, 0x7c, 0x96, 0xff);
                core = new Color32(0xa8, 0xa4, 0xbc, 0xff);
                return;
            }
            // Blue, then violet, then red, then orange with a white-hot core.
            float t = (lvl - 1) / (float)(AscensionLevels.Max - 1) * (Heat.Length - 1);
            int a = Mathf.Min(Heat.Length - 2, Mathf.FloorToInt(t));
            float f = t - a;
            outer = Color.Lerp(Heat[a][0], Heat[a + 1][0], f);
            mid = Color.Lerp(Heat[a][1], Heat[a + 1][1], f);
            core = Color.Lerp(Heat[a][2], Heat[a + 1][2], f);
        }

        public static Sprite FlameSprite(int lvl, int frame)
        {
            int key = lvl * 10 + frame;
            Sprite sprite;
            if (flameSprites.TryGetValue(key, out sprite) && sprite != null)
                return sprite;
            Color outer, mid, core;
            FlameColours(lvl, out outer, out mid, out core);
            sprite = Make(FlameRows[frame], c =>
            {
                switch (c)
                {
                    case 'o': return (Color32?)outer;
                    case 'm': return mid;
                    case 'c': return core;
                    default: return null;
                }
            }, new Vector2(0.5f, 0f));
            flameSprites[key] = sprite;
            return sprite;
        }

        // For the banner.
        public static Sprite Icon(int lvl) => FlameSprite(lvl, 0);

        // 3x5 pixel digits for the number on the altar's face.
        private static readonly string[] Digits =
        {
            "###,#.#,#.#,#.#,###", ".#.,##.,.#.,.#.,###", "###,..#,###,#..,###", "###,..#,.##,..#,###",
            "#.#,#.#,###,..#,..#", "###,#..,###,..#,###", "###,#..,###,#.#,###", "###,..#,..#,.#.,.#.",
            "###,#.#,###,#.#,###", "###,#.#,###,..#,###",
        };

        // The level carved into the front, glowing in the flame's colour.
        private static Sprite NumberSprite(int lvl)
        {
            Sprite sprite;
            if (numberSprites.TryGetValue(lvl, out sprite) && sprite != null)
                return sprite;
            string text = lvl.ToString();
            var rows = new string[5];
            for (int r = 0; r < 5; r++)
            {
                var line = new System.Text.StringBuilder();
                for (int d = 0; d < text.Length; d++)
                {
                    if (d > 0)
                        line.Append('.');
                    line.Append(Digits[text[d] - '0'].Split(',')[r]);
                }
                rows[r] = line.ToString();
            }
            Color outer, mid, core;
            FlameColours(lvl, out outer, out mid, out core);
            Color glow = lvl <= 0 ? (Color)new Color32(0x4a, 0x46, 0x5e, 0xff) : mid;
            sprite = Make(rows, c => c == '#' ? (Color32?)glow : null, new Vector2(0.5f, 0.5f));
            numberSprites[lvl] = sprite;
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
