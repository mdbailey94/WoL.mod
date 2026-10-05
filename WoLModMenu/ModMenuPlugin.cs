using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Rewired;
using UnityEngine;

namespace WoLModMenu
{
    // A "Mods" panel on the title screen with an on/off switch for each of this repo's mods.
    // Each mod owns a [General] Enabled setting and checks it live, so switches apply instantly.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class ModMenuPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.modmenu";
        public const string PluginName = "Mod Menu";
        public const string PluginVersion = "0.1.1";

        private const string ModGuidPrefix = "mdbailey94.wol.";
        private const float ReferenceHeight = 1080f;
        private const float FindTitleInterval = 1f;

        // Rewired element names for the Select-style button on Xbox/PlayStation/Switch pads,
        // and for confirm/cancel. Matched case-insensitively against the whole name.
        private static readonly string[] SelectButtons = { "back", "view", "select", "share", "create", "-", "minus" };
        private static readonly string[] ConfirmButtons = { "a", "cross" };
        private static readonly string[] CancelButtons = { "b", "circle" };

        private class ModToggle
        {
            public string Name;
            public ConfigEntry<bool> Enabled;
        }

        private readonly List<ModToggle> mods = new List<ModToggle>();
        private bool modsDiscovered;
        private TitleScreen titleScreen;
        private float nextTitleSearch;
        private bool open;
        private int selected;
        private int reenableTitleFrame = -1;
        private string lastTitleStatus;

        private GUIStyle textStyle;
        private GUIStyle boxStyle;
        private Rect hintRect;

        private void Awake()
        {
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void Update()
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                Logger.LogError(e);
                Close();
            }
        }

        private void Tick()
        {
            // Give the title menu its input back a couple of frames after closing, so the
            // button that closed us doesn't also act on it.
            if (reenableTitleFrame >= 0 && Time.frameCount >= reenableTitleFrame)
            {
                if (titleScreen != null)
                    titleScreen.enabled = true;
                reenableTitleFrame = -1;
            }

            if (!OnTitleMenu())
            {
                if (open)
                    Close();
                return;
            }

            DiscoverMods();
            if (mods.Count == 0)
                return;

            if (!open)
            {
                if (Input.GetKeyDown(KeyCode.M) || AnyButtonDown(SelectButtons))
                    Open();
                return;
            }

            if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Escape)
                || AnyButtonDown(SelectButtons) || AnyButtonDown(CancelButtons))
            {
                Close();
                return;
            }

            if (Direction(InputDirection.Up, KeyCode.UpArrow))
                selected = (selected + mods.Count - 1) % mods.Count;
            else if (Direction(InputDirection.Down, KeyCode.DownArrow))
                selected = (selected + 1) % mods.Count;
            else if (Direction(InputDirection.Left, KeyCode.LeftArrow) || Direction(InputDirection.Right, KeyCode.RightArrow)
                     || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || AnyButtonDown(ConfirmButtons))
                Toggle(selected);
        }

        private bool OnTitleMenu()
        {
            if (titleScreen == null && Time.unscaledTime >= nextTitleSearch)
            {
                nextTitleSearch = Time.unscaledTime + FindTitleInterval;
                titleScreen = FindObjectOfType<TitleScreen>();
            }
            // Show on the title screen unless the game's own Options screen is up. We disable the
            // title screen ourselves while open, so don't require it enabled then.
            bool onTitle = titleScreen != null && titleScreen.gameObject.activeInHierarchy
                && (open || titleScreen.enabled)
                && titleScreen.currentState != TitleScreen.TitleScreenState.Options;
            LogTitleStatus(onTitle);
            return onTitle;
        }

        // Logs only when something changes, so a log shows why the panel did or didn't appear.
        private void LogTitleStatus(bool onTitle)
        {
            string status = titleScreen == null
                ? "no title screen"
                : $"title screen active={titleScreen.gameObject.activeInHierarchy} enabled={titleScreen.enabled} " +
                  $"state={titleScreen.currentState} -> showing={onTitle}";
            if (status == lastTitleStatus)
                return;
            lastTitleStatus = status;
            Logger.LogInfo(status);
        }

        private void DiscoverMods()
        {
            if (modsDiscovered)
                return;
            modsDiscovered = true;

            foreach (PluginInfo info in Chainloader.PluginInfos.Values)
            {
                string guid = info.Metadata.GUID;
                var plugin = info.Instance as BaseUnityPlugin;
                if (plugin == null || guid == PluginGuid || !guid.StartsWith(ModGuidPrefix))
                    continue;
                // Binding an existing key returns that mod's own entry, so changes reach it live.
                mods.Add(new ModToggle
                {
                    Name = info.Metadata.Name,
                    Enabled = plugin.Config.Bind("General", "Enabled", true, "Turn the mod on or off.")
                });
            }
            mods.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            Logger.LogInfo($"Found {mods.Count} mods to manage");
        }

        private void Open()
        {
            open = true;
            selected = Mathf.Clamp(selected, 0, mods.Count - 1);
            reenableTitleFrame = -1;
            titleScreen.enabled = false;
        }

        private void Close()
        {
            if (!open)
                return;
            open = false;
            reenableTitleFrame = Time.frameCount + 2;
        }

        private void Toggle(int index)
        {
            ModToggle mod = mods[index];
            mod.Enabled.Value = !mod.Enabled.Value;
            Logger.LogInfo($"{mod.Name}: {(mod.Enabled.Value ? "on" : "off")}");
        }

        private static bool Direction(InputDirection direction, KeyCode key)
        {
            if (Input.GetKeyDown(key))
                return true;
            try
            {
                return InputController.GetAnyDeviceDirection(direction, true);
            }
            catch
            {
                return false;
            }
        }

        private static bool AnyButtonDown(string[] names)
        {
            if (!ReInput.isReady)
                return false;
            foreach (Joystick joystick in ReInput.controllers.Joysticks)
            {
                if (joystick == null || joystick.Buttons == null)
                    continue;
                foreach (Rewired.Controller.Button button in joystick.Buttons)
                {
                    if (button == null || !button.justPressed || string.IsNullOrEmpty(button.name))
                        continue;
                    string name = button.name.Trim().ToLowerInvariant();
                    if (Array.IndexOf(names, name) >= 0)
                        return true;
                }
            }
            return false;
        }

        private void OnGUI()
        {
            if (!OnTitleMenu() || mods.Count == 0)
                return;

            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true, wordWrap = false };
                boxStyle = new GUIStyle(GUI.skin.box);
                var background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.8f));
                background.Apply();
                boxStyle.normal.background = background;
            }

            float scale = Screen.height / ReferenceHeight;
            float virtualWidth = Screen.width / scale;
            float virtualHeight = ReferenceHeight;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            if (!open)
            {
                var hint = new GUIContent("<b>MODS</b>  <color=#9aa>Select / M</color>");
                Vector2 size = textStyle.CalcSize(hint);
                hintRect = new Rect(24f, virtualHeight - size.y - 40f, size.x + 24f, size.y + 12f);
                GUI.Box(hintRect, GUIContent.none, boxStyle);
                GUI.Label(new Rect(hintRect.x + 12f, hintRect.y + 6f, size.x, size.y), hint, textStyle);
                if (Event.current.type == EventType.MouseDown && hintRect.Contains(Event.current.mousePosition))
                    Open();
            }
            else
            {
                DrawPanel(virtualWidth, virtualHeight);
            }

            GUI.matrix = previous;
        }

        private void DrawPanel(float virtualWidth, float virtualHeight)
        {
            const float rowHeight = 34f;
            const float width = 460f;
            float height = 70f + mods.Count * rowHeight + 50f;
            var area = new Rect((virtualWidth - width) / 2f, (virtualHeight - height) / 2f, width, height);
            GUI.Box(area, GUIContent.none, boxStyle);
            GUI.Label(new Rect(area.x + 20f, area.y + 16f, width - 40f, 30f), "<b>MODS</b>", textStyle);

            for (int i = 0; i < mods.Count; i++)
            {
                var row = new Rect(area.x + 20f, area.y + 60f + i * rowHeight, width - 40f, rowHeight);
                bool on = mods[i].Enabled.Value;
                string cursor = i == selected ? "<color=#fd5>></color> " : "   ";
                string state = on ? "<color=#7d7>ON</color>" : "<color=#d77>OFF</color>";
                GUI.Label(row, $"{cursor}{mods[i].Name}", textStyle);
                GUI.Label(new Rect(row.xMax - 60f, row.y, 60f, rowHeight), state, textStyle);

                if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
                {
                    selected = i;
                    Toggle(i);
                    Event.current.Use();
                }
            }

            GUI.Label(new Rect(area.x + 20f, area.yMax - 44f, width - 40f, 30f),
                "<color=#9aa>Left/Right or A: toggle   B / Select: close</color>", textStyle);
        }
    }
}
