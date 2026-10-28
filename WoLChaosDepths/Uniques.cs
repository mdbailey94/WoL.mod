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
    //   Embersting Charm (Charm)   Ember: the red orb darts at the nearest enemy every 1.5 s.
    //   Mossguard Charm (Charm)    Moss: every 12 s a ward halves the next hit you take.
    //   Voidmote Charm (Charm)     Void: enemies near you are slowed.
    // (The last three, 0.16.0, wear WoL Art's red, green and violet Legendary orbs.)
    // Their looks are PowerFx's (pixel-drawn); their pictures are ItemArt's.
    public static class Uniques
    {
        public class Def
        {
            public string key, name, slot;
            public Func<string> power;
        }

        private static ConfigEntry<float> uniqueChance, freshUniqueChance;
        private static ConfigEntry<float> chainShare, chainRange, pyreShare, pyreRadius, leechShare;
        private static ConfigEntry<float> echoShare, galeSpeed, galeSeconds, thornDamage, thornRadius, thornCooldown;
        private static ConfigEntry<int> echoEvery;
        private static ConfigEntry<float> emberEvery, emberRange, emberDamage, emberPerLevel;
        private static ConfigEntry<float> mossEvery, mossBlock, voidRadius, voidSlow;

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
            freshUniqueChance = config.Bind("Uniques", "FreshUniqueChance", 0.3f,
                new ConfigDescription("UniqueChance in the profile's fresh save (WoL Player's balanced rates, #31); UniqueChance is used in the Debugging save.", new AcceptableValueRange<float>(0f, 1f)));
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
            emberEvery = config.Bind("Uniques", "EmberstingEvery", 1.5f, "Embersting Charm: seconds between darts.");
            emberRange = config.Bind("Uniques", "EmberstingRange", 5f, "Embersting Charm: how far the orb reaches for an enemy.");
            emberDamage = config.Bind("Uniques", "EmberstingDamage", 8f, "Embersting Charm: damage of a dart, before EmberstingPerLevel.");
            emberPerLevel = config.Bind("Uniques", "EmberstingPerLevel", 1f, "Embersting Charm: extra dart damage per character level.");
            mossEvery = config.Bind("Uniques", "MossguardEvery", 12f, "Mossguard Charm: seconds for the ward to form again after it breaks.");
            mossBlock = config.Bind("Uniques", "MossguardBlock", 0.5f,
                new ConfigDescription("Mossguard Charm: how much of the next hit the ward takes (0.5 = half).", new AcceptableValueRange<float>(0f, 1f)));
            voidRadius = config.Bind("Uniques", "VoidmoteRadius", 3f, "Voidmote Charm: enemies this close to you are slowed.");
            voidSlow = config.Bind("Uniques", "VoidmoteSlow", 0.25f,
                new ConfigDescription("Voidmote Charm: how much slower they move (0.25 = 25%; bosses half as much).", new AcceptableValueRange<float>(0f, 0.9f)));

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
            Defs.Add(new Def { key = "Ember", name = "Embersting Charm", slot = "Charm",
                power = () => $"Unique: every {emberEvery.Value:0.#} s the orb darts at an enemy for {emberDamage.Value:0} + {emberPerLevel.Value:0.#} per level" });
            Defs.Add(new Def { key = "Moss", name = "Mossguard Charm", slot = "Charm",
                power = () => $"Unique: every {mossEvery.Value:0} s a ward takes {Mathf.RoundToInt(mossBlock.Value * 100f)}% of the next hit" });
            Defs.Add(new Def { key = "Void", name = "Voidmote Charm", slot = "Charm",
                power = () => $"Unique: enemies within {voidRadius.Value:0.#} are slowed {Mathf.RoundToInt(voidSlow.Value * 100f)}%" });
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
            if (Defs.Count == 0 || UnityEngine.Random.value >= (ProfileSave.Fresh ? freshUniqueChance.Value : uniqueChance.Value))
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

        // The wizard took damage: the Mossguard's ward breaks, the Thornwick lashes out.
        public static void OnPlayerHurt(Player player, CharacterSave save, AttackInfo attack)
        {
            if (applying || player == null || attack == null)
                return;
            if (attack.damage > 0 && WardUp(player))
                BreakWard(player);
            if (!Active(save, "Thorn"))
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

        // ---- The charms that act on their own (0.16.0): every frame in the trials ----

        private const string WardMod = "ChaosDepths_Mossguard", VoidMod = "ChaosDepths_Voidmote";
        private static readonly Dictionary<int, float> emberNext = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> wardReadyAt = new Dictionary<int, float>();
        private static readonly Dictionary<Entity, float> slowed = new Dictionary<Entity, float>();
        private static float nextVoidCheck, nextVoidMote;

        public static void Tick(IEnumerable<Player> players, bool inTrials)
        {
            try
            {
                var voidPlayers = new List<Player>();
                foreach (Player player in players)
                {
                    if (player == null || ChaosDepthsPlugin.Instance == null)
                        continue;
                    CharacterSave save = ChaosDepthsPlugin.Instance.SaveOf(player);
                    Ember(player, save, inTrials);
                    Ward(player, save, inTrials);
                    if (inTrials && Active(save, "Void"))
                        voidPlayers.Add(player);
                }
                Voidmote(voidPlayers);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"unique charms: {e.Message}");
            }
        }

        // Embersting: the orb darts at the nearest enemy in reach, every EmberstingEvery seconds.
        private static void Ember(Player player, CharacterSave save, bool inTrials)
        {
            int id = player.GetInstanceID();
            if (!inTrials || !Active(save, "Ember"))
            {
                emberNext.Remove(id);
                return;
            }
            float next;
            if (emberNext.TryGetValue(id, out next) && Time.time < next)
                return;
            Vector3 from = player.transform.position + new Vector3(0f, 0.5f, 0f);
            Entity target = Nearest(player.transform.position, emberRange.Value, null);
            if (target == null)
            {
                emberNext[id] = Time.time + 0.2f;   // look again soon
                return;
            }
            emberNext[id] = Time.time + Mathf.Max(0.3f, emberEvery.Value);
            int damage = Mathf.Max(1, Mathf.RoundToInt(emberDamage.Value + emberPerLevel.Value * Mathf.Max(1, save.level)));
            Vector3 to = target.transform.position + new Vector3(0f, 0.5f, 0f);
            PowerFx.Arc(from, to, ColourOf("Ember"), player);
            PowerFx.Burst(to, 0.6f, ColourOf("Ember"), player);
            Hit(player, target, damage, ElementType.Fire);
            if (ChaosDepthsPlugin.DebugEnabled)
                ChaosDepthsPlugin.Log($"Embersting: dart at {target.GetType().Name} for {damage}");
        }

        // Mossguard: a damage-taken mod while the ward is up, so the next hit is cut before it
        // lands (a killing blow included); the hit breaks it (OnPlayerHurt), and it forms again
        // MossguardEvery seconds later.
        public static bool WardUp(Player player)
        {
            try
            {
                return player != null && player.health != null && player.health.damageTakenStat != null
                    && player.health.damageTakenStat.GetModIndex(WardMod) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static void Ward(Player player, CharacterSave save, bool inTrials)
        {
            int id = player.GetInstanceID();
            bool up = WardUp(player);
            if (!Active(save, "Moss"))
            {
                if (up)
                    player.health.damageTakenStat.RemoveMod(WardMod);
                wardReadyAt.Remove(id);
                return;
            }
            if (up || player.health == null || player.health.damageTakenStat == null)
                return;
            float ready;
            if (!wardReadyAt.TryGetValue(id, out ready))
                wardReadyAt[id] = ready = Time.time;   // just put on: ready straight away
            if (Time.time < ready)
                return;
            player.health.damageTakenStat.AddMod(new NumVarStatMod(WardMod, -Mathf.Clamp01(mossBlock.Value), 10, VarStatModType.Multiplicative, false));
            if (inTrials)
                PowerFx.Echo(player.transform.position, ColourOf("Moss"), player);
            if (ChaosDepthsPlugin.DebugEnabled)
                ChaosDepthsPlugin.Log("Mossguard: ward up");
        }

        private static void BreakWard(Player player)
        {
            try
            {
                player.health.damageTakenStat.RemoveMod(WardMod);
                wardReadyAt[player.GetInstanceID()] = Time.time + Mathf.Max(1f, mossEvery.Value);
                PowerFx.Burst(player.transform.position + new Vector3(0f, 0.4f, 0f), 1f, ColourOf("Moss"), player);
                if (ChaosDepthsPlugin.DebugEnabled)
                    ChaosDepthsPlugin.Log($"Mossguard: ward broke, back in {mossEvery.Value:0} s");
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Mossguard: {e.Message}");
            }
        }

        // Voidmote: enemies within VoidmoteRadius of a wearer move slower (bosses half as much);
        // checked five times a second, the slow comes off as soon as they're out of reach.
        private static void Voidmote(List<Player> wearers)
        {
            if (Time.time < nextVoidCheck)
                return;
            nextVoidCheck = Time.time + 0.2f;
            var near = new HashSet<Entity>();
            foreach (Player p in wearers)
                foreach (Entity e in EnemiesNear(p.transform.position, voidRadius.Value, null))
                    near.Add(e);
            foreach (Entity e in new List<Entity>(slowed.Keys))
                if (e == null || !near.Contains(e))
                    Unslow(e);
            bool mote = Time.time >= nextVoidMote;
            if (mote)
                nextVoidMote = Time.time + 0.8f;
            foreach (Entity e in near)
            {
                if (e.movement == null || e.movement.moveSpeedStat == null)
                    continue;
                if (!slowed.ContainsKey(e))
                {
                    float slow = Mathf.Clamp(voidSlow.Value, 0f, 0.9f) * (e is Boss || e is MiniBoss ? 0.5f : 1f);
                    e.movement.moveSpeedStat.AddMod(new NumVarStatMod(VoidMod, -slow, 10, VarStatModType.Multiplicative, false));
                    slowed[e] = slow;
                }
                if (mote && wearers.Count > 0)
                    PowerFx.Echo(e.transform.position + new Vector3(0f, 0.6f, 0f), ColourOf("Void"), wearers[0]);
            }
        }

        private static void Unslow(Entity e)
        {
            slowed.Remove(e);
            try
            {
                if (e != null && e.movement != null && e.movement.moveSpeedStat != null)
                    e.movement.moveSpeedStat.RemoveMod(VoidMod);
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
            emberNext.Clear();
            foreach (Entity e in new List<Entity>(slowed.Keys))
                Unslow(e);
            slowed.Clear();
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
