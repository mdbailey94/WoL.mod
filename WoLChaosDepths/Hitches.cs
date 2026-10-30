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
            SceneManager.activeSceneChanged += (from, to) =>
            {
                quietUntil = Time.unscaledTime + 2f;
                probeAt = Time.unscaledTime + 2f;
            };
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
            if (probeAt > 0f && Time.unscaledTime >= probeAt)
            {
                probeAt = 0f;
                Probe();
            }
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

        // Long-run lag (0.16.3): once per floor, two seconds after it loads, what's alive. The
        // game's objects, textures, sprites and materials (loaded assets included, so it's the
        // growth from floor to floor that matters, not the totals), the managed heap, and the six
        // most common object names: something made per cast or per floor and never destroyed
        // shows up as a name whose count keeps climbing. A few tens of milliseconds, once a floor.
        private static float probeAt;
        private static int probes;

        private static void Probe()
        {
            try
            {
                probes++;
                GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
                int textures = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int sprites = Resources.FindObjectsOfTypeAll<Sprite>().Length;
                int materials = Resources.FindObjectsOfTypeAll<Material>().Length;
                var names = new System.Collections.Generic.Dictionary<string, int>();
                int live = 0;
                foreach (GameObject go in objects)
                {
                    if (go == null || !go.scene.IsValid())
                        continue;   // a prefab or asset, not something in the game
                    live++;
                    string n = go.name.Replace("(Clone)", "").Trim();
                    int c;
                    names.TryGetValue(n, out c);
                    names[n] = c + 1;
                }
                var top = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(names);
                top.Sort((a, b) => b.Value.CompareTo(a.Value));
                var parts = new System.Collections.Generic.List<string>();
                for (int i = 0; i < top.Count && i < 6; i++)
                    parts.Add($"{top[i].Key} {top[i].Value}");
                long heap = System.GC.GetTotalMemory(false) / (1024 * 1024);
                ChaosDepthsPlugin.Log($"Probe {probes} ({SceneManager.GetActiveScene().name}): {live} objects in the scene ({objects.Length} with assets), " +
                    $"{textures} textures, {sprites} sprites, {materials} materials, managed heap {heap} MB; " +
                    $"Chaos Depths: {Pickups.LiveCount} pickups, {EliteMarkers.LiveCount} elite markers; most common: {string.Join(", ", parts.ToArray())}");
            }
            catch (System.Exception e)
            {
                ChaosDepthsPlugin.Log($"Probe failed: {e.Message}");
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
