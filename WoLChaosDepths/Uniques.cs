using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLChaosDepths
{
    // M3, second slice: unique legendaries. A Legendary drop can be a unique: a named item with
    // its usual affixes plus a power that changes how your attacks behave while it is equipped.
    //   Stormlinked Signet (Ring)  Chain: each hit arcs part of its damage to the nearest enemy.
    //   Cinderheart (Amulet)       Pyre: enemies you kill burst, hurting enemies around them.
    //   Bloodwell Charm (Charm)    Leech: kills restore a little health.
    public static class Uniques
    {
        public class Def
        {
            public string key, name, slot;
            public Func<string> power;
        }

        private static ConfigEntry<float> uniqueChance, chainShare, chainRange, pyreShare, pyreRadius, leechShare;

        public static readonly List<Def> Defs = new List<Def>();

        // True while a power deals its own damage, so that damage doesn't trigger powers again.
        private static bool applying;

        public static void Bind(ConfigFile config)
        {
            uniqueChance = config.Bind("Uniques", "UniqueChance", 0.5f,
                new ConfigDescription("Chance a Legendary drop is a unique legendary with a power.", new AcceptableValueRange<float>(0f, 1f)));
            chainShare = config.Bind("Uniques", "ChainShare", 0.35f, "Stormlinked Signet: share of each hit's damage that arcs to the nearest enemy.");
            chainRange = config.Bind("Uniques", "ChainRange", 6f, "Stormlinked Signet: how far the arc reaches.");
            pyreShare = config.Bind("Uniques", "PyreShare", 0.4f, "Cinderheart: share of a slain enemy's max health dealt to enemies around it.");
            pyreRadius = config.Bind("Uniques", "PyreRadius", 3.5f, "Cinderheart: burst radius.");
            leechShare = config.Bind("Uniques", "LeechShare", 0.02f, "Bloodwell Charm: share of your max health restored per kill.");

            Defs.Clear();
            Defs.Add(new Def { key = "Chain", name = "Stormlinked Signet", slot = "Ring",
                power = () => $"Unique: hits arc {Mathf.RoundToInt(chainShare.Value * 100f)}% damage to a nearby enemy" });
            Defs.Add(new Def { key = "Pyre", name = "Cinderheart", slot = "Amulet",
                power = () => $"Unique: slain enemies burst for {Mathf.RoundToInt(pyreShare.Value * 100f)}% of their health" });
            Defs.Add(new Def { key = "Leech", name = "Bloodwell Charm", slot = "Charm",
                power = () => $"Unique: kills restore {Mathf.RoundToInt(leechShare.Value * 1000f) / 10f}% health" });
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

        // ---- Powers ----

        public static void OnPlayerHit(Player player, CharacterSave save, AttackInfo attack, Entity hurt)
        {
            if (applying || attack == null || attack.damage <= 0 || !Active(save, "Chain"))
                return;
            Entity target = Nearest(hurt.transform.position, chainRange.Value, hurt);
            if (target == null)
                return;
            int damage = Mathf.Max(1, Mathf.RoundToInt(attack.damage * chainShare.Value));
            Hit(player, target, damage, attack.elementType);
            Flash.Line(hurt.transform.position, target.transform.position, new Color(0.55f, 0.9f, 1f), player);
        }

        public static void OnPlayerKill(Player player, CharacterSave save, Entity slain)
        {
            if (applying)
                return;
            if (Active(save, "Pyre"))
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
                Flash.Ring(at, pyreRadius.Value, new Color(1f, 0.55f, 0.2f), player);
                if (burned > 0 && ChaosDepthsPlugin.DebugEnabled)
                    ChaosDepthsPlugin.Log($"Cinderheart: burst hit {burned} enemies for {damage}");
            }
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
            }
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

        // A short-lived line or ring, drawn over the floor, so a power is visible.
        private class Flash : MonoBehaviour
        {
            private LineRenderer line;
            private float life, age;
            private Color color;

            public static void Line(Vector3 a, Vector3 b, Color color, Player near)
            {
                Make(new[] { a, b }, color, 0.18f, 0.15f, near);
            }

            public static void Ring(Vector3 at, float radius, Color color, Player near)
            {
                const int Segments = 24;
                var points = new Vector3[Segments + 1];
                for (int i = 0; i <= Segments; i++)
                {
                    float t = i * Mathf.PI * 2f / Segments;
                    points[i] = at + new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f);
                }
                Make(points, color, 0.25f, 0.12f, near);
            }

            private static void Make(Vector3[] points, Color color, float life, float width, Player near)
            {
                try
                {
                    var go = new GameObject("ChaosDepthsUniqueFlash");
                    var flash = go.AddComponent<Flash>();
                    flash.life = life;
                    flash.color = color;
                    flash.line = go.AddComponent<LineRenderer>();
                    flash.line.material = new Material(Shader.Find("Sprites/Default"));
                    flash.line.positionCount = points.Length;
                    flash.line.SetPositions(points);
                    flash.line.startWidth = width;
                    flash.line.endWidth = width;
                    flash.line.startColor = color;
                    flash.line.endColor = color;
                    SpriteRenderer sr = near != null ? near.spriteRenderer : null;
                    if (sr != null)
                    {
                        flash.line.sortingLayerID = sr.sortingLayerID;
                        flash.line.sortingOrder = sr.sortingOrder + 50;
                    }
                }
                catch
                {
                }
            }

            private void Update()
            {
                age += Time.deltaTime;
                Color c = color;
                c.a = Mathf.Clamp01(1f - age / life);
                line.startColor = c;
                line.endColor = c;
                if (age >= life)
                    Destroy(gameObject);
            }
        }
    }
}
