using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLChaosDepths
{
    // M4, first slice: the deep dungeon.
    //
    // A dive has a depth. It starts at the depth the first player chose in the Hub (the
    // inventory panel, left and right) and goes up by one with every floor and boss arena. The
    // deeper it is, the tougher the enemies (health, and the damage they deal) and the better the
    // loot (item level, rarer items, experience). Each character remembers the deepest depth it
    // has reached: that is its waypoint, and the next dive can start anywhere up to it.
    public static class Depth
    {
        private const string Prefix = "ChaosDepthsDepth_";

        private static ConfigEntry<bool> enabled;
        private static ConfigEntry<float> healthPerDepth, damagePerDepth, xpPerDepth, rarityPerDepth;
        private static ConfigEntry<int> itemLevelsPerDepth;

        // The running dive: its starting depth and how many floors it has gone down since.
        private static bool diving;
        private static int startDepth = 1, floorsDown;
        private static readonly HashSet<int> scaled = new HashSet<int>();
        private static readonly Dictionary<int, float> overrideOriginal = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> overrideScaled = new Dictionary<int, float>();
        private static readonly HashSet<int> playersScaled = new HashSet<int>();
        private static int playerScaleDepth = -1;

        public static bool Enabled => enabled.Value;

        public static int Current => diving ? startDepth + floorsDown : startDepth;

        public static void Bind(ConfigFile config)
        {
            enabled = config.Bind("Depth", "Enabled", true,
                "Dives have a depth: one deeper each floor, starting at the depth chosen in the Hub's inventory panel.");
            healthPerDepth = config.Bind("Depth", "EnemyHealthPerDepth", 0.06f, "Enemy and boss health per depth below the first (0.06 = +6% each).");
            damagePerDepth = config.Bind("Depth", "EnemyDamagePerDepth", 0.04f, "Damage you take per depth below the first (0.04 = +4% each).");
            xpPerDepth = config.Bind("Depth", "XpPerDepth", 0.05f, "Experience per depth below the first (0.05 = +5% each).");
            rarityPerDepth = config.Bind("Depth", "RarityBoostPerDepth", 0.05f, "Magic and better rarities per depth below the first (0.05 = +5% weight each).");
            itemLevelsPerDepth = config.Bind("Depth", "ItemLevelsPerDepth", 1, "Item levels added per depth below the first.");
            // Scale bosses the moment they spawn, not on the next half-second scan (the auto-dive
            // could kill an altar floor boss before the scan reached it).
            try
            {
                MiniBoss.globalMiniBossSpawnEventHandlers += mini => OnBossSpawn(mini);
                Boss.globalBossSpawnEventHandlers += boss => OnBossSpawn(boss);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"depth boss spawn hook: {e.Message}");
            }
        }

        private static int Below => Mathf.Max(0, Current - 1);

        public static float XpMultiplier => enabled.Value ? 1f + xpPerDepth.Value * Below : 1f;

        // Experience comes from the slain enemy's max health. An enemy that died before the depth
        // health bonus reached it (the auto-dive kills within half a second, and bosses reset
        // their health stat) is counted as if it had it.
        public static float MissingHealthFactor(Entity e)
        {
            if (!enabled.Value || !diving || Below <= 0 || e == null || e.health == null || e.health.healthStat == null)
                return 1f;
            return e.health.healthStat.GetModIndex(Prefix + "Health") >= 0 ? 1f : 1f + healthPerDepth.Value * Below;
        }
        public static float RarityBoost => enabled.Value ? rarityPerDepth.Value * Below : 0f;
        public static int ItemLevelBonus => enabled.Value ? itemLevelsPerDepth.Value * Below : 0;

        // A dive starts (from the Hub into the trials): begin at the chosen depth.
        public static void StartDive(CharacterSave save)
        {
            diving = true;
            floorsDown = 0;
            startDepth = save != null ? Mathf.Clamp(save.depthChosen, 1, Mathf.Max(1, save.depthReached)) : 1;
            scaled.Clear();
            ResetPlayers();
            if (enabled.Value)
                ChaosDepthsPlugin.Log($"Depth: dive starts at depth {startDepth}");
        }

        // A trial floor or arena loaded from another: one deeper.
        public static void NextFloor(IEnumerable<CharacterSave> saves)
        {
            if (!diving)
                return;
            floorsDown++;
            scaled.Clear();
            Reached(saves);
            if (enabled.Value)
                ChaosDepthsPlugin.Log($"Depth: now {Current} (enemy health +{healthPerDepth.Value * Below:P0}, damage taken +{damagePerDepth.Value * Below:P0}, " +
                    $"xp +{xpPerDepth.Value * Below:P0}, rarity +{RarityBoost:P0}, item level +{ItemLevelBonus})");
        }

        // The dive is over (back in the Hub or anywhere outside the trials).
        public static void EndDive(IEnumerable<CharacterSave> saves)
        {
            if (!diving)
                return;
            Reached(saves);
            diving = false;
            ResetPlayers();
            if (enabled.Value)
                ChaosDepthsPlugin.Log($"Depth: dive over at depth {startDepth + floorsDown}");
        }

        // Each character's waypoint: the deepest depth it has stood on.
        private static void Reached(IEnumerable<CharacterSave> saves)
        {
            if (!enabled.Value || saves == null)
                return;
            foreach (CharacterSave save in saves)
            {
                if (save == null || Current <= save.depthReached)
                    continue;
                save.depthReached = Current;
                ChaosDepthsPlugin.Log($"Depth: {save.name} reached depth {Current} (new waypoint)");
                ChaosDepthsPlugin.Notice($"Waypoint: depth {Current}", 2.5f);
            }
        }

        // Twice a second in the trials: scale new enemies and the players' damage taken.
        public static void Tick()
        {
            if (!enabled.Value || !diving)
                return;
            float health = healthPerDepth.Value * Below;
            if (health > 0f)
            {
                // Every Enemy in the scene: realm bosses and the altar's floor boss aren't
                // always in the game's enemy lists (the 0.6.0 test saw their health unscaled).
                foreach (Enemy enemy in UnityEngine.Object.FindObjectsOfType<Enemy>())
                    if (enemy != null)
                        Scale(enemy, health);
            }
            if (playerScaleDepth != Current)
            {
                ResetPlayers();
                playerScaleDepth = Current;
            }
            float damage = damagePerDepth.Value * Below;
            Player[] players = GameController.activePlayers;
            if (damage > 0f && players != null)
                foreach (Player p in players)
                {
                    if (p == null || p.health == null || p.health.damageTakenStat == null || !playersScaled.Add(p.GetInstanceID()))
                        continue;
                    p.health.damageTakenStat.AddMod(new NumVarStatMod(Prefix + "DamageTaken", damage, 10, VarStatModType.Additive, false));
                }
        }

        // Every tick, like the Ascension mod: bosses reset their health stat after spawning (the
        // 0.6.1 test saw the same max health at every depth), so a missing mod is added again.
        private static void Scale(Entity e, float health)
        {
            if (e == null || e.health == null || e.health.healthStat == null || e.health.CurrentHealthValue <= 0)
                return;
            NumVarStat stat = e.health.healthStat;
            // Done already: the multiplier is on, or (for an override) the check below finds the
            // override still at its scaled value.
            if (!stat.HasOverrideMod && stat.GetModIndex(Prefix + "Health") >= 0)
                return;
            float before = stat.ModifiedValue;
            // Bosses set their health with an Override mod ('BossHP', 'MiniBossHP') that ignores
            // multipliers, and a second override one priority higher didn't win either (the
            // 0.6.3 test). So the boss's own override is scaled in place: removed, its value
            // multiplied, and added back so the stat recalculates. Its original value is kept
            // per boss so the bonus never compounds.
            var top = stat.HasOverrideMod ? stat.topPriorityOverride as NumVarStatMod : null;
            string how;
            if (top != null)
            {
                int id = e.GetInstanceID();
                float original, scaledValue;
                if (!overrideOriginal.TryGetValue(id, out original) || !overrideScaled.TryGetValue(id, out scaledValue) || !Mathf.Approximately(top.modValue, scaledValue))
                    original = top.modValue; // first time, or the game set a new value
                float target = original * (1f + health);
                if (Mathf.Approximately(top.modValue, target))
                    return;
                stat.RemoveMod(top.ID);
                top.modValue = target;
                top.fillToNewMax = true;
                stat.AddMod(top);
                overrideOriginal[id] = original;
                overrideScaled[id] = target;
                how = $"override '{top.ID}' {original:0} x{1f + health:0.##} in place";
                // A marker so the tick knows this one is done (and XP doesn't add the bonus again).
                if (stat.GetModIndex(Prefix + "Health") < 0)
                    stat.AddMod(new NumVarStatMod(Prefix + "Health", 0f, 10, VarStatModType.Additive, false));
            }
            else
            {
                if (stat.GetModIndex(Prefix + "Health") >= 0)
                    return;
                stat.AddMod(new NumVarStatMod(Prefix + "Health", health, 10, VarStatModType.Multiplicative, true));
                how = "multiplier";
            }
            bool again = !scaled.Add(e.GetInstanceID());
            if (e is Boss || e is MiniBoss || Mathf.Approximately(before, stat.ModifiedValue))
                ChaosDepthsPlugin.Log($"Depth: {e.GetType().Name} max health {before:0} -> {stat.ModifiedValue:0}, health now {e.health.CurrentHealthValue} (+{health:P0}, {how}){(again ? ", re-applied after the game reset it" : "")}");
        }

        private static void OnBossSpawn(Entity boss)
        {
            try
            {
                if (enabled.Value && diving && Below > 0)
                    Scale(boss, healthPerDepth.Value * Below);
                Layers.OnBossSpawn(boss, Current);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"depth boss spawn: {e.Message}");
            }
        }

        private static void ResetPlayers()
        {
            playersScaled.Clear();
            playerScaleDepth = -1;
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            foreach (Player p in players)
                if (p != null && p.health != null && p.health.damageTakenStat != null)
                    p.health.damageTakenStat.RemoveMod(Prefix + "DamageTaken");
        }
    }
}
