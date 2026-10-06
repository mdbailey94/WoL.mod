using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WoLAscension
{
    // Ascension levels 1-10. Stepping into a portal that starts a run pauses the game and shows a
    // pixel-art prompt to pick the level; the run then gets that level's cumulative modifiers,
    // applied as the game's own stat modifiers so they stack with relics and are removed in the hub.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class AscensionPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.ascension";
        public const string PluginName = "Ascension";
        public const string PluginVersion = "0.3.0";

        private const string PlayerDamageTakenMod = "Ascension_DamageTaken";
        private const string PlayerHealingMod = "Ascension_Healing";
        private const string PlayerMaxHealthMod = "Ascension_MaxHealth";
        private const string EnemyHealthMod = "Ascension_EnemyHealth";
        private const string EnemySpeedMod = "Ascension_EnemySpeed";

        private const float ApplyInterval = 0.25f;
        private const float TrialCheckInterval = 0.5f;
        // Ignore confirm presses right after opening, so the button that entered the portal
        // doesn't also start the run.
        private const float ConfirmDelay = 0.3f;

        private static AscensionPlugin instance;

        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<int> level;
        private ConfigEntry<bool> showInRun;

        private readonly List<Player> players = new List<Player>();
        // Whether the current scene is part of the Chaos Trials (a tier floor or boss arena).
        private bool inTrials;
        private float nextTrialCheck;
        // Set by a level load out of the trials (death, quitting, finishing); handled in Tick.
        private volatile bool runEnded;
        private float nextApply;
        private Wallet hookedWallet;
        private Wallet hookedGemWallet;
        // The part of a gem the bonus has earned but not yet paid (gems come one or a few at a time).
        private float gemCarry;

        // -1 = no run in progress (hub / before the portal); otherwise the level locked for this run.
        private int runLevel = -1;

        // Prompt state. `pendingLoader` is the portal whose level load we're holding back.
        private bool promptOpen;
        private NextLevelLoader pendingLoader;
        private bool bypassPrompt;
        private float savedTimeScale = 1f;
        private bool savedInputLock;
        private float promptOpenedAt;

        private void Awake()
        {
            instance = this;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Turn the mod on or off (also in the title screen Mods menu). Off = normal difficulty.");
            level = Config.Bind("General", "Level", 0,
                new ConfigDescription("Last Ascension level picked; the portal prompt starts here (0 = off).",
                    new AcceptableValueRange<int>(0, AscensionLevels.Max)));
            showInRun = Config.Bind("General", "ShowLevelInRun", true,
                "Show a small 'ASCENSION n' tag in the corner during a run.");

            try
            {
                new Harmony(PluginGuid).CreateClassProcessor(typeof(PortalPatch)).Patch();
            }
            catch (Exception e)
            {
                Logger.LogError($"Portal hook failed to install; the prompt will only appear when enemies show up: {e.Message}");
            }

            GameController.levelLoadEventHandlers += (next, previous) =>
            {
                Logger.LogInfo($"Level load: '{previous}' -> '{next}'");
                if (IsTrialScene(previous) && !IsTrialScene(next))
                    runEnded = true;
            };
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded (last level {level.Value})");
        }

        // ---- Portal hook ----

        [HarmonyPatch(typeof(NextLevelLoader), nameof(NextLevelLoader.LoadNextLevel))]
        private static class PortalPatch
        {
            // Returning false holds the portal back until the prompt is confirmed.
            private static bool Prefix(NextLevelLoader __instance)
            {
                try
                {
                    return instance == null || instance.OnPortalEntered(__instance);
                }
                catch (Exception e)
                {
                    instance?.Logger.LogError(e);
                    return true;
                }
            }
        }

        private bool OnPortalEntered(NextLevelLoader loader)
        {
            if (bypassPrompt || promptOpen || !modEnabled.Value || GameController.pvpOn || runLevel >= 0)
                return true;

            string destination = loader.nextLevelName;
            bool startsRun = IsTrialScene(destination) && !IsTrialScene();
            Logger.LogInfo($"Portal to '{destination}' (starts a run: {startsRun})");
            if (!startsRun)
                return true;

            OpenPrompt(loader);
            return false;
        }

        // ---- Main loop ----

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
            if (promptOpen)
            {
                HandlePromptInput();
                return;
            }

            RefreshPlayers();
            if (runEnded)
            {
                runEnded = false;
                Logger.LogInfo("Left the trials - Ascension modifiers off");
                runLevel = -1;
                gemCarry = 0f;
                RemovePlayerMods();
            }
            if (Time.unscaledTime >= nextTrialCheck)
            {
                nextTrialCheck = Time.unscaledTime + TrialCheckInterval;
                inTrials = IsTrialScene();
            }

            if (!modEnabled.Value || players.Count == 0 || GameController.pvpOn)
                return;

            // Backup: on a trial floor without having picked at the portal (e.g. a different
            // entrance) - ask now. Only on trial floors, so the plaza's training dummies don't count.
            if (runLevel < 0 && inTrials && AnyEnemy())
            {
                Logger.LogInfo("Run started without the portal prompt; asking now");
                OpenPrompt(null);
                return;
            }

            if (runLevel <= 0)
                return;

            HookGold();
            HookGems();
            if (Time.unscaledTime < nextApply)
                return;
            nextApply = Time.unscaledTime + ApplyInterval;
            ApplyPlayerMods();
            ApplyEnemyMods();
        }

        // The Chaos Trials' floors and boss arenas, by the game's own scene checks. The house, the
        // plaza and the title screen are outside. An empty name means the current scene.
        private static bool IsTrialScene(string levelName = "")
        {
            try
            {
                return NextLevelLoader.InTierScene(levelName) || NextLevelLoader.InBossScene(levelName);
            }
            catch
            {
                return false;
            }
        }

        // ---- Prompt ----

        private void OpenPrompt(NextLevelLoader loader)
        {
            promptOpen = true;
            promptOpenedAt = Time.unscaledTime;
            pendingLoader = loader;
            savedTimeScale = Time.timeScale;
            savedInputLock = ChaosInputDevice.lockControllerInput;
            Time.timeScale = 0f;
            ChaosInputDevice.lockControllerInput = true;
        }

        private void HandlePromptInput()
        {
            int step = PadInput.Horizontal();
            if (step != 0)
                level.Value = Mathf.Clamp(level.Value + step, 0, AscensionLevels.Max);
            if (PadInput.Confirm() && Time.unscaledTime - promptOpenedAt >= ConfirmDelay)
                ConfirmPrompt();
        }

        private void ConfirmPrompt()
        {
            promptOpen = false;
            runLevel = Mathf.Clamp(level.Value, 0, AscensionLevels.Max);
            Time.timeScale = savedTimeScale;
            ChaosInputDevice.lockControllerInput = savedInputLock;
            Logger.LogInfo($"Starting run at Ascension {runLevel}");

            NextLevelLoader loader = pendingLoader;
            pendingLoader = null;
            if (loader == null)
                return;
            bypassPrompt = true;
            try
            {
                loader.LoadNextLevel();
            }
            finally
            {
                bypassPrompt = false;
            }
        }

        // ---- Applying modifiers ----

        private void ApplyPlayerMods()
        {
            foreach (Player player in players)
            {
                Health health = player.health;
                if (health == null)
                    continue;
                EnsureMod(health.damageTakenStat, PlayerDamageTakenMod, AscensionLevels.EnemyDamage(runLevel), false);
                EnsureMod(health.healModifierStat, PlayerHealingMod, AscensionLevels.Healing(runLevel), false);
                EnsureMod(health.healthStat, PlayerMaxHealthMod, AscensionLevels.PlayerMaxHealth(runLevel), false);
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
            float speed = AscensionLevels.EnemySpeed(runLevel);
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
                float health = AscensionLevels.EnemyHealth(runLevel) * (isBoss ? AscensionLevels.BossHealth(runLevel) : 1f);

                // Fill to the new max so enemies start at full (higher) health.
                if (enemy.health != null)
                    EnsureMod(enemy.health.healthStat, EnemyHealthMod, health, true);
                if (enemy.movement != null)
                    EnsureMod(enemy.movement.moveSpeedStat, EnemySpeedMod, speed, false);
            }
        }

        private static bool AnyEnemy() => FindObjectOfType<Enemy>() != null;

        // Adds, updates or (for a 1x multiplier) removes one of our stat mods. The game's
        // Multiplicative mod value is the *change*: 0.15 means +15%, -0.15 means -15%
        // (passing 0.85 for "x0.85" actually gave +85%).
        private void EnsureMod(NumVarStat stat, string id, float multiplier, bool fillToNewMax)
        {
            if (stat == null)
                return;

            float change = multiplier - 1f;
            int index = stat.GetModIndex(id);
            if (index >= 0)
            {
                var existing = stat.modifiers[index] as NumVarStatMod;
                if (existing != null && Mathf.Approximately(existing.modValue, change))
                    return;
                stat.RemoveMod(id);
            }
            if (Mathf.Approximately(change, 0f))
                return;

            float before = stat.ModifiedValue;
            stat.AddMod(new NumVarStatMod(id, change, 10, VarStatModType.Multiplicative, fillToNewMax));
            if (loggedMods.Add(id))
                Logger.LogInfo($"{id}: x{multiplier:0.##} -> {before:0.##} became {stat.ModifiedValue:0.##}");
        }

        // Each mod's first application is logged so the log shows the real before/after values.
        private readonly HashSet<string> loggedMods = new HashSet<string>();

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
            float multiplier = AscensionLevels.Gold(runLevel);
            if (runLevel > 0 && amount > 0 && multiplier < 1f)
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * multiplier));
        }

        // Chaos gems the same way: a little more at higher Ascension. Pickups are small, so the
        // fraction the bonus earns is carried over to the next one rather than rounded away.
        private void HookGems()
        {
            Wallet wallet = Player.platWallet;
            if (wallet == null || wallet == hookedGemWallet)
                return;
            wallet.preDepositEventHandlers += OnGemDeposit;
            hookedGemWallet = wallet;
        }

        private void OnGemDeposit(ref int amount)
        {
            float multiplier = AscensionLevels.Gems(runLevel);
            if (runLevel <= 0 || amount <= 0 || multiplier <= 1f)
                return;
            float exact = amount * multiplier + gemCarry;
            int paid = Mathf.FloorToInt(exact);
            gemCarry = exact - paid;
            if (paid > amount && !loggedGems)
            {
                loggedGems = true;
                Logger.LogInfo($"Chaos gems x{multiplier:0.##}: {amount} became {paid}");
            }
            amount = paid;
        }

        private bool loggedGems;

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
            if (promptOpen)
                DrawPrompt();
            else if (modEnabled.Value && showInRun.Value && runLevel > 0 && players.Count > 0 && inTrials)
                DrawRunTag();
        }

        private void DrawPrompt()
        {
            int u = PixelUI.Unit;
            int lvl = Mathf.Clamp(level.Value, 0, AscensionLevels.Max);

            // Dim the paused game behind the prompt.
            PixelUI.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));

            GUIStyle title = PixelUI.Style(9 * u, PixelUI.Border, TextAnchor.MiddleCenter);
            GUIStyle levelStyle = PixelUI.Style(6 * u, PixelUI.Accent, TextAnchor.MiddleCenter);
            GUIStyle body = PixelUI.Style(4 * u, PixelUI.Text, TextAnchor.MiddleCenter);
            GUIStyle hint = PixelUI.Style(4 * u, PixelUI.Dim, TextAnchor.MiddleCenter);

            List<string> lines = AscensionLevels.Describe(lvl);
            float lineHeight = body.CalcSize(new GUIContent("Ag")).y;
            float bodyWidth = 0f;
            foreach (string line in lines)
                bodyWidth = Mathf.Max(bodyWidth, body.CalcSize(new GUIContent(line)).x);

            const int pips = AscensionLevels.Max;
            int pipSize = 6 * u;
            int pipGap = 2 * u;
            float pipRowWidth = pips * pipSize + (pips - 1) * pipGap;
            float arrowGap = 6 * u;

            float width = Mathf.Max(pipRowWidth + 2 * (arrowGap + 5 * u), bodyWidth) + 24 * u;
            float titleHeight = title.CalcSize(new GUIContent("ASCENSION")).y;
            float levelHeight = levelStyle.CalcSize(new GUIContent("LEVEL 10")).y;
            float height = 6 * u + titleHeight + 4 * u + pipSize + 4 * u + levelHeight + 4 * u
                           + lines.Count * lineHeight + 6 * u + lineHeight + 8 * u;

            var panel = new Rect(Mathf.Round((Screen.width - width) / 2f), Mathf.Round((Screen.height - height) / 2f),
                                 Mathf.Round(width), Mathf.Round(height));
            PixelUI.Panel(panel, u);

            float y = panel.y + 6 * u;
            GUI.Label(new Rect(panel.x, y, panel.width, titleHeight), "ASCENSION", title);
            y += titleHeight + 4 * u;

            // < pips >
            float pipX = panel.x + (panel.width - pipRowWidth) / 2f;
            float pipCenterY = y + pipSize / 2f;
            Rect left = PixelUI.Arrow(pipX - arrowGap - 4 * u, pipCenterY, 7, false, lvl > 0 ? PixelUI.Border : PixelUI.PipEmpty, u);
            for (int i = 0; i < pips; i++)
                PixelUI.Pip(new Rect(pipX + i * (pipSize + pipGap), y, pipSize, pipSize), i < lvl, u);
            Rect right = PixelUI.Arrow(pipX + pipRowWidth + arrowGap, pipCenterY, 7, true,
                                       lvl < AscensionLevels.Max ? PixelUI.Border : PixelUI.PipEmpty, u);
            y += pipSize + 4 * u;

            GUI.Label(new Rect(panel.x, y, panel.width, levelHeight), lvl == 0 ? "OFF" : $"LEVEL {lvl}", levelStyle);
            y += levelHeight + 4 * u;

            foreach (string line in lines)
            {
                GUI.Label(new Rect(panel.x, y, panel.width, lineHeight), line, body);
                y += lineHeight;
            }
            y += 6 * u;
            GUI.Label(new Rect(panel.x, y, panel.width, lineHeight), "< >  choose        A / Enter  begin", hint);

            if (Event.current.type == EventType.MouseDown)
            {
                if (left.Contains(Event.current.mousePosition))
                    level.Value = Mathf.Max(0, lvl - 1);
                else if (right.Contains(Event.current.mousePosition))
                    level.Value = Mathf.Min(AscensionLevels.Max, lvl + 1);
                Event.current.Use();
            }
        }

        private void DrawRunTag()
        {
            int u = PixelUI.Unit;
            GUIStyle style = PixelUI.Style(4 * u, PixelUI.Text);
            var content = new GUIContent($"ASCENSION <color=#ff8a3d>{runLevel}</color>");
            Vector2 size = style.CalcSize(content);
            var panel = new Rect(Screen.width - size.x - 14 * u, 4 * u, size.x + 10 * u, size.y + 6 * u);
            PixelUI.Panel(panel, u);
            GUI.Label(new Rect(panel.x + 5 * u, panel.y + 3 * u, size.x, size.y), content, style);
        }
    }
}
