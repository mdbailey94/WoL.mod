using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WoLChaosDepths
{
    // Phase 1b (Roadmap): arcana upgrades, simple. Each arcana levels 1-10 from use: every cast
    // in a run (not the house or the Hub) is 1 xp for that arcana, per character. Every level is
    // a flat step on ONE line, picked per arcana: damage (the default) or cooldown. No branching,
    // no choices. The step goes on the game's own stats for that player's arcana (StatData.AddMod,
    // what relics use), so every hit the arcana makes and its cooldown follow it. Level 10's
    // mastery perk is for later. Behind [Arcana Levels] Enabled, off by default.
    public static class ArcanaLevels
    {
        public const int MaxLevel = 10;
        private const string ModID = "ChaosDepths_ArcanaLevel";

        private static ConfigEntry<bool> enabled;
        private static ConfigEntry<float> xpScale, damageStep, cooldownStep;
        private static ConfigEntry<string> cooldownArcana;
        private static readonly HashSet<string> failed = new HashSet<string>();
        // The step each arcana's stats object has now (level, negative for a cooldown step), so a
        // cast only touches the stats when they're new (a level load) or the level changed.
        private static readonly Dictionary<StatData, int> applied = new Dictionary<StatData, int>();

        // Pulled out for now (Matt, 2026-11-02 evening: "Pull out arcana upgrade system for now, will
        // rework later"): off whatever the config says, so no xp, no steps and no spellbook strip.
        // Saved xp stays in the character files for the rework.
        private const bool PulledOut = true;
        public static bool Enabled => !PulledOut && PhaseOne.On && enabled != null && enabled.Value;

        public static void Bind(ConfigFile config)
        {
            enabled = config.Bind("Arcana Levels", "Enabled", true,
                "Phase 1, while the Mod Menu's Phase One switch is on: arcana level 1-10 from use (each cast in a run is 1 xp), each level a flat step on one " +
                "line (damage, or cooldown for the arcana listed below). Off: no xp and no steps.");
            xpScale = config.Bind("Arcana Levels", "XpScale", 1f,
                new ConfigDescription("Casts needed per level, times this (1: level 2 at 11 casts, level 5 at 176, level 10 at 891).",
                    new AcceptableValueRange<float>(0.1f, 10f)));
            damageStep = config.Bind("Arcana Levels", "DamageStep", 0.05f,
                new ConfigDescription("Damage added per level above 1 (0.05: level 10 hits 45% harder).", new AcceptableValueRange<float>(0f, 0.5f)));
            cooldownStep = config.Bind("Arcana Levels", "CooldownStep", 0.03f,
                new ConfigDescription("Cooldown taken off per level above 1, for the cooldown arcana (0.03: level 10 is 27% shorter).",
                    new AcceptableValueRange<float>(0f, 0.09f)));
            cooldownArcana = config.Bind("Arcana Levels", "CooldownArcana", "",
                "Arcana IDs (comma separated) whose step is cooldown instead of damage. Empty: every dash arcana (the " +
                "second slot) steps cooldown, the rest damage.");
        }

        public static void Install(Harmony harmony)
        {
            try
            {
                harmony.Patch(AccessTools.Method(typeof(Player.SkillState), nameof(Player.SkillState.StartCooldownTimer)),
                    prefix: new HarmonyMethod(typeof(ArcanaLevels), nameof(BeforeCooldownStart)),
                    postfix: new HarmonyMethod(typeof(ArcanaLevels), nameof(AfterCooldownStart)));
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Arcana levels: couldn't hook casts ({e.Message})");
            }
            try
            {
                var go = new GameObject("ChaosDepths ArcanaLevelBadge");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<SpellBookBadge>();
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Arcana levels: no spellbook badge ({e.Message})");
            }
        }

        // While the spellbook is open: WoL Art's level badge (ui_arcana_level.png, 9x7, normal and
        // mastery plates, the digits in its 7x5 well) for the arcana under the cursor, with its
        // step and the casts to the next level, in a strip at the bottom of the screen. A first
        // version: drawing it on the game's own card needs its layout seen in game.
        private class SpellBookBadge : MonoBehaviour
        {
            private static readonly Color NormalDigits = new Color32(0xd8, 0xda, 0xe4, 0xff);
            private static readonly Color MasteryDigits = new Color32(0x3a, 0x2a, 0x08, 0xff);
            private SpellBookUI book;
            private float nextFind;

            private void OnGUI()
            {
                if (!Enabled || ChaosDepthsPlugin.Instance == null)
                    return;
                try
                {
                    if (book == null && Time.unscaledTime >= nextFind)
                    {
                        nextFind = Time.unscaledTime + 0.5f;
                        book = FindObjectOfType<SpellBookUI>();
                    }
                    if (book == null)
                        return;
                    var t = Traverse.Create(book);
                    if (!t.Field("isActive").GetValue<bool>())
                        return;
                    Player player = t.Field("player").GetValue<Player>();
                    Player.SkillState skill = t.Field("currentSkill").GetValue<Player.SkillState>();
                    if (player == null || skill == null || string.IsNullOrEmpty(skill.skillID))
                        return;
                    CharacterSave save = ChaosDepthsPlugin.Instance.SaveOf(player);
                    if (save == null)
                        return;
                    Draw(player, save, skill.skillID);
                }
                catch (Exception e)
                {
                    if (failed.Add("badge"))
                        ChaosDepthsPlugin.LogError($"Arcana levels (badge): {e.Message}");
                }
            }

            private static void Draw(Player player, CharacterSave save, string arcana)
            {
                int s = Mathf.Max(2, Mathf.FloorToInt(Screen.height / 180f));
                int level = Level(save, arcana), xp = Xp(save, arcana);
                bool max = level >= MaxLevel;
                string text = Describe(player, save, arcana).ToUpperInvariant() +
                    (max ? "   MASTERED" : $"   {xp - XpFor(level)} / {XpFor(level + 1) - XpFor(level)} CASTS TO LV {level + 1}");
                int font = Mathf.Max(10, 6 * s);
                GUIStyle style = PixelUI.Style(font, PixelUI.Text);
                float textW = style.CalcSize(new GUIContent(text)).x;
                float w = 9 * s + 4 * s + textW + 8 * s, h = 11 * s;
                var strip = new Rect(Mathf.Floor((Screen.width - w) / 2f), Screen.height - h - 6 * s, w, h);
                PixelUI.Fill(strip, new Color(0f, 0f, 0f, 0.6f));
                float bx = strip.x + 4 * s, by = strip.y + 2 * s;
                if (PixelUI.Sheet("ui_arcana_level", 2, max ? 1 : 0, bx, by, s, Color.white) != Vector2.zero)
                    GUI.Label(new Rect(bx + s, by + s, 7 * s, 5 * s), level.ToString(),
                        PixelUI.Style(Mathf.Max(8, 5 * s), max ? MasteryDigits : NormalDigits, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(bx + 13 * s, strip.y, textW + 4 * s, h), text,
                    PixelUI.Style(font, PixelUI.Text, TextAnchor.MiddleLeft));
            }
        }

        // ---- Levels ----

        // Total casts to reach a level: 11 x (level - 1) squared, times XpScale.
        public static int XpFor(int level) =>
            level <= 1 ? 0 : Mathf.RoundToInt(11f * (level - 1) * (level - 1) * (xpScale != null ? xpScale.Value : 1f));

        public static int LevelOf(int xp)
        {
            int level = 1;
            while (level < MaxLevel && xp >= XpFor(level + 1))
                level++;
            return level;
        }

        public static int Xp(CharacterSave save, string arcana)
        {
            if (save?.arcana == null)
                return 0;
            foreach (ArcanaData a in save.arcana)
                if (a.arcana == arcana)
                    return a.xp;
            return 0;
        }

        public static int Level(CharacterSave save, string arcana) => LevelOf(Xp(save, arcana));

        private static bool StepsCooldown(Player player, string arcana)
        {
            string list = cooldownArcana != null ? cooldownArcana.Value : "";
            if (!string.IsNullOrEmpty(list.Trim()))
            {
                foreach (string id in list.Split(','))
                    if (string.Equals(id.Trim(), arcana, StringComparison.OrdinalIgnoreCase))
                        return true;
                return false;
            }
            try
            {
                Player.SkillState dash = player.GetDashSkill();
                return dash != null && dash.skillID == arcana;
            }
            catch
            {
                return false;
            }
        }

        // A one-line description for the panel and the log: "LV 3: +10% damage".
        public static string Describe(Player player, CharacterSave save, string arcana)
        {
            int level = Level(save, arcana);
            if (StepsCooldown(player, arcana))
                return $"LV {level}: -{Mathf.RoundToInt(cooldownStep.Value * (level - 1) * 100f)}% cooldown";
            return $"LV {level}: +{Mathf.RoundToInt(damageStep.Value * (level - 1) * 100f)}% damage";
        }

        // ---- Casts ----

        // Every use of an arcana starts its cooldown once: that's the cast. Before it, the step is
        // put on (or refreshed on) this player's own stats for the arcana, so the cooldown about to
        // start and the hits that follow already have it; the game rebuilds those stats on every
        // level load, so this is checked each cast. After it, the cast counts as xp.
        private static void BeforeCooldownStart(Player.SkillState __instance)
        {
            Player player;
            CharacterSave save;
            if (Owner(__instance, out player, out save))
                Guard("apply", () => Apply(__instance, save));
        }

        private static void AfterCooldownStart(Player.SkillState __instance)
        {
            Player player;
            CharacterSave save;
            if (Owner(__instance, out player, out save) && ChaosDepthsPlugin.InTrialsNow())
                Guard("xp", () => Gain(player, save, __instance.skillID));
        }

        private static bool Owner(Player.SkillState state, out Player player, out CharacterSave save)
        {
            player = null;
            save = null;
            if (!Enabled || state == null || string.IsNullOrEmpty(state.skillID) || ChaosDepthsPlugin.Instance == null)
                return false;
            player = state.parent as Player;
            save = player != null ? ChaosDepthsPlugin.Instance.SaveOf(player) : null;
            return save != null;
        }

        private static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (failed.Add(what))
                    ChaosDepthsPlugin.LogError($"Arcana levels ({what}): {e.Message}");
            }
        }

        private static void Gain(Player player, CharacterSave save, string arcana)
        {
            ArcanaData entry = null;
            foreach (ArcanaData a in save.arcana)
                if (a.arcana == arcana)
                    entry = a;
            if (entry == null)
            {
                entry = new ArcanaData { arcana = arcana };
                save.arcana.Add(entry);
            }
            int before = LevelOf(entry.xp);
            entry.xp++;
            int after = LevelOf(entry.xp);
            if (after > before)
            {
                string line = Describe(player, save, arcana);
                ChaosDepthsPlugin.Notice($"{arcana} reached level {after}{(after == MaxLevel ? " (MAX)" : "")}", 2f);
                ChaosDepthsPlugin.Log($"Arcana levels: player {player.playerID + 1}'s {arcana} reached level {after} at {entry.xp} cast(s): {line}");
                // Saved with the character's next save (level loads, inventory changes).
            }
        }

        // Puts this arcana's step on the casting player's own stats for it (state.skillData:
        // category Player0 / Player1, what the game builds every attack and cooldown from, the way
        // relics mod them), unless that same stats object already has this level's step.
        private static void Apply(Player.SkillState state, CharacterSave save)
        {
            StatData data = state.skillData;
            if (data == null)
                return;
            Player player = (Player)state.parent;
            string arcana = state.skillID;
            int level = Level(save, arcana);
            bool cooldown = StepsCooldown(player, arcana);
            int code = cooldown ? -level : level;
            int had;
            if (applied.TryGetValue(data, out had) && had == code)
                return;
            if (applied.Count > 256)
                applied.Clear();   // old stats objects from earlier level loads
            data.RemoveMod(StatData.damageStr, ModID);
            data.RemoveMod(StatData.cdStr, ModID);
            if (level > 1)
            {
                if (cooldown)
                    data.AddMod(StatData.cdStr, new NumVarStatMod(ModID, -cooldownStep.Value * (level - 1), 10, VarStatModType.Multiplicative, false));
                else
                    data.AddMod(StatData.damageStr, new NumVarStatMod(ModID, damageStep.Value * (level - 1), 10, VarStatModType.Multiplicative, false));
            }
            applied[data] = code;
        }
    }
}
