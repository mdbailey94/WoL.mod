using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace WoLChaosDepths
{
    /// <summary>
    /// #110 (Matt, 11-04): the perks added when the seven archetype trees became 4 tiers of 3.
    /// WoL Player's file (the Modder's ArchetypeMechanics keeps the older archetype nodes).
    /// Installed from TalentMechanics.Install. Every stat that heads for 100% (crit, evade, move
    /// speed) goes through the #114 point register (Points.Set); damage bonuses stay linear.
    ///   Apprentice: Versatile (+3% damage per element in the loadout), Sharp Eye (+5 crit points),
    ///               Quick Study (Arcana Levels xp: no effect while Arcana Levels is pulled out).
    ///   Bulwark:    Spiked Plate (melee attackers take 20% of the hit back), Rally (a cleared
    ///               room heals 5%).
    ///   Duelist:    Lacerate (crits bleed 20% of the hit over 3 s), Focus Fire (+2% a hit on the
    ///               same enemy, to +20%). (Flurry's rework to move speed is in ArchetypeMechanics.)
    ///   Arcanist:   Arcane Flow (a standard cast takes 0.25 s off the other standard arcana),
    ///               Grand Design (+25% damage while every standard arcana is cooling down).
    ///   Windrunner: Wind Wake (a dash leaves a 2 s streak: +20 speed points on it), Gale Step
    ///               (every 3rd dash costs no dash cooldown). (Untouchable is in TalentMechanics,
    ///               beside the sprint check it shares with Evasive.)
    ///   Mender:     Triage (heals on a wizard under 30% health are doubled), Undying Light (once
    ///               a floor, dropping under 10% health heals 40% over 4 s).
    ///   Gambler:    Coin Toss (each room: 50% double gold, 50% none).
    /// </summary>
    public static class TreeMechanics
    {
        private const string Prefix = "ChaosDepths_TR_";

        private const float VersatileStep = 0.03f;
        private const float SharpEyePoints = 5f;
        private const float SpikedShare = 0.20f, SpikedReach = 2.5f;
        private const float RallyHeal = 0.05f;
        private const float LacerateShare = 0.20f, LacerateTime = 3f;
        private const int LacerateTicks = 6;
        private const float FocusStep = 0.02f;
        private const int FocusMax = 10;
        private const float ArcaneFlowCut = 0.25f;
        private const float GrandDesignDamage = 0.25f;
        private const float WakeTime = 2f, WakePoints = 20f, WakeReach = 0.7f, WakeSpacing = 0.35f, WakeRecord = 0.35f;
        private const int GaleStepEvery = 3;
        private const float TriageBelow = 0.30f;
        private const float UndyingBelow = 0.10f, UndyingHeal = 0.40f, UndyingTime = 4f;
        private const float RoomRadius = 15f, RoomCheck = 0.25f, CoinTossAfter = 5f;

        private class State
        {
            public float damageApplied;
            public int damageOn;
            public int focusTarget, focusStacks;
            public bool inRoom;
            public float dashRecordUntil = -1f;
            public Vector2 lastWake;
            public readonly List<KeyValuePair<Vector2, float>> wake = new List<KeyValuePair<Vector2, float>>();
            public int dashes;
            public float galeStepUntil = -1f;   // a free dash owed until its cooldown starts (or 1 s)
            public bool undyingUsed;
            public float undyingUntil, undyingPending;
        }

        private class Bleed
        {
            public Enemy enemy;
            public Player owner;
            public int perTick, ticksLeft;
            public float next;
        }

        private static ManualLogSource log;
        private static readonly Dictionary<int, State> states = new Dictionary<int, State>();
        private static readonly Dictionary<int, Health> hooked = new Dictionary<int, Health>();
        private static readonly List<Bleed> bleeds = new List<Bleed>();
        private static readonly HashSet<string> said = new HashSet<string>();
        private static bool dealing;           // our own bleed / spike damage is going in
        private static GoldWallet hookedWallet;
        private static int coinToss;           // 0 = no roll this room, 2 = double, -1 = none
        private static float coinTossUntil;
        private static bool coinRoom;
        private static float nextRoomCheck;

        public static void Install(ManualLogSource logger)
        {
            log = logger;
            var host = new GameObject("ChaosDepths TreeMechanics");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<Runner>();
            GameController.levelLoadEventHandlers += (next, previous) =>
            {
                bleeds.Clear();
                said.Clear();
                coinToss = 0;
                coinRoom = false;
                foreach (State s in states.Values)
                {
                    s.damageOn = 0;          // the wizard's stats may be rebuilt: put the mod back
                    s.focusTarget = 0;
                    s.focusStacks = 0;
                    s.inRoom = false;
                    s.wake.Clear();
                    s.undyingUsed = false;   // Undying Light: once a floor
                    s.undyingUntil = 0f;
                }
            };
            Health.globalTakeDamageEnterHandlers += BeforeAnyHit;
            Health.globalTakeDamageActualHandlers += AfterAnyHit;
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

        private static State StateOf(Player player)
        {
            State s;
            if (!states.TryGetValue(player.playerID, out s))
                states[player.playerID] = s = new State();
            return s;
        }

        private static void Tick()
        {
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            bool roomCheck = Time.time >= nextRoomCheck;
            if (roomCheck)
                nextRoomCheck = Time.time + RoomCheck;
            bool inTrials = ChaosDepthsPlugin.InTrialsNow();
            bool anyCoinToss = false;
            foreach (Player player in players)
            {
                if (player == null || player.health == null)
                    continue;
                Hook(player);
                State s = StateOf(player);

                Points.Set(player, "CritChance", "Talent.SharpEye", Talents.Rank(player, "SharpEye") > 0 ? SharpEyePoints : 0f);

                float damage = 0f;
                if (Talents.Rank(player, "Versatile") > 0)
                    damage += VersatileStep * Elements(player);
                if (Talents.Rank(player, "GrandDesign") > 0 && AllStandardsCooling(player))
                {
                    damage += GrandDesignDamage;
                    Say(player, "GrandDesign", $"Grand Design: every standard arcana cooling down, +{GrandDesignDamage * 100f:0}% damage");
                }
                SetDamage(player, s, damage);

                WindWake(player, s);
                if (s.galeStepUntil > 0f)
                {
                    // The dash's cooldown may start only as the dash ends: wait for it, then give it back.
                    Player.SkillState dash = player.GetDashSkill();
                    Cooldown cd = dash != null ? dash.cooldownRef : null;
                    if (cd != null && (cd.isChargeSkill ? cd.chargeCount < cd.MaxChargeCount : !cd.Ready))
                    {
                        GiveCharge(cd);
                        s.galeStepUntil = -1f;
                    }
                    else if (Time.time >= s.galeStepUntil)
                        s.galeStepUntil = -1f;
                }
                UndyingLight(player, s);

                if (Talents.Rank(player, "CoinToss") > 0)
                    anyCoinToss = true;
                if (roomCheck && inTrials)
                {
                    bool near = EnemiesNear(player.transform.position);
                    if (s.inRoom && !near && Talents.Rank(player, "Rally") > 0 && player.health.CurrentHealthValue > 0)
                    {
                        Heal(player, Mathf.Max(1, Mathf.RoundToInt(RallyHeal * player.health.healthStat.ModifiedValue)));
                        log?.LogInfo($"Talents: Rally, player {player.playerID + 1} cleared a room and healed {RallyHeal * 100f:0}%");
                    }
                    s.inRoom = near;
                }
            }
            if (roomCheck)
                CoinTossRoom(players, anyCoinToss && inTrials);
            Bleeds();
            HookGold();
        }

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
            health.takeDamageActualHandlers += (info, attacker) => Guard("spiked plate", () => SpikedPlate(owner, info, attacker));
            health.healEnterEventHandlers += Triage(owner);
        }

        // ---- Apprentice ----

        // Versatile: the different elements across the wizard's equipped arcana (Neutral doesn't count).
        private static int Elements(Player player)
        {
            if (player.skillsDict == null)
                return 0;
            var seen = new HashSet<ElementType>();
            foreach (Player.SkillState skill in player.skillsDict.Values)
                if (skill != null && skill.element != ElementType.Neutral)
                    seen.Add(skill.element);
            return seen.Count;
        }

        // ---- Bulwark ----

        // Spiked Plate: an enemy that hits the wizard from up close takes 20% of that hit back.
        private static void SpikedPlate(Player player, AttackInfo info, Entity attacker)
        {
            if (!Talents.Enabled || dealing || info == null || info.damage <= 0 || info.isStatusEffect || Talents.Rank(player, "SpikedPlate") <= 0)
                return;
            Enemy enemy = attacker as Enemy;
            if (enemy == null || enemy.health == null || enemy.health.CurrentHealthValue <= 0)
                return;
            if (((Vector2)enemy.transform.position - (Vector2)player.transform.position).magnitude > SpikedReach)
                return;   // a shot from range, not a melee hit
            int back = Mathf.Max(1, Mathf.CeilToInt(info.damage * SpikedShare));
            Deal(enemy, player, back, false);
            Say(player, "SpikedPlate", $"Spiked Plate: a melee attacker took {back} back");
        }

        // ---- Duelist ----

        // Before any hit lands: Focus Fire's bonus on the wizard's hits.
        private static void BeforeAnyHit(AttackInfo info, Entity attacker, Entity hurt)
        {
            if (!Talents.Enabled || dealing || info == null || info.damage <= 0 || info.isStatusEffect || !(hurt is Enemy))
                return;
            Guard("focus fire", () =>
            {
                Player player = PlayerOf(info.skillCategory);
                if (player == null || Talents.Rank(player, "FocusFire") <= 0)
                    return;
                State s = StateOf(player);
                int target = hurt.GetInstanceID();
                if (target != s.focusTarget)
                {
                    s.focusTarget = target;
                    s.focusStacks = 0;
                }
                if (s.focusStacks > 0)
                    info.damage = Mathf.CeilToInt(info.damage * (1f + FocusStep * s.focusStacks));
                if (s.focusStacks < FocusMax)
                    s.focusStacks++;
                if (s.focusStacks == FocusMax)
                    Say(player, "FocusFire", $"Focus Fire: full +{FocusStep * FocusMax * 100f:0}% on one target");
            });
        }

        // After any hit lands: Lacerate starts a bleed on a crit.
        private static void AfterAnyHit(AttackInfo info, Entity attacker, Entity hurt)
        {
            if (!Talents.Enabled || dealing || info == null || !info.isCritical || info.isStatusEffect || info.damage <= 0)
                return;
            Guard("lacerate", () =>
            {
                Enemy enemy = hurt as Enemy;
                if (enemy == null || enemy.health == null || enemy.health.CurrentHealthValue <= 0)
                    return;
                Player player = PlayerOf(info.skillCategory);
                if (player == null || Talents.Rank(player, "Lacerate") <= 0)
                    return;
                int total = Mathf.Max(1, Mathf.RoundToInt(info.damage * LacerateShare));
                // A new crit on a bleeding enemy refreshes it with the bigger bleed.
                bleeds.RemoveAll(b => b.enemy == enemy && b.owner == player && b.perTick * b.ticksLeft <= total);
                bleeds.Add(new Bleed
                {
                    enemy = enemy,
                    owner = player,
                    perTick = Mathf.Max(1, Mathf.CeilToInt((float)total / LacerateTicks)),
                    ticksLeft = LacerateTicks,
                    next = Time.time + LacerateTime / LacerateTicks
                });
                Say(player, "Lacerate", $"Lacerate: a crit bleeds for {total} over {LacerateTime:0} s");
            });
        }

        private static void Bleeds()
        {
            for (int i = bleeds.Count - 1; i >= 0; i--)
            {
                Bleed b = bleeds[i];
                if (b.enemy == null || b.enemy.health == null || b.enemy.health.CurrentHealthValue <= 0 || b.ticksLeft <= 0 || b.owner == null)
                {
                    bleeds.RemoveAt(i);
                    continue;
                }
                if (Time.time < b.next)
                    continue;
                b.next = Time.time + LacerateTime / LacerateTicks;
                b.ticksLeft--;
                Deal(b.enemy, b.owner, b.perTick, true);
            }
        }

        // Our own damage (bleed ticks, Spiked Plate's thorns): credited to the wizard (its skill
        // category, so kills count for Leech / Hot Streak), no knockback, can't crit.
        private static void Deal(Enemy enemy, Player owner, int damage, bool status)
        {
            var info = new AttackInfo
            {
                damage = damage,
                entity = owner,
                skillCategory = owner.skillCategory,
                skillID = "TalentThorns",
                elementType = ElementType.Neutral,
                isStatusEffect = status,
                knockbackMultiplier = 0f,
                showDamageNumber = true,
                showDamageEffect = !status,
                attackerPosition = owner.transform.position,
                targetPosition = enemy.transform.position
            };
            dealing = true;
            try
            {
                enemy.health.TakeDamage(info, owner);
            }
            finally
            {
                dealing = false;
            }
        }

        // ---- Arcanist and Windrunner: casts ----

        private static void OnCast(Player player, Player.SkillState state)
        {
            if (!Talents.Enabled || state == null || player == null)
                return;
            State s = StateOf(player);
            Player.SkillState dash = player.GetDashSkill(), basic = player.GetBasicSkill();
            if (dash != null && state.skillID == dash.skillID)
            {
                if (Talents.Rank(player, "WindWake") > 0)
                {
                    s.dashRecordUntil = Time.time + WakeRecord;
                    s.lastWake = player.transform.position;
                    AddWake(s, s.lastWake);
                }
                if (Talents.Rank(player, "GaleStep") > 0)
                {
                    s.dashes++;
                    if (s.dashes % GaleStepEvery == 0)
                    {
                        s.galeStepUntil = Time.time + 1f;   // given back once the dash's cooldown has started
                        Say(player, "GaleStep", $"Gale Step: every {GaleStepEvery}rd dash is free");
                    }
                }
                return;
            }
            bool standard = !state.isUltimate && (basic == null || state.skillID != basic.skillID);
            if (!standard || Talents.Rank(player, "ArcaneFlow") <= 0 || player.skillsDict == null)
                return;
            foreach (Player.SkillState other in player.skillsDict.Values)
            {
                if (other == null || other.skillID == state.skillID || other == dash || other == basic || other.isUltimate)
                    continue;
                Cooldown cd = other.cooldownRef;
                if (cd == null || cd.isChargeSkill || cd.Ready)
                    continue;
                if (cd.RemainingTime - ArcaneFlowCut <= 0.05f)
                    player.cooldownManager.ResetCooldown(cd.ID);
                else
                    cd.ReduceRemainingTime(ArcaneFlowCut);
            }
            Say(player, "ArcaneFlow", $"Arcane Flow: a cast took {ArcaneFlowCut} s off the other arcana");
        }

        // Grand Design: every standard arcana (not the basic, dash or signature) is cooling down.
        private static bool AllStandardsCooling(Player player)
        {
            if (player.skillsDict == null)
                return false;
            Player.SkillState dash = player.GetDashSkill(), basic = player.GetBasicSkill();
            int standards = 0;
            foreach (Player.SkillState skill in player.skillsDict.Values)
            {
                if (skill == null || skill == dash || skill == basic || skill.isUltimate || skill.cooldownRef == null)
                    continue;
                standards++;
                if (skill.cooldownRef.Ready)
                    return false;
            }
            return standards > 0;
        }

        // Wind Wake: points along the dash, each lasting 2 s; standing on one gives the speed.
        private static void WindWake(Player player, State s)
        {
            Vector2 here = player.transform.position;
            if (Time.time < s.dashRecordUntil && (here - s.lastWake).magnitude >= WakeSpacing)
            {
                s.lastWake = here;
                AddWake(s, here);
            }
            s.wake.RemoveAll(w => w.Value <= Time.time);
            bool on = false;
            if (Talents.Rank(player, "WindWake") > 0)
                foreach (KeyValuePair<Vector2, float> w in s.wake)
                    if ((here - w.Key).magnitude <= WakeReach)
                    {
                        on = true;
                        break;
                    }
            Points.Set(player, "MoveSpeed", "Talent.WindWake", on ? WakePoints : 0f);
            if (on)
                Say(player, "WindWake", $"Wind Wake: +{WakePoints:0} move speed points on the streak");
        }

        private static void AddWake(State s, Vector2 at)
        {
            s.wake.Add(new KeyValuePair<Vector2, float>(at, Time.time + WakeTime));
            // The streak shows as the game's dust puffs (until Art draws a wind streak).
            Guard("wind wake fx", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(6, 0.4f, -1f, -1f, new Vector3?(at), null));
        }

        private static void GiveCharge(Cooldown cd)
        {
            if (cd.isChargeSkill)
                cd.chargeCount = Mathf.Min(cd.MaxChargeCount, cd.chargeCount + 1);
            else if (!cd.Ready)
                cd.Reset();
        }

        // ---- Mender ----

        // Triage: a heal on a wizard under 30% health is doubled (any wizard with the node covers
        // both in co-op: it's the healer's perk).
        private static Health.HealEnterEventHandler Triage(Player owner) => (ref int amount) =>
        {
            if (!Talents.Enabled || amount <= 0 || owner == null || owner.health == null)
                return;
            float max = owner.health.healthStat.ModifiedValue;
            if (owner.health.CurrentHealthValue <= 0 || owner.health.CurrentHealthValue >= max * TriageBelow)
                return;
            Player[] players = GameController.activePlayers;
            bool any = false;
            if (players != null)
                foreach (Player p in players)
                    if (p != null && Talents.Rank(p, "Triage") > 0)
                        any = true;
            if (!any)
                return;
            amount *= 2;
            Say(owner, "Triage", "Triage: a heal under 30% health was doubled");
        };

        // Undying Light: once a floor, under 10% health starts a 40% heal over 4 s.
        private static void UndyingLight(Player player, State s)
        {
            Health h = player.health;
            float max = Mathf.Max(1f, h.healthStat.ModifiedValue);
            if (!s.undyingUsed && Talents.Rank(player, "UndyingLight") > 0 && h.CurrentHealthValue > 0 && h.CurrentHealthValue < max * UndyingBelow)
            {
                s.undyingUsed = true;
                s.undyingUntil = Time.time + UndyingTime;
                s.undyingPending = 0f;
                log?.LogInfo($"Talents: Undying Light, player {player.playerID + 1} under {UndyingBelow * 100f:0}%: healing {UndyingHeal * 100f:0}% over {UndyingTime:0} s");
            }
            if (Time.time >= s.undyingUntil || h.CurrentHealthValue <= 0)
                return;
            s.undyingPending += UndyingHeal / UndyingTime * max * Time.deltaTime;
            int whole = Mathf.FloorToInt(s.undyingPending);
            if (whole > 0)
            {
                s.undyingPending -= whole;
                Heal(player, whole);
            }
        }

        private static void Heal(Player player, int amount)
        {
            if (amount <= 0)
                return;
            player.health.RestoreHealth(amount, false, false);
        }

        // ---- Gambler: Coin Toss ----

        // A room starts when enemies appear near a wizard after none; the coin is tossed then and
        // holds until a few seconds after the room is clear (so the dropped gold is picked up).
        private static void CoinTossRoom(Player[] players, bool active)
        {
            if (!active)
            {
                coinToss = 0;
                coinRoom = false;
                return;
            }
            bool near = false;
            foreach (Player p in players)
                if (p != null && EnemiesNear(p.transform.position))
                    near = true;
            if (near && !coinRoom)
            {
                coinToss = UnityEngine.Random.value < 0.5f ? 2 : -1;
                log?.LogInfo($"Talents: Coin Toss, {(coinToss == 2 ? "heads: double gold" : "tails: no gold")} in this room");
            }
            if (near)
                coinTossUntil = Time.time + CoinTossAfter;
            else if (coinToss != 0 && Time.time >= coinTossUntil)
                coinToss = 0;
            coinRoom = near;
        }

        private static void HookGold()
        {
            GoldWallet wallet = Player.goldWallet;
            if (wallet == null || wallet == hookedWallet)
                return;
            hookedWallet = wallet;
            wallet.preDepositEventHandlers += CoinTossGold;
        }

        private static void CoinTossGold(ref int amount)
        {
            if (amount <= 0 || !Talents.Enabled || coinToss == 0)
                return;
            amount = coinToss == 2 ? amount * 2 : 0;
        }

        // ---- Helpers ----

        private static bool EnemiesNear(Vector2 at)
        {
            foreach (Collider2D col in Physics2D.OverlapCircleAll(at, RoomRadius))
            {
                Enemy enemy = col != null ? col.GetComponentInParent<Enemy>() : null;
                if (enemy != null && enemy.gameObject.activeInHierarchy && enemy.health != null && enemy.health.CurrentHealthValue > 0)
                    return true;
            }
            return false;
        }

        // One damage mod per wizard for Versatile + Grand Design, changed only when its value does.
        private static void SetDamage(Player player, State s, float value)
        {
            int on = player.GetInstanceID();
            if (Mathf.Approximately(value, s.damageApplied) && s.damageOn == on)
                return;
            string id = Prefix + "Damage";
            StatManager.ModifyAllStatData(new NumVarStatMod(id, 0f), player.skillCategory, StatData.damageStr, null, false);
            if (value > 0f)
                StatManager.ModifyAllStatData(new NumVarStatMod(id, value, 10, VarStatModType.Multiplicative), player.skillCategory, StatData.damageStr);
            s.damageApplied = value;
            s.damageOn = on;
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

        private static void Say(Player player, string node, string message)
        {
            if (said.Add(player.playerID + "/" + node))
                log?.LogInfo($"Talents: player {player.playerID + 1}: {message}");
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
                    log?.LogWarning($"Talents: {what} failed ({e.Message})");
            }
        }
    }
}
