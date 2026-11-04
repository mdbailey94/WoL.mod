using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLChaosDepths
{
    // #89 Shade cloak v1, part 1 (approved by Matt 2026-11-03 21:24: a LIVING wizard dressed as a
    // ghost, numbers from Log - Modder 16:20). Behind [Shade] Enabled; v1 unlock is config only:
    // [Shade] Players picks who wears it (P1, P2 or Both).
    //
    // In this part: the ghost look (pale tint at ~70%, a wisp trail, a lantern on the chest), the
    // SPIRIT bar (100, start 40, no regen) that replaces health for the wearer (hits cost spirit,
    // not health), spirit from kills, and the death rule (0 spirit for 3 s = out; the run ends
    // only in solo or if the partner is down). Next part: Blink (dash), Spirit Wolf (basic),
    // health/boss-chunk orbs, untargetable + floating over pits (waits on the decompile greps).
    public static class Shade
    {
        private const string Section = "Shade";
        private static ConfigEntry<bool> enabled;
        private static ConfigEntry<string> players;
        private static ConfigEntry<float> spiritMax, spiritStart, killSpirit, eliteSpirit, bossSpirit;
        private static ConfigEntry<float> hitCost, bossHitCost, outSeconds, ghostAlpha, wispEvery;
        private static ConfigEntry<bool> untargetedSolo;
        private static ConfigEntry<float> orbSpirit, chunkSpirit, floorChunk, realmChunk, finalChunk, orbMinHeal;

        private static readonly Color Ghost = new Color(0.8f, 0.9f, 1f);

        private class State
        {
            public float spirit;
            public float outFor;
            public float nextWisp;
            public Vector2 lastPos;
            public SpriteRenderer lantern;
            public bool ended;
            public Color lastSet = Color.white, baseTint = Color.white;
            public bool tinted;
            public bool offList;   // we took the wizard off GameController.allies
        }

        // Keyed by player slot (0/1): the Player objects are rebuilt per scene; spirit carries over.
        private static readonly Dictionary<int, State> states = new Dictionary<int, State>();
        private static readonly HashSet<int> credited = new HashSet<int>();
        private static Sprite[] wispFrames, lanternFrames;
        private static bool artLooked;

        public static bool Enabled => enabled != null && enabled.Value;

        public static void Bind(ConfigFile config)
        {
            enabled = config.Bind(Section, "Enabled", false, "Shade cloak v1 (#89): the chosen wizard(s) play as a living ghost with a spirit bar instead of health.");
            players = config.Bind(Section, "Players", "P1", new ConfigDescription("Who wears the Shade (v1 unlock is config only).", new AcceptableValueList<string>("P1", "P2", "Both")));
            untargetedSolo = config.Bind(Section, "UntargetedInSolo", false,
                "Review fix (0.35.4): in SOLO the Shade stays a normal target (with nobody else to chase, enemies and bosses would just idle). In co-op enemies always skip it. True = skip it in solo too.");
            const string t = "Shade.Tuning";
            spiritMax = config.Bind(t, "SpiritMax", 100f, "Size of the spirit bar.");
            spiritStart = config.Bind(t, "SpiritStart", 40f, "Spirit at the start of a run (and of a new floor in v1).");
            killSpirit = config.Bind(t, "KillSpirit", 8f, "Spirit from a kill.");
            eliteSpirit = config.Bind(t, "EliteKillSpirit", 20f, "Spirit from an elite kill.");
            bossSpirit = config.Bind(t, "BossKillSpirit", 40f, "Spirit from a boss kill.");
            hitCost = config.Bind(t, "HitCost", 6f, "Spirit lost to a hit (the hit does no health damage).");
            bossHitCost = config.Bind(t, "BossHitCost", 12f, "Spirit lost to a boss's hit.");
            outSeconds = config.Bind(t, "OutSeconds", 3f, "Seconds at 0 spirit before the Shade is out.");
            ghostAlpha = config.Bind(t, "GhostAlpha", 0.7f, "Opacity of the Shade wizard, the wisps and the wolf (Art draws hard alpha).");
            wispEvery = config.Bind(t, "WispEvery", 0.08f, "Seconds between wisps while the Shade moves.");
            orbSpirit = config.Bind(t, "HealthOrbSpirit", 10f, "Spirit from a health orb (or any heal of at least HealthOrbMinHeal of max health).");
            orbMinHeal = config.Bind(t, "HealthOrbMinHeal", 0.08f, "A heal this big (share of max health) counts as an orb; smaller regen ticks give nothing.");
            chunkSpirit = config.Bind(t, "BossChunkSpirit", 10f, "Spirit for each chunk of a boss's health knocked off.");
            floorChunk = config.Bind(t, "FloorBossChunk", 0.05f, "Chunk size for a floor boss (share of its max health).");
            realmChunk = config.Bind(t, "RealmBossChunk", 0.04f, "Chunk size for a realm boss.");
            finalChunk = config.Bind(t, "FinalBossChunk", 0.03f, "Chunk size for the final boss.");
            try
            {
                Health.globalTakeDamageEnterHandlers += BeforeHit;
                Health.globalTakeDamageActualHandlers += AfterHit;
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"shade: hooks: {e.Message}");
            }
            var go = new GameObject("ChaosDepthsShade");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        // healHooked is NOT cleared (review fix 0.35.4): a wizard that survives the scene change
        // would be hooked again and get the orb's spirit twice; instance ids are never reused.
        public static void ClearScene()
        {
            credited.Clear();
            chunksPaid.Clear();
        }

        private static int Slot(Player p) => Array.IndexOf(GameController.activePlayers ?? new Player[0], p);

        public static bool IsShade(Player p)
        {
            if (!Enabled || p == null)
                return false;
            string who = players.Value;
            if (who == "Both")
                return true;
            int index = Array.IndexOf(GameController.activePlayers ?? new Player[0], p);
            return who == "P1" ? index == 0 : who == "P2" && index == 1;
        }

        private static State StateOf(Player p)
        {
            State s;
            int slot = Slot(p);
            if (!states.TryGetValue(slot, out s))
            {
                s = new State { spirit = Mathf.Min(spiritStart.Value, spiritMax.Value), lastPos = p.transform.position };
                states[slot] = s;
                ChaosDepthsPlugin.Log($"Shade: {p.name} is the Shade, spirit {s.spirit:0}/{spiritMax.Value:0}");
            }
            return s;
        }

        public static float Spirit(Player p) => IsShade(p) ? StateOf(p).spirit : 0f;
        public static float BossHitCostValue => bossHitCost != null ? bossHitCost.Value : 12f;

        public static void AddSpirit(Player p, float amount)
        {
            if (!IsShade(p))
                return;
            State s = StateOf(p);
            s.spirit = Mathf.Clamp(s.spirit + amount, 0f, spiritMax.Value);
            if (s.spirit > 0f)
                s.outFor = 0f;
        }

        // ---- Hits: the Shade loses spirit, not health ----

        private static void BeforeHit(AttackInfo info, Entity attacker, Entity hurt)
        {
            if (!Enabled || killing || info == null || info.damage <= 0)
                return;
            var p = hurt as Player;
            if (p == null || !IsShade(p))
                return;
            Guard("hit", () =>
            {
                State s = StateOf(p);
                // Burn/poison ticks do nothing to a ghost (a hit costs spirit, a tick would drain it).
                if (!info.isStatusEffect)
                {
                    float cost = attacker is Boss || attacker is MiniBoss ? bossHitCost.Value : hitCost.Value;
                    s.spirit = Mathf.Max(0f, s.spirit - cost);
                }
                info.damage = 0;
                info.showDamageNumber = false;
            });
        }

        // Kills feed every Shade's spirit (shared, like the game's gold).
        private static void AfterHit(AttackInfo info, Entity attacker, Entity hurt)
        {
            if (!Enabled || states.Count == 0 && !AnyShade())
                return;
            var enemy = hurt as Enemy;
            if (enemy == null || enemy.health == null)
                return;
            if (enemy is Boss || enemy is MiniBoss)
                Guard("chunks", () => BossChunks(enemy));
            if (enemy.health.CurrentHealthValue > 0 || !credited.Add(enemy.GetInstanceID()))
                return;
            Guard("kill", () =>
            {
                float gain = enemy is Boss || enemy is MiniBoss ? bossSpirit.Value : Elites.IsElite(enemy) ? eliteSpirit.Value : killSpirit.Value;
                Player[] all = GameController.activePlayers;
                if (all != null)
                    foreach (Player p in all)
                        if (p != null && IsShade(p) && p.health != null && p.health.CurrentHealthValue > 0)
                            AddSpirit(p, gain);
            });
        }

        // Boss chunks (v1: the spirit goes straight to the Shades, no orb to pick up): every
        // FloorBossChunk / RealmBossChunk / FinalBossChunk of the boss's max health knocked off.
        private static readonly Dictionary<int, int> chunksPaid = new Dictionary<int, int>();

        private static void BossChunks(Enemy boss)
        {
            float max = boss.health.healthStat.ModifiedValue;
            if (max <= 0f)
                return;
            float chunk = boss is MiniBoss ? floorChunk.Value
                : DiveDirector.IsFinalBoss(DiveDirector.CurrentScene()) ? finalChunk.Value : realmChunk.Value;
            if (chunk <= 0f)
                return;
            int earned = Mathf.FloorToInt(Mathf.Clamp01(1f - boss.health.CurrentHealthValue / max) / chunk);
            int id = boss.GetInstanceID(), paid;
            chunksPaid.TryGetValue(id, out paid);
            if (earned <= paid)
                return;
            chunksPaid[id] = earned;
            ForEachShade(p => AddSpirit(p, (earned - paid) * chunkSpirit.Value));
        }

        private static void ForEachShade(Action<Player> act)
        {
            Player[] all = GameController.activePlayers;
            if (all != null)
                foreach (Player p in all)
                    if (p != null && IsShade(p) && p.health != null && p.health.CurrentHealthValue > 0)
                        act(p);
        }

        // Health orbs: a heal of at least HealthOrbMinHeal of max health on the Shade becomes
        // HealthOrbSpirit (health means nothing to a ghost; small regen ticks don't count).
        private static readonly HashSet<int> healHooked = new HashSet<int>();

        private static void HookHeal(Player p)
        {
            if (!healHooked.Add(p.health.GetInstanceID()))
                return;
            Player who = p;
            p.health.healEnterEventHandlers += (ref int amount) =>
            {
                if (!Enabled || !IsShade(who) || who.health == null)
                    return;
                if (amount >= who.health.healthStat.ModifiedValue * orbMinHeal.Value)
                    AddSpirit(who, orbSpirit.Value);
            };
        }

        private static bool AnyShade()
        {
            Player[] all = GameController.activePlayers;
            if (all != null)
                foreach (Player p in all)
                    if (IsShade(p))
                        return true;
            return false;
        }

        // ---- Look, the death rule and the bar ----

        private class Runner : MonoBehaviour
        {
            private bool wasOn;

            private void Update()
            {
                if (!Enabled)
                {
                    if (wasOn && GameController.activePlayers != null)
                        foreach (Player p in GameController.activePlayers)
                            if (p != null)
                                Guard("restore", () => Restore(p));
                    wasOn = false;
                    return;
                }
                wasOn = true;
                Guard("tick", Tick);
            }

            private void OnGUI()
            {
                if (!Enabled || states.Count == 0)
                    return;
                Guard("gui", DrawGui);
            }
        }

        private static void Tick()
        {
            Player[] all = GameController.activePlayers;
            if (all == null)
                return;
            if (!artLooked)
            {
                artLooked = true;
                wispFrames = ItemArt.Frames("shade_wisp", 6, new Vector2(0.5f, 0.5f), 16f);
                lanternFrames = ItemArt.Frames("shade_lantern", 4, new Vector2(0.5f, 1f), 16f);
            }
            foreach (Player p in all)
            {
                if (p == null || p.health == null)
                    continue;
                if (!IsShade(p))
                {
                    State was;
                    if ((p.fall != null && p.fall.ignoreFall != null && p.fall.ignoreFall.GetModIndex(FloatMod) >= 0)
                        || (states.TryGetValue(Slot(p), out was) && was.tinted))
                        Restore(p);
                    continue;
                }
                State s = StateOf(p);
                // Outside the trials (Hub, house) the bar refills to its start for the next run.
                if (!ChaosDepthsPlugin.InTrialsNow())
                {
                    s.spirit = Mathf.Min(spiritStart.Value, spiritMax.Value);
                    s.outFor = 0f;
                    s.ended = false;
                }
                bool alive = p.health.CurrentHealthValue > 0;
                if (p.spriteRenderer != null && alive)
                    Tint(p, s);
                HookHeal(p);
                Wisps(p, s);
                Lantern(p, s, alive);
                Untargeted(p, s, alive);
                DeathRule(p, s, all);
            }
        }

        // 0.35.1 (from WoL Player's decompile greps, Log - Player 08:10): enemies pick targets
        // from GameController.allies (Entity.GetClosestTarget), and the game's own fall state and
        // stealth relic take a wizard off that list, so the Shade is kept off it while alive; it
        // still has its hurt box, so shots and swings in its way still hit it. Pits: a mod on
        // fall.ignoreFall, as the game's airborne skills do, so the Shade floats over them.
        private const string FloatMod = "ChaosDepthsShade_Float";

        // Review fix (0.35.4): the ghost tint goes ON TOP of whatever another mod set (the Ice
        // Sword's charge glow, a talent's flash): a colour we didn't write becomes the new base.
        private static void Tint(Player p, State s)
        {
            Color now = p.spriteRenderer.color;
            if (!s.tinted || now != s.lastSet)
                s.baseTint = s.tinted ? now : Color.white;
            s.tinted = true;
            Color c = s.baseTint * Ghost;
            c.a = s.baseTint.a * ghostAlpha.Value;
            p.spriteRenderer.color = c;
            s.lastSet = c;
        }

        private static bool Solo(Player p)
        {
            Player[] all = GameController.activePlayers;
            if (all == null)
                return true;
            foreach (Player o in all)
                if (o != null && o != p)
                    return false;
            return true;
        }

        private static void Untargeted(Player p, State s, bool alive)
        {
            List<GameObject> allies = GameController.allies;
            bool skip = alive && (untargetedSolo.Value || !Solo(p));
            if (allies != null && skip)
            {
                allies.Remove(p.gameObject);
                s.offList = true;
            }
            else if (allies != null && s.offList)
            {
                // Only undo our own removal (the game takes wizards off the list too: falling, the stealth relic).
                s.offList = false;
                if (alive && !allies.Contains(p.gameObject))
                    allies.Add(p.gameObject);
            }
            if (p.fall != null && p.fall.ignoreFall != null && p.fall.ignoreFall.GetModIndex(FloatMod) < 0)
                p.fall.ignoreFall.AddMod(new BoolVarStatMod(FloatMod, true));
        }

        // A wizard that stops being the Shade (switched off) goes back on the list and can fall.
        private static void Restore(Player p)
        {
            List<GameObject> allies = GameController.allies;
            State st;
            bool ours = states.TryGetValue(Slot(p), out st) && st.offList;
            if (ours)
                st.offList = false;
            if (ours && allies != null && p.health != null && p.health.CurrentHealthValue > 0 && !allies.Contains(p.gameObject))
                allies.Add(p.gameObject);
            if (p.fall != null && p.fall.ignoreFall != null)
                p.fall.ignoreFall.RemoveMod(FloatMod);
            // Back to the wizard's normal look.
            State s;
            if (p.spriteRenderer != null && states.TryGetValue(Slot(p), out s) && s.tinted)
            {
                p.spriteRenderer.color = s.baseTint;
                s.tinted = false;
            }
        }

        private static void Wisps(Player p, State s)
        {
            Vector2 at = p.transform.position;
            bool moving = (at - s.lastPos).sqrMagnitude > 0.0004f;
            s.lastPos = at;
            if (!moving || wispFrames == null || Time.time < s.nextWisp)
                return;
            s.nextWisp = Time.time + wispEvery.Value;
            var go = new GameObject("ChaosDepthsShadeWisp");
            go.transform.position = at + new Vector2(UnityEngine.Random.Range(-0.15f, 0.15f), 0.4f);
            var r = go.AddComponent<SpriteRenderer>();
            if (p.spriteRenderer != null)
            {
                r.sortingLayerID = p.spriteRenderer.sortingLayerID;
                r.sortingOrder = p.spriteRenderer.sortingOrder - 1;
            }
            r.color = new Color(1f, 1f, 1f, ghostAlpha.Value);
            go.AddComponent<Puff>().Init(r, wispFrames, 12f);
        }

        private class Puff : MonoBehaviour
        {
            private SpriteRenderer r;
            private Sprite[] frames;
            private float fps, born;

            public void Init(SpriteRenderer r, Sprite[] frames, float fps)
            {
                this.r = r;
                this.frames = frames;
                this.fps = fps;
                born = Time.time;
                r.sprite = frames[0];
            }

            private void Update()
            {
                int f = (int)((Time.time - born) * fps);
                if (f >= frames.Length)
                {
                    Destroy(gameObject);
                    return;
                }
                r.sprite = frames[f];
            }
        }

        // The lantern hangs on the chest (about 40% up the picture), behind the body facing up.
        private static void Lantern(Player p, State s, bool alive)
        {
            if (lanternFrames == null || p.spriteRenderer == null)
                return;
            if (s.lantern == null)
            {
                var go = new GameObject("ChaosDepthsShadeLantern");
                go.transform.SetParent(p.transform, false);
                s.lantern = go.AddComponent<SpriteRenderer>();
                s.lantern.sortingLayerID = p.spriteRenderer.sortingLayerID;
            }
            s.lantern.enabled = alive && p.spriteRenderer.enabled;
            if (!s.lantern.enabled)
                return;
            Bounds b = p.spriteRenderer.bounds;
            bool up = p.facingDirection == FacingDirection.Up;
            float x = b.center.x + (p.facingDirection == FacingDirection.Left ? -0.12f : p.facingDirection == FacingDirection.Right ? 0.12f : 0f);
            s.lantern.transform.position = new Vector3(Mathf.Round(x * 16f) / 16f, Mathf.Round((b.min.y + b.size.y * 0.55f) * 16f) / 16f, 0f);
            s.lantern.sortingOrder = p.spriteRenderer.sortingOrder + (up ? -1 : 1);
            s.lantern.sprite = lanternFrames[(int)(Time.time * 6f) % lanternFrames.Length];
        }

        // 'Out' = 0 spirit for OutSeconds (v1: no summons yet). The run ends only in solo or if
        // the partner is already down; otherwise the Shade waits at 0 for a revive (v1: the
        // partner's kills refill it).
        private static void DeathRule(Player p, State s, Player[] all)
        {
            if (p.health.CurrentHealthValue <= 0)
                return;
            if (s.spirit > 0f)
            {
                s.outFor = 0f;
                s.ended = false;
                return;
            }
            s.outFor += Time.deltaTime;
            if (s.outFor < outSeconds.Value || s.ended)
                return;
            bool partnerUp = false;
            foreach (Player o in all)
                if (o != null && o != p && o.health != null && o.health.CurrentHealthValue > 0 && !(IsShade(o) && StateOf(o).spirit <= 0f))
                    partnerUp = true;
            if (partnerUp)
                return;
            s.ended = true;
            ChaosDepthsPlugin.Log("Shade: out of spirit with no partner standing; the run ends");
            var info = new AttackInfo { damage = 999999, isDamageOverride = true, showDamageNumber = false, entity = p };
            KillThrough(p, info);
        }

        private static void KillThrough(Player p, AttackInfo info)
        {
            killing = true;
            try
            {
                p.health.TakeDamage(info, null);
                if (p.health.CurrentHealthValue > 0)
                    p.health.CurrentHealthValue = 0;
            }
            finally
            {
                killing = false;
            }
        }

        private static bool killing;

        private static GUIStyle style;

        private static void DrawGui()
        {
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            style.fontSize = Mathf.Max(11, Screen.height / 60);
            Player[] all = GameController.activePlayers;
            if (all == null)
                return;
            int row = 0;
            foreach (Player p in all)
            {
                if (p == null || !IsShade(p))
                    continue;
                State s = StateOf(p);
                float w = Screen.width * 0.16f, h = Mathf.Max(6f, Screen.height / 110f);
                float x = Screen.width * 0.02f, y = Screen.height * 0.13f + row * (h + style.fontSize + 8f);
                GUI.Label(new Rect(x, y, w, style.fontSize + 4f), $"SPIRIT {Mathf.Max(0f, s.spirit):0}", style);
                y += style.fontSize + 4f;
                Color old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(x - 1, y - 1, w + 2, h + 2), Texture2D.whiteTexture);
                GUI.color = s.spirit > 0f ? new Color(0.7f, 0.88f, 1f) : new Color(0.9f, 0.3f, 0.3f, 0.5f + 0.5f * Mathf.PingPong(Time.time * 4f, 1f));
                GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(s.spirit / spiritMax.Value), h), Texture2D.whiteTexture);
                GUI.color = old;
                row++;
            }
        }

        private static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"shade: {what}: {e.Message}");
            }
        }
    }
}
