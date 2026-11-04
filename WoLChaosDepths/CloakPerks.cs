using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    // Each cloak's own two perks, on top of its talent tree (Matt, 2026-11-02: "each cloak should
    // have 1 elemental perk and one co-op perk"; drafts Log - Player 20:00, approved 20:20 with his
    // Grit and Fall edits). Always on while the cloak is worn and talents are on (the Mod Menu's
    // Phase One switch); no points. They use timing stats (cast speed, durations, cooldown ticks)
    // rather than relic abilities. Each logs "Cloak perk: ..." the first time it acts on a floor.
    //   Vigor     Earth: casting an earth arcana plays 15% faster.  Co-op: a hit on your partner within 3 u is 30% yours.
    //   Grit      Earth: stuns you deal last 40% longer.            Co-op: a partner you revive gets up at half health.
    //   Rule      Fire: burns you apply are a level stronger.       Co-op: your partner's first hit after yours on an enemy deals +20%.
    //   Awe       Lightning: shocks a level stronger; a crit on a shocked enemy arcs to the nearest other enemy.
    //             Co-op: your crits take 0.3 s off your partner's dash cooldown.
    //   Switch    Air: after an evade, your next cast plays 30% faster.  Co-op: your evade gives your partner +10% evade for 2 s.
    //   Duelist's Cloak  Air: dash cooldown -20%.  Co-op: an enemy you both hit within 1 s takes +15% from both of you.
    //   Tempo     Water: freezes last 0.5 s longer, and your cooldowns tick 15% faster while a frozen enemy is near.
    //             Co-op: your signature cast gives your partner 15% signature charge.
    //   Fury      Fire: your signature's cast plays 30% faster.  Co-op: your partner's signature gives you 10% charge.
    //   Pace      Air: a cast keeps your momentum: +10% move speed for 2 s.  Co-op: within 4 u of each other, both +5% move speed.
    //   Venture   Lightning: a dash leaves a short static trail that shocks.  Co-op: your partner crossing it gets +15% move speed for 2 s.
    //   Savile    Water: a water arcana cast within 1 s of a dash comes back 15% sooner.  Co-op: dashing through your partner resets their dash.
    //   Greed     Earth: an earth arcana kill has a 15% chance to knock 5 gold loose.  Co-op: every 100 gold the team banks heals both 3%.
    //   Level     Chaos: every 10 kills on a floor, +1% cast speed (up to +10%), reset each floor.  Co-op: your partner's kills count half.
    //   Pink      Water: healing +25%.  Co-op: a big heal (10% of your health or more: an orb) heals your partner 30% of it.
    //   Fall      Air: you can't fall into a pit for 1 s after a dash.
    //             Co-op: once a minute, when your partner falls into a pit, a tether pulls them to your side, untargetable.
    //   Pride     Fire: signature charge builds 25% faster, but each hit you take empties 10% of it.
    //             Co-op: while you're alive, your partner deals +10%.
    //   Hope / Patience  Earth / Water: cooldowns -5%.  Co-op: within 3 u of each other, both regain 1% health every 5 s.
    // Venture's co-op, Savile's elemental, and Greed's and Level's perks are Matt's picks of
    // 2026-11-02 21:00 ("Pick one for me"), replacing drafts the game can't support as written.
    public static class CloakPerks
    {
        private const string Prefix = "ChaosDepths_CP_";
        private static readonly Dictionary<int, Health> hooked = new Dictionary<int, Health>();
        private static readonly Dictionary<KeyValuePair<object, string>, float> applied = new Dictionary<KeyValuePair<object, string>, float>();
        private static readonly HashSet<string> said = new HashSet<string>();
        private static readonly Dictionary<int, float> shockedUntil = new Dictionary<int, float>();
        private static readonly HashSet<int> statusHooked = new HashSet<int>();
        private static readonly Dictionary<int, Hit> lastHits = new Dictionary<int, Hit>();
        private static readonly Dictionary<int, State> states = new Dictionary<int, State>();
        private static bool sharing;

        private class Hit
        {
            public string category;
            public float time;
            public bool followed;
        }

        private class State
        {
            public float castFastUntil = -1f, fastCastAfterEvade = -1f, momentumUntil = -1f, evadeBuffUntil = -1f;
            public float noPitUntil = -1f, rescueReady, regenNext, dashUntil;
            public bool wasDead, prevented;
            public Vector2 dashFrom;
            public bool partnerDashReset;
            public float lastDash = -10f, trailSpeedUntil = -1f, floorKills, togetherHealed;
            public readonly List<Vector3> trail = new List<Vector3>();   // x, y, until
        }

        // Matt's picks, 2026-11-02 21:00 ("Pick one for me").
        private const float GreedGoldChance = 0.15f;
        private const int GreedGold = 5;
        private const int GreedHealEvery = 100;
        private const float SavileWindow = 1f, SavileRefund = 0.15f;
        private static readonly HashSet<int> killed = new HashSet<int>();
        private static readonly List<Player.SkillState> refunds = new List<Player.SkillState>();
        private static int lastBalance = -1, goldSinceHeal;

        public static void Install()
        {
            var host = new GameObject("ChaosDepths CloakPerks");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<Runner>();
            GameController.levelLoadEventHandlers += (next, previous) =>
            {
                said.Clear();
                killed.Clear();
                elementsOn.Clear();
                foreach (State s in states.Values)
                    s.togetherHealed = 0f;   // Together's cap is per floor
                foreach (State s in states.Values)
                    s.floorKills = 0f;   // Level's stacks are per floor
                lastHits.Clear();
                shockedUntil.Clear();
                statusHooked.Clear();
            };
            Health.globalTakeDamageEnterHandlers += BeforeAnyHit;
            Health.globalTakeDamageActualHandlers += AfterAnyHit;
            Attack.globalCritEventHandlers += OnCrit;
        }

        private class Runner : MonoBehaviour
        {
            private void Update()
            {
                if (!Talents.Enabled)
                    return;
                Guard("tick", Tick);
            }
        }

        private static string Cloak(Player p) => p != null ? Talents.CloakOf(p) : "";

        // The perks are talent nodes in the cloak's tree (Matt, 2026-11-02 20:45: "they should be
        // part of the talent trees, rename the perk so they aren't confused with the cloaks"):
        // the elemental perk in tier 1 and the co-op perk in tier 2, a point each, named apart
        // from the cloaks. A perk acts while it's learned in the worn cloak's tree.
        private class Perk
        {
            public string Key, Name, Effect;
        }

        // cloak -> { elemental, co-op }
        private static readonly Dictionary<string, Perk[]> perks = new Dictionary<string, Perk[]>
        {
            { "Vigor", P("EarthenHaste", "Earthen Haste", "Earth arcana cast 15% faster",
                "ShoulderTheBlow", "Shoulder the Blow", "Co-op: you take 30% of a hit on your partner within 3 u") },
            { "Grit", P("Concussive", "Concussive", "Stuns you deal last 40% longer",
                "Rally", "Rally", "Co-op: a partner you revive gets up at half health") },
            { "Rule", P("Kindled", "Kindled", "Burns you apply are a level stronger",
                "FollowThrough", "Follow Through", "Co-op: your partner's first hit after yours on an enemy deals +20%") },
            { "Awe", P("ArcConductor", "Arc Conductor", "Shocks a level stronger; a crit on a shocked enemy arcs to the next",
                "QuickSpark", "Quick Spark", "Co-op: your crits take 0.3 s off your partner's dash cooldown") },
            { "Switch", P("GaleReflex", "Gale Reflex", "After an evade, your next cast is 30% faster",
                "SharedFooting", "Shared Footing", "Co-op: your evade gives your partner +10% evade for 2 s") },
            // Matt, 2026-11-03: crit chance over 100% turning into crit damage is a cloak perk, not
            // for everyone; it replaced Swift Edge (dash cooldown -20%). ArchetypeMechanics applies it.
            { "DuelistCloak", P("Overedge", "Overedge", "Crit chance over 100% becomes crit damage (+1% for each 1% over)",
                "Pincer", "Pincer", "Co-op: an enemy you both hit within 1 s takes +15% from both") },
            { "Tempo", P("Rimeweave", "Rimeweave", "Freezes last 0.5 s longer; cooldowns tick 15% faster near a frozen enemy",
                "SharedSpark", "Shared Spark", "Co-op: your signature gives your partner 15% signature charge") },
            { "Fury", P("Fervor", "Fervor", "Your signature casts 30% faster",
                "KindredFury", "Kindred Fury", "Co-op: your partner's signature gives you 10% charge") },
            { "Pace", P("CarryThrough", "Carry Through", "+10% move speed for 2 s after a cast",
                "InStep", "In Step", "Co-op: within 4 u of each other, both +5% move speed") },
            { "Venture", P("StaticWake", "Static Wake", "Your dash leaves a short static trail that shocks",
                "Tailcurrent", "Tailcurrent", "Co-op: your partner crossing your static trail gets +15% move speed for 2 s") },
            { "Savile", P("TidalStep", "Tidal Step", "A water arcana cast within 1 s of a dash comes back 15% sooner",
                "Relay", "Relay", "Co-op: dashing through your partner resets their dash") },
            // Matt, 2026-11-02 21:00: Soothing Waters (+25% healing) became Confluence.
            { "Pink", P("Confluence", "Confluence", "Each different element you hit an enemy with: +15% chance it heals you 3% when it dies",
                "SharedDraught", "Shared Draught", "Co-op: a big heal (an orb) also heals your partner 30% of it") },
            { "Greed", P("Prospector", "Prospector", "Earth arcana kills have a 15% chance to bank 5 gold",
                "Dividend", "Dividend", "Co-op: every 100 gold the team banks heals you both 3%") },
            { "Level", P("Bloodrush", "Bloodrush", "+1% cast speed per 10 kills on a floor (up to +10%)",
                "SharedGlory", "Shared Glory", "Co-op: your partner's kills count half toward your Bloodrush") },
            { "Fall", P("SureFooting", "Sure Footing", "You can't fall into a pit for 1 s after a dash",
                "Lifeline", "Lifeline", "Co-op: once a minute, a falling partner is tethered to your side, untargetable") },
            { "Pride", P("Brinkmanship", "Brinkmanship", "Signature charge +25%, but each hit you take empties 10% of it",
                "Inspire", "Inspire", "Co-op: your partner deals +10% while within 5 u of you") },
        };

        // The starter cloaks (Hope, Patience, the Normal cloak) share one pair.
        private static readonly Perk[] starter = P("Fundamentals", "Fundamentals", "Cooldowns -5%",
            "Together", "Together", "Co-op: within 3 u of each other, both regain 1% health every 5 s while below 20% health");

        // Matt's numbers, 2026-11-02 21:00.
        private const float InspireRange = 5f;
        private const float TogetherCap = 0.20f;
        private const float ConfluenceChance = 0.15f, ConfluenceHeal = 0.03f;
        // Confluence: the elements each wizard has hit each enemy with (by enemy, then player).
        private static readonly Dictionary<int, Dictionary<int, HashSet<ElementType>>> elementsOn = new Dictionary<int, Dictionary<int, HashSet<ElementType>>>();

        private static Perk[] P(string ek, string en, string ee, string ck, string cn, string ce) =>
            new[] { new Perk { Key = ek, Name = en, Effect = ee }, new Perk { Key = ck, Name = cn, Effect = ce } };

        private static Perk[] PairOf(string cloak)
        {
            Perk[] pair;
            if (cloak != null && perks.TryGetValue(cloak, out pair))
                return pair;
            return cloak == "Hope" || cloak == "Patience" || cloak == Outfit.normalID ? starter : null;
        }

        private static readonly Dictionary<string, Talents.Node[]> nodesOf = new Dictionary<string, Talents.Node[]>();

        // The perk nodes Talents.TreeFor adds to a cloak's tree (none for a cloak without perks).
        public static Talents.Node[] NodesFor(string cloak)
        {
            if (cloak == null)
                return null;
            Talents.Node[] nodes;
            if (nodesOf.TryGetValue(cloak, out nodes))
                return nodes;
            Perk[] pair = PairOf(cloak);
            nodes = pair == null ? null : new[]
            {
                new Talents.Node { Key = pair[0].Key, Name = pair[0].Name, Tier = 1, MaxPoints = 1, Effect = pair[0].Effect },
                new Talents.Node { Key = pair[1].Key, Name = pair[1].Name, Tier = 2, MaxPoints = 1, Effect = pair[1].Effect },
            };
            nodesOf[cloak] = nodes;
            return nodes;
        }

        // Learned: the elemental (E) or co-op (C) perk of the cloak this wizard wears, if it's that cloak.
        private static bool E(Player p, string cloak) => Learned(p, cloak, 0);
        private static bool C(Player p, string cloak) => Learned(p, cloak, 1);

        private static bool Learned(Player p, string cloak, int which)
        {
            if (p == null)
                return false;
            Perk[] pair = PairOf(cloak);
            return pair != null && Talents.Rank(p, pair[which].Key) > 0;
        }

        private static State StateOf(Player p)
        {
            State st;
            if (!states.TryGetValue(p.playerID, out st))
                states[p.playerID] = st = new State();
            return st;
        }

        private static Player Partner(Player me)
        {
            Player[] players = GameController.activePlayers;
            if (me == null || players == null)
                return null;
            foreach (Player p in players)
                if (p != null && p != me && p.gameObject.activeInHierarchy && p.health != null)
                    return p;
            return null;
        }

        private static Player PlayerOf(string category)
        {
            Player[] players = GameController.activePlayers;
            if (players == null || string.IsNullOrEmpty(category))
                return null;
            foreach (Player p in players)
                if (p != null && p.skillCategory == category)
                    return p;
            return null;
        }

        private static bool Alive(Player p) => p != null && p.health != null && p.health.CurrentHealthValue > 0;

        // ---- Every frame ----

        private static void Tick()
        {
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            foreach (Player.SkillState s in refunds)
                if (s != null && s.cooldownRef != null && !s.cooldownRef.Ready)
                    s.cooldownRef.ReduceRemainingTime(SavileRefund * s.cooldownRef.MaxTime);
            refunds.Clear();
            GreedHeal(players);
            foreach (Player p in players)
            {
                if (p == null || p.health == null || p.movement == null)
                    continue;
                Hook(p);
                State st = StateOf(p);
                string cloak = Cloak(p);
                Player partner = Partner(p);
                SkillStats(p, cloak);

                // Cast speed (Vigor's earth casts, Switch after an evade, Fury's signature), and
                // Level's stacks: +1% for every 10 kills on this floor, up to +10%.
                bool fast = Time.time < st.castFastUntil;
                SetMod(p.animSpeedHandler, "CastSpeed", fast ? st.dashFrom.x : 0f);
                int stacks = E(p, "Level") ? Mathf.Min(10, Mathf.FloorToInt(st.floorKills / 10f)) : 0;
                SetMod(p.animSpeedHandler, "LevelSpeed", 0.01f * stacks);
                if (stacks > 0)
                    Say(p, "Level", "Level: kills on this floor speed up casting");

                // Venture's co-op: crossing your partner's static trail.
                if (C(partner, "Venture"))
                {
                    State ps = StateOf(partner);
                    ps.trail.RemoveAll(v => Time.time >= v.z);
                    foreach (Vector3 v in ps.trail)
                        if (((Vector2)p.transform.position - new Vector2(v.x, v.y)).magnitude <= 0.8f)
                        {
                            st.trailSpeedUntil = Time.time + 2f;
                            Say(partner, "VentureCoop", "Venture: partner crossed the static trail, +15% speed");
                            break;
                        }
                }

                // Speed: Pace's momentum and the partners-close bonus.
                float speed = 0f;
                if (E(p, "Pace") && Time.time < st.momentumUntil)
                    speed += 0.10f;
                if (C(p, "Pace") && Close(p, partner, 4f))
                    speed += 0.05f;
                if (C(partner, "Pace") && Close(p, partner, 4f))
                    speed += 0.05f;
                if (Time.time < st.trailSpeedUntil)
                    speed += 0.15f;
                Points.Set(p, "MoveSpeed", "Perk.Speed", speed * 100f);   // #114: points

                // Switch's partner evade.
                bool evadeBuff = partner != null && Time.time < StateOf(partner).evadeBuffUntil;
                Points.Set(p, "Evade", "Perk.SwitchEvade", evadeBuff ? 10f : 0f);   // #114: points

                // Pride's partner damage, and Tempo's cooldowns near a frozen enemy.
                float partnerDamage = C(partner, "Pride") && Close(p, partner, InspireRange) ? 0.10f : 0f;
                SetSkillMods(p, StatData.damageStr, "PartnerDamage", partnerDamage, true);
                if (E(p, "Tempo") && FrozenNear(p, 6f))
                {
                    TickCooldowns(p, 0.15f * Time.deltaTime);
                    Say(p, "Tempo", "Tempo: cooldowns tick faster near a frozen enemy");
                }

                // Fall: no pits for a moment after a dash.
                bool noPit = E(p, "Fall") && Time.time < st.noPitUntil;
                if (noPit != st.prevented)
                {
                    st.prevented = noPit;
                    Guard("fall", () => p.TogglePreventFallCollider(noPit));
                }

                // Grit: a revived partner gets up at half health.
                bool dead = p.health.CurrentHealthValue <= 0;
                if (st.wasDead && !dead && C(partner, "Grit"))
                {
                    int target = Mathf.RoundToInt(p.health.healthStat.ModifiedValue * 0.5f);
                    if (p.health.CurrentHealthValue < target)
                        p.health.RestoreHealth(target - p.health.CurrentHealthValue, false, false);
                    Say(partner, "Grit", "Grit: partner revived at half health");
                }
                st.wasDead = dead;

                // Fall's rescue, Savile's dash reset, Hope / Patience regen.
                Rescue(p, partner);
                if (C(p, "Savile") && Time.time < st.dashUntil && !st.partnerDashReset && Close(p, partner, 1f))
                {
                    st.partnerDashReset = true;
                    Guard("savile", () => ResetDash(partner));
                    Say(p, "Savile", "Savile: dashed through partner, their dash reset");
                }
                if (PairOf(cloak) == starter && C(p, cloak) && Close(p, partner, 3f) && Time.time >= st.regenNext)
                {
                    st.regenNext = Time.time + 5f;
                    TogetherHeal(p);
                    TogetherHeal(partner);
                    Say(p, cloak, $"{cloak}: close together, both regain 1%");
                }
            }
        }

        // Greed's co-op: every 100 gold the team banks (one shared wallet) heals both wizards 3%,
        // while a wizard wears Greed.
        private static void GreedHeal(Player[] players)
        {
            GoldWallet wallet = Player.goldWallet;
            if (wallet == null)
                return;
            int balance = wallet.balance;
            int gained = lastBalance >= 0 ? balance - lastBalance : 0;
            lastBalance = balance;
            Player greedy = null;
            foreach (Player p in players)
                if (C(p, "Greed"))
                    greedy = p;
            if (greedy == null || Partner(greedy) == null || gained <= 0)
                return;
            goldSinceHeal += gained;
            while (goldSinceHeal >= GreedHealEvery)
            {
                goldSinceHeal -= GreedHealEvery;
                foreach (Player p in players)
                    Heal(p, 0.03f);
                Say(greedy, "GreedCoop", "Greed: 100 team gold healed both 3%");
            }
        }

        private static bool Close(Player a, Player b, float range) =>
            Alive(a) && Alive(b) && ((Vector2)a.transform.position - (Vector2)b.transform.position).magnitude <= range;

        // The always-on stat steps, on this wizard's own arcana stats.
        private static void SkillStats(Player p, string cloak)
        {
            SetSkillMods(p, StatData.stunDurStr, "Stun", E(p, "Grit") ? 0.4f : 0f, true);
            SetSkillMods(p, StatData.burnLvStr, "Burn", E(p, "Rule") ? 1f : 0f, false);
            SetSkillMods(p, StatData.shockLvStr, "Shock", E(p, "Awe") ? 1f : 0f, false);
            SetSkillMods(p, StatData.frzDurStr, "Freeze", E(p, "Tempo") ? 0.5f : 0f, false);
            SetSkillMods(p, StatData.odProgStr, "Charge", E(p, "Pride") ? 0.25f : 0f, true);
            Points.Set(p, "Cooldown", "Perk.Pair", E(p, cloak) && PairOf(cloak) == starter ? -5f : 0f);   // #114: points
            float dashCd = 0f;   // Swift Edge's old -20% dash cooldown, cleared (Overedge replaced it)
            Player.SkillState dash = p.GetDashSkill();
            if (dash != null && dash.skillData != null)
                SetStatData(dash.skillData, StatData.cdStr, "DashCooldown", dashCd, true);
            SetMod(p.health.healModifierStat, "Heal", 0f);   // Soothing Waters' old +25%, cleared
        }

        // ---- Hooks on each wizard ----

        private static void Hook(Player p)
        {
            Health health = p.health;
            int id = health.GetInstanceID();
            Health known;
            if (hooked.TryGetValue(id, out known) && known == health)
                return;
            hooked[id] = health;
            Player me = p;
            p.skillExecEventHandlers += state => Guard("cast", () => OnCast(me, state));
            health.evadeEventHandlers += () => Guard("evade", () => OnEvade(me));
            health.takeDamageEventHandlers += (info, attacker) => Guard("hurt", () => OnHurt(me, info));
            health.healEventHandlers += (value, overheal) => Guard("heal", () => OnHeal(me, value));
        }

        private static void OnCast(Player p, Player.SkillState state)
        {
            if (!Talents.Enabled || state == null)
                return;
            State st = StateOf(p);
            string cloak = Cloak(p);
            Player partner = Partner(p);
            Player.SkillState dash = p.GetDashSkill();
            bool isDash = dash != null && state.skillID == dash.skillID;
            if (E(p, "Vigor") && Element(state) == "Earth")
                Fast(st, 0.15f, 0.5f);
            if (E(p, "Switch") && Time.time < st.fastCastAfterEvade && !isDash)
            {
                Fast(st, 0.30f, 0.6f);
                st.fastCastAfterEvade = -1f;
                Say(p, "Switch", "Switch: cast faster after an evade");
            }
            if (state.isUltimate)
            {
                if (E(p, "Fury"))
                {
                    Fast(st, 0.30f, 0.6f);
                    Say(p, "Fury", "Fury: signature cast faster");
                }
                if (C(p, "Tempo") && Alive(partner))
                {
                    AddCharge(partner, 0.15f);
                    Say(p, "TempoCoop", "Tempo: gave partner 15% signature charge");
                }
                if (C(partner, "Fury"))
                {
                    AddCharge(partner, 0.10f);
                    Say(partner, "FuryCoop", "Fury: partner's signature gave 10% charge");
                }
            }
            if (E(p, "Pace") && !isDash)
                st.momentumUntil = Time.time + 2f;
            // Savile: a water arcana cast soon after a dash comes back 15% sooner (its cooldown
            // starts after this event, so it's shortened on the next tick).
            if (E(p, "Savile") && !isDash && Time.time - st.lastDash <= SavileWindow && Element(state) == "Water")
            {
                refunds.Add(state);
                Say(p, "Savile", "Savile: water cast after a dash, cooldown 15% shorter");
            }
            if (isDash)
                st.lastDash = Time.time;
            if (isDash)
            {
                st.dashUntil = Time.time + 0.3f;
                st.partnerDashReset = false;
                if (E(p, "Fall"))
                {
                    st.noPitUntil = Time.time + 1f;
                    Say(p, "Fall", "Fall: no pits for 1 s after the dash");
                }
                if (E(p, "Venture"))
                    StaticTrail(p, dash);
            }
        }

        private static void Fast(State st, float amount, float seconds)
        {
            st.castFastUntil = Time.time + seconds;
            st.dashFrom = new Vector2(amount, 0f);   // the cast-speed amount for the tick
        }

        private static string Element(Player.SkillState state)
        {
            try
            {
                return state.skillData != null ? state.skillData.GetValue<string>(StatData.eleStr) : "";
            }
            catch
            {
                return "";
            }
        }

        private static void OnEvade(Player p)
        {
            if (!Talents.Enabled)
                return;
            State st = StateOf(p);
            string cloak = Cloak(p);
            if (E(p, "Switch"))
                st.fastCastAfterEvade = Time.time + 2f;
            if (C(p, "Switch"))
            {
                st.evadeBuffUntil = Time.time + 2f;
                Say(p, "SwitchCoop", "Shared Footing: evaded, partner +10% evade for 2 s");
            }
        }

        // A hit landing on this wizard: Vigor takes 30% of a hit on a close partner; Pride's
        // charge spills.
        private static void OnHurt(Player p, AttackInfo info)
        {
            if (!Talents.Enabled || info == null || info.damage <= 0 || sharing)
                return;
            if (E(p, "Pride"))
                AddCharge(p, -0.10f);
            Player partner = Partner(p);
            if (C(partner, "Vigor") && Close(p, partner, 3f))
            {
                int share = Mathf.FloorToInt(info.damage * 0.3f);
                int take = Mathf.Min(share, partner.health.CurrentHealthValue - 1);
                if (take > 0)
                {
                    info.damage -= take;
                    sharing = true;
                    try
                    {
                        partner.health.CurrentHealthValue -= take;
                    }
                    finally
                    {
                        sharing = false;
                    }
                    Say(partner, "Vigor", $"Vigor: took {take} of a hit on partner");
                }
            }
        }

        // Pink's co-op: a big heal (an orb) spills 30% to the partner.
        private static void OnHeal(Player p, int value)
        {
            if (!Talents.Enabled || value <= 0 || sharing || !C(p, "Pink"))
                return;
            if (value < 0.10f * p.health.healthStat.ModifiedValue)
                return;
            Player partner = Partner(p);
            if (!Alive(partner))
                return;
            sharing = true;
            try
            {
                partner.health.RestoreHealth(Mathf.CeilToInt(value * 0.3f * Pacts.PartnerHealScale(p)), false, false);
            }
            finally
            {
                sharing = false;
            }
            Say(p, "PinkCoop", "Pink: a big heal also healed partner 30%");
        }

        // ---- Hits on enemies ----

        // Before damage: Rule's follow-up and Duelist's Cloak's focus raise the hit.
        private static void BeforeAnyHit(AttackInfo info, Entity attacker, Entity hurt)
        {
            if (!Talents.Enabled || info == null || !(hurt is Enemy) || info.damage <= 0)
                return;
            Guard("hit", () =>
            {
                Player hitter = PlayerOf(info.skillCategory);
                if (hitter == null)
                    return;
                Player partner = Partner(hitter);
                int id = hurt.GetInstanceID();
                Hit last;
                lastHits.TryGetValue(id, out last);
                bool partnerHitFirst = last != null && partner != null && last.category == partner.skillCategory;
                float boost = 1f;
                if (partnerHitFirst && C(partner, "Rule") && !last.followed && Time.time - last.time <= 2f)
                {
                    boost += 0.20f;
                    last.followed = true;
                    Say(partner, "RuleCoop", "Rule: partner's follow-up hit +20%");
                }
                bool duelist = C(hitter, "DuelistCloak") || C(partner, "DuelistCloak");
                if (partnerHitFirst && duelist && Time.time - last.time <= 1f)
                {
                    boost += 0.15f;
                    Say(hitter, "DuelistCoop", "Duelist's Cloak: focused enemy takes +15%");
                }
                if (boost > 1f)
                    info.damage = Mathf.RoundToInt(info.damage * boost);
                if (last == null || last.category != info.skillCategory)
                    lastHits[id] = new Hit { category = info.skillCategory, time = Time.time };
                else
                    last.time = Time.time;
                HookStatus(hurt);
                // Confluence: remember each element this wizard has hit this enemy with.
                if (E(hitter, "Pink") && info.elementType != ElementType.Neutral)
                {
                    Dictionary<int, HashSet<ElementType>> byPlayer;
                    if (!elementsOn.TryGetValue(id, out byPlayer))
                        elementsOn[id] = byPlayer = new Dictionary<int, HashSet<ElementType>>();
                    HashSet<ElementType> set;
                    if (!byPlayer.TryGetValue(hitter.playerID, out set))
                        byPlayer[hitter.playerID] = set = new HashSet<ElementType>();
                    set.Add(info.elementType);
                }
            });
        }

        // Kills: Greed's earth gold and Level's floor stacks.
        private static void AfterAnyHit(AttackInfo info, Entity attacker, Entity hurt)
        {
            if (!Talents.Enabled || info == null)
                return;
            Enemy enemy = hurt as Enemy;
            if (enemy == null || enemy.health == null || enemy.health.CurrentHealthValue > 0 || !killed.Add(enemy.GetInstanceID()))
                return;
            Guard("kill", () =>
            {
                Player killer = PlayerOf(info.skillCategory);
                if (killer == null)
                    return;
                if (E(killer, "Greed") && info.elementType == ElementType.Earth && UnityEngine.Random.value < GreedGoldChance)
                {
                    Player.goldWallet?.Deposit(GreedGold);
                    Say(killer, "Greed", $"Greed: an earth kill knocked {GreedGold} gold loose");
                }
                Confluence(enemy);
                StateOf(killer).floorKills += 1f;
                Player partner = Partner(killer);
                if (C(partner, "Level"))
                    StateOf(partner).floorKills += 0.5f;   // Shared Glory: a partner's kill counts half
            });
        }

        // Confluence: an enemy dies; each wizard with Confluence who hit it gets a chance to be
        // healed, +15% for every different element they hit it with.
        private static void Confluence(Enemy enemy)
        {
            Dictionary<int, HashSet<ElementType>> byPlayer;
            if (!elementsOn.TryGetValue(enemy.GetInstanceID(), out byPlayer))
                return;
            elementsOn.Remove(enemy.GetInstanceID());
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            foreach (Player p in players)
            {
                HashSet<ElementType> set;
                if (p == null || !byPlayer.TryGetValue(p.playerID, out set) || !E(p, "Pink") || !Alive(p))
                    continue;
                if (UnityEngine.Random.value < ConfluenceChance * set.Count)
                {
                    Heal(p, ConfluenceHeal);
                    Say(p, "Confluence", $"Confluence: a kill hit with {set.Count} element(s) healed 3%");
                }
            }
        }

        // Together: 1% of max health, only while below 20% of max (Matt: "below 20% health"),
        // never past it.
        private static void TogetherHeal(Player p)
        {
            if (!Alive(p))
                return;
            float max = p.health.healthStat.ModifiedValue;
            int cap = Mathf.FloorToInt(TogetherCap * max);
            int amount = Mathf.Min(Mathf.Max(1, Mathf.RoundToInt(0.01f * max)), cap - p.health.CurrentHealthValue);
            if (amount > 0)
                p.health.RestoreHealth(amount, false, false);
        }

        // Shocks are seen through the enemy's own status events.
        private static void HookStatus(Entity enemy)
        {
            int id = enemy.GetInstanceID();
            if (!statusHooked.Add(id))
                return;
            enemy.statusApplyEventHandlers += element =>
            {
                if (element == ElementType.Lightning)
                    shockedUntil[id] = Time.time + 5f;
            };
            enemy.statusRemoveEventHandlers += element =>
            {
                if (element == ElementType.Lightning)
                    shockedUntil.Remove(id);
            };
        }

        // Awe: a crit on a shocked enemy arcs to the nearest other enemy; a crit refunds the
        // partner's dash.
        private static void OnCrit(AttackInfo info, Entity attacker, Entity hurt)
        {
            if (!Talents.Enabled || info == null || !(hurt is Enemy))
                return;
            Guard("crit", () =>
            {
                Player p = PlayerOf(info.skillCategory);
                if (p == null || (!E(p, "Awe") && !C(p, "Awe")))
                    return;
                Player partner = Partner(p);
                if (C(p, "Awe") && Alive(partner))
                {
                    Player.SkillState dash = partner.GetDashSkill();
                    if (dash != null && dash.cooldownRef != null)
                        dash.cooldownRef.ReduceRemainingTime(0.3f);
                    Say(p, "AweCoop", "Awe: crit took 0.3 s off partner's dash");
                }
                float until;
                if (E(p, "Awe") && shockedUntil.TryGetValue(hurt.GetInstanceID(), out until) && Time.time < until)
                {
                    Enemy next = Nearest(hurt, 4f);
                    if (next != null)
                    {
                        LightningBurst.CreateBurst(next.transform.position, info.skillCategory, info.skillID, Mathf.Max(1, info.skillLevel), 0.6f, false);
                        Say(p, "Awe", "Awe: crit on a shocked enemy arced");
                    }
                }
            });
        }

        private static Enemy Nearest(Entity from, float range)
        {
            Enemy best = null;
            float bestDist = range;
            foreach (Collider2D col in Physics2D.OverlapCircleAll(from.transform.position, range))
            {
                Enemy e = col != null ? col.GetComponentInParent<Enemy>() : null;
                if (e == null || e == from || e.health == null || e.health.CurrentHealthValue <= 0)
                    continue;
                float d = ((Vector2)e.transform.position - (Vector2)from.transform.position).magnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = e;
                }
            }
            return best;
        }

        // Tempo's test, asked every frame: the game's enemy list instead of a physics overlap, and the
        // answer kept for 0.2 s per wizard (audit A17: an OverlapCircleAll + GetComponentInParent
        // per collider every frame).
        private static readonly Dictionary<int, KeyValuePair<float, bool>> frozenNear = new Dictionary<int, KeyValuePair<float, bool>>();

        private static bool FrozenNear(Player p, float range)
        {
            int id = p.GetInstanceID();
            KeyValuePair<float, bool> had;
            if (frozenNear.TryGetValue(id, out had) && Time.time < had.Key)
                return had.Value;
            bool found = false;
            var enemies = GameController.enemies;
            if (enemies != null)
            {
                Vector2 at = p.transform.position;
                float r2 = range * range;
                foreach (GameObject go in enemies)
                {
                    if (go == null || ((Vector2)go.transform.position - at).sqrMagnitude > r2)
                        continue;
                    Enemy enemy = go.GetComponent<Enemy>();
                    if (enemy != null && enemy.fsm != null && enemy.fsm.currentState is Entity.FrozenState)
                    {
                        found = true;
                        break;
                    }
                }
            }
            if (frozenNear.Count > 16)
                frozenNear.Clear();
            frozenNear[id] = new KeyValuePair<float, bool>(Time.time + 0.2f, found);
            return found;
        }

        // ---- Fall's rescue ----

        // The partner of a Fall wearer starting to fall into a pit, at most once a minute: the fall
        // is called off, and a tether pulls them to the wearer's side while they're untargetable.
        private static void Rescue(Player p, Player partner)
        {
            if (partner == null || !C(partner, "Fall") || !Alive(partner) || p.fsm == null)
                return;
            if (!(p.fsm.currentState is Player.PlayerFallState))
                return;
            State keeper = StateOf(partner);
            if (Time.time < keeper.rescueReady)
                return;
            keeper.rescueReady = Time.time + 60f;
            foreach (var pair in p.fsm.states)
            {
                if (pair.Value is Player.IdleState)
                {
                    p.fsm.ChangeState(pair.Key, false);
                    break;
                }
            }
            var go = new GameObject("ChaosDepths FallRescue");
            go.AddComponent<Pull>().Begin(p, partner);
            Say(partner, "FallCoop", "Fall: pulled partner out of a pit");
        }

        private class Pull : MonoBehaviour
        {
            private const float Time_ = 0.45f;
            private Player who, to;
            private Vector2 from;
            private float t;

            public void Begin(Player falling, Player keeper)
            {
                who = falling;
                to = keeper;
                from = falling.transform.position;
                falling.health.invulnerable = true;
                Guard("rescue", () => falling.TogglePreventFallCollider(true));
                KeystoneFx.SetActive(keeper, "Lifebloom", true);
            }

            private void Update()
            {
                t += Time.deltaTime;
                if (who == null || to == null)
                {
                    Destroy(gameObject);
                    return;
                }
                Vector2 side = (Vector2)to.transform.position + new Vector2(0.8f, 0f);
                Vector2 at = Vector2.Lerp(from, side, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Time_)));
                who.transform.position = at;
                if (who.rigidbody2D != null)
                {
                    who.rigidbody2D.position = at;
                    who.rigidbody2D.velocity = Vector2.zero;
                }
                if (t < Time_)
                    return;
                who.health.invulnerable = false;
                Guard("rescue end", () => who.TogglePreventFallCollider(false));
                KeystoneFx.SetActive(to, "Lifebloom", false);
                Destroy(gameObject);
            }
        }

        // ---- Small helpers ----

        private static void StaticTrail(Player p, Player.SkillState dash)
        {
            Vector2 start = p.transform.position;
            Vector2 dir = p.movement.dashVector.sqrMagnitude > 0.0001f ? p.movement.dashVector.normalized : Entity.GetFacingDirectionVector(p.facingDirection);
            State st = StateOf(p);
            for (int i = 0; i < 3; i++)
            {
                Vector2 at = start + dir * (0.8f * i);
                Guard("venture", () => LightningBurst.CreateBurst(at, p.skillCategory, dash.skillID, 1, 0.5f, false));
                st.trail.Add(new Vector3(at.x, at.y, Time.time + 1.5f));   // the partner's speed boost spots
            }
            Say(p, "Venture", "Venture: static trail");
        }

        private static void ResetDash(Player partner)
        {
            Player.SkillState dash = partner.GetDashSkill();
            if (dash != null && dash.cooldownRef != null)
                dash.cooldownRef.Reset();
        }

        private static void TickCooldowns(Player p, float seconds)
        {
            if (p.skillsDict == null)
                return;
            foreach (Player.SkillState s in p.skillsDict.Values)
                if (s != null && s.cooldownRef != null && !s.cooldownRef.Ready)
                    s.cooldownRef.ReduceRemainingTime(seconds);
        }

        private static void AddCharge(Player p, float share)
        {
            if (p == null)
                return;
            p.OverdriveProgress = Mathf.Clamp(p.OverdriveProgress + share * Player.overdriveMaxValue, 0f, Player.overdriveMaxValue);
        }

        private static void Heal(Player p, float share)
        {
            if (Alive(p))
                p.health.RestoreHealth(Mathf.Max(1, Mathf.RoundToInt(share * p.health.healthStat.ModifiedValue)), false, false);
        }

        private static void SetSkillMods(Player p, string field, string node, float value, bool multiplicative)
        {
            if (p.skillsDict == null)
                return;
            foreach (Player.SkillState s in p.skillsDict.Values)
                if (s != null && s.skillData != null)
                    SetStatData(s.skillData, field, node, value, multiplicative);
        }

        private static void SetStatData(StatData data, string field, string node, float value, bool multiplicative)
        {
            string id = Prefix + node;
            var key = new KeyValuePair<object, string>(data, id + field);
            float had;
            if (applied.TryGetValue(key, out had) && Mathf.Approximately(had, value))
                return;
            if (applied.Count > 2048)
                applied.Clear();
            data.RemoveMod(field, id);
            if (!Mathf.Approximately(value, 0f))
                data.AddMod(field, new NumVarStatMod(id, value, 10, multiplicative ? VarStatModType.Multiplicative : VarStatModType.Additive, false));
            applied[key] = value;
        }

        private static void SetMod(NumVarStat stat, string node, float value, bool multiplicative = true)
        {
            if (stat == null)
                return;
            string id = Prefix + node;
            var key = new KeyValuePair<object, string>(stat, id);
            float had;
            if (applied.TryGetValue(key, out had) && Mathf.Approximately(had, value))
                return;
            stat.RemoveMod(id);
            if (!Mathf.Approximately(value, 0f))
                stat.AddMod(new NumVarStatMod(id, value, 10, multiplicative ? VarStatModType.Multiplicative : VarStatModType.Additive, false));
            applied[key] = value;
        }

        private static void SetMod(AnimSpeedHandler handler, string node, float value)
        {
            if (handler == null)
                return;
            string id = Prefix + node;
            var key = new KeyValuePair<object, string>(handler, id);
            float had;
            if (applied.TryGetValue(key, out had) && Mathf.Approximately(had, value))
                return;
            if (applied.ContainsKey(key))
                handler.Modify(new NumVarStatMod(id, had, 10, VarStatModType.Multiplicative, false), false);
            if (!Mathf.Approximately(value, 0f))
                handler.Modify(new NumVarStatMod(id, value, 10, VarStatModType.Multiplicative, false), true);
            applied[key] = value;
        }

        private static void Say(Player p, string key, string message)
        {
            if (p != null && said.Add(p.playerID + "/" + key))
                ChaosDepthsPlugin.Log($"Cloak perk: player {p.playerID + 1}: {message}");
        }

        private static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (said.Add("error/" + what))
                    ChaosDepthsPlugin.LogWarning($"Cloak perk: {what} failed ({e.Message})");
            }
        }
    }
}
