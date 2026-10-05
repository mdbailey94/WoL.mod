using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLAscension
{
    // Ascension levels 1-10: pick a level in the hub, and every run outside the hub gets that
    // level's modifiers, applied through the game's own stat modifiers so they stack with relics
    // and come off cleanly when you return to the hub.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class AscensionPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.ascension";
        public const string PluginName = "Ascension";
        public const string PluginVersion = "0.1.0";

        private const string PlayerDamageTakenMod = "Ascension_DamageTaken";
        private const string PlayerHealingMod = "Ascension_Healing";
        private const string PlayerMaxHealthMod = "Ascension_MaxHealth";
        private const string EnemyHealthMod = "Ascension_EnemyHealth";
        private const string EnemySpeedMod = "Ascension_EnemySpeed";

        private const float ApplyInterval = 0.25f;
        private const float HubSearchInterval = 1f;
        private const float StickThreshold = 0.6f;
        private const float StickRepeatSeconds = 0.3f;
        private const float ReferenceHeight = 1080f;

        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<int> level;
        private ConfigEntry<bool> showInRun;

        private readonly List<Player> players = new List<Player>();
        private PlayerRoomUI hubUI;
        private float nextHubSearch;
        private bool inHub;
        private bool wasInHub;
        private int activeLevel;
        private float nextApply;
        private float nextStickStep;
        private Wallet hookedWallet;

        private GUIStyle textStyle;
        private GUIStyle boxStyle;
        private Rect leftArrow;
        private Rect rightArrow;

        private void Awake()
        {
            modEnabled = Config.Bind("General", "Enabled", true,
                "Turn the mod on or off (also in the title screen Mods menu). Off = normal difficulty.");
            level = Config.Bind("General", "Level", 0,
                new ConfigDescription("Ascension level for your next run (0 = off). Change it in the hub.",
                    new AcceptableValueRange<int>(0, AscensionLevels.Max)));
            showInRun = Config.Bind("General", "ShowLevelInRun", true,
                "Show a small 'ASCENSION n' tag in the corner during a run.");

            GameController.levelLoadEventHandlers += (next, previous) =>
                Logger.LogInfo($"Level load: '{previous}' -> '{next}'");
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded (level {level.Value})");
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
            }
        }

        private void Tick()
        {
            RefreshPlayers();
            inHub = players.Count > 0 && FindHub();
            if (inHub != wasInHub)
            {
                Logger.LogInfo(inHub ? "Entered the hub" : $"Left the hub - running at Ascension {CurrentLevel()}");
                wasInHub = inHub;
            }

            if (!modEnabled.Value || players.Count == 0 || GameController.pvpOn || inHub)
            {
                // Hub (or off): no modifiers, and the hub is where the level is chosen.
                if (activeLevel != 0)
                    RemovePlayerMods();
                activeLevel = 0;
                if (inHub && modEnabled.Value)
                    HandleLevelInput();
                return;
            }

            activeLevel = CurrentLevel();
            HookGold();
            if (Time.unscaledTime < nextApply)
                return;
            nextApply = Time.unscaledTime + ApplyInterval;
            ApplyPlayerMods();
            ApplyEnemyMods();
        }

        private int CurrentLevel() => Mathf.Clamp(level.Value, 0, AscensionLevels.Max);

        // ---- Hub and level selection ----

        // The hub is the house with the spellbook, wardrobe, relic chest and loadout.
        private bool FindHub()
        {
            if (Time.unscaledTime >= nextHubSearch)
            {
                nextHubSearch = Time.unscaledTime + HubSearchInterval;
                hubUI = FindObjectOfType<PlayerRoomUI>();
            }
            return hubUI != null && hubUI.gameObject.activeInHierarchy;
        }

        private void HandleLevelInput()
        {
            int step = 0;
            if (Input.GetKeyDown(KeyCode.RightBracket))
                step = 1;
            else if (Input.GetKeyDown(KeyCode.LeftBracket))
                step = -1;
            else
                step = StickStep();

            if (step != 0)
                SetLevel(CurrentLevel() + step);
        }

        // Right stick left/right while any player has the character menu open (Select).
        private int StickStep()
        {
            float strongest = 0f;
            foreach (Player player in players)
            {
                if (player.lowerHUD == null || !player.lowerHUD.equipMenuActive)
                    continue;
                float x = RightStickX.Value(player);
                if (Mathf.Abs(x) > Mathf.Abs(strongest))
                    strongest = x;
            }

            if (Mathf.Abs(strongest) < StickThreshold)
            {
                nextStickStep = 0f;
                return 0;
            }
            if (Time.unscaledTime < nextStickStep)
                return 0;
            nextStickStep = Time.unscaledTime + StickRepeatSeconds;
            return strongest > 0f ? 1 : -1;
        }

        private void SetLevel(int value)
        {
            value = Mathf.Clamp(value, 0, AscensionLevels.Max);
            if (value == level.Value)
                return;
            level.Value = value;
            Logger.LogInfo($"Ascension set to {value}");
        }

        // ---- Applying modifiers ----

        private void ApplyPlayerMods()
        {
            int lvl = activeLevel;
            foreach (Player player in players)
            {
                Health health = player.health;
                if (health == null)
                    continue;
                EnsureMod(health.damageTakenStat, PlayerDamageTakenMod, AscensionLevels.EnemyDamage(lvl), false);
                EnsureMod(health.healModifierStat, PlayerHealingMod, AscensionLevels.Healing(lvl), false);
                EnsureMod(health.healthStat, PlayerMaxHealthMod, AscensionLevels.PlayerMaxHealth(lvl), false);
            }
        }

        private void RemovePlayerMods()
        {
            foreach (Player player in players)
            {
                Health health = player.health;
                if (health == null)
                    continue;
                EnsureMod(health.damageTakenStat, PlayerDamageTakenMod, 1f, false);
                EnsureMod(health.healModifierStat, PlayerHealingMod, 1f, false);
                EnsureMod(health.healthStat, PlayerMaxHealthMod, 1f, false);
            }
        }

        private void ApplyEnemyMods()
        {
            int lvl = activeLevel;
            float speed = AscensionLevels.EnemySpeed(lvl);
            var bosses = new HashSet<int>();
            if (GameController.bosses != null)
            {
                foreach (GameObject boss in GameController.bosses)
                {
                    if (boss != null)
                        bosses.Add(boss.GetInstanceID());
                }
            }

            foreach (Enemy enemy in FindObjectsOfType<Enemy>())
            {
                if (enemy == null)
                    continue;
                bool isBoss = bosses.Contains(enemy.gameObject.GetInstanceID())
                    || enemy.GetType().Name.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0;
                float health = AscensionLevels.EnemyHealth(lvl) * (isBoss ? AscensionLevels.BossHealth(lvl) : 1f);

                // Fill to the new max so enemies start at full (higher) health.
                if (enemy.health != null)
                    EnsureMod(enemy.health.healthStat, EnemyHealthMod, health, true);
                if (enemy.movement != null)
                    EnsureMod(enemy.movement.moveSpeedStat, EnemySpeedMod, speed, false);
            }
        }

        // Adds, updates or (for a 1x multiplier) removes one of our multiplicative stat mods.
        private static void EnsureMod(NumVarStat stat, string id, float multiplier, bool fillToNewMax)
        {
            if (stat == null)
                return;

            int index = stat.GetModIndex(id);
            if (index >= 0)
            {
                var existing = stat.modifiers[index] as NumVarStatMod;
                if (existing != null && Mathf.Approximately(existing.modValue, multiplier))
                    return;
                stat.RemoveMod(id);
            }
            if (!Mathf.Approximately(multiplier, 1f))
                stat.AddMod(new NumVarStatMod(id, multiplier, 10, VarStatModType.Multiplicative, fillToNewMax));
        }

        // Gold is deposited through a wallet that may be recreated per run, so (re)hook when it changes.
        private void HookGold()
        {
            Wallet wallet = Player.goldWallet;
            if (wallet == null || wallet == hookedWallet)
                return;
            wallet.preDepositEventHandlers += OnGoldDeposit;
            hookedWallet = wallet;
        }

        private void OnGoldDeposit(ref int amount)
        {
            float multiplier = AscensionLevels.Gold(activeLevel);
            if (activeLevel > 0 && amount > 0 && multiplier < 1f)
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * multiplier));
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
        }

        // ---- Drawing ----

        private void OnGUI()
        {
            if (!modEnabled.Value || players.Count == 0)
                return;
            bool inRun = !inHub && activeLevel > 0 && showInRun.Value;
            if (!inHub && !inRun)
                return;

            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true, wordWrap = false };
                boxStyle = new GUIStyle(GUI.skin.box);
                var background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.75f));
                background.Apply();
                boxStyle.normal.background = background;
            }

            float scale = Screen.height / ReferenceHeight;
            float width = Screen.width / scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            if (inHub)
                DrawHubPanel(width);
            else
                DrawRunTag(width);

            GUI.matrix = previous;
        }

        private void DrawHubPanel(float screenWidth)
        {
            int lvl = CurrentLevel();
            var lines = AscensionLevels.Describe(lvl);
            string title = lvl == 0 ? "<b>ASCENSION  OFF</b>" : $"<b>ASCENSION  <color=#fd5>{lvl}</color></b>";
            string body = string.Join("\n", lines.ToArray());
            const string hint = "<color=#9aa>Select + right stick left/right, or [ and ]</color>";

            var titleContent = new GUIContent(title);
            var bodyContent = new GUIContent(body);
            var hintContent = new GUIContent(hint);
            Vector2 titleSize = textStyle.CalcSize(titleContent);
            Vector2 bodySize = textStyle.CalcSize(bodyContent);
            Vector2 hintSize = textStyle.CalcSize(hintContent);

            float width = Mathf.Max(titleSize.x + 90f, Mathf.Max(bodySize.x, hintSize.x)) + 32f;
            float height = titleSize.y + bodySize.y + hintSize.y + 36f;
            var area = new Rect((screenWidth - width) / 2f, 16f, width, height);
            GUI.Box(area, GUIContent.none, boxStyle);

            float y = area.y + 10f;
            leftArrow = new Rect(area.x + 16f, y, 32f, titleSize.y);
            rightArrow = new Rect(area.xMax - 48f, y, 32f, titleSize.y);
            GUI.Label(new Rect(area.x + (width - titleSize.x) / 2f, y, titleSize.x, titleSize.y), titleContent, textStyle);
            GUI.Label(leftArrow, "<b><</b>", textStyle);
            GUI.Label(rightArrow, "<b>></b>", textStyle);
            y += titleSize.y + 8f;
            GUI.Label(new Rect(area.x + 16f, y, bodySize.x, bodySize.y), bodyContent, textStyle);
            y += bodySize.y + 8f;
            GUI.Label(new Rect(area.x + 16f, y, hintSize.x, hintSize.y), hintContent, textStyle);

            if (Event.current.type == EventType.MouseDown)
            {
                if (leftArrow.Contains(Event.current.mousePosition))
                    SetLevel(lvl - 1);
                else if (rightArrow.Contains(Event.current.mousePosition))
                    SetLevel(lvl + 1);
            }
        }

        private void DrawRunTag(float screenWidth)
        {
            var content = new GUIContent($"<b>ASCENSION <color=#fd5>{activeLevel}</color></b>");
            Vector2 size = textStyle.CalcSize(content);
            var area = new Rect(screenWidth - size.x - 32f, 16f, size.x + 16f, size.y + 8f);
            GUI.Box(area, GUIContent.none, boxStyle);
            GUI.Label(new Rect(area.x + 8f, area.y + 4f, size.x, size.y), content, textStyle);
        }
    }
}
