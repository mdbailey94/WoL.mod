using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WoLChaosDepths
{
    // One Chaos Depths character: its own file, nothing to do with the game's save. Written as
    // JSON by Json.cs (public fields only, so every field below is one; Unity's JsonUtility can't
    // see plugin classes), with a version number for later upgrades and a .bak copy kept from the
    // previous save.
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
        public int dust;        // Chaos Dust from salvaged items
        public int depthReached = 1;   // the deepest depth this character has reached (its waypoint)
        public int depthChosen = 1;    // where its next dive starts (1 up to depthReached)
        public bool firstBossLegendary; // up to 0.15.2: the first realm boss's guaranteed Legendary was given (#31); unused now
        public bool firstBossUnique;    // the testing unique from this character's first boss win is given (0.15.8)
        public List<int> finalLegendaryAt = new List<int>(); // Ascension levels whose first final boss win's Legendary is given (0.15.4)
        public List<ItemData> inventory = new List<ItemData>();
        // Item id equipped in each slot, "" for none; same order as LootTable.Slots.
        public List<string> equipped = new List<string>();
        // Phase 1a: talent points spent, per cloak (Talents.cs).
        public List<TalentData> talents = new List<TalentData>();
        // Phase 1c: named sets of cloak + arcana + gear (LoadoutSets.cs); talents follow the cloak.
        public List<LoadoutData> loadouts = new List<LoadoutData>();
        // Phase 1b: each arcana's xp (casts in runs; ArcanaLevels.cs).
        public List<ArcanaData> arcana = new List<ArcanaData>();
    }

    [Serializable]
    public class ArcanaData
    {
        public string arcana;  // the skill ID
        public int xp;
    }

    [Serializable]
    public class LoadoutData
    {
        public string name;
        public string cloak;                                    // the game's outfit ID
        public List<string> skills = new List<string>();        // arcana per slot (Player.assignedSkills order)
        public int signatureSlot = -1;
        public List<string> equipped = new List<string>();      // item ids, as CharacterSave.equipped
    }

    [Serializable]
    public class TalentData
    {
        public string cloak;   // the game's outfit ID
        public string node;    // Talents.Node.Key
        public int points;
    }

    [Serializable]
    public class ItemData
    {
        public string id;
        public string name;
        public string slot;
        public string rarity;
        public int itemLevel;
        public string unique = "";
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
        // Each save slot's characters live in a folder of their own (Fresh for Save 1, Save2,
        // Save3...); the Debugging save's (and outside the profile, the only save's) where they
        // always were (#31, slots 0.15.0).
        public static string Folder => ProfileSave.CharacterFolder != null
            ? Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "ChaosDepths"), ProfileSave.CharacterFolder)
            : Path.Combine(BepInEx.Paths.ConfigPath, "ChaosDepths");

        public static string PathFor(int playerIndex) => Path.Combine(Folder, $"character{playerIndex + 1}.json");

        public static CharacterSave Load(int playerIndex)
        {
            string path = PathFor(playerIndex);
            try
            {
                if (File.Exists(path))
                {
                    CharacterSave save = Json.Read<CharacterSave>(File.ReadAllText(path));
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
                        CharacterSave save = Json.Read<CharacterSave>(File.ReadAllText(path + ".bak"));
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
                File.WriteAllText(path, Json.Write(save));
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
            if (save.loadouts == null)
                save.loadouts = new List<LoadoutData>();
            if (save.equipped == null)
                save.equipped = new List<string>();
            if (save.talents == null)
                save.talents = new List<TalentData>();
            if (save.arcana == null)
                save.arcana = new List<ArcanaData>();
            while (save.equipped.Count < LootTable.Slots.Length)
                save.equipped.Add("");
            save.depthReached = Math.Max(1, save.depthReached);
            save.depthChosen = Math.Min(Math.Max(1, save.depthChosen), save.depthReached);
            if (save.version < 2)
                SimplifyItems(save);
            save.version = 2;
        }

        // 0.40.0 (Matt 14:17, simpler item stats): items made before keep their power in the 9 stats
        // left. An arcana-type damage line becomes general damage at 60% (it only ever helped some
        // arcana), a type cooldown line general cooldown at 60%, evade 1.5x as damage-taken points;
        // two lines of one stat merge; every value goes to whole points. Names follow the new lines.
        private static readonly string[] TypeDamage = { "BasicDamage", "ProjectileDamage", "MeleeDamage", "MovementDamage", "SummonDamage", "HoldDamage" };
        private static readonly string[] TypeCooldown = { "DashCooldown", "ProjectileCooldown", "MeleeCooldown", "MovementCooldown", "HoldCooldown" };

        private static void SimplifyItems(CharacterSave save)
        {
            int changed = 0;
            foreach (ItemData item in save.inventory)
            {
                if (item == null || item.affixes == null)
                    continue;
                var merged = new List<AffixData>();
                bool any = false;
                foreach (AffixData a in item.affixes)
                {
                    if (a == null)
                        continue;
                    string key = a.key;
                    float value = a.value;
                    if (Array.IndexOf(TypeDamage, key) >= 0) { key = "Damage"; value *= 0.6f; }
                    else if (Array.IndexOf(TypeCooldown, key) >= 0) { key = "Cooldown"; value *= 0.6f; }
                    else if (key == "Evade") { key = "DamageTaken"; value = -1.5f * value; }
                    AffixData same = merged.Find(m => m.key == key);
                    if (same != null)
                        same.value += value;
                    else
                        merged.Add(new AffixData { key = key, value = value });
                    any |= key != a.key;
                }
                foreach (AffixData m in merged)
                {
                    float whole = LootTable.Whole(m.value);
                    any |= !Mathf.Approximately(whole, m.value);
                    m.value = whole;
                }
                if (!any)
                    continue;
                item.affixes = merged;
                if (LootTable.Current != null)
                    LootTable.Rename(item);
                changed++;
            }
            if (changed > 0)
                ChaosDepthsPlugin.Log($"Simplified item stats on {changed} item(s) (0.40.0: 9 stats, whole points)");
        }
    }
}
