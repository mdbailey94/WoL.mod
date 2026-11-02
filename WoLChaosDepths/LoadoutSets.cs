using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace WoLChaosDepths
{
    /// <summary>
    /// Loadouts (Roadmap Phase 1c): up to [Loadouts] Sets named sets per character, each holding a
    /// cloak, the six arcana slots (with which one is the signature) and the equipped gear; the
    /// cloak's talents come with it, since talents are kept per cloak. Saved and swapped in the
    /// house only (PlayerRoom), on the keyboard for now: Ctrl+1..3 wears a set, Ctrl+Shift+1..3
    /// stores what you're wearing in it. A panel tab can call Store / Wear later.
    /// Wearing uses the game's own calls: Player.EquipOutfit for the cloak (it refuses a cloak not
    /// unlocked or worn by the other player) and Player.AssignSkillSlot for the arcana (an arcana
    /// no longer unlocked, or a signature without its signature unlocked, keeps that slot as it
    /// is), then the gear (items no longer in the inventory leave the slot empty), the stats and
    /// both saves.
    /// </summary>
    public static class LoadoutSets
    {
        private const string HouseScene = "PlayerRoom";

        private static ConfigEntry<bool> enabled;
        private static ConfigEntry<int> sets;
        private static ManualLogSource log;

        public static bool Enabled => enabled != null && enabled.Value;
        public static int Sets => Mathf.Clamp(sets != null ? sets.Value : 3, 1, 9);

        public static void Install(ConfigFile config, ManualLogSource logger)
        {
            log = logger;
            enabled = config.Bind("Loadouts", "Enabled", false,
                "Phase 1c (in testing): named sets of cloak + arcana + gear, stored and worn in the house. " +
                "Keyboard: Ctrl+1..3 wears a set, Ctrl+Shift+1..3 stores what you're wearing in it.");
            sets = config.Bind("Loadouts", "Sets", 3,
                new ConfigDescription("How many sets each character has.", new AcceptableValueRange<int>(1, 9)));
            var host = new GameObject("ChaosDepths Loadouts");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<Keys>();
        }

        private class Keys : MonoBehaviour
        {
            private void Update()
            {
                if (!Enabled || Application.loadedLevelName != HouseScene)
                    return;
                if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
                    return;
                for (int i = 0; i < Sets; i++)
                {
                    if (!Input.GetKeyDown(KeyCode.Alpha1 + i))
                        continue;
                    bool store = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    try
                    {
                        Player[] players = GameController.activePlayers;
                        Player player = players != null ? ChaosDepthsPlugin.KeyboardPlayer(players) : null;
                        if (player != null)
                        {
                            if (store)
                                Store(player, i);
                            else
                                Wear(player, i);
                        }
                    }
                    catch (Exception e)
                    {
                        log?.LogError($"Loadouts: set {i + 1} failed ({e.Message})");
                    }
                }
            }
        }

        private static CharacterSave SaveOf(Player player) =>
            ChaosDepthsPlugin.Instance != null ? ChaosDepthsPlugin.Instance.CharacterOf(player) : null;

        private static LoadoutData Set(CharacterSave save, int index, bool create)
        {
            while (create && save.loadouts.Count <= index)
                save.loadouts.Add(new LoadoutData { name = "SET " + (save.loadouts.Count + 1) });
            return index < save.loadouts.Count ? save.loadouts[index] : null;
        }

        /// <summary>Stores what the wizard wears now in set index (0-based).</summary>
        public static bool Store(Player player, int index)
        {
            CharacterSave save = SaveOf(player);
            if (save == null || index < 0 || index >= Sets)
                return false;
            LoadoutData set = Set(save, index, true);
            set.cloak = Talents.CloakOf(player);
            set.skills.Clear();
            set.signatureSlot = -1;
            Player.SkillState[] slots = player.assignedSkills;
            Player.SkillState signature = player.GetSignatureSkill();
            for (int i = 0; slots != null && i < slots.Length; i++)
            {
                Player.SkillState s = slots[i];
                set.skills.Add(s != null ? s.skillID : string.Empty);
                if (s != null && s == signature)
                    set.signatureSlot = i;
            }
            set.equipped = new List<string>(save.equipped);
            ChaosDepthsPlugin.Instance.SaveCharacter(player, save);
            string text = $"{set.name} stored: {Talents.CloakName(set.cloak)}, {string.Join(", ", set.skills.ToArray())}";
            ChaosDepthsPlugin.Notice($"Player {player.playerID + 1}: {set.name} stored", 2f);
            log?.LogInfo($"Loadouts: player {player.playerID + 1}'s {text}; gear {string.Join(", ", set.equipped.ToArray())}");
            return true;
        }

        /// <summary>Wears set index (0-based). False if it's empty or the wizard isn't in the house.</summary>
        public static bool Wear(Player player, int index)
        {
            CharacterSave save = SaveOf(player);
            LoadoutData set = save != null ? Set(save, index, false) : null;
            if (set == null || string.IsNullOrEmpty(set.cloak))
            {
                ChaosDepthsPlugin.Notice($"Player {player.playerID + 1}: SET {index + 1} is empty (Ctrl+Shift+{index + 1} stores one)", 2.5f);
                return false;
            }
            if (Application.loadedLevelName != HouseScene)
                return false;
            var notes = new List<string>();

            // The cloak (its talents come with it).
            if (Talents.CloakOf(player) != set.cloak && !player.EquipOutfit(set.cloak))
                notes.Add($"cloak {Talents.CloakName(set.cloak)} not available, kept {Talents.CloakName(Talents.CloakOf(player))}");

            // The arcana, slot by slot; the old signature stops being one first.
            Player.SkillState oldSignature = player.GetSignatureSkill();
            int kept = 0;
            for (int i = 0; i < set.skills.Count && player.assignedSkills != null && i < player.assignedSkills.Length; i++)
            {
                string id = set.skills[i];
                bool sig = i == set.signatureSlot;
                Player.SkillState state;
                if (string.IsNullOrEmpty(id) || !player.skillsDict.TryGetValue(id, out state) || state == null || !state.isUnlocked
                    || (sig && (!state.hasSignatureVariant || !state.signatureUnlocked)))
                {
                    kept++;
                    continue;
                }
                if (oldSignature != null && oldSignature != state && sig)
                {
                    oldSignature.isSignature = false;
                    oldSignature.SetEmpowered(false, EmpowerStatMods.DefaultEmpowerMod);
                    oldSignature = null;
                }
                // An arcana moved out of another slot leaves that one as the set says next.
                for (int j = 0; j < player.assignedSkills.Length; j++)
                    if (j != i && player.assignedSkills[j] == state)
                        player.AssignSkillSlot(j, string.Empty);
                player.AssignSkillSlot(i, id, true, sig);
            }
            if (kept > 0)
                notes.Add($"{kept} arcana slot(s) kept (not unlocked here)");

            // The gear: only items still in the inventory.
            int missing = 0;
            var equipped = new List<string>();
            foreach (string id in set.equipped)
            {
                bool have = !string.IsNullOrEmpty(id) && StatMods.FindItem(save, id) != null;
                if (!string.IsNullOrEmpty(id) && !have)
                    missing++;
                equipped.Add(have ? id : string.Empty);
            }
            while (equipped.Count < save.equipped.Count)
                equipped.Add(string.Empty);
            save.equipped = equipped;
            if (missing > 0)
                notes.Add($"{missing} item(s) no longer owned");

            StatMods.Apply(player, save);
            ChaosDepthsPlugin.Instance.SaveCharacter(player, save);
            try
            {
                // The game's own save: the cloak and the arcana slots (GameData.SavePlayerData).
                GameDataManager.Save(true, true);
            }
            catch (Exception e)
            {
                log?.LogWarning($"Loadouts: couldn't save the game ({e.Message})");
            }
            ChaosDepthsPlugin.Notice($"Player {player.playerID + 1}: {set.name}" + (notes.Count > 0 ? " (some kept, see log)" : ""), 2.5f);
            log?.LogInfo($"Loadouts: player {player.playerID + 1} wears {set.name}: {Talents.CloakName(set.cloak)}, " +
                $"{string.Join(", ", set.skills.ToArray())}" + (notes.Count > 0 ? "; " + string.Join("; ", notes.ToArray()) : ""));
            return true;
        }
    }
}
