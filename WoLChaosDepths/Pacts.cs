using System;
using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    // #75 PACT KEYSTONES (Talent Trees tab rows 72-82, approved by Matt 2026-11-03 8:47am): a third
    // keystone in each tree, strong with a cost felt every run. Still one keystone per tree, so a
    // pact means giving up both safe ones. Each pact's cost shows on the HUD while it's learned.
    //   Borrowed Time (Apprentice): cooldowns -30%; max health -1% every 30 s spent in a run, to 40%.
    //   Bastion (Bulwark): +25% armor, a hit takes at most 20% of max health, no knockback; move
    //     speed -20% except for 2 s after each dash.
    //   Marked for Death (Duelist): +60% crit damage, crits mark the target (a crosshair); evade is 0
    //     for 6 s after each crit.
    //   Overchannel (Arcanist): arcana +40% damage and cast speed; each cast adds 1 s more cooldown
    //     than the last (+1, +2, +3 ... s) until 3 s pass without a cast. Basic and dash are spared.
    //   Momentum Engine (Windrunner): +2% damage per 1% move speed above base, dash cooldown halved;
    //     every hit taken roots you for 0.5 s.
    //   Martyr (Mender): the heals you pass to your partner (Lifebloom, Shared Draught) are doubled,
    //     and you regenerate 4%/s in cleared rooms; your max health is halved.
    //   Double Down (Gambler): relics in shops cost half; every relic bought also brings a random
    //     curse relic.
    // Only while talents are on (the Mod Menu's Phase One). Each logs "Pact: ..." the first time it
    // acts on a floor.
    public static class Pacts
    {
        private const string Prefix = "ChaosDepths_PACT_";
        private const float OverchannelStep = 1f, OverchannelReset = 3f;
        private const float BorrowedEvery = 30f, BorrowedStep = 0.01f, BorrowedFloor = 0.40f;
        private const float BastionSlow = -0.20f, BastionDashGrace = 2f, BastionCap = 0.20f;
        private const float MarkedCritDamage = 0.60f, MarkedEvadeOff = 6f;
        private const float RootTime = 0.5f;
        private const float MartyrRegen = 0.04f, ClearRadius = 15f, ClearDelay = 1f;

        private class State
        {
            public float runTime, lastDash = -10f, evadeOffUntil = -1f, rootUntil = -1f, lastCast = -10f, clearSince = -1f, regenPending;
            public int streak;
            public bool wasInRun;
            public int itemsSeen = -1;
        }

        private static readonly Dictionary<int, State> states = new Dictionary<int, State>();
        private static readonly Dictionary<int, Health> hooked = new Dictionary<int, Health>();
        private static readonly Dictionary<KeyValuePair<object, string>, float> applied = new Dictionary<KeyValuePair<object, string>, float>();
        private static readonly HashSet<string> said = new HashSet<string>();
        private static float lastWithdraw = -10f, nextStoreScan;
        private static GoldWallet hookedWallet;

        public static void Install()
        {
            var host = new GameObject("ChaosDepths Pacts");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<Runner>();
            GameController.levelLoadEventHandlers += (next, previous) => said.Clear();
            Attack.globalCritEventHandlers += OnCrit;
        }

        private static bool Has(Player p, string pact) => p != null && Talents.Rank(p, pact) > 0;

        // The heals a Martyr passes to the partner are doubled (TalentMechanics' Lifebloom,
        // CloakPerks' Shared Draught ask for this).
        public static float PartnerHealScale(Player healer) => Has(healer, "Martyr") ? 2f : 1f;

        private static State StateOf(Player p)
        {
            State st;
            if (!states.TryGetValue(p.playerID, out st))
                states[p.playerID] = st = new State();
            return st;
        }

        private class Runner : MonoBehaviour
        {
            private void Update()
            {
                if (!Talents.Enabled)
                    return;
                Guard("tick", () => Tick(Time.deltaTime));
            }

            private void OnGUI()
            {
                if (Talents.Enabled)
                    Guard("hud", Hud);
            }
        }

        private static void Tick(float dt)
        {
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            bool inRun = ChaosDepthsPlugin.InTrialsNow();
            HookWallet();
            foreach (Player p in players)
            {
                if (p == null || p.health == null || p.movement == null)
                    continue;
                Hook(p);
                State st = StateOf(p);
                if (inRun && !st.wasInRun)
                    st.runTime = 0f;   // a new run
                st.wasInRun = inRun;
                if (inRun)
                    st.runTime += dt;

                // Borrowed Time.
                bool borrowed = Has(p, "BorrowedTime");
                SkillMods(p, StatData.cdStr, "Borrowed", borrowed ? -0.30f : 0f, true, all: true);
                float lost = borrowed && inRun ? Mathf.Min(1f - BorrowedFloor, BorrowedStep * Mathf.Floor(st.runTime / BorrowedEvery)) : 0f;
                // Martyr: max health halved.
                float martyr = Has(p, "Martyr") ? -0.5f : 0f;
                SetMod(p.health.healthStat, "MaxHealth", -lost + martyr, true);

                // Bastion.
                bool bastion = Has(p, "Bastion");
                SetMod(p.health.armorStat, "Armor", bastion ? 0.25f : 0f, false);
                float slow = bastion && Time.time - st.lastDash > BastionDashGrace ? BastionSlow : 0f;
                // Momentum Engine's root.
                if (Time.time < st.rootUntil)
                    slow = -1f;
                SetMod(p.movement.moveSpeedStat, "Speed", slow, true);

                // Marked for Death.
                bool marked = Has(p, "MarkedForDeath");
                SkillMods(p, StatData.critDmgStr, "Marked", marked ? MarkedCritDamage : 0f, false, all: true);
                SetMod(p.health.evadeStat, "EvadeOff", marked && Time.time < st.evadeOffUntil ? -1f : 0f, true);

                // Overchannel.
                bool over = Has(p, "Overchannel");
                if (Time.time - st.lastCast > OverchannelReset)
                    st.streak = 0;
                SkillMods(p, StatData.damageStr, "Overchannel", over ? 0.40f : 0f, true, all: false);
                SkillMods(p, StatData.cdStr, "OverchannelCd", over ? OverchannelStep * (st.streak + 1) : 0f, false, all: false);
                float anim = over ? 0.40f : 0f;

                // Momentum Engine.
                bool engine = Has(p, "MomentumEngine");
                float speedBonus = 0f;
                if (engine && p.movement.moveSpeedStat.BaseValue > 0.01f)
                    speedBonus = Mathf.Max(0f, p.movement.moveSpeedStat.ModifiedValue / p.movement.moveSpeedStat.BaseValue - 1f);
                SkillMods(p, StatData.damageStr, "Engine", engine ? 2f * speedBonus : 0f, true, all: true);
                Player.SkillState dash = p.GetDashSkill();
                if (dash != null && dash.skillData != null)
                    SetStat(dash.skillData, StatData.cdStr, "EngineDash", engine ? -0.5f : 0f, true);
                SetAnim(p, "Overchannel", anim);

                // The pacts' lasting looks (WoL Art's #76).
                Player me = p;
                bool slowed = bastion && Time.time - st.lastDash > BastionDashGrace;
                Fx(() => KeystoneFx.SetActive(me, "Pact_BorrowedTime", borrowed && inRun));
                Fx(() => KeystoneFx.SetActive(me, "Pact_Bastion", slowed));
                Fx(() => KeystoneFx.SetActive(me, "Pact_Overchannel", over && st.streak > 0));
                Fx(() => KeystoneFx.SetActive(me, "Pact_MomentumEngine", engine && Time.time < st.rootUntil));
                Fx(() => KeystoneFx.SetActive(me, "Pact_Martyr", Has(me, "Martyr")));

                // Martyr's regen in cleared rooms.
                Martyr(p, st, inRun, dt);
                // Double Down: half-price relics and the curse on purchase.
                DoubleDown(p, st);
            }
        }

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
            health.takeDamageEventHandlers += (info, attacker) => Guard("hurt", () => OnHurt(me, info));
        }

        private static void OnCast(Player p, Player.SkillState state)
        {
            if (!Talents.Enabled || state == null)
                return;
            State st = StateOf(p);
            Player.SkillState dash = p.GetDashSkill(), basic = p.GetBasicSkill();
            if (dash != null && state.skillID == dash.skillID)
            {
                st.lastDash = Time.time;
                return;
            }
            if (basic != null && state.skillID == basic.skillID)
                return;
            if (Has(p, "Overchannel"))
            {
                // This cast's cooldown already has the +streak; the next one gets one more.
                st.streak++;
                st.lastCast = Time.time;
                Say(p, "Overchannel", $"Overchannel: casts in a row add +1 s cooldown each (now +{st.streak + 1} s)");
            }
        }

        // A hit on this wizard: Bastion caps it and stops the knockback; Momentum Engine roots.
        private static void OnHurt(Player p, AttackInfo info)
        {
            if (!Talents.Enabled || info == null || info.damage <= 0)
                return;
            if (Has(p, "Bastion"))
            {
                int cap = Mathf.Max(1, Mathf.RoundToInt(BastionCap * p.health.healthStat.ModifiedValue));
                if (info.damage > cap)
                {
                    info.damage = cap;
                    Say(p, "Bastion", $"Bastion: a hit capped at {cap}");
                }
                info.knockbackMultiplier = 0f;
                info.knockbackVector = Vector2.zero;
            }
            if (Has(p, "MomentumEngine"))
            {
                StateOf(p).rootUntil = Time.time + RootTime;
                Say(p, "MomentumEngine", "Momentum Engine: rooted 0.5 s by a hit");
            }
        }

        // Marked for Death: the wizard's crit marks the enemy and leaves the wizard unable to evade.
        private static void OnCrit(AttackInfo info, Entity attacker, Entity hurt)
        {
            if (!Talents.Enabled || info == null || !(hurt is Enemy))
                return;
            Guard("crit", () =>
            {
                Player p = PlayerOf(info.skillCategory);
                if (p == null || !Has(p, "MarkedForDeath"))
                    return;
                StateOf(p).evadeOffUntil = Time.time + MarkedEvadeOff;
                KeystoneFx.Fire(hurt, "Pact_MarkedForDeath");
                Say(p, "MarkedForDeath", "Marked for Death: crit marked the target, evade 0 for 6 s");
            });
        }

        private static void Martyr(Player p, State st, bool inRun, float dt)
        {
            if (!inRun || !Has(p, "Martyr") || p.health.CurrentHealthValue <= 0)
            {
                st.clearSince = -1f;
                return;
            }
            foreach (Collider2D col in Physics2D.OverlapCircleAll(p.transform.position, ClearRadius))
            {
                Enemy e = col != null ? col.GetComponentInParent<Enemy>() : null;
                if (e != null && e.health != null && e.health.CurrentHealthValue > 0)
                {
                    st.clearSince = -1f;
                    return;
                }
            }
            if (st.clearSince < 0f)
                st.clearSince = Time.time;
            if (Time.time - st.clearSince < ClearDelay)
                return;
            st.regenPending += MartyrRegen * p.health.healthStat.ModifiedValue * dt;
            int whole = Mathf.FloorToInt(st.regenPending);
            if (whole > 0)
            {
                st.regenPending -= whole;
                p.health.RestoreHealth(whole, false, false);
                Say(p, "Martyr", "Martyr: regenerating 4%/s in a cleared room");
            }
        }

        // Double Down: every relic for gold in a shop is half price while a wizard has the pact; a
        // relic that turns up in the wizard's inventory right after gold was spent was bought, and
        // brings a random curse relic.
        private static void DoubleDown(Player p, State st)
        {
            bool on = Has(p, "DoubleDown");
            if (Time.unscaledTime >= nextStoreScan)
            {
                nextStoreScan = Time.unscaledTime + 0.5f;
                foreach (ItemStoreItem item in UnityEngine.Object.FindObjectsOfType<ItemStoreItem>())
                    if (item != null && !item.usePlatinumCost && item.costStat != null)
                    {
                        SetMod(item.costStat, "DoubleDown", on ? -0.5f : 0f, true);
                        item.UpdatePrice();   // the shown price follows the stat (as the game's discounts do)
                    }
            }
            if (p.inventory == null)
                return;
            List<string> ids = p.inventory.GetItemIDList();
            int count = ids != null ? ids.Count : 0;
            if (on && st.itemsSeen >= 0 && count > st.itemsSeen && Time.time - lastWithdraw < 0.5f)
            {
                List<string> curses = LootManager.cursedItemIDList;
                if (curses != null && curses.Count > 0)
                {
                    var pick = new List<string>();
                    foreach (string c in curses)
                        if (!p.inventory.ContainsItem(c))
                            pick.Add(c);
                    if (pick.Count > 0)
                    {
                        string curse = pick[UnityEngine.Random.Range(0, pick.Count)];
                        p.inventory.AddItem(curse, true, true);
                        count++;
                        Player me = p;
                        Fx(() => KeystoneFx.Fire(me, "Pact_DoubleDown"));
                        Say(p, "DoubleDown" + curse, $"Double Down: the bought relic brought the curse {curse}");
                    }
                }
                lastWithdraw = -10f;
            }
            st.itemsSeen = count;
        }

        private static void HookWallet()
        {
            GoldWallet wallet = Player.goldWallet;
            if (wallet == null || wallet == hookedWallet)
                return;
            hookedWallet = wallet;
            wallet.withdrawEventHandlers += amount =>
            {
                if (!ArchetypeMechanics.Spilling)   // All In's spill isn't a purchase (review fix 0.35.4)
                    lastWithdraw = Time.time;
            };
        }

        // ---- The HUD marker: each wizard's pact cost, top left, while learned ----

        private static void Hud()
        {
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            int s = Mathf.Max(1, Mathf.FloorToInt(Screen.height / 360f));
            GUIStyle style = PixelUI.Style(Mathf.Max(10, 6 * s), new Color32(0xe0, 0x5a, 0x5a, 0xff));
            float y = 40 * s;
            foreach (Player p in players)
            {
                if (p == null)
                    continue;
                string line = CostLine(p, StateOf(p));
                if (line == null)
                    continue;
                string text = (players.Length > 1 ? $"P{p.playerID + 1} " : "") + "PACT: " + line;
                var size = style.CalcSize(new GUIContent(text));
                var box = new Rect(8 * s, y, size.x + 8 * s, size.y + 4 * s);
                PixelUI.Fill(box, new Color(0f, 0f, 0f, 0.55f));
                GUI.Label(new Rect(box.x + 4 * s, box.y + 2 * s, size.x, size.y), text, style);
                y += box.height + 2 * s;
            }
        }

        private static string CostLine(Player p, State st)
        {
            if (Has(p, "BorrowedTime"))
            {
                float lost = Mathf.Min(1f - BorrowedFloor, BorrowedStep * Mathf.Floor(st.runTime / BorrowedEvery));
                return $"BORROWED TIME  max health {Mathf.RoundToInt((1f - lost) * 100f)}%";
            }
            if (Has(p, "Bastion"))
                return Time.time - st.lastDash > BastionDashGrace ? "BASTION  -20% speed" : "BASTION  free to move";
            if (Has(p, "MarkedForDeath"))
                return Time.time < st.evadeOffUntil ? $"MARKED  no evade {Mathf.CeilToInt(st.evadeOffUntil - Time.time)}s" : "MARKED  evade ok";
            if (Has(p, "Overchannel"))
                return $"OVERCHANNEL  next cast +{OverchannelStep * (st.streak + 1):0} s cooldown";
            if (Has(p, "MomentumEngine"))
                return Time.time < st.rootUntil ? "MOMENTUM ENGINE  ROOTED" : "MOMENTUM ENGINE  hits root you";
            if (Has(p, "Martyr"))
                return "MARTYR  max health halved";
            if (Has(p, "DoubleDown"))
                return "DOUBLE DOWN  every relic brings a curse";
            return null;
        }

        // ---- Helpers ----

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

        // A mod on every arcana's stats (all) or on all but the basic and the dash.
        private static void SkillMods(Player p, string field, string node, float value, bool multiplicative, bool all)
        {
            if (p.skillsDict == null)
                return;
            Player.SkillState basic = p.GetBasicSkill(), dash = p.GetDashSkill();
            foreach (Player.SkillState s in p.skillsDict.Values)
            {
                if (s == null || s.skillData == null)
                    continue;
                bool skip = !all && (s == basic || s == dash);
                SetStat(s.skillData, field, node, skip ? 0f : value, multiplicative);
            }
        }

        private static void SetStat(StatData data, string field, string node, float value, bool multiplicative)
        {
            string id = Prefix + node;
            var key = new KeyValuePair<object, string>(data, id + field);
            float had;
            if (applied.TryGetValue(key, out had) && Mathf.Approximately(had, value))
                return;
            if (applied.Count > 4096)
                applied.Clear();
            data.RemoveMod(field, id);
            if (!Mathf.Approximately(value, 0f))
                data.AddMod(field, new NumVarStatMod(id, value, 10, multiplicative ? VarStatModType.Multiplicative : VarStatModType.Additive, false));
            applied[key] = value;
        }

        private static void SetMod(NumVarStat stat, string node, float value, bool multiplicative)
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

        private static void SetAnim(Player p, string node, float value)
        {
            if (p.animSpeedHandler == null)
                return;
            string id = Prefix + node;
            var key = new KeyValuePair<object, string>(p.animSpeedHandler, id);
            float had;
            if (applied.TryGetValue(key, out had) && Mathf.Approximately(had, value))
                return;
            p.animSpeedHandler.Modify(new NumVarStatMod(id, 0f, 10, VarStatModType.Multiplicative, false), false);
            if (!Mathf.Approximately(value, 0f))
                p.animSpeedHandler.Modify(new NumVarStatMod(id, value, 10, VarStatModType.Multiplicative, false), true);
            applied[key] = value;
        }

        private static void Fx(Action show) => Guard("fx", show);

        private static void Say(Player p, string key, string message)
        {
            if (p != null && said.Add(p.playerID + "/" + key))
                ChaosDepthsPlugin.Log($"Pact: player {p.playerID + 1}: {message}");
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
                    ChaosDepthsPlugin.LogWarning($"Pact: {what} failed ({e.Message})");
            }
        }
    }
}
