using System.Collections.Generic;
using UnityEngine;

namespace WoLCyclone
{
    // What Cyclone looks like, all from the game's own wind effects:
    // - the Twister arcana's little tornado, growing from tiny to big with the charge;
    // - the air boss's storm vortex (dust, debris and light streaks), fading in around it as it
    //   grows, for the hurricane at full power;
    // - the air vortex swirls (drawn by the state), which also stand in if these can't be made.
    //
    // Each is made from the game's prefab and stripped down to just its looks: every script,
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

        private Look twister;
        private Look storm;
        private bool stormTried;
        private static bool loggedTwister, loggedStorm;

        public CycloneLook(Vector2 position)
        {
            twister = Look.Make(() => Twister.Prefab, position, TwisterStart, "twister", ref loggedTwister);
        }

        public void Update(Vector2 position, float progress)
        {
            twister?.Set(position, Mathf.Lerp(TwisterStart, TwisterEnd, progress), 0.5f + progress);
            if (!stormTried && progress >= StormFrom)
            {
                stormTried = true;
                storm = Look.Make(() => AirBossVortex.Prefab, position, StormStart, "storm vortex", ref loggedStorm);
            }
            if (storm != null)
            {
                float t = Mathf.Clamp01((progress - StormFrom) / (1f - StormFrom));
                storm.Set(position, Mathf.Lerp(StormStart, StormEnd, t), 0.15f + 0.85f * t);
            }
        }

        public void Remove()
        {
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
