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
        public const string PluginVersion = "0.15.11";


        private static BepInEx.Logging.ManualLogSource log;
        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<float> healthPerLevel, xpPerHealth, bossXpMultiplier, dropChance, freshDropChance, freshDropChanceZero;

        // An ordinary kill's chance of an item: the save slots' balanced rate (lower again at
        // Ascension 0), or the Debugging save's own.
        private static float KillDropChance() => !ProfileSave.Fresh ? dropChance.Value
            : AscensionLink.Level <= 0 ? freshDropChanceZero.Value : freshDropChance.Value;
        private static ConfigEntry<int> levelCap, bossDropRarity, floorBossDropRarity;
        private static ConfigEntry<float> floorBossXpMultiplier, xpToLevelScale;
        private static ConfigEntry<bool> firstBossUnique;
        private static ConfigEntry<bool> autoEquip, logKills;
        private static ConfigEntry<string> panelKey, panelButton;
        private static ConfigEntry<bool> diveEnabled, debugEnabled;
        public static bool DebugEnabled => debugEnabled?.Value ?? false;
        private static ConfigEntry<string> autoDiveRelics, showcaseEnemy, dumpTextures;
        public static string DumpTextures => dumpTextures?.Value ?? "";
        private static ConfigEntry<float> showcaseHealth;
        public static string ShowcaseEnemy => showcaseEnemy?.Value ?? "Knight";
        public static float ShowcaseHealth => showcaseHealth?.Value ?? 0f;
        public static string AutoDiveRelics => autoDiveRelics?.Value ?? "";
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
        public static void LogWarning(string message) => log?.LogWarning(message);

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
            // A new key in 0.15.10 (XpToLevelScale = 2 from 0.15.2 stays in config files unused),
            // so the slower pace reaches existing installs.
            xpToLevelScale = Config.Bind("Progression", "LevelXpScale", 3f,
                new ConfigDescription("Experience each level takes, times the base curve (1 = the pace before 0.15.2; 3 = levels about a third as often).", new AcceptableValueRange<float>(0.25f, 10f)));
            floorBossXpMultiplier = Config.Bind("Progression", "FloorBossXpMultiplier", 4f,
                "Experience multiplier for the floor boss summoned at the skull altar (realm bosses use BossXpMultiplier).");
            floorBossDropRarity = Config.Bind("Loot", "FloorBossDropMinRarity", 1,
                "The floor boss always drops an item of at least this rarity (0 Common, 1 Magic, 2 Rare, 3 Legendary).");
            dropChance = Config.Bind("Loot", "DropChance", 0.15f,
                new ConfigDescription("Chance a kill drops an item (bosses always do).", new AcceptableValueRange<float>(0f, 1f)));
            freshDropChance = Config.Bind("Loot", "FreshDropChance", 0.06f,
                new ConfigDescription("DropChance in the profile's fresh save (WoL Player's balanced rates, #31); DropChance is used in the Debugging save.", new AcceptableValueRange<float>(0f, 1f)));
            freshDropChanceZero = Config.Bind("Loot", "FreshDropChanceAtAscension0", 0.03f,
                new ConfigDescription("FreshDropChance at Ascension 0 (Matt: the game on its own comes first; from level 1, FreshDropChance).", new AcceptableValueRange<float>(0f, 1f)));
            bossDropRarity = Config.Bind("Loot", "BossDropMinRarity", 2,
                new ConfigDescription("A boss drop is at least this rarity (0 Common, 1 Magic, 2 Rare, 3 Legendary).", new AcceptableValueRange<int>(0, 3)));
            firstBossUnique = Config.Bind("Testing", "FirstBossUnique", true,
                "For test runs: each character's first boss win (a realm boss or the final boss) drops a unique Legendary.");
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
            Instance = this;
            try
            {
                InProfile = Environment.GetEnvironmentVariable("CHAOSDEPTHS_PROFILE") == "1";
            }
            catch
            {
                InProfile = false;
            }
            panel = new InventoryPanel(OnInventoryChanged);
            InventoryPanel.BindLook(Config);
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
            Elites.Bind(Config);
            EliteMarkers.Bind(Config);
            Uniques.Bind(Config);
            Depth.Bind(Config);
            Layers.Bind(Config);
            Pickups.Bind(Config);
            Adornments.Bind(Config);
            ProfileSave.Bind(Config);
            Pickups.Collected = Collect;
            debugEnabled = Config.Bind("Debug", "Enabled", false,
                "Test keys: F6 kill all enemies, F7 skip floor, F8 god mode, F9 gold, F10 auto-dive (kill and skip floor after floor until the run ends), F5 spawn one elite of each affix, F3 open a rift on this floor, F4 give one of each unique legendary, F11 log every relic's ID and name.");
            Hitches.Bind(Config);
            PortalProbe.Bind(Config);
            showcaseEnemy = Config.Bind("Debug", "ShowcaseEnemy", "Knight",
                "F5 spawns one elite of each affix of this enemy beside the wizard (in the trials). Game names, case matters: Knight, Archer, Rogue, Lancer, SkeletonMage, Summoner, Ghoul, GhoulLarge, Blob, Spider, Cyclops, and the elemental forms (FrostKnight, EarthArcher, FlameRogue, WindLancer, ThunderMage...). A name the game doesn't know logs the full list and spawns Knights. For status-effect screenshots, GhoulLarge with ShowcaseHealth 30 is big, slow and visible.");
            showcaseHealth = Config.Bind("Debug", "ShowcaseHealth", 0f,
                new ConfigDescription("0 = F5 spawns the three elites. Above 0 = F5 spawns three plain (no affix, no guard or evade) enemies instead, with this many times their health, to hold still for screenshots of burns and freezes.", new AcceptableValueRange<float>(0f, 100f)));
            dumpTextures = Config.Bind("Debug", "DumpTextures", "Ice,Frozen,Burn,Fire,ChaosGem,Status,Pickup",
                "F12 writes every loaded game texture whose name contains one of these (comma separated, case ignored) as a PNG at true size into the mod's save folder under dump\\, with sprites.txt listing the sprites cut from each. Press it on a floor where the effect has been seen, so its textures are loaded.");
            autoDiveRelics = Config.Bind("Debug", "AutoDiveRelics", "",
                "Relic IDs (comma separated) given to the first player when the auto-dive (F10) starts, e.g. one that speeds up getting to the exit room. Press F11 in a run to log every relic's ID and name.");
            var harmony = new Harmony(PluginGuid);
            ProfileSave.Install(harmony, Logger);
            try
            {
                DiveDirector.Install(harmony);
            }
            catch (Exception e)
            {
                Logger.LogError($"Dive director failed to install: {e.Message}");
            }
            try
            {
                Pickups.Install(harmony);
            }
            catch (Exception e)
            {
                Logger.LogError($"Pickup press hook failed to install: {e.Message}");
            }
            try
            {
                ShopOdds.Install(Config, harmony);
            }
            catch (Exception e)
            {
                Logger.LogError($"Shop odds failed to install: {e.Message}");
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
                Elites.ClearScene();
                Uniques.ClearScene();
                Pickups.Clear();
                Adornments.Clear();
                deathHooked.Clear();
            };
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded ({(InProfile ? "profile" : "inside the base game; see WoLChaosDepths/Profile/README.md to run it as its own profile")}); saves in {SaveStore.Folder}");
        }

        private void OnApplicationQuit() => SaveAll();

        private void Update()
        {
            Hitches.FrameStart();
            Hitches.Begin();
            UpdateFrame();
            Hitches.EndUpdate();
        }

        // Twice a second: make sure each active player has a character and their stats on.
        private void UpdateFrame()
        {
            if (!modEnabled.Value)
                return;
            DebugKeys.Update(this);
            DebugKeys.ShieldTick();
            Pickups.Update();
            if (panel.Open)
            {
                panel.Update();
                return;
            }
            TitleSaveSwitch();
            PortalProbe.Update(InTrials());
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
            if (InTrials())
            {
                Elites.Tick();
                Depth.Tick();
                Layers.Tick();
            }
            foreach (Player player in players)
            {
                if (player == null)
                    continue;
                StatMods.Apply(player, CharacterOf(player));
                Adornments.Tick(player, CharacterOf(player));
            }
        }

        private void OnGUI()
        {
            Hitches.Begin();
            DrawGui();
            Hitches.EndGui();
        }

        private void DrawGui()
        {
            if (!modEnabled.Value)
                return;
            if (panel != null && panel.Open)
                panel.Draw();
            else if (InTrials())
            {
                Layers.Draw();
                Pickups.Draw();
            }
            else if (InProfile)
                ProfileTag();
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
                // Each press opens only its own wizard's inventory (#30): the keyboard key belongs
                // to the wizard on the keyboard, a pad's button to the wizard whose pad it is. Up to
                // 0.13.5 both went to the first wizard free to open it, so player 2's press opened
                // player 1's inventory unless player 1 was busy (e.g. up on Thunderhead's cloud).
                // A key press mid-dash or mid-attack waits up to 0.75 s for its wizard to be free.
                if (OpenKeyPressed())
                {
                    keyOpenUntil = Time.unscaledTime + 0.75f;
                    keyOwner = KeyboardPlayer(players);
                }
                if (keyOpenUntil > 0f)
                {
                    if (keyOwner != null && CanOpen(keyOwner))
                    {
                        keyOpenUntil = 0f;
                        return keyOwner;
                    }
                    if (Time.unscaledTime > keyOpenUntil)
                    {
                        keyOpenUntil = 0f;
                        Log($"Inventory: not opened, the wizard was busy ({(keyOwner != null && keyOwner.fsm != null ? keyOwner.fsm.currentStateName : "?")})");
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
                        if (!ButtonJustPressed(joystick))
                            continue;
                        Player owner = PadOwner(players, joystick);
                        return owner != null && CanOpen(owner) ? owner : null;
                    }
                }
            }
            catch (Exception e)
            {
                LogError($"open check: {e.Message}");
            }
            return null;
        }

        private Player keyOwner;

        // Which inputs drive an open inventory (#30). With one wizard, all of them (the keyboard and
        // any pad), as before. With two or more, the keyboard drives only the keyboard wizard's
        // panel and a pad wizard's panel reads only that wizard's own controller: up to 0.14.8 the
        // panel read the whole keyboard whoever owned it, so player 1 walking or pressing Space,
        // Delete or Escape moved, equipped, salvaged in or closed player 2's inventory.
        internal static void PanelInputs(Player owner, out bool keyboard, out bool pad)
        {
            keyboard = true;
            pad = true;
            try
            {
                Player[] players = GameController.activePlayers;
                if (players == null || owner == null)
                    return;
                int count = 0;
                foreach (Player p in players)
                    if (p != null && p.gameObject != null && p.gameObject.activeInHierarchy)
                        count++;
                if (count < 2)
                    return;
                bool ownerOnKeyboard = KeyboardPlayer(players) == owner;
                keyboard = ownerOnKeyboard;
                pad = !ownerOnKeyboard;
            }
            catch (Exception e)
            {
                LogError($"panel inputs: {e.Message}");
            }
        }

        // The wizard playing on the keyboard: the one whose input is set to it, else the one
        // whose controls include the keyboard, else the first.
        private static Player KeyboardPlayer(Player[] players)
        {
            foreach (Player p in players)
                if (p != null && p.inputDevice != null && p.inputDevice.inputScheme == ChaosInputDevice.InputScheme.Keyboard)
                    return p;
            foreach (Player p in players)
                if (p != null && p.inputDevice != null && p.inputDevice.rewiredPlayer != null && p.inputDevice.rewiredPlayer.controllers.hasKeyboard)
                    return p;
            return players.Length > 0 ? players[0] : null;
        }

        // The wizard a pad belongs to: the one playing on it (their primary controller), else the
        // only one whose controls list it, else the one who used it last.
        private static Player PadOwner(Player[] players, Joystick joystick)
        {
            Player listed = null;
            int listedCount = 0;
            foreach (Player p in players)
            {
                if (p == null || p.inputDevice == null)
                    continue;
                Controller primary = p.inputDevice.primaryController;
                if (primary != null && primary.type == ControllerType.Joystick && primary.id == joystick.id)
                    return p;
                Rewired.Player rp = p.inputDevice.rewiredPlayer;
                if (rp != null && rp.controllers.ContainsController(joystick))
                {
                    listed = listed ?? p;
                    listedCount++;
                }
            }
            if (listedCount <= 1)
                return listed;
            foreach (Player p in players)
            {
                Rewired.Player rp = p != null && p.inputDevice != null ? p.inputDevice.rewiredPlayer : null;
                Controller last = rp != null ? rp.controllers.GetLastActiveController() : null;
                if (last != null && last.type == ControllerType.Joystick && last.id == joystick.id)
                    return p;
            }
            return listed;
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
            return PadButtonJustPressed(joystick, index, names);
        }

        // A face or stick button just pressed on this pad: by its XInput index on a generic pad
        // (buttons named "Button N"), by name on a recognised one.
        public static bool PadButtonJustPressed(Joystick joystick, int index, string[] names)
        {
            if (joystick == null || joystick.Buttons == null)
                return false;
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

        public static ChaosDepthsPlugin Instance { get; private set; }

        // Launched through the Chaos Depths profile's launcher (Profile/README.md), which sets
        // CHAOSDEPTHS_PROFILE=1; inside the base game's BepInEx otherwise.
        public static bool InProfile { get; private set; }

        public static bool InTrialsNow() => InTrials();

        public CharacterSave SaveOf(Player player) => CharacterOf(player);

        // The first active player's character (the one whose chosen depth starts a dive).
        public CharacterSave FirstSave()
        {
            Player[] players = GameController.activePlayers;
            if (players != null)
                foreach (Player p in players)
                    if (p != null)
                        return CharacterOf(p);
            return null;
        }

        public IEnumerable<Player> ActivePlayers()
        {
            var list = new List<Player>();
            Player[] players = GameController.activePlayers;
            if (players != null)
                foreach (Player p in players)
                    if (p != null)
                        list.Add(p);
            return list;
        }

        // A rift cleared in time: a bonus item for this player.
        public void RiftReward(Player player, int minRarity)
        {
            try
            {
                CharacterSave save = CharacterOf(player);
                Drop(player, save, minRarity);
            }
            catch (Exception e)
            {
                LogError($"rift reward: {e.Message}");
            }
        }

        public IEnumerable<CharacterSave> AllSaves()
        {
            var list = new List<CharacterSave>();
            Player[] players = GameController.activePlayers;
            if (players != null)
                foreach (Player p in players)
                    if (p != null)
                        list.Add(CharacterOf(p));
            return list;
        }

        public void SaveCharacter(Player player, CharacterSave save) => SaveStore.Save(player.playerID, save);

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
                if (DebugEnabled && hurt is Player && attack != null && DebugKeys.Shielding)
                    Log($"Debug: a kill sweep hit player {((Player)hurt).playerID + 1} for {attack.damage} " +
                        $"(attacker {(attacker != null ? attacker.GetType().Name : "none")}, skill '{attack.skillID}', category '{attack.skillCategory}')");
                if (modEnabled.Value && attack != null && hurt is Player && InTrials())
                    Uniques.OnPlayerHurt((Player)hurt, CharacterOf((Player)hurt), attack);
                if (!modEnabled.Value || attack == null || !(hurt is Enemy))
                    return;
                Player player = FindAttacker(attack, attacker);
                if (player == null)
                    return;
                lastHitBy[hurt.GetInstanceID()] = player;
                Health health = hurt.health;
                if (health != null && deathHooked.Add(health.GetInstanceID()))
                    health.deathEventHandlers += OnEnemyDeath;
                if (InTrials())
                    Uniques.OnPlayerHit(player, CharacterOf(player), attack, hurt);
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
                // Realm bosses, floor bosses (the skull altar's), elites, then everyone else.
                bool boss = entity is Boss || (IsBoss(entity) && !(entity is MiniBoss));
                bool floorBoss = entity is MiniBoss;
                string elite = Elites.AffixOf(entity);
                if (boss)
                    DiveDirector.BossKilled(entity);
                float maxHealth = entity.health != null && entity.health.healthStat != null ? entity.health.healthStat.ModifiedValue : 10f;
                float mult = (boss ? bossXpMultiplier.Value : floorBoss ? floorBossXpMultiplier.Value : elite != null ? Elites.XpMultiplier : 1f) * Layers.XpMultiplier(entity);
                int xp = Mathf.Max(1, Mathf.RoundToInt(maxHealth * xpPerHealth.Value * mult * Depth.XpMultiplier * AscensionLink.Experience * Depth.MissingHealthFactor(entity)));
                save.kills++;
                string kind = Layers.IsLayerBoss(entity) ? " (layer boss)" : boss ? " (boss)" : floorBoss ? " (floor boss)" : elite != null ? $" ({elite} elite)" : "";
                if (logKills.Value || kind.Length > 0)
                    Log($"{entity.GetType().Name}{kind} slain by player {player.playerID + 1}: +{xp} xp");
                AddXp(player, save, xp);
                Uniques.OnPlayerKill(player, save, entity);
                if (boss)
                {
                    // Every boss drops an item, rolled from BossDropMinRarity up: a Legendary is
                    // possible, not promised (Matt, 0.15.3; layer bosses and each character's
                    // first realm boss used to promise one).
                    int min = bossDropRarity.Value;
                    // Ascension 10: the realm boss drops a Legendary (#27).
                    if (AscensionLink.RealmBossDropsLegendary)
                        min = LootTable.RarityIndex("Legendary");
                    // The first time a character beats the final boss at each Ascension level, it
                    // drops a Legendary.
                    else if (DiveDirector.IsFinalBoss(DiveDirector.CurrentScene()) && !OtherBossAlive(entity)
                        && !save.finalLegendaryAt.Contains(AscensionLink.Level))
                    {
                        save.finalLegendaryAt.Add(AscensionLink.Level);
                        min = LootTable.RarityIndex("Legendary");
                        Log($"Player {player.playerID + 1}'s first final boss win at Ascension {AscensionLink.Level}: a guaranteed Legendary");
                    }
                    // Testing (Matt, 0.15.8): a character's first boss win drops a unique, so
                    // the powers can be tried early. [Testing] FirstBossUnique turns it off.
                    bool unique = false;
                    if (firstBossUnique.Value && !save.firstBossUnique)
                    {
                        save.firstBossUnique = unique = true;
                        min = LootTable.RarityIndex("Legendary");
                        Log($"Player {player.playerID + 1}'s first boss win: a guaranteed unique Legendary (testing)");
                    }
                    Drop(player, save, min, entity.transform.position, unique);
                }
                else if (floorBoss)
                    Drop(player, save, floorBossDropRarity.Value, entity.transform.position);
                else if (elite != null)
                {
                    if (Elites.DropsItem())
                        Drop(player, save, Elites.DropMinRarity, entity.transform.position);
                }
                else if (UnityEngine.Random.value < KillDropChance())
                    Drop(player, save, 0, entity.transform.position);
                if (boss)
                    SaveStore.Save(player.playerID, save);
            }
            catch (Exception e)
            {
                LogError($"death hook: {e.Message}");
            }
        }

        // Whether a boss other than this one still stands (the final boss is the last to fall).
        private static bool OtherBossAlive(Entity entity)
        {
            try
            {
                if (GameController.bosses != null)
                    foreach (GameObject go in GameController.bosses)
                    {
                        Entity e = go != null && go != entity.gameObject && go.activeInHierarchy ? go.GetComponent<Entity>() : null;
                        if (e != null && e.health != null && e.health.CurrentHealthValue > 0)
                            return true;
                    }
            }
            catch
            {
            }
            return false;
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

        // A small "CHAOS DEPTHS" tag in the top-left corner outside the trials, so you can tell
        // the profile from the base game at a glance.
        // The tag in the corner: the profile and which save it is playing; on the title screen,
        // how to switch saves (#31).
        private static void ProfileTag()
        {
            try
            {
                int u = Mathf.Max(2, Mathf.FloorToInt(Screen.height / 240f));
                GUIStyle style = PixelUI.Style(Mathf.Max(10, 3 * u), PixelUI.Accent, TextAnchor.UpperLeft);
                bool saves = ProfileSave.Active;
                var rect = new Rect(3 * u, 2 * u, 90 * u, 5 * u);
                PixelUI.Fill(new Rect(rect.x - u, rect.y, (saves ? 70 : 36) * u, (onTitle && saves ? 10 : 5) * u), new Color(0f, 0f, 0f, 0.4f));
                GUI.Label(rect, saves ? "CHAOS DEPTHS  " + ProfileSave.SaveName : "CHAOS DEPTHS", style);
                if (onTitle && saves)
                    GUI.Label(new Rect(rect.x, rect.y + 5 * u, rect.width, rect.height), "TAB / LB: NEXT SAVE",
                        PixelUI.Style(Mathf.Max(10, 3 * u), PixelUI.Dim, TextAnchor.UpperLeft));
            }
            catch
            {
            }
        }

        // ---- Picking the save on the title screen (#31) ----

        private static bool onTitle;
        private float nextTitleCheck;

        private void TitleSaveSwitch()
        {
            if (!ProfileSave.Active)
            {
                onTitle = false;
                return;
            }
            if (Time.unscaledTime >= nextTitleCheck)
            {
                nextTitleCheck = Time.unscaledTime + 1f;
                onTitle = OnTitleMenu();
            }
            if (!onTitle || !(Input.GetKeyDown(KeyCode.Tab) || PadShoulderPressed()))
                return;
            if (!OnTitleMenu())
            {
                onTitle = false;
                return;
            }
            // The characters of the save being left are written out and forgotten, so the other
            // save's are read fresh when a run starts.
            SaveAll();
            characters.Clear();
            if (ProfileSave.Switch())
                LootTable.Load(); // each save has its own loot.json beside its characters
        }

        // On the title screen's menu, with no wizard about (not in its Options screen).
        private static bool OnTitleMenu()
        {
            try
            {
                Player[] players = GameController.activePlayers;
                if (players != null)
                    foreach (Player p in players)
                        if (p != null)
                            return false;
                TitleScreen title = UnityEngine.Object.FindObjectOfType<TitleScreen>();
                return title != null && title.gameObject.activeInHierarchy && title.currentState != TitleScreen.TitleScreenState.Options;
            }
            catch
            {
                return false;
            }
        }

        // LB on any pad (XInput button 4).
        private static bool PadShoulderPressed()
        {
            try
            {
                if (!ReInput.isReady)
                    return false;
                foreach (Joystick joystick in ReInput.controllers.Joysticks)
                    if (joystick != null && joystick.buttonCount > 4 && joystick.GetButtonDown(4))
                        return true;
            }
            catch
            {
            }
            return false;
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

        // Experience for the next level: 60 x level^1.4, times XpToLevelScale (2 since 0.15.2, so
        // levels come about half as often as before; Matt).
        public static int XpToNext(int level) => Mathf.RoundToInt(60f * (xpToLevelScale != null ? xpToLevelScale.Value : 1f) * Mathf.Pow(Mathf.Max(1, level), 1.4f));

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

        // Rolls an item for this player. Dropped on the floor at 'at' as a pickup when physical
        // drops are on and a position is known; otherwise straight into the inventory.
        private void Drop(Player player, CharacterSave save, int minRarity, Vector3? at = null, bool unique = false)
        {
            // Item level: the character's level, plus 2 a level of Ascension (#27, in place of
            // depth's 1 a depth).
            ItemData item = LootTable.Roll(save.level + AscensionLink.ItemLevelBonus + Depth.ItemLevelBonus, minRarity, Elites.RarityBoost() + Depth.RarityBoost + Layers.RarityBonus);
            // A promised unique: a Legendary that didn't roll one becomes a random unique.
            if (unique && string.IsNullOrEmpty(item.unique) && Uniques.Defs.Count > 0)
                Uniques.MakeUnique(item, Uniques.Defs[UnityEngine.Random.Range(0, Uniques.Defs.Count)]);
            Log($"Player {player.playerID + 1}'s drop: {item.rarity} {item.name} ({LootTable.Describe(item)})" + (at.HasValue && Pickups.Physical ? ", on the floor" : ""));
            if (at.HasValue && Pickups.Physical)
                Pickups.Spawn(player, item, at.Value);
            else
                Collect(player, item, false);
        }

        // An item reaches a player's inventory: picked up, or collected when leaving the floor.
        private void Collect(Player player, ItemData item, bool leftover)
        {
            if (player == null || item == null)
                return;
            CharacterSave save = CharacterOf(player);
            save.inventory.Add(item);
            save.itemsFound++;
            Log($"Player {player.playerID + 1} {(leftover ? "collected" : "found")}: {item.rarity} {item.name} ({LootTable.Describe(item)})");
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
