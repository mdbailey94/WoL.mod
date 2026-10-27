using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLChaosDepths
{
    // M3, first slice: elite enemies and Ascension-scaled loot.
    //
    // While in the trials, each new ordinary enemy (not a boss or floor boss) has a chance to
    // become an elite: more health, a tint, and one affix. Elites give more experience and always
    // drop an item of at least Magic rarity. The Ascension mod's level (when it is installed)
    // raises the elite chance and the odds of better rarities.
    public static class Elites
    {
        public class Affix
        {
            public string name;
            public Color tint;
            public Affix(string name, Color tint)
            {
                this.name = name;
                this.tint = tint;
            }
        }

        // Swift: faster. Armored: takes less damage. Giant: bigger, even more health.
        private static readonly Affix[] Affixes =
        {
            new Affix("Swift", new Color(0.55f, 0.85f, 1f)),
            new Affix("Armored", new Color(0.75f, 0.75f, 0.85f)),
            new Affix("Giant", new Color(1f, 0.6f, 0.45f)),
        };

        private const string Prefix = "ChaosDepthsElite_";

        private static ConfigEntry<bool> enabled;
        private static ConfigEntry<float> chance, chancePerAscension, healthMultiplier, xpMultiplier;
        private static ConfigEntry<float> swiftSpeed, armoredReduction, giantScale, giantHealth;
        private static ConfigEntry<int> dropMinRarity;
        private static ConfigEntry<float> rarityBoostPerAscension;

        // Enemies already rolled this scene, and the ones that became elites.
        private static readonly HashSet<int> rolled = new HashSet<int>();
        private static readonly Dictionary<int, Affix> elites = new Dictionary<int, Affix>();
        private static readonly Dictionary<int, Entity> eliteEntities = new Dictionary<int, Entity>();

        public static float XpMultiplier => xpMultiplier.Value;
        public static int DropMinRarity => dropMinRarity.Value;

        public static void Bind(ConfigFile config)
        {
            enabled = config.Bind("Elites", "Enabled", true, "Ordinary enemies can spawn as elites in the trials.");
            chance = config.Bind("Elites", "Chance", 0.08f,
                new ConfigDescription("Chance an ordinary enemy spawns as an elite.", new AcceptableValueRange<float>(0f, 1f)));
            chancePerAscension = config.Bind("Elites", "ChancePerAscension", 0.01f,
                "Added to the elite chance per Ascension level (Ascension mod).");
            healthMultiplier = config.Bind("Elites", "HealthMultiplier", 2.5f, "Elite health, times the enemy's own.");
            xpMultiplier = config.Bind("Elites", "XpMultiplier", 2f,
                "Extra experience for an elite, on top of the experience from its larger health.");
            dropMinRarity = config.Bind("Elites", "DropMinRarity", 1,
                "Elites always drop an item of at least this rarity (0 Common, 1 Magic, 2 Rare, 3 Legendary).");
            swiftSpeed = config.Bind("Elites", "SwiftSpeed", 0.4f, "Swift elites move this much faster (0.4 = +40%).");
            armoredReduction = config.Bind("Elites", "ArmoredReduction", 0.4f, "Armored elites take this much less damage (0.4 = -40%).");
            giantScale = config.Bind("Elites", "GiantScale", 1.35f, "Giant elites are drawn this much larger.");
            giantHealth = config.Bind("Elites", "GiantHealth", 1.5f, "Giant elites have this much more health again.");
            rarityBoostPerAscension = config.Bind("Loot", "RarityBoostPerAscension", 0.15f,
                "Per Ascension level, Magic and better rarities become this much more likely (0.15 = +15% weight each level).");
        }

        private static bool announced;

        public static void ClearScene()
        {
            announced = false;
            rolled.Clear();
            elites.Clear();
            eliteEntities.Clear();
        }

        public static bool IsElite(Entity entity) => entity != null && elites.ContainsKey(entity.GetInstanceID());

        public static string AffixOf(Entity entity)
        {
            Affix affix;
            return entity != null && elites.TryGetValue(entity.GetInstanceID(), out affix) ? affix.name : null;
        }

        // The Ascension mod's level, or 0 without it.
        public static int AscensionLevel()
        {
            try
            {
                PluginInfo info;
                if (!Chainloader.PluginInfos.TryGetValue("mdbailey94.wol.ascension", out info) || info.Instance == null)
                    return 0;
                var field = info.Instance.GetType().GetField("level",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var entry = field != null ? field.GetValue(field.IsStatic ? null : info.Instance) as ConfigEntry<int> : null;
                return entry != null ? Mathf.Max(0, entry.Value) : 0;
            }
            catch
            {
                return 0;
            }
        }

        public static float RarityBoost() => rarityBoostPerAscension.Value * AscensionLevel();

        public static float Chance() => Mathf.Clamp01(chance.Value + chancePerAscension.Value * AscensionLevel() + Layers.EliteChanceBonus);

        // Twice a second from the plugin, in the trials: roll new enemies, keep elite tints on.
        public static void Tick()
        {
            if (!enabled.Value || GameController.enemies == null)
                return;
            float odds = Chance();
            if (!announced)
            {
                announced = true;
                ChaosDepthsPlugin.Log($"Elites: chance {odds:P0} per enemy, Ascension {AscensionLevel()}, rarity boost +{RarityBoost() + Depth.RarityBoost + Layers.RarityBonus:P0} (Ascension +{RarityBoost():P0}, depth +{Depth.RarityBoost:P0}, rift +{Layers.RarityBonus:P0})");
            }
            foreach (GameObject go in new List<GameObject>(GameController.enemies))
            {
                if (go == null || !go.activeInHierarchy)
                    continue;
                int id;
                Entity entity = go.GetComponent<Entity>();
                if (entity == null || entity.health == null)
                    continue;
                id = entity.GetInstanceID();
                if (!rolled.Add(id))
                    continue;
                if (entity is Boss || entity is MiniBoss || entity is Player || Stationary(entity))
                    continue;
                if (UnityEngine.Random.value < odds)
                    MakeElite(entity, Affixes[UnityEngine.Random.Range(0, Affixes.Length)]);
            }
            // Hit flashes reset the colour: put the tint back.
            foreach (KeyValuePair<int, Entity> pair in eliteEntities)
                if (pair.Value != null && pair.Value.spriteRenderer != null && pair.Value.health != null && pair.Value.health.CurrentHealthValue > 0)
                    pair.Value.spriteRenderer.color = elites[pair.Key].tint;
        }

        // Spawners and turrets (Coffin, EnemyTurret...) don't move or fight like a creature, so
        // an elite affix on them reads as a bug.
        private static readonly string[] StationaryNames = { "Coffin", "Turret", "Spawner", "Totem", "Factory" };

        private static bool Stationary(Entity entity)
        {
            string name = entity.GetType().Name;
            foreach (string part in StationaryNames)
                if (name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        // Debug: one elite of each affix beside the wizard, to look at the tints, the Giant's size
        // and the Swift speed.
        // The enemy names the game can spawn (the keys of its prefab table), for the config hint.
        public static List<string> EnemyNames()
        {
            var names = new List<string>();
            try
            {
                if (Enemy.enemyPrefabPaths != null)
                    names.AddRange(Enemy.enemyPrefabPaths.Keys);
            }
            catch
            {
            }
            if (names.Count == 0)
                names.AddRange(Enum.GetNames(typeof(Enemy.EName)));
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        // plainHealth > 0: three plain enemies (no affix) with that many times their health.
        public static void SpawnShowcase(Player player, string enemyName, float plainHealth = 0f)
        {
            if (player == null)
                return;
            // A name the game doesn't know would throw inside Enemy.Spawn; fall back to the
            // Knight and log the names that work (case matters).
            List<string> known = EnemyNames();
            if (!known.Contains(enemyName))
            {
                string match = known.Find(n => string.Equals(n, enemyName, StringComparison.OrdinalIgnoreCase));
                ChaosDepthsPlugin.Log($"Debug: no enemy called '{enemyName}'{(match != null ? $" (did you mean '{match}'?)" : "")}; spawning Knights instead. Known names: {string.Join(", ", known.ToArray())}");
                enemyName = match ?? "Knight";
            }
            Vector3 origin = player.transform.position;
            for (int i = 0; i < Affixes.Length; i++)
            {
                Vector3 pos = origin + new Vector3((i - 1) * 3f, 4f, 0f);
                try
                {
                    Enemy enemy = Enemy.Spawn(enemyName, pos, null, true, true);
                    if (enemy == null)
                    {
                        ChaosDepthsPlugin.Log($"Debug: couldn't spawn a '{enemyName}' (set [Debug] ShowcaseEnemy to another enemy name)");
                        return;
                    }
                    if (plainHealth > 0f)
                    {
                        enemy.health?.healthStat?.AddMod(new NumVarStatMod(Prefix + "Showcase", plainHealth - 1f, 10, VarStatModType.Multiplicative, true));
                        rolled.Add(enemy.GetInstanceID());   // never rolled as an elite
                        continue;
                    }
                    MakeElite(enemy, Affixes[i]);
                }
                catch (Exception e)
                {
                    ChaosDepthsPlugin.LogError($"Debug elite showcase: {e.Message}");
                    return;
                }
            }
            if (plainHealth > 0f)
                ChaosDepthsPlugin.Log($"Debug: spawned three plain {enemyName}s with {plainHealth:0.#}x health beside the wizard");
            else
                ChaosDepthsPlugin.Log($"Debug: spawned a Swift, an Armored and a Giant {enemyName} beside the wizard");
        }

        public static void MakeElite(Entity entity, Affix affix)
        {
            try
            {
                float health = healthMultiplier.Value * (affix.name == "Giant" ? giantHealth.Value : 1f);
                entity.health.healthStat?.AddMod(new NumVarStatMod(Prefix + "Health", health - 1f, 10, VarStatModType.Multiplicative, true));
                if (affix.name == "Swift")
                    entity.movement?.moveSpeedStat?.AddMod(new NumVarStatMod(Prefix + "Speed", swiftSpeed.Value, 10, VarStatModType.Multiplicative, false));
                if (affix.name == "Armored")
                    entity.health.damageTakenStat?.AddMod(new NumVarStatMod(Prefix + "Armor", -armoredReduction.Value, 10, VarStatModType.Additive, false));
                if (affix.name == "Giant")
                    entity.transform.localScale = entity.transform.localScale * giantScale.Value;
                if (entity.spriteRenderer != null)
                    entity.spriteRenderer.color = affix.tint;
                int id = entity.GetInstanceID();
                rolled.Add(id);
                elites[id] = affix;
                eliteEntities[id] = entity;
                ChaosDepthsPlugin.Log($"Elite: {affix.name} {entity.GetType().Name} (health {entity.health.CurrentHealthValue})");
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"elite {affix.name}: {e.Message}");
            }
        }
    }
}
