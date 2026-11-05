using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace WoLChaosDepths
{
    /// <summary>
    /// What the Apprentice tree's talents do (Phase 1a; ranks from Talents.Rank, all 0 while
    /// [Talents] Enabled is off, so nothing here changes the game then). The talents apply to the
    /// cloak the wizard is wearing (Talents reads player.outfitID).
    /// - Sturdy, Keen, Quick Feet, Steady Hands: plain numbers, added to the gear totals that
    ///   StatMods.Apply turns into the game's stat mods (so a cloak change or a respec reapplies
    ///   them on the next half-second tick, like new gear).
    /// - Second Wind: after a hit, the game's own heal restores a share of max health over a few
    ///   seconds, then waits out its cooldown.
    /// - Pocket Change: more gold from every deposit into the run's gold wallet (shared in co-op,
    ///   so the best rank among the wizards counts).
    /// - Graduate: entering the trials, a random relic drops at the wizard's feet (LootManager's own
    ///   drop, no curse).
    /// - Survivor: once per floor, a hit that would kill leaves the wizard at 1 health instead,
    ///   with a flash and a notice.
    /// </summary>
    public static class TalentEffects
    {
        // Per point, as fractions (from the Talent Trees tab, approved by Matt).
        private const float SturdyHealth = 0.04f;
        private const float KeenDamage = 0.03f;
        private const float QuickFeetSpeed = 0.03f;
        private const float SteadyHandsCooldown = -0.05f;   // negative = shorter, as gear's Cooldown affix
        private const float PocketChangeGold = 0.15f;
        private const float FortuneGold = 0.08f;
        private const float SecondWindShare = 0.02f;        // of max health...
        private const float SecondWindTime = 3f;            // ...over this long...
        private const float SecondWindCooldown = 6f;        // ...at most this often
        private const float TickInterval = 0.25f;

        private class Wind
        {
            public float remaining;   // health still to restore (fractional)
            public float pending;     // restored so far but not yet a whole point
            public float rate;        // per second
            public float readyAt;
        }

        private static ManualLogSource log;
        private static readonly Dictionary<int, Health> hooked = new Dictionary<int, Health>();
        private static readonly Dictionary<int, Wind> winds = new Dictionary<int, Wind>();
        private static readonly HashSet<int> survived = new HashSet<int>();   // players whose Survivor is spent this floor
        private static GoldWallet hookedWallet;
        private static bool wasInTrials;

        public static void Install(ManualLogSource logger)
        {
            log = logger;
            GameController.levelLoadEventHandlers += OnLevelLoad;
            var host = new GameObject("ChaosDepths Talents");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<Runner>();
            try
            {
                ArchetypeMechanics.Install(logger);
            }
            catch (Exception e)
            {
                logger.LogError($"Talents: Bulwark/Duelist/Arcanist/Gambler mechanics failed to install ({e.Message})");
            }
            try
            {
                TalentMechanics.Install(logger);
            }
            catch (Exception e)
            {
                logger.LogError($"Talents: Windrunner/Mender mechanics failed to install ({e.Message})");
            }
        }

        /// <summary>Adds the stat talents to the totals StatMods.Apply builds from gear.</summary>
        public static void AddStats(Player player, Dictionary<string, float> totals)
        {
            if (!Talents.Enabled || player == null)
                return;
            // Rank is 0 for a node outside the worn cloak's tree, so every tree's stat nodes can be
            // summed here and only the worn one's count.
            Add(totals, "Health", SturdyHealth * Talents.Rank(player, "Sturdy"));
            Add(totals, "Damage", KeenDamage * Talents.Rank(player, "Keen"));
            Add(totals, "MoveSpeed", QuickFeetSpeed * Talents.Rank(player, "QuickFeet"));
            Add(totals, "Cooldown", SteadyHandsCooldown * Talents.Rank(player, "SteadyHands"));
            // The elemental trees (approved 2026-11-02).
            Add(totals, "Health", 0.04f * Talents.Rank(player, "HotBlood") + 0.05f * Talents.Rank(player, "Bedrock")
                + 0.04f * Talents.Rank(player, "Tide"));
            Add(totals, "MoveSpeed", 0.03f * (Talents.Rank(player, "Flicker") + Talents.Rank(player, "Tailwind")
                + Talents.Rank(player, "Quickened") + Talents.Rank(player, "Glide")));
            Add(totals, "CritChance", 0.05f * Talents.Rank(player, "Conductor"));
            Add(totals, "Knockback", 0.25f * Talents.Rank(player, "Updraft"));
            // The stat-archetype trees (approved 2026-11-02 17:12, the default mapping).
            Add(totals, "DamageTaken", -0.02f * Talents.Rank(player, "IronHide") - 0.10f * Talents.Rank(player, "Juggernaut")); // Bulwark
            Add(totals, "Health", 0.04f * Talents.Rank(player, "ThickSkin"));
            Add(totals, "Healing", 0.08f * Talents.Rank(player, "Remedy"));
            Add(totals, "CritChance", 0.02f * Talents.Rank(player, "KeenEye"));                         // Duelist
            Add(totals, "CritDamage", 0.08f * Talents.Rank(player, "DeepCuts"));
            Add(totals, "Cooldown", -0.03f * Talents.Rank(player, "Focus"));                            // Arcanist
            Add(totals, "SignatureDamage", 0.4f * Talents.Rank(player, "Cataclysm"));
            Add(totals, "MoveSpeed", 0.03f * Talents.Rank(player, "Fleet"));                            // Windrunner
            Add(totals, "HealCrit", 0.03f * Talents.Rank(player, "LuckyHeal"));                         // Mender
            // Gambler All In (2026-11-03 rework): gold-scaled damage, in ArchetypeMechanics.
            Element(player, totals, "Fire", "Kindling", "HeatSink");
            Element(player, totals, "Air", "SharpGust", "SecondBreath");
            Element(player, totals, "Earth", "HeavyHands", "Quarry");
            Element(player, totals, "Lightning", "Static", "Capacitor");
            Element(player, totals, "Water", "ColdSnap", "Riptide");
        }

        // +4% damage per point and -6% cooldowns on that element's arcana only (StatMods applies
        // the "<Element>Damage" / "<Element>Cooldown" keys to arcana of that element).
        private static void Element(Player player, Dictionary<string, float> totals, string element, string damageNode, string cooldownNode)
        {
            Add(totals, element + "Damage", 0.04f * Talents.Rank(player, damageNode));
            Add(totals, element + "Cooldown", -0.06f * Talents.Rank(player, cooldownNode));
        }

        private static void Add(Dictionary<string, float> totals, string key, float value)
        {
            if (Mathf.Approximately(value, 0f))
                return;
            float current;
            totals.TryGetValue(key, out current);
            totals[key] = current + value;
        }

        private class Runner : MonoBehaviour
        {
            private float nextTick;

            private void Update()
            {
                if (!Talents.Enabled)
                    return;
                try
                {
                    Heal(Time.deltaTime);
                    if (Time.unscaledTime < nextTick)
                        return;
                    nextTick = Time.unscaledTime + TickInterval;
                    Tick();
                }
                catch (Exception e)
                {
                    LogOnce("tick", $"Talents: effects tick failed ({e.Message})");
                }
            }
        }

        // Each floor gives Survivor back; leaving the house for the trials is a run's start.
        // (hooked isn't cleared: a wizard that carries over keeps its one set of handlers, and a
        // new wizard's Health is a new object, hooked on the next tick.)
        private static void OnLevelLoad(string next, string previous)
        {
            survived.Clear();
            winds.Clear();
            Fx(() => KeystoneFx.Clear());
        }

        // Keystone visuals (WoL Player's KeystoneFx.cs); a failure there never stops an effect.
        private static void Fx(Action show)
        {
            try
            {
                show();
            }
            catch (Exception e)
            {
                LogOnce("keystone fx", $"Talents: keystone visual failed ({e.Message})");
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
                // The lasting keystone looks that are on whenever the keystone is learned.
                Player p = player;
                Fx(() => KeystoneFx.SetActive(p, "AllIn", Talents.Rank(p, "AllIn") > 0));
            }
            HookGold(players);
            bool inTrials = ChaosDepthsPlugin.InTrials();
            if (inTrials && !wasInTrials)
                Graduate(players);
            wasInTrials = inTrials;
        }

        // ---- Second Wind and Survivor: the wizard's own damage events ----

        private static void Hook(Player player)
        {
            Health health = player.health;
            int id = health.GetInstanceID();
            Health known;
            if (hooked.TryGetValue(id, out known) && known == health)
                return;
            hooked[id] = health;
            Player owner = player;
            // Before the hit lands (the same AttackInfo the game then subtracts): Survivor.
            health.takeDamageEventHandlers += (info, attacker) => BeforeHit(owner, info);
            // After it landed: Second Wind.
            health.takeDamageActualHandlers += (info, attacker) => AfterHit(owner, info);
            // Cataclysm's look on a signature cast (its +40% is a stat, in AddStats).
            player.skillExecEventHandlers += state =>
            {
                if (state != null && state.isUltimate && Talents.Rank(owner, "Cataclysm") > 0)
                    Fx(() => KeystoneFx.Fire(owner, "Cataclysm"));
            };
        }

        private static void BeforeHit(Player player, AttackInfo info)
        {
            try
            {
                Health h = player != null ? player.health : null;
                if (h == null || info == null || info.damage <= 0 || h.CurrentHealthValue <= 0 || h.CurrentHealthValue > info.damage)
                    return;
                // Hits the game stops anyway don't use it up.
                if (h.invulnerable || h.GuardNextAtk || h.CurrentShieldValue > 0 || h.deathProof.CurrentValue)
                    return;
                if (Talents.Rank(player, "Survivor") <= 0 || !survived.Add(player.playerID))
                    return;
                info.damage = Mathf.Max(0, h.CurrentHealthValue - 1);
                Effect(() => PoolManager.GetPoolItem<HitSparkEmitter>().EmitSingle(HitSparkType.Large, player.transform.position, player.transform.position));
                Effect(() => CameraController.ShakeCamera(0.5f, false));
                ChaosDepthsPlugin.Notice($"Player {player.playerID + 1}: SURVIVOR!", 2f);
                Fx(() => KeystoneFx.Fire(player, "Survivor"));
                log?.LogInfo($"Talents: Survivor kept player {player.playerID + 1} at 1 health (used for this floor)");
            }
            catch (Exception e)
            {
                LogOnce("survivor", $"Talents: Survivor failed ({e.Message})");
            }
        }

        private static void AfterHit(Player player, AttackInfo info)
        {
            try
            {
                Health h = player != null ? player.health : null;
                if (h == null || h.CurrentHealthValue <= 0 || info == null || info.damage <= 0)
                    return;
                if (Talents.Rank(player, "SecondWind") <= 0)
                    return;
                Wind wind;
                if (!winds.TryGetValue(player.playerID, out wind))
                    winds[player.playerID] = wind = new Wind();
                if (Time.time < wind.readyAt)
                    return;
                float total = SecondWindShare * h.healthStat.ModifiedValue;
                wind.remaining = total;
                wind.pending = 0f;
                wind.rate = total / SecondWindTime;
                wind.readyAt = Time.time + SecondWindCooldown;
                log?.LogInfo($"Talents: Second Wind, player {player.playerID + 1} regains {total:0.#} health over {SecondWindTime:0} s");
            }
            catch (Exception e)
            {
                LogOnce("second wind", $"Talents: Second Wind failed ({e.Message})");
            }
        }

        // Second Wind's heal, a whole point of health at a time through the game's own heal.
        private static void Heal(float dt)
        {
            if (winds.Count == 0)
                return;
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            foreach (Player player in players)
            {
                Wind wind;
                if (player == null || player.health == null || !winds.TryGetValue(player.playerID, out wind) || wind.remaining <= 0f)
                    continue;
                if (player.health.CurrentHealthValue <= 0)
                {
                    wind.remaining = 0f;
                    continue;
                }
                float step = Mathf.Min(wind.remaining, wind.rate * dt);
                wind.remaining -= step;
                wind.pending += step;
                // The last fraction rounds up, so the full share is always given.
                int whole = wind.remaining <= 0.0001f ? Mathf.CeilToInt(wind.pending - 0.0001f) : Mathf.FloorToInt(wind.pending);
                if (whole > 0)
                {
                    wind.pending = Mathf.Max(0f, wind.pending - whole);
                    player.health.RestoreHealth(whole, false, false);
                }
                if (wind.remaining <= 0.0001f)
                    wind.remaining = wind.pending = 0f;
            }
        }

        // ---- Pocket Change: the run's gold wallet (Player.goldWallet, one for everyone) ----

        private static void HookGold(Player[] players)
        {
            GoldWallet wallet = Player.goldWallet;
            if (wallet == null || wallet == hookedWallet)
                return;
            hookedWallet = wallet;
            wallet.preDepositEventHandlers += MoreGold;
        }

        private static void MoreGold(ref int amount)
        {
            try
            {
                if (amount <= 0 || !Talents.Enabled)
                    return;
                // Pocket Change (Apprentice) +15% a point, Fortune (Gambler) +8% a point; the
                // best wizard's bonus counts.
                float best = 0f;
                Player[] players = GameController.activePlayers;
                if (players != null)
                    foreach (Player player in players)
                        if (player != null)
                            best = Mathf.Max(best, PocketChangeGold * Talents.Rank(player, "PocketChange") + FortuneGold * Talents.Rank(player, "Fortune"));
                if (best > 0f)
                    amount = Mathf.RoundToInt(amount * (1f + best));
            }
            catch (Exception e)
            {
                LogOnce("gold", $"Talents: Pocket Change failed ({e.Message})");
            }
        }

        // ---- Graduate: a relic at the start of a run ----

        private static void Graduate(Player[] players)
        {
            foreach (Player player in players)
            {
                if (player == null || Talents.Rank(player, "Graduate") <= 0)
                    continue;
                try
                {
                    Vector3 at = player.transform.position + new Vector3(0f, -1.2f, 0f);
                    LootManager.DropItem(at, 1, string.Empty, false, 0f);
                    Player p = player;
                    Fx(() => KeystoneFx.Fire(p, "Graduate"));
                    log?.LogInfo($"Talents: Graduate dropped a relic for player {player.playerID + 1}");
                }
                catch (Exception e)
                {
                    LogOnce("graduate", $"Talents: Graduate's relic failed ({e.Message})");
                }
            }
        }

        private static readonly HashSet<string> failed = new HashSet<string>();

        private static void Effect(Action effect)
        {
            try
            {
                effect();
            }
            catch
            {
            }
        }

        private static void LogOnce(string key, string message)
        {
            if (failed.Add(key))
                log?.LogWarning(message);
        }
    }
}
