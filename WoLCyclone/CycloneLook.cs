using System.Collections.Generic;
using UnityEngine;

namespace WoLCyclone
{
    // What Cyclone looks like:
    // - the drawn funnel (CycloneFunnel): stacked spinning bands that widen and darken as it
    //   grows, debris orbiting it, a dust ring at its base, and its collapse or gust-ring burst;
    // - if that can't be drawn, the Twister arcana's little tornado instead, growing from tiny to
    //   big with the charge;
    // - the air boss's storm vortex (dust, debris and light streaks), fading in around it as it
    //   grows, for the hurricane at full power;
    // - the air vortex swirls (drawn by the state), which also stand in if these can't be made.
    //
    // The game's ones are made from its prefabs and stripped down to just their looks: every script,
    // collider and rigidbody is removed, so nothing of the original (its flight time, its hit count,
    // its attack, the storm's pull) can hurt anyone or make it vanish early. What's left (sprites,
    // animations, particles, sound) is kept looping and scaled as one with the object.
    public class CycloneLook
    {
        private const float TwisterStart = 0.3f;   // twister size, tiny to big
        private const float TwisterEnd = 2.1f;
        private const float StormFrom = 0.35f;     // progress at which the storm starts showing
        private const float StormStart = 0.4f;     // storm size as it appears and at full power
        private const float StormEnd = 1.3f;

        private CycloneFunnel funnel;
        private Look twister;
        private Look storm;
        private bool stormTried;
        private static bool loggedTwister, loggedStorm;

        // How much bigger everything is drawn (the signature's wider twister, 0.5.0).
        private readonly float size = 1f;

        public CycloneLook(Vector2 position, Player player, bool leaves = true, float widthScale = 1f, float heightScale = 1f)
        {
            size = widthScale;
            funnel = CycloneFunnel.Create(position, player, leaves, widthScale, heightScale);
            if (funnel == null)
                twister = Look.Make(() => Twister.Prefab, position, TwisterStart * size, "twister", ref loggedTwister);
        }

        // Burst at full power: the funnel blows apart in a gust ring this wide (units, radius).
        public void Burst(float radius) => EndFunnel(true, radius);

        // Let go early: the funnel collapses.
        public void DieDown() => EndFunnel(false, 0f);

        private void EndFunnel(bool blow, float radius)
        {
            // Unity null (not ?.), as a level load may have destroyed it.
            if (funnel != null)
                funnel.End(blow, radius);
            funnel = null;
        }

        public void Update(Vector2 position, float progress)
        {
            if (funnel != null)
                funnel.Set(position, progress);
            twister?.Set(position, Mathf.Lerp(TwisterStart, TwisterEnd, progress) * size, 0.5f + progress);
            if (!stormTried && progress >= StormFrom)
            {
                stormTried = true;
                storm = Look.Make(() => AirBossVortex.Prefab, position, StormStart * size, "storm vortex", ref loggedStorm);
            }
            if (storm != null)
            {
                float t = Mathf.Clamp01((progress - StormFrom) / (1f - StormFrom));
                storm.Set(position, Mathf.Lerp(StormStart, StormEnd, t) * size, 0.15f + 0.85f * t);
            }
        }

        // Cast over (or cut short by a dash, a hit or a level change): the game's effects go at
        // once; the funnel, unless already ending, collapses on its own and then removes itself.
        public void Remove()
        {
            EndFunnel(false, 0f);
            twister?.Remove();
            storm?.Remove();
            twister = null;
            storm = null;
        }

        // One stripped-down game effect.
        private class Look
        {
            private GameObject root;
            private readonly List<KeyValuePair<ParticleSystem, float>> rates = new List<KeyValuePair<ParticleSystem, float>>();

            public static Look Make(System.Func<GameObject> prefab, Vector2 position, float size, string name, ref bool logged)
            {
                GameObject go = null;
                try
                {
                    GameObject source = prefab();
                    if (source == null)
                        throw new System.Exception("no prefab");
                    go = Object.Instantiate(source, position, Quaternion.identity);
                    var look = new Look { root = go };
                    look.Strip();
                    look.Loop();
                    look.Set(position, size, 0.5f);
                    if (!logged)
                    {
                        logged = true;
                        CyclonePlugin.Log($"Cyclone: using the game's {name}");
                    }
                    return look;
                }
                catch (System.Exception e)
                {
                    if (go != null)
                        Object.Destroy(go);
                    if (!logged)
                    {
                        logged = true;
                        CyclonePlugin.Log($"Cyclone: the game's {name} is unavailable: {e.Message}");
                    }
                    return null;
                }
            }

            // Off with everything but the looks.
            private void Strip()
            {
                foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
                    Object.Destroy(script);
                foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(true))
                    Object.Destroy(collider);
                foreach (Rigidbody2D body in root.GetComponentsInChildren<Rigidbody2D>(true))
                    Object.Destroy(body);
            }

            // Particles loop and scale with the object; sound loops.
            private void Loop()
            {
                foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = system.main;
                    main.loop = true;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    // Some of the game's emitters are switched on by the scripts we removed: give
                    // those a steady stream of their own.
                    float rate = system.emission.rateOverTimeMultiplier;
                    if (rate < 0.01f)
                        rate = 20f;
                    ParticleSystem.EmissionModule emission = system.emission;
                    emission.enabled = true;
                    rates.Add(new KeyValuePair<ParticleSystem, float>(system, rate));
                    if (!system.isPlaying)
                        system.Play();
                }
                foreach (AudioSource audio in root.GetComponentsInChildren<AudioSource>(true))
                {
                    audio.loop = true;
                    if (!audio.isPlaying)
                        audio.Play();
                }
                foreach (Animator anim in root.GetComponentsInChildren<Animator>(true))
                    anim.enabled = true;
            }

            public void Set(Vector2 position, float size, float density)
            {
                if (root == null)
                    return;
                if (!root.activeSelf)
                    root.SetActive(true);
                root.transform.position = position;
                root.transform.localScale = new Vector3(size, size, 1f);
                foreach (KeyValuePair<ParticleSystem, float> entry in rates)
                {
                    if (entry.Key == null)
                        continue;
                    ParticleSystem.EmissionModule emission = entry.Key.emission;
                    emission.rateOverTime = new ParticleSystem.MinMaxCurve(entry.Value * density);
                }
            }

            public void Remove()
            {
                if (root != null)
                    Object.Destroy(root);
                root = null;
            }
        }
    }
}
