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
    // exactly which ones it unlocked, and turning it off locks only those again.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class UnlockModContentPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.unlockmodcontent";
        public const string PluginName = "Unlock Mod Content";
        public const string PluginVersion = "0.1.0";

        private const float CheckInterval = 2f;

        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<string> unlockedByUs;
        private float nextCheck;

        private void Awake()
        {
            modEnabled = Config.Bind("General", "Enabled", true,
                "Unlock all mod-added arcana and relics (also in the title screen Mods menu). " +
                "Turning it off locks again only what this mod unlocked.");
            unlockedByUs = Config.Bind("Internal", "UnlockedByThisMod", string.Empty,
                "IDs this mod unlocked (so turning it off can undo exactly those). Don't edit.");
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

            if (modEnabled.Value)
            {
                foreach (string id in CatalogKeys("LegendAPI.Skills", "SkillCatalog"))
                    changed |= UnlockSkill(data, id, ours);
                foreach (string id in CatalogKeys("LegendAPI.Items", "ItemCatalog"))
                    changed |= UnlockItem(data, id, ours);
            }
            else if (ours.Count > 0)
            {
                foreach (string entry in ours)
                {
                    string id = entry.Substring(2);
                    if (entry.StartsWith("s:") && data.skillDataDictionary.ContainsKey(id))
                        data.skillDataDictionary[id].isUnlocked = false;
                    else if (entry.StartsWith("i:") && data.itemDataDictionary.ContainsKey(id))
                        data.itemDataDictionary[id].isUnlocked = false;
                    Logger.LogInfo($"Locked again: {id}");
                }
                ours.Clear();
                changed = true;
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
