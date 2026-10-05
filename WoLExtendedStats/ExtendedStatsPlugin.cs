using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace WoLExtendedStats
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class ExtendedStatsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.extendedstats";
        public const string PluginName = "Extended Stats";
        public const string PluginVersion = "0.4.0";

        private const float RefreshInterval = 0.25f;
        private const float ReferenceHeight = 1080f;
        private const float Margin = 16f;
        private const float PanelTop = 140f;
        private const float ScrollRepeatSeconds = 0.15f;
        private const float StickThreshold = 0.5f;

        private static ExtendedStatsPlugin instance;
        private static ManualLogSource log;

        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<KeyboardShortcut> toggleKey;
        private ConfigEntry<bool> pinned;
        private ConfigEntry<bool> menuInfo;
        private ConfigEntry<bool> runSummary;
        private ConfigEntry<float> panelScale;
        private ConfigEntry<int> infoTextSize;
        private ConfigEntry<int> infoMaxLines;

        private readonly List<Player> players = new List<Player>();
        // IMGUI panels in screen order: index 0 on the left, 1 on the right.
        private readonly List<string> overlayTexts = new List<string>();
        private readonly List<string> summaryTexts = new List<string>();
        private bool summaryVisible;
        private DeathSummaryUI summaryScreen;
        private float nextRefresh;
        private GUIStyle textStyle;
        private GUIStyle boxStyle;

        // What we last wrote into each game info box, so we can tell when the game replaces it.
        private class InfoBoxState
        {
            public string BaseText;
            public string Written;
            public int OriginalFontSize;
            public int ScrollOffset;
            public float NextScrollTime;
        }

        private struct PendingInfo
        {
            public string Extra;
            public Player Owner;
        }

        private readonly Dictionary<Text, InfoBoxState> infoBoxes = new Dictionary<Text, InfoBoxState>();
        private readonly Dictionary<Text, PendingInfo> pendingExtras = new Dictionary<Text, PendingInfo>();

        private void Awake()
        {
            instance = this;
            log = Logger;

            modEnabled = Config.Bind("General", "Enabled", true,
                "Turn the mod on or off (also in the title screen Mods menu).");
            toggleKey = Config.Bind("Hotkeys", "Toggle", new KeyboardShortcut(KeyCode.F2),
                "Pin a full stats overlay on screen, or unpin it.");
            pinned = Config.Bind("General", "Visible", false,
                "Keep the full stats overlay on screen (toggled with the hotkey).");
            menuInfo = Config.Bind("General", "ShowInCharacterMenu", true,
                "Add stats to the character menu's info box: arcana stats when an arcana is highlighted, " +
                "wizard stats when the cloak is highlighted.");
            runSummary = Config.Bind("General", "ShowRunSummary", true,
                "Show post-run stats next to the end-of-run screen.");
            infoTextSize = Config.Bind("General", "InfoTextSize", 75,
                new ConfigDescription("Size of the added stats in the character menu info box, as % of the game's text size.",
                    new AcceptableValueRange<int>(40, 100)));
            infoMaxLines = Config.Bind("General", "InfoMaxLines", 5,
                new ConfigDescription("Most stat lines shown in the info box at once; scroll for the rest " +
                    "(right stick, Page Up/Down or mouse wheel).", new AcceptableValueRange<int>(2, 20)));
            panelScale = Config.Bind("General", "Scale", 1f,
                new ConfigDescription("Overlay and summary panel size multiplier.", new AcceptableValueRange<float>(0.5f, 2f)));

            // Patch each hook on its own so one failing (e.g. after a game update) can't take the
            // others down. Falls, dashes, run resets and the summary need these; the rest works without.
            var harmony = new Harmony(PluginGuid);
            foreach (Type hook in new[] { typeof(FallPatch), typeof(DashPatch), typeof(ResetStatsPatch),
                                          typeof(SummaryShowPatch), typeof(SummaryHidePatch) })
            {
                try
                {
                    harmony.CreateClassProcessor(hook).Patch();
                }
                catch (Exception e)
                {
                    Logger.LogError($"Hook {hook.Name} failed to install: {e.Message}");
                }
            }

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded (overlay: {toggleKey.Value})");
        }

        public static void LogError(string where, Exception e) => log?.LogError($"[{where}] {e}");

        public static void ShowRunSummary(DeathSummaryUI screen)
        {
            if (instance == null || !instance.modEnabled.Value || !instance.runSummary.Value)
                return;

            instance.RefreshPlayers();
            instance.summaryTexts.Clear();
            instance.summaryScreen = screen;
            string title = screen.gameWon ? "VICTORY - RUN STATS" : "RUN STATS";
            foreach (Player player in instance.players)
            {
                string header = instance.players.Count > 1 ? PlayerHeader(player) : string.Empty;
                instance.summaryTexts.Add(header + StatText.RunSummary(RunTracker.For(player).Clone(), title));
            }
            instance.summaryVisible = instance.summaryTexts.Count > 0;
        }

        public static void HideRunSummary()
        {
            if (instance != null)
                instance.summaryVisible = false;
        }

        private void Update()
        {
            if (!modEnabled.Value)
            {
                summaryVisible = false;
                overlayTexts.Clear();
                return;
            }

            if (toggleKey.Value.IsDown())
                pinned.Value = !pinned.Value;

            // Hide the summary if the end screen went away without calling Deactivate.
            if (summaryVisible && (summaryScreen == null || !summaryScreen.gameObject.activeInHierarchy))
                summaryVisible = false;

            RefreshPlayers();
            try
            {
                RunTracker.EnsureHooked(players);
                foreach (Player player in players)
                    RunTracker.TrackMovement(player);
            }
            catch (Exception e)
            {
                LogError("tracking", e);
            }

            if (Time.unscaledTime < nextRefresh)
                return;
            nextRefresh = Time.unscaledTime + RefreshInterval;

            overlayTexts.Clear();
            if (!pinned.Value)
                return;
            foreach (Player player in players)
            {
                try
                {
                    overlayTexts.Add(StatText.Overlay(player, players.Count > 1 ? PlayerHeader(player) : string.Empty));
                }
                catch (Exception e)
                {
                    overlayTexts.Add("Extended Stats error:\n" + e.Message);
                }
            }
        }

        // Runs after the game's menus have updated their info text for this frame.
        private void LateUpdate()
        {
            pendingExtras.Clear();
            if (modEnabled.Value && menuInfo.Value)
            {
                foreach (Player player in players)
                {
                    try
                    {
                        CollectMenuInfo(player);
                    }
                    catch (Exception e)
                    {
                        LogError("menu info", e);
                    }
                }
            }

            // Write our extras, and restore any box we touched before but no longer should.
            foreach (var pair in pendingExtras)
                WriteInfoBox(pair.Key, pair.Value.Extra, pair.Value.Owner);
            foreach (Text box in new List<Text>(infoBoxes.Keys))
            {
                if (box == null)
                    infoBoxes.Remove(box);
                else if (!pendingExtras.ContainsKey(box))
                    WriteInfoBox(box, null, null);
            }
        }

        private void CollectMenuInfo(Player player)
        {
            LowerHUD hud = player.lowerHUD;
            if (hud == null || !hud.equipMenuActive)
                return;

            EquipMenu equip = hud.equipMenu;
            if (equip != null && equip.hasFocus && equip.infoText != null)
            {
                Player.SkillState[] skills = player.assignedSkills;
                int index = equip.navigationIndex;
                if (skills != null && index >= 0 && index < skills.Length && skills[index] != null)
                    pendingExtras[equip.infoText] = new PendingInfo
                    {
                        Extra = StatText.Arcana(player, skills[index], equip.infoText.supportRichText),
                        Owner = player
                    };
            }

            OutfitMenu outfit = hud.outfitMenu;
            if (outfit != null && outfit.hasFocus && outfit.infoText != null)
                pendingExtras[outfit.infoText] = new PendingInfo
                {
                    Extra = StatText.Wizard(player, outfit.infoText.supportRichText),
                    Owner = player
                };
        }

        private void WriteInfoBox(Text box, string extra, Player owner)
        {
            InfoBoxState state;
            if (!infoBoxes.TryGetValue(box, out state))
            {
                if (extra == null)
                    return;
                state = new InfoBoxState { OriginalFontSize = box.fontSize };
                infoBoxes[box] = state;
            }

            // If the text isn't what we last wrote, the game has put up a new description.
            if (box.text != state.Written)
            {
                state.BaseText = box.text;
                state.ScrollOffset = 0;
            }

            string section = string.IsNullOrEmpty(extra) ? null : FormatSection(box, state, extra, owner);
            string desired = section == null
                ? state.BaseText
                : (string.IsNullOrEmpty(state.BaseText) ? section : state.BaseText + "\n\n" + section);
            if (box.text != desired)
                box.text = desired;
            state.Written = desired;
        }

        // Shrinks our section's font and shows at most InfoMaxLines of it, scrolled by the owner.
        private string FormatSection(Text box, InfoBoxState state, string extra, Player owner)
        {
            string[] lines = extra.Split('\n');
            int maxLines = infoMaxLines.Value;
            string body;
            if (lines.Length <= maxLines)
            {
                state.ScrollOffset = 0;
                body = extra;
            }
            else
            {
                int maxOffset = lines.Length - maxLines;
                state.ScrollOffset = Mathf.Clamp(state.ScrollOffset + ReadScroll(state, owner), 0, maxOffset);
                var shown = new string[maxLines];
                Array.Copy(lines, state.ScrollOffset, shown, 0, maxLines);
                string position = $"lines {state.ScrollOffset + 1}-{state.ScrollOffset + maxLines} of {lines.Length} - scroll with right stick";
                body = string.Join("\n", shown) + "\n" + (box.supportRichText ? $"<color=#9aa>{position}</color>" : position);
            }

            if (!box.supportRichText)
                return body;
            int size = Mathf.Max(8, Mathf.RoundToInt(state.OriginalFontSize * infoTextSize.Value / 100f));
            return $"<size={size}>{body}</size>";
        }

        // +1 scrolls down a line, -1 up. Held stick repeats; keyboard/mouse work for everyone.
        private static int ReadScroll(InfoBoxState state, Player owner)
        {
            int step = 0;
            if (Input.GetKeyDown(KeyCode.PageDown) || Input.mouseScrollDelta.y < 0f)
                step = 1;
            else if (Input.GetKeyDown(KeyCode.PageUp) || Input.mouseScrollDelta.y > 0f)
                step = -1;
            if (step != 0)
                return step;

            float stick = owner != null ? RightStick.Vertical(owner) : 0f;
            if (Mathf.Abs(stick) < StickThreshold)
            {
                state.NextScrollTime = 0f;
                return 0;
            }
            if (Time.unscaledTime < state.NextScrollTime)
                return 0;
            state.NextScrollTime = Time.unscaledTime + ScrollRepeatSeconds;
            // Rewired reports stick-up as positive, which should scroll back up.
            return stick > 0f ? -1 : 1;
        }

        private void OnGUI()
        {
            List<string> texts = summaryVisible ? summaryTexts : overlayTexts;
            if (texts.Count == 0)
                return;

            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, wordWrap = false };
                boxStyle = new GUIStyle(GUI.skin.box);
                boxStyle.normal.background = MakeTexture(new Color(0f, 0f, 0f, 0.75f));
            }

            float scale = Screen.height / ReferenceHeight * panelScale.Value;
            float virtualWidth = Screen.width / scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            for (int i = 0; i < texts.Count; i++)
            {
                if (string.IsNullOrEmpty(texts[i]))
                    continue;

                var content = new GUIContent(texts[i]);
                Vector2 size = textStyle.CalcSize(content);
                float width = size.x + 24f;
                // Player 1 hugs the left edge, player 2 the right, matching the split HUD.
                float x = i == 0 ? Margin : virtualWidth - width - Margin;
                var area = new Rect(x, PanelTop, width, size.y + 16f);
                GUI.Box(area, GUIContent.none, boxStyle);
                GUI.Label(new Rect(area.x + 12f, area.y + 8f, size.x, size.y), content, textStyle);
            }

            GUI.matrix = previous;
        }

        private void RefreshPlayers()
        {
            players.Clear();
            Player[] active = GameController.activePlayers;
            if (active == null)
                return;
            foreach (Player player in active)
            {
                if (player != null)
                    players.Add(player);
            }
            players.Sort((a, b) => a.playerID.CompareTo(b.playerID));
        }

        private static string PlayerHeader(Player player) =>
            $"<b><color=#fd5>PLAYER {player.playerID + 1}</color></b>\n";

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
