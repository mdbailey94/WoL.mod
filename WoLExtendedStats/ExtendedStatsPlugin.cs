using System;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLExtendedStats
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class ExtendedStatsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.extendedstats";
        public const string PluginName = "Extended Stats";
        public const string PluginVersion = "0.1.0";

        private const float RefreshInterval = 0.25f;
        private const float ReferenceHeight = 1080f;

        private ConfigEntry<KeyboardShortcut> toggleKey;
        private ConfigEntry<bool> visible;
        private ConfigEntry<float> panelScale;

        private string panelText = string.Empty;
        private float nextRefresh;
        private GUIStyle textStyle;
        private GUIStyle boxStyle;

        private void Awake()
        {
            toggleKey = Config.Bind("Hotkeys", "Toggle", new KeyboardShortcut(KeyCode.F2), "Show or hide the stats panel.");
            visible = Config.Bind("General", "Visible", true, "Whether the panel is shown when the game starts.");
            panelScale = Config.Bind("General", "Scale", 1f,
                new ConfigDescription("Panel size multiplier.", new AcceptableValueRange<float>(0.5f, 2f)));

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded (toggle with {toggleKey.Value})");
        }

        private void Update()
        {
            if (toggleKey.Value.IsDown())
                visible.Value = !visible.Value;

            if (!visible.Value || Time.unscaledTime < nextRefresh)
                return;

            nextRefresh = Time.unscaledTime + RefreshInterval;
            try
            {
                panelText = BuildText(GetLocalPlayer());
            }
            catch (Exception e)
            {
                // Never let a bad read break the game loop; show it in the panel instead.
                panelText = "Extended Stats error:\n" + e.Message;
            }
        }

        private void OnGUI()
        {
            if (!visible.Value || string.IsNullOrEmpty(panelText))
                return;

            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, wordWrap = false };
                boxStyle = new GUIStyle(GUI.skin.box);
                boxStyle.normal.background = MakeTexture(new Color(0f, 0f, 0f, 0.7f));
            }

            float scale = Screen.height / ReferenceHeight * panelScale.Value;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            var content = new GUIContent(panelText);
            Vector2 size = textStyle.CalcSize(content);
            var area = new Rect(16f, 140f, size.x + 24f, size.y + 16f);
            GUI.Box(area, GUIContent.none, boxStyle);
            GUI.Label(new Rect(area.x + 12f, area.y + 8f, size.x, size.y), content, textStyle);

            GUI.matrix = previous;
        }

        private static Player GetLocalPlayer()
        {
            Player[] players = GameController.activePlayers;
            if (players == null)
                return null;

            foreach (Player player in players)
            {
                if (player != null)
                    return player;
            }
            return null;
        }

        private static string BuildText(Player player)
        {
            if (player == null)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("<b>WIZARD</b>");

            Health health = player.health;
            if (health != null)
            {
                Line(sb, "Health", $"{health.CurrentHealthValue} / {Value(health.healthStat):0}");
                if (health.CurrentShieldValue > 0)
                    Line(sb, "Shield", health.CurrentShieldValue.ToString());
                Line(sb, "Armor", Percent(Value(health.armorStat)));
                Line(sb, "Evade", Percent(Value(health.evadeStat)));
                Line(sb, "Damage taken", Multiplier(Value(health.damageTakenStat)));
                Line(sb, "Healing", Multiplier(Value(health.healModifierStat)));
            }

            Movement movement = player.movement;
            if (movement != null)
            {
                Line(sb, "Move speed", $"{Value(movement.moveSpeedStat):0.#}");
                Line(sb, "Dash speed", $"{Value(movement.dashSpeedStat):0.#}");
            }

            if (Player.goldWallet != null)
                Line(sb, "Gold", Player.goldWallet.balance.ToString());
            if (Player.platWallet != null)
                Line(sb, "Platinum", Player.platWallet.balance.ToString());

            AppendSkills(sb, player);
            AppendRunStats(sb, player.stats);

            return sb.ToString().TrimEnd();
        }

        private static void AppendSkills(StringBuilder sb, Player player)
        {
            Player.SkillState[] skills = player.assignedSkills;
            if (skills == null)
                return;

            sb.AppendLine();
            sb.AppendLine("<b>ARCANA</b>  <color=#9aa>dmg · crit · crit dmg · cooldown</color>");

            foreach (Player.SkillState skill in skills)
            {
                if (skill == null || string.IsNullOrEmpty(skill.skillID))
                    continue;

                StatData data = skill.skillData;
                string name = SkillName(skill.skillID);
                if (data == null)
                {
                    sb.AppendLine(name);
                    continue;
                }

                float damage = Stat(data, StatData.damageStr, true);
                float critChance = Stat(data, StatData.critChStr, false);
                float critDamage = Stat(data, StatData.critDmgStr, false);
                float cooldown = Cooldown(player, skill, data);

                string empowered = skill.IsEmpowered ? " <color=#fd5>(enhanced)</color>" : string.Empty;
                sb.AppendLine($"{name}{empowered}");
                sb.AppendLine($"   {damage:0} · {Percent(critChance)} · {critDamage:0.##}x · {cooldown:0.##}s");
            }
        }

        private static void AppendRunStats(StringBuilder sb, PlayerEndStats stats)
        {
            if (stats == null)
                return;

            sb.AppendLine();
            sb.AppendLine("<b>THIS RUN</b>");
            int basic = stats.GetStatValue(PlayerEndStats.Stat.Basic);
            int spell = stats.GetStatValue(PlayerEndStats.Stat.Spell);
            Line(sb, "Damage dealt", $"{basic + spell}  <color=#9aa>(basic {basic}, arcana {spell})</color>");
            Line(sb, "Damage taken", stats.GetStatValue(PlayerEndStats.Stat.Taken).ToString());
            Line(sb, "Enemies defeated", stats.GetStatValue(PlayerEndStats.Stat.Enemies).ToString());
            Line(sb, "Gold spent", stats.GetStatValue(PlayerEndStats.Stat.Spent).ToString());
        }

        // Reads a skill stat with relic/outfit modifiers applied. Each field is stored as a list
        // of ints or floats, so a failed read on one field just shows 0 instead of hiding the panel.
        private static float Stat(StatData data, string field, bool isInt)
        {
            try
            {
                return isInt
                    ? data.ApplyVarStatMod(field, data.GetValue<int>(field))
                    : data.ApplyVarStatMod(field, data.GetValue<float>(field));
            }
            catch
            {
                return 0f;
            }
        }

        private static float Cooldown(Player player, Player.SkillState skill, StatData data)
        {
            try
            {
                if (player.cooldownManager != null && player.cooldownManager.cooldowns != null
                    && player.cooldownManager.cooldowns.ContainsKey(skill.skillID))
                    return player.cooldownManager.GetMaxTime(skill.skillID);
            }
            catch
            {
                // Fall back to the stat value below.
            }
            return Stat(data, StatData.cdStr, false);
        }

        private static string SkillName(string skillID)
        {
            TextManager.SkillInfo info;
            if (TextManager.skillInfoDict != null && TextManager.skillInfoDict.TryGetValue(skillID, out info)
                && !string.IsNullOrEmpty(info.displayName))
                return info.displayName;
            return skillID;
        }

        private static float Value(NumVarStat stat) => stat != null ? stat.CurrentValue : 0f;

        private static string Percent(float fraction) => $"{fraction * 100f:0.#}%";

        private static string Multiplier(float value) => $"{value:0.##}x";

        private static void Line(StringBuilder sb, string label, string value) =>
            sb.AppendLine($"{label}: <b>{value}</b>");

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
