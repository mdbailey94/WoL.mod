using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WoLChaosDepths
{
    /// <summary>
    /// The mechanic nodes and keystones of the Bulwark, Duelist, Arcanist and Gambler trees
    /// (WoL Modder's share; Windrunner and Mender are WoL Player's TalentMechanics.cs). Every rank
    /// comes from Talents.Rank (0 while [Talents] Enabled is off or the node isn't in the worn
    /// cloak's tree). Hooks: each wizard's own damage events (as TalentEffects), the cast event,
    /// one Harmony postfix on Player.SkillState.StartCooldownTimer, and the run's gold wallet.
    /// Built so far:
    /// - Bulwark: Stand Firm, Retaliate, Juggernaut (its armor is in TalentEffects.AddStats),
    ///   Last Stand.
    /// - Arcanist: Echo, Archmage, Surge.
    /// - Gambler: Hot Streak, Double or Nothing.
    /// </summary>
    public static class ArchetypeMechanics
    {
        private const string ModPrefix = "ChaosDepths_AM_";
        private const float TickInterval = 0.25f;
        // Bulwark
        private const float StandFirmCut = 0.15f;
        private const float StillSpeed = 0.3f;           // units a second: slower than this is standing still
        private const float RetaliateCharge = 0.10f;     // of the signature bar
        private const float RetaliateCooldown = 3f;
        private const float LastStandBelow = 0.30f;
        private const float LastStandCut = 0.30f;
        private const float LastStandDamage = 0.20f;
        // Arcanist
        private const float EchoChance = 0.10f;
        private const int ArchmageEvery = 4;
        private const float SurgeDamage = 0.15f;
        private const float SurgeTime = 4f;
        // Duelist
        private const float MomentumCut = 0.3f;          // seconds off every cooldown per crit...
        private const float MomentumSpacing = 0.5f;      // ...at most twice a second
        private const float ExploitBonus = 0.25f;
        private const float AssassinBonus = 0.5f;
        private const float FlurryStep = 0.03f;
        private const float FlurryTime = 4f;
        private const int FlurryMax = 5;
        // Gambler
        private const float HotStreakStep = 0.02f;
        private const int HotStreakMax = 10;
        private const int DoubleMax = 4;

        private class State
        {
            public float retaliateReadyAt;
            public int standardCasts;
            public float surgeUntil;
            public int streak;
            public bool hitThisFloor;
            public bool lastStand;
            public bool juggernaut;
            public float juggernautFxAt;
            public float damageApplied;     // the damage mod on the player now (fraction)
            public int damageOn;            // which Player object it's on
            public float momentumAt;
            public bool riposteReady;
            public readonly List<float> flurry = new List<float>();   // when each stack runs out
            public int critApplied = -1;    // Flurry stacks the crit mod holds now
            public int critOn;
        }

        // Hits Assassin forced to crit, to add its bonus when the crit lands (cleared each tick).
        private static readonly HashSet<AttackInfo> assassinHits = new HashSet<AttackInfo>();

        private static ManualLogSource log;
        private static readonly Dictionary<int, State> states = new Dictionary<int, State>();
        private static readonly Dictionary<int, Health> hooked = new Dictionary<int, Health>();
        private static readonly HashSet<string> failed = new HashSet<string>();
        private static int goldMultiplier = 1;      // Double or Nothing (shared, like the wallet)
        private static bool anyHitThisFloor;
        private static GoldWallet hookedWallet;

        public static void Install(ManualLogSource logger)
        {
            log = logger;
            GameController.levelLoadEventHandlers += OnLevelLoad;
            Health.globalTakeDamageActualHandlers += OnAnyHit;
            Health.globalTakeDamageEnterHandlers += OnHitEnter;
            Attack.globalCritEventHandlers += OnCrit;
            var harmony = new Harmony("mdbailey94.wol.chaosdepths.archetypes");
            harmony.Patch(AccessTools.Method(typeof(Player.SkillState), nameof(Player.SkillState.StartCooldownTimer)),
                postfix: new HarmonyMethod(typeof(ArchetypeMechanics), nameof(AfterCooldownStart)));
            var host = new GameObject("ChaosDepths Archetypes");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<Runner>();
        }

        private static State StateOf(Player player)
        {
            State s;
            if (!states.TryGetValue(player.playerID, out s))
                states[player.playerID] = s = new State();
            return s;
        }

        private class Runner : MonoBehaviour
        {
            private float nextTick;

            private void Update()
            {
                if (!Talents.Enabled || Time.unscaledTime < nextTick)
                    return;
                nextTick = Time.unscaledTime + TickInterval;
                try
                {
                    Tick();
                }
                catch (Exception e)
                {
                    LogOnce("tick", $"Talents: archetype tick failed ({e.Message})");
                }
            }
        }

        // Each floor: Double or Nothing settles (a clean floor doubles the next one's gold).
        private static void OnLevelLoad(string next, string previous)
        {
            try
            {
                Player[] players = GameController.activePlayers;
                bool anyHolder = false;
                if (players != null)
                    foreach (Player p in players)
                        if (p != null && Talents.Rank(p, "DoubleOrNothing") > 0)
                            anyHolder = true;
                if (anyHolder && ChaosDepthsPlugin.InTrials())
                {
                    int before = goldMultiplier;
                    goldMultiplier = anyHitThisFloor ? 1 : Mathf.Min(DoubleMax, goldMultiplier * 2);
                    if (goldMultiplier != before)
                        log?.LogInfo($"Talents: Double or Nothing, gold x{goldMultiplier} on this floor");
                }
                else if (!ChaosDepthsPlugin.InTrials())
                    goldMultiplier = 1;
                anyHitThisFloor = false;
                foreach (State s in states.Values)
                {
                    s.hitThisFloor = false;
                    s.damageOn = 0;   // the player's stats may be rebuilt: put the damage mod back
                    s.critOn = 0;
                    s.flurry.Clear();
                }
            }
            catch (Exception e)
            {
                LogOnce("level", $"Talents: archetype level load failed ({e.Message})");
            }
        }

        private static void Tick()
        {
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            foreach (Player player in players)
            {
                if (player == null || player.health == null)
                    continue;
                Hook(player);
                State s = StateOf(player);
                Health h = player.health;
                float max = Mathf.Max(1f, h.healthStat.ModifiedValue);

                // Last Stand: below 30% health.
                bool low = Talents.Rank(player, "LastStand") > 0 && h.CurrentHealthValue > 0 && h.CurrentHealthValue < max * LastStandBelow;
                if (low != s.lastStand)
                {
                    s.lastStand = low;
                    log?.LogInfo($"Talents: Last Stand {(low ? "on" : "off")} for player {player.playerID + 1}");
                }
                Player p = player;
                Fx(() => KeystoneFx.SetActive(p, "LastStand", low));
                // (s.juggernaut is read by BeforeHit for Juggernaut's look.)

                // Juggernaut: no knockback, no stagger.
                // (Re-set every tick while learned: a new level's wizard has a fresh Health.)
                bool jug = Talents.Rank(player, "Juggernaut") > 0;
                if (jug || s.juggernaut)
                {
                    SetBool(h.ignoreKnockbackVarStat, "Juggernaut", jug);
                    SetBool(h.ignoreHurtVarStat, "Juggernaut", jug);
                }
                s.juggernaut = jug;
                Fx(() => KeystoneFx.SetActive(p, "Archmage", Talents.Rank(p, "Archmage") > 0));

                // Hot Streak, Last Stand and Surge share one damage mod.
                float damage = (low ? LastStandDamage : 0f)
                    + (Talents.Rank(player, "HotStreak") > 0 ? HotStreakStep * s.streak : 0f)
                    + (Time.time < s.surgeUntil ? SurgeDamage : 0f);
                SetDamage(player, s, damage);

                Fx(() => KeystoneFx.SetActive(p, "DoubleOrNothing", Talents.Rank(p, "DoubleOrNothing") > 0 && goldMultiplier > 1));

                // Flurry: stacks run out after FlurryTime; the crit chance mod follows them.
                s.flurry.RemoveAll(t => t <= Time.time);
                if (Talents.Rank(player, "Flurry") <= 0)
                    s.flurry.Clear();
                SetCrit(player, s, s.flurry.Count);
                Fx(() => KeystoneFx.SetActive(p, "Flurry", s.flurry.Count > 0));
                if (Talents.Rank(player, "Riposte") <= 0)
                    s.riposteReady = false;
            }
            assassinHits.Clear();
            HookGold();
        }

        // ---- Hooks ----

        private static void Hook(Player player)
        {
            Health health = player.health;
            int id = health.GetInstanceID();
            Health known;
            if (hooked.TryGetValue(id, out known) && known == health)
                return;
            hooked[id] = health;
            Player owner = player;
            health.takeDamageEventHandlers += (info, attacker) => BeforeHit(owner, info);
            health.takeDamageActualHandlers += (info, attacker) => AfterHit(owner, info);
            player.skillExecEventHandlers += state => OnCast(owner, state);
            // Riposte: an evaded hit readies a sure crit.
            health.evadeEventHandlers += () =>
            {
                if (Talents.Rank(owner, "Riposte") > 0 && !StateOf(owner).riposteReady)
                {
                    StateOf(owner).riposteReady = true;
                    log?.LogInfo($"Talents: Riposte ready, player {owner.playerID + 1}'s next hit crits");
                }
            };
        }

        // ---- Duelist: the wizard's hits on enemies ----

        // Before a hit on an enemy is worked out (crits are rolled after this): Assassin and
        // Riposte make it a sure crit.
        private static void OnHitEnter(AttackInfo info, Entity attacker, Entity hurt)
        {
            try
            {
                if (!Talents.Enabled || info == null || info.damage <= 0 || !(hurt is Enemy) || hurt.health == null)
                    return;
                Player player = PlayerOf(info.skillCategory);
                if (player == null)
                    return;
                State s = StateOf(player);
                if (Talents.Rank(player, "Assassin") > 0 && hurt.health.CurrentHealthValue >= (int)hurt.health.healthStat.ModifiedValue)
                {
                    info.critHitChance = 1f;
                    assassinHits.Add(info);
                }
                if (s.riposteReady)
                {
                    s.riposteReady = false;
                    info.critHitChance = 1f;
                }
            }
            catch (Exception e)
            {
                LogOnce("hit enter", $"Talents: Duelist hit setup failed ({e.Message})");
            }
        }

        // A crit, before its multiplier: Exploit and Assassin add to it; Momentum and Flurry count it.
        private static void OnCrit(AttackInfo info, Entity attacker, Entity hurt)
        {
            try
            {
                if (!Talents.Enabled || info == null || !(hurt is Enemy))
                    return;
                Player player = PlayerOf(info.skillCategory);
                if (player == null)
                    return;
                State s = StateOf(player);
                float bonus = 0f;
                if (Talents.Rank(player, "Exploit") > 0 && Afflicted(hurt))
                    bonus += ExploitBonus;
                if (assassinHits.Remove(info))
                {
                    bonus += AssassinBonus;
                    Fx(() => KeystoneFx.Fire(player, "Assassin"));
                }
                if (bonus > 0f)
                    info.damage = Mathf.CeilToInt(info.damage * (1f + bonus));
                if (Talents.Rank(player, "Momentum") > 0 && Time.time >= s.momentumAt)
                {
                    s.momentumAt = Time.time + MomentumSpacing;
                    CutCooldowns(player, MomentumCut);
                }
                if (Talents.Rank(player, "Flurry") > 0)
                {
                    s.flurry.Add(Time.time + FlurryTime);
                    while (s.flurry.Count > FlurryMax)
                        s.flurry.RemoveAt(0);
                    SetCrit(player, s, s.flurry.Count);
                }
            }
            catch (Exception e)
            {
                LogOnce("crit", $"Talents: Duelist crit failed ({e.Message})");
            }
        }

        // Burning, shocked or frozen (the game's own checks, as its on-status relics use).
        private static bool Afflicted(Entity enemy)
        {
            if (enemy.dotManager != null && (enemy.dotManager.HasDot(BurnDoTEffect.burnStatusStr) || enemy.dotManager.HasDot(ShockDotEffect.shockStatusStr)))
                return true;
            return enemy.fsm != null && enemy.fsm.currentStateName.Contains("Frozen");
        }

        // Momentum: every running cooldown (not charge-based ones) loses this many seconds.
        private static void CutCooldowns(Player player, float seconds)
        {
            if (player.cooldownManager == null)
                return;
            foreach (Cooldown cd in player.cooldownManager.cooldowns.Values)
            {
                if (cd == null || cd.isChargeSkill || cd.Ready)
                    continue;
                float left = cd.RemainingTime - seconds;
                if (left <= 0.05f)
                    player.cooldownManager.ResetCooldown(cd.ID);
                else
                    ChaosStopwatch.Refresh(cd.remainingTimeStopwatchID, left);
            }
        }

        // Flurry's crit chance, one mod per wizard, changed only when the stacks do.
        private static void SetCrit(Player player, State s, int stacks)
        {
            int on = player.GetInstanceID();
            if (stacks == s.critApplied && s.critOn == on)
                return;
            string id = ModPrefix + "Flurry";
            StatManager.ModifyAllStatData(new NumVarStatMod(id, 0f), player.skillCategory, StatData.critChStr, null, false);
            if (stacks > 0)
                StatManager.ModifyAllStatData(new NumVarStatMod(id, FlurryStep * stacks, 10, VarStatModType.Additive), player.skillCategory, StatData.critChStr);
            s.critApplied = stacks;
            s.critOn = on;
        }

        // Before a hit on the wizard lands: the damage cuts.
        private static void BeforeHit(Player player, AttackInfo info)
        {
            try
            {
                if (info == null || info.damage <= 0 || player.health == null)
                    return;
                float cut = 0f;
                if (Talents.Rank(player, "StandFirm") > 0 && (StandingStill(player) || Casting(player)))
                    cut += StandFirmCut;
                if (StateOf(player).lastStand)
                    cut += LastStandCut;
                if (cut > 0f)
                    info.damage = Mathf.Max(1, Mathf.RoundToInt(info.damage * (1f - Mathf.Min(0.9f, cut))));
                if (StateOf(player).juggernaut && Time.time >= StateOf(player).juggernautFxAt && info.knockbackMultiplier > 0f)
                {
                    StateOf(player).juggernautFxAt = Time.time + 1f;
                    Fx(() => KeystoneFx.Fire(player, "Juggernaut"));
                }
            }
            catch (Exception e)
            {
                LogOnce("before hit", $"Talents: archetype hit cut failed ({e.Message})");
            }
        }

        // After a hit on the wizard: Retaliate, and the Gambler's streaks break.
        private static void AfterHit(Player player, AttackInfo info)
        {
            try
            {
                if (info == null || info.damage <= 0)
                    return;
                State s = StateOf(player);
                if (Talents.Rank(player, "Retaliate") > 0 && Time.time >= s.retaliateReadyAt && player.health.CurrentHealthValue > 0)
                {
                    s.retaliateReadyAt = Time.time + RetaliateCooldown;
                    player.OverdriveProgress = Mathf.Min(Player.overdriveMaxValue, player.OverdriveProgress + Player.overdriveMaxValue * RetaliateCharge);
                }
                if (s.streak > 0)
                {
                    log?.LogInfo($"Talents: Hot Streak lost at {s.streak} kill(s), player {player.playerID + 1}");
                    s.streak = 0;
                }
                s.hitThisFloor = true;
                if (!anyHitThisFloor && goldMultiplier > 1 && Talents.Rank(player, "DoubleOrNothing") > 0)
                {
                    Fx(() => KeystoneFx.Fire(player, "DoubleOrNothing"));
                    log?.LogInfo("Talents: Double or Nothing broken by a hit (next floor back to x1)");
                }
                anyHitThisFloor = true;
            }
            catch (Exception e)
            {
                LogOnce("after hit", $"Talents: archetype after-hit failed ({e.Message})");
            }
        }

        // Any hit anywhere: a wizard's kill feeds Hot Streak.
        private static void OnAnyHit(AttackInfo info, Entity attacker, Entity hurt)
        {
            try
            {
                if (!Talents.Enabled || info == null || !(hurt is Enemy) || hurt.health == null || hurt.health.CurrentHealthValue > 0)
                    return;
                Player killer = PlayerOf(info.skillCategory);
                if (killer == null || Talents.Rank(killer, "HotStreak") <= 0)
                    return;
                State s = StateOf(killer);
                if (s.streak < HotStreakMax)
                    s.streak++;
            }
            catch (Exception e)
            {
                LogOnce("kill", $"Talents: Hot Streak kill failed ({e.Message})");
            }
        }

        // A cast: Surge on the signature (Cataclysm's look is TalentEffects').
        private static void OnCast(Player player, Player.SkillState state)
        {
            try
            {
                if (state == null || !state.isUltimate || Talents.Rank(player, "Surge") <= 0)
                    return;
                StateOf(player).surgeUntil = Time.time + SurgeTime;
                Player.SkillState dash = player.GetDashSkill();
                if (dash != null && player.cooldownManager != null && player.cooldownManager.cooldowns.ContainsKey(dash.skillID))
                    player.cooldownManager.ResetCooldown(dash.skillID);
                log?.LogInfo($"Talents: Surge, player {player.playerID + 1}'s dash reset, +15% damage for {SurgeTime:0} s");
            }
            catch (Exception e)
            {
                LogOnce("surge", $"Talents: Surge failed ({e.Message})");
            }
        }

        // After a cooldown starts: Echo and Archmage give it back (standard arcana only: not the
        // basic, the dash or a signature cast).
        private static void AfterCooldownStart(Player.SkillState __instance)
        {
            try
            {
                Player player = __instance != null ? __instance.parent : null;
                if (!Talents.Enabled || player == null || __instance.isBasic || __instance.isDash || __instance.isUltimate
                    || player.cooldownManager == null || !player.cooldownManager.cooldowns.ContainsKey(__instance.skillID))
                    return;
                bool free = false;
                string why = null;
                if (Talents.Rank(player, "Archmage") > 0)
                {
                    State s = StateOf(player);
                    s.standardCasts++;
                    Player p = player;
                    if (s.standardCasts % ArchmageEvery == 0)
                    {
                        free = true;
                        why = "Archmage";
                        Fx(() => KeystoneFx.Fire(p, "Archmage"));
                    }
                }
                if (!free && Talents.Rank(player, "Echo") > 0 && UnityEngine.Random.value < EchoChance)
                {
                    free = true;
                    why = "Echo";
                }
                if (!free)
                    return;
                player.cooldownManager.ResetCooldown(__instance.skillID);
                log?.LogInfo($"Talents: {why}, {__instance.skillID} has no cooldown this cast (player {player.playerID + 1})");
            }
            catch (Exception e)
            {
                LogOnce("cooldown", $"Talents: Echo/Archmage failed ({e.Message})");
            }
        }

        // ---- Gold: Double or Nothing ----

        private static void HookGold()
        {
            GoldWallet wallet = Player.goldWallet;
            if (wallet == null || wallet == hookedWallet)
                return;
            hookedWallet = wallet;
            wallet.preDepositEventHandlers += MoreGold;
        }

        private static void MoreGold(ref int amount)
        {
            if (amount > 0 && goldMultiplier > 1 && Talents.Enabled)
                amount *= goldMultiplier;
        }

        // ---- Helpers ----

        private static bool StandingStill(Player player) =>
            player.rigidbody2D == null || player.rigidbody2D.velocity.magnitude < StillSpeed;

        private static bool Casting(Player player)
        {
            var state = player.fsm != null ? player.fsm.currentState as Player.SkillState : null;
            return state != null && !state.isDash;
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

        private static void SetBool(BoolVarStat stat, string name, bool on)
        {
            if (stat == null)
                return;
            stat.RemoveMod(ModPrefix + name);
            if (on)
                stat.AddMod(new BoolVarStatMod(ModPrefix + name, true));
        }

        // One damage mod per wizard for the conditional bonuses, changed only when its value does.
        private static void SetDamage(Player player, State s, float value)
        {
            int on = player.GetInstanceID();
            if (Mathf.Approximately(value, s.damageApplied) && s.damageOn == on)
                return;
            string id = ModPrefix + "Damage";
            StatManager.ModifyAllStatData(new NumVarStatMod(id, 0f), player.skillCategory, StatData.damageStr, null, false);
            if (value > 0f)
                StatManager.ModifyAllStatData(new NumVarStatMod(id, value, 10, VarStatModType.Multiplicative), player.skillCategory, StatData.damageStr);
            s.damageApplied = value;
            s.damageOn = on;
        }

        private static void Fx(Action show)
        {
            try
            {
                show();
            }
            catch (Exception e)
            {
                LogOnce("fx", $"Talents: keystone visual failed ({e.Message})");
            }
        }

        private static void LogOnce(string key, string message)
        {
            if (failed.Add(key))
                log?.LogWarning(message);
        }
    }
}
