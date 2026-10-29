using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WoLAscension
{
    // Ascension levels 1-10. The level is set at an Ascension altar standing beside the Chaos
    // Trials portal in the plaza (interact to raise it; the game's own on-screen notice shows the level
    // and what it does). Stepping into the portal starts the run at that level, with no pause: the
    // run gets the level's cumulative modifiers, applied as the game's own stat modifiers so they
    // stack with relics and are removed in the hub.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class AscensionPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.ascension";
        public const string PluginName = "Ascension";
        public const string PluginVersion = "0.8.0";

        private const string PlayerDamageTakenMod = "Ascension_DamageTaken";
        private const string PlayerHealingMod = "Ascension_Healing";
        private const string PlayerMaxHealthMod = "Ascension_MaxHealth";
        private const string EnemyHealthMod = "Ascension_EnemyHealth";
        private const string EnemySpeedMod = "Ascension_EnemySpeed";

        private const float ApplyInterval = 0.25f;
        private const float TrialCheckInterval = 0.5f;
        private const float AltarCheckInterval = 1f;

        private static AscensionPlugin instance;

        public static void Log(string message) => instance?.Logger.LogInfo(message);

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

        private AscensionAltar altar;
        private float nextAltarCheck;

        private void Awake()
        {
            instance = this;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Turn the mod on or off (also in the title screen Mods menu). Off = normal difficulty.");
            level = Config.Bind("General", "Level", 0,
                new ConfigDescription("The Ascension level set at the altar; runs start at it (0 = off).",
                    new AcceptableValueRange<int>(0, AscensionLevels.Max)));
            showInRun = Config.Bind("General", "ShowLevelInRun", true,
                "Show a small 'ASCENSION n' tag in the corner during a run.");
            // A new key (0.7.4): the fresh save now starts at 0 (Ascension off) and the old
            // FreshSave key started at 1, so it isn't carried over.
            unlockedFresh = Config.Bind("Unlocks", "FreshSaveHighest", 0,
                new ConfigDescription("Chaos Depths' fresh save: the highest level the altar offers. It starts at 0 (off): beating a run at 0 unlocks 1, beating 1 unlocks 2, and so on (#32).",
                    new AcceptableValueRange<int>(0, AscensionLevels.Max)));
            unlockedOther = Config.Bind("Unlocks", "OtherSaves", AscensionLevels.Max,
                new ConfigDescription("Every other save (the base game, Chaos Depths' Debugging save): the highest level the altar offers.",
                    new AcceptableValueRange<int>(1, AscensionLevels.Max)));
            AscensionAltar.RememberedPortal = Config.Bind("Altar", "LastPortal", string.Empty,
                "Where the trials portal was when a run last started (found automatically). Don't edit.");
            AscensionAltar.Offset = new Vector2(
                Config.Bind("Altar", "LeftOfPortalX", -3.5f, "Where the altar stands, in game units from the trials portal (negative = left).").Value,
                Config.Bind("Altar", "LeftOfPortalY", 0f, "Where the altar stands, in game units from the trials portal (negative = down).").Value);
            AscensionAltar.Effects = Config.Bind("Altar", "Effects", true,
                "The altar's glow, ground ring, embers and light. Off leaves just the totem, its flame and runes.");
            // The level table ([Level 01] to [Level 10], 0.8.0).
            AscensionLevels.Bind(Config);

            var harmony = new Harmony(PluginGuid);
            try
            {
                harmony.CreateClassProcessor(typeof(PortalPatch)).Patch();
            }
            catch (Exception e)
            {
                Logger.LogError($"Portal hook failed to install; the level will be set when enemies show up: {e.Message}");
            }
            // Raising the level at the altar must not also fire the attack or dash that shares its
            // button (see AscensionAltar.PassInput).
            try
            {
                harmony.CreateClassProcessor(typeof(AscensionAltar.ButtonDownPatch)).Patch();
                harmony.CreateClassProcessor(typeof(AscensionAltar.ButtonPatch)).Patch();
            }
            catch (Exception e)
            {
                Logger.LogError($"Input hook failed to install; interacting at the altar may also attack: {e.Message}");
            }

            GameController.levelLoadEventHandlers += (next, previous) =>
            {
                Logger.LogInfo($"Level load: '{previous}' -> '{next}'");
                // Out of the final boss's arena to anywhere but the Hub or the house (dying goes
                // back there): the game is won.
                if (IsFinalBoss(previous) && !IsTrialScene(next) && next != "Hub" && next != "PlayerRoom")
                    RunBeaten("the final boss was beaten");
                if (IsTrialScene(previous) && !IsTrialScene(next))
                    runEnded = true;
            };
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded (last level {level.Value})");
        }

        // ---- Portal hook ----

        [HarmonyPatch(typeof(NextLevelLoader), nameof(NextLevelLoader.LoadNextLevel))]
        private static class PortalPatch
        {
            // Never holds the portal back: the level is already chosen at the altar.
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
            if (!modEnabled.Value || GameController.pvpOn || runLevel >= 0)
                return true;

            string destination = loader.nextLevelName;
            bool startsRun = IsTrialScene(destination) && !IsTrialScene();
            Logger.LogInfo($"Portal to '{destination}' (starts a run: {startsRun})");
            if (startsRun)
            {
                AscensionAltar.RememberPortal(loader);
                StartRun();
            }
            return true;
        }

        // ---- Unlocking levels in order (#32) ----

        private ConfigEntry<int> unlockedFresh, unlockedOther;

        // Chaos Depths' fresh save is on (it says so through the process's environment).
        private static bool FreshSave
        {
            get
            {
                try
                {
                    return Environment.GetEnvironmentVariable("CHAOSDEPTHS_FRESH_SAVE") == "1";
                }
                catch
                {
                    return false;
                }
            }
        }

        // Chaos Depths' save slot (0 = its Debugging save or no Chaos Depths; 1, 2, 3... its saves).
        private static int SaveSlot
        {
            get
            {
                try
                {
                    int n;
                    string v = Environment.GetEnvironmentVariable("CHAOSDEPTHS_SAVE_SLOT");
                    if (!string.IsNullOrEmpty(v) && int.TryParse(v, out n))
                        return Mathf.Max(0, n);
                    return FreshSave ? 1 : 0;   // a Chaos Depths without slots: its one fresh save
                }
                catch
                {
                    return 0;
                }
            }
        }

        private readonly Dictionary<int, ConfigEntry<int>> unlockedSlots = new Dictionary<int, ConfigEntry<int>>();

        // Each save slot keeps its own highest unlocked level (0.7.5): Save 1's in
        // FreshSaveHighest, Save 2's in Save2Highest, and so on; every other save in OtherSaves.
        private ConfigEntry<int> UnlockedEntry
        {
            get
            {
                int s = SaveSlot;
                if (s <= 0)
                    return unlockedOther;
                if (s == 1)
                    return unlockedFresh;
                ConfigEntry<int> entry;
                if (!unlockedSlots.TryGetValue(s, out entry))
                {
                    entry = Config.Bind("Unlocks", $"Save{s}Highest", 0,
                        new ConfigDescription($"Chaos Depths' save {s}: the highest level the altar offers (starts at 0; beating a run at it unlocks the next).",
                            new AcceptableValueRange<int>(0, AscensionLevels.Max)));
                    unlockedSlots[s] = entry;
                }
                return entry;
            }
        }

        // The highest level the altar offers in this save.
        public static int Unlocked => instance == null || instance.UnlockedEntry == null
            ? AscensionLevels.Max : Mathf.Clamp(instance.UnlockedEntry.Value, 0, AscensionLevels.Max);

        // Called by Chaos Depths when a dive is complete (its final boss, or its last realm boss).
        public static void ReportRunBeaten() => instance?.RunBeaten("Chaos Depths: dive complete");

        private bool runBeatenLogged;

        // A run at the highest unlocked level was beaten: the next level opens.
        private void RunBeaten(string why)
        {
            if (runLevel < 0 || runBeatenLogged)
                return;
            runBeatenLogged = true;
            ConfigEntry<int> entry = UnlockedEntry;
            int next = Mathf.Min(AscensionLevels.Max, runLevel + 1);
            Logger.LogInfo($"Run beaten at Ascension {runLevel} ({why})");
            if (entry == null || next <= entry.Value)
                return;
            entry.Value = next;
            Logger.LogInfo($"Ascension {next} unlocked ({(SaveSlot > 0 ? $"Chaos Depths save {SaveSlot}" : "this save")})");
            GameBanner.Show($"ASCENSION {next} UNLOCKED", "Choose it at the altar by the trials portal", AscensionAltar.Icon(next));
        }

        private static bool IsFinalBoss(string scene) =>
            !string.IsNullOrEmpty(scene) && scene.IndexOf("FinalBoss", StringComparison.OrdinalIgnoreCase) >= 0;

        // The run is locked to the altar's level; the banner says so as you go in.
        private void StartRun()
        {
            runBeatenLogged = false;
            runLevel = Mathf.Clamp(level.Value, 0, Unlocked);
            Logger.LogInfo($"Starting run at Ascension {runLevel}");
            if (runLevel > 0)
                GameBanner.Show($"ASCENSION {runLevel}", AscensionLevels.Summary(runLevel), AscensionAltar.Icon(runLevel));
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
            RefreshPlayers();
            if (runEnded)
            {
                runEnded = false;
                Logger.LogInfo("Left the trials - Ascension modifiers off");
                runLevel = -1;
                gemCarry = 0f;
                animSped.Clear();
                RemovePlayerMods();
            }
            if (Time.unscaledTime >= nextTrialCheck)
            {
                nextTrialCheck = Time.unscaledTime + TrialCheckInterval;
                inTrials = IsTrialScene();
            }

            if (!modEnabled.Value || players.Count == 0 || GameController.pvpOn)
                return;

            // In the hub: make sure the plaza has its altar by the trials portal.
            if (!inTrials && Time.unscaledTime >= nextAltarCheck)
            {
                nextAltarCheck = Time.unscaledTime + AltarCheckInterval;
                if (altar == null)
                    altar = AscensionAltar.SpawnBesideTrialsPortal(level, Logger);
            }

            // Backup: on a trial floor without coming through the portal hook (e.g. a different
            // entrance): start at the altar's level now. Only on trial floors, so the plaza's
            // training dummies don't count.
            if (runLevel < 0 && inTrials && AnyEnemy())
            {
                Logger.LogInfo("Run started without the portal hook; using the altar's level");
                StartRun();
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
                // "Move and attack faster" (the table, 0.7.3): their animations, which pace their
                // wind-ups and swings, run faster by the same amount, the way the game's own
                // attack-speed relic speeds up the wizard. Once per enemy, so it never stacks.
                if (speed > 1f && enemy.animSpeedHandler != null && animSped.Add(enemy.GetInstanceID()))
                {
                    try
                    {
                        enemy.animSpeedHandler.Modify(new NumVarStatMod(EnemyAnimMod, speed - 1f, 10, VarStatModType.Multiplicative, false), true);
                    }
                    catch (Exception e)
                    {
                        if (!loggedAnimError)
                        {
                            loggedAnimError = true;
                            Logger.LogWarning($"Enemy attack speed: {e.Message}");
                        }
                    }
                }
            }
        }

        private const string EnemyAnimMod = "Ascension_EnemyAnimSpeed";
        // Enemies whose animations were sped up (instance ids; the ids aren't reused within a run).
        private readonly HashSet<int> animSped = new HashSet<int>();
        private bool loggedAnimError;

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
            if (modEnabled.Value && showInRun.Value && runLevel > 0 && players.Count > 0 && inTrials)
                DrawRunTag();
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
