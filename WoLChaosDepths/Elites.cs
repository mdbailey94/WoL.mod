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

        // Enemies already rolled this scene, and the ones that became elites (with their affixes
        // and the tint those give).
        private static readonly HashSet<int> rolled = new HashSet<int>();
        private static readonly Dictionary<int, Affix[]> elites = new Dictionary<int, Affix[]>();
        private static readonly Dictionary<int, Color> tints = new Dictionary<int, Color>();
        private static readonly Dictionary<int, Entity> eliteEntities = new Dictionary<int, Entity>();

        public static float XpMultiplier => xpMultiplier.Value;
        // Ascension 5 and up (#27): elites drop Rare or better.
        public static int DropMinRarity => AscensionLink.ElitesDropRare ? Mathf.Max(dropMinRarity.Value, 2) : dropMinRarity.Value;

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
            // Ascension 5 and up: bosses get an elite affix as they appear (#27).
            try
            {
                MiniBoss.globalMiniBossSpawnEventHandlers += mini => OnBossSpawn(mini, false);
                Boss.globalBossSpawnEventHandlers += boss => OnBossSpawn(boss, true);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"elite boss spawn hook: {e.Message}");
            }
        }

        private static bool announced;

        public static void ClearScene()
        {
            announced = false;
            rolled.Clear();
            elites.Clear();
            tints.Clear();
            eliteEntities.Clear();
            bossesDone.Clear();
        }

        // Ordinary elites only (a boss with an affix isn't one: it drops and scores as a boss).
        public static bool IsElite(Entity entity) => entity != null && elites.ContainsKey(entity.GetInstanceID());

        // "Swift", or "Swift + Armored" for two.
        public static string AffixOf(Entity entity)
        {
            Affix[] affixes;
            if (entity == null || !elites.TryGetValue(entity.GetInstanceID(), out affixes))
                return null;
            var names = new string[affixes.Length];
            for (int i = 0; i < affixes.Length; i++)
                names[i] = affixes[i].name;
            return string.Join(" + ", names);
        }

        // The Ascension mod's level (the run's while one is on), or 0 without it.
        public static int AscensionLevel() => AscensionLink.Level;

        public static float RarityBoost() => rarityBoostPerAscension.Value * AscensionLevel();

        // Ascension's table sets the elite chance from level 1 (12% up to 40%); at level 0, or
        // with an Ascension older than its table, Chance plus ChancePerAscension a level.
        public static float Chance()
        {
            float table = AscensionLink.EliteChance;
            float baseChance = table > 0f ? table : chance.Value + chancePerAscension.Value * AscensionLevel();
            return Mathf.Clamp01(baseChance + Layers.EliteChanceBonus);
        }

        // One affix, or (Ascension 4 and up, 1 in 4 at 4, half at 8, all at 10) two different ones.
        private static Affix[] RollAffixes()
        {
            Affix first = Affixes[UnityEngine.Random.Range(0, Affixes.Length)];
            if (UnityEngine.Random.value >= AscensionLink.TwoAffixChance)
                return new[] { first };
            Affix second;
            do
                second = Affixes[UnityEngine.Random.Range(0, Affixes.Length)];
            while (second == first);
            return new[] { first, second };
        }

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
                {
                    Affix[] affixes = RollAffixes();
                    MakeElite(entity, affixes);
                    if (AscensionLink.ElitesInPairs)
                        SpawnPartner(entity, affixes);
                }
            }
            // Hit flashes reset the colour: put the tint back.
            foreach (KeyValuePair<int, Entity> pair in eliteEntities)
                if (pair.Value != null && pair.Value.spriteRenderer != null && pair.Value.health != null && pair.Value.health.CurrentHealthValue > 0)
                    pair.Value.spriteRenderer.color = tints[pair.Key];
        }

        // Ascension 9 and up: an elite comes with a partner, another of the same enemy with the
        // same affixes, summoned beside it the way the game brings enemies in.
        private static void SpawnPartner(Entity entity, Affix[] affixes)
        {
            try
            {
                string name = entity.gameObject.name.Replace("(Clone)", "").Trim();
                if (Enemy.enemyPrefabPaths == null || !Enemy.enemyPrefabPaths.ContainsKey(name))
                {
                    ChaosDepthsPlugin.Log($"Elite pair: no prefab named '{name}' to summon a partner from");
                    return;
                }
                Vector3 at = entity.transform.position + (Vector3)(UnityEngine.Random.insideUnitCircle.normalized * 1.5f);
                Enemy partner = Enemy.Spawn(name, at, entity.transform.parent, true, true);
                if (partner == null)
                    return;
                rolled.Add(partner.GetInstanceID());   // never rolled again (and never pairs itself)
                MakeElite(partner, affixes);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"elite pair: {e.Message}");
            }
        }

        // ---- Bosses with affixes (Ascension 5 and up; realm bosses two at 10) ----

        private static readonly HashSet<int> bossesDone = new HashSet<int>();
        // A Giant boss would be scaled up inside its arena, so bosses roll Swift or Armored only.
        private static readonly Affix[] BossAffixPool = { Affixes[0], Affixes[1] };

        private static void OnBossSpawn(Entity boss, bool realm)
        {
            try
            {
                if (!enabled.Value || boss == null || boss.health == null || !ChaosDepthsPlugin.InTrialsNow())
                    return;
                int count = Mathf.Min(AscensionLink.BossAffixes(realm), BossAffixPool.Length);
                if (count <= 0 || !bossesDone.Add(boss.GetInstanceID()))
                    return;
                var picked = new List<Affix>(BossAffixPool);
                while (picked.Count > count)
                    picked.RemoveAt(UnityEngine.Random.Range(0, picked.Count));
                var names = new List<string>();
                for (int i = 0; i < picked.Count; i++)
                {
                    ApplyEffect(boss, picked[i]);
                    EliteMarkers.Attach(boss, picked[i].name, i, picked.Count);
                    names.Add(picked[i].name);
                }
                ChaosDepthsPlugin.Log($"Ascension {AscensionLevel()}: {(realm ? "realm" : "floor")} boss {boss.GetType().Name} is {string.Join(" + ", names.ToArray())}");
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"boss affix: {e.Message}");
            }
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

        // An elite with one affix or two: the elite's extra health (more again for a Giant), each
        // affix's effect, a tint between their colours and a marker for each over its head.
        public static void MakeElite(Entity entity, params Affix[] affixes)
        {
            try
            {
                bool giant = Array.Exists(affixes, a => a.name == "Giant");
                float health = healthMultiplier.Value * (giant ? giantHealth.Value : 1f);
                entity.health.healthStat?.AddMod(new NumVarStatMod(Prefix + "Health", health - 1f, 10, VarStatModType.Multiplicative, true));
                Color tint = Color.black;
                for (int i = 0; i < affixes.Length; i++)
                {
                    ApplyEffect(entity, affixes[i]);
                    EliteMarkers.Attach(entity, affixes[i].name, i, affixes.Length);
                    tint += affixes[i].tint / affixes.Length;
                }
                tint.a = 1f;
                if (entity.spriteRenderer != null)
                    entity.spriteRenderer.color = tint;
                int id = entity.GetInstanceID();
                rolled.Add(id);
                elites[id] = affixes;
                tints[id] = tint;
                eliteEntities[id] = entity;
                ChaosDepthsPlugin.Log($"Elite: {AffixOf(entity)} {entity.GetType().Name} (health {entity.health.CurrentHealthValue})");
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"elite: {e.Message}");
            }
        }

        private static void ApplyEffect(Entity entity, Affix affix)
        {
            if (affix.name == "Swift")
                entity.movement?.moveSpeedStat?.AddMod(new NumVarStatMod(Prefix + "Speed", swiftSpeed.Value, 10, VarStatModType.Multiplicative, false));
            if (affix.name == "Armored")
                entity.health?.damageTakenStat?.AddMod(new NumVarStatMod(Prefix + "Armor", -armoredReduction.Value, 10, VarStatModType.Additive, false));
            if (affix.name == "Giant")
                entity.transform.localScale = entity.transform.localScale * giantScale.Value;
        }
    }
}
