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
    //   Venture   Lightning: a dash leaves a short static trail that shocks.
    //   Savile    Co-op: dashing through your partner resets their dash.
    //   Pink      Water: healing +25%.  Co-op: a big heal (10% of your health or more: an orb) heals your partner 30% of it.
    //   Fall      Air: you can't fall into a pit for 1 s after a dash.
    //             Co-op: once a minute, when your partner falls into a pit, a tether pulls them to your side, untargetable.
    //   Pride     Fire: signature charge builds 25% faster, but each hit you take empties 10% of it.
    //             Co-op: while you're alive, your partner deals +10%.
    //   Hope / Patience  Earth / Water: cooldowns -5%.  Co-op: within 3 u of each other, both regain 1% health every 5 s.
    // Not built (they need systems this file doesn't reach, or clash with how co-op works): Venture's
    // co-op (the gold wallet is already shared), Savile's puddle refund, and Greed's and Level's perks.
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
        }

        public static void Install()
        {
            var host = new GameObject("ChaosDepths CloakPerks");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<Runner>();
            GameController.levelLoadEventHandlers += (next, previous) =>
            {
                said.Clear();
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
            foreach (Player p in players)
            {
                if (p == null || p.health == null || p.movement == null)
                    continue;
                Hook(p);
                State st = StateOf(p);
                string cloak = Cloak(p);
                Player partner = Partner(p);
                SkillStats(p, cloak);

                // Cast speed (Vigor's earth casts, Switch after an evade, Fury's signature).
                bool fast = Time.time < st.castFastUntil;
                SetMod(p.animSpeedHandler, "CastSpeed", fast ? st.dashFrom.x : 0f);

                // Speed: Pace's momentum and the partners-close bonus.
                float speed = 0f;
                if (cloak == "Pace")
                {
                    if (Time.time < st.momentumUntil)
                        speed += 0.10f;
                    if (Close(p, partner, 4f))
                        speed += 0.05f;
                }
                if (partner != null && Cloak(partner) == "Pace" && Close(p, partner, 4f))
                    speed += 0.05f;
                SetMod(p.movement.moveSpeedStat, "Speed", speed);

                // Switch's partner evade.
                bool evadeBuff = partner != null && Time.time < StateOf(partner).evadeBuffUntil;
                SetMod(p.health.evadeStat, "Evade", evadeBuff ? 0.10f : 0f, false);

                // Pride's partner damage, and Tempo's cooldowns near a frozen enemy.
                float partnerDamage = partner != null && Cloak(partner) == "Pride" && Alive(partner) ? 0.10f : 0f;
                SetSkillMods(p, StatData.damageStr, "PartnerDamage", partnerDamage, true);
                if (cloak == "Tempo" && FrozenNear(p, 6f))
                {
                    TickCooldowns(p, 0.15f * Time.deltaTime);
                    Say(p, "Tempo", "Tempo: cooldowns tick faster near a frozen enemy");
                }

                // Fall: no pits for a moment after a dash.
                bool noPit = cloak == "Fall" && Time.time < st.noPitUntil;
                if (noPit != st.prevented)
                {
                    st.prevented = noPit;
                    Guard("fall", () => p.TogglePreventFallCollider(noPit));
                }

                // Grit: a revived partner gets up at half health.
                bool dead = p.health.CurrentHealthValue <= 0;
                if (st.wasDead && !dead && partner != null && Cloak(partner) == "Grit")
                {
                    int target = Mathf.RoundToInt(p.health.healthStat.ModifiedValue * 0.5f);
                    if (p.health.CurrentHealthValue < target)
                        p.health.RestoreHealth(target - p.health.CurrentHealthValue, false, false);
                    Say(partner, "Grit", "Grit: partner revived at half health");
                }
                st.wasDead = dead;

                // Fall's rescue, Savile's dash reset, Hope / Patience regen.
                Rescue(p, partner);
                if (cloak == "Savile" && Time.time < st.dashUntil && !st.partnerDashReset && Close(p, partner, 1f))
                {
                    st.partnerDashReset = true;
                    Guard("savile", () => ResetDash(partner));
                    Say(p, "Savile", "Savile: dashed through partner, their dash reset");
                }
                if ((cloak == "Hope" || cloak == "Patience") && Close(p, partner, 3f) && Time.time >= st.regenNext)
                {
                    st.regenNext = Time.time + 5f;
                    Heal(p, 0.01f);
                    Heal(partner, 0.01f);
                    Say(p, cloak, $"{cloak}: close together, both regain 1%");
                }
            }
        }

        private static bool Close(Player a, Player b, float range) =>
            Alive(a) && Alive(b) && ((Vector2)a.transform.position - (Vector2)b.transform.position).magnitude <= range;

        // The always-on stat steps, on this wizard's own arcana stats.
        private static void SkillStats(Player p, string cloak)
        {
            SetSkillMods(p, StatData.stunDurStr, "Stun", cloak == "Grit" ? 0.4f : 0f, true);
            SetSkillMods(p, StatData.burnLvStr, "Burn", cloak == "Rule" ? 1f : 0f, false);
            SetSkillMods(p, StatData.shockLvStr, "Shock", cloak == "Awe" ? 1f : 0f, false);
            SetSkillMods(p, StatData.frzDurStr, "Freeze", cloak == "Tempo" ? 0.5f : 0f, false);
            SetSkillMods(p, StatData.odProgStr, "Charge", cloak == "Pride" ? 0.25f : 0f, true);
            SetSkillMods(p, StatData.cdStr, "Cooldown", cloak == "Hope" || cloak == "Patience" ? -0.05f : 0f, true);
            float dashCd = cloak == "DuelistCloak" ? -0.20f : 0f;
            Player.SkillState dash = p.GetDashSkill();
            if (dash != null && dash.skillData != null)
                SetStatData(dash.skillData, StatData.cdStr, "DashCooldown", dashCd, true);
            SetMod(p.health.healModifierStat, "Heal", cloak == "Pink" ? 0.25f : 0f);
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
            if (cloak == "Vigor" && Element(state) == "Earth")
                Fast(st, 0.15f, 0.5f);
            if (cloak == "Switch" && Time.time < st.fastCastAfterEvade && !isDash)
            {
                Fast(st, 0.30f, 0.6f);
                st.fastCastAfterEvade = -1f;
                Say(p, "Switch", "Switch: cast faster after an evade");
            }
            if (state.isUltimate)
            {
                if (cloak == "Fury")
                {
                    Fast(st, 0.30f, 0.6f);
                    Say(p, "Fury", "Fury: signature cast faster");
                }
                if (cloak == "Tempo" && Alive(partner))
                {
                    AddCharge(partner, 0.15f);
                    Say(p, "TempoCoop", "Tempo: gave partner 15% signature charge");
                }
                if (partner != null && Cloak(partner) == "Fury")
                {
                    AddCharge(partner, 0.10f);
                    Say(partner, "FuryCoop", "Fury: partner's signature gave 10% charge");
                }
            }
            if (cloak == "Pace" && !isDash)
                st.momentumUntil = Time.time + 2f;
            if (isDash)
            {
                st.dashUntil = Time.time + 0.3f;
                st.partnerDashReset = false;
                if (cloak == "Fall")
                {
                    st.noPitUntil = Time.time + 1f;
                    Say(p, "Fall", "Fall: no pits for 1 s after the dash");
                }
                if (cloak == "Venture")
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
            if (cloak == "Switch")
            {
                st.fastCastAfterEvade = Time.time + 2f;
                st.evadeBuffUntil = Time.time + 2f;
                Say(p, "SwitchCoop", "Switch: evaded, partner +10% evade for 2 s");
            }
        }

        // A hit landing on this wizard: Vigor takes 30% of a hit on a close partner; Pride's
        // charge spills.
        private static void OnHurt(Player p, AttackInfo info)
        {
            if (!Talents.Enabled || info == null || info.damage <= 0 || sharing)
                return;
            if (Cloak(p) == "Pride")
                AddCharge(p, -0.10f);
            Player partner = Partner(p);
            if (partner != null && Cloak(partner) == "Vigor" && Close(p, partner, 3f))
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
            if (!Talents.Enabled || value <= 0 || sharing || Cloak(p) != "Pink")
                return;
            if (value < 0.10f * p.health.healthStat.ModifiedValue)
                return;
            Player partner = Partner(p);
            if (!Alive(partner))
                return;
            sharing = true;
            try
            {
                partner.health.RestoreHealth(Mathf.CeilToInt(value * 0.3f), false, false);
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
                if (partnerHitFirst && Cloak(partner) == "Rule" && !last.followed && Time.time - last.time <= 2f)
                {
                    boost += 0.20f;
                    last.followed = true;
                    Say(partner, "RuleCoop", "Rule: partner's follow-up hit +20%");
                }
                bool duelist = Cloak(hitter) == "DuelistCloak" || (partner != null && Cloak(partner) == "DuelistCloak");
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
            });
        }

        private static void AfterAnyHit(AttackInfo info, Entity attacker, Entity hurt)
        {
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
                if (p == null || Cloak(p) != "Awe")
                    return;
                Player partner = Partner(p);
                if (Alive(partner))
                {
                    Player.SkillState dash = partner.GetDashSkill();
                    if (dash != null && dash.cooldownRef != null)
                        dash.cooldownRef.ReduceRemainingTime(0.3f);
                    Say(p, "AweCoop", "Awe: crit took 0.3 s off partner's dash");
                }
                float until;
                if (shockedUntil.TryGetValue(hurt.GetInstanceID(), out until) && Time.time < until)
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

        private static bool FrozenNear(Player p, float range)
        {
            foreach (Collider2D col in Physics2D.OverlapCircleAll(p.transform.position, range))
            {
                Enemy e = col != null ? col.GetComponentInParent<Enemy>() : null;
                if (e != null && e.fsm != null && e.fsm.currentState is Entity.FrozenState)
                    return true;
            }
            return false;
        }

        // ---- Fall's rescue ----

        // The partner of a Fall wearer starting to fall into a pit, at most once a minute: the fall
        // is called off, and a tether pulls them to the wearer's side while they're untargetable.
        private static void Rescue(Player p, Player partner)
        {
            if (partner == null || Cloak(partner) != "Fall" || !Alive(partner) || p.fsm == null)
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
            for (int i = 0; i < 3; i++)
            {
                Vector2 at = start + dir * (0.8f * i);
                Guard("venture", () => LightningBurst.CreateBurst(at, p.skillCategory, dash.skillID, 1, 0.5f, false));
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
