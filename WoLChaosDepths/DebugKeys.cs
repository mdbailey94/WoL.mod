using System;
using System.Collections;
using UnityEngine;

namespace WoLChaosDepths
{
    // Test keys, off by default ([Debug] Enabled). They drive the game's own developer menu
    // (hidden in release builds, but its methods still work), with a plain fallback when that
    // object isn't there:
    //   F6  kill every enemy in the room       F7  skip to the next floor
    //   F8  god mode on/off                     F9  gold
    //   F10 auto-dive: kill, skip, kill, skip... until the run leaves the trials (e.g. the dive
    //       director sends you back to the Hub), logging each step. Start it on a trial floor.
    public static class DebugKeys
    {
        private const int MaxSteps = 40;
        private static bool running;

        public static void Update(MonoBehaviour host)
        {
            if (!ChaosDepthsPlugin.DebugEnabled)
                return;
            if (Input.GetKeyDown(KeyCode.F6))
                KillAll();
            if (Input.GetKeyDown(KeyCode.F7))
                SkipStage(host);
            if (Input.GetKeyDown(KeyCode.F8))
                GodMode();
            if (Input.GetKeyDown(KeyCode.F9))
                Money();
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
            try
            {
                // First a huge hit from the player on everyone the game lists as an enemy, so the
                // kills are credited (experience, loot); then the developer menu for anything left.
                Player player = FirstPlayer();
                int hit = 0;
                if (GameController.enemies != null)
                {
                    foreach (GameObject go in new System.Collections.Generic.List<GameObject>(GameController.enemies))
                    {
                        if (go == null || !go.activeInHierarchy)
                            continue;
                        Entity entity = go.GetComponent<Entity>();
                        if (entity == null || entity.health == null)
                            continue;
                        var info = new AttackInfo { damage = 999999, entity = player, skillCategory = player != null ? player.skillCategory : "" };
                        entity.health.TakeDamage(info, player);
                        hit++;
                    }
                }
                DebugMenu menu = Menu();
                if (menu != null)
                    menu.WrathOfTheDeveloper();
                ChaosDepthsPlugin.Log($"Debug: hit {hit} enemies as the player" + (menu != null ? ", then wrath of the developer" : ""));
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug kill all: {e.Message}");
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
            string scene = GameController.CurrentSceneName;
            try
            {
                Menu()?.SkipStage();
                ChaosDepthsPlugin.Log("Debug: skip stage (developer menu: teleports to the exit room)");
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug skip: {e.Message}");
            }
            yield return new WaitForSecondsRealtime(1.5f);
            if (GameController.CurrentSceneName == scene)
                ForceExit();
        }

        // Makes the floor's exit portal load its level: the loader whose destination is a trial
        // scene, by forceTriggerExit, then LoadNextLevel, then the game's LoadLevel itself.
        private static string ForceExit()
        {
            try
            {
                NextLevelLoader[] loaders = UnityEngine.Object.FindObjectsOfType<NextLevelLoader>();
                var names = new System.Collections.Generic.List<string>();
                NextLevelLoader exit = null;
                foreach (NextLevelLoader loader in loaders)
                {
                    names.Add($"{loader.name} -> '{loader.nextLevelName}'");
                    if (exit == null && !string.IsNullOrEmpty(loader.nextLevelName) && IsTrialScene(loader.nextLevelName))
                        exit = loader;
                }
                if (exit == null)
                {
                    foreach (NextLevelLoader loader in loaders)
                        if (!string.IsNullOrEmpty(loader.nextLevelName))
                        {
                            exit = loader;
                            break;
                        }
                }
                ChaosDepthsPlugin.Log($"Debug: exit portals here: {(names.Count == 0 ? "none" : string.Join("; ", names.ToArray()))}");
                if (exit == null)
                    return null;
                exit.forceTriggerExit = true;
                exit.LoadNextLevel();
                ChaosDepthsPlugin.Log($"Debug: forced the exit portal '{exit.name}' to load '{exit.nextLevelName}'");
                return exit.nextLevelName;
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug force exit: {e.Message}");
                return null;
            }
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
            string startScene = GameController.CurrentSceneName;
            ChaosDepthsPlugin.Log($"Auto-dive: starting in '{startScene}'");
            if (!InTrials())
            {
                ChaosDepthsPlugin.Log("Auto-dive: enter the trials portal first, then press F10 on the first floor");
                running = false;
                yield break;
            }
            GodModeOn();
            int step = 0;
            while (step < MaxSteps)
            {
                step++;
                string scene = GameController.CurrentSceneName;
                ChaosDepthsPlugin.Log($"Auto-dive step {step}: in '{scene}' (stage {GameController.stageCount}, tier {GameController.tierCount})");
                KillAll();
                yield return new WaitForSecondsRealtime(1.5f);
                KillAll();
                yield return new WaitForSecondsRealtime(1f);
                try
                {
                    Menu()?.SkipStage();
                }
                catch
                {
                }
                yield return new WaitForSecondsRealtime(1.5f);
                string forced = null;
                if (GameController.CurrentSceneName == scene)
                    forced = ForceExit();
                // Wait for the scene to change (up to 20 s); as a last resort load it directly.
                float waited = 0f;
                while (GameController.CurrentSceneName == scene && waited < 20f)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    waited += 0.5f;
                }
                if (GameController.CurrentSceneName == scene && !string.IsNullOrEmpty(forced))
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
                    while (GameController.CurrentSceneName == scene && waited < 20f)
                    {
                        yield return new WaitForSecondsRealtime(0.5f);
                        waited += 0.5f;
                    }
                }
                if (GameController.CurrentSceneName == scene)
                {
                    ChaosDepthsPlugin.Log($"Auto-dive: the scene didn't change after a skip in '{scene}'; stopping");
                    break;
                }
                yield return new WaitForSecondsRealtime(3f);
                if (!InTrials())
                {
                    ChaosDepthsPlugin.Log($"Auto-dive finished: left the trials for '{GameController.CurrentSceneName}' after {step} floor(s)");
                    break;
                }
            }
            if (step >= MaxSteps)
                ChaosDepthsPlugin.Log($"Auto-dive stopped after {MaxSteps} steps, still in '{GameController.CurrentSceneName}'");
            running = false;
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
