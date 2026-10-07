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
        private const float Range = 1.6f;
        private const string InteractAction = "Interact";
        private const float BobSpeed = 2.2f;
        private const float BobHeight = 0.08f;

        public static Vector2 Offset = new Vector2(-3.5f, 0f);

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

        // Finds the portal into the trials in this scene and puts an altar beside it (null if this
        // scene has no such portal).
        public static AscensionAltar SpawnBesideTrialsPortal(ConfigEntry<int> level, ManualLogSource log)
        {
            NextLevelLoader portal = null;
            foreach (NextLevelLoader loader in FindObjectsOfType<NextLevelLoader>())
            {
                if (loader != null && IsTrialScene(loader.nextLevelName))
                {
                    portal = loader;
                    break;
                }
            }
            if (portal == null)
                return null;
            try
            {
                ReadPixelSize();
                var go = new GameObject("AscensionAltar");
                go.transform.position = (Vector2)portal.transform.position + Offset;
                AscensionAltar altar = go.AddComponent<AscensionAltar>();
                altar.level = level;
                altar.log = log;
                altar.Build();
                log.LogInfo($"Ascension altar placed at {go.transform.position} beside the portal to '{portal.nextLevelName}'");
                return altar;
            }
            catch (Exception e)
            {
                log.LogError($"Couldn't place the Ascension altar: {e.Message}");
                return null;
            }
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
            }
            if (near.inputDevice != null && near.inputDevice.GetButtonDown(InteractAction))
            {
                level.Value = Level >= AscensionLevels.Max ? 0 : Level + 1;
                crystal.sprite = CrystalSprite(Level);
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
                float d = Vector2.Distance(p.transform.position, transform.position);
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

        private static readonly Dictionary<char, Color32> Stone = new Dictionary<char, Color32>
        {
            { 'o', new Color32(0x1e, 0x1a, 0x2a, 0xff) },
            { 'D', new Color32(0x3e, 0x3a, 0x52, 0xff) },
            { 'M', new Color32(0x5e, 0x5a, 0x78, 0xff) },
            { 'L', new Color32(0x86, 0x82, 0xa0, 0xff) },
            { 's', new Color32(0xb4, 0xb0, 0xc8, 0xff) },
        };

        private static readonly string[] PedestalRows =
        {
            "..oooooooooooo..",
            ".osssssssssssso.",
            ".oLLLLLLLLLLLLo.",
            "..oDDDDDDDDDDo..",
            "...oMMMLMMMMo...",
            "...oMMMLMMMMo...",
            "...oMMLMMMMMo...",
            "...oMMLMMMMDo...",
            "...oMMMLMMMDo...",
            "..oLLLLLLLLLLo..",
            ".oMMMMMMMMMMMMo.",
            ".oDDDDDDDDDDDDo.",
            "..oooooooooooo..",
        };

        private static readonly string[] CrystalRows =
        {
            "...oo...",
            "..oWLo..",
            ".oWWLMo.",
            ".oWLLMo.",
            "oWWLLMDo",
            "oWLLMMDo",
            ".oLLMDo.",
            ".oLMMDo.",
            "..oMDo..",
            "...oo...",
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
                    case 'o': return (Color32?)new Color32(0x1e, 0x1a, 0x2a, 0xff);
                    case 'W': return Color.Lerp(tint, Color.white, 0.75f);
                    case 'L': return Color.Lerp(tint, Color.white, 0.35f);
                    case 'M': return tint;
                    case 'D': return Color.Lerp(tint, Color.black, 0.35f);
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
