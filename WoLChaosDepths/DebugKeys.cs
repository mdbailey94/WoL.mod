using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    // Test keys, off by default ([Debug] Enabled). They drive the game's own developer menu
    // (hidden in release builds, but its methods still work), with a plain fallback when that
    // object isn't there:
    //   F6  kill every enemy in the room       F7  clear the floor: skip to the exit room, summon
    //                                              the mini-boss, kill it, take the exit portal
    //   F8  god mode on/off                     F9  gold
    //   F5  spawn a Swift, an Armored and a Giant elite beside the wizard ([Debug] ShowcaseEnemy)
    //   F11 log every relic's ID and name ([Debug] AutoDiveRelics gives relics at F10's start)
    //   F10 auto-dive: clear floor after floor (as F7) until the run leaves the trials (e.g. the dive
    //       director sends you back to the Hub), logging each step. Start it on a trial floor.
    public static class DebugKeys
    {
        private const int MaxSteps = 40;
        private static bool running;
        private static bool hooked;
        // Counts scene loads: two floors of one realm share a scene and a display name, so names
        // can't tell them apart.
        private static int loads;
        private static int bossHitsIgnored;
        private static KeyValuePair<int, float> bossZeroSince = new KeyValuePair<int, float>(0, 0f);

        private static void Hook()
        {
            if (hooked)
                return;
            hooked = true;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => loads++;
        }

        private static string Where()
        {
            return $"{DiveDirector.CurrentScene()} ({GameController.CurrentSceneName}, load {loads})";
        }

        public static void Update(MonoBehaviour host)
        {
            if (!ChaosDepthsPlugin.DebugEnabled)
                return;
            Hook();
            if (Input.GetKeyDown(KeyCode.F6))
                KillAll();
            if (Input.GetKeyDown(KeyCode.F7))
                SkipStage(host);
            if (Input.GetKeyDown(KeyCode.F8))
                GodMode();
            if (Input.GetKeyDown(KeyCode.F9))
                Money();
            if (Input.GetKeyDown(KeyCode.F5))
            {
                if (InTrials())
                    Elites.SpawnShowcase(FirstPlayer(), ChaosDepthsPlugin.ShowcaseEnemy);
                else
                    ChaosDepthsPlugin.Log("Debug: F5 spawns elites in the trials only");
            }
            if (Input.GetKeyDown(KeyCode.F11))
                LogRelics();
            if (Input.GetKeyDown(KeyCode.F10))
            {
                if (running)
                    ChaosDepthsPlugin.Log("Auto-dive already running");
                else
                    host.StartCoroutine(AutoDive());
            }
        }

        private static DebugMenu Menu()
        {
            try
            {
                return GameController.debugMenu ?? DebugMenu.instance;
            }
            catch
            {
                return null;
            }
        }

        public static void KillAll()
        {
            KillAll(false);
        }

        public static int KillAll(bool quiet)
        {
            try
            {
                // First a huge hit from the player on everyone the game lists as an enemy or boss,
                // and on every Enemy component in the scene (the floor's mini-boss among them), so
                // the kills are credited (experience, loot); then the developer menu for anything
                // left. Invulnerability is lifted first, since mini-bosses sit behind it between
                // their hurt windows.
                Player player = FirstPlayer();
                var seen = new System.Collections.Generic.HashSet<int>();
                int hit = 0;
                if (GameController.enemies != null)
                    foreach (GameObject go in new System.Collections.Generic.List<GameObject>(GameController.enemies))
                        hit += Hit(go, player, seen);
                if (GameController.bosses != null)
                    foreach (GameObject go in new System.Collections.Generic.List<GameObject>(GameController.bosses))
                        hit += Hit(go, player, seen);
                foreach (Enemy enemy in UnityEngine.Object.FindObjectsOfType<Enemy>())
                    if (enemy != null)
                        hit += Hit(enemy.gameObject, player, seen);
                DebugMenu menu = Menu();
                if (menu != null)
                    menu.WrathOfTheDeveloper();
                if (!quiet || hit > 0)
                    ChaosDepthsPlugin.Log($"Debug: hit {hit} enemies as the player" + (menu != null ? ", then wrath of the developer" : ""));
                return hit;
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug kill all: {e.Message}");
                return 0;
            }
        }

        // If the arena's boss has been dead for 10 s and its exit still hasn't come, run the
        // arena's own exit routine.
        private static void ArenaExitIfBossDead(ref float deadFor, float step)
        {
            try
            {
                BossRoomEventHandler room = UnityEngine.Object.FindObjectOfType<BossRoomEventHandler>();
                if (room == null || room.boss == null || room.boss.health == null || room.boss.health.CurrentHealthValue > 0)
                {
                    deadFor = 0f;
                    return;
                }
                deadFor += step;
                if (deadFor < 10f || room.exitSpawned)
                    return;
                deadFor = -1000f; // once
                ChaosDepthsPlugin.Log("Debug: the boss has been dead 10 s with no exit; running the arena's exit routine");
                room.StartCoroutine(room.ActivateExitPortal());
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug arena exit: {e.Message}");
            }
        }

        // A boss at zero health that hasn't entered its dead state (its hurt window or intro held
        // it) is moved there, as its own death would.
        private static void ForceDeadState(Boss boss)
        {
            try
            {
                FSM fsm = boss.fsm;
                if (fsm == null || fsm.states == null)
                    return;
                if (fsm.currentState is Entity.DeadState)
                    return;
                string current = fsm.currentStateName ?? "";
                // The arena runs its own outro once the boss is down; pushing 'Dead' into it
                // fights that. Only a boss still in its intro or fight 3 s after dying is pushed.
                if (current.IndexOf("Outro", StringComparison.OrdinalIgnoreCase) >= 0)
                    return;
                int id = boss.GetInstanceID();
                if (bossZeroSince.Key != id)
                    bossZeroSince = new KeyValuePair<int, float>(id, Time.unscaledTime);
                if (Time.unscaledTime - bossZeroSince.Value < 3f)
                    return;
                foreach (KeyValuePair<string, IState> pair in fsm.states)
                {
                    if (pair.Value is Entity.DeadState)
                    {
                        ChaosDepthsPlugin.Log($"Debug: {boss.GetType().Name} at 0 health in state '{fsm.currentStateName}'; moving it to '{pair.Key}'");
                        fsm.ChangeState(pair.Key, false);
                        return;
                    }
                }
                ChaosDepthsPlugin.Log($"Debug: {boss.GetType().Name} at 0 health but has no dead state (states: {string.Join(", ", new List<string>(fsm.states.Keys).ToArray())})");
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug boss death: {e.Message}");
            }
        }

        // Boss arenas: the boss only appears once a wizard walks in past the trigger. Fires the
        // arena's trigger with the wizard's collider and moves the wizard inside. Returns true if
        // an arena handler was found.
        private static bool EnterArena()
        {
            try
            {
                BossRoomEventHandler room = UnityEngine.Object.FindObjectOfType<BossRoomEventHandler>();
                if (room == null)
                    return false;
                if (room.bossSpawned || room.boss != null)
                {
                    string hp = room.boss != null && room.boss.health != null ? room.boss.health.CurrentHealthValue.ToString() : "?";
                    ChaosDepthsPlugin.Log($"Debug: the arena's boss is here: health {hp}, defeated flag {room.bossDefeated}, hits ignored {bossHitsIgnored}");
                    return true;
                }
                bossHitsIgnored = 0;
                Player player = FirstPlayer();
                if (player != null)
                {
                    Vector3 inside = room.triggeredPosition;
                    if (inside == Vector3.zero)
                        inside = room.bossSpawnPosition + new Vector3(0f, -4f, 0f);
                    player.transform.position = inside;
                    Collider2D col = player.GetComponent<Collider2D>();
                    if (col != null)
                        room.OnTriggerEnter2D(col);
                }
                ChaosDepthsPlugin.Log($"Debug: walked into the boss arena (boss spawned {room.bossSpawned})");
                return true;
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug arena: {e.Message}");
                return false;
            }
        }

        private static int Hit(GameObject go, Player player, System.Collections.Generic.HashSet<int> seen)
        {
            if (go == null || !go.activeInHierarchy || !seen.Add(go.GetInstanceID()))
                return 0;
            Entity entity = go.GetComponent<Entity>();
            if (entity == null || entity.health == null || entity is Player)
                return 0;
            try
            {
                entity.health.invulnerable = false;
                var mini = entity as MiniBoss;
                if (mini != null)
                    mini.AllowHurtState(true, true, true);
                var boss = entity as Boss;
                if (boss != null)
                {
                    // Out of health: leave its gates alone and make sure it runs its death, so the
                    // arena plays the outro and spawns its exit.
                    if (entity.health.CurrentHealthValue <= 0)
                    {
                        ForceDeadState(boss);
                        return 0;
                    }
                    // Bosses ignore damage until their intro is over, and again between hurt windows.
                    if (boss.transitionToIntro)
                        boss.ExitIntro();
                    boss.SetInvulnerable(false);
                    boss.AllowHurtState(true);
                    int before = entity.health.CurrentHealthValue;
                    if (before > 1 && bossHitsIgnored >= 4)
                        entity.health.CurrentHealthValue = 1; // the hits aren't landing: leave one point for a credited kill
                    var probe = new AttackInfo { damage = 999999, entity = player, skillCategory = player != null ? player.skillCategory : "" };
                    entity.health.TakeDamage(probe, player);
                    int after = entity.health.CurrentHealthValue;
                    if (after >= before && before > 0)
                        bossHitsIgnored++;
                    else
                        bossHitsIgnored = 0;
                    if (after <= 0)
                        ForceDeadState(boss);
                    return 1;
                }
            }
            catch
            {
            }
            var info = new AttackInfo { damage = 999999, entity = player, skillCategory = player != null ? player.skillCategory : "" };
            entity.health.TakeDamage(info, player);
            return 1;
        }

        // The floor's exit portal only appears after the exit room's mini-boss is beaten, and the
        // mini-boss only appears once a wizard steps onto the skull altar (MiniBossActivationCircle).
        // Returns true if the altar was activated now.
        // This floor's skull altar. The game's static "current" altar can be a stale one from the
        // previous floor, and FindObjectsOfTypeAll also returns prefabs, so candidates must be
        // live objects of the loaded scene: active ones first, then the nearest to the exit room
        // (or the wizard). The choice is logged.
        private static MiniBossActivationCircle PickAltar()
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            ExitRoomEventHandler room = UnityEngine.Object.FindObjectOfType<ExitRoomEventHandler>();
            Player wizard = FirstPlayer();
            Vector3? anchor = room != null ? room.transform.position : (wizard != null ? wizard.transform.position : (Vector3?)null);
            MiniBossActivationCircle best = null;
            float bestScore = float.MaxValue;
            var seen = new List<string>();
            foreach (MiniBossActivationCircle any in Resources.FindObjectsOfTypeAll<MiniBossActivationCircle>())
            {
                if (any == null || any.gameObject == null || !any.gameObject.scene.IsValid())
                    continue;
                bool here = any.gameObject.scene == scene || any.gameObject.scene.name == "DontDestroyOnLoad";
                bool active = any.gameObject.activeInHierarchy;
                float dist = anchor.HasValue ? Vector2.Distance(any.transform.position, anchor.Value) : 0f;
                seen.Add($"{any.name} (active {active}, scene '{any.gameObject.scene.name}', {dist:0} from {(room != null ? "the exit room" : "the wizard")})");
                if (!here)
                    continue;
                float score = dist + (active ? 0f : 10000f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = any;
                }
            }
            if (seen.Count > 1 || best == null)
                ChaosDepthsPlugin.Log($"Debug: skull altars found: {(seen.Count == 0 ? "none" : string.Join("; ", seen.ToArray()))}");
            if (best != null)
                ChaosDepthsPlugin.Log($"Debug: using skull altar '{best.name}' (active {best.gameObject.activeInHierarchy})");
            return best;
        }

        private static bool ActivateAltar()
        {
            try
            {
                MiniBossActivationCircle circle = PickAltar();
                // The developer skip sometimes leaves the wizard on the start pad: put the wizard
                // on the circle, so the mini-boss and its exit spawn where the sweeps reach.
                Player wizard = FirstPlayer();
                if (wizard != null && circle != null)
                {
                    float away = Vector2.Distance(wizard.transform.position, circle.transform.position);
                    if (away > 3f)
                    {
                        wizard.transform.position = circle.transform.position;
                        ChaosDepthsPlugin.Log($"Debug: the wizard was {away:0} units from the skull altar; moved onto it");
                    }
                }
                if (circle == null)
                {
                    ExitRoomEventHandler room = UnityEngine.Object.FindObjectOfType<ExitRoomEventHandler>();
                    if (room != null && wizard != null && Vector2.Distance(wizard.transform.position, room.transform.position) > 6f)
                    {
                        wizard.transform.position = room.transform.position;
                        ChaosDepthsPlugin.Log("Debug: moved the wizard to the exit room");
                    }
                    if (room == null)
                    {
                        ChaosDepthsPlugin.Log("Debug: no skull altar or exit room handler in this scene");
                        return false;
                    }
                    room.Activate();
                    ChaosDepthsPlugin.Log("Debug: activated the exit room directly (no skull altar)");
                    return true;
                }
                if (circle.miniBossActivated)
                {
                    ChaosDepthsPlugin.Log("Debug: the skull altar is already active");
                    return false;
                }
                if (circle.player == null)
                    circle.player = FirstPlayer();
                circle.Activate();
                ChaosDepthsPlugin.Log($"Debug: stepped on the skull altar; mini-boss activated = {circle.miniBossActivated}");
                return true;
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug altar: {e.Message}");
                return false;
            }
        }

        // The exit portal the game spawns once the floor (or the boss) is beaten, or null.
        private static ExitPortal FindExitPortal()
        {
            try
            {
                foreach (ExitPortal portal in UnityEngine.Object.FindObjectsOfType<ExitPortal>())
                    if (portal != null && portal.gameObject.activeInHierarchy && !(portal is EndingAltar))
                        return portal;
            }
            catch
            {
            }
            return null;
        }

        // Skip to the exit room, summon the mini-boss, kill everything that appears until the exit
        // portal spawns (up to maxSeconds), then take it. Yields; the caller checks the scene.
        private static IEnumerator ClearFloor(float maxSeconds)
        {
            int load = loads;
            KillAll();
            yield return new WaitForSecondsRealtime(1f);
            bool bossScene = false;
            try
            {
                bossScene = NextLevelLoader.InBossScene("");
            }
            catch
            {
            }
            if (bossScene)
                ChaosDepthsPlugin.Log("Debug: boss arena, no developer skip (it marks the boss defeated without a fight)");
            else
            {
                try
                {
                    Menu()?.SkipStage();
                    ChaosDepthsPlugin.Log("Debug: skip stage (developer menu: teleports to the exit room)");
                }
                catch (Exception e)
                {
                    ChaosDepthsPlugin.LogError($"Debug skip: {e.Message}");
                }
            }
            yield return new WaitForSecondsRealtime(1.5f);
            if (loads != load)
                yield break;
            bool arena = false;
            bool activated = false;
            if (FindExitPortal() == null)
            {
                arena = EnterArena();
                if (!arena)
                    activated = ActivateAltar();
            }
            float waited = 0f;
            int kills = 0, hits = 0;
            float bossDeadFor = 0f;
            ExitPortal exit = null;
            while (loads == load && waited < maxSeconds)
            {
                exit = FindExitPortal();
                if (exit != null)
                    break;
                hits += KillAll(true);
                kills++;
                if (arena && FindExitPortal() == null)
                    ArenaExitIfBossDead(ref bossDeadFor, 0.5f);
                if (kills % 10 == 0 && FindExitPortal() == null)
                {
                    if (arena)
                        EnterArena(); // the intro may not have taken the first time
                    else if (!activated)
                        activated = ActivateAltar(); // the altar may only exist once the room is reached
                }
                yield return new WaitForSecondsRealtime(0.5f);
                waited += 0.5f;
            }
            ChaosDepthsPlugin.Log($"Debug: {kills} kill sweep(s) hit {hits} enemies in {waited:0.0} s");
            if (loads != load)
                yield break;
            if (exit != null)
            {
                ChaosDepthsPlugin.Log($"Debug: exit portal '{exit.name}' appeared");
                yield return new WaitForSecondsRealtime(1f);
                try
                {
                    if (exit.nextLevelLoader != null)
                    {
                        exit.nextLevelLoader.forceTriggerExit = true;
                        ChaosDepthsPlugin.Log($"Debug: taking the exit portal to '{exit.nextLevelLoader.nextLevelName}'");
                        exit.nextLevelLoader.LoadNextLevel();
                    }
                    else
                    {
                        ChaosDepthsPlugin.Log("Debug: taking the exit portal (no loader on it; ExitPortal.LoadNextLevel)");
                        exit.LoadNextLevel();
                    }
                }
                catch (Exception e)
                {
                    ChaosDepthsPlugin.LogError($"Debug exit portal: {e.Message}");
                }
                // The exit shows the "STAGE CLEARED" summary and waits for Continue.
                float summaryWait = 0f;
                while (loads == load && summaryWait < 8f)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    summaryWait += 0.5f;
                    if (Continue())
                        break;
                }
                yield return new WaitForSecondsRealtime(3f);
            }
            else
                ChaosDepthsPlugin.Log($"Debug: no exit portal appeared in {maxSeconds:0} s; forcing the exit");
            if (loads == load)
            {
                ForceExit();
                float summaryWait = 0f;
                while (loads == load && summaryWait < 8f)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    summaryWait += 0.5f;
                    if (Continue())
                        break;
                }
            }
        }

        // Presses Continue on the stage-cleared summary, if it is up: loads its next level the
        // way the button does. Returns true if it did.
        private static bool Continue()
        {
            try
            {
                LevelSummary summary = LevelSummary.instance;
                if (summary == null || !summary.activated || string.IsNullOrEmpty(summary.nextLevelName))
                    return false;
                string next = summary.nextLevelName;
                ChaosDepthsPlugin.Log($"Debug: Continue on the stage summary -> '{next}'");
                try
                {
                    summary.Deactivate();
                }
                catch
                {
                }
                GameController.LoadLevel(next);
                return true;
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug continue: {e.Message}");
                return false;
            }
        }

        // The developer menu's skip only teleports the wizard to the floor's exit room (the first
        // test showed), so F7 goes straight for the exit portal.
        public static void SkipStage(MonoBehaviour host)
        {
            host.StartCoroutine(SkipAndForce());
        }

        private static IEnumerator SkipAndForce()
        {
            yield return ClearFloor(30f);
        }

        // Makes the floor's exit load its level. First the floor's own exit portal (a NextLevelLoader
        // whose destination is a trial scene), inactive ones included, since the exit may sit
        // switched off until the last room is cleared: forceTriggerExit, then LoadNextLevel. With
        // none to be found, the next scene is worked out the way the game would (the same realm
        // scene for another floor, the realm's boss scene after the last floor, the next realm after
        // a boss) and loaded through the game's own SetLevelInfo and LoadLevel. Returns the
        // destination, or null.
        private static string ForceExit()
        {
            try
            {
                var names = new System.Collections.Generic.List<string>();
                NextLevelLoader exit = null, fallback = null;
                foreach (NextLevelLoader loader in Resources.FindObjectsOfTypeAll<NextLevelLoader>())
                {
                    if (loader == null || loader.gameObject == null || !loader.gameObject.scene.IsValid())
                        continue; // a prefab, not something in this scene
                    bool active = loader.gameObject.activeInHierarchy;
                    names.Add($"{loader.name}{(active ? "" : " (inactive)")} -> '{loader.nextLevelName}'");
                    if (string.IsNullOrEmpty(loader.nextLevelName))
                        continue;
                    if (IsTrialScene(loader.nextLevelName))
                    {
                        if (exit == null || (!exit.gameObject.activeInHierarchy && active))
                            exit = loader;
                    }
                    else if (fallback == null)
                        fallback = loader;
                }
                ChaosDepthsPlugin.Log($"Debug: exit portals here: {(names.Count == 0 ? "none" : string.Join("; ", names.ToArray()))}");
                if (exit == null)
                    exit = fallback;
                if (exit != null)
                {
                    if (!exit.gameObject.activeSelf)
                        exit.gameObject.SetActive(true);
                    exit.forceTriggerExit = true;
                    exit.LoadNextLevel();
                    ChaosDepthsPlugin.Log($"Debug: forced the exit portal '{exit.name}' to load '{exit.nextLevelName}'");
                    return exit.nextLevelName;
                }
                return ComputedSkip();
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug force exit: {e.Message}");
                return null;
            }
        }

        // No exit portal: the next scene by the game's own counters.
        private static string ComputedSkip()
        {
            string current = DiveDirector.CurrentScene();
            string next = null;
            string why;
            if (DiveDirector.IsFinalBoss(current))
            {
                next = "Hub";
                why = "nothing comes after the final boss";
            }
            else if (NextLevelLoader.InBossScene(current))
            {
                System.Collections.Generic.List<string> order = GameController.levelNameList;
                int nextTier = GameController.tierCount + 1;
                int realms = order != null ? order.Count : 0;
                if (GameController.maxBaseTierCount > 0)
                    realms = Mathf.Min(realms, GameController.maxBaseTierCount);
                if (order != null && nextTier >= realms)
                {
                    next = "FinalBossLevel";
                    why = $"after the last realm's boss ({realms} realms a run)";
                }
                else if (order != null && nextTier < order.Count)
                {
                    next = order[nextTier];
                    why = $"after the boss, realm {nextTier + 1} of {realms}";
                }
                else
                {
                    next = "Hub";
                    why = "after the last boss";
                }
            }
            else
            {
                string tierScene = NextLevelLoader.GetCurrentTierScene(-1);
                if (string.IsNullOrEmpty(tierScene))
                    tierScene = current;
                if (GameController.stageCount <= GameController.stagesPerTier)
                {
                    next = tierScene;
                    why = $"floor {GameController.stageCount + 1} of this realm (counter {GameController.stagesPerTier})";
                }
                else
                {
                    string boss;
                    if (GameController.bossLevelNameDict != null && GameController.bossLevelNameDict.TryGetValue(tierScene, out boss))
                    {
                        next = boss;
                        why = "the realm's boss after its last floor";
                    }
                    else
                    {
                        next = tierScene;
                        why = "no boss scene known for this realm, another floor";
                    }
                }
            }
            ChaosDepthsPlugin.Log($"Debug: no exit portal; loading '{next}' ({why}) through the game's SetLevelInfo and LoadLevel");
            try
            {
                NextLevelLoader.SetLevelInfo(next);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug SetLevelInfo: {e.Message}");
            }
            try
            {
                GameController.LoadLevel(next);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug LoadLevel: {e.Message}");
                return null;
            }
            return next;
        }

        private static bool IsTrialScene(string scene)
        {
            try
            {
                return NextLevelLoader.InTierScene(scene) || NextLevelLoader.InBossScene(scene);
            }
            catch
            {
                return false;
            }
        }

        public static void GodMode()
        {
            try
            {
                DebugMenu menu = Menu();
                if (menu != null)
                {
                    menu.GodMode();
                    ChaosDepthsPlugin.Log($"Debug: god mode {(DebugMenu.godModeOn ? "on" : "off")}");
                    return;
                }
                Player player = FirstPlayer();
                if (player != null && player.health != null)
                {
                    player.health.invulnerable = !player.health.invulnerable;
                    ChaosDepthsPlugin.Log($"Debug: invulnerable {player.health.invulnerable}");
                }
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug god mode: {e.Message}");
            }
        }

        public static void Money()
        {
            try
            {
                DebugMenu menu = Menu();
                if (menu != null)
                {
                    menu.Money();
                    ChaosDepthsPlugin.Log("Debug: money");
                }
                else if (Player.goldWallet != null)
                {
                    Player.goldWallet.Deposit(1000);
                    ChaosDepthsPlugin.Log("Debug: +1000 gold");
                }
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug money: {e.Message}");
            }
        }

        private static Player FirstPlayer()
        {
            Player[] players = GameController.activePlayers;
            if (players == null)
                return null;
            foreach (Player player in players)
                if (player != null)
                    return player;
            return null;
        }

        private static bool InTrials()
        {
            try
            {
                return NextLevelLoader.InTierScene("") || NextLevelLoader.InBossScene("");
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerator AutoDive()
        {
            running = true;
            ChaosDepthsPlugin.Log($"Auto-dive: starting in {Where()}");
            if (!InTrials())
            {
                ChaosDepthsPlugin.Log("Auto-dive: enter the trials portal first, then press F10 on the first floor");
                running = false;
                yield break;
            }
            GodModeOn();
            GiveRelics();
            int step = 0;
            while (step < MaxSteps)
            {
                step++;
                int load = loads;
                string scene = Where();
                ChaosDepthsPlugin.Log($"Auto-dive step {step}: in {scene} (stage {GameController.stageCount}, tier {GameController.tierCount})");
                yield return ClearFloor(DiveDirector.IsFinalBoss(DiveDirector.CurrentScene()) ? 40f : 90f);
                string forced = null;
                if (loads == load)
                    forced = GameController.queuedLevelName;
                // Wait for the scene to change (up to 20 s); as a last resort load it directly.
                float waited = 0f;
                while (loads == load && waited < 20f)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    waited += 0.5f;
                }
                if (loads == load && !string.IsNullOrEmpty(forced))
                {
                    ChaosDepthsPlugin.Log($"Auto-dive: the portal didn't load '{forced}'; loading it directly");
                    try
                    {
                        GameController.LoadLevel(forced);
                    }
                    catch (Exception e)
                    {
                        ChaosDepthsPlugin.LogError($"Auto-dive load: {e.Message}");
                    }
                    waited = 0f;
                    while (loads == load && waited < 20f)
                    {
                        yield return new WaitForSecondsRealtime(0.5f);
                        waited += 0.5f;
                    }
                }
                if (loads == load)
                {
                    ChaosDepthsPlugin.Log($"Auto-dive: the scene didn't change after a skip in {scene}; stopping");
                    break;
                }
                yield return new WaitForSecondsRealtime(3f);
                if (!InTrials())
                {
                    ChaosDepthsPlugin.Log($"Auto-dive finished: left the trials for {Where()} after {step} floor(s)");
                    break;
                }
            }
            if (step >= MaxSteps)
                ChaosDepthsPlugin.Log($"Auto-dive stopped after {MaxSteps} steps, still in {Where()}");
            running = false;
        }

        // Every relic the game knows: ID, display name and class, to pick AutoDiveRelics from.
        public static void LogRelics()
        {
            try
            {
                if (LootManager.completeItemDict == null || LootManager.completeItemDict.Count == 0)
                {
                    ChaosDepthsPlugin.Log("Debug: the relic list isn't loaded yet; press F11 again in a run");
                    return;
                }
                var lines = new List<string>();
                foreach (KeyValuePair<string, Item> pair in LootManager.completeItemDict)
                {
                    string name;
                    try
                    {
                        name = TextManager.GetItemName(pair.Key);
                    }
                    catch
                    {
                        name = "?";
                    }
                    bool locked = LootManager.lockedItemIDList != null && LootManager.lockedItemIDList.Contains(pair.Key);
                    lines.Add($"{pair.Key} = {name} ({(pair.Value != null ? pair.Value.GetType().Name : "?")}){(locked ? " [locked]" : "")}");
                }
                lines.Sort();
                ChaosDepthsPlugin.Log($"Debug: {lines.Count} relics:\n  " + string.Join("\n  ", lines.ToArray()));
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug relic list: {e.Message}");
            }
        }

        // Gives the first player the relics named in [Debug] AutoDiveRelics, once each.
        private static void GiveRelics()
        {
            string list = ChaosDepthsPlugin.AutoDiveRelics;
            if (string.IsNullOrEmpty(list))
                return;
            Player player = FirstPlayer();
            if (player == null || player.inventory == null)
                return;
            foreach (string raw in list.Split(','))
            {
                string id = raw.Trim();
                if (id.Length == 0)
                    continue;
                try
                {
                    if (player.inventory.ContainsItem(id))
                    {
                        ChaosDepthsPlugin.Log($"Auto-dive: already holding relic '{id}'");
                        continue;
                    }
                    bool locked = LootManager.lockedItemIDList != null && LootManager.lockedItemIDList.Contains(id);
                    bool added = player.inventory.AddItem(id, true, true);
                    ChaosDepthsPlugin.Log($"Auto-dive: relic '{id}'{(locked ? " (still locked in this save)" : "")} {(added ? "given" : "could not be given (unknown ID? press F11 for the list)")}");
                }
                catch (Exception e)
                {
                    ChaosDepthsPlugin.LogError($"Auto-dive relic '{id}': {e.Message}");
                }
            }
        }

        private static void GodModeOn()
        {
            try
            {
                DebugMenu menu = Menu();
                if (menu != null)
                {
                    if (!DebugMenu.godModeOn)
                        menu.GodMode();
                    ChaosDepthsPlugin.Log($"Auto-dive: god mode {(DebugMenu.godModeOn ? "on" : "could not be turned on")}");
                }
                else
                {
                    Player player = FirstPlayer();
                    if (player != null && player.health != null)
                    {
                        player.health.invulnerable = true;
                        ChaosDepthsPlugin.Log("Auto-dive: player invulnerable (no developer menu object)");
                    }
                }
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Auto-dive god mode: {e.Message}");
            }
        }
    }
}
