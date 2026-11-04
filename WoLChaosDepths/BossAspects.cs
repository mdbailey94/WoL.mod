using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLChaosDepths
{
    // #88 Boss Aspects v1 (approved by Matt 2026-11-03, numbers from Log - Modder 16:20).
    //
    // Bosses in the trials roll "aspects" as they appear: floor bosses one, realm and final
    // bosses two. v1 has the four aspects that need no new boss AI:
    //   LEECH      every living wizard loses a share of max health a second, the boss heals half
    //              of it; never below 10% health; paused for the boss's intro.
    //   BULWARKED  a shell (a share of the boss's max health) takes every hit first; it regrows
    //              while unhit, so only burst breaks it; broken, the boss is open for a while.
    //   ATTRITION  the boss regains health unless it took 3+ hits lately; one hit is capped at a
    //              share of its max health. Burn/poison/shock ticks don't count as hits.
    //   HAILSTORM  hail falls across the arena, each stone marked on the floor before it lands;
    //              a third aim at a wizard.
    // Bulwarked + Attrition never roll together (one needs burst, the other wastes it).
    // Switches (Matt: "turn these on and off"): [Aspects] Enabled is the master, and each aspect
    // has its own Leech / Bulwarked / Attrition / Hailstorm switch; Mod Menu lists them too.
    public static class BossAspects
    {
        private const string Section = "Aspects";

        private static ConfigEntry<bool> enabled, leechOn, bulwarkOn, attritionOn, hailOn;
        private static ConfigEntry<int> floorCount, realmCount, strongFrom;
        private static ConfigEntry<float> floorScale, introDelay;
        private static ConfigEntry<float> leechRate, leechRateStrong, leechFloor, leechHeal;
        private static ConfigEntry<float> shellSize, shellRegrow, shellQuiet, shellOpen, shellRegrowTime;
        private static ConfigEntry<float> attritionRegen, attritionWindow, attritionCap;
        private static ConfigEntry<int> attritionHits;
        private static ConfigEntry<float> hailRate, hailRateStrong, hailAimed, hailWarning, hailRadius, hailDamage, hailArena;

        public static bool Enabled => enabled != null && enabled.Value;

        private static readonly string[] Names = { "Leech", "Bulwarked", "Attrition", "Hailstorm" };
        private static readonly Color[] Colors =
        {
            new Color(0.85f, 0.15f, 0.2f),   // Leech: blood red
            new Color(0.45f, 0.65f, 0.95f),  // Bulwarked: steel blue
            new Color(0.4f, 0.85f, 0.4f),    // Attrition: green
            new Color(0.8f, 0.95f, 1f),      // Hailstorm: pale ice
        };

        private class Aspected
        {
            public Entity boss;
            public bool realm;
            public List<string> aspects = new List<string>();
            public float scale;          // 1 for realm/final bosses, FloorScale for floor bosses
            public bool strong;          // the stronger numbers (from Ascension StrongFrom)
            public float activeAt;       // after the intro
            public float drainCarry;     // fractional health drained, per wizard (summed)
            public float healCarry;
            // Bulwarked
            public float shellMax, shell, lastHitAt, openUntil, regrowFrom;
            public bool broken;
            // Attrition
            public readonly Queue<float> hits = new Queue<float>();
            public float regenCarry;
            // Hailstorm
            public float hailCarry;
            // Art (0.34.1): the shell sprite on the boss
            public SpriteRenderer shellLook;
            public Sprite[] shellFrames, breakFrames;
            public float breakAt = -1f;
            public bool sized;           // the shell is sized once the aspects start (after Ascension's health)
            public Color baseTint = Color.white;
            public bool tintTaken;

            public bool Has(string a) => aspects.Contains(a);
            public bool Alive => boss != null && boss.health != null && boss.health.CurrentHealthValue > 0;
            public bool Active => Alive && Time.time >= activeAt;
            public float Max => boss.health.healthStat.ModifiedValue;
        }

        private static readonly Dictionary<int, Aspected> bosses = new Dictionary<int, Aspected>();
        private static readonly Dictionary<Player, float> playerDrainCarry = new Dictionary<Player, float>();
        private static readonly List<Hail> hail = new List<Hail>();
        // v1 "never repeats": the aspects each boss type had last time, this game session.
        private static readonly Dictionary<string, List<string>> lastRoll = new Dictionary<string, List<string>>();

        public static void Bind(ConfigFile config)
        {
            enabled = config.Bind(Section, "Enabled", false,
                "MASTER SWITCH for Boss Aspects (#88): bosses in the trials roll aspects (floor boss 1, realm/final boss 2).");
            leechOn = config.Bind(Section, "Leech", true, "Leech can roll: wizards in the fight lose health over time and the boss heals from it.");
            bulwarkOn = config.Bind(Section, "Bulwarked", true, "Bulwarked can roll: a regrowing shell takes hits first; only burst breaks it.");
            attritionOn = config.Bind(Section, "Attrition", true, "Attrition can roll: the boss regenerates unless hit often; single hits are capped.");
            hailOn = config.Bind(Section, "Hailstorm", true, "Hailstorm can roll: marked hail falls across the arena all fight.");

            const string t = "Aspects.Tuning";
            floorCount = config.Bind(t, "FloorBossAspects", 1, new ConfigDescription("Aspects on a floor boss.", new AcceptableValueRange<int>(0, 2)));
            realmCount = config.Bind(t, "RealmBossAspects", 2, new ConfigDescription("Aspects on a realm or final boss.", new AcceptableValueRange<int>(0, 3)));
            strongFrom = config.Bind(t, "StrongerFromAscension", 5, "From this Ascension level the stronger Leech and Hailstorm numbers apply.");
            floorScale = config.Bind(t, "FloorBossScale", 0.75f, "Floor bosses get this share of each aspect's strength (drain, shell, regen, hail rate).");
            introDelay = config.Bind(t, "IntroDelay", 3f, "Seconds after the boss appears before its aspects start (its intro).");
            leechRate = config.Bind(t, "LeechPerSecond", 0.015f, "Leech: share of each wizard's max health drained a second.");
            leechRateStrong = config.Bind(t, "LeechPerSecondStrong", 0.02f, "Leech from StrongerFromAscension.");
            leechFloor = config.Bind(t, "LeechMinHealth", 0.10f, "Leech never takes a wizard below this share of max health.");
            leechHeal = config.Bind(t, "LeechHealShare", 0.5f, "Share of the drained health the boss heals.");
            shellSize = config.Bind(t, "ShellSize", 0.25f, "Bulwarked: the shell, as a share of the boss's max health.");
            shellRegrow = config.Bind(t, "ShellRegrowPerSecond", 0.10f, "Bulwarked: share of the shell regrown a second while unhit.");
            shellQuiet = config.Bind(t, "ShellRegrowAfter", 1.5f, "Bulwarked: seconds unhit before the shell regrows.");
            shellOpen = config.Bind(t, "ShellBrokenSeconds", 8f, "Bulwarked: seconds the boss stays open after the shell breaks.");
            shellRegrowTime = config.Bind(t, "ShellReturnSeconds", 1f, "Bulwarked: seconds for the shell to grow back after the open window.");
            attritionRegen = config.Bind(t, "AttritionRegenPerSecond", 0.02f, "Attrition: share of max health regained a second while not pressured.");
            attritionHits = config.Bind(t, "AttritionHits", 3, "Attrition: hits needed within AttritionWindow to stop the regen.");
            attritionWindow = config.Bind(t, "AttritionWindow", 2f, "Attrition: seconds the hits are counted over.");
            attritionCap = config.Bind(t, "AttritionHitCap", 0.08f, "Attrition: one hit can take at most this share of max health.");
            hailRate = config.Bind(t, "HailPerSecond", 6f, "Hailstorm: stones a second.");
            hailRateStrong = config.Bind(t, "HailPerSecondStrong", 8f, "Hailstorm from StrongerFromAscension.");
            hailAimed = config.Bind(t, "HailAimedShare", 0.33f, "Hailstorm: share of stones aimed at a wizard.");
            hailWarning = config.Bind(t, "HailWarning", 0.6f, "Hailstorm: seconds a spot is marked before the stone lands.");
            hailRadius = config.Bind(t, "HailRadius", 0.5f, "Hailstorm: radius of a stone's hit, in units.");
            hailDamage = config.Bind(t, "HailDamage", 0.03f, "Hailstorm: a hit takes this share of the wizard's max health (evade still rolls).");
            hailArena = config.Bind(t, "HailArenaRadius", 8f, "Hailstorm: random stones fall within this many units of the boss.");

            try
            {
                MiniBoss.globalMiniBossSpawnEventHandlers += mini => OnSpawn(mini, false);
                Boss.globalBossSpawnEventHandlers += boss => OnSpawn(boss, true);
                Health.globalTakeDamageEnterHandlers += BeforeHit;
                Health.globalTakeDamageActualHandlers += AfterHit;
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"aspects: hooks: {e.Message}");
            }
            var go = new GameObject("ChaosDepthsBossAspects");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        public static void ClearScene()
        {
            bosses.Clear();
            playerDrainCarry.Clear();
            foreach (Hail h in hail)
                if (h.go != null)
                    UnityEngine.Object.Destroy(h.go);
            hail.Clear();
            spareMarks.Clear();
            foreach (SpriteRenderer[] tiles in mist.Values)
                foreach (SpriteRenderer r in tiles)
                    if (r != null)
                        UnityEngine.Object.Destroy(r.gameObject);
            mist.Clear();
        }

        // ---- Rolling ----

        private static bool On(string a)
        {
            switch (a)
            {
                case "Leech": return leechOn.Value;
                case "Bulwarked": return bulwarkOn.Value;
                case "Attrition": return attritionOn.Value;
                case "Hailstorm": return hailOn.Value;
            }
            return false;
        }

        private static void OnSpawn(Entity boss, bool realm)
        {
            Guard("spawn", () =>
            {
                if (!Enabled || boss == null || boss.health == null || !ChaosDepthsPlugin.InTrialsNow())
                    return;
                int id = boss.GetInstanceID();
                if (bosses.ContainsKey(id))
                    return;
                int count = realm ? realmCount.Value : floorCount.Value;
                string type = boss.GetType().Name;
                List<string> picked = Roll(count, type);
                if (picked.Count == 0)
                    return;
                lastRoll[type] = picked;
                var a = new Aspected
                {
                    boss = boss,
                    realm = realm,
                    aspects = picked,
                    scale = realm ? 1f : Mathf.Clamp(floorScale.Value, 0f, 2f),
                    strong = AscensionLink.Level >= strongFrom.Value,
                    activeAt = Time.time + Mathf.Max(0f, introDelay.Value),
                };
                bosses[id] = a;
                string list = string.Join(", ", picked.ToArray());
                ChaosDepthsPlugin.Log($"Aspects: {(realm ? "realm" : "floor")} boss {type} is {list} (Ascension {AscensionLink.Level}{(a.strong ? ", stronger numbers" : "")}, scale {a.scale:0.##})");
                ChaosDepthsPlugin.Notice($"{(realm ? "BOSS" : "FLOOR BOSS")} ASPECTS: {list.ToUpperInvariant()}", 4f);
            });
        }

        private static List<string> Roll(int count, string type)
        {
            var pool = new List<string>();
            foreach (string n in Names)
                if (On(n))
                    pool.Add(n);
            List<string> last;
            lastRoll.TryGetValue(type, out last);
            var picked = new List<string>();
            while (picked.Count < count && pool.Count > 0)
            {
                // Prefer aspects this boss type didn't have last time; fall back if none are left.
                var fresh = pool.FindAll(n => last == null || !last.Contains(n));
                List<string> from = fresh.Count > 0 ? fresh : pool;
                string a = from[UnityEngine.Random.Range(0, from.Count)];
                picked.Add(a);
                pool.Remove(a);
                // The banned pair.
                if (a == "Bulwarked") pool.Remove("Attrition");
                if (a == "Attrition") pool.Remove("Bulwarked");
            }
            return picked;
        }

        private static Aspected Of(Entity e)
        {
            Aspected a;
            return e != null && bosses.TryGetValue(e.GetInstanceID(), out a) ? a : null;
        }

        // ---- Hits on the boss: shell and the Attrition cap ----

        private static void BeforeHit(AttackInfo info, Entity attacker, Entity hurt)
        {
            if (!Enabled || info == null || info.damage <= 0 || bosses.Count == 0)
                return;
            Aspected a = Of(hurt);
            if (a == null || !a.Active)
                return;
            Guard("hit", () =>
            {
                if (a.Has("Attrition"))
                {
                    int cap = Mathf.Max(1, Mathf.RoundToInt(a.Max * attritionCap.Value));
                    if (info.damage > cap)
                        info.damage = cap;
                }
                if (a.Has("Bulwarked") && a.sized && a.shell > 0f && !a.broken)
                {
                    a.lastHitAt = Time.time;
                    float soaked = Mathf.Min(a.shell, info.damage);
                    a.shell -= soaked;
                    info.damage -= Mathf.RoundToInt(soaked);   // overflow from a big hit goes through
                    if (a.shell <= 0.5f)
                    {
                        a.shell = 0f;
                        a.broken = true;
                        a.openUntil = Time.time + shellOpen.Value;
                        ChaosDepthsPlugin.Notice("SHELL BROKEN!", 2f);
                        ChaosDepthsPlugin.Log($"Aspects: {a.boss.GetType().Name}'s shell broke; open for {shellOpen.Value:0.#} s");
                    }
                }
            });
        }

        private static void AfterHit(AttackInfo info, Entity attacker, Entity hurt)
        {
            if (!Enabled || info == null || bosses.Count == 0)
                return;
            Aspected a = Of(hurt);
            if (a == null || !a.Active || !a.Has("Attrition") || info.isStatusEffect)
                return;
            a.hits.Enqueue(Time.time);
        }

        // ---- Over time ----

        private class Runner : MonoBehaviour
        {
            private void Update()
            {
                if (!Enabled || bosses.Count == 0 && hail.Count == 0 && mist.Count == 0)
                    return;
                Guard("tick", () => Tick(Time.deltaTime));
            }

            private void OnGUI()
            {
                if (!Enabled || bosses.Count == 0)
                    return;
                Guard("gui", DrawGui);
            }
        }

        private static readonly List<int> gone = new List<int>();

        private static bool leechActive;

        private static void Tick(float dt)
        {
            leechActive = false;
            gone.Clear();
            foreach (KeyValuePair<int, Aspected> kv in bosses)
            {
                Aspected a = kv.Value;
                if (!a.Alive)
                {
                    gone.Add(kv.Key);
                    continue;
                }
                if (!a.Active)
                    continue;
                if (a.Has("Leech")) { Leech(a, dt); leechActive = true; }
                if (a.Has("Bulwarked")) Shell(a, dt);
                if (a.Has("Attrition")) Attrition(a, dt);
                if (a.Has("Hailstorm")) Hailstorm(a, dt);
            }
            foreach (int id in gone)
                bosses.Remove(id);
            TickHail(dt);
            TickMist();
        }

        // WoL Art's aspect_leech_mist (4 x 32x16, 6 fps loop, tileable) in a 3x3 patch of tiles
        // under each living wizard while a Leech boss drains them, at 60% opacity.
        private static Sprite[] mistFrames;
        private static bool mistLooked;
        private static readonly Dictionary<Player, SpriteRenderer[]> mist = new Dictionary<Player, SpriteRenderer[]>();

        private static void TickMist()
        {
            if (!mistLooked)
            {
                mistLooked = true;
                mistFrames = ItemArt.Frames("aspect_leech_mist", 4, new Vector2(0.5f, 0.5f), 16f);
            }
            if (mistFrames == null)
                return;
            Player[] players = GameController.activePlayers;
            int frame = (int)(Time.time * 6f) % mistFrames.Length;
            if (players != null)
                foreach (Player p in players)
                {
                    if (p == null)
                        continue;
                    bool show = leechActive && p.health != null && p.health.CurrentHealthValue > 0;
                    SpriteRenderer[] tiles;
                    if (!mist.TryGetValue(p, out tiles) || tiles[0] == null)
                    {
                        if (!show)
                            continue;
                        tiles = new SpriteRenderer[9];
                        for (int i = 0; i < 9; i++)
                        {
                            var go = new GameObject("ChaosDepthsLeechMist");
                            tiles[i] = go.AddComponent<SpriteRenderer>();
                            if (p.spriteRenderer != null)
                            {
                                tiles[i].sortingLayerID = p.spriteRenderer.sortingLayerID;
                                tiles[i].sortingOrder = p.spriteRenderer.sortingOrder - 60;
                            }
                            tiles[i].color = new Color(1f, 1f, 1f, 0.6f);
                        }
                        mist[p] = tiles;
                    }
                    // Snapped to the 2x1 u tile grid so the pattern stays put on the floor.
                    Vector2 at = p.transform.position;
                    float bx = Mathf.Floor(at.x / 2f) * 2f + 1f, by = Mathf.Floor(at.y) + 0.5f;
                    for (int i = 0; i < 9; i++)
                    {
                        tiles[i].enabled = show;
                        if (!show)
                            continue;
                        tiles[i].transform.position = new Vector3(bx + (i % 3 - 1) * 2f, by + (i / 3 - 1), 0f);
                        tiles[i].sprite = mistFrames[frame];
                    }
                }
        }

        private static void Leech(Aspected a, float dt)
        {
            float rate = (a.strong ? leechRateStrong.Value : leechRate.Value) * a.scale;
            float drained = 0f;
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            foreach (Player p in players)
            {
                if (p == null || p.health == null || p.health.CurrentHealthValue <= 0)
                    continue;
                float max = p.health.healthStat.ModifiedValue;
                int floor = Mathf.CeilToInt(max * leechFloor.Value);
                float carry;
                playerDrainCarry.TryGetValue(p, out carry);
                carry += max * rate * dt;
                int whole = Mathf.FloorToInt(carry);
                carry -= whole;
                playerDrainCarry[p] = carry;
                int cur = p.health.CurrentHealthValue;
                int take = Mathf.Min(whole, Mathf.Max(0, cur - floor));
                if (take > 0)
                {
                    p.health.CurrentHealthValue = cur - take;
                    drained += take;
                }
            }
            a.healCarry += drained * leechHeal.Value;
            int heal = Mathf.FloorToInt(a.healCarry);
            if (heal > 0 && a.boss.health.CurrentHealthValue < a.Max)
            {
                a.healCarry -= heal;
                a.boss.health.RestoreHealth(heal, false, false);
            }
        }

        private static void Shell(Aspected a, float dt)
        {
            // Review fix (0.35.4): sized when the aspects start, not at spawn: Ascension raises a
            // boss's health in its own scan a moment after it appears, so at spawn the shell came
            // out at 25% of the unscaled health.
            if (!a.sized)
            {
                a.sized = true;
                a.shellMax = Mathf.Max(1f, a.Max * shellSize.Value * a.scale);
                a.shell = a.shellMax;
                ChaosDepthsPlugin.Log($"Aspects: {a.boss.GetType().Name}'s shell is {a.shellMax:0} ({shellSize.Value * a.scale:P0} of {a.Max:0})");
            }
            if (a.broken)
            {
                if (Time.time < a.openUntil)
                    return;
                // Grows back over ShellReturnSeconds.
                a.shell += a.shellMax * dt / Mathf.Max(0.05f, shellRegrowTime.Value);
                if (a.shell >= a.shellMax)
                {
                    a.shell = a.shellMax;
                    a.broken = false;
                    a.lastHitAt = Time.time;
                }
            }
            else if (a.shell < a.shellMax && Time.time - a.lastHitAt >= shellQuiet.Value)
            {
                a.shell = Mathf.Min(a.shellMax, a.shell + a.shellMax * shellRegrow.Value * dt);
            }
            // Steel-blue tint while the shell is up.
            // From the boss's own tint (a layer boss's or an elite's), not from white.
            if (a.boss.spriteRenderer != null)
            {
                if (!a.tintTaken)
                {
                    a.tintTaken = true;
                    a.baseTint = a.boss.spriteRenderer.color;
                }
                float k = a.broken ? 0f : a.shell / a.shellMax;
                a.boss.spriteRenderer.color = Color.Lerp(a.baseTint, Colors[1], (a.shellLook != null ? 0.25f : 0.55f) * k);
            }
            ShellLook(a);
        }

        // WoL Art's shell (aspect_shell_48/_80: 4 frames, 8 fps loop) round the boss's body
        // centre, and its break (6 frames, 20 fps once) when it shatters. The 80 px one for a
        // boss whose picture is more than 2.5 u across.
        private static void ShellLook(Aspected a)
        {
            if (a.shellFrames == null)
            {
                SpriteRenderer body = a.boss.spriteRenderer;
                bool big = body != null && body.bounds.size.x > 2.5f;
                string n = big ? "80" : "48";
                a.shellFrames = ItemArt.Frames("aspect_shell_" + n, 4, new Vector2(0.5f, 0.5f), 16f) ?? new Sprite[0];
                a.breakFrames = ItemArt.Frames("aspect_shell_break_" + n, 6, new Vector2(0.5f, 0.5f), 16f) ?? new Sprite[0];
                if (a.shellFrames.Length == 0)
                    return;
                var go = new GameObject("ChaosDepthsAspectShell");
                go.transform.SetParent(a.boss.transform, false);
                a.shellLook = go.AddComponent<SpriteRenderer>();
                if (body != null)
                {
                    a.shellLook.sortingLayerID = body.sortingLayerID;
                    a.shellLook.sortingOrder = body.sortingOrder + 1;
                }
            }
            if (a.shellLook == null)
                return;
            SpriteRenderer b = a.boss.spriteRenderer;
            a.shellLook.transform.position = b != null ? b.bounds.center : a.boss.transform.position;
            if (a.broken)
            {
                if (a.breakAt < 0f)
                    a.breakAt = Time.time;
                int f = (int)((Time.time - a.breakAt) * 20f);
                a.shellLook.enabled = f < a.breakFrames.Length;
                if (a.shellLook.enabled)
                    a.shellLook.sprite = a.breakFrames[f];
                return;
            }
            a.breakAt = -1f;
            a.shellLook.enabled = a.shell > 0f;
            a.shellLook.sprite = a.shellFrames[(int)(Time.time * 8f) % a.shellFrames.Length];
            a.shellLook.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 1f, a.shell / a.shellMax));
        }

        private static void Attrition(Aspected a, float dt)
        {
            while (a.hits.Count > 0 && Time.time - a.hits.Peek() > attritionWindow.Value)
                a.hits.Dequeue();
            if (a.hits.Count >= attritionHits.Value || a.boss.health.CurrentHealthValue >= a.Max)
            {
                a.regenCarry = 0f;
                return;
            }
            a.regenCarry += a.Max * attritionRegen.Value * a.scale * dt;
            int whole = Mathf.FloorToInt(a.regenCarry);
            if (whole > 0)
            {
                a.regenCarry -= whole;
                a.boss.health.RestoreHealth(whole, false, false);
            }
        }

        // ---- Hailstorm ----

        private class Hail
        {
            public GameObject go;
            public SpriteRenderer mark;
            public Vector2 spot;
            public float landAt, born;
            public Entity boss;
        }

        private static readonly List<SpriteRenderer> spareMarks = new List<SpriteRenderer>();
        private static Sprite ringSprite;
        private static Sprite[] markFrames;
        private static bool markLooked;

        private static void Hailstorm(Aspected a, float dt)
        {
            float rate = (a.strong ? hailRateStrong.Value : hailRate.Value) * a.scale;
            a.hailCarry += rate * dt;
            Player[] players = GameController.activePlayers;
            while (a.hailCarry >= 1f)
            {
                a.hailCarry -= 1f;
                Vector2 spot;
                Player target = RandomLivingPlayer(players);
                if (target != null && UnityEngine.Random.value < hailAimed.Value)
                    spot = (Vector2)target.transform.position + UnityEngine.Random.insideUnitCircle * 0.3f;
                else
                    spot = (Vector2)a.boss.transform.position + UnityEngine.Random.insideUnitCircle * hailArena.Value;
                SpawnMark(a.boss, spot);
            }
        }

        private static readonly List<Player> aliveBuffer = new List<Player>();

        private static Player RandomLivingPlayer(Player[] players)
        {
            if (players == null)
                return null;
            aliveBuffer.Clear();
            foreach (Player p in players)
                if (p != null && p.health != null && p.health.CurrentHealthValue > 0)
                    aliveBuffer.Add(p);
            return aliveBuffer.Count > 0 ? aliveBuffer[UnityEngine.Random.Range(0, aliveBuffer.Count)] : null;
        }

        private static void SpawnMark(Entity boss, Vector2 spot)
        {
            if (!markLooked)
            {
                markLooked = true;
                markFrames = ItemArt.Frames("aspect_hail_mark", 4, new Vector2(0.5f, 0.5f), 16f);
            }
            // #101 cleanup (0.36.1): marks are reused (it was a new GameObject per stone, 6-8 a second).
            GameObject go;
            SpriteRenderer sr;
            if (spareMarks.Count > 0 && spareMarks[spareMarks.Count - 1] != null)
            {
                sr = spareMarks[spareMarks.Count - 1];
                spareMarks.RemoveAt(spareMarks.Count - 1);
                go = sr.gameObject;
                go.SetActive(true);
                go.transform.localScale = Vector3.one;
            }
            else
            {
                spareMarks.Clear();
                go = new GameObject("ChaosDepthsHailMark");
                sr = go.AddComponent<SpriteRenderer>();
            }
            go.transform.position = new Vector3(spot.x, spot.y, 0f);
            sr.sprite = markFrames != null ? markFrames[0] : Ring();
            if (markFrames == null)
            {
                float d = hailRadius.Value * 2f;
                go.transform.localScale = new Vector3(d, d, 1f);
            }
            if (boss.spriteRenderer != null)
            {
                sr.sortingLayerID = boss.spriteRenderer.sortingLayerID;
                sr.sortingOrder = boss.spriteRenderer.sortingOrder - 50;
            }
            sr.color = new Color(0.8f, 0.95f, 1f, 0.5f);
            hail.Add(new Hail { go = go, mark = sr, spot = spot, born = Time.time, landAt = Time.time + hailWarning.Value, boss = boss });
        }

        private static void TickHail(float dt)
        {
            for (int i = hail.Count - 1; i >= 0; i--)
            {
                Hail h = hail[i];
                if (h.go == null)
                {
                    hail.RemoveAt(i);
                    continue;
                }
                float t = Mathf.InverseLerp(h.born, h.landAt, Time.time);
                if (markFrames != null)
                    h.mark.sprite = markFrames[Mathf.Min(markFrames.Length - 1, (int)(t * markFrames.Length))];
                else
                {
                    // Code-drawn: the ring closes in and brightens until the stone lands.
                    float d = hailRadius.Value * 2f * Mathf.Lerp(1.6f, 1f, t);
                    h.go.transform.localScale = new Vector3(d, d, 1f);
                }
                h.mark.color = new Color(0.8f, 0.95f, 1f, Mathf.Lerp(0.35f, 0.9f, t));
                if (Time.time < h.landAt)
                    continue;
                Land(h);
                h.go.SetActive(false);
                spareMarks.Add(h.mark);
                hail.RemoveAt(i);
            }
        }

        private static void Land(Hail h)
        {
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            foreach (Player p in players)
            {
                if (p == null || p.health == null || p.health.CurrentHealthValue <= 0)
                    continue;
                if (((Vector2)p.transform.position - h.spot).sqrMagnitude > hailRadius.Value * hailRadius.Value)
                    continue;
                int dmg = Mathf.Max(1, Mathf.RoundToInt(p.health.healthStat.ModifiedValue * hailDamage.Value));
                Entity boss = h.boss != null ? h.boss : null;   // Unity-null once the boss is gone
                var info = new AttackInfo
                {
                    damage = dmg,
                    entity = boss,
                    skillCategory = boss != null ? boss.skillCategory : "",
                    elementType = ElementType.Water,
                    showDamageNumber = true,
                    showDamageEffect = true,
                    attackerPosition = h.spot,
                    targetPosition = p.transform.position,
                };
                // Review fix (0.35.4): if the game's damage path rejects a hit built by hand, the
                // stone still hurts (straight off health, never below 1), and the log says so once.
                try
                {
                    p.health.TakeDamage(info, boss);
                }
                catch (Exception e)
                {
                    if (!hailFallbackLogged)
                    {
                        hailFallbackLogged = true;
                        ChaosDepthsPlugin.LogError($"aspects: hail TakeDamage failed ({e.Message}); hail now takes health directly");
                    }
                    if (Shade.IsShade(p))
                        Shade.AddSpirit(p, -Shade.BossHitCostValue);
                    else
                        p.health.CurrentHealthValue = Mathf.Max(1, p.health.CurrentHealthValue - dmg);
                }
            }
        }

        private static bool hailFallbackLogged;

        private static Sprite Ring()
        {
            if (ringSprite != null)
                return ringSprite;
            const int n = 16;
            var tex = new Texture2D(n, n, TextureFormat.ARGB32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var c = new Vector2((n - 1) / 2f, (n - 1) / 2f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), c);
                    tex.SetPixel(x, y, r <= 7.5f && r >= 6f ? Color.white : r < 6f ? new Color(1f, 1f, 1f, 0.15f) : Color.clear);
                }
            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);   // 1 unit across
            return ringSprite;
        }

        // ---- HUD: aspect names (and the shell's bar) at the top of the screen ----

        private static GUIStyle style;
        private static Texture2D white;

        private static void DrawGui()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                white = Texture2D.whiteTexture;
            }
            style.fontSize = Mathf.Max(12, Screen.height / 50);
            float y = Screen.height * 0.13f;
            foreach (Aspected a in bosses.Values)
            {
                if (!a.Alive)
                    continue;
                float w = Screen.width * 0.4f, h = style.fontSize * 1.6f;
                float x = (Screen.width - w) / 2f;
                var parts = new List<string>();
                foreach (string n in a.aspects)
                {
                    Color c = Colors[Array.IndexOf(Names, n)];
                    parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{n.ToUpperInvariant()}</color>");
                }
                style.richText = true;
                // WoL Art's 12x12 icons (boss_aspect_<name>), drawn 2x left of the names.
                float icon = Mathf.Round(h * 0.9f);
                float ix = x + w / 2f - (TextWidth(a) / 2f) - icon * a.aspects.Count - 6f;
                foreach (string n in a.aspects)
                {
                    Texture2D tex = Icon(n);
                    if (tex != null)
                        GUI.DrawTexture(new Rect(ix, y + (h - icon) / 2f, icon, icon), tex);
                    ix += icon + 2f;
                }
                GUI.Label(new Rect(x + 1, y + 1, w, h), "<color=#000000>" + string.Join("  ·  ", a.aspects.ConvertAll(n => n.ToUpperInvariant()).ToArray()) + "</color>", style);
                GUI.Label(new Rect(x, y, w, h), string.Join("  ·  ", parts.ToArray()), style);
                y += h;
                if (a.Has("Bulwarked"))
                {
                    float bw = Screen.width * 0.2f, bh = Mathf.Max(4f, Screen.height / 160f);
                    float bx = (Screen.width - bw) / 2f;
                    Color old = GUI.color;
                    GUI.color = new Color(0f, 0f, 0f, 0.6f);
                    GUI.DrawTexture(new Rect(bx - 1, y - 1, bw + 2, bh + 2), white);
                    GUI.color = a.broken ? new Color(0.5f, 0.5f, 0.55f) : Colors[1];
                    GUI.DrawTexture(new Rect(bx, y, bw * (a.shellMax > 0 ? a.shell / a.shellMax : 0f), bh), white);
                    GUI.color = old;
                    y += bh + 4f;
                }
                y += 2f;
            }
        }

        private static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

        private static Texture2D Icon(string aspect)
        {
            Texture2D tex;
            if (icons.TryGetValue(aspect, out tex))
                return tex;
            Sprite[] f = ItemArt.Frames("boss_aspect_" + aspect.ToLowerInvariant(), 1, new Vector2(0.5f, 0.5f), 16f);
            tex = f != null && f.Length > 0 ? f[0].texture : null;
            if (tex != null)
                tex.filterMode = FilterMode.Point;
            icons[aspect] = tex;
            return tex;
        }

        private static float TextWidth(Aspected a) =>
            style.CalcSize(new GUIContent(string.Join("  ·  ", a.aspects.ConvertAll(n => n.ToUpperInvariant()).ToArray()))).x;

        private static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"aspects: {what}: {e.Message}");
            }
        }
    }
}
