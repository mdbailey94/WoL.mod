using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLChaosDepths
{
    // Unique legendaries. A Legendary drop can be a unique: a named item with its usual affixes
    // plus a power that changes how you fight while it is equipped. Two per slot:
    //   Stormlinked Signet (Ring)  Chain: each hit arcs part of its damage to the nearest enemy.
    //   Twinfold Band (Ring)       Echo: every fourth hit on an enemy strikes it again.
    //   Cinderheart (Amulet)       Pyre: enemies you kill burst, hurting enemies around them.
    //   Galeheart (Amulet)         Gale: a kill puts the wind at your back for a few seconds.
    //   Bloodwell Charm (Charm)    Leech: kills restore a little health.
    //   Thornwick Charm (Charm)    Thorn: being hit lashes out at enemies round you.
    // Their looks are PowerFx's (pixel-drawn); their pictures are ItemArt's.
    public static class Uniques
    {
        public class Def
        {
            public string key, name, slot;
            public Func<string> power;
        }

        private static ConfigEntry<float> uniqueChance;
        private static ConfigEntry<float> chainShare, chainRange, pyreShare, pyreRadius, leechShare;
        private static ConfigEntry<float> echoShare, galeSpeed, galeSeconds, thornDamage, thornRadius, thornCooldown;
        private static ConfigEntry<int> echoEvery;

        public static readonly List<Def> Defs = new List<Def>();

        // True while a power deals its own damage, so that damage doesn't trigger powers again.
        private static bool applying;
        // Per player: hits towards the next echo, when the gale ends, when thorns may lash again.
        private static readonly Dictionary<int, int> echoHits = new Dictionary<int, int>();
        private static readonly Dictionary<int, float> galeUntil = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> thornReady = new Dictionary<int, float>();
        private const string GaleMod = "ChaosDepths_Gale";

        public static void Bind(ConfigFile config)
        {
            uniqueChance = config.Bind("Uniques", "UniqueChance", 0.5f,
                new ConfigDescription("Chance a Legendary drop is a unique legendary with a power.", new AcceptableValueRange<float>(0f, 1f)));
            chainShare = config.Bind("Uniques", "ChainShare", 0.35f, "Stormlinked Signet: share of each hit's damage that arcs to the nearest enemy.");
            chainRange = config.Bind("Uniques", "ChainRange", 6f, "Stormlinked Signet: how far the arc reaches.");
            echoEvery = config.Bind("Uniques", "EchoEvery", 4, "Twinfold Band: every this many hits on an enemy, it is struck again.");
            echoShare = config.Bind("Uniques", "EchoShare", 0.6f, "Twinfold Band: the echo's damage, as a share of the hit.");
            pyreShare = config.Bind("Uniques", "PyreShare", 0.4f, "Cinderheart: share of a slain enemy's max health dealt to enemies around it.");
            pyreRadius = config.Bind("Uniques", "PyreRadius", 3.5f, "Cinderheart: burst radius.");
            galeSpeed = config.Bind("Uniques", "GaleSpeed", 0.3f, "Galeheart: move speed after a kill (0.3 = +30%).");
            galeSeconds = config.Bind("Uniques", "GaleSeconds", 3f, "Galeheart: how long the wind lasts (a kill restarts it).");
            leechShare = config.Bind("Uniques", "LeechShare", 0.02f, "Bloodwell Charm: share of your max health restored per kill.");
            thornDamage = config.Bind("Uniques", "ThornDamage", 0.5f, "Thornwick Charm: damage of the lash, as a share of the damage you took (at least 3).");
            thornRadius = config.Bind("Uniques", "ThornRadius", 2.5f, "Thornwick Charm: how far the lash reaches.");
            thornCooldown = config.Bind("Uniques", "ThornCooldown", 1f, "Thornwick Charm: seconds between lashes.");

            Defs.Clear();
            Defs.Add(new Def { key = "Chain", name = "Stormlinked Signet", slot = "Ring",
                power = () => $"Unique: hits arc {Mathf.RoundToInt(chainShare.Value * 100f)}% damage to a nearby enemy" });
            Defs.Add(new Def { key = "Echo", name = "Twinfold Band", slot = "Ring",
                power = () => $"Unique: every {echoEvery.Value}th hit on an enemy strikes again for {Mathf.RoundToInt(echoShare.Value * 100f)}%" });
            Defs.Add(new Def { key = "Pyre", name = "Cinderheart", slot = "Amulet",
                power = () => $"Unique: slain enemies burst for {Mathf.RoundToInt(pyreShare.Value * 100f)}% of their health" });
            Defs.Add(new Def { key = "Gale", name = "Galeheart", slot = "Amulet",
                power = () => $"Unique: kills give +{Mathf.RoundToInt(galeSpeed.Value * 100f)}% move speed for {galeSeconds.Value:0.#} s" });
            Defs.Add(new Def { key = "Leech", name = "Bloodwell Charm", slot = "Charm",
                power = () => $"Unique: kills restore {Mathf.RoundToInt(leechShare.Value * 1000f) / 10f}% health" });
            Defs.Add(new Def { key = "Thorn", name = "Thornwick Charm", slot = "Charm",
                power = () => $"Unique: being hit lashes enemies within {thornRadius.Value:0.#} for {Mathf.RoundToInt(thornDamage.Value * 100f)}% of the damage" });
        }

        public static Def Find(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            foreach (Def def in Defs)
                if (def.key == key)
                    return def;
            return null;
        }

        // Called on every new Legendary: maybe turn it into a unique (its slot follows the unique).
        public static void MaybeMakeUnique(ItemData item)
        {
            if (Defs.Count == 0 || UnityEngine.Random.value >= uniqueChance.Value)
                return;
            MakeUnique(item, Defs[UnityEngine.Random.Range(0, Defs.Count)]);
        }

        public static void MakeUnique(ItemData item, Def def)
        {
            item.unique = def.key;
            item.slot = def.slot;
            item.name = def.name;
        }

        public static string PowerText(ItemData item)
        {
            Def def = Find(item?.unique);
            return def != null ? def.power() : null;
        }

        public static bool Active(CharacterSave save, string key)
        {
            if (save == null || save.equipped == null)
                return false;
            foreach (string id in save.equipped)
            {
                if (string.IsNullOrEmpty(id))
                    continue;
                ItemData item = StatMods.FindItem(save, id);
                if (item != null && item.unique == key)
                    return true;
            }
            return false;
        }

        private static Color ColourOf(string key)
        {
            return ItemArt.Stone(new ItemData { unique = key, slot = "Ring", rarity = "Legendary" });
        }

        // ---- Powers ----

        public static void OnPlayerHit(Player player, CharacterSave save, AttackInfo attack, Entity hurt)
        {
            if (applying || attack == null || attack.damage <= 0 || hurt == null)
                return;
            if (Active(save, "Chain"))
            {
                Entity target = Nearest(hurt.transform.position, chainRange.Value, hurt);
                if (target != null)
                {
                    int damage = Mathf.Max(1, Mathf.RoundToInt(attack.damage * chainShare.Value));
                    Hit(player, target, damage, attack.elementType);
                    PowerFx.Arc(hurt.transform.position + Vector3.up * 0.5f, target.transform.position + Vector3.up * 0.5f, ColourOf("Chain"), player);
                }
            }
            if (Active(save, "Echo"))
            {
                int id = player.GetInstanceID();
                int hits;
                echoHits.TryGetValue(id, out hits);
                hits++;
                if (hits >= Mathf.Max(2, echoEvery.Value))
                {
                    hits = 0;
                    int damage = Mathf.Max(1, Mathf.RoundToInt(attack.damage * echoShare.Value));
                    Hit(player, hurt, damage, attack.elementType);
                    PowerFx.Echo(hurt.transform.position, ColourOf("Echo"), player);
                    if (ChaosDepthsPlugin.DebugEnabled)
                        ChaosDepthsPlugin.Log($"Twinfold: echo on {hurt.GetType().Name} for {damage}");
                }
                echoHits[id] = hits;
            }
        }

        public static void OnPlayerKill(Player player, CharacterSave save, Entity slain)
        {
            if (applying || player == null)
                return;
            if (Active(save, "Pyre") && slain != null)
            {
                float max = slain.health != null && slain.health.healthStat != null ? slain.health.healthStat.ModifiedValue : 0f;
                int damage = Mathf.Max(1, Mathf.RoundToInt(max * pyreShare.Value));
                Vector3 at = slain.transform.position;
                int burned = 0;
                foreach (Entity enemy in EnemiesNear(at, pyreRadius.Value, slain))
                {
                    Hit(player, enemy, damage, ElementType.Fire);
                    burned++;
                }
                PowerFx.Burst(at, pyreRadius.Value, ColourOf("Pyre"), player);
                if (burned > 0 && ChaosDepthsPlugin.DebugEnabled)
                    ChaosDepthsPlugin.Log($"Cinderheart: burst hit {burned} enemies for {damage}");
            }
            if (Active(save, "Gale"))
                Gale(player);
            if (Active(save, "Leech") && player.health != null && player.health.healthStat != null)
            {
                int amount = Mathf.Max(1, Mathf.RoundToInt(player.health.healthStat.ModifiedValue * leechShare.Value));
                try
                {
                    player.health.RestoreHealth(amount, true, false);
                }
                catch
                {
                }
                if (slain != null)
                    PowerFx.Motes(slain.transform.position + Vector3.up * 0.4f, player, ColourOf("Leech"), 3);
            }
        }

        // The wizard took damage: the Thornwick lashes out.
        public static void OnPlayerHurt(Player player, CharacterSave save, AttackInfo attack)
        {
            if (applying || player == null || attack == null || !Active(save, "Thorn"))
                return;
            int id = player.GetInstanceID();
            float ready;
            if (thornReady.TryGetValue(id, out ready) && Time.time < ready)
                return;
            thornReady[id] = Time.time + thornCooldown.Value;
            int damage = Mathf.Max(3, Mathf.RoundToInt(attack.damage * thornDamage.Value));
            int lashed = 0;
            foreach (Entity enemy in EnemiesNear(player.transform.position, thornRadius.Value, null))
            {
                Hit(player, enemy, damage, ElementType.Plant);
                lashed++;
            }
            PowerFx.Thorns(player, thornRadius.Value, ColourOf("Thorn"));
            if (lashed > 0 && ChaosDepthsPlugin.DebugEnabled)
                ChaosDepthsPlugin.Log($"Thornwick: lashed {lashed} enemies for {damage}");
        }

        // Galeheart: a move speed mod for a few seconds, restarted by each kill.
        private static void Gale(Player player)
        {
            try
            {
                if (player.movement == null || player.movement.moveSpeedStat == null)
                    return;
                int id = player.GetInstanceID();
                bool fresh = !galeUntil.ContainsKey(id) || Time.time >= galeUntil[id];
                galeUntil[id] = Time.time + galeSeconds.Value;
                if (player.movement.moveSpeedStat.GetModIndex(GaleMod) < 0)
                    player.movement.moveSpeedStat.AddMod(new NumVarStatMod(GaleMod, galeSpeed.Value, 10, VarStatModType.Multiplicative, false));
                Vector2 dir = player.movement.moveVector;
                PowerFx.Gust(player, dir, ColourOf("Gale"));
                if (ChaosDepthsPlugin.DebugEnabled)
                    ChaosDepthsPlugin.Log($"Galeheart: +{galeSpeed.Value:P0} speed for {galeSeconds.Value:0.#} s{(fresh ? "" : " (restarted)")}");
                if (fresh && ChaosDepthsPlugin.Instance != null)
                    ChaosDepthsPlugin.Instance.StartCoroutine(GaleEnds(player, id));
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Galeheart: {e.Message}");
            }
        }

        private static IEnumerator GaleEnds(Player player, int id)
        {
            // While the wind lasts, streaks whip off the wizard whenever they move.
            float nextStreak = 0f;
            while (galeUntil.ContainsKey(id) && Time.time < galeUntil[id])
            {
                try
                {
                    if (player != null && player.movement != null && Time.time >= nextStreak && player.movement.moveVector.sqrMagnitude > 0.1f)
                    {
                        nextStreak = Time.time + 0.12f;
                        PowerFx.Gust(player, player.movement.moveVector, ColourOf("Gale"));
                    }
                }
                catch
                {
                }
                yield return null;
            }
            galeUntil.Remove(id);
            try
            {
                if (player != null && player.movement != null && player.movement.moveSpeedStat != null)
                    player.movement.moveSpeedStat.RemoveMod(GaleMod);
            }
            catch
            {
            }
        }

        // A scene change: nothing lingers.
        public static void ClearScene()
        {
            echoHits.Clear();
            galeUntil.Clear();
            thornReady.Clear();
        }

        private static void Hit(Player player, Entity target, int damage, ElementType element)
        {
            if (target == null || target.health == null || target.health.CurrentHealthValue <= 0)
                return;
            applying = true;
            try
            {
                var info = new AttackInfo
                {
                    damage = damage,
                    entity = player,
                    skillCategory = player != null ? player.skillCategory : "",
                    elementType = element,
                    showDamageNumber = true,
                    showDamageEffect = true,
                    attackerPosition = player != null ? (Vector2)player.transform.position : Vector2.zero,
                    targetPosition = target.transform.position
                };
                target.health.TakeDamage(info, player);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"unique power hit: {e.Message}");
            }
            finally
            {
                applying = false;
            }
        }

        private static IEnumerable<Entity> EnemiesNear(Vector3 at, float radius, Entity except)
        {
            var found = new List<Entity>();
            if (GameController.enemies == null)
                return found;
            foreach (GameObject go in new List<GameObject>(GameController.enemies))
            {
                if (go == null || !go.activeInHierarchy)
                    continue;
                Entity e = go.GetComponent<Entity>();
                if (e == null || e == except || e.health == null || e.health.CurrentHealthValue <= 0)
                    continue;
                if (Vector2.Distance(at, go.transform.position) <= radius)
                    found.Add(e);
            }
            return found;
        }

        private static Entity Nearest(Vector3 at, float radius, Entity except)
        {
            Entity best = null;
            float bestDist = radius;
            foreach (Entity e in EnemiesNear(at, radius, except))
            {
                float d = Vector2.Distance(at, e.transform.position);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = e;
                }
            }
            return best;
        }
    }
}
