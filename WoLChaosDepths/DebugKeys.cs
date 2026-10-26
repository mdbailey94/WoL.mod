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
                SkipStage();
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
                DebugMenu menu = Menu();
                if (menu != null)
                {
                    menu.WrathOfTheDeveloper();
                    ChaosDepthsPlugin.Log("Debug: wrath of the developer (all enemies killed)");
                    return;
                }
                // No developer menu object: a huge hit on everyone the game lists as an enemy.
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
                ChaosDepthsPlugin.Log($"Debug: killed {hit} enemies (no developer menu object)");
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug kill all: {e.Message}");
            }
        }

        public static void SkipStage()
        {
            try
            {
                DebugMenu menu = Menu();
                if (menu != null)
                {
                    menu.SkipStage();
                    ChaosDepthsPlugin.Log("Debug: skip stage (developer menu)");
                    return;
                }
                NextLevelLoader loader = UnityEngine.Object.FindObjectOfType<NextLevelLoader>();
                if (loader == null)
                {
                    ChaosDepthsPlugin.Log("Debug: no exit portal in this scene to skip with");
                    return;
                }
                loader.forceTriggerExit = true;
                ChaosDepthsPlugin.Log($"Debug: forcing the exit portal (to '{loader.nextLevelName}')");
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Debug skip: {e.Message}");
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
                SkipStage();
                // Wait for the scene to change (up to 25 s), then let it settle.
                float waited = 0f;
                while (GameController.CurrentSceneName == scene && waited < 25f)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    waited += 0.5f;
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
                }
                else
                {
                    Player player = FirstPlayer();
                    if (player != null && player.health != null)
                        player.health.invulnerable = true;
                }
            }
            catch
            {
            }
        }
    }
}
