using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WoLChaosDepths
{
    // One Chaos Depths character: its own file, nothing to do with the game's save. Written as
    // JSON through Unity's JsonUtility (public fields only, so every field below is one), with a
    // version number for later upgrades and a .bak copy kept from the previous save.
    [Serializable]
    public class CharacterSave
    {
        public int version = 1;
        public string name = "Wizard";
        public int level = 1;
        public int xp;
        public int totalXp;
        public int kills;
        public int itemsFound;
        public List<ItemData> inventory = new List<ItemData>();
        // Item id equipped in each slot, "" for none; same order as LootTable.Slots.
        public List<string> equipped = new List<string>();
    }

    [Serializable]
    public class ItemData
    {
        public string id;
        public string name;
        public string slot;
        public string rarity;
        public int itemLevel;
        public List<AffixData> affixes = new List<AffixData>();

        public ItemData Find(string key)
        {
            foreach (AffixData affix in affixes)
                if (affix.key == key)
                    return this;
            return null;
        }
    }

    [Serializable]
    public class AffixData
    {
        public string key;   // see LootTable: "Damage", "CritChance", ...
        public float value;  // the rolled amount, in the affix's own unit (0.12 = +12%)
    }

    public static class SaveStore
    {
        public static string Folder => Path.Combine(BepInEx.Paths.ConfigPath, "ChaosDepths");

        public static string PathFor(int playerIndex) => Path.Combine(Folder, $"character{playerIndex + 1}.json");

        public static CharacterSave Load(int playerIndex)
        {
            string path = PathFor(playerIndex);
            try
            {
                if (File.Exists(path))
                {
                    CharacterSave save = JsonUtility.FromJson<CharacterSave>(File.ReadAllText(path));
                    if (save != null)
                    {
                        Upgrade(save);
                        ChaosDepthsPlugin.Log($"Loaded {path}: {save.name}, level {save.level}, {save.inventory.Count} item(s)");
                        return save;
                    }
                }
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Couldn't read {path} ({e.Message}); trying the backup");
                try
                {
                    if (File.Exists(path + ".bak"))
                    {
                        CharacterSave save = JsonUtility.FromJson<CharacterSave>(File.ReadAllText(path + ".bak"));
                        if (save != null)
                        {
                            Upgrade(save);
                            return save;
                        }
                    }
                }
                catch (Exception e2)
                {
                    ChaosDepthsPlugin.LogError($"Backup unreadable too: {e2.Message}");
                }
            }
            var fresh = new CharacterSave { name = playerIndex == 0 ? "Wizard" : $"Wizard {playerIndex + 1}" };
            Upgrade(fresh);
            ChaosDepthsPlugin.Log($"New character for player {playerIndex + 1} (will be saved to {path})");
            return fresh;
        }

        public static void Save(int playerIndex, CharacterSave save)
        {
            string path = PathFor(playerIndex);
            try
            {
                Directory.CreateDirectory(Folder);
                if (File.Exists(path))
                    File.Copy(path, path + ".bak", true);
                File.WriteAllText(path, JsonUtility.ToJson(save, true));
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Couldn't save {path}: {e.Message}");
            }
        }

        // Older files get any new fields filled in here.
        private static void Upgrade(CharacterSave save)
        {
            if (save.inventory == null)
                save.inventory = new List<ItemData>();
            if (save.equipped == null)
                save.equipped = new List<string>();
            while (save.equipped.Count < LootTable.Slots.Length)
                save.equipped.Add("");
            save.version = 1;
        }
    }
}
