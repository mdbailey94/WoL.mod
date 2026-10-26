using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Rewired;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WoLChaosDepths
{
    // Chaos Depths, M0 technical test 1: a persistent character on top of a normal run.
    // - Its own save file per player (BepInEx\config\ChaosDepths\characterN.json), never the game's.
    // - Experience from kills in the trials, levels with a little health each, a notice on level up.
    // - Random items rolled from a data file on some kills (always on a boss), kept in the save,
    //   and (until there is an inventory screen) equipped automatically when better than what's
    //   in the slot. Their stats go on through the game's own stat modifiers.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class ChaosDepthsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.chaosdepths";
        public const string PluginName = "Chaos Depths";
        public const string PluginVersion = "0.3.10";


        private static BepInEx.Logging.ManualLogSource log;
        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<float> healthPerLevel, xpPerHealth, bossXpMultiplier, dropChance;
        private static ConfigEntry<int> levelCap, bossDropRarity;
        private static ConfigEntry<bool> autoEquip, logKills;
        private static ConfigEntry<string> panelKey, panelButton;
        private static ConfigEntry<bool> diveEnabled, debugEnabled;
        public static bool DebugEnabled => debugEnabled?.Value ?? false;
        private static ConfigEntry<string> diveRealm;
        private static ConfigEntry<int> floorsPerRealm, realmsPerDive;

        public static bool DiveEnabled => (modEnabled?.Value ?? false) && (diveEnabled?.Value ?? false);
        public static string DiveRealm => diveRealm?.Value ?? "Game";
        public static int FloorsPerRealm => Mathf.Clamp(floorsPerRealm?.Value ?? 0, 0, 5);
        public static int RealmsPerDive => Mathf.Clamp(realmsPerDive?.Value ?? 0, 0, 6);
        private static KeyCode openKey = KeyCode.I;
        private InventoryPanel panel;

        public static float HealthPerLevel => Mathf.Clamp(healthPerLevel?.Value ?? 0.02f, 0f, 0.1f);
        public static void Log(string message) => log?.LogInfo(message);
        public static void LogError(string message) => log?.LogError(message);

        private readonly Dictionary<int, CharacterSave> characters = new Dictionary<int, CharacterSave>();
        private readonly Dictionary<int, Player> lastHitBy = new Dictionary<int, Player>();
        private readonly Dictionary<int, float> lastKillTime = new Dictionary<int, float>();
        private readonly HashSet<int> deathHooked = new HashSet<int>();
        private float nextTick;

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Chaos Depths character progression (EXPERIMENTAL technical test).");
            logKills = Config.Bind("General", "LogKills", false, "Log the experience of every kill.");
            healthPerLevel = Config.Bind("Progression", "HealthPerLevel", 0.02f,
                new ConfigDescription("Max health gained per level, as a fraction (0.02 = +2%).", new AcceptableValueRange<float>(0f, 0.1f)));
            xpPerHealth = Config.Bind("Progression", "XpPerEnemyHealth", 0.125f,
                new ConfigDescription("Experience per point of the slain enemy's max health (at least 1 per kill).", new AcceptableValueRange<float>(0.01f, 2f)));
            bossXpMultiplier = Config.Bind("Progression", "BossXpMultiplier", 10f,
                new ConfigDescription("Bosses give this many times the experience their health would.", new AcceptableValueRange<float>(1f, 50f)));
            levelCap = Config.Bind("Progression", "LevelCap", 50, new ConfigDescription("Highest level.", new AcceptableValueRange<int>(2, 99)));
            dropChance = Config.Bind("Loot", "DropChance", 0.15f,
                new ConfigDescription("Chance a kill drops an item (bosses always do).", new AcceptableValueRange<float>(0f, 1f)));
            bossDropRarity = Config.Bind("Loot", "BossDropMinRarity", 2,
                new ConfigDescription("A boss drop is at least this rarity (0 Common, 1 Magic, 2 Rare, 3 Legendary).", new AcceptableValueRange<int>(0, 3)));
            autoEquip = Config.Bind("Loot", "AutoEquip", true,
                "Equip a found item when it's better than the one in its slot (there's no inventory screen yet).");

            panelKey = Config.Bind("Panel", "KeyboardKey", "I", "Keyboard key that opens the inventory (a Unity KeyCode name, e.g. I, Tab, F1).");
            panelButton = Config.Bind("Panel", "ControllerButton", "RightStick",
                "Controller button that opens the inventory: RightStick (R3), LeftStick (L3), Back (Select/View) or None.");
            try
            {
                openKey = (KeyCode)Enum.Parse(typeof(KeyCode), panelKey.Value.Trim(), true);
            }
            catch
            {
                Logger.LogWarning($"Unknown key '{panelKey.Value}', using I");
                openKey = KeyCode.I;
            }
            panel = new InventoryPanel(OnInventoryChanged);
            diveEnabled = Config.Bind("Dive", "Enabled", true,
                "Shape the run as a dive (the settings below) and log what the game does with it.");
            diveRealm = Config.Bind("Dive", "Realm", "Game",
                new ConfigDescription("Which realm a run starts in. Game = the game's own order.",
                    new AcceptableValueList<string>("Game", "Earth", "Fire", "Water", "Air", "Lightning")));
            floorsPerRealm = Config.Bind("Dive", "FloorsPerRealm", 0,
                new ConfigDescription("Floors before each realm's boss. 0 = the game's own count.", new AcceptableValueRange<int>(0, 5)));
            realmsPerDive = Config.Bind("Dive", "RealmsPerDive", 0,
                new ConfigDescription("Realm bosses to beat before the dive ends and you return to the Hub. 0 = the whole run as the game plays it.",
                    new AcceptableValueRange<int>(0, 6)));
            debugEnabled = Config.Bind("Debug", "Enabled", false,
                "Test keys: F6 kill all enemies, F7 skip floor, F8 god mode, F9 gold, F10 auto-dive (kill and skip floor after floor until the run ends).");
            try
            {
                DiveDirector.Install(new Harmony(PluginGuid));
            }
            catch (Exception e)
            {
                Logger.LogError($"Dive director failed to install: {e.Message}");
            }

            LootTable.Load();
            try
            {
                Health.globalTakeDamageActualHandlers += OnAnyDamage;
            }
            catch (Exception e)
            {
                Logger.LogError($"Couldn't hook damage: {e.Message}");
            }
            SceneManager.activeSceneChanged += (from, to) =>
            {
                panel?.Close();
                SaveAll();
                lastHitBy.Clear();
                lastKillTime.Clear();
                deathHooked.Clear();
            };
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded (EXPERIMENTAL); saves in {SaveStore.Folder}");
        }

        private void OnApplicationQuit() => SaveAll();

        // Twice a second: make sure each active player has a character and their stats on.
        private void Update()
        {
            if (!modEnabled.Value)
                return;
            DebugKeys.Update(this);
            if (panel.Open)
            {
                panel.Update();
                return;
            }
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            Player opener = OpenRequested(players);
            if (opener != null)
            {
                panel.Show(opener, CharacterOf(opener));
                return;
            }
            if (Time.unscaledTime < nextTick)
                return;
            nextTick = Time.unscaledTime + 0.5f;
            DiveDirector.Tick();
            foreach (Player player in players)
            {
                if (player == null)
                    continue;
                StatMods.Apply(player, CharacterOf(player));
            }
        }

        private void OnGUI()
        {
            if (modEnabled.Value && panel != null && panel.Open)
                panel.Draw();
        }

        private void OnInventoryChanged(Player player, CharacterSave save)
        {
            StatMods.Apply(player, save);
            SaveStore.Save(player.playerID, save);
        }

        // ---- Opening the panel ----

        public static bool OpenKeyPressed() => Input.GetKeyDown(openKey);

        // The player who asked for the inventory this frame: the keyboard key (first player), or
        // the controller button on a pad a player holds. Not while the game's own pause menu is
        // up, or mid-dash or attack (idle and running only, like the shops).
        private float keyOpenUntil;

        private Player OpenRequested(Player[] players)
        {
            try
            {
                if (TimeScaleController.paused)
                    return null;
                // A key press mid-dash or mid-attack waits up to 0.75 s for the wizard to be free.
                if (OpenKeyPressed())
                    keyOpenUntil = Time.unscaledTime + 0.75f;
                if (keyOpenUntil > 0f)
                {
                    foreach (Player player in players)
                        if (player != null && CanOpen(player))
                        {
                            keyOpenUntil = 0f;
                            return player;
                        }
                    if (Time.unscaledTime > keyOpenUntil)
                    {
                        keyOpenUntil = 0f;
                        Player first = players.Length > 0 ? players[0] : null;
                        Log($"Inventory: not opened, the wizard was busy ({(first != null && first.fsm != null ? first.fsm.currentStateName : "?")})");
                    }
                    return null;
                }
                if (!ReInput.isReady)
                    return null;
                foreach (Player player in players)
                {
                    if (player == null || player.inputDevice == null || player.inputDevice.rewiredPlayer == null)
                        continue;
                    foreach (Joystick joystick in player.inputDevice.rewiredPlayer.controllers.Joysticks)
                    {
                        if (ButtonJustPressed(joystick) && CanOpen(player))
                            return player;
                    }
                }
            }
            catch (Exception e)
            {
                LogError($"open check: {e.Message}");
            }
            return null;
        }

        private static bool CanOpen(Player player)
        {
            try
            {
                if (!player.gameObject.activeInHierarchy || (GameUI.dialogManager != null && GameUI.dialogManager.IsActive))
                    return false;
                string state = player.fsm != null ? player.fsm.currentStateName : null;
                return state == null || state.Contains("Idle") || state.Contains("Run");
            }
            catch
            {
                return true;
            }
        }

        // XInput order: A0 B1 X2 Y3 LB4 RB5 Back6 Start7 LS8 RS9; named pads by button name.
        private static bool ButtonJustPressed(Joystick joystick)
        {
            if (joystick == null || joystick.Buttons == null)
                return false;
            string which = (panelButton.Value ?? "").Trim().ToLowerInvariant();
            int index;
            string[] names;
            switch (which)
            {
                case "leftstick": index = 8; names = new[] { "left stick button", "l3", "ls", "left stick" }; break;
                case "back": index = 6; names = new[] { "back", "select", "view", "share" }; break;
                case "none": return false;
                default: index = 9; names = new[] { "right stick button", "r3", "rs", "right stick" }; break;
            }
            bool generic = true;
            foreach (Rewired.Controller.Button button in joystick.Buttons)
            {
                if (button != null && !string.IsNullOrEmpty(button.name) && !button.name.StartsWith("Button ", StringComparison.OrdinalIgnoreCase))
                {
                    generic = false;
                    break;
                }
            }
            for (int i = 0; i < joystick.Buttons.Count; i++)
            {
                Rewired.Controller.Button button = joystick.Buttons[i];
                if (button == null || !button.justPressed)
                    continue;
                if (generic ? i == index : !string.IsNullOrEmpty(button.name) && Array.IndexOf(names, button.name.Trim().ToLowerInvariant()) >= 0)
                    return true;
            }
            return false;
        }

        private CharacterSave CharacterOf(Player player)
        {
            CharacterSave save;
            if (!characters.TryGetValue(player.playerID, out save))
            {
                save = SaveStore.Load(player.playerID);
                characters[player.playerID] = save;
                Log($"Player {player.playerID + 1}: {save.name}, level {save.level}, {save.xp}/{XpToNext(save.level)} xp, " +
                    $"{save.inventory.Count} item(s), equipped: {Equipped(save)}");
            }
            return save;
        }

        private void SaveAll()
        {
            foreach (KeyValuePair<int, CharacterSave> pair in characters)
                SaveStore.Save(pair.Key, pair.Value);
        }

        // ---- Kills ----

        private void OnAnyDamage(AttackInfo attack, Entity attacker, Entity hurt)
        {
            try
            {
                if (!modEnabled.Value || attack == null || !(hurt is Enemy))
                    return;
                Player player = FindAttacker(attack, attacker);
                if (player == null)
                    return;
                lastHitBy[hurt.GetInstanceID()] = player;
                Health health = hurt.health;
                if (health != null && deathHooked.Add(health.GetInstanceID()))
                    health.deathEventHandlers += OnEnemyDeath;
            }
            catch (Exception e)
            {
                LogError($"damage hook: {e.Message}");
            }
        }

        private static Player FindAttacker(AttackInfo attack, Entity attacker)
        {
            var player = attacker as Player ?? attack.entity as Player;
            if (player != null)
                return player;
            Player[] players = GameController.activePlayers;
            if (players == null || string.IsNullOrEmpty(attack.skillCategory))
                return null;
            foreach (Player candidate in players)
                if (candidate != null && candidate.skillCategory == attack.skillCategory)
                    return candidate;
            return null;
        }

        private void OnEnemyDeath(Entity entity)
        {
            try
            {
                if (entity == null || !InTrials())
                    return;
                // Credited once per enemy per scene: bosses can announce their death more than once.
                int id = entity.GetInstanceID();
                if (lastKillTime.ContainsKey(id))
                    return;
                lastKillTime[id] = Time.time;
                Player player;
                if (!lastHitBy.TryGetValue(id, out player) || player == null)
                    return;
                CharacterSave save = CharacterOf(player);
                bool boss = IsBoss(entity);
                if (boss)
                    DiveDirector.BossKilled(entity);
                float maxHealth = entity.health != null && entity.health.healthStat != null ? entity.health.healthStat.ModifiedValue : 10f;
                int xp = Mathf.Max(1, Mathf.RoundToInt(maxHealth * xpPerHealth.Value * (boss ? bossXpMultiplier.Value : 1f)));
                save.kills++;
                if (logKills.Value || boss)
                    Log($"{entity.GetType().Name}{(boss ? " (boss)" : "")} slain by player {player.playerID + 1}: +{xp} xp");
                AddXp(player, save, xp);
                if (boss || UnityEngine.Random.value < dropChance.Value)
                    Drop(player, save, boss ? bossDropRarity.Value : 0);
                if (boss)
                    SaveStore.Save(player.playerID, save);
            }
            catch (Exception e)
            {
                LogError($"death hook: {e.Message}");
            }
        }

        private static bool IsBoss(Entity entity)
        {
            try
            {
                if (GameController.bosses != null)
                    foreach (GameObject boss in GameController.bosses)
                        if (boss != null && boss == entity.gameObject)
                            return true;
            }
            catch
            {
            }
            return entity.GetType().Name.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool InTrials()
        {
            try
            {
                return NextLevelLoader.InTierScene("") || NextLevelLoader.InBossScene("");
            }
            catch
            {
                return false;
            }
        }

        // ---- Experience and levels ----

        public static int XpToNext(int level) => Mathf.RoundToInt(60f * Mathf.Pow(Mathf.Max(1, level), 1.4f));

        private void AddXp(Player player, CharacterSave save, int xp)
        {
            if (save.level >= levelCap.Value)
                return;
            save.xp += xp;
            save.totalXp += xp;
            bool levelled = false;
            while (save.level < levelCap.Value && save.xp >= XpToNext(save.level))
            {
                save.xp -= XpToNext(save.level);
                save.level++;
                levelled = true;
            }
            if (!levelled)
                return;
            Notice($"Level {save.level}!", 2.5f);
            Log($"Player {player.playerID + 1} reached level {save.level} ({save.xp}/{XpToNext(save.level)} xp to the next)");
            StatMods.Apply(player, save);
            SaveStore.Save(player.playerID, save);
        }

        // ---- Loot ----

        private void Drop(Player player, CharacterSave save, int minRarity)
        {
            ItemData item = LootTable.Roll(save.level, minRarity);
            save.inventory.Add(item);
            save.itemsFound++;
            string text = $"{item.rarity} {item.name} ({LootTable.Describe(item)})";
            Log($"Player {player.playerID + 1} found: {text}");
            bool equipped = autoEquip.Value && EquipIfBetter(save, item);
            Notice((equipped ? "Equipped: " : "Found: ") + item.rarity + " " + item.name, 3f);
            if (equipped)
            {
                Log($"  equipped in the {item.slot} slot");
                StatMods.Apply(player, save);
            }
            SaveStore.Save(player.playerID, save);
        }

        private static bool EquipIfBetter(CharacterSave save, ItemData item)
        {
            int slot = Array.IndexOf(LootTable.Slots, item.slot);
            if (slot < 0)
                return false;
            ItemData current = StatMods.FindItem(save, save.equipped[slot]);
            if (current != null && LootTable.Score(current) >= LootTable.Score(item))
                return false;
            save.equipped[slot] = item.id;
            return true;
        }

        private static string Equipped(CharacterSave save)
        {
            var parts = new List<string>();
            for (int i = 0; i < LootTable.Slots.Length && i < save.equipped.Count; i++)
            {
                ItemData item = StatMods.FindItem(save, save.equipped[i]);
                parts.Add($"{LootTable.Slots[i]}: {(item != null ? item.rarity + " " + item.name : "-")}");
            }
            return string.Join("; ", parts.ToArray());
        }

        public static void Notice(string message, float seconds = 2.5f)
        {
            try
            {
                GameUI.BroadcastNoticeMessage(message, seconds);
            }
            catch (Exception e)
            {
                LogError($"notice: {e.Message}");
            }
        }
    }
}
