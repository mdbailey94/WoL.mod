using System.Diagnostics;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WoLChaosDepths
{
    // Task #26 (frame-rate drops): a frame-time probe for playtests. With [Debug] HitchLog on,
    // every frame slower than HitchMs is logged with whether the garbage collector ran in it and
    // how long Chaos Depths' own Update and OnGUI took that frame, so a test can tell this mod's
    // cost from the game's (and the other mods'), and a collection pause from steady load. Every
    // 10 s a line gives that stretch's average and worst frame. Scene loads are left out.
    public static class Hitches
    {
        private static ConfigEntry<bool> on;
        private static ConfigEntry<float> hitchMs;

        private static readonly Stopwatch watch = new Stopwatch();
        private static double updateMs, guiMs;
        private static int lastGc;
        private static float windowStart, quietUntil;
        private static int frames, hitches, collections;
        private static float total, worst;
        private static double worstOwn;

        public static bool On => on != null && on.Value;

        public static void Bind(ConfigFile config)
        {
            on = config.Bind("Debug", "HitchLog", false,
                "Log every frame slower than HitchMs (with whether the garbage collector ran and Chaos Depths' own share of it), and every 10 s the average and worst frame. For finding frame-rate drops (task #26).");
            hitchMs = config.Bind("Debug", "HitchMs", 34f,
                "A frame at least this long (milliseconds) is logged by HitchLog. 34 = two frames at 60 fps.");
            SceneManager.activeSceneChanged += (from, to) => quietUntil = Time.unscaledTime + 2f;
            // The frame cap this probe led to (0.14.6) moved to Mod Menu 0.1.5 ([Display]
            // FrameCap, 120), which runs in the base game too.
        }

        // First thing in the plugin's Update: the frame that just ended.
        public static void FrameStart()
        {
            if (!On)
                return;
            float ms = Time.unscaledDeltaTime * 1000f;
            int gc = System.GC.CollectionCount(0);
            bool collected = gc != lastGc;
            lastGc = gc;
            double own = updateMs + guiMs;
            if (Time.unscaledTime >= quietUntil)
            {
                frames++;
                total += ms;
                if (collected)
                    collections++;
                if (ms > worst)
                    worst = ms;
                if (own > worstOwn)
                    worstOwn = own;
                if (ms >= hitchMs.Value)
                {
                    hitches++;
                    ChaosDepthsPlugin.Log($"Hitch: {ms:0} ms frame{(collected ? ", the garbage collector ran" : "")} in {SceneManager.GetActiveScene().name}; " +
                        $"Chaos Depths' own Update {updateMs:0.0} ms, OnGUI {guiMs:0.0} ms");
                }
            }
            updateMs = guiMs = 0;
            if (Time.unscaledTime - windowStart >= 10f)
            {
                if (frames > 0)
                    ChaosDepthsPlugin.Log($"Frames: {frames} in the last 10 s, average {total / frames:0.0} ms ({frames * 1000f / Mathf.Max(1f, total):0} fps), " +
                        $"worst {worst:0} ms, {hitches} over {hitchMs.Value:0} ms, {collections} garbage collection(s); Chaos Depths' worst frame {worstOwn:0.0} ms");
                windowStart = Time.unscaledTime;
                frames = hitches = collections = 0;
                total = worst = 0f;
                worstOwn = 0;
            }
        }

        public static void Begin()
        {
            if (!On)
                return;
            watch.Reset();
            watch.Start();
        }

        public static void EndUpdate()
        {
            if (!On)
                return;
            watch.Stop();
            updateMs += watch.Elapsed.TotalMilliseconds;
        }

        public static void EndGui()
        {
            if (!On)
                return;
            watch.Stop();
            guiMs += watch.Elapsed.TotalMilliseconds;
        }
    }
}
