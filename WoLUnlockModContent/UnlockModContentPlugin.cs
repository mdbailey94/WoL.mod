using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLUnlockModContent
{
    // While on, marks every arcana and relic that mods add through LegendAPI as unlocked, so they
    // show up in the spellbook/loadout without having to find or buy them first. It remembers
    // exactly which ones it unlocked, and turning it off locks only those again: in the save data
    // and on the wizard's own arcana (the game copies those back into the save, so locking only
    // the save wasn't enough). If no wizard is loaded yet (e.g. switched off on the title screen),
    // the arcana are locked as soon as one is.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class UnlockModContentPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.unlockmodcontent";
        public const string PluginName = "Unlock Mod Content";
        public const string PluginVersion = "0.2.0";

        private const float CheckInterval = 2f;

        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<string> unlockedByUs;
        private ConfigEntry<string> toRelock;
        private ConfigEntry<bool> repaired;
        private float nextCheck;

        private void Awake()
        {
            modEnabled = Config.Bind("General", "Enabled", true,
                "Unlock all mod-added arcana and relics (also in the title screen Mods menu). " +
                "Turning it off locks again only what this mod unlocked.");
            unlockedByUs = Config.Bind("Internal", "UnlockedByThisMod", string.Empty,
                "IDs this mod unlocked (so turning it off can undo exactly those). Don't edit.");
            toRelock = Config.Bind("Internal", "WaitingToRelock", string.Empty,
                "Arcana to lock on the wizard once one is loaded. Don't edit.");
            repaired = Config.Bind("Internal", "RepairedOldRelock", false,
                "Whether the one-time fix for 0.1.0's switching off (which didn't stick) has run. Don't edit.");
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + CheckInterval;

            try
            {
                Sync();
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }

        private void Sync()
        {
            GameData data = GameDataManager.gameData;
            if (data == null || data.skillDataDictionary == null || data.itemDataDictionary == null)
                return;

            var ours = new HashSet<string>(unlockedByUs.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            bool changed = false;

            // 0.1.0 forgot what it had unlocked when switched off, and the game then unlocked
            // those again from the wizard. Once, if it's off now: lock every mod-added arcana.
            if (!repaired.Value)
            {
                repaired.Value = true;
                if (!modEnabled.Value)
                {
                    var all = new HashSet<string>(Split(toRelock.Value));
                    foreach (string id in CatalogKeys("LegendAPI.Skills", "SkillCatalog"))
                    {
                        all.Add(id);
                        if (data.skillDataDictionary.ContainsKey(id))
                            data.skillDataDictionary[id].isUnlocked = false;
                    }
                    toRelock.Value = string.Join(",", all.ToArray());
                    Logger.LogInfo($"One-time fix: locking the mod arcana again ({toRelock.Value})");
                }
            }

            if (modEnabled.Value)
            {
                // Switched back on before a wizard loaded: nothing left to lock.
                if (toRelock.Value.Length > 0)
                    toRelock.Value = string.Empty;
                foreach (string id in CatalogKeys("LegendAPI.Skills", "SkillCatalog"))
                    changed |= UnlockSkill(data, id, ours);
                foreach (string id in CatalogKeys("LegendAPI.Items", "ItemCatalog"))
                    changed |= UnlockItem(data, id, ours);
            }
            else if (ours.Count > 0)
            {
                var relock = new HashSet<string>(Split(toRelock.Value));
                foreach (string entry in ours)
                {
                    string id = entry.Substring(2);
                    if (entry.StartsWith("s:") && data.skillDataDictionary.ContainsKey(id))
                    {
                        data.skillDataDictionary[id].isUnlocked = false;
                        relock.Add(id);
                    }
                    else if (entry.StartsWith("i:") && data.itemDataDictionary.ContainsKey(id))
                        data.itemDataDictionary[id].isUnlocked = false;
                    Logger.LogInfo($"Locked again: {id}");
                }
                ours.Clear();
                toRelock.Value = string.Join(",", relock.ToArray());
                changed = true;
            }

            // Lock them on the wizard too, once one is loaded, and save so it sticks.
            if (!modEnabled.Value && toRelock.Value.Length > 0 && LockOnPlayers(data))
            {
                toRelock.Value = string.Empty;
                SaveGame();
            }

            if (!changed)
                return;
            unlockedByUs.Value = string.Join(",", ours.ToArray());
            try
            {
                // Copy the stored unlocks onto the players' skills so the spellbook sees them now.
                data.PullSkillData();
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Couldn't refresh players' skills yet (will apply on next load): {e.Message}");
            }
        }

        private static IEnumerable<string> Split(string list) =>
            list.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        // True once there was a wizard to lock them on.
        private bool LockOnPlayers(GameData data)
        {
            Player[] players = GameController.activePlayers;
            if (players == null || players.Length == 0)
                return false;
            bool any = false;
            foreach (Player player in players)
            {
                if (player == null || player.skillsDict == null)
                    continue;
                any = true;
                foreach (string id in Split(toRelock.Value))
                {
                    Player.SkillState state;
                    if (player.skillsDict.TryGetValue(id, out state) && state != null && state.isUnlocked)
                    {
                        state.isUnlocked = false;
                        Logger.LogInfo($"Locked on the wizard: {id}");
                    }
                    GameData.StoredSkillData stored;
                    if (data.skillDataDictionary.TryGetValue(id, out stored) && stored != null)
                        stored.isUnlocked = false;
                }
            }
            return any;
        }

        private void SaveGame()
        {
            try
            {
                GameDataManager.Save(true, false);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Couldn't save after locking: {e.Message}");
            }
        }

        private bool UnlockSkill(GameData data, string id, HashSet<string> ours)
        {
            GameData.StoredSkillData stored;
            if (!data.skillDataDictionary.TryGetValue(id, out stored) || stored == null)
            {
                stored = new GameData.StoredSkillData();
                data.skillDataDictionary[id] = stored;
            }
            if (stored.isUnlocked)
                return false;
            stored.isUnlocked = true;
            ours.Add("s:" + id);
            Logger.LogInfo($"Unlocked arcana: {id}");
            return true;
        }

        private bool UnlockItem(GameData data, string id, HashSet<string> ours)
        {
            GameData.StoredItemData stored;
            if (!data.itemDataDictionary.TryGetValue(id, out stored) || stored == null)
            {
                stored = new GameData.StoredItemData();
                data.itemDataDictionary[id] = stored;
            }
            if (stored.isUnlocked)
                return false;
            stored.isUnlocked = true;
            ours.Add("i:" + id);
            Logger.LogInfo($"Unlocked relic: {id}");
            return true;
        }

        // LegendAPI keeps its catalogs internal, so read their keys by reflection.
        private IEnumerable<string> CatalogKeys(string typeName, string fieldName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(typeName, false))
                .FirstOrDefault(t => t != null);
            FieldInfo field = type?.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var catalog = field?.GetValue(null) as IDictionary;
            if (catalog == null)
                return Enumerable.Empty<string>();
            return catalog.Keys.OfType<string>().ToList();
        }
    }
}
