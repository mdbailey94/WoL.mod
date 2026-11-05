using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace WoLDamageMeter
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class DamageMeterPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.damagemeter";
        public const string PluginName = "Damage Meter";
        public const string PluginVersion = "0.1.0";

        // How often the overlay text is rebuilt while it's on screen (seconds, real time).
        private const float RefreshInterval = 0.1f;
        // How long the final numbers take to fade out once the hold time is over (seconds).
        private const float FadeSeconds = 1f;
        // Overlay layout, in pixels of a 1080p screen (scaled to the real resolution), matching ExtendedStats.
        private const float ReferenceHeight = 1080f;
        private const float Margin = 16f;
        // Top-right corner: ExtendedStats' panel sits on the left from y=140, the health bar is top-left.
        private const float PanelTop = 16f;
        // Narrowest the panel gets, so it doesn't jump around as the numbers change.
        private const float MinPanelWidth = 280f;
        private const int FontSize = 16;
        private const float BackgroundAlpha = 0.75f;

        private static DamageMeterPlugin instance;
        private static ManualLogSource log;
        private static readonly HashSet<string> loggedErrors = new HashSet<string>();
        private static bool loggedFirstDummyHit;

        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<KeyboardShortcut> toggleKey;
        private ConfigEntry<bool> visible;
        private ConfigEntry<float> idleSeconds;
        private ConfigEntry<float> holdSeconds;
        private ConfigEntry<bool> meterEnemies;
        private ConfigEntry<float> panelScale;

        private MeterSession session;
        private bool sessionEnded;
        private float endedAt;
        private string overlayText;
        private float nextRefresh;
        private float panelWidth;
        private GUIStyle textStyle;
        private GUIStyle boxStyle;

        private void Awake()
        {
            instance = this;
            log = Logger;

            modEnabled = Config.Bind("General", "Enabled", true,
                "Turn the mod on or off (also in the title screen Mods menu).");
            toggleKey = Config.Bind("Hotkeys", "Toggle", new KeyboardShortcut(KeyCode.F3),
                "Show or hide the damage meter overlay (sessions are still logged while hidden).");
            visible = Config.Bind("General", "Visible", true,
                "Show the overlay while you're hitting a dummy (toggled with the hotkey).");
            idleSeconds = Config.Bind("General", "IdleSeconds", 3f,
                new ConfigDescription("A session ends after this many seconds without a hit.",
                    new AcceptableValueRange<float>(0.5f, 30f)));
            holdSeconds = Config.Bind("General", "HoldSeconds", 5f,
                new ConfigDescription("How long the final numbers stay on screen after a session ends, before fading.",
                    new AcceptableValueRange<float>(0f, 60f)));
            meterEnemies = Config.Bind("General", "MeterEnemies", false,
                "Also meter damage to real enemies, not just training dummies.");
            panelScale = Config.Bind("General", "Scale", 1f,
                new ConfigDescription("Overlay size multiplier.", new AcceptableValueRange<float>(0.5f, 2f)));

            // The game's own "damage actually dealt" event, fired from Health.TakeDamage after crits,
            // armor, elemental and other modifiers. The game adds and removes its own handlers with
            // Delegate.Combine/Remove, so ours stays subscribed.
            Health.globalTakeDamageActualHandlers += OnAnyDamage;

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded (toggle: {toggleKey.Value}, idle gap {idleSeconds.Value:0.#} s, " +
                           $"enemies {(meterEnemies.Value ? "on" : "off")})");
        }

        private void OnDestroy() => Health.globalTakeDamageActualHandlers -= OnAnyDamage;

        private static void LogErrorOnce(string where, Exception e)
        {
            if (loggedErrors.Add(where))
                log?.LogError($"[{where}] {e}");
        }

        private static void OnAnyDamage(AttackInfo attack, Entity attacker, Entity hurt)
        {
            try
            {
                if (instance != null)
                    instance.OnDamage(attack, attacker, hurt);
            }
            catch (Exception e)
            {
                LogErrorOnce("hit", e);
            }
        }

        private void OnDamage(AttackInfo attack, Entity attacker, Entity hurt)
        {
            if (!modEnabled.Value || attack == null || hurt == null)
                return;

            // BossDummy derives from TrainingDummy, and both derive from Enemy.
            bool dummy = hurt is TrainingDummy;
            if (!dummy && !(meterEnemies.Value && hurt is Enemy))
                return;
            if (FindAttacker(attack, attacker) == null)
                return;

            int dealt = attack.damage;
            if (dummy)
            {
                dealt = Uncapped(hurt.health, attack, dealt);
                LogFirstDummyHit(hurt, dealt);
            }
            if (dealt <= 0)
                return;

            float now = Time.time;
            if (session == null || sessionEnded)
            {
                session = new MeterSession(now);
                sessionEnded = false;
                panelWidth = 0f;
            }
            session.Add(dealt, attack.isCritical, attack.skillID, hurt.GetInstanceID(), now);
            nextRefresh = 0f;
        }

        // TakeDamage caps the announced damage at the target's remaining health. Dummies have no
        // death state and don't heal between hits, so once one is near 0 the cap would hide real
        // damage. Health keeps the uncapped value (after all modifiers) for the hit it just took.
        private static int Uncapped(Health health, AttackInfo attack, int dealt)
        {
            if (health != null && health.currentAtkInfo == attack)
                return Mathf.Max(dealt, Mathf.CeilToInt(health.currentDmgVal));
            return dealt;
        }

        // One line so the log shows what a dummy looks like to us (type and health pool).
        private static void LogFirstDummyHit(Entity dummy, int dealt)
        {
            if (loggedFirstDummyHit)
                return;
            loggedFirstDummyHit = true;
            Health health = dummy.health;
            string pool = health != null && health.healthStat != null
                ? $"{health.healthStat.CurrentValue:0}/{health.healthStat.ModifiedValue:0}"
                : "?";
            log?.LogInfo($"First dummy hit: {dummy.GetType().Name} '{dummy.name}', {dealt} dmg, health now {pool}");
        }

        // Same rules as ExtendedStats: the attacking entity if it's a player, else the player
        // whose skill category the attack carries (projectiles and summons may not set the entity).
        private static Player FindAttacker(AttackInfo attack, Entity attacker)
        {
            var player = attacker as Player ?? attack.entity as Player;
            if (player != null)
                return player;

            Player[] players = GameController.activePlayers;
            if (players == null || string.IsNullOrEmpty(attack.skillCategory))
                return null;
            foreach (Player candidate in players)
            {
                if (candidate != null && candidate.skillCategory == attack.skillCategory)
                    return candidate;
            }
            return null;
        }

        private void Update()
        {
            try
            {
                if (!modEnabled.Value)
                {
                    session = null;
                    overlayText = null;
                    return;
                }

                if (toggleKey.Value.IsDown())
                    visible.Value = !visible.Value;

                if (session != null && !sessionEnded && Time.time - session.LastHitTime > idleSeconds.Value)
                    EndSession();

                if (session != null && sessionEnded && Time.unscaledTime - endedAt > holdSeconds.Value + FadeSeconds)
                    session = null;

                if (session == null)
                {
                    overlayText = null;
                    return;
                }
                if (Time.unscaledTime < nextRefresh)
                    return;
                nextRefresh = Time.unscaledTime + RefreshInterval;
                overlayText = session.Overlay(sessionEnded);
            }
            catch (Exception e)
            {
                LogErrorOnce("update", e);
            }
        }

        private void EndSession()
        {
            sessionEnded = true;
            endedAt = Time.unscaledTime;
            nextRefresh = 0f;
            foreach (string line in session.LogLines())
                Logger.LogInfo(line);
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(overlayText) || !visible.Value)
                return;

            // Full opacity while live and during the hold, then a linear fade.
            float alpha = 1f;
            if (sessionEnded)
                alpha = Mathf.Clamp01(1f - (Time.unscaledTime - endedAt - holdSeconds.Value) / FadeSeconds);
            if (alpha <= 0f)
                return;

            // GUI.matrix and GUI.color are shared with every other mod's OnGUI, so always put them back.
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            try
            {
                if (textStyle == null)
                {
                    textStyle = new GUIStyle(GUI.skin.label) { fontSize = FontSize, richText = true, wordWrap = false };
                    boxStyle = new GUIStyle(GUI.skin.box);
                    boxStyle.normal.background = MakeTexture(new Color(0f, 0f, 0f, BackgroundAlpha));
                }

                float scale = Screen.height / ReferenceHeight * panelScale.Value;
                float virtualWidth = Screen.width / scale;
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
                GUI.color = new Color(1f, 1f, 1f, alpha);

                var content = new GUIContent(overlayText);
                Vector2 size = textStyle.CalcSize(content);
                // Only ever grows during a session, so the right-aligned panel doesn't wobble.
                panelWidth = Mathf.Max(panelWidth, MinPanelWidth, size.x + 24f);
                var area = new Rect(virtualWidth - panelWidth - Margin, PanelTop, panelWidth, size.y + 16f);
                GUI.Box(area, GUIContent.none, boxStyle);
                GUI.Label(new Rect(area.x + 12f, area.y + 8f, size.x, size.y), content, textStyle);
            }
            catch (Exception e)
            {
                LogErrorOnce("draw", e);
            }
            finally
            {
                GUI.color = previousColor;
                GUI.matrix = previousMatrix;
            }
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
