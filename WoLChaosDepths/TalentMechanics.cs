using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace WoLChaosDepths
{
    /// <summary>
    /// The Windrunner and Mender trees' mechanic nodes and keystones (WoL Player's file; the other
    /// trees' mechanics are in TalentEffects). Installed from TalentEffects.Install. Ranks come
    /// from Talents.Rank, which is 0 while [Talents] Enabled is off. Numbers are the approved
    /// drafts (Log - Player 2026-11-02 17:05 / 17:10).
    ///   Windrunner: Quick Start (sprint builds up 15% faster a point), Long Stride (+5% dash
    ///   distance), Tailspin (a dash through an enemy knocks it aside and slows it 30% for 1.5 s),
    ///   Hit and Run (+15% damage for 2 s after a dash), Evasive (+5% evade while sprinting),
    ///   Afterimage (a dash leaves the game's ice decoy, which enemies go after, for 1.5 s; 3 s
    ///   apart), Velocity (+1% damage for every 1% move speed above base).
    ///   Mender: Leech (a kill heals 1% max health), Overheal (healing past full becomes a 5 s
    ///   shield, up to 10% max health), Regrowth (in a cleared room, regain 1% max health a
    ///   second up to half), Sanctuary (a signature cast leaves a 4 s healing circle: 3% max health
    ///   a second to wizards inside), Lifebloom (your healing also heals the other wizard 50%).
    /// Every effect logs a "Talents: ..." line the first time it acts on a floor.
    /// </summary>
    public static class TalentMechanics
    {
        private const string Prefix = "ChaosDepths_TM_";
        private const float QuickStartStep = -0.15f, LongStrideStep = 0.05f;
        private const float TailspinRadius = 0.9f, TailspinWindow = 0.3f, TailspinSlow = -0.3f, TailspinSlowTime = 1.5f, TailspinPush = 12f;
        private const float HitAndRunDamage = 0.15f, HitAndRunTime = 2f;
        private const float EvasiveEvade = 0.05f, SprintAfter = 1f, MovingStep = 0.04f;
        private const float AfterimageTime = 1.5f, AfterimageCooldown = 3f;
        private const float LeechShare = 0.01f;
        private const float OverhealShare = 0.10f, OverhealTime = 5f;
        private const float RegrowthRate = 0.01f, RegrowthCap = 0.5f, RegrowthClearRadius = 15f, RegrowthDelay = 1f;
        private const float SanctuaryRadius = 2.5f, SanctuaryTime = 4f, SanctuaryRate = 0.03f;
        private const float LifebloomShare = 0.5f, LifebloomShow = 1f;

        private class State
        {
            public float dashUntil, hitAndRunUntil, afterimageReady, movingSince = -1f, clearSince = -1f, lifebloomShowUntil;
            public float quickdrawUntil = -1f, fortifyUntil = -1f;
            public bool fortifySet;
            public Vector2 lastPos, dashDir;
            public readonly HashSet<int> tailspun = new HashSet<int>();
            public int shieldAdded;
            public float shieldUntil;
            public float regrowthPending, sanctuaryPending;
        }

        private class Circle
        {
            public Vector2 at;
            public float until;
        }

        private class Slow
        {
            public Enemy enemy;
            public float until;
        }

        private static ManualLogSource log;
        private static readonly Dictionary<int, State> states = new Dictionary<int, State>();
        private static readonly Dictionary<int, Health> hooked = new Dictionary<int, Health>();
        private static readonly List<Circle> circles = new List<Circle>();
        private static readonly List<Slow> slows = new List<Slow>();
        private static readonly HashSet<int> killed = new HashSet<int>();
        private static readonly HashSet<string> said = new HashSet<string>();
        // What each stat holds now from us, so a value is only re-set when it changes or the stat
        // object is new (players are rebuilt on level loads).
        private static readonly Dictionary<KeyValuePair<object, string>, float> applied = new Dictionary<KeyValuePair<object, string>, float>();
        private static bool relaying;

        public static void Install(ManualLogSource logger)
        {
            log = logger;
            var host = new GameObject("ChaosDepths TalentMechanics");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<Runner>();
            GameController.levelLoadEventHandlers += (next, previous) =>
            {
                circles.Clear();
                slows.Clear();
                killed.Clear();
                said.Clear();
                states.Clear();
            };
            Health.globalTakeDamageActualHandlers += OnAnyDamage;
            // Unique-stat nodes (Matt, 2026-11-02 20:20).
            Health.globalTakeDamageEnterHandlers += LuckyStrike;
            Projectile.projectileShootEvent += Ricochet;
        }

        // ---- Unique-stat nodes (Matt, 2026-11-02 20:20) ----
        //   Warden (agents/summons +25% health), Heavy Hand (+15% knockback), Fortify (+0.15 s
        //   invulnerable after a dash), Quickdraw (basic arcana +8% attack speed), Ricochet
        //   (projectiles +10% speed and damage), Deep Reserves (+1 charge on standard arcana),
        //   Spellweaver (+5% cast speed a point), Prolong (signature lasts 20% longer), Second Gust
        //   (+1 dash charge), Long Vigil (agents/summons last 25% longer a point), Loaded Dice
        //   (+5% on every status chance), Fester (damage over time ticks 15% faster), Lucky Strike
        //   (10% chance a hit lands twice).
        private const float FortifyTime = 0.15f, FortifyDash = 0.25f;
        private const float QuickdrawSpeed = 0.08f, QuickdrawWindow = 0.5f;

        private static void UniqueStats(Player player, State st)
        {
            int warden = Talents.Rank(player, "Warden"), vigil = Talents.Rank(player, "LongVigil");
            int heavy = Talents.Rank(player, "HeavyHand"), dice = Talents.Rank(player, "LoadedDice"), fester = Talents.Rank(player, "Fester");
            Player.SkillState basic = null, dash = null;
            try
            {
                basic = player.GetBasicSkill();
                dash = player.GetDashSkill();
            }
            catch
            {
            }
            if (player.skillsDict != null)
            {
                foreach (Player.SkillState skill in player.skillsDict.Values)
                {
                    StatData sd = skill != null ? skill.skillData : null;
                    if (sd == null)
                        continue;
                    bool summons = Summons(sd);
                    SetStat(sd, StatData.healthStr, "Warden", summons ? 0.25f * warden : 0f, true);
                    SetStat(sd, StatData.durStr, "LongVigil", summons ? 0.25f * vigil : 0f, true);
                    SetStat(sd, StatData.kbMultiStr, "HeavyHand", 0.15f * heavy, true);
                    SetStat(sd, StatData.intervalStr, "Fester", -0.15f * fester, true);
                    foreach (string chance in StatusChances)
                        SetStat(sd, chance, "LoadedDice", 0.05f * dice, false);
                    // Steady Draw: a charged basic (the game's NegMeleeState) charges 15% faster,
                    // on its own charge-time stat, as the game's ReduceBasicChargeTime relic does.
                    Player.NegMeleeState charged = skill as Player.NegMeleeState;
                    if (charged != null && charged.chargeTimeStat != null)
                        SetCharge(charged.chargeTimeStat, Talents.Rank(player, "SteadyDraw") > 0 ? -0.15f : 0f);
                    // Deep Reserves: standard arcana (not the basic, the dash or the signature slot).
                    bool standard = skill != basic && skill != dash && !skill.isUltimate;
                    if (skill.cooldownRef != null && skill.cooldownRef.maxChargeStat != null)
                    {
                        int extra = standard ? Talents.Rank(player, "DeepReserves") : skill == dash ? Talents.Rank(player, "SecondGust") : 0;
                        SetMod(skill.cooldownRef.maxChargeStat, "Charges", extra, false);
                    }
                }
            }
            // Spellweaver: activation speed; Quickdraw: the basic arcana's swings just after one starts.
            float anim = 0.05f * Talents.Rank(player, "Spellweaver");
            if (Time.time < st.quickdrawUntil && Talents.Rank(player, "Quickdraw") > 0)
                anim += QuickdrawSpeed;
            SetAnim(player, "Activation", anim);
            // Prolong: the signature's active time drains 20% slower.
            SetMod(player.overdriveActiveDecayRate, "Prolong", Talents.Rank(player, "Prolong") > 0 ? -0.2f : 0f, true);
            // Fortify: still untouchable a moment after the dash ends.
            bool fortified = Time.time < st.fortifyUntil && Talents.Rank(player, "Fortify") > 0;
            if (fortified)
            {
                player.health.invulnerable = true;
                st.fortifySet = true;
            }
            else if (st.fortifySet)
            {
                player.health.invulnerable = false;
                st.fortifySet = false;
            }
        }

        private static readonly string[] StatusChances =
            { StatData.burnChStr, StatData.slowChStr, StatData.shockChStr, StatData.frzChStr, StatData.rootChStr };

        // An arcana that puts agents or summons out: it has their health or a spawn count.
        private static bool Summons(StatData sd)
        {
            try
            {
                return sd.GetValue<int>(StatData.spawnStr) > 0 || sd.GetValue<int>(StatData.healthStr) > 0;
            }
            catch
            {
                return false;
            }
        }

        // Lucky Strike: before a wizard's hit lands, a 10% chance it counts twice.
        private static void LuckyStrike(AttackInfo info, Entity attacker, Entity hurt)
        {
            if (!Talents.Enabled || info == null || info.damage <= 0 || !(hurt is Enemy))
                return;
            Guard("lucky strike", () =>
            {
                Player player = PlayerOf(info.skillCategory);
                if (player == null || Talents.Rank(player, "LuckyStrike") <= 0 || UnityEngine.Random.value >= 0.10f)
                    return;
                info.damage *= 2;
                Say(player, "LuckyStrike", "Lucky Strike: a hit struck twice");
            });
        }

        // Ricochet: a wizard's projectile, as it's shot, flies 10% faster and hits 10% harder (the
        // game's own projectile-shoot event, which fires once a shot).
        private static void Ricochet(Projectile p)
        {
            if (!Talents.Enabled || p == null || p.attackBox == null || p.attackBox.atkInfo == null)
                return;
            Guard("ricochet", () =>
            {
                Player player = PlayerOf(p.attackBox.atkInfo.skillCategory);
                if (player == null || Talents.Rank(player, "Ricochet") <= 0)
                    return;
                p.moveSpeed *= 1.1f;
                p.attackBox.atkInfo.damage = Mathf.RoundToInt(p.attackBox.atkInfo.damage * 1.1f);
                Say(player, "Ricochet", "Ricochet: projectile faster and harder");
            });
        }

        private static void SetStat(StatData data, string field, string node, float value, bool multiplicative)
        {
            string id = Prefix + node;
            var key = new KeyValuePair<object, string>(data, id + field);
            float had;
            if (applied.TryGetValue(key, out had) && Mathf.Approximately(had, value))
                return;
            data.RemoveMod(field, id);
            if (!Mathf.Approximately(value, 0f))
                data.AddMod(field, new NumVarStatMod(id, value, 10, multiplicative ? VarStatModType.Multiplicative : VarStatModType.Additive, false));
            applied[key] = value;
        }

        private static void SetCharge(NumVarStat stat, float value)
        {
            string id = Prefix + "SteadyDraw";
            var key = new KeyValuePair<object, string>(stat, id);
            float had;
            if (applied.TryGetValue(key, out had) && Mathf.Approximately(had, value))
                return;
            if (applied.ContainsKey(key))
                stat.Modify(new NumVarStatMod(id, had, 10, VarStatModType.Multiplicative), false);
            if (!Mathf.Approximately(value, 0f))
                stat.Modify(new NumVarStatMod(id, value, 10, VarStatModType.Multiplicative), true);
            applied[key] = value;
        }

        private static void SetAnim(Player player, string node, float value)
        {
            if (player.animSpeedHandler == null)
                return;
            string id = Prefix + node;
            var key = new KeyValuePair<object, string>(player.animSpeedHandler, id);
            float had;
            if (applied.TryGetValue(key, out had) && Mathf.Approximately(had, value))
                return;
            if (applied.ContainsKey(key))
                player.animSpeedHandler.Modify(new NumVarStatMod(id, had, 10, VarStatModType.Multiplicative, false), false);
            if (!Mathf.Approximately(value, 0f))
                player.animSpeedHandler.Modify(new NumVarStatMod(id, value, 10, VarStatModType.Multiplicative, false), true);
            applied[key] = value;
        }

        private class Runner : MonoBehaviour
        {
            private void Update()
            {
                if (!Talents.Enabled)
                    return;
                Guard("tick", () => Tick(Time.deltaTime));
            }
        }

        private static void Tick(float dt)
        {
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            bool inTrials = ChaosDepthsPlugin.InTrialsNow();
            foreach (Player player in players)
            {
                if (player == null || player.health == null || player.movement == null)
                    continue;
                Hook(player);
                State st = StateOf(player);
                Stats(player, st);
                UniqueStats(player, st);
                Tailspin(player, st);
                Regrowth(player, st, inTrials, dt);
                Overheal(player, st);
                Player p = player;
                Fx(() => KeystoneFx.SetActive(p, "Lifebloom", Time.time < st.lifebloomShowUntil));
            }
            Circles(players, dt);
            EndSlows();
        }

        private static State StateOf(Player player)
        {
            State st;
            if (!states.TryGetValue(player.playerID, out st))
                states[player.playerID] = st = new State { lastPos = player.transform.position };
            return st;
        }

        // ---- Hooks on each wizard (keyed on its Health, which a carried-over wizard keeps) ----

        private static void Hook(Player player)
        {
            Health health = player.health;
            int id = health.GetInstanceID();
            Health known;
            if (hooked.TryGetValue(id, out known) && known == health)
                return;
            hooked[id] = health;
            Player owner = player;
            player.skillExecEventHandlers += state => Guard("cast", () => OnCast(owner, state));
            health.healEventHandlers += (value, overheal) => Guard("heal", () => OnHeal(owner, value, overheal));
        }

        private static void OnCast(Player player, Player.SkillState state)
        {
            if (!Talents.Enabled || state == null || player == null)
                return;
            State st = StateOf(player);
            Player.SkillState dash = player.GetDashSkill();
            Player.SkillState basicSkill = player.GetBasicSkill();
            if (basicSkill != null && state.skillID == basicSkill.skillID)
                st.quickdrawUntil = Time.time + QuickdrawWindow;
            if (dash != null && state.skillID == dash.skillID)
            {
                st.fortifyUntil = Time.time + FortifyDash + FortifyTime;
                st.dashUntil = Time.time + TailspinWindow;
                st.tailspun.Clear();
                Vector2 dir = player.movement.dashVector;
                st.dashDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Entity.GetFacingDirectionVector(player.facingDirection);
                if (Talents.Rank(player, "HitAndRun") > 0)
                {
                    st.hitAndRunUntil = Time.time + HitAndRunTime;
                    Say(player, "HitAndRun", $"Hit and Run: +{HitAndRunDamage * 100f:0}% damage for {HitAndRunTime:0} s");
                }
                if (Talents.Rank(player, "Afterimage") > 0 && Time.time >= st.afterimageReady)
                {
                    st.afterimageReady = Time.time + AfterimageCooldown;
                    Decoy(player);
                }
            }
            if (state.isUltimate && Talents.Rank(player, "Sanctuary") > 0)
            {
                Vector2 at = player.transform.position;
                circles.Add(new Circle { at = at, until = Time.time + SanctuaryTime });
                Fx(() => KeystoneFx.FireAt(player, at, "Sanctuary", SanctuaryTime));
                Say(player, "Sanctuary", $"Sanctuary: a {SanctuaryTime:0} s healing circle");
            }
        }

        // ---- Windrunner ----

        // Stat-style nodes, re-set only when their value changes: Quick Start, Long Stride,
        // Evasive (while sprinting) and the damage from Hit and Run and Velocity.
        private static void Stats(Player player, State st)
        {
            SetMod(player.runSpeedupStat, "QuickStart", QuickStartStep * Talents.Rank(player, "QuickStart"), true);
            SetMod(player.movement.dashSpeedStat, "LongStride", LongStrideStep * Talents.Rank(player, "LongStride"), true);

            // Sprinting: on the move without a stop for SprintAfter seconds.
            Vector2 here = player.transform.position;
            bool moving = (here - st.lastPos).magnitude > MovingStep * Time.deltaTime * 60f;
            st.lastPos = here;
            if (!moving)
                st.movingSince = -1f;
            else if (st.movingSince < 0f)
                st.movingSince = Time.time;
            bool sprinting = st.movingSince >= 0f && Time.time - st.movingSince >= SprintAfter;
            int evasive = Talents.Rank(player, "Evasive");
            SetMod(player.health.evadeStat, "Evasive", sprinting && evasive > 0 ? EvasiveEvade : 0f, false);
            if (sprinting && evasive > 0)
                Say(player, "Evasive", $"Evasive: +{EvasiveEvade * 100f:0}% evade while sprinting");

            float damage = 0f;
            if (Time.time < st.hitAndRunUntil && Talents.Rank(player, "HitAndRun") > 0)
                damage += HitAndRunDamage;
            float speedBonus = 0f;
            if (Talents.Rank(player, "Velocity") > 0)
            {
                var speed = player.movement.moveSpeedStat;
                if (speed.BaseValue > 0.01f)
                    speedBonus = Mathf.Max(0f, speed.ModifiedValue / speed.BaseValue - 1f);
                damage += speedBonus;
                if (speedBonus > 0.05f)
                    Say(player, "Velocity", $"Velocity: +{speedBonus * 100f:0}% damage from move speed");
            }
            Player p = player;
            bool fast = moving && speedBonus > 0.05f;
            Fx(() => KeystoneFx.SetActive(p, "Velocity", fast));
            if (player.skillsDict != null)
                foreach (Player.SkillState skill in player.skillsDict.Values)
                    if (skill != null && skill.skillData != null)
                        SetSkillDamage(skill.skillData, damage);
        }

        // Tailspin: during a dash's first moments, enemies it passes through are knocked aside
        // (the game's own knockback, no damage) and slowed.
        private static void Tailspin(Player player, State st)
        {
            if (Time.time >= st.dashUntil || Talents.Rank(player, "Tailspin") <= 0)
                return;
            foreach (Collider2D col in Physics2D.OverlapCircleAll(player.transform.position, TailspinRadius))
            {
                Enemy enemy = col != null ? col.GetComponentInParent<Enemy>() : null;
                if (enemy == null || enemy.health == null || enemy.health.CurrentHealthValue <= 0 || !st.tailspun.Add(enemy.GetInstanceID()))
                    continue;
                Vector2 side = new Vector2(-st.dashDir.y, st.dashDir.x);
                if (Vector2.Dot((Vector2)enemy.transform.position - (Vector2)player.transform.position, side) < 0f)
                    side = -side;
                Guard("tailspin push", () =>
                {
                    Player.SkillState dash = player.GetDashSkill();
                    AttackInfo info = AttackInfo.GetInfo(player, player.skillCategory, dash.skillID, 1, false);
                    info.damage = 0;
                    info.knockbackVector = side;
                    info.knockbackOverwrite = true;
                    info.knockbackMultiplier = TailspinPush;
                    enemy.ApplyKnockback(info);
                });
                if (enemy.movement != null)
                {
                    SetMod(enemy.movement.moveSpeedStat, "Tailspin", TailspinSlow, true);
                    slows.Add(new Slow { enemy = enemy, until = Time.time + TailspinSlowTime });
                }
                Say(player, "Tailspin", $"Tailspin: knocked {enemy.name} aside, slowed {-TailspinSlow * 100f:0}% for {TailspinSlowTime} s");
            }
        }

        private static void EndSlows()
        {
            for (int i = slows.Count - 1; i >= 0; i--)
            {
                Slow s = slows[i];
                if (s.enemy != null && Time.time < s.until)
                    continue;
                if (s.enemy != null && s.enemy.movement != null)
                    SetMod(s.enemy.movement.moveSpeedStat, "Tailspin", 0f, true);
                slows.RemoveAt(i);
            }
        }

        // Afterimage: the game's ice decoy (what enemies go after), left where the dash began.
        private static void Decoy(Player player)
        {
            Vector2 at = player.transform.position;
            Guard("afterimage", () =>
            {
                GameObject go = UnityEngine.Object.Instantiate(IceDecoy.Prefab, at, Quaternion.identity);
                IceDecoy decoy = go != null ? go.GetComponent<IceDecoy>() : null;
                if (decoy != null)
                {
                    decoy.parentEnt = player;
                    decoy.duration = AfterimageTime;
                }
            });
            Fx(() => KeystoneFx.Fire(player, "Afterimage"));
            Say(player, "Afterimage", $"Afterimage: a decoy for {AfterimageTime} s");
        }

        // ---- Mender ----

        // Leech: a kill by this wizard's arcana (the hit's skill category is Player0 / Player1,
        // which bursts carry even with no attacker) heals it.
        private static void OnAnyDamage(AttackInfo info, Entity attacker, Entity hurt)
        {
            if (!Talents.Enabled || info == null)
                return;
            Guard("leech", () =>
            {
                Enemy enemy = hurt as Enemy;
                if (enemy == null || enemy.health == null || enemy.health.CurrentHealthValue > 0 || !killed.Add(enemy.GetInstanceID()))
                    return;
                Player player = PlayerOf(info.skillCategory);
                if (player == null || player.health == null || player.health.CurrentHealthValue <= 0 || Talents.Rank(player, "Leech") <= 0)
                    return;
                Heal(player, Mathf.Max(1, Mathf.RoundToInt(LeechShare * player.health.healthStat.ModifiedValue)));
                Say(player, "Leech", "Leech: a kill healed 1% max health");
            });
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

        private static void OnHeal(Player player, int value, int overheal)
        {
            if (!Talents.Enabled || player == null || player.health == null)
                return;
            State st = StateOf(player);
            // Overheal: what went past full becomes a shield (up to 10% max health), for 5 s.
            if (overheal > 0 && Talents.Rank(player, "Overheal") > 0)
            {
                int cap = Mathf.RoundToInt(OverhealShare * player.health.healthStat.ModifiedValue);
                int add = Mathf.Min(overheal, cap - st.shieldAdded);
                if (add > 0)
                {
                    player.health.CurrentShieldValue += add;
                    st.shieldAdded += add;
                    st.shieldUntil = Time.time + OverhealTime;
                    Say(player, "Overheal", $"Overheal: {add} overhealing became a shield");
                }
            }
            // Lifebloom: half of it to the other wizard (not passed back).
            if (value > 0 && !relaying && Talents.Rank(player, "Lifebloom") > 0)
            {
                int share = Mathf.CeilToInt(value * LifebloomShare);
                Player[] players = GameController.activePlayers;
                if (players == null)
                    return;
                foreach (Player other in players)
                {
                    if (other == null || other == player || other.health == null || other.health.CurrentHealthValue <= 0)
                        continue;
                    relaying = true;
                    try
                    {
                        other.health.RestoreHealth(share, false, false);
                    }
                    finally
                    {
                        relaying = false;
                    }
                    st.lifebloomShowUntil = Time.time + LifebloomShow;
                    Say(player, "Lifebloom", $"Lifebloom: {share} healing passed to player {other.playerID + 1}");
                }
            }
        }

        // The Overheal shield fades when its time is up (only what it gave).
        private static void Overheal(Player player, State st)
        {
            if (st.shieldAdded <= 0 || Time.time < st.shieldUntil)
                return;
            int take = Mathf.Min(st.shieldAdded, player.health.CurrentShieldValue);
            if (take > 0)
                player.health.CurrentShieldValue -= take;
            st.shieldAdded = 0;
        }

        // Regrowth: no enemy alive within reach for a second (a cleared room), in a run: regain
        // 1% max health a second, up to half.
        private static void Regrowth(Player player, State st, bool inTrials, float dt)
        {
            if (!inTrials || Talents.Rank(player, "Regrowth") <= 0 || player.health.CurrentHealthValue <= 0)
            {
                st.clearSince = -1f;
                return;
            }
            foreach (Collider2D col in Physics2D.OverlapCircleAll(player.transform.position, RegrowthClearRadius))
            {
                Enemy enemy = col != null ? col.GetComponentInParent<Enemy>() : null;
                if (enemy != null && enemy.health != null && enemy.health.CurrentHealthValue > 0)
                {
                    st.clearSince = -1f;
                    return;
                }
            }
            if (st.clearSince < 0f)
                st.clearSince = Time.time;
            float max = player.health.healthStat.ModifiedValue;
            if (Time.time - st.clearSince < RegrowthDelay || player.health.CurrentHealthValue >= RegrowthCap * max)
                return;
            st.regrowthPending += RegrowthRate * max * dt;
            int whole = Mathf.FloorToInt(st.regrowthPending);
            if (whole > 0)
            {
                st.regrowthPending -= whole;
                Heal(player, whole);
                Say(player, "Regrowth", "Regrowth: regenerating in a cleared room");
            }
        }

        // Sanctuary's circles: 3% max health a second to every wizard inside.
        private static void Circles(Player[] players, float dt)
        {
            for (int i = circles.Count - 1; i >= 0; i--)
            {
                Circle c = circles[i];
                if (Time.time >= c.until)
                {
                    circles.RemoveAt(i);
                    continue;
                }
                foreach (Player p in players)
                {
                    if (p == null || p.health == null || p.health.CurrentHealthValue <= 0)
                        continue;
                    if (((Vector2)p.transform.position - c.at).magnitude > SanctuaryRadius)
                        continue;
                    State st = StateOf(p);
                    st.sanctuaryPending += SanctuaryRate * p.health.healthStat.ModifiedValue * dt;
                    int whole = Mathf.FloorToInt(st.sanctuaryPending);
                    if (whole > 0)
                    {
                        st.sanctuaryPending -= whole;
                        Heal(p, whole);
                    }
                }
            }
        }

        private static void Heal(Player player, int amount)
        {
            if (amount > 0)
                player.health.RestoreHealth(amount, false, false);
        }

        // ---- Helpers ----

        // One of our mods on a stat at this value (0 removes it), touched only when it changes.
        private static void SetMod(NumVarStat stat, string node, float value, bool multiplicative)
        {
            if (stat == null)
                return;
            string id = Prefix + node;
            var key = new KeyValuePair<object, string>(stat, id);
            float had;
            if (applied.TryGetValue(key, out had) && Mathf.Approximately(had, value))
                return;
            if (applied.Count > 512)
                applied.Clear();
            stat.RemoveMod(id);
            if (!Mathf.Approximately(value, 0f))
                stat.AddMod(new NumVarStatMod(id, value, 10, multiplicative ? VarStatModType.Multiplicative : VarStatModType.Additive, false));
            applied[key] = value;
        }

        private static void SetSkillDamage(StatData data, float value)
        {
            string id = Prefix + "Damage";
            var key = new KeyValuePair<object, string>(data, id);
            float had;
            if (applied.TryGetValue(key, out had) && Mathf.Approximately(had, value))
                return;
            data.RemoveMod(StatData.damageStr, id);
            if (!Mathf.Approximately(value, 0f))
                data.AddMod(StatData.damageStr, new NumVarStatMod(id, value, 10, VarStatModType.Multiplicative, false));
            applied[key] = value;
        }

        private static void Say(Player player, string node, string message)
        {
            if (said.Add(player.playerID + "/" + node))
                log?.LogInfo($"Talents: player {player.playerID + 1}: {message}");
        }

        private static void Fx(Action show) => Guard("keystone fx", show);

        private static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (said.Add("error/" + what))
                    log?.LogWarning($"Talents: {what} failed ({e.Message})");
            }
        }
    }
}
