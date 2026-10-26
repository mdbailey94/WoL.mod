using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
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
        public const string PluginVersion = "0.1.0";

        private const float KillDedupeSeconds = 2f;

        private static BepInEx.Logging.ManualLogSource log;
        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<float> healthPerLevel, xpPerHealth, bossXpMultiplier, dropChance;
        private static ConfigEntry<int> levelCap, bossDropRarity;
        private static ConfigEntry<bool> autoEquip, logKills;

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
            if (!modEnabled.Value || Time.unscaledTime < nextTick)
                return;
            nextTick = Time.unscaledTime + 0.5f;
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            foreach (Player player in players)
            {
                if (player == null)
                    continue;
                StatMods.Apply(player, CharacterOf(player));
            }
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
                int id = entity.GetInstanceID();
                float last;
                if (lastKillTime.TryGetValue(id, out last) && Time.time - last < KillDedupeSeconds)
                    return;
                lastKillTime[id] = Time.time;
                Player player;
                if (!lastHitBy.TryGetValue(id, out player) || player == null)
                    return;
                CharacterSave save = CharacterOf(player);
                bool boss = IsBoss(entity);
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

        private static void Notice(string message, float seconds)
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
